# ADR-061: Uçuş Tiyatrosu — Aşama 7: Ses Kablolama, ReduceMotion ve WebGL Denetimi (Flight Theatre Phase 7 — Audio Wiring, ReduceMotion & WebGL Audit)

**Tarih:** 2026-11-13
**Durum:** Kabul Edildi
**İlgili:** GDD §3.4, §3.5, §15.4, §17.3, §17.4; ADR-047 (Ses Sistemi), ADR-048 (WebGL), ADR-055 (Phase 1, "Ses kablolama → Aşama 7" kapsam kararı), ADR-056, ADR-057, ADR-058, ADR-059, ADR-060

---

## Bağlam

Aşama 1-6'nın sonunda Uçuş Tiyatrosu görsel olarak tamamlanmıştı; ancak ses katmanı
ekranda parça parça duruyordu: her `ScreenController` beat içindeki `AudioEventId`
değerini doğrudan `UnityAudioService.PlaySfx()`'e pompalıyordu. Sahne fazı değişiminde
ambient müzik geçişi yoktu, terminal sonuçta başarı/başarısızlık müzikal imzası
tetiklenmiyordu ve repaint/ses yükünün WebGL bütçesine (≤30 Hz) uyup uymadığını
gösteren canlı bir ölçüm mekanizması bulunmuyordu. ReduceMotion tercihi yalnızca
görsel bileşenlere uygulanıyor, ses geçişlerine yansımıyordu.

GDD §3.4 (sinematik sunum), §15.4 (ses olay mimarisi) ve §17.3/§17.4 (WebGL
performans bütçesi) gereksinimleri uyarınca Aşama 7'de merkezi ses yönetimi,
performans bütçesi izleme ve WebGL denetimi hayata geçirilmiştir.

---

## Alınan Kararlar

### 1. `AudioEvent` Genişletmesi (Uçuş Tiyatrosu Ses Sözlüğü)
**Konum:** `Assets/_Project/Core/Audio/AudioEvent.cs`

- Yeni **Bilim / Uçuş Sonuçları** grubu (40-44): `ScienceSuccess`, `ScienceFail`,
  `CruiseNominalPing`, `TransferInjectionBurn`, `ArrivalCaptureRing` — Aşama 5'in
  Trans-Injection Burn ve varış halkası sahneleri için sessizdi.
- Yeni **Ambient Müzik Anahatları**: `MusicCruiseAmbient` (54), `MusicEdlTension` (55),
  `MusicFailureStinger` (57); `MusicVictoryTheme` 54 → 56'ya taşındı (müzik bloğu 50-59
  içinde tutarlı yerleşim).
- `MusicMainMenu/StrategyAmbient/WorkshopCad` değerleri değişmedi; mevcut
  kayıtlar/Inspector eşlemeleri bozulmaz.

### 2. `FlightAudioDirector` — Merkezi Ses Yönetmeni (Pure C#)
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightAudioDirector.cs`

- Beat-güdümlü tek giriş noktası: `OnBeatStarted(FlightBeat)` → beat'in
  `AudioEventId` değerini `IAudioService.PlaySfx()`'e dağıtır.
- **Faz-güdümlü müzik geçişi:** `PhaseId` öneki `ScenePhase`'e çözümlenir; faz
  değiştiğinde ambient müzik anahatı çapraz geçiş yapılır:
  `Pad/Ascent → MusicLaunchTension`, `Space → MusicCruiseAmbient`,
  `Landing → MusicEdlTension`.
- `ResolvePhase` `LaunchPhaseType` enum adlarını ("Ignition", "MaxQ", "Stage1Burn",
  "FairingJettison" vb.) ve `Launch.Terminal / EDL.Terminal` son beat'lerini
  tanıyacak şekilde yazıldı (`FlightTimeline`'ın gerçek `PhaseId` üretimiyle uyumlu).
- **Terminal müzikal imza:** `OnSequenceFinished(success)` → `MissionSuccessJingle`
  veya `MissionFailureJingle`.
- **ReduceMotion duyarlılığı:** ReduceMotion açıkken müzik crossfade yerine anlık
  geçiş yapılır (`FadeInstant`); SFX engellenmez (ses, görsel animasyon denetiminden
  bağımsızdır).
- `IAudioService` enjeksiyonla çalışır; Unity bulunamazsa `NullAudioService` ile
  sessiz çalışır. Beat başına sıfır heap alloc.

### 3. `FlightPerformanceBudget` — Canlı Performans Bütçesi (Pure C#)
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightPerformanceBudget.cs`

