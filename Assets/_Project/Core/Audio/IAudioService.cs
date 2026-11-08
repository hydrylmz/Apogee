using System;

namespace Apogee.Core.Audio
{
    /// <summary>
    /// Service contract for audio playback and channel volume control (GDD §15.4, M8-02).
    /// Pure C# interface — implemented by presentation layer (Unity AudioSource/Mixer) or mock service.
    /// </summary>
    public interface IAudioService
    {
        AudioSettingsData Settings { get; }

        event Action<AudioEvent, AudioChannel> AudioTriggered;

        void PlaySfx(AudioEvent audioEvent, float pitchMultiplier = 1.0f);
        void PlayMusic(AudioEvent musicEvent, bool loop = true, float fadeDurationSeconds = 1.0f);
        void StopMusic(float fadeDurationSeconds = 0.5f);
        void SetSettings(AudioSettingsData settings);
        void SetMute(bool muted);
    }
}
