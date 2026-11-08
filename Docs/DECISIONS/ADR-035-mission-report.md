# ADR-035: Misyon Sonuç Raporu (M5-06)

## Durum
Kabul Edildi

## Bağlam
GDD §10.5 (Sonuç Raporu), §13.2 ("Neden?" İpuçları), §14.1 (#16 Sonuç Raporu), §17.3, §19.12 (Misyon Modeli) ve §21 M5-06 gereğince:
Tamamlanan, kısmi başarıya ulaşan veya erken safhada kaybedilen uzay misyonlarının deterministik olarak değerlendirilmesi, Misyon Besteleyici'de tanımlanan ölçülebilir başarı kriterlerinin (`MissionSuccessCriterion`) kontrol edilmesi, bilim veri teslimi ve downlink/depolama darboğazlarının tespiti, tasarım kararlarından operasyona uzanan neden-sonuç zincirinin (`CauseEffectNode`) kronolojik olarak oluşturulması, öğrenilen dersler ile bilgi puanlarının türetilmesi ve kampanya ekonomisi (bütçe, itibar, araştırma) üzerindeki etkilerin uygulanması gerekiyordu.

## Kararlar

1. **Çekirdek Değerlendirme Modeli (`Apogee.Core.Simulation.Report`):**
   - **`CriterionEvaluationResult`:** Her bilimsel hedefin eşik ve gerçekleşen değerini (`ActualValue`, `IsMet`, `SummaryText`) açıklar.
   - **`ScienceDeliverySummary`:** İndirilen vs. üretilen vs. depolama taşmasıyla kaybedilen veri oranını (`DeliveryRatio`, `TotalDroppedGb`, `HasDataBottleneck`, `BottleneckAnalysis`) hesaplar.
   - **`CauseEffectNode`:** Tasarım, Fırlatma, Seyir, Operasyon Olayı, İniş (EDL) ve Bilim safhalarında yaşanan kararları ve arızaları neden-sonuç ilişkisiyle sade dilde bağlar.
   - **`MissionCampaignImpact`:** Başarı primi veya kaza cezası olarak bütçe, itibar, araştırma ve bilgi puanı değişimlerini hesaplar.
   - **`MissionReportEvaluator`:** Saf C# motoru olarak tüm telemetri kayıtlarını alır ve deterministik `MissionReport` üretir.

2. **Strateji Komutları (`Apogee.Strategy.Mission`):**
   - **`MissionInstance`:** `Report`, `LaunchResult`, `Profile` alanlarıyla zenginleştirildi.
   - **`CompleteMissionCommand`:** Misyonu sonuçlandırır, raporu oluşturur, bütçe/itibar/araştırma/bilgi puanı etkilerini uygular. Tam simetrik `Undo`/`Redo` desteği ve `AgencyDirector | ScienceDirector | LeadEngineer` rol yetkisi denetimi içerir.

3. **Kullanıcı Arayüzü (`Apogee.UI`):**
   - **`MissionReportViewModel`:** Kriter listesi, bilim verisi ve darboğaz özeti, neden-sonuç zinciri, dersler, kampanya etkisi ve Atölye/Harita navigasyon komutlarını sunar.
   - **`MissionReportScreenController`:** 3 sütunlu UI Toolkit ekranı (`ScreenId.MissionReport`).
   - **Navigasyon:** `OperationsScreenController` görev tamamlandığında Sonuç Raporu ekranına yönlendirir. `ApogeeUIManager` ekranı kaydeder.

4. **Doğrulama ve Testler:**
   - 9 yeni birim testi (`ReportRunnerTests`) yazıldı:
     - Tüm kriterlerin sağlanması durumunda genel başarı
     - Eksik veri durumunda kriter başarısızlığı
     - Fırlatma felaketinde kayıp raporu ve itibar cezası
     - EDL yanmasında aerotermal kayıp zinciri ve ders türetimi
     - Veri darboğazı analizi ve depolama taşması tespiti
     - Neden-sonuç zincirinin kronolojik faz akışı
     - `CompleteMissionCommand` yürütme ve kampanya etkileri
     - `CompleteMissionCommand` simetrik Undo/Redo doğrulaması
     - `MissionReportViewModel` veri bağlama ve olay tetiklenmesi
   - Toplam test sayısı 275'ten **284**'e yükseldi (%100 geçti, 0 hata).
   - `noEngineReferences` kuralı doğrulandı.
