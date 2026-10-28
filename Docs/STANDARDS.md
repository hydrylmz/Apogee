# APOGEE — Kod ve Veri Standartları (M0-06)

> Tek doğruluk kaynağı `APOGEE_GDD.md`'dir.  
> Bu doküman, AI ve geliştirici için çalışma kurallarını tanımlar.

---

## 1. İsimlendirme Kuralları

### C# (kod, tanımlayıcı, yorum → İngilizce)
| Öğe | Kural | Örnek |
|---|---|---
| Namespace | `PascalCase`, proje önekiyle | `Apogee.Core`, `Apogee.Design` |
| Class / Struct | `PascalCase` | `CampaignState`, `RocketBlueprint` |
| Interface | `I` öneki + `PascalCase` | `ICommand`, `IStatSource` |
| Enum | `PascalCase`, üyeler de `PascalCase` | `MissionPhase.Cruise` |
| Public property / field | `PascalCase` | `DryMass`, `TotalDeltaV` |
| Private field | `_camelCase` (alt çizgi öneki) | `_seed`, `_modules` |
| Method | `PascalCase` | `ComputeDeltaV()`, `Validate()` |
| Local variable | `camelCase` | `totalMass`, `result` |
| Constant | `PascalCase` veya `ALL_CAPS` (proje tercihine göre; tutarlı ol) | `G0 = 9.80665` |
| Test class | `<TestedClass>Tests` | `DeltaVCalculatorTests` |
| Test method | `<Method>_<Scenario>_<Expected>` | `Compute_SingleStage_MatchesReference()` |

### Veri Dosyaları (SO / JSON)
| Öğe | Kural | Örnek |
|---|---|---|
| ScriptableObject dosyası | `kebab-case`, anlamlı | `engine-kerosene-lox.asset` |
| JSON anahtar | `camelCase` | `"dryMass"`, `"thrustVac"` |
| Yerelleştirme anahtarı | `SCREAMING_SNAKE`, bölüm öneki | `MODULE_ENGINE_KEROSENE_NAME` |
| Görsel/ses varlık | `kebab-case` | `icon-mass.svg`, `sfx-launch.wav` |

---

## 2. Assembly / Klasör Yapısı

```
Assets/_Project/
  Core/          Apogee.Core        noEngineReferences=true   Sim, Rng, Scoring, Events
  Design/        Apogee.Design      noEngineReferences=true   Blueprint, Modules, Stats, Validators, MissionProfile
  Strategy/      Apogee.Strategy    noEngineReferences=true   Campaign, Time, Bases, Research, Production, Contracts, Composer
  Data/          Apogee.Data        (Unity bağımlı)           Katalog SO'ları, JSON yükleyiciler
  Net/           Apogee.Net         (Unity bağımlı)           Lobi, komut yayını, BotAgent
  Game/          Apogee.Game        (Unity bağımlı)           GameFlow, Session, Persistence, Localization, Settings
  Presentation/  Apogee.UI          (Unity bağımlı)           UXML/USS, ViewModels, Harita/Roket görünümleri, Ses
  Tests/
    EditMode/    Apogee.Tests.EditMode   Core/Design/Strategy birim testleri
    PlayMode/    Apogee.Tests.PlayMode   Duman testleri

Server/          Relay sunucusu (ayrı Node.js projesi)
Docs/            GDD, standartlar, denge notları, lisanslar
```

**Bağımlılık yönü (tek yönlü, tersine izin yok):**
```
Presentation → Game → (Strategy, Net) → Design → Core
Data ← hepsi (okunur)
```

---

## 3. ScriptableObject / JSON Ayrımı

| İçerik tipi | Nerede? | Neden? |
|---|---|---|
| Katalog verisi (modül, motor, malzeme, şirket, hedef, amaç, olay, araştırma düğümü) | `ScriptableObject` (`.asset`) | Unity Inspector'da düzenlenebilir |
| Büyük/dinamik veri (NASA kataloğu, çeviri tablosu) | `JSON` (`StreamingAssets` veya `Resources`) | Kod dışı güncelleme |
| Oyun durumu (CampaignState) | `JSON` (kayıt) | Taşınabilir, sürümlü |
| Ayar katsayıları (skor ağırlıkları, denge sabitleri) | `JSON` (ayar dosyası) | Kod değişikliği olmadan ayarlanabilir |

