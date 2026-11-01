using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Data;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Events
{
    /// <summary>
    /// ViewModel representing a single decision option inside an event popup card (GDD §6.3, §10.2).
    /// </summary>
    public sealed class EventOptionItemViewModel : ViewModelBase
    {
        public string OptionId { get; }
        public string Text { get; }
        public string Tooltip { get; }
        public string VisibleEffectSummary { get; }
        public EventOutcomeRecord Outcome { get; }

        public ViewActionCommand SelectCommand { get; }

        public EventOptionItemViewModel(EventOptionRecord record, Action<string> onSelect)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            OptionId = record.Id;
            Text = record.TextKey ?? record.Id;
            Tooltip = record.TooltipKey;
            VisibleEffectSummary = record.VisibleEffectSummary ?? string.Empty;
            Outcome = record.Outcome;

            SelectCommand = new ViewActionCommand(() => onSelect?.Invoke(OptionId));
        }
    }

    /// <summary>
    /// ViewModel for the Campaign Decision Card / Event Popup (GDD §6.3, §10.2, §14.1 #15).
    /// </summary>
    public sealed class EventPopupViewModel : ViewModelBase
    {
        private CampaignState _campaignState;
        private ScreenNavigator _navigator;
        private ActiveEventInstance _activeEvent;

        public ObservableValue<string> EventTitle { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> EventCategory { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> EventDescription { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<string> TriggerDateText { get; } = new ObservableValue<string>(string.Empty);
        public ObservableValue<bool> HasPendingEvent { get; } = new ObservableValue<bool>(false);

        public List<EventOptionItemViewModel> Options { get; } = new List<EventOptionItemViewModel>();

        public ViewActionCommand DismissCommand { get; }

        public EventPopupViewModel()
        {
            DismissCommand = new ViewActionCommand(DismissPopup);
        }

        public void Bind(CampaignState state, ScreenNavigator navigator = null)
        {
            _campaignState = state;
            _navigator = navigator;
            Refresh();
        }

        public void Refresh()
        {
            Options.Clear();
            _activeEvent = _campaignState?.Events?.PendingEvent;

            if (_activeEvent == null)
            {
                HasPendingEvent.Value = false;
                EventTitle.Value = string.Empty;
                EventCategory.Value = string.Empty;
                EventDescription.Value = string.Empty;
                TriggerDateText.Value = string.Empty;
                return;
            }

            HasPendingEvent.Value = true;
            EventTitle.Value = _activeEvent.EventRecord.TitleKey ?? _activeEvent.EventRecord.Id;
            EventCategory.Value = (_activeEvent.EventRecord.Category ?? "CAMPAIGN").ToUpperInvariant();
            EventDescription.Value = _activeEvent.EventRecord.DescKey ?? string.Empty;
            TriggerDateText.Value = _activeEvent.TriggeredDate.ToString();

            if (_activeEvent.EventRecord.Options != null)
            {
                foreach (var opt in _activeEvent.EventRecord.Options)
                {
                    Options.Add(new EventOptionItemViewModel(opt, OnOptionSelected));
                }
            }
        }

        private void OnOptionSelected(string optionId)
        {
            if (_campaignState == null || _activeEvent == null)
                return;

            var cmd = new ResolveEventOptionCommand(_activeEvent.EventRecord.Id, optionId);
            var result = _campaignState.Execute(cmd);

            if (result.Success)
            {
                Refresh();
                DismissPopup();
            }
        }

        private void DismissPopup()
        {
            if (_navigator != null && _navigator.CurrentScreenId == ScreenId.EventPopup)
            {
                _navigator.GoBack();
            }
        }
    }
}
