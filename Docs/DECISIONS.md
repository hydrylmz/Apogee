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
