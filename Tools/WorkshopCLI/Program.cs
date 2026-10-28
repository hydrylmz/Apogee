using System;
using System.Collections.Generic;
using Apogee.Design;

namespace Apogee.Workshop
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            double s1Fuel = 150.0;
            double s2Fuel = 30.0;
            int s1EngineIndex = 0; // 0: Kerosen, 1: Metan, 2: Katı
            int materialIndex = 0; // 0: Alüminyum, 1: Al-Li, 2: Karbon Kompozit
            int supplierIndex = 0; // 0: Helion, 1: Kestrel, 2: Nordvik
            int targetIndex = 1;   // 0: LEO, 1: Ay Yörüngesi, 2: Mars

            while (true)
            {
                Console.Clear();
                PrintHeader();

                var material = materialIndex switch
                {
                    1 => MaterialSpec.AluminumLithium,
                    2 => MaterialSpec.CarbonComposite,
                    _ => MaterialSpec.AluminumAlloy
                };

                var supplier = supplierIndex switch
                {
                    1 => new SupplierSpec("kestrel", "Kestrel Aerospace", costMultiplier: 1.35, deliveryMultiplier: 1.25, reliabilityMultiplier: 1.15),
                    2 => new SupplierSpec("nordvik", "Nordvik Propulsion", costMultiplier: 1.10, deliveryMultiplier: 1.00, reliabilityMultiplier: 1.08),
                    _ => new SupplierSpec("helion", "Helion Dynamics", costMultiplier: 0.85, deliveryMultiplier: 0.90, reliabilityMultiplier: 0.90)
                };

                (string engName, double engIsp, double engMass, double engCost) = s1EngineIndex switch
                {
                    1 => ("Raptor-M1 (Metan/LOX)", 350.0, 4.0, 8.0),
                    2 => ("Solid-Titan (Katı Yakıt)", 270.0, 6.0, 3.5),
                    _ => ("Merlin-K1 (Kerosen/LOX)", 311.0, 3.0, 5.0)
                };

                var s1Engine = new DesignComponent("s1-engine", "engine", engName, dryMass: engMass, cost: engCost, buildTime: 40, reliability: 0.96, isp: engIsp, supplier: supplier);
                var s1Tank = new DesignComponent("s1-tank", "tank", "Aşama 1 Ana Tank", dryMass: s1Fuel * 0.08, cost: 3.0, buildTime: 25, reliability: 0.99, supplier: supplier, material: material);
                var stage1 = new StageDesign("Aşama 1 (Booster)", engName, engine: s1Engine, tank: s1Tank, propellantMass: s1Fuel, structureMaterial: material, stageSupplier: supplier);

                var s2Engine = new DesignComponent("s2-engine", "engine", "Kestrel-Vac (Üst Aşama)", dryMass: 1.2, cost: 4.5, buildTime: 35, reliability: 0.98, isp: 420.0, supplier: supplier);
                var s2Tank = new DesignComponent("s2-tank", "tank", "Aşama 2 Tankı", dryMass: s2Fuel * 0.10, cost: 2.0, buildTime: 20, reliability: 0.99, supplier: supplier, material: material);
                var stage2 = new StageDesign("Aşama 2 (Upper)", "Vakuum Motoru", engine: s2Engine, tank: s2Tank, propellantMass: s2Fuel, structureMaterial: material, stageSupplier: supplier);

                var bus = new DesignComponent("bus", "bus", "Kaşif Gövdesi", dryMass: 0.8, cost: 3.0, buildTime: 30, reliability: 0.99, powerGeneration: 2.5, powerConsumption: 1.0, thermalDissipation: 1.2, thermalLoad: 0.6);
                var inst = new DesignComponent("cam", "instrument", "Gelişmiş Yüzey Kamerası", dryMass: 0.3, cost: 4.0, buildTime: 45, reliability: 0.97, powerConsumption: 0.8, dataRate: 15.0, dataStorage: 64.0, downlinkRate: 20.0);
                var fairing = new DesignComponent("fairing", "structure", "Burun Konisi (Fairing)", dryMass: 0.6, cost: 1.0, buildTime: 10, reliability: 0.995, material: material);
                var payload = new PayloadDesign("sat-1", "Keşif Uydusu", bus: bus, instruments: new[] { inst });

                var blueprint = new Blueprint("custom-rocket", "Özel Görev Aracı", 1, payload: payload, stages: new[] { stage1, stage2 }, fairingAdapter: fairing);

                var lossProfile = DeltaVLossProfile.EarthToLeo;
                var stats = BlueprintEvaluator.Evaluate(blueprint, lossProfile);

                (string targetName, double targetDv) = targetIndex switch
                {
                    0 => ("Alçak Dünya Yörüngesi (LEO)", 9400.0),
                    2 => ("Mars Transferi & Varış", 13000.0),
                    _ => ("Ay Yörüngesi (Trans-Lunar)", 12500.0)
                };

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("================================================================================");
                Console.WriteLine($"  HEDEF: {targetName,-32} GEREKEN Δv: {targetDv:N0} m/s");
                Console.WriteLine("================================================================================");
                Console.ResetColor();

                double dvRatio = stats.NetDeltaV / targetDv;
                Console.Write("  MENZİL (Δv): ");
                PrintBar(dvRatio, $"{stats.NetDeltaV:N0} m/s / {targetDv:N0} m/s");

                bool canReach = stats.NetDeltaV >= targetDv;
                if (canReach)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    [✓] MENZİL YETERLİ (+{stats.NetDeltaV - targetDv:N0} m/s rezerv)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    [✗] MENZİL YETERSİZ ({targetDv - stats.NetDeltaV:N0} m/s açık var)");
                }
                Console.ResetColor();

                Console.WriteLine();
                Console.WriteLine($"  Toplam Fırlatma Kütlesi : {stats.WetMass,7:F1} ton  (Kuru: {stats.DryMass:F1} t | Yakıt: {stats.PropellantMass:F1} t)");
                Console.WriteLine($"  Toplam Görev Maliyeti   : {stats.Cost,7:F1} M$");
                Console.WriteLine($"  Üretim & Montaj Süresi  : {stats.BuildTimeDays,7} gün");

                Console.Write("  Görev Güvenilirliği     : ");
                if (stats.Reliability >= 0.85) Console.ForegroundColor = ConsoleColor.Green;
                else if (stats.Reliability >= 0.70) Console.ForegroundColor = ConsoleColor.Yellow;
                else Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"%{stats.Reliability * 100:F1} (Seri sistem arıza riski: %{(1 - stats.Reliability) * 100:F1})");
                Console.ResetColor();

                Console.WriteLine($"  Güç Durumu              : Üretim {stats.PowerGeneration:F1} kW | Tüketim {stats.PowerConsumption:F1} kW -> Marj: +{stats.PowerMargin:F1} kW");
                Console.WriteLine($"  Isı Durumu              : Yük {stats.ThermalLoad:F1} kW | Dağıtım {stats.ThermalDissipation:F1} kW -> Marj: +{stats.ThermalMargin:F1} kW");

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("--------------------------------------------------------------------------------");
                Console.ResetColor();
                Console.WriteLine("  MEVCUT YAPILANDIRMA:");
                Console.WriteLine($"  [1] Aşama 1 Yakıtı      : {s1Fuel} ton");
                Console.WriteLine($"  [2] Aşama 2 Yakıtı      : {s2Fuel} ton");
                Console.WriteLine($"  [3] Aşama 1 Motoru      : {engName}");
                Console.WriteLine($"  [4] Yapısal Malzeme     : {material.Name} (Kütle x{material.MassMultiplier}, Maliyet x{material.CostMultiplier})");
                Console.WriteLine($"  [5] Tedarikçi Şirket    : {supplier.Name} (Maliyet x{supplier.CostMultiplier}, Güvenilirlik x{supplier.ReliabilityMultiplier})");
                Console.WriteLine($"  [6] Görev Hedefi Değiştir: {targetName}");
                Console.WriteLine("  [Q] Çıkış");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\nDeğiştirmek istediğiniz ayarın numarasını girin [1-6] veya [Q]: ");
                Console.ResetColor();

                var input = Console.ReadLine()?.Trim().ToUpper();
                if (input == "Q") break;

                switch (input)
                {
                    case "1":
                        Console.Write("Yeni Aşama 1 Yakıt Miktarı (ton, örn: 100-400): ");
                        if (double.TryParse(Console.ReadLine(), out double f1) && f1 > 5) s1Fuel = f1;
                        break;
                    case "2":
                        Console.Write("Yeni Aşama 2 Yakıt Miktarı (ton, örn: 20-80): ");
                        if (double.TryParse(Console.ReadLine(), out double f2) && f2 > 1) s2Fuel = f2;
                        break;
                    case "3":
                        s1EngineIndex = (s1EngineIndex + 1) % 3;
                        break;
                    case "4":
                        materialIndex = (materialIndex + 1) % 3;
                        break;
                    case "5":
                        supplierIndex = (supplierIndex + 1) % 3;
                        break;
                    case "6":
                        targetIndex = (targetIndex + 1) % 3;
                        break;
                }
            }
        }

        static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ___    ____  ____  ____________ ____ 
   /   |  / __ \/ __ \/ ____/ ____// __ \
  / /| | / /_/ / / / / / __/ __/  / /_/ /
 / ___ |/ ____/ /_/ / /_/ / /___ / ____/ 
/_/  |_/_/    \____/\____/_____//_/      
       ATÖLYE SIMÜLASYONU (M1 Test Konsolu)");
            Console.ResetColor();
        }

        static void PrintBar(double ratio, string label)
        {
            int totalBlocks = 30;
            int filled = Math.Min(totalBlocks, (int)(ratio * totalBlocks));

            Console.Write("[");
            if (ratio >= 1.0) Console.ForegroundColor = ConsoleColor.Green;
            else if (ratio >= 0.8) Console.ForegroundColor = ConsoleColor.Yellow;
            else Console.ForegroundColor = ConsoleColor.Red;

            Console.Write(new string('█', filled));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', totalBlocks - filled));
            Console.ResetColor();
            Console.WriteLine($"]  {label}");
        }
    }
}
