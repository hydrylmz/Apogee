using System;
using System.IO;
using UnityEngine;

namespace Apogee.UI.Media
{
    /// <summary>
    /// Uçuş akışında oynatılan geçici (placeholder) görev videolarının katalogu.
    /// Videolar <c>Assets/StreamingAssets/Videos</c> altında durur ve VideoPlayer
    /// <c>UrlSource</c> üzerinden oynatılır (Unity 2022.3 — masaüstü hedefi).
    /// (Launch sekansı theatre sisteminin geçici ikamesi — legacy sistem dokunulmadı.)
    /// </summary>
    public enum FlightVideoClip
    {
        /// <summary>Kalkış / fırlatma anı (kalkis.mp4).</summary>
        LaunchTakeoff = 0,
        /// <summary>Yüzeye iniş / EDL (inis.mp4).</summary>
        Landing = 1,
        /// <summary>Kaza / patlama (kaza.mp4).</summary>
        Crash = 2,
        /// <summary>Uçuş (seyir) sırasında geçici güneş sistemi görüntüsü (gunes_sistemi.mp4).</summary>
        Cruise = 3
    }

    /// <summary>
    /// Static katalog: klip → StreamingAssets göreli yolu, oynatma URL'si ve varlık kontrolü.
    /// </summary>
    public static class FlightVideoLibrary
    {
        private const string VideosFolder = "Videos";

        /// <summary>StreamingAssets'e göreli dosya yolu (bazı klipler eksik olabilir).</summary>
        public static string GetRelativePath(FlightVideoClip clip) => clip switch
        {
            FlightVideoClip.LaunchTakeoff => $"{VideosFolder}/kalkis.mp4",
            FlightVideoClip.Landing       => $"{VideosFolder}/inis.mp4",
            FlightVideoClip.Crash         => $"{VideosFolder}/kaza.mp4",
            FlightVideoClip.Cruise        => $"{VideosFolder}/gunes_sistemi.mp4",
            _ => null
        };

        /// <summary>UI'da gösterilecek Türkçe etiket.</summary>
        public static string GetDisplayLabel(FlightVideoClip clip) => clip switch
        {
            FlightVideoClip.LaunchTakeoff => "Kalkış Görüntüsü",
            FlightVideoClip.Landing       => "İniş Görüntüsü",
            FlightVideoClip.Crash         => "Kaza Görüntüsü",
            FlightVideoClip.Cruise        => "Güneş Sistemi Canlı Yayını",
            _ => string.Empty
        };

        /// <summary>Dosya StreamingAssets altında mevcut mu?</summary>
        public static bool Exists(FlightVideoClip clip)
        {
            string relative = GetRelativePath(clip);
            if (string.IsNullOrEmpty(relative)) return false;
            try
            {
                return File.Exists(Path.Combine(Application.streamingAssetsPath,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>VideoPlayer UrlSource için dosya URL'si (masaüstü / editör).</summary>
        public static string GetUrl(FlightVideoClip clip)
        {
            string relative = GetRelativePath(clip);
            if (string.IsNullOrEmpty(relative)) return null;

            string absolute = Path.Combine(Application.streamingAssetsPath,
                relative.Replace('/', Path.DirectorySeparatorChar));
            return "file://" + absolute.Replace('\\', '/');
        }
    }
}
