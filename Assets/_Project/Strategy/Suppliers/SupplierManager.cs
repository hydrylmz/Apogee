using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Coordinates supplier relations, procurement contracts, calendar delivery schedules,
    /// and single-source risk assessments across the campaign.
    /// Complies with GDD §9.1 and §19.13.
    /// </summary>
    public sealed class SupplierManager
    {
        private readonly Dictionary<string, SupplierCompanyState> _companies;
        private readonly List<SupplierContract> _contracts;

        public event Action<SupplierContract> OnContractSigned;
        public event Action<SupplierContract> OnContractFulfilled;
        public event Action<SupplierContract> OnContractCancelled;
        public event Action<string, double>   OnRelationshipChanged;

        public IReadOnlyCollection<SupplierCompanyState> AllCompanies => _companies.Values;
        public IReadOnlyList<SupplierContract> AllContracts => _contracts;

        public IEnumerable<SupplierContract> PendingContracts =>
            _contracts.Where(c => c.Status == ContractStatus.PendingDelivery);

        public IEnumerable<SupplierContract> FulfilledContracts =>
            _contracts.Where(c => c.Status == ContractStatus.Fulfilled);

        public SupplierManager(IEnumerable<SupplierRecord> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            _companies = new Dictionary<string, SupplierCompanyState>(StringComparer.OrdinalIgnoreCase);
            _contracts = new List<SupplierContract>();

            foreach (var record in records)
            {
                if (record != null && !string.IsNullOrWhiteSpace(record.Id))
                {
                    _companies[record.Id] = new SupplierCompanyState(record);
                }
            }
        }

        public SupplierCompanyState GetCompany(string supplierId)
        {
            if (string.IsNullOrWhiteSpace(supplierId)) return null;
            return _companies.TryGetValue(supplierId, out var state) ? state : null;
        }

        public SupplierContract GetContract(string contractId)
        {
            if (string.IsNullOrWhiteSpace(contractId)) return null;
            return _contracts.FirstOrDefault(c => string.Equals(c.Id, contractId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retrieves upcoming hardware deliveries on or after a specified date, sorted chronologically.
        /// </summary>
        public IReadOnlyList<SupplierContract> GetUpcomingDeliveries(GameDate fromDate, int maxCount = 5)
        {
            return _contracts
                .Where(c => c.Status == ContractStatus.PendingDelivery && c.DeliveryDate >= fromDate)
                .OrderBy(c => c.DeliveryDate.TotalDays)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// Retrieves all hardware deliveries scheduled exactly on a given date.
        /// </summary>
        public IReadOnlyList<SupplierContract> GetDeliveriesOnDate(GameDate date)
        {
            return _contracts
                .Where(c => c.DeliveryDate == date)
                .ToList();
        }

        /// <summary>
        /// Advances the procurement timeline by specified days.
        /// Any contract whose delivery date is reached transitions to Fulfilled,
        /// improving supplier relationship and notifying subscribers.
        /// </summary>
        public IReadOnlyList<SupplierContract> AdvanceDays(int days, GameDate currentDate)
        {
            var newlyFulfilled = new List<SupplierContract>();

            foreach (var contract in _contracts)
            {
                if (contract.Status == ContractStatus.PendingDelivery && currentDate >= contract.DeliveryDate)
                {
                    contract.MarkFulfilled(contract.DeliveryDate);
                    newlyFulfilled.Add(contract);

                    if (_companies.TryGetValue(contract.SupplierId, out var company))
                    {
                        company.ContractsFulfilled++;
                        // Successful fulfillment improves relationship (+2%)
                        company.AdjustRelationship(0.02);
                        OnRelationshipChanged?.Invoke(company.Id, company.RelationshipScore);
                    }

                    OnContractFulfilled?.Invoke(contract);
                }
            }

            return newlyFulfilled;
        }

        /// <summary>
        /// Reverts contract status transitions that occurred after <paramref name="previousDate"/>.
        /// Used by AdvanceTimeCommand Undo to maintain state determinism and symmetry.
        /// </summary>
        public void UndoAdvance(GameDate previousDate)
        {
            foreach (var contract in _contracts)
            {
                if (contract.Status == ContractStatus.Fulfilled &&
                    contract.FulfilledDate.HasValue &&
                    contract.FulfilledDate.Value > previousDate)
                {
                    contract.RevertState(ContractStatus.PendingDelivery, null);

                    if (_companies.TryGetValue(contract.SupplierId, out var company))
                    {
                        company.ContractsFulfilled = Math.Max(0, company.ContractsFulfilled - 1);
                        company.AdjustRelationship(-0.02);
                        OnRelationshipChanged?.Invoke(company.Id, company.RelationshipScore);
                    }
                }
            }
        }

        /// <summary>
        /// Registers a newly signed contract in the calendar schedule.
        /// </summary>
        public void AddContract(SupplierContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));

            _contracts.Add(contract);

            if (_companies.TryGetValue(contract.SupplierId, out var company))
            {
                company.ContractsSigned++;
                company.TotalSpendMUSD += contract.CostMUSD;
            }

            OnContractSigned?.Invoke(contract);
        }

        /// <summary>
        /// Removes a contract directly (used during Undo of SignContractCommand).
        /// </summary>
        public bool RemoveContract(string contractId)
        {
            var c = GetContract(contractId);
            if (c == null) return false;

            _contracts.Remove(c);

            if (_companies.TryGetValue(c.SupplierId, out var company))
            {
                company.ContractsSigned = Math.Max(0, company.ContractsSigned - 1);
                company.TotalSpendMUSD = Math.Max(0.0, company.TotalSpendMUSD - c.CostMUSD);
            }

            return true;
        }

        /// <summary>
        /// Cancels a pending contract, applying a relationship penalty.
        /// </summary>
        public bool CancelContract(string contractId, double relationshipPenalty = 0.05)
        {
            var c = GetContract(contractId);
            if (c == null || c.Status != ContractStatus.PendingDelivery) return false;

            c.MarkCancelled();

            if (_companies.TryGetValue(c.SupplierId, out var company))
            {
                company.AdjustRelationship(-Math.Abs(relationshipPenalty));
                OnRelationshipChanged?.Invoke(company.Id, company.RelationshipScore);
            }

            OnContractCancelled?.Invoke(c);
            return true;
        }

        /// <summary>
        /// Evaluates a vehicle blueprint against all supplier concentrations.
        /// </summary>
        public SingleSourceRiskReport EvaluateBlueprint(Blueprint blueprint) =>
            SingleSourceRiskEvaluator.Evaluate(blueprint);
    }
}
