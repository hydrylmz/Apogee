# ADR-066: Ar-Ge Ağacı Ekranı Kimliği — Devre Şeması / PCB Teknoloji Ağacı (Circuit Board Identity)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §9.2, §14.1 (#10), §14.2, §15.1; ADR-063 (Fiziksel Dünya UI Temaları), ADR-064 (Montaj Salonu), ADR-065 (Sözleşme Masası)

---

## Bağlam

Ar-Ge Ağacı ekranı (`ScreenId.ResearchTree = 10`) düz bir `ScrollView` liste
idi: her düğüm tek satır, inline renkler, ağaç yapısı (ön koşul zincirleri,
dal kimliği) hiç görünmüyordu. Oyuncu hangi teknolojinin neye bağlandığını,
hangi dalın ilerlediğini göremiyordu; HOI4/Paradox tarzı bir "teknoloji
ağacına bakma" deneyiminin tamamı eksikti.

Kullanıcı referans olarak HOI4 teknoloji ağacı ekran görüntüsünü verdi:
karanlık zeminde dallara ayrılmış fotoğraflı kartlar, kilitli ağaçlar silüet
kadar karanlık, tamamlananlar aydınlanıyor. Ek görsel politika kararı:
her araştırma kartına uygun bir fotoğraf konacak ve fotoğraflar NASA kamu
malı arşivinden seçilecek (kullanıcı "NASA fotoğrafları" seçeneğini onayladı).

---

## Alınan Kararlar

### 1. Konsept: Devre Şeması / PCB
**Konum:** `Screens/Research/ResearchTreeScreenController.cs`,
`Screens/Research/ResearchTreeGraphView.cs`

Ekran; koyu PCB yeşili-laciverti zemin + **cyan bakır iz** kimliği
(`.theme-circuit`) ile HOI4 tarzı **görsel ağaç** olarak yeniden yazıldı:

- **Dal = yatay swim-lane:** 8 dal (ADCS → Haberleşme → EDL → Yaşam Desteği →
  Güç → İtki → Güvenilirlik → Yapı) sabit `LaneOrder` ile alt alta istiflenir;
  her lane'in solunda büyük harf dal başlık şeridi vardır. Veri modelinde x/y
  YOKTUR; konumlar `ResearchTreeLayout.Solve` tarafından
  `branch` + `tier` + `prerequisites`'tan hesaplanır (deterministik longest-path
  layering; aynı girdi → aynı çıktı, test edilir).
- **Derinlik = kolon:** ön koşul zincirindeki en uzun yol node'un kolonunu
  belirler (mevcut katalog: 3 kolon). Kartlar `NodeWidth×NodeHeight=168×140`
  hücrelere oturur; aynı (dal, kolon) çiftine düşen kartlar dikey istiflenir.
- **Connector katmanı (Painter2D):** ince devre grid'i, lane bantları ve ön
  koşul **izleri (traces)** — uçlarda lehim pedleri. İz rengi ağaç ilerlemesini
  kodlar: `dim` (kilitli) → `amber` (kaynak tamam, hedef açık) → `cyan`
  (araştırılıyor) → `yeşil` (iki uç tamam).
- **Dal çipleri filtre değil vurgu:** tıklanan dal parlaklaşır, diğer lane'ler
  %28 opaklığa düşer ("Tümü" sıfırlar). Zoom ± (%55–%130) `transform: scale`
  ile kanvas üzerinde uygulanır; scroll ve seçim korunur.

### 2. NASA Fotoğraflı Düğüm Kartları — Karanlık Kilitleme
**Konum:** `Assets/Resources/Research/Art/<nodeId>.jpg` (22 dosya),
`research_tree.json` → `art` / `artCredit` alanları

- Her düğümün `ResearchNodeRecord`'una `Art` (Resources yolu) ve `ArtCredit`
  (tek satır Türkçe kaynak atfı) alanları eklendi; item VM'e aynalanır.
- 22 görsel NASA Image and Video Library'den (`images-assets.nasa.gov`)
  seçildi, indirildi ve **tek tek görsel olarak doğrulandı** (yanlış eşleşenler
  değiştirildi). Atıflar `attribution_data.json` → `attr-nasa-research-art`
  girdisiyle kredi zincirine bağlandı (iki kopya senkron:
  `Data/Catalog/Json/` + `Resources/Catalog/Json/`).
