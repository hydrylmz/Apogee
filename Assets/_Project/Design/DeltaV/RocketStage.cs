using System;

namespace Apogee.Design
{
    /// <summary>
    /// Represents one stage of a multi-stage rocket.
    /// Carries the data required for the Tsiolkovsky rocket equation:
    ///   Δv_stage = Isp × g₀ × ln(m_wet / m_dry)
    ///
    /// All masses in metric tons (t). Isp in seconds.
    /// Complies with GDD §8.1 (Rocket Equation) and §19.4 (Module schema).
    /// </summary>
    public sealed class RocketStage
    {
        /// <summary>Stage identifier / label (e.g. "Stage-1", "Upper Stage").</summary>
        public string Id { get; }

        /// <summary>
        /// Specific impulse of the engine in this stage (seconds).
        /// Determines propellant efficiency. GDD §8.1.
        /// </summary>
        public double Isp { get; }

        /// <summary>
        /// Dry mass of the stage (structure + engine, no propellant), in metric tons.
        /// </summary>
        public double DryMass { get; }

        /// <summary>
        /// Wet mass of the stage (dry mass + propellant), in metric tons.
        /// Must be ≥ DryMass.
        /// </summary>
        public double WetMass { get; }

        /// <summary>Propellant mass = WetMass − DryMass, in metric tons.</summary>
        public double PropellantMass => WetMass - DryMass;

        /// <param name="id">Human-readable stage identifier.</param>
        /// <param name="isp">Specific impulse in seconds (> 0).</param>
        /// <param name="dryMass">Dry mass in metric tons (> 0).</param>
        /// <param name="wetMass">Wet mass in metric tons (≥ dryMass).</param>
        public RocketStage(string id, double isp, double dryMass, double wetMass)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Stage id must not be empty.", nameof(id));
            if (isp <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(isp), "Isp must be > 0.");
            if (dryMass <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(dryMass), "Dry mass must be > 0.");
            if (wetMass < dryMass)
                throw new ArgumentOutOfRangeException(nameof(wetMass),
                    $"Wet mass ({wetMass} t) must be ≥ dry mass ({dryMass} t).");

            Id       = id;
            Isp      = isp;
            DryMass  = dryMass;
            WetMass  = wetMass;
        }

        public override string ToString() =>
            $"{Id}: Isp={Isp:0.#}s  dry={DryMass:0.###}t  wet={WetMass:0.###}t  prop={PropellantMass:0.###}t";
    }
}
