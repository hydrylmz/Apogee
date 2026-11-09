using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;
using NUnit.Framework;

namespace Apogee.Tests.EditMode.Strategy
{
    /// <summary>
    /// EditMode unit tests for M4-05: Research System.
    /// Covers ResearchManager, ResearchNodeState, StartResearchCommand, CancelResearchCommand,
    /// unlock propagation, progress advancement, and CampaignState integration.
    /// GDD §9.2, §21 M4-05.
    /// </summary>
    [TestFixture]
    public sealed class ResearchSystemTests
    {
        // ── Test catalog helpers ──────────────────────────────────────────────────

        private static ResearchNodeRecord MakeNode(
            string id,
            string branch = "Propulsion",
            int tier = 1,
            double cost = 100,
            int durationDays = 60,
            double startCost = 0,
            string[] prerequisites = null,
            string[] unlocksModules = null)
        {
            // Use JSON round-trip to honour private setters
            var json = $@"{{
                ""id"":""{id}"",
                ""nameKey"":""key.{id}.name"",
                ""branch"":""{branch}"",
                ""tier"":{tier},
                ""researchPointCost"":{cost},
                ""baseDurationDays"":{durationDays},
                ""startCostMUSD"":{startCost},
                ""prerequisites"":[{(prerequisites == null ? "" : string.Join(",", prerequisites.Select(p => $"\"{p}\"")))}],
                ""unlocksModuleIds"":[{(unlocksModules == null ? "" : string.Join(",", unlocksModules.Select(m => $"\"{m}\"")))}]
            }}";

