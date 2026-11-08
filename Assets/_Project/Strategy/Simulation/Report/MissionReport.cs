using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Scoring;
using Apogee.Design;

namespace Apogee.Core.Simulation.Report
{
    /// <summary>
    /// Represents a single node or link in the mission cause-and-effect explanatory chain.
    /// Used to clearly narrate why a mission succeeded, degraded, or failed (GDD §10.5, §13.2).
    /// </summary>
    public sealed class CauseEffectNode
    {
        public string Phase { get; }
        public string Title { get; }
        public string Explanation { get; }
        public bool IsNegativeOrFault { get; }

        public CauseEffectNode(string phase, string title, string explanation, bool isNegativeOrFault = false)
        {
            Phase = phase ?? "Genel";
            Title = title ?? string.Empty;
            Explanation = explanation ?? string.Empty;
            IsNegativeOrFault = isNegativeOrFault;
        }

        public override string ToString() => $"[{Phase}] {Title}: {Explanation}";
    }

    /// <summary>
    /// Result assessment of an individual mission success criterion.
    /// Complies with GDD §5.5, §10.5.
    /// </summary>
    public sealed class CriterionEvaluationResult
    {
        public MissionSuccessCriterion Criterion { get; }
        public bool IsMet { get; }
        public double ActualValue { get; }
        public string SummaryText { get; }

        public CriterionEvaluationResult(MissionSuccessCriterion criterion, bool isMet, double actualValue, string summaryText)
        {
            Criterion = criterion ?? throw new ArgumentNullException(nameof(criterion));
            IsMet = isMet;
            ActualValue = actualValue;
            SummaryText = summaryText ?? string.Empty;
        }

        public override string ToString() => $"[{(IsMet ? "BAŞARILI" : "BAŞARISIZ")}] {Criterion.Description} (Gerçekleşen: {ActualValue:0.#})";
    }

    /// <summary>
    /// Evaluated science data delivery and data bottleneck summary.
    /// GDD §10.5 ("Bilim teslimi: Yere inen veri vs üretilen veri").
    /// </summary>
    public sealed class ScienceDeliverySummary
    {
        public double TotalGeneratedGb { get; }
        public double TotalDownlinkedGb { get; }
        public double TotalDroppedGb { get; }
        public double DeliveryRatio { get; } // 0.0 - 1.0
        public bool HasDataBottleneck { get; }
        public string BottleneckAnalysis { get; }

        public ScienceDeliverySummary(
            double totalGeneratedGb,
            double totalDownlinkedGb,
            double totalDroppedGb)
        {
            TotalGeneratedGb = Math.Max(0.0, totalGeneratedGb);
            TotalDownlinkedGb = Math.Max(0.0, totalDownlinkedGb);
            TotalDroppedGb = Math.Max(0.0, totalDroppedGb);

            DeliveryRatio = TotalGeneratedGb > 0.001
                ? Math.Min(1.0, TotalDownlinkedGb / TotalGeneratedGb)
                : (TotalDownlinkedGb > 0.0 ? 1.0 : 0.0);

            HasDataBottleneck = TotalDroppedGb > 0.1 || (TotalGeneratedGb > 0.5 && DeliveryRatio < 0.65);

            if (TotalDroppedGb > 0.1)
            {
                BottleneckAnalysis = $"Veri Darboğazı: On-board depolama tamponu taştı ve {TotalDroppedGb:F1} GB bilimsel veri downlink edilemeden kayboldu. İletişim anteni aktarım hızı veya yer istasyonu görüş süresi aletlerin üretim hızına yetişemedi.";
            }
            else if (HasDataBottleneck)
            {
                BottleneckAnalysis = "Kısıtlı Downlink: Aletlerin ürettiği verilerin önemli bir kısmı Dünya'ya indirilemedi; iletişim bant genişliği sınırlayıcı etken oldu.";
            }
            else
            {
                BottleneckAnalysis = "Veri Akışı Dengeli: Üretilen bilim verilerinin tamamına yakını başarıyla Dünya'ya indirildi; depolama ve downlink kapasitesi yeterliydi.";
            }
        }
    }

    /// <summary>
    /// Calculated campaign rewards or penalties resulting from the mission.
    /// GDD §4.3, §10.5 ("Kampanya etkisi: Bütçe, itibar, araştırma, ders").
    /// </summary>
    public sealed class MissionCampaignImpact
    {
        public double BudgetChange { get; }
        public double ReputationChange { get; }
        public double ResearchPointsGranted { get; }
        public double KnowledgePointsGranted { get; }
        public string SummaryText { get; }

        public MissionCampaignImpact(
            double budgetChange,
            double reputationChange,
            double researchPointsGranted,
            double knowledgePointsGranted,
            string summaryText)
        {
            BudgetChange = budgetChange;
            ReputationChange = reputationChange;
            ResearchPointsGranted = researchPointsGranted;
            KnowledgePointsGranted = knowledgePointsGranted;
            SummaryText = summaryText ?? string.Empty;
        }
    }

    /// <summary>
    /// Complete post-mission evaluation and diagnostic report.
    /// Complies with GDD §10.5 (Sonuç Raporu) and §13.2 ("Neden?" İpuçları).
    /// </summary>
    public sealed class MissionReport
    {
        public string MissionId { get; }
        public string MissionName { get; }
        public string DestinationId { get; }
        public bool IsOverallSuccess { get; }
        public string FinalStatusSummary { get; }

        public IReadOnlyList<CriterionEvaluationResult> CriteriaResults { get; }
        public ScienceDeliverySummary ScienceSummary { get; }
        public IReadOnlyList<CauseEffectNode> CauseAndEffectChain { get; }
        public MissionCampaignImpact CampaignImpact { get; }
        public IReadOnlyList<string> LessonsLearned { get; }
        public MissionScoreBreakdown ScoreBreakdown { get; }

        public MissionReport(
            string missionId,
            string missionName,
            string destinationId,
            bool isOverallSuccess,
            string finalStatusSummary,
            IEnumerable<CriterionEvaluationResult> criteriaResults,
            ScienceDeliverySummary scienceSummary,
            IEnumerable<CauseEffectNode> causeAndEffectChain,
            MissionCampaignImpact campaignImpact,
            IEnumerable<string> lessonsLearned,
            MissionScoreBreakdown scoreBreakdown = null)
        {
            MissionId = missionId ?? "mission_unspecified";
            MissionName = missionName ?? "Misyon";
            DestinationId = destinationId ?? "Bilinmeyen Hedef";
            IsOverallSuccess = isOverallSuccess;
            FinalStatusSummary = finalStatusSummary ?? string.Empty;
            CriteriaResults = criteriaResults != null ? criteriaResults.ToList() : new List<CriterionEvaluationResult>();
            ScienceSummary = scienceSummary ?? new ScienceDeliverySummary(0, 0, 0);
            CauseAndEffectChain = causeAndEffectChain != null ? causeAndEffectChain.ToList() : new List<CauseEffectNode>();
            CampaignImpact = campaignImpact ?? new MissionCampaignImpact(0, 0, 0, 0, string.Empty);
            LessonsLearned = lessonsLearned != null ? lessonsLearned.ToList() : new List<string>();
            ScoreBreakdown = scoreBreakdown;
        }

        public override string ToString() =>
            $"[MissionReport:{MissionId}] {(IsOverallSuccess ? "BAŞARILI" : "BAŞARISIZ")} - {FinalStatusSummary} (Kriterler: {CriteriaResults.Count(c => c.IsMet)}/{CriteriaResults.Count})";
    }
}
