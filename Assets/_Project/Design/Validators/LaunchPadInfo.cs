using System;

namespace Apogee.Design
{
    /// <summary>
    /// Represents the engineering limits and capabilities of an agency launch pad facility.
    /// Complies with GDD §6.1 and §16.7.
    /// </summary>
    public sealed class LaunchPadInfo
    {
        public int Level { get; }
        public double MaxWetMass { get; }
        public int MaxStages { get; }

        public LaunchPadInfo(int level, double maxWetMass, int maxStages)
        {
            Level = Math.Max(1, level);
            MaxWetMass = Math.Max(0.1, maxWetMass);
            MaxStages = Math.Max(1, maxStages);
        }

        // Standard presets by facility tier (GDD §6.1, §16.7)
        public static readonly LaunchPadInfo Level1Small = new LaunchPadInfo(level: 1, maxWetMass: 60.0, maxStages: 2);
        public static readonly LaunchPadInfo Level2Medium = new LaunchPadInfo(level: 2, maxWetMass: 350.0, maxStages: 3);
        public static readonly LaunchPadInfo Level3Heavy = new LaunchPadInfo(level: 3, maxWetMass: 1500.0, maxStages: 4);
        public static readonly LaunchPadInfo Level4SuperHeavy = new LaunchPadInfo(level: 4, maxWetMass: 4000.0, maxStages: 5);

        public override string ToString() => $"[LaunchPad Tier {Level} MaxWetMass: {MaxWetMass:0.#}t MaxStages: {MaxStages}]";
    }
}
