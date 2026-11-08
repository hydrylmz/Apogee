using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Campaign
{
    /// <summary>
    /// UI Toolkit Screen Controller for the Campaign End / Legacy Report screen (ScreenId.CampaignEnd).
    /// Displays the final agency legacy report after the campaign concludes:
    ///   - End status badge (Victory / Bankrupt / Time Expired / Retired)
    ///   - Conclusion narrative paragraph
    ///   - Mission statistics, milestone progress, science data
    ///   - Final budget, reputation, crew loss, and key achievements
    ///
    /// GDD §4.1, §4.5, §14.1, §21 M6-02.
    /// </summary>
    public sealed class CampaignEndScreenController : ScreenControllerBase<CampaignEndViewModel>
    {
        public override ScreenId ScreenId => ScreenId.CampaignEnd;

        // ---------------------------------------------------------------- cached UI refs
        private Label _statusBadgeLabel;
        private Label _agencyNameLabel;
        private Label _conclusionLabel;
        private Label _yearsActiveLabel;
        private Label _missionStatsLabel;
        private Label _milestoneProgressLabel;
        private Label _scienceDataLabel;
        private Label _finalBudgetLabel;
        private Label _finalReputationLabel;
        private Label _crewLossLabel;
        private Label _keyAchievementsLabel;

        // ---------------------------------------------------------------- construction
        public CampaignEndScreenController(CampaignEndViewModel viewModel) : base(viewModel) { }

        // ---------------------------------------------------------------- screen lifecycle
        protected override VisualElement CreateView()
        {
            var root = new VisualElement { name = "campaign-end-screen" };
            root.AddToClassList("campaign-end-screen");

            // Status badge row
            var header = new VisualElement { name = "header" };
            header.AddToClassList("screen-header");
            _statusBadgeLabel = new Label { name = "status-badge" };
            _statusBadgeLabel.AddToClassList("campaign-end-badge");
            _agencyNameLabel = new Label { name = "agency-name" };
            _agencyNameLabel.AddToClassList("campaign-end-agency-name");
            header.Add(_statusBadgeLabel);
            header.Add(_agencyNameLabel);
            root.Add(header);

            // Conclusion paragraph
            _conclusionLabel = new Label { name = "conclusion-text" };
            _conclusionLabel.AddToClassList("campaign-end-conclusion");
            root.Add(_conclusionLabel);

            // Stats grid
            var stats = new VisualElement { name = "stats-grid" };
            stats.AddToClassList("campaign-end-stats");
            _yearsActiveLabel       = AddStatRow(stats, "Duration",          "years-active");
            _missionStatsLabel      = AddStatRow(stats, "Missions",          "mission-stats");
            _milestoneProgressLabel = AddStatRow(stats, "Milestones",        "milestone-progress");
            _scienceDataLabel       = AddStatRow(stats, "Science Data",      "science-data");
            _finalBudgetLabel       = AddStatRow(stats, "Final Budget",      "final-budget");
            _finalReputationLabel   = AddStatRow(stats, "Final Reputation",  "final-reputation");
            _crewLossLabel          = AddStatRow(stats, "Crew Incidents",    "crew-loss");
            root.Add(stats);

            // Key achievements
            var achievLabel = new Label("Key Achievements");
            achievLabel.AddToClassList("section-header");
            root.Add(achievLabel);
            _keyAchievementsLabel = new Label { name = "key-achievements" };
            _keyAchievementsLabel.AddToClassList("campaign-end-achievements");
            root.Add(_keyAchievementsLabel);

            return root;
        }

        protected override void OnInitialized()
        {
            BindObservables();
        }

        /// <summary>
        /// Updates the underlying campaign data model without displaying the end screen.
        /// </summary>
        public void BindCampaign(CampaignState campaign)
        {
            if (campaign == null) return;
            ViewModel = new CampaignEndViewModel(campaign);
            BindObservables();
        }

        protected override void OnShown(object navigationArgs)
        {
            // navigationArgs can be a CampaignState for late-binding (e.g. navigated from map)
            if (navigationArgs is CampaignState campaign)
            {
                ViewModel = new CampaignEndViewModel(campaign);
                BindObservables();
            }
            ViewModel?.Refresh();
        }

        // ---------------------------------------------------------------- data binding
        private void BindObservables()
        {
            if (ViewModel == null) return;

            BindLabel(_statusBadgeLabel,       ViewModel.EndStatusBadgeText);
            BindLabel(_agencyNameLabel,        ViewModel.AgencyNameText);
            BindLabel(_conclusionLabel,        ViewModel.ConclusionText);
            BindLabel(_yearsActiveLabel,       ViewModel.YearsActiveText);
            BindLabel(_missionStatsLabel,      ViewModel.MissionStatsText);
            BindLabel(_milestoneProgressLabel, ViewModel.MilestoneProgressText);
            BindLabel(_scienceDataLabel,       ViewModel.ScienceDataText);
            BindLabel(_finalBudgetLabel,       ViewModel.FinalBudgetText);
            BindLabel(_finalReputationLabel,   ViewModel.FinalReputationText);
            BindLabel(_crewLossLabel,          ViewModel.CrewLossText);
            BindLabel(_keyAchievementsLabel,   ViewModel.KeyAchievementsText);

            // Victory visual class toggle
            ApplyVictoryClass();
            ViewModel.IsVictory.Subscribe(_ => ApplyVictoryClass(), notifyImmediately: false);
        }

        private void BindLabel(Label label, ObservableValue<string> observable)
        {
            if (label == null || observable == null) return;
            label.text = observable.Value;
            observable.Subscribe(v => label.text = v, notifyImmediately: false);
        }

        private void ApplyVictoryClass()
        {
            if (_statusBadgeLabel == null || ViewModel == null) return;
            const string victoryClass = "campaign-end--victory";
            if (ViewModel.IsVictory.Value)
                _statusBadgeLabel.AddToClassList(victoryClass);
            else
                _statusBadgeLabel.RemoveFromClassList(victoryClass);
        }

        // ---------------------------------------------------------------- helper
        private static Label AddStatRow(VisualElement parent, string labelText, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("stat-row");
            var key = new Label(labelText);
            key.AddToClassList("stat-key");
            var value = new Label { name = valueName };
            value.AddToClassList("stat-value");
            row.Add(key);
            row.Add(value);
            parent.Add(row);
            return value;
        }
    }
}
