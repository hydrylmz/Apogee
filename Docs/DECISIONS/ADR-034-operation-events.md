# ADR-034: Operasyon Olayları ve Karar Kartları (M5-05)

## Durum
Kabul Edildi

## Bağlam
GDD §10.2 (Karar Kartları), §10.3 (Olay Havuzu İlkeleri), §13.2 ("Neden?" İpuçları), §14.1 (#14 Operasyon Ekranı), §17.3, §19.12 ve §21 M5-05 uyarınca:
Uzay araçlarının seyir (cruise), iniş (EDL) ve bilim operasyonu (science) fazlarında deterministik olarak operasyon olaylarının tetiklenmesi, bu olayların araç tasarımındaki zayıflıklara (güç marjı, termal limit, haberleşme darboğazı, radyasyon vb.) göre ağırlıklandırılması, oyuncuya 2-3 seçenekli karar kartlarının sunulması, seçimlerin görünür maliyet ve artı/eksi sonuçlarla uygulanması ve operasyon ekranında canlı izlenebilmesi gerekiyordu.

## Kararlar

1. **Operasyon Olay Modeli ve Çekirdek Simülasyon (`Apogee.Core.Simulation.Operations`):**
   - **`OperationEventRecord` & `OperationEventOption`:** Olayların kimliği, faz kısıtı (`Cruise`, `Edl`, `Science`, `Any`), başlık/açıklama anahtarları, taban ağırlıkları, tekrarlanabilirlik durumu ve tasarım zayıflığı tetikleyici eşikleri (`powerWeaknessTriggerBelow`, `thermalWeaknessTriggerBelow`, `commsWeaknessTriggerBelow`, `weaknessWeightMultiplier`).
   - **`OperationHealthSnapshot`:** Aracın güvenilirlik, güç marjı, termal marj, haberleşme kapasite oranı ve radyasyon/toz tehlike seviyelerini içeren anlık sağlık modeli.
   - **`OperationEventManager`:** 15 operasyon olayından oluşan dahili kataloğu (`BuildCatalog()`) ve tohumlu deterministik ağırlıklı seçim algoritmasını (`SelectEvent`, `ShouldFireEvent`) yöneten saf C# motoru.
   - **15 Operasyon Olayı:**
     1. `op_solar_storm` (Güneş fırtınası)
     2. `op_comms_blackout` (Haberleşme kararması)
     3. `op_battery_low` (Düşük batarya gerilimi)
     4. `op_micrometeorite_impact` (Mikrometeorit çarpması)
     5. `op_propellant_leak` (İtki yakıtı sızıntısı)
     6. `op_ground_station_fault` (Yer istasyonu arızası)
     7. `op_instrument_calibration_drift` (Alet kalibrasyon kayması)
     8. `op_thermal_spike` (Termal tepe yükü / aşırı ısınma)
     9. `op_data_buffer_overflow` (Veri tampon taşması)
     10. `op_unexpected_discovery` (Beklenmedik bilimsel keşif / bonus)
     11. `op_power_brownout` (Güç kesintisi / brownout)
     12. `op_radiation_hit` (Radyasyon darbesi)
     13. `op_attitude_control_glitch` (ADCS yönelim kontrol aksaması)
     14. `op_spare_part_shortage` (Yedek parça kıtlığı)
     15. `op_science_instrument_failure` (Bilim aleti donanım arızası)

2. **Katalog JSON Verisi (`operation_events.json`):**
   - GDD §16/§19 standartlarında `Assets/_Project/Data/Catalog/Json/operation_events.json` dosyası oluşturuldu ve 15 operasyon olayının tüm seçenek ve katsayıları veri odaklı olarak kaydedildi.

3. **Strateji Katmanı ve Komutlar (`Apogee.Strategy.Mission`):**
   - **`MissionInstance`:** `PendingOperationEvent`, `OperationEventLog` ve `UsedNonRepeatableOperationEvents` alanları eklendi.
   - **`TriggerOperationEventCommand`:** Faz, sağlık durumu ve deterministik RNG ile tur bazlı operasyon olayını tetikler, Undo/Redo destekler.
   - **`ResolveOperationEventCommand`:** Seçilen opsiyonun bütçe (`BudgetDelta`), itibar (`ReputationDelta`), araştırma puanı (`ResearchPointsDelta`), güvenilirlik kaybı (`ReliabilityPenalty`) ve bilim verisi (`ScienceDataDeltaGb`) etkilerini CampaignState ve MissionInstance'a uygular. Tam simetrik Undo/Redo destekler. `AgencyDirector | ScienceDirector | LeadEngineer` rol yetkisi denetimi içerir.

4. **Kullanıcı Arayüzü (`Apogee.UI`):**
   - **`OperationsViewModel`:** `HasPendingOperationEvent`, `OperationEventTitle`, `OperationEventNarrative`, `OperationEventOptions`, `OperationEventLog` ve `ResolveOperationEventOptionCommand` eklendi.
   - **`OperationsScreenController`:** Karar kutusunun (`_decisionBox`) operasyon karar kartlarını da göstermesi ve telemetri geçmişinde olayların `[OLAY:FAZ]` başlığı altında renkli log satırları olarak listelenmesi sağlandı.

5. **Doğrulama ve Testler:**
   - 9 yeni doğrulama testi (`OperationRunnerTests`) eklendi.
   - Toplam test sayısı 266'dan **275**'e yükseldi (%100 geçti, 0 hata).
   - `check-no-engine-refs.ps1` ile Core, Design ve Strategy katmanlarında sıfır UnityEngine bağımlılığı doğrulandı.
