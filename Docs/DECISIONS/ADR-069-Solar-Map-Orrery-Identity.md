# ADR-069: Güneş Sistemi Ekranı Kimliği — Planetaryum / Pirinç Astrolap (Orrery Identity)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §6.2, §14.1 (#4), §14.2, §15.1; ADR-063 (Fiziksel Dünya UI Temaları), ADR-064–068 (ekran kimlikleri), ADR-067 (Görev Hedefi Fotoğrafları)

---

## Bağlam

ADR-063 mimarisiyle altı ekran (Üsler, Atölye, Üretim, Sözleşmeler, Ar-Ge Ağacı,
Misyon Tasarla) fiziksel dünya nesnesi kimliklerine kavuştu; sıra **Güneş
Sistemi Haritası**'ndaydı. Kullanıcı konsept kararını onayladı: kimlik
**"Planetaryum / Pirinç Astrolap"** — koyu gece göğü zemininde pirinç
(pirinç metalden ovulmuş bir astrolap/gezegen makinesi) halkalar ve derece
tikleri; gezegen bilgisi ise **katalok ficheleri** (pirinç çerçeveli müze
plaketleri) olarak sunulur. Fotoğraf politikası ADR-064/066/067 ile aynı:
NASA kamu malı arşivi.

Güneş Sistemi ekranı (`ScreenId.SolarMap = 4`) bu karara kadar fonksiyonel ama
jenerik bir "AI paneli" idi: hardcoded koyu mavi HUD/pil renkleri, CSS border
daireleriyle taklit edilen yörünge halkaları (geniş tuvalde elipse bozulan,
işaretleyici konumlarıyla hizasız), ad-hoc navigasyon butonları ("Dünya
Haritası", "Misyon Bestecisi", "Tasarım Atölyesi", "Ana Menü") ve fotoğrafı/
atıfı olmayan brifing kartı.

---

## Alınan Kararlar

### 1. Konsept: Planetaryum / Pirinç Astrolap
**Konum:** `Screens/SolarMap/SolarMapScreenController.cs`,
`Screens/SolarMap/OrreryArt.cs`

Ekran; gece göğü zemin + **pirinç astrolap** kimliği (`.theme-orrery`) ile
yeniden yazıldı:

- **Astrolap plakası (Painter2D):** `OrreryBackdrop` — deterministik yıldız
  alanı (tohumlu `System.Random`, her yeniden çizimde aynı gökyüzü), merkeze
  soluk güneş parıltısı ve **halkalar + derece tikleri**. Halkalar, gezegen
  işaretleyicileriyle hizalı olsun diye `NormalizedX/Y` koordinat sistemiyle
  aynı olan **eksene bağlı elipsler** olarak çizilir (rx = kesir × genişlik,
  ry = kesir × yükseklik); kesirler `CelestialBodyItemViewModel.
  OrbitVisualRadiusPercent` bantlarıyla senkrondur (%14 Venüs, %25 LEO/Ay,
  %35 Mars, %44 Europa). En dış halkada 10°'de bir tik (30°'de uzun tik) ve
  ana yönlerde pirinç elmaslar vardır. Dekoratif (`PickingMode.Ignore`).
- **Eski CSS daireleri kaldırıldı:** yörünge halkaları artık Painter2D
  katmanında; güneş ve gezegen işaretleyicileri VisualElement olarak plaka
  üzerinde durur (tıklanabilirlik korunur).
- **Katalok fişi (müze plaketi):** sağ kolon, pirinç çerçeveli koyu plaket
  (`orrery-fiche`) olarak yeniden düzenlendi: "KATALOK NO: ORR-04" kazıma
  satırı, hedef adı (pirinç tonlu başlık), tip · AU, foto penceresi, transfer
  penceresi kartı, telemetri plaketi (Δv / seyir / gecikme) ve tehlike
  çipleri.

### 2. Katalok Fotoğrafları — Yeni İndirme Yok, Yeniden Kullanım
**Konum:** `destinations.json` → `art` / `artCredit` (ADR-067)

- Fiche foto penceresi, ADR-067'de indirilmiş **5 görev hedefi fotoğrafını**
  (`Resources/Destinations/Art/<id>.jpg`) `DestinationRecord.Art/ArtCredit`
  üzerinden yeniden kullanır: Galileo Dünya, LRO Ay, Viking Mars, Magellan
  Venüs, Galileo Europa. Yeni bir görsel indirilmedi; atıf zinciri tek
  girdide toplanır — `attr-nasa-destination-art` `usedInSystems` listesine
  **`SolarMapScreen`** eklendi (iki kopya senkron).
