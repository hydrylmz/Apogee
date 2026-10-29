# APOGEE — Mimari Kararlar (ADR)

> Bu dosya M0 sırasında alınan ve GDD'yi tamamlayan teknik kararları kayıt altına alır.

---

## ADR-001: Render Pipeline → URP 2D (M0-04)

**Durum:** Kesinleşti  
**Karar:** Universal Render Pipeline (URP) 2D — `com.unity.render-pipelines.universal` v14.0.12  
**Gerekçe:** Proje URP 2D şablonuyla oluşturulmuş; Renderer2D.asset, UniversalRP.asset hazır. 2D ışık, normal map ve post-process gerektiğinde destekli. WebGL uyumu iyi.  
**Sonuç:** `Assets/Settings/` içindeki mevcut URP ayarları korunur; Built-in 2D'ye geçiş yok.

---

## ADR-002: Vektör Pipeline → PNG Atlas (M0-04)

**Durum:** Kesinleşti  
**Karar:** SVG kaynak → yüksek çözünürlüklü PNG dışa aktarma + Sprite Atlas  
**Gerekçe:** Unity Vector Graphics paketi (com.unity.vectorgraphics) önizleme aşamasında; WebGL performansı ve build boyutu riskli. PNG atlas yolu kararlı, Addressables ile boyut kontrol edilebilir.  
**Sonuç:** Sanat pipeline = Inkscape/Figma → PNG export → Sprite Atlas → Addressables. Vektör paketi tekrar değerlendirilebilir (M8 öncesi).

---

## ADR-003: WebSocket Paketi → NativeWebSocket (M0-05)

