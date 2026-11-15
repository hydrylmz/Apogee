using System;
using Apogee.Design;

namespace Apogee.UI.Screens.Launch
{
    /// <summary>
    /// Gerçek roket dinamiği (Newton mekaniği, Tsiolkovsky roket denklemi,
    /// atmosferik sürükleme ve yerçekimi) hesaplayıcısı.
    /// Fırlatma sekansında (video ve tiyatro modlarında) T+5.8s rampa klemplerinin
    /// açıldığı andan itibaren telemetriyi (Hız, İrtifa, MET) gerçek fizik
    /// parametreleriyle kesintisiz hesaplar. Dummy / yapay artışlar kullanmaz.
    /// </summary>
    public sealed class LaunchAscentPhysics
    {
        /// <summary>Rampa klemplerinin açıldığı ve aracın yükselmeye başladığı zaman (saniye).</summary>
        public const double LiftoffTimeSeconds = 5.8;

        public const double EarthGravity = 9.80665;          // Standart yerçekimi ivmesi (m/s^2)
        public const double EarthRadius = 6_371_000.0;       // Dünya yarıçapı (m)
        public const double SeaLevelAirDensity = 1.225;      // Deniz seviyesi hava yoğunluğu (kg/m^3)
        public const double AtmosphericScaleHeight = 8500.0; // Atmosferik ölçek yüksekliği (m)

        public double WetMassKg { get; }
        public double DryMassKg { get; }
        public double ThrustN { get; }
        public double IspSeconds { get; }
        public double MassFlowRate { get; }
        public double DragArea { get; }
        public double DragCoefficient { get; }

        public LaunchAscentPhysics(Blueprint blueprint = null, BlueprintStats stats = null)
        {
            // 1. Kütle parametreleri: Blueprint / BlueprintStats veya standart fırlatıcı
            if (stats != null && stats.WetMass > 0)
            {
                WetMassKg = stats.WetMass * 1000.0;
                DryMassKg = Math.Max(WetMassKg * 0.08, stats.DryMass * 1000.0);
            }
            else if (blueprint != null && blueprint.Stages.Count > 0)
            {
                double wet = 0.0;
                double dry = 0.0;
                foreach (var st in blueprint.Stages)
                {
                    wet += st.WetMass;
                    dry += st.DryMass;
                }
                WetMassKg = (wet > 0 ? wet : 540.0) * 1000.0;
                DryMassKg = (dry > 0 ? dry : 45.0) * 1000.0;
            }
            else
            {
                WetMassKg = 540_000.0; // Tipik orta/ağır sınıf fırlatıcı (ör. Falcon 9 ~549t)
                DryMassKg = 45_000.0;
            }

            // 2. 1. Aşama itki ve Isp parametreleri
            double thrustKn = 0.0;
            double isp = 300.0;

            if (blueprint?.Stages.Count > 0 && blueprint.Stages[0].Engine != null)
            {
                thrustKn = blueprint.Stages[0].Engine.Thrust;
                if (blueprint.Stages[0].Engine.Isp > 0)
                {
                    isp = blueprint.Stages[0].Engine.Isp;
                }
            }

            if (thrustKn <= 0.0)
            {
                // Standart liftoff itki-ağırlık oranı (TWR = 1.38)
                ThrustN = WetMassKg * EarthGravity * 1.38;
            }
            else
            {
                ThrustN = thrustKn * 1000.0;
                // Minimum liftoff TWR koruması (en az 1.15 g)
                if (ThrustN < WetMassKg * EarthGravity * 1.15)
                {
                    ThrustN = WetMassKg * EarthGravity * 1.25;
                }
            }

            IspSeconds = isp;
            MassFlowRate = ThrustN / (IspSeconds * EarthGravity);

            // 3. Aerodinamik parametreler (Çap ~3.7m silindirik roket gövdesi)
            double diameter = 3.7;
            DragArea = Math.PI * Math.Pow(diameter / 2.0, 2);
            DragCoefficient = 0.32;
        }

        /// <summary>
        /// Verilen video / sekans zamanında (saniye) anlık telemetri durumunu hesaplar.
        /// t &lt; 5.8s için araç rampadadır: hız = 0 m/s, irtifa = 0 km.
        /// t &gt;= 5.8s için gerçek Newton + roket denklemi entegrasyonu ile (hız, irtifa) döner.
        /// </summary>
        public TelemetryState Evaluate(double timeSeconds)
        {
            if (timeSeconds < LiftoffTimeSeconds)
            {
                return new TelemetryState(
                    timestampSeconds: timeSeconds,
                    altitudeMeters: 0.0,
                    velocityMps: 0.0,
                    accelerationMps2: 0.0,
                    isAirborne: false);
            }

            double flightTime = timeSeconds - LiftoffTimeSeconds;

            // Sayısal entegrasyon (50 ms adımlarla Euler-Cromer)
            double dt = 0.05;
            double currentT = 0.0;
            double alt = 0.0;
            double vel = 0.0;
            double mass = WetMassKg;
            double accel = 0.0;

            while (currentT < flightTime)
            {
                double step = Math.Min(dt, flightTime - currentT);

                // Anlık irtifaya bağlı yerçekimi
                double g = EarthGravity * Math.Pow(EarthRadius / (EarthRadius + alt), 2);

                // Anlık barometrik hava yoğunluğu
                double rho = SeaLevelAirDensity * Math.Exp(-alt / AtmosphericScaleHeight);

                // Aerodinamik sürükleme kuvveti: Fd = 0.5 * rho * v^2 * Cd * A
                double drag = 0.5 * rho * vel * vel * DragCoefficient * DragArea;

                // Anlık net ivme: a = (F_thrust - F_drag) / m - g
                accel = (ThrustN - drag) / Math.Max(DryMassKg, mass) - g;
                if (accel < 0 && alt <= 0.0) accel = 0.0;

                // Hız ve irtifa güncellemesi
                vel += accel * step;
                if (vel < 0 && alt <= 0.0) vel = 0.0;

                alt += vel * step;
                if (alt < 0) alt = 0.0;

                mass = Math.Max(DryMassKg, mass - MassFlowRate * step);
                currentT += step;
            }

            return new TelemetryState(
                timestampSeconds: timeSeconds,
                altitudeMeters: alt,
                velocityMps: vel,
                accelerationMps2: accel,
                isAirborne: true);
        }

        public readonly struct TelemetryState
        {
            public double TimestampSeconds { get; }
            public double AltitudeMeters { get; }
            public double AltitudeKm => AltitudeMeters / 1000.0;
            public double VelocityMps { get; }
            public double AccelerationMps2 { get; }
            public bool IsAirborne { get; }

            public TelemetryState(double timestampSeconds, double altitudeMeters, double velocityMps, double accelerationMps2, bool isAirborne)
            {
                TimestampSeconds = timestampSeconds;
                AltitudeMeters = altitudeMeters;
                VelocityMps = velocityMps;
                AccelerationMps2 = accelerationMps2;
                IsAirborne = isAirborne;
            }
        }
    }
}
