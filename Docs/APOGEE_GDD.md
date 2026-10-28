# APOGEE — Game Design Document (GDD) & AI Çalışma Rehberi
Sürüm 1.0 · NASA Space Apps 2026 · "Space Mission Design Game" · Unity 2022.3 LTS · 2D Vektörel

> **Bu doküman iki amaçla yazıldı:**
> 1. Takımın oyun tasarım referansı (konsept, mekanikler, içerik, UI).
> 2. **Kod yazan bir yapay zekâya (AI) verilecek tek doğruluk kaynağı** (prompt + rehber + mimari + yapılacaklar).
>
> Bu dokümanda **kod yoktur**; yalnızca tasarım, kurallar, veri şemaları (alan listesi olarak), formül tanımları (matematiksel/sözel) ve görev listesi vardır. Uygulamayı AI ya da geliştirici yapar.

---

## İÇİNDEKİLER
0. AI Kullanım Rehberi (Prompt + Çalışma Kuralları)
1. Proje Özeti ve Yarışma Gereksinimleri Eşlemesi
2. Tasarım Sütunları ve Kararlar
3. Oyun Konsepti
4. Kampanya Yapısı
5. Misyon Besteleyici (Oyuncunun Kendi Görevini Tasarlaması)
6. Strateji Haritası (HOI4 Tarzı)
7. Tasarım Atölyesi (Roket ve Uzay Aracı)
8. Hesaplama Modeli (Arka Plan Gerçek, Arayüz Sade)
9. Şirketler, Araştırma, Üretim, Zaman
10. Operasyon, Olaylar, Sonuç Raporu
11. Skor ve İlerleme
12. Çok Oyunculu (Online) Tasarımı
13. Eğitim ve Geri Bildirim Katmanı
14. UI / UX Spesifikasyonu
15. Sanat Yönü (2D Vektörel) ve Ses
16. İçerik Kataloğu (Başlangıç Değerleri)
17. Teknik Mimari
18. Tech Stack
19. Veri Şemaları (Alan Listeleri)
20. Test, Kalite ve Denge Stratejisi
21. Yapılacaklar (Backlog: Epic → Görev → Kabul Kriteri)
22. Riskler
23. Sözlük
24. Kaynaklar ve Atıflar

---

# 0. AI KULLANIM REHBERİ

## 0.1 AI'ya Verilecek Ana Sistem Prompt'u (kopyala-yapıştır)

```
Sen APOGEE adlı Unity 2022.3 LTS tabanlı 2D uzay misyonu tasarım/yönetim oyununu geliştiren kıdemli bir oyun mühendisisin.
Tek doğruluk kaynağın APOGEE_GDD.md dosyasıdır. Bir kararla çelişen bir şey yapman gerekiyorsa DUR ve önce kullanıcıya sor.

Değiştirilemez ilkeler:
1. Simülasyon/hesaplama mantığı UnityEngine'e bağımlı olmayan saf C# (Apogee.Core, Apogee.Design, Apogee.Strategy) içinde yaşar. Unity sadece görüntü, girdi ve ses katmanıdır.
2. Oyun durumu tek bir CampaignState nesnesidir; değişiklikler sadece Command'lar ile yapılır (undo, online ve kayıt buna dayanır).
3. Simülasyon deterministiktir: tohumlu RNG, sabit zaman adımı, System.Random ve DateTime.Now yasak.
4. İçerik (bileşen, şirket, olay, hedef, araştırma) kodda değil veri dosyalarındadır (ScriptableObject + JSON). Yeni içerik eklemek kod değişikliği gerektirmemeli.
5. Hesaplamalar gerçek fizik temellidir ama oyuncuya ham formül gösterilmez; oyuncu seçimlerinin SONUÇLARINI sade dille görür (bkz. GDD §8, §13).
6. Hedef platform WebGL'dir. Thread, System.IO dosya erişimi, blocking çağrı ve platforma özgü eklenti kullanma; WebGL'de çalışmayan bir şey önerme.
7. C# 9 ve Unity 2022.3 sınırlarında kal. Unity 6 özelliklerini (runtime data binding vb.) kullanma.
8. UI Unity UI Toolkit (runtime) ile yapılır. Veri bağlama yok; ViewModel + olay tabanlı elle güncelleme kullan.
9. Sanat vektörel 2D'dir. Gerçek marka/şirket adı, telifli görsel/ses kullanma. Şirketler kurgusaldır.
10. Sayısal değerleri uydurma: katalog değerleri GDD §16'dadır; yoksa "TODO-VERİ" olarak işaretle ve kullanıcıya sor.

Çalışma şekli:
- Her görevde önce GDD'deki ilgili bölümleri ve görev kabul kriterlerini özetle, sonra planı yaz, sonra uygula.
- Küçük, test edilebilir adımlarla ilerle. Her saf C# mantığı için EditMode testi yaz.
- Mevcut mimariyi bozan değişiklik önermeden önce gerekçeni açıkla.
- Görev bitince: ne yapıldı, hangi dosyalar, hangi testler, açık sorular, bir sonraki mantıklı görev.
- Emin olmadığın tasarım kararını varsayma; "Açık Sorular" başlığıyla listele.
- Kullanıcıyla Türkçe konuş; kod, tanımlayıcı ve yorum dili İngilizce olsun.
```

## 0.2 Görev Başına Prompt Şablonu

```
Görev: <Görev ID ve adı, GDD §21'den>
Bağlam: GDD §<ilgili bölümler>
Kapsam dışı: <yapılmayacaklar>
Kabul kriterleri: <GDD'deki maddeler>
Çıktı beklentisi: değişen/eklenen dosya listesi + testler + kısa özet + açık sorular
Kısıt: Mimari ilkeler (GDD §0.1) geçerlidir.
```

## 0.3 AI İçin Altın Kurallar
- **Önce Core, sonra görüntü.** Bir mekanik, UI'ye bağlanmadan Core'da testlerle çalışır olmalı.
- **Tek yol:** Oyun durumunu değiştiren her şey Command'dır. UI doğrudan durumu değiştirmez.
- **Veri odaklı:** Sihirli sayı yok. Katsayılar ayar dosyalarında.
- **Açıklanabilirlik:** Her hesaplanan istatistik "neden bu değer" kaynak listesi üretebilmelidir (oyuncuya sade, geliştiriciye ayrıntılı).
- **WebGL ilk gün test edilir.** Yeni bir paket/özellik eklenince WebGL build ile doğrula.
- **Sınır tanı:** Kapsam şişmesi yok. Backlog'da olmayan özelliği eklemeden önce sor.

---

# 1. PROJE ÖZETİ VE YARIŞMA EŞLEMESİ

**Oyun adı (çalışma):** APOGEE
**Tür:** Uzay ajansı yönetimi + roket/uzay aracı tasarımı (HOI4 tarzı strateji + tasarım atölyesi)
**Platform:** WebGL (öncelikli), Windows (yedek)
**Oyuncu:** 1-4 (tek kişi veya takım)
**Hedef kitle:** Beginner/Youth ve Intermediate; sade arayüz, gerçek arka plan hesapları.

## 1.1 Yarışma Gereksinimleri → Oyundaki Karşılığı

| Yarışma beklentisi | APOGEE'deki karşılığı | Bölüm |
|---|---|---|
| Misyon hedefi tanımlama | Misyon Besteleyici: hedef + tip + bilim amaçları | §5 |
| Gereksinimler | Hedef×Tip'ten otomatik türeyen gereksinim listesi | §5.4 |
| Uzay aracı tasarımı | Tasarım Atölyesi (bus, güç, haberleşme, ısı, yönelim, EDL) | §7 |
| Bilimsel alet seçimi | Alet kataloğu, kütle/güç/veri/maliyet/değer | §16.5 |
| Fırlatıcı ve yörünge | Roket tasarımı + yörünge/hedef profili + rampa uyumu | §7, §6 |
| Bütçe/kütle/güç/veri/haberleşme/süre | 6 çekirdek kaynak | §8 |
| Kaynak yönetimi | Kampanya bütçesi, üretim kapasitesi, zaman | §4, §9 |
| Takım modu (≤4) | 4 rol + ortak komuta masası + bot yedek | §12 |
| Simülasyon ve değerlendirme | Operasyon simülasyonu + neden-sonuç raporu + skor | §10, §11 |
| Zorluk/senaryo/rastgele olay | Kampanya zorluğu + misyon iddia katsayısı + olay havuzu | §4, §10 |
| Eğitim değeri | Sonuç odaklı geri bildirim, sözlük, tutorial | §13 |
| Kaydet/paylaş/karşılaştır | Misyon kodu, tasarım şablonları, karşılaştırma ekranı | §17 |
| Sandbox | Kısıtsız mod (Backlog: opsiyonel) | §21 |
| NASA verisi | Atıflı katalog + (opsiyonel) canlı API'ler | §24 |

---

# 2. TASARIM SÜTUNLARI VE KARARLAR

## 2.1 Sütunlar
1. **Her karar bir bedel öder.** Bedelsiz seçenek yok; bedel oyuncuya görünür.
2. **Tek doğru çözüm yok.** Aynı hedef birden fazla mimariyle başarılabilir.
3. **Sonuçlar görünür, formüller değil.** Oyuncu "ne olduğunu ve neden" görür; denklemleri değil.
4. **Başarısızlık öğretir.** Kayıp, sebep-sonuç zinciriyle açıklanır.
5. **Oyuncu kendi görevini yaratır.** Hazır görev listesi değil, oyuncunun beste yaptığı görev vardır.
6. **Strateji hissi.** Harita, zaman, olay, üs, araştırma: HOI4 tadı.

## 2.2 Kesinleşmiş Kararlar

| Konu | Karar |
|---|---|
| Süre/kapsam | Önemsiz; kilometre taşları ile ilerlenir |
| Oyun modu | **Kampanya** (yıllar boyu); ek olarak "tek misyon" kısayolu (aynı sistem) |
| Görev yapısı | **Oyuncu kendi görevini tasarlar** (Misyon Besteleyici). Hazır görev şart değil |
| Mühendis modu | **Yok.** Tek mod; hesaplar arkada gerçek, arayüz sade |
| Başlangıç | **Kurgusal ajans**, ülke seçimi yok |
| Online | En azından gösterişli: gerçek minimal online + bot yedek (§12) |
| Unity | 2022.3 LTS |
| Sanat | 2D, **vektörel**, takım içinde üretilir |
| Dil | UI: TR + EN (yerelleştirme dosyası); kod/tanımlayıcı: EN |
| Platform | WebGL öncelikli |

---

# 3. OYUN KONSEPTİ

