using System;
using Apogee.Design;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure mathematical specification for base upgrade costs, maintenance expenses,
    /// build times, and operational capacities per tier level (1-5).
    /// All values are data-driven gameplay constants complying with GDD §6.1, §16.7, and §19.8.
    /// Uncalibrated figures are denoted TODO-VERİ.
    /// </summary>
    public static class BaseLevelSpec
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        /// <summary>
        /// Monthly maintenance cost in million USD (M$) for a base of the specified type and level.
        /// </summary>
        public static double GetMonthlyMaintenanceCost(BaseType type, int level)
        {
            int lvl = Math.Clamp(level, MinLevel, MaxLevel);

            switch (type)
            {
                case BaseType.LaunchPad:
                    // Launch pads have high maintenance per tier (TODO-VERİ: 1.5M$ - 15M$)
                    return Math.Round(1.5 * Math.Pow(1.8, lvl - 1), 2);

                case BaseType.Factory:
                    return Math.Round(2.0 * Math.Pow(1.7, lvl - 1), 2);

                case BaseType.ResearchCenter:
                    return Math.Round(1.8 * Math.Pow(1.75, lvl - 1), 2);

                case BaseType.GroundStation:
                    return Math.Round(0.8 * Math.Pow(1.6, lvl - 1), 2);

                case BaseType.TestFacility:
                default:
                    return Math.Round(1.2 * Math.Pow(1.65, lvl - 1), 2);
            }
        }

        /// <summary>
        /// Construction / expansion cost in million USD (M$) to build or upgrade to targetLevel.
        /// </summary>
        public static double GetUpgradeCost(BaseType type, int targetLevel, double laborCostMultiplier = 1.0)
        {
            int lvl = Math.Clamp(targetLevel, MinLevel, MaxLevel);
            double baseCost;

            switch (type)
            {
                case BaseType.LaunchPad:
                    // Level 1: 50M$, Level 2: 120M$, Level 3: 260M$, Level 4: 550M$, Level 5: 1100M$ (TODO-VERİ)
                    baseCost = 50.0 * Math.Pow(2.15, lvl - 1);
                    break;

                case BaseType.Factory:
                    baseCost = 40.0 * Math.Pow(2.0, lvl - 1);
                    break;

                case BaseType.ResearchCenter:
                    baseCost = 45.0 * Math.Pow(2.05, lvl - 1);
                    break;

                case BaseType.GroundStation:
                    baseCost = 25.0 * Math.Pow(1.9, lvl - 1);
                    break;

                case BaseType.TestFacility:
                default:
                    baseCost = 35.0 * Math.Pow(2.0, lvl - 1);
                    break;
            }

            return Math.Round(baseCost * Math.Max(0.5, laborCostMultiplier), 1);
        }

        /// <summary>
        /// Build / expansion duration in game-days to reach targetLevel.
        /// </summary>
        public static int GetUpgradeDurationDays(BaseType type, int targetLevel)
        {
            int lvl = Math.Clamp(targetLevel, MinLevel, MaxLevel);
            switch (type)
            {
                case BaseType.LaunchPad:
                    return 180 + (lvl - 1) * 120; // 180d to 660d

                case BaseType.Factory:
                    return 150 + (lvl - 1) * 100;

                case BaseType.ResearchCenter:
                    return 120 + (lvl - 1) * 90;

                case BaseType.GroundStation:
                    return 90 + (lvl - 1) * 60;

                case BaseType.TestFacility:
                default:
                    return 120 + (lvl - 1) * 80;
            }
        }

        /// <summary>
        /// Operational capacity rating for the base type at this level:
        /// - LaunchPad: Max liftoff wet mass in metric tons (t)
        /// - Factory: Monthly production capacity units
        /// - ResearchCenter: Monthly research points generated
        /// - GroundStation: Daily downlink data bandwidth in GB/day
        /// - TestFacility: System reliability bonus percent points
        /// </summary>
        public static double GetCapacityRating(BaseType type, int level)
        {
            int lvl = Math.Clamp(level, MinLevel, MaxLevel);

            switch (type)
            {
                case BaseType.LaunchPad:
                    // Wet mass capacity: Level 1=60t, 2=350t, 3=1500t, 4=4000t, 5=9000t
                    switch (lvl)
                    {
                        case 1: return 60.0;
                        case 2: return 350.0;
                        case 3: return 1500.0;
                        case 4: return 4000.0;
                        case 5: default: return 9000.0;
                    }

                case BaseType.Factory:
                    return 10.0 * lvl; // 10 to 50 production throughput units

                case BaseType.ResearchCenter:
                    return 15.0 * lvl; // 15 to 75 research points / month

                case BaseType.GroundStation:
                    return 25.0 * lvl; // 25 to 125 GB/day downlink throughput

                case BaseType.TestFacility:
                default:
                    return 0.03 * lvl; // +3% to +15% reliability verification bonus
            }
        }

        /// <summary>
        /// Converts a LaunchPad base level into a <see cref="LaunchPadInfo"/> engineering specification
        /// used by Design validators.
        /// </summary>
        public static LaunchPadInfo ToLaunchPadInfo(int level)
        {
            int lvl = Math.Clamp(level, MinLevel, MaxLevel);
            double maxWetMass = GetCapacityRating(BaseType.LaunchPad, lvl);
            int maxStages = Math.Min(5, lvl + 1); // Level 1 supports 2 stages, Level 4-5 supports up to 5 stages
            return new LaunchPadInfo(lvl, maxWetMass, maxStages);
        }
    }
}
