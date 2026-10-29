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

*Son güncelleme: M1-07 tamamlandı · 2026-10-29*
