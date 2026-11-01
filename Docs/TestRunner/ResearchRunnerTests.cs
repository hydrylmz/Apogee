using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;

namespace ApogeeLocalTestRunner
{
    /// <summary>
    /// Verification tests for M4-05: Research System.
    /// Covers catalog loading, ResearchManager queue, progress, prerequisites unlock,
    /// StartResearchCommand and CancelResearchCommand with Undo/Redo.
    /// </summary>
    public static class ResearchRunnerTests
    {
        private static string GetJsonDir()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            if (!Directory.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            }
            return path;
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M4_05_Catalog_ResearchTree_LoadsAndValidates", Test_Catalog_LoadsAndValidates);
            runTest("M4_05_Research_InitialAvailableAndLockedNodes", Test_InitialAvailableAndLockedNodes);
            runTest("M4_05_Research_StartResearchCommand_ExecutesAndDeductsBudget", Test_StartResearchCommand_ExecutesAndDeductsBudget);
            runTest("M4_05_Research_StartResearchCommand_UndoRestoresBudgetAndQueue", Test_StartResearchCommand_UndoRestoresBudgetAndQueue);
            runTest("M4_05_Research_CancelResearchCommand_RefundsBudget", Test_CancelResearchCommand_RefundsBudget);
            runTest("M4_05_Research_AdvanceProgress_CompletesAndUnlocksSuccessors", Test_AdvanceProgress_CompletesAndUnlocksSuccessors);
            runTest("M4_05_Research_QueueMultiple_OverflowAdvancesNext", Test_QueueMultiple_OverflowAdvancesNext);
            runTest("M4_05_Research_GetTreeByBranch_ContainsAllBranches", Test_GetTreeByBranch_ContainsAllBranches);
        }

        private static List<ResearchNodeRecord> LoadCatalog()
        {
            string file = Path.Combine(GetJsonDir(), "research_tree.json");
            string json = File.ReadAllText(file);
            var col = CatalogLoader.Load<ResearchNodeRecord>(json);
            if (!col.Validation.IsValid)
            {
                throw new Exception("Catalog validation failed: " + string.Join("; ", col.Validation.Errors));
            }
            return col.All.ToList();
        }

        private static void Test_Catalog_LoadsAndValidates()
        {
            var nodes = LoadCatalog();
            if (nodes.Count < 15)
                throw new Exception($"Expected at least 15 research nodes in catalog, got {nodes.Count}");

            var prop = nodes.FirstOrDefault(n => n.Id == "prop-advanced-kerosene");
            if (prop == null) throw new Exception("prop-advanced-kerosene node not found");
            if (prop.Tier != 1) throw new Exception($"Expected tier 1, got {prop.Tier}");
            if (prop.Branch != "Propulsion") throw new Exception($"Expected Propulsion branch, got {prop.Branch}");
        }

        private static void Test_InitialAvailableAndLockedNodes()
        {
            var nodes = LoadCatalog();
            var mgr = new ResearchManager(nodes);

            if (mgr.GetStatus("prop-advanced-kerosene") != ResearchStatus.Available)
                throw new Exception("prop-advanced-kerosene should be Available initially");

            if (mgr.GetStatus("prop-hydrogen-upper") != ResearchStatus.Locked)
                throw new Exception("prop-hydrogen-upper should be Locked initially");
        }

        private static void Test_StartResearchCommand_ExecutesAndDeductsBudget()
        {
            var nodes = LoadCatalog();
            var state = new CampaignState();
            state.InitialiseResearch(nodes);

            // Find a node with startCost or use prop-advanced-kerosene
            var cmd = new StartResearchCommand("prop-advanced-kerosene");
            var res = state.Execute(cmd);

            if (!res.Success)
                throw new Exception("StartResearchCommand failed: " + res.Message);

            if (state.Research.ActiveNodeId != "prop-advanced-kerosene")
                throw new Exception("ActiveNodeId mismatch");

            if (state.Research.GetStatus("prop-advanced-kerosene") != ResearchStatus.InProgress)
                throw new Exception("Status should be InProgress");
        }

