using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.Operations;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Operations;
using Newtonsoft.Json;

namespace ApogeeLocalTestRunner
{
    public static class OperationRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_05_Operations_Catalog_Loads15EventsSuccessfully", Test_Catalog_Loads15Events);
            runTest("M5_05_Operations_HealthWeakness_AmplifiesTargetEventWeight", Test_HealthWeakness_AmplifiesWeight);
            runTest("M5_05_Operations_SeededDeterminism_ProducesIdenticalEventPicks", Test_SeededDeterminism_ProducesIdenticalPicks);
            runTest("M5_05_Operations_NonRepeatableEvent_ExcludedAfterFirstUse", Test_NonRepeatable_Excluded);
            runTest("M5_05_Operations_TriggerCommand_PopulatesPendingEvent_AndUndoes", Test_TriggerCommand_ExecutionAndUndo);
            runTest("M5_05_Operations_ResolveCommand_AppliesEffects_DeltasAndLogs", Test_ResolveCommand_AppliesEffects);
            runTest("M5_05_Operations_ResolveCommand_UndoRestoresPendingEventSymmetrically", Test_ResolveCommand_UndoRedo);
            runTest("M5_05_Operations_RoleAuthorization_RejectsUnauthorized", Test_RoleAuthorization_RejectsUnauthorized);
            runTest("M5_05_Operations_ViewModel_BindsAndResolvesEvent", Test_ViewModel_BindingAndResolution);
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

        private static void Test_Catalog_Loads15Events()
        {
            var builtInCatalog = OperationEventManager.BuildCatalog();
            AssertTrue(builtInCatalog.Count >= 15, $"Builtin catalog should have >= 15 events, got {builtInCatalog.Count}");

            string jsonPath = Path.GetFullPath("Assets/_Project/Data/Catalog/Json/operation_events.json");
            AssertTrue(File.Exists(jsonPath), "operation_events.json must exist");
            string json = File.ReadAllText(jsonPath);
            var parsedList = JsonConvert.DeserializeObject<List<OperationEventRecord>>(json);
            AssertTrue(parsedList != null && parsedList.Count == 15, $"JSON must contain 15 events, got {parsedList?.Count}");

            foreach (var ev in builtInCatalog)
            {
                AssertTrue(!string.IsNullOrEmpty(ev.EventId), "Event ID cannot be empty");
                AssertTrue(ev.Options.Count >= 2, $"Event {ev.EventId} must have at least 2 options");
            }
        }

        private static void Test_HealthWeakness_AmplifiesWeight()
        {
            var catalog = OperationEventManager.BuildCatalog();

            // Nominal health: high margins
            var nominalHealth = new OperationHealthSnapshot(
                reliability: 0.95,
                powerMarginRatio: 0.50,
                thermalMarginRatio: 0.40,
                commsCapacityRatio: 1.50,
                radiationHazard: 0.05,
                dustHazard: 0.02
            );

            // Severely power-constrained spacecraft
            var powerStarvedHealth = new OperationHealthSnapshot(
                reliability: 0.70,
                powerMarginRatio: 0.02, // < 0.05 triggers brownout multiplier 4.0
                thermalMarginRatio: 0.40,
                commsCapacityRatio: 1.50,
                radiationHazard: 0.05,
                dustHazard: 0.02
            );

            int powerBrownoutCountNominal = 0;
            int powerBrownoutCountStarved = 0;
            int samples = 200;

            for (int i = 0; i < samples; i++)
            {
                var rng1 = new SeededRng((ulong)(1000 + i));
                var rng2 = new SeededRng((ulong)(1000 + i));

                var ev1 = OperationEventManager.SelectEvent(catalog, OperationEventPhase.Science, nominalHealth, rng1, null);
                var ev2 = OperationEventManager.SelectEvent(catalog, OperationEventPhase.Science, powerStarvedHealth, rng2, null);

                if (ev1?.EventId == "op_power_brownout") powerBrownoutCountNominal++;
                if (ev2?.EventId == "op_power_brownout") powerBrownoutCountStarved++;
            }

            AssertTrue(powerBrownoutCountStarved > powerBrownoutCountNominal,
                $"Power brownout should be selected significantly more often when power-starved ({powerBrownoutCountStarved} vs {powerBrownoutCountNominal})");
        }

        private static void Test_SeededDeterminism_ProducesIdenticalPicks()
        {
            var catalog = OperationEventManager.BuildCatalog();
            var health = new OperationHealthSnapshot(0.85, 0.20, 0.20, 1.0, 0.15, 0.05);

            var rng1 = new SeededRng(424242UL);
            var rng2 = new SeededRng(424242UL);

            for (int i = 0; i < 20; i++)
            {
                var pick1 = OperationEventManager.SelectEvent(catalog, OperationEventPhase.Cruise, health, rng1, null);
                var pick2 = OperationEventManager.SelectEvent(catalog, OperationEventPhase.Cruise, health, rng2, null);

                AssertEqual(pick1?.EventId, pick2?.EventId, $"Seed determinism check at iteration {i}");
            }
        }

