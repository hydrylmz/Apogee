using System;
using System.Collections.Generic;
using System.Globalization;

namespace Apogee.Core.Localization
{
    /// <summary>
    /// Thread-safe, pure-C# localization manager for APOGEE (GDD §17.6).
    ///
    /// Design decisions:
    ///  - Two locale tables are loaded at startup from JSON (locale_tr.json / locale_en.json).
    ///  - Look-up is O(1) via Dictionary.
    ///  - Missing key → returns the key string prefixed with '?' so missing entries are
    ///    immediately visible during development without crashing.
    ///  - Singleton pattern for convenience; can also be instantiated directly for tests.
    ///  - No UnityEngine dependency — safe to use in Core/Design/Strategy (noEngineReferences).
    /// </summary>
    public sealed class LocalizationManager : ILocalizationManager
    {
        // ── Singleton ─────────────────────────────────────────────────────────────
        private static LocalizationManager _instance;

        /// <summary>
        /// Global singleton instance. Created lazily on first access.
        /// Tests should use <c>new LocalizationManager()</c> for isolation.
        /// </summary>
        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LocalizationManager();
                }
                return _instance;
            }
        }

        /// <summary>Replaces the singleton (useful for unit tests).</summary>
        public static void SetInstance(LocalizationManager instance)
        {
            _instance = instance;
        }

        // ── State ─────────────────────────────────────────────────────────────────
        private readonly Dictionary<Language, Dictionary<string, string>> _tables
            = new Dictionary<Language, Dictionary<string, string>>();

        private Language _currentLanguage = Language.TR;

        public Language CurrentLanguage => _currentLanguage;

        public event Action<Language> LanguageChanged;

        // ── Construction ─────────────────────────────────────────────────────────
        public LocalizationManager()
        {
            // Pre-create empty tables so Get() never throws KeyNotFoundException.
            _tables[Language.TR] = new Dictionary<string, string>(StringComparer.Ordinal);
            _tables[Language.EN] = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // ── ILocalizationManager ─────────────────────────────────────────────────

        /// <inheritdoc />
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var table = _tables[_currentLanguage];
            if (table.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fallback: try TR if current language is EN and key is missing.
            if (_currentLanguage == Language.EN && _tables[Language.TR].TryGetValue(key, out var trFallback))
            {
                return trFallback;
            }

            // Key not found — return a visibly broken placeholder for dev builds.
            return $"?{key}";
        }

        /// <inheritdoc />
        public string Get(string key, params object[] args)
        {
            var raw = Get(key);
            if (args == null || args.Length == 0)
            {
                return raw;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, raw, args);
            }
            catch (FormatException)
            {
                // Malformed format string — return raw value rather than crash.
                return raw;
            }
        }

        /// <inheritdoc />
        public void SetLanguage(Language language)
        {
            if (_currentLanguage == language)
            {
                return;
            }

            _currentLanguage = language;
            LanguageChanged?.Invoke(language);
        }

        /// <inheritdoc />
        public void LoadLocale(Language language, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var entries = ParseLocaleJson(json);
            var table = _tables[language];
            foreach (var kv in entries)
            {
                table[kv.Key] = kv.Value;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Minimal JSON parser for flat key→value dictionaries.
        /// Avoids a Newtonsoft dependency in Core (noEngineReferences).
        /// Format: { "key": "value", ... }
        /// Supports: escaped quotes (\"), escaped newlines (\n, \r), basic Unicode escapes.
        /// </summary>
        internal static Dictionary<string, string> ParseLocaleJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            int i = 0;
            int len = json.Length;

            // Skip to opening '{'
            while (i < len && json[i] != '{') i++;
            i++; // consume '{'

            while (i < len)
            {
                // Skip whitespace
                while (i < len && char.IsWhiteSpace(json[i])) i++;
                if (i >= len || json[i] == '}') break;

                // Expect '"' for key
                if (json[i] != '"') { i++; continue; }

                string key = ReadJsonString(json, ref i);

                // Skip ':' and whitespace
                while (i < len && (json[i] == ':' || char.IsWhiteSpace(json[i]))) i++;

                // Expect '"' for value (we only support string values)
                if (i >= len || json[i] != '"') { SkipValue(json, ref i, len); continue; }

                string value = ReadJsonString(json, ref i);
                result[key] = value;

                // Skip comma
                while (i < len && (json[i] == ',' || char.IsWhiteSpace(json[i]))) i++;
            }

            return result;
        }

        private static string ReadJsonString(string json, ref int i)
        {
            i++; // consume opening '"'
            var sb = new System.Text.StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"') break; // closing quote
                if (c == '\\' && i < json.Length)
                {
                    char esc = json[i++];
                    switch (esc)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case '/':  sb.Append('/');  break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'u':
                            // 4-hex Unicode escape
                            if (i + 3 < json.Length)
                            {
                                string hex = json.Substring(i, 4);
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                }
                                i += 4;
                            }
                            break;
                        default:   sb.Append(esc);  break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static void SkipValue(string json, ref int i, int len)
        {
            while (i < len && json[i] != ',' && json[i] != '}') i++;
        }
    }
}
