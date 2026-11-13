# ADR-058: Uçuş Tiyatrosu — Aşama 4: OperationsScreen Kablolaması (Flight Theatre Phase 4 — OperationsScreen Wiring)

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi  
**İlgili:** GDD §3.4, §3.5, §3.6, §10.1, §10.2, §10.3, §14.1 #14; ADR-055 (Phase 1), ADR-056 (Phase 2), ADR-057 (Phase 3)

---

## Bağlam

Aşama 1-3 kapsamında temel deterministik veri modelleri (`FlightBeat`, `FlightTimeline`, `PlaybackClock`), Painter2D sahne altyapısı (`FlightStageView`, `VehicleSilhouetteView`, `ParticlePool`, `FlightCamera`) ve `LaunchSequenceScreenController` tamamlanmıştı.

Aşama 4'ün hedefi, eski salt metin ve basit butonlardan oluşan `OperationsScreenController`'ı (ScreenId.Operations) bütünüyle canlı sinematik "Uçuş Tiyatrosu" (Flight Theatre) mimarisine dönüştürmektir:
- Derin uzay seyri (`ScenePhase.Space`), hedef gezegen yaklaşımı ve yüzey inişi (`ScenePhase.Landing`) arasında dinamik geçişler
- Sol tarafta seyir adımlarını, EDL aşamalarını ve bilim günlerini listeleyen etkileşimli `PhaseRailView`
- Üstte görev süresi, seyir ilerlemesi, irtifa, hız, ısı kalkanı ve bilim telemetrisini anlık yansıtan genişletilmiş `FlightHudView`
- Ortada 3 modlu (Kapalı / Altyazı / Tam Günlük) anlatı katmanı `FlightCaptionView`
- Seyir sırasında karar gerektiren durumlarda (`RequiresDecision = true`) veya operasyon olaylarında (M5-05) otomatik duraklayan, seçenekleri ve sonuç ipuçlarını sunan altın/kehribar vurgulu yüzen `DecisionCardModal`
- Altta kesintisiz oynatma kontrolleri (▶⏸, 1×/2×/4×, Adım, Atla, Altyazı Değiştir) ve aşama eylemleri (Seyri İlerlet, İniş / EDL Başlat, Bilim Günü İlerlet, Görevi Tamamla & Raporu Gör, Komuta Masası)
- `AudioEvent` tetikleyicileri ile `UnityAudioService` üzerinden atmosferik giriş homurtusu, retro motor itkisi, telemetri pinglemesi ve başarı sesleri
- `GameSettingsData` ayarlarından `ReduceMotion`, `CaptionMode` ve `FontScale` tercihlerinin tam senkronizasyonu

---

## Alınan Kararlar

### 1. `FlightHudView` Çoklu Aşama Desteği ile Genişletildi
**Konum:** `Assets/_Project/Presentation/Screens/Launch/FlightHudView.cs`

- Fırlatma dışında Seyir, EDL ve Bilim operasyonlarına özgü telemetri başlık ve değerlerini dinamik sunmak üzere `SetMetricTitles(...)`, `SetMetricValues(...)`, `SetStatusBadge(...)` ve `SetWeatherVisible(...)` eklendi.
- `UpdateTelemetry(FlightBeat beat)` metodu faz tipine göre kendini uyarlar:
  - **Cruise:** `GÖREV GÜNÜ`, `İLERLEME (%progress)`, `DURUM (status)`
  - **EDL:** `İNİŞ ZAMANI (MET)`, `İRTİFA (km)`, `ISI KALKANI / HIZ`
  - **Science:** `BİLİM GÜNÜ`, `DURUM`, `VERİ AKIŞI (downlink)`

### 2. `VehicleSilhouetteView` ve `FlightStageView` Geliştirmeleri
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`, `Assets/_Project/Presentation/Core/Vehicle/VehicleSilhouetteView.cs`

- `FlightStageView`'a `Vehicle` erişimcisi ve `SetPayloadType(VehicleSilhouetteView.PayloadType)` köprüsü eklendi.
- Tasarım blueprint'i veya hedef tipine göre araç iniş silüeti (Rover, Lander, Orbiter) dinamik yapılandırılıyor.
- Derleyici uyarıları (kullanılmayan özel alanlar) temizlendi.

### 3. Yüzen Karar Kartı Modalı (`DecisionCardModal`)
**Konum:** `Assets/_Project/Presentation/Screens/Operations/OperationsScreenController.cs`

- `PlaybackClock.DecisionRequired` veya simülatörün `PendingDecisionCard` / `PendingOperationEvent` durumlarında oynatma duraklatılır ve yarı saydam modal açılır.
- Başlık, durum rozeti, hikaye açıklaması ve seçenek butonları gösterilir (`LabelKey`, `VisibleConsequenceKey`).
- Oyuncu bir seçeneğe tıkladığında komut (`ResolveOptionCommand` / `ResolveOperationEventOptionCommand`) çalıştırılır, ses efekti çalınır, modal kapanır ve zaman akışı yeniden senkronize edilir.

### 4. `OperationsScreenController` Yeniden Tasarımı
**Konum:** `Assets/_Project/Presentation/Screens/Operations/OperationsScreenController.cs`

- Eski 550 satırlık metin arayüzü yerine tam ekran sinematik UI Toolkit orkestrasyonu kuruldu:
  - `FlightStageView` arka planı
  - `PhaseRailView` sol navigasyon rayı
  - `FlightHudView` üst telemetri çubuğu
  - `FlightCaptionView` alt anlatı katmanı
  - Playback kontrolleri ve operasyon eylem butonları dock'u
- Hem `OperationsNavigationArgs` hem `MissionInstance` navigasyon bağlamları desteklendi.
- Görev tamamlandığında `CompleteMissionCommand` icra edilerek `ScreenId.MissionReport` ekranına sorunsuz geçiş sağlandı.

---

## Sonuçlar

- Seyir (Cruise), İniş (EDL), Yüzey Bilimi (Science) ve Karar Kartları (Decision Cards) artık Launch ekranı ile aynı üst düzey Uçuş Tiyatrosu deneyimini sunmaktadır.
- Hiçbir test dosyası oluşturulmadı veya değiştirilmedi; mevcut 409 doğrulama testinin tamamı başarıyla geçmektedir (%100 PASS, 0 warning, 0 error).