## 3.1 Tek Cümle
Kurgusal bir uzay ajansını yıllar boyunca yönetirsin; kendi misyonlarını tasarlar, roketini ve uzay aracını şirketlerle birlikte inşa eder, doğru fırlatma penceresinde gönderir ve her kararın sonucunu görürsün.

## 3.2 Fantezi
"Bu ajansı ben yönetiyorum; Mars'a gitmek için neye ihtiyaç olduğunu **yaşayarak** öğreniyorum."

## 3.3 Üç Katman

```
┌────────────────────────────────────────────────────────────────┐
│ 1. STRATEJİ HARİTASI                                           │
│    Dünya haritası (üsler) · Güneş sistemi haritası (hedefler)  │
│    Zaman akışı · olay pencereleri · araştırma · üretim         │
├────────────────────────────────────────────────────────────────┤
│ 2. TASARIM ATÖLYESİ                                            │
│    Roket + uzay aracı modül-slot tasarımı, şirket seçimi,      │
│    anlık artı/eksi sonuçlar                                    │
├────────────────────────────────────────────────────────────────┤
│ 3. MİSYON OPERASYONU                                           │
│    Fırlatma → seyir → varış/iniş → bilim → sonuç raporu        │
└────────────────────────────────────────────────────────────────┘
```

## 3.3.1 Ana Döngü (Misyon Başına)

```
Misyon Besteleyici → Gereksinim türetme → Araştırma/Üretim planı
→ Tasarım Atölyesi → Şirket sözleşmeleri → İnceleme (Design Review)
→ Üretim ve test (zaman akar) → Fırlatma penceresi → Operasyon
→ Sonuç raporu → Kampanya etkisi (bütçe, itibar, araştırma, ders)
```

## 3.4 Oynanış Gerilimleri (Oyuncu Ne Hisseder)
- "Daha ileri gitmek istiyorum ama bütçem yetmiyor."
- "Daha hafif malzeme lazım ama araştırması aylar sürüyor, pencere kaçacak."
- "Ucuz şirket mi, güvenilir şirket mi?"
- "İnsanlı mı göndereyim, bir rover mi? Maliyet ve risk çok farklı."
- "Pencereyi kaçırırsam 26 ay beklerim; bu sırada bütçe erir, itibar düşer."

---

# 4. KAMPANYA YAPISI

## 4.1 Kurgusal Ajans
- **Ad:** *Birleşik Yörünge Ajansı (BYA)* / *United Orbital Agency (UOA)*.
- Ülke seçimi yok; ajans tarafsız bir uluslararası konsorsiyum. Üs konumları haritada **önceden tanımlı bölgelerdir**; oyuncu ilk üsleri seçer.
- Başlangıç yılı: **2031**. Kampanya sınırı: **2061** (30 yıl) veya oyuncunun serbestçe bitirmesi.

## 4.2 Başlangıç Durumu
- Bütçe: düşük. Teknoloji: temel (Al alaşım, katı/kerosen motor).
- Bir Fırlatma Rampası (küçük sınıf), bir Fabrika, bir küçük Araştırma Merkezi.
- Açık hedefler: LEO ve Ay (yakın). Mars/Venüs/Europa kilitli görünür, "gereken araştırmalar" listelenir.

## 4.3 İlerleme Ekseni
| Eksen | Etki |
|---|---|
| **Bütçe** | Yıllık tahsis (itibara bağlı) + bonuslar |
| **İtibar** | Başarılı misyonlar artırır, kayıplar düşürür; bütçe katsayısı ve şirket şartlarını etkiler |
| **Araştırma** | Yeni modül/malzeme/motor açar |
| **Altyapı** | Üs seviyeleri, rampa sınıfı |
| **Bilgi Puanı** | Misyonlardan öğrenilen "ders"ler (ör. belli arıza sonrası güvenilirlik testi indirimi) |

## 4.4 Kampanya Hedefleri (Kilometre Taşları — oyuncu istediğini seçer)
Kilometre taşları zorunlu sıra değildir; ajans "görev tahtasında" öneri olarak görünür:
1. İlk yörünge uydusu
2. Ay'a ilk sonda
3. Ay yüzeyine iniş (robotik)
4. İlk gezegenler arası sonda (Venüs/Mars)
5. Mars rover
6. İnsanlı Ay görevi
7. Numune getirme
8. İnsanlı Mars (uzun vadeli son hedef)

## 4.5 Başarısızlık Koşulları
- Bütçe bitmesi + itibar sıfır → kampanya "bütçe kesintisiyle sona erdi" (oyun sonu raporu).
- İnsanlı misyon kaybı → ağır itibar kaybı, bir süre insanlı program donar.

## 4.6 Tek Misyon Kısayolu
Kampanya yerine "tek misyon" seçildiğinde aynı sistemler çalışır, ama başlangıç kaynakları ve teknolojisi önceden verilir (hızlı demo/jüri için).

---

# 5. MİSYON BESTELEYİCİ (OYUNCUNUN KENDİ GÖREVİNİ TASARLAMASI)

> Kritik gereksinim: Görevler hazır verilmez. Oyuncu hedef ve amaçları seçer; oyun gereksinimleri türetir.

## 5.1 Adımlar

1. **Hedef:** LEO, Ay, Venüs, Mars, Europa (kilitliyse nedeni gösterilir).
2. **Misyon tipi:** Orbiter, Lander, Rover, Numune Getirme, İnsanlı.
3. **Bilimsel/operasyonel amaçlar (çoklu seçim):** Hedefe özel amaç kütüphanesinden (§5.2).
4. **Hedefler arası iddia seviyesi:** Oyun, seçilen amaçlara göre bir **Zorluk Katsayısı** hesaplar (ödül ↔ risk).
5. **Takvim tercihi:** Bir sonraki pencere veya daha sonraki (araştırmaya zaman kazanmak için).
6. **Bütçe tavanı (iç hedef):** Oyuncu kendine bir tavan koyabilir (skor etkisi).

## 5.2 Amaç Kütüphanesi (Örnekler)

| Hedef | Amaçlar (oyuncu seçer) |
|---|---|
| LEO | Küresel görüntüleme, iklim ölçümü, haberleşme rölesi, teknoloji demosu |
| Ay | Yüzey haritalama, kutup buzu tespiti, iniş + yüzey deneyi, numune alma |
| Mars | Atmosfer örnekleme, yüzey haritalama, iniş + gezgin keşfi, yaşam izleri taraması, numune hazırlama |
| Venüs | Atmosfer profili, yüzey radar haritalama |
| Europa | Buz kabuğu radar sondajı, manyetik alan ölçümü |

Her amaç şu özellikleri taşır: gerekli alet türleri, gerekli minimum veri hacmi, gerekli minimum kapsama süresi, bilim değeri puanı, ek gereksinimler (ör. "yüzey deneyi" → iniş modülü).

## 5.3 Zorluk Katsayısı
`Zorluk = Σ(amaç zorlukları) × hedef çarpanı × tip çarpanı`. Oyuncuya şu şekilde gösterilir: **"İddia seviyesi: Orta/Yüksek/Çok Yüksek"** ve beklenen ödül/risk özeti.

## 5.4 Gereksinim Türetme (Çıktı)
Misyon besteleyici bitince oyun şu **Misyon Profili**ni üretir ve "Misyon Dosyası" ekranında gösterir:

- Gerekli toplam Δv (fırlatma + transfer + varış + iniş + dönüş) — oyuncuya **"menzil ihtiyacı"** çubuğu olarak
- Zorunlu modüller (ör. insanlı → yaşam destek; rover → iniş/EDL paketi)
- Gerekli araştırmalar (kilitliyse kırmızı; oyuncu tek tıkla araştırma kuyruğuna ekleyebilir)
- Güvenilirlik eşiği
- Fırlatma pencereleri (tarih listesi)
- Haberleşme gerekliliği (gecikme, veri hacmi)
- Süre gereksinimi (misyon ömrü)
- Tahmini maliyet aralığı ve tahmini üretim süresi

**Önemli:** Misyon Dosyası bir "yapılacaklar listesi"dir; hiçbir şeyi otomatik çözmez. Oyuncu bu listeyi tasarımla karşılar.

## 5.5 Misyon Başarı Kriterleri (Oyuncunun Seçtiklerinden Otomatik)
Her amaç için ölçülebilir kriter oluşur ("≥ X GB veri yere inmeli", "≥ Y ay çalışma", "iniş başarılı"). Skor ve sonuç raporu bu kriterlere göre hesaplanır.

---

# 6. STRATEJİ HARİTASI (HOI4 TARZI)

## 6.1 Dünya Haritası
- Stilize düz harita; **bölgeler** (eyalet yerine). Bölge özellikleri: enlem, iklim/hava riski, işçilik maliyeti, siyasi istikrar, lojistik erişimi.
- **Üs Türleri:**

| Üs | İşlev | Haritada önemi |
|---|---|---|
| Fırlatma Rampası | Fırlatıcı sınıfına göre seviye | Enlem: ekvatora yakın → ek Δv avantajı; kutupsal yörünge için uygun rampa gerekir; hava durumu gecikme riski |
| Fabrika/Entegrasyon | Üretim kapasitesi | Rampaya uzaklık = taşıma süresi/maliyeti |
| Araştırma Merkezi | Araştırma puanı üretimi | Dallara uzmanlaşma |
| Yer İstasyonu | Haberleşme penceresi | Kıtalara dağılım = daha uzun kapsama |
| Test Tesisi | Test kapasitesi | Güvenilirlik artışı (süre+bütçe) |

- Her üs: seviye, bakım gideri, kapasite, inşa süresi.
- **Harita lensleri:** Lojistik, Haberleşme kapsama, Fırlatma verimi (enlem), Araştırma, Risk.

## 6.2 Güneş Sistemi Haritası
- Basitleştirilmiş daire yörüngeler; gezegenler zamanla hareket eder.
- **Transfer pencereleri** parlayan zaman aralıkları olarak görünür.
- Hedef kartı: menzil ihtiyacı, seyir süresi, haberleşme gecikmesi, tehlike simgeleri (radyasyon, ısı, toz, basınç).
- Seyirdeki aktif misyonlar haritada ilerleyen işaretçidir (HOI4 birlik hareketi hissi).

## 6.3 Zaman Sistemi
- Duraklat / 1× / 2× / 3×. Gün/hafta/ay ölçekleri.
- Zamana bağlı: araştırma, üretim, test, sözleşme teslimleri, pencereler, seyir.
- **Olay pencereleri:** Karar kartı + 2-3 seçenek, her biri görünür artı/eksi etki gösterir.
- Duraklatılabilir tasarım: Oyuncu zaman duruyorken Tasarım Atölyesi/Besteleyici'yi kullanabilir.

