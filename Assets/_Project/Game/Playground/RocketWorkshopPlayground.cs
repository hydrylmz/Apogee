using UnityEngine;
using Apogee.Design;

namespace Apogee.Game.Playground
{
    /// <summary>
    /// Interactive Unity Inspector playground for testing rocket design formulas in real-time.
    /// Attach this component to any GameObject in a Unity scene.
    /// Modify sliders/enums in the Inspector to see instant Delta-V, Cost, Mass, Reliability calculations.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Apogee/Playground/Rocket Workshop Playground")]
    public class RocketWorkshopPlayground : MonoBehaviour
    {
        public enum EngineType
        {
            KeroseneLOX,    // Merlin-K1 (Isp 311s)
            MethaneLOX,     // Raptor-M1 (Isp 350s)
            SolidRocket     // Solid-Titan (Isp 270s)
        }

        public enum StructuralMaterial
        {
            AluminumAlloy,      // x1.0 kütle, x1.0 maliyet
            AluminumLithium,    // x0.88 kütle, x1.35 maliyet
            CarbonComposite     // x0.75 kütle, x2.50 maliyet
        }

        public enum SupplierCompany
        {
            HelionDynamics,     // Ucuz (%85), hızlı, düşük güvenilirlik (%90)
            KestrelAerospace,   // Pahalı (%135), yavaş, yüksek güvenilirlik (%115)
            NordvikPropulsion   // Orta (%110), motor uzmanı (%108)
        }

        public enum MissionTarget
        {
            LowEarthOrbit_LEO,  // 9,400 m/s
            LunarOrbit,         // 12,500 m/s
            MarsTransfer        // 13,000 m/s
        }

        [Header("🎯 Görev Hedefi")]
        public MissionTarget Target = MissionTarget.LunarOrbit;

        [Header("🚀 Aşama 1 (Booster)")]
        public EngineType Stage1Engine = EngineType.KeroseneLOX;
        [Range(10f, 500f)]
        public float Stage1PropellantTons = 160f;

        [Header("🚀 Aşama 2 (Upper Stage)")]
        [Range(5f, 100f)]
        public float Stage2PropellantTons = 35f;

        [Header("⚙️ Malzeme & Tedarikçi Tercihleri")]
        public StructuralMaterial Material = StructuralMaterial.AluminumAlloy;
        public SupplierCompany Supplier = SupplierCompany.HelionDynamics;

        [Header("🛸 Faydalı Yük (Uydu / Sonda)")]
        [Range(0.5f, 10f)]
        public float PayloadDryMassTons = 1.5f;

        [Header("📊 Canlı Mühendislik Raporu")]
        [TextArea(10, 20)]
        public string LiveTelemetryReport = "Değerleri Inspector'da değiştirin...";

        private void OnValidate()
        {
            Recalculate();
        }

        private void Start()
        {
            Recalculate();
        }

