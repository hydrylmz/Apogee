using UnityEngine;

namespace Apogee.UI.Media
{
    /// <summary>
    /// Video tabanlı uçuş sunumu için merkezi özellik anahtarı (feature flag) ve
    /// <b>fallback koruması</b>.
    ///
    /// Arka plan: Fırlatma/İniş sekanslarındaki 2B/3B "Flight Theatre" sahnesi
    /// geçici olarak devre dışı bırakıldı ve yerine StreamingAssets altındaki
    /// görev videoları (kalkış / iniş / kaza / güneş sistemi) oynatılıyor.
    /// Legacy theatre kodu <b>hiç silinmedi</b> — geri dönebilmek için üç katmanlı
    /// koruma vardır:
    ///
    /// 1. <b>Ayarlar anahtarı</b> (Ayarlar → "Uçuş Videoları (Deneysel)") — kalıcı;
    ///    kapatıldığında legacy theatre tam olarak eski haliyle çalışır.
    /// 2. <b>Varlık kontrolü</b> — ilgili mp4 StreamingAssets altında yoksa o oturumda
    ///    legacy theatre kullanılır.
    /// 3. <b>Çalışma zamanı arıza fallback'i</b> — VideoPlayer bir hata verir
    ///    (<see cref="ReportPlaybackFailure"/>) ise mod oturum boyunca devre dışı
    ///    kalır ve ekran anında legacy theatre'a düşer; oyun akışı asla kırılmaz.
    /// </summary>
    public static class FlightVideoMode
    {
        /// <summary>PlayerPrefsSettingsStorage ile paylaşılan kalıcı ayar anahtarı.</summary>
        public const string SettingsKey = "apogee_settings_useflightvideos";
        public const bool DefaultEnabled = true;

        private static bool? _userEnabledCache;
        private static bool _runtimeDisabled;

        /// <summary>Kullanıcının kalıcı tercihi (Ayarlar ekranından yönetilir).</summary>
        public static bool UserEnabled
        {
            get
            {
                if (_userEnabledCache.HasValue) return _userEnabledCache.Value;
                bool value = DefaultEnabled;
                try
                {
                    if (PlayerPrefs.HasKey(SettingsKey))
                        value = PlayerPrefs.GetInt(SettingsKey, DefaultEnabled ? 1 : 0) == 1;
                }
                catch
                {
                    // PlayerPrefs yok (test harness / editör geç başlangıcı) — default
                }
                _userEnabledCache = value;
                return value;
            }
        }

        /// <summary>Bir oynatma arızası sonrası bu oturum için fallback devrede mi?</summary>
        public static bool RuntimeDisabled => _runtimeDisabled;

        /// <summary>Video modu şu anda aktif mi? Tüm ekranlar bu bayrağa bakar.</summary>
        public static bool IsActive => UserEnabled && !_runtimeDisabled;

        /// <summary>Ayarlar ekranı / kalıcı tercih değiştiğinde önbelleği senkronize eder.</summary>
        public static void SetUserEnabled(bool enabled)
        {
            _userEnabledCache = enabled;
            try
            {
                PlayerPrefs.SetInt(SettingsKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch
            {
                // Kalıcılık yazılamadı — oturum içi değer yine de güncel
            }

            if (enabled)
            {
                // Kullanıcı bilerek geri açtıysa oturumluk arızayı temizle
                ResetRuntimeState();
            }
        }

        /// <summary>
        /// Video oynatma arızası bildirimi — bu oturum boyunca video modunu kapatır,
        /// legacy theatre'a otomatik düşüşü tetikler.
        /// </summary>
        public static void ReportPlaybackFailure(string reason)
        {
            if (_runtimeDisabled) return;
            _runtimeDisabled = true;
            Debug.LogWarning($"[FlightVideoMode] Video playback failed ('{reason}'). " +
                             "Falling back to legacy flight theatre for this session. " +
                             "Re-enable via Settings > 'Uçuş Videoları (Deneysel)' to retry.");
        }

        /// <summary>Oturum içi arıza durumunu sıfırlar (test / yeniden deneme için).</summary>
        public static void ResetRuntimeState()
        {
            _runtimeDisabled = false;
        }
    }
}
