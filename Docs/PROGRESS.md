# APOGEE — İlerleme ve AI Çalışma Günlüğü

> Bu dosya AI'nın aktif çalışma belgesidir. Her görev tamamlandığında buradaki durum güncellenir.
> **AI her yeni görev başında bu dosyayı okur; biter okur ve günceller.**
> Tüm AI çalışma dosyaları `Docs/` klasöründe tutulur.

---

## Kilometre Taşı Durumu

| Taş | Adı | Durum |
|---|---|---|
| **M0** | Temel ve Doğrulama | ✅ Tamamlandı |
| **M1** | Çekirdek Hesaplar ve Komut Sistemi | ✅ Tamamlandı |
| **M2** | Tasarım Atölyesi Dikey Dilimi | 🟡 Devam Ediyor |
| **M3** | Misyon Besteleyici | 🔒 Bekliyor |
| **M4** | Strateji Katmanı | 🔒 Bekliyor |
| **M5** | Operasyon ve Sonuç | 🔒 Bekliyor |
| **M6** | Kampanya Bütünleştirme | 🔒 Bekliyor |
| **M7** | Online ve Botlar | 🔒 Bekliyor |
| **M8** | Cila ve Teslim | 🔒 Bekliyor |

---

## M0 — Tamamlanan Görevler

| Görev | Tarih | Notlar |
|---|---|---|
| M0-01 Proje kurulumu + assembly yapısı | 2026-10-28 | 9 asmdef, `noEngineReferences` doğru |
| M0-02 CI (GitHub Actions + GameCI) | 2026-10-28 | `.github/workflows/ci.yml` — Secrets henüz eklenmedi |
| M0-04 Sanat pipeline kararı | 2026-10-28 | **URP 2D** + **PNG Atlas** — bkz. DECISIONS.md ADR-001/002 |
| M0-05 WebSocket paketi kararı | 2026-10-28 | **NativeWebSocket** — manifest.json'a eklendi — bkz. ADR-003 |
| M0-06 Standartlar dokümanı | 2026-10-28 | `Docs/STANDARDS.md` |

**Bekleyen M0 görevi:**
- [ ] M0-03: CI aktif edildikten sonra WebGL build linki buraya yazılacak

---

## M1 — Görev Durumu

Bağımlılık sırası (önce Core, sonra görüntü ilkesi):

| Görev | Ad | Durum | Notlar |
|---|---|---|---|
| M1-01 | Deterministik RNG | ✅ Tamamlandı | Xoshiro256** + SplitMix64, bağımsız akışlar, durum serileştirme, 16 test |
| M1-02 | Komut altyapısı (ICommand, CommandBus, Undo/Redo) | ✅ Tamamlandı | ICommand, CommandBase, CommandBus, Undo/Redo, PlayerRole yetki maskesi, CommandRecord denetim kaydı, 18 test |
| M1-03 | İstatistik modeli (temel + etkiler + kaynak dökümü) | ✅ Tamamlandı | Stat, StatModifier (Flat/PercentAdd/PercentMult), StatBreakdown (kaynak/neden dökümü), StatCollection, 11 test |
| M1-04 | Roket Δv hesabı (çok aşamalı, Isp/kütle, kayıplar) | ✅ Tamamlandı | RocketStage, DeltaVLossProfile, DeltaVCalculator, DeltaVCalculationResult, DeltaVStageResult — 17 test |
| M1-05 | Katalog veri yükleyici (JSON, doğrulama) | ✅ Tamamlandı | ICatalogRecord, CatalogLoader, CatalogCollection, CatalogValidationResult + ModuleRecord/DestinationRecord/SupplierRecord + destinations.json/suppliers.json — 37 test — ADR-005 |
| M1-06 | Blueprint modeli ve istatistik türetme | ✅ Tamamlandı | Blueprint, StageDesign, PayloadDesign, DesignComponent, SupplierSpec, MaterialSpec, BlueprintStats, BlueprintEvaluator — 32 test |
| M1-07 | Doğrulayıcılar | ✅ Tamamlandı | MissionProfile, LaunchPadInfo, ValidationContext/Report, IBlueprintValidator, 8 validator, BlueprintValidator koordinatör — 30 yeni test (toplam 129) — ADR-006 |
| M1-08 | Fırlatıcı kapasite hesabı | ✅ Tamamlandı | LaunchCapacityCalculator (binary search), LaunchCapacityResult, PayloadCapacityValidator — 20 test |

---

## M2 — Tasarım Atölyesi Dikey Dilimi (Ay + Rover, Tek Şirket)

| Görev | Ad | Durum | Notlar |
|---|---|---|---|
| M2-01 | UI temel sistemi (Tema, Tooltip, ViewModel, Navigation) | ✅ Tamamlandı | Variables.uss, Common.uss, Theme.uss, ViewModelBase, ObservableValue, TooltipService/Manipulator/View, ScreenNavigator, Workshop & Review ekranları, 7 yeni test (toplam 36 yerel doğrulama testi) — ADR-008 |
| M2-02 | Modül-slot UI (Slot listesi, seçim, hayalet delta) | 🔒 Bekliyor | |
| M2-03 | Roket/araç kesit görünümü (Modüler vektör, snap) | 🔒 Bekliyor | |
| M2-04 | Sonuç paneli (Sade sunum: çubuklar, kısa neden) | 🔒 Bekliyor | |
| M2-05 | Design Review ekranı (Kırmızı/sarı/yeşil liste) | 🔒 Bekliyor | |
| M2-06 | Şablonlar (Kaydet/kopyala/yeniden adlandır/karşılaştır) | 🔒 Bekliyor | |
| M2-07 | Dikey dilim içerik (Ay hedefi, rover, modüller) | 🔒 Bekliyor | |

---

## Açık Sorular

| # | Soru | Sorumlu | Durum |
|---|---|---|---|
| 1 | M0-03: WebGL yayın linki (ilk CI push sonrası) | Takım | ⏳ CI Secrets bekleniyor |
| 2 | Relay sunucu barındırma platformu | Takım | 🔒 M7'de |
| 3 | Kampanya yıl hızı (1 yıl ≈ kaç gerçek dakika) | Playtest | 🔒 M6'da |
| 4 | Bölge sayısı ve harita detayı | Sanat kapasitesi | 🔒 M4'te |
| 5 | Ses: özgün üretim mi, telifsiz kütüphane mi | Takım | 🔒 M8'de |

---

## AI Kuralları (Bu Dosya İçin)

- Her görev başında `PROGRESS.md` okunur.
- Görev bitince: durum güncellenir, açık sorular eklenir, sıradaki görev belirtilir.
- Dosya kayıt olarak birikir; eski satırlar silinmez, üstü çizilmez — durum sütunu değiştirilir.
- Tüm AI çalışma dosyaları (notlar, karar kayıtları, standartlar, bu dosya) `Docs/` altında yaşar.

---

*Son güncelleme: M2-01 tamamlandı · 2026-10-29*
