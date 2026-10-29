using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Pure C# factory that converts a <see cref="ValidationReport"/> (or raw
    /// <see cref="BlueprintStats"/>) into a ready-to-display list of
    /// <see cref="ResultBarViewModel"/> objects (GDD §8.3, §7.4).
    ///
    /// Design rules:
    /// - No formulas or raw coefficients are surfaced; only outcome values in plain language.
    /// - Status (Good / Warning / Error) is driven by the validator findings so the Results Panel
    ///   and Design Review always agree.
    /// - Fill ratio semantics: closer to 1 = closer to goal/limit. For "more is better" stats
    ///   (e.g. Delta-V, Reliability) fill = actual/target capped at 1.
    ///   For "less is better headroom" stats (e.g. Mass vs capacity) fill = 1 - excess_ratio.
    /// No UnityEngine references.
    /// </summary>
    public static class ResultsPanelViewModel
    {
        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the ordered list of result bars from a full validation report.
        /// Bars are ordered according to GDD §14.3 / §8.3 display priority.
        /// </summary>
        public static IReadOnlyList<ResultBarViewModel> Build(ValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return Build(report.Stats, report);
        }

        /// <summary>
        /// Builds bars from raw stats only (no validator data — useful when validation hasn't
        /// run yet, e.g. during the ghost-delta preview phase).
        /// All bars will default to Neutral or derive status from thresholds.
        /// </summary>
        public static IReadOnlyList<ResultBarViewModel> Build(BlueprintStats stats)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            return Build(stats, null);
        }

        // ── Internal builder ───────────────────────────────────────────────────────

        private static IReadOnlyList<ResultBarViewModel> Build(BlueprintStats stats, ValidationReport report)
        {
            var bars = new List<ResultBarViewModel>(7);

            bars.Add(BuildDeltaVBar(stats, report));
            bars.Add(BuildMassBar(stats, report));
            bars.Add(BuildReliabilityBar(stats, report));
            bars.Add(BuildPowerBar(stats, report));
            bars.Add(BuildThermalBar(stats, report));
            bars.Add(BuildCostBar(stats, report));
            bars.Add(BuildBuildTimeBar(stats, report));

            return bars;
        }

        // ── Individual bar builders ────────────────────────────────────────────────

        /// <summary>
        /// Menzil (Δv) bar — GDD §8.1, §8.3.
        /// Fill = actual / required, capped at 1. Red when < 1, amber within 5 % margin.
        /// </summary>
        private static ResultBarViewModel BuildDeltaVBar(BlueprintStats stats, ValidationReport report)
        {
            const string key   = "delta_v";
            const string label = "Menzil (Δv)";

            double actual   = stats.NetDeltaV;
            // Retrieve required Δv from validator findings (TargetValue stored by DeltaVValidator)
            double required = report?.Context?.MissionProfile?.RequiredDeltaV ?? 0.0;

            bool hasError = ReportHasError(report, "delta_v");
            bool hasWarn  = ReportHasWarning(report, "delta_v");

            string valueText;
            float  fill;
            ResultBarStatus status;
            string reason;

            if (required <= 0)
            {
                // No mission profile — show raw delta-v without comparison
                fill      = (float)Math.Min(actual / 15000.0, 1.0);
                valueText = $"{actual:N0} m/s";
                status    = actual > 0 ? ResultBarStatus.Good : ResultBarStatus.Neutral;
                reason    = "Hedef menzil profili atanmadı; mutlak Δv gösteriliyor.";
            }
            else
            {
                double ratio = required > 0 ? actual / required : 0.0;
                fill = (float)Math.Min(ratio, 1.0);
                valueText = $"{actual:N0} / {required:N0} m/s";

                if (hasError || ratio < 0.98)
                {
                    status = ResultBarStatus.Error;
                    reason = "Roket bu hedefe ulaşmak için yeterli menzile sahip değil. Daha verimli motor, daha fazla yakıt veya daha hafif yük gerekebilir.";
                }
                else if (hasWarn || ratio < 1.05)
                {
                    status = ResultBarStatus.Warning;
                    reason = "Menzil yeterli ancak marj dardır. Küçük bir kütle artışı veya kayıp tahmini değişikliği sorun yaratabilir.";
                }
                else
                {
                    status = ResultBarStatus.Good;
                    reason = "Roket hedef menzili rahat karşılıyor.";
                }
            }

            return Create(key, label, fill, status, valueText, reason);
        }

        /// <summary>
        /// Kütle (Islak) bar — GDD §8.1.
        /// Fill represents used capacity fraction. Red when over launch capacity.
        /// </summary>
        private static ResultBarViewModel BuildMassBar(BlueprintStats stats, ValidationReport report)
        {
            const string key   = "mass";
            const string label = "Kütle (Islak)";

            double wetMass  = stats.WetMass;
            double capacity = stats.MaxPayloadCapacity;

            bool hasError = ReportHasError(report, "mass_capacity") || ReportHasError(report, "payload_capacity");
            bool hasWarn  = ReportHasWarning(report, "mass_capacity") || ReportHasWarning(report, "payload_capacity");

            string valueText = capacity > 0
                ? $"{wetMass:0.##} t / {capacity:0.##} t"
                : $"{wetMass:0.##} t";

            float fill;
            ResultBarStatus status;
            string reason;

            if (capacity <= 0)
            {
                fill   = (float)Math.Min(wetMass / 200.0, 1.0);
                status = ResultBarStatus.Neutral;
                reason = "Fırlatıcı kapasitesi henüz hesaplanamadı.";
            }
            else
            {
                double ratio = wetMass / (capacity + wetMass); // fraction of total launch mass that's wet
                fill = (float)Math.Min(wetMass / (capacity * 1.2), 1.0); // fill against 120% capacity so "full" bar = overloaded

                if (hasError || wetMass > capacity)
                {
                    status = ResultBarStatus.Error;
                    reason = "Araç kütlesi fırlatıcı kapasitesini aşıyor. Daha hafif bileşen seçin ya da daha güçlü bir fırlatıcı tasarlayın.";
                }
                else if (hasWarn || (capacity - wetMass) / capacity < 0.10)
                {
                    status = ResultBarStatus.Warning;
                    reason = "Kütle kapasiteye çok yakın; küçük bir bileşen değişikliği sınırı aşabilir.";
                }
                else
                {
                    status = ResultBarStatus.Good;
                    reason = "Araç kütlesi fırlatıcı kapasitesi içinde rahat bir marjla sığıyor.";
                }
            }

            return Create(key, label, fill, status, valueText, reason);
        }

        /// <summary>
        /// Güvenilirlik bar — GDD §8.1.
        /// Fill = reliability (0-1). Red below mission threshold.
        /// </summary>
        private static ResultBarViewModel BuildReliabilityBar(BlueprintStats stats, ValidationReport report)
        {
            const string key   = "reliability";
            const string label = "Güvenilirlik";

            double rel     = stats.Reliability;
            bool hasError  = ReportHasError(report, "reliability");
            bool hasWarn   = ReportHasWarning(report, "reliability");

            string valueText = $"%{rel * 100.0:0.#}";
            float  fill      = (float)Math.Min(rel, 1.0);

            ResultBarStatus status;
            string reason;

            if (hasError || rel < 0.85)
            {
                status = ResultBarStatus.Error;
                reason = "Güvenilirlik çok düşük. Kalitesiz tedarikçi bileşeni veya test eksikliği başarısızlık olasılığını yükseltiyor. Daha güvenilir bileşen veya daha iyi tedarikçi seçin.";
            }
            else if (hasWarn || rel < 0.92)
            {
                status = ResultBarStatus.Warning;
                reason = "Güvenilirlik kabul edilebilir ama risk yüksek. Bileşen testleri veya daha güvenilir tedarikçi seçimi güvenilirliği artırır.";
            }
            else
            {
                status = ResultBarStatus.Good;
                reason = "Güvenilirlik iyi seviyede; başarılı fırlatma olasılığı yüksek.";
            }

            return Create(key, label, fill, status, valueText, reason);
        }

        /// <summary>
        /// Güç Dengesi bar — GDD §8.1.
        /// Positive margin = good, negative = power deficit (red).
        /// Fill represents generation fraction vs consumption.
        /// </summary>
        private static ResultBarViewModel BuildPowerBar(BlueprintStats stats, ValidationReport report)
        {
            const string key   = "power";
            const string label = "Güç Dengesi";

            double gen   = stats.PowerGeneration;
            double cons  = stats.PowerConsumption;
            double margin = stats.PowerMargin;  // gen - cons

            bool hasError = ReportHasError(report, "power");
            bool hasWarn  = ReportHasWarning(report, "power");

            string valueText = $"{margin:+0.##;-0.##;0} kW";
            float fill;
            ResultBarStatus status;
            string reason;

            double total = gen + Math.Abs(cons);
            fill = total > 0 ? (float)Math.Min(gen / total, 1.0) : 0.5f;

            if (hasError || margin < 0)
            {
                status = ResultBarStatus.Error;
                reason = "Güç açığı var; araç ürettiğinden fazla tüketiyor. Daha büyük güneş paneli / RTG ekleyin ya da bazı sistemleri kapatın.";
            }
            else if (hasWarn || margin < gen * 0.10)
            {
                status = ResultBarStatus.Warning;
                reason = "Güç marjı dar; gölge dönemlerinde veya yüksek güç gerektiren operasyonlarda açık oluşabilir.";
            }
            else
            {
                status = ResultBarStatus.Good;
                reason = "Güç dengesi yeterli; üretim tüketimi rahatça karşılıyor.";
            }

            return Create(key, label, fill, status, valueText, reason);
        }

        /// <summary>
        /// Isıl Denge bar — GDD §8.1.
        /// Fill = dissipation fraction vs load. Red when dissipation < load.
        /// </summary>
        private static ResultBarViewModel BuildThermalBar(BlueprintStats stats, ValidationReport report)
        {
            const string key   = "thermal";
            const string label = "Isıl Denge";

            double load   = stats.ThermalLoad;
            double diss   = stats.ThermalDissipation;
            double margin = stats.ThermalMargin;  // diss - load

            bool hasError = ReportHasError(report, "thermal");
            bool hasWarn  = ReportHasWarning(report, "thermal");

            string valueText = $"{margin:+0.##;-0.##;0} kW";
            float fill;
            ResultBarStatus status;
            string reason;

            double total = load + diss;
            fill = total > 0 ? (float)Math.Min(diss / total, 1.0) : 0.5f;

            if (hasError || margin < 0)
            {
                status = ResultBarStatus.Error;
                reason = "Isıl kapasitesi yetersiz; araç ısınmaya devam eder. Radyatör ekleyin veya termal yükü azaltın.";
            }
            else if (hasWarn || margin < load * 0.10)
            {
                status = ResultBarStatus.Warning;
                reason = "Isıl marj dar; yoğun çalışma sırasında termal limit aşılabilir.";
            }
            else
            {
                status = ResultBarStatus.Good;
                reason = "Isıl sistem dengeli; ısı yönetimi yeterli.";
            }

            return Create(key, label, fill, status, valueText, reason);
        }

        /// <summary>
        /// Maliyet bar — GDD §8.1. Relative cost fill (no hard cap; informational).
        /// </summary>
        private static ResultBarViewModel BuildCostBar(BlueprintStats stats, ValidationReport report)
        {
            const string key   = "cost";
            const string label = "Maliyet";
            const double softCap = 200.0; // M$ — informational upper bound for fill calculation

            double cost   = stats.Cost;
            float  fill   = (float)Math.Min(cost / softCap, 1.0);

            // Cost has no hard validator in M2 scope; use thresholds
            ResultBarStatus status;
            string reason;

            if (cost > softCap)
            {
                status = ResultBarStatus.Warning;
                reason = "Maliyet çok yüksek; bütçe kısıtlarını kontrol edin.";
            }
            else if (cost > softCap * 0.75)
            {
                status = ResultBarStatus.Warning;
                reason = "Maliyet üst sınıra yaklaşıyor.";
            }
            else
            {
                status = ResultBarStatus.Good;
                reason = "Maliyet bütçe hedeflerinizle uyumlu görünüyor.";
            }

            string valueText = $"${cost:0.##} M";
            return Create(key, label, fill, status, valueText, reason);
        }

        /// <summary>
        /// Üretim Süresi bar — GDD §8.1. Informational; no hard cap.
        /// </summary>
        private static ResultBarViewModel BuildBuildTimeBar(BlueprintStats stats, ValidationReport report)
        {
            const string key   = "build_time";
            const string label = "Üretim Süresi";
            const int    softCap = 730; // days (~2 years) as fill reference

            int days  = stats.BuildTimeDays;
            float fill = (float)Math.Min((double)days / softCap, 1.0);

            ResultBarStatus status = days > softCap ? ResultBarStatus.Warning : ResultBarStatus.Good;
            string reason = days > softCap
                ? "Üretim süresi çok uzun; fırlatma penceresi kaçabilir. Daha az sayıda bileşen veya hızlı tedarikçi seçin."
                : "Üretim süresi makul aralıkta.";

            string valueText = days >= 365
                ? $"{days / 365} yıl {days % 365} gün"
                : $"{days} gün";

            return Create(key, label, fill, status, valueText, reason);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static ResultBarViewModel Create(string key, string label, float fill, ResultBarStatus status, string valueText, string reason)
        {
            return new ResultBarViewModel(key, label)
            {
                FillRatio  = fill,
                Status     = status,
                ValueText  = valueText,
                ReasonText = reason ?? string.Empty
            };
        }

        private static bool ReportHasError(ValidationReport report, string ruleIdSubstring)
        {
            if (report == null) return false;
            foreach (var issue in report.Issues)
            {
                if (issue.IsError &&
                    issue.RuleId.IndexOf(ruleIdSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ReportHasWarning(ValidationReport report, string ruleIdSubstring)
        {
            if (report == null) return false;
            foreach (var issue in report.Issues)
            {
                if (issue.IsWarning &&
                    issue.RuleId.IndexOf(ruleIdSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
