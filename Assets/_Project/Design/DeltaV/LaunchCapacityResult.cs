using System;

namespace Apogee.Design
{
    /// <summary>
    /// Immutable result of a launch capacity analysis.
    /// Expresses how much payload mass a rocket stack can deliver to a target trajectory
    /// with a given Delta-V requirement, computed by <see cref="LaunchCapacityCalculator"/>.
    ///
    /// GDD §8.1 — "Fırlatıcı kapasitesi: Roketin hedef yörüngeye taşıyabileceği kütle"
    /// GDD §21 M1-08.
    /// </summary>
    public sealed class LaunchCapacityResult
    {
        /// <summary>
        /// The target net Delta-V the rocket must achieve (m/s).
        /// This is the mission requirement value passed to the calculator.
        /// </summary>
        public double RequiredDeltaV { get; }

        /// <summary>
        /// Maximum payload mass (t) the rocket can deliver to the target trajectory
        /// while still meeting <see cref="RequiredDeltaV"/>.
        /// Returns 0 when the rocket cannot meet the requirement even with zero payload.
        /// </summary>
        public double MaxPayloadMass { get; }

        /// <summary>
        /// Actual payload mass used for the current design (t).
        /// Provided by the evaluator from the blueprint's spacecraft dry mass.
        /// </summary>
        public double ActualPayloadMass { get; }

        /// <summary>
        /// Margin = (MaxPayloadMass − ActualPayloadMass) / max(MaxPayloadMass, ε).
        /// Positive  → payload fits with headroom.
        /// Zero      → exact capacity boundary.
        /// Negative  → payload exceeds capacity (overloaded).
        /// </summary>
        public double CapacityMargin { get; }

        /// <summary>
        /// Whether the actual payload mass is within the computed capacity.
        /// True when ActualPayloadMass ≤ MaxPayloadMass.
        /// </summary>
        public bool IsWithinCapacity => ActualPayloadMass <= MaxPayloadMass;

        /// <summary>
        /// Net Delta-V produced by the rocket when carrying exactly <see cref="MaxPayloadMass"/> (m/s).
        /// Should be very close to <see cref="RequiredDeltaV"/> (within solver tolerance).
        /// </summary>
        public double NetDeltaVAtMaxPayload { get; }

        /// <summary>
        /// Net Delta-V produced by the rocket with the actual spacecraft payload (m/s).
        /// Provided for convenience; matches <see cref="DeltaVCalculationResult.NetDeltaV"/>
        /// from the current design.
        /// </summary>
        public double NetDeltaVAtActualPayload { get; }

        /// <summary>
        /// Whether the capacity solver converged within tolerance.
        /// False indicates the binary search hit its iteration limit — the result is an
        /// approximation, but should still be accurate to the stated <see cref="SolverToleranceMetersPerSecond"/>.
        /// </summary>
        public bool SolverConverged { get; }

        /// <summary>
        /// The Delta-V tolerance (m/s) used during the binary search.
        /// </summary>
        public double SolverToleranceMetersPerSecond { get; }

        internal LaunchCapacityResult(
            double requiredDeltaV,
            double maxPayloadMass,
            double actualPayloadMass,
            double netDeltaVAtMaxPayload,
            double netDeltaVAtActualPayload,
            bool solverConverged,
            double solverToleranceMetersPerSecond)
        {
            RequiredDeltaV             = requiredDeltaV;
            MaxPayloadMass             = Math.Max(0.0, maxPayloadMass);
            ActualPayloadMass          = Math.Max(0.0, actualPayloadMass);
            NetDeltaVAtMaxPayload      = netDeltaVAtMaxPayload;
            NetDeltaVAtActualPayload   = netDeltaVAtActualPayload;
            SolverConverged            = solverConverged;
            SolverToleranceMetersPerSecond = solverToleranceMetersPerSecond;

            double denom = Math.Max(MaxPayloadMass, 1e-9);
            CapacityMargin = (MaxPayloadMass - ActualPayloadMass) / denom;
        }

        public override string ToString() =>
            $"[LaunchCapacity] maxPayload={MaxPayloadMass:0.###}t actual={ActualPayloadMass:0.###}t " +
            $"margin={CapacityMargin:P1} Δv@max={NetDeltaVAtMaxPayload:0.#}m/s converged={SolverConverged}";
    }
}
