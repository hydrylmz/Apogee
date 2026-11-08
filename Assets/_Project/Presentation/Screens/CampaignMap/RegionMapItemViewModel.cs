using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;
using Apogee.Strategy;

namespace Apogee.UI.Screens.CampaignMap
{
    /// <summary>
    /// Presentation model for a single strategic region node on the World Map (GDD §6.1, §19.9, M9-04).
    /// Computes 2D geographic coordinate projections and lens evaluations.
    /// </summary>
    public sealed class RegionMapItemViewModel
    {
        public RegionRecord Record { get; }
        public string Id => Record.Id;
        public string Name { get; }
        public double Latitude => Record.Latitude;
        public double Longitude => Record.Longitude;

        /// <summary>
        /// Equirectangular X coordinate percentage [0.0 - 100.0%] for 2D UI positioning.
        /// Longitude -180° is 0%, +180° is 100%.
        /// </summary>
        public float NormalizedX => (float)((Record.Longitude + 180.0) / 360.0 * 100.0);

        /// <summary>
        /// Equirectangular Y coordinate percentage [0.0 - 100.0%] for 2D UI positioning.
        /// Latitude +90° is 0% (top), -90° is 100% (bottom).
        /// </summary>
        public float NormalizedY => (float)((90.0 - Record.Latitude) / 180.0 * 100.0);

        public double LensScore { get; private set; }
        public string LensScoreDisplay { get; private set; }
        public string LensDescription { get; private set; }
        public bool IsSelected { get; set; }

        public IReadOnlyList<BaseFacility> ActiveBases { get; private set; } = Array.Empty<BaseFacility>();
        public int BaseCount => ActiveBases.Count;
        public bool HasLaunchPad => ActiveBases.Any(b => b.Type == BaseType.LaunchPad);
        public bool HasFactory => ActiveBases.Any(b => b.Type == BaseType.Factory);
        public bool HasResearchCenter => ActiveBases.Any(b => b.Type == BaseType.ResearchCenter);
        public bool HasGroundStation => ActiveBases.Any(b => b.Type == BaseType.GroundStation);
        public bool HasTestFacility => ActiveBases.Any(b => b.Type == BaseType.TestFacility);

        public RegionMapItemViewModel(RegionRecord record, MapLens lens = MapLens.Overview)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Name = FormatRegionName(record.Id);
            UpdateLens(lens);
        }

        public void UpdateLens(MapLens lens)
        {
            LensScore = RegionLensEvaluator.EvaluateScore(Record, lens);
            LensScoreDisplay = $"{LensScore * 100.0:F0}%";
            LensDescription = RegionLensEvaluator.GetLensDescription(Record, lens);
        }

        public void UpdateBases(IEnumerable<BaseFacility> basesInRegion)
        {
            ActiveBases = basesInRegion != null
                ? basesInRegion.ToList()
                : (IReadOnlyList<BaseFacility>)Array.Empty<BaseFacility>();
        }

        public static string FormatRegionName(string id)
        {
            return id switch
            {
                "guiana_coast" => "Guiana Coast",
                "cape_peninsula" => "Cape Peninsula",
                "steppe_cosmodrome" => "Steppe Cosmodrome",
                "pacific_atoll" => "Pacific Atoll",
                "nordic_range" => "Nordic Range",
                "mediterranean_hub" => "Mediterranean Hub",
                "anatolian_plateau" => "Anatolian Plateau",
                "outback_station" => "Outback Station",
                _ => id
            };
        }
    }
}
