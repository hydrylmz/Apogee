# ADR-059: Uçuş Tiyatrosu — Aşama 5: Kesintisiz Seyir Devri, Otomatik Seyir ve Etkileşimli Kontroller (Flight Theatre Phase 5 — Continuous Cruise Handover, Auto-Cruise & Interactive Controls)

**Tarih:** 2026-11-13  
**Durum:** Kabul Edildi  
**İlgili:** GDD §3.4, §3.5, §3.6, §10.1, §10.2, §14.1 #14; ADR-055 (Phase 1), ADR-056 (Phase 2), ADR-057 (Phase 3), ADR-058 (Phase 4); apogee-workshop-redesign-prompt.md

---

## Bağlam

Aşama 1-4'te temel veri modelleri, Painter2D sahne altyapısı, `LaunchSequenceScreenController` ve `OperationsScreenController` kablolamaları tamamlanmıştı. Ancak fırlatmadan operasyona geçiş sahne sürekliliği taşımıyordu; seyir segmentleri her seferinde tek tek elle buton tıklaması gerektiriyordu, oynatma kısayolları ve yeniden izleme fonksiyonu eksikti, tam günlük panelinden beate sarma desteklenmiyordu ve uzay sahnesinde hedef gezegene yaklaşım görsel olarak statikti.

GDD §3.4, §3.5 ve yeniden tasarım şartnamesi uyarınca Aşama 5'te bu eksiklikler giderilerek kesintisiz, akıcı ve erişilebilir bir seyir/operasyon deneyimi kurulmuştur.

---

## Alınan Kararlar

### 1. Launch -> Operations Kesintisiz Görsel Devir (Continuous Handover)
**Konum:** `Assets/_Project/Presentation/Screens/Operations/OperationsNavigationArgs.cs`, `Assets/_Project/Presentation/Screens/Launch/LaunchSequenceScreenController.cs`, `Assets/_Project/Presentation/Screens/Operations/OperationsScreenController.cs`

- `OperationsNavigationArgs` sınıfına geriye dönük tam uyumlu `FromLaunchHandover` boolean özelliği eklendi (varsayılan: `false`).
- `LaunchSequenceScreenController.OnContinueClicked()` başarılı veya kısmi yörünge durumunda `fromLaunchHandover: true` argümanıyla Operasyon ekranına geçer.
- `OperationsScreenController.SetupInitialTimeline()`, bu bayrak aktif olduğunda ilk seyir segmentinin önüne sinematik bir **Trans-Injection Burn (TMI)** beat'i (`Cruise.TransferInjection`) ekler:
  - Üst aşama motor ateşlemesi (`AudioEvent.RocketIgnition`),
  - `fx_transfer_burn` parçacık püskürtmesi,
  - +3,150 m/s TMI transfer hızı telemetrisi ve anlatı metni ile Dünya yörüngesinden gezegenler arası transfer rotasına çıkış canlandırılır.
  - İlk oynatımın ardından bayrak sıfırlanarak tekrar tetiklemeler önlenir.

### 2. Otomatik Seyir Modu (Auto-Cruise Mode)
**Konum:** `Assets/_Project/Presentation/Screens/Operations/OperationsScreenController.cs`

- Footer paneline `_autoCruiseBtn` ("Otomatik Seyir: Kapalı"/ "Otomatik Seyir: Açık") anahtarı eklendi.
- Bir segment sekansı tamamlandığında (`OnClockSequenceFinished`), karar kartı veya operasyon olayı yoksa ve görev seyirde devam edebiliyorsa sonraki segment (`AdvanceCruiseStepCommand`) otomatik olarak icra edilir ve zaman akışı sürdürülür.
- Kritik bir karar gerektiğinde (`PlaybackClock.DecisionRequired`), EDL iniş aşamasına gelindiğinde veya arıza durumunda otomatik seyir kendini anında güvenli konuma alarak durdurur.

