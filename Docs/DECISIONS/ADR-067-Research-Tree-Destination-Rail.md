# ADR-067: Ar-Ge Ağacı — Görev Hedefi Şeridi (Destination Rail Identity)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §9.2, §14.1 (#10), §16.1, §19.1; ADR-063 (Fiziksel Dünya UI Temaları), ADR-066 (Devre Şeması Ar-Ge Ağacı)

---

## Bağlam

ADR-066 ile Ar-Ge Ağacı HOI4 tarzı görsel bir ağaca taşındı: 8 dal swim-lane
halinde, 22 düğümün NASA fotoğraflı kartları var; kilitli kartlar karanlık
silüet. Ancak oyuncu ağaca bakınca **"neden"** sorusunun cevabını göremiyordu:
hangi teknolojiler hangi görev hedefini (Dünya Yörüngesi / Ay / Mars / Venüs /
Europa) açıyor? Ayrıca `destinations.json`'daki 5 görev hedefi UI'da yalnızca
Misyon Tasarla ekranında ham ID etiketleriyle (`LEO`, `MOON`…) görünüyordu;
oyunun 5 hedeflik ilerleme yayı oyuncuya net aktarılmıyordu.

İki veri sorunu da bu kararla keşfedildi:

1. `destinations.json` → `unlockResearchIds` alanı eski node ID'leri içeriyordu
   (`edl-heatshield-2`, `propulsion-meth-lox`, `thermal-extreme-shielding`) —
   araştırma ağacındaki gerçek ID'ler (`edl-heat-shield-mk2`,
   `prop-methane-reuse`) ile eşleşmiyor, kilit hesabı ve
   `MissionComposer.DetermineRequiredResearch` profilleri ölü veriye bakıyordu.
2. `nameKey`/`descKey`'lerin işaret ettiği `DEST_*_NAME` / `DEST_*_DESC`
   anahtarları hiçbir locale dosyasında tanımlı değildi (eksik anahtar fallback
   ile atlanıyordu).

---

## Alınan Kararlar

### 1. Konsept: Görev Hedefi Şeridi (Destination Rail)
**Konum:** `Screens/Research/ResearchTreeScreenController.cs`,
`Screens/Research/ResearchTreeViewModel.cs`,
`Screens/Research/DestinationItemViewModel.cs` (yeni)

Aktif araştırma şeridi ile dal çubuğu arasına **"GÖREV HEDEFLERİ"** şeridi
eklendi: 5 sekme (LEO, Ay, Mars, Venüs, Europa), her sekmede 22px dairesel
NASA fotoğraf "madeni para" + Türkçe ad.

