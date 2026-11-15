# ADR-070: Hedefler Ekranı Kimliği — Madalya Panosu / Yama Duvarı (Medal Board Identity)

**Tarih:** 2026-11-15
**Durum:** Kabul Edildi
**İlgili:** GDD §4.4, §14.1 (#21 Hedefler), §14.2, §15.1; ADR-063 (Fiziksel Dünya UI Temaları), ADR-064–069 (ekran kimlikleri), ADR-067 (NASA Fotoğraf Politikası)

---

## Bağlam

ADR-063 mimarisiyle yedi ekran fiziksel dünya kimliklerine kavuştu; sıra
**Hedefler**'deydi (`ScreenId.MilestoneBoard = 21`). Kullanıcı konsept
kararını onayladı: kimlik **"Madalya Panosu"** — koyu meşe bir duvara asılı,
altın tel dikişli **derin yeşil keçe yama panosu**; kampanya kilometre
taşları kurdeleli madalya kartları olarak panoya serilir, tamamlanan
kilometre taşının "madalyası takılır". Sağ kolon, sonraki önerilen hedefi
duyuran pirinç çerçeveli bir **tören plaketi**dir. Fotoğraf politikası
ADR-064/066/067/068 ile aynı: NASA kamu malı arşivi.

Hedefler ekranı bu karara kadar fonksiyonel ama jenerik bir "AI paneli"
idi: hardcoded koyu mavi kartlar, fotoğrafsız/atıfsız kilometre taşı
satırları, ad-hoc "Geri Dön" butonu (ortak `ScreenNavBar` yok) ve yeşil
düz bildirim bandı.

---

## Alınan Kararlar

### 1. Konsept: Madalya Panosu / Yama Duvarı
**Konum:** `Screens/Milestones/MilestoneBoardScreenController.cs`,
`Screens/Milestones/MedalBoardArt.cs`

Ekran; meşe duvar + **keçe yama panosu** kimliği (`.theme-medal-board`)
ile yeniden yazıldı:

- **Yama panosu arkaplanı (Painter2D):** `MedalBoardBackdrop` — meşe
  duvar paneli dikey damar çizgileri, meşe çerçeveli asılı pano, çift
  altın dikiş bordürü (kesik çizgi segmentleriyle), dört köşede altın
  pim ve keçenin merkezinde büyük **pusula gülü nakışı** (8 kollu yıldız +
  kızıl kuzey ibresi). Dekoratif (`PickingMode.Ignore`), hiçbir tıklamayı
  bloklamaz.
- **Madalya rozeti (Painter2D):** `MedalPatchBadge` — altın halka, koyu
  keçe disk, beş kollu yıldız nakşi ve altından sarkan kızıl kurdele.
  Başlık tacında ve sağ plakette dekoratif olarak kullanılır.
- **Madalya kartı:** her kilometre taşı; üstte NASA foto slotu, başlık +
  durum kurdelesi, açıklama, ödül çipleri ve talep butonuyla
  `medal-card` olarak çizilir. Duruma göre kart zemini değişir:
  beklemede altın tören kartı (`--pending`), takılmış defne kartı
  (`--claimed`).
- **Madalya takma = talep:** özet butonu "Madalyayı Tak"; tamamlanan
  kilometre taşının kurdelesi "★ MADALYA TAKILDI" olur (etkileşim aynı:
  `ViewModel.ClaimMilestone(id)`).
- **Bildirim bandı = tören duyurusu:** yeşil keçe bandı
  (`medal-banner`) aynen çalışır — X ile kapatma dahil.

### 2. NASA Fotoğraflı Kilometre Taşı Kartları
**Konum:** `Assets/Resources/Milestones/Art/<id>.jpg` (8 dosya),
`milestones.json` → `art` / `artCredit` alanları

- Her kilometre taşının `MilestoneRecord`'una `Art` (Resources yolu) ve
  `ArtCredit` (tek satır Türkçe kaynak atfı) alanları eklendi
  (ADR-066/067/068 kayıt deseni).
- 8 görsel NASA Image and Video Library'den seçildi, indirildi ve **tek
  tek görsel olarak doğrulandı**: Explorer 1 afişi (PIA04601, ilk uydu),
  Ranger 7'nin Ay'ın ilk fotoğrafı (PIA02975, ilk Ay sondası), Lunar
  Orbiter III ile Surveyor 1 iniş bölgesi (LRC-1967-B701_P-04028, Ay'a
  iniş), Mariner sonda maketi (GRC-1964-C-70182, gezegenler arası sonda),
  üç nesil Mars gezgini maketi (PIA11436, Mars rover), Buzz Aldrin'in Ay
  yürüyüşü (AS11-40-5902, insanlı Ay), ilk Apollo 11 Ay örneği
  konteyneri (S69-45002, numune getirme) ve insanlı Mars keşfi konsepti
  (PIA23302, insanlı Mars). Atıflar `attribution_data.json` →
  `attr-nasa-milestone-art` girdisiyle kredi zincirine bağlandı (iki
  kopya senkron).
- Kart yapısı: üstte foto slotu (`medal-card-photo`,
  `BackgroundSizeType.Cover`, altta "Kaynak: NASA — …" atıf şeridi).
  Foto yoksa "FOTO BEKLENİYOR" placeholder'ı (test ortamı asla asset
  varlığına bağımlı değildir).
