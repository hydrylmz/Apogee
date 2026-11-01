using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Strategy;

namespace ApogeeLocalTestRunner
{
    public static class EconomyRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M4_08_Economy_AnnualAllocation_ScalesWithReputation", Test_AnnualAllocation);
            runTest("M4_08_Economy_MonthlyExpenses_SumsActiveFacilitiesAccurately", Test_MonthlyExpenses);
            runTest("M4_08_Economy_AdvanceDays_DeductsMonthlyMaintenanceOnNewMonth", Test_MonthlyMaintenanceDeduction);
            runTest("M4_08_Economy_AdvanceDays_GrantsAnnualBudgetAllocationOnYearCrossing", Test_AnnualBudgetGrant);
            runTest("M4_08_Economy_AdvanceDays_UndoRestoresBudgetAndLedgerSymmetrically", Test_UndoRestore);
            runTest("M4_08_Economy_IsBankrupt_IdentifiesFailureConditionCorrectly", Test_BankruptcyCheck);
            runTest("M4_08_Economy_ExternalTransaction_RecordedInLedger", Test_ExternalTransaction);
            runTest("M4_08_Economy_HigherBaseLevel_IncreasesMaintenanceExpense", Test_HigherBaseLevelMaintenance);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        private static void AssertEqual(double expected, double actual, double tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception($"Assertion Failed: {message}. Expected: {expected}, Actual: {actual}");
        }

        private static void Test_AnnualAllocation()
        {
            var settings = EconomySettings.CreateDefault();
            double allocZeroRep = EconomyEvaluator.CalculateAnnualBudgetAllocation(0.0, settings);
            double allocStartRep = EconomyEvaluator.CalculateAnnualBudgetAllocation(30.0, settings);
            double allocMaxRep = EconomyEvaluator.CalculateAnnualBudgetAllocation(100.0, settings);

            AssertEqual(settings.MinimumAnnualBudget, allocZeroRep, 0.01, "Rep 0 floor");
            AssertTrue(allocStartRep > allocZeroRep, "Rep 30 exceeds floor");
            AssertEqual(settings.MaximumAnnualBudget, allocMaxRep, 0.01, "Rep 100 max ceiling");
        }

        private static void Test_MonthlyExpenses()
        {
            var pad = new BaseFacility("pad1", "cape", BaseType.LaunchPad, level: 1);
            var factory = new BaseFacility("fac1", "cape", BaseType.Factory, level: 1);
            var research = new BaseFacility("res1", "cape", BaseType.ResearchCenter, level: 1);

            var bases = new List<BaseFacility> { pad, factory, research };
            var breakdown = EconomyEvaluator.ComputeMonthlyExpenses(bases);

            AssertTrue(breakdown.FacilityExpenses.Count == 3, "3 facilities");
            AssertEqual(5.3, breakdown.TotalMonthlyCost, 0.05, "Monthly total 5.3 M$");
        }

        private static void Test_MonthlyMaintenanceDeduction()
        {
            var campaign = new CampaignState(seed: 12345);
            double initialBudget = campaign.Budget; // 500 M$

            var cmd = new AdvanceTimeCommand(31);
            var result = campaign.Execute(cmd);

            AssertTrue(result.Success, result.Message);
            double expectedBudget = initialBudget - 5.3;
            AssertEqual(expectedBudget, campaign.Budget, 0.1, "Budget deducted by 5.3 M$");
            AssertTrue(campaign.Economy.Ledger.Count == 1, "Ledger has 1 record");
            AssertTrue(campaign.Economy.Ledger[0].Type == FinancialRecordType.MonthlyMaintenance, "Type is MonthlyMaintenance");
        }

        private static void Test_AnnualBudgetGrant()
        {
            var campaign = new CampaignState(seed: 12345);
            double expectedAlloc = EconomyEvaluator.CalculateAnnualBudgetAllocation(campaign.Reputation);

            var cmd = new AdvanceTimeCommand(365);
            var result = campaign.Execute(cmd);

            AssertTrue(result.Success, result.Message);
            bool foundAnnual = false;
            foreach (var rec in campaign.Economy.Ledger)
            {
                if (rec.Type == FinancialRecordType.AnnualAllocation)
                {
                    foundAnnual = true;
                    AssertEqual(expectedAlloc, rec.Amount, 0.5, "Annual allocation amount matches");
                }
            }
            AssertTrue(foundAnnual, "Annual allocation record found");
        }

        private static void Test_UndoRestore()
        {
            var campaign = new CampaignState(seed: 12345);
            double initialBudget = campaign.Budget;
            int initialLedgerCount = campaign.Economy.Ledger.Count;

            var cmd = new AdvanceTimeCommand(65);
            campaign.Execute(cmd);

            AssertTrue(Math.Abs(campaign.Budget - initialBudget) > 1.0, "Budget changed after 65 days");
            AssertTrue(campaign.Economy.Ledger.Count > initialLedgerCount, "Ledger has entries");

            var undoRes = campaign.Undo();
            AssertTrue(undoRes.Success, undoRes.Message);

            AssertEqual(initialBudget, campaign.Budget, 0.001, "Budget restored by Undo");
            AssertTrue(campaign.Economy.Ledger.Count == initialLedgerCount, "Ledger restored by Undo");
        }

        private static void Test_BankruptcyCheck()
        {
            var settings = EconomySettings.CreateDefault();
            AssertTrue(!EconomyEvaluator.IsBankrupt(100.0, 50.0, settings), "Solvent");
            AssertTrue(!EconomyEvaluator.IsBankrupt(-50.0, 10.0, settings), "Has reputation");
            AssertTrue(!EconomyEvaluator.IsBankrupt(-50.0, 0.0, settings), "Within debt limit");
            AssertTrue(EconomyEvaluator.IsBankrupt(-150.0, 0.0, settings), "Insolvent & zero reputation");
        }

        private static void Test_ExternalTransaction()
        {
            var campaign = new CampaignState(seed: 12345);
            campaign.Economy.RecordTransaction(
                campaign.Time.CurrentDate,
                FinancialRecordType.FacilityConstruction,
                -50.0,
                campaign.Budget - 50.0,
                "New Launch Pad");

            AssertTrue(campaign.Economy.Ledger.Count == 1, "Transaction recorded");
            AssertTrue(campaign.Economy.Ledger[0].Type == FinancialRecordType.FacilityConstruction, "Correct type");
            AssertEqual(-50.0, campaign.Economy.Ledger[0].Amount, 0.001, "Correct amount");
        }

        private static void Test_HigherBaseLevelMaintenance()
        {
            var padLvl1 = new BaseFacility("pad1", "cape", BaseType.LaunchPad, level: 1);
            var padLvl3 = new BaseFacility("pad3", "cape", BaseType.LaunchPad, level: 3);

            double cost1 = BaseLevelSpec.GetMonthlyMaintenanceCost(padLvl1.Type, padLvl1.Level);
            double cost3 = BaseLevelSpec.GetMonthlyMaintenanceCost(padLvl3.Type, padLvl3.Level);

            AssertTrue(cost3 > cost1 * 2.5, "Level 3 costs > 2.5x of Level 1");
        }
    }
}