            return Newtonsoft.Json.JsonConvert.DeserializeObject<ResearchNodeRecord>(json);
        }

        private static ResearchManager MakeManager(params ResearchNodeRecord[] nodes) =>
            new ResearchManager(nodes);

        // ── 1. Initial state ──────────────────────────────────────────────────────

        [Test]
        public void InitialState_NoPrerequisite_NodeIsAvailable()
        {
            var mgr = MakeManager(MakeNode("A"));
            Assert.AreEqual(ResearchStatus.Available, mgr.GetStatus("A"));
        }

        [Test]
        public void InitialState_WithPrerequisite_NodeIsLocked()
        {
            var mgr = MakeManager(
                MakeNode("A"),
                MakeNode("B", prerequisites: new[] { "A" }));

            Assert.AreEqual(ResearchStatus.Locked, mgr.GetStatus("B"));
        }

        [Test]
        public void InitialState_UnknownId_ReturnsLocked()
        {
            var mgr = MakeManager(MakeNode("A"));
            Assert.AreEqual(ResearchStatus.Locked, mgr.GetStatus("nonexistent"));
        }

        // ── 2. Enqueue / StartResearchCommand ─────────────────────────────────────

        [Test]
        public void Enqueue_Available_Succeeds()
        {
            var mgr = MakeManager(MakeNode("A"));
            var (ok, reason) = mgr.TryEnqueue("A", currentTick: 0, campaignBudget: 1000, out _);

            Assert.IsTrue(ok, reason);
            Assert.AreEqual("A", mgr.ActiveNodeId);
            Assert.AreEqual(ResearchStatus.InProgress, mgr.GetStatus("A"));
        }

        [Test]
        public void Enqueue_Locked_Fails()
        {
            var mgr = MakeManager(
                MakeNode("A"),
                MakeNode("B", prerequisites: new[] { "A" }));

            var (ok, _) = mgr.TryEnqueue("B", currentTick: 0, campaignBudget: 1000, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void Enqueue_Duplicate_Fails()
        {
            var mgr = MakeManager(MakeNode("A"));
            mgr.TryEnqueue("A", 0, 1000, out _);
            var (ok, _) = mgr.TryEnqueue("A", 0, 1000, out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void Enqueue_StartCostCharged_WhenBudgetSufficient()
        {
            var mgr = MakeManager(MakeNode("A", startCost: 50));
            var (ok, _) = mgr.TryEnqueue("A", 0, 100, out double cost);
            Assert.IsTrue(ok);
            Assert.AreEqual(50, cost);
        }

        [Test]
        public void Enqueue_StartCostFails_WhenBudgetInsufficient()
        {
            var mgr = MakeManager(MakeNode("A", startCost: 200));
            var (ok, _) = mgr.TryEnqueue("A", 0, campaignBudget: 50, out _);
            Assert.IsFalse(ok);
        }

        // ── 3. Cancel / CancelResearchCommand ─────────────────────────────────────

        [Test]
        public void Cancel_Queued_RemovesFromQueue()
        {
            var mgr = MakeManager(MakeNode("A"));
            mgr.TryEnqueue("A", 0, 1000, out _);

            var (ok, _) = mgr.TryCancel("A");
            Assert.IsTrue(ok);
            Assert.IsNull(mgr.ActiveNodeId);
            Assert.AreEqual(ResearchStatus.Available, mgr.GetStatus("A"));
        }

        [Test]
        public void Cancel_NotQueued_Fails()
        {
            var mgr = MakeManager(MakeNode("A"));
            var (ok, _) = mgr.TryCancel("A");
            Assert.IsFalse(ok);
        }

        // ── 4. Progress advancement ────────────────────────────────────────────────

        [Test]
        public void AdvanceDays_Partial_DoesNotComplete()
        {
            var mgr = MakeManager(MakeNode("A", durationDays: 60));
            mgr.TryEnqueue("A", 0, 1000, out _);

            var completed = mgr.AdvanceDays(30, researchPointsPerDay: 5, currentTick: 30);

            Assert.IsEmpty(completed);
            Assert.AreEqual(ResearchStatus.InProgress, mgr.GetStatus("A"));
        }

        [Test]
        public void AdvanceDays_Full_Completes()
        {
            var mgr = MakeManager(MakeNode("A", durationDays: 60));
            mgr.TryEnqueue("A", 0, 1000, out _);

            var completed = mgr.AdvanceDays(60, researchPointsPerDay: 5, currentTick: 60);

            Assert.AreEqual(1, completed.Count);
            Assert.AreEqual("A", completed[0].Id);
            Assert.AreEqual(ResearchStatus.Completed, mgr.GetStatus("A"));
        }

        [Test]
        public void AdvanceDays_Overflow_CompletesAndStartsNext()
        {
            var mgr = MakeManager(
                MakeNode("A", durationDays: 30),
                MakeNode("B", durationDays: 30));

            mgr.TryEnqueue("A", 0, 1000, out _);
            mgr.TryEnqueue("B", 0, 1000, out _);

            // 50 days: completes A (30 days) then starts B with 20 remaining days
            var completed = mgr.AdvanceDays(50, researchPointsPerDay: 2, currentTick: 50);

            Assert.AreEqual(1, completed.Count, "A should complete");
            Assert.AreEqual("A", completed[0].Id);
            Assert.AreEqual(ResearchStatus.Completed, mgr.GetStatus("A"));
            Assert.AreEqual(ResearchStatus.InProgress, mgr.GetStatus("B"));
        }

        [Test]
        public void AdvanceDays_CompletesMultipleNodes_InOneCall()
        {
            var mgr = MakeManager(
                MakeNode("A", durationDays: 10),
                MakeNode("B", durationDays: 10));

            mgr.TryEnqueue("A", 0, 1000, out _);
            mgr.TryEnqueue("B", 0, 1000, out _);

            var completed = mgr.AdvanceDays(25, researchPointsPerDay: 1, currentTick: 25);

            Assert.AreEqual(2, completed.Count, "Both A and B should complete");
        }

        // ── 5. Prerequisite unlock propagation ────────────────────────────────────

        [Test]
        public void CompleteNode_UnlocksSuccessor()
        {
            var mgr = MakeManager(
                MakeNode("A", durationDays: 10),
                MakeNode("B", prerequisites: new[] { "A" }));

            mgr.TryEnqueue("A", 0, 1000, out _);
            mgr.AdvanceDays(10, 1, 10);

            Assert.AreEqual(ResearchStatus.Completed, mgr.GetStatus("A"));
            Assert.AreEqual(ResearchStatus.Available, mgr.GetStatus("B"),
                "B should be unlocked after A completes");
        }

        [Test]
        public void MultiplePrerequisites_NotUnlockedUntilAllDone()
        {
            var mgr = MakeManager(
                MakeNode("A", durationDays: 10),
                MakeNode("B", durationDays: 10),
                MakeNode("C", prerequisites: new[] { "A", "B" }));

            // Complete A only
            mgr.TryEnqueue("A", 0, 1000, out _);
            mgr.AdvanceDays(10, 1, 10);

            Assert.AreEqual(ResearchStatus.Locked, mgr.GetStatus("C"),
                "C should remain locked until both A and B are done");

            // Complete B as well
            mgr.TryEnqueue("B", 0, 1000, out _);
            mgr.AdvanceDays(10, 1, 20);

            Assert.AreEqual(ResearchStatus.Available, mgr.GetStatus("C"));
        }

        // ── 6. Events ─────────────────────────────────────────────────────────────

        [Test]
        public void OnNodeCompleted_FiredOnce_PerCompletedNode()
        {
            var mgr = MakeManager(MakeNode("A", durationDays: 10));
            int firedCount = 0;
            mgr.OnNodeCompleted += (id, record) => firedCount++;

            mgr.TryEnqueue("A", 0, 1000, out _);
            mgr.AdvanceDays(10, 1, 10);

            Assert.AreEqual(1, firedCount);
        }

        [Test]
        public void OnActiveResearchChanged_FiredWhenQueueChanges()
        {
            var mgr = MakeManager(MakeNode("A", durationDays: 10));
            var changes = new List<string>();
            mgr.OnActiveResearchChanged += id => changes.Add(id ?? "<null>");

            mgr.TryEnqueue("A", 0, 1000, out _);    // adds to queue → fires
            mgr.AdvanceDays(10, 1, 10);              // completes A, queue empty → fires

            Assert.AreEqual(2, changes.Count);
        }

        // ── 7. CampaignState integration ──────────────────────────────────────────

        [Test]
        public void CampaignState_InitialiseResearch_ResearchManagerAvailable()
        {
            var state = new CampaignState();
            state.InitialiseResearch(new[] { MakeNode("A") });

            Assert.IsNotNull(state.Research);
            Assert.AreEqual(ResearchStatus.Available, state.Research.GetStatus("A"));
        }

        [Test]
        public void StartResearchCommand_ViaCommandBus_Succeeds()
        {
            var state = new CampaignState();
            state.InitialiseResearch(new[] { MakeNode("A") });

            var result = state.Execute(new StartResearchCommand("A"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(ResearchStatus.InProgress, state.Research.GetStatus("A"));
        }

        [Test]
        public void StartResearchCommand_Undo_RemovesFromQueue()
        {
            var state = new CampaignState();
            state.InitialiseResearch(new[] { MakeNode("A") });
            state.Execute(new StartResearchCommand("A"));

            state.CommandBus.Undo(CommandContext.CreateLocalAdmin(state));

            Assert.AreEqual(ResearchStatus.Available, state.Research.GetStatus("A"));
        }

        [Test]
        public void CancelResearchCommand_ViaCommandBus_Succeeds()
        {
            var state = new CampaignState();
            state.InitialiseResearch(new[] { MakeNode("A") });
            state.Execute(new StartResearchCommand("A"));

            var result = state.Execute(new CancelResearchCommand("A"));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(ResearchStatus.Available, state.Research.GetStatus("A"));
        }

        [Test]
        public void StartResearchCommand_WithStartCost_DeductsBudget()
        {
            var state = new CampaignState();
            state.InitialiseResearch(new[] { MakeNode("A", startCost: 50) });
            double budgetBefore = state.Budget;

            state.Execute(new StartResearchCommand("A"));

            Assert.AreEqual(budgetBefore - 50, state.Budget, 0.001);
        }

        [Test]
        public void CancelResearchCommand_WithStartCost_RefundsBudget()
        {
            var state = new CampaignState();
            state.InitialiseResearch(new[] { MakeNode("A", startCost: 50) });
            state.Execute(new StartResearchCommand("A"));
            double budgetAfterStart = state.Budget;

            state.Execute(new CancelResearchCommand("A"));

            Assert.AreEqual(budgetAfterStart + 50, state.Budget, 0.001);
        }

        // ── 8. GetProgressFraction ─────────────────────────────────────────────────

        [Test]
        public void GetProgressFraction_HalfDone_Returns0Point5()
        {
            var mgr = MakeManager(MakeNode("A", durationDays: 100));
            mgr.TryEnqueue("A", 0, 1000, out _);
            mgr.AdvanceDays(50, 1, 50);

            var nodeState = mgr.GetNodeState("A");
            Assert.AreEqual(0.5, nodeState.GetProgressFraction(100), 0.001);
        }

        [Test]
        public void GetProgressFraction_Completed_Returns1()
        {
            var mgr = MakeManager(MakeNode("A", durationDays: 10));
            mgr.TryEnqueue("A", 0, 1000, out _);
            mgr.AdvanceDays(10, 1, 10);

            var nodeState = mgr.GetNodeState("A");
            Assert.AreEqual(1.0, nodeState.GetProgressFraction(10), 0.001);
        }

        // ── 9. GetTreeByBranch ─────────────────────────────────────────────────────

        [Test]
        public void GetTreeByBranch_GroupsCorrectly()
        {
            var mgr = MakeManager(
                MakeNode("A", branch: "Propulsion"),
                MakeNode("B", branch: "Propulsion"),
                MakeNode("C", branch: "Structure"));

            var tree = mgr.GetTreeByBranch();

            Assert.IsTrue(tree.ContainsKey("Propulsion"));
            Assert.IsTrue(tree.ContainsKey("Structure"));
            Assert.AreEqual(2, tree["Propulsion"].Count);
            Assert.AreEqual(1, tree["Structure"].Count);
        }

        // ── 10. CatalogLoader integration for ResearchNodeRecord ──────────────────

        [Test]
        public void CatalogLoader_LoadsResearchTreeJson_Valid()
        {
            string json = @"{
              ""records"": [
                {
                  ""id"": ""test-node-01"",
                  ""nameKey"": ""key.test.name"",
                  ""branch"": ""Propulsion"",
                  ""tier"": 1,
                  ""prerequisites"": [],
                  ""researchPointCost"": 80,
                  ""baseDurationDays"": 90,
                  ""startCostMUSD"": 0,
                  ""unlocksModuleIds"": [],
                  ""globalStatModifiers"": [],
                  ""effectSummary"": ""Test node""
                }
              ]
            }";

            var collection = Apogee.Data.CatalogLoader.Load<ResearchNodeRecord>(json);
            Assert.IsTrue(collection.Validation.IsValid, string.Join(", ", collection.Validation.Errors));
            Assert.AreEqual(1, collection.Count);
            Assert.AreEqual("test-node-01", collection.All[0].Id);
            Assert.AreEqual("Propulsion", collection.All[0].Branch);
            Assert.AreEqual(80, collection.All[0].ResearchPointCost);
        }

        [Test]
        public void AdvanceTimeCommand_AdvancesActiveResearchProgress_AndProducesResearchPoints()
        {
            var node = MakeNode("struct-al-li-alloy", durationDays: 60);
            var campaign = new CampaignState();
            campaign.InitialiseResearch(new[] { node });

            // Enqueue research node (60 days)
            campaign.Execute(new StartResearchCommand("struct-al-li-alloy"));

            var stateBefore = campaign.Research.GetNodeState("struct-al-li-alloy");
            Assert.AreEqual(0, stateBefore.ProgressDays);
            Assert.AreEqual(ResearchStatus.InProgress, stateBefore.Status);

            // Advance 10 days
            var cmd = new AdvanceTimeCommand(10);
            var res = campaign.Execute(cmd);
            Assert.IsTrue(res.Success);

            // Progress should now be 10/60 days
            Assert.AreEqual(10, stateBefore.ProgressDays);
            Assert.AreEqual(ResearchStatus.InProgress, stateBefore.Status);
            Assert.Greater(campaign.ResearchPoints, 0.0, "Research points should be generated from starting ResearchCenter");

            // Advance another 50 days (total 60) -> completed!
            campaign.Execute(new AdvanceTimeCommand(50));
            Assert.AreEqual(60, stateBefore.ProgressDays);
            Assert.AreEqual(ResearchStatus.Completed, stateBefore.Status);
            Assert.IsTrue(campaign.Research.IsCompleted("struct-al-li-alloy"));

            // Test Undo on the 50-day advance
            campaign.Undo();
            Assert.AreEqual(10, stateBefore.ProgressDays);
            Assert.AreEqual(ResearchStatus.InProgress, stateBefore.Status);
            Assert.IsFalse(campaign.Research.IsCompleted("struct-al-li-alloy"));
        }
    }
}
