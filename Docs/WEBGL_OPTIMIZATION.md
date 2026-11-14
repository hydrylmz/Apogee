# APOGEE — WebGL ve Performans Optimizasyon Rehberi (M8-07)

> Bu belge GDD §17.3, §17.4, §20.4 ve M8-07 uyarınca APOGEE projesinin tarayıcı ortamında (WebGL) yüksek performans, düşük yükleme süresi ve minimum bellek tüketimiyle çalışması için uygulanan optimizasyon kurallarını ve ayar standartlarını belgeler.

---

## 1. Hedef Metrikler ve Bütçe (Performance & Size Budget)

| Metrik | Hedef Değer | Kritik Eşik | Açıklama |
|---|---|---|---|
| **Sıkıştırılmış Derleme Boyutu** | **< 35 MB** (Brotli) | < 50 MB | İlk sayfa yükleme süresi < 5 saniye (100 Mbps bağlantıda) |
| **İlk İndirme (Wasm + Data)** | **< 25 MB** | < 40 MB | `build.wasm.br` + `build.data.br` toplamı |
| **Kare Hızı (FPS)** | **60 FPS** (Masaüstü) / **30+ FPS** (WebGL) | < 30 FPS | UI animasyonları ve harita kaydırmalarında akıcı tepki |
| **Başlangıç Bellek Tahsisi** | **256 MB** | 512 MB | WebGL Wasm Heap; aşırı bellek tüketiminde sekme çökmesini önler |
| **Tasarım İstatistik Hesabı** | **< 5 ms** | < 16 ms | Tek slot bileşeni değişiminde 1 frame içinde anlık tepki |
| **Günlük Zaman İlerlemesi (Tick)** | **< 2 ms** | < 8 ms | Strateji katmanında günlük döngü ve olay değerlendirmesi |

---

## 2. Unity PlayerSettings WebGL Konfigürasyonu

`ProjectSettings/ProjectSettings.asset` içinde geçerli kılınan üretim ayarları:

```yaml
# Sıkıştırma ve Dağıtım
webGLCompressionFormat: 2          # 2 = Brotli (En yüksek sıkıştırma oranı)
webGLDecompressionFallback: 1      # 1 = Gzip/Brotli desteklemeyen eski sunucularda JS fallback
webGLDataCaching: 1                # 1 = IndexedDB üzerinde tarayıcı önbelleklemesi (sonraki açılışlar anlık)

# Bellek Yönetimi (Heap Configuration)
webGLInitialMemorySize: 256        # 256 MB başlangıç heap boyutu
webGLMaximumMemorySize: 1024       # 1024 MB maksimum tavan
webGLMemoryGrowthMode: 2           # Geometric büyüme modu (belge genişledikçe orantılı tahsisat)
webGLMemoryGeometricGrowthStep: 0.2
webGLMemoryGeometricGrowthCap: 96

# Kod Soyma ve Boyut Küçültme
stripEngineCode: 1                 # Kullanılmayan Unity C++ motor modüllerini çıkarır
ManagedStrippingLevel: High        # Kullanılmayan C# IL kodlarını ve sınıflarını ayıklar
webGLExceptionSupport: 1           # Explicitly Thrown Only (Boyutu %15 düşürür)
webGLDebugSymbols: 0               # Release derlemesinde debug sembolleri kapalı
```

---

## 3. Mimari Bellek ve Çöp Toplayıcı (GC) Optimizasyonları

APOGEE'nin saf C# mimarisi (GDD §0.1) WebGL'in en kritik darboğazı olan Garbage Collection (GC) duraksamalarını engellemek üzere tasarlanmıştır:

1. **`noEngineReferences=true` Katmanları (Core / Design / Strategy):**
   - Unity `MonoBehaviour` ve `GameObject` yükü taşımaz.
   - Nesneler struct ve saf C# POCO olarak yaşar.
2. **Deterministik Komut Sistemi (CommandBus):**
   - Geri alma yığını `maxHistorySize=50` ile sınırlandırılmıştır; bellek sızıntısı yapmaz.
   - Komut yürütme sırasında yeni koleksiyon tahsis edilmez; mevcut durum referansları güncellenir.
3. **StatCollection ve Pipeline Hesaplamaları:**
   - Değer hesaplamaları struct tabanlı çarpan döngüleriyle (`StatModifier`) yapılır.
   - String birleştirmeler yerine enum tabanlı `StatType` anahtarları ile O(1) arama kullanılır.
4. **JSON Yükleme ve Kataloglar:**
   - Oyun başlangıcında tüm JSON katalogları bir kez belleğe alınır (`CatalogLoader`), oyun esnasında tekrar eden JSON parsing yapılmaz.
   - Yerelleştirme sözlüğü `Dictionary<string, string>` ile bellek içi O(1) hash tablosunda tutulur.

