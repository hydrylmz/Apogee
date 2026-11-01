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
