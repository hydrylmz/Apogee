# ADR-065: Sözleşmeler Ekranı Kimliği — Sözleşme Masası / Dosya Klasörü (Contract Desk Identity)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §9.1, §14.1 (#8), §14.2, §15.1; ADR-063 (Fiziksel Dünya UI Temaları), ADR-064 (Montaj Salonu), ADR-054 (Painter2D)

---

## Bağlam

ADR-063, her ekran için "fiziksel dünya nesnesi" kimliği mimarisini kurdu; ADR-064
Üretim ekranını "montaj salonuna" taşıdı ve sıradaki kimliği **Sözleşmeler —
Tedarikçi Dosyası/Klasör** ilan etti. Kullanıcı konsept kararlarını tek tek
onayladı: yerleşim **açık masa** (tüm bilgi tek bakışta, splitter'sız sabit
düzen), palet **ahşap masa + sıcak kağıt**, ürün fotoğrafları **teklif
mektuplarında**, imza akışı **özel imza popup'ı**.

Sözleşmeler ekranı (`ScreenId.Contracts = 9`) bu karar öncesinde jenerik bir
"AI paneli" idi: koyu mavi hardcoded paneller, ad-hoc "Geri" butonu, ikonsuz
teklif satırları ve diğer hiçbir ekranda bulunmayan resize splitter'lar.
Ekranda "kağıt üzerinde anlaşma imzalıyorsun" hissi veren hiçbir görsel dil yoktu.

---

## Alınan Kararlar

### 1. Konsept: Sözleşme Masası (Walnut Desk + Warm Paper)
**Konum:** `Screens/Contracts/ContractsScreenController.cs`,
`Screens/Contracts/ContractDeskArt.cs`, `Screens/Contracts/SignatureDialog.cs`

Ekran; koyu ceviz ahşap masa + sıcak krem kağıt + pirinç/brass + mühür kırmızısı
(`.theme-contract-desk`) kimliğiyle üç fiziksel bölgeye ayrıldı:

- **Tedarikçi Kartvizitleri (sol kolon):** her tedarikçi; marka renkli kenar
  şeridi + monogram rozeti + "İLİŞKİ %N" mini damgası olan krem kartvizit.
  Güçlü/zayıf/özellik satırları renkli mürekkep anahtarlarıyla; aktif sözleşme
  sayısı Contracts ikonuyla. Tıklama → `ViewModel.SelectCompany` (ADR-063'teki
  seçim deseni): kartvizit "SEÇİLİ" olur, o tedarikçinin teklif mektupları
  pirinç çerçeveyle vurgulanır (veri saklanmaz, yalnız odak).