- 1 saniyelik kayan pencere: `LastRepaintsPerSecond` (hedef ≤30), `LastAudioTriggersPerSecond`
  (hedef ≤4) ve aşım bayrakları (`IsRepaintBudgetExceeded` / `IsAudioBudgetExceeded`).
- `RecordRepaint()` her `FlightStageView.OnAnimationTick` çağrısında; `RecordAudioTrigger()`
  her `FlightAudioDirector.OnBeatStarted` çağrısında artırılır.
- `GetSummary()` tek satırlık geliştirici özeti üretir (`Repaint: 30/s ✓ | Audio: 1/s ✓`).
- Sıfır heap alloc; `Tick(dt)` ekran kontrolcüsünün mevcut 16 ms tick'inden beslenir.

### 4. `FlightStageView` — Repaint Sayacı Bağlantısı
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`

- `SetPerformanceBudget(FlightPerformanceBudget)` eklendi; `OnAnimationTick` her
  çalıştığında `_budget?.RecordRepaint()` çağrılır. Bütçe isteğe bağlıdır (null →
  izleme kapalı), böylece bileşen test ortamlarında bağımsız kalır.

### 5. `LaunchSequenceScreenController` Kablolaması
**Konum:** `Assets/_Project/Presentation/Screens/Launch/LaunchSequenceScreenController.cs`

- `EnsureAudioDirector(reduceMotion)`: `UnityAudioService` sahnede bulunur
  (`FindObjectOfType`), bulunamazsa `NullAudioService` ile yönetmen oluşturulur;
  mevcut yönetmen varsa yalnızca `SetReduceMotion` senkronize edilir.
- `OnBeatAdvanced` → artık doğrudan `_audioDirector.OnBeatStarted(beat)` (eski
  ham `TriggerAudio` çağrısı kaldırıldı); buton geri bildirimleri (`Yeniden İzle`)
  ham `TriggerAudio` ile kalır.
- `OnClockSequenceFinished` → `LaunchOutcome.Success/PartialOrbit` bazlı terminal
  müzik imzası.
- `OnHidden` → `_audioDirector.StopMusic()` (ekran arkasında müzik sızması yok).
- Footer'a ~1 Hz güncellenen `FlightPerformanceBudget.GetSummary()` geliştirici
  etiketi eklendi; bütçe penceresi 60 Hz clock tick'inde beslenir.

### 6. `OperationsScreenController` Kablolaması
**Konum:** `Assets/_Project/Presentation/Screens/Operations/OperationsScreenController.cs`

- Launch ile aynı desen: `EnsureAudioDirector`, beat-güdümlü dağıtım, `OnHidden`
  müzik durdurma, footer bütçe etiketi, `_budget.Tick(dt)` 60 Hz tick'inde.
- Terminal sonuç etiketi (`OutcomeTag`) `Fail/Crash/Loss/Abort/Burn` içermezse
  başarı imzası tetiklenir (Seyir, EDL ve Bilim döngüleri için ortak).

### 7. `UnityAudioService` — Yeni Olay Ses Eşlemeleri
**Konum:** `Assets/_Project/Presentation/Audio/UnityAudioService.cs`

- `s_eventToClipName` tablosu genişletildi (tümü Kenney CC0, `Resources/Audio/UI/`):
  `ScienceSuccess → maximize_006`, `ScienceFail → glitch_001`,
  `CruiseNominalPing → tick_004`, `TransferInjectionBurn → scratch_005`,
  `ArrivalCaptureRing → glass_004`. Müzik anahatları (mevcut desenle) Inspector
  eşlemesine bırakılır; eşleme yoksa sessizce oynar.

---

## Sonuçlar

- Uçuş Tiyatrosu ses katmanı tek bir yönetmenden (Director) beslenir: faz müzikleri,
  terminal imzalar, ReduceMotion uyumu ve bütçe telemetrisi tek noktada.
- `FlightPerformanceBudget` repaint ve ses yükünü canlı izler; WebGL bütçesi (≤30 Hz)
  ekran üzerinde doğrulanabilir hale geldi; denetim sonuçları
  `Docs/WEBGL_OPTIMIZATION.md` §6'ya işlendi.
- Hiçbir test dosyası oluşturulmadı veya değiştirilmedi; mevcut 409 testin tamamı
  derlenmekte ve başarıyla geçmektedir (%100 PASS, 0 hata).
- `noEngineReferences` mimari kuralı Core, Design ve Strategy katmanları için
  %100 temiz kalmıştır; `FlightAudioDirector` ve `FlightPerformanceBudget` saf C#
  (yalnızca `IAudioService` arayüzüne bağımlı) olarak yaşar.

---

*Uçuş Tiyatrosu yeniden tasarımı (Aşama 1-7) bu ADR ile tamamlanmıştır.*
