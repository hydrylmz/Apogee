using System;

namespace Apogee.Design
{
    /// <summary>
    /// Material properties applied to structural components (e.g. tanks, structural fairings/bus).
    /// Complies with GDD §16.4 and §19.5.
    /// </summary>
    public sealed class MaterialSpec
    {
        public string Id { get; }
        public string Name { get; }
        public double MassMultiplier { get; }
        public double CostMultiplier { get; }
        public double BuildTimeMultiplier { get; }

        public MaterialSpec(
            string id,
            string name = null,
            double massMultiplier = 1.0,
            double costMultiplier = 1.0,
            double buildTimeMultiplier = 1.0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Material ID cannot be empty.", nameof(id));

            Id = id;
            Name = name ?? id;
            MassMultiplier = massMultiplier > 0.0 ? massMultiplier : 1.0;
            CostMultiplier = costMultiplier > 0.0 ? costMultiplier : 1.0;
            BuildTimeMultiplier = buildTimeMultiplier > 0.0 ? buildTimeMultiplier : 1.0;
        }

        public static readonly MaterialSpec AluminumAlloy = new MaterialSpec("al-alloy", "Aluminum Alloy", 1.0, 1.0, 1.0);
        public static readonly MaterialSpec AluminumLithium = new MaterialSpec("al-li", "Al-Li Alloy", 0.88, 1.35, 1.15);
        public static readonly MaterialSpec Titanium = new MaterialSpec("titanium", "Titanium Alloy", 0.92, 1.80, 1.50);
        public static readonly MaterialSpec CarbonComposite = new MaterialSpec("carbon-composite", "Carbon Composite", 0.75, 2.50, 1.80);

        public override string ToString() => $"[Material: {Id} Mass x{MassMultiplier:0.##} Cost x{CostMultiplier:0.##}]";
    }
}
