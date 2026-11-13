using System;
using System.Collections.Generic;
using Apogee.UI.Core.Flight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// Vertical rail showing flight phases/beats on the side.
    /// Provides visual state (Done, Active, Pending, Error) for each step in the sequence.
    /// Supports clicking on a past/future phase to seek. (GDD §3.4, §10.1)
    /// </summary>
    public sealed class PhaseRailView : VisualElement
    {
        private readonly ScrollView _scroll;
        private readonly List<VisualElement> _nodeElements = new List<VisualElement>();
        private readonly List<Label> _titleLabels = new List<Label>();
        private readonly List<Label> _timeLabels = new List<Label>();
        private readonly List<VisualElement> _indicators = new List<VisualElement>();

        private IReadOnlyList<FlightBeat> _beats;
        private int _currentIndex = -1;

        public event Action<int> OnBeatSelected;

        public PhaseRailView()
        {
            style.width = 240;
            style.flexShrink = 0;
            style.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 0.85f);
            style.borderRightWidth = 1;
            style.borderRightColor = new Color(0.12f, 0.18f, 0.28f, 0.9f);
            style.paddingTop = 12;
            style.paddingBottom = 12;
            style.paddingLeft = 10;
            style.paddingRight = 10;

            var header = new Label("UÇUŞ AŞAMALARI")
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.45f, 0.65f, 0.85f),
                    marginBottom = 8,
                    paddingLeft = 4
                }
            };
            Add(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { flexGrow = 1 }
            };
            Add(_scroll);
        }

        public void BindBeats(IReadOnlyList<FlightBeat> beats)
        {
            _beats = beats;
            _scroll.Clear();
            _nodeElements.Clear();
            _titleLabels.Clear();
            _timeLabels.Clear();
            _indicators.Clear();
            _currentIndex = -1;

            if (_beats == null || _beats.Count == 0) return;

            for (int i = 0; i < _beats.Count; i++)
            {
                int index = i;
                var beat = _beats[i];

                var node = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingTop = 6,
                        paddingBottom = 6,
                        paddingLeft = 8,
                        paddingRight = 8,
                        marginBottom = 4,
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4,
                        backgroundColor = new Color(0.06f, 0.09f, 0.14f, 0.6f)
                    }
                };

                // Click to seek
                node.RegisterCallback<ClickEvent>(_ => OnBeatSelected?.Invoke(index));

                var indicator = new VisualElement
                {
                    style =
                    {
                        width = 8,
                        height = 8,
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4,
                        marginRight = 8,
                        backgroundColor = new Color(0.25f, 0.35f, 0.45f)
                    }
                };
                node.Add(indicator);
                _indicators.Add(indicator);

                var textCol = new VisualElement { style = { flexGrow = 1 } };

                var timeStr = $"T+{(int)beat.TimestampSeconds}s";
                var timeLbl = new Label(timeStr)
                {
                    style =
                    {
                        fontSize = 10,
                        color = new Color(0.4f, 0.55f, 0.7f),
                        marginBottom = 1
                    }
                };
                textCol.Add(timeLbl);
                _timeLabels.Add(timeLbl);

                var title = !string.IsNullOrEmpty(beat.TitleKey) ? beat.TitleKey : beat.PhaseId;
                var titleLbl = new Label(title)
                {
                    style =
                    {
                        fontSize = 11,
                        color = new Color(0.75f, 0.85f, 0.95f),
                        unityFontStyleAndWeight = FontStyle.Normal
                    }
                };
                textCol.Add(titleLbl);
                _titleLabels.Add(titleLbl);

                node.Add(textCol);

                _scroll.Add(node);
                _nodeElements.Add(node);
            }

            SetCurrentIndex(0);
        }

        public void SetBeatTitle(int index, string localizedTitle)
        {
            if (index >= 0 && index < _titleLabels.Count && !string.IsNullOrEmpty(localizedTitle))
            {
                _titleLabels[index].text = localizedTitle;
            }
        }

        public void SetCurrentIndex(int activeIndex)
        {
            if (_beats == null || _beats.Count == 0) return;
            _currentIndex = Mathf.Clamp(activeIndex, 0, _beats.Count - 1);

            for (int i = 0; i < _nodeElements.Count; i++)
            {
                var node = _nodeElements[i];
                var ind = _indicators[i];
                var title = _titleLabels[i];
                var time = _timeLabels[i];
                var beat = _beats[i];

                bool isError = beat.StatusId.Contains("Failure") || beat.StatusId.Contains("Scrub") || beat.StatusId.Contains("Abort");

                if (i < _currentIndex)
                {
                    // Completed
                    node.style.backgroundColor = new Color(0.06f, 0.12f, 0.10f, 0.5f);
                    ind.style.backgroundColor = isError ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.1f, 0.75f, 0.5f);
                    title.style.color = new Color(0.55f, 0.7f, 0.65f);
                    time.style.color = new Color(0.35f, 0.5f, 0.45f);
                    title.style.unityFontStyleAndWeight = FontStyle.Normal;
                }
                else if (i == _currentIndex)
                {
                    // Active
                    node.style.backgroundColor = new Color(0.08f, 0.22f, 0.32f, 0.9f);
                    ind.style.backgroundColor = isError ? new Color(0.95f, 0.25f, 0.25f) : new Color(0.05f, 0.75f, 0.9f);
                    title.style.color = Color.white;
                    time.style.color = new Color(0.35f, 0.85f, 1f);
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                else
                {
                    // Pending
                    node.style.backgroundColor = new Color(0.05f, 0.07f, 0.11f, 0.4f);
                    ind.style.backgroundColor = new Color(0.2f, 0.26f, 0.35f);
                    title.style.color = new Color(0.45f, 0.55f, 0.65f);
                    time.style.color = new Color(0.3f, 0.38f, 0.48f);
                    title.style.unityFontStyleAndWeight = FontStyle.Normal;
                }
            }

            if (_currentIndex >= 0 && _currentIndex < _nodeElements.Count)
            {
                var target = _nodeElements[_currentIndex];
                _scroll.ScrollTo(target);
            }
        }
    }
}
