using System;

namespace Apogee.Core.Audio
{
    /// <summary>
    /// Audio configuration settings model (GDD §14.1 #19, §15.4, M8-02).
    /// Pure C# — no UnityEngine references. Clamps volumes safely between 0.0 and 1.0.
    /// </summary>
    public sealed class AudioSettingsData
    {
        private float _masterVolume = 1.0f;
        private float _musicVolume  = 0.75f;
        private float _sfxVolume    = 0.85f;
        private float _uiVolume     = 0.90f;
        private bool  _isMuted      = false;

        public float MasterVolume
        {
            get => _masterVolume;
            set => _masterVolume = Clamp(value);
        }

        public float MusicVolume
        {
            get => _musicVolume;
            set => _musicVolume = Clamp(value);
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Clamp(value);
        }

        public float UiVolume
        {
            get => _uiVolume;
            set => _uiVolume = Clamp(value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => _isMuted = value;
        }

        public AudioSettingsData() { }

        public AudioSettingsData(float master, float music, float sfx, float ui = 0.9f, bool muted = false)
        {
            MasterVolume = master;
            MusicVolume  = music;
            SfxVolume    = sfx;
            UiVolume     = ui;
            IsMuted      = muted;
        }

        /// <summary>
        /// Calculates the effective volume (0.0 to 1.0) taking Master volume and Mute into account.
        /// </summary>
        public float GetEffectiveVolume(AudioChannel channel)
        {
            if (_isMuted) return 0.0f;

            float channelVol = channel switch
            {
                AudioChannel.Master   => 1.0f,
                AudioChannel.Music    => _musicVolume,
                AudioChannel.Sfx      => _sfxVolume,
                AudioChannel.Ambience => _musicVolume * 0.8f,
                AudioChannel.Ui       => _uiVolume,
                _                     => 1.0f
            };

            return Clamp(_masterVolume * channelVol);
        }

        private static float Clamp(float v) => Math.Max(0.0f, Math.Min(1.0f, v));
    }
}
