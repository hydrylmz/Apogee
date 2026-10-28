using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Immutable result of a multi-stage Δv calculation.
    /// Contains per-stage detail and the overall totals.
    /// Produced by <see cref="DeltaVCalculator.Calculate"/>.
    /// </summary>
    public sealed class DeltaVCalculationResult
    {
        /// <summary>Per-stage breakdown, ordered from first (lowest) to last (upper) stage.</summary>
        public IReadOnlyList<DeltaVStageResult> Stages { get; }

        /// <summary>
        /// Sum of ideal (loss-free) Δv across all stages (m/s).
        /// Useful for comparing theoretical performance.
        /// </summary>
        public double TotalIdealDeltaV { get; }

        /// <summary>
        /// Total Δv losses applied (m/s), including gravity drag, atmospheric drag,
        /// and steering losses from <see cref="DeltaVLossProfile"/>.
        /// </summary>
        public double TotalLosses { get; }

        /// <summary>
        /// Net Δv available for mission use after losses (m/s).
        /// This is what the mission profile's requirement is checked against.
        /// NetDeltaV = TotalIdealDeltaV − TotalLosses.
        /// </summary>
        public double NetDeltaV => TotalIdealDeltaV - TotalLosses;

        /// <summary>Total gross (wet) launch mass of the entire stack (t).</summary>
        public double TotalWetMass { get; }

        /// <summary>Total structural dry mass of the entire stack (t).</summary>
        public double TotalDryMass { get; }

        /// <summary>Total propellant mass across all stages (t).</summary>
        public double TotalPropellantMass => TotalWetMass - TotalDryMass;

        /// <summary>
        /// Payload mass delivered to orbit / upper trajectory (t).
        /// This is the dry mass of the final stage's payload (anything above the last engine stage).
        /// Provided for convenience; set by the calculator from the payload input.
        /// </summary>
        public double PayloadMass { get; }

        /// <summary>Number of stages.</summary>
        public int StageCount => Stages.Count;

        internal DeltaVCalculationResult(
            IReadOnlyList<DeltaVStageResult> stages,
            double totalIdealDeltaV,
            double totalLosses,
            double totalWetMass,
            double totalDryMass,
            double payloadMass)
        {
            Stages            = stages ?? throw new ArgumentNullException(nameof(stages));
            TotalIdealDeltaV  = totalIdealDeltaV;
            TotalLosses       = totalLosses;
            TotalWetMass      = totalWetMass;
            TotalDryMass      = totalDryMass;
            PayloadMass       = payloadMass;
        }

        public override string ToString() =>
            $"Δv net={NetDeltaV:0.#}m/s  ideal={TotalIdealDeltaV:0.#}m/s  " +
            $"losses={TotalLosses:0.#}m/s  mass={TotalWetMass:0.###}t  stages={StageCount}";
    }
}
