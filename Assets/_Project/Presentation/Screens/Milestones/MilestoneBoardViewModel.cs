using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Apogee.Core;
using Apogee.Strategy;
using Apogee.Strategy.Milestones;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Milestones
{
    /// <summary>
    /// ViewModel for the Agency Milestone and Quest Board (GDD §4.4, §14.1, §21 M6-01).
    /// Displays recommended milestones, progress counters, rewards, and claim actions.
    /// </summary>
    public sealed class MilestoneBoardViewModel : ViewModelBase
    {
        private CampaignState _campaign;

        public ObservableCollection<MilestoneItemViewModel> MilestoneItems { get; } =
            new ObservableCollection<MilestoneItemViewModel>();

        public ObservableValue<string> CompletedCountText { get; } = new ObservableValue<string>("0 / 8");
        public ObservableValue<float> ProgressFraction { get; } = new ObservableValue<float>(0.0f);
        public ObservableValue<string> ActiveGoalTitleText { get; } = new ObservableValue<string>("-");
        public ObservableValue<string> ActiveGoalDescriptionText { get; } = new ObservableValue<string>("-");
        public ObservableValue<string> NotificationText { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<bool> HasNotification { get; } = new ObservableValue<bool>(false);

        public ViewActionCommand RefreshCommand { get; }
        public ViewActionCommand DismissNotificationCommand { get; }

        public MilestoneBoardViewModel(CampaignState campaign = null)
        {
            _campaign = campaign;

            RefreshCommand = new ViewActionCommand(() => Refresh());
            DismissNotificationCommand = new ViewActionCommand(() => ClearNotification());

            if (_campaign?.Milestones != null)
            {
                _campaign.Milestones.OnMilestoneCompleted += HandleMilestoneCompleted;
                _campaign.Milestones.OnMilestoneClaimed += HandleMilestoneClaimed;
            }

            RebuildItems();
        }

        public void BindCampaign(CampaignState campaign)
        {
            if (_campaign?.Milestones != null)
            {
                _campaign.Milestones.OnMilestoneCompleted -= HandleMilestoneCompleted;
                _campaign.Milestones.OnMilestoneClaimed -= HandleMilestoneClaimed;
            }

            _campaign = campaign;

            if (_campaign?.Milestones != null)
            {
                _campaign.Milestones.OnMilestoneCompleted += HandleMilestoneCompleted;
                _campaign.Milestones.OnMilestoneClaimed += HandleMilestoneClaimed;
            }

            RebuildItems();
            Refresh();
        }

        public void Refresh()
        {
            if (_campaign.Milestones != null)
            {
                _campaign.Milestones.EvaluateProgress(_campaign.Missions.Values, _campaign.Time?.CurrentDate.ToString());
            }

            foreach (var item in MilestoneItems)
            {
                item.RefreshState();
            }

            UpdateSummary();
        }

        public void ClaimMilestone(string milestoneId)
        {
            if (_campaign == null) return;
            var cmd = new ClaimMilestoneRewardCommand(_campaign, milestoneId);
            var result = _campaign.Execute(cmd, PlayerRole.AgencyDirector);
            if (result.Success)
            {
                ShowNotification(result.Message);
                Refresh();
            }
            else
            {
                ShowNotification($"Hata: {result.Message}");
            }
        }

        private void RebuildItems()
        {
            MilestoneItems.Clear();
            if (_campaign?.Milestones == null) return;

            foreach (var progress in _campaign.Milestones.AllMilestones.Values.OrderBy(m => m.Record.OrderIndex))
            {
                var itemVm = new MilestoneItemViewModel(progress, ClaimMilestone);
                MilestoneItems.Add(itemVm);
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (_campaign?.Milestones == null) return;

            int total = _campaign.Milestones.AllMilestones.Count;
            int claimedOrCompleted = _campaign.Milestones.AllMilestones.Values.Count(
                m => m.Status == MilestoneStatus.Claimed || m.Status == MilestoneStatus.CompletedPendingClaim);

            CompletedCountText.Value = $"{claimedOrCompleted} / {total}";
            ProgressFraction.Value = total > 0 ? (float)claimedOrCompleted / total : 0f;

            var nextGoal = _campaign.Milestones.AllMilestones.Values
                .OrderBy(m => m.Record.OrderIndex)
                .FirstOrDefault(m => m.Status == MilestoneStatus.Available || m.Status == MilestoneStatus.CompletedPendingClaim);

            if (nextGoal != null)
            {
                ActiveGoalTitleText.Value = nextGoal.Record.Title;
                ActiveGoalDescriptionText.Value = nextGoal.Record.Description;
            }
            else if (claimedOrCompleted >= total && total > 0)
            {
                ActiveGoalTitleText.Value = "Tüm Kilometre Taşları Tamamlandı!";
                ActiveGoalDescriptionText.Value = "Ajansınız Güneş Sistemi'nin yeni ufuklarını açtı.";
            }
            else
            {
                ActiveGoalTitleText.Value = "-";
                ActiveGoalDescriptionText.Value = "-";
            }
        }

        private void HandleMilestoneCompleted(MilestoneProgress progress)
        {
            ShowNotification($"Kilometre Taşı Tamamlandı: {progress.Record.Title}! Ödülü Görev Tahtasından alabilirsiniz.");
            Refresh();
        }

        private void HandleMilestoneClaimed(MilestoneProgress progress)
        {
            Refresh();
        }

        private void ShowNotification(string message)
        {
            NotificationText.Value = message;
            HasNotification.Value = !string.IsNullOrWhiteSpace(message);
        }

        private void ClearNotification()
        {
            NotificationText.Value = string.Empty;
            HasNotification.Value = false;
        }

        public override void Dispose()
        {
            if (_campaign?.Milestones != null)
            {
                _campaign.Milestones.OnMilestoneCompleted -= HandleMilestoneCompleted;
                _campaign.Milestones.OnMilestoneClaimed -= HandleMilestoneClaimed;
            }
            base.Dispose();
        }
    }
}
