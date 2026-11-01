using System;
using System.Collections.Generic;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Type of financial transaction recorded by the agency economy ledger.
    /// </summary>
    public enum FinancialRecordType
    {
        AnnualAllocation,
        MonthlyMaintenance,
        MissionContractCost,
        MissionReward,
        FacilityConstruction,
        FacilityUpgrade,
        ProductionCost,
        ResearchCost,
        TestingCost,
        Refund,
        Penalty
    }

    /// <summary>
    /// Immutable record of an economic transaction.
    /// </summary>
    public sealed class FinancialRecord
    {
        public GameDate Date { get; }
        public FinancialRecordType Type { get; }
        public double Amount { get; } // Positive = Income, Negative = Expense
        public double ResultingBudget { get; }
        public string Description { get; }

        public FinancialRecord(GameDate date, FinancialRecordType type, double amount, double resultingBudget, string description)
        {
            Date = date;
            Type = type;
            Amount = amount;
            ResultingBudget = resultingBudget;
            Description = description ?? string.Empty;
        }

        public override string ToString() =>
            $"[{Date}] {Type}: {(Amount >= 0 ? "+" : "")}{Amount:F1} M$ (Balance: {ResultingBudget:F1} M$) — {Description}";
    }

    /// <summary>
    /// Memento snapshot used for deterministic rollback during Undo operations.
    /// </summary>
    public sealed class EconomySnapshot
    {
        public GameDate Date { get; }
        public double Budget { get; }
        public double Reputation { get; }
        public int LastProcessedYear { get; }
        public int LastProcessedMonthYear { get; }
        public int LedgerCount { get; }

        public EconomySnapshot(GameDate date, double budget, double reputation, int lastProcessedYear, int lastProcessedMonthYear, int ledgerCount)
        {
            Date = date;
            Budget = budget;
            Reputation = reputation;
            LastProcessedYear = lastProcessedYear;
            LastProcessedMonthYear = lastProcessedMonthYear;
            LedgerCount = ledgerCount;
        }
    }

    /// <summary>
    /// Runtime economic manager for the agency.
    /// Tracks annual allocations, monthly facility maintenance, budget ledger,
    /// and handles deterministic calendar-triggered financial updates during time advancement.
    /// Fully reversible via Undo/Redo (GDD §4.3, §6.3, §6.4, §8.1, §16.8, §19.13).
    /// </summary>
    public sealed class EconomyManager
    {
        private readonly List<FinancialRecord> _ledger = new List<FinancialRecord>();
        private readonly Stack<EconomySnapshot> _history = new Stack<EconomySnapshot>();

        public EconomySettings Settings { get; }

        /// <summary>The most recent campaign year for which annual budget was granted.</summary>
        public int LastProcessedYear { get; private set; }

        /// <summary>
        /// Hash identifier for (Year * 12 + Month) to detect monthly boundaries.
        /// </summary>
        public int LastProcessedMonthYear { get; private set; }

        /// <summary>Full transaction history ledger.</summary>
        public IReadOnlyList<FinancialRecord> Ledger => _ledger;

        public EconomyManager(EconomySettings settings = null, int initialYear = GameDate.EpochYear, int initialMonth = GameDate.EpochMonth)
        {
            Settings = settings ?? EconomySettings.CreateDefault();
            // The starting state already has starting budget (500 M$) for epoch year (2031-01-01),
            // so we set the initial marker to current date to avoid duplicate day 0 allocation.
            LastProcessedYear = initialYear;
            LastProcessedMonthYear = initialYear * 12 + initialMonth;
        }

        /// <summary>
        /// Records an external transaction (e.g. facility construction, contract, research) into the ledger.
        /// </summary>
        public void RecordTransaction(GameDate date, FinancialRecordType type, double amount, double resultingBudget, string description)
        {
            _ledger.Add(new FinancialRecord(date, type, amount, resultingBudget, description));
        }

        /// <summary>
        /// Advances time by days and triggers calendar-based financial events (monthly maintenance, annual budget).
        /// Saves a rollback snapshot before mutating.
        /// </summary>
        public (double netBudgetDelta, List<FinancialRecord> newRecords) AdvanceDays(
            int days,
            GameDate oldDate,
            GameDate newDate,
            CampaignState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            // Push snapshot for undo
            _history.Push(new EconomySnapshot(
                oldDate,
                state.Budget,
                state.Reputation,
                LastProcessedYear,
                LastProcessedMonthYear,
                _ledger.Count));

            var newRecords = new List<FinancialRecord>();
            double startingBudget = state.Budget;

            // Iterate through every month and year transition passed during this time step
            // We step day by day or check monthly/annual boundaries:
            for (int dayOffset = 1; dayOffset <= days; dayOffset++)
            {
                GameDate curDate = oldDate.AddDays(dayOffset);
                int curMonthYear = curDate.Year * 12 + curDate.Month;

                // 1. Check Monthly Maintenance on Month Boundary (First day of new month)
                if (curMonthYear > LastProcessedMonthYear)
                {
                    var breakdown = EconomyEvaluator.ComputeMonthlyExpenses(state.Bases.Values, Settings);
                    if (breakdown.TotalMonthlyCost > 0.0)
                    {
                        state.Budget -= breakdown.TotalMonthlyCost;
                        var rec = new FinancialRecord(
                            curDate,
                            FinancialRecordType.MonthlyMaintenance,
                            -breakdown.TotalMonthlyCost,
                            state.Budget,
                            $"Monthly base maintenance for {breakdown.FacilityExpenses.Count} facilities.");
                        _ledger.Add(rec);
                        newRecords.Add(rec);
                    }

                    if (Settings.PassiveReputationDecayPerMonth > 0)
                    {
                        state.Reputation = Math.Max(EconomySettings.MinReputation, state.Reputation - Settings.PassiveReputationDecayPerMonth);
                    }

                    LastProcessedMonthYear = curMonthYear;
                }

                // 2. Check Annual Budget Allocation on Year Boundary (Jan 1st or new year crossing)
                if (curDate.Year > LastProcessedYear)
                {
                    double allocation = EconomyEvaluator.CalculateAnnualBudgetAllocation(state.Reputation, Settings);
                    state.Budget += allocation;
                    var rec = new FinancialRecord(
                        curDate,
                        FinancialRecordType.AnnualAllocation,
                        allocation,
                        state.Budget,
                        $"Annual budget grant for year {curDate.Year} (Reputation: {state.Reputation:F0}).");
                    _ledger.Add(rec);
                    newRecords.Add(rec);

                    LastProcessedYear = curDate.Year;
                }
            }

            double netBudgetDelta = state.Budget - startingBudget;
            return (netBudgetDelta, newRecords);
        }

        /// <summary>
        /// Undoes the last advance step and restores budget, reputation, markers, and ledger.
        /// </summary>
        public void UndoAdvance(GameDate targetDate, CampaignState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (_history.Count > 0)
            {
                var snap = _history.Pop();
                state.Budget = snap.Budget;
                state.Reputation = snap.Reputation;
                LastProcessedYear = snap.LastProcessedYear;
                LastProcessedMonthYear = snap.LastProcessedMonthYear;

                // Trim ledger back to snapshot count
                if (_ledger.Count > snap.LedgerCount)
                {
                    _ledger.RemoveRange(snap.LedgerCount, _ledger.Count - snap.LedgerCount);
                }
            }
        }
    }
}
