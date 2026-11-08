using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Core.Simulation.Scoring;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Report
{
    /// <summary>
    /// ViewModel for the Post-Mission Report screen (ScreenId.MissionReport).
    /// Binds mission success criteria, downlink vs generated science data, cause-and-effect narrative,
    /// campaign rewards/penalties, and navigation actions back to the Workshop or Campaign Map.
    ///
    /// Complies with GDD §10.5, §13.2, §14.1 #16, and §21 M5-06.
    /// </summary>
    public sealed class MissionReportViewModel : ViewModelBase
    {
        public IScreenNavigator Navigator { get; set; }
        public CampaignState Campaign { get; set; }

        // ---------------------------------------------------------------- Observable State
        public ObservableValue<string> MissionName { get; } = new ObservableValue<string>("Misyon Raporu");
        public ObservableValue<string> DestinationText { get; } = new ObservableValue<string>("HEDEF");
        public ObservableValue<string> OverallResultBadgeText { get; } = new ObservableValue<string>("DEĞERLENDİRİLİYOR");
        public ObservableValue<bool> IsOverallSuccess { get; } = new ObservableValue<bool>(false);
        public ObservableValue<string> FinalStatusNarrative { get; } = new ObservableValue<string>(string.Empty);

        // Science telemetry
        public ObservableValue<string> ScienceDeliveryRatioText { get; } = new ObservableValue<string>("0 / 0 GB (%0)");
        public ObservableValue<string> ScienceBottleneckNarrative { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<bool> HasScienceBottleneck { get; } = new ObservableValue<bool>(false);

        // Campaign impact
        public ObservableValue<string> CampaignImpactText { get; } = new ObservableValue<string>(string.Empty);

        // ---------------------------------------------------------------- Scoring & Medals (M5-08)
        public ObservableValue<string> FinalScoreText { get; } = new ObservableValue<string>("0");
        public ObservableValue<string> GradeText { get; } = new ObservableValue<string>("-");
        public ObservableValue<string> ScorePillarsSummaryText { get; } = new ObservableValue<string>(string.Empty);
        public List<AwardedMedal> AwardedMedalsList { get; } = new List<AwardedMedal>();

        // Lists
        public List<CriterionEvaluationResult> CriteriaList { get; } = new List<CriterionEvaluationResult>();
        public List<CauseEffectNode> CauseAndEffectNodes { get; } = new List<CauseEffectNode>();
        public List<string> LessonsLearnedList { get; } = new List<string>();

        // Commands
        public ViewActionCommand ReturnToWorkshopCommand { get; }
        public ViewActionCommand ReturnToMapCommand { get; }

        // ---------------------------------------------------------------- What-If (M5-07)
        /// <summary>
        /// Embedded What-If panel ViewModel. Populated once a report is bound.
        /// GDD §10.5 ("Ya şöyle olsaydı?"), §13.2, §21 M5-07.
        /// </summary>
        public WhatIfViewModel WhatIf { get; } = new WhatIfViewModel();

        public event Action OnReportDataUpdated;

        public MissionReportViewModel()
        {
            ReturnToWorkshopCommand = new ViewActionCommand(
                () => Navigator?.GoTo(ScreenId.Workshop),
                () => Navigator != null
            );

            ReturnToMapCommand = new ViewActionCommand(
                () => Navigator?.GoTo(ScreenId.CampaignMap),
                () => Navigator != null
            );
        }

        /// <summary>
        /// Populates the ViewModel with a generated or passed MissionReport.
        /// If report is null, attempts to generate it on-the-fly from the mission instance in CampaignState.
        /// </summary>
        public void BindReport(string missionId, MissionReport report = null)
        {
            MissionReport activeReport = report;

            if (activeReport == null && Campaign != null && !string.IsNullOrEmpty(missionId))
            {
                if (Campaign.Missions.TryGetValue(missionId, out var mission))
                {
                    if (mission.Report != null)
                    {
                        activeReport = mission.Report;
                    }
                    else
                    {
                        // Execute completion command on-the-fly
                        var completeCmd = new CompleteMissionCommand(Campaign, missionId);
                        var res = Campaign.Execute(completeCmd);
                        if (res.Success)
                        {
                            activeReport = completeCmd.GeneratedReport;
                        }
                    }
                }
            }

            if (activeReport == null)
            {
                MissionName.Value = "Misyon Raporu (Veri Yok)";
                DestinationText.Value = "-";
                OverallResultBadgeText.Value = "BİLGİ YOK";
                IsOverallSuccess.Value = false;
                FinalStatusNarrative.Value = "Raporlanacak misyon kaydı bulunamadı.";
                CriteriaList.Clear();
                CauseAndEffectNodes.Clear();
                LessonsLearnedList.Clear();
                OnReportDataUpdated?.Invoke();
                return;
            }

            MissionName.Value = activeReport.MissionName;
            DestinationText.Value = activeReport.DestinationId.ToUpperInvariant();
            IsOverallSuccess.Value = activeReport.IsOverallSuccess;
            OverallResultBadgeText.Value = activeReport.IsOverallSuccess ? "GÖREV BAŞARILI" : "GÖREV BAŞARISIZ";
            FinalStatusNarrative.Value = activeReport.FinalStatusSummary;

            // Science delivery summary
            if (activeReport.ScienceSummary != null)
            {
                var sc = activeReport.ScienceSummary;
                ScienceDeliveryRatioText.Value =
                    $"{sc.TotalDownlinkedGb:F1} GB İndirildi / {sc.TotalGeneratedGb:F1} GB Üretildi (%{sc.DeliveryRatio * 100:F0})";
                ScienceBottleneckNarrative.Value = sc.BottleneckAnalysis;
                HasScienceBottleneck.Value = sc.HasDataBottleneck;
            }
            else
            {
                ScienceDeliveryRatioText.Value = "0 GB";
                ScienceBottleneckNarrative.Value = string.Empty;
                HasScienceBottleneck.Value = false;
            }

            // Campaign Impact
            if (activeReport.CampaignImpact != null)
            {
                CampaignImpactText.Value = activeReport.CampaignImpact.SummaryText;
            }
            else
            {
                CampaignImpactText.Value = string.Empty;
            }

            // Criteria
            CriteriaList.Clear();
            if (activeReport.CriteriaResults != null)
            {
                CriteriaList.AddRange(activeReport.CriteriaResults);
            }

            // Cause and Effect Chain
            CauseAndEffectNodes.Clear();
            if (activeReport.CauseAndEffectChain != null)
            {
                CauseAndEffectNodes.AddRange(activeReport.CauseAndEffectChain);
            }

            // Lessons learned
            LessonsLearnedList.Clear();
            if (activeReport.LessonsLearned != null)
            {
                LessonsLearnedList.AddRange(activeReport.LessonsLearned);
            }

            // Scoring and Medals (M5-08)
            AwardedMedalsList.Clear();
            if (activeReport.ScoreBreakdown != null)
            {
                var sb = activeReport.ScoreBreakdown;
                FinalScoreText.Value = $"{sb.FinalScore:F0}";
                GradeText.Value = sb.Grade;
                ScorePillarsSummaryText.Value =
                    $"Bilim: %{sb.ScienceDeliveryScore * 100:F0} | Amaç: %{sb.ObjectiveSuccessScore * 100:F0} | Kaynak: %{sb.ResourceEfficiencyScore * 100:F0} | Karar: %{sb.DecisionQualityScore * 100:F0} | Plan: %{sb.PlanningEfficiencyScore * 100:F0} (Zorluk: {sb.DifficultyMultiplier:F1}x)";

                if (sb.Medals != null)
                {
                    AwardedMedalsList.AddRange(sb.Medals);
                }
            }
            else
            {
                FinalScoreText.Value = "0";
                GradeText.Value = "-";
                ScorePillarsSummaryText.Value = string.Empty;
            }

            // What-If panel (M5-07): bind mission data for re-analysis
            BindWhatIfFromMission(missionId, activeReport);

            OnReportDataUpdated?.Invoke();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Binds the What-If sub-ViewModel after the report has been generated and the
        /// MissionInstance is available. Called from BindReport when Campaign is set.
        /// </summary>
        private void BindWhatIfFromMission(string missionId, MissionReport report)
        {
            if (Campaign == null || string.IsNullOrEmpty(missionId) || report == null) return;
            if (!Campaign.Missions.TryGetValue(missionId, out var mission)) return;
            WhatIf.BindMission(mission, report);
        }
    }
}