## 6.4 Kaynaklar (Kampanya Seviyesi)
Bütçe, Zaman (takvim), Araştırma Puanı, Üretim Kapasitesi, İtibar, Bilgi Puanı, (insanlı programda) Mürettebat Deneyimi.

---

# 7. TASARIM ATÖLYESİ (ROKET + UZAY ARACI)

## 7.1 Genel
HOI4 tank/uçak tasarımcısı hissi: **slot tabanlı**, **canlı artı/eksi göstergeleri**, **şablon kaydetme**.

## 7.2 Tasarım Parçaları

1. **Faydalı yük (Payload):** Misyon tipine göre: orbiter/lander/rover/insanlı kabin/numune aracı. Bilimsel aletler burada seçilir.
2. **Uzay aracı (Bus):** Yapı, güç (panel/RTG, batarya), ısıl kontrol, haberleşme, bilgisayar, yönelim (ADCS), itki, EDL (iniş gerekiyorsa).
3. **Roket:** 1-3 aşama + isteğe bağlı yan güçlendirici. Her aşama: motor + yakıt tankı + yapı malzemesi.
4. **Entegrasyon:** Fairing, adaptör, rampa uyumu.

## 7.3 Seçim Mantığı
- Her slot bir seçim listesi açar.
- Bir seçeneğin üzerine gelince **hayalet delta**: toplam kütle, menzil, maliyet, süre, güvenilirlik, güç dengesi çubuklarında yeşil/kırmızı değişim.
- **Şirket seçimi:** Her bileşen/aşama için tedarikçi seçilir (§9.1). Seçim, fiyatı, teslim süresini, güvenilirliği ve bileşen kalitesini değiştirir.
- Bileşen kilitliyse araştırma gerekliliği gösterilir.

## 7.4 Doğrulama (Design Review)
- Kırmızı = engel (menzil açığı, rampa uyumsuz, kütle > kapasite, araştırma kilitli, güç açığı)
- Sarı = uyarı (düşük marj, yüksek risk)
- Yeşil = tamam
- Her madde **"neden"** ve **"neye bakmalı"** ipucu verir (çözümü söylemez).

## 7.5 Şablonlar
- Tasarımı kaydet/kopyala/yeniden adlandır, iki şablonu yan yana karşılaştır.
- **Misyon kodu:** Tasarım + hedef + seed paylaşılabilir kısa bir kod olarak dışa aktarılır/içe alınır.

## 7.6 İnsanlı Misyon Özel Kuralları
- Kabin, yaşam destek, radyasyon kalkanı, abort sistemi zorunlu.
- Dönüş için ek yakıt/araç.
- Yüksek güvenilirlik eşiği (şirket sertifikası ve test gerektirir).
- Çoklu fırlatma / yörüngede buluşma gerekebilir.
- **İnsanlı misyon kaybı** ağır sonuçlar doğurur (§4.5).

## 7.7 Rover/Lander Özel Kuralları
- EDL paketi zorunlu (ısı kalkanı, paraşüt/gökvinci/retro itki; hedef atmosferine göre farklı gereksinim).
- Güç kaynağı seçimi: güneş (uzak hedefte zayıf), RTG (güçlü ama pahalı/kısıtlı).
- Haberleşme gecikmesi nedeniyle özerklik bilgisayarı seçimi etkisi.

---

# 8. HESAPLAMA MODELİ (ARKA PLAN GERÇEK, ARAYÜZ SADE)

> **İlke:** Hesaplar gerçek fiziğe dayanır; oyuncu sadece sonuçları görür. Geliştirici için her istatistik "kaynak listesi" üretir; oyuncuya sade bir "neden?" tooltip'i verilir.

## 8.1 Çekirdek Hesaplar (Arka Planda)

| Konu | Model (sözel/matematiksel) |
|---|---|
| **Roket denklemi** | Aşama başına Δv = Isp × g0 × ln(başlangıç kütle / bitiş kütle); aşamalar toplanır |
| **Kütle** | Σ bileşen kütleleri + yakıt + yapı malzemesi katsayısı; kuru ve ıslak kütle ayrı |
| **Menzil ihtiyacı (Δv)** | Hedef profiline göre toplam: fırlatma kayıpları + transfer + varış + (iniş) + (dönüş); fırlatma enlemi avantajı düşülür |
| **Fırlatıcı kapasitesi** | Roketin hedef yörüngeye taşıyabileceği kütle (tasarımdan türer) |
| **Güç** | Üretim = panel alanı × verim × güneş akısı(hedef uzaklığı) × geometri × (1 − tutulma oranı); RTG sabit. Tüketim = Σ aktif sistem. Batarya tutulmayı kapatır |
| **Isı** | Hedef ısı yükü (güneş uzaklığı, albedo) vs radyatör/izolasyon kapasitesi |
| **Veri** | Üretim = Σ alet veri hızı × çalışma oranı; depolama sınırı |
| **Haberleşme** | Günlük indirilebilir veri = anten kazancı × verici gücü ÷ mesafe² temelli hız × yer istasyonu görüş süresi |
| **Güvenilirlik** | Bileşen güvenilirlikleri × şirket çarpanı × test seviyesi × deneyim; seri sistem olarak birleştirilir (zayıf halka baskın) |
| **Maliyet** | Bileşen + şirket çarpanı + fırlatma + operasyon (aylık) + risk marjı |
| **Üretim süresi** | Bileşen süreleri, kapasite kuyruğu, şirket teslim süresi, gecikme riski |
| **Radyasyon** | Hedef/rota radyasyon yükü vs kalkan/dayanım; insanlıda doz sınırı |
| **EDL başarısı** | Atmosfer yoğunluğuna göre giriş kütlesi, balistik katsayı, ısı kalkanı kapasitesi, iniş yöntemi uygunluğu |
| **Yaşam destek** | Kişi-gün tüketimi, geri dönüşüm verimi, süre |