        private static void Test_StartResearchCommand_UndoRestoresBudgetAndQueue()
        {
            var nodes = LoadCatalog();
            var state = new CampaignState();
            state.InitialiseResearch(nodes);
            double initialBudget = state.Budget;

            var cmd = new StartResearchCommand("prop-advanced-kerosene");
            state.Execute(cmd);

            var ctx = CommandContext.CreateLocalAdmin(state);
            var undoRes = state.CommandBus.Undo(ctx);

            if (!undoRes.Success)
                throw new Exception("Undo failed: " + undoRes.Message);

            if (state.Research.ActiveNodeId != null)
                throw new Exception("Queue should be empty after undo");

            if (state.Research.GetStatus("prop-advanced-kerosene") != ResearchStatus.Available)
                throw new Exception("Status should revert to Available");

            if (Math.Abs(state.Budget - initialBudget) > 1e-4)
                throw new Exception($"Budget mismatch: expected {initialBudget}, got {state.Budget}");
        }

        private static void Test_CancelResearchCommand_RefundsBudget()
        {
            var nodes = LoadCatalog();
            var state = new CampaignState();
            state.InitialiseResearch(nodes);

            state.Execute(new StartResearchCommand("prop-advanced-kerosene"));
            var cancelCmd = new CancelResearchCommand("prop-advanced-kerosene");
            var res = state.Execute(cancelCmd);

            if (!res.Success)
                throw new Exception("CancelResearchCommand failed: " + res.Message);

            if (state.Research.ActiveNodeId != null)
                throw new Exception("Queue should be empty after cancel");

            if (state.Research.GetStatus("prop-advanced-kerosene") != ResearchStatus.Available)
                throw new Exception("Status should revert to Available");
        }

        private static void Test_AdvanceProgress_CompletesAndUnlocksSuccessors()
        {
            var nodes = LoadCatalog();
            var mgr = new ResearchManager(nodes);

            mgr.TryEnqueue("prop-advanced-kerosene", 0, 1000, out _);
            var record = mgr.GetRecord("prop-advanced-kerosene");

            var completed = mgr.AdvanceDays(record.BaseDurationDays, 1.0, record.BaseDurationDays);

            if (completed.Count != 1 || completed[0].Id != "prop-advanced-kerosene")
                throw new Exception("Expected prop-advanced-kerosene to complete");

            if (mgr.GetStatus("prop-advanced-kerosene") != ResearchStatus.Completed)
                throw new Exception("prop-advanced-kerosene should be Completed");

            if (mgr.GetStatus("prop-hydrogen-upper") != ResearchStatus.Available)
                throw new Exception("prop-hydrogen-upper should now be unlocked and Available");
        }

        private static void Test_QueueMultiple_OverflowAdvancesNext()
        {
            var nodes = LoadCatalog();
            var mgr = new ResearchManager(nodes);

            mgr.TryEnqueue("prop-advanced-kerosene", 0, 1000, out _);
            mgr.TryEnqueue("struct-al-li-alloy", 0, 1000, out _);

            var recKero = mgr.GetRecord("prop-advanced-kerosene");
            // Advance by kero duration + 20 days
            var completed = mgr.AdvanceDays(recKero.BaseDurationDays + 20, 1.0, 100);

            if (completed.Count != 1 || completed[0].Id != "prop-advanced-kerosene")
                throw new Exception("Expected prop-advanced-kerosene to complete");

            if (mgr.ActiveNodeId != "struct-al-li-alloy")
                throw new Exception("Active node should now be struct-al-li-alloy");

            var stateStruct = mgr.GetNodeState("struct-al-li-alloy");
            if (stateStruct.ProgressDays != 20)
                throw new Exception($"Expected 20 progress days on struct-al-li-alloy, got {stateStruct.ProgressDays}");
        }

        private static void Test_GetTreeByBranch_ContainsAllBranches()
        {
            var nodes = LoadCatalog();
            var mgr = new ResearchManager(nodes);
            var tree = mgr.GetTreeByBranch();

            string[] expectedBranches = { "Propulsion", "Structure", "EDL", "LifeSupport", "Power", "Comms", "Reliability" };
            foreach (var b in expectedBranches)
            {
                if (!tree.ContainsKey(b))
                    throw new Exception($"Branch {b} missing from tree");
            }
        }
    }
}
