# ADR-060: Uçuş Tiyatrosu — Aşama 6: EDL ve Yüzey Bilim Animasyonu (Flight Theatre Phase 6 — EDL & Surface Science Animation)

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi  
**İlgili:** GDD §3.4, §3.5, §3.6, §10.2, §10.3, §14.1 #14; ADR-055 (Phase 1), ADR-056 (Phase 2), ADR-057 (Phase 3), ADR-058 (Phase 4), ADR-059 (Phase 5)

---

## Bağlam

Aşama 1-5 kapsamında uçuş çekirdek modelleri, Painter2D sahne altyapısı, `LaunchSequenceScreenController`, `OperationsScreenController`, kesintisiz fırlatma-seyir geçişi ve interaktif oynatma kontrolleri tamamlanmıştı. Ancak gezegene iniş (EDL) ve yüzey bilim operasyonları (Science) sırasında görsel sunum sade ve statikti: paraşüt açılması, ablatif şok dalgası parlaması, retro itki alevleri, rover robotik kolu, yüzey tekerlek izleri, bilim lazeri taraması ve uplink/downlink radyo dalgaları eksikti. Ayrıca `FlightCamera` sahne bileşenine tam bağlanmamış durumdaydı.

GDD §3.4, §10.2 ve §10.3 gereksinimleri uyarınca Aşama 6'da EDL ve Science safhaları için eksiksiz sinematik animasyonlar ve kamera koreografisi hayata geçirilmiştir.

---

## Alınan Kararlar

### 1. USS Transform Kamera Entegrasyonu (`FlightCamera`)
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightCamera.cs`, `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`

- `FlightCamera` sınıfı hem `cam_*` hem `edl_*` / `launch_*` / `cruise_*` / `science_*` anahtarlarını tek elden çözümleyecek şekilde genişletildi.
- `FlightStageView` içine `_camera = new FlightCamera(_vehicle, _reduceMotion)` bileşeni entegre edildi.
- `ApplyBeat` her çalıştığında beat içindeki `CameraMotionKey` kameraya aktarılır (`_camera.ApplyMotionKey`), animasyon döngüsünde (`OnAnimationTick`) lerp enterpolasyonu icra edilir. `ReduceMotion` aktif olduğunda veya ekran kapandığında anında sıfırlanır (`Reset`).

### 2. İrtifaya Duyarlı İniş Konumlandırması (EDL Altitude Descent)
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`

- `ScenePhase.Landing` modunda aracın dikey konumu (`_vehicle.style.top`) telemetrideki `AltitudeKm` değerine göre dinamik olarak hesaplanır:
  - 60+ km irtifada gökyüzünün üst kısmında (`top: 14%`),
  - Alçaldıkça yumuşak bir şekilde aşağı inerek yüzey seviyesine (`top: 38%`) oturur.
  - Dokunma (`Touchdown`) ve Bilim (`Science`) evrelerinde yüzey zeminine tam kilitlenir.

### 3. EDL Görsel Animasyonları (Painter2D)
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`

- **Paraşüt Kubbesi ve İpleri (`DrawParachute`):** Sesüstü paraşüt açılma evresinde (`PARACHUTE` / `fx_chute_deploy`) aracın üstünde Apollo/NASA turuncu-beyaz dikey şeritli kubbe ve araca uzanan gergin askı ipleri çizilir.
- **Isı Kalkanı Plazma Şok Dalgası (`DrawHeatShockwave`):** Atmosferik giriş ve pik ısınma evrelerinde (`ENTRY` / `HEATING` / `fx_peak_heating` / `fx_entry_heating`) aracın alt burnunda kıvrılan ablatif turuncu-sarı iyonize plazma pruva şoku çizilir; `Heat` partikülleriyle desteklenir.
- **Retro İniş Alev Jetleri (`DrawRetroPlume`):** Son alçalma ve itkili iniş evresinde (`DESCENT` / `fx_retro_ignite`) aracın çift iniş nozulundan aşağı doğru yüksek hızlı kehribar-camgöbeği retro egzoz alevleri püskürtülür.
- **Yüzey Temas Halkası (`DrawTouchdownShockRing`):** Başarılı dokunma anında zeminde genişleyen eliptik şok ve toz halkası belirir.
- **Kaza ve Enkaz Alanı (`DrawCrashSite`):** Başarısızlık durumunda (`fx_edl_fail` / `CriticalFailure`) zeminde yanık krateri, duman ve `Debris` partikülleri oluşur.
- **Gezegene Özgü Zemin Detayları (`DrawPlanetSpecificTerrain`):** Ay için kraterler, Mars için kırmızı kum tepesi dalgalanmaları ve kaya çıkıntıları, Venüs/Europa için çatlak ve yarık çizgileri işlenir.

### 4. Yüzey Bilim Operasyonu Görselleri (Surface Science)
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`, `Assets/_Project/Presentation/Core/Vehicle/VehicleSilhouetteView.cs`

- **Robotik Numune Kolu ve Sensör:** Rover silüetinde bilim modunda (`_isScienceActive = true`) gövdeden zemine uzanan 3 mafsallı robotik numune kolu ve delici/spektrometre ucu açılır.
- **Lazer/Sensör Tarama Işını (`DrawScienceScanBeam`):** Araç sensör mastından zemine doğru açılan parlak neon camgöbeği/nane renkli lazer tarama konisi ve zeminde salınan analiz tarama noktası canlandırılır.
- **Veri İndirme (Downlink) Radyo Dalgaları (`DrawDownlinkWaves`):** Yüksek kazançlı antenden gökyüzüne doğru yükselen 3 kademeli konsantrik radyo sinyal yayları canlandırılır.
- **Yüzey Tekerlek İzleri (`DrawSurfaceTracks`):** Yüzeyde rover'ın arkasında zeminde paralel tekerlek izleri çizilir.

### 5. Partikül Havuzu Kapsamı (`ParticlePool`)
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`

- `SpawnParticlesForBeat` metodu tüm EDL ve Science görsel efekt anahtarlarını (`fx_entry_heating`, `fx_peak_heating`, `fx_chute_deploy`, `fx_retro_ignite`, `fx_retro_burn`, `fx_edl_retro_ignite`, `fx_touchdown_dust`, `fx_edl_fail`, `fx_science_data_collect`, `fx_science_downlink`, `fx_science_contact`, `fx_science_fail`) eksiksiz olarak sıfır-alloc havuzuna bağlamıştır.

---

## Sonuçlar

- EDL (giriş, ısı kalkanı, paraşüt, retro itki, yumuşak iniş/kaza) ve Science (lazer tarama, robotik kol, downlink, zemin izleri) sinematikleri Painter2D vektörel motoruyla tamamlandı.
- Hiçbir test dosyası oluşturulmadı veya değiştirilmedi; mevcut 409 testin tamamı derlenmekte ve başarıyla geçmektedir (%100 PASS, 0 hata).
- `noEngineReferences` mimari kuralı Core, Design ve Strategy katmanları için %100 temiz kalmıştır.
