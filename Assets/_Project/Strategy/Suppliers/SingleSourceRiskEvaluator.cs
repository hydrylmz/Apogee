using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure functional calculation engine that assesses single-source supplier concentration,
    /// calculates volume discounts, and determines systemic risk ratings for vehicles (GDD §9.1).
    /// </summary>
    public static class SingleSourceRiskEvaluator
    {
        public const double ModerateThreshold = 0.50;
        public const double HighThreshold     = 0.60;
        public const double CriticalThreshold = 0.80;

        public const double ModerateDiscount = 0.05;  // 5% volume discount
        public const double HighDiscount     = 0.075; // 7.5% volume discount
        public const double CriticalDiscount = 0.10;  // 10% volume discount

        /// <summary>
        /// Evaluates a vehicle blueprint and determines supplier distribution,
        /// volume discount rate, and concentration vulnerability.
        /// </summary>
        public static SingleSourceRiskReport Evaluate(Blueprint blueprint)
        {
            if (blueprint == null)
            {
                return CreateEmptyReport();
            }

            var components = ExtractComponents(blueprint);
            return Evaluate(components);
        }

        /// <summary>
        /// Evaluates an arbitrary collection of design components.
        /// </summary>
        public static SingleSourceRiskReport Evaluate(IEnumerable<DesignComponent> components)
        {
            if (components == null)
            {
                return CreateEmptyReport();
            }

            var list = components.Where(c => c != null && c.Supplier != null && !string.IsNullOrWhiteSpace(c.Supplier.Id)).ToList();
            if (list.Count == 0)
            {
                return CreateEmptyReport();
            }

            var countDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in list)
            {
                string sId = c.Supplier.Id;
                countDict[sId] = countDict.TryGetValue(sId, out int current) ? current + 1 : 1;
            }

            int total = list.Count;
            var shareDict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            string dominantSupplierId = null;
            int maxCount = 0;

            foreach (var kvp in countDict)
            {
                double share = (double)kvp.Value / total;
                shareDict[kvp.Key] = share;
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    dominantSupplierId = kvp.Key;
                }
            }

            double dominantShare = (double)maxCount / total;

            // Single item designs cannot suffer systemic cascade concentration
            if (total < 2)
            {
                return new SingleSourceRiskReport(
                    totalComponents: total,
                    countBySupplier: countDict,
                    shareBySupplier: shareDict,
                    dominantSupplierId: dominantSupplierId,
                    dominantSupplierShare: dominantShare,
                    hasConcentration: false,
                    volumeDiscountPercent: 0.0,
                    riskLevel: SingleSourceRiskLevel.None,
                    summaryText: "Çeşitlendirilmiş tedarik (tek bileşen)",
                    riskExplanation: "Tekil bileşende tek kaynak yoğunlaşma riski bulunmamaktadır.");
            }

            SingleSourceRiskLevel riskLevel;
            double discount;
            string summary;
            string explanation;

            if (dominantShare >= CriticalThreshold)
            {
                riskLevel = SingleSourceRiskLevel.Critical;
                discount = CriticalDiscount;
                summary = $"Kritik Tek Kaynak Riski ({dominantSupplierId}: {dominantShare:P0})";
                explanation = $"Bileşenlerin %{dominantShare * 100:F0}'i tek bir şirketten ({dominantSupplierId}) temin ediliyor. %{discount * 100:F0} toplu sipariş indirimi kazanıldı; ancak tedarikçi kaynaklı grev, kalite sorunu veya gecikmeler tüm misyonu durdurabilir.";
            }
            else if (dominantShare >= HighThreshold)
            {
                riskLevel = SingleSourceRiskLevel.High;
                discount = HighDiscount;
                summary = $"Yüksek Tedarikçi Yoğunluğu ({dominantSupplierId}: {dominantShare:P0})";
                explanation = $"Bileşenlerin %{dominantShare * 100:F0}'i {dominantSupplierId} tarafından sağlanıyor. %{discount * 100:F1} hacim indirimi sağlandı; gecikme ve arıza riski belirgin şekilde arttı.";
            }
            else if (dominantShare >= ModerateThreshold)
            {
                riskLevel = SingleSourceRiskLevel.Moderate;
                discount = ModerateDiscount;
                summary = $"Orta Tedarikçi Yoğunluğu ({dominantSupplierId}: {dominantShare:P0})";
                explanation = $"Bileşenlerin %{dominantShare * 100:F0}'i {dominantSupplierId} tarafından sağlanıyor. %{discount * 100:F0} hacim indirimi elde edildi; risk dengeli seviyede.";
            }
            else if (dominantShare >= 0.35)
            {
                riskLevel = SingleSourceRiskLevel.Low;
                discount = 0.0;
                summary = "Düşük Tedarikçi Yoğunluğu";
                explanation = "Bileşenler büyük oranda dengeli dağıtılmış; tedarik zinciri riski düşük.";
            }
            else
            {
                riskLevel = SingleSourceRiskLevel.None;
                discount = 0.0;
                summary = "Çeşitlendirilmiş Tedarik Zinciri";
                explanation = "Tedarikçiler çeşitlendirilmiş. Tek bir şirketteki aksaklık tüm sistemi kilitlemez.";
            }

            return new SingleSourceRiskReport(
                totalComponents: total,
                countBySupplier: countDict,
                shareBySupplier: shareDict,
                dominantSupplierId: dominantSupplierId,
                dominantSupplierShare: dominantShare,
                hasConcentration: dominantShare >= ModerateThreshold,
                volumeDiscountPercent: discount,
                riskLevel: riskLevel,
                summaryText: summary,
                riskExplanation: explanation);
        }

        private static List<DesignComponent> ExtractComponents(Blueprint bp)
        {
            var result = new List<DesignComponent>();
            if (bp == null) return result;

            if (bp.Payload != null)
            {
                result.AddRange(bp.Payload.AllComponents.Where(c => c != null));
            }

            if (bp.FairingAdapter != null)
            {
                result.Add(bp.FairingAdapter);
            }

            if (bp.Stages != null)
            {
                foreach (var stage in bp.Stages)
                {
                    if (stage == null) continue;
                    if (stage.Engine != null) result.Add(stage.Engine);
                    if (stage.Tank != null) result.Add(stage.Tank);
                    if (stage.ExtraComponents != null)
                    {
                        result.AddRange(stage.ExtraComponents.Where(c => c != null));
                    }
                }
            }

            return result;
        }

        private static SingleSourceRiskReport CreateEmptyReport()
        {
            return new SingleSourceRiskReport(
                totalComponents: 0,
                countBySupplier: new Dictionary<string, int>(),
                shareBySupplier: new Dictionary<string, double>(),
                dominantSupplierId: string.Empty,
                dominantSupplierShare: 0.0,
                hasConcentration: false,
                volumeDiscountPercent: 0.0,
                riskLevel: SingleSourceRiskLevel.None,
                summaryText: "Bileşen bulunamadı",
                riskExplanation: "Analiz edilecek takılı donanım parçası mevcut değil.");
        }
    }
}
