namespace Apogee.Core.Localization
{
    /// <summary>
    /// Contract for the APOGEE localization system (GDD §17.6).
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public interface ILocalizationManager
    {
        /// <summary>Currently active language.</summary>
        Language CurrentLanguage { get; }

        /// <summary>
        /// Returns the localized string for <paramref name="key"/> in the active language.
        /// Falls back to the key itself when the entry is missing (never throws).
        /// </summary>
        string Get(string key);

        /// <summary>
        /// Returns the localized string for <paramref name="key"/> with optional string.Format
        /// substitutions. Example: Get("ui.budget", 1500) → "Bütçe: 1.500 ₺".
        /// </summary>
        string Get(string key, params object[] args);

        /// <summary>
        /// Changes the active language and raises <see cref="LanguageChanged"/>.
        /// </summary>
        void SetLanguage(Language language);

        /// <summary>
        /// Loads locale entries from a JSON string (key → value dictionary).
        /// Call once per language on startup.
        /// </summary>
        void LoadLocale(Language language, string json);

        /// <summary>Fired after SetLanguage completes.</summary>
        event System.Action<Language> LanguageChanged;
    }
}
