using System;
using System.Collections.Generic;
using Apogee.Core.Audio;
using UnityEngine;

namespace Apogee.UI.Audio
{
    /// <summary>
    /// Presentation layer audio controller implementing <see cref="IAudioService"/> via Unity AudioSource components.
    /// Complies with GDD §15.4 and §17.1.
    /// Clips are auto-loaded from Resources/Audio/UI/ at startup; Inspector mappings override auto-loaded ones.
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

        [Header("Audio Clips Mapping (overrides auto-load)")]
        [SerializeField] private List<AudioEventClipEntry> _clipMappings = new List<AudioEventClipEntry>();

        [Header("Debug Info (read-only)")]
        [SerializeField, HideInInspector] private int _loadedClipsCount;

        private readonly Dictionary<AudioEvent, AudioClip> _clipLookup = new Dictionary<AudioEvent, AudioClip>();
        private AudioSettingsData _settings = new AudioSettingsData();

        public AudioSettingsData Settings => _settings;

        public event Action<AudioEvent, AudioChannel> AudioTriggered;

        // Maps each AudioEvent to its preferred Kenney clip filename (without extension).
        // These live under Resources/Audio/UI/*.ogg
        private static readonly Dictionary<AudioEvent, string> s_eventToClipName = new Dictionary<AudioEvent, string>
        {
            // UI
            { AudioEvent.ButtonClick,           "click_001" },
            { AudioEvent.ButtonHover,           "select_001" },
            { AudioEvent.TabSwitch,             "switch_001" },
            { AudioEvent.ModalOpen,             "maximize_001" },
            { AudioEvent.ModalClose,            "minimize_001" },
            { AudioEvent.ErrorBuzz,             "error_001" },
            // Mission & Launch
            { AudioEvent.CountdownBeep,         "tick_001" },
            { AudioEvent.RocketIgnition,        "confirmation_001" },
            { AudioEvent.StageSeparation,       "bong_001" },
            { AudioEvent.OrbitInsertion,        "confirmation_002" },
            { AudioEvent.AbortAlarm,            "error_003" },
            // Operations & EDL
            { AudioEvent.TelemetryPing,         "tick_002" },
            { AudioEvent.AtmosphericEntryRumble,"scratch_004" },
            { AudioEvent.ParachuteDeployment,   "open_001" },
            { AudioEvent.RetroThrust,           "scroll_001" },
            { AudioEvent.LandingSuccess,        "confirmation_004" },
            { AudioEvent.LandingCrash,          "error_007" },
            // Strategy & Report
            { AudioEvent.EventNotification,     "question_001" },
            { AudioEvent.ContractSigned,        "confirmation_003" },
            { AudioEvent.ResearchCompleted,     "maximize_005" },
            { AudioEvent.MilestoneClaimed,      "confirmation_002" },
            { AudioEvent.MissionSuccessJingle,  "maximize_004" },
            { AudioEvent.MissionFailureJingle,  "error_008" },
            { AudioEvent.BankruptcySiren,       "error_006" },
            // Science / Flight Outcomes (ADR-061)
            { AudioEvent.ScienceSuccess,        "maximize_006" },
            { AudioEvent.ScienceFail,           "glitch_001" },
            { AudioEvent.CruiseNominalPing,     "tick_004" },
            { AudioEvent.TransferInjectionBurn, "scratch_005" },
            { AudioEvent.ArrivalCaptureRing,    "glass_004" },
            // Music cues — no Kenney equivalents; kept empty so silence plays gracefully
        };

        private void Awake()
        {
            EnsureAudioSources();
            AutoLoadClips();
            BuildLookup(); // Inspector entries override auto-loaded ones
            ApplySettingsToSources();
        }

        // Start() as a second chance in case Resources aren't ready during Awake
        private void Start()
        {
            if (_clipLookup.Count == 0)
            {
                AutoLoadClips();
                BuildLookup();
            }
        }

        /// <summary>
        /// Auto-creates AudioSource components when Inspector slots are unassigned.
        /// Music source loops; SFX and UI sources play one-shots.
        /// </summary>
        private void EnsureAudioSources()
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.loop = true;
                _musicSource.volume = 0.5f;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.volume = 0.8f;
            }

            if (_uiSource == null)
            {
                _uiSource = gameObject.AddComponent<AudioSource>();
                _uiSource.playOnAwake = false;
                _uiSource.loop = false;
                _uiSource.volume = 0.9f;
            }
        }

        /// <summary>
        /// Loads AudioClips from Resources/Audio/UI/ using the s_eventToClipName map.
        /// Inspector-assigned _clipMappings applied afterwards and win on conflict.
        /// </summary>
        private void AutoLoadClips()
        {
            foreach (var kvp in s_eventToClipName)
            {
                if (_clipLookup.ContainsKey(kvp.Key)) continue; // inspector already set
                var clip = Resources.Load<AudioClip>($"Audio/UI/{kvp.Value}");
                if (clip != null)
                    _clipLookup[kvp.Key] = clip;
                else
                    Debug.LogWarning($"[UnityAudioService] Clip not found in Resources: Audio/UI/{kvp.Value}");
            }
            _loadedClipsCount = _clipLookup.Count;
            Debug.Log($"[UnityAudioService] AutoLoadClips complete: {_loadedClipsCount}/{s_eventToClipName.Count} clips loaded.");
        }

        private void BuildLookup()
        {
            if (_clipMappings == null) return;
            foreach (var entry in _clipMappings)
            {
                if (entry.audioEvent != AudioEvent.None && entry.clip != null)
                    _clipLookup[entry.audioEvent] = entry.clip; // inspector wins
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Populate Clips From Resources")]
        public void PopulateClipsInEditor()
        {
            if (_clipMappings == null) _clipMappings = new List<AudioEventClipEntry>();
            _clipMappings.Clear();

            foreach (var kvp in s_eventToClipName)
            {
                var clip = Resources.Load<AudioClip>($"Audio/UI/{kvp.Value}");
                if (clip != null)
                {
                    _clipMappings.Add(new AudioEventClipEntry
                    {
                        audioEvent = kvp.Key,
                        clip = clip
                    });
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[UnityAudioService] Populated {_clipMappings.Count} clip mappings in Inspector.");
        }

        private void OnValidate()
        {
            if (_clipMappings == null || _clipMappings.Count == 0)
            {
                PopulateClipsInEditor();
            }
        }
#endif

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
