using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Apogee.Core;
using Apogee.Design;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;
using Apogee.UI.Screens.Composer;

namespace Apogee.UI.Screens.Dossier
{
    /// <summary>
    /// Requirement item displayed in the Dossier Checklist.
    /// Represents mandatory components, system checks, or capability gates.
    /// </summary>
    public sealed class DossierRequirementItem
    {
        public string Title { get; }
        public string Category { get; }
        public string Description { get; }
        public bool IsMandatory { get; }
        public string StatusBadge { get; }
        public bool IsSatisfied { get; }

        public DossierRequirementItem(
            string title,
            string category,
            string description,
            bool isMandatory = true,
            string statusBadge = "ZORUNLU",
            bool isSatisfied = false)
        {
            Title = title ?? string.Empty;
            Category = category ?? "Genel";
            Description = description ?? string.Empty;
            IsMandatory = isMandatory;
            StatusBadge = statusBadge ?? (isMandatory ? "ZORUNLU" : "ÖNERİLEN");
            IsSatisfied = isSatisfied;
        }
    }

    /// <summary>
    /// Research requirement item displayed with unlocked status and "Araştırmaya Ekle" action.
    /// GDD §5.4 and §21 M3-05.
    /// </summary>
    public sealed class DossierResearchItem
    {
        public string ResearchId { get; }
        public string Title { get; }
        public string Description { get; }
        public bool IsUnlocked { get; }
        public bool IsInQueue { get; set; }

        public DossierResearchItem(string researchId, string title, string description, bool isUnlocked, bool isInQueue = false)
        {
            ResearchId = researchId ?? string.Empty;
            Title = title ?? researchId ?? string.Empty;
            Description = description ?? string.Empty;
            IsUnlocked = isUnlocked;
            IsInQueue = isInQueue;
        }
    }

    /// <summary>
    /// ViewModel for the Mission Dossier Screen (Misyon Dosyası).
    /// GDD §5.4 & §5.5, §14.1 item 6, §21 M3-05.
    /// Displays comprehensive requirements:
    ///   - Delta-V range requirement breakdown
    ///   - Mandatory modules checklist
    ///   - Research requirements with lock status and queue dispatch
    ///   - Reliability threshold, data volume & comms latency
    ///   - Launch window schedule & build/cost estimates
    ///   - Measurable success criteria
    /// </summary>
    public class MissionDossierViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private readonly HashSet<string> _unlockedResearchIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _researchQueue = new();

        public IScreenNavigator Navigator => _navigator;
        public IReadOnlyList<string> ResearchQueue => _researchQueue;

        // ── Navigation Context & Active Profile ──────────────────────────────────
        public ObservableValue<MissionProfile> Profile { get; } = new(null);
        public ObservableValue<MissionCompositionResultArgs> CompositionArgs { get; } = new(null);

        // ── Dossier Header & Overview Observables ────────────────────────────────
        public ObservableValue<string> MissionName { get; } = new("İsimsiz Misyon");
        public ObservableValue<string> DestinationLabel { get; } = new("-");
        public ObservableValue<string> MissionTypeLabel { get; } = new("-");
        public ObservableValue<string> ClaimLevelBadge { get; } = new("DÜŞÜK");
        public ObservableValue<string> ClaimLevelCssClass { get; } = new("claim-badge--low");
        public ObservableValue<string> RewardRiskSummary { get; } = new("-");

        // ── Telemetry & Budgets ──────────────────────────────────────────────────
        public ObservableValue<double> RequiredDeltaV { get; } = new(0.0);
        public ObservableValue<string> RequiredDeltaVText { get; } = new("0 m/s");
        public ObservableValue<double> MinReliabilityThreshold { get; } = new(0.80);
        public ObservableValue<string> ReliabilityThresholdText { get; } = new("%80");
        public ObservableValue<int> RequiredLaunchPadLevel { get; } = new(1);
        public ObservableValue<string> LaunchPadLevelText { get; } = new("Seviye 1 (Küçük Rampa)");
        public ObservableValue<double> MinDownlinkRate { get; } = new(0.5);
        public ObservableValue<double> MinDataStorage { get; } = new(2.0);
        public ObservableValue<string> TelemetrySpecsText { get; } = new("-");