        [ContextMenu("Tasarımı Şimdi Yeniden Hesapla")]
        public void Recalculate()
        {
            var matSpec = Material switch
            {
                StructuralMaterial.AluminumLithium => MaterialSpec.AluminumLithium,
                StructuralMaterial.CarbonComposite => MaterialSpec.CarbonComposite,
                _ => MaterialSpec.AluminumAlloy
            };

            var supSpec = Supplier switch
            {
                SupplierCompany.KestrelAerospace => new SupplierSpec("kestrel", "Kestrel Aerospace", 1.35, 1.25, 1.15),
                SupplierCompany.NordvikPropulsion => new SupplierSpec("nordvik", "Nordvik Propulsion", 1.10, 1.00, 1.08),
                _ => new SupplierSpec("helion", "Helion Dynamics", 0.85, 0.90, 0.90)
            };

            (string engName, double isp, double dryMass, double cost) = Stage1Engine switch
            {
                EngineType.MethaneLOX => ("Raptor-M1 (Metan/LOX)", 350.0, 4.0, 8.0),
                EngineType.SolidRocket => ("Solid-Titan (Katı Yakıt)", 270.0, 6.0, 3.5),
                _ => ("Merlin-K1 (Kerosen/LOX)", 311.0, 3.0, 5.0)
            };

            var s1Eng = new DesignComponent("s1-eng", "engine", engName, dryMass: dryMass, cost: cost, buildTime: 40, reliability: 0.96, isp: isp, supplier: supSpec);
            var s1Tank = new DesignComponent("s1-tank", "tank", "Stage 1 Tank", dryMass: Stage1PropellantTons * 0.08, cost: 3.0, buildTime: 25, reliability: 0.99, supplier: supSpec, material: matSpec);
            var stage1 = new StageDesign("Aşama 1", engName, engine: s1Eng, tank: s1Tank, propellantMass: Stage1PropellantTons, structureMaterial: matSpec, stageSupplier: supSpec);

            var s2Eng = new DesignComponent("s2-eng", "engine", "Kestrel-Vac", dryMass: 1.2, cost: 4.5, buildTime: 35, reliability: 0.98, isp: 420.0, supplier: supSpec);
            var s2Tank = new DesignComponent("s2-tank", "tank", "Stage 2 Tank", dryMass: Stage2PropellantTons * 0.10, cost: 2.0, buildTime: 20, reliability: 0.99, supplier: supSpec, material: matSpec);
            var stage2 = new StageDesign("Aşama 2", "Upper Stage", engine: s2Eng, tank: s2Tank, propellantMass: Stage2PropellantTons, structureMaterial: matSpec, stageSupplier: supSpec);

            var bus = new DesignComponent("bus", "bus", "Uydu Gövdesi", dryMass: PayloadDryMassTons * 0.6, cost: 3.0, powerGeneration: 2.5, powerConsumption: 1.0, thermalDissipation: 1.2, thermalLoad: 0.5, reliability: 0.99);
            var inst = new DesignComponent("cam", "instrument", "Kamera", dryMass: PayloadDryMassTons * 0.4, cost: 4.0, dataRate: 15.0, dataStorage: 64.0, downlinkRate: 20.0, reliability: 0.97);
            var fairing = new DesignComponent("fairing", "structure", "Fairing", dryMass: 0.6, cost: 1.0, reliability: 0.995, material: matSpec);
            var payload = new PayloadDesign("payload", "Gözlem Uydusu", bus: bus, instruments: new[] { inst });

            var blueprint = new Blueprint("inspector-rocket", "Canlı Test Aracı", 1, payload: payload, stages: new[] { stage1, stage2 }, fairingAdapter: fairing);

            var stats = BlueprintEvaluator.Evaluate(blueprint, DeltaVLossProfile.EarthToLeo);

            (string targetName, double targetDv) = Target switch
            {
                MissionTarget.LowEarthOrbit_LEO => ("Alçak Dünya Yörüngesi (LEO)", 9400.0),
                MissionTarget.MarsTransfer => ("Mars Transferi & Varış", 13000.0),
                _ => ("Ay Yörüngesi (Trans-Lunar)", 12500.0)
            };

            bool canReach = stats.NetDeltaV >= targetDv;
            string status = canReach ? $"[✓ YETERLİ (+{(int)(stats.NetDeltaV - targetDv)} m/s rezerv)]" : $"[✗ YETERSİZ ({(int)(targetDv - stats.NetDeltaV)} m/s eksik)]";

            LiveTelemetryReport =
$@"🎯 HEDEF: {targetName} (Gereken Δv: {targetDv:N0} m/s)
--------------------------------------------------
🚀 MENZİL (Δv)       : {stats.NetDeltaV:N0} m/s {status}
⚖️ TOPLAM KÜTLE      : {stats.WetMass:F1} t (Kuru: {stats.DryMass:F1} t | Yakıt: {stats.PropellantMass:F1} t)
💰 TOPLAM MALİYET    : {stats.Cost:F1} M$
⏱️ ÜRETİM SÜRESİ    : {stats.BuildTimeDays} gün
🛡️ GÜVENİLİRLİK     : %{stats.Reliability * 100:F1} (Arıza İhtimali: %{(1 - stats.Reliability) * 100:F1})
⚡ GÜÇ MARJI         : +{stats.PowerMargin:F1} kW (Üretim: {stats.PowerGeneration:F1} kW, Tüketim: {stats.PowerConsumption:F1} kW)
🌡️ ISI MARJI         : +{stats.ThermalMargin:F1} kW (Dağıtım: {stats.ThermalDissipation:F1} kW, Yük: {stats.ThermalLoad:F1} kW)";

            Debug.Log($"[APOGEE Telemetry] Δv: {stats.NetDeltaV:N0} m/s | Kütle: {stats.WetMass:F1}t | Maliyet: {stats.Cost:F1}M$ | Durum: {status}");
        }
    }
}
