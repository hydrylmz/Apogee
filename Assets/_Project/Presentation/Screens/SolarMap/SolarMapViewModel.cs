using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Mission;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.SolarMap
{
    /// <summary>
    /// ViewModel for the Solar System Orrery Map Screen (ScreenId.SolarMap = 4).
    /// Complies with GDD §6.2, §14.1 (#4 Güneş Sistemi Haritası), and §21 M9-06.
    /// Exposes dynamic planetary positions, launch transfer windows, celestial telemetry cards,
    /// and active spacecraft markers for in-flight interplanetary missions.
    /// </summary>
    public sealed class SolarMapViewModel : ViewModelBase
    {
        private readonly IScreenNavigator _navigator;
        private CampaignState _campaign;
        private readonly List<CelestialBodyItemViewModel> _bodyItems = new();
        private readonly List<ActiveMissionMarkerViewModel> _missionMarkers = new();

        // ── HUD Observables ──────────────────────────────────────────────────
        public ObservableValue<string> DateDisplay { get; } = new(string.Empty);
        public ObservableValue<TimeSpeed> CurrentSpeed { get; } = new(TimeSpeed.Paused);
        public ObservableValue<double> Budget { get; } = new(0.0);
        public ObservableValue<double> Reputation { get; } = new(0.0);
        public ObservableValue<double> ResearchPoints { get; } = new(0.0);
        public ObservableValue<string> AgencyName { get; } = new(string.Empty);

        // ── Solar System Observables ─────────────────────────────────────────
        public ObservableValue<IReadOnlyList<CelestialBodyItemViewModel>> Bodies { get; } =
            new(Array.Empty<CelestialBodyItemViewModel>());

        public ObservableValue<CelestialBodyItemViewModel> SelectedBody { get; } = new(null);
        public ObservableValue<DestinationBriefingCard> BriefingCard { get; } = new(null);

        public ObservableValue<IReadOnlyList<ActiveMissionMarkerViewModel>> ActiveMissions { get; } =
            new(Array.Empty<ActiveMissionMarkerViewModel>());

        public ObservableValue<ActiveMissionMarkerViewModel> SelectedMission { get; } = new(null);
        public ObservableValue<bool> IsWindowOpenNow { get; } = new(false);
        public ObservableValue<string> StatusMessage { get; } = new("Güneş Sistemi Hazır.");

        // ── Time & Simulation Commands ───────────────────────────────────────
        public ViewActionCommand AdvanceDayCommand { get; }
        public ViewActionCommand PauseCommand { get; }
        public ViewActionCommand Speed1xCommand { get; }
        public ViewActionCommand Speed2xCommand { get; }
        public ViewActionCommand Speed3xCommand { get; }

        // ── Action & Navigation Commands ─────────────────────────────────────
        public ViewActionCommand LaunchToSelectedDestinationCommand { get; }
        public ViewActionCommand GoToCampaignMapCommand { get; }
        public ViewActionCommand GoToComposerCommand { get; }
        public ViewActionCommand GoToWorkshopCommand { get; }
        public ViewActionCommand GoToMainMenuCommand { get; }

        public CampaignState Campaign => _campaign;

        public SolarMapViewModel(
            IScreenNavigator navigator,
            CampaignState campaign = null,
            IEnumerable<DestinationRecord> destinationRecords = null)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            // Time controls
            AdvanceDayCommand = new ViewActionCommand(() => AdvanceDays(1));
            PauseCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.Paused));
            Speed1xCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.Normal));
            Speed2xCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.Fast));
            Speed3xCommand = new ViewActionCommand(() => SetSpeed(TimeSpeed.VeryFast));

            // Navigation
            GoToCampaignMapCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.CampaignMap));
            GoToComposerCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.MissionComposer));
            GoToWorkshopCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.Workshop));
            GoToMainMenuCommand = new ViewActionCommand(() => _navigator.GoTo(ScreenId.MainMenu));

            LaunchToSelectedDestinationCommand = new ViewActionCommand(OnLaunchToSelected);

            InitializeBodies(destinationRecords);

            if (campaign != null)
            {
                BindCampaign(campaign);
            }
            else
            {
                Refresh();
            }
        }

        private void InitializeBodies(IEnumerable<DestinationRecord> records)
        {
            var list = records?.ToList();
            if (list == null || list.Count == 0)
            {
                list = CreateDefaultDestinationRecords();
            }

            _bodyItems.Clear();

            var initialDate = new GameDate(0);
            foreach (var rec in list)
            {
                _bodyItems.Add(new CelestialBodyItemViewModel(rec, initialDate));
            }

            Bodies.Value = _bodyItems.ToList();
            SelectedBody.Value = _bodyItems.FirstOrDefault(b => b.Id == "mars") ?? _bodyItems.FirstOrDefault();
        }

        public void BindCampaign(CampaignState campaign)
        {
            _campaign = campaign;
            Refresh();
        }

        public void Refresh()
        {
            var currentDate = _campaign?.Time?.CurrentDate ?? new GameDate(0);

            // Update HUD
            if (_campaign != null)
            {
                DateDisplay.Value = $"{currentDate} · Gün {currentDate.TotalDays}";
                CurrentSpeed.Value = _campaign.Time.Speed;
                Budget.Value = _campaign.Budget;
                Reputation.Value = _campaign.Reputation;
                ResearchPoints.Value = _campaign.ResearchPoints;
                AgencyName.Value = _campaign.AgencyName;
            }
            else
            {
                DateDisplay.Value = $"{currentDate} · Gün {currentDate.TotalDays}";
                CurrentSpeed.Value = TimeSpeed.Paused;
                Budget.Value = 0.0;
                Reputation.Value = 0.0;
                ResearchPoints.Value = 0.0;
                AgencyName.Value = "Birleşik Yörünge Ajansı";
            }

            // Update planetary positions & window states
            foreach (var body in _bodyItems)
            {
                body.UpdatePosition(currentDate);
            }
            Bodies.Value = _bodyItems.ToList();

            // Refresh selected body & briefing card
            if (SelectedBody.Value != null)
            {
                var refreshed = _bodyItems.FirstOrDefault(b => b.Id == SelectedBody.Value.Id) ?? _bodyItems.FirstOrDefault();
                SelectedBody.Value = refreshed;
                if (refreshed != null)
                {
                    BriefingCard.Value = new DestinationBriefingCard(refreshed.Record, currentDate);
                    IsWindowOpenNow.Value = refreshed.IsWindowOpen;
                }
            }
            else if (_bodyItems.Count > 0)
            {
                SelectBody(_bodyItems[0].Id);
            }

            // Refresh active missions
            RefreshActiveMissions();
        }

        private void RefreshActiveMissions()
        {
            _missionMarkers.Clear();

            if (_campaign?.Missions != null)
            {
                var earth = _bodyItems.FirstOrDefault(b => b.Id == "moon" || b.Id == "leo");

                foreach (var mission in _campaign.Missions.Values)
                {
                    if (mission.Status == MissionStatus.InCruise ||
                        mission.Status == MissionStatus.DecisionPending ||
                        mission.Status == MissionStatus.Arrival ||
                        mission.Status == MissionStatus.Operating)
                    {
                        var target = _bodyItems.FirstOrDefault(b => string.Equals(b.Id, mission.DestinationId, StringComparison.OrdinalIgnoreCase));
                        _missionMarkers.Add(new ActiveMissionMarkerViewModel(mission, earth, target));
                    }
                }
            }

            ActiveMissions.Value = _missionMarkers.ToList();
            if (SelectedMission.Value != null)
            {
                SelectedMission.Value = _missionMarkers.FirstOrDefault(m => m.MissionId == SelectedMission.Value.MissionId);
            }
        }

        public void SelectBody(string destinationId)
        {
            if (string.IsNullOrEmpty(destinationId)) return;

            var match = _bodyItems.FirstOrDefault(b => string.Equals(b.Id, destinationId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedBody.Value = match;
                var currentDate = _campaign?.Time?.CurrentDate ?? new GameDate(0);
                BriefingCard.Value = new DestinationBriefingCard(match.Record, currentDate);
                IsWindowOpenNow.Value = match.IsWindowOpen;
                StatusMessage.Value = $"Hedef seçildi: {match.Name}";
            }
        }

        public void SelectMission(string missionId)
        {
            if (string.IsNullOrEmpty(missionId)) return;
            var match = _missionMarkers.FirstOrDefault(m => string.Equals(m.MissionId, missionId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedMission.Value = match;
                StatusMessage.Value = $"Aktif misyon seçildi: {match.MissionName} ({match.ProgressPercent})";
            }
        }

        private void OnLaunchToSelected()
        {
            if (SelectedBody.Value == null) return;
            _navigator.GoTo(ScreenId.MissionComposer, SelectedBody.Value.Id);
        }

        public void AdvanceDays(int days)
        {
            if (_campaign == null) return;
            _campaign.Execute(new AdvanceTimeCommand(days));
            Refresh();
        }

        public void SetSpeed(TimeSpeed speed)
        {
            if (_campaign == null) return;
            _campaign.Execute(new SetTimeSpeedCommand(speed));
            Refresh();
        }

        private static List<DestinationRecord> CreateDefaultDestinationRecords()
        {
            const string fallbackJson = @"[
              { ""id"": ""leo"", ""nameKey"": ""Alçak Dünya Yörüngesi (LEO)"", ""bodyType"": ""orbit"", ""dvAscent"": 9400, ""transitDaysMin"": 0, ""transitDaysMax"": 1, ""commDelaySecondsMin"": 0.001, ""commDelaySecondsMax"": 0.05, ""windowPeriodDays"": 0, ""windowDurationDays"": 365, ""hazards"": { ""radiation"": 0.1 } },
              { ""id"": ""moon"", ""nameKey"": ""Ay (Luna)"", ""bodyType"": ""body"", ""dvAscent"": 9400, ""dvTransfer"": 3100, ""dvArrival"": 900, ""dvLanding"": 1700, ""dvReturn"": 2400, ""transitDaysMin"": 3, ""transitDaysMax"": 5, ""commDelaySecondsMin"": 1.2, ""commDelaySecondsMax"": 1.4, ""windowPeriodDays"": 29, ""windowFirstOffsetDays"": 14, ""windowDurationDays"": 3, ""hazards"": { ""heat"": 0.4, ""dust"": 0.3 } },
              { ""id"": ""mars"", ""nameKey"": ""Mars"", ""bodyType"": ""body"", ""dvAscent"": 9400, ""dvTransfer"": 2700, ""dvArrival"": 900, ""dvLanding"": 600, ""dvReturn"": 5600, ""transitDaysMin"": 180, ""transitDaysMax"": 270, ""commDelaySecondsMin"": 180, ""commDelaySecondsMax"": 1320, ""windowPeriodDays"": 780, ""windowFirstOffsetDays"": 400, ""windowDurationDays"": 30, ""hazards"": { ""radiation"": 0.7, ""dust"": 0.6, ""atmosphere"": 0.3 } },
              { ""id"": ""venus"", ""nameKey"": ""Venüs"", ""bodyType"": ""body"", ""dvAscent"": 9400, ""dvTransfer"": 2700, ""dvArrival"": 1000, ""transitDaysMin"": 120, ""transitDaysMax"": 150, ""commDelaySecondsMin"": 120, ""commDelaySecondsMax"": 840, ""windowPeriodDays"": 584, ""windowFirstOffsetDays"": 100, ""windowDurationDays"": 25, ""hazards"": { ""heat"": 0.9, ""atmosphere"": 0.8 } },
              { ""id"": ""europa"", ""nameKey"": ""Europa (Jüpiter)"", ""bodyType"": ""body"", ""dvAscent"": 9400, ""dvTransfer"": 8800, ""dvArrival"": 2800, ""transitDaysMin"": 1000, ""transitDaysMax"": 1200, ""commDelaySecondsMin"": 1980, ""commDelaySecondsMax"": 3120, ""windowPeriodDays"": 399, ""windowFirstOffsetDays"": 200, ""windowDurationDays"": 20, ""hazards"": { ""radiation"": 0.95, ""heat"": 0.85 } }
            ]";

            var catalog = CatalogLoader.Load<DestinationRecord>(fallbackJson);
            return catalog?.All?.ToList() ?? new List<DestinationRecord>();
        }
    }
}
