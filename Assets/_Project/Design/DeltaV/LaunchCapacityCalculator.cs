using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Calculates the maximum payload mass a given rocket stack can deliver to a
    /// target trajectory specified by a required net Delta-V (mission range budget).
    ///
    /// GDD §8.1 — "Fırlatıcı kapasitesi: Roketin hedef yörüngeye taşıyabileceği kütle (tasarımdan türer)"
    /// GDD §21 M1-08 — "Tasarımdan hedef yörünge kütle kapasitesi."
    ///
    /// Approach — binary search on payload mass:
    ///   The rocket equation is multi-stage and chained (each stage carries all upper
    ///   stages + payload), so there is no closed-form inversion for "payload given ΔV".
    ///   Instead we run a fast (≤ 64 iterations) bisection search:
    ///     • If ΔV(payloadMass=0) &lt; requiredΔV  → rocket cannot meet requirement at all.
    ///     • Otherwise binary-search the zero-crossing of f(m) = ΔV(m) − requiredΔV.
    ///   f is strictly decreasing (adding payload always reduces ΔV), ensuring unique root.
    ///
    /// Determinism guarantee:
    ///   Same stages + same lossProfile + same requiredDeltaV → same result every time.
    ///   No System.Random or DateTime used.
    /// </summary>
    public static class LaunchCapacityCalculator
    {
        /// <summary>Default solver tolerance in m/s (Delta-V accuracy of max-payload result).</summary>
        public const double DefaultToleranceMetersPerSecond = 1.0;

        /// <summary>Maximum binary search iterations (more than enough for any physically meaningful range).</summary>
        private const int MaxIterations = 64;

        /// <summary>
        /// Finds the maximum payload mass the rocket stack can carry while still
        /// achieving at least <paramref name="requiredDeltaV"/> net Delta-V.
        /// </summary>
        /// <param name="stages">
        ///   Rocket stages, index 0 = first (booster) → last = upper stage.
        ///   Must not be null or empty.
        /// </param>
        /// <param name="requiredDeltaV">
        ///   Mission net Delta-V requirement (m/s) — the value from
        ///   <see cref="MissionProfile.RequiredDeltaV"/>.  Must be ≥ 0.
        /// </param>
        /// <param name="lossProfile">
        ///   Ascent loss profile (stage-1 drag / gravity losses, latitude advantage).
        ///   Pass <see cref="DeltaVLossProfile.None"/> for in-space stages.
        /// </param>
        /// <param name="actualPayloadMass">
        ///   Dry mass of the actual spacecraft (bus + payload + fairing) in metric tons.
        ///   Used to compute <see cref="LaunchCapacityResult.CapacityMargin"/> and
        ///   <see cref="LaunchCapacityResult.NetDeltaVAtActualPayload"/>.
        ///   Must be ≥ 0.
        /// </param>
        /// <param name="toleranceMetersPerSecond">
        ///   Binary search stopping criterion (m/s).  Defaults to
        ///   <see cref="DefaultToleranceMetersPerSecond"/>.
        /// </param>
        /// <returns>A <see cref="LaunchCapacityResult"/> with the max payload and margin information.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="stages"/> or <paramref name="lossProfile"/> is null.</exception>
        /// <exception cref="ArgumentException">If <paramref name="stages"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If any mass/dv argument is negative.</exception>
        public static LaunchCapacityResult Calculate(
            IReadOnlyList<RocketStage> stages,
            double requiredDeltaV,
            DeltaVLossProfile lossProfile,
            double actualPayloadMass,
            double toleranceMetersPerSecond = DefaultToleranceMetersPerSecond)
        {
            if (stages == null)      throw new ArgumentNullException(nameof(stages));
            if (lossProfile == null) throw new ArgumentNullException(nameof(lossProfile));
            if (stages.Count == 0)   throw new ArgumentException("At least one stage is required.", nameof(stages));
            if (requiredDeltaV < 0)  throw new ArgumentOutOfRangeException(nameof(requiredDeltaV), "Required Delta-V must be ≥ 0.");
            if (actualPayloadMass < 0) throw new ArgumentOutOfRangeException(nameof(actualPayloadMass), "Actual payload mass must be ≥ 0.");

            double tol = Math.Max(toleranceMetersPerSecond, 1e-6);

            // ── Compute actual-payload ΔV for reporting. ─────────────────────────────
            double dvActual = DeltaVCalculator.Calculate(stages, actualPayloadMass, lossProfile).NetDeltaV;

            // ── Edge case: no ΔV requirement — capacity is effectively unbounded. ───
            // Return a large but finite value (structural capacity limit not modelled here).
            if (requiredDeltaV <= 0.0)
            {
                return new LaunchCapacityResult(
                    requiredDeltaV:             0.0,
                    maxPayloadMass:             actualPayloadMass,  // no constraint
                    actualPayloadMass:          actualPayloadMass,
                    netDeltaVAtMaxPayload:      dvActual,
                    netDeltaVAtActualPayload:   dvActual,
                    solverConverged:            true,
                    solverToleranceMetersPerSecond: tol);
            }

            // ── Check if rocket meets requirement even with zero payload. ────────────
            double dvAtZeroPayload = DeltaVCalculator.Calculate(stages, 0.0, lossProfile).NetDeltaV;

            if (dvAtZeroPayload < requiredDeltaV)
            {
                // Rocket cannot meet mission requirement under any payload scenario.
                // MaxPayloadMass = 0 signals this incapability.
                return new LaunchCapacityResult(
                    requiredDeltaV:             requiredDeltaV,
                    maxPayloadMass:             0.0,
                    actualPayloadMass:          actualPayloadMass,
                    netDeltaVAtMaxPayload:      dvAtZeroPayload,
                    netDeltaVAtActualPayload:   dvActual,
                    solverConverged:            true,
                    solverToleranceMetersPerSecond: tol);
            }

            // ── Binary search: find payloadMass such that ΔV(payloadMass) ≈ requiredΔV. ──
            // f(m) = ΔV(m) − requiredΔV is positive at m=0, and approaches negative infinity
            // as m → ∞ (or at some point below the structural limit).
            // We need f(m) ≥ 0, so the maximum useful payload is the root of f(m) = 0.

            // Upper search bound: a payload that causes the rocket to fall well below
            // the requirement. We grow the upper bound geometrically until ΔV drops below
            // (requiredΔV - tol), i.e., until f < 0.
            double lo = 0.0;
            double hi = Math.Max(actualPayloadMass, 1.0); // start with at least 1 t or actual

            // Grow hi until ΔV(hi) < requiredΔV
            const int maxGrow = 40;
            for (int g = 0; g < maxGrow; g++)
            {
                double dvHi = DeltaVCalculator.Calculate(stages, hi, lossProfile).NetDeltaV;
                if (dvHi < requiredDeltaV) break;
                hi *= 2.0;
            }

            // ── Bisect. ──────────────────────────────────────────────────────────────
            bool converged = false;
            double midPayload = 0.0;
            double dvMid = 0.0;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                midPayload = (lo + hi) * 0.5;
                dvMid = DeltaVCalculator.Calculate(stages, midPayload, lossProfile).NetDeltaV;

                double fMid = dvMid - requiredDeltaV;

                if (Math.Abs(fMid) <= tol)
                {
                    converged = true;
                    break;
                }

                if (fMid > 0.0)
                    lo = midPayload;   // can carry more payload
                else
                    hi = midPayload;   // payload is too heavy

                if ((hi - lo) * 0.5 < 1e-9)
                {
                    converged = true;
                    break;
                }
            }

            // The root is at midPayload; we take the lower of (lo, midPayload) so that
            // ΔV at the reported capacity is guaranteed to be ≥ requiredΔV - tol.
            double maxPayload = lo;
            double dvAtMaxPayload = DeltaVCalculator.Calculate(stages, maxPayload, lossProfile).NetDeltaV;

            return new LaunchCapacityResult(
                requiredDeltaV:             requiredDeltaV,
                maxPayloadMass:             maxPayload,
                actualPayloadMass:          actualPayloadMass,
                netDeltaVAtMaxPayload:      dvAtMaxPayload,
                netDeltaVAtActualPayload:   dvActual,
                solverConverged:            converged,
                solverToleranceMetersPerSecond: tol);
        }

        /// <summary>
        /// Convenience overload using <see cref="DefaultToleranceMetersPerSecond"/>.
        /// </summary>
        public static LaunchCapacityResult Calculate(
            IReadOnlyList<RocketStage> stages,
            double requiredDeltaV,
            DeltaVLossProfile lossProfile,
            double actualPayloadMass)
            => Calculate(stages, requiredDeltaV, lossProfile, actualPayloadMass, DefaultToleranceMetersPerSecond);
    }
}