- Foto yoksa (test dışı harness) "FOTO BEKLENİYOR" placeholder'ı; foto varsa
  pencerenin altında "Kaynak: NASA — …" Türkçe atıf şeridi.
- Foto yükleme ADR-054/063/064/066/068 deseni: `Resources.Load<Texture2D>` +
  statik sözlük cache + try/catch.

### 3. Etkileşim Sözleşmesi Korundu
- Zaman kontrolleri (Duraklat/1x/2x/3x/+1G), gezegen seçme tıklamaları,
  transfer penceresi durum kartı, tehlike çipleri, aktif seyir listesi ve
  araç pinleri, "BU HEDEFE GÖREV TASARLA" akışı birebir korunur.
- Alt bar ortak **`ScreenNavBar`**'a taşındı (geri=Harita + 8 sekme + status
  slot; `StatusMessage` navbar status slot'una bağlandı); ekranın "Ana Menü"
  kısayolu navbar yanında ayrı buton olarak korundu — hiçbir etkileşim
  kaybolmadı. `SolarMapViewModel`'a ADR-066 deseninde `public IScreenNavigator
  Navigator` açıldı.

### 4. Tema Dosyası
**Konum:** `Styles/ThemeOrrery.uss` (`Theme.uss` import zincirine eklendi)

`.theme-orrery` kök sınıfı `--color-*` token'larını gece göğü + pirinç paletiyle
override eder: zemin `#0A0D18` (plaka `#070A12`), panel `#0F1524`, vurgu amber →
**pirinç** `#C9A25E` (yapısal/dekoratif), cyan `#4EC9E0` (pencere/veri semantiği
korunur), metin hafif sıcak yıldız ışığı `#F3EFE4`. Tema-özel token'lar:
`--orrery-ring/-faint/-bright`, `--orrery-tick`, `--orrery-star`, `--fiche-bg`,
`--fiche-edge/-deep`, `--engraved-ink/-muted`. Tema-özel sınıflar:
`.orrery-header`, `.orrery-hud-pill`, `.orrery-speed-btn(--active)`,
`.orrery-canvas`, `.orrery-sun(-label)`, `.orrery-planet(--selected)`,
`.orrery-planet-aura/-label`, `.orrery-craft-pin`, `.orrery-fiche(-header)`,
`.orrery-fiche-title/-sub`, `.orrery-fiche-photo(__caption/__fallback)`,
`.orrery-plaque(-row/-row__label)`, `.orrery-section-label`,
`.orrery-window-card`, `.orrery-window-badge(--open/--closed)`,
`.orrery-hazard-chip/-none`, `.orrery-mission-item`, `.orrery-empty`,
`.orrery-footer`, `.orrery-menubtn`, `.orrery-backdrop`. Painter2D sabitleri
(`OrreryArt`) USS token'larıyla elle senkron tutulur (ADR-063 kısıtı).

---

## Sonuçlar

- Tüm önceki etkileşimler korundu: HUD telemetri bağları, zaman kontrolü,
  gezegen seçimi, brifing kartı, tehlikeler, aktif misyonlar ve görev tasarlama
  yönlendirmesi; ortak navbar gezinmesi kazandırıldı, "Ana Menü" korundu.
- Doğrulama: yeni `Tests/EditMode/UI/OrreryMapTests.cs` tema yapısı (kök
  sınıf, navbar, astrolap plakası, fiche, pencere kartı, footer), dekoratif
  sanatın picking'i bloklamaması, 5 gezegen işaretleyicisi + konum
  normalizasyonu, fiche başlığı/seçim halkası takibi, foto atıf şeridi /
  placeholder, pencere rozeti açık/kapalı durumları, aktif seyir boş durumu,
  5 zaman kontrolü ve katalok bütünlüğünü (art/atıf → gerçek dosyalar,
  telemetri tutarlılığı) kapsar.
- Kompozisyon: pirinç/mürekkep paleti Ar-Ge'nin cyan PCB'sinden, Montaj
  Salonu'nun beton-amber'ından ve Misyon Panosu'nun kraft-kartonundan net
  ayrışır; müze plaketi dili katalok/arşiv metaforunu (Sözleşme klasörü,
  Planlama dosyası) tamamlar.
