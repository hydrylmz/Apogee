/// <summary>
/// Standalone validation script for DeltaVCalculator logic.
/// Run with: dotnet-script or compile as a simple console app.
/// This is NOT a Unity test — just a quick pre-commit sanity check.
/// Unity EditMode tests in Assets/_Project/Tests/EditMode/Design/DeltaVCalculatorTests.cs
/// are the authoritative test suite (NUnit).
/// </summary>

using System;
using System.Collections.Generic;

// ── Minimal stubs matching the Apogee.Design namespace ──────────────────────

namespace Apogee.Design
{
    public sealed class RocketStage
    {
        public string Id { get; }
        public double Isp { get; }
        public double DryMass { get; }
        public double WetMass { get; }
        public double PropellantMass => WetMass - DryMass;

        public RocketStage(string id, double isp, double dryMass, double wetMass)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Empty id", nameof(id));
            if (isp <= 0) throw new ArgumentOutOfRangeException(nameof(isp));
            if (dryMass <= 0) throw new ArgumentOutOfRangeException(nameof(dryMass));
            if (wetMass < dryMass) throw new ArgumentOutOfRangeException(nameof(wetMass));
            Id = id; Isp = isp; DryMass = dryMass; WetMass = wetMass;
        }
    }

    public sealed class DeltaVLossProfile
    {
        public double Stage1LossMetersPerSecond { get; }
        public double LatitudeAdvantageMetersPerSecond { get; }
        public string Label { get; }
        public DeltaVLossProfile(string label, double stage1Loss, double latAdv = 0)
        {
            if (stage1Loss < 0) throw new ArgumentOutOfRangeException(nameof(stage1Loss));
            if (latAdv < 0) throw new ArgumentOutOfRangeException(nameof(latAdv));
            Label = label; Stage1LossMetersPerSecond = stage1Loss; LatitudeAdvantageMetersPerSecond = latAdv;
        }
        public static readonly DeltaVLossProfile None = new DeltaVLossProfile("None", 0, 0);
        public static readonly DeltaVLossProfile EarthToLeo = new DeltaVLossProfile("Earth to LEO", 1500, 0);
    }

    public sealed class DeltaVStageResult
    {
        public string StageId { get; }
        public double IdealDeltaV { get; }
        public double LossesApplied { get; }
        public double NetDeltaV => IdealDeltaV - LossesApplied;
        public double Isp { get; }
        public double MassRatio { get; }
        public double EffectiveWetMass { get; }
        public double EffectiveDryMass { get; }
        public DeltaVStageResult(string id, double ideal, double losses, double isp, double ratio, double ew, double ed)
        { StageId=id; IdealDeltaV=ideal; LossesApplied=losses; Isp=isp; MassRatio=ratio; EffectiveWetMass=ew; EffectiveDryMass=ed; }
    }

    public sealed class DeltaVCalculationResult
    {
        public IReadOnlyList<DeltaVStageResult> Stages { get; }
        public double TotalIdealDeltaV { get; }
        public double TotalLosses { get; }
        public double NetDeltaV => TotalIdealDeltaV - TotalLosses;
        public double TotalWetMass { get; }
        public double TotalDryMass { get; }
        public double TotalPropellantMass => TotalWetMass - TotalDryMass;
        public double PayloadMass { get; }
        public int StageCount => Stages.Count;
        internal DeltaVCalculationResult(IReadOnlyList<DeltaVStageResult> s, double ideal, double loss, double wet, double dry, double pl)
        { Stages=s; TotalIdealDeltaV=ideal; TotalLosses=loss; TotalWetMass=wet; TotalDryMass=dry; PayloadMass=pl; }
    }

    public static class DeltaVCalculator
    {
        public const double G0 = 9.80665;

        public static DeltaVCalculationResult Calculate(IReadOnlyList<RocketStage> stages, double payloadMass, DeltaVLossProfile lossProfile)
        {
            if (stages == null)      throw new ArgumentNullException(nameof(stages));
            if (lossProfile == null) throw new ArgumentNullException(nameof(lossProfile));
            if (stages.Count == 0)   throw new ArgumentException("Empty stages", nameof(stages));
            if (payloadMass < 0)     throw new ArgumentOutOfRangeException(nameof(payloadMass));

            int n = stages.Count;
            double[] suffixWet = new double[n];
            suffixWet[n-1] = payloadMass;
            for (int i = n-2; i >= 0; i--) suffixWet[i] = stages[i+1].WetMass + suffixWet[i+1];

            var results = new List<DeltaVStageResult>(n);
            double totalIdeal = 0, totalLoss = 0;
            for (int i = 0; i < n; i++)
            {
                var s = stages[i];
                double ew = s.WetMass + suffixWet[i];
                double ed = s.DryMass + suffixWet[i];
                double ratio = ew / ed;
                double ideal = s.Isp * G0 * Math.Log(ratio);
                double loss  = (i == 0) ? lossProfile.Stage1LossMetersPerSecond : 0;
                results.Add(new DeltaVStageResult(s.Id, ideal, loss, s.Isp, ratio, ew, ed));
                totalIdeal += ideal; totalLoss += loss;
            }
            double totalWet = stages[0].WetMass + suffixWet[0];
            double totalDry = payloadMass;
            foreach (var s in stages) totalDry += s.DryMass;
            return new DeltaVCalculationResult(results, totalIdeal, totalLoss, totalWet, totalDry, payloadMass);
        }

        public static DeltaVCalculationResult Calculate(IReadOnlyList<RocketStage> stages, double payloadMass)
            => Calculate(stages, payloadMass, DeltaVLossProfile.None);
    }
}

