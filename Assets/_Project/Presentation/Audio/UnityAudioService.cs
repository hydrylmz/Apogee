using System;
using System.Collections.Generic;
using Apogee.Core.Audio;
using UnityEngine;

namespace Apogee.UI.Audio
{
    /// <summary>
    /// Presentation layer audio controller implementing <see cref="IAudioService"/> via Unity AudioSource components.
    /// Complies with GDD §15.4 and §17.1.
    /// </summary>
    public sealed class UnityAudioService : MonoBehaviour, IAudioService
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _uiSource;

        [System.Serializable]
        public struct AudioEventClipEntry
        {
            public AudioEvent audioEvent;
            public AudioClip clip;
        }

        [Header("Audio Clips Mapping")]
        [SerializeField] private List<AudioEventClipEntry> _clipMappings = new List<AudioEventClipEntry>();

        private readonly Dictionary<AudioEvent, AudioClip> _clipLookup = new Dictionary<AudioEvent, AudioClip>();
        private AudioSettingsData _settings = new AudioSettingsData();

        public AudioSettingsData Settings => _settings;

        public event Action<AudioEvent, AudioChannel> AudioTriggered;

        private void Awake()
        {
            BuildLookup();
            ApplySettingsToSources();
        }

        private void BuildLookup()
        {
            _clipLookup.Clear();
            if (_clipMappings == null) return;
            foreach (var entry in _clipMappings)
            {
                if (entry.audioEvent != AudioEvent.None && entry.clip != null)
                {
                    _clipLookup[entry.audioEvent] = entry.clip;
                }
            }
        }

        public void PlaySfx(AudioEvent audioEvent, float pitchMultiplier = 1.0f)
        {
            if (audioEvent == AudioEvent.None) return;

            AudioTriggered?.Invoke(audioEvent, AudioChannel.Sfx);

            if (_settings.IsMuted) return;

            if (_clipLookup.TryGetValue(audioEvent, out var clip) && clip != null)
            {
                var source = (audioEvent <= AudioEvent.ErrorBuzz) ? _uiSource : _sfxSource;
                if (source != null)
                {
                    source.pitch = Mathf.Clamp(pitchMultiplier, 0.5f, 2.0f);
                    source.PlayOneShot(clip, _settings.GetEffectiveVolume(AudioChannel.Sfx));
                }
            }
        }

        public void PlayMusic(AudioEvent musicEvent, bool loop = true, float fadeDurationSeconds = 1.0f)
        {
            if (musicEvent == AudioEvent.None) return;

            AudioTriggered?.Invoke(musicEvent, AudioChannel.Music);

            if (_musicSource == null) return;

            if (_clipLookup.TryGetValue(musicEvent, out var clip) && clip != null)
            {
                _musicSource.clip = clip;
                _musicSource.loop = loop;
                _musicSource.volume = _settings.GetEffectiveVolume(AudioChannel.Music);
                _musicSource.Play();
            }
        }

        public void StopMusic(float fadeDurationSeconds = 0.5f)
        {
            if (_musicSource != null && _musicSource.isPlaying)
            {
                _musicSource.Stop();
            }
        }

        public void SetSettings(AudioSettingsData settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ApplySettingsToSources();
        }

        public void SetMute(bool muted)
        {
            _settings.IsMuted = muted;
            ApplySettingsToSources();
        }

        private void ApplySettingsToSources()
        {
            if (_musicSource != null)
                _musicSource.volume = _settings.GetEffectiveVolume(AudioChannel.Music);
            if (_sfxSource != null)
                _sfxSource.volume = _settings.GetEffectiveVolume(AudioChannel.Sfx);
            if (_uiSource != null)
                _uiSource.volume = _settings.GetEffectiveVolume(AudioChannel.Ui);
        }
    }
}