- Foto yükleme ADR-054/063/064/066/068/069 deseni:
  `Resources.Load<Texture2D>` + statik sözlük cache + try/catch.

### 3. Tema Dosyası
**Konum:** `Styles/ThemeMedalBoard.uss` (`Theme.uss` import zincirine eklendi)

`.theme-medal-board` kök sınıfı `--color-*` token'larını meşe + keçe
paletiyle override eder: duvar `#241B14`, panel `#27201A`, vurgu amber →
**altın tel** `#D4B05E`, cyan → **patina** `#5FA99A` (veri semantiği
korunur), yeşil defne `#4CAF6E`, kurdele kızılı `#A03A3A`. Tema-özel
token'lar: `--felt-bg/-deep`, `--felt-stitch`, `--felt-edge`,
`--oak-frame`, `--gold-thread`, `--ribbon-crimson/-blue`,
`--ink-embroidered`, `--embroidered-muted`. Tema-özel sınıflar:
`.medal-header`, `.medal-title/-subtitle`, `.medal-summary`,
`.medal-count`, `.medal-track(--fill)`, `.medal-banner(-text)`,
`.medal-board`, `.medal-board-scroll`, `.medal-card(--claimed/--pending)`,
`.medal-card-title(--claimed)/-desc`, `.medal-card-photo(__caption/
__fallback)`, `.medal-badge(--claimed/--pending/--available/--locked)`,
`.medal-chip`, `.medal-detail`, `.medal-detail-plaque`,
`.medal-section-label`, `.medal-goal-title/-desc`, `.medal-claim`,
`.medal-footer`, `.medal-backdrop`, `.medal-patch`. Painter2D sabitleri
(`MedalBoardArt`) USS token'larıyla elle senkron tutulur (ADR-063 kısıtı).

### 4. Etkileşim Sözleşmesi Korundu
- İki yapıcı imzası (`(IScreenNavigator)` ve `(MilestoneBoardViewModel,
  IScreenNavigator)`), `BindCampaign(CampaignState)` ve
  `OnShown(context as CampaignState)` akışı birebir korundu.
- `CompletedCountText`/`ProgressFraction` (altın dolgulu keçe track),
  `ActiveGoalTitleText`/`ActiveGoalDescriptionText` (plaket),
  `HasNotification`/`NotificationText` (tören bandı),
  `MilestoneItems.CollectionChanged` (kart yeniden kurulumu) ve
  `CanClaim`/`Status` abonelikleri yerinde kaldı.
- Ad-hoc "Geri Dön" butonu kaldırılıp alt bar ortak **`ScreenNavBar`**'a
  taşındı (`(_navigator, ScreenId.MilestoneBoard)`; geri + 8 sekme +
  status slot) — hiçbir etkileşim kaybolmadı.

---

## Sonuçlar

- Tüm önceki etkileşimler korundu: kampanya bağlama, ilerleme özeti,
  bildirim bandı, durum kurdeleleri, ödül çipleri, tek tıkla talep;
  ortak navbar gezinmesi kazandırıldı.
- Doğrulama: yeni `Tests/EditMode/UI/MedalBoardTests.cs` tema yapısı
  (kök sınıf, navbar, yama panosu, tac, plaket, footer), dekoratif
  sanatın picking'i bloklamaması, kampanyasız boş pano, 8 madalya
  kartı + foto slotları, gün-0 kurdele durumları (1 önerilen / 7
  kilitli), talep butonu görünürlüğü ve zarif hata yolu, kart stili
  durum tepkisi, plaket/bağlama testleri ve katalog bütünlüğünü (art
  yolları → gerçek dosyalar, önkoşullar → gerçek ID'ler, benzersiz
  sıra) kapsar.
- Kompozisyon: meşe/keçe/altın paleti Radar, Montaj Salonu, Planlama
  Panosu (kraft) ve Orrery (pirinç gece) kimliklerinden net ayrışır;
  yama/madalya dili misyon yaması rozeti (ADR-068) ve müze plaketleri
  (ADR-069) ile aynı arşiv-ceremonial ailesini tamamlar.
