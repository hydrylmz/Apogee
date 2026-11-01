using System;
using UnityEngine;
using UnityEngine.UIElements;
using Apogee.UI.Core.Navigation;

namespace Apogee.UI.Screens.Events
{
    /// <summary>
    /// UI Toolkit screen controller for the Event Popup card (GDD §6.3, §14.1 #15).
    /// Renders modal decision card, category badge, narrative text, and visible effect choices.
    /// </summary>
    public class EventPopupScreenController : ScreenControllerBase<EventPopupViewModel>
    {
        public override ScreenId ScreenId => ScreenId.EventPopup;

        private VisualElement _cardContainer;
        private Label _categoryLabel;
        private Label _titleLabel;
        private Label _dateLabel;
        private Label _descLabel;
        private VisualElement _optionsList;

        public EventPopupScreenController(EventPopupViewModel viewModel)
            : base(viewModel)
        {
        }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.AddToClassList("screen-event-popup");
            root.style.flexGrow = 1;

            // Overlay backdrop
            var backdrop = new VisualElement();
            backdrop.AddToClassList("modal-backdrop");
            backdrop.style.flexGrow = 1;
            backdrop.style.alignItems = Align.Center;
            backdrop.style.justifyContent = Justify.Center;
            backdrop.style.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.85f);
            root.Add(backdrop);

            // Decision Card
            _cardContainer = new VisualElement();
            _cardContainer.AddToClassList("event-card");
            _cardContainer.style.width = 620;
            _cardContainer.style.paddingLeft = 24;
            _cardContainer.style.paddingRight = 24;
            _cardContainer.style.paddingTop = 20;
            _cardContainer.style.paddingBottom = 24;
            _cardContainer.style.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 1.0f);
            _cardContainer.style.borderLeftWidth = 2;
            _cardContainer.style.borderRightWidth = 2;
            _cardContainer.style.borderTopWidth = 2;
            _cardContainer.style.borderBottomWidth = 2;
            _cardContainer.style.borderLeftColor = new Color(0.96f, 0.65f, 0.14f, 1f); // Amber border accent
            _cardContainer.style.borderRightColor = new Color(0.96f, 0.65f, 0.14f, 1f);
            _cardContainer.style.borderTopColor = new Color(0.96f, 0.65f, 0.14f, 1f);
            _cardContainer.style.borderBottomColor = new Color(0.96f, 0.65f, 0.14f, 1f);
            _cardContainer.style.borderTopLeftRadius = 8;
            _cardContainer.style.borderTopRightRadius = 8;
            _cardContainer.style.borderBottomLeftRadius = 8;
            _cardContainer.style.borderBottomRightRadius = 8;
            backdrop.Add(_cardContainer);

            // Header Row: Category Badge + Date
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 12;

            _categoryLabel = new Label("CAMPAIGN EVENT");
            _categoryLabel.AddToClassList("badge");
            _categoryLabel.style.backgroundColor = new Color(0.96f, 0.65f, 0.14f, 0.25f);
            _categoryLabel.style.color = new Color(0.96f, 0.65f, 0.14f, 1f);
            _categoryLabel.style.paddingLeft = 8;
            _categoryLabel.style.paddingRight = 8;
            _categoryLabel.style.paddingTop = 3;
            _categoryLabel.style.paddingBottom = 3;
            _categoryLabel.style.fontSize = 11;
            headerRow.Add(_categoryLabel);

            _dateLabel = new Label();
            _dateLabel.style.color = new Color(0.6f, 0.7f, 0.8f, 1f);
            _dateLabel.style.fontSize = 12;
            headerRow.Add(_dateLabel);
            _cardContainer.Add(headerRow);

            // Title
            _titleLabel = new Label();
            _titleLabel.AddToClassList("event-card__title");
            _titleLabel.style.fontSize = 20;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = Color.white;
            _titleLabel.style.marginBottom = 12;
            _titleLabel.style.whiteSpace = WhiteSpace.Normal;
            _cardContainer.Add(_titleLabel);

            // Narrative Description
            _descLabel = new Label();
            _descLabel.AddToClassList("event-card__description");
            _descLabel.style.fontSize = 13;
            _descLabel.style.color = new Color(0.8f, 0.85f, 0.9f, 1f);
            _descLabel.style.marginBottom = 20;
            _descLabel.style.whiteSpace = WhiteSpace.Normal;
            _cardContainer.Add(_descLabel);

            // Options List
            _optionsList = new VisualElement();
            _optionsList.AddToClassList("event-card__options");
            _optionsList.style.flexDirection = FlexDirection.Column;
            _cardContainer.Add(_optionsList);

            return root;
        }

        protected override void OnShown(object context)
        {
            base.OnShown(context);
            UpdateView();
        }

        public void UpdateView()
        {
            if (ViewModel == null || _cardContainer == null) return;

            if (!ViewModel.HasPendingEvent.Value)
            {
                if (RootElement != null) RootElement.style.display = DisplayStyle.None;
                return;
            }

            if (RootElement != null) RootElement.style.display = DisplayStyle.Flex;
            _categoryLabel.text = ViewModel.EventCategory.Value;
            _titleLabel.text = ViewModel.EventTitle.Value;
            _dateLabel.text = ViewModel.TriggerDateText.Value;
            _descLabel.text = ViewModel.EventDescription.Value;

            _optionsList.Clear();
            foreach (var opt in ViewModel.Options)
            {
                var optBox = new VisualElement();
                optBox.style.marginBottom = 10;
                optBox.style.paddingLeft = 14;
                optBox.style.paddingRight = 14;
                optBox.style.paddingTop = 10;
                optBox.style.paddingBottom = 10;
                optBox.style.backgroundColor = new Color(0.12f, 0.17f, 0.24f, 1f);
                optBox.style.borderLeftWidth = 1;
                optBox.style.borderRightWidth = 1;
                optBox.style.borderTopWidth = 1;
                optBox.style.borderBottomWidth = 1;
                optBox.style.borderLeftColor = new Color(0.2f, 0.3f, 0.42f, 1f);
                optBox.style.borderRightColor = new Color(0.2f, 0.3f, 0.42f, 1f);
                optBox.style.borderTopColor = new Color(0.2f, 0.3f, 0.42f, 1f);
                optBox.style.borderBottomColor = new Color(0.2f, 0.3f, 0.42f, 1f);
                optBox.style.borderTopLeftRadius = 4;
                optBox.style.borderTopRightRadius = 4;
                optBox.style.borderBottomLeftRadius = 4;
                optBox.style.borderBottomRightRadius = 4;

                var btn = new Button();
                btn.text = opt.Text;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.fontSize = 13;
                btn.style.backgroundColor = new Color(0.18f, 0.26f, 0.38f, 1f);
                btn.style.color = Color.white;
                btn.style.marginBottom = 4;
                btn.clicked += () => opt.SelectCommand?.Execute();
                optBox.Add(btn);

                if (!string.IsNullOrEmpty(opt.VisibleEffectSummary))
                {
                    var effectLabel = new Label(opt.VisibleEffectSummary);
                    effectLabel.style.fontSize = 12;
                    effectLabel.style.color = new Color(0.3f, 0.85f, 0.7f, 1f); // Cyan / mint
                    effectLabel.style.whiteSpace = WhiteSpace.Normal;
                    optBox.Add(effectLabel);
                }

                _optionsList.Add(optBox);
            }
        }
    }
}
