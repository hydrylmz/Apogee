using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Core.Flight
{
    /// <summary>
    /// Full-screen sequence-transition overlay for the Flight Theatre (playtest fix #5).
    /// Shows a large semi-transparent banner ("SIRADAKİ AŞAMA: SEYİR" etc.) for a short
    /// time when a sequence finishes and playback hands over to the next stage.
    ///
    /// - Non-blocking: does not pause playback or intercept clicks (picking-mode Ignore).
    /// - Fades in/out via USS opacity transitions; snaps instantly under ReduceMotion.
    /// - Purely presentational — no sim state changes. (GDD §3.4, §15.1)
    /// </summary>
    public sealed class FlightTransitionOverlay : VisualElement
    {
        private const float VisibleSeconds = 1.5f;
        private const float FadeSeconds    = 0.25f;

        private readonly Label _title;
        private readonly Label _subtitle;
        private bool _reduceMotion;
        private IVisualElementScheduledItem _hideSchedule;
        private float _fadeAlpha;
        private IVisualElementScheduledItem _fadeSchedule;
        private bool _fadingOut;

        public FlightTransitionOverlay(bool reduceMotion = false)
        {
            _reduceMotion = reduceMotion;

            style.position = Position.Absolute;
            style.left = 0; style.top = 0;
            style.right = 0; style.bottom = 0;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.55f);
            style.display = DisplayStyle.None;
            pickingMode = PickingMode.Ignore;

            var band = new VisualElement
            {
                style =
                {
                    paddingTop = 18,
                    paddingBottom = 18,
                    paddingLeft = 48,
                    paddingRight = 48,
                    backgroundColor = new Color(0.03f, 0.07f, 0.13f, 0.85f),
                    borderTopWidth = 2,
                    borderBottomWidth = 2,
                    borderTopColor = new Color(0.20f, 0.75f, 1f, 0.8f),
                    borderBottomColor = new Color(0.20f, 0.75f, 1f, 0.8f),
                    alignItems = Align.Center
                }
            };

            _title = new Label
            {
                style =
                {
                    fontSize = 30,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.85f, 0.95f, 1f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };
            _subtitle = new Label
            {
                style =
                {
                    fontSize = 14,
                    color = new Color(0.55f, 0.75f, 0.9f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginTop = 4,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };

            band.Add(_title);
            band.Add(_subtitle);
            Add(band);
        }

        /// <summary>Sync the accessibility ReduceMotion preference.</summary>
        public void SetReduceMotion(bool reduce)
        {
            _reduceMotion = reduce;
        }

        /// <summary>
        /// Show the transition banner. Auto-hides after ~1.5 s without pausing playback.
        /// </summary>
        /// <param name="title">Large headline, e.g. "SIRADAKİ AŞAMA: SEYİR".</param>
        /// <param name="subtitle">Optional smaller line, e.g. "Fırlatma tamamlandı".</param>
        public void Show(string title, string subtitle = null)
        {
            if (string.IsNullOrEmpty(title)) return;

            _title.text = title;
            _subtitle.text = subtitle ?? string.Empty;
            _subtitle.style.display = string.IsNullOrEmpty(subtitle)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            _hideSchedule?.Pause();
            _fadeSchedule?.Pause();
            _fadeSchedule = null;

            style.display = DisplayStyle.Flex;

            if (_reduceMotion)
            {
                style.opacity = 1f;
            }
            else
            {
                // Fade in over FadeSeconds, hold, then fade out
                _fadeAlpha = 0f;
                style.opacity = 0f;
                _fadingOut = false;
                _fadeSchedule = schedule.Execute(FadeTick).Every(33);
            }

            _hideSchedule = schedule.Execute(Hide).StartingIn((long)(VisibleSeconds * 1000));
        }

        /// <summary>Hide the banner immediately.</summary>
        public void Hide()
        {
            _hideSchedule?.Pause();
            _hideSchedule = null;
            _fadeSchedule?.Pause();
            _fadeSchedule = null;
            style.opacity = 1f;
            style.display = DisplayStyle.None;
        }

        private void FadeTick()
        {
            float step = 0.033f / FadeSeconds;
            if (_fadingOut)
            {
                _fadeAlpha -= step;
                if (_fadeAlpha <= 0f)
                {
                    _fadeSchedule?.Pause();
                    _fadeSchedule = null;
                    style.opacity = 1f;
                    style.display = DisplayStyle.None;
                    return;
                }
            }
            else
            {
                _fadeAlpha += step;
                if (_fadeAlpha >= 1f)
                {
                    _fadeAlpha = 1f;
                    _fadingOut = true;
                }
            }
            style.opacity = _fadeAlpha;
        }
    }
}
