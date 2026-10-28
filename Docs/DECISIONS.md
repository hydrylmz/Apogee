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

*Son güncelleme: M0 tamamlanırken · APOGEE GDD v1.0*
