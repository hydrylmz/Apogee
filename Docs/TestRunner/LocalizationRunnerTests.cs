// LocalizationRunnerTests.cs — M8-04 TR/EN Localization System
// Verifies LocalizationManager (Apogee.Core.Localization) independently of Unity.

using System;
using System.IO;
using Apogee.Core.Localization;

namespace ApogeeLocalTestRunner
{
    public static class LocalizationRunnerTests
    {
        // ── Run registration ──────────────────────────────────────────────────────

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M8_04_Loc_DefaultLanguageIsTurkish",             Test_DefaultLanguageIsTurkish);
            runTest("M8_04_Loc_GetReturnsTurkishValue",               Test_GetReturnsTurkishValue);
            runTest("M8_04_Loc_GetReturnsEnglishValue",               Test_GetReturnsEnglishValue);
            runTest("M8_04_Loc_MissingEnglishKeyFallsBackToTurkish",  Test_MissingEnKeyFallsBackToTr);
            runTest("M8_04_Loc_CompletelyMissingKeyReturnsPlaceholder",Test_MissingKeyPlaceholder);
            runTest("M8_04_Loc_GetWithFormatArgsSubstitutes",         Test_GetWithFormatArgs);
            runTest("M8_04_Loc_SetLanguageFiresEvent",                Test_SetLanguageFiresEvent);
            runTest("M8_04_Loc_SetLanguageSameDoesNotFireEvent",      Test_SetLanguageSameNoEvent);
            runTest("M8_04_Loc_ParseLocaleJsonReadsAllEntries",        Test_ParseLocaleJson);
            runTest("M8_04_Loc_ParseLocaleJsonHandlesEscapes",         Test_ParseLocaleJsonEscapes);
            runTest("M8_04_Loc_LoadLocaleOverwritesExistingKeys",      Test_LoadLocaleOverwrites);
            runTest("M8_04_Loc_LocaleJsonFilesExistAndLoad",           Test_LocaleJsonFilesExistAndLoad);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static LocalizationManager BuildManager(string trJson = null, string enJson = null)
        {
            var mgr = new LocalizationManager();
            if (trJson != null) mgr.LoadLocale(Language.TR, trJson);
            if (enJson != null) mgr.LoadLocale(Language.EN, enJson);
            return mgr;
        }

        private static readonly string SampleTR = @"{
            ""hello"": ""Merhaba"",
            ""score"": ""Puan: {0}"",
            ""nested.key"": ""\u0130\u00e7 De\u011fer"",
            ""unicode.test"": ""T\u00fcrk\u00e7e karakter""
        }";

