using System;
using System.IO;
using Apogee.Data;

namespace Apogee.Strategy.Campaign
{
    /// <summary>
    /// Factory for creating and bootstrapping fully-configured <see cref="CampaignState"/> instances.
    /// Loads and wires all catalog-backed subsystems (Research, Suppliers, Events, Milestones).
    /// Pure C# — no Unity engine dependencies. GDD §4.1, §4.2, §21 M9-02.
    /// </summary>
    public static class CampaignFactory
    {
        /// <summary>
        /// Creates a new campaign state with optional custom setup args and catalog data.
        /// </summary>
        public static CampaignState CreateDefault(
            CampaignSetupArgs args = null,
            string catalogDirectory = null)
        {
            args ??= new CampaignSetupArgs();

            var campaign = new CampaignState(args.Seed, args.CampaignId)
            {
                AgencyName = string.IsNullOrWhiteSpace(args.AgencyName) ? "Birleşik Yörünge Ajansı" : args.AgencyName,
                Budget = args.StartingBudget > 0 ? args.StartingBudget : 500.0,
                Reputation = args.StartingReputation >= 0 ? args.StartingReputation : 30.0
            };

            if (!string.IsNullOrEmpty(args.StartingRegionId))
            {
                campaign.SelectedRegionId = args.StartingRegionId;
            }

            if (!string.IsNullOrEmpty(catalogDirectory) && Directory.Exists(catalogDirectory))
            {
                LoadCatalogsFromDirectory(campaign, catalogDirectory);
            }

            return campaign;
        }

        /// <summary>
        /// Loads catalog data from raw JSON strings into the respective subsystem managers.
        /// </summary>
        public static void LoadCatalogsFromJson(
            CampaignState campaign,
            string researchJson = null,
            string suppliersJson = null,
            string eventsJson = null,
            string milestonesJson = null)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            if (!string.IsNullOrWhiteSpace(researchJson))
            {
                var cat = CatalogLoader.Load<ResearchNodeRecord>(researchJson);
                if (cat != null && cat.All != null)
                {
                    campaign.InitialiseResearch(cat.All);
                }
            }

            if (!string.IsNullOrWhiteSpace(suppliersJson))
            {
                var cat = CatalogLoader.Load<SupplierRecord>(suppliersJson);
                if (cat != null && cat.All != null)
                {
                    campaign.InitialiseSuppliers(cat.All);
                }
            }

            if (!string.IsNullOrWhiteSpace(eventsJson))
            {
                var cat = CatalogLoader.Load<CampaignEventRecord>(eventsJson);
                if (cat != null && cat.All != null)
                {
                    campaign.InitialiseEvents(cat.All);
                }
            }

            if (!string.IsNullOrWhiteSpace(milestonesJson))
            {
                var cat = CatalogLoader.Load<MilestoneRecord>(milestonesJson);
                if (cat != null && cat.All != null)
                {
                    campaign.InitialiseMilestones(cat.All);
                }
            }
        }

        /// <summary>
        /// Loads all standard catalogs from a disk directory containing the json files.
        /// </summary>
        public static void LoadCatalogsFromDirectory(CampaignState campaign, string catalogDir)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (string.IsNullOrEmpty(catalogDir) || !Directory.Exists(catalogDir)) return;

            string researchPath = Path.Combine(catalogDir, "research_tree.json");
            string suppliersPath = Path.Combine(catalogDir, "suppliers.json");
            string eventsPath = Path.Combine(catalogDir, "campaign_events.json");
            string milestonesPath = Path.Combine(catalogDir, "milestones.json");

            string researchJson = File.Exists(researchPath) ? File.ReadAllText(researchPath) : null;
            string suppliersJson = File.Exists(suppliersPath) ? File.ReadAllText(suppliersPath) : null;
            string eventsJson = File.Exists(eventsPath) ? File.ReadAllText(eventsPath) : null;
            string milestonesJson = File.Exists(milestonesPath) ? File.ReadAllText(milestonesPath) : null;

            LoadCatalogsFromJson(campaign, researchJson, suppliersJson, eventsJson, milestonesJson);
        }
    }
}
