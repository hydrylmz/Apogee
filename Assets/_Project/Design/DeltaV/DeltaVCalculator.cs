using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Calculates the total Δv capacity of a multi-stage rocket using the
    /// Tsiolkovsky rocket equation, applying ascent losses and latitude advantages
    /// from a <see cref="DeltaVLossProfile"/>.
    ///
    /// GDD §8.1 — "Aşama başına Δv = Isp × g₀ × ln(başlangıç kütle / bitiş kütle); aşamalar toplanır"
    ///
    /// Assumptions and simplifications (GDD §8.5):
    ///   • Circular-orbit, Hohmann-transfer model — no patched-conic propagation.
    ///   • Stages are fired sequentially; each spent stage is immediately jettisoned.
    ///   • The payload sits above the last propulsion stage and contributes only mass.
    ///   • Losses (gravity drag, atmospheric drag, steering) are applied only to
    ///     Stage-1 as a flat Δv budget deduction; upper stages are in-space.
    ///   • Latitude advantage is a global deduction from the net Δv requirement
    ///     (handled externally) — the calculator reports it but does NOT add it to
    ///     capacity; the mission validator subtracts it from the required Δv.
    ///
    /// All masses in metric tons (t). Isp in seconds. Δv in m/s.
    /// </summary>
    public static class DeltaVCalculator
    {
        /// <summary>
        /// Standard gravity (m/s²) — CODATA exact value used in the rocket equation.
        /// </summary>
        public const double G0 = 9.80665;

        /// <summary>
        /// Calculates the multi-stage Δv for a rocket stack.
        ///
        /// Stages are provided from bottom (first ignited) to top (last ignited).
        /// Payload mass is the dry mass of everything above the uppermost propulsion stage.
        /// </summary>
        /// <param name="stages">
        ///   Ordered list of rocket stages, index 0 = first stage (booster) → last = upper stage.
        ///   Must contain at least one stage.
        /// </param>
        /// <param name="payloadMass">
        ///   Mass of the payload (spacecraft + fairing + adapter) in metric tons. Must be ≥ 0.
        /// </param>
        /// <param name="lossProfile">
        ///   Loss budget for this trajectory. Pass <see cref="DeltaVLossProfile.None"/>
        ///   for in-space stages (already-orbiting stacks, etc.).
        /// </param>
        /// <returns>A <see cref="DeltaVCalculationResult"/> with per-stage breakdown.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="stages"/> or <paramref name="lossProfile"/> is null.</exception>
        /// <exception cref="ArgumentException">If <paramref name="stages"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="payloadMass"/> is negative.</exception>
        public static DeltaVCalculationResult Calculate(
            IReadOnlyList<RocketStage> stages,
            double payloadMass,
            DeltaVLossProfile lossProfile)
        {
            if (stages == null)       throw new ArgumentNullException(nameof(stages));
            if (lossProfile == null)  throw new ArgumentNullException(nameof(lossProfile));
            if (stages.Count == 0)    throw new ArgumentException("At least one stage is required.", nameof(stages));
            if (payloadMass < 0.0)    throw new ArgumentOutOfRangeException(nameof(payloadMass), "Payload mass must be ≥ 0.");

            // ── Step 1: compute mass of everything above each stage at ignition. ──────
            // "Mass above stage i" = sum of wet masses of stages i+1..N + payload mass.
            // At burnout of stage i, the expended stage structure (dry mass of stage i)
            // is jettisoned, so the remaining vehicle is lighter.

            // Pre-compute suffix wet-mass totals (stages above each stage).
            // suffixWetMass[i] = wet mass of stages [i+1 .. N-1] + payload mass.
            int n = stages.Count;
            double[] suffixWetMass = new double[n];
            suffixWetMass[n - 1] = payloadMass; // nothing above the uppermost stage except payload
            for (int i = n - 2; i >= 0; i--)
            {
                suffixWetMass[i] = stages[i + 1].WetMass + suffixWetMass[i + 1];
            }

            // ── Step 2: per-stage Tsiolkovsky calculation. ───────────────────────────
            var stageResults = new List<DeltaVStageResult>(n);
            double totalIdealDeltaV = 0.0;
            double totalLossesApplied = 0.0;

            for (int i = 0; i < n; i++)
            {
                var stage = stages[i];

                // Effective wet mass at this stage's ignition:
                //   stage's own wet mass + everything above it (already computed as suffix).
                double effectiveWetMass = stage.WetMass + suffixWetMass[i];

                // At burnout the stage's own dry mass remains (before jettison),
                // plus everything above it.
                double effectiveDryMass = stage.DryMass + suffixWetMass[i];

                double massRatio = effectiveWetMass / effectiveDryMass;

                // Tsiolkovsky: Δv = Isp × g₀ × ln(m_wet / m_dry)
                double idealDv = stage.Isp * G0 * Math.Log(massRatio);

                // Losses: only applied to Stage-1 (index 0) for atmospheric/gravity drag.
                double losses = (i == 0) ? lossProfile.Stage1LossMetersPerSecond : 0.0;

                stageResults.Add(new DeltaVStageResult(
                    stageId:           stage.Id,
                    idealDeltaV:       idealDv,
                    lossesApplied:     losses,
                    isp:               stage.Isp,
                    massRatio:         massRatio,
                    effectiveWetMass:  effectiveWetMass,
                    effectiveDryMass:  effectiveDryMass
                ));

                totalIdealDeltaV   += idealDv;
                totalLossesApplied += losses;
            }

            // ── Step 3: stack-level totals. ──────────────────────────────────────────
            double totalWetMass = stages[0].WetMass + suffixWetMass[0];   // full launch mass
            double totalDryMass = 0.0;
            foreach (var s in stages) totalDryMass += s.DryMass;
            totalDryMass += payloadMass; // payload dry mass counts in stack dry mass

            return new DeltaVCalculationResult(
                stages:            stageResults,
                totalIdealDeltaV:  totalIdealDeltaV,
                totalLosses:       totalLossesApplied,
                totalWetMass:      totalWetMass,
                totalDryMass:      totalDryMass,
                payloadMass:       payloadMass
            );
        }

        /// <summary>
        /// Convenience overload with no losses (in-space Δv calculation).
        /// </summary>
        public static DeltaVCalculationResult Calculate(
            IReadOnlyList<RocketStage> stages,
            double payloadMass)
            => Calculate(stages, payloadMass, DeltaVLossProfile.None);
    }
}
