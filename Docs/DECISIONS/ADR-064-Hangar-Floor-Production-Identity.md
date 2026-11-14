# ADR-064: Üretim Ekranı Kimliği — Montaj Salonu / VAB High Bay (Hangar Floor Identity)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §9.3, §14.1 (#11), §14.2, §15.1; ADR-063 (Fiziksel Dünya UI Temaları), ADR-054 (Painter2D / HangarRocketCanvas)

---

## Bağlam

ADR-063, her ekran için "fiziksel dünya nesnesi" kimliği mimarisini kurdu ve iki
şablon ekranı (Üsler → radar konsolu, Atölye → blueprint masası) onaylattı.
ADR-063'ün Sonuçlar bölümünde kalan ekranların onay sonrası ADR-064+ ile
genişletileceği taahhüt edilmişti; kullanıcı altı ekranın konseptlerini tek tek
onayladı ve sıralamayı **Üretim → Sözleşmeler → Ar-Ge → Misyon → Güneş Sistemi →
Hedefler** olarak belirledi.

Üretim ekranı (`ScreenId.ProductionQueue = 12`) bu karar öncesinde jenerik bir
"AI paneli" idi: solda düz bir `ScrollView` iş listesi, sağda hardcoded mavi
vurgulu hızlı aksiyon kutusu, fabrika/test kapasitesi düz metin. Ekranda
kullanıcıya "araç üreten bir fabrika katında olduğun" hissini veren hiçbir
görsel dil yoktu. Ayrıca NASA fotoğrafı gösterme taahhüdü (kullanıcının kendi
kaydedeceği `vab_assembly_01/02.jpg` — VAB'de teknisyenlerin araç üzerinde
çalıştığı kareler) için bir bağlantı noktası gerekiyordu.

---

## Alınan Kararlar

### 1. Konsept: Montaj Salonu / VAB High Bay
**Konum:** `Screens/Production/ProductionQueueScreenController.cs`,
`Screens/Production/AssemblyStandView.cs`

Ekran; beton zemin grisi + çelik iskele + endüstriyel **sarı güvenlik rengi**
(`.theme-hangar-floor`) kimliğiyle dört fiziksel bölgeye ayrıldı:

- **Montaj Standı (sol kolon):** seçili iş, duran araç gibi sunulur.
  `AssemblyStandView` (Painter2D) arka planda çelik iskele direkleri, yatay +
  çapraz payandalar, üç denetleme platformu, tavan vinci rayı + kanca, zemin
  güvenlik çizgisi çizer; önünde `VehicleSilhouetteView` tam fırlatma yığını
  olarak katmanlanır. İş `InProgress` iken periyodik kaynak kıvılcımı flicker'ı
  yanar (`SetWorking(bool)` + 260 ms `schedule.Execute` — planlanmış öğe zaten
  çalışır durumda döner, ayrı `Start()` yok).
- **İş Emri Defteri (sağ kolon):** her iş, sol kenarı vurgulu fabrika iş emri
  kartı; `#KOD · İSİM · durum damgası · gün/kapasite/maliyet metası · ilerleme
  çubuğu`. Hızlı iş emirleri `ActionButton` primitifiyle (ikon + maliyet + süre
  + Reason tooltip) korunur.
- **Konveyör Hattı (alt şerit):** kuyruktaki işler (`Queued` + `InProgress`),
  mini araç silüeti taşıyan palet kartları olarak yatay kaydırılabilir bantta.
