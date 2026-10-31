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
| **M2** | Tasarım Atölyesi Dikey Dilimi | ✅ Tamamlandı |
| **M3** | Misyon Besteleyici | 🟡 Devam ediyor |
| **M4** | Strateji Katmanı | 🟡 Devam ediyor |
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
| M2-02 | Modül-slot UI (Slot listesi, seçim, hayalet delta) | ✅ Tamamlandı | VehicleSlotDefinition, Blueprint.WithSlotComponent, SetSlotComponentCommand (CommandBus, Undo/Redo, yetki kontrolü), GhostDelta, Workshop slot listesi & seçim çekmecesi, telemetri çubuklarında anlık fark göstergeleri, modules.json, 11 yeni test (toplam 47 yerel doğrulama testi) — ADR-009 |
| M2-03 | Roket/araç kesit görünümü (Modüler vektör, snap) | ✅ Tamamlandı | CrossSectionPartType, SnapPoint, CrossSectionNode, CraftCrossSectionLayout (3 aşamalı roket + yük otomatik dizilim, merkez snap hattı, tedarikçi renk şeritleri), RocketCrossSectionViewModel, RocketCrossSectionView (CAD grid, cetvel, iki yönlü slot senkronizasyonu, boş yuva hayalet çerçevesi, canlı montaj), 5 yeni test (toplam 52 yerel doğrulama testi) — ADR-010 |
| M2-04 | Sonuç paneli (Sade sunum: çubuklar, kısa neden) | ✅ Tamamlandı | ResultBarStatus, ResultBarViewModel, ResultsPanelViewModel (7 standart telemetri çubuğu: Menzil, Kütle, Güvenilirlik, Güç, Isıl, Maliyet, Süre; validator raporuna duyarlı renk dili; sade sunum), ResultsBarView (UI Toolkit, TooltipManipulator "neden?" entegrasyonu), ReviewViewModel/Controller gerçek validator entegrasyonu, 5 yeni test (toplam 57 yerel doğrulama testi) — ADR-011 |
| M2-05 | Design Review ekranı (Kırmızı/sarı/yeşil liste) | ✅ Tamamlandı | ReviewFilterMode (Tümü / Engeller / Uyarılar / Tamamlanan), ReviewIssueItem ("Neden" ve "Neye bakmalı" ipucu, kural kimliği, değerler), ReviewNavigationArgs (Workshop->Review canlı veri aktarımı), GO/NO-GO rozeti, 4 filtreleme butonu, kırmızı/sarı/yeşil sol kenar vurgulu kartlar, hover tooltip entegrasyonu, 4 yeni test (toplam 61 yerel doğrulama testi) — ADR-012 |
| M2-06 | Şablonlar (Kaydet/kopyala/yeniden adlandır/karşılaştır) | ✅ Tamamlandı | DesignTemplate, ITemplateRepository, InMemoryTemplateRepository, Save/Duplicate/Rename/DeleteTemplateCommand (CommandBus + Undo/Redo + rol yetkisi), MissionCodeCodec (APG1-...), BlueprintDifference, BlueprintComparator (istatistik + slot fark analizi), ComparisonNavigationArgs, ComparisonViewModel, ComparisonScreenController (yan yana istatistik farkları ve slot değişimleri), TemplateManagerViewModel, Workshop hızlı şablon ve karşılaştırma entegrasyonu, 15 yeni test (toplam 76 yerel doğrulama testi) — ADR-013 |
| M2-07 | Dikey dilim içerik (Ay hedefi, rover, modüller) | ✅ Tamamlandı | Ay hedefi (moon), rover tipi (rover), ~12 modül (17 bileşen), 3 motor (Kerosen Mk-1, Ağır Kerosen, Hidrojen Üst), 3 malzeme (al-alloy, al-li, carbon-composite; materials.json + MaterialRecord), 1 şirket (Helion Dynamics), mission_types.json + MissionTypeRecord, LunarRoverVerticalSlice senaryoları (Passing, Missing EDL, Low Delta-V), EDL takma ile NO-GO -> GO dönüşümü, 10 yeni test (toplam 86 yerel doğrulama testi) — ADR-014 |

---

## M3 — Misyon Besteleyici ve Hedef Sistemi

