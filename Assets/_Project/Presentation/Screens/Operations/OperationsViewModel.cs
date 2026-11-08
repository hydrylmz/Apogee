using System;
using System.Collections.Generic;
using Apogee.Core.Simulation.Cruise;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Operations
{
    /// <summary>
    /// ViewModel for the Operations / Cruise screen.
    /// Exposes spacecraft telemetry, cruise progress, event stream, and active decision card.
    /// GDD §10.1, §10.2, §14.1 #14.
    /// </summary>
    public sealed class OperationsViewModel : ViewModelBase
    {
        public ObservableValue<string> MissionName { get; } = new ObservableValue<string>("Aktif Misyon");
        public ObservableValue<string> VehicleName { get; } = new ObservableValue<string>("Uzay Aracı");
        public ObservableValue<string> DestinationName { get; } = new ObservableValue<string>("Hedef");
        public ObservableValue<double> CruiseProgress { get; } = new ObservableValue<double>(0.0);
        public ObservableValue<string> MissionStatusText { get; } = new ObservableValue<string>("Seyirde");
        public ObservableValue<bool> HasActiveDecision { get; } = new ObservableValue<bool>(false);
        public ObservableValue<string> DecisionTitle { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> DecisionNarrative { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> NarrativeSummary { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<bool> CanAdvance { get; } = new ObservableValue<bool>(true);
        public ObservableValue<bool> CanExecuteEdl { get; } = new ObservableValue<bool>(false);
        public ObservableValue<bool> CanExecuteScience { get; } = new ObservableValue<bool>(false);
        public ObservableValue<bool> IsComplete { get; } = new ObservableValue<bool>(false);

        // Science telemetry
        public ObservableValue<double> DownlinkDeliveredGb { get; } = new ObservableValue<double>(0.0);
        public ObservableValue<double> RawDataGeneratedGb { get; } = new ObservableValue<double>(0.0);
        public ObservableValue<double> BufferStorageGb { get; } = new ObservableValue<double>(0.0);
        public ObservableValue<double> DroppedDataGb { get; } = new ObservableValue<double>(0.0);
        public ObservableValue<int> ScienceDaysCompleted { get; } = new ObservableValue<int>(0);

        public List<CruiseSegmentEvent> EventLog { get; } = new List<CruiseSegmentEvent>();
        public List<Apogee.Core.Simulation.EDL.EdlPhaseEvent> EdlEventLog { get; } = new List<Apogee.Core.Simulation.EDL.EdlPhaseEvent>();
        public List<Apogee.Core.Simulation.Science.SciencePhaseEvent> ScienceEventLog { get; } = new List<Apogee.Core.Simulation.Science.SciencePhaseEvent>();
        public List<CruiseDecisionOption> CurrentDecisionOptions { get; } = new List<CruiseDecisionOption>();

        public CampaignState Campaign { get; private set; }
        public string MissionId { get; private set; }
        public ScreenNavigator Navigator { get; set; }

        public ViewActionCommand AdvanceCruiseStepCommand { get; }
        public ViewActionCommand ExecuteEdlStepCommand { get; }
        public ViewActionCommand ExecuteScienceStepCommand { get; }
        public ViewActionCommand<string> ResolveOptionCommand { get; }

        public event Action OnStateUpdated;

        public OperationsViewModel()
        {
            AdvanceCruiseStepCommand = new ViewActionCommand(ExecuteAdvanceCruiseStep);
            ExecuteEdlStepCommand = new ViewActionCommand(ExecuteEdlStep);
            ExecuteScienceStepCommand = new ViewActionCommand(ExecuteScienceStep);
            ResolveOptionCommand = new ViewActionCommand<string>(ExecuteResolveOption);
        }

        public void BindMission(CampaignState campaign, string missionId, OperationsNavigationArgs args)
        {
            Campaign = campaign;
            MissionId = missionId;

            if (campaign != null && !campaign.Missions.ContainsKey(missionId) && args != null)
            {
                var mission = new MissionInstance(
                    missionId,
                    args.MissionName,
                    args.DestinationId,
                    args.Blueprint,
                    args.CruiseDurationDays,
                    args.MissionSeed
                )
                {
                    Status = MissionStatus.InCruise
                };
                campaign.Missions[missionId] = mission;
            }

            MissionName.Value = args?.MissionName ?? "Aktif Misyon";
            VehicleName.Value = args?.Blueprint?.Name ?? "Uzay Aracı";
            DestinationName.Value = (args?.DestinationId ?? "Hedef").ToUpperInvariant();

            RefreshFromMission();
        }

        public void RefreshFromMission()
        {
            if (Campaign == null || string.IsNullOrEmpty(MissionId) || !Campaign.Missions.TryGetValue(MissionId, out var mission))
            {
                return;
            }

            CruiseProgress.Value = mission.CruiseProgress;
            MissionStatusText.Value = mission.Status.ToString().ToUpperInvariant();

            EventLog.Clear();
            EventLog.AddRange(mission.CruiseEventLog);

            if (mission.Status == MissionStatus.DecisionPending && mission.PendingDecisionCard != null)
            {
                HasActiveDecision.Value = true;
                DecisionTitle.Value = mission.PendingDecisionCard.TitleKey;
                DecisionNarrative.Value = mission.PendingDecisionCard.NarrativeKey;
                CurrentDecisionOptions.Clear();
                CurrentDecisionOptions.AddRange(mission.PendingDecisionCard.Options);
                CanAdvance.Value = false;
                IsComplete.Value = false;
            }
            else
            {
                HasActiveDecision.Value = false;
                DecisionTitle.Value = string.Empty;
                DecisionNarrative.Value = string.Empty;
                CurrentDecisionOptions.Clear();
                CanAdvance.Value = mission.Status == MissionStatus.InCruise;
                CanExecuteEdl.Value = mission.Status == MissionStatus.Arrival;
                CanExecuteScience.Value = mission.Status == MissionStatus.Operating;
                IsComplete.Value = mission.Status == MissionStatus.Operating || mission.Status == MissionStatus.Completed || mission.Status == MissionStatus.Failed;
            }

            // Update science telemetry
            DownlinkDeliveredGb.Value = mission.AccumulatedDownlinkDataGb;
            RawDataGeneratedGb.Value = mission.AccumulatedRawDataGeneratedGb;
            BufferStorageGb.Value = mission.CurrentBufferStorageGb;
            DroppedDataGb.Value = mission.AccumulatedDroppedDataGb;
            ScienceDaysCompleted.Value = mission.ScienceDaysCompleted;

            ScienceEventLog.Clear();
            foreach (var sciRes in mission.ScienceLog)
            {
                ScienceEventLog.AddRange(sciRes.Events);
            }

            EdlEventLog.Clear();
            if (mission.EdlResult != null)
            {
                EdlEventLog.AddRange(mission.EdlResult.PhaseEvents);
            }

            if (mission.Status == MissionStatus.Arrival)
            {
                NarrativeSummary.Value = "Uzay aracı hedefine başarıyla ulaştı. İniş ve EDL operasyonu başlatılabilir.";
            }
            else if (mission.Status == MissionStatus.Operating)
            {
                if (mission.ScienceLog.Count > 0)
                {
                    var lastSci = mission.ScienceLog[mission.ScienceLog.Count - 1];
                    NarrativeSummary.Value = $"[Bilim Günü #{mission.ScienceDaysCompleted}] {lastSci.PlainLanguageSummary} (Toplam İndirilen: {mission.AccumulatedDownlinkDataGb:F1} GB)";
                }
                else
                {
                    NarrativeSummary.Value = mission.EdlResult != null
                        ? $"İniş tamamlandı ({mission.EdlResult.Outcome}): {mission.EdlResult.PlainLanguageSummary}. Bilim operasyonu başlatılabilir."
                        : "Uzay aracı operasyonel konumda. Bilim operasyonu başlatılabilir.";
                }
            }
            else if (mission.Status == MissionStatus.Failed)
            {
                NarrativeSummary.Value = mission.EdlResult != null
                    ? $"İniş arızası ({mission.EdlResult.Outcome}): {mission.EdlResult.PlainLanguageSummary}"
                    : "Seyir sırasında kritik bir arıza meydana geldi ve görev sonlandı.";
            }

            OnStateUpdated?.Invoke();
        }

        private void ExecuteAdvanceCruiseStep()
        {
            if (Campaign == null || string.IsNullOrEmpty(MissionId))
                return;

            var cmd = new AdvanceCruiseCommand(Campaign, MissionId);
            Campaign.Execute(cmd);
            RefreshFromMission();
        }

        private void ExecuteEdlStep()
        {
            if (Campaign == null || string.IsNullOrEmpty(MissionId))
                return;

            var cmd = new ExecuteEdlCommand(Campaign, MissionId);
            Campaign.Execute(cmd);
            RefreshFromMission();
        }

        private void ExecuteScienceStep()
        {
            if (Campaign == null || string.IsNullOrEmpty(MissionId))
                return;

            var cmd = new ExecuteScienceTurnCommand(Campaign, MissionId);
            Campaign.Execute(cmd);
            RefreshFromMission();
        }

        private void ExecuteResolveOption(string optionId)
        {
            if (Campaign == null || string.IsNullOrEmpty(MissionId) || string.IsNullOrEmpty(optionId))
                return;

            var cmd = new ResolveCruiseDecisionCommand(Campaign, MissionId, optionId);
            Campaign.Execute(cmd);
            RefreshFromMission();
        }
    }
}