- **Gelen Teklif Mektupları (sağ üst):** her teklif; solda ürün foto slotu,
  sağda "TEKLİF MEKTUBU" kağıt başlığı + kalem adı + kategori/güvenilirlik
  metası + `ActionButton` imza sözleşmesi ("İmzala · 25 M$ · 15 gün",
  `IconType.Sign`). Bütçe yetmezse buton pasifleşir ve nedenini tooltip ile
  söyler (etkileşim sözleşmesi #3).
- **Sözleşme Arşivi (sağ alt):** imzalanan sözleşmeler; manila dosya kartı,
  sekmede `DOSYA #CTR-...` referansı, gövdede kalem/tedarikçi/maliyet/hedef
  tarihi ve 4° eğik **durum damgası** ("TESLİM BEKLENİYOR" amber,
  "TESLİM EDİLDİ" yeşil, "İPTAL EDİLDİ" kırmızı + soluk dosya).

### 2. İmza Akışı: Kağıt İmza Popup'ı
**Konum:** `Screens/Contracts/SignatureDialog.cs`

İmza tek tık değildir: `ActionButton` → `SignatureDialog.Show(...)` açılır.
Diyalog bir sözleşme kağıdıdır: başlık "TEDARİK SÖZLEŞMESİ" + REF no, şart
listesi (tedarikçi/kalem/maliyet/teslim/güvenilirlik), açık sonuç maddesi
("İmzalandığı anda maliyet bütçeden düşülür... iptal edilebilir: tutarın
yarısı iade edilir, ilişki puanı düşer"), `SignStrokeView` (Painter2D kıvrımlı
imza) + imza çizgisi + `WaxSealView` (Painter2D tırtıklı kırmızı mühür +
beş köşeli yıldız). Onay butonu "İmzala ve Mühürle · {maliyet}" — bütçe
yetmiyorsa pasif + neden tooltip'li. Escape = Vazgeç, Enter = İmzala
(`ConfirmDialog` klavye sözleşmesiyle aynı). Backdrop tıklaması vazgeçir.

### 3. İptal Akışı: Açık Sonuçlu ConfirmDialog
İptal **yarı geri alınabilir** eylemdir (`CancelContractCommand`: %50 iade,
%5 ilişki cezası): `ConfirmDialog.Show(..., destructive: true)` tam sonuç yazar —
"Ödenen X M$'ın yarısı (Y M$) bütçeye iade edilir; ilişki puanı %5 düşer."
Onay etiketi de sonucu taşır: "İptal Et · Y M$ iade". Kampanyasız ortamda
`ViewModel.CancelContract` güvenle no-op döner.

### 4. Ürün Foto Slotları (Drop-in)
**Konum:** `Assets/Resources/Contracts/Photos/offer_{engine|tank|cabin|structure|avionics|science}.jpg`
(kullanıcı sağlayacak; NASA kamu malı donanım kareleri önerilir)

- Teklif kategorisi → kaynak yolu eşlemesi (`GetOfferPhotoResource`);
  `Resources.Load<Texture2D>` + statik `PhotoCache` (CampaignMap/ADR-054 deseni).
- Dosya yokken placeholder ("ÜRÜN FOTOĞRAFI BEKLENİYOR" + tam yol ipucu) zarifçe
  görünür; dosya düşünce caption şeridi ("... · Kaynak: NASA") eklenir.
  Testler dosya varlığına bağımlı değildir (placeholder veya caption iddiası).
- İmza diyaloğu da aynı fotoğrafı küçük önizleme + caption tooltip ile taşır.

### 5. Durum Dili ve Temizlenmiş StatusName
Damga metinleri controller'da eşlenir; VM'deki glyph önekli `StatusName`
("OK Teslim Edildi" / "X İptal Edildi") temizlendi → "Teslim Edildi" /
"İptal Edildi" (ADR-064'ün "emoji öneki ekrana taşınmaz" kararının aynısı).
HUD risk etiketi metne göre renklenir (KRİTİK/YÜKSEK kırmızı, ORTA amber,
düşük/yeşil-yeşil).

### 6. Etkileşim Sözleşmesi Korundu
- `ScreenNavBar` (geri + 8 sekme + status slot) eklendi; ad-hoc "Geri" ve
  footer status satırı kaldırıldı; status mesajı navbar status slot'una bağlandı.
  VM, ADR-063/064 deseninde `public IScreenNavigator Navigator` açığa çıkarıldı.
- Tüm etkileşimli öğelerde hover tooltip'i: kartvizit (ilişki açıklaması),
  teklif mektubu imza butonu (ActionButton sonuç tooltip'i), foto slotu
  (caption), arşiv dosyası (durum + iptal koşulları), HUD hapları.
- `OpenSignatureDialog(offer)` / `ConfirmCancelContract(contract)` public
  kancaları hem kart tıklamaları hem UI testleri içindir (ADR-064
  `RequestCancelJob` deseni).

### 7. Tema Dosyası ve Masa Dokusu
**Konum:** `Styles/ThemeContracts.uss` (`Theme.uss` import zincirine eklendi)

`.theme-contract-desk` kök sınıfı `--color-*` token'larını ceviz/krem/pirinç
paletiyle override eder (ortak omurga — navbar, badge, button, ActionButton —
token'lar üzerinden otomatik uyum sağlar); tema-özel sınıflar: `.paper-sheet`,
`.paper-title/-meta/-muted/-brass/-empty`, `.contract-bizcard(--selected)`,
`.contract-bizcard__mark`, `.contract-offer-letter(--focused)`,
`.contract-offer-photo(__placeholder/__caption)`, `.contract-folder(--cancelled)`,
`.contract-folder__tab`, `.contract-stamp--{pending|fulfilled|cancelled}`,
`.contract-rel-stamp--{good|mid|bad}`, `.contract-hud-pill`,
`.contract-column-board/-header`, `.contract-sign-*`.
`ContractDeskBackdrop` (Painter2D) masayı çizer: dikey tahta derzleri, dalgalı
tane çizgileri (koyu + açık), boğum elipsleri; `pickingMode = Ignore` ile hiçbir
tıklamayı yakalamaz. Kanvas sabitleri USS token'larıyla elle senkron tutulur
(ADR-063 kısıtı).

---

## Sonuçlar

- Tüm önceki etkileşimler korundu: sözleşme imzalama, iptal, bütçe/risk/aktif
  sayaç bağlamaları, durum mesajları, teklif listesi üretimi. Ekran ortak
  omurgaya (navbar, ActionButton, ConfirmDialog, tooltip) tam bağlandı;
  splitter'lar kaldırıldı (diğer ekranlarla tutarlı sabit 52/48 sağ kolon).
- Doğrulama: `Tests/EditMode/UI/ContractsDeskTests.cs` — tema/nav/yapı,
  kartvizit damga+seçim, teklif mektubu foto slotu + imza sözleşmesi + bütçe
  pasif nedeni, imza diyaloğu akışı (bütçe kesintisi + arşive dosyalama +
  damga), bütçesiz onay pasifleşmesi, iptal diyaloğu açık sonuç + İPTAL damgası,
  temiz StatusName. Derleme doğrulaması, Unity Editor açıkken batch mod kilitli
  olduğu için kullanıcının editor konsolunda yapılır.
- Bilinen eksik: ürün foto dosyaları kullanıcı tarafından kaydedilecek
  (6 kategori); dosya düşene kadar placeholder görünür, attribution girdisi
  ondan sonra `attribution_data.json`'a eklenir (ADR-064 deseni).
- Sıradaki ekran kimliği: **Ar-Ge Ağacı — Devre Şeması** (ADR-066).

---

*Sözleşmeler ekranı, ADR-063 şablonunu dördüncü kez uygulayarak "sözleşme masası" kimliğine taşınmış; kartvizit, mektup, dosya ve mühür dili eklenmiştir.*
