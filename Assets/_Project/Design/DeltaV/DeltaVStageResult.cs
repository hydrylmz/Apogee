namespace Apogee.Design
{
    /// <summary>
    /// Δv contribution and mass accounting for a single stage.
    /// Produced by <see cref="DeltaVCalculator"/>; consumed by the UI (read-only).
    /// </summary>
    public sealed class DeltaVStageResult
    {
        /// <summary>Stage identifier matching <see cref="RocketStage.Id"/>.</summary>
        public string StageId { get; }

        /// <summary>Ideal Δv produced by this stage alone (m/s), before losses.</summary>
        public double IdealDeltaV { get; }

        /// <summary>
        /// Δv losses applied to this stage (m/s, positive value to subtract).
        /// Covers gravity drag, atmospheric drag, and steering losses as configured
        /// in <see cref="DeltaVLossProfile"/>.
        /// </summary>
        public double LossesApplied { get; }

        /// <summary>Net Δv from this stage after losses (m/s). IdealDeltaV − LossesApplied.</summary>
        public double NetDeltaV => IdealDeltaV - LossesApplied;

        /// <summary>Specific impulse used for this stage (seconds).</summary>
        public double Isp { get; }

        /// <summary>Mass ratio = WetMass / DryMass for this stage (after jettisoning previous stages).</summary>
        public double MassRatio { get; }

        /// <summary>Effective wet mass at ignition of this stage (t), including all payload above it.</summary>
        public double EffectiveWetMass { get; }

        /// <summary>Effective dry mass at burnout of this stage (t), including all payload above it.</summary>
        public double EffectiveDryMass { get; }

        public DeltaVStageResult(
            string stageId,
            double idealDeltaV,
            double lossesApplied,
            double isp,
            double massRatio,
            double effectiveWetMass,
            double effectiveDryMass)
        {
            StageId           = stageId;
            IdealDeltaV       = idealDeltaV;
            LossesApplied     = lossesApplied;
            Isp               = isp;
            MassRatio         = massRatio;
            EffectiveWetMass  = effectiveWetMass;
            EffectiveDryMass  = effectiveDryMass;
        }

        public override string ToString() =>
            $"[{StageId}] ideal={IdealDeltaV:0.#}m/s  losses={LossesApplied:0.#}m/s  net={NetDeltaV:0.#}m/s  ratio={MassRatio:0.###}";
    }
}
