using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Design;
using Apogee.Strategy.Campaign;

namespace Apogee.Strategy
{
    /// <summary>
    /// The single source of truth for a campaign session.
    /// All mutations occur via the CommandBus; this object is only read from non-command code.
    /// Implements GDD §19.13 (CampaignState schema) — fields added incrementally per milestone.
    ///
    /// M4-01 adds: identity, time system, budget, reputation, research points.
    /// M4-03 adds: bases.
    /// M4-05 adds: research tree manager (ResearchManager).
    /// M4-06 adds: supplier manager and contracts (SupplierManager), active blueprint holder.
    /// M4-08 adds: agency economy manager (EconomyManager).
    /// </summary>
    public sealed class CampaignState : IBlueprintHolder
    {
        // ---------------------------------------------------------------- identity
        /// <summary>Unique identifier for this campaign (used in save files).</summary>
        public string CampaignId { get; }

        /// <summary>Schema version — bumped when save format changes to enable migration.</summary>
        public int SchemaVersion { get; private set; } = 1;

        // ---------------------------------------------------------------- active vehicle design (IBlueprintHolder)
        /// <summary>Currently active blueprint being developed/procured in the campaign.</summary>
        public Blueprint ActiveBlueprint { get; set; }

        // ---------------------------------------------------------------- time
        /// <summary>The campaign clock (date, tick, speed).</summary>
        public CampaignTime Time { get; }

        // ---------------------------------------------------------------- resources (GDD §4.3, §6.4)
        /// <summary>Current budget in million USD (M$). Modified by annual allocation, missions, and maintenance.</summary>
        public double Budget { get; set; }

        /// <summary>
        /// Agency reputation score [0–100]. Affects annual budget multiplier and contractor terms (§4.3).
        /// </summary>
        public double Reputation { get; set; }

        /// <summary>Accumulated research points available to unlock tech tree nodes (§4.3, §9.2).</summary>
        public double ResearchPoints { get; set; }

        /// <summary>Knowledge points gained from completed missions — unlock experience bonuses (§4.3).</summary>
        public double KnowledgePoints { get; set; }

        // ---------------------------------------------------------------- economy (GDD §4.3, §6.4, §8.1, M4-08)
        /// <summary>
        /// Financial manager tracking annual allocations, monthly maintenance, and transaction ledger.
        /// </summary>
        public EconomyManager Economy { get; private set; }

        // ---------------------------------------------------------------- map & strategy (GDD §6.1, M4-02 & M4-03)
        /// <summary>Active strategic lens applied to the World Map.</summary>
        public MapLens ActiveMapLens { get; set; } = MapLens.Overview;

        /// <summary>Currently selected region ID on the Strategy Map.</summary>
        public string SelectedRegionId { get; set; }

