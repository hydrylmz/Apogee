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


---

# ADR-044: M8-04 TR/EN Yerelleştirme Sistemi

**Tarih:** 2026-11-08
**Durum:** Kabul Edildi
**İlgili:** GDD §17.6, §2.2, §13.3, §21 M8-04, EK B (Definition of Done)

## Bağlam

GDD §17.6: *"Tek metin tablosu (anahtar → TR/EN). Kod içinde sabit metin yok. Sayı/tarih biçimleri kültüre duyarlı."*

Mevcut kod tabanında UI ekranlarındaki metinler doğrudan C# string literalleri olarak yazılmıştı ve yerelleştirme altyapısı yoktu. M8 "Definition of Done" gereği sabit metin kodda bulunmamalı; tüm kullanıcıya dönük metinler anahtar-değer tablosundan gelmeli.

## Alınan Kararlar

### 1. Çekirdek Lokalizasyon Altyapısı (`Apogee.Core.Localization`)

- **`Language.cs`**: `TR` (varsayılan) ve `EN` değerlerini barındıran enum. `Apogee.Core` assembly'sinde, `noEngineReferences = true`.
- **`ILocalizationManager.cs`**: `Get(key)`, `Get(key, args)`, `SetLanguage()`, `LoadLocale()`, `LanguageChanged` olayı ile tam sözleşme.
- **`LocalizationManager.cs`** (pure C#):
  - Singleton pattern (`LocalizationManager.Instance`) + test izolasyonu için doğrudan `new LocalizationManager()`.
  - İki dil tablosu (TR + EN) `Dictionary<string, string>` ile O(1) arama.
  - **Fallback zinciri:** EN → TR → `?key` placeholder (asla exception atmaz).
  - `string.Format(CultureInfo.InvariantCulture, ...)` ile `{0}` argüman desteği.
  - `LanguageChanged` olayı yalnızca dil gerçekten değişince tetiklenir.
  - **Özel JSON ayrıştırıcısı** (`ParseLocaleJson`): Newtonsoft bağımlılığı olmadan düz `{key: value}` JSON'u okur. `\n`, `\r`, `\t`, `\"`, `\\`, `\/`, `\uXXXX` kaçış dizilerini destekler. `Apogee.Core`'da `noEngineReferences` kısıtına uyumlu.
  - `LoadLocale()` çağrısı mevcut anahtarların üzerine yazar → hot-reload desteği.

### 2. Locale Veri Dosyaları (`Assets/_Project/Data/Catalog/Json/`)

- **`locale_tr.json`**: ~200 anahtar, Türkçe değerler.
- **`locale_en.json`**: ~200 anahtar, İngilizce değerler.

Kapsanan bölgeler (19 ekran/bölüm):
`general`, `nav`, `topbar`, `workshop`, `results`, `review`, `composer`, `dossier`, `launch`, `operations`, `event`, `report`, `research`, `contracts`, `bases`, `production`, `milestones`, `campaignEnd`, `comparison`, `templates`, `glossary`, `settings`, `tooltip`

Format-arg destekli anahtarlar: `bases.level` → `"Level {0}"`, `milestones.progress` → `"{0} / {1} Completed"`, `operations.cruise.segment` → `"Segment {0}/{1}"`.

### 3. Kullanım Şablonu (UI Katmanı)

```csharp
// ScreenController veya ViewModel içinde
var loc = LocalizationManager.Instance;
loc.LanguageChanged += OnLanguageChanged;
myLabel.text = loc.Get("workshop.title");
// Format argümanlı:
levelLabel.text = loc.Get("bases.level", base.Level);
```

Dil değişince `LanguageChanged` olayını dinleyen tüm controller'lar UI'ı yeniden doldurur.

### 4. Test ve Doğrulama (`LocalizationRunnerTests.cs`)

12 yeni test:
- Varsayılan dil TR
- TR/EN değer dönüşü
- Eksik EN anahtarı → TR fallback
- Tamamen eksik anahtar → `?key` placeholder
- Format argümanı ikamesi (InvariantCulture)
- `SetLanguage` → `LanguageChanged` olayı
- Aynı dile geçiş → olay tetiklenmez
- `ParseLocaleJson` düz okuma
- `ParseLocaleJson` kaçış karakterleri
- `LoadLocale` üzerine yazma
- Gerçek locale JSON dosyaları okunur ve spot-check doğrulanır

Toplam test sayısı 330'dan **342**'ye yükseldi (%100 geçti, 0 hata).

## Sonuçlar

- `noEngineReferences` kuralı korundu (Core assembly'de Unity bağımlılığı yok)
- WebGL uyumlu (System.IO.File kullanımı yalnızca test runner'da; oyun tarafı `LoadLocale(json)` string API'sini kullanır)
- Singleton `LocalizationManager.Instance` thread-safe değil (Unity single-threaded; bu yeterli)

---

# ADR-045: M8-06 NASA Atıf Ekranı ve Veri Kaynakları Modeli

## Durum
Kabul Edildi

## Bağlam
NASA Space Apps Challenge 2026 yarışma gereksinimleri ve GDD §17.7 uyarınca, projede yararlanılan NASA veri setlerinin (Planetary Fact Sheets, JPL Horizons, Mars EDL verileri, Moon Fact Sheet, Europa Clipper görev parametreleri), açık kaynak kütüphanelerinin (xoshiro256**, Howard Hinnant Date, NativeWebSocket) ve ses varlıklarının şeffaf bir şekilde belgelenmesi, atfedilmesi ve oyun içinde erişilebilir bir ekran ile sunulması gerekiyordu.

## Alınan Kararlar

### 1. Veri Kaynağı Modeli (`attribution_data.json` & `AttributionRecord`)
- `Assets/_Project/Data/Catalog/Json/attribution_data.json` dosyasında 12 adet doğrulanmış veri kaynağı ve kütüphane atıfı listelendi.
- `AttributionRecord` modeli `ICatalogRecord` sözleşmesini uygulayarak `CatalogLoader` ile tip güvenli yüklenebilir kılındı.
- Her kayıt `id`, `category` (`NasaData`, `OpenSourceLibrary`, `AudioAsset`, `ArtAsset`, `Other`), `title`, `description`, `source`, `url`, `licenseType` (`PublicDomain`, `CreativeCommonsCC0`, `MIT`, `Boost` vb.), `licenseText` ve `usedInSystems` alanlarını içerir.

### 2. Sorgu Yöneticisi (`AttributionManager`)
- `Apogee.Core.Attribution` altında pure C# sınıfı oluşturuldu (`noEngineReferences=true`).
- Kategoriye göre listeleme, NASA kaynaklarını izole çekme (`GetNasaSources`), kimliğe göre arama ve metin araması (`Search`) fonksiyonları eklendi.

### 3. Ekran ve Gezinti Entegrasyonu (`AttributionViewModel` & `AttributionScreenController`)
- `ScreenId.Attribution = 23` tanımlandı.
- `AttributionViewModel` reaktif `ObservableValue` ile kategori sekmeleri (`All`, `NASA Data`, `OpenSource`, `Audio`, `Art`), filtrelenmiş liste ve seçili detay kartını bağladı.
- `AttributionScreenController` UI Toolkit ile başlık, NASA Space Apps bilgilendirme rozeti, kategori sekme çubuğu, dikey kaydırılabilir kart listesi ve detay paneli (kaynak, açıklama, lisans, URL) sundu.

### 4. Test ve Doğrulama (`AttributionRunnerTests.cs`)
- 10 yeni test:
  - JSON kataloğunun eksiksiz ve hatasız yüklenmesi
  - NASA veri kaynaklarının varlığı ve lisans tipi doğrulaması
  - Açık kaynak kütüphanelerin ve sistem eşleştirmelerinin doğrulanması
  - `AttributionManager` GetById / GetCategories / GetByCategory / Search davranışları
  - `AttributionViewModel` başlangıç durumu ve sekme filtreleme reaktivitesi
  - Kayıt alanlarının boş olmama kontrolü
- Toplam test sayısı 342'den **352**'ye yükseldi (%100 geçti, 0 hata).

## Sonuçlar
- NASA Space Apps Challenge 2026 şeffaflık ve veri kaynağı doğrulama kriteri eksiksiz karşılandı.
- Pure C# Core mimarisi korundu.

---

# ADR-046: M8-01 Görsel Cila (UI Toolkit Transitions, Glowing Accents, Map Styles)

## Durum
Kabul Edildi

## Bağlam
GDD §14, §15.1 ve M8-01 gereksinimleri doğrultusunda, arayüz bileşenlerinin (butonlar, sekme düğmeleri, telemetri çubukları, kartlar, Dünya ve Güneş Sistemi haritaları) tutarlı bir animasyon, hover geçişi ve parıltı (glow) sistemine kavuşturulması gerekiyordu.

## Alınan Kararlar
1. **Buton ve Sekme Stilleri (`.apogee-btn`, `.apogee-tab-btn`):**
   - `.apogee-btn`, `.apogee-btn--primary`, `.apogee-btn--secondary`, `.apogee-btn--go`, `.apogee-btn--danger` sınıfları `Common.uss`'e eklendi.
   - `transition-property: background-color, border-color, scale` ile 100ms akıcı tepkime sağlandı; aktif durumda hafif baskı (scale 0.98) efekti verildi.
   - Sekme çubukları için `.apogee-tab-btn`, `.apogee-tab-btn:hover` ve `.apogee-tab-btn--active` sınıfları oluşturuldu.

2. **Parıltı ve Vurgu Sınıfları (`.apogee-glow--*`):**
   - Cyan, amber, green ve red renklerinde şeffaf arka plan ve kenarlık kombinasyonuyla tematik parıltı sınıfları tanımlandı.
   - Kartlar için `.apogee-card--interactive` ve hover zoom (scale 1.01) eklendi.

3. **Telemetri Geçişleri (`.telemetry-bar-fill--animated`):**
   - Telemetri çubuk dolumlarında anlık keskin değişim yerine 250ms `transition: width, background-color` animasyonu sağlandı.

4. **Harita ve Yörünge Detayı:**
   - Dünya haritası grid çizgileri (`.map-viewport-grid`), radar halkaları (`.map-radar-ring`), bölge düğümleri (`.map-region-node`) ve hover ölçeklemesi.
   - Güneş sistemi yörünge yolları (`.solar-orbit-line`) ve gezegen odak düğümleri (`.solar-planet-marker`).

## Sonuçlar
- UI Toolkit CSS standartlarına tam uyum sağlandı.
- Tüm ekranlar genelinde tutarlı hover, odak ve geçiş hissi tamamlandı.

---

# ADR-047: M8-02 Ses/Müzik Mimarisi ve Telifsiz Lisans Dokümantasyonu

## Durum
Kabul Edildi

## Bağlam
GDD §15.4 ve M8-02 uyarınca, APOGEE'nin UI, fırlatma, operasyon ve strateji sesleri ile ortam müziklerinin yönetilmesi için saf C# sözleşmeli ve Presentation katmanında Unity AudioSource ile çalışan bir ses sistemi kurulması, ayrıca yarışma gereksinimi olarak kullanılan tüm ses varlıklarının açık kaynak / kamuya açık (CC0 / Public Domain) lisans dökümünün yapılması gerekiyordu.

## Alınan Kararlar
1. **Saf C# Çekirdek Modeli (`Apogee.Core.Audio`):**
   - `AudioEvent` enum: UI (tıklama, hover, sekme, modal), Fırlatma (geri sayım, motor ateşleme, aşama ayrılma), Operasyon (downlink pingleme, atmosfer sürtünmesi, paraşüt, itkili iniş, başarı/kaza) ve Müzik temaları.
   - `AudioChannel` enum: Master, Music, Sfx, Ambience, Ui bağımsız kanal matrisi.
   - `AudioSettingsData`: 0.0 - 1.0 aralığına kenetlenen ses seviyeleri, susturma (`IsMuted`) ve etkili kanal ses hesabı (`GetEffectiveVolume`).
   - `IAudioService` & `NullAudioService`: Deterministik ve test edilebilir olay tabanlı ses servisi ve oynatma geçmişi kaydı.

2. **Unity Presentation Entegrasyonu (`Apogee.UI.Audio`):**
   - `UnityAudioService`: MonoBehaviour bileşeni; Inspector üzerinden AudioSource ve AudioClip eşlemeleri (`_clipLookup`), pitch modülasyonu ve WebGL uyumlu ses yönetimi.

3. **Lisans Belgeleri:**
   - `Docs/Licenses/LICENSES.md`: Üçüncü taraf kütüphane ve genel telif bildirimi.
   - `Docs/Licenses/audio_credits.md`: Her bir ses olayı ve müzik parçasının Kenney (CC0 1.0), Freesound (CC0 1.0) ve NASA Audio Archives (Public Domain) kaynak ve lisans dökümü.

4. **Test ve Doğrulama (`AudioRunnerTests.cs`):**
   - 8 yeni test: Varsayılan değerler, sınırlama (clamping), kanal çarpanları, susturma davranışı, SFX ve Müzik tetikleyicileri, geçmiş temizleme ve kanal kategorizasyonu.
   - Toplam test sayısı 352'den **360**'a yükseldi (%100 geçti, 0 hata).

## Sonuçlar
- Pure C# Core mimarisi bozulmadan korundu (`noEngineReferences=true`).
- NASA Space Apps Challenge telifsiz içerik kuralı sağlandı.

---

# ADR-048: M8-07 WebGL ve Performans Optimizasyon Standartları

## Durum
Kabul Edildi

## Bağlam
GDD §17.3, §17.4, §20.4 ve M8-07 uyarınca, APOGEE'nin birincil hedef platformu olan WebGL'de hızlı açılış (<5 sn), düşük dosya boyutu (<35 MB Brotli), akıcı kare hızı (30+ FPS) ve kararlı bellek kullanımı (256 MB başlangıç heap, geometric büyüme) sağlamak için derleme ayarlarının, bellek/GC yönetiminin ve sunucu MIME başlıklarının standartlaştırılması gerekiyordu.

## Alınan Kararlar
1. **Derleme Ayarları (`ProjectSettings.asset`):**
   - `webGLCompressionFormat: 2` (Brotli sıkıştırma aktif).
   - `webGLDecompressionFallback: 1` (Brotli desteklemeyen eski sunucularda JS fallback).
   - `webGLInitialMemorySize: 256` MB başlangıç heap boyutu (katalog ve font yüklemelerinde OOM önleme).
   - `stripEngineCode: 1` ve `ManagedStrippingLevel: High` (gereksiz Unity C++ modüllerini ve IL kodunu soyma).

2. **Mimari GC ve Bellek İlkeleri:**
   - Core / Design / Strategy katmanlarında sıfır MonoBehaviour tahsisatı.
   - CommandBus yığın boyutu sınırlandırması (`maxHistorySize: 50`).
   - Tek seferlik başlangıç JSON parsing ve O(1) in-memory arama sözlükleri.

3. **Dokümantasyon:**
   - `Docs/WEBGL_OPTIMIZATION.md` belgesi oluşturuldu; Nginx ve Apache dağıtım kuralları ile bellek bütçesi tanımlandı.

## Sonuçlar
- WebGL ilk yükleme boyutu ve bellek çökme riskleri minimize edildi.
- Jüri ve kullanıcı tarayıcılarında kesintisiz performans güvence altına alındı.

---

# ADR-049: M8-08 Demo Hazırlığı, 3 Dakikalık "Altın Yol" ve Yarışma Teslimatı

## Durum
Kabul Edildi

## Bağlam
NASA Space Apps Challenge 2026 değerlendirmesi ve jüri sunumu için oyunun tüm temel mekaniklerini (Ajans Stratejisi → Roket Montajı → Fırlatma → Seyir Olayı → EDL İnişi → Bilim Verisi → Rapor ve What-If Analizi) 180 saniyede kesintisiz sergileyen "Altın Yol" senaryosu, tanıtım videosu metni ve yarışma başvuru içeriklerinin hazırlanması gerekiyordu.

## Alınan Kararlar
1. **3 Dakikalık "Altın Yol" Akışı (`Docs/DEMO_GUIDE.md`):**
   - 0:00 - 0:30: Kampanya ve Ay Gezgini Misyon Besteleme.
   - 0:30 - 1:15: Atölyede modüler slot montajı ve canlı telemetri çubuklarının GO durumuna geçişi.
   - 1:15 - 1:50: Fırlatma ve aşama ayrılması.
   - 1:50 - 2:25: Seyir operasyonları ve anomali karar kartı çözümü.
   - 2:25 - 2:45: EDL retro-roket inişi ve bilimsel veri downlink.
   - 2:45 - 3:00: Sonuç raporu, Grade S skoru, "Ya Şöyle Olsaydı?" alternatifi ve NASA Atıflar ekranı doğrulaması.

2. **Tanıtım Videosu Senaryosu:**
   - 60 saniyelik kurgu planı (Kanca, Fizik/Tasarım, Operasyon, Eğitim ve Çağrı).

3. **Yarışma Başvuru Dokümantasyonu:**
   - İngilizce & Türkçe proje başlığı, asansör konuşması ve çözüm özeti.

## Sonuçlar
- M8 (Cila ve Teslim) kilometre taşının tüm teslimatları tamamlandı.
- Proje NASA Space Apps Challenge 2026 jüri değerlendirmesine hazır hale getirildi.


---

# ADR-050: M9-03 Ana Menü Ekranı (MainMenu Screen, ScreenId=1)

**Tarih:** 2026-11-08
**Durum:** Kabul Edildi
**İlgili:** GDD §4.1, §14.1 (#1 Ana Menü), §17.5, §21 M9-03

## Bağlam

M9-02'de `CampaignBootstrapper` ve `CampaignFactory` tamamlanmıştı; uygulama doğrudan Workshop ekranında açılıyordu. GDD §21 M9-03, oynayan kişinin karşılaştığı ilk ekranın bir Ana Menü olması gerektiğini, buradan Yeni Kampanya / Kayıttan Devam / Hızlı Görev / Sözlük / Ayarlar / Atıflar seçeneklerine erişilmesi gerektiğini belirtiyordu.

## Alınan Kararlar

1. **`ICampaignLifecycleHandler` arayüzü (`Apogee.UI.Bootstrap`):**
   - `CampaignBootstrapper` MonoBehaviour'ını test edilebilir biçimde sarmak için saf C# arayüz tanımlandı.
   - `CurrentCampaign`, `IsInitialized`, `StartNewCampaign(args)`, `LoadCampaign(json)` üyeleri.
   - `CampaignBootstrapper` bu arayüzü uygular; testlerde `MockLifecycleHandler` kullanılır.

2. **`ICampaignSaveStorage` + `InMemoryCampaignSaveStorage` + `PlayerPrefsCampaignSaveStorage` (`Apogee.UI.Screens.MainMenu`):**
   - Kayıt okuma/yazma WebGL uyumlu `PlayerPrefs` üzerinden soyutlandı. IndexedDB persistansı Unity tarafından sağlanıyor.
   - `InMemoryCampaignSaveStorage` test ve CI ortamı için; `PlayerPrefsCampaignSaveStorage` oyun içi kullanım için.
   - Anahtar: `apogee_campaign_quicksave_v1`.

3. **`MainMenuViewModel` (`Apogee.UI.Screens.MainMenu`):**
   - Reaktif `ObservableValue` ile title/subtitle/badge/version/status/hasSave/saveInfo.
   - Kampanya kurulum girdileri: `AgencyNameInput`, `SeedInput`, `StartingBudget`, `StartingReputation`.
   - Komutlar: `NewCampaignCommand`, `ContinueCampaignCommand`, `QuickMissionCommand`, `OpenGlossaryCommand`, `OpenAttributionsCommand`, `OpenSettingsCommand`, `QuitCommand`.
   - `RefreshSaveState()`: `CampaignSaveSerializer.FromJson()` ile kayıt okunur, ajans adı + gün + bütçe özetlenir.
   - Navigasyon: Önce `ScreenId.CampaignMap` denenir; kayıtlı değilse `ScreenId.Workshop` fallback.

4. **`MainMenuScreenController` (`Apogee.UI.Screens.MainMenu`):**
   - GDD §14.1 #1 uyarınca hero branding (Ajans Rozetli Başlık), iki sütun (aksiyon butonları + Kampanya Profil Kartı) ve durum/footer satırı.
   - Tohum + Ajans adı inline `TextField` ile konfigüre edilebilir.
   - `OnShown()`: `RefreshSaveState()` çağırır, kayıt yoksa "Devam Et" butonu disabled.

5. **`ApogeeUIManager` güncelleme:**
   - `_initialScreen` varsayılanı `ScreenId.Workshop → ScreenId.MainMenu` olarak değiştirildi.
   - `MainMenuScreenController` tüm controllerlerin en başına kayıt edildi.
   - `BindToUI()`: `MainMenuController?.ViewModel?.BindLifecycleHandler(this)` eklendi.
   - `TeardownUI()`: MainMenu controller Dispose zinciri eklendi.

6. **Yerelleştirme:**
   - `locale_tr.json` ve `locale_en.json` dosyalarına `mainMenu.*` anahtar seti (17 anahtar × 2 dil) eklendi.

7. **Testler:**
   - 7 yeni saf C# yerel doğrulama testi (MockLifecycleHandler, InMemoryCampaignSaveStorage):
     `InitialState`, `NewCampaign_StartsAndNavigates`, `NewCampaign_FallbackToWorkshop`, `ContinueCampaign_LoadsFromStorage`, `ContinueCampaign_NoSaveFailsGracefully`, `NavigationCommands_DispatchScreens`, `RefreshSaveState_UpdatesObservables`.
   - `CreateViewAndLifecycle` testi Unity Play Mode gerektirdiğinden headless runner'dan çıkarıldı (ECall kısıtı).
   - Toplam test sayısı 368 → **375** (%100 geçti, 0 hata).

## Sonuçlar

- `noEngineReferences = true` Core/Design/Strategy/Data katmanlarında korunmaktadır.
- Oyun artık `MainMenu → (Yeni Kampanya) → CampaignMap → Composer → Workshop → Review → Launch → Operations → Report` tam navigasyon zincirine sahiptir.
- WebGL uyumlu save/load: `PlayerPrefs` → IndexedDB (Unity WebGL persist).


---

# ADR-051: M9-04 Kampanya Haritası Ekranı (CampaignMap Screen, ScreenId=3)

**Tarih:** 2026-11-08
**Durum:** Kabul Edildi
**İlgili:** GDD §6.1, §6.2, §6.3, §14.1 (#3 Kampanya Ana Ekranı), §19.9, §21 M9-04

## Bağlam

GDD §6.1 ve §14.1 uyarınca strateji katmanının ana merkezi, ajansın dünya üzerindeki 8 stratejik bölgesini (`RegionRecord`), üs tesislerini (`BaseFacility`), zaman akışını (hız kontrolleri ve gün adımları), harita lenslerini (`MapLens`: Genel Bakış, Lojistik, Haberleşme, Fırlatma Verimi, Ar-Ge, Risk) ve ajansın çekirdek kaynak HUD'unu (Bütçe, İtibar, Ar-Ge, Bilgi Puanı) tek bir etkileşimli komuta masasında sunmalıdır.

## Alınan Kararlar

1. **`RegionMapItemViewModel` (`Apogee.UI.Screens.CampaignMap`):**
   - 8 bölge için enlem/boylam üzerinden 2D silindirik/eşdikdörtgen (equirectangular) harita projeksiyonu:
     `NormalizedX = (lon + 180) / 360 * 100%`, `NormalizedY = (90 - lat) / 180 * 100%`.
   - `RegionLensEvaluator` entegrasyonu: aktif lense göre anlık 0.0–1.0 skorlama, telemetri açıklaması ve yüzde rozeti.
   - Üs envanteri (`ActiveBases`): bölgedeki üslerin türlerine (Rampa, Fabrika, Ar-Ge, İstasyon, Test) göre mini simgeler ve seviye etiketleri.

2. **`CampaignMapViewModel` (`Apogee.UI.Screens.CampaignMap`):**
   - Reaktif HUD: Tarih ("2031-01-01 · Gün 0"), Zaman Hızı (`TimeSpeed`), Bütçe, İtibar, Ar-Ge, Bilgi Puanı, Ajans Adı.
   - Zaman kontrolleri: Duraklat (`Paused`), `1x`, `2x`, `3x` hız butonları `SetTimeSpeedCommand` üretir ve `CampaignState.Execute()` ile komut defterine işlenir.
   - Zaman ilerletme: `+1 Gün` butonu `AdvanceTimeCommand(1)` tetikler.
   - Lens seçici: 6 lens modu `SetMapLensCommand` ile değiştirilir ve tüm bölgelerin skorları anlık güncellenir.
   - Bölge seçimi: `SelectRegionCommand` ile seçilen bölgenin detay kartı ve telemetri metrikleri dinamik olarak bağlanır.
   - Navigasyon kısayolları: Besteleyici, Atölye, Güneş Sistemi, Ar-Ge, Üretim, Sözleşmeler, Üsler, Hedefler, Ana Menü.

3. **`CampaignMapScreenController` (`Apogee.UI.Screens.CampaignMap`):**
   - Üst HUD: Ajans başlığı, kaynak hapı (yeşil bütçe, altın itibar, mavi Ar-Ge, mor Bilgi), tarih ve zaman kontrol butonları.
   - Lens çubuğu: 6 lens filtresi, aktif lens mavi vurgusu.
   - Harita alanı: Okyanus mavisi kanvas, ekvator çizgisi (0°), 8 bölge pini (enlem/boylama göre yerleşik, skor rozeti, üs simgeleri, tıklanabilir).
   - Bölge inceleme paneli: Sağ sütun; bölge adı, koordinatlar, aktif lens uygunluk çubuğu, rotasyon bonusu, işçilik çarpanı, istikrar, hava/iptal riski, lojistik, kapsama, aktif tesisler ve imar izinleri.
   - Alt araç çubuğu: 9 stratejik ekran navigasyon butonu ve durum satırı.

4. **`ApogeeUIManager` & `CampaignBootstrapper` Entegrasyonu:**
   - `ApogeeUIManager`: `regions.json` yükleme, `CampaignMapScreenController` oluşturma, inicializasyon ve `Navigator.RegisterScreen(ScreenId.CampaignMap)`.
   - `CampaignBootstrapper.BindToUI()`: `CampaignMapController?.ViewModel?.BindCampaign(CurrentCampaign)` eklendi.
   - `TeardownUI()`: dispose ve cleanup sağlandı.

5. **Yerelleştirme:**
   - `locale_tr.json` ve `locale_en.json` dosyalarına `campaignMap.*` 12 anahtarlık set eklendi.

6. **Testler:**
   - 7 yeni saf C# yerel doğrulama testi (`Milestone9Tests.cs`):
     `InitialState`, `SelectRegion_ShowsLensEvaluationAndBases`, `SetMapLens_DispatchesCommandAndUpdatesScores`, `TimeSpeedControls_DispatchesSetTimeSpeedCommand`, `AdvanceDays_AdvancesClock`, `NavigationCommands_DispatchScreens`, `NormalizedCoordinates`.
   - Toplam test sayısı 375 → **382** (%100 geçti, 0 hata).

## Sonuçlar

- `noEngineReferences = true` ilkesi korundu.
- Strateji haritası, HOI4 hissini veren zaman akışı, lens seçimi, üs ve bölge incelemesiyle tam operasyonel hale geldi.
- Ana Menüden "Yeni Kampanya" veya "Kayıttan Devam" akışı doğrudan `ScreenId.CampaignMap`'e geçer.

---

# ADR-052: M9-07 Ayarlar Ekranı (Settings Screen, ScreenId=18)

**Tarih:** 2026-11-08  
**Durum:** Kabul Edildi  
**İlgili:** GDD §13.2, §14.1 (#19 Ayarlar), §15.4, §17.6, §21 M9-07

## Bağlam
GDD §14.1 #19 ve M9-07 uyarınca, oyuncunun ses seviyelerini (Master, Müzik, SFX, UI), dilini (Türkçe/İngilizce), ekran modunu (Tam Ekran/Pencereli) ve eğitim yardım düzeyini (İpuçlarını Göster/Gizle) yapılandırabileceği, ayarların oturumlar arasında kalıcı (persistent) olarak saklanacağı bir Ayarlar ekranına ihtiyaç vardı.

## Alınan Kararlar

1. **Konsolide Ayar Veri Modeli (`GameSettingsData`):**
   - `MasterVolume`, `MusicVolume`, `SfxVolume`, `UiVolume` (0.0 - 1.0 aralığına kenetlenen float değerler).
   - `IsMuted` (bool ses susturma).
   - `Language` (`Language.TR` / `Language.EN`).
   - `IsFullscreen` (bool tam ekran tercihi).
   - `ShowHints` (bool eğitim ve tasarım ipuçları).
   - `ToAudioSettings()` ile `AudioSettingsData` köprüsü.

2. **Kalıcılık Katmanı (`ISettingsStorage` + Implementasyonlar):**
   - `ISettingsStorage` arayüzü: `LoadSettings()`, `SaveSettings()`, `ResetSettings()`.
   - `PlayerPrefsSettingsStorage`: Masaüstü ve WebGL (IndexedDB) uyumlu kalıcı saklama.
   - `InMemorySettingsStorage`: Saf C# testleri ve CI ortamı için yalıtılmış bellek deposu.

3. **`SettingsViewModel` (`Apogee.UI.Screens.Settings`):**
   - Reaktif `ObservableValue` özellikleri: ses seviyeleri, dil, tam ekran, ipuçları, kirlilik (`IsDirty`) ve durum mesajı (`StatusMessage`).
   - Ses seviyeleri veya susturma değiştiğinde `IAudioService` anlık güncellenir (oyuncu anında duyar).
   - Dil değiştiğinde `ILocalizationManager.SetLanguage()` tetiklenir ve tüm etiketler dinamik olarak yeniden çevrilir.
   - `ApplySettingsCommand`: Değişiklikleri depolayıcıya yazar, kirlilik bayrağını temizler ve bildirim üretir.
   - `ResetDefaultsCommand`: Fabrika ayarlarına sıfırlar.
   - `CloseCommand`: Değişiklikleri otomatik uygular ve önceki ekrana (`GoBack()`) veya `ScreenId.MainMenu`'ye döner.

4. **`SettingsScreenController` (`Apogee.UI.Screens.Settings`):**
   - UI Toolkit görünümü: Üst bar (Başlık, alt başlık, Geri butonu).
   - Kaydırılabilir alan içinde 4 tematik kart:
     - **Ses Düzeyleri:** 4 adet kaydırma çubuğu (`Slider`), canlı yüzde rozetleri ve "Tüm Sesleri Kapat" onay kutusu (`Toggle`).
     - **Dil:** Türkçe (TR) ve English (EN) seçim butonları (`.apogee-btn--primary` / `.apogee-btn--secondary`).
     - **Görüntü:** "Tam Ekran Modu" toggle.
     - **Oynanış ve Yardım:** "Eğitim ve Yardım İpuçları" toggle ve açıklama metni.
   - Alt aksiyon çubuğu: "Varsayılana Sıfırla", "Uygula ve Kaydet", yeşil durum etiketi.
   - `LanguageChanged` dinleyicisi ile diller arası geçişte tüm başlıklar ve açıklamalar tek satır kod gerektirmeden güncellenir.

5. **`ApogeeUIManager` Entegrasyonu:**
   - `SettingsScreenController` oluşturulup `_screenContainer`'a eklendi ve `Navigator.RegisterScreen(ScreenId.Settings)` ile kaydedildi.
   - `MainMenuViewModel` ve `CampaignMapViewModel` üzerindeki `OpenSettingsCommand` artık doğrudan `ScreenId.Settings` ekranını açmaktadır.

6. **Yerelleştirme:**
   - `locale_tr.json` ve `locale_en.json` dosyalarına `settings.*` anahtar takımı (18 anahtar × 2 dil) eklendi.

7. **Testler:**
   - `Milestone9Tests.cs` içerisine 8 yeni yerel test eklendi:
     `InitialState_Defaults`, `VolumeAndMute_PropagatesToAudioService`, `Language_PropagatesToLocalizationManager`, `FullscreenAndHints_Toggle`, `ApplySettings_SavesToStorage`, `ResetToDefaults_RestoresValues`, `Navigation_GoBackOrMainMenu`, `Settings_ScreenRegistration_And_Navigation`.
   - Toplam test sayısı 393'ten **401**'e yükseldi (%100 geçti, 0 hata).

## Sonuçlar
- Pure C# ilkesi korundu; WebGL uyumlu ayar kalıcılığı sağlandı.
- M9-07 tamamlandı. Sıradaki görev: M9-08 (Altın Yol Unity Play Mode entegrasyon testi).


# ADR-053: M9-08 Altın Yol Headless Entegrasyon Testi (Golden Path Integration Test)

## Durum
Kabul Edildi

## Bağlam
GDD §21 M9-08 gereğince oyunun tam demo akışının (MainMenu → CampaignMap → MissionComposer → Workshop → DesignReview → LaunchSequence → Operations/EDL/Science → MissionReport → WhatIf → Attribution → Settings → MainMenu) uçtan uca doğrulanması gerekiyordu. Unity Play Mode IEnumerator testi Engine bağımlılığı getirdiğinden, headless dotnet runner ile de doğrulanabilir bir entegrasyon testi eklenmesine karar verildi.

## Alınan Kararlar
1. **Headless Golden Path Testi (`Test_M9_08_GoldenPath_FullDemoLoop_Integration`):**
   - `Docs/TestRunner/Milestone9Tests.cs` içine eklendi; `dotnet run` ile Unity bağımlılığı olmadan çalışır.
   - Tüm 10 ekran `DummyScreen` ile `ScreenNavigator`'a kaydedildi.
   - `LunarRoverVerticalSlice` yardımcı sınıfı ile passing blueprint/profile/validationContext oluşturuldu — katalog yükleme karmaşıklığı atlandı.
   - `LaunchSimulationParameters`: `weatherRisk=0.0`, `reliabilities=[1.0, 1.0]` deterministik başarı garantisi için (seed=42, random roll ≠ weather scrub).
   - `ReviewNavigationArgs(blueprint, context)` 2-arg constructor kullanıldı; `ReviewViewModel(nav, blueprint, context)` 3-arg constructor ile validation otomatik çalıştırıldı.
   - `AdvanceCruiseCommand(campaign, missionId)` 2-arg; `MissionStatus.Operating` doğru enum değeri.
   - `CompleteMissionCommand(campaign, missionId)` 2-arg; `WhatIfSimulator.Simulate(...)` → `.WhatIfReport` (counterfactual).

2. **SampleScene.unity Sahne Kablolaması:**
   - UIDocument GameObject aktif edildi (`m_IsActive: 1`).
   - `CampaignBootstrapper` ve `UnityAudioService` MonoBehaviour bileşenleri eklendi.
   - `_initialScreen` = 1 (MainMenu) olarak ayarlandı.

3. **Unity PlayMode Testi (`GoldenPathIntegrationTests.cs`):**
   - `Assets/_Project/Tests/PlayMode/` altında IEnumerator Play Mode testi oluşturuldu.
   - `Apogee.Tests.PlayMode.asmdef` referanslarına Apogee.Design, Apogee.Strategy, Apogee.Data, Apogee.UI eklendi.


# ADR-054: Atölye Hangar Deneyimi ve 2D Vektörel Kesit Yeniden Tasarımı (Interactive Rocket Hangar Overhaul)

## Durum
Kabul Edildi

## Bağlam
Mevcut Tasarım Atölyesi ekranı CAD tarzı kutu yığınlarından oluşuyor, aşama genişlikleri yapay alt sınırlarla kırpıldığı için roket silueti vermiyor ve payload fairing'in dışında ayrı kutular olarak duruyordu. Ayrıca slot listesi ile kesit çizimi birbirinden kopuktu; Stage3, Instrument2 ve insanlı/EDL sistemleri arasında eksiklikler vardı. Oyuncuya canlı bir roket atölyesi (KSP VAB / Hangar) hissi vermek, parçaları ait oldukları roket bölümlerinde canlı düzenlenebilir kılmak ve sade bir HUD sunmak amacıyla komple bir yeniden tasarım kararlaştırıldı.

## Alınan Kararlar
1. **2D Yüksek Detaylı Vektör İllüstrasyon (Painter2D):**
   - 3D RenderTexture mimari karmaşası yerine, WebGL uyumlu ve donanım hızlandırmalı UI Toolkit `Painter2D` vektör çizim pipeline'ı seçildi (`generateVisualContent`).
   - Roket silueti merkez eksende gerçek fiziksel oranlarla (`PropellantMass`, `Thrust`, `DryMass`) çizilir; sabit piksel kırpması kaldırıldı.
2. **4 Katman Modu (Exterior, Cutaway, Subsystems, Schematic):**
   - **Dış Görünüm (Exterior):** Boyalı gövde, kapalı fairing, kanatçıklar ve tedarikçi şeritleri.
   - **Kesit (Cutaway):** Yarı şeffaf gövde, sıvı yakıt seviyesi dolumu ve menisküs çizgisi, motor yanma odaları, açık fairing bölmesinde faydalı yük modülleri.
   - **İç Sistemler (Subsystems):** Güç (altın sarısı), veri (cyan/mavi), yakıt ve ısı boru hatları.
   - **Şematik (Schematic):** İnce tel kafes blueprint çizgileri ve CAD ızgarası.
   - Tüm katmanlarda aynı hit-zone koordinatları geçerlidir; tıklama her modda çalışır.
3. **Saf C# Anatomi ve Profil Modeli (`RocketVisualAnatomy` & `PartVisualProfile`):**
   - `Apogee.Design` katmanında `noEngineReferences=true` korunarak uygulandı.
   - Tüm 17 standart `VehicleSlotId` kapsandı; Stage 3, Instrument 2 ve insanlı sistemler eksiksiz haritalandı.
   - Parça şekil arketipleri (NozzleVisualType, SolarArrayVisualType, AntennaVisualType, EdlVisualType, TankShapeVisualType) tanımlandı.
4. **Section-Tabanlı Etkileşim:**
   - Roket bölümlerine tıklanınca kamera o bölgeye yumuşak yaklaşır (pan+zoom), sağdan kayan `HangarPartDrawer` açılır.
   - Kart hover'ında hayalet delta ve roket üzerinde yeşil kesikli hayalet silüet belirir.
   - Karta tıklanınca parça `SetSlotComponentCommand` ile takılır (CommandBus / Undo/Redo destekli).
   - Boş yuvalar kesikli soket ve '+' ikonuyla, zorunluysa amber uyarıyla gösterilir.
   - Sol minyatür bölüm rayı (`HangarSectionRail`) ile roket durumu ve iki yönlü seçim senkronize edilir.
5. **Büyük İkonlu HUD & Mühendis Görünümü (Engineering View):**
   - Alt HUD: Menzil (hedef işareti + hayalet çubuğu), Kütle, Maliyet, Güvenilirlik, Güç ve Süre göstergeleri ile sağ altta "Tasarım İncelemesi →" CTA'sı.
   - Eski ayrıntılı telemetri ve slot listesi kaybolmadı; varsayılan gizli olan "Mühendis Görünümü" paneline (`E` kısayolu) taşındı.
6. **Klavye ve Ses:**
   - Kısayollar: `1-9` slot seçimi, `Tab` katman değiştirme, `Esc` kapatma/kamerayı sıfırlama, `Ctrl+Z/Y` undo/redo, `E` mühendis görünümü.
   - Mevcut `IAudioService` sesleri (`ButtonClick`, `ButtonHover`, `TabSwitch`, `ModalOpen`, `ModalClose`) bağlandı.
7. **GDD §14.3 & §15.1 Evrimi:**
   - GDD §15.1 "sakin HOI4 tonu"ndan oyuncu onayıyla daha canlı, tepkisel ve etkileşimli "Hangar" oyun deneyimine geçiş belgelendi.


## Sonuçlar
- Toplam test sayısı 401'den **402**'ye yükseldi (%100 geçti, 0 hata).
- M9-08 tamamlandı. Milestone 9 (Oynanabilir Kampanya Döngüsü) tüm görevleri tamamlandı.


---

# ADR-055: Uçuş Tiyatrosu — Aşama 1: Çekirdek Veri Modeli

**Tarih:** 2026-11-12  
**Durum:** Kabul Edildi  
**İlgili:** GDD §3.4, §3.5, §3.6, §10.1, §15.1

Bkz. tam karar belgesi: `Docs/DECISIONS/ADR-055-Flight-Theatre-Phase1.md`

**Özet kararlar:**
- `FlightBeat` / `PlaybackClock` / `FlightTimeline` / `FlightTimelineLimits` / `FlightChoreography` / `FlightCaptionMode` — engine-free Pure C# (Apogee.UI.Core.Flight)
- 4 factory: `FromLaunch`, `FromCruise`, `FromEdl`, `FromScience` — deterministik, hata toleranslı
- `GameSettingsData` + `SettingsViewModel`: `CaptionMode` / `ReduceMotion` / `FontScale` eklendi (geri uyumlu)
- `locale_tr/en.json`: 388 → 496 anahtar (tüm LAUNCH/CRUISE/EDL/SCI grupları)
- `Variables.uss`: `--flight-*` token grubu eklendi
- `LaunchNavigationArgs`: `DestinationId` + `DefaultFallbackSeed` eklendi
- `flight_choreography.json` + `flight_visuals.json` veri dosyaları oluşturuldu


---

# ADR-056: Uçuş Tiyatrosu — Aşama 2: Sahne ve Araç

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi

Bkz. tam karar belgesi: `Docs/DECISIONS/ADR-056-Flight-Theatre-Phase2.md`

**Özet:**
- `VehicleSilhouetteView`: Painter2D, 3 seviye (Launch/Cruise/Landing), 3 PayloadType, ≤3Hz exhaust, accent rengi
- `FlightStageView`: Tek Painter2D geçişi, 4 ScenePhase (Pad/Ascent/Space/Landing), ≤30Hz schedule, ParticlePool entegrasyonu, VehicleSilhouetteView kompozisyon, ReduceMotion desteği
- `ParticlePool`: 20 slot pre-alloc, 5 Kind, round-robin emit, sıfır heap alloc, Painter2D draw
- `FlightCamera`: USS transform animasyonu, 14 preset, lerp hızı, ReduceMotion snap


---

# ADR-057: Uçuş Tiyatrosu — Aşama 3: LaunchSequenceScreen Kablolaması

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi

Bkz. tam karar belgesi: `Docs/DECISIONS/ADR-057-Flight-Theatre-Phase3.md`

**Özet:**
- `PhaseRailView`: Uçuş aşamaları dikey rayı, renk durumları, tıklayarak beate atlama (SeekTo).
- `FlightHudView`: Görev başlığı, T+ MET sayacı, irtifa, hız ve sonuç rozeti telemetri HUD'ı.
- `FlightCaptionView`: 3 modlu altyazı katmanı (Kapalı / Altyazı / Tam Günlük), dinamik yazı ölçekleme.
- `LaunchSequenceScreenController`: Sahne, ray, HUD, altyazı ve oynatma kontrolleri (▶⏸, 1×/2×/4×, Adım, Atla) orkestrasyonu; ses olayları tetikleyicisi.


---

# ADR-058: Uçuş Tiyatrosu — Aşama 4: OperationsScreen Kablolaması

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi

Bkz. tam karar belgesi: `Docs/DECISIONS/ADR-058-Flight-Theatre-Phase4.md`

**Özet:**
- `OperationsScreenController`: Seyir (Cruise), İniş (EDL), Bilim (Science) döngülerini Flight Theatre sahnesine bağlayan tam ekran sinematik arayüz.
- `FlightHudView` çoklu aşama desteği: Seyir ilerlemesi, EDL irtifa/ısı kalkanı ve Bilim veri downlink telemetrileri.
- `DecisionCardModal`: `RequiresDecision` beatlerinde veya M5-05 operasyon olaylarında otomatik duraklayan, Apogee kehribar vurgulu yüzen karar kartı.
- Kesintisiz oynatma motoru (`PlaybackClock`) ve aşama eylemleri (Seyri İlerlet, EDL Başlat, Bilim Günü İlerlet, Görevi Tamamla).
- Ses efektleri (`RetroThrust`, `AtmosphericEntryRumble`, `TelemetryPing`, `LandingSuccess`) ve kullanıcı ayarları senkronizasyonu.


---

# ADR-059: Uçuş Tiyatrosu — Aşama 5: Kesintisiz Seyir Devri, Otomatik Seyir ve Etkileşimli Kontroller

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi

Bkz. tam karar belgesi: `Docs/DECISIONS/ADR-059-Flight-Theatre-Phase5.md`

**Özet:**
- `OperationsNavigationArgs.FromLaunchHandover`: Fırlatma sonrası operasyona kesintisiz geçiş; Trans-Injection Burn (`Cruise.TransferInjection`) sinematiği.
- `_autoCruiseBtn` (Otomatik Seyir): Karar veya varış noktasına kadar segment komutlarını otomatik ilerleten ve oynatan mod.
- `_rewatchBtn` (Yeniden İzle): Hem Launch hem Operations ekranlarında deterministik tohumlu replay fonksiyonu.
- Klavye Kısayolları: `Space` (oynat/duraklat), `T` (altyazı modu), `1/2/4` (hız), `RightArrow/N` (adım), `End` (atla), `R` (yeniden izle).
- `FlightCaptionView.OnBeatSelected`: Tam Günlük (FullLog) panelinde geçmiş olay kartına tıklayarak zaman çizelgesinde o beate sarma.
- `FlightStageView`: Seyir ilerledikçe Dünya küçülür, hedef gövde büyür, varışta parlayan yörünge yakalama halkası (`DrawArrivalCaptureRing`) belirir; `fx_transfer_burn` ve `fx_correction_burn` partikülleri.
- TR/EN yerelleştirme: `CRUISE_TRANSFER_INJECTION_*` ve `CRUISE_ARRIVAL_*` anahtarları eklendi.


---

# ADR-060: Uçuş Tiyatrosu — Aşama 6: EDL ve Yüzey Bilim Animasyonu

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi

Bkz. tam karar belgesi: `Docs/DECISIONS/ADR-060-Flight-Theatre-Phase6.md`

**Özet:**
- `FlightCamera`: `FlightStageView`'a tam bağlandı; `ApplyBeat` ile koreografi kamera presetleri lerp/snap ile aracı takip ediyor.
- İrtifa İniş Konumlandırması: EDL sırasında irtifaya bağlı olarak araç gökyüzünden yüzey seviyesine yumuşakça indirilir.
- EDL Görselleri (Painter2D): Sesüstü paraşüt kubbesi ve ipleri (`DrawParachute`), ablatif ısı kalkanı plazma pruva şoku (`DrawHeatShockwave`), çift retro iniş nozulu alevleri (`DrawRetroPlume`), yüzey temas halkası (`DrawTouchdownShockRing`) ve kaza/çarpma alanı (`DrawCrashSite`).
- Gezegen Yüzey Detayları: Ay kraterleri, Mars kum tepesi ve kaya çıkıntıları, Venüs/Europa çatlak ve yarıkları (`DrawPlanetSpecificTerrain`).
- Yüzey Bilim Operasyonu Görselleri: Rover 3 mafsallı robotik numune kolu, parlak neon camgöbeği lazer tarama konisi ve analiz noktası (`DrawScienceScanBeam`), antenden uzaya yükselen konsantrik downlink radyo dalgaları (`DrawDownlinkWaves`) ve yüzey tekerlek izleri (`DrawSurfaceTracks`).
- Partikül Havuzu: Tüm EDL ve Science efekt anahtarları (`fx_entry_heating`, `fx_chute_deploy`, `fx_retro_ignite`, `fx_touchdown_dust`, `fx_edl_fail`, `fx_science_data_collect`, `fx_science_downlink`) sıfır-alloc havuzuna bağlandı.

---

# ADR-061: Uçuş Tiyatrosu — Aşama 7: Ses Kablolama, ReduceMotion ve WebGL Denetimi

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi

Bkz. tam karar belgesi: `Docs/DECISIONS/ADR-061-Flight-Theatre-Phase7.md`

**Özet:**
- `FlightAudioDirector` (saf C#): Beat-güdümlü SFX dağıtımı ve faz-güdümlü ambient müzik geçişi (Pad/Ascent→`MusicLaunchTension`, Space→`MusicCruiseAmbient`, Landing→`MusicEdlTension`); terminal başarı/başarısızlık müzikal imzası; ReduceMotion açıkken anlık (crossfade'siz) müzik geçişi; `NullAudioService` ile sessiz çalışabilir.
- `FlightPerformanceBudget` (saf C#): 1 sn pencerede ≤30 Hz repaint ve ≤4/s ses tetikleme bütçesi izleme, aşım bayrakları ve `GetSummary()` geliştirici özeti.
- `AudioEvent` genişletildi: `ScienceSuccess/Fail`, `CruiseNominalPing`, `TransferInjectionBurn`, `ArrivalCaptureRing` (40-44) ve `MusicCruiseAmbient/EdlTension/FailureStinger` müzik anahatları; `UnityAudioService`'e 5 yeni Kenney CC0 ses eşlemesi.
- `LaunchSequenceScreenController` ve `OperationsScreenController`: `EnsureAudioDirector()` kablolaması, beat dağıtımının yönetmene taşınması, `OnHidden()` müzik durdurma, footer'da ~1 Hz güncellenen canlı bütçe etiketi.
- `FlightStageView.SetPerformanceBudget()`: Her repaint tick'i bütçeye kaydedilir.
- WebGL denetimi `Docs/WEBGL_OPTIMIZATION.md` §6'ya işlendi (≤30 Hz repaint, sıfır per-frame heap alloc, 256 MB heap — ✅ uyumlu).
