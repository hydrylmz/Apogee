using System;
using UnityEngine;
using UnityEngine.Video;

namespace Apogee.UI.Media
{
    /// <summary>
    /// Tek bir görev videosunu RenderTexture üzerine oynatan hafif MonoBehaviour
    /// sarmalayıcı (VideoPlayer + RenderTexture). UI Toolkit tarafı yalnızca
    /// <see cref="TargetTexture"/> özelliğini bir <c>Image</c> elemanına bağlar.
    ///
    /// Oynatma arızaları <see cref="Failed"/> olayı ile yukarı raporlanır;
    /// <see cref="FlightVideoMode.ReportPlaybackFailure"/> fallback zinciri buradan beslenir.
    /// </summary>
    public sealed class FlightVideoPlayer : MonoBehaviour
    {
        private const int TextureWidth = 1920;
        private const int TextureHeight = 1080;

        private VideoPlayer _player;
        private RenderTexture _renderTexture;
        private bool _prepared;
        private FlightVideoClip _currentClip;

        /// <summary>Videonun render edildiği doku (UI Image'e bağlanır).</summary>
        public Texture TargetTexture => _renderTexture;

        public bool IsPlaying => _player != null && _player.isPlaying && _player.enabled;
        public bool IsPrepared => _prepared;

        /// <summary>Geçerli oynatma zamanı (saniye).</summary>
        public double CurrentTime => (_player != null && _player.enabled) ? _player.time : 0.0;

        /// <summary>Klip toplam süresi (saniye).</summary>
        public double Duration => (_player != null && _player.enabled) ? _player.length : 0.0;

        /// <summary>0..1 arası normalize oynatma konumu (klipte süre yoksa 0).</summary>
        public double NormalizedTime
        {
            get
            {
                if (_player == null || _player.length <= 0.0) return 0.0;
                return Math.Clamp(_player.clockTime / _player.length, 0.0, 1.0);
            }
        }

        /// <summary>Klip bittiğinde (loop olmayan kliplerde) tetiklenir.</summary>
        public event Action PlaybackFinished;

        /// <summary>VideoPlayer arıza bildirimi (dosya yok, codec, vb.).</summary>
        public event Action<string> Failed;

        /// <summary>Gizli bir host GameObject üzerinde oynatıcı oluşturur.</summary>
        public static FlightVideoPlayer Create()
        {
            var host = new GameObject("FlightVideoPlayerHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            return host.AddComponent<FlightVideoPlayer>();
        }

        private void EnsurePlayer()
        {
            if (_player != null) return;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.Url;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.waitForFirstFrame = true;
            _player.prepareCompleted += OnPrepareCompleted;
            _player.loopPointReached += OnLoopPointReached;
            _player.errorReceived += OnErrorReceived;
        }

        /// <summary>
        /// Klipi yükler ve hazırlığı başlatır. Oynatma hazırlık tamamlanınca otomatik başlar.
        /// Bir önceki klip varsa kaynakları serbest bırakılır.
        /// </summary>
        public void Load(FlightVideoClip clip, bool loop)
        {
            EnsurePlayer();
            _currentClip = clip;

            string url = FlightVideoLibrary.GetUrl(clip);
            if (string.IsNullOrEmpty(url))
            {
                OnErrorReceived(_player, "missing clip mapping");
                return;
            }

            ReleaseTexture();
            _renderTexture = new RenderTexture(TextureWidth, TextureHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = $"FlightVideoRT_{clip}"
            };
            _renderTexture.Create();

            _player.targetTexture = _renderTexture;
            _player.isLooping = loop;

            // Güneş sistemi videosu (Cruise) %100 yavaşlatılmış (0.5x) oynatılır; diğer videolar normal hızda (1.0x)
            float baseSpeed = (clip == FlightVideoClip.Cruise) ? 0.5f : 1.0f;
            _player.playbackSpeed = baseSpeed;

            _player.enabled = true;
            _prepared = false;
            _player.url = url;
            _player.Prepare();
        }

        public void Play()
        {
            if (_player == null || string.IsNullOrEmpty(_player.url)) return;
            if (!_player.isPrepared)
            {
                _player.Prepare();
            }
            _player.Play();
        }

        public void Pause()
        {
            if (_player != null && _player.isPlaying)
            {
                _player.Pause();
            }
        }

        public void TogglePlayPause()
        {
            if (_player == null) return;
            if (_player.isPlaying)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }

        public void Restart()
        {
            if (_player == null || string.IsNullOrEmpty(_player.url)) return;
            _player.Stop();
            _player.Play();
        }

        /// <summary>Hız çarpanı (1x / 2x / 4x footer düğmeleri için).</summary>
        public void SetSpeed(float speed)
        {
            if (_player != null)
            {
                float baseSpeed = (_currentClip == FlightVideoClip.Cruise) ? 0.5f : 1.0f;
                _player.playbackSpeed = Mathf.Max(0.05f, speed * baseSpeed);
            }
        }

        /// <summary>"Sona Atla" — döngüdeki klip için bile bitiş sinyali üretir.</summary>
        public void SkipToEnd()
        {
            if (_player == null) return;
            _player.Pause();
            PlaybackFinished?.Invoke();
        }

        /// <summary>Oynatmayı durdurur ve klip kaynaklarını serbest bırakır.</summary>
        public void StopAndRelease()
        {
            if (_player != null)
            {
                _player.Stop();
                _player.targetTexture = null;
                _player.url = string.Empty;
                _player.enabled = false;
            }
            ReleaseTexture();
            _prepared = false;
        }

        private void ReleaseTexture()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        private void OnPrepareCompleted(VideoPlayer source)
        {
            _prepared = true;
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (!source.isLooping)
            {
                PlaybackFinished?.Invoke();
            }
        }

        private void OnErrorReceived(VideoPlayer source, string message)
        {
            Failed?.Invoke(message);
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.prepareCompleted -= OnPrepareCompleted;
                _player.loopPointReached -= OnLoopPointReached;
                _player.errorReceived -= OnErrorReceived;
            }
            StopAndRelease();
        }
    }
}
