using System;
using System.Collections.Generic;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// The single source of truth for a campaign session.
    /// All mutations occur via the CommandBus; this object is only read from non-command code.
    /// Implements GDD §19.13 (CampaignState schema) — fields added incrementally per milestone.
    ///
    /// M4-01 adds: identity, time system, budget, reputation, research points.
    /// Future milestones add bases, missions, contracts, research tree, etc.
    /// </summary>
    public sealed class CampaignState
    {
        // ---------------------------------------------------------------- identity
        /// <summary>Unique identifier for this campaign (used in save files).</summary>
        public string CampaignId { get; }

        /// <summary>Schema version — bumped when save format changes to enable migration.</summary>
        public int SchemaVersion { get; private set; } = 1;

        // ---------------------------------------------------------------- time
        /// <summary>The campaign clock (date, tick, speed).</summary>
        public CampaignTime Time { get; }

        // ---------------------------------------------------------------- resources (GDD §4.3, §6.4)
        /// <summary>Current budget in million USD (M$). Modified by annual allocation, missions, and maintenance.</summary>
        public double Budget { get; internal set; }

        /// <summary>
        /// Agency reputation score [0–100]. Affects annual budget multiplier and contractor terms (§4.3).
        /// </summary>
        public double Reputation { get; internal set; }

        /// <summary>Accumulated research points available to unlock tech tree nodes (§4.3, §9.2).</summary>
        public double ResearchPoints { get; internal set; }

        /// <summary>Knowledge points gained from completed missions — unlock experience bonuses (§4.3).</summary>
        public double KnowledgePoints { get; internal set; }

        // ---------------------------------------------------------------- map & strategy (GDD §6.1, M4-02)
        /// <summary>Active strategic lens applied to the World Map.</summary>
        public MapLens ActiveMapLens { get; internal set; } = MapLens.Overview;

        /// <summary>Currently selected region ID on the Strategy Map.</summary>
        public string SelectedRegionId { get; internal set; }

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

        public override string ToString() =>
            $"CampaignState[Id={CampaignId}, {Time}, Budget={Budget:F0}M$, Rep={Reputation:F0}]";
    }
}