- **Fotoğraflar:** `Assets/Resources/Destinations/Art/<id>.jpg` — NASA Image
  and Video Library'den indirildi ve tek tek görsel olarak doğrulandı:
  Dünya tam disk (Galileo 1990, PIA00076; katalog ID'si `leo`), Ay dolunay
  mozaği (LRO), Mars global görünüm (Viking, PIA00407), Venüs radar mozaği
  (Magellan, PIA00478), Europa doğal renk (Galileo, PIA00502; iki globlu
  kareden doğal renkli sol glob kırpıldı). `destinations.json` → `art` +
  `artCredit` (tek satır Türkçe atıf); `attribution_data.json` →
  `attr-nasa-destination-art` girdisi. Kargo deseni ADR-054/063/066 ile aynı:
  `Resources.Load<Texture2D>` + sözlük cache + try/catch.
- **Karanlık kilit:** kilidi açılmamış hedefin madeni parası %72 karartma
  perdesi (`--locked .circuit-dest-coin-veil`) ve soluk çerçeveyle HOI4 silüet
  hissiyle görünür; tooltip "Kilitli — gerekli teknolojiler/itibar" yazar.
  Kilit koşulu tamamlanınca sekme aydınlanır — başarım açılana kadar karanlık
  kalması oyuncuya somut bir hedef verir.
- **Detay kartı:** sekme seçilince şeridin altında açılır: 64px fotoğraf +
  atıf satırı, ad + **AÇIK/KİLİTLİ** rozeti, açıklama, "Δv 21.9 km/s ·
  Transit 180-270 gün · Pencere ~780 günde bir · Sinyal 3-22 dk" telemetri
  satırı, Türkçe tehlike etiketleri (Radyasyon %70 …) ve sağda **KİLİT
  KOŞULLARI** kontrol listesi — her koşul ✓/✗ ikonuyla; eksik teknoloji
  satırına tıklanınca ağaçta o düğüm seçilir ve denetçi paneli açılır.
- **Filtre etkileşimi:** hedef seçimi ağaçta o hedefin gerektirdiği düğümleri
  **amber çerçeveyle işaretler** (`circuit-node--required`), diğer kartları
  %28'e soluklaştırır; kart tooltip'ine "… · Mars görevi için gerekli" notu
  düşer. Dal çipleriyle **karşılıklı dışlama**: hedef seçimi dal vurgusunu,
  dal çipi hedef vurgusunu sıfırlar ("Tümü" yerine ikisi birden karışmaz).
  Aynı sekmeye ikinci tık filtreyi kaldırır.

### 2. Veri Düzeltmeleri
- `destinations.json` (iki kopya senkron): `unlockResearchIds` gerçek node
  ID'leriyle düzeltildi — Mars: `edl-heat-shield-mk2` + `prop-methane-reuse`;
  Venüs: `edl-heat-shield-mk2` (ADR-032'deki "Mk1 Venüs'te yanar" kuralıyla
  uyumlu); Europa: `power-rtg` + `comms-laser` + `edl-heat-shield-mk2`.
  Bu düzeltme `MissionComposer.DetermineRequiredResearch` profillerini de
  gerçek teknoloji adlarına bağlar.
- `locale_tr.json` / `locale_en.json` (dört kopya): eksik `DEST_*_NAME` /
  `DEST_*_DESC` anahtarları eklendi (Dünya Yörüngesi (LEO), Ay, Mars, Venüs,
  Europa + birer cümlelik Türkçe/İngilizce açıklamalar).
- Bilinen teknik borç: iki locale kopyası (143 `DEST` öncesi anahtar farkı,
  Resources kopyasında 132 ek launch/EDL anahtarı) **önceden sapmıştı**; bu
  ADR kapsamında birleştirilmedi, her iki kopyaya aynı anahtarlar eklendi.

### 3. ViewModel Katmanı
- Yeni `DestinationItemViewModel`: kayıt alanlarını sarar; `UpdateLockState`
  tamamlanmış araştırma ID seti + itibar ile `IsUnlocked`/`MissingResearchIds`
  hesaplar; `BuildLockReason`/`BuildTravelSummary`/`BuildHazardSummary`
  Türkçe özet üretir. Kilit yeniden hesaplaması her `Refresh()`'te
  (`RecomputeDestinations`) tekrarlanır — bir teknoloji tamamlanınca şerit ve
  kilit rozetleri anında güncellenir.
- `ResearchTreeViewModel`: `Destinations` + `SelectedDestination`
  observable'ları, `SetDestinations(records)`, `SelectDestination(id)`
  (toggle + dal filtresi sıfırlama), `SelectBranch` içinde hedef sıfırlama ve
  `LoadDefaultDestinations()` (Resources → dosya sistemi, hata → boş liste →
  şerit gizli). Misyon Panosu ekranındaki yükleme deseninin aynısıdır.

### 4. Tema Dosyası
`ThemeCircuit.uss`'e eklenen sınıflar: `.circuit-dest-tab(--active/--locked)`,
`.circuit-dest-coin`, `.circuit-dest-coin-veil` (kilitli perdesi),
`.circuit-node--required` (amber çerçeve, durum renklerini ezer).
`.circuit-dest-details` paneli inline stillerle kurulmuştur.

---

## Sonuçlar

- Oyuncu artık tek ekranda hem **8 dala** (nasıl) hem **5 görev hedefine**
  (neden) göre okur: "Mars'a gitmek istiyorum → EDL İsi Kalkanı Mk2 +
  Metan-LOX itkisi eksik → karta tıkla → araştırmayı başlat" akışı kesintisiz.
- Doğrulama: Unity 2022.3.62f3 batch modda derleme temiz; yeni
  `Tests/EditMode/UI/DestinationRailTests.cs` kayıt alanları, kilit hesabı,
  VM toggle/dışlama, grafik vurgusu ve **katalog bütünlüğü** (gerçek
  `research_tree.json` ID referansları) testlerini kapsar; bozuk referansı
  assert eden `MissionComposerTests` satırı yeni ID'ye güncellendi.
- Bilinen sınırlar: hedef şeridi kilit hesabını yalnızca Ar-Ge + itibardan
  okur (GDD §19.1'in "araştırma/itibar" kilidi); hedef bazlı görev
  açılışlarında ek koşul gerekirse `DestinationItemViewModel.UpdateLockState`
  genişletilir.

---

*Görev hedefi şeridi, ADR-066 devre şemasına oyuncunun "neden araştırıyorum?"
cevabını geri ekledi: teknoloji ağacı artık beş hedefin haritasına bağlı.*
