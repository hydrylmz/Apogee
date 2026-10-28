using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Configuration for an individual propulsion stage in a rocket stack.
    /// Complies with GDD §7.2, §8.1, and §19.11.
    /// </summary>
    public sealed class StageDesign
    {
        private readonly List<DesignComponent> _extraComponents = new List<DesignComponent>();

        public string Id { get; }
        public string Name { get; }
        public DesignComponent Engine { get; }
        public DesignComponent Tank { get; }
        public double PropellantMass { get; }
        public MaterialSpec StructureMaterial { get; }
        public SupplierSpec StageSupplier { get; }
        public IReadOnlyList<DesignComponent> ExtraComponents => _extraComponents;

        public double DryMass
        {
            get
            {
                double total = 0.0;
                if (Engine != null) total += Engine.EffectiveDryMass;
                if (Tank != null) total += Tank.EffectiveDryMass;
                foreach (var c in _extraComponents)
                {
                    total += c.EffectiveDryMass;
                }
                return total;
            }
        }

        public double WetMass => DryMass + PropellantMass;

        public double Cost
        {
            get
            {
                double total = 0.0;
                if (Engine != null) total += Engine.EffectiveCost;
                if (Tank != null) total += Tank.EffectiveCost;
                foreach (var c in _extraComponents)
                {
                    total += c.EffectiveCost;
                }
                return total;
            }
        }

        public int BuildTimeDays
        {
            get
            {
                int max = 0;
                if (Engine != null) max = Math.Max(max, Engine.EffectiveBuildTime);
                if (Tank != null) max = Math.Max(max, Tank.EffectiveBuildTime);
                foreach (var c in _extraComponents)
                {
                    max = Math.Max(max, c.EffectiveBuildTime);
                }
                return max;
            }
        }

        public double Reliability
        {
            get
            {
                double r = 1.0;
                if (Engine != null) r *= Engine.EffectiveReliability;
                if (Tank != null) r *= Tank.EffectiveReliability;
                foreach (var c in _extraComponents)
                {
                    r *= c.EffectiveReliability;
                }
                return r;
            }
        }

        public StageDesign(
            string id,
            string name = null,
            DesignComponent engine = null,
            DesignComponent tank = null,
            double propellantMass = 0.0,
            MaterialSpec structureMaterial = null,
            SupplierSpec stageSupplier = null,
            IEnumerable<DesignComponent> extraComponents = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Stage ID cannot be empty.", nameof(id));

            Id = id;
            Name = name ?? id;
            Engine = engine;
            Tank = tank;
            PropellantMass = Math.Max(0.0, propellantMass);
            StructureMaterial = structureMaterial ?? MaterialSpec.AluminumAlloy;
            StageSupplier = stageSupplier ?? SupplierSpec.Default;

            if (extraComponents != null)
            {
                _extraComponents.AddRange(extraComponents);
            }
        }

        /// <summary>
        /// Converts this stage into a <see cref="RocketStage"/> suitable for <see cref="DeltaVCalculator"/>.
        /// </summary>
        public RocketStage ToRocketStage()
        {
            double isp = Engine != null && Engine.Isp > 0.0 ? Engine.Isp : 300.0;
            double dry = Math.Max(0.001, DryMass);
            double wet = dry + PropellantMass;
            return new RocketStage(Id, isp, dry, wet);
        }

        public override string ToString() => $"{Id}: dry={DryMass:0.##}t wet={WetMass:0.##}t prop={PropellantMass:0.##}t cost={Cost:0.##}M$";
    }
}
