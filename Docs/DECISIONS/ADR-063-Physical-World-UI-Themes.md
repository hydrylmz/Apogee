# ADR-063: Fiziksel Dünya Kimliği — Ortak UI Tema Mimarisi ve Ekran Şablonları (Physical World UI Themes)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §14, §15.1; ADR-054 (Painter2D / HangarRocketCanvas), ADR-062 (Uçuş Tiyatrosu 3B Sahne)

---

## Bağlam

APOGEE'nin tüm ekranları %100 programatik UI Toolkit ile (UXML yok) inşa
ediliyordu; her ekran kendi inline renk sabitlerini kullanıyor ve görsel dil
"AI paneli" hissi veriyordu: ortogonal kutular, jenerik mavi-vurgu, fiziksel
dünyaya hiçbir gönderme yoktu. Oyunun çekirdek vaadi (gerçek gezegen verisi,
gerçek roket mühendisliği) ile sunum katmanı arasında bir kimlik boşluğu vardı.

Hedef: her ekrana **fiziksel bir dünya nesnesi** kimliği vermek —

- **Üsler ekranı** → misyon kontrol radar konsolu (dönen tarama ışını, blip'ler,
  telemetri defteri, inşa sipariş fişleri),
- **Atölye ekranı** → mühendislik blueprint masası (Prusya mavisi kağıt, ölçü
  okları, kartela/title block, kırmızı kalem redline notları, onay damgası CTA).

Bunu yaparken: (1) mevcut tüm etkileşimler (slotlar, katmanlar, undo/redo,
1-9/E/Tab/Esc kısayolları, sesler) bozulmadan kalmalı, (2) ekran başına yeni bir
tema eklemek kopyala-yapıştır CSS gerektirmemeli, (3) kalan 6 ekran için
tekrarlanabilir bir şablon tanımlanmalı, (4) NASA verisi gösteren yerler
kaynak atfı satırıyla bitmelidir.

---

## Alınan Kararlar

### 1. Tema Mimarisi: USS Custom Property Inheritance
**Konum:** `Assets/_Project/Presentation/Styles/` — `Common.uss`, `Variables.uss`,
`ThemeRadar.uss`, `ThemeBlueprint.uss`

- Ortak omurga sınıfları (`apogee-panel`, `apogee-button`, `apogee-badge`,
  navbar, action-btn, confirm-dialog) `Common.uss`'te kalır ve renklerini
  yalnızca `var(--color-*)` token'larından tüketir.
- Her tema, `--color-*` token'larını override eden tek bir sınıf dosyasıdır:
  ekran köküne `.theme-radar` veya `.theme-blueprint` eklenmekle temada
  etkinleşir; UI Toolkit custom property'leri subtree'ye inherit ettiği için
  tüm ortak sınıflar otomatik yeniden renklenir. Tema başına sıfır yeni
  bileşen CSS'i yazmak mümkündür; tema dosyası yalnızca tema-özel yardımcı
  sınıfları (`.blueprint-redline`, `.blueprint-stamp-button` gibi) ekler.
- **Kanvas çizim renkleri C# sabitleridir** (Painter2D USS okuyamaz) ve temanın
  token değerleriyle birebir eşleştirilir (ör. `BlueprintGridColor` ↔
  `--bp-grid`). Senkron sorumluluğu her kanvasın sabit bloğundaki yorumlarla
  belgelenir.

### 2. İkon Sistemi: Programatik Painter2D Kütüphanesi
**Konum:** `Assets/_Project/Presentation/Core/Icons/` — `IconType.cs`,
`IconLibrary.cs`, `IconElement.cs`

- 8 ana ekran ikonu + 10 yardımcı ikon, 24 birimlik tasarım ızgarasında
  `IconLibrary.Draw(Painter2D, IconType, Rect)` ile çizilir; asset import'u,
  sprite atlas'ı veya vector dosyası gerekmez (WebGL bellek bütçesi korunur).
- `IconElement` (`generateVisualContent` + `Icon`/`IconSize`/`Color` özellikleri)
  tek yeniden kullanılabilir elemandır; `resolvedStyle.width` ile `style.width.value.value`
  (StyleLength → Length → float) farkına dikkat: çözümlü stil `float` döndürür,
  stil baştan `Length` sarmalar.

### 3. Ortak UI Primitifleri
**Konum:** `Core/Navigation/ScreenNavBar.cs`, `Core/Dialogs/ConfirmDialog.cs`,
`Core/Controls/ActionButton.cs`

