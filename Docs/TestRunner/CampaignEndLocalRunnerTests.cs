using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Core.Simulation.Report;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Milestones;
using Apogee.Strategy.Mission;
using Apogee.UI.Campaign;

namespace ApogeeLocalTestRunner
{
    public static class CampaignEndLocalRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M6_02_CampaignEnd_ActiveCampaign_NoEndConditionTriggered",    Test_Active_NoEnd);
            runTest("M6_02_CampaignEnd_BudgetZero_ReputationZero_Bankruptcy",      Test_Bankruptcy);
            runTest("M6_02_CampaignEnd_TimeExpired_EndCondition",                  Test_TimeExpired);
            runTest("M6_02_CampaignEnd_AllMilestonesClaimed_Victory",              Test_AllMilestones_Victory);
            runTest("M6_02_CampaignEnd_EndCommand_UpdatesStateAndBuildsReport",    Test_EndCommand_UpdatesState);
            runTest("M6_02_CampaignEnd_EndCommand_Undo_RestoresActiveStatus",      Test_EndCommand_Undo);
            runTest("M6_02_CampaignEnd_ViewModel_BindsReportData",                 Test_ViewModel_Binds);
            runTest("M6_02_CampaignEnd_AdvanceTime_AutoTriggersTimeExpiredEnd",    Test_AdvanceTime_AutoEnd);
        }

        // ---------------------------------------------------------------- assertions
        private static void AssertTrue(bool cond, string msg)
        {
            if (!cond) throw new Exception("Assertion Failed: " + msg);
        }
        private static void AssertEqual<T>(T expected, T actual, string msg)
        {
            if (!object.Equals(expected, actual))
                throw new Exception($"Assertion Failed: {msg}. Expected: '{expected}', Actual: '{actual}'");
        }

        // ---------------------------------------------------------------- helpers
        private static CampaignState MakeCampaign() => new CampaignState(seed: 42u, campaignId: "TEST-01");

        private static IEnumerable<MilestoneRecord> MakeThreeMilestones()
        {
            return new[]
            {
                new MilestoneRecord
                {
                    Id = "ms_a", Title = "Alpha", Description = "A",
                    TargetMissionTypeId = "robotic",
                    TargetDestinationId = null, IsCrewed = false,
                    RewardBudget = 0, RewardReputation = 0, RewardKnowledgePoints = 0,
                    RequiredMilestoneIds = null, OrderIndex = 1
                },
                new MilestoneRecord
                {
                    Id = "ms_b", Title = "Beta", Description = "B",
                    TargetMissionTypeId = "robotic",
                    TargetDestinationId = null, IsCrewed = false,
                    RewardBudget = 0, RewardReputation = 0, RewardKnowledgePoints = 0,
                    RequiredMilestoneIds = null, OrderIndex = 2
                },
                new MilestoneRecord
                {
                    Id = "ms_c", Title = "Gamma", Description = "C",
                    TargetMissionTypeId = "robotic",
                    TargetDestinationId = null, IsCrewed = false,
                    RewardBudget = 0, RewardReputation = 0, RewardKnowledgePoints = 0,
                    RequiredMilestoneIds = null, OrderIndex = 3
                }
            };
        }

        private static void ForceClaimAll(MilestoneManager mgr)
        {
            foreach (var p in mgr.AllMilestones.Values)
                p.Status = MilestoneStatus.Claimed;
        }

        // ---------------------------------------------------------------- TEST 1 — active, no condition
        private static void Test_Active_NoEnd()
        {
            var state = MakeCampaign();
            // Default: Budget=500, Rep=30, not expired → Active
            CampaignEndStatus result = CampaignEndEvaluator.EvaluateEndCondition(state);
            AssertEqual(CampaignEndStatus.Active, result, "Fresh campaign should be Active");
        }

        // ---------------------------------------------------------------- TEST 2 — bankruptcy
        private static void Test_Bankruptcy()
        {
            var state = MakeCampaign();
            state.Budget     = 0.0;
            state.Reputation = 0.0;
            CampaignEndStatus result = CampaignEndEvaluator.EvaluateEndCondition(state);
            AssertEqual(CampaignEndStatus.Bankruptcy, result, "Zero budget+rep → Bankruptcy");
        }

        // ---------------------------------------------------------------- TEST 3 — time expired
        private static void Test_TimeExpired()
        {
            var state = MakeCampaign();
            // Advance past 2061-01-01 (30 years ≈ 10957 days; advance 10958 to clear the boundary)
            state.Execute(new AdvanceTimeCommand(10958));
            AssertTrue(state.Time.IsExpired, "Time should be expired after 10958 days");
            CampaignEndStatus result = CampaignEndEvaluator.EvaluateEndCondition(state);
            AssertEqual(CampaignEndStatus.TimeExpired, result, "Expired time → TimeExpired");
        }

        // ---------------------------------------------------------------- TEST 4 — all milestones claimed
        private static void Test_AllMilestones_Victory()
        {
            var state = MakeCampaign();
            state.InitialiseMilestones(MakeThreeMilestones());

            // Before claiming — not all claimed
            AssertEqual(CampaignEndStatus.Active,
                CampaignEndEvaluator.EvaluateEndCondition(state),
                "Before claiming milestones should be Active");

            // Force-claim all milestones
            ForceClaimAll(state.Milestones);

            CampaignEndStatus result = CampaignEndEvaluator.EvaluateEndCondition(state);
            AssertEqual(CampaignEndStatus.AllMilestonesCompleted, result,
                "All milestones claimed → AllMilestonesCompleted");
        }

        // ---------------------------------------------------------------- TEST 5 — EndCampaignCommand updates state
        private static void Test_EndCommand_UpdatesState()
        {
            var state = MakeCampaign();
            AssertEqual(CampaignEndStatus.Active, state.EndStatus, "Initial status should be Active");
            AssertTrue(state.EndReport == null, "Initial EndReport should be null");

            var cmd = new EndCampaignCommand(CampaignEndStatus.VoluntaryRetirement);
            var result = state.Execute(cmd);

            AssertTrue(result.Success, "EndCampaignCommand should succeed");
            AssertEqual(CampaignEndStatus.VoluntaryRetirement, state.EndStatus, "EndStatus should be VoluntaryRetirement");
            AssertTrue(state.EndReport != null, "EndReport should be built");
            AssertEqual("TEST-01", state.EndReport.CampaignId, "CampaignId should match");
            AssertEqual(CampaignEndStatus.VoluntaryRetirement, state.EndReport.EndStatus, "Report status should match");
        }

        // ---------------------------------------------------------------- TEST 6 — EndCampaignCommand undo
        private static void Test_EndCommand_Undo()
        {
            var state = MakeCampaign();
            var cmd = new EndCampaignCommand(CampaignEndStatus.Bankruptcy);
            state.Execute(cmd);

            AssertEqual(CampaignEndStatus.Bankruptcy, state.EndStatus, "Status should be Bankruptcy before undo");

            var undoResult = state.Undo();
            AssertTrue(undoResult.Success, "Undo should succeed");
            AssertEqual(CampaignEndStatus.Active, state.EndStatus, "Status should be restored to Active after undo");
            AssertTrue(state.EndReport == null, "EndReport should be restored to null after undo");
        }

        // ---------------------------------------------------------------- TEST 7 — ViewModel binds report
        private static void Test_ViewModel_Binds()
        {
            var state = MakeCampaign();
            state.AgencyName = "Test Agency";

            // End it
            state.Execute(new EndCampaignCommand(CampaignEndStatus.TimeExpired));

            var vm = new CampaignEndViewModel(state);

            AssertEqual("Test Agency", vm.AgencyNameText.Value, "AgencyName should bind");
            AssertEqual("CAMPAIGN CONCLUDED", vm.EndStatusBadgeText.Value, "Badge should be CAMPAIGN CONCLUDED");
            AssertTrue(vm.ConclusionText.Value.Contains("Test Agency"), "Conclusion should contain agency name");
            AssertEqual(false, vm.IsVictory.Value, "TimeExpired is not a victory");

            // Refresh after changing state (simulate re-query)
            vm.Refresh();
            AssertEqual("CAMPAIGN CONCLUDED", vm.EndStatusBadgeText.Value, "Badge should persist after Refresh");
        }

        // ---------------------------------------------------------------- TEST 8 — AdvanceTime auto-triggers
        private static void Test_AdvanceTime_AutoEnd()
        {
            var state = MakeCampaign();
            AssertEqual(CampaignEndStatus.Active, state.EndStatus, "Initially Active");

            // Advance past the 2061-01-01 deadline (> 30 years = ~10958 days)
            var advCmd = new AdvanceTimeCommand(10958);
            var result = state.Execute(advCmd);

            AssertTrue(result.Success, "AdvanceTimeCommand should succeed even past boundary");
            AssertTrue(state.Time.IsExpired, "Time should be expired");
            AssertEqual(CampaignEndStatus.TimeExpired, state.EndStatus,
                "AdvanceTimeCommand should auto-trigger TimeExpired end condition");
            AssertTrue(state.EndReport != null, "EndReport should be built automatically");

            // Undo restores Active
            var undoResult = state.Undo();
            AssertTrue(undoResult.Success, "Undo AdvanceTime should succeed");
            AssertEqual(CampaignEndStatus.Active, state.EndStatus,
                "Undo should restore Active end status");
            AssertTrue(state.EndReport == null, "Undo should clear EndReport");
        }
    }
}
