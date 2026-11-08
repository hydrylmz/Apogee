using System;
using System.Collections.Generic;
using Apogee.Data;
using Apogee.Strategy;

namespace Apogee.UI.Screens.SolarMap
{
    /// <summary>
    /// ViewModel representing a celestial body or destination on the Solar System Map (GDD §6.2, §14.1 #4, M9-06).
    /// Tracks planetary orbit angles, AU heliocentric coordinates, transfer window availability, and telemetry.
    /// </summary>
    public sealed class CelestialBodyItemViewModel
    {
        public string Id { get; }
        public string Name { get; }
        public string BodyType { get; }
        public double SemiMajorAxisAu { get; }
        public double OrbitalPeriodDays { get; }
        public double DvTotalMps { get; }
        public string TransitDurationText { get; }
        public string CommDelayText { get; }
        public IReadOnlyList<string> HazardSummary { get; }
        public DestinationRecord Record { get; }

        public string ColorHex { get; }
        public float RadiusPx { get; }
        public float OrbitVisualRadiusPercent { get; }

        public double CurrentAngleRad { get; private set; }
        public (double X, double Y) HeliocentricAu { get; private set; }
        public float NormalizedX { get; private set; }
        public float NormalizedY { get; private set; }
        public bool IsWindowOpen { get; private set; }
        public int DaysUntilNextWindow { get; private set; }
        public string WindowStatusText { get; private set; }

        public CelestialBodyItemViewModel(DestinationRecord record, GameDate? currentDate = null)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Id = record.Id;
            Name = FormatDisplayName(record.Id, record.NameKey);
            BodyType = FormatBodyType(record.Id, record.BodyType);
            SemiMajorAxisAu = PlanetaryOrbitCalculator.GetSemiMajorAxisAu(record.Id);
            OrbitalPeriodDays = PlanetaryOrbitCalculator.GetOrbitalPeriodDays(record.Id);
            DvTotalMps = record.DvTotal;

            TransitDurationText = record.TransitDaysMin == record.TransitDaysMax
                ? $"{record.TransitDaysMin} gün"
                : $"{record.TransitDaysMin}–{record.TransitDaysMax} gün";

            CommDelayText = record.CommDelaySecondsMax < 60.0
                ? $"{record.CommDelaySecondsMin:0.#} sn"
                : $"{Math.Round(record.CommDelaySecondsMin / 60.0, 1)}–{Math.Round(record.CommDelaySecondsMax / 60.0, 1)} dk";

            var hazards = new List<string>();
            if (record.Hazards != null)
            {
                foreach (var kvp in record.Hazards)
                {
                    if (kvp.Value > 0.0)
                    {
                        hazards.Add($"{FormatHazardName(kvp.Key)} (%{kvp.Value * 100:0})");
                    }
                }
            }
            HazardSummary = hazards;

            (ColorHex, RadiusPx, OrbitVisualRadiusPercent) = GetVisualAttributes(record.Id);

            UpdatePosition(currentDate ?? new GameDate(0));
        }

        public void UpdatePosition(GameDate currentDate)
        {
            int totalDays = currentDate.TotalDays;
            CurrentAngleRad = PlanetaryOrbitCalculator.GetOrbitalAngleRadians(Id, totalDays);
            HeliocentricAu = PlanetaryOrbitCalculator.GetHeliocentricPositionAu(Id, totalDays);

            // Compute normalized 0–100% position on the Orrery map canvas
            // Sun is at center (50%, 50%)
            float r = OrbitVisualRadiusPercent;
            float cos = (float)Math.Cos(CurrentAngleRad);
            float sin = (float)Math.Sin(CurrentAngleRad);

            if (Id == "moon")
            {
                // Moon orbits around Earth
                double earthAngle = PlanetaryOrbitCalculator.GetOrbitalAngleRadians("earth", totalDays);
                float earthX = 50f + 32f * (float)Math.Cos(earthAngle);
                float earthY = 50f + 32f * (float)Math.Sin(earthAngle);

                // Moon offset (~5% from Earth)
                NormalizedX = Math.Clamp(earthX + 5f * cos, 4f, 96f);
                NormalizedY = Math.Clamp(earthY + 5f * sin, 4f, 96f);
            }
            else if (Id == "leo")
            {
                // LEO stays immediately around Earth
                double earthAngle = PlanetaryOrbitCalculator.GetOrbitalAngleRadians("earth", totalDays);
                float earthX = 50f + 32f * (float)Math.Cos(earthAngle);
                float earthY = 50f + 32f * (float)Math.Sin(earthAngle);
                NormalizedX = Math.Clamp(earthX + 2.5f * cos, 4f, 96f);
                NormalizedY = Math.Clamp(earthY + 2.5f * sin, 4f, 96f);
            }
            else
            {
                NormalizedX = Math.Clamp(50f + r * cos, 4f, 96f);
                NormalizedY = Math.Clamp(50f + r * sin, 4f, 96f);
            }

            // Launch window status
            IsWindowOpen = LaunchWindowCalculator.IsWindowOpen(Record, currentDate);
            DaysUntilNextWindow = LaunchWindowCalculator.CalculateWaitDaysForNextWindow(Record, currentDate);

            if (Record.WindowPeriodDays <= 0)
            {
                WindowStatusText = "Sürekli Açık";
            }
            else if (IsWindowOpen)
            {
                WindowStatusText = "PENCERE AÇIK!";
            }
            else
            {
                WindowStatusText = $"{DaysUntilNextWindow} gün sonra";
            }
        }

        private static string FormatDisplayName(string id, string nameKey) => id?.ToLowerInvariant() switch
        {
            "leo" => "Alçak Dünya Yörüngesi (LEO)",
            "moon" => "Ay (Luna)",
            "mars" => "Mars",
            "venus" => "Venüs",
            "europa" => "Europa (Jüpiter)",
            "earth" => "Dünya",
            _ => nameKey ?? id
        };

        private static string FormatBodyType(string id, string defaultBodyType) => id?.ToLowerInvariant() switch
        {
            "leo" => "Yörünge İstasyonu",
            "moon" => "Doğal Uydu",
            "mars" => "Karasal Gezegen",
            "venus" => "Karasal Gezegen",
            "europa" => "Buz Uydusu",
            "earth" => "Karasal Gezegen",
            _ => defaultBodyType ?? "Gök Cismi"
        };

        private static string FormatHazardName(string key) => key?.ToLowerInvariant() switch
        {
            "atmosphere" => "Atmosfer Sürtünmesi",
            "radiation" => "İyonize Radyasyon",
            "heat" => "Aşırı Sıcaklık",
            "dust" => "Regolit / Toz Fırtınası",
            _ => key
        };

        private static (string ColorHex, float RadiusPx, float OrbitVisualRadiusPercent) GetVisualAttributes(string id) => id?.ToLowerInvariant() switch
        {
            "venus" => ("#E3BB7B", 18f, 18f),
            "earth" => ("#4A90E2", 20f, 32f),
            "moon" => ("#C0C0C0", 12f, 32f),
            "leo" => ("#38BDF8", 10f, 32f),
            "mars" => ("#E2583E", 16f, 48f),
            "europa" => ("#A8DADC", 14f, 68f),
            _ => ("#FFFFFF", 14f, 40f)
        };
    }
}