        /// <summary>
        /// Collection of active agency bases across world regions.
        /// Key = base facility ID.
        /// </summary>
        public Dictionary<string, BaseFacility> Bases { get; } = new Dictionary<string, BaseFacility>(StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- research (GDD §9.2, M4-05)
        /// <summary>
        /// Research tree runtime manager.
        /// Null until initialised by the game session (requires catalog data).
        /// Call <see cref="InitialiseResearch"/> once catalog is loaded.
        /// </summary>
        public ResearchManager Research { get; private set; }

        // ---------------------------------------------------------------- suppliers & contracts (GDD §9.1, M4-06)
        /// <summary>
        /// Supplier relationships, procurement contracts, and delivery schedules.
        /// Call <see cref="InitialiseSuppliers"/> once catalog is loaded.
        /// </summary>
        public SupplierManager Suppliers { get; private set; }

        // ---------------------------------------------------------------- production & testing (GDD §9.3, M4-07)
        /// <summary>
        /// Agency manufacturing queue, integration assembly, and qualification testing pipeline.
        /// </summary>
        public ProductionManager Production { get; private set; }

        // ---------------------------------------------------------------- events (GDD §6.3, §10.2, §10.3, M4-09)
        /// <summary>
        /// Campaign event director managing triggered decision cards, cooldowns, and history.
        /// </summary>
        public CampaignEventManager Events { get; private set; }

        // ---------------------------------------------------------------- missions (GDD §19.12, M5-02)
        /// <summary>
        /// All mission instances in this campaign keyed by MissionId.
        /// Includes planning, in-flight, and completed missions.
        /// </summary>
        public Dictionary<string, Apogee.Strategy.Mission.MissionInstance> Missions { get; } =
            new Dictionary<string, Apogee.Strategy.Mission.MissionInstance>(StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- milestones (GDD §4.4, §19.13, M6-01)
        /// <summary>
        /// Agency milestone and quest board manager.
        /// </summary>
        public Apogee.Strategy.Milestones.MilestoneManager Milestones { get; private set; }

        // ---------------------------------------------------------------- campaign lifecycle (GDD §4.1, §4.5, M6-02)
        /// <summary>Agency display name shown in reports and UI. GDD §4.1.</summary>
        public string AgencyName { get; set; } = "Birleşik Yörünge Ajansı";

        /// <summary>
        /// Current lifecycle status. Active until an end condition is triggered.
        /// Only EndCampaignCommand may set this to a non-Active value.
        /// </summary>
        public CampaignEndStatus EndStatus { get; set; } = CampaignEndStatus.Active;

        /// <summary>
        /// Final legacy report populated by EndCampaignCommand. Null while the campaign is active.
        /// </summary>
        public CampaignEndReport EndReport { get; set; }

        // ---------------------------------------------------------------- RNG
        /// <summary>Root seed for campaign determinism (§8.4, §17.3 item 4).</summary>
        public ulong Seed { get; }

        // ---------------------------------------------------------------- command bus
        /// <summary>
        /// The command bus for this campaign. All state mutations route through here.
        /// </summary>
        public CommandBus CommandBus { get; }

        // ---------------------------------------------------------------- construction
        /// <summary>Creates a fresh campaign starting at 2031-01-01 with default resources.</summary>
        public CampaignState(ulong seed = 0, string campaignId = null)
        {
            CampaignId    = string.IsNullOrWhiteSpace(campaignId) ? NewId() : campaignId;
            Seed          = seed;
            Time          = new CampaignTime();
            CommandBus    = new CommandBus(maxHistorySize: 200);

            // Starting resources (GDD §4.2 — "low budget, basic tech")
            Budget         = 500.0;   // 500 M$ starting budget  (TODO-VERİ: balance pass §16.8)
            Reputation     = 30.0;    // 0-100 scale, starts low
            ResearchPoints = 0.0;
            KnowledgePoints = 0.0;

            // Starting infrastructure (GDD §4.2: "Bir Fırlatma Rampası (küçük sınıf), bir Fabrika, bir küçük Araştırma Merkezi")
            var defaultPad = new BaseFacility("base_cape_pad", "cape_peninsula", BaseType.LaunchPad, level: 1);
            var defaultFactory = new BaseFacility("base_cape_factory", "cape_peninsula", BaseType.Factory, level: 1);
            var defaultResearch = new BaseFacility("base_cape_research", "cape_peninsula", BaseType.ResearchCenter, level: 1);

            Bases[defaultPad.Id] = defaultPad;
            Bases[defaultFactory.Id] = defaultFactory;
            Bases[defaultResearch.Id] = defaultResearch;

            // Production pipeline
            Production = new ProductionManager();

            // Economy manager (GDD §4.3, M4-08)
            Economy = new EconomyManager(initialYear: Time.CurrentDate.Year, initialMonth: Time.CurrentDate.Month);

            // Events manager (GDD §6.3, §10.3, M4-09)
            Events = new CampaignEventManager(seed: seed ^ 0xE7E475u);
        }

        // ---------------------------------------------------------------- research initialisation
        /// <summary>
        /// Initialises the research tree from a set of node records.
        /// Call once after catalog data is loaded (e.g. from GameSession on startup).
        /// Idempotent — subsequent calls replace the existing manager.
        /// </summary>
        public void InitialiseResearch(System.Collections.Generic.IEnumerable<Apogee.Data.ResearchNodeRecord> nodes)
        {
            Research = new ResearchManager(nodes);
        }

        // ---------------------------------------------------------------- supplier initialisation (M4-06)
        /// <summary>
        /// Initialises supplier companies and procurement contracts from supplier catalog records.
        /// Call once after catalog data is loaded.
        /// </summary>
        public void InitialiseSuppliers(System.Collections.Generic.IEnumerable<Apogee.Data.SupplierRecord> records)
        {
            Suppliers = new SupplierManager(records);
        }

        // ---------------------------------------------------------------- event initialisation (M4-09)
        /// <summary>
        /// Initialises or updates campaign events from catalog records.
        /// </summary>
        public void InitialiseEvents(System.Collections.Generic.IEnumerable<Apogee.Data.CampaignEventRecord> records)
        {
            if (Events == null)
            {
                Events = new CampaignEventManager(records, seed: Seed ^ 0xE7E475u);
            }
            else
            {
                Events.LoadEvents(records);
            }
        }

        // ---------------------------------------------------------------- milestone initialisation (M6-01)
        /// <summary>
        /// Initialises agency campaign milestones from catalog records.
        /// </summary>
        public void InitialiseMilestones(System.Collections.Generic.IEnumerable<Apogee.Data.MilestoneRecord> records)
        {
            Milestones = new Apogee.Strategy.Milestones.MilestoneManager(records);
        }

        // ---------------------------------------------------------------- helpers
        private static string NewId()
        {
            // Engine-free, time-independent unique ID using a simple counter.
            return $"CAM-{System.Threading.Interlocked.Increment(ref _idCounter):D6}";
        }

        private static int _idCounter = 0;

        // ---------------------------------------------------------------- convenience execution
        /// <summary>
        /// Executes a command through the campaign's own bus with an admin context at the current tick.
        /// Useful for single-player and testing.
        /// </summary>
        public CommandExecutionResult Execute(ICommand command, PlayerRole role = PlayerRole.All, string playerId = "local")
        {
            var ctx = new CommandContext(playerId, role, Time.Tick, this);
            return CommandBus.Execute(command, ctx);
        }

        /// <summary>
        /// Undoes the last command on the campaign bus using the current context.
        /// </summary>
        public CommandExecutionResult Undo(PlayerRole role = PlayerRole.All, string playerId = "local")
        {
            var ctx = new CommandContext(playerId, role, Time.Tick, this);
            return CommandBus.Undo(ctx);
        }

        /// <summary>
        /// Redoes the last undone command on the campaign bus using the current context.
        /// </summary>
        public CommandExecutionResult Redo(PlayerRole role = PlayerRole.All, string playerId = "local")
        {
            var ctx = new CommandContext(playerId, role, Time.Tick, this);
            return CommandBus.Redo(ctx);
        }

        public override string ToString() =>
            $"CampaignState[Id={CampaignId}, {Time}, Budget={Budget:F0}M$, Rep={Reputation:F0}]";
    }
}
