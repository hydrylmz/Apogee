using System;
using System.Collections.Generic;
using NUnit.Framework;
using Apogee.Core;
using Apogee.Strategy;

namespace Apogee.Tests.EditMode.Strategy
{
    [TestFixture]
    public class EconomySystemTests
    {
        [Test]
        public void Economy_AnnualAllocation_ScalesWithReputation()
        {
            var settings = EconomySettings.CreateDefault();

            double allocZeroRep = EconomyEvaluator.CalculateAnnualBudgetAllocation(0.0, settings);
            double allocStartRep = EconomyEvaluator.CalculateAnnualBudgetAllocation(30.0, settings);
            double allocMaxRep = EconomyEvaluator.CalculateAnnualBudgetAllocation(100.0, settings);

            Assert.AreEqual(settings.MinimumAnnualBudget, allocZeroRep, 0.01, "Reputation 0 should give minimum floor.");
            Assert.IsTrue(allocStartRep > allocZeroRep, "Allocation at rep 30 must exceed rep 0 floor.");
            Assert.AreEqual(settings.MaximumAnnualBudget, allocMaxRep, 0.01, "Reputation 100 should give maximum budget ceiling.");
        }

        [Test]
        public void Economy_MonthlyExpenses_SumsActiveFacilitiesAccurately()
        {
            var pad = new BaseFacility("pad1", "cape", BaseType.LaunchPad, level: 1);
            var factory = new BaseFacility("fac1", "cape", BaseType.Factory, level: 1);
            var research = new BaseFacility("res1", "cape", BaseType.ResearchCenter, level: 1);

            var bases = new List<BaseFacility> { pad, factory, research };
            var breakdown = EconomyEvaluator.ComputeMonthlyExpenses(bases);

            // Pad L1: 1.5, Factory L1: 2.0, Research L1: 1.8 -> Sum = 5.3 M$/month
            Assert.AreEqual(3, breakdown.FacilityExpenses.Count);
            Assert.AreEqual(5.3, breakdown.TotalMonthlyCost, 0.05);
        }

        [Test]
        public void Economy_AdvanceDays_DeductsMonthlyMaintenanceOnNewMonth()
        {
            var campaign = new CampaignState(seed: 12345);
            double initialBudget = campaign.Budget; // 500 M$

            // Advance 31 days (2031-01-01 -> 2031-02-01)
            var cmd = new AdvanceTimeCommand(31);
            var result = campaign.Execute(cmd);

            Assert.IsTrue(result.Success, result.Message);
            // Starting bases: pad (1.5) + factory (2.0) + research (1.8) = 5.3 M$
            double expectedBudget = initialBudget - 5.3;
            Assert.AreEqual(expectedBudget, campaign.Budget, 0.1);
            Assert.AreEqual(1, campaign.Economy.Ledger.Count);
            Assert.AreEqual(FinancialRecordType.MonthlyMaintenance, campaign.Economy.Ledger[0].Type);
        }

        [Test]
        public void Economy_AdvanceDays_GrantsAnnualBudgetAllocationOnYearCrossing()
        {
            var campaign = new CampaignState(seed: 12345);
            // Start at 2031-01-01. Initial budget = 500 M$. Initial reputation = 30.
            // Expected allocation for rep 30 = 50 + (800 - 50) * 0.30 = 275 M$
            double expectedAlloc = EconomyEvaluator.CalculateAnnualBudgetAllocation(campaign.Reputation);

            // Advance 365 days -> 2032-01-01 (12 months of maintenance + 1 annual grant for year 2032)
            var cmd = new AdvanceTimeCommand(365);
            var result = campaign.Execute(cmd);

            Assert.IsTrue(result.Success, result.Message);

            // Verify annual allocation record exists in ledger
            bool foundAnnual = false;
            foreach (var rec in campaign.Economy.Ledger)
            {
                if (rec.Type == FinancialRecordType.AnnualAllocation)
                {
                    foundAnnual = true;
                    Assert.AreEqual(expectedAlloc, rec.Amount, 0.5);
                }
            }

            Assert.IsTrue(foundAnnual, "Annual budget allocation must be granted when crossing into new campaign year.");
        }

        [Test]
        public void Economy_AdvanceDays_UndoRestoresBudgetAndLedgerSymmetrically()
        {
            var campaign = new CampaignState(seed: 12345);
            double initialBudget = campaign.Budget;
            int initialLedgerCount = campaign.Economy.Ledger.Count;

            // Advance across 2 months
            var cmd = new AdvanceTimeCommand(65);
            campaign.Execute(cmd);

            Assert.AreNotEqual(initialBudget, campaign.Budget);
            Assert.IsTrue(campaign.Economy.Ledger.Count > initialLedgerCount);

            // Undo
            var undoRes = campaign.Undo();
            Assert.IsTrue(undoRes.Success, undoRes.Message);

            Assert.AreEqual(initialBudget, campaign.Budget, 0.001);
            Assert.AreEqual(initialLedgerCount, campaign.Economy.Ledger.Count);
        }

        [Test]
        public void Economy_IsBankrupt_IdentifiesFailureConditionCorrectly()
        {
            var settings = EconomySettings.CreateDefault();

            // Budget above threshold or positive reputation -> not bankrupt
            Assert.IsFalse(EconomyEvaluator.IsBankrupt(100.0, 50.0, settings));
            Assert.IsFalse(EconomyEvaluator.IsBankrupt(-50.0, 10.0, settings));
            Assert.IsFalse(EconomyEvaluator.IsBankrupt(-50.0, 0.0, settings));

            // Budget below debt limit AND reputation <= 0 -> bankrupt
            Assert.IsTrue(EconomyEvaluator.IsBankrupt(-150.0, 0.0, settings));
        }

        [Test]
        public void Economy_ExternalTransaction_RecordedInLedger()
        {
            var campaign = new CampaignState(seed: 12345);
            campaign.Economy.RecordTransaction(
                campaign.Time.CurrentDate,
                FinancialRecordType.FacilityConstruction,
                -50.0,
                campaign.Budget - 50.0,
                "New Launch Pad in Guiana Coast");

            Assert.AreEqual(1, campaign.Economy.Ledger.Count);
            Assert.AreEqual(FinancialRecordType.FacilityConstruction, campaign.Economy.Ledger[0].Type);
            Assert.AreEqual(-50.0, campaign.Economy.Ledger[0].Amount, 0.001);
        }

        [Test]
        public void Economy_HigherBaseLevel_IncreasesMaintenanceExpense()
        {
            var padLvl1 = new BaseFacility("pad1", "cape", BaseType.LaunchPad, level: 1);
            var padLvl3 = new BaseFacility("pad3", "cape", BaseType.LaunchPad, level: 3);

            double cost1 = BaseLevelSpec.GetMonthlyMaintenanceCost(padLvl1.Type, padLvl1.Level);
            double cost3 = BaseLevelSpec.GetMonthlyMaintenanceCost(padLvl3.Type, padLvl3.Level);

            Assert.IsTrue(cost3 > cost1 * 2.5, "Level 3 facility must cost significantly more maintenance than Level 1.");
        }
    }
}