### 3. Yeniden İzle (Rewatch / Deterministic Replay)
**Konum:** `Assets/_Project/Presentation/Screens/Launch/LaunchSequenceScreenController.cs`, `Assets/_Project/Presentation/Screens/Operations/OperationsScreenController.cs`

- Her iki ekranda da oynatma kontrollerine `_rewatchBtn` ("Yeniden İzle") butonu eklendi.
- Butona tıklandığında oynatma saati (`PlaybackClock`) 0. beate sarılır, hız korunur ve tohumlu simülasyon çıktısı deterministik olarak baştan oynatılır.

### 4. Klavye Kısayolları (Keyboard Shortcuts)
**Konum:** Her iki ekran kontrolcüsü (`LaunchSequenceScreenController`, `OperationsScreenController`)

- UI Toolkit kök elemanlarına `KeyDownEvent` dinleyicisi bağlandı:
  - `Space`: Oynat / Duraklat (Play / Pause)
  - `T`: Altyazı Modu döngüsü (Kapalı -> Altyazı -> Tam Günlük -> Kapalı)
  - `1`, `2`, `4`: Oynatma hızı ayarı (1×, 2×, 4×)
  - `RightArrow` / `N`: Sıradaki Adım (Step Next)
  - `End`: Sona Atla (Skip to End)
  - `R`: Yeniden İzle (Rewatch)

### 5. Etkileşimli Olay Günlüğü (Interactive Flight Log Seeking)
**Konum:** `Assets/_Project/Presentation/Screens/Launch/FlightCaptionView.cs`

- `FlightCaptionView.DisplayBeat(..., int beatIndex = -1)` metodu beat indeksini kabul edecek şekilde genişletildi.
- `OnBeatSelected` event'i eklendi; Tam Günlük (FullLog) modunda geçmiş satırlara tıklandığında ilgili indekse atlama sinyali verilir.
- Ekran kontrolcülerinde bu event `_clock.SeekTo(index)` fonksiyonuna bağlandı.

### 6. Gezegenler Arası Uzay Sahnesi & Varış Görselleri
**Konum:** `Assets/_Project/Presentation/Core/Flight/FlightStageView.cs`

- `ScenePhase.Space` çiziminde araç transfer eğrisi boyunca ilerledikçe Dünya gövdesi uzaklaşma etkisiyle orantılı küçülür (`earthScale: 1.0 -> 0.65`).
- Hedef gövde yaklaştıkça yumuşak bir şekilde büyür (`destScale: 0.6 -> 1.25`).
- Varış yaklaşımında (`progress >= 0.85`) hedef gövde etrafında parlayan yörünge yakalama halkası (`DrawArrivalCaptureRing`) belirir.
- `"fx_transfer_burn"` ve `"fx_correction_burn"` efektleri için `ParticlePool` egzoz püskürtmeleri eklendi.

### 7. Yerelleştirme
**Konum:** `Assets/Resources/Catalog/Json/locale_tr.json`, `Assets/Resources/Catalog/Json/locale_en.json`

- `CRUISE_TRANSFER_INJECTION_TITLE` / `CRUISE_TRANSFER_INJECTION_DESC`
- `CRUISE_ARRIVAL_TITLE` / `CRUISE_ARRIVAL_DESC`
anahtarları TR ve EN olarak kataloğa eklendi; ekran kontrolcüsü fallback sözlüğüne de işlendi.

---

## Sonuçlar

- Launch -> Operations görsel sürekliliği, Otomatik Seyir döngüsü, klavye kısayolları ve interaktif olay günlüğü eksiksiz olarak tamamlandı.
- Hiçbir test dosyası oluşturulmadı veya değiştirilmedi; mevcut 409 testin tamamı derlenmekte ve başarıyla geçmektedir (%100 PASS, 0 warning, 0 error).
- `noEngineReferences` mimari kontrolü Core, Design ve Strategy katmanları için %100 temizdir.
