# ADR-057: Uçuş Tiyatrosu — Aşama 3: LaunchSequenceScreen Kablolaması (Flight Theatre Phase 3 — LaunchSequenceScreen Wiring)

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi  
**İlgili:** GDD §3.4, §3.5, §3.6, §10.1, §14.1 #13; ADR-055 (Phase 1), ADR-056 (Phase 2)

---

## Bağlam

Aşama 1'de deterministik uçuş veri modeli (`FlightBeat`, `FlightTimeline`, `PlaybackClock`, `FlightChoreography`) ve Aşama 2'de Painter2D sahne altyapısı (`FlightStageView`, `VehicleSilhouetteView`, `ParticlePool`, `FlightCamera`) geliştirilmişti.

Aşama 3'ün hedefi, eski salt metin listesi sunan `LaunchSequenceScreenController`'ı tamamen yenileyerek canlı sinematik "Uçuş Tiyatrosu" (Flight Theatre) sunumuna dönüştürmek ve tüm bu bileşenleri uyum içinde bağlamaktır:
- Sol tarafta aşama düğümlerini ve durumlarını gösteren `PhaseRailView`
- Üstte gerçek zamanlı görev süresi (MET) ve telemetri sunan `FlightHudView`
- Ortada 3 modlu (Kapalı / Altyazı / Günlük) anlatı katmanı `FlightCaptionView`
- Altta oynatma kontrolleri (>, 1×/2×/4×, Adım, Atla, Altyazı Değiştir, Devam)
- Ses efektlerinin `AudioEvent` tetikleyicileri ile `UnityAudioService` üzerinden çalınması
- Kullanıcı ayarlarından (`GameSettingsData`) `CaptionMode`, `ReduceMotion` ve `FontScale` tercihlerinin okunması ve uygulanması

---

## Alınan Kararlar

### 1. `PhaseRailView` — Aşama Rayı Görünümü
**Konum:** `Assets/_Project/Presentation/Screens/Launch/PhaseRailView.cs`

- Ekranın sol kenarında dikey `ScrollView` içinde beat/aşama listesi sunar.
- Her düğüm için durum renk kodlaması:
  - **Tamamlanan (Completed):** Koyu yeşil zemin, yeşil durum indikatörü (`#10B981`)
  - **Aktif (Active):** Mavi/camgöbeği vurgu (`#06B6D4`), kalın beyaz yazı tipi
  - **Bekleyen (Pending):** Sönük gri/mavi zemin
  - **Hata/Arıza (Error):** Kırmızı indikatör ve vurgu (`#EF4444`)
- Düğümlere tıklandığında `OnBeatSelected` olayı tetiklenerek `PlaybackClock.SeekTo(index)` çağrılır.
- Aktif aşama değiştikçe otomatik olarak ilgili düğüme kaydırma (`ScrollTo`) yapılır.

### 2. `FlightHudView` — Görev Başlığı ve Telemetri HUD
**Konum:** `Assets/_Project/Presentation/Screens/Launch/FlightHudView.cs`

- Üst kısımda yarı saydam (`rgba(7, 11, 20, 0.88)`) HUD çubuğu olarak konumlanır.
- Sol üstte Görev Adı ve Taşıyıcı Araç adı, sağ üstte Hava Durumu özeti ve Sonuç Rozeti (`HAZIR`, `SUCCESS`, `PARTIALORBIT`, `CRITICALFAILURE` vb.) yer alır.
- Alt çubuğunda 3 kritik telemetri kutusu:
  - **Görev Zamanı (MET):** `T+MM:SS` formatında formatlanmış kronometre
  - **İrtifa (Altitude):** `km` cinsinden anlık değer
  - **Hız (Velocity):** `m/s` cinsinden anlık değer

### 3. `FlightCaptionView` — Anlatı ve Altyazı Katmanı
**Konum:** `Assets/_Project/Presentation/Screens/Launch/FlightCaptionView.cs`

- GDD §3.5 uyarınca 3 mod (`FlightCaptionMode`) desteği:
  - `Off`: Katman tamamen gizlenir (`display = None`).
  - `Subtitle`: Ekranın alt-orta kısmında şık, yarı saydam kart üzerinde başlık ve açıklama sunulur.
  - `FullLog`: Geçmiş tüm olayların listelendiği kaydırılabilir telemetri günlüğü paneli açılır.
- Dinamik yazı ölçekleme (`SetFontScale(scale)`) ile erişilebilirlik gereksinimini karşılar.
- Durum rengine duyarlı sol kenarlık vurgusu (Nominal: Cyan, Arıza: Kırmızı).

### 4. `LaunchSequenceScreenController` — Orkestrasyon ve Kablolama
**Konum:** `Assets/_Project/Presentation/Screens/Launch/LaunchSequenceScreenController.cs`

- `ScreenControllerBase<LaunchSequenceViewModel>`'den türeyerek UI Toolkit hiyerarşisini kurar:
  - Arka planda tam ekran `FlightStageView`
  - Ön katmanda `PhaseRailView`, `FlightHudView`, `FlightCaptionView` ve oynatma kontrolleri
- `OnShown(context)` çalıştığında:
  1. `PlayerPrefsSettingsStorage` üzerinden oyuncu ayarlarını (`ReduceMotion`, `CaptionMode`, `FontScale`) okur ve ilgili görünümlere iletir.
  2. `LaunchSimulator.Simulate()` sonucundan `FlightTimeline.FromLaunch()` ile beat dizisini üretir.
  3. `FlightChoreography.Instance.ApplyOverride()` ile JSON tabanlı koreografi özelleştirmelerini beatlere uygular.
  4. Hedef kimliğine (`destinationId`) göre gökyüzü/gezegen renk profilini (`ApplyDestinationProfile`) sahneye aktarır.
  5. `PlaybackClock.Load()` ve `.Play()` ile oynatmayı başlatır.
  6. 60 Hz tick zamanlayıcısı ile `PlaybackClock.Tick(Time.deltaTime)` çağrısını sürdürür.
- Oynatma sırasında:
  - `BeatAdvanced` olayında `FlightStageView.ApplyBeat()`, `_phaseRail.SetCurrentIndex()`, `_hudView.UpdateTelemetry()` ve `_captionView.DisplayBeat()` güncellenir.
  - Beat içindeki `AudioEventId` (`RocketIgnition`, `StageSeparation`, `OrbitInsertion`, `AbortAlarm` vb.) doğrudan `UnityAudioService` üzerinden çalınır.
  - Hız butonuna tıklandığında `1×` -> `2×` -> `4×` döngüsü çalışır.
  - Adım (`StepNext`) ve Atla (`SkipToEnd`) butonları mevcut ViewModel komutlarıyla senkronize çalışır.
  - Oynatma bittiğinde (`OnClockSequenceFinished`) "Operasyona Geç (Operations) >"butonu aktif hale gelir.

---

## Sonuçlar

- Launch sequence ekranı artık statik liste yerine tam teşekküllü interaktif sinematik bir uçuş deneyimi sunuyor.
- Yeni bileşenler Presentation katmanına entegre edildi; hiçbir harici test dosyası oluşturulmadı.
- Mevcut `LaunchSequenceViewModel` ve navigasyon sözleşmeleri (`LaunchNavigationArgs`, `OperationsNavigationArgs`) ile %100 uyumluluk korundu.
