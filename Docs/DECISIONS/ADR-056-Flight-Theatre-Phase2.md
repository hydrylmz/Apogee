# ADR-056: Uçuş Tiyatrosu — Aşama 2: Sahne ve Araç

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi  
**İlgili:** GDD §3.4, §3.1, §15.1; ADR-054 (Painter2D pipeline), ADR-055 (Phase 1 core)

---

## Bağlam

Aşama 1'de engine-free çekirdek veri modeli (`FlightBeat`, `PlaybackClock`, `FlightTimeline`) oluşturulmuştu. Aşama 2'nin görevi, bu beat verisini görsel olarak işleyecek Presentation katmanı bileşenlerini yazmaktır:

- Araç silueti (3 seviye × 3 payload tipi)
- Full-screen sahne kanvası (4 scene layer, ≤30 Hz)
- Havuzlanmış partiküller (sıfır heap alloc)
- Kamera simülasyonu (USS transition tabanlı)

---

## Alınan Kararlar

### 1. `VehicleSilhouetteView` — Araç Silueti (`Apogee.UI.Core.Vehicle`)

**Dosya:** `Assets/_Project/Presentation/Core/Vehicle/VehicleSilhouetteView.cs`

**3 Seviye:**
| Seviye | Görsel |
|---|---|
| `Launch` | Tam roket yığını: gövde + üst aşama + burun konisi + kanatçıklar + egzoz alevi |
| `Cruise` | Yük kapsülü / uzay aracı gövdesi: bus + güneş panelleri + yüksek kazançlı anten |
| `Landing` | Payload tipine göre: Rover (4 tekerlek + mast) / Lander (bacaklar + iniş motoru) / Orbiter (Cruise ile aynı) |

**`PayloadType` enum** bu view içinde tanımlandı — Design katmanına bağımlılık yok.

**Exhaust animasyonu:** `schedule.Execute(...).Every(333)` → ≤3 Hz, erişilebilirlik §3.5 uyumlu. `ReduceMotion=true` iken animasyon durdurulur.

**Accent rengi:** `SetAccentColor(Color)` ile dışarıdan set edilir — `flight_visuals.json`'daki `orbitRingColorHex` değeri. Tüm Painter2D geçişlerinde bu renk kullanılır (burun konisi, güneş paneli kenarı, tekerlek çerçevesi, vb.)

### 2. `FlightStageView` — Tam Ekran Sahne (`Apogee.UI.Core.Flight`)

**Dosya:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`

**4 ScenePhase → 4 Çizim Metodu:**

| Faz | Trigger (beat.PhaseId) | İçerik |
|---|---|---|
| `Pad` | Varsayılan / Ignition öncesi | Ön şafak gökyüzü, beton pad, fırlatma kulesi silueti, yıldızlar |
| `Ascent` | Ignition, Liftoff, MaxQ, Stage*, Fairing, Orbital | Irtifaya bağlı gökyüzü geçişi, atmosfer bandı, Max-Q buhar konisi, Dünya eğrisi (>%70 irtifa) |
| `Space` | Cruise | Derin uzay, yıldız alanı, sol Dünya + sağ hedef gezegen, Bezier yörünge yayı |
| `Landing` | Deorbit, Entry, Heating, Parachute, Descent, Touchdown, Science, Arrival | Hedef atmosferi / uzay gökyüzü, yüzey silueti, ısı parlaması (HeatShieldIntegrity) |

**Tek Painter2D geçişi:** `generateVisualContent += OnGenerateVisualContent` — ek kanvas yok. Yıldız alanı deterministik `System.Random(42)` ile çizilir (aynı pattern, farklı alpha twinkle).

**Repaint hızı:** `schedule.Execute(OnAnimationTick).Every(33)` → ~30 Hz. `StopAnimation()` çağrısında schedule durdurulur.

**Sky gradyanı:** Painter2D native gradient desteği olmadığı için 6 bant (`DrawSkyGradient`) ile yatay quad üst üste bindirilir.

**`VehicleSilhouetteView` kompozisyon:** `AbsolutePosition` çocuk element olarak eklenir; `Add(_vehicle)`. Painter2D arka plan üzerine composited. Level sync `UpdateVehicleLevel()` ile `PhaseId` değişiminde çalışır.

**DestinationProfile:** `SetDestinationProfile(DestinationProfile)` ile set edilir; `flight_visuals.json` verisini taşır. Çağıran taraf JSON'ı parse eder, bu view struct alır.

### 3. `ParticlePool` — Havuzlanmış Partiküller (`Apogee.UI.Core.Flight`)

**Dosya:** `Assets/_Project/Presentation/Core/Flight/ParticlePool.cs`

20 `Particle` struct'ı önceden tahsis edilir. Round-robin write head ile `Emit(count, Kind, normalizedOrigin)` çalışır — heap alloc yok. `Draw(Painter2D, w, h)` tüm aktif partikülleri çizer — ayrı canvas değil, ana FlightStageView geçişine eklenir.

**5 efekt türü:**
| Kind | Renk | Kullanım |
|---|---|---|
| Exhaust | Amber → Transparan | Launch exhaust FX |
| Debris | Gri → Transparan | Stage separation |
| Vapour | Beyaz → Transparan | Max-Q |
| Heat | Turuncu-kırmızı → Transparan | EDL ısı kalkanı |
| Dust | Kıremit → Transparan | Landing tozu |

`Clear()`: tüm partikülleri deaktive eder (ReduceMotion veya ekran gizlenme).

### 4. `FlightCamera` — USS Kamera Simülasyonu (`Apogee.UI.Core.Flight`)

**Dosya:** `Assets/_Project/Presentation/Core/Flight/FlightCamera.cs`

Unity Camera bileşeni yok — `VisualElement.transform.position` ve `transform.scale` ile 2D pan+scale animasyonu. `ApplyMotionKey(string)` ile beat'teki `CameraMotionKey` değerine karşılık gelen preset uygulanır.

**14 preset** (örnek):
- `cam_launch_ground` → pan y+20, scale 1.05
- `cam_launch_orbit` → pan sıfır, scale 0.85 (uzaklaş)
- `cam_edl_heating` → pan x+10 y+20, scale 1.40 (yakınlaş)
- `cam_edl_crash` → pan x+30, scale 1.60 (sallantı efekti için sağa kaydır)

`Tick(float dt)`: lerp ile hedef pan/scale'e yaklaşır. `ReduceMotion=true`: snap (lerp atlanır).

---

## Kapsam Dışı (Bu ADR)

- LaunchSequenceScreenController kablolama → Aşama 3
- Caption layer → Aşama 4
- Operations screen entegrasyonu → Aşama 5
- EDL / Science sahne içerikleri (tam) → Aşama 6

---

## Sonuçlar

- Tüm Aşama 2 bileşenleri `Apogee.UI.asmdef` kapsamında; `noEngineReferences=false` (UnityEngine kullanımı gerekli ve beklenen)
- `VehicleSilhouetteView`, `FlightStageView`, `ParticlePool`, `FlightCamera` oluşturuldu
- Exhaust animasyonu ≤3 Hz (erişilebilirlik §3.5 uyumlu)
- Sahne repaint ≤30 Hz (WebGL perf bütçesi)
- ParticlePool: 20 slot, sıfır per-frame heap alloc
- ReduceMotion: animasyon durur, geçişler snap olur, partiküller temizlenir

---

*Sıradaki:* ADR-057 — Aşama 3: LaunchSequenceScreen kablolama (FlightStageView, PlaybackClock, beat→audio, PhaseRailView, HUD)
