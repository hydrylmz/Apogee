using System;
using System.IO;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.UI.Screens.Events;

namespace ApogeeLocalTestRunner
{
    public static class EventRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M4_09_Catalog_CampaignEvents_LoadsSuccessfully", Test_CatalogLoads);
            runTest("M4_09_EventEvaluator_YearAndReputationConditions_Enforced", Test_ConditionEvaluation);
            runTest("M4_09_EventEvaluator_SupplierTendencyWeightModifier_Applied", Test_WeightModifier);
            runTest("M4_09_CampaignEventManager_AdvanceDays_TriggersEligibleEvent_Deterministically", Test_AdvanceDaysTriggers);
            runTest("M4_09_ResolveEventOptionCommand_ExecutesAndAppliesDeltas", Test_ResolveCommandExecution);
            runTest("M4_09_ResolveEventOptionCommand_UndoRestoresStateSymmetrically", Test_ResolveCommandUndo);
            runTest("M4_09_CampaignEventManager_NonRepeatableEvent_NotTriggeredTwice", Test_NonRepeatableCooldown);
            runTest("M4_09_EventPopupViewModel_BindsAndExecutesOption", Test_ViewModelIntegration);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new Exception($"Assertion Failed: {message}. Expected: {expected}, Actual: {actual}");
        }

        private static CatalogCollection<CampaignEventRecord> LoadEventsCatalog()
        {
            string path = Path.GetFullPath("Assets/_Project/Data/Catalog/Json/campaign_events.json");
            string json = File.ReadAllText(path);
            return CatalogLoader.Load<CampaignEventRecord>(json);
        }

        private static void Test_CatalogLoads()
        {
            var catalog = LoadEventsCatalog();
            AssertTrue(catalog.Validation.IsValid, "Catalog should be valid");
            AssertTrue(catalog.Count >= 15, $"Expected at least 15 events, got {catalog.Count}");

            foreach (var evt in catalog.All)
            {
                AssertTrue(!string.IsNullOrEmpty(evt.Id), "Event id must not be empty");
                AssertTrue(!string.IsNullOrEmpty(evt.TitleKey), "Event title must not be empty");
                AssertTrue(evt.Options.Count >= 2, $"Event {evt.Id} should have at least 2 options");
            }
        }

        private static void Test_ConditionEvaluation()
        {
            var catalog = LoadEventsCatalog();
            var summitEvt = catalog.GetById("event_geopolitical_summit");
            var campaign = new CampaignState(seed: 42); // starts in year 2031

            // Summit requires minYear: 2032
            bool eligible2031 = EventEvaluator.IsEventEligible(summitEvt, campaign, null, null);
            AssertEqual(false, eligible2031, "Summit should be ineligible in 2031");

            // Advance to 2032
            campaign.Time.AdvanceDays(366);
            bool eligible2032 = EventEvaluator.IsEventEligible(summitEvt, campaign, null, null);
            AssertEqual(true, eligible2032, "Summit should be eligible in 2032");
        }

        private static void Test_WeightModifier()
        {
            var catalog = LoadEventsCatalog();
            var delayEvt = catalog.GetById("event_supplier_delay");
            var campaign = new CampaignState(seed: 42);

            string supplierJson = File.ReadAllText(Path.GetFullPath("Assets/_Project/Data/Catalog/Json/suppliers.json"));
            var supplierCatalog = CatalogLoader.Load<SupplierRecord>(supplierJson);
            campaign.InitialiseSuppliers(supplierCatalog.All);

            // Helion has eventWeightModifier for event_supplier_delay = 1.3
            double baseWeight = delayEvt.BaseWeight;
            double effectiveWeight = EventEvaluator.CalculateEffectiveWeight(delayEvt, campaign);

            AssertTrue(effectiveWeight >= baseWeight * 1.29, $"Effective weight should be scaled by supplier modifier. Base: {baseWeight}, Eff: {effectiveWeight}");
        }

        private static void Test_AdvanceDaysTriggers()
        {
            var catalog = LoadEventsCatalog();
            var campaign1 = new CampaignState(seed: 9999);
            campaign1.InitialiseEvents(catalog.All);
            campaign1.Events.CheckIntervalDays = 10;
            campaign1.Events.TriggerChanceOnCheck = 1.0; // Guaranteed on check

            var cmd1 = new AdvanceTimeCommand(10);
            campaign1.Execute(cmd1);

            AssertTrue(campaign1.Events.PendingEvent != null, "Pending event should have triggered");
            string eventId1 = campaign1.Events.PendingEvent.EventRecord.Id;

            // Determinism check with campaign2 using identical seed
            var campaign2 = new CampaignState(seed: 9999);
            campaign2.InitialiseEvents(catalog.All);
            campaign2.Events.CheckIntervalDays = 10;
            campaign2.Events.TriggerChanceOnCheck = 1.0;

            var cmd2 = new AdvanceTimeCommand(10);
            campaign2.Execute(cmd2);

            AssertTrue(campaign2.Events.PendingEvent != null, "Campaign2 pending event triggered");
            AssertEqual(eventId1, campaign2.Events.PendingEvent.EventRecord.Id, "Deterministik RNG produces identical event selection");
        }

        private static void Test_ResolveCommandExecution()
        {
            var catalog = LoadEventsCatalog();
            var campaign = new CampaignState(seed: 12345);
            campaign.InitialiseEvents(catalog.All);

            string supplierJson = File.ReadAllText(Path.GetFullPath("Assets/_Project/Data/Catalog/Json/suppliers.json"));
            var supplierCatalog = CatalogLoader.Load<SupplierRecord>(supplierJson);
            campaign.InitialiseSuppliers(supplierCatalog.All);

            // Trigger grant event directly
            campaign.Events.TriggerEventDirectly("event_budget_bonus_grant", campaign);
            AssertTrue(campaign.Events.PendingEvent != null, "Grant event pending");

            double startBudget = campaign.Budget;
            double startRep = campaign.Reputation;

            // Option 1: +40 M$, +3 Rep
            var resolveCmd = new ResolveEventOptionCommand("event_budget_bonus_grant", "opt_accept_grant");
            var result = campaign.Execute(resolveCmd);

            AssertTrue(result.Success, "Command executed successfully");
            AssertEqual(startBudget + 40.0, campaign.Budget, "Budget increased by 40 M$");
            AssertEqual(startRep + 3.0, campaign.Reputation, "Reputation increased by 3");
            AssertTrue(campaign.Events.PendingEvent == null, "Pending event cleared after resolution");
            AssertEqual(1, campaign.Events.History.Count, "1 event in history");
        }

        private static void Test_ResolveCommandUndo()
        {
            var catalog = LoadEventsCatalog();
            var campaign = new CampaignState(seed: 12345);
            campaign.InitialiseEvents(catalog.All);

            campaign.Events.TriggerEventDirectly("event_budget_bonus_grant", campaign);
            double startBudget = campaign.Budget;
            double startRep = campaign.Reputation;

            var resolveCmd = new ResolveEventOptionCommand("event_budget_bonus_grant", "opt_accept_grant");
            campaign.Execute(resolveCmd);

            // Undo
            var undoResult = campaign.Undo();
            AssertTrue(undoResult.Success, "Undo succeeded");
            AssertEqual(startBudget, campaign.Budget, "Budget restored");
            AssertEqual(startRep, campaign.Reputation, "Reputation restored");
            AssertTrue(campaign.Events.PendingEvent != null, "Pending event restored on undo");
            AssertEqual(0, campaign.Events.History.Count, "History entry reverted");
        }

        private static void Test_NonRepeatableCooldown()
        {
            var catalog = LoadEventsCatalog();
            var campaign = new CampaignState(seed: 12345);
            campaign.InitialiseEvents(catalog.All);

            var kestrelEvt = catalog.GetById("event_kestrel_partnership_offer");
            AssertEqual(false, kestrelEvt.IsRepeatable, "Kestrel event is non-repeatable");

            campaign.Events.TriggerEventDirectly("event_kestrel_partnership_offer", campaign);
            var resolveCmd = new ResolveEventOptionCommand("event_kestrel_partnership_offer", "opt_decline_partnership");
            campaign.Execute(resolveCmd);

            AssertTrue(campaign.Events.CompletedNonRepeatableEvents.Contains("event_kestrel_partnership_offer"), "Recorded as completed");

            // Check eligibility after completion
            bool eligible = EventEvaluator.IsEventEligible(
                kestrelEvt,
                campaign,
                campaign.Events.Cooldowns,
                campaign.Events.CompletedNonRepeatableEvents);

            AssertEqual(false, eligible, "Completed non-repeatable event should not be eligible again");
        }

        private static void Test_ViewModelIntegration()
        {
            var catalog = LoadEventsCatalog();
            var campaign = new CampaignState(seed: 12345);
            campaign.InitialiseEvents(catalog.All);

            campaign.Events.TriggerEventDirectly("event_budget_bonus_grant", campaign);

            var vm = new EventPopupViewModel();
            vm.Bind(campaign);

            AssertTrue(vm.HasPendingEvent.Value, "VM reflects pending event");
            AssertEqual("EVENT_BUDGET_BONUS_TITLE", vm.EventTitle.Value, "Title key matches");
            AssertEqual(2, vm.Options.Count, "2 options loaded in VM");

            // Trigger selection via option command
            vm.Options[0].SelectCommand.Execute();

            AssertEqual(false, vm.HasPendingEvent.Value, "VM reflects event resolved");
            AssertTrue(campaign.Budget > 500.0, "Budget modified via VM action");
        }
    }
}