**Durum:** Kesinleşti  
**Karar:** `com.endel.nativewebsocket` — [github.com/endel/NativeWebSocket](https://github.com/endel/NativeWebSocket)  
**Gerekçe:**  
- WebGL'de gerçek `WebSocket` API'sini kullanır (JavaScript bridge ile). Diğer paketler (`websocket-sharp`, `System.Net.WebSockets`) WebGL'de çalışmaz.  
- Standart `UnityEngine.WebGL` dışı ek bağımlılık yok; hafif (~200 satır).  
- MIT lisanslı, aktif bakım altında.  
- `Apogee.Net` assembly'sinde izole kullanılacak; diğer katmanlar bağımlı olmayacak.  
**Kurulum:** `Packages/manifest.json`'a eklendi:
```json
"com.endel.nativewebsocket": "https://github.com/endel/NativeWebSocket.git#upm"
```
**Uyarı:** WebGL'de `NativeWebSocket.WebSocket.DispatchMessageQueue()` her frame `Update()` içinde çağrılmalı. Bu çağrı `Apogee.Net` katmanındaki bir `NetworkManager` MonoBehaviour'da yapılacak; Core/Design/Strategy'e sızmayacak.

---

## ADR-004: CI Sistemi → GameCI + GitHub Actions (M0-02)

**Durum:** Kesinleşti  
**Karar:** GameCI (`game-ci/unity-test-runner@v4` + `game-ci/unity-builder@v4`) + GitHub Actions  
**Repo:** https://github.com/hydrylmz/Project-Nexir  
**İş akışı:** `.github/workflows/ci.yml`  
- `push` / `PR` → EditMode testleri → WebGL build  
- Build boyutu her çalışmada raporlanır (§17.7 bütçe takibi)  

### Gerekli GitHub Secrets (repo → Settings → Secrets → Actions)

| Secret | Nasıl alınır |
|---|---|
| `UNITY_LICENSE` | Yerel makinede Unity → Help → Manage License → XML lisans dosyası içeriği |
| `UNITY_EMAIL` | Unity hesap e-postası |
| `UNITY_PASSWORD` | Unity hesap şifresi |

### Lisans XML alma adımları
1. Unity 2022.3'ü aç → **Help → Manage License → Activate New License → Unity Personal**
2. `%APPDATA%\Unity\Unity_lic.ulf` dosyasını aç, tüm içeriği kopyala
3. GitHub repo → Settings → Secrets → `UNITY_LICENSE` olarak ekle

> **Not:** GameCI ücretsiz plan ayda 400 dakika Linux dakikası verir. EditMode test + WebGL build ~15-25 dk sürer; ayda ~15-25 push için yeterli.

---

---

## ADR-005: Katalog Veri Formatı → Saf JSON (M1-05)

**Durum:** Kesinleşti  
**Karar:** Tüm katalog verisi (modül, hedef, tedarikçi, araştırma, olay, amaç) **sadece JSON** olarak saklanır. ScriptableObject kullanılmaz.  
**Gerekçe:** Kullanıcı tercihi. JSON, herhangi bir metin editörüyle düzenlenebilir; Unity Inspector bağımlılığı yoktur; Git diff'lerinde okunabilir; CI'da doğrulama betikleriyle kolayca parse edilir.  
**Sonuç:**  
- Katalog JSON dosyaları: `Assets/_Project/Data/Catalog/Json/` altında (`.json` uzantılı).  
- `CatalogLoader` statik sınıfı JSON string → `CatalogCollection<T>` dönüşümünü sağlar (Newtonsoft.Json).  
- `Apogee.Data.asmdef` Newtonsoft.Json'a bağımlıdır; Unity'nin `com.unity.nuget.newtonsoft-json` paketi kullanılır.  
- `STANDARDS.md §3` tablosu bu karara göre güncellendi (SO satırları kaldırıldı — yalnızca JSON).  
- Kayıt şemaları: `ICatalogRecord`, `ModuleRecord`, `DestinationRecord`, `SupplierRecord` (ve ilerleyen M'lerde diğerleri).  

---

## ADR-006: Doğrulayıcı Mimarisi → Saf Fonksiyonlar (M1-07)

**Durum:** Kesinleşti  
**Karar:** Her validator, `(Blueprint, BlueprintStats, ValidationContext) → IEnumerable<ValidationIssue>` imzasına sahip saf bir fonksiyon olarak uygulanır. Yan etkisiz; tüm girdi immutable record'lardan okunur.  
**Gerekçe:**  
- GDD §17.3 madde 6: "validators are pure functions" gerekliliği.  
- Coordinator (`BlueprintValidator`) tüm 8 validator'ı bir `ValidationReport` içinde toplar; `IsValid = no Errors` (Warning'lar bloklamaz).  
- Her validator bağımsız test edilebilir; integration testi ise `BlueprintValidator` üzerinden yapılır.  

**Sonuç:**  
- `IBlueprintValidator`: tek metot `IEnumerable<ValidationIssue> Validate(Blueprint, BlueprintStats, ValidationContext)`.  
- 8 validator: `DeltaVValidator`, `MassCapacityValidator`, `LaunchPadValidator`, `ResearchLockValidator`, `PowerValidator`, `ThermalValidator`, `MandatoryModuleValidator`, `ReliabilityValidator`.  
- `ValidationReport`: tüm Issues listesi + `IsValid` + `Errors`/`Warnings`/`Infos` filtrelenmiş koleksiyonlar.  
- `ValidationSeverity` enum: `Info | Warning | Error`.  
- `ValidationRuleId` enum: her validator için benzersiz rule ID'leri.  
- Oyuncu mesajları (Reason/Hint/Title) GDD §23 gereği Türkçe; localization key'e geçiş M2/M3'te yapılacak.  
- `DesignComponent.RequiredResearchId` ve `MaterialSpec.RequiredResearchId` property'leri `ResearchLockValidator`'ı desteklemek için eklendi.  
- `CatalogLoader` null-id pre-check eklendi (boş ID'li kayıtlarda açıklayıcı hata raporlar).  

---

## ADR-007: Fırlatıcı Kapasite Hesabı → İkili Arama (Binary Search) (M1-08)

**Durum:** Kesinleşti  
**Karar:** Çok aşamalı roket yığınının belirli bir hedef $\Delta v$ ihtiyacına taşıyabileceği azami faydalı yük kütlesi (`MaxPayloadMass`), `LaunchCapacityCalculator` içinde ikili arama (bisection search) ile hesaplanır.  
**Gerekçe:**  
- Çok aşamalı Tsiolkovsky roket denklemi analitik/kapalı formda tersine çevrilemez; çünkü her aşamanın kuru kütlesi ve yakıtı üst aşamaların toplam kütlesine bağlıdır.  
- Fonksiyon $f(m) = \Delta v(m) - \Delta v_{req}$ kütleye göre kesin azalandır (monotonik), bu nedenle tek bir köke sahiptir ve $\le 64$ iterasyonda $< 1\text{ m/s}$ toleransla yakınsar.  
- Hesaplama tamamen deterministiktir (`System.Random` veya `DateTime` içermez).  

**Sonuç:**  
- `LaunchCapacityResult`: immutable sonuç modeli (`MaxPayloadMass`, `CapacityMargin`, `IsWithinCapacity`, `NetDeltaVAtMaxPayload`).  
- `LaunchCapacityCalculator`: saf C# statik sınıf (`Apogee.Design`).  
- `StatType.PayloadCapacity` eklendi.  
- `BlueprintStats`: `LaunchCapacity` ve `MaxPayloadCapacity` erişimcileri ile güncellendi.  
- `BlueprintEvaluator`: opsiyonel `requiredDeltaV` veya `MissionProfile` parametresiyle kapasite hesabını tetikler.  
- `PayloadCapacityValidator`: `ValidationRuleId.PayloadCapacity` ("VAL_PAYLOAD_CAPACITY") kuralı üzerinden 9. standart validator olarak eklendi.  

---

## ADR-008: UI Temel Mimarisi — UI Toolkit, ViewModel & Navigation (M2-01)

**Durum:** Kesinleşti  
**Karar:** UI Toolkit runtime kullanılarak tek merkezden yönetilen USS tema sistemi (`Variables.uss`, `Common.uss`, `Theme.uss`), `ViewModelBase` ve `ObservableValue<T>` reaktif desenleri, merkezi `TooltipService` ve `ScreenNavigator` durum yönlendiricisi kuruldu.  
**Gerekçe:**  
- Unity 2022.3 LTS'te runtime data-binding desteği bulunmadığından GDD §14.5 gereği ViewModel + olay tabanlı güncelleme mimarisi zorunludur.  
- UI bileşenleri (`Apogee.UI`) bağımsız olarak test edilebilir saf C# ViewModelleri ve durum yöneticileri üzerine inşa edilmiştir.  
- Tema renk paleti (Koyu lacivert zemin, mat çelik paneller, amber uyarı, cyan telemetri, kırmızı engel) GDD §15.1 ile birebir eşleştirilmiştir.  

**Sonuç:**  
- `Variables.uss`, `Common.uss`, `Theme.uss`: Tek kaynaktan tema ve UI değişkenleri.  
- `ViewModelBase`, `ObservableValue<T>`, `ViewActionCommand`: Reaktif durum yönetimi ve UI eylem bağlamaları.  
- `ITooltipService`, `TooltipService`, `TooltipData`, `TooltipManipulator`, `TooltipView`: Zengin ve ekran sınırlarına kenetlenen tooltip altyapısı.  
- `ScreenId`, `IScreenController`, `ScreenNavigator`, `ScreenControllerBase<TViewModel>`: Ekranlar arası geçiş ve geri-yığın (history) mekanizması.  
- `WorkshopScreenController` ve `ReviewScreenController`: M2 dikey dilimi için örnek iki ekran ve geçiş doğrulaması.  
- `ApogeeUIManager`: Root `UIDocument` üzerinde tüm sistemi birleştiren ana sunum bileşeni.  

---

## ADR-009: Modül-Slot Sistemi, Command Tabanlı Seçim & Hayalet Delta (M2-02)

**Durum:** Kesinleşti  
**Karar:** Araç bileşen slotları (`VehicleSlotId`, `VehicleSlotDefinition`), değişmez (immutable) blueprint dönüşümleri (`WithSlotComponent`, `GetComponentInSlot`), `IBlueprintHolder` üzerinde çalışan ve `CommandBus` tarafından yürütülen `SetSlotComponentCommand`, ve saf fonksiyonel `GhostDelta` telemetri fark motoru uygulandı.  
**Gerekçe:**  
- GDD §0.1 madde 2 ve §0.3 kuralı: "Oyun durumu tek bir CampaignState nesnesidir; değişiklikler sadece Command'lar ile yapılır (undo, online ve kayıt buna dayanır)." UI asla durumu doğrudan değiştiremez.  
- GDD §21 M2-02 kabul kriterleri: "Hover'da çubuklar değişimi gösterir; seçim Command ile gider."  
- Değişmez (immutable) veri yapısı sayesinde slot seçimi, `GhostDelta` hesaplamasında mevcut aracı kirletmeden ad hoc önizleme imkânı tanır ve simetrik `Undo`/`Redo` işlemlerini hatasız kılar.  

**Sonuç:**  
- `VehicleSlotId`: Faydalı yük ve roket aşamaları için standart slot numaralandırması (Bus, Power, Thermal, Comms, ADCS, EDL, Instruments, Stage 1-3 Engine/Tank).  
- `VehicleSlotDefinition`: Kategori etiketi, grup adı, zorunluluk durumu ve kullanıcı dostu başlık metadata'sı.  
- `Blueprint.WithSlotComponent` & `GetComponentInSlot`: Saf fonksiyonel slot bileşen okuma ve yazma.  
- `IBlueprintHolder`: Aktif araç şablonunu taşıyan durum bağlam arayüzü.  
- `SetSlotComponentCommand`: `CommandBase<IBlueprintHolder>` tabanlı; `PlayerRole.LeadEngineer | PlayerRole.AgencyDirector` yetki maskesi, simetrik `Undo` ve `CommandBus` tam entegrasyonu.  
- `GhostDelta`: Mevcut ve aday şablon istatistiklerini karşılaştırıp $\Delta \Delta v$, $\Delta \text{Mass}$, $\Delta \text{Cost}$, $\Delta \text{Reliability}$, $\Delta \text{PowerMargin}$, $\Delta \text{BuildDays}$ farklarını ve Türkçe özet metnini üreten saf sınıf.  
- `VehicleSlotViewModel`, `ModuleOptionViewModel`, `GhostDeltaViewModel`: Reaktif sunum modelleri.  
- `WorkshopScreenController`: Sol kolonda kategorize edilmiş slot listesi, uyumlu modül seçim çekmecesi, kart üzerinde hover ile anlık hayalet delta aktivasyonu ve tıklandığında `CommandBus` üzerinden yürütme; sağ panelde telemetri çubukları üzerinde renkli fark göstergeleri ve önizleme kutusu.  
- `modules.json`: GDD §16.3/16.4/16.5 ile uyumlu roket ve uzay aracı modül kataloğu.  

---

## ADR-010: Roket ve Araç Kesit Görünümü, Modüler Canlı Montaj ve Otomatik Snap (M2-03)

**Durum:** Kesinleşti  
**Karar:** 2D vektörel CAD roket ve uzay aracı kesit görünümü (`CraftCrossSectionLayout`, `CrossSectionNode`, `SnapPoint`), `RocketCrossSectionViewModel` ve UI Toolkit `RocketCrossSectionView` bileşeni üzerinden uygulandı.  
**Gerekçe:**  
- GDD §7.2, §14.3 ve §15.1: Tasarım Atölyesi'nin merkezinde, 3 aşamalı roket + uzay aracı yükünü otomatik olarak dizen, snap noktalarıyla kenetleyen ve şirket renk şeritlerini gösteren interaktif bir 2D kesit görünümü yer almalıdır.
- GDD §0.1 İlke 1: Kesit hesaplama ve dikey montaj düzeni mantığı (`Apogee.Design`), `UnityEngine` bağımlılığı içermeyen saf C# sınıfları olarak yazıldı (`noEngineReferences = true`).
- GDD §21 M2-03 kabul kriteri: "3 aşamalı roket + yük otomatik dizilir."

**Sonuç:**  
- `CrossSectionPartType`: Yapısal roller (FairingNose, PayloadInstrument, PayloadBus, PayloadSubsystem, PayloadEdl, InterstageDecoupler, StageTank, StageEngine, AeroFins).
- `SnapPoint`: Modüler parça bağlantı noktaları ve kenetlenme durumu (`X, Y, IsConnected`).
- `CrossSectionNode`: Her montaj modülü için kütle, malzeme, tedarikçi renk kodu (`SupplierColorHex`), bağıl genişlik/yükseklik ve bağlı olduğu `VehicleSlotId` bilgilerini taşıyan saf model.
- `CraftCrossSectionLayout`: Blueprint'i analiz ederek 1, 2 veya 3 aşamalı roket ve faydalı yük bloklarını otomatik dikey sırada dizen, merkez hattı üzerinden auto-snap bağlantılarını kuran ve tedarikçi renk şeritlerini çözümleyen saf motor.
- `RocketCrossSectionViewModel`: CAD montaj durumu, seçili parça/slot senkronizasyonu ve hover callout bilgilerini yöneten ViewModel.
- `RocketCrossSectionView`: Tasarım Atölyesi orta sütununa entegre edilen UI Toolkit CAD görünümü; yükseklik cetveli (ruler), CAD grid arka planı, parçaya tıklandığında sol çekmecede ilgili slotu açan iki yönlü etkileşim, boş yuvalar için kesikli hayalet çerçeve ve canlı montaj desteği.
- `Common.uss`: CAD kesit görünümü, parça durumları ve snap konnektör stilleri.

---

## ADR-011: Sonuç Paneli (Sade Sunum) ve Telemetri Çubukları (M2-04)

**Durum:** Kesinleşti  
**Karar:** `ResultBarStatus` (Good/Warning/Error/Neutral), `ResultBarViewModel`, `ResultsPanelViewModel` (ValidationReport ve BlueprintStats üzerinden 7 temel çubuk üretimi) ve UI Toolkit `ResultsBarView` bileşeni uygulandı.  
**Gerekçe:**  
- GDD §8.3 (Sade Sunum İlkesi): Oyuncuya asla ham formüller veya karmaşık katsayılar gösterilmez. Çubuklar (Menzil, Kütle, Güvenilirlik, Güç Dengesi, Isıl Denge, Maliyet, Üretim Süresi) yeşil (rahat), sarı (sınırda/uyarı) ve kırmızı (yetersiz/engel) renk diliyle sunulur.  
- GDD §13.2 ("Neden?" İpuçları): Her çubuğun üzerine gelindiğinde `TooltipManipulator` aracılığıyla tek cümlelik sade neden açıklaması ve rehberlik sağlanır.  
- GDD §0.1 İlke 1: İstatistik değerlendirme ve çubuk türetme mantığı (`Apogee.Design`), `UnityEngine` bağımlılığı olmayan saf C# sınıfları olarak yazıldı (`noEngineReferences = true`).  
- GDD §21 M2-04 kabul kriterleri: "Formül gösterilmez; her çubuk kısa neden metni verir."

**Sonuç:**  
- `ResultBarStatus`: `Good` (yeşil), `Warning` (kehribar/sarı), `Error` (kırmızı), `Neutral` (nötr camgöbeği) trafik ışığı durumları.  
- `ResultBarViewModel`: Saf C# model; `Key`, `Label`, `FillRatio` ([0, 1] arasına kenetlenen), `Status`, `ValueText` ve `ReasonText` alanları.  
- `ResultsPanelViewModel`: `ValidationReport` bulgularını (veya salt `BlueprintStats` nesnesini) analiz ederek 7 standart telemetri çubuğunu türeten saf fabrika. Doğrulayıcı hataları (örn. yetersiz $\Delta v$, güç açığı, düşük güvenilirlik) çubuk rengine ve durumuna doğrudan yansır.  
- `ResultsBarView`: Reaktif UI Toolkit bileşeni; değer etiketi, renk geçişli dolgu çubuğu, inline gerekçe ipucu ve hover tooltip entegrasyonu.  
- `ReviewViewModel` & `ReviewScreenController`: Gerçek `BlueprintValidator` koordinatörü ile entegre edildi; doğrulama raporu hem sol taraftaki 7 sonuç çubuğuna hem de sağ taraftaki bulgu kartlarına dinamik olarak beslenir.  
- `Common.uss`: Sonuç paneli, çubuk stilleri ve durum sınıfları eklendi.  
- 10 yeni test (toplam 57 yerel doğrulama testi) başarıyla geçti.

---

## ADR-012: Design Review Ekranı, Kırmızı/Sarı/Yeşil Trafik Işığı Listesi ve Kategori Filtreleme (M2-05)

**Durum:** Kesinleşti  
**Karar:** `ReviewFilterMode` (Tümü / Engeller / Uyarılar / Tamamlanan), zenginleştirilmiş `ReviewIssueItem` ("Neden" ve "Neye bakmalı / İpucu" ayrımı, kural kimliği, sayısal değerler), `ReviewNavigationArgs` (Atölyeden İnceleme ekranına canlı şablon ve bağlam aktarımı), `ReviewViewModel` (GO / NO-GO ve Şartlı GO durum değerlendirmesi, hata/uyarı/tamam sayaçları) ve `ReviewScreenController` (iki kolonlu düzen, interaktif filtreleme çubuğu, hover tooltip'ler, canlı reaktif yenilenme) uygulandı.  
**Gerekçe:**  
- GDD §7.4: Tasarım İnceleme (Design Review) aşamasında araç 3 renk koduyla denetlenir:
  - Kırmızı = Engel (menzil açığı, rampa uyumsuzluğu, kütle aşımı, kilitli araştırma, güç açığı, eksik zorunlu modül). Fırlatmayı engeller (NO-GO).
  - Sarı = Uyarı (düşük güvenlik marjı, sınırda kapasite, kritik güvenilirlik). Fırlatmaya izin verilir ancak risk taşır (Şartlı GO).
  - Yeşil = Tamam (tüm kriterler güvenli marjla sağlandı). Fırlatmaya hazır (GO).
- GDD §7.4 & §8.3: Her kontrol maddesi sade bir dille **"Neden"** (hatayı veya başarıyı doğuran sebep) ve **"Neye bakmalı"** (çözümü doğrudan dikte etmeden oyuncuyu doğru alt sisteme yönlendiren ipucu) sunmalıdır. Formül veya ham katsayı gösterilmez.  
- GDD §21 M2-05 kabul kriteri: "Kırmızı/sarı/yeşil, neden + ipucu. Kabul: Tüm doğrulayıcı sonuçları listelenir."

**Sonuç:**  
- `ReviewNavigationArgs`: Atölye ekranından İnceleme ekranına `Blueprint` ve `ValidationContext` taşıyan güvenli gezinti argümanı.  
- `ReviewFilterMode`: `All`, `Errors`, `Warnings`, `Passed` filtreleme modları.  
- `ReviewIssueItem`: `Title`, `Reason` ("Neden"), `Hint` ("Neye bakmalı"), `Severity` ("Error" | "Warning" | "Info"), `RuleId`, `CurrentValue`, `TargetValue`, `IsError`, `IsWarning`, `IsPass`, `SeverityBadgeText` ("ENGEL" | "UYARI" | "TAMAM") ve `SeveritySymbol` ("⛔" | "▲" | "✓") alanlarını barındıran veri modeli.  
- `ReviewViewModel`: 9 çekirdek doğrulayıcıyı çalıştırır, sonuçları `ResultBars` ve `Issues` listelerine işler; `GoNoGoBadgeText`, `ErrorCount`, `WarningCount`, `PassCount`, `IsValid` ve `FilteredIssues` alanlarını günceller; filtre değişimlerinde `OnIssuesChanged` olayını tetikler.  
- `ReviewScreenController`:
  - Üst başlık: GO / NO-GO rozeti, Engel, Uyarı ve Tamam sayaç hapları, özet durum metni.  
  - Sol sütun: 7 telemetri çubuğu ile anlık araç performans özeti (ResultsPanel).  
  - Sağ sütun: 4 butonlu interaktif filtreleme araç çubuğu (`Tümü (n)`, `⛔ Engeller (n)`, `▲ Uyarılar (n)`, `✓ Tamam (n)`), kırmızı/sarı/yeşil sol kenar vurgulu ve hover tooltip destekli bulgu kartları listesi (`ScrollView`). Filtre sonucu boşsa bilgilendirici pozitif mesaj.  
  - Alt çubuk: "← Atölyeye Geri Dön" butonu.  
- `WorkshopViewModel`: `GoToReviewCommand` tetiklendiğinde `ActiveBlueprint` ve Ay hedefi doğrulama bağlamını (`ReviewNavigationArgs`) iletir; `ReviewScreenController.OnShown` bu argümanı yakalayarak canlı şablonu inceler.  
- `Common.uss`: İnceleme filtre butonları, aktif durum göstergesi ve renkli kart stilleri eklendi.  
- 4 yeni doğrulama testi (toplam 61 yerel doğrulama testi) başarıyla geçti.

---

## ADR-013: Tasarım Şablonları, Karşılaştırma Ekranı ve Misyon Kodu (M2-06)

**Durum:** Kesinleşti  
**Karar:** Tasarım şablonu modeli (`DesignTemplate`, `ITemplateRepository`, `InMemoryTemplateRepository`), Command tabanlı şablon operasyonları (`SaveTemplateCommand`, `DuplicateTemplateCommand`, `RenameTemplateCommand`, `DeleteTemplateCommand`), taşınabilir kompakt misyon kodu sistemi (`MissionCodeCodec`), yan yana diferansiyel analiz motoru (`BlueprintDifference`, `BlueprintComparator`), karşılaştırma ekranı (`ComparisonViewModel`, `ComparisonScreenController`) ve Atölye şablon paneli entegrasyonu (`TemplateManagerViewModel`) uygulandı.  
**Gerekçe:**  
- GDD §7.5: Tasarımları kaydetme, kopyalama, yeniden adlandırma, iki şablonu yan yana farklarla karşılaştırma ve paylaşılabilir kısa misyon kodu olarak dışa/içe aktarma.
- GDD §14.1 (#17): Karşılaştırma Ekranı (Comparison Screen). İki tasarımın telemetri değerlerini ve donanım slotlarını yan yana fark göstergeleriyle (yeşil/kırmızı delta) sunma.
- GDD §0.1 İlke 1: Şablon ve karşılaştırma mantığı `Apogee.Design` katmanında `UnityEngine` bağımlılığı olmadan saf C# olarak geliştirildi (`noEngineReferences = true`).
- GDD §0.1 İlke 2 & STANDARDS.md Madde 6: Şablon kaydetme, çoğaltma, yeniden adlandırma ve silme işlemleri `CommandBase` türevi komutlar olarak yazıldı, `CommandBus` üzerinden yetki ve Undo/Redo desteği sağlandı.
- GDD §21 M2-06 kabul kriteri: "İki tasarım yan yana farklarla gösterilir."

**Sonuç:**  
- `DesignTemplate`: Şablon üst verileri (`Id`, `Name`, `Description`, `CreatedAtTicks`, `Blueprint`, `Tags`) ve saf mutasyonlar (`WithName`, `WithBlueprint`, `Clone`).
- `ITemplateRepository` & `InMemoryTemplateRepository`: CRUD, çoğaltma (`Duplicate`), yeniden adlandırma (`Rename`) ve reaktif bildirim (`OnTemplatesChanged`).
- `SaveTemplateCommand`, `DuplicateTemplateCommand`, `RenameTemplateCommand`, `DeleteTemplateCommand`: LeadEngineer/AgencyDirector yetkili, simetrik Undo destekli komutlar.
- `MissionCodeCodec`: WebGL güvenli, UTF8 + Base64 formatında deterministik paylaşılabilir misyon kodlama (`APG1-...`) ve çözme.
- `BlueprintDifference` & `BlueprintComparator`: Menzil ($\Delta v$), kütle, maliyet, güvenilirlik, güç, ısı ve üretim süresi farklarını (`StatComparisonItem`, `IsBetter` yön duyarlılığı) ve tüm slot modül değişimlerini (`SlotComparisonItem`, `SlotComparisonStatus`) hesaplayan saf motor.
- `ComparisonNavigationArgs`, `ComparisonViewModel` & `ComparisonScreenController`: İki tasarımın yan yana istatistik farklarını ve slot değişimlerini reaktif UI Toolkit görünümünde sunan tam ekran kontrolcü.
- `TemplateManagerViewModel`: Atölye içinden şablon kaydetme, seçme, silme, kopyalama, karşılaştırmaya gönderme ve kod alma işlevlerini yöneten ViewModel.
- `ApogeeUIManager`: `ComparisonScreenController` kaydı tamamlandı.
- `Common.uss`: Karşılaştırma ekranı kartları, fark rozetleri ve çift renk stilleri güncellendi.
- 15 yeni birim testi (toplam 76 yerel doğrulama testi) başarıyla geçti.

---

*Son güncelleme: M2-06 tamamlandı · 2026-10-29*