// ── Test runner ──────────────────────────────────────────────────────────────

namespace StandaloneValidation
{
    using Apogee.Design;

    class Program
    {
        static int pass = 0, fail = 0;
        const double G0 = DeltaVCalculator.G0;
        const double Tol = 1.0; // m/s

        static void Main()
        {
            Console.WriteLine("=== APOGEE M1-04 DeltaV Standalone Validation ===\n");

            // 1. Single-stage, no payload, no losses
            {
                double isp=311, dry=25, wet=400;
                var s = new RocketStage("S1", isp, dry, wet);
                var r = DeltaVCalculator.Calculate(new[]{s}, 0, DeltaVLossProfile.None);
                double expected = isp * G0 * Math.Log(wet/dry);
                Check("1. Single-stage ideal Δv = Tsiolkovsky", Math.Abs(r.TotalIdealDeltaV - expected) < Tol,
                      $"expected={expected:0.#}  got={r.TotalIdealDeltaV:0.#}");
            }

            // 2. Zero propellant → zero Δv
            {
                var s = new RocketStage("Empty", 300, 10, 10);
                var r = DeltaVCalculator.Calculate(new[]{s}, 0);
                Check("2. Zero propellant → zero Δv", Math.Abs(r.TotalIdealDeltaV) < 1e-9,
                      $"got={r.TotalIdealDeltaV}");
            }

            // 3. Two-stage, no payload, no losses
            {
                var s1 = new RocketStage("S1", 300, 20, 200);
                var s2 = new RocketStage("S2", 450, 5,  50);
                double dv1 = 300*G0*Math.Log((200+50)/(20.0+50));
                double dv2 = 450*G0*Math.Log(50.0/5);
                double exp = dv1 + dv2;
                var r = DeltaVCalculator.Calculate(new[]{s1,s2}, 0, DeltaVLossProfile.None);
                Check("3a. Two-stage total ideal", Math.Abs(r.TotalIdealDeltaV - exp) < Tol, $"exp={exp:0.#} got={r.TotalIdealDeltaV:0.#}");
                Check("3b. Stage-1 Δv", Math.Abs(r.Stages[0].IdealDeltaV - dv1) < Tol, $"exp={dv1:0.#} got={r.Stages[0].IdealDeltaV:0.#}");
                Check("3c. Stage-2 Δv", Math.Abs(r.Stages[1].IdealDeltaV - dv2) < Tol, $"exp={dv2:0.#} got={r.Stages[1].IdealDeltaV:0.#}");
            }

            // 4. Three-stage Saturn V-inspired
            {
                var s1 = new RocketStage("S-IC",  263, 130, 2300);
                var s2 = new RocketStage("S-II",  421, 40,  490);
                var s3 = new RocketStage("S-IVB", 421, 13,  119);
                double pl = 45;
                double dv1 = 263*G0*Math.Log((2300+490+119+pl)/(130.0+490+119+pl));
                double dv2 = 421*G0*Math.Log((490+119+pl)/(40.0+119+pl));
                double dv3 = 421*G0*Math.Log((119+pl)/(13.0+pl));
                double exp = dv1+dv2+dv3;
                var r = DeltaVCalculator.Calculate(new[]{s1,s2,s3}, pl, DeltaVLossProfile.None);
                Check("4a. Three-stage total", Math.Abs(r.TotalIdealDeltaV - exp) < Tol, $"exp={exp:0.#} got={r.TotalIdealDeltaV:0.#}");
                Check("4b. Three stages count", r.StageCount == 3, $"count={r.StageCount}");
            }

            // 5. Loss profile applied only to Stage-1
            {
                var s1 = new RocketStage("S1", 300, 20, 200);
                var s2 = new RocketStage("S2", 450, 5,  50);
                var prof = new DeltaVLossProfile("Test", 1200);
                var r = DeltaVCalculator.Calculate(new[]{s1,s2}, 0, prof);
                Check("5a. Loss on Stage-1", Math.Abs(r.Stages[0].LossesApplied - 1200) < 1e-9, $"got={r.Stages[0].LossesApplied}");
                Check("5b. No loss on Stage-2", Math.Abs(r.Stages[1].LossesApplied) < 1e-9, $"got={r.Stages[1].LossesApplied}");
                Check("5c. TotalLosses=1200", Math.Abs(r.TotalLosses - 1200) < 1e-9, $"got={r.TotalLosses}");
            }

            // 6. Mass accounting
            {
                var s1 = new RocketStage("S1", 300, 20, 200);
                var s2 = new RocketStage("S2", 450, 5,  50);
                double pl = 10;
                var r = DeltaVCalculator.Calculate(new[]{s1,s2}, pl);
                Check("6a. TotalWetMass", Math.Abs(r.TotalWetMass - (200+50+pl)) < 1e-9, $"got={r.TotalWetMass}");
                Check("6b. TotalDryMass", Math.Abs(r.TotalDryMass - (20+5+pl)) < 1e-9, $"got={r.TotalDryMass}");
                Check("6c. PropellantMass", Math.Abs(r.TotalPropellantMass - (r.TotalWetMass-r.TotalDryMass)) < 1e-9, "prop consistency");
            }

            // 7. Guard checks
            {
                bool caught = false;
                try { DeltaVCalculator.Calculate(new RocketStage[0], 0, DeltaVLossProfile.None); }
                catch (ArgumentException) { caught = true; }
                Check("7a. Empty stages throws", caught);

                caught = false;
                try { var s = new RocketStage("X",300,10,100); DeltaVCalculator.Calculate(new[]{s}, -1, DeltaVLossProfile.None); }
                catch (ArgumentOutOfRangeException) { caught = true; }
                Check("7b. Negative payload throws", caught);

                caught = false;
                try { new RocketStage("", 300, 10, 20); }
                catch (ArgumentException) { caught = true; }
                Check("7c. Empty stage id throws", caught);
            }

            // 8. Determinism
            {
                var s1 = new RocketStage("S1", 300, 20, 200);
                var s2 = new RocketStage("S2", 450, 5,  50);
                var r1 = DeltaVCalculator.Calculate(new[]{s1,s2}, 10, DeltaVLossProfile.EarthToLeo);
                var r2 = DeltaVCalculator.Calculate(new[]{s1,s2}, 10, DeltaVLossProfile.EarthToLeo);
                Check("8. Determinism: same inputs → same output", Math.Abs(r1.NetDeltaV - r2.NetDeltaV) < 1e-12);
            }

            Console.WriteLine($"\n=== Results: {pass} PASS  {fail} FAIL ===");
            if (fail > 0) Environment.Exit(1);
        }

        static void Check(string name, bool ok, string detail = "")
        {
            if (ok) { pass++; Console.WriteLine($"  ✓ {name}"); }
            else    { fail++; Console.WriteLine($"  ✗ {name}  [{detail}]"); }
        }
    }
}