- `ScreenNavBar`: her ekrana tek tip, ikonlu sekme çubuğu (8 ekran + geri +
  bir status slot'u). Etkileşim sözleşmesi: her butonda ikon + sonuç etiketi,
  geri alınamaz eylemler `ConfirmDialog` (public `Confirm()`/`Cancel()` —
  testler click simülasyonu olmadan çalışır), pasif butonlar "Reason"
  tooltip'i gösterir.
- `ActionButton`: ikon + etiket + maliyet/süre satırı + devre dışı gerekçesi;
  Üsler yükseltme ve ilerideki tüm maliyetli eylemler bu primitifi kullanır.

### 4. Ekran Şablonu A — Radar Konsolu (Üsler)
**Konum:** `Screens/Bases/RadarScopeView.cs`, `Screens/Bases/BaseManagementScreenController.cs`

- Kök: `.theme-radar`. Sol tarafta Painter2D radar: 4 halka, tik işaretleri,
  33 ms'de bir dönen süpürme ışını (repaint ≤30 Hz), blip'ler
  `regions.json`'daki **gerçek dünya koordinatlarından** equirectangular
  projeksiyonla yerleştirilir (CampaignMap pin verisiyle aynı kaynak).
  Hover tooltip + `RegionClicked` eventi.
- Sağda seçili bölge detay kartı: NASA fotoğrafı, NASA içerikten Türkçe
  açıklama, "Kaynak: …" atfı, seviye/kapasite barları, `ActionButton`
  Yükselt (maliyet + süre), `ConfirmDialog` onayı.
- Altta telemetri defteri (ledger satırları) ve inşa sipariş fişi kartları.

### 5. Ekran Şablonu B — Blueprint Masası (Atölye)
**Konum:** `Screens/Workshop/` — `WorkshopScreenController.cs`,
`HangarRocketCanvas.cs`, `HangarSectionRail.cs`, `HangarPartDrawer.cs`

- Kök: `.theme-blueprint`. `HangarRocketCanvas` kâğıt mavisi zemin + ölçü
  ızgarasına geçti; **dikey boy ölçü oku** (uzatma çizgileri + ok uçları,
  `≈ {HeightMeters} m` etiketi kamera pan/zoom animasyonuyla
  `UpdateMeasurementOverlays` üzerinden senkron) ve sol üstte **ölçü plakası**
  (Δv / ıslak kütle / boy-çap oranı) eklendi. Boy statısı ViewModel'de
  bulunmadığından oran `RocketVisualAnatomy.TotalHeightMeters /
  MaxDiameterMeters`'dan türetilir; Δv/kütle `SetMeasurements(deltaV, massTons)`
  ile canlı telemetri aboneliklerinden beslenir.
- Sağ altta mühendislik **kartelası** (`blueprint-title-block`): ajans satırı,
  blueprint adı (`BlueprintName`), tarih, rev sayacı (`Anatomy` değişiminde
  artar), çizim no/ölçek satırı; `pickingMode = Ignore` ile taramayı bloklamaz.
- Sol ray başlığı "PARÇA LİSTESİ (BOM)", çekmece başlığı "MODÜL KATALOĞU" +
  "SPEC:" kategori dili; mühendislik paneli satırları `blueprint-ledger-row`;
  hayalet delta kutusu `blueprint-redline` (kırmızı kalem revizyon notu —
  UI Toolkit kesikli border desteklemediğinden ince düz kırmızı kural);
  inceleme CTA'sı `blueprint-stamp-button` + "TASARIM İNCELEMESİ →".

### 6. NASA İçerik Zinciri
**Konum:** `Data/Catalog/Json/region_descriptions.json` (+ `Resources/Catalog/Json/`
mirror), `RegionDescriptionRecord.cs`, `attribution_data.json`

- 8 bölge için Türkçe açıklama + "Kaynak" atfı tek JSON kaynağında;
  `attribution_data.json`'a `attr-nasa-region-descriptions` girdisi eklenerek
  kredi ekranı zincirine bağlandı.

---

## Sonuçlar

- Tema eklemek artık tek dosyalık iş: `Common.uss`'i tüketen bir `.theme-x`
  token override'ı; kalan 6 ekran (Menü, Ayarlar, Kampanya Haritası, Sonuçlar,
  Misyon, Kredi) bu iki şablon + ortak primitiflerle ADR-064+ kayıtlarıyla
  genişletilecek (onay bekliyor).
- Atölyenin tüm mevcut etkileşimleri korundu: slot seçimi (ray/canvas/çekmece
  iki yönlü), 4 katman modu, undo/redo, 1-9/E/Tab/Esc kısayolları, hayalet
  önizleme, sesler. Üsler ekranı yeni nesil radar + kartela kimliğine taşındı.
- Painter2D kanvas sabitleri ile USS token'ları elle senkron tutulmalıdır —
  bilinen kısıt; otomatik eşleme (örn. temaları ScriptableObject'ten üretme)
  gelecek ADR konusudur.
- `IconElement.IconSize` getter'ı panel dışında (testlerde) `style.width.value.value`
  yolunu kullanır; layout çözülmeden `resolvedStyle` güvenilmezdir.
- Derleme Unity 2022.3.62f3 batch modda doğrulanmıştır; eski Faz 0/1a
  kodundaki API uyuşmazlıkları (PickingMode.Visible, IStyle.borderRadius/padding,
  FlexDirection.FlexEnd/Start, IVisualElementScheduledItem.Start) bu ADR'nin
  kapsamında düzeltilmiştir.

---

*Ortak tema mimarisi, ikon kütüphanesi ve radar/blueprint ekran şablonları bu ADR ile belgelenmiştir; kalan ekranlar onay sonrası aynı şablonlarla ADR-064+ altında kaydedilecektir.*
