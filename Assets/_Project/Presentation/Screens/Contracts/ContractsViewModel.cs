using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Contracts
{
    /// <summary>
    /// ViewModel for an individual supplier company card (GDD §9.1).
    /// </summary>
    public sealed class SupplierCompanyItemViewModel
    {
        public string Id { get; }
        public string Name { get; }
        public string BrandColor { get; }
        public double RelationshipScore { get; }
        public string RelationshipPercent => $"%{RelationshipScore * 100:F0}";
        public string Strengths { get; }
        public string Weaknesses { get; }
        public string SpecialCapabilities { get; }
        public int ActiveContractsCount { get; }

        public SupplierCompanyItemViewModel(SupplierCompanyState company, int activeContracts)
        {
            if (company == null) throw new ArgumentNullException(nameof(company));

            Id = company.Id;
            Name = FormatSupplierName(company.Id, company.Record?.NameKey);
            BrandColor = company.Record?.BrandColor ?? "#4A90D9";
            RelationshipScore = Math.Clamp(company.RelationshipScore, 0.0, 1.0);
            Strengths = company.Record?.StrengthTags != null && company.Record.StrengthTags.Count > 0
                ? string.Join(", ", company.Record.StrengthTags)
                : "Standart";
            Weaknesses = company.Record?.WeaknessTags != null && company.Record.WeaknessTags.Count > 0
                ? string.Join(", ", company.Record.WeaknessTags)
                : "Yok";
            SpecialCapabilities = company.Record?.SpecialCapabilities != null && company.Record.SpecialCapabilities.Count > 0
                ? string.Join(", ", company.Record.SpecialCapabilities)
                : "Yok";
            ActiveContractsCount = activeContracts;
        }

        public static string FormatSupplierName(string id, string nameKey)
        {
            if (!string.IsNullOrEmpty(nameKey))
            {
                if (nameKey.Contains("HELION")) return "Helion Dynamics";
                if (nameKey.Contains("KESTREL")) return "Kestrel Aerospace";
                if (nameKey.Contains("NORDVIK")) return "Nordvik Propulsion";
                if (nameKey.Contains("TARSUS")) return "Tarsus Mühendislik";
                if (nameKey.Contains("MERIDIAN")) return "Meridian Systems";
                if (nameKey.Contains("AURELIA")) return "Aurelia Labs";
            }

            return id switch
            {
                "helion-dynamics" => "Helion Dynamics",
                "kestrel-aerospace" => "Kestrel Aerospace",
                "nordvik-propulsion" => "Nordvik Propulsion",
                "tarsus-muhendislik" => "Tarsus Mühendislik",
                "meridian-systems" => "Meridian Systems",
                "aurelia-labs" => "Aurelia Labs",
                _ => id
            };
        }
    }

    /// <summary>
    /// ViewModel for an active or past supplier contract (GDD §9.1).
    /// </summary>
    public sealed class SupplierContractItemViewModel
    {
        public string Id { get; }
        public string SupplierId { get; }
        public string SupplierName { get; }
        public string ItemName { get; }
        public string Category { get; }
        public double CostMUSD { get; }
        public ContractStatus Status { get; }
        public string StatusName { get; }
        public string DeliveryDate { get; }
        public int DeliveryDays { get; }
        public bool CanCancel { get; }

        public SupplierContractItemViewModel(SupplierContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));

            Id = contract.Id;
            SupplierId = contract.SupplierId;
            SupplierName = SupplierCompanyItemViewModel.FormatSupplierName(contract.SupplierId, null);
            ItemName = contract.ItemName;
            Category = contract.Category;
            CostMUSD = contract.CostMUSD;
            Status = contract.Status;
            StatusName = GetStatusName(contract.Status);
            DeliveryDate = contract.DeliveryDate.ToString();
            DeliveryDays = contract.DeliveryDays;
            CanCancel = contract.Status == ContractStatus.PendingDelivery;
        }

        public static string GetStatusName(ContractStatus status) => status switch
        {
            ContractStatus.PendingDelivery => "Teslim Bekleniyor",
            ContractStatus.Fulfilled => "OK Teslim Edildi",
            ContractStatus.Cancelled => "X İptal Edildi",
            _ => status.ToString()
        };
    }

    /// <summary>
    /// ViewModel for an available procurement contract offer (GDD §9.1).
    /// </summary>
    public sealed class ContractOfferItemViewModel
    {
        public string OfferId { get; }
        public string SupplierId { get; }
        public string SupplierName { get; }
        public string ItemName { get; }
        public string Category { get; }
        public double BaseCost { get; }
        public int DeliveryDays { get; }
        public double ReliabilityMultiplier { get; }

        public ContractOfferItemViewModel(
            string offerId,
            string supplierId,
            string itemName,
            string category,
            double cost,
            int deliveryDays,
            double relMult = 1.0)
        {
            OfferId = offerId;
            SupplierId = supplierId;
            SupplierName = SupplierCompanyItemViewModel.FormatSupplierName(supplierId, null);
            ItemName = itemName;
            Category = category;
            BaseCost = cost;
            DeliveryDays = deliveryDays;
            ReliabilityMultiplier = relMult;
        }
    }

    /// <summary>
    /// ViewModel for the Strategic Contracts & Suppliers Screen (ScreenId.Contracts = 9).
    /// Complies with GDD §9.1, §14.1 (#8 Şirketler/Sözleşmeler), and M9-05.
    /// Exposes suppliers, contracts, single source risks, and dispatches SignContract commands.
    /// </summary>
    public sealed class ContractsViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private CampaignState _campaign;

        public ObservableValue<double> Budget { get; } = new(0.0);
        public ObservableValue<string> RiskLevelText { get; } = new("Düşük Risk");
        public ObservableValue<string> RiskSummaryText { get; } = new("Tedarik riski dengeli.");
        public ObservableValue<int> ActiveContractsCount { get; } = new(0);

        public ObservableValue<IReadOnlyList<SupplierCompanyItemViewModel>> Companies { get; } =
            new(Array.Empty<SupplierCompanyItemViewModel>());

        public ObservableValue<IReadOnlyList<SupplierContractItemViewModel>> Contracts { get; } =
            new(Array.Empty<SupplierContractItemViewModel>());

        public ObservableValue<IReadOnlyList<ContractOfferItemViewModel>> AvailableOffers { get; } =
            new(Array.Empty<ContractOfferItemViewModel>());

        public ObservableValue<SupplierCompanyItemViewModel> SelectedCompany { get; } = new(null);
        public ObservableValue<string> StatusMessage { get; } = new("Hazır.");

        public ViewActionCommand BackCommand { get; }

        public CampaignState Campaign => _campaign;

        public ContractsViewModel(IScreenNavigator navigator, CampaignState campaign = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            BackCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.CampaignMap));

            if (campaign != null)
            {
                BindCampaign(campaign);
            }
        }

        public void BindCampaign(CampaignState campaign)
        {
            if (_campaign?.Suppliers != null)
            {
                _campaign.Suppliers.OnContractSigned -= HandleContractUpdated;
                _campaign.Suppliers.OnContractFulfilled -= HandleContractUpdated;
                _campaign.Suppliers.OnContractCancelled -= HandleContractUpdated;
                _campaign.Suppliers.OnRelationshipChanged -= HandleRelationshipUpdated;
            }

            _campaign = campaign;

            if (_campaign?.Suppliers != null)
            {
                _campaign.Suppliers.OnContractSigned += HandleContractUpdated;
                _campaign.Suppliers.OnContractFulfilled += HandleContractUpdated;
                _campaign.Suppliers.OnContractCancelled += HandleContractUpdated;
                _campaign.Suppliers.OnRelationshipChanged += HandleRelationshipUpdated;
            }

            Refresh();
        }

        private void HandleContractUpdated(SupplierContract contract) => Refresh();
        private void HandleRelationshipUpdated(string supplierId, double val) => Refresh();

        public void Refresh()
        {
            if (_campaign == null)
            {
                Budget.Value = 0.0;
                RiskLevelText.Value = "Risk: Bilinmiyor";
                RiskSummaryText.Value = "Kampanya verisi yok.";
                Companies.Value = Array.Empty<SupplierCompanyItemViewModel>();
                Contracts.Value = Array.Empty<SupplierContractItemViewModel>();
                AvailableOffers.Value = Array.Empty<ContractOfferItemViewModel>();
                return;
            }

            Budget.Value = _campaign.Budget;

            if (_campaign.Suppliers != null)
            {
                // Single source risk — use a campaign-level summary (no active blueprint to evaluate)
                // We compute a simple supplier concentration index from active contracts
                int totalContracts = _campaign.Suppliers.AllContracts.Count(c => c.Status == ContractStatus.PendingDelivery);
                var bySupplier = _campaign.Suppliers.AllContracts
                    .Where(c => c.Status == ContractStatus.PendingDelivery)
                    .GroupBy(c => c.SupplierId)
                    .ToDictionary(g => g.Key, g => g.Count());

                string dominantId = null;
                double dominantShare = 0.0;
                if (totalContracts > 0 && bySupplier.Count > 0)
                {
                    var maxEntry = bySupplier.OrderByDescending(kv => kv.Value).First();
                    dominantId = maxEntry.Key;
                    dominantShare = (double)maxEntry.Value / totalContracts;
                }

                string riskDisplay;
                string riskSummary;
                if (totalContracts == 0)
                {
                    riskDisplay = "Risk: Yok";
                    riskSummary = "Aktif tedarik sözleşmesi bulunmuyor.";
                }
                else if (dominantShare >= 0.80)
                {
                    riskDisplay = "KRİTİK Risk";
                    riskSummary = $"{SupplierCompanyItemViewModel.FormatSupplierName(dominantId, null)} sözleşmelerin %{dominantShare * 100:F0}'ını oluşturuyor — kritik tek kaynak riski.";
                }
                else if (dominantShare >= 0.60)
                {
                    riskDisplay = "YÜKSEK Risk";
                    riskSummary = $"{SupplierCompanyItemViewModel.FormatSupplierName(dominantId, null)} sözleşmelerin %{dominantShare * 100:F0}'ını oluşturuyor — yüksek konsantrasyon.";
                }
                else if (dominantShare >= 0.50)
                {
                    riskDisplay = "ORTA Risk";
                    riskSummary = $"Tedarik kısmen konsantre. İndirim mevcut ama gecikme riski var.";
                }
                else
                {
                    riskDisplay = "Düşük Risk";
                    riskSummary = "Tedarik çeşitlilendirilmiş — tek kaynak riski düşük.";
                }
                RiskLevelText.Value = riskDisplay;
                RiskSummaryText.Value = riskSummary;

                // Contracts
                var contractsList = _campaign.Suppliers.AllContracts
                    .Select(c => new SupplierContractItemViewModel(c))
                    .OrderBy(c => c.Status == ContractStatus.PendingDelivery ? 0 : 1)
                    .ThenBy(c => c.DeliveryDate)
                    .ToList();

                Contracts.Value = contractsList;
                ActiveContractsCount.Value = _campaign.Suppliers.PendingContracts.Count();

                // Companies
                var compList = new List<SupplierCompanyItemViewModel>();
                foreach (var comp in _campaign.Suppliers.AllCompanies)
                {
                    int activeCount = _campaign.Suppliers.PendingContracts.Count(c => c.SupplierId == comp.Id);
                    compList.Add(new SupplierCompanyItemViewModel(comp, activeCount));
                }
                Companies.Value = compList;

                if (SelectedCompany.Value != null)
                {
                    SelectedCompany.Value = compList.FirstOrDefault(c => c.Id == SelectedCompany.Value.Id) ?? compList.FirstOrDefault();
                }
                else
                {
                    SelectedCompany.Value = compList.FirstOrDefault();
                }

                // Generate standard offers based on suppliers
                GenerateStandardOffers(compList);
            }
        }

        public void SelectCompany(string supplierId)
        {
            if (string.IsNullOrEmpty(supplierId)) return;
            var match = Companies.Value.FirstOrDefault(c => string.Equals(c.Id, supplierId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedCompany.Value = match;
            }
        }

        private void GenerateStandardOffers(List<SupplierCompanyItemViewModel> companies)
        {
            var offers = new List<ContractOfferItemViewModel>();

            foreach (var comp in companies)
            {
                string sId = comp.Id;
                if (sId.Contains("helion"))
                {
                    offers.Add(new ContractOfferItemViewModel($"offer_helion_engine_{_campaign.Time.Tick}", sId, "Kerosen Motor Grubu (H-1)", "engine", 25.0, 15, 0.88));
                    offers.Add(new ContractOfferItemViewModel($"offer_helion_tank_{_campaign.Time.Tick}", sId, "Hafif Yakıt Tankı (T-100)", "tank", 15.0, 10, 0.90));
                }
                else if (sId.Contains("kestrel"))
                {
                    offers.Add(new ContractOfferItemViewModel($"offer_kestrel_crew_{_campaign.Time.Tick}", sId, "İnsanlı Komuta Kapsülü (K-IV)", "cabin", 60.0, 35, 1.20));
                    offers.Add(new ContractOfferItemViewModel($"offer_kestrel_engine_{_campaign.Time.Tick}", sId, "Yüksek Güvenilirlikli Motor (K-9)", "engine", 45.0, 30, 1.15));
                }
                else if (sId.Contains("nordvik"))
                {
                    offers.Add(new ContractOfferItemViewModel($"offer_nordvik_upper_{_campaign.Time.Tick}", sId, "Vakum Hidrojen Motoru (NV-3)", "engine", 35.0, 20, 1.05));
                }
                else if (sId.Contains("tarsus"))
                {
                    offers.Add(new ContractOfferItemViewModel($"offer_tarsus_struct_{_campaign.Time.Tick}", sId, "Karbon Kompozit Gövde (TR-C)", "structure", 20.0, 12, 1.0));
                }
                else if (sId.Contains("meridian"))
                {
                    offers.Add(new ContractOfferItemViewModel($"offer_meridian_bus_{_campaign.Time.Tick}", sId, "Aviyonik & Bus Paketi (MB-2)", "avionics", 30.0, 18, 1.10));
                }
                else if (sId.Contains("aurelia"))
                {
                    offers.Add(new ContractOfferItemViewModel($"offer_aurelia_science_{_campaign.Time.Tick}", sId, "Spektrometre & Bilim Sensörü (AL-X)", "science", 25.0, 25, 1.15));
                }
            }

            AvailableOffers.Value = offers;
        }

        public bool SignContract(ContractOfferItemViewModel offer)
        {
            if (offer == null) return false;
            string contractId = $"CTR-{offer.SupplierId.ToUpper()[..Math.Min(4, offer.SupplierId.Length)]}-{_campaign.Time.Tick}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            return SignContract(contractId, offer.SupplierId, offer.ItemName, offer.Category, offer.BaseCost, offer.DeliveryDays, offer.ReliabilityMultiplier);
        }

        public bool SignContract(
            string contractId,
            string supplierId,
            string itemName,
            string category,
            double costMUSD,
            int deliveryDays,
            double reliabilityMultiplier = 1.0)
        {
            if (_campaign == null)
            {
                StatusMessage.Value = "Hata: Kampanya durumu bulunamadı.";
                return false;
            }

            var cmd = new SignContractCommand(contractId, supplierId, itemName, category, costMUSD, deliveryDays, reliabilityMultiplier: reliabilityMultiplier);
            var result = _campaign.Execute(cmd, PlayerRole.ProcurementDirector);

            if (result.Success)
            {
                StatusMessage.Value = $"Sözleşme imzalandı: {itemName} ({supplierId})";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"İmzalanamadı: {result.Message}";
                return false;
            }
        }

        public bool CancelContract(string contractId)
        {
            if (_campaign == null) return false;

            var cmd = new CancelContractCommand(contractId);
            var result = _campaign.Execute(cmd, PlayerRole.ProcurementDirector);

            if (result.Success)
            {
                StatusMessage.Value = $"Sözleşme iptal edildi: {contractId}";
                Refresh();
                return true;
            }
            else
            {
                StatusMessage.Value = $"İptal edilemedi: {result.Message}";
                return false;
            }
        }

        public override void Dispose()
        {
            if (_campaign?.Suppliers != null)
            {
                _campaign.Suppliers.OnContractSigned -= HandleContractUpdated;
                _campaign.Suppliers.OnContractFulfilled -= HandleContractUpdated;
                _campaign.Suppliers.OnContractCancelled -= HandleContractUpdated;
                _campaign.Suppliers.OnRelationshipChanged -= HandleRelationshipUpdated;
            }
            base.Dispose();
        }
    }
}
