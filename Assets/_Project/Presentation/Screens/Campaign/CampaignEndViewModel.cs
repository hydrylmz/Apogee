using System.Globalization;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Campaign
{
    /// <summary>
    /// ViewModel for the Campaign End / Legacy Report screen (GDD §4.1, §4.5, §21 M6-02).
    /// Binds the <see cref="CampaignEndReport"/> to UI-ready string/bool properties.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public sealed class CampaignEndViewModel : ViewModelBase
    {
        private readonly CampaignState _campaign;

        // ---------------------------------------------------------------- observables
        /// <summary>Localized badge text: "CAMPAIGN ENDED", "AGENCY RETIRED", "VICTORY", etc.</summary>
        public ObservableValue<string> EndStatusBadgeText    { get; } = new ObservableValue<string>(string.Empty);

        /// <summary>Flavour conclusion paragraph from the evaluator.</summary>
        public ObservableValue<string> ConclusionText        { get; } = new ObservableValue<string>(string.Empty);

        public ObservableValue<string> AgencyNameText        { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> YearsActiveText       { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> MissionStatsText      { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> MilestoneProgressText { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> ScienceDataText       { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> FinalBudgetText       { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> FinalReputationText   { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> CrewLossText          { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> KeyAchievementsText   { get; } = new ObservableValue<string>(string.Empty);

        /// <summary>True when the campaign ended in total victory.</summary>
        public ObservableValue<bool> IsVictory               { get; } = new ObservableValue<bool>(false);

        // ---------------------------------------------------------------- construction
        public CampaignEndViewModel(CampaignState campaign)
        {
            _campaign = campaign;
            Refresh();
        }

        // ---------------------------------------------------------------- refresh
        /// <summary>Re-reads the campaign's end report and updates all observables.</summary>
        public void Refresh()
        {
            if (_campaign == null) return;

            CampaignEndReport report = _campaign.EndReport;
            CampaignEndStatus status = _campaign.EndStatus;

            AgencyNameText.Value = _campaign.AgencyName;

            EndStatusBadgeText.Value = StatusBadge(status);
            IsVictory.Value = status == CampaignEndStatus.AllMilestonesCompleted;

            if (report == null)
            {
                ConclusionText.Value        = "Campaign still active.";
                YearsActiveText.Value       = "—";
                MissionStatsText.Value      = "—";
                MilestoneProgressText.Value = "—";
                ScienceDataText.Value       = "—";
                FinalBudgetText.Value       = "—";
                FinalReputationText.Value   = "—";
                CrewLossText.Value          = "—";
                KeyAchievementsText.Value   = string.Empty;
                return;
            }

            ConclusionText.Value = report.ConclusionSummary;

            YearsActiveText.Value = report.TotalYearsActive == 1
                ? "1 year"
                : $"{report.TotalYearsActive} years";

            MissionStatsText.Value =
                $"{report.TotalMissionsSucceeded} / {report.TotalMissionsLaunched} successful " +
                $"({(report.MissionSuccessRate * 100.0):F0}%)";

            MilestoneProgressText.Value =
                $"{report.MilestonesClaimedCount} / {report.TotalMilestonesCount}";

            ScienceDataText.Value =
                report.TotalScienceDataDownlinkedGb.ToString("F1", CultureInfo.InvariantCulture) + " GB";

            FinalBudgetText.Value =
                report.FinalBudget.ToString("F0", CultureInfo.InvariantCulture) + " M$";

            FinalReputationText.Value =
                report.FinalReputation.ToString("F0", CultureInfo.InvariantCulture) + " / 100";

            CrewLossText.Value = report.CrewLossIncidents == 0
                ? "None"
                : report.CrewLossIncidents.ToString(CultureInfo.InvariantCulture);

            if (report.KeyAchievements.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var a in report.KeyAchievements)
                    sb.AppendLine("• " + a);
                KeyAchievementsText.Value = sb.ToString().TrimEnd();
            }
            else
            {
                KeyAchievementsText.Value = "No special achievements recorded.";
            }
        }

        // ---------------------------------------------------------------- helpers
        private static string StatusBadge(CampaignEndStatus status)
        {
            switch (status)
            {
                case CampaignEndStatus.Bankruptcy:          return "AGENCY DISBANDED";
                case CampaignEndStatus.TimeExpired:         return "CAMPAIGN CONCLUDED";
                case CampaignEndStatus.AllMilestonesCompleted: return "VICTORY";
                case CampaignEndStatus.VoluntaryRetirement: return "AGENCY RETIRED";
                default:                                    return "CAMPAIGN ACTIVE";
            }
        }
    }
}
