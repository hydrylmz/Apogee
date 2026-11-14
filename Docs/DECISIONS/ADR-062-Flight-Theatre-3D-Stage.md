# ADR-062: Uçuş Tiyatrosu — 3B Sahne Katmanı (Flight Theatre 3D Stage Layer)

**Tarih:** 2026-11-14
**Durum:** Kabul Edildi
**İlgili:** GDD §3.4, §3.5, §15.1; ADR-054 (Painter2D/HangarRocketCanvas), ADR-055–061 (Uçuş Tiyatrosu Aşama 1-7), ADR-048 (WebGL)

---

## Bağlam

Aşama 1-7'nin sonunda Uçuş Tiyatrosu, Painter2D tabanlı 2B bir sahne olarak
tamamlanmıştı. GDD §3.4'ün sinematik sunum hedefi, fırlatma sekansının **gerçek
zamanlı 3B sahnede** (URP kamera, modüler roket modeli, ortam ve FX) oynatılması
ihtiyacını doğurdu; ancak bu hedef:

- "Beyin" katmanını (`FlightTimeline`, `FlightBeat`, `FlightChoreography`,
  `PlaybackClock`, `FlightAudioDirector`, simülasyon ve testler) değiştirmeden,
- FBX/GLB modeller henüz hazır olmadığından placeholder geometriyle başlayıp
  sonrasında drop-in yapılabilecek şekilde,
- 2B sahnenin erişilebilirlik/perf fallback'i olarak kalmasıyla (oyuncu tercihi)
  gerçekleştirilmek zorundaydı.

---

## Alınan Kararlar

### 1. Gerçek 3B Yaklaşım: URP Kamera → RenderTexture → UI Toolkit Image
**Konum:** `Assets/_Project/Presentation/Flight3D/FlightStageView3D.cs`

- Sahne, runtime'da inşa edilen bir `Camera` GameObject'i ile `RenderTexture`
  (ARGB32, 24-bit depth, boyut element rect'inden clamp'li 320×240–1920×1080)
  üzerine render edilir; çıktı UI Toolkit `Image` çocuğuyla gösterilir.
- Kamera yalnızca RT'ye bakar (`targetTexture`), ana ekranı asla etkilemez.
- `StartAnimation` → `StopAnimation` tam yaşam döngüsü teardown'u yapar: RT
  release edilir, tüm rig GameObject'leri yok edilir — tek bootstrap sahnesi
  temiz kalır, WebGL bellek bütçesi sınırlı kalır.
- `GeometryChangedEvent` ile RT yeniden boyutlandırılır (≥4 px değişim eşiği).

### 2. Ortak Sunum Sözleşmesi: `IFlightStage`
**Konum:** `Assets/_Project/Presentation/Flight3D/IFlightStage.cs`

- 2B (`FlightStageView`) ve 3B (`FlightStageView3D`) renderer'lar aynı arayüzü
  uygular: `ApplyBeat`, `SetPayloadType`, `SetDestinationProfile`,
  `SetReduceMotion`, `SetPerformanceBudget`, `AdjustCameraZoom`,
  `ResetCameraZoom`, `PanCameraBy`, `SnapCamera`, `StartAnimation`,
  `StopAnimation`.
- `LaunchSequenceScreenController` iki renderer'ı da oluşturur; oyuncu tercihi
  aktif olanı seçer (`ActivateStageView`). Beat orkestrasyonu, koreografi,
  ses ve HUD katmanına **dokunulmadı**.
- 2B sahnenin davranışı değişmedi; `OperationsScreenController` 2B sahneyi
  kullanmaya devam eder (kapsam dışı bırakıldı).

### 3. Ayar Zinciri: "3B Uçuş Sahnesi" Tercihi
**Konum:** `GameSettingsData.cs`, `PlayerPrefsSettingsStorage.cs`,
`SettingsViewModel.cs`, `SettingsScreenController.cs`

