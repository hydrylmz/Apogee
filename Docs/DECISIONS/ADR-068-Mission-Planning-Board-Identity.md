# ADR-068: Misyon Tasarla Ekranı Kimliği — Görev Planlama Panosu / Uçuş Planı Dosyası (Planning Board Identity)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §5, §14.1 (#5), §14.2, §15.1; ADR-063 (Fiziksel Dünya UI Temaları), ADR-065 (Sözleşme Masası), ADR-066 (Devre Şeması), ADR-067 (Görev Hedefi Şeridi)

---

## Bağlam

ADR-063, her ekran için "fiziksel dünya nesnesi" kimliği mimarisini kurdu; ADR-064
üretimi montaj salonuna, ADR-065 sözleşmeleri dosya klasörüne, ADR-066/067 Ar-Ge
Ağacı'nı devre şemasına taşıdı. Sırada **Misyon Tasarla** vardı: kullanıcı
sıralamada Misyon'u Güneş Sistemi ve Hedefler'in önüne koymuştu.

Misyon Tasarla ekranı (`ScreenId.MissionComposer = 5`) bu karara kadar dört adımlı
bir sihirbazdı; adımlar düz "AI paneli" kartlarıyla (koyu mavi hardcoded paneller,
jenerik mavi vurgu) temsil ediliyordu. Görev planlamanın "uçuş planı dosyası
üzerinde çalışan bir misyon planlayıcısı" hissi veren hiçbir görsel dili yoktu;
hedef ve görev tipi kartlarında fotoğraf/atıf da yoktu. Ayrıca ekran, ad-hoc
"Menüye Dön" butonu yerine ortak `ScreenNavBar`'ı kullanmıyordu.

Kullanıcı konsept kararlarını onayladı: kimlik **"Görev Planlama Panosu"** —
koyu bir planlama masası üzerinde **kraft karton uçuş planı dosyası**; adımlar
klasör sekmeleri, hedef/görev tipi kartları pullu kraft form sayfaları, seçimler
kırmızı "DOSYA SEÇİLDİ" / "ŞEMATİK ONAYLI" damgaları, sağ kolon "MİSYON DOSYASI"
dossier paneli. Fotoğraflar NASA kamu malı arşivinden seçilecek (ADR-064/066/067
politikası).

---

## Alınan Kararlar

### 1. Konsept: Görev Planlama Panosu / Uçuş Planı Dosyası
**Konum:** `Screens/Composer/MissionComposerScreenController.cs`,
`Screens/Composer/PlanningBoardArt.cs`

Ekran; koyu planlama masası zemin + **kraft karton dosya** kimliği
(`.theme-planning-board`) ile yeniden yazıldı:

- **Masa arkaplanı (Painter2D):** `PlanningBoardBackdrop` koyu masa yüzeyi +
  dikey mantar pano dikişleri + dört köşede kırmızı push-pin. Dekoratif
  (`PickingMode.Ignore`), hiçbir tıklamayı bloklamaz.
- **Adım = klasör sekmesi:** 4 sihirbaz adımı (HEDEF → MİSYON TİPİ → BİLİMSEL
  AMAÇLAR → TAKVİM & BÜTÇE) `planning-tabbar` üzerinde klasör sekmeleri olarak
  çizilir; aktif sekme kraft zemin + kırmızı alt şerit, tamamlananlar mavi-mürekkep
  alt şerit alır (`planning-tab--active/--done`).
- **Kart = pullu kraft form sayfası:** hedef ve görev tipi kartları
  `planning-sheet` sınıfıyla kraft zeminli, koyu kenar bordürlü form sayfalarıdır;
  seçili sayfa kırmızı çerçeve + damga alır. Tüm etkileşimler (tıkla-seç,
  tooltip'ler, puan/maliyet pill'leri) aynen korunur.
- **Görev yaması rozeti (Painter2D):** `PlanningPatchBadge` — dış dikişli halka,
  lacivert iç disk, mavi yörünge yayı, tepe noktasında chevron ("Apogee" göndermesi)
  ve yörünge üzerinde gezegen noktası. Başlıkta ve dossier panelinde dekoratif
  olarak kullanılır.
- **Görev kapanışı = damga:** özet butonu "Uçuş Planını Damgala →" olarak
  yeniden adlandırıldı (etkileşim aynı: `FinalizeMissionCommand`).

### 2. NASA Fotoğraflı Görev Tipi Kartları
**Konum:** `Assets/Resources/Composer/Art/<type>.jpg` (5 dosya),
`mission_types.json` → `art` / `artCredit` alanları

- Her görev tipinin `MissionTypeRecord`'una `Art` (Resources yolu) ve `ArtCredit`
  (tek satır Türkçe kaynak atfı) alanları eklendi (ADR-066/067 kayıt deseni).
- 5 görsel NASA Image and Video Library'den seçildi, indirildi ve **tek tek
  görsel olarak doğrulandı**: MRO artist konsepti (PIA04241, orbiter), InSight
  konsepti (PIA19811, lander), Curiosity Namib Dünü oto-portresi (PIA20316,
  rover), OSIRIS-REx kapsül paraşüt drop testi (NHQ202308300001, sample-return),
  Apollo 11 mürettebat portresi (8903695, crewed). Atıflar
  `attribution_data.json` → `attr-nasa-composer-mission-art` girdisiyle kredi
  zincirine bağlandı (iki kopya senkron).