## 8.2 İstatistik Modeli (Temel + Etkiler)
Her istatistik **temel değer + etkiler (toplanır/çarpılır)** olarak hesaplanır. Etkiler: şirket, malzeme, teknoloji, olay, test, deneyim. Her istatistik **kaynak dökümü** üretir (geliştirici ve "neden?" tooltip'i için).

## 8.3 Oyuncuya Ne Gösterilir (Sade Sunum)
- **Çubuklar:** Menzil (yeterli/yetersiz), Kütle (kapasite içinde mi), Maliyet, Süre, Güvenilirlik, Güç dengesi, Veri/Haberleşme dengesi.
- **Renk dili:** Yeşil = rahat, sarı = sınırda, kırmızı = yetersiz.
- **Tooltip (kısa):** "Bu motor +%12 verim sağlıyor ama +3 t kütle ekliyor."
- **Sayılar sade:** Formül, katsayı, birim dönüşümü gösterilmez. Gerekiyorsa tek cümlelik neden.
- **Sonuç ekranları** (Review, Fırlatma, Rapor): neden-sonuç zinciri sade dille (§10.5, §13).

## 8.4 Determinizm
- Aynı tasarım + aynı seed + aynı kampanya durumu = aynı sonuç.
- Tohumlu RNG, sabit adım.
- Olaylar tasarımdaki zayıflıklara göre ağırlıklandırılır (adil ama rastgele).

## 8.5 Gerçekçilik Sınırı
Orbital mekanik basitleştirilmiş (daire yörünge, Hohmann tabanlı transferler, tablo + yaklaşım). Amaç eğitim ve karar gerilimi; birebir simülasyon değil. Katalog değerleri NASA/JPL kaynaklarından beslenir ve atıflanır (§24).

---

# 9. ŞİRKETLER, ARAŞTIRMA, ÜRETİM, ZAMAN

## 9.1 Tedarikçi Şirketler (Kurgusal)

| Şirket | Güçlü | Zayıf | Özellik |
|---|---|---|---|
| **Helion Dynamics** | Düşük maliyet, hızlı teslim | Düşük güvenilirlik | Yeniden kullanılabilir aşama indirimi |
| **Kestrel Aerospace** | Yüksek güvenilirlik, insanlı sertifikası | Pahalı, yavaş | İnsanlı program için gerekli olabilir |
| **Nordvik Propulsion** | Motor verimi | Yapı zayıf | Üst aşama uzmanı |
| **Tarsus Mühendislik** | Hafif yapılar (kompozit) | Küçük kapasite | Kütle avantajı |
| **Meridian Systems** | Elektronik, haberleşme, aviyonik | Mekanik zayıf | Uzay aracı bus uzmanı |
| **Aurelia Labs** | Bilimsel alet kalitesi | Pahalı alet, uzun teslim | Alet uzmanı |

- **Sözleşme:** Şirket × bileşen. Fiyat, teslim süresi, gecikme riski, güvenilirlik çarpanı.
- **Tek kaynak riski:** Çok bileşen tek şirketten → indirim ama toplu gecikme/arıza olayı riski.
- **İlişki puanı:** İyi çalışma → daha iyi şartlar; kötü → zam, gecikme.
- **Olaylar:** Şirket iflası, grev, teslim gecikmesi, kalite sorunu.

## 9.2 Araştırma Ağacı
Dallar ve örnek düğümler:

- **İtki:** Gelişmiş kerosen, hidrojen üst aşama, metan, yeniden kullanım, iyon/elektrik itki
- **Yapı/Malzeme:** Al-Li, titanyum, karbon kompozit
- **EDL/İniş:** Isı kalkanı I-II, paraşüt, gökvinci, retro itki, hassas iniş
- **Yaşam Destek:** Açık döngü kabin, kapalı döngü, radyasyon kalkanı, sağlık sistemi
- **Güç/Isı:** Verimli paneller, gelişmiş batarya, RTG, ısıl yönetim
- **Haberleşme:** Yüksek kazanç anten, Ka-bant, lazer iletişim
- **Güvenilirlik/Test:** Kalite süreçleri, tam sistem testi, insanlı sertifikasyon

Her düğüm: araştırma puanı + süre + (bazen) bütçe + ön koşullar. Etkiler: modül açar veya istatistik iyileştirir. Misyon dosyasında eksik araştırmalar **"önerilen"** olarak parlar.

## 9.3 Üretim ve Test
- Üretim kuyruğu: bileşen üretimi kapasiteyle sınırlı; şirketten gelenler teslim süresine bağlı.
- Entegrasyon: tüm bileşenler hazır olunca montaj süresi.
- Test: Test Tesisi kapasitesi ve seviyesi; test = süre+bütçe, güvenilirlik artışı ve sürpriz arıza tespiti.

## 9.4 Fırlatma Pencereleri
- Her hedef için pencere takvimi (basitleştirilmiş sinodik periyotlar).
- Pencere kaçırılırsa bir sonraki pencere beklenir; bu sürede bütçe harcanır, üretilen bileşenler beklerken bakım maliyeti oluşur.
- Rampa hava durumu: gecikme olayı olasılığı.

---

# 10. OPERASYON, OLAYLAR, SONUÇ RAPORU

## 10.1 Operasyon Fazları
1. **Fırlatma:** Roket güvenilirliği, hava, aşama ayrılması. (Kısa görsel sekans.)
2. **Seyir:** Zaman hızlı akar; küçük olaylar, yörünge düzeltme kararları.
3. **Varış:** Yörünge girişi veya iniş sekansı (EDL).
4. **Bilim/Operasyon:** Alet çalışması, veri üretimi, indirme, güç/ısı yönetimi.
5. **Misyon sonu:** Ömür bitişi veya erken kayıp.

## 10.2 Karar Kartları (Operasyon Sırasında)
Oyuncu zaman akarken müdahale eder. Örnekler:
- Güneş fırtınası: aletleri güvenli moda al (bilim kaybı) ↔ risk al (donanım hasarı olasılığı)
- Veri darboğazı: hangi veriyi önceliklendireceksin?
- Batarya düşük: bir aleti kapat / panel yönelimini değiştir
- Yer istasyonu arızası: yedek istasyon (maliyet) ↔ beklemek
- Yakıt sızıntısı/ADCS arızası: görev kapsamını daralt ↔ riskli onarım
- Şanslı fırsat: hedefte beklenmedik olay yakalandı (bonus bilim)

## 10.3 Olay Havuzu İlkeleri
- Olaylar **tasarımdaki zayıflığa göre ağırlıklanır** (adil ama rastgele).
- Her olay: koşullar, ağırlık, seçenekler, görünür etkiler, sonuç dağılımı.
- Hedef ~30 olay: kampanya olayları (bütçe, politik, şirket) ve operasyon olayları (teknik, doğa).

## 10.4 Kayıp Tipleri
- Fırlatma kaybı, seyir kaybı, varış/iniş kaybı, kısmi başarı (bazı aletler çalışmaz), erken ömür sonu.
- Her kayıp tipi ayrı mesaj ve ders üretir.

## 10.5 Sonuç Raporu (Misyon Sonrası)
- **Başarı özeti:** Her başarı kriteri ✓/✗.
- **Bilim teslimi:** Yere inen veri vs üretilen veri (veri darboğazı dersi).
- **Neden-sonuç zinciri:** Düğümler = kararlar/olaylar, oklar = etkiler. Başarısızlıkta zincir vurgulanır.
  Örnek anlatım: *"Radar eklendi → güç ihtiyacı arttı → panel büyüdü → kütle arttı → fırlatıcı sınıfı değişti → bütçe aşıldı."*
- **"Ya şöyle olsaydı?":** Tek bir kararı değiştirip raporu yeniden hesaplar (deterministik olduğu için ucuz).
- **Kampanya etkisi:** Bütçe/itibar/bilgi puanı değişimleri, açılan dersler.
- **Paylaş:** Misyon kodu/kartı.

---

# 11. SKOR VE İLERLEME

## 11.1 Misyon Skoru
Bileşenler (ağırlıklar ayar dosyasında tutulur, başlangıç önerisi):
- **Bilim Teslimi (%35):** Yere inen veri × amaç değeri / hedef
- **Amaç Başarısı (%20):** Oyuncunun seçtiği amaçların sağlanma oranı
- **Kaynak Verimliliği (%15):** Bütçe/kütle/güç marjları; aşırı marj da cezalı (ideal %10-20)
- **Karar Kalitesi (%15):** Olay yanıtları, risk yönetimi
- **Koordinasyon veya Verimlilik (%15):** Takım modunda koordinasyon; tek kişide planlama verimliliği
- **Zorluk çarpanı:** İddia seviyesi yükseldikçe çarpan artar (ödül ↔ risk)

## 11.2 Madalyalar (Örnekler)
"Ucuz ama etkili", "Sıfır kayıp veri", "Fırtınayı atlattı", "İlk insanlı", "Pencereyi yakaladı", "Tek kaynak yok", "Hafif ve hızlı".

## 11.3 Kampanya Skoru
Yıl sonu ve kampanya sonu: tamamlanan kilometre taşları, toplam bilim, itibar, bütçe sağlığı, kayıp sayısı.

## 11.4 Liderlik Tablosu
- Misyon bazında: aynı misyon kodu + seed için skor karşılaştırması.
- Gerçek sunucu olabilir; yoksa yerel/sahte tablo (§12.4).

---

# 12. ÇOK OYUNCULU (ONLINE) TASARIMI

## 12.1 Hedef
Gösterişli ve çalışan bir 1-4 kişilik ortak komuta masası. Ağır çok oyunculu altyapı **gerekmez**: oyun gerçek zamanlı aksiyon değil, paylaşılan bir yönetim durumudur.

## 12.2 Roller (Departmanlar)

| Rol | Sorumluluk |
|---|---|
| **Ajans Başkanı** | Hedef/misyon seçimi, bütçe tahsisi, Go/No-Go, kampanya yönü |
| **Baş Mühendis** | Roket ve uzay aracı tasarımı |
| **Tedarik & Üretim Direktörü** | Şirket sözleşmeleri, üretim kuyruğu, test |
| **Bilim & Operasyon Direktörü** | Amaçlar ve aletler, yer istasyonları, operasyon kararları |

- Herkes ortak haritayı ve kaynak panelini görür (şeffaflık).
- Roller yetki ve ekran odağını belirler; her şeyi herkes **önerebilir**, ilgili rol **onaylar**.
- Tek kişide tüm roller tek kullanıcıdadır (rol sekmeleri yok, sade görünüm).

## 12.3 Takım Mekanikleri
- **Öneri-Onay:** Değişiklik "diff" olarak sunulur; ilgili rol onaylar/reddeder (hazır gerekçe seçenekleri).
- **Gizli Öncelikler:** Her rolün küçük kişisel hedefi (örn. Mühendis: "güç marjı ≥ %20"; Bilim: "≥ 3 alet"). Hepsi sağlanırsa **Koordinasyon Bonusu**.
- **Ortak zaman kontrolü:** Başkan zamanı yönetir; isteğe bağlı oylama.
- **Koordinasyon skoru:** Çakışmalar, geri alınan kararlar, onay hızı.

## 12.4 Uygulama Stratejisi (Gerçek + Yedek)
- **Gerçek online:** Küçük bir röle sunucusu + oda kodu (4 harf). Otoriter host modeli: istemciler komut gönderir, host doğrular ve yayınlar. Yeni katılan = anlık durum + komut kaydı.
- **Bot yedek:** Boş roller kural tabanlı botlarla dolar (öneri yapar, onaylar, bazen itiraz eder). İnternet yoksa/oyuncu azsa da "takım hissi" ve demo çalışır.
- **Yerel/sahte liderlik tablosu:** Sunucu yoksa önceden doldurulmuş + yerel skorlar.
- **Bağlantı kopması:** Rol bota geçer veya başka oyuncu devralır.
- **Ağ arayüzü tek:** İnsan oyuncu ve bot aynı komut yolunu kullanır (aynı arayüz).

## 12.5 Sınırlar (Bilinçli)
Gerçek zamanlı fizik senkronu yok; tur/komut tabanlı. Anti-hile: sunucu skoru yeniden hesaplayabilir (determinizm sayesinde).

---

# 13. EĞİTİM VE GERİ BİLDİRİM KATMANI

## 13.1 İlke
Mühendis modu yok; ancak **eğitim değeri korunur**: oyuncu sonuçları ve nedenlerini görür, formülleri değil.

## 13.2 Araçlar
- **"Neden?" kartları:** Her kısıt ihlali ve kritik olay için kısa, sade açıklama ve ipucu.
  Örnek: *"Aracın bu roketin taşıyabileceğinden ağır. Ağırlık arttıkça gereken yakıt çok daha hızlı artar; bu yüzden küçük bir alet bile roketi büyütebilir."*
- **Sözlük kartları:** Terim ilk karşılaşmada kısa açıklama; Sözlük menüsünde birikir (menzil, güç marjı, veri darboğazı, fırlatma penceresi...).
- **Etki önizlemesi (hayalet delta):** Seçmeden önce sonucu gör.
- **Neden-sonuç raporu:** Misyon sonrası zincir (§10.5).
- **"Ya şöyle olsaydı?":** Tek karar değiştirip karşılaştır.
- **Kısa tutorial:** Küçük bir LEO uydusu görevi (≈ 5 dk), kademeli ipuçları, atlanabilir.
- **Zorluk kademesi yerine yardım düzeyi:** "İpuçlarını göster / gizle" ayarı (oyun modu değil).

## 13.3 Dil Kuralı
Yalın, kısa, günlük dil. Birim gerektiğinde kullanılır ama ham katsayı yok. Çeviri tablosunda (TR/EN) tek kaynak.

---

# 14. UI / UX SPESİFİKASYONU

## 14.1 Ekran Listesi
1. Ana Menü (Kampanya Başlat / Tek Misyon / Online / Ayarlar / Sözlük)
2. Lobi (oda kodu, rol seçimi, botları ekle)
3. Kampanya Ana Ekranı (Dünya Haritası + üst bar + yan paneller)
4. Güneş Sistemi Haritası
5. Misyon Besteleyici (adımlı sihirbaz)
6. Misyon Dosyası (gereksinim listesi)
7. Tasarım Atölyesi
8. Şirketler/Sözleşmeler
9. Araştırma Ağacı
10. Üs Yönetimi
11. Üretim/Test Kuyruğu
12. Design Review (doğrulama)
13. Fırlatma Sekansı
14. Operasyon Ekranı
15. Olay Penceresi (popup)
16. Sonuç Raporu
17. Karşılaştırma Ekranı
18. Sözlük/Yardım
19. Ayarlar (ses, dil, erişilebilirlik)

## 14.2 Ortak UI Öğeleri
- **Üst bar:** Tarih, hız kontrolü, Bütçe, Araştırma Puanı, İtibar.
- **Sol/üst sekmeler:** Harita, Besteleyici, Atölye, Şirketler, Araştırma, Üsler, Üretim.
- **Bildirim akışı:** Teslim, araştırma bitişi, olay.
- **Tooltip sistemi:** Tek, tutarlı, kısa metinli.

## 14.3 Tasarım Atölyesi Yerleşimi (Referans)

```
┌─────────────────────────────────────────────────────────────────┐
│ Tarih · Hız · Bütçe · Araştırma · İtibar                        │
├─────────────────────────────────────────────────────────────────┤
│ MİSYON DOSYASI ŞERİDİ: ✓ amaç A  ✗ menzil  ✓ pencere  ...       │
├──────────────┬────────────────────────────┬─────────────────────┤
│ SLOTLAR      │  ROKET/ARAÇ KESİTİ (2D)    │  SONUÇLAR           │
│ Faydalı yük  │  (modüller canlı birleşir) │  Menzil ▓▓▓░ yetersiz│
│ Bus          │                            │  Kütle  ▓▓▓▓ uygun  │
│ Aşama-1/2/3  │                            │  Maliyet ▓▓░░       │
│ Güç/Isı/İlet.│                            │  Süre   ▓▓▓░        │
│ EDL          │                            │  Güvenilirlik ▓▓▓░  │
├──────────────┴────────────────────────────┴─────────────────────┤
│ Şirket seçimi · Uyarılar (kırmızı/sarı) · Şablonlar             │
└─────────────────────────────────────────────────────────────────┘
```

## 14.4 Etkileşim İlkeleri
- Her seçimde **anlık geri bildirim** (hayalet delta).
- **Geri al/yinele** her yerde.
- **Tıkla-ekle ve sürükle-bırak** ikisi de.
- Duraklatılmışken tasarım serbest; zaman akarken kritik ekranlar uyarı verir.
- **Klavye/ fare öncelikli** (WebGL masaüstü); dokunmatik opsiyonel.
- **Erişilebilirlik:** Renk körü paleti, ölçeklenebilir yazı, renk dışı gösterge (ikon/desen).

## 14.5 UI Teknolojisi Notları (2022.3)
UI Toolkit runtime kullanılır. Veri bağlama olmadığı için her ekranın bir **ViewModel**i vardır; Core olayları ViewModel'i günceller, UI ViewModel'e bakar. Stil (USS) değişkenleri renk/aralık tek yerde.

---

# 15. SANAT YÖNÜ (2D VEKTÖREL) VE SES

## 15.1 Görsel Dil
- **Ton:** Ciddi, bilgi yoğun, sakin (HOI4'ten esinli) + uzay temalı.
- **Palet:** Koyu lacivert zemin, mat çelik/gri paneller, **amber** (uyarı/vurgu), **cyan** (veri/başarı), kırmızı (engel). Renk körü uyumlu varyantlar.
- **Stil:** Düz vektör, keskin geometri, hafif gradyan/kenar parıltısı; dokusal kâğıt/harita hissi (hafif).
- **Harita:** Stilize dünya (bölgeler), ince çizgiler, ikon + seviye rozetleri. Güneş sistemi paralaks arka plan.
- **Roket/araç parçaları:** Modüler vektör sprite'lar. **Snap noktaları** ile otomatik hizalanır; şirket renk şeridi parça üzerinde görünür.
- **Animasyon:** Basit, vektör tabanlı (parça birleşme, fırlatma, ayrılma, yörünge çizgisi, gezgin iniş).

## 15.2 Vektör Pipeline (Önemli Karar)
- Kaynak: SVG/AI ile çizim. Unity tarafında iki seçenek:
  1. **Unity Vector Graphics paketi** (SVG içe aktarma; paket önizleme niteliğinde olabilir → WebGL/performans/kalite erken test edilmeli).
  2. **Yüksek çözünürlüklü PNG dışa aktarma** + sprite atlas (daha güvenli, kararlı).
- **Öneri:** İlk dikey dilimde ikisini de dene, karar ver; varsayılan güvenli yol = (2). Karar GDD'ye geri yazılır.
- Ortak: tek pivot kuralı, 9-slice paneller, sprite atlas, Addressables.

## 15.3 İkon Seti
Stat ikonları: kütle, menzil, güç, ısı, veri, haberleşme, maliyet, süre, güvenilirlik, radyasyon, itibar, araştırma. Üs ikonları, şirket logoları (kurgusal), olay kategorileri.

## 15.4 Ses
Minimal UI sesleri, ambient/telsiz dokusu, geri sayım, motor, ayrılma, başarı/kayıp müzik vurguları. **Telifsiz kaynak**; lisans listesi tutulur (§24).

---

# 16. İÇERİK KATALOĞU (BAŞLANGIÇ DEĞERLERİ)

> **Tüm değerler oyun içi sadeleştirilmiş başlangıç değerleridir.** Geliştirme sırasında NASA/JPL/üretici kaynaklarıyla doğrulanıp §24'e atıf eklenecektir. Doğrulanamayanlar "TODO-VERİ" olarak işaretlenir. Birim: ton (t), kg, kW, GB/gün, milyon $ (M$).

## 16.1 Hedefler

| Hedef | Menzil ihtiyacı (yerden, kabaca) | Seyir | Tek yön gecikme | Pencere | Tehlikeler |
|---|---|---|---|---|---|
| LEO | ~9.4 km/s | saat | ~0 | sürekli | atmosfer sürtünmesi (düşük irtifa) |
| Ay | ~13.5 (yörünge) / ~15.5 (iniş) km/s | ~3 gün | ~1.3 s | aylık | toz, ısı döngüsü, kutup gölgesi |
| Venüs | ~13 km/s + varış | 4-5 ay | 2-14 dk | ~19 ay | aşırı ısı/basınç |
| Mars | ~13 km/s + varış/iniş | 6-9 ay | 3-22 dk | ~26 ay | radyasyon, EDL, toz fırtınası |
| Europa | gravite yardımlı rota | 5+ yıl | 35-52 dk | seyrek | radyasyon, zayıf güneş |

## 16.2 Fırlatıcı Sınıfları (Aşama/Motor Sistemi Yanında "Hazır Şablon" Olarak da Sunulabilir)
Küçük / Orta / Ağır / Süper Ağır sınıf: LEO kapasitesi, maliyet, güvenilirlik, gerekli rampa seviyesi. (Değerler TODO-VERİ; gerçek sınıf aralıkları referans alınacak.)

## 16.3 Motor Tipleri

| Tip | Verim | İtki | Not |
|---|---|---|---|
| Katı yakıt | Düşük | Çok yüksek | Ucuz, kapatılamaz |
| Kerosen/LOX | Orta | Yüksek | Alt aşama |
| Hidrojen/LOX | Yüksek | Orta | Üst aşama, büyük tank |
| Metan/LOX | İyi | Yüksek | Gelecek/yeniden kullanım |
| İyon/elektrik | Çok yüksek | Çok düşük | Seyir, yüksek güç, yavaş |

## 16.4 Malzemeler

| Malzeme | Yapı kütlesi | Maliyet | Dayanım | Üretim süresi | Araştırma |
|---|---|---|---|---|---|
| Al alaşım | Yüksek | Düşük | Orta | Kısa | Başlangıç |
| Al-Li | Orta | Orta | İyi | Orta | Sev. 1 |
| Titanyum | Orta | Yüksek | Yüksek (ısı) | Uzun | Sev. 2 |
| Karbon kompozit | Düşük | Çok yüksek | Yüksek | Uzun | Sev. 3 |
| Ablatif ısı kalkanı | — | Yüksek | Giriş ısısı | Orta | EDL |

## 16.5 Bilimsel Aletler (Esinlenme: Gerçek Misyon Sınıfları)

| Alet | Kütle | Güç | Veri | Maliyet | Bilim değeri | Not |
|---|---|---|---|---|---|---|
| Optik kamera | düşük | düşük | orta | düşük | orta | Haritalama |
| Çok bantlı spektrometre | orta | orta | yüksek | orta | yüksek | Mineral/atmosfer |
| Radar/altimetre | yüksek | yüksek | yüksek | yüksek | çok yüksek | Buz/yüzey altı |
| Manyetometre | düşük | düşük | düşük | düşük | orta | Alan ölçümü |
| Parçacık/radyasyon dedektörü | düşük | düşük | düşük | düşük | orta | Radyasyon haritası |
| Termal kızılötesi | orta | orta | orta | orta | yüksek | Isı haritası |
| Yüzey bilim paketi (gezgin) | orta | orta | orta | yüksek | yüksek | Kimya/jeoloji |
(Sayısal değerler TODO-VERİ; sınıflar arası **göreli** sıralama doğru tutulur.)

## 16.6 İnsanlı Misyon Referansları
Kişi başı günlük tüketim (geri dönüşümsüz yaklaşık ~5 kg/kişi/gün oyun değeri), kabin kütlesi, kalkan, abort sistemi, dönüş aracı. Güvenilirlik eşiği ≥ %99 (oyun değeri). (Doğrulanacak.)

## 16.7 Üs Seviyeleri
Her üs 1-5 seviye; seviye başına kapasite, bakım, inşa süresi/maliyeti (TODO-VERİ, ayar dosyasında).

## 16.8 Başlangıç Dengesi Hedefleri (Playtest İçin)
- İlk LEO uydusu: ~3-5 oyun yılı içinde makul bütçeyle yapılabilmeli.
- Ay robotik iniş: başlangıç sonrası ~5-8 yıl.
- Mars rover: ~10-15 yıl.
- İnsanlı Mars: 25+ yıl, çok pahalı ve riskli.
(Bu hedefler denge testlerinde ayarlanır.)

---

# 17. TEKNİK MİMARİ

## 17.1 Temel Prensip
**Simülasyonu Unity'den ayır.** Oyun mantığı saf C# kütüphaneleridir; Unity sadece görüntü, girdi, ses, ağ taşıma ve I/O'dur.

## 17.2 Katman Şeması

```
┌──────────────────────── Unity (Presentation) ────────────────────────┐
│ UI Toolkit ekranları · Harita/Roket görünümleri · Ses · Girdi        │
└──────────▲──────────────────────────────────────▲────────────────────┘
           │ ViewModel/olaylar                    │ Command istekleri
┌──────────┴──────────────────────────────────────┴────────────────────┐
│ Application: GameFlow (durum makinesi), Session, CommandBus,         │
│ Undo/Redo, Persistence, Localization, Settings                       │
└──────────▲──────────────────────────────────────▲────────────────────┘
           │                                       │
┌──────────┴──────────────┐              ┌─────────┴────────────────────┐
│ Strategy Layer (saf C#) │              │ Network Layer                │
│ CampaignState, Time,    │              │ Lobby, roller, komut yayını, │
│ Bases, Research,        │              │ BotAgent (yedek)             │
│ Production, Contracts,  │              └─────────▲────────────────────┘
│ Events, Reputation,     │                        │
│ MissionComposer         │              ┌─────────┴────────────────────┐
└──────────▲──────────────┘              │ Relay Sunucusu (ayrı, küçük) │
           │                             └──────────────────────────────┘
┌──────────┴──────────────┐
│ Design Layer (saf C#)   │
│ Blueprint, Modules,     │
│ StatModel(+etkiler),    │
│ Validators, DeltaV,     │
│ MissionProfile          │
└──────────▲──────────────┘
           │
┌──────────┴──────────────┐
│ Core Simulation (saf C#)│
│ Flight/Orbit/Power/Comms│
│ /Thermal/EDL, Events    │
│ Resolution, Scoring, RNG│
└──────────▲──────────────┘
           │
┌──────────┴──────────────┐
│ Data Layer              │
│ Catalog (SO + JSON),    │
│ NASA katalog, Importers │
└─────────────────────────┘
```

## 17.3 Temel Mimari Kararlar

1. **Tek durum nesnesi: CampaignState.** Harita, üsler, zaman, araştırma, sözleşmeler, şablonlar, aktif misyonlar. Okunur her yerden; değiştirilir sadece Command ile.
2. **Command deseni:** Her komut: kim yapabilir (rol maskesi), uygulanabilir mi + sebep, uygula, geri al. Komut kaydı → undo/redo, online yayın, kayıt/tekrar, test.
3. **Zaman = tick sistemi:** Gün adımlı, deterministik. Araştırma/üretim/test/seyir/olay hep tick'te işlenir. Zaman hızı sadece "saniyede kaç tick".
4. **Deterministik RNG:** Tohumlu, bağımsız akışlar (olaylar, fırlatma, EDL) ayrı akıştan beslenir ki birinin değişimi diğerini bozmasın.
5. **Veri odaklı içerik:** Bileşen, motor, malzeme, şirket, alet, hedef, amaç, olay, araştırma düğümü, üs → veri dosyaları. Katsayılar ayar dosyası.
6. **Doğrulama saf fonksiyon:** (tasarım, misyon profili, kampanya durumu) → rapor. UI sadece gösterir.
7. **İstatistik modeli:** Temel + etkiler; her değer kaynak dökümü verir.
8. **Misyon Besteleyici → MissionProfile:** Amaç kütüphanesi + hedef + tip → gereksinimler. Saf fonksiyon, veri odaklı.
9. **Olay yönetmeni:** Tasarım zayıflıklarına göre ağırlıklı seçim; seçenekler ve etkiler veri.
10. **Ağ = komut replikasyonu:** Host doğrular/sıralar; istemciler aynı komutları uygular; yeni katılımcıya anlık durum + kayıt.
11. **Bot = oyuncu arayüzü:** İnsan ve bot aynı "oyuncu ajanı" arayüzünü kullanır.
12. **Sürümleme:** Kayıt/misyon kodu sürüm numarası taşır; veri değişince geçiş (migrasyon) kuralı.

## 17.4 Assembly / Klasör Yapısı

```
Assets/_Project/
  Core/          Apogee.Core        (noEngineReferences)   Sim, Rng, Scoring, Events resolution
  Design/        Apogee.Design      (noEngineReferences)   Blueprint, Modules, Stats, Validators, MissionProfile
  Strategy/      Apogee.Strategy    (noEngineReferences)   Campaign, Time, Bases, Research, Production, Contracts, Composer, Events
  Data/          Apogee.Data        Katalog tanımları (SO), JSON yükleyiciler, NASA katalog verisi
  Net/           Apogee.Net         İstemci taşıma, lobi, komut yayını, botlar
  Game/          Apogee.Game        GameFlow, Session, Persistence, Localization, Settings
  Presentation/  Apogee.UI          UXML/USS, ViewModel'ler, Harita/Roket görünümleri, Ses, VFX
  Tests/         EditMode (Core/Design/Strategy), PlayMode (duman testleri)
Server/          Relay sunucusu (ayrı küçük proje)
Docs/            GDD, veri kaynakları, atıflar, denge notları
```

Bağımlılık yönü: Presentation → Game → (Strategy, Net) → Design → Core; Data hepsi tarafından okunur. **Core/Design/Strategy UnityEngine'e referans vermez.**

## 17.5 Kayıt / Paylaşım
- **Kayıt:** CampaignState → JSON (sürümlü) → tarayıcı depolaması (WebGL) / dosya (masaüstü).
- **Misyon kodu:** Tasarım + hedef + seed + sürüm → sıkıştır → metin kodu.
- **Karşılaştırma:** İki şablon/rapor yan yana.

## 17.6 Yerelleştirme
Tek metin tablosu (anahtar → TR/EN). Kod içinde sabit metin yok. Sayı/tarih biçimleri kültüre duyarlı.

## 17.7 Performans Bütçesi
- Tasarım istatistiği yeniden hesabı: oyuncu etkileşiminde anlık (< birkaç ms).
- Zaman tick: ekranda yüzlerce nesne olsa da akıcı (hedef 60 fps masaüstü, WebGL'de 30+).
- WebGL derleme boyutu: sıkıştırılmış hedef makul (ör. < 60 MB; ölçülüp ayarlanır).

## 17.8 Hata ve Günlük
Merkezi günlük; oyuncuya anlaşılır hata, geliştiriciye ayrıntı. Komut reddi sebebi her zaman metin.

---

# 18. TECH STACK

| Alan | Seçim | Not |
|---|---|---|
| Motor | **Unity 2022.3 LTS** | 2D, WebGL derleme |
| Dil | **C# (9.0 sınırı)** | Unity 2022 uyumlu |
| Render | **URP 2D** | 2D ışık gerektirmezse Built-in 2D de olabilir; karar M0'da |
| UI | **UI Toolkit (runtime)** | UXML/USS; veri bağlama yok |
| Sanat formatı | **SVG kaynak** → PNG/atlas veya Vector Graphics paketi | §15.2 |
| Varlık yönetimi | **Addressables** | WebGL boyutu/yükleme |
| Veri | **ScriptableObject + JSON** | Newtonsoft JSON paketi (Unity paketi) |
| Test | **Unity Test Framework (NUnit)** | Core/Design/Strategy EditMode ağırlıklı |
| Ağ taşıma (WebGL) | **WebSocket** (WebGL uyumlu bir istemci paketi) | Erken prototip şart |
| Relay sunucu | **Node.js + ws** (veya C# minimal) | Tek küçük süreç; oda kodu, mesaj yayını |
| Barındırma | Ucuz/ücretsiz bir uygulama barındırma servisi | Kararı takım verir |
| Derleme/CI | **GameCI (GitHub Actions)** | Otomatik test + WebGL derleme |
| Sürüm kontrol | **Git + Git LFS** | Sanat/ses için LFS |
| Yerelleştirme | Basit kendi tablo (veya Unity Localization) | TR/EN |
| Ses | Unity AudioSource/Mixer | Telifsiz içerik |
| Sanat araçları | Inkscape/Figma/Illustrator (vektör) | Takım tercihi |
| NASA verisi | Statik atıflı JSON; opsiyonel canlı API'ler (çevrimdışı yedekli) | §24 |

**Kaçınılacaklar:** Thread, System.IO erişimi (WebGL), Unity 6'ya özgü API'ler, gerçek şirket marka/logoları, telifli görsel/ses, ağır fizik motoru kullanımı (gerek yok).

---

# 19. VERİ ŞEMALARI (ALAN LİSTELERİ)

> Kod değil; AI'nın veri dosyalarını/SO'ları tasarlarken uyacağı alan sözleşmesi. Her kayıt: benzersiz **id**, **görünen ad anahtarı** (yerelleştirme), **açıklama anahtarı**, **ikon**, **etiketler**.

## 19.1 Hedef (Destination)
id, ad, tür (yörünge/cisim), menzil profili (fırlatma/transfer/varış/iniş/dönüş bileşenleri), seyir süresi aralığı, tek yön haberleşme gecikmesi aralığı, pencere periyodu/ofseti, tehlike etiketleri ve şiddetleri (radyasyon, ısı, basınç, toz, atmosfer yoğunluğu), güneş akısı çarpanı, kilit koşulu (araştırma/itibar), harita konumu.

## 19.2 Misyon Tipi (MissionType)
id, ad, zorunlu modül etiketleri, ek gereksinim kuralları, güvenilirlik eşiği, ödül ve risk çarpanı, insanlı mı.

## 19.3 Amaç (Objective)
id, hedef kısıtı, tip kısıtı, gerekli alet kategorileri, minimum veri hacmi, minimum kapsama/süre, bilim değeri, zorluk puanı, ek zorunlu modüller, başarı kriteri tanımı (ölçüt türü + eşik).

## 19.4 Bileşen/Modül (Module)
id, kategori (yük/bus/aşama motoru/tank/yapı/güç/ısı/haberleşme/ADCS/EDL/yaşam destek/kabin...), kütle (kuru), güç üretimi/tüketimi, ısı yükü/kapasitesi, veri hızı/depolama, maliyet, üretim süresi, temel güvenilirlik, araştırma koşulu, uyumlu slot etiketleri, ek etkiler listesi, (motor için) verim/itki/yakıt tipi, (alet için) bilim değeri/veri/çalışma oranı.

## 19.5 Malzeme
id, yapı kütle çarpanı, maliyet çarpanı, dayanım/ısı sınıfı, üretim süresi çarpanı, araştırma koşulu.

## 19.6 Şirket (Supplier)
id, ad, güçlü/zayıf etiketleri, kategori bazlı fiyat çarpanları, teslim süresi çarpanı, güvenilirlik çarpanı, kapasite, ilişki başlangıç puanı, özel yetenekler (ör. insanlı sertifikası), olay eğilimleri, görsel kimlik (renk/logo anahtarı).

## 19.7 Araştırma Düğümü
id, dal, ön koşullar, puan maliyeti, süre, bütçe maliyeti, açtığı modül/etki listesi, açıklama.

## 19.8 Üs Türü/Seviyesi
tür, seviye, kapasite etkisi, bakım gideri, inşa süresi/maliyeti, bölge uygunluk koşulları.

## 19.9 Bölge
id, ad, harita poligonu/konumu, enlem, iklim/hava riski, işçilik/maliyet çarpanı, istikrar, kullanılabilir üs türleri.

## 19.10 Olay
id, kategori (kampanya/operasyon), tetik koşulları, ağırlık hesabı kuralları (tasarım zayıflığına bağlı), seçenekler (metin anahtarı, görünür etki özeti, gerçek etki kuralları, sonuç dağılımı), tekrar kuralı.

## 19.11 Tasarım (Blueprint)
id, ad, aşama listesi (her aşama: motor, tank, malzeme, şirket seçimleri), yük/bus seçimleri, alet listesi, güç/ısı/haberleşme seçimleri, EDL seçimi, tasarlanan hedef/tip bağlantısı, sürüm.

## 19.12 Misyon (Mission Instance)
id, ad, hedef, tip, seçilen amaçlar, türetilmiş MissionProfile, bağlı tasarım, sözleşmeler, durum (planlama/üretim/pencere bekliyor/fırlatıldı/seyir/operasyon/bitti), seed, tarih bilgileri, sonuç raporu.

## 19.13 Kampanya Durumu
tarih, bütçe, itibar, araştırma puanı, bilgi puanı, üsler, araştırma durumu, üretim/test kuyruğu, sözleşmeler/ilişkiler, şablonlar, misyonlar, olay geçmişi, RNG durumu, sürüm.

## 19.14 Komut Kaydı
komut id, tür, yetkili rol maskesi, parametreler, sıra numarası, oyuncu, zaman damgası (oyun içi tick).

---

# 20. TEST, KALİTE VE DENGE STRATEJİSİ

## 20.1 Birim Testleri (Zorunlu — Core/Design/Strategy)
- Roket denklemi ve çok aşamalı Δv doğruluğu (bilinen örnek değerlerle).
- Kütle/maliyet/süre toplamları; sınır durumları (sıfır yakıt, tek aşama, üç aşama).
- Güç dengesi: tutulma, mesafe, RTG.
- Veri/haberleşme darboğazı: teslim edilen veri hesabı.
- Doğrulayıcılar: her ihlal tipi için pozitif/negatif test.
- Determinizm: aynı tohum → aynı sonuç; farklı RNG akışları birbirini etkilemez.
- Komutlar: uygula/geri al simetrisi; yetki; geçersiz durum reddi.
- Kayıt/yükleme: gidiş-dönüş eşitliği; sürüm göçü.
- Misyon Besteleyici: amaç → gereksinim türetme tablosu testleri.

## 20.2 Otomatik Denge Testi ("Bot Oyuncu")
Rastgele/sezgisel tasarım üreten bot: N bin tasarım dener; geçerli çözüm oranı, ucuz/pahalı dağılımı, "tek baskın strateji" var mı ölçer. Sonuçlar denge notlarına yazılır.

## 20.3 Oynanabilirlik Testi
- Her kilometre taşı için "ulaşılabilirlik" kontrolü (bot veya elle).
- Dikey dilim playtest: yeni oyuncu 10 dk içinde ilk tasarımını bitirebiliyor mu?

## 20.4 Platform Testi
- Her kilometre taşında WebGL derleme + tarayıcıda duman testi.
- Çoklu tarayıcı (Chrome, Firefox, Edge), düşük donanım kontrolü.

## 20.5 Kabul İlkesi
Bir görev, ilgili birim testleri geçmeden ve WebGL derlemesi bozulmadan **tamamlanmış sayılmaz**.

---

# 21. YAPILACAKLAR (BACKLOG)

> Her görev: **ID · Ad · Kapsam · Kabul Kriteri**. Sıra, bağımlılık sırasıdır. AI her seferinde bir görev alır (§0.2 şablonu).
> Süre kısıtı olmadığı için görevler **kilometre taşlarına** (M0-M8) göre gruplanmıştır.

## M0 — Temel ve Doğrulama ✅ TAMAMLANDI

> Detaylar ve alınan kararlar için bkz. `Docs/DECISIONS.md` ve `Docs/PROGRESS.md`.
>
> **Önemli notlar (silinmez):**
> - Assembly bağımlılık yönü: `Presentation → Game → (Strategy, Net) → Design → Core`. Tersine referans yasak.
> - Core / Design / Strategy: `noEngineReferences = true`. Doğrulama scripti: `Docs/check-no-engine-refs.ps1`
> - WebSocket paketi: NativeWebSocket — WebGL'de `DispatchMessageQueue()` her frame çağrılmalı (Net katmanında).
> - CI için GitHub Secrets gerekli: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` — bkz. `Docs/CI_KURULUM.md`
> - M0-03 (WebGL yayın linki) ilk CI push'undan sonra tamamlanacak.

## M1 — Çekirdek Hesaplar ve Komut Sistemi
- **M1-01 Deterministik RNG:** Tohumlu, bağımsız akışlar.
  *Kabul:* Aynı tohum aynı dizi; akışlar bağımsız; testler.
- **M1-02 Komut altyapısı:** Command arayüzü, CommandBus, Undo/Redo, rol yetki maskesi, komut kaydı.
  *Kabul:* Uygula/geri al simetrik; yetkisiz komut reddedilir ve sebep verir.
- **M1-03 İstatistik modeli:** Temel+etki, kaynak dökümü.
  *Kabul:* Örnek istatistik toplam/çarpan doğru; döküm tutarlı.
- **M1-04 Roket Δv hesabı:** Çok aşamalı, Isp/kütle, kayıplar.
  *Kabul:* Referans örneklerle uyum (tolerans belirtilmiş).
- **M1-05 Katalog veri yükleyici:** SO + JSON, doğrulama (eksik/yinelenen id).
  *Kabul:* Bozuk veri anlaşılır hatayla reddedilir.
- **M1-06 Blueprint modeli ve istatistik türetme:** Kütle, maliyet, süre, güvenilirlik, güç, ısı, veri/haberleşme.
  *Kabul:* Örnek tasarımlar için beklenen çıktılar; testler.
- **M1-07 Doğrulayıcılar:** Menzil, kütle/kapasite, rampa uyumu, araştırma kilidi, güç, ısı, zorunlu modül, güvenilirlik eşiği.
  *Kabul:* Her ihlal "neden + ipucu" üretir.
- **M1-08 Fırlatıcı kapasite hesabı:** Tasarımdan hedef yörünge kütle kapasitesi.
  *Kabul:* Testler; Mars/Ay profilleriyle tutarlılık.

## M2 — Tasarım Atölyesi Dikey Dilimi (Ay + Rover, Tek Şirket)
- **M2-01 UI temel sistemi:** Tema (USS değişkenleri), tooltip sistemi, ViewModel altyapısı, ekran geçişleri.
  *Kabul:* Örnek iki ekran arası geçiş; tema tek yerden değişir.
- **M2-02 Modül-slot UI:** Slot listesi, seçim, hayalet delta.
  *Kabul:* Hover'da çubuklar değişimi gösterir; seçim Command ile gider.
- **M2-03 Roket/araç kesit görünümü:** Modüler vektör parçalar, snap, canlı birleşme.
  *Kabul:* 3 aşamalı roket + yük otomatik dizilir.
- **M2-04 Sonuç paneli (sade sunum):** Çubuklar, renk dili, kısa "neden?" tooltip.
  *Kabul:* Formül gösterilmez; her çubuk kısa neden metni verir.
- **M2-05 Design Review ekranı:** Kırmızı/sarı/yeşil, neden + ipucu.
  *Kabul:* Tüm doğrulayıcı sonuçları listelenir.
- **M2-06 Şablonlar:** Kaydet/kopyala/yeniden adlandır/karşılaştır.
  *Kabul:* İki tasarım yan yana farklarla gösterilir.
- **M2-07 Dikey dilim içerik:** Ay hedefi, rover tipi, ~12 modül, 3 motor, 3 malzeme, 1 şirket.
  *Kabul:* Oyuncu Ay rover'ı tasarlayıp review'dan geçirebilir.

## M3 — Misyon Besteleyici ve Hedef Sistemi
- **M3-01 Hedef/tip/amaç verisi:** §16.1, §5.2.
  *Kabul:* Tüm hedefler ve amaçlar veri dosyasında; doğrulayıcı geçer.
- **M3-02 MissionComposer (Core):** Seçimler → MissionProfile (menzil, zorunlu modüller, araştırma, eşik, pencere, süre).
  *Kabul:* Tablo testleri (örn. Mars+insanlı vs Mars+rover çıktısı belirgin farklı).
- **M3-03 Zorluk katsayısı ve iddia seviyesi:** Hesap + gösterim.
  *Kabul:* Seçim değişince seviye/ödül/risk güncellenir.
- **M3-04 Besteleyici UI:** Adımlı sihirbaz.
  *Kabul:* Oyuncu hedef→tip→amaçlar→takvim akışını tamamlar, Misyon Dosyası oluşur.
- **M3-05 Misyon Dosyası UI:** Gereksinim listesi, kilitli araştırmalar, "araştırmaya ekle".
  *Kabul:* Kilitli öğe tek tıkla araştırma kuyruğuna girer.
- **M3-06 İnsanlı/rover özel kuralları:** §7.6, §7.7.
  *Kabul:* İnsanlı seçilince kabin/yaşam destek/abort zorunlu; rover'da EDL zorunlu.

## M4 — Strateji Katmanı (HOI4 Hissi)
- **M4-01 CampaignState ve zaman sistemi:** Tick, hız, duraklat, deterministik ilerleme.
  *Kabul:* Aynı komut dizisi aynı durumu üretir.
- **M4-02 Dünya haritası ve bölgeler:** Veri + görünüm + lensler.
  *Kabul:* Bölge seçilebilir; lens değişince bilgi değişir.
- **M4-03 Üs sistemi:** Kurma/yükseltme, kapasite, bakım.
  *Kabul:* Rampa seviyesi fırlatıcı uyumunu etkiler; enlem avantajı hesaba girer.
- **M4-04 Güneş sistemi haritası:** Hareket, pencereler, hedef kartı.
  *Kabul:* Pencereler doğru zamanda parlar; kaçırma hesabı doğru.
- **M4-05 Araştırma sistemi:** Ağaç UI + kuyruk + etkiler.
  *Kabul:* Araştırma tamamlanınca modül açılır/istatistik değişir.
- **M4-06 Şirketler ve sözleşmeler:** Fiyat/teslim/güvenilirlik, ilişki, tek kaynak riski.
  *Kabul:* Şirket seçimi tasarım istatistiğine yansır; sözleşme takvime girer.
- **M4-07 Üretim ve test kuyruğu:** Kapasite, entegrasyon, test etkisi.
  *Kabul:* Gecikmeler ve kapasite sınırı davranışı testli.
- **M4-08 Bütçe/itibar ekonomisi:** Yıllık tahsis, itibar katsayıları, bakım giderleri.
  *Kabul:* Başlangıç dengesi hedefleri (§16.8) için ayar dosyası + playtest notu.
- **M4-09 Olay sistemi (kampanya):** Olay yönetmeni, karar kartı UI, ~15 olay.
  *Kabul:* Seçenek etkileri görünür; seed ile tekrarlanabilir.

## M5 — Operasyon ve Sonuç
- **M5-01 Fırlatma çözümü:** Güvenilirlik, hava, aşama kaybı; görsel sekans.
  *Kabul:* Sonuç deterministik; sebep kaydı oluşur.
- **M5-02 Seyir simülasyonu:** Zaman akışı, küçük olaylar, düzeltmeler.
  *Kabul:* Haritada ilerleme işaretçisi; olaylar tasarım zayıflığına göre ağırlıklanır.
- **M5-03 Varış/iniş (EDL) çözümü:** Atmosfer/iniş yöntemi/ısı kalkanı.
  *Kabul:* Rover/lander için başarı olasılığı tasarıma bağlı; testli.
- **M5-04 Bilim operasyonu:** Güç/ısı/veri/haberleşme akışı, teslim edilen veri.
  *Kabul:* Veri darboğazı bilim skorunu sınırlar.
- **M5-05 Operasyon olayları ve karar kartları:** ~15 olay.
  *Kabul:* Seçimlerin sonuçları görünür ve raporda izlenebilir.
- **M5-06 Sonuç raporu:** Kriterler, neden-sonuç zinciri, kampanya etkisi.
  *Kabul:* Başarısızlıkta zincir doğru ve anlaşılır.
- **M5-07 "Ya şöyle olsaydı?":** Tek karar değişimi + yeniden hesap.
  *Kabul:* Aynı seed ile karşılaştırmalı rapor.
- **M5-08 Skor ve madalyalar:** §11.
  *Kabul:* Ağırlıklar ayar dosyasında; testler.

## M6 — Kampanya Bütünleştirme
- **M6-01 Kilometre taşı/görev tahtası:** Öneri listesi, ilerleme.
  *Kabul:* Taş tamamlanınca bildirim + ödül.
- **M6-02 Kampanya başlangıcı ve bitişi:** Kurgusal ajans kurulumu, bitiş/ceza koşulları, kampanya raporu.
  *Kabul:* Tam kampanya başlat→bitir akışı çalışır.
- **M6-03 Tek misyon kısayolu:** Hazır kaynak/teknoloji paketi.
  *Kabul:* Jüri demosu 3 dakikada ilk misyonu bitirebilir.
- **M6-04 Kayıt/yükleme ve misyon kodu:** Sürümlü JSON, kod içe/dışa aktarma.
  *Kabul:* Gidiş-dönüş eşitliği testli.
- **M6-05 İçerik genişletme:** Tüm hedefler, tipler, şirketler, araştırma ağacı, malzemeler, aletler (§16).
  *Kabul:* İçerik doğrulayıcıdan geçer; Mars insanlı tasarlanabilir.
- **M6-06 Eğitim katmanı:** "Neden?" kartları, sözlük, tutorial.
  *Kabul:* Yeni oyuncu tutorial'ı tamamlar.

## M7 — Online ve Botlar
- **M7-01 Komut yayını katmanı:** Host doğrulama, sıralama, yeni katılana anlık durum.
  *Kabul:* İki istemci aynı durumda kalır (çakışma testi).
- **M7-02 Relay sunucusu:** Oda kodu, 4 katılımcı, bağlantı kopma.
  *Kabul:* 4 tarayıcı aynı odada çalışır.
- **M7-03 Lobi UI ve roller:** Rol seçimi, botları ekle.
  *Kabul:* Roller yetkileri uygular.
- **M7-04 Öneri-onay akışı:** Diff gösterimi, onay/ret, gerekçe.
  *Kabul:* Baş Mühendis önerir → Başkan onaylar zinciri çalışır.
- **M7-05 Gizli öncelikler ve koordinasyon bonusu.**
  *Kabul:* Koşullar sağlanınca bonus ve rapor satırı.
- **M7-06 BotAgent:** Kural tabanlı öneri/onay/itiraz.
  *Kabul:* Tek oyuncuda boş roller botla dolu; bot kararları gerekçeli.
- **M7-07 Liderlik tablosu:** Sunucu yoksa yerel/sahte, varsa sunucu skor doğrulama.
  *Kabul:* Misyon kodu ile skor kaydı/karşılaştırma.

## M8 — Cila ve Teslim
- **M8-01 Görsel cila:** Animasyonlar, parıltılar, harita detayı.
- **M8-02 Ses/müzik entegrasyonu ve lisans listesi.**
- **M8-03 Erişilebilirlik:** Renk körü paleti, yazı ölçeği, ikon/desen.
- **M8-04 TR/EN yerelleştirme tamamlama.**
- **M8-05 Denge turu:** Bot testi + playtest; ayar dosyaları güncelle.
- **M8-06 NASA atıf ekranı ve veri kaynak doğrulaması.**
- **M8-07 Performans/WebGL optimizasyonu:** Boyut, yükleme süresi, bellek.
- **M8-08 Demo hazırlığı:** Altın yol (3 dk), tanıtım videosu, yarışma sayfası metinleri.
  *Kabul (M8 genel):* Çok tarayıcı testi, açık hata yok, jüri demosu sorunsuz.

## Opsiyonel / Sonrası
- Sandbox modu (kısıtsız deneme).
- Canlı NASA API entegrasyonları (APOD arka plan, gerçek yörünge verisi) — çevrimdışı yedekle.
- Rekabet modu (aynı misyon kodu, takımlar arası).
- Ek hedefler (asteroit, Titan vb.).
- Mobil/dokunmatik uyarlama.

---

# 22. RİSKLER

| Risk | Etki | Önlem |
|---|---|---|
| WebGL'de WebSocket/ağ uyumsuzluğu | Online çalışmaz | M0-05'te erken prototip; yedek = bot + yerel |
| Vektör sanat pipeline'ı (paket/kalite/boyut) | Görsel/boyut sorunu | M0-04 karar; güvenli yol PNG atlas |
| Kapsam şişmesi (HOI4 derinliği) | Bitmeyen oyun | Dikey dilim önce (M2); ayrı katman/kilometre taşları; opsiyonel listesi |
| Dengesiz ekonomi | Sıkıcı/imkânsız | Bot denge testi + ayar dosyaları + playtest |
| Gerçeklik hataları | Güven kaybı | Katalog doğrulama + atıf; "oyun için basitleştirilmiş" notu |
| UI karmaşıklığı (yoğun HOI4 arayüzü) | Yeni oyuncu kaybolur | Hayalet delta, kademeli açılım, tutorial, sade sunum |
| Telif/lisans | Yarışma sorunu | Kurgusal şirketler, telifsiz ses/görsel, lisans listesi |
| Unity 2022 UI Toolkit sınırları | Geliştirme yavaşlar | ViewModel deseni; kritik ekranlarda erken prototip |
| WebGL boyut/performans | Yavaş yükleme | Addressables, atlas, ölçüm bütçesi |
| AI'nın mimariyi bozması | Teknik borç | §0 kuralları, kod incelemesi, testlerin geçmesi şartı |

---

# 23. SÖZLÜK (Oyuncuya Sade Dilde Karşılıklar)

| Teknik terim | Oyunda görünen / açıklama |
|---|---|
| Δv (delta-v) | **Menzil**: Roketin yön/hız değiştirebilme gücü |
| Isp | Motor verimi |
| Payload | Faydalı yük |
| Bus | Uzay aracı gövde sistemleri |
| EDL | Giriş ve iniş: gezegen atmosferinden güvenli inme |
| Transfer penceresi | Fırlatma penceresi: gezegenlerin doğru hizalandığı zaman aralığı |
| Downlink | Veriyi yere gönderme kapasitesi |
| Duty cycle | Çalışma oranı |
| RTG | Radyoaktif ısıdan elektrik üreten güç kaynağı |
| Güç marjı | Üretimin tüketimden fazlalığı |
| Tutulma | Güneşin gölgede kalınan süre |
| Güvenilirlik | Başarı olasılığı katsayısı |
| Seed | Aynı sonucu yeniden üreten tohum |

---

# 24. KAYNAKLAR VE ATIFLAR

> Bu bölüm, katalog değerlerini doğrulamak için **kullanılacak kaynak türlerini** listeler. Gerçek rakamlar ve bağlantılar geliştirme sırasında doğrulanıp bu bölüme işlenir (M8-06).

- **Yarışma:** NASA Space Apps Challenge — "Space Mission Design Game" sayfası ve **Resources** sekmesi (önce buradaki veri setleri kullanılmalı; yarışma öncesi güncellenebilir).
- **NASA misyon ve alet bilgileri:** NASA misyon sayfaları, NSSDCA (NASA Space Science Data Coordinated Archive) ana katalog.
- **Yörünge/gezegen verisi:** JPL (Horizons, gezegen fiziksel parametreleri).
- **Gezegen/atmosfer parametreleri:** NASA gezegen fact sheet'leri.
- **Ekzogezegen/hedef bilgisi (opsiyonel):** NASA Exoplanet Archive.
- **Uydu yörüngeleri (opsiyonel, canlı):** CelesTrak.
- **Görsel/içerik (opsiyonel):** NASA Image and Video Library, APOD (kullanım koşulları kontrol edilecek).
- **Roket denklemi ve temel mühendislik:** Standart astrodinamik/uzay sistemleri kitapları ve NASA eğitim materyalleri.
- **Ses/görsel/yazı tipi lisansları:** Kullanılan tüm üçüncü taraf öğelerin lisansları `Docs/Licenses` altında listelenir.

**Atıf ilkesi:** Oyunda "Hakkında" ekranında kaynak listesi bulunur; katalogdaki her değerin kaynak notu veri dosyasında tutulur.

---

# EK A — Açık Sorular (AI veya Takım Cevaplamalı)
1. URP 2D mi, Built-in 2D mi? (M0-01'de performans/WebGL boyutuyla karar.)
2. Vektör pipeline: Vector Graphics paketi mi, PNG atlas mı? (M0-04.)
3. Relay sunucu barındırma ve dil seçimi? (Takım.)
4. Kampanya yıl hızı: 1 oyun yılı ≈ kaç gerçek dakika? (Playtest ile.)
5. Bölge sayısı ve harita ayrıntı düzeyi? (Sanat kapasitesine göre.)
6. Ses kaynağı: özgün üretim mi, telifsiz kütüphane mi? (Takım.)

# EK B — Tanım: "Bitti" (Definition of Done)
- Kabul kriterleri sağlandı.
- Birim testleri yazıldı ve geçiyor.
- Core/Design/Strategy katmanlarında UnityEngine bağımlılığı yok.
- WebGL derlemesi bozulmadı.
- Sabit metin yok (yerelleştirme anahtarı kullanıldı).
- Yeni içerik veri dosyasında; sihirli sayı yok.
- Açık sorular ve varsayımlar dokümante edildi.
- Kullanıcıya kısa özet + sonraki önerilen görev verildi.
