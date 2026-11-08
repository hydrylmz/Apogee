using System;
using System.Globalization;
using Apogee.Strategy.Milestones;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Milestones
{
    /// <summary>
    /// ViewModel representing a single milestone card on the agency quest board.
    /// GDD §4.4, §14.1, §21 M6-01.
    /// </summary>
    public sealed class MilestoneItemViewModel : ViewModelBase
    {
        public MilestoneProgress Progress { get; }

        public string Id => Progress.Record.Id;
        public string Title => Progress.Record.Title;
        public string Description => Progress.Record.Description;
        public int OrderIndex => Progress.Record.OrderIndex;

        public string RewardBudgetText => $"+{Progress.Record.RewardBudget.ToString("F0", CultureInfo.InvariantCulture)} M$ Bütçe";
        public string RewardReputationText => $"+{Progress.Record.RewardReputation.ToString("F0", CultureInfo.InvariantCulture)} İtibar";
        public string RewardKnowledgeText => $"+{Progress.Record.RewardKnowledgePoints.ToString("F0", CultureInfo.InvariantCulture)} BP";

        public ObservableValue<MilestoneStatus> Status { get; }
        public ObservableValue<string> StatusBadgeText { get; }
        public ObservableValue<bool> CanClaim { get; }
        public ObservableValue<bool> IsCompleted { get; }

        public ViewActionCommand ClaimCommand { get; }

        public MilestoneItemViewModel(MilestoneProgress progress, Action<string> onClaimRequested)
        {
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));

            Status = new ObservableValue<MilestoneStatus>(progress.Status);
            StatusBadgeText = new ObservableValue<string>(GetBadgeText(progress.Status));
            CanClaim = new ObservableValue<bool>(progress.Status == MilestoneStatus.CompletedPendingClaim);
            IsCompleted = new ObservableValue<bool>(progress.Status == MilestoneStatus.Claimed);

            ClaimCommand = new ViewActionCommand(
                executeAction: () => onClaimRequested?.Invoke(Id),
                canExecutePredicate: () => Status.Value == MilestoneStatus.CompletedPendingClaim
            );
        }

        public void RefreshState()
        {
            Status.Value = Progress.Status;
            StatusBadgeText.Value = GetBadgeText(Progress.Status);
            CanClaim.Value = (Progress.Status == MilestoneStatus.CompletedPendingClaim);
            IsCompleted.Value = (Progress.Status == MilestoneStatus.Claimed);
            ClaimCommand.RaiseCanExecuteChanged();
        }

        private static string GetBadgeText(MilestoneStatus status)
        {
            switch (status)
            {
                case MilestoneStatus.Locked:
                    return "KİLİTLİ";
                case MilestoneStatus.Available:
                    return "ÖNERİLEN HEDEF";
                case MilestoneStatus.CompletedPendingClaim:
                    return "TAMAMLANDI (ÖDÜLÜ AL)";
                case MilestoneStatus.Claimed:
                    return "TAMAMLANDI";
                default:
                    return string.Empty;
            }
        }
    }
}