        // ── Schedules, Windows & Estimates ───────────────────────────────────────
        public ObservableValue<string> LaunchWindowInfoText { get; } = new("-");
        public ObservableValue<string> TransitDurationText { get; } = new("-");
        public ObservableValue<string> CommDelayText { get; } = new("-");
        public ObservableValue<string> EstimatedCostText { get; } = new("-");
        public ObservableValue<string> EstimatedBuildTimeText { get; } = new("-");
        public ObservableValue<string> BudgetCeilingText { get; } = new("Sınırsız");

        // ── Collections: Requirements, Research, Success Criteria ────────────────
        public ObservableValue<IReadOnlyList<DossierRequirementItem>> MandatoryRequirements { get; } =
            new(Array.Empty<DossierRequirementItem>());

        public ObservableValue<IReadOnlyList<DossierResearchItem>> ResearchRequirements { get; } =
            new(Array.Empty<DossierResearchItem>());

        public ObservableValue<IReadOnlyList<MissionSuccessCriterion>> SuccessCriteria { get; } =
            new(Array.Empty<MissionSuccessCriterion>());

        public ObservableValue<int> LockedResearchCount { get; } = new(0);
        public ObservableValue<int> QueuedResearchCount { get; } = new(0);

        // ── Navigation & Action Commands ─────────────────────────────────────────
        public ViewActionCommand GoToWorkshopCommand { get; }
        public ViewActionCommand BackToComposerCommand { get; }
        public ViewActionCommand QueueAllLockedResearchCommand { get; }

        public event Action<string> OnResearchQueued;

        public MissionDossierViewModel(
            IScreenNavigator navigator = null,
            IEnumerable<string> unlockedResearch = null)
        {
            _navigator = navigator;

            if (unlockedResearch != null)
            {
                foreach (var r in unlockedResearch)
                {
                    if (!string.IsNullOrWhiteSpace(r))
                    {
                        _unlockedResearchIds.Add(r.Trim());
                    }
                }
            }

            GoToWorkshopCommand = new ViewActionCommand(ExecuteGoToWorkshop);
            BackToComposerCommand = new ViewActionCommand(ExecuteBackToComposer);
            QueueAllLockedResearchCommand = new ViewActionCommand(QueueAllLockedResearch);
        }

