# ADR-032: Varış ve İniş (EDL) Çözümü ve Simülasyonu (M5-03)

## Durum
Kabul Edildi

## Bağlam
APOGEE GDD §7.7 (Rover/Lander Özel Kuralları), §8.1 (Çekirdek Hesaplar — EDL Başarısı, Isı Kalkanı, İniş Yöntemi), §10.1 (#3 Varış ve İniş), §10.4 (Kayıp Tipleri) ve §21 M5-03 uyarınca; hedefine ulaşan bir uzay aracının atmosferik giriş, termal tepe yükü, paraşüt açılımı ve son yaklaşma (skycrane/retro roket/hava yastığı) fazlarını deterministik tohumlu simülasyonla çözümleyecek saf C# motorunun ve kampanya komut entegrasyonunun inşa edilmesi gerekiyordu.

## Kararlar

1. **Çekirdek Simülasyon (`Apogee.Core.Simulation.EDL`):**
   - **`EdlSimulator`:** Saf C#, deterministik tohumlu motor. Hedef cismin atmosferine (`hasAtmosphere`, `atmospherePressureKpa`), uzay aracı ve EDL paketinin güvenilirliğine (`spacecraftReliability`, `edlSystemReliability`), ısı kalkanı seviyesine (`none`, `mk1`, `mk2`), paraşüt ve itkili iniş (`skycraneOrRetro`) donanımlarına ve gerekli iniş $\Delta v$ değerine göre adım adım hesaplama yapar.
   - **EDL Fazları (`EdlPhaseType`):** `DeorbitBurn`, `AtmosphericEntry`, `PeakHeating`, `ParachuteDeploy`, `TerminalDescent`, `Touchdown`.
   - **Sonuç Tipleri (`EdlOutcome`):**
     - `Success`: Yumuşak ve nominal iniş.
     - `PartialSuccess`: Sert iniş (hasarlı ayak vb.), ancak araç operasyonel.
     - `ThermalDestruction`: Isı kalkanı yokluğu veya yetersizliği (ör. Venüs'te Mk1 kalkan) sonucu yanma.
     - `HighSpeedImpact`: Paraşüt/motor arızası veya vakumda (Ay) hatalı şekilde paraşüt kullanılması sonucu yüzeye yüksek hızda çarpma.
     - `PropulsionLoss`: İniş itki yakıtının tükenmesi sonucu sert çakılma.
     - `TrajectoryLoss`: Deorbit ateşleme arızası sonucu iniş koridorunun kaçırılması.
   - **Eğitsel Geri Bildirim ve Neden-Sonuç Zinciri (`EdlPhaseEvent`):** Her faz irtifa, hız, ısı kalkanı erime bütünlüğü ve sade dille Türkçe teknik özet üretir.

2. **Katalog Genişletmesi (`modules.json`):**
   - Araştırma ağacında yer alan `edl-heat-shield-mk1` ve `edl-heat-shield-mk2` modülleri katalog JSON dosyasına resmen eklendi.

3. **Strateji Katmanı (`Apogee.Strategy.Mission`):**
   - **`MissionInstance`:** `EdlResult` alanı ile zenginleştirildi.
   - **`ExecuteEdlCommand`:** `Arrival` fazındaki misyonu alır, blueprint'teki EDL bileşenlerini otomatik analiz eder (`VehicleSlotId.PayloadEdl`), deterministik tohumla `EdlSimulator` çalıştırır. Başarı durumunda misyonu `Operating` statüsüne geçirip +10 itibar kazandırır; başarısızlık durumunda `Failed` statüsüne geçirip itibar düşürür. Tam simetrik Undo/Redo destekler.

4. **Kullanıcı Arayüzü (`Apogee.UI`):**
   - `OperationsViewModel`: `CanExecuteEdl` durumu, `ExecuteEdlStepCommand` ve `EdlEventLog` desteği ile güncellendi.
   - `OperationsScreenController`: İniş sekansı butonları ve EDL telemetri olay satırları arayüze bağlandı.

5. **Doğrulama ve Testler:**
   - 9 yeni doğrulama testi (`EdlRunnerTests`) eklendi.
   - Toplam test sayısı 248'den **257**'ye çıktı (%100 geçti, 0 hata).
   - Core, Design ve Strategy katmanlarında `noEngineReferences = true` ilkesi korundu.
