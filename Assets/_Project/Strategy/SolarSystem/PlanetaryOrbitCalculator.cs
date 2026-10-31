using System;
using Apogee.Data;

namespace Apogee.Strategy
{
    /// <summary>
    /// Pure mathematical model for planetary positions in circular orbit approximations.
    /// Complies with GDD §6.2 ("Basitleştirilmiş daire yörüngeler; gezegenler zamanla hareket eder").
    /// </summary>
    public static class PlanetaryOrbitCalculator
    {
        // Orbital periods in Earth days (approximate for game simulation)
        public const double MercuryPeriodDays = 87.97;
        public const double VenusPeriodDays   = 224.70;
        public const double EarthPeriodDays   = 365.25;
        public const double MarsPeriodDays    = 686.98;
        public const double JupiterPeriodDays = 4332.59; // Parent of Europa
        public const double EuropaMoonPeriodDays = 3.55;

        // Semi-major axis (AU)
        public const double VenusDistanceAu   = 0.723;
        public const double EarthDistanceAu   = 1.000;
        public const double MarsDistanceAu    = 1.524;
        public const double JupiterDistanceAu = 5.204;

        /// <summary>
        /// Computes current orbital true anomaly angle in radians [0, 2?) for the celestial body.
        /// </summary>
        public static double GetOrbitalAngleRadians(string destinationId, int currentTotalDays)
        {
            double period = GetOrbitalPeriodDays(destinationId);
            if (period <= 0.0) return 0.0;

            double revs = (currentTotalDays % period) / period;
            return revs * 2.0 * Math.PI;
        }

        /// <summary>
        /// Computes orbital position coordinates (X, Y) in AU around the Sun (0, 0).
        /// </summary>
        public static (double x, double y) GetHeliocentricPositionAu(string destinationId, int currentTotalDays)
        {
            double r = GetSemiMajorAxisAu(destinationId);
            double theta = GetOrbitalAngleRadians(destinationId, currentTotalDays);

            return (r * Math.Cos(theta), r * Math.Sin(theta));
        }

        public static double GetSemiMajorAxisAu(string destinationId)
        {
            switch (destinationId?.ToLowerInvariant())
            {
                case "venus": return VenusDistanceAu;
                case "earth":
                case "leo":
                case "moon": return EarthDistanceAu;
                case "mars": return MarsDistanceAu;
                case "europa": return JupiterDistanceAu;
                default: return 1.0;
            }
        }

        public static double GetOrbitalPeriodDays(string destinationId)
        {
            switch (destinationId?.ToLowerInvariant())
            {
                case "venus": return VenusPeriodDays;
                case "earth":
                case "leo":
                case "moon": return EarthPeriodDays;
                case "mars": return MarsPeriodDays;
                case "europa": return JupiterPeriodDays;
                default: return 365.25;
            }
        }
    }
}
