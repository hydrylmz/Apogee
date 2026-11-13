using System;
using Apogee.UI.Core.Flight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// Narrative and caption layer supporting three modes (GDD §3.5):
    /// - Off: completely hidden
    /// - Subtitle: single-line / 2-line bottom caption with title + narrative
    /// - FullLog: scrollable historical event card log overlay
    /// </summary>
    public sealed class FlightCaptionView : VisualElement
    {
        private FlightCaptionMode _mode = FlightCaptionMode.Subtitle;
        private float _fontScale = 1.0f;

        private readonly VisualElement _subtitleContainer;
        private readonly Label _subtitleTitle;
        private readonly Label _subtitleNarrative;

        private readonly VisualElement _logContainer;
        private readonly ScrollView _logScroll;

        public FlightCaptionMode Mode => _mode;

        public FlightCaptionView()
        {
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.bottom = 60; // Above playback controls footer
            pickingMode = PickingMode.Ignore;

            // Subtitle banner
            _subtitleContainer = new VisualElement
            {
                style =
                {
                    alignSelf = Align.Center,
                    maxWidth = Length.Percent(70),
                    backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.85f),
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 16,
                    paddingRight = 16,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderLeftWidth = 3,
                    borderLeftColor = new Color(0.024f, 0.714f, 0.831f)
                }
            };

            _subtitleTitle = new Label
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    marginBottom = 2
                }
            };
            _subtitleContainer.Add(_subtitleTitle);

            _subtitleNarrative = new Label
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.8f, 0.9f, 0.98f),
                    whiteSpace = WhiteSpace.Normal
                }
            };
            _subtitleContainer.Add(_subtitleNarrative);
            Add(_subtitleContainer);

            // Full log panel
            _logContainer = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 20,
                    right = 20,
                    bottom = 0,
                    maxHeight = 220,
                    backgroundColor = new Color(0.04f, 0.07f, 0.12f, 0.92f),
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 12,
                    paddingRight = 12,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftColor = new Color(0.15f, 0.22f, 0.35f, 0.8f),
                    borderRightColor = new Color(0.15f, 0.22f, 0.35f, 0.8f),
                    borderTopColor = new Color(0.15f, 0.22f, 0.35f, 0.8f),
                    borderBottomColor = new Color(0.15f, 0.22f, 0.35f, 0.8f),
                    display = DisplayStyle.None
                }
            };

            var logHeader = new Label("GÖREV TELEMETRİ & OLAY GÜNLÜĞÜ (FLIGHT LOG)")
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.4f, 0.75f, 0.95f),
                    marginBottom = 6
                }
            };
            _logContainer.Add(logHeader);

            _logScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { flexGrow = 1 }
            };
            _logContainer.Add(_logScroll);
            Add(_logContainer);

            ApplyMode();
        }

        public void SetMode(FlightCaptionMode mode)
        {
            _mode = mode;
            ApplyMode();
        }

        public void SetFontScale(float scale)
        {
            _fontScale = Mathf.Clamp(scale, 0.5f, 2.0f);
            _subtitleTitle.style.fontSize = 13 * _fontScale;
            _subtitleNarrative.style.fontSize = 11 * _fontScale;
        }

        public void DisplayBeat(FlightBeat beat, string localizedTitle, string localizedNarrative)
        {
            if (beat == null) return;

            string title = !string.IsNullOrEmpty(localizedTitle) ? localizedTitle : beat.TitleKey;
            string narrative = !string.IsNullOrEmpty(localizedNarrative) ? localizedNarrative : beat.NarrativeKey;
            if (string.IsNullOrEmpty(narrative))
            {
                narrative = beat.TechnicalDetails;
            }

            _subtitleTitle.text = title;
            _subtitleNarrative.text = narrative;

            // Border color according to status
            bool isError = beat.StatusId.Contains("Failure") || beat.StatusId.Contains("Scrub") || beat.StatusId.Contains("Abort");
            _subtitleContainer.style.borderLeftColor = isError ? new Color(0.95f, 0.25f, 0.25f) : new Color(0.024f, 0.714f, 0.831f);

            // Add card to full log
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    alignItems = Align.Center,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 8,
                    paddingRight = 8,
                    marginBottom = 3,
                    backgroundColor = new Color(0.07f, 0.10f, 0.16f, 0.8f),
                    borderLeftWidth = 3,
                    borderLeftColor = isError ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.1f, 0.75f, 0.5f)
                }
            };

            var left = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var timeLbl = new Label($"T+{(int)beat.TimestampSeconds}s")
            {
                style =
                {
                    width = 55,
                    fontSize = 10 * _fontScale,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.35f, 0.85f, 1f)
                }
            };
            left.Add(timeLbl);

            var titleLbl = new Label(title)
            {
                style =
                {
                    fontSize = 11 * _fontScale,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white
                }
            };
            left.Add(titleLbl);
            row.Add(left);

            var descLbl = new Label(narrative)
            {
                style =
                {
                    fontSize = 10 * _fontScale,
                    color = new Color(0.6f, 0.75f, 0.88f)
                }
            };
            row.Add(descLbl);

            _logScroll.Add(row);
            _logScroll.ScrollTo(row);
        }

        public void ClearLog()
        {
            _logScroll.Clear();
        }

        private void ApplyMode()
        {
            switch (_mode)
            {
                case FlightCaptionMode.Off:
                    _subtitleContainer.style.display = DisplayStyle.None;
                    _logContainer.style.display = DisplayStyle.None;
                    break;
                case FlightCaptionMode.Subtitle:
                    _subtitleContainer.style.display = DisplayStyle.Flex;
                    _logContainer.style.display = DisplayStyle.None;
                    break;
                case FlightCaptionMode.FullLog:
                    _subtitleContainer.style.display = DisplayStyle.None;
                    _logContainer.style.display = DisplayStyle.Flex;
                    break;
            }
        }
    }
}
