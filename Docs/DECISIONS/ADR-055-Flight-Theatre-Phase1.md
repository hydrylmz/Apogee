# ADR-055: Uçuş Tiyatrosu — Aşama 1: Çekirdek Veri Modeli (Flight Theatre Phase 1 — Core Data Model)

**Tarih:** 2026-11-12  
**Durum:** Kabul Edildi  
**İlgili:** GDD §3.4, §3.5, §3.6, §10.1, §15.1; apogee-workshop-redesign-prompt.md

---

## Bağlam

Mevcut `LaunchSequenceScreen` ve `OperationsScreen` ekranları düz metin listesi sunuyordu — simülatör sonuçları okunuyor, faz etiketleri statik switch/case ile çeviriliyor ve kullanıcıya yalnızca bir metin akışı gösteriliyordu. GDD §3.4-§3.6 ve yeniden tasarım promptu, bu iki ekranın birleşik bir "Uçuş Tiyatrosu" (Flight Theatre) animasyon sahnesine dönüştürülmesini emrediyordu:

- Launch → Cruise → Arrival → EDL → Science sürekli sinematiği
- 8 aşama, 6 LaunchOutcome, tüm CruiseSegmentStatus/EdlOutcome sonları
- 3 modlu altyazı sistemi (Kapalı / Altyazı / Tam Kayıt)
- Oynatma kontrolleri (▶⏸, 1×/2×/4×, Adım, Atla, Tekrar)
- Deterministik tekrar (aynı seed → aynı beat dizisi)
- WebGL performans bütçesi (tek Painter2D, havuzlanmış partiküller, ≤30 Hz)

Bu ADR **Aşama 1**'i — engine-free çekirdek veri modelini — kapsar.

---

## Alınan Kararlar

### 1. `FlightBeat` — Temel Birim (Pure C#)

Konumlandırma: `Assets/_Project/Presentation/Core/Flight/FlightBeat.cs`  
Namespace: `Apogee.UI.Core.Flight`

Her simülasyon olayını tek bir `FlightBeat` nesnesine dönüştürme kararı alındı. Beat şunları taşır:
- **Kimlik:** `PhaseId`, `StatusId`
- **Lokalizasyon:** `TitleKey`, `NarrativeKey`, `TechnicalDetails`
- **Telemetri:** `AltitudeKm`, `VelocityMps`, `TimestampSeconds`, `HeatShieldIntegrity`, `CruiseProgress`
- **Koreografi:** `CameraMotionKey`, `VisualEffectKey`, `AudioEventId`
- **Oynatma:** `DurationSeconds`, `RequiresDecision`, `IsTerminal`, `OutcomeTag`

`FlightBeat` immutable (salt constructor) tasarlandı; testlerde kolaylıkla oluşturulabilir.

### 2. `PlaybackClock` — Oynatma Motoru (Pure C#)

Konumlandırma: `Assets/_Project/Presentation/Core/Flight/PlaybackClock.cs`

