using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apogee.Data;

namespace Apogee.UI.Screens.Research
{
    /// <summary>
    /// ViewModel for one mission destination tab (LEO / Moon / Mars / Venus / Europa)
    /// shown as the destination rail on the Research Tree screen (GDD §16.1, §19.1).
    /// Carries the destination's NASA art, travel stats and hazard profile; the lock
    /// state is recomputed against completed research nodes and campaign reputation,
    /// so the tree can highlight "which technologies are needed to reach this target".
    /// Locked destinations render dark on the rail (HOI4 locked-node feel) until the
    /// required research is completed.
    /// </summary>
    public sealed class DestinationItemViewModel
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        /// <summary>"orbit" (LEO) or "body" (surface / atmospheric target).</summary>
        public string BodyType { get; }
        /// <summary>Resources path of the destination's NASA photo (may be null).</summary>
        public string Art { get; }
        /// <summary>One-line Turkish source caption for <see cref="Art"/> (may be null).</summary>
        public string ArtCredit { get; }

        /// <summary>Research node ids that must be completed before this destination unlocks.</summary>
        public IReadOnlyList<string> UnlockResearchIds { get; }
        /// <summary>Minimum reputation required to unlock this destination (0 = none).</summary>
        public double UnlockReputationMin { get; }

        /// <summary>Total mission Δv budget in km/s (ascent + transfer + arrival + landing + return).</summary>
        public double DvTotalKmS { get; }
        public int TransitDaysMin { get; }
        public int TransitDaysMax { get; }
        /// <summary>Synodic window period in days (0 = launch any time, e.g. LEO).</summary>
        public int WindowPeriodDays { get; }
        public double CommDelaySecondsMin { get; }
        public double CommDelaySecondsMax { get; }
        /// <summary>Hazard tag → 0..1 severity (radiation, heat, pressure, dust, atmosphere).</summary>
        public IReadOnlyDictionary<string, double> Hazards { get; }

        // ── Lock state (recomputed by ResearchTreeViewModel.Refresh) ──────────────

        private readonly HashSet<string> _missingResearchIds = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Required research ids not yet completed (filled by <see cref="UpdateLockState"/>).</summary>
        public IReadOnlyCollection<string> MissingResearchIds => _missingResearchIds;
        /// <summary>True when the given research id is required but not yet completed.</summary>
        public bool IsResearchMissing(string researchId) => _missingResearchIds.Contains(researchId);
        /// <summary>Reputation value observed during the last <see cref="UpdateLockState"/> call.</summary>
        public double CurrentReputation { get; private set; }
        /// <summary>True when every required research node is completed and reputation suffices.</summary>
        public bool IsUnlocked { get; private set; }

        public DestinationItemViewModel(DestinationRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            Id = record.Id;
            DisplayName = ResearchNodeItemViewModel.LocalizeOr(
                record.NameKey, GetFallbackName(record.Id));
            Description = ResearchNodeItemViewModel.LocalizeOr(
                record.DescKey, $"{GetFallbackName(record.Id)} görev hedefi");
            BodyType = record.BodyType;
            Art = record.Art;
            ArtCredit = record.ArtCredit;
            UnlockResearchIds = record.UnlockResearchIds ?? (IReadOnlyList<string>)Array.Empty<string>();
            UnlockReputationMin = record.UnlockReputationMin;

            DvTotalKmS = record.DvTotal / 1000.0;
            TransitDaysMin = record.TransitDaysMin;
            TransitDaysMax = record.TransitDaysMax;
            WindowPeriodDays = record.WindowPeriodDays;
            CommDelaySecondsMin = record.CommDelaySecondsMin;
            CommDelaySecondsMax = record.CommDelaySecondsMax;
            Hazards = record.Hazards ?? new Dictionary<string, double>();

            // No requirements → open from campaign start (LEO, Moon).
            IsUnlocked = UnlockResearchIds.Count == 0 && UnlockReputationMin <= 0.0;
        }

