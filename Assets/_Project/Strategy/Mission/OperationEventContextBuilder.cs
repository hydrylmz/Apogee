using System;
using Apogee.Core.Simulation.Operations;
using Apogee.Design;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Builds an <see cref="OperationHealthSnapshot"/> for a mission from its blueprint's
    /// evaluated stats, accumulated penalties, and the per-destination hazard table.
    ///
    /// GDD §10.3: "olaylar tasarımdaki zayıflığa göre ağırlıklanır" — a weak design
    /// (thin power/thermal/comms margins, low reliability) sees more and harder events.
    /// </summary>
    public static class OperationEventContextBuilder
    {
        public static OperationHealthSnapshot BuildSnapshot(MissionInstance mission)
        {
            double reliability    = 0.85;   // neutral baseline when no blueprint exists
            double powerMargin    = 0.30;
            double thermalMargin  = 0.30;
            double commsCapacity  = 1.00;

            var bp = mission?.Blueprint;
            if (bp != null)
            {
                var eval = BlueprintEvaluator.Evaluate(bp);
                if (eval != null)
                {
                    if (eval.Reliability > 0.0)
                        reliability = eval.Reliability;

                    if (eval.PowerGeneration > 0.0)
                        powerMargin = Math.Max(0.0,
                            (eval.PowerGeneration - eval.PowerConsumption) / eval.PowerGeneration);

                    if (eval.ThermalDissipation > 0.0)
                        thermalMargin = Math.Max(0.0,
                            (eval.ThermalDissipation - eval.ThermalLoad) / eval.ThermalDissipation);

                    if (eval.DataRate > 0.0)
                        commsCapacity = Math.Max(0.0, eval.DownlinkRate / eval.DataRate);
                }
            }

            // Accumulated cruise/decision penalties erode reliability
            reliability = Clamp(reliability - (mission?.AccumulatedReliabilityPenalty ?? 0.0), 0.05, 1.0);

            GetHazards(mission?.DestinationId, out double radiation, out double dust);

            return new OperationHealthSnapshot(
                reliability:         reliability,
                powerMarginRatio:    powerMargin,
                thermalMarginRatio:  thermalMargin,
                commsCapacityRatio:  commsCapacity,
                radiationHazard:     radiation,
                dustHazard:          dust);
        }

        /// <summary>
        /// Per-destination hazard table — mirrors AdvanceCruiseCommand.GetHazards (GDD §16.1).
        /// </summary>
        public static void GetHazards(string destinationId, out double radiation, out double dust)
        {
            switch (destinationId?.ToLowerInvariant())
            {
                case "moon":   radiation = 0.05; dust = 0.02; break;
                case "venus":  radiation = 0.10; dust = 0.03; break;
                case "mars":   radiation = 0.18; dust = 0.20; break;
                case "europa": radiation = 0.60; dust = 0.02; break;
                default:       radiation = 0.03; dust = 0.01; break; // LEO or unknown
            }
        }

        private static double Clamp(double v, double min, double max) =>
            v < min ? min : v > max ? max : v;
    }
}