---

## 4. Yerelleştirme Anahtarı Kuralı

- Kod içinde **hiçbir sabit UI metni** bulunmaz.
- Her string bir `LocalizationKey` sabitiyle referans edilir.
- Anahtar biçimi: `BÖLÜM_ALT_BÖLÜM_ÖĞE` → örn. `MODULE_ENGINE_KEROSENE_DESC`
- Tablo konumu: `Assets/_Project/Data/Localization/strings_tr.json` ve `strings_en.json`
- Çeviri kaynağı: her zaman `strings_tr.json`; İngilizce buna paralel tutulur.

---

## 5. "Sihirli Sayı Yok" Kuralı

- Hesaplamalarda kullanılan her katsayı ya:
  - Bir katalog varlığından (SO/JSON) gelir, **ya da**
  - `BalanceSettings` / `SimulationConstants` ayar dosyasında tanımlıdır.
- `g0 = 9.80665 m/s²` gibi evrensel sabitler `SimulationConstants` sınıfında `const` olarak tutulur.
- Kodda direkt sayısal literal (`0.85f`, `500000f`) yasaktır; her zaman adlandırılmış sabit veya veri alanı kullanılır.

---

## 6. Komut (Command) Kuralı

- Oyun durumunu (`CampaignState`) değiştiren **her** işlem bir `ICommand` implementasyonudur.
- UI hiçbir zaman durumu doğrudan değiştirmez; `CommandBus.Execute(command)` çağırır.
- Her komut: `CanExecute() → (bool, string reason)`, `Execute()`, `Undo()`.
- Komutlar serileştirilebilir olmalı (online yayın ve kayıt için).

---

## 7. Determinizm Kuralı

- `System.Random` ve `DateTime.Now` **yasak**. Her rastgelelik `SeededRng` üzerinden geçer.
- RNG akışları bağımsızdır: olaylar, fırlatma, EDL ayrı akışlardan beslenir.
- Zaman sistemi tick tabanlı ve sabit adımlıdır; `Time.deltaTime` simülasyon mantığına girmez.

---

## 8. "noEngineReferences" Kontrolü

`Apogee.Core`, `Apogee.Design`, `Apogee.Strategy` assembly'lerinde Unity namespace'leri kullanılamaz.  
CI aşamasında bu kontrol otomatik doğrulanmalıdır (build log'da `UnityEngine` referansı bu assembly'lerde hata verir).

---

## 9. Test Kuralı

- Her yeni Core/Design/Strategy sınıfı için en az bir EditMode test dosyası oluşturulur.
- Test sınıfı, test edilen sınıfla aynı adı alır + `Tests` soneki.
- Bir görev, testleri geçmeden **tamamlanmış** sayılmaz (GDD EK B).

---

## 10. TODO-VERİ İşareti

- Sayısal değeri doğrulanmamış her yer `// TODO-VERİ` yorumu alır.
- GDD §16'da listeli olmayan katalog değerleri üretilmez; önce GDD güncellenir veya kullanıcıya sorulur.

---

## 11. AI Çalışma Klasörü

AI'nın ürettiği tüm belgeler (ilerleme kaydı, kararlar, standartlar, rehberler) **`Docs/`** klasöründe yaşar.

| Dosya | İçerik |
|---|---|
| `PROGRESS.md` | Aktif ilerleme kaydı — AI her görevde okur ve günceller |
| `DECISIONS.md` | Alınan mimari kararlar (ADR) |
| `STANDARDS.md` | Kod ve veri standartları (bu dosya) |
| `CI_KURULUM.md` | CI kurulum rehberi |
| `APOGEE_GDD.md` | Ana tasarım dokümanı (tek doğruluk kaynağı) |
| `check-no-engine-refs.ps1` | Assembly doğrulama scripti |

Kod üretmeden önce AI şu sırayı izler:
1. `PROGRESS.md` → mevcut durum
2. `APOGEE_GDD.md` → ilgili bölümler
3. `DECISIONS.md` → daha önce alınmış kararlar

---

*Son güncelleme: M0-06 · APOGEE GDD v1.0*
