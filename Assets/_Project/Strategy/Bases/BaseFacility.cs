using System;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Represents an individual agency base facility located within a specific World Map region.
    /// Complies with GDD §6.1, §19.8, §16.7.
    /// </summary>
    public sealed class BaseFacility
    {
        public string Id { get; }
        public string RegionId { get; }
        public BaseType Type { get; }
        public int Level { get; private set; }

        public BaseFacility(string id, string regionId, BaseType type, int level = 1)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrWhiteSpace(regionId)) throw new ArgumentNullException(nameof(regionId));

            Id = id;
            RegionId = regionId;
            Type = type;
            Level = Math.Clamp(level, BaseLevelSpec.MinLevel, BaseLevelSpec.MaxLevel);
        }

        public double MonthlyMaintenanceCost => BaseLevelSpec.GetMonthlyMaintenanceCost(Type, Level);
        public double CapacityRating => BaseLevelSpec.GetCapacityRating(Type, Level);
        public bool CanUpgrade => Level < BaseLevelSpec.MaxLevel;

        /// <summary>
        /// Produces a <see cref="LaunchPadInfo"/> engineering spec if this is a LaunchPad base.
        /// </summary>
        public LaunchPadInfo AsLaunchPadInfo()
        {
            if (Type != BaseType.LaunchPad) return null;
            return BaseLevelSpec.ToLaunchPadInfo(Level);
        }

        internal void SetLevel(int level)
        {
            Level = Math.Clamp(level, BaseLevelSpec.MinLevel, BaseLevelSpec.MaxLevel);
        }

        public override string ToString() =>
            $"BaseFacility[{Id} {Type} Lvl={Level} in {RegionId}]";
    }
}
