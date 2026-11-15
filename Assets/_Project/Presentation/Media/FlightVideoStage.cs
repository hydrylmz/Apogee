using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apogee.UI.Media
{
    /// <summary>
    /// UI Toolkit içinde tam ekran görev videosu oynatan görsel katman.
    /// Video, bir RenderTexture üzerinden <c>Image</c> elemanına aktarılır.
    ///
    /// Kullanım: ekran controller'ı stage alanının üzerine ekler, video modu aktifken
    /// legacy 2B/3B stage'i gizler. Arıza durumunda <see cref="PlaybackFailed"/>
    /// üzerinden <see cref="FlightVideoMode.ReportPlaybackFailure"/> çağrılır ve
    /// ekran legacy theatre'a geri düşer.
    /// </summary>
    public sealed class FlightVideoStage : VisualElement
    {
        private readonly Image _image;
        private readonly Label _clipLabel;
        private FlightVideoPlayer _player;
        private bool _eventsWired;
        private bool _playRequested;

        /// <summary>Loop olmayan klip sonuna ulaştığında tetiklenir.</summary>
        public event Action PlaybackFinished;

        /// <summary>Oynatma arızası — ekran legacy fallback'e geçmeli.</summary>
        public event Action<string> PlaybackFailed;

        public bool IsVisible => style.display == DisplayStyle.Flex;
        public bool IsPlaying => _player != null && _player.IsPlaying;

        public FlightVideoStage()
        {
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            style.backgroundColor = new Color(0.01f, 0.02f, 0.04f, 1f);
            style.overflow = Overflow.Hidden;

            _image = new Image
            {
                scaleMode = ScaleMode.ScaleAndCrop,
                image = null
            };
            _image.style.position = Position.Absolute;
            _image.style.left = 0;
            _image.style.top = 0;
            _image.style.right = 0;
            _image.style.bottom = 0;
            Add(_image);

            _clipLabel = new Label(string.Empty);
            _clipLabel.style.position = Position.Absolute;
            _clipLabel.style.left = 14;
            _clipLabel.style.bottom = 66;
            _clipLabel.style.fontSize = 11;
            _clipLabel.style.color = new Color(0.75f, 0.85f, 0.95f, 0.8f);
            _clipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(_clipLabel);

            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
        }

        /// <summary>
        /// Klipi yükler ve oynatmaya başlar. Klip dosyası yoksa <c>false</c> döner
        /// (çağıran taraf legacy fallback'e geçmelidir).
        /// </summary>
        public bool PlayClip(FlightVideoClip clip, bool loop)
        {
            if (!FlightVideoLibrary.Exists(clip))
            {
                return false;
            }

            EnsurePlayer();
            _playRequested = true;
            _clipLabel.text = FlightVideoLibrary.GetDisplayLabel(clip);

            // Önce yükle — Load() yeni bir RenderTexture oluşturur — sonra bağla.
            // (Sıra ters olursa Image eski/null dokuya bağlı kalır ve video görünmez.)
            _player.Load(clip, loop);
            _image.image = _player.TargetTexture;
            return true;
        }

        public void Pause() => _player?.Pause();

        public void TogglePlayPause() => _player?.TogglePlayPause();

        public void Restart() => _player?.Restart();

        public void SetSpeed(float speed) => _player?.SetSpeed(speed);

        /// <summary>"Sona Atla" davranışı — bitiş sinyalini üretir.</summary>
        public void SkipToEnd() => _player?.SkipToEnd();

        public void SetVisible(bool visible)
        {
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Oynatmayı durdurur ve RenderTexture'ı serbest bırakır.</summary>
        public void StopAndRelease()
        {
            _playRequested = false;
            _player?.StopAndRelease();
            _image.image = null;
        }

        private void EnsurePlayer()
        {
            if (_player != null)
            {
                WireEvents();
                return;
            }

            _player = FlightVideoPlayer.Create();
            WireEvents();
        }

        private void WireEvents()
        {
            if (_eventsWired || _player == null) return;
            _eventsWired = true;
            _player.PlaybackFinished += OnPlayerFinished;
            _player.Failed += OnPlayerFailed;
        }

        private void OnPlayerFinished()
        {
            if (!_playRequested) return;
            PlaybackFinished?.Invoke();
        }

        private void OnPlayerFailed(string message)
        {
            if (!_playRequested) return;
            _playRequested = false;
            FlightVideoMode.ReportPlaybackFailure(message);
            PlaybackFailed?.Invoke(message);
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent _)
        {
            // Ekran hiyerarşiden sökülürse (screen rebuild) GPU kaynaklarını bırak.
            StopAndRelease();
        }
    }
}