| Görev | Ad | Durum | Notlar |
|---|---|---|---|
| M3-01 | Hedef/tip/amaç verisi | ✅ Tamamlandı | Mevcut 5 hedef ve 5 görev tipi kataloğuna ek olarak `ObjectiveRecord` ve `objectives.json` eklendi. GDD §5.2'deki 17 amaç (LEO 4, Ay 4, Mars 5, Venüs 2, Europa 2) hedef/tip/alet, veri/süre, bilim/zorluk, zorunlu modül ve başarı ölçütü sözleşmesiyle yüklendi. GDD'de sayısal kaynak bulunmayan değerler açıkça `TODO-VERİ` olarak işaretlendi. 1 yeni yerel doğrulama testi; toplam 87 test geçti. |
| M3-02 | MissionComposer (Core) | ✅ Tamamlandı | `MissionComposer`, `MissionCompositionRequest`, `MissionSuccessCriterion` ve genişletilmiş `MissionProfile` modeli saf C# ile uygulandı (`Apogee.Strategy`). Seçimlerden $\Delta v$ (fırlatma, transfer, varış, iniş, dönüş), zorunlu modüller (EDL, kabin, yaşam destek, abort, radyasyon kalkanı, aletler), araştırma önkoşulları, rampa seviyesi (1-5), güvenilirlik eşiği, pencereler, süre, haberleşme gecikmesi, zorluk puanı ve ölçülebilir başarı kriterleri eksiksiz türetildi. Mars insanlı ($19,200$ m/s, çift yön, yaşam destek, rampa 5, $\ge \%98$ güvenilirlik) vs Mars gezgin ($13,600$ m/s, tek yön, EDL, rampa 3, $\%50$ güvenilirlik) tablo testleriyle doğrulandı. 5 yeni test; toplam 92 test geçti. — ADR-015 |
| M3-03 | Zorluk katsayısı ve iddia seviyesi | ✅ Tamamlandı | GDD §5.3 formülü ($\text{Zorluk} = \Sigma(\text{amaç zorlukları}) \times \text{hedef çarpanı} \times \text{tip çarpanı}$), iddia seviyesi sınıflandırması (Düşük <5, Orta 5–12.9, Yüksek 13–24.9, Çok Yüksek $\ge$25), ödül/risk çarpanları ve sade sunum özetleri saf fonksiyonel `MissionDifficultyCalculator` ve `MissionDifficultyEvaluation` ile uygulandı. `MissionProfile` bu alanlarla zenginleştirildi. Arayüz katmanında reaktif `MissionClaimViewModel` ve `MissionClaimCardView` (UI Toolkit) geliştirildi; hedef, tip veya amaçlar değiştikçe seviye, rozet (`claim-badge--*`) ve ödül/risk değerleri anlık güncelleniyor. 24 yeni test (18 alan, 5 arayüz, 1 entegrasyon) eklendi; toplam test sayısı 92'den **116**'ya çıktı (%100 geçti). `noEngineReferences` doğrulandı. — ADR-016 |
| M3-04 | Besteleyici UI (Adımlı sihirbaz) | ✅ Tamamlandı | GDD §5.1–§5.5 uyarınca 4 adımlı sihirbaz (`ComposerStep`: Hedef → Misyon Tipi → Bilimsel Amaçlar [çoklu seçim] → Takvim/Bütçe/İsim), `MissionCompositionResultArgs`, reaktif `MissionComposerViewModel` ve `MissionComposerScreenController` UI Toolkit ekranı geliştirildi. `MissionClaimCardView` ve telemetri önizleme paneli ile anlık iddia, ödül/risk ve $\Delta v$ türetimi entegre edildi. Hedef/tip değişiminde uyumsuz amaçlar otomatik budanıyor. `ComposeMissionProfile()` ile `MissionProfile` oluşturuluyor ve `ScreenNavigator` üzerinden sonraki ekrana aktarılıyor. 10 yeni test eklendi; toplam test sayısı 116'dan **126**'ya çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-017 |

---

## M4 — Strateji Katmanı (HOI4 Hissi)

| Görev | Ad | Durum | Notlar |
|---|---|---|---|
| M4-01 | CampaignState ve zaman sistemi | ✅ Tamamlandı | GameDate (Howard Hinnant takvim algoritması, 2031-01-01 epoch), TimeSpeed (Paused, Normal, Fast, VeryFast), CampaignTime (günlük tick adımları, 2061-01-01 bitiş sınırı, UndoAdvance), CampaignState (tekil durum nesnesi, bütçe, itibar, araştırma/bilgi puanları, izole CommandBus), AdvanceTimeCommand ve SetTimeSpeedCommand (CommandBus, Undo/Redo, yetki kontrolü). 23 yeni test eklendi; toplam test sayısı 126'dan **149**'a çıktı (%100 geçti, 0 hata). 
oEngineReferences doğrulandı. — ADR-018 |

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

*Son güncelleme: M4-01 CampaignState ve zaman sistemi tamamlandı · 2026-10-31*