- Kart yapısı: üstte fotoğraf (`BackgroundSizeType.Cover`) + T{tier} rozeti +
  durum ikonu çipi; altta isim + maliyet/süre şeridi. Kilitli kartlar HOI4
  silüet hissiyle **karanlık** kalır (`--locked`: koyu zemin, kararmış yazı,
  foto üzerinde %78 karartma perdesi); açık kartlarda perde incelir, tamamlan
  kartta kalkar. `InProgress` kartta alt kenarda cyan ilerleme çubuğu vardır.
- Foto yükleme ADR-054/063/064 deseni: `Resources.Load<Texture2D>` +
  sözlük cache + try/catch (test ortamında null → vektör ikon fallback).

### 3. Denetim Paneli (Inspector)
Sağ kolon (320px): seçili düğümün fotoğrafı + atıf satırı, ad, dal/aşama,
durum (renkli), açıklama, maliyet/süre/ön koşul/açılan modül satırları ve
`ActionButton` ("Araştırmayı Başlat" — fiyat/süre segmentleri, bütçe yetersiz
sebebi, kilitli/açık/araştırılıyor/tamamlandı ayrımı). Aktif araştırma şeridi
üstte ilerleme çubuğu + **geri alınamaz** iptal akışı
(`ConfirmDialog.Show(RootElement, ...)`) ile korunur.

### 4. Tema Dosyası
**Konum:** `Styles/ThemeCircuit.uss` (`Theme.uss` import zincirine eklendi)

`.theme-circuit` kök sınıfı `--color-*` token'larını koyu PCB + cyan iz
paletiyle override eder; tema-özel sınıflar: `.circuit-console-header`,
`.circuit-active-strip`, `.circuit-branch-bar`, `.circuit-chip(--active)`,
`.circuit-inspector`, `.circuit-zoom`, `.circuit-graph`,
`.circuit-lane-label`, `.circuit-node(--locked/available/inprogress/
completed/selected)`, `.circuit-node__art-veil`, `.circuit-node__name`,
`.circuit-node__meta`, `.circuit-credit`. Painter2D sabitleri
(`ResearchTreeConnectorLayer`: `TraceDim/Ready/Active/Done` vb.) USS
token'larıyla elle senkron tutulur (ADR-063 kısıtı).

### 5. Etkileşim Sözleşmesi Korundu
- `ScreenNavBar` (geri + 8 sekme + status slot) eklendi; VM'de
  `public IScreenNavigator Navigator` açık.
- `SelectNode(string)` kart tıklamalarının ve UI testlerinin ortak kancası;
  VM'de zaten vardı. RP/KP/bütçe pill'leri observable'lara bağlı.

---

## Sonuçlar

- Tüm önceki etkileşimler korundu: araştırma başlatma/iptal, dal seçimi,
  durum mesajları, navbar gezinmesi. Eski düz liste ekranı tamamen kaldırıldı.
- Doğrulama: Unity 2022.3.62f3 batch modda derleme temiz (`IStyle.fontStyle`,
  `borderWidth` shorthand ve Button→Label cast kalıntıları düzeltildi; kart
  ctor'una eksik ilk `UpdateState` çağrısı eklendi). Yeni
  `Tests/EditMode/UI/ResearchTreeUITests.cs` **23/23 geçti**; tam EditMode
  koşusu 418 test / 399 geçti — 15 benzersiz hata mevcut baseline
  (ResultsPanel, LaunchCapacity, CampaignTime, Production, ContractsDesk vb.)
  olup bu değişikliklerden sıfır yeni hata eklenmiştir.
- Bilinen sınırlar: katalog 22 düğüm/3 kolona sığar; ileride büyürse lane
  başına satır istifleme zaten desteklidir. Locale'de
  `research.branch.*` anahtarları (tr/en, dört kopya) doğrulandı; eksikse
  dal adı fallback döner.

---

*Ar-Ge Ağacı, ADR-063 şablonunu dördüncü kez uygulayarak "devre şeması"
kimliğine taşınmış; teknoloji ağacı artık bakıldığında okunan bir haritadır.*