---

## 4. Grafik, UI Toolkit ve Varlık Optimizasyonları

1. **Vektörel Sanat ve Sprite Atlas:**
   - ADR-001/002 uyarınca SVG kaynak çizimler yüksek çözünürlüklü PNG sprite olarak dışa aktarılmış ve tek bir Sprite Atlas içinde birleştirilmiştir.
   - Draw call sayısı tüm ekranlarda 5-15 arasında tutulur.
2. **UI Toolkit USS Animasyonları:**
   - Animasyonlar CPU tabanlı `Update()` döngüleri yerine USS `transition-property` üzerinden GPU/C++ motor katmanında işlenir.
   - Gereksiz layout reflow'ları önlemek için sabit genişlik ve flex kuralları tanımlanmıştır.
3. **Ses Sıkıştırması:**
   - Müzik parçaları WebGL için Vorbis / OGG formatında ve 96 kbps kalitede sıkıştırılır.
   - Kısa SFX efektleri bellekte önceden yüklenmiş `DecompressOnLoad` olarak tutulur.

---

## 5. Web Sunucusu ve MIME Type Dağıtım Kılavuzu

WebGL Brotli sıkıştırılmış dosyaların doğru sunulabilmesi için web sunucusunda aşağıdaki HTTP başlıklarının tanımlanması şarttır:

### Nginx Konfigürasyonu
```nginx
location ~ .+\.(data|symbols\.json)\.br$ {
    gzip off;
    add_header Content-Encoding br;
    default_type application/octet-stream;
}
location ~ .+\.wasm\.br$ {
    gzip off;
    add_header Content-Encoding br;
    default_type application/wasm;
}
location ~ .+\.js\.br$ {
    gzip off;
    add_header Content-Encoding br;
    default_type application/javascript;
}
```

### Apache `.htaccess` Konfigürasyonu
```apache
<IfModule mod_mime.c>
  AddEncoding br .br
  AddType application/wasm .wasm
  AddType application/javascript .js
  AddType application/octet-stream .data
</IfModule>
```

---

## 6. Uçuş Tiyatrosu WebGL Denetimi (ADR-061 / M9-15)

Uçuş Tiyatrosu (Launch Sequence + Operations ekranları) WebGL'de sürekli çalışan tek
ağır bileşendir. Aşama 7 ile birlikte şu denetim sonuçları kayda alınmıştır:

| Öğe | Uygulama | Denetim Sonucu |
|---|---|---|
| **Repaint Hızı** | `FlightStageView` tek `schedule.Execute(...).Every(33 ms)` -> **≤30 Hz** (60 FPS ekranlarda bile) | OK Bütçe içinde |
| **Repaint İzleme** | `FlightPerformanceBudget.RecordRepaint()` her `OnAnimationTick` çağrısında sayılır; 1 sn'lik pencerede 30 üzeri aşım `IsRepaintBudgetExceeded` bayrağını yakar | OK Canlı izleme (HUD alt bandı) |
| **Partikül Tahsisi** | `ParticlePool` 20 slot, sıfır heap alloc; beat başına `Emit` havuzdan alır | OK GC duraksaması yok |
| **Ses Tetikleme Frekansı** | `FlightPerformanceBudget.RecordAudioTrigger()` beat başına; pencere başına ≤4 tetikleme hedefi | OK 1 sn'de en fazla 1 beat tetiklemesi |
| **Ses Dosyaları** | `UnityAudioService` `Resources/Audio/UI/*.ogg` (Kenney CC0); müzik anahatları Inspector eşlemesiyle verilir | OK Ek indirme bütçesi < 2 MB |
| **Ekran Kapalıyken Yük** | `OnHidden()` -> `StopAnimation()` + `StopClockTicker()` + `StopMusic()` | OK Sıfır tick |
| **ReduceMotion** | Ayar açıkken: kamera anlık konum, partikül temiz, egzoz animasyonu durur, müzik crossfade kapalı | OK |
| **Bütçe Özeti** | `FlightPerformanceBudget.GetSummary()` -> Launch/Operations footer'ında ~1 Hz güncellenen geliştirici satırı (`Repaint: 30/s OK \| Audio: 1/s OK`) | OK |

**Sonuç:** Uçuş Tiyatrosu, WebGL bütçesi (≤30 Hz repaint, sıfır per-frame heap alloc,
256 MB heap) ile uyumludur. Kalıcı ihlal durumunda `IsRepaintBudgetExceeded`
bayrağı kullanılarak beat süreleri dinamik olarak uzatılabilir (ileri iş).
