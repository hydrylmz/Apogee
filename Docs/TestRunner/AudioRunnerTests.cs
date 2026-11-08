using System;
using System.Collections.Generic;
using Apogee.Core.Audio;

namespace ApogeeLocalTestRunner
{
    public static class AudioRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M8_02_Audio_Settings_DefaultValuesAndClamping",             Test_Audio_Settings_DefaultValuesAndClamping);
            runTest("M8_02_Audio_Settings_EffectiveVolumeChannels",              Test_Audio_Settings_EffectiveVolumeChannels);
            runTest("M8_02_Audio_Settings_Mute_SetsEffectiveToZero",            Test_Audio_Settings_Mute_SetsEffectiveToZero);
            runTest("M8_02_Audio_Service_PlaySfx_RecordsEventAndFiresAction",    Test_Audio_Service_PlaySfx_RecordsEventAndFiresAction);
            runTest("M8_02_Audio_Service_PlayMusic_TracksCurrentTrackAndLoop",   Test_Audio_Service_PlayMusic_TracksCurrentTrackAndLoop);
            runTest("M8_02_Audio_Service_StopMusic_ClearsCurrentTrack",         Test_Audio_Service_StopMusic_ClearsCurrentTrack);
            runTest("M8_02_Audio_Service_ClearHistory_EmptiesList",              Test_Audio_Service_ClearHistory_EmptiesList);
            runTest("M8_02_Audio_Events_ChannelCategorization",                  Test_Audio_Events_ChannelCategorization);
        }

        private static void Test_Audio_Settings_DefaultValuesAndClamping()
        {
            var settings = new AudioSettingsData();
            AssertEqual(1.0f, settings.MasterVolume, "Default master volume is 1.0");
            AssertEqual(0.75f, settings.MusicVolume, "Default music volume is 0.75");
            AssertEqual(0.85f, settings.SfxVolume,   "Default sfx volume is 0.85");
            AssertEqual(false, settings.IsMuted,     "Default isMuted is false");

            // Clamping test
            settings.MasterVolume = 1.5f;
            AssertEqual(1.0f, settings.MasterVolume, "Master volume clamped to 1.0");

            settings.MusicVolume = -0.3f;
            AssertEqual(0.0f, settings.MusicVolume,  "Music volume clamped to 0.0");
        }

        private static void Test_Audio_Settings_EffectiveVolumeChannels()
        {
            var settings = new AudioSettingsData(master: 0.5f, music: 0.8f, sfx: 0.6f, ui: 0.9f);

            // Effective = master * channel
            AssertApproxEqual(0.5f * 0.8f, settings.GetEffectiveVolume(AudioChannel.Music), 1e-4f, "Effective music volume");
            AssertApproxEqual(0.5f * 0.6f, settings.GetEffectiveVolume(AudioChannel.Sfx),   1e-4f, "Effective sfx volume");
            AssertApproxEqual(0.5f * 0.9f, settings.GetEffectiveVolume(AudioChannel.Ui),    1e-4f, "Effective ui volume");
            AssertApproxEqual(0.5f,        settings.GetEffectiveVolume(AudioChannel.Master), 1e-4f, "Effective master volume");
        }

        private static void Test_Audio_Settings_Mute_SetsEffectiveToZero()
        {
            var settings = new AudioSettingsData(master: 1.0f, music: 1.0f, sfx: 1.0f, ui: 1.0f, muted: true);
            AssertEqual(0.0f, settings.GetEffectiveVolume(AudioChannel.Master), "Muted master volume is 0");
            AssertEqual(0.0f, settings.GetEffectiveVolume(AudioChannel.Music),  "Muted music volume is 0");
            AssertEqual(0.0f, settings.GetEffectiveVolume(AudioChannel.Sfx),    "Muted sfx volume is 0");
            AssertEqual(0.0f, settings.GetEffectiveVolume(AudioChannel.Ui),     "Muted ui volume is 0");
        }

        private static void Test_Audio_Service_PlaySfx_RecordsEventAndFiresAction()
        {
            var audio = new NullAudioService();
            AudioEvent receivedEvent = AudioEvent.None;
            AudioChannel receivedChannel = AudioChannel.Master;

            audio.AudioTriggered += (evt, ch) =>
            {
                receivedEvent = evt;
                receivedChannel = ch;
            };

            audio.PlaySfx(AudioEvent.ButtonClick);
            AssertEqual(AudioEvent.ButtonClick, receivedEvent, "Triggered event matches");
            AssertEqual(AudioChannel.Sfx, receivedChannel,     "Triggered channel is Sfx");
            AssertEqual(1, audio.PlaybackHistory.Count,        "Playback history count is 1");
            AssertEqual(AudioEvent.ButtonClick, audio.PlaybackHistory[0], "Playback history contains ButtonClick");

            // None event should be ignored
            audio.PlaySfx(AudioEvent.None);
            AssertEqual(1, audio.PlaybackHistory.Count, "None event is ignored");
        }

        private static void Test_Audio_Service_PlayMusic_TracksCurrentTrackAndLoop()
        {
            var audio = new NullAudioService();
            AudioEvent triggered = AudioEvent.None;
            audio.AudioTriggered += (evt, ch) => triggered = evt;

            audio.PlayMusic(AudioEvent.MusicMainMenu);
            AssertEqual(AudioEvent.MusicMainMenu, audio.CurrentMusic, "Current track is MusicMainMenu");
            AssertEqual(AudioEvent.MusicMainMenu, triggered,          "Music trigger event fired");
            AssertEqual(1, audio.PlaybackHistory.Count,               "Playback history tracked music");

            audio.PlayMusic(AudioEvent.MusicLaunchTension);
            AssertEqual(AudioEvent.MusicLaunchTension, audio.CurrentMusic, "Current track updated");
            AssertEqual(2, audio.PlaybackHistory.Count,                    "Playback history count updated");
        }

        private static void Test_Audio_Service_StopMusic_ClearsCurrentTrack()
        {
            var audio = new NullAudioService();
            audio.PlayMusic(AudioEvent.MusicStrategyAmbient);
            AssertEqual(AudioEvent.MusicStrategyAmbient, audio.CurrentMusic, "Music playing");

            audio.StopMusic();
            AssertEqual(AudioEvent.None, audio.CurrentMusic, "Music stopped and reset to None");
        }

        private static void Test_Audio_Service_ClearHistory_EmptiesList()
        {
            var audio = new NullAudioService();
            audio.PlaySfx(AudioEvent.CountdownBeep);
            audio.PlaySfx(AudioEvent.RocketIgnition);
            AssertEqual(2, audio.PlaybackHistory.Count, "History has 2 events");

            audio.ClearHistory();
            AssertEqual(0, audio.PlaybackHistory.Count, "History cleared");
        }

        private static void Test_Audio_Events_ChannelCategorization()
        {
            var sfxEvents = new[] { AudioEvent.ButtonClick, AudioEvent.RocketIgnition, AudioEvent.LandingSuccess };
            foreach (var evt in sfxEvents)
            {
                AssertTrue(evt != AudioEvent.None, $"Event {evt} is valid");
            }

            var musicEvents = new[] { AudioEvent.MusicMainMenu, AudioEvent.MusicStrategyAmbient, AudioEvent.MusicVictoryTheme };
            foreach (var mus in musicEvents)
            {
                AssertTrue((int)mus >= 50, $"Music event {mus} is in music range");
            }
        }

        // ── Assertion helpers ─────────────────────────────────────────────────

        private static void AssertEqual<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Expected '{expected}' but got '{actual}'. {message}");
        }

        private static void AssertApproxEqual(float expected, float actual, float tolerance = 1e-4f, string message = "")
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception($"Expected '{expected:0.####}' but got '{actual:0.####}'. {message}");
        }

        private static void AssertTrue(bool condition, string message = "")
        {
            if (!condition) throw new Exception($"Expected condition to be true. {message}");
        }
    }
}