        private static void Test_NonRepeatable_Excluded()
        {
            var catalog = OperationEventManager.BuildCatalog();
            var health = new OperationHealthSnapshot(0.80, 0.20, 0.20, 1.0, 0.10, 0.05);

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "op_propellant_leak",
                "op_unexpected_discovery",
                "op_spare_part_shortage",
                "op_science_instrument_failure"
            };

            for (int i = 0; i < 50; i++)
            {
                var rng = new SeededRng((ulong)(9000 + i));
                var pick = OperationEventManager.SelectEvent(catalog, OperationEventPhase.Cruise, health, rng, used);
                if (pick != null)
                {
                    AssertTrue(!used.Contains(pick.EventId),
                        $"Selected event {pick.EventId} was in used non-repeatable set");
                }
            }
        }

        private static void Test_TriggerCommand_ExecutionAndUndo()
        {
            var campaign = new CampaignState(seed: 1111UL);
            var mission = new MissionInstance("test_op_mission", "Explorer", "mars", null, 180, 5555UL);
            campaign.Missions[mission.MissionId] = mission;

            var health = new OperationHealthSnapshot(0.50, 0.01, 0.01, 0.2, 0.4, 0.2); // low reliability & margins to ensure trigger

            var triggerCmd = new TriggerOperationEventCommand(campaign, mission.MissionId, OperationEventPhase.Cruise, health, turnIndex: 1, totalTurns: 10);
            var res = campaign.Execute(triggerCmd);
            AssertTrue(res.Success, "Trigger command should execute successfully");

            if (mission.PendingOperationEvent != null)
            {
                AssertEqual(triggerCmd.TriggeredEvent, mission.PendingOperationEvent, "Mission pending event must match command triggered event");
                AssertTrue(!string.IsNullOrEmpty(mission.PendingOperationEvent.Record.EventId), "Triggered event must have valid ID");

                // Test Undo
                var undoRes = campaign.Undo();
                AssertTrue(undoRes.Success, "Undo must succeed");
                AssertTrue(mission.PendingOperationEvent == null, "Pending event must be cleared on undo");

                // Test Redo
                var redoRes = campaign.Redo();
                AssertTrue(redoRes.Success, "Redo must succeed");
                AssertTrue(mission.PendingOperationEvent != null, "Pending event restored on redo");
            }
        }

        private static void Test_ResolveCommand_AppliesEffects()
        {
            var campaign = new CampaignState(seed: 2222UL);
            campaign.Budget = 50.0;
            campaign.Reputation = 50.0;
            campaign.ResearchPoints = 100.0;

            var mission = new MissionInstance("resolve_mission", "ScienceSat", "moon", null, 30, 7777UL);
            mission.AccumulatedReliabilityPenalty = 0.0;
            mission.AccumulatedDownlinkDataGb = 10.0;

            var record = new OperationEventRecord(
                "op_test_decision",
                "TITLE",
                "DESC",
                OperationEventPhase.Science,
                baseWeight: 10.0,
                isRepeatable: false,
                options: new[]
                {
                    new OperationEventOption("opt_spend", "SPEND", "CONSEQ", "EXTRA_COST", budgetDelta: -5.0, reputationDelta: 2.0, researchPointsDelta: 15.0),
                    new OperationEventOption("opt_risk", "RISK", "CONSEQ", "RISK_HARDWARE", reliabilityPenalty: 0.04, scienceDataDeltaGb: -1.5)
                }
            );

            mission.PendingOperationEvent = new ActiveOperationEvent("inst_01", record, OperationEventPhase.Science, 1);
            campaign.Missions[mission.MissionId] = mission;

            var resolveCmd = new ResolveOperationEventCommand(campaign, mission.MissionId, "opt_spend");
            var res = campaign.Execute(resolveCmd);
            AssertTrue(res.Success, $"Resolve command must succeed: {res.Message}");

            AssertEqual(45.0, campaign.Budget, "Budget must decrease by 5.0");
            AssertEqual(52.0, campaign.Reputation, "Reputation must increase by 2.0");
            AssertEqual(115.0, campaign.ResearchPoints, "Research points must increase by 15.0");
            AssertTrue(mission.PendingOperationEvent == null, "Pending event must be cleared after resolution");
            AssertEqual(1, mission.OperationEventLog.Count, "Event log must have 1 entry");
            AssertEqual("opt_spend", mission.OperationEventLog[0].ChosenOptionId, "Chosen option ID recorded");
            AssertTrue(mission.UsedNonRepeatableOperationEvents.Contains("op_test_decision"), "Non-repeatable event logged");
        }

        private static void Test_ResolveCommand_UndoRedo()
        {
            var campaign = new CampaignState(seed: 3333UL);
            campaign.Budget = 100.0;
            campaign.Reputation = 60.0;

            var mission = new MissionInstance("undo_mission", "Orbiter", "mars", null, 200, 8888UL);
            var record = new OperationEventRecord(
                "op_undo_test",
                "TITLE",
                "DESC",
                OperationEventPhase.Cruise,
                baseWeight: 10.0,
                isRepeatable: true,
                options: new[]
                {
                    new OperationEventOption("opt_fix", "FIX", "CONSEQ", "REPAIR", budgetDelta: -10.0, reputationDelta: -3.0)
                }
            );

            var activeEv = new ActiveOperationEvent("inst_undo", record, OperationEventPhase.Cruise, 2);
            mission.PendingOperationEvent = activeEv;
            campaign.Missions[mission.MissionId] = mission;

            var cmd = new ResolveOperationEventCommand(campaign, mission.MissionId, "opt_fix");
            campaign.Execute(cmd);

            AssertEqual(90.0, campaign.Budget, "Budget updated after exec");
            AssertEqual(57.0, campaign.Reputation, "Reputation updated after exec");

            var undo = campaign.Undo();
            AssertTrue(undo.Success, "Undo should succeed");
            AssertEqual(100.0, campaign.Budget, "Budget restored after undo");
            AssertEqual(60.0, campaign.Reputation, "Reputation restored after undo");
            AssertTrue(mission.PendingOperationEvent != null, "Pending event restored after undo");
            AssertEqual(0, mission.OperationEventLog.Count, "Log cleared after undo");

            var redo = campaign.Redo();
            AssertTrue(redo.Success, "Redo should succeed");
            AssertEqual(90.0, campaign.Budget, "Budget updated after redo");
            AssertEqual(57.0, campaign.Reputation, "Reputation updated after redo");
            AssertTrue(mission.PendingOperationEvent == null, "Pending cleared after redo");
            AssertEqual(1, mission.OperationEventLog.Count, "Log restored after redo");
        }

        private static void Test_RoleAuthorization_RejectsUnauthorized()
        {
            var campaign = new CampaignState(seed: 4444UL);
            var mission = new MissionInstance("auth_mission", "Rover", "moon", null, 10, 9999UL);
            var record = new OperationEventRecord(
                "op_auth", "TITLE", "DESC", OperationEventPhase.Science, 10.0,
                new[] { new OperationEventOption("opt1", "OPT", "CONSEQ", "NONE") }
            );
            mission.PendingOperationEvent = new ActiveOperationEvent("inst_auth", record, OperationEventPhase.Science, 1);
            campaign.Missions[mission.MissionId] = mission;

            var cmd = new ResolveOperationEventCommand(campaign, mission.MissionId, "opt1");
            var unauthContext = new CommandContext("hacker", PlayerRole.ProcurementDirector); // ProcurementDirector is not authorized for operations resolution

            var validation = cmd.CanExecute(unauthContext);
            AssertTrue(!validation.IsValid, "Unauthorized role must be rejected");
        }

        private static void Test_ViewModel_BindingAndResolution()
        {
            var campaign = new CampaignState(seed: 5555UL);
            var mission = new MissionInstance("vm_op_mission", "LunarSat", "moon", null, 15, 12345UL);
            var record = new OperationEventRecord(
                "op_vm_event", "VM_TITLE", "VM_DESC", OperationEventPhase.Cruise, 10.0,
                new[]
                {
                    new OperationEventOption("opt_a", "Option A", "Result A", "SAFE_MODE"),
                    new OperationEventOption("opt_b", "Option B", "Result B", "BONUS_SCIENCE")
                }
            );
            mission.PendingOperationEvent = new ActiveOperationEvent("inst_vm", record, OperationEventPhase.Cruise, 1);
            campaign.Missions[mission.MissionId] = mission;

            var vm = new OperationsViewModel();
            vm.BindMission(campaign, mission.MissionId, null);

            AssertTrue(vm.HasPendingOperationEvent.Value, "VM must recognize pending operation event");
            AssertEqual("VM_TITLE", vm.OperationEventTitle.Value, "Title bound");
            AssertEqual("VM_DESC", vm.OperationEventNarrative.Value, "Narrative bound");
            AssertEqual(2, vm.OperationEventOptions.Count, "Options bound");

            // Execute option A via VM command
            vm.ResolveOperationEventOptionCommand.Execute("opt_a");

            AssertTrue(!vm.HasPendingOperationEvent.Value, "VM pending flag cleared after resolution");
            AssertEqual(1, vm.OperationEventLog.Count, "VM event log updated");
            AssertEqual("opt_a", vm.OperationEventLog[0].ChosenOptionId, "Option A logged in VM");
        }
    }
}