Unity bağımlılığı yok — `Tick(float deltaTime)` çağrısı dışarıdan yapılır (MonoBehaviour'dan veya test fikstüründen). Kararlar:

- **`Play()` / `Pause()` / `SetSpeed(float)`:** Temel kontroller
- **`StepNext()`:** `StepNextPhaseCommand` semantiğini korur (mevcut komut sistemi değişmez)
- **`SkipToEnd()`:** `SkipToEndCommand` semantiğiyle aynı — final beat index + elapsed = duration, `Finish()` tetiklenir → determinizm garantisi
- **`SeekTo(int)`:** Log panelinden herhangi bir beate atlama
- **`BeatAdvanced` event:** `(newIndex, oldIndex)` — UI kablolama bu event üzerinden yapılır
- **`DecisionRequired` event:** `RequiresDecision=true` beate ulaşınca otomatik pause ve event tetikleme

### 3. `FlightTimeline` — Fabrika (Pure C#)

Konumlandırma: `Assets/_Project/Presentation/Core/Flight/FlightTimeline.cs`

4 statik factory metodu — simülatör sonuçlarını `FlightBeat[]` dizisine çevirir:

| Metot | Girdi | Çıktı |
|---|---|---|
| `FromLaunch(LaunchSimulationResult)` | 12 LaunchPhaseType + 6 LaunchOutcome | Beat dizisi, son beat terminal |
| `FromCruise(IReadOnlyList<CruiseSegmentEvent>)` | Segment listesi | Her segment = 1 beat |
| `FromEdl(EdlSimulationResult, bool hasAtmosphere)` | 6 EdlPhaseType + 6 EdlOutcome | `hasAtmosphere` EDL kamera/fx anahtarını etkiler |
| `FromScience(ScienceSimulationResult)` | SciencePhaseEvent[] | `SciencePhaseEvent.NarrativeKey` yok → `"SCI_EVENT_GENERIC_DESC"` fallback + `// TODO-VERİ` |

**Deterministik kural:** Aynı girdi → aynı çıktı dizisi. Hiçbir random çağrısı yok.  
**Hata toleransı:** Null sonuç → `ArgumentNullException`; boş event listesi → 1 generic beat döner, asla fırlatmaz.

### 4. `FlightTimelineLimits` — Süre Sabitleri

Beat sürelerinin kodda sabit yazılması yerine merkezi bir sınıfta toplanması kararı alındı:

| Sabit | Değer |
|---|---|
| `LaunchMaxSeconds` | 75f |
| `CruiseSegmentMaxSeconds` | 6f |
| `EdlMaxSeconds` | 45f |
| `ScienceEventMaxSeconds` | 20f |

`FlightTimelineLimits.Default` singleton; `FromLaunch/Cruise/Edl/Science` opsiyonel parametre ile test override'ına izin verir.

### 5. `FlightChoreography` — Veri Güdümlü Koreografi

Konumlandırma: `Assets/_Project/Presentation/Core/Flight/FlightChoreography.cs`  
Veri: `Assets/Resources/Catalog/Json/flight_choreography.json`

Beat override tablosu JSON'da — bir `phaseId.statusId` anahtarına karşılık gelen `cameraMotionKey`, `visualEffectKey`, `audioEventId` ve `durationOverride` değerleri. Anahtar bulunamazsa `FlightTimeline`'ın hesapladığı varsayılan değer kullanılır (graceful fallback). Unity katmanında okunur; Core katmanına geçmez.

### 6. `FlightCaptionMode` — Altyazı Modu Enum

```csharp
public enum FlightCaptionMode { Off, Subtitle, FullLog }
```

GDD §3.5 uyarınca 3 mod. `GameSettingsData` ve `SettingsViewModel` bu enum'u kullanır.

### 7. `flight_visuals.json` — Hedef Görsel Profilleri

`Assets/Resources/Catalog/Json/flight_visuals.json` — her `destinationId` için gökyüzü renkleri, gezegen rengi, atmosfer varlığı, yüzey dokusu anahtarı. Aşama 2'deki `FlightStageView` bu dosyayı okuyacak.

### 8. Locale Anahtarları (388 → 496)

`locale_tr.json` ve `locale_en.json` dosyalarına 108 yeni anahtar eklendi:

| Grup | Anahtar Sayısı | Örnek |
|---|---|---|
| `LAUNCH_EVENT_*` | 12 | `LAUNCH_EVENT_IGNITION_TITLE` |
| `LAUNCH_OUTCOME_*` | 6×2 | `LAUNCH_OUTCOME_SUCCESS_TITLE/DESC` |
| `CRUISE_SEG_*` | 6×2 | `CRUISE_SEG_NOMINAL_TITLE/DESC` |
| `EDL_EVENT_*` | 6×2 | `EDL_EVENT_PEAK_HEATING_TITLE/DESC` |
| `EDL_OUTCOME_*` | 6×2 | `EDL_OUTCOME_SUCCESS_TITLE/DESC` |
| `SCI_EVENT_*` | 8×2 | `SCI_EVENT_DATA_COLLECTION_TITLE/DESC` |
| `FLIGHT_GENERIC_*` | 6 | `FLIGHT_GENERIC_NOMINAL`, `FLIGHT_GENERIC_SKIP` |

### 9. `GameSettingsData` + `SettingsViewModel` — Flight Alanları

`GameSettingsData` üç yeni alan eklendi (opsiyonel constructor parametreleri → geri uyumluluk):

```csharp
public FlightCaptionMode CaptionMode  { get; set; } = FlightCaptionMode.Subtitle;
public bool              ReduceMotion { get; set; } = false;
public float             FontScale    { get; set; } = 1.0f;  // [0.5, 2.0]
```

`PlayerPrefsSettingsStorage` her alan için `HasKey` guard ile yükleme — eski kayıt dosyaları varsayılan değerle yüklenir, kayıt bozulması yok.

`SettingsViewModel`'e eklendi:
- `ObservableValue<FlightCaptionMode> CaptionMode`
- `ObservableValue<bool> ReduceMotion`
- `ObservableValue<float> FontScale`
- `SetCaptionMode()`, `SetReduceMotion()`, `SetFontScale()` setterları
- `LoadInitialSettings()` ve `ApplySettings()` bu alanları içerir
- `ResetToDefaults()` bu alanları sıfırlar

### 10. `LaunchNavigationArgs` — DestinationId Düzeltmesi

`DestinationId` opsiyonel eklendi; `DefaultFallbackSeed = 1_234_567UL` sabiti merkezi yapıldı:

```csharp
public string DestinationId { get; }
public const ulong DefaultFallbackSeed = 1_234_567UL;
```

`LaunchSequenceScreenController.OnContinueClicked()` artık `DestinationId` → `MissionName` → `"leo"` öncelik sırası ile `destinationId` çözer.

### 11. `Variables.uss` — Flight Token'ları

`:root {}` bloğuna `--flight-*` token grubu eklendi:
- Renk: `--color-flight-hud-bg`, `--color-flight-caption-bg`, `--color-flight-phase-active/done/error`, `--color-flight-heat-shield`, `--color-flight-trajectory`, `--color-flight-sky-ground/high`, `--color-flight-exhaust`
- Tipografi: `--font-size-hud-large` (32px), `--font-size-hud-clock` (28px)
- Boşluk: `--space-hud-pad` (20px)
- Geçiş: `--flight-beat-transition` (300ms), `--flight-reduce-motion-transition` (0ms)

---

## Kapsam Dışı (Bu ADR)

- VehicleSilhouetteView, FlightStageView, ParticlePool → Aşama 2
- LaunchSequenceScreenController kablolama → Aşama 3
- Caption layer, FlightLogPanel → Aşama 4
- Operations screen entegrasyonu → Aşama 5
- EDL / Science animasyonu → Aşama 6
- Ses kablolama, ReduceMotion, WebGL audit → Aşama 7

---

## Sonuçlar

- Tüm Aşama 1 Core/Flight dosyaları `using UnityEngine` içermez → EditMode testlerinde çalışabilir
- Sim sonuçları değişmez (CommandBus korunur); UI yalnızca okur
- Skip-to-end determinizm garantisi: `SkipToEnd()` → son beat index + elapsed = duration → `Finish()` → `SequenceFinished` event (doğal oynatmayla aynı terminal durum)
- Geri uyumluluk: `PlayerPrefsSettingsStorage` eski kayıtları `HasKey` guard ile varsayılan değerle yükler
- Locale: ham anahtar görünmesi artık imkânsız — tüm LAUNCH/CRUISE/EDL/SCI anahtarları locale dosyalarında mevcut

---

*Sıradaki:* ADR-056 — Aşama 2: VehicleSilhouetteView + FlightStageView + ParticlePool