        private static readonly string SampleEN = @"{
            ""hello"": ""Hello"",
            ""score"": ""Score: {0}"",
            ""nested.key"": ""Inner Value""
        }";

        private static string GetJsonDir()
        {
            // Works both from bin/Debug/net10.0 and from repo root
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..",
                "Assets", "_Project", "Data", "Catalog", "Json");
            return Path.GetFullPath(path);
        }

        // ── Tests ─────────────────────────────────────────────────────────────────

        private static void Test_DefaultLanguageIsTurkish()
        {
            var mgr = new LocalizationManager();
            AssertTrue(mgr.CurrentLanguage == Language.TR,
                "Default language should be TR");
        }

        private static void Test_GetReturnsTurkishValue()
        {
            var mgr = BuildManager(SampleTR, SampleEN);
            mgr.SetLanguage(Language.TR);
            var result = mgr.Get("hello");
            AssertEqual("Merhaba", result, "TR Get(hello)");
        }

        private static void Test_GetReturnsEnglishValue()
        {
            var mgr = BuildManager(SampleTR, SampleEN);
            mgr.SetLanguage(Language.EN);
            var result = mgr.Get("hello");
            AssertEqual("Hello", result, "EN Get(hello)");
        }

        private static void Test_MissingEnKeyFallsBackToTr()
        {
            var mgr = BuildManager(SampleTR, SampleEN);
            mgr.SetLanguage(Language.EN);
            // "unicode.test" is in TR only — should fall back to TR value
            var result = mgr.Get("unicode.test");
            AssertTrue(!result.StartsWith("?"),
                $"Missing EN key should fall back to TR, not placeholder. Got: '{result}'");
        }

        private static void Test_MissingKeyPlaceholder()
        {
            var mgr = BuildManager(SampleTR, SampleEN);
            var result = mgr.Get("nonexistent.key.xyz");
            AssertTrue(result.StartsWith("?"),
                $"Completely missing key should return '?key'. Got: '{result}'");
        }

        private static void Test_GetWithFormatArgs()
        {
            var mgr = BuildManager(SampleTR, SampleEN);
            mgr.SetLanguage(Language.TR);
            var result = mgr.Get("score", 9500);
            AssertEqual("Puan: 9500", result, "Format substitution in TR");

            mgr.SetLanguage(Language.EN);
            result = mgr.Get("score", 9500);
            AssertEqual("Score: 9500", result, "Format substitution in EN");
        }

        private static void Test_SetLanguageFiresEvent()
        {
            var mgr = BuildManager(SampleTR, SampleEN);
            Language? fired = null;
            mgr.LanguageChanged += lang => fired = lang;
            mgr.SetLanguage(Language.EN);
            AssertTrue(fired == Language.EN,
                $"LanguageChanged should fire with Language.EN. Got: {fired}");
        }

        private static void Test_SetLanguageSameNoEvent()
        {
            var mgr = BuildManager(SampleTR, SampleEN);
            int fireCount = 0;
            mgr.LanguageChanged += _ => fireCount++;
            mgr.SetLanguage(Language.TR); // already TR — no event
            AssertEqual(0, fireCount, "LanguageChanged should not fire when language unchanged");
        }

        private static void Test_ParseLocaleJson()
        {
            var json = @"{ ""a"": ""Alpha"", ""b"": ""Beta"", ""c.d"": ""Gamma"" }";
            var result = LocalizationManager.ParseLocaleJson(json);
            AssertEqual(3, result.Count, "Entry count");
            AssertEqual("Alpha", result["a"], "Key 'a'");
            AssertEqual("Beta",  result["b"], "Key 'b'");
            AssertEqual("Gamma", result["c.d"], "Key 'c.d'");
        }

        private static void Test_ParseLocaleJsonEscapes()
        {
            // Test escaped newline and escaped quote
            var json = "{\"key\": \"Line1\\nLine2\\t\\\"quoted\\\"\" }";
            var result = LocalizationManager.ParseLocaleJson(json);
            AssertTrue(result.ContainsKey("key"), "Key should exist after parsing");
            AssertTrue(result["key"].Contains("\n"), "Should contain newline after escape");
            AssertTrue(result["key"].Contains("\""), "Should contain quote after escape");
        }

        private static void Test_LoadLocaleOverwrites()
        {
            var mgr = BuildManager(SampleTR);
            mgr.LoadLocale(Language.TR, @"{ ""hello"": ""Selam"" }");
            var result = mgr.Get("hello");
            AssertEqual("Selam", result, "Second LoadLocale should overwrite 'hello'");
        }

        private static void Test_LocaleJsonFilesExistAndLoad()
        {
            string dir = GetJsonDir();
            string trPath = Path.Combine(dir, "locale_tr.json");
            string enPath = Path.Combine(dir, "locale_en.json");

            AssertTrue(File.Exists(trPath), $"locale_tr.json not found at: {trPath}");
            AssertTrue(File.Exists(enPath), $"locale_en.json not found at: {enPath}");

            if (!File.Exists(trPath) || !File.Exists(enPath)) return;

            var mgr = new LocalizationManager();
            mgr.LoadLocale(Language.TR, File.ReadAllText(trPath));
            mgr.LoadLocale(Language.EN, File.ReadAllText(enPath));

            // TR spot-checks
            mgr.SetLanguage(Language.TR);
            AssertEqual("Tamam", mgr.Get("general.ok"), "TR general.ok");
            AssertEqual("GEÇMEDİ", mgr.Get("review.noGoBadge"), "TR review.noGoBadge");
            AssertEqual("Tasarım Atölyesi", mgr.Get("workshop.title"), "TR workshop.title");

            // EN spot-checks
            mgr.SetLanguage(Language.EN);
            AssertEqual("OK", mgr.Get("general.ok"), "EN general.ok");
            AssertEqual("NO-GO", mgr.Get("review.noGoBadge"), "EN review.noGoBadge");
            AssertEqual("Design Workshop", mgr.Get("workshop.title"), "EN workshop.title");
            AssertEqual("GO", mgr.Get("review.goNoBadge"), "EN review.goNoBadge");
            AssertEqual("Mission Dossier", mgr.Get("dossier.title"), "EN dossier.title");
        }

        // ── Assert helpers ────────────────────────────────────────────────────────

        private static void AssertTrue(bool condition, string message = "")
        {
            if (!condition) throw new Exception($"Expected condition to be true. {message}");
        }

        private static void AssertEqual<T>(T expected, T actual, string message = "")
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"Expected '{expected}' but got '{actual}'. {message}");
            }
        }
    }
}