        /// <summary>
        /// Recomputes the lock state. <paramref name="completedResearchIds"/> holds the ids
        /// of completed research nodes; <paramref name="currentReputation"/> is the campaign
        /// reputation (0-100). Safe to call repeatedly — state is cleared each time.
        /// </summary>
        public void UpdateLockState(
            IReadOnlyCollection<string> completedResearchIds,
            double currentReputation)
        {
            if (completedResearchIds == null) throw new ArgumentNullException(nameof(completedResearchIds));

            _missingResearchIds.Clear();
            foreach (var id in UnlockResearchIds)
            {
                if (!completedResearchIds.Contains(id))
                {
                    _missingResearchIds.Add(id);
                }
            }

            CurrentReputation = currentReputation;
            IsUnlocked = _missingResearchIds.Count == 0 && currentReputation >= UnlockReputationMin;
        }

        /// <summary>Turkish one-line lock summary for tooltips ("Gerekli: X · İtibar 30 gerekli").</summary>
        public string BuildLockReason(Func<string, string> researchNameLookup = null)
        {
            var parts = new List<string>();
            if (_missingResearchIds.Count > 0)
            {
                string names = string.Join(", ", _missingResearchIds.Select(id =>
                    researchNameLookup != null ? researchNameLookup(id) : ResearchNodeItemViewModel.PrettifyId(id)));
                parts.Add($"Gerekli teknolojiler: {names}");
            }

            if (CurrentReputation < UnlockReputationMin)
            {
                parts.Add($"İtibar yetersiz ({UnlockReputationMin:F0} gerekli, {CurrentReputation:F0} mevcut)");
            }

            return string.Join(" · ", parts);
        }

        /// <summary>"Δv 21.9 km/s · Transit 180-270 gün" style summary for rail tooltips.</summary>
        public string BuildTravelSummary()
        {
            var sb = new StringBuilder();
            sb.Append($"Δv {DvTotalKmS:F1} km/s");
            sb.Append($" · Transit {FormatDaySpan(TransitDaysMin, TransitDaysMax)}");
            if (WindowPeriodDays > 0)
            {
                sb.Append($" · Pencere ~{WindowPeriodDays} günde bir");
            }

            sb.Append($" · Sinyal {FormatDelaySpan()}");
            return sb.ToString();
        }

        /// <summary>"Radyasyon %70 · Toz %60" hazard summary; empty string when hazard-free.</summary>
        public string BuildHazardSummary()
        {
            if (Hazards == null || Hazards.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" · ", Hazards.OrderBy(h => h.Key, StringComparer.Ordinal).Select(h =>
                $"{GetHazardLabel(h.Key)} %{Math.Round(Math.Clamp(h.Value, 0.0, 1.0) * 100.0)}"));
        }

        /// <summary>"3-22 dk" / "0.001-0.05 sn" one-way signal delay span.</summary>
        public string FormatDelaySpan()
        {
            double min = CommDelaySecondsMin;
            double max = CommDelaySecondsMax;
            if (max >= 60.0)
            {
                return $"{min / 60.0:F0}-{max / 60.0:F0} dk";
            }

            return $"{min:0.###}-{max:0.###} sn";
        }

        // ── Display helpers ───────────────────────────────────────────────────────

        private static string FormatDaySpan(int min, int max)
        {
            return min == max ? $"{max} gün" : $"{min}-{max} gün";
        }

        /// <summary>Turkish fallback names when the locale table is unavailable.</summary>
        private static string GetFallbackName(string id)
        {
            switch (id?.ToLowerInvariant())
            {
                case "leo": return "Dünya Yörüngesi (LEO)";
                case "moon": return "Ay";
                case "mars": return "Mars";
                case "venus": return "Venüs";
                case "europa": return "Europa";
                default: return ResearchNodeItemViewModel.PrettifyId(id);
            }
        }

        /// <summary>Turkish label for a hazard tag key.</summary>
        public static string GetHazardLabel(string hazardKey)
        {
            switch (hazardKey?.ToLowerInvariant())
            {
                case "radiation": return "Radyasyon";
                case "heat": return "Isı";
                case "pressure": return "Basınç";
                case "dust": return "Toz";
                case "atmosphere": return "Atmosfer";
                default: return ResearchNodeItemViewModel.PrettifyId(hazardKey);
            }
        }
    }
}