        public void SetUnlockedResearch(IEnumerable<string> researchIds)
        {
            _unlockedResearchIds.Clear();
            if (researchIds != null)
            {
                foreach (var id in researchIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        _unlockedResearchIds.Add(id.Trim());
                    }
                }
            }
            RefreshResearchItems();
        }

        public void LoadProfile(MissionProfile profile, MissionCompositionResultArgs args = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            Profile.Value = profile;
            CompositionArgs.Value = args;

            // Overview
            MissionName.Value = !string.IsNullOrWhiteSpace(profile.Name) ? profile.Name : profile.Id;
            DestinationLabel.Value = ResolveDestinationDisplayName(profile.TargetDestinationId);
            MissionTypeLabel.Value = ResolveMissionTypeDisplayName(profile.TargetMissionTypeId);

            ClaimLevelBadge.Value = !string.IsNullOrWhiteSpace(profile.ClaimLevel) ? profile.ClaimLevel.ToUpperInvariant() : "DÜŞÜK";
            ClaimLevelCssClass.Value = GetClaimCssClass(profile.ClaimLevel);
            RewardRiskSummary.Value = $"Ödül: {profile.RewardMultiplier:P0} ({profile.RewardSummary}) · Risk: {profile.RiskMultiplier:P0} ({profile.RiskSummary})";

            // Telemetry & Engineering Bars
            RequiredDeltaV.Value = profile.RequiredDeltaV;
            RequiredDeltaVText.Value = $"{profile.RequiredDeltaV:N0} m/s";

            MinReliabilityThreshold.Value = profile.MinReliabilityThreshold;
            ReliabilityThresholdText.Value = $"%{(profile.MinReliabilityThreshold * 100).ToString("F0", CultureInfo.InvariantCulture)}";

            RequiredLaunchPadLevel.Value = profile.RequiredLaunchPadLevel;
            LaunchPadLevelText.Value = $"Seviye {profile.RequiredLaunchPadLevel} ({GetLaunchPadTierName(profile.RequiredLaunchPadLevel)})";

            MinDownlinkRate.Value = profile.MinDownlinkRate;
            MinDataStorage.Value = profile.MinDataStorage;
            TelemetrySpecsText.Value = $"Depolama: ≥ {profile.MinDataStorage.ToString("F0", CultureInfo.InvariantCulture)} GB · İndirme: ≥ {profile.MinDownlinkRate.ToString("F1", CultureInfo.InvariantCulture)} GB/gün";

            // Timelines & Windows
            LaunchWindowInfoText.Value = profile.SelectedWindowIndex == 0
                ? "İlk Fırlatma Penceresi (Hemen / En Erken)"
                : $"{profile.SelectedWindowIndex + 1}. Fırlatma Penceresi (+{profile.SelectedWindowIndex * profile.WindowPeriodDays} gün gecikmeli)";

            TransitDurationText.Value = $"{profile.TransitDaysMin} – {profile.TransitDaysMax} gün ({profile.MinDurationDays} gün toplam ömür)";
            CommDelayText.Value = profile.CommDelaySecondsMax <= 0.05
                ? "Anlık (<0.05 sn)"
                : $"{profile.CommDelaySecondsMin.ToString("F1", CultureInfo.InvariantCulture)} – {profile.CommDelaySecondsMax.ToString("F1", CultureInfo.InvariantCulture)} sn";

            EstimatedCostText.Value = $"{profile.EstimatedCostMin.ToString("F1", CultureInfo.InvariantCulture)}M$ – {profile.EstimatedCostMax.ToString("F1", CultureInfo.InvariantCulture)}M$";
            EstimatedBuildTimeText.Value = $"{profile.EstimatedBuildTimeDaysMin} – {profile.EstimatedBuildTimeDaysMax} gün";
            BudgetCeilingText.Value = profile.BudgetCeiling.HasValue ? $"{profile.BudgetCeiling.Value.ToString("F1", CultureInfo.InvariantCulture)}M$" : "Sınırsız";

            // Collections
            BuildMandatoryRequirements(profile);
            RefreshResearchItems();

            SuccessCriteria.Value = profile.SuccessCriteria != null ? profile.SuccessCriteria.ToList() : Array.Empty<MissionSuccessCriterion>();
        }

        public bool QueueResearch(string researchId)
        {
            if (string.IsNullOrWhiteSpace(researchId)) return false;

            if (_unlockedResearchIds.Contains(researchId))
            {
                return false; // Already unlocked
            }

            if (!_researchQueue.Contains(researchId, StringComparer.OrdinalIgnoreCase))
            {
                _researchQueue.Add(researchId);
                OnResearchQueued?.Invoke(researchId);
                RefreshResearchItems();
                return true;
            }

            return false;
        }

        public void QueueAllLockedResearch()
        {
            var currentResearch = ResearchRequirements.Value;
            if (currentResearch == null) return;

            bool anyAdded = false;
            foreach (var item in currentResearch)
            {
                if (!item.IsUnlocked && !_researchQueue.Contains(item.ResearchId, StringComparer.OrdinalIgnoreCase))
                {
                    _researchQueue.Add(item.ResearchId);
                    OnResearchQueued?.Invoke(item.ResearchId);
                    anyAdded = true;
                }
            }

            if (anyAdded)
            {
                RefreshResearchItems();
            }
        }

        private void BuildMandatoryRequirements(MissionProfile profile)
        {
            var reqs = new List<DossierRequirementItem>();

            // 1. Delta-V & Pad
            reqs.Add(new DossierRequirementItem(
                title: "Menzil & İtki (Δv)",
                category: "Yörünge / İtki",
                description: $"Görev toplamda en az {profile.RequiredDeltaV:N0} m/s hız değişimi sağlamalıdır.",
                statusBadge: "ZORUNLU"
            ));

            reqs.Add(new DossierRequirementItem(
                title: $"Fırlatma Rampası (Seviye {profile.RequiredLaunchPadLevel})",
                category: "Altyapı",
                description: $"Roket toplam kütlesi için en az Seviye {profile.RequiredLaunchPadLevel} ({GetLaunchPadTierName(profile.RequiredLaunchPadLevel)}) gereklidir.",
                statusBadge: "ZORUNLU"
            ));

            // 2. Mandatory modules
            if (profile.RequiresEdl)
            {
                reqs.Add(new DossierRequirementItem(
                    title: "EDL / Atmosfer İniş Paketi",
                    category: "Uzay Aracı (EDL)",
                    description: "Hedef yüzeye güvenli iniş için ısı kalkanı, paraşüt veya skycrane/retro iniş donanımı zorunludur.",
                    statusBadge: "ZORUNLU"
                ));
            }

            if (profile.RequiresLifeSupport)
            {
                reqs.Add(new DossierRequirementItem(
                    title: "Yaşam Destek & Mürettebat Kabini",
                    category: "İnsanlı Sistemler",
                    description: "Mürettebatlı görev için basınçlı kabin, kapalı döngü yaşam desteği ve acil fırlatma kaçış (abort) sistemi zorunludur.",
                    statusBadge: "ZORUNLU"
                ));
            }

            if (profile.RequiredModuleCategories != null)
            {
                foreach (var modCat in profile.RequiredModuleCategories)
                {
                    if (string.Equals(modCat, "edl", StringComparison.OrdinalIgnoreCase) && profile.RequiresEdl) continue;
                    if (string.Equals(modCat, "life-support", StringComparison.OrdinalIgnoreCase) && profile.RequiresLifeSupport) continue;

                    reqs.Add(new DossierRequirementItem(
                        title: $"Zorunlu Sistem: {modCat.ToUpperInvariant()}",
                        category: "Donanım / Modül",
                        description: $"Misyon profili gereği araç üzerinde '{modCat}' kategorisine ait onaylı bir modül bulunmalıdır.",
                        statusBadge: "ZORUNLU"
                    ));
                }
            }

            // 3. Instruments
            if (profile.RequiredInstrumentCategories != null && profile.RequiredInstrumentCategories.Count > 0)
            {
                foreach (var inst in profile.RequiredInstrumentCategories)
                {
                    reqs.Add(new DossierRequirementItem(
                        title: $"Bilimsel Enstrüman: {inst.ToUpperInvariant()}",
                        category: "Bilimsel Yük",
                        description: $"Seçilen bilimsel amaçların verilerini toplamak için '{inst}' sensör paketi monte edilmelidir.",
                        statusBadge: "ZORUNLU"
                    ));
                }
            }

            // 4. Reliability Threshold
            reqs.Add(new DossierRequirementItem(
                title: $"Güvenilirlik Eşiği (≥ %{(profile.MinReliabilityThreshold * 100).ToString("F0", CultureInfo.InvariantCulture)})",
                category: "Kalite & Güvenlik",
                description: $"Görev karmaşıklığı ve hedefin risk seviyesi gereği tümleşik güvenilirlik en az %{(profile.MinReliabilityThreshold * 100).ToString("F0", CultureInfo.InvariantCulture)} olmalıdır.",
                statusBadge: "KRİTİK"
            ));

            MandatoryRequirements.Value = reqs;
        }

        private void RefreshResearchItems()
        {
            var profile = Profile.Value;
            if (profile == null || profile.RequiredResearchIds == null || profile.RequiredResearchIds.Count == 0)
            {
                ResearchRequirements.Value = Array.Empty<DossierResearchItem>();
                LockedResearchCount.Value = 0;
                QueuedResearchCount.Value = 0;
                return;
            }

            var list = new List<DossierResearchItem>();
            int locked = 0;
            int queued = 0;

            foreach (var rId in profile.RequiredResearchIds)
            {
                bool unlocked = _unlockedResearchIds.Contains(rId);
                bool inQueue = _researchQueue.Contains(rId, StringComparer.OrdinalIgnoreCase);

                if (!unlocked) locked++;
                if (inQueue) queued++;

                list.Add(new DossierResearchItem(
                    researchId: rId,
                    title: ResolveResearchTitle(rId),
                    description: ResolveResearchDescription(rId),
                    isUnlocked: unlocked,
                    isInQueue: inQueue
                ));
            }

            ResearchRequirements.Value = list;
            LockedResearchCount.Value = locked;
            QueuedResearchCount.Value = queued;
        }

        private void ExecuteGoToWorkshop()
        {
            var profile = Profile.Value;
            if (profile == null) return;

            // Route to Workshop with context
            _navigator?.GoTo(ScreenId.Workshop, profile);
        }

        private void ExecuteBackToComposer()
        {
            _navigator?.GoTo(ScreenId.MissionComposer);
        }

        // ── String Helpers ────────────────────────────────────────────────────────

        public static string ResolveDestinationDisplayName(string destinationId) => destinationId?.ToLowerInvariant() switch
        {
            "leo" => "Alçak Dünya Yörüngesi (LEO)",
            "moon" => "Ay (Luna)",
            "venus" => "Venüs",
            "mars" => "Mars",
            "europa" => "Europa (Jüpiter Uydusu)",
            _ => destinationId?.ToUpperInvariant() ?? "Bilinmeyen Hedef"
        };

        public static string ResolveMissionTypeDisplayName(string typeId) => typeId?.ToLowerInvariant() switch
        {
            "orbiter" => "Yörünge Aracı (Orbiter)",
            "lander" => "İniş Aracı (Lander)",
            "rover" => "Yüzey Gezgini (Rover)",
            "sample-return" => "Numune Getirme (Sample Return)",
            "crewed" => "İnsanlı Görev (Crewed)",
            _ => typeId?.ToUpperInvariant() ?? "Bilinmeyen Tip"
        };

        private static string GetClaimCssClass(string claimLevel) => claimLevel?.ToLowerInvariant() switch
        {
            "düşük" or "low" => "claim-badge--low",
            "orta" or "medium" => "claim-badge--medium",
            "yüksek" or "high" => "claim-badge--high",
            "çok yüksek" or "extreme" => "claim-badge--extreme",
            _ => "claim-badge--low"
        };

        private static string GetLaunchPadTierName(int tier) => tier switch
        {
            1 => "Mikro / Hafif",
            2 => "Orta Sınıf",
            3 => "Ağır Sınıf",
            4 => "Süper Ağır",
            5 => "Mega Sınıf (Devasa)",
            _ => "Standart"
        };

        public static string ResolveResearchTitle(string researchId) => researchId switch
        {
            "edl-heavy" => "Ağır EDL & Atmosfer Girişi II",
            "deep-space-comms" => "Derin Uzay İletişimi (X/Ka Bant)",
            "rad-shield-extreme" => "Aşırı Radyasyon Kalkanı & Sertleştirme",
            "life-support-closed" => "Kapalı Döngü Yaşam Destek Sistemi",
            "radiation-shielding" => "Biyolojik Radyasyon Kalkanı",
            "sample-container" => "Steril Numune Kapsülü & Yakalama",
            "aerobraking" => "Aerobraking & Termal Koruma",
            "cryo-thermal" => "Kriyojenik Isıl Yönetim",
            _ => researchId
        };

        public static string ResolveResearchDescription(string researchId) => researchId switch
        {
            "edl-heavy" => "Ağır yüklerin ince veya yoğun atmosferlere paraşüt ve retro-itki ile inişini sağlar.",
            "deep-space-comms" => "Güneş sistemi dış kenarlarıyla yüksek veri hızında çift yönlü iletişim.",
            "rad-shield-extreme" => "Jüpiter/Europa radyasyon kuşaklarında aviyoniklerin ve sistemlerin hayatta kalmasını sağlar.",
            "life-support-closed" => "Uzun süreli insanlı uçuşlarda oksijen ve suyun %95+ geri dönüşümünü sağlar.",
            "radiation-shielding" => "Derin uzay kozmik ışınlarına ve güneş patlamalarına karşı mürettebatı korur.",
            "sample-container" => "Gezegen yüzeyinden alınan kaya ve toprak örneklerinin Dünya'ya kirlenmeden taşınmasını sağlar.",
            "aerobraking" => "Atmosfere sürtünerek yakıt harcamadan yörüngeye oturma manevrası teknolojisi.",
            "cryo-thermal" => "Sıvı hidrojen ve metanın aylarca kaynamadan depolanmasını sağlayan aktif soğutma.",
            _ => "Bu misyonun gereksinimlerini karşılamak için araştırılması gereken teknoloji dalı."
        };
    }
}