- **Andon Lambaları (başlık):** fabrika/test kapasitesi, fabrika katı durum
  lambası dizisi olarak gösterilir (yeşil = boş, sarı = dolu, kırmızı = tam
  kapasite; `FactoryCapacityFraction`/`TestCapacityFraction` observer'ları).

### 2. Durum Dili: Damga (Stamp) Sistemi
`Queued → "KUYRUKTA"`, `InProgress → "ÜRETİMDE"`, `Paused → "DURAKLATILDI"`,
`Completed → "TAMAMLANDI"`, `Cancelled → "İPTAL EDİLDİ"`. Damgalar
`floor-stamp--{queued|inprogress|paused|completed|cancelled}` sınıflarıyla
renklenir ve `style.rotate = new Rotate(new Angle(-4f, AngleUnit.Degree))` ile
4° eğik "elle basılmış" görünüm alır (Rotate(Angle) ctor'u — float'tan iki
adımlı implicit dönüşüm derlenmez). VM'deki emoji önekli `StatusName`
("&#62; Üretimde") metinleri ekrana taşınmadı; controller'da temiz damga
eşlemesi yapıldı.

### 3. NASA İş Günlüğü Foto Kartı
**Konum:** `Assets/Resources/Production/Photos/vab_assembly_01.jpg`,
`vab_assembly_02.jpg` (kullanıcı sağlayacak; kamu malı NASA arşivi)

- Yalnızca `VehicleAssembly` işlerinde görünür; `Resources.Load&#60;Texture2D&#62;`
  + statik `PhotoCache` + try/catch ile yüklenir (ADR-054/063 deseni).
- İki küçük seçici (`.floor-thumb` / `.floor-thumb--active`) ana fotoğrafı
  değiştirir; dosya yoksa placeholder ("NASA FOTOĞRAFI BEKLENİYOR" + yol ipucu)
  zarifçe gösterilir — testler dosya varlığına bağımlı değildir.
- Foto kartında atıf satırı (`KAYNAK: NASA`) bulunmaz; foto dosyası projeye
  tam entegre edildiğinde `attribution_data.json`'a
  `attr-nasa-production-assembly` girdisi eklenerek kredi zincirine
  bağlanacaktır (iki kopya: `Data/Catalog/Json/` + `Resources/Catalog/Json/`).

### 4. Etkileşim Sözleşmesi Korundu
- `ScreenNavBar` (geri + 8 sekme + status slot) eklendi; eski ad-hoc "Geri"
  butonu kaldırıldı. VM, ADR-063 deseninde `public IScreenNavigator Navigator`
  özelliğini açığa çıkardı.
- İptal **geri alınamaz eylemdir** → `ConfirmDialog.Show(..., destructive: true)`
  ("...harcanan bütçe geri dönmez. Bu işlem geri alınamaz."). Kampanyasız
  ortamda `ViewModel.CancelJob` güvenle no-op döner.
- `SelectJobById(string)` / `RequestCancelJob(string)` public kancaları hem
  kart/palet tıklamaları hem UI testleri içindir.

### 5. Tema Dosyası
**Konum:** `Styles/ThemeHangarFloor.uss` (`Theme.uss` import zincirine eklendi)

`.theme-hangar-floor` kök sınıfı `--color-*` token'larını beton/çelik/amber
paletiyle override eder; tema-özel sınıflar: `.floor-stand`,
`.floor-photo-frame`, `.floor-thumb(--active)`, `.floor-workorder(--selected)`,
`.floor-conveyor`, `.floor-pallet(--selected)`, `.floor-stamp--*`,
`.floor-andon-lamp(--free/busy/full)`. Kanvas sabitleri (`AssemblyStandView`:
`SteelColor`, `SafetyColor`, `SparkColor`, `GroundColor`) USS token'larıyla
elle senkron tutulur (ADR-063 kısıtı devam eder).

---

## Sonuçlar

- Tüm önceki etkileşimler korundu: hızlı araç montajı/test komutları, bütçe ve
  kapasite canlı bağlamaları, durum mesajları, iş iptal akışı. Ekran ortak
  omurgaya (navbar, ActionButton, ConfirmDialog, tooltip) tam bağlandı.
- Doğrulama: Unity 2022.3.62f3 batch modda derleme temiz (4 adet
  `IStyle.borderRadius` kalıntısı köşe bazlı API'ye çevrildi); yeni
  `Tests/EditMode/UI/ProductionStandTests.cs` 7/7 geçti; tam EditMode koşusu
  **411/425** — önceki koşudaki (404/418) 14 baseline failure'a sıfır ek.
- Bilinen eksik: NASA foto dosyaları kullanıcı tarafından kaydedilecek; dosya
  düşene kadar placeholder görünür, attribution girdisi ondan sonra eklenir.
- Sıradaki ekran kimliği: **Sözleşmeler — Tedarikçi Dosyası/Klasör** (ADR-065).

---

*Üretim ekranı, ADR-063 şablonunu üçüncü kez uygulayarak "fabrika katı" kimliğine taşınmış; damga, andon ve konveyör dili eklenmiştir.*