- Hedef kartları yeni indirme yapmadan ADR-067'deki `Destinations/Art` fotoğraflarını
  `DestinationRecord.Art/ArtCredit` üzerinden yeniden kullanır.
- Kart yapısı: solda fotoğraf slotu (`planning-photo`, `BackgroundSizeType.Cover`,
  altta "Kaynak: NASA — …" atıf şeridi), sağda ad + İNSANLI/ROBOTİK rozeti +
  güvenilirlik eşiği + zorunlu modül pill'leri. Foto yoksa "FOTO BEKLENİYOR"
  placeholder'ı (test ortamı asla asset varlığına bağımlı değildir).
- Foto yükleme ADR-054/063/064/066/067 deseni: `Resources.Load<Texture2D>` +
  statik sözlük cache + try/catch.

### 3. Amaçlar Adımı = Kontrol Listesi
Bilimsel amaçlar kraft kontrol listesi satırlarıdır (`planning-check-row`);
işaretli satır mavi-mürekkep sol şerit alır (`--checked`), köşeli parantez
göstergesi `[x]/[ ]` mürekkep mavisi olur. "Tümünü Seç"/"Temizle" ve amaç
tooltip'leri (kriter cümleli, `TooltipManipulator`) aynen korunur.

### 4. Dossier Paneli (Sağ Kolon)
`MissionClaimCardView` (canlı iddia kartı) korunur; altına koyu "MİSYON DOSYASI"
paneli (`planning-dossier`) gelir: hedef·tip rozeti, Δv çubuğu, rampa/eşik/seyir/
gecikme pill'leri, seçili amaçlar ve fırlatma penceresi satırı. Takvim adımındaki
fırlatma penceresi kartları kraft formlardır (`planning-window-form`), bütçe
hedefi butonları ve mürekkep çizgili misyon adı alanı (`planning-ink-field`)
aynen çalışır.

### 5. Tema Dosyası
**Konum:** `Styles/ThemePlanningBoard.uss` (`Theme.uss` import zincirine eklendi)

`.theme-planning-board` kök sınıfı `--color-*` token'larını koyu masa + kraft
paletiyle override eder: zemin `#1C2328`, panel `#232D34`, vurgu cyan → **mürekkep
mavisi** `#5F8FC4` (veri/başarı semantiği korunur), amber `#D9A24B`, damga
kırmızısı `#B23A2E`. Tema-özel token'lar: `--kraft-bg #C8AE84`, `--kraft-edge`,
`--ink-navy #26324A`, `--stamp-red`, `--stamp-green`, `--form-line`,
`--chart-blue`. Tema-özel sınıflar: `.planning-board-header`,
`.planning-hud-pill`, `.planning-tabbar`, `.planning-tab(--active/--done)`,
`.planning-sheet(--selected)`, `.planning-sheet-title/-meta/-muted/-ink`,
`.planning-photo(__caption/__fallback)`, `.planning-stamp(--selected/--info/--ok)`,
`.planning-check-row(--checked)`, `.planning-window-form(--selected)`,
`.planning-dossier(-header)`, `.planning-ink-field`, `.planning-empty`,
`.planning-patch`, `.planning-backdrop`. Painter2D sabitleri (`PlanningBoardArt`)
USS token'larıyla elle senkron tutulur (ADR-063 kısıtı).

### 6. Etkileşim Sözleşmesi Korundu
- `ScreenNavBar` (geri + 8 sekme + status slot) eklendi; ad-hoc "Menüye Dön"
  butonu kaldırıldı (`ViewModel.Navigator` zaten açıktı).
- 4 adım navigasyonu, tıkla-seç, amaç toggle, pencere seçimi, bütçe tavanı,
  misyon adı senkronizasyonu ve özet akışı birebir korunur; footer'daki
  Önceki/Sonraki/Damgala butonları ve `composer-*` ortak sınıfları yerinde kalır.

---

## Sonuçlar

- Tüm önceki etkileşimler korundu: sihirbaz adımları, hedef/görev tipi/amaç
  seçimleri, fırlatma penceresi ve bütçe tavanı, misyon adı, dossier önizleme
  ve özet akışı; ortak `ScreenNavBar` gezinmesi kazandırıldı.
- Veri katmanı: `mission_types.json` (iki kopya senkron) `art`/`artCredit`
  alanlarıyla zenginleşti; `MISSION_TYPE_*_NAME/DESC` locale anahtarlarının
  hiçbir kopyada bulunmadığı görüldü ve tr/en (4 kopya) tamamlandı — eksik
  anahtarlar UI'da "?key" yerine gerçek adları gösteriyordu.
- Doğrulama: Unity 2022.3.62f3 batch modda derleme temiz; yeni
  `Tests/EditMode/UI/MissionPlanningBoardTests.cs` tema yapısı (kök sınıf,
  navbar, backdrop, klasör sekmeleri, dossier), dekoratif sanatın picking'i
  bloklamaması, kraft kart damgaları, kontrol listesi toggle'ı, foto slotu
  zarif düşüşü ve katalog bütünlüğü (art yolları → gerçek dosyalar, locale
  anahtarları) testlerini kapsar. Tam EditMode koşusunda mevcut baseline
  dışında sıfır yeni hata.
- Kompozisyon: kraft/mürekkep paleti Sözleşmeler'in krem/ceviz ve Montaj
  Salonu'nun beton/amber kimliklerinden ayrışır; damga ve klasör dili
  ADR-065'in dosya metaforuyla akrabadır (konsanüstü okunabilirlik).