- `GameSettingsData.Use3DStage` (varsayılan `true`), PlayerPrefs anahtarı
  `apogee_settings_use3dstage` (HasKey guard'ı ile eski kayıt uyumluluğu),
  `SettingsViewModel.Use3DStage` observable + `SetUse3DStage()`, kaydet/
  sıfırla akışlarına dahil.
- Settings ekranının Görüntü kartına toggle + TR/EN açıklama eklendi
  (lokalizasyon fallback metinleriyle).

### 4. Kamera Koreografisi: `FlightCameraDirector`
**Konum:** `Assets/_Project/Presentation/Flight3D/FlightCameraDirector.cs`

- `flight_choreography.json`'daki **18 `CameraMotionKey`'in tamamı** kod
  tablosunda 3B kamera pozu (odak offset'i, look offset'i, FOV, lerp hızı)
  olarak eşlenir; 2B `FlightCamera.ResolvePreset` anahtarlarıyla birebir
  aynıdır — JSON override'ları iki renderer'ı özdeş sürer.
- Oyuncu zoom/pan katmanları 2B ile aynı kısıtlarda (zoom 0.55–1.70, pan
  clamp'li), ReduceMotion'da snap + shake bastırma, lansman/Max-Q/staging'de
  decaying shake.

### 5. Modüler Araç Rig'i: `FlightVehicleRig`
**Konum:** `Assets/_Project/Presentation/Flight3D/FlightVehicleRig.cs`

- Placeholder geometri (Unity primitive'leri) **FBX drop-in sözleşmesiyle**
  kurulur: parça isimleri `Stage1/Stage2/Stage3/Fairing/Capsule/Rover/Lander/
  Orbiter`; egzoz bağlantıları `AnchorStage1/2/3`, `AnchorBus`,
  `AnchorPayloadBase`, `AnchorNose`. Modeller geldiğinde kod değişikliği
  gerekmeyecek.
- Staging **idempotent**: `Stage1Separation`, `Stage2Ignition`,
  `FairingJettison`, `ORBIT/Insertion` anahtarları arzu edildiğinde ara
  seek'lerde güvenli; ayrılan kademe dünya-uzayı enkaz olarak düşer (8 s ömür).
- `ActiveExhaustAnchor` faz duyarlıdır: Ascent'te yanan kademe, Space'te
  kapsül bus nozulu, Landing'de payload tabanı — MECO/SECO/coast beat'lerinde
  alev söner (2B playtest fix #2 paritesi).
- Bezier cruise yayı, EDL alçaklığı, bilim yüzeyinde yavaş rover sürüşü.

### 6. Ortam Rig'i: `FlightSceneRig`
**Konum:** `Assets/_Project/Presentation/Flight3D/FlightSceneRig.cs`

- Dört ortam grubu (Pad + kule / Uzay: kalkış+varış gezegeni, atmosfer kabuğu,
  halkalar, trajectory yayı, capture halkası / Yıldızlar / Yüzey: arazi, iniş
  pedi, kaya props). Renkler `DestinationProfile`'dan (flight_visuals.json)
  beslenir — kodda hedef adına dallanma yok.
- Rig-lokal directional sun (stage yaşarken aktif); gökyüzü rengi faz+yükseklik
  interpolasyonuyla çözülür (`ResolveBackground`).

### 7. FX Rig'i: `FlightFxRig`
**Konum:** `Assets/_Project/Presentation/Flight3D/FlightFxRig.cs`

- Tüm `VisualEffectKey`'ler karşılanır: sürekli sistemler (egzoz alevi, retro
  plume, pad buharı, ısı parıltısı, paraşüt, Max-Q buhar konisi, tarama ışını,
  downlink halkaları) ve tek atımlık patlamalar (kademe ayrılması, iniş tozu,
  abort/patlama, orbit insertion kutlaması, weather scrub).
- Renk sabitleri 2B sahneyle aynı (görsel süreklilik); tüm PS'ler kurulumda
  bir kez havuzlanır — beat başına sıfır allocation.
- `ResetTransient()` her beat öncesi geçici toggle'ları sıfırlar;
  ReduceMotion tüm FX'i durdurur.

### 8. Materyal Çözümü: `Flight3DMaterials`
**Konum:** `Assets/_Project/Presentation/Flight3D/Flight3DMaterials.cs`

- URP shader'ları öncelikli (`Lit/SimpleLit/Unlit`), bulunamazsa
  `Sprites/Default` fallback. Tüm materyaller taze oluşturulur — paylaşılan
  asset mutasyonu yok.

---

## Sonuçlar

- Beat orkestrasyonu, koreografi JSON'u, ses ve HUD katmanı değişmedi; 3B
  sahne tamamen sunum katmanında (`Apogee.UI.Flight3D` namespace'i,
  `Apogee.UI` assembly'si) yaşar.
- 2B fallback korunur: ayarlardan kapatılabilir; tercihi ekran açılışında ve
  `LoadSettingsPreferences` ile okunur.
- Placeholder geometri, FBX/GLB drop-in sözleşmesi (parça isimleri + anchor
  transformları) ile modeller geldiğinde değişikliksiz yükseltilebilir.
- `noEngineReferences` kuralı bozulmadı; yeni katman yalnızca UnityEngine +
  `Apogee.UI.Core` tipine bağımlıdır.
- Yerel test paketi 399/409 geçmektedir; 10 failure temiz HEAD'de de birebir
  aynıdır (ortam kaynaklı: UnityEngine.CoreModule native yüklemesi ve test
  runner locale yolu) — 3B çalışma sıfır regresyon üretmiştir.
- Bilinen kısıtlar: `SurfaceZ` sabiti `FlightSceneRig` ve `FlightVehicleRig`
  içinde senkron tutulmalıdır; kamera preset'leri şimdilik kod tablosudur
  (`flight_3d.json` veri dosyasına taşıma opsiyonel gelecek adımdır).

---

*3B Uçuş Tiyatrosu katmanı bu ADR ile belgelenmiştir; FBX/GLB modeller geldiğinde ADR-063 ile güncellenecektir.*
