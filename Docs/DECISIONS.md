# ADR-028: M4-09 Kampanya Olay Sistemi (Event Director, Decision Cards, Effects)

## Durum
Kabul Edildi

## Bağlam
GDD §6.3, §9.1, §10.2, §10.3, §14.1 (#15 Olay Penceresi) ve §19.10 gereğince strateji katmanında zaman akarken (günlük/aylık tick adımlarında) ajansı etkileyen kampanya olaylarının tetiklenmesi, oyuncuya görünür artı/eksi etki seçenekleri sunulması ve kararların deterministik olarak simülasyona yansıması gerekiyordu.

## Alınan Kararlar
1. **Veri Modeli (`campaign_events.json` ve `CampaignEventRecord`):**
   - 15 adet zengin kampanya ve tedarikçi olayı tanımlandı (bütçe hibe bonusu, tedarikçi teslimat gecikmesi ve kalite denetimi, araştırma atılımı, jeopolitik zirve, halk ilgisi dalgası, işçi grevi tehdidi, Kestrel stratejik ortaklık teklifi, yetenek transferi, tedarik zinciri kıtlığı, aviyonik hatası, sensör ortak fonlama, motor test atışı, kompozit yeniliği, mali denetim).
   - Her olay; kategori, taban ağırlık (`baseWeight`), tekrarlanabilirlik kuralı (`isRepeatable`), bekleme süresi (`cooldownDays`), tetikleme koşulları (`EventTriggerCondition`) ve en az iki seçenek (`EventOptionRecord`) içerir.
   - Her seçenek oyuncuya görünür özet (`visibleEffectSummary`) ve arka planda somut `EventOutcomeRecord` (bütçe, itibar, araştırma, bilgi puanı, tedarikçi ilişkisi) taşır.

2. **Olay Yönetmeni (`CampaignEventManager` & `EventEvaluator`):**
   - Tetikleme koşulları (`IsEventEligible`): Yıl aralığı, bütçe ve itibar sınırları, zorunlu tedarikçi, aktif araştırma veya üretim varlığı kontrol edilir.
   - Tedarikçi eğilimleri (`CalculateEffectiveWeight`): Şirketlerin `eventWeightModifiers` çarpanları (ör. Helion Dynamics kalite ve gecikme riski) olay taban ağırlıklarını dinamik olarak ölçeklendirir.
   - Deterministik tohumlu RNG (`RngStreamType.Events`): Olay tetiklemeleri ve ağırlıklı seçimler izole tohumla çalışır; simülasyonun diğer sistemlerini bozmaz.

3. **Komut ve Durum Entegrasyonu (`ResolveEventOptionCommand`):**
   - Oyuncu bir opsiyonu seçtiğinde bütçe, itibar (0-100 clamp), araştırma ve bilgi puanları ile tedarikçi ilişki puanları atomik olarak güncellenir.
   - Bütçe hareketleri `EconomyManager` işlem defterine `FinancialRecordType.EventImpact` olarak kaydedilir.
   - Tam simetrik `Undo/Redo` desteği sağlanmıştır.
   - `AdvanceTimeCommand` zaman ilerledikçe bekleme sürelerini ve olay tetiklemelerini yürütür, geri almada önceki bekleyen durumu eksiksiz geri yükler.

4. **UI Toolkit Ekran ve ViewModel:**
   - `EventPopupViewModel`: Reaktif başlık, kategori rozeti, anlatı metni, tarih ve `EventOptionItemViewModel` seçenek listesi.
   - `EventPopupScreenController`: Modal karar kartı, amber vurgulu kenarlıklar, koyu lacivert tema, şık seçenek butonları ve mint/cyan renkli görünür etki özetleri.

5. **Mimari Doğrulama:**
   - `Apogee.Strategy` katmanı tamamen `noEngineReferences = true` ilkesine uymaktadır.
   - EditMode yerel doğrulama testleri 222'den **230**'a çıktı (%100 geçti).


# ADR-029: M5-01 Fırlatma Çözümü (Launch Resolution, Staging, Weather, Abort, Visual Sequence)

## Durum
Kabul Edildi

## Bağlam
GDD §8.1, §8.4, §10.1, §10.4, §10.5, §14.1 (#13 Fırlatma Sekansı) ve §21 M5-01 uyarınca operasyon katmanının ilk adımı olan fırlatma simülasyonunun deterministik tohumlu RNG ile çözülmesi, aşama aşama ayrılma ve telemetri olaylarının kaydedilmesi, rampa hava riski, abort kaçış mekanizmaları, neden-sonuç zinciri ve arayüz sekansı ViewModel/Controller entegrasyonu gerekiyordu.

## Alınan Kararlar
1. **Çekirdek Simülasyon Modeli (`Apogee.Core.Simulation.Launch`):**
   - `LaunchPhaseType`: Ateşleme (Ignition), Kalkış (Liftoff), Max-Q, Aşama-1 Yanma/Ayrılma, Aşama-2 Ateşleme/Yanma, Fairing Ayrılması, Aşama-3 ve Yörüngeye Giriş (OrbitalInsertion).
   - `LaunchOutcome`: Başarı (Success), Kısmi Yörünge (PartialOrbit), Felaket Kaybı (CatastrophicFailure), Rampa İptali (PadAbort), Uçuş Sırasında Kaçış (InFlightAbort) ve Hava İptali (WeatherScrub).
   - `LaunchSimulationParameters`: Görev ID, araç adı, aşama sayısı, aşama güvenilirlikleri, genel sistem güvenilirliği, rampa hava riski, ekvatoral enlem bonusu, insanlı mı (`IsCrewed`), acil kaçış sistemi (`HasAbortSystem`) ve tohum (`Seed`).
   - `LaunchSimulator`: Saf C# fonksiyonel simülasyon motoru. Tohumlu deterministik rastgele sayı üretimi ile çalışır. Rampa hava koşullarını (`LaunchWeatherCondition`), aşama güvenilirlik kontrollerini, arıza durumunda kaçış kulesi (`HandleCrewAbort`) aktivasyonunu ve mürettebat hayatta kalma durumunu simüle eder.
   - Her olay için irtifa (km), hız (m/s), zaman damgası (T+s), teknik telemetri detayları ve oyuncuya dönük sade dil açıklaması (`plainLanguageSummary`) üretilir.

2. **Tasarım Katmanı Köprüsü (`BlueprintLaunchExtensions`):**
   - `Blueprint`, `BlueprintStats` ve fırlatma rampası verilerinden tek fonksiyonla otomatik `LaunchSimulationParameters` nesnesi türetilir.

3. **Strateji ve Komut Entegrasyonu (`LaunchMissionCommand`):**
   - Ajans Başkanı ve Baş Mühendis yetkisi (`PlayerRole.AgencyDirector | PlayerRole.LeadEngineer`).
   - Fırlatma başarısında ajans itibarı artar (+5); felaket kaybında itibar düşer (insanlıda -25, insansızda -8); kaçış kulesi mürettebatı kurtardığında ceza hafifletilir (-6).
   - `CommandBus` üzerinden tam simetrik Undo/Redo desteği.

4. **UI Toolkit Ekran ve ViewModel Katmanı (`LaunchSequenceViewModel` & `LaunchSequenceScreenController`):**
   - GDD §14.1 #13 (Fırlatma Sekansı) ve §10.1 gereksinimlerini karşılayan, reaktif telemetri göstergeleri (İrtifa, Hız, Durum Rozeti, Hava Durumu, Neden-Sonuç Özeti).
   - Kronolojik aşama aşama ilerleme (`StepNextPhaseCommand`) ve sekansı tek tıkla tamamlama (`SkipToEndCommand`).
   - Başarı durumunda sonraki operasyon aşamasına (`ScreenId.Operations`), başarısızlık durumunda strateji haritasına navigasyon.

5. **Mimari ve Testler:**
   - `Apogee.Core`, `Apogee.Design` ve `Apogee.Strategy` katmanlarında `noEngineReferences = true` ilkesi korundu.
   - EditMode yerel doğrulama testleri 230'dan **239**'a çıktı (%100 geçti).

---

# ADR-030: M5-01b Review → Launch Sequence Navigasyonu

**Tarih:** 2026-11-01
**Durum:** Kabul Edildi

## Bağlam

Design Review ekranından Launch Sequence ekranına geçiş eksikti. Oyuncu tasarımını onaylayıp fırlatmaya geçemiyordu.

## Karar

- `ReviewViewModel`'e `ProceedToLaunchCommand` (ViewActionCommand) eklendi. Yalnızca `IsValid == true && ActiveBlueprint != null` olduğunda etkinleşir.
- `ExecuteProceedToLaunch()` metodu: `BlueprintEvaluator.Evaluate()` ile stats hesaplar, `LaunchNavigationArgs` oluşturur (blueprint, stats, missionName=TargetDestinationId, launchPadBaseId="base_cape_pad", isCrewed), `_navigator.GoTo(ScreenId.LaunchSequence, launchArgs)` çağırır.
- `ReviewScreenController.CreateView()` footer'ına "Fırlatmaya Geç" butonu eklendi. `IsValid` observable'ına abone olarak `SetEnabled()` ile aktif/pasif yapılır.
- `LaunchSequenceScreenController.OnShown(object context)` güncellendi: `LaunchNavigationArgs`'ı alır, `ToLaunchParameters()` ile simülasyon parametrelerini türetir, `LaunchSimulator.Simulate()` çalıştırır, `ViewModel.BindResult()` ile UI'ya bağlar.

## Alternatifler

- Review ekranından doğrudan simülasyon çalıştırmak: SoC ihlali olurdu; simülasyon Launch Sequence Controller'ın sorumluluğudur.

## Sonuç

239 test geçiyor. Review → Launch Sequence arası tam navigasyon akışı tamamlandı.

---

# ADR-031: M5-02 Seyir Simülasyonu (Cruise Simulator, Segment Events, Decision Cards)

## Durum
Kabul Edildi

## Bağlam
GDD §6.2, §10.1 (Faz 2: Seyir), §10.2 (Karar Kartları), §10.3, §14.1 (#14 Operasyon Ekranı), §17.3, §19.12 ve §21 M5-02 gereğince fırlatılmış uzay aracının hedef transfer yörüngesindeki seyrinin deterministik olarak simüle edilmesi, segment bazlı olaylar, zayıflığa dayalı anormallikler, karar kartları ve UI Toolkit operasyon ekranı gerekiyordu.

## Alınan Kararlar
1. **Deterministik Seyir Simülatörü (`Apogee.Core.Simulation.Cruise`):**
   - `CruiseSimulator`, `CruiseSimulationParameters`, `CruiseSimulationResult`, `CruiseSegmentEvent`, `CruiseDecisionCard`, `CruiseDecisionOption`.
2. **Strateji Katmanı ve Komutlar (`Apogee.Strategy.Mission`):**
   - `MissionInstance`, `AdvanceCruiseCommand`, `ResolveCruiseDecisionCommand`.
3. **Arayüz Katmanı:** `OperationsViewModel`, `OperationsScreenController`.
4. 248 test geçti.

---

# ADR-032: M5-03 Varış / İniş (EDL) Çözümü

## Durum
Kabul Edildi

## Bağlam
GDD §7.7, §8.1, §10.1, §10.4, §16.1, §19.12 ve §21 M5-03 uyarınca rover ve lander araçlarının hedef gezegen ortamına (vakum, Mars, Venüs) iniş mekaniğinin simülasyonu.

## Alınan Kararlar
1. **Deterministik EDL Motoru (`Apogee.Core.Simulation.EDL`):**
   - `EdlSimulator`, `EdlOutcome`, `EdlPhaseType`, `EdlPhaseEvent`, `EdlSimulationParameters`, `EdlSimulationResult`.
2. **Strateji Komutları:** `ExecuteEdlCommand` (Undo/Redo ve itibar etkisi).
3. **Arayüz Entegrasyonu:** `OperationsViewModel` ve `OperationsScreenController` EDL buton ve telemetri log desteği.
4. 257 test geçti.

---

# ADR-033: M5-04 Bilim Operasyonu ve Veri Darboğazı Çözümü

## Durum
Kabul Edildi

## Bağlam
GDD §8.1 (Güç, Isı, Veri, Haberleşme), §10.1 (Faz 4: Bilim/Operasyon), §10.5 (Downlink vs Üretilen Veri), §11.1 (Bilim Skoru), §13.2 ("Neden?" İpuçları) ve §21 M5-04 uyarınca:
Uzay araçlarının hedefe ulaştıktan sonra (yörünge veya yüzeyde `MissionStatus.Operating`) bilimsel veri toplama, enerji (güneş / RTG / batarya), ısıl kontrol, depolama tamponu (buffer) ve yer istasyonlarına downlink süreçlerinin deterministik bir simülasyon modeliyle çalıştırılması gerekiyordu.
Ayrıca, downlink kapasitesinin veya tampon hafızanın yetersiz olduğu durumlarda "Veri Darboğazı" (Data Bottleneck) oluşarak toplanan bilim verisinin kaybedilmesi ve bilim skorunun kısıtlanması mekaniği uygulanmalıydı.

## Alınan Kararlar
1. **Deterministik Simülasyon Çekirdeği (`Apogee.Core.Simulation.Science`):**
   - `ScienceSimulator`, `ScienceSimulationParameters`, `ScienceSimulationResult`, `SciencePhaseEvent`, `ScienceEventType`, `ScienceEventStatus`.
   - Güç dengesi: Güneş panelleri gündüz üretir (gün ışığı oranı $1 - \text{eclipse}$), RTG 24 saat kesintisiz üretir. Gölge geçişlerinde batarya deşarjı simüle edilir; batarya tükenirse ("brownout") bilimsel aletler kısılır ve kritik olay loglanır.
   - Isıl denge: Isı yükü radyatör kapasitesini aşarsa aletler güvenli moda/kısıtlamaya alınır.
   - Veri üretimi ve darboğazı: Aletlerin günlük veri üretimi ($\text{DataRate} \times \text{DutyCycle} \times \text{OperationalFactor}$) hesaplanır. Dahili depolama kapasitesi aşılırsa veri kaybı (`DroppedDataGb`) ve `BufferOverflow` darboğazı tetiklenir.
   - Downlink: Yer istasyonu görüş süresi ve anten kapasitesine göre Dünya'ya indirilen veri (`DownlinkDeliveredGb`) tampondan düşülür.
   - Sade dil özeti (`PlainLanguageSummary`) oluşturulur.

2. **Komut ve Durum Yönetimi (`Apogee.Strategy.Mission`):**
   - `MissionInstance` alanları genişletildi: `AccumulatedDownlinkDataGb`, `AccumulatedRawDataGeneratedGb`, `AccumulatedDroppedDataGb`, `CurrentBufferStorageGb`, `CurrentBatteryStorageKwh`, `ScienceDaysCompleted`, `ScienceLog`.
   - `ExecuteScienceTurnCommand`: Yetki kontrolü (`AgencyDirector | ScienceDirector | LeadEngineer`). Simetrik `Undo`/`Redo` desteği. Her başarılı downlink için araştırma puanı ödülü.

3. **Arayüz Katmanı (`Apogee.UI`):**
   - `OperationsViewModel`: `CanExecuteScience`, `DownlinkDeliveredGb`, `RawDataGeneratedGb`, `BufferStorageGb`, `DroppedDataGb`, `ScienceDaysCompleted`, `ScienceEventLog`, `ExecuteScienceStepCommand`.
   - `OperationsScreenController`: "Bilim Günü İlerlet" butonu, canlı telemetri etiketi (`_scienceTelemetryLabel`), bilim olayları renkli log satırları.

4. **Test ve Doğrulama:**
   - `ScienceRunnerTests`: 9 yeni test (determinizm, nominal operasyon, batarya kesintisi/brownout, ısıl aşırı yük, depolama taşması/veri kaybı, kısıtlı downlink darboğazı, komut çalıştırma & ödül, Undo/Redo simetrisi, ViewModel veri bağlama).
   - Toplam test sayısı 257'den **266**'ya yükseldi (%100 başarıyla geçti).
   - `noEngineReferences` kuralı doğrulandı.


---

# ADR-034: M5-05 Operasyon Olayları ve Karar Kartları

## Durum
Kabul Edildi

## Bağlam
GDD §10.2 (Karar Kartları), §10.3 (Olay Havuzu İlkeleri), §13.2 ("Neden?" İpuçları), §14.1 (#14 Operasyon Ekranı), §17.3, §19.12 ve §21 M5-05 gereğince operasyonel fazlarda (Seyir, EDL, Bilim) tasarımdaki zayıflığa göre ağırlıklandırılan olay havuzunun ve oyuncu karar kartlarının modellenmesi gerekiyordu.

## Alınan Kararlar
1. **Operasyon Olay Modeli ve Çekirdek Simülasyon (`Apogee.Core.Simulation.Operations`):**
   - `OperationEventRecord`, `OperationEventOption`, `OperationEventPhase`, `OperationHealthSnapshot`, `ActiveOperationEvent`, `OperationEventManager`.
   - Tasarım zayıflığı ağırlıklandırması: Güç marjı yetersizliğinde brownout olasılığı 4 katına, haberleşme darboğazında veri tampon taşması 2.5 katına, radyasyon tehlikesinde güneş fırtınası/radyasyon hasarı 3 kata kadar çarpanla artar.
   - 15 adet operasyon olayı (`operation_events.json`) veri odaklı olarak eklendi.
2. **Strateji Komutları (`Apogee.Strategy.Mission`):**
   - `TriggerOperationEventCommand`: Olay tetikleme ve pending atama (Undo/Redo).
   - `ResolveOperationEventCommand`: Oyuncu kararını bütçe, itibar, araştırma, güvenilirlik ve veri etkisiyle uygulama (Undo/Redo, yetki kontrolü).
3. **Kullanıcı Arayüzü (`Apogee.UI`):**
   - `OperationsViewModel` ve `OperationsScreenController` operasyon karar kartları (`_decisionBox`) ve olay logları entegrasyonu.
4. **Test ve Doğrulama:**
   - `OperationRunnerTests`: 9 yeni test (katalog bütünlüğü, zayıflık ağırlıklandırması, determinizm, non-repeatable filtreleme, komut yürütme/Undo-Redo, rol yetkilendirmesi, ViewModel bağlama).
   - Toplam test sayısı 266'dan **275**'e yükseldi (%100 başarıyla geçti).
   - `noEngineReferences` kuralı korundu.


---

# ADR-035: M5-06 Misyon Sonuç Raporu

## Durum
Kabul Edildi

## Bağlam
GDD §10.5 (Sonuç Raporu), §13.2 ("Neden?" İpuçları), §14.1 (#16 Sonuç Raporu), §17.3, §19.12 ve §21 M5-06 uyarınca tamamlanan veya erken safhada sonlanan misyonların deterministik analizi, başarı kriterleri denetimi, bilim/downlink darboğazı analizi, kronolojik neden-sonuç zinciri, dersler ve kampanya etkisi üretimi gerekiyordu.

## Alınan Kararlar
1. **Çekirdek Değerlendirme Modeli (`Apogee.Core.Simulation.Report`):**
   - `CriterionEvaluationResult`: Belirlenen başarı kriterlerinin sağlandığını (`IsMet`) ve gerçekleşen veriyi (`ActualValue`) açıklar.
   - `ScienceDeliverySummary`: İndirilen vs. üretilen vs. depolama taşmasıyla kaybedilen veri oranını (`DeliveryRatio`, `TotalDroppedGb`, `HasDataBottleneck`, `BottleneckAnalysis`) hesaplar.
   - `CauseEffectNode`: Tasarım, Fırlatma, Seyir, Operasyon Olayı, İniş (EDL) ve Bilim safhalarındaki kritik kararları ve hataları neden-sonuç zincirine dönüştürür.
   - `MissionCampaignImpact`: Başarı ödülü veya kaza cezası olarak bütçe, itibar, araştırma ve bilgi puanı değişimlerini hesaplar.
   - `MissionReportEvaluator`: Saf C# motoru olarak deterministik `MissionReport` üretir.
2. **Strateji Komutları (`Apogee.Strategy.Mission`):**
   - `MissionInstance`: `Report`, `LaunchResult`, `Profile` alanları eklendi.
   - `CompleteMissionCommand`: Misyonu sonuçlandırır, raporu oluşturur, bütçe/itibar/araştırma/bilgi puanı etkilerini uygular (simetrik `Undo`/`Redo` desteği, rol yetkilendirmesi).
3. **Kullanıcı Arayüzü (`Apogee.UI`):**
   - `MissionReportViewModel`: Kriter listesi, bilim verisi ve darboğaz özeti, neden-sonuç zinciri, dersler, kampanya etkisi ve Atölye/Harita navigasyon komutlarını sunar.
   - `MissionReportScreenController`: 3 sütunlu UI Toolkit ekranı (`ScreenId.MissionReport`).
   - Navigasyon: `OperationsScreenController` görev tamamlandığında Sonuç Raporu ekranına yönlendirir. `ApogeeUIManager` ekranı kaydeder.
4. **Test ve Doğrulama:**
   - `ReportRunnerTests`: 9 yeni test eklendi (tüm kriterlerin sağlanması, eksik veri, fırlatma felaketi, EDL yanması, veri darboğazı analizi, faz akışı, komut yürütme/Undo-Redo, ViewModel bağlama).
   - Toplam test sayısı 275'ten **284**'e yükseldi (%100 başarıyla geçti).
   - `noEngineReferences` kuralı korundu.


---

# ADR-036: M5-07 "Ya Şöyle Olsaydı?" (What-If) Analizi

## Durum
Kabul Edildi

## Bağlam
GDD §10.5 ("Ya şöyle olsaydı?"), §13.2 ("Tek karar değiştirip karşılaştır"), §14.1 (#16 Sonuç Raporu Ekranı) ve §21 M5-07 gereğince tamamlanan misyonların ardından oyuncunun, operasyonel karar kartlarından birini değiştirerek deterministik simülasyon sayesinde raporu anlık olarak yeniden hesaplayabilmesi ve orijinal sonuç ile hipotetik sonuç farkını karşılaştırabilmesi gerekiyordu.

## Alınan Kararlar
1. **Çekirdek Simülatör ve Karşılaştırma Modeli (`Apogee.Core.Simulation.Report`):**
   - `WhatIfChange`: Değiştirilen olayın kimliği, başlığı, orijinal seçimi ve alternatif seçeneği saklar.
   - `WhatIfComparison`: Orijinal rapor ile hipotetik raporu karşılaştırarak `OutcomeChanged`, bütçe/itibar/bilim verisi/araştırma puanı net deltalarını ve sade dilde özetini (`DeltaSummaryText`) hesaplar.
   - `WhatIfSimulator`: Saf C# fonksiyonel modülüdür. Orijinal kampanya veya misyon verilerini asla mutasyona uğratmaz (immutability). Seçilen alternatif seçeneğin net farklarını ve yamalı olay günlüğünü (`BuildPatchedEventLog`) uygulayarak `MissionReportEvaluator.Evaluate` üzerinden deterministik rapor üretir.
2. **Kullanıcı Arayüzü Modeli ve Ekran Entegrasyonu (`Apogee.UI`):**
   - `WhatIfViewModel`: Yeniden karar verilebilir olayları filtreler (`EligibleEvents`), alternatif seçenekleri listeler, `RunWhatIf` ile hesaplamayı tetikler ve `ClearWhatIf` ile başlangıç durumuna dönülmesini sağlar.
   - `MissionReportViewModel`: `WhatIf` alt modelini barındırır ve misyon bağlandığında otomatik devreye alır.
   - `MissionReportScreenController`: Sonuç Raporu ekranının altına 🔀 "Ya Şöyle Olsaydı?" paneli eklenmiştir. Olay seçim butonları, alternatif karar seçenekleri, sonuç rozeti ve fark telemetrisi reaktif olarak güncellenir.
3. **Test ve Doğrulama:**
   - `WhatIfRunnerTests`: 9 yeni test eklendi (uygun olayların filtrelenmesi, alternatif seçenek listesi, başarısızlıktan başarıya dönüşüm, net delta hesaplamaları, immutability, determinizm, ViewModel bağlama, RunWhatIf, ClearWhatIf).
   - Toplam test sayısı 284'ten **293**'e yükseldi (%100 başarıyla geçti, 0 hata).
   - `noEngineReferences` kuralı korundu.


---

# ADR-037: M5-08 Misyon Skoru ve Madalyalar

## Durum
Kabul Edildi

## Bağlam
GDD §11 (Skor ve İlerleme), §11.1 (Misyon Skoru 5 Temel Direk), §11.2 (Madalyalar), §14.1 (#16 Sonuç Raporu) ve §21 M5-08 gereğince tamamlanan misyonların 5 çekirdek performans direği (Bilim Teslimi %35, Amaç Başarısı %20, Kaynak Verimliliği %15, Karar Kalitesi %15, Planlama Verimliliği %15) üzerinden ağırlıklı olarak puanlanması, zorluk çarpanı ile ölçeklenmesi, aşırı marj israfının cezalandırılması, belirlenen başarımlar (madalyalar) için bonus puan ve rozet atanması ve sonuç raporunda gösterilmesi gerekiyordu.

## Alınan Kararlar
1. **Ayar Dosyası ve Veri Modeli (`scoring_config.json` / `Apogee.Core.Simulation.Scoring`):**
   - `ScoringConfig`: Ağırlıkların kodda gömülü kalmaması için JSON tabanlı konfigürasyon yapısı.
   - `MedalDefinition` & `AwardedMedal`: Madalyaların kimliği, adı, açıklaması, simgesi, kategorisi ve bonus puanı.
   - `MissionScoreBreakdown`: 5 direğin oranları, taban puan, zorluk katsayısı, madalya bonusları, nihai puan (`FinalScore`) ve harf notu derecelendirmesi (`Grade`: S, A, B, C, D, F).
2. **Çekirdek Skor Hesaplayıcı (`MissionScoreCalculator`):**
   - Bilim Teslimi (%35), Amaç Başarısı (%20), Kaynak Verimliliği (%15, aşırı marj cezası), Karar Kalitesi (%15), Planlama Verimliliği (%15).
   - Madalya Kuralları: "Sıfır kayıp veri", "Pencereyi yakaladı", "Fırtınayı atlattı", "İlk insanlı", "Ucuz ama etkili", "Tek kaynak yok", "Hafif ve hızlı".
3. **Sonuç Raporu ve Arayüz Entegrasyonu (`MissionReport`, `MissionReportViewModel`, `MissionReportScreenController`):**
   - `MissionReport` modeline `ScoreBreakdown` özelliği eklendi. `MissionReportEvaluator` raporu üretirken skoru ve madalyaları otomatik bağlar.
   - `MissionReportViewModel` reaktif `FinalScoreText`, `GradeText`, `ScorePillarsSummaryText` ve `AwardedMedalsList` alanları ile donatıldı.
   - `MissionReportScreenController` başlık barında Skor ve Harf Derecesi rozetlerini gösterir; sağ sütunda ise "6. KAZANILAN MADALYALAR" bölümünü ve direklerin özet dökümünü çizer.
4. **Test ve Doğrulama:**
   - `ScoringRunnerTests`: 9 yeni test eklendi (varsayılan ayarlar, yüksek skor / S-A derecesi, başarısız görev / F derecesi, marj cezası, zorluk çarpanı, madalya kazanımları, ViewModel bağlama).
   - Toplam test sayısı 293'ten **302**'ye yükseldi (%100 başarıyla geçti, 0 hata).
   - `noEngineReferences` kuralı korundu.


---

# ADR-038: M6-01 Kilometre Taşı ve Görev Tahtası Sistemi

## Durum
Kabul Edildi

## Bağlam
GDD §4.4 (Kampanya Hedefleri / Kilometre Taşları), §14.1 (Görev Tahtası), §19.13 (Kampanya Durumu) ve §21 M6-01 gereğince kampanya boyunca oyuncunun ilerlemesini yönlendiren, zorunlu sıralı olmayan ancak öneri listesi sunan 8 resmi kilometre taşının (`milestones.json` / `MilestoneRecord`) tanımlanması, tamamlanan başarılı misyonlara göre otomatik değerlendirilmesi, ödüllerin (`ClaimMilestoneRewardCommand`) bütçe/itibar/bilgi puanı olarak kampanyaya aktarılması ve simetrik Undo/Redo ile geri alınabilmesi gerekiyordu.

## Alınan Kararlar
1. **Veri Modeli ve Katalog (`MilestoneRecord.cs`, `milestones.json`):**
   - 8 resmi kilometre taşı (İlk Yörünge Uydusu, Ay'a İlk Sonda, Ay Yüzeyine Robotik İniş, İlk Gezegenler Arası Sonda, Mars Rover, İnsanlı Ay Görevi, Numune Getirme, İnsanlı Mars Görevi).
   - Önkoşul kilometre taşı kimlikleri (`required_milestone_ids`), hedef ve görev tipi filtreleri, insanlı/numune bayrakları ve ödül miktarları (Bütçe, İtibar, Bilgi Puanı).
2. **Değerlendirme ve İlerleme Mantığı (`MilestoneEvaluator`, `MilestoneProgress`, `MilestoneManager`):**
   - `MilestoneStatus`: `Locked`, `Available`, `CompletedPendingClaim`, `Claimed`.
   - `MilestoneEvaluator`: Misyonun genel başarı durumunu, hedefini, görev tipini, insanlı olma durumunu saf C# fonksiyonlarıyla kontrol eder.
   - `MilestoneManager`: Kampanyadaki tüm kilometre taşlarının durumunu tutar, tamamlanma ve ödül alma olaylarını (`OnMilestoneCompleted`, `OnMilestoneClaimed`) reaktif tetikler.
   - `CompleteMissionCommand` Entegrasyonu: Bir misyon başarıyla sonuçlandığında `CampaignState.Milestones.EvaluateProgress` otomatik çağrılır.
3. **Komut ve Ekonomi Entegrasyonu (`ClaimMilestoneRewardCommand`):**
   - `AgencyDirector`, `LeadEngineer`, `ScienceDirector` yetkisiyle çalışır.
   - Bütçe, itibar (maksimum 100) ve bilgi puanı ödüllerini kampanyaya ekler.
   - Ekonomi yöneticisi (`EconomyManager`) aktifse muhasebe defterine (`Ledger`) gelir kaydı işler.
   - Simetrik `Undo` ile ödülleri geri alır ve durumu önceki haline döndürür.
4. **Kullanıcı Arayüzü / ViewModel (`MilestoneBoardViewModel`, `MilestoneItemViewModel`):**
   - Görev tahtasındaki kilometre taşlarını sıralı liste olarak sunar.
   - İlerleme oranı (ör. `1 / 8`), aktif odak hedefi ve tamamlanma bildirimlerini yönetir.
   - "Ödülü Al" eylemi ile tek tıkla ödül toplama sağlar.
5. **Test ve Doğrulama:**
   - 7 yeni test senaryosu (`MilestoneLocalRunnerTests` & `MilestoneRunnerTests`):
     - Başlangıç durumu ve önkoşul kilit uygunluğu
     - Başarılı misyon eşleşmesi doğrulaması
     - Başarısız veya hedefi uyuşmayan misyon reddi
     - İlerleme değerlendirmesi ile `CompletedPendingClaim` geçişi
     - Ödül alma komutunun bütçe/itibar/bilgi puanı artırması ve sonraki taşı açması
     - Ödül alma komutunun geri alınması (Undo) ile kaynak ve durum restorasyonu
     - `MilestoneBoardViewModel` veri bağlama ve reaktif ödül talep testi
   - Toplam test sayısı 302'den **309**'a yükseldi (%100 başarıyla geçti, 0 hata).
   - `noEngineReferences` kuralı doğrulandı.


---

# ADR-039: M6-02 Kampanya Yaşam Döngüsü — Başlangıç ve Bitiş Sistemi

**Tarih:** 2026-11-08  
**Durum:** Kabul Edildi  
**İlgili:** GDD §4.1, §4.5, §21 M6-02

## Özet

Kampanya başlangıç kurulumu (ajans adı, bölge, seed) ve dört bitiş koşulu (iflas, süre dolumu, zafer, emeklilik) uygulandı.

## Eklenen Bileşenler

- `CampaignEndStatus` enum (5 değer: Active, Bankruptcy, TimeExpired, AllMilestonesCompleted, VoluntaryRetirement)
- `CampaignEndReport` immutable rapor sınıfı (misyon istatistikleri, kilometre taşları, bilim verisi, anahtar başarılar)
- `CampaignEndEvaluator` pure-static değerlendirici + rapor üreticisi
- `EndCampaignCommand` (Undo/Redo destekli, rol kontrolü)
- `CampaignSetupArgs` ajans kurulum parametreleri
- `CampaignEndViewModel` + `CampaignEndScreenController` (ScreenId.CampaignEnd = 22)

## Değiştirilen Bileşenler

- `CampaignState`: AgencyName, EndStatus, EndReport alanları eklendi
- `AdvanceTimeCommand`: Her zaman adımında otomatik bitiş koşulu kontrolü; Undo'da snapshot restore
- `ScreenId`: MilestoneBoard = 21, CampaignEnd = 22 eklendi

## Sonuçlar

- 8 yeni test: toplam **317 test, %100 başarıyla geçti**
- noEngineReferences kuralı korundu (Core/Design/Strategy assemblyleri)
- WebGL uyumlu


---

# ADR-040: M6-04 Sürümlü Kayıt/Yükleme ve Misyon Kodu (Save/Load)

**Tarih:** 2026-11-08  
**Durum:** Kabul Edildi  
**İlgili:** GDD §17.5, §19.13, §21 M6-04

## Özet

`CampaignSaveData` sürümlü veri sözleşmesi ve pure C# `CampaignSaveSerializer` geliştirildi. WebGL dostu, string tabanlı JSON gidiş-dönüş eşitliği 9 yeni test ile doğrulandı. Toplam test sayısı 326'ya ulaştı (%100 geçti).

---

# ADR-041: M6-05 İçerik Doğrulama ve Mars İnsanlı Tasarım Doğrulaması

**Tarih:** 2026-11-08  
**Durum:** Kabul Edildi  
**İlgili:** GDD §5.2, §7.6, §7.7, §9.2, §16.4, §16.7, §21 M6-05

## Özet

GDD §21 M6-05 kabul kriteri olan *"İçerik doğrulayıcıdan geçer; Mars insanlı tasarlanabilir."* hedefi doğrultusunda:
1. `materials.json` içindeki malzeme araştırma önkoşul anahtarları (`tech_*`) doğrudan araştırma ağacı kimlikleriyle (`struct-al-li-alloy`, `struct-titanium`, `struct-carbon-composite`) senkronize edildi.
2. `LaunchPadInfo` seviye 5 süper ağır fırlatma tesisi önayarı (`LaunchPadInfo.Level5Colossal`, 9000t, 5 aşama) eklendi.
3. Mars insanlı tam görev mimarisi için çok aşamalı roket + uzay aracı tasarımı (`mars-crewed-bp`) modellendi. 19,200 m/s Δv, kabin, ECLSS, abort sistemi, RTG, ısı kalkanı ve %98 güvenilirlik şartı 8 doğrulayıcıdan eksiksiz geçerek test edildi.

## Sonuçlar

- 1 yeni test: toplam **327 test, %100 başarıyla geçti**
- `noEngineReferences` ilkesi korundu
- WebGL uyumluluğu korundu

---

# ADR-042: M6-06 Eğitim ve Terimler Katmanı (Glossary & Knowledge Base)

**Tarih:** 2026-11-08  
**Durum:** Kabul Edildi  
**İlgili:** GDD §13, §21 M6-06, §23

## Özet

GDD §13 (Eğitim ve Yardım Sistemi) ve §23 (Terimler Sözlüğü) uyarınca oyun içi bilgi tabanı ve sözlük sistemi kuruldu:
1. `glossary.json` ve `GlossaryTermRecord`: 12 temel uzay ve ajans terimi (Δv, Isp, Faydalı Yük, Bus, EDL, Transfer Penceresi, Downlink, Güç Marjı, RTG, Termal Kısılma, Tek Tedarikçi Riski, İnsanlı Sertifikasyon) tanımlandı.
2. `GlossaryManager`: Saf C# arama ve kategori filtreleme motoru (`noEngineReferences`).
3. `GlossaryViewModel`: Reaktif terim arama, filtreleme, seçili terim ve navigasyon desteği.
4. `GlossaryScreenController`: UI Toolkit 2 sütunlu interaktif sözlük ekranı.
5. Kullanıcı yönlendirmesi doğrultusunda TutorialManager durum makinesi kapsam dışı tutuldu; sözlük ve bilgi tabanı altyapısı eksiksiz tamamlandı.

## Sonuçlar

- 3 yeni test: toplam **330 test, %100 başarıyla geçti**
- Pure C# prensibi ve WebGL uyumluluğu korundu


---

# ADR-043: M7 Çok Oyunculu (Online) ve Botların İptal Edilmesi

**Tarih:** 2026-11-08  
**Durum:** Kabul Edildi (İptal Kararı)  
**İlgili:** GDD §12, §21 M7

## Bağlam ve Karar

Kullanıcı yönergesi doğrultusunda M7 (Online, Relay Sunucusu, Komut Yayını, Lobi, Öneri-Onay, Gizli Öncelikler ve Botlar) tamamen kapsam dışı bırakılmış ve iptal edilmiştir.
Oyun doğrudan tek oyunculu kampanya/tasarım/simülasyon yapısı ile **M8 — Cila ve Teslim** aşamasına geçmektedir.
Assets/_Project/Net/ dizini ve Apogee.Net gereksiz çevrimiçi bağımlılıklardan arındırılmıştır.
