using System;
using System.Collections.Generic;

namespace Apogee.Core.Audio
{
    /// <summary>
    /// Pure C# in-memory audio service.
    /// Used in tests, headless runs, or as default fallback.
    /// Records event triggers for assertion and verification.
    /// </summary>
    public sealed class NullAudioService : IAudioService
    {
        private AudioSettingsData _settings = new AudioSettingsData();
        private readonly List<AudioEvent> _playbackHistory = new List<AudioEvent>();
        private AudioEvent _currentMusic = AudioEvent.None;

        public AudioSettingsData Settings => _settings;
        public IReadOnlyList<AudioEvent> PlaybackHistory => _playbackHistory;
        public AudioEvent CurrentMusic => _currentMusic;

        public event Action<AudioEvent, AudioChannel> AudioTriggered;

        public void PlaySfx(AudioEvent audioEvent, float pitchMultiplier = 1.0f)
        {
            if (audioEvent == AudioEvent.None) return;
            _playbackHistory.Add(audioEvent);
            AudioTriggered?.Invoke(audioEvent, AudioChannel.Sfx);
        }

        public void PlayMusic(AudioEvent musicEvent, bool loop = true, float fadeDurationSeconds = 1.0f)
        {
            _currentMusic = musicEvent;
            if (musicEvent == AudioEvent.None) return;
            _playbackHistory.Add(musicEvent);
            AudioTriggered?.Invoke(musicEvent, AudioChannel.Music);
        }

        public void StopMusic(float fadeDurationSeconds = 0.5f)
        {
            _currentMusic = AudioEvent.None;
        }

        public void SetSettings(AudioSettingsData settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void SetMute(bool muted)
        {
            _settings.IsMuted = muted;
        }

        public void ClearHistory()
        {
            _playbackHistory.Clear();
        }
    }
}
