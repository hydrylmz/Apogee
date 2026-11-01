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
| **M3** | Misyon Besteleyici | ✅ Tamamlandı |
| **M4** | Strateji Katmanı | ✅ Tamamlandı |
| **M5** | Operasyon ve Sonuç | 🟡 Devam ediyor |
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
| M3-05 | Misyon Dosyası UI (Gereksinim listesi, kilitli araştırmalar, "araştırmaya ekle") | ✅ Tamamlandı | GDD §5.4, §5.5 ve §14.1 uyarınca `ScreenId.MissionBrief` ekranı geliştirildi. `MissionDossierViewModel` (reaktif telemetri, zorunlu modül kontrol listesi, kilitli teknoloji algılama ve tek tıkla "araştırmaya ekle" kuyruk aksiyonu, GDD §5.5 ölçülebilir başarı kriterleri listesi, Workshop ve Besteleyici navigasyon komutları) ve 3 sütunlu `MissionDossierScreenController` (UI Toolkit) oluşturuldu. Common.uss stil sınıfları eklendi. InvariantCulture formatlaması uygulandı. 7 yeni yerel test eklendi; toplam test sayısı 172'den **179**'a çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-022 |
| M3-06 | İnsanlı/rover özel kuralları (§7.6, §7.7) | ✅ Tamamlandı | GDD §7.6, §7.7 ve §21 M3-06 kuralları uygulandı: İnsanlı (`IsCrewed`) görevlerde `MandatoryModuleValidator` kabin (`PayloadCabin`), yaşam destek (`PayloadLifeSupport`) ve kaçış/abort (`PayloadAbortSystem`) sistemlerini zorunlu tutar; $\ge \%98$ güvenilirlik eşiği (`ReliabilityValidator`), dönüş $\Delta v$ ve en az Seviye 4-5 fırlatma rampası şart koşulur. Rover/Lander görevlerinde `edl` paketi zorunlu tutulur. `VehicleSlotId` ve `PayloadDesign` `Cabin` ve `AbortSystem` slotlarıyla genişletildi. Kataloğa `cabin-crewed-mk1`, `eclss-life-support-mk1`, `abort-system-tower-mk1`, `power-rtg-deepspace` ve `adcs-autonomous-ai-core` modülleri eklendi. 9 yeni yerel test eklendi; toplam test sayısı 179'dan **188**'e çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-023 |

---

## M4 — Strateji Katmanı (HOI4 Hissi)

| Görev | Ad | Durum | Notlar |
|---|---|---|---|
| M4-01 | CampaignState ve zaman sistemi | ✅ Tamamlandı | GameDate (Howard Hinnant takvim algoritması, 2031-01-01 epoch), TimeSpeed (Paused, Normal, Fast, VeryFast), CampaignTime (günlük tick adımları, 2061-01-01 bitiş sınırı, UndoAdvance), CampaignState (tekil durum nesnesi, bütçe, itibar, araştırma/bilgi puanları, izole CommandBus), AdvanceTimeCommand ve SetTimeSpeedCommand (CommandBus, Undo/Redo, yetki kontrolü). 23 yeni test eklendi; toplam test sayısı 126'dan **149**'a çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-018 |
| M4-02 | Dünya haritası ve bölgeler | ✅ Tamamlandı | RegionRecord (GDD §19.9), regions.json (8 stratejik bölge: Guiana Coast, Cape Peninsula, Steppe Cosmodrome, Pacific Atoll, Nordic Range, Mediterranean Hub, Anatolian Plateau, Outback Station), MapLens (Overview, Logistics, CommsCoverage, LaunchEfficiency, Research, Risk), RegionLensEvaluator (lens puanlama ve sade açıklamalar), SetMapLensCommand ve SelectRegionCommand (CommandBus, Undo/Redo). 9 yeni test eklendi; toplam test sayısı 149'dan **158**'e çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-019 |
| M4-03 | Üs sistemi | ✅ Tamamlandı | BaseType (LaunchPad, Factory, ResearchCenter, GroundStation, TestFacility), BaseLevelSpec (1-5 seviye kapasite, bakım, inşa/yükseltme maliyet/süre tablosu), BaseFacility (LaunchPadInfo entegrasyonu), CampaignState.Bases başlangıç üsleri (GDD §4.2), BuildBaseCommand ve UpgradeBaseCommand (CommandBus, Undo/Redo, yetki ve bütçe kontrolü). 8 yeni test eklendi; toplam test sayısı 158'den **166**'ya çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-020 |
| M4-04 | Güneş sistemi haritası | ✅ Tamamlandı | LaunchWindowInfo (hedef pencere tarihleri, aktiflik, süre), LaunchWindowCalculator (sinodik periyot tabanlı pencereler, bir sonraki pencere ve kaçırma bekleme hesabı), PlanetaryOrbitCalculator (daire yörünge hareket açıları ve AU bazlı heliosantrik koordinatlar), DestinationBriefingCard (sade brifing kartı: Δv, gecikme, süre, tehlikeler, pencere durumu). 6 yeni test eklendi; toplam test sayısı 166'dan **172**'ye çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-021 |
| M4-05 | Araştırma sistemi | ✅ Tamamlandı | ResearchNodeRecord (GDD §19.7), research_tree.json (GDD §9.2'deki 8 dalda 22 teknoloji düğümü), ResearchStatus, ResearchNodeState, ResearchManager (kuyruk, puan/süre ilerlemesi, otomatik ardıl kilit açma), StartResearchCommand ve CancelResearchCommand (CommandBus, Undo/Redo, bütçe kesintisi/iadesi), CampaignState.Research entegrasyonu. 8 yeni doğrulama testi eklendi; toplam test sayısı 188'den **196**'ya çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-024 |
| M4-06 | Şirketler ve sözleşmeler | ✅ Tamamlandı | GDD §9.1, §12.2, §19.6, §19.12/13 ve §21 M4-06 uyarınca: `SupplierCompanyState` (katalog başlangıç ilişkisi, dinamik ilişki ve itibar çarpanları), `SingleSourceRiskEvaluator` ve `SingleSourceRiskReport` (tek kaynak yoğunlaşma riski, %5-%10 hacim indirimi, sade açıklama), `SupplierContract` ve `SupplierManager` (tedarik sözleşmeleri, takvim entegrasyonu, teslimat ve ilişki gelişimi), `SignContractCommand` ve `CancelContractCommand` (bütçe kesintisi/iadesi, ceza, Undo/Redo, ProcurementDirector yetkilendirmesi), `SetSlotSupplierCommand` ve `BatchAssignSupplierCommand` (tasarıma şirket atama, blueprint maliyet/süre/güvenilirlik istatistiğine anlık yansıma), `AdvanceTimeCommand` entegrasyonu. 10 yeni test eklendi; toplam test sayısı 196'dan **206**'ya çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-025 |
| M4-07 | Üretim ve test kuyruğu | ✅ Tamamlandı | GDD §6.1, §9.3, §12.2, §16.7, §19.13 ve §21 M4-07 uyarınca: Fabrika ve test tesisi operasyonel kapasiteleri (`GetTotalFactoryCapacity`, `GetTotalTestCapacity`), `ProductionJobStatus` ve `ProductionJobType`, `ProductionJob` ve `ProductionManager` (üretim/montaj/test kuyruğu, kapasite aşımı yavaşlaması ve gecikme birikimi, boşalan kapasitede otomatik kuyruk terfisi), `StartVehicleProductionCommand` (tasarımdan montaj başlatma, bütçe kesintisi), `StartTestingCommand` (test tesisi şartı, güvenilirlik bonusu), `CancelProductionJobCommand` (kalan süreye göre iade, kapasite boşaltma), `AdvanceTimeCommand` entegrasyonu ve simetrik Undo/Redo. 8 yeni test eklendi; toplam test sayısı 206'dan **214**'e çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-026 |
| M4-08 | Bütçe ve itibar ekonomisi | ✅ Tamamlandı | GDD §4.3, §4.5, §6.1, §6.4, §8.1, §16.7, §16.8, §19.13 ve §21 M4-08 uyarınca: `EconomySettings` (veri odaklı ekonomi konfigürasyonu, yıllık taban bütçe, itibar eğrisi, bakım katsayıları, iflas eşiği), `EconomyEvaluator` (itibar bazlı yıllık bütçe tahsisatı, üs bakım masrafları dökümü ve iflas kontrolü), `EconomyManager` (`FinancialRecord` muhasebe defteri, periyodik takvim tetikleyicileri), `CampaignState.Economy` ve `AdvanceTimeCommand` entegrasyonu (yıl geçişinde tahsisat, ay geçişinde bakım kesintisi, simetrik Undo/Redo), `Docs/BALANCE_NOTES.md` başlangıç dengesi belgesi. 8 yeni test eklendi; toplam test sayısı 214'ten **222**'ye çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-027 |
| M4-09 | Olay sistemi (kampanya) | ✅ Tamamlandı | GDD §6.3, §9.1, §10.2, §10.3, §14.1 (#15 Olay Penceresi), §19.10 ve §21 M4-09 uyarınca: `CampaignEventRecord`, `EventOptionRecord`, `EventOutcomeRecord` ve `campaign_events.json` (15 adet olay: bütçe bonusu, tedarikçi gecikmesi ve kalite sorunu, araştırma atılımı, jeopolitik zirve, halk ilgisi, işçi grevi, stratejik ortaklık, mali denetim vb.); `EventEvaluator` (tetik koşulları, tedarikçi olay eğilimleri ağırlıklandırması); `CampaignEventManager` (deterministik tohumlu RNG, bekleme süresi, geçmiş takibi); `ResolveEventOptionCommand` (bütçe/itibar/araştırma/tedarikçi etkileri, muhasebe defteri kaydı, simetrik Undo/Redo); `AdvanceTimeCommand` entegrasyonu; `EventPopupViewModel` ve `EventPopupScreenController` (UI Toolkit modal karar kartı, sade sunum dili). 8 yeni test eklendi; toplam test sayısı 222'den **230**'a çıktı (%100 geçti, 0 hata). `noEngineReferences` doğrulandı. — ADR-028 |

---

## M5 — Operasyon ve Sonuç

| Görev | Ad | Durum | Notlar |
|---|---|---|---|
| M5-01 | Fırlatma çözümü | ✅ Tamamlandı | Roket güvenilirliği, hava durumu riski, aşama ayrılmaları, kaçış kulesi (abort), neden-sonuç zinciri, deterministik tohumlu simülasyon, LaunchMissionCommand (itibar etkisi, Undo/Redo) ve LaunchSequenceScreenController (UI Toolkit, telemetri göstergeleri). 9 yeni test (toplam 239 test, %100 geçti). — ADR-029 |
| M5-01b | Review → Launch Sequence navigasyonu | ✅ Tamamlandı | ReviewViewModel'e ProceedToLaunchCommand + LaunchNavigationArgs oluşturma eklendi; ReviewScreenController'a "Fırlatmaya Geç" butonu eklendi; LaunchSequenceScreenController.OnShown() bağlandı. 239 test geçiyor. — ADR-030 |
| M5-02 | Seyir simülasyonu | ✅ Tamamlandı | Deterministik seyir simülatörü (CruiseSimulator, SeededRng), segment olayları, alt sistem bozulmaları, karar kartları (Decision Cards), MissionInstance & MissionStatus, AdvanceCruiseCommand ve ResolveCruiseDecisionCommand (simetrik Undo/Redo), OperationsScreenController & OperationsViewModel (UI Toolkit). 9 yeni test (toplam 248 test, %100 geçti). — ADR-031 |
| M5-03 | Varış/iniş (EDL) çözümü | 🟡 Sıradaki Görev | Atmosfer, iniş yöntemi, ısı kalkanı |
| M5-04 | Bilim operasyonu | 🔒 Bekliyor | Güç/ısı/veri/haberleşme akışı, teslim edilen veri, darboğaz |
| M5-05 | Operasyon olayları ve karar kartları | 🔒 Bekliyor | ~15 operasyon olayı, karar kartları |
| M5-06 | Sonuç raporu | 🔒 Bekliyor | Kriterler, neden-sonuç zinciri, kampanya etkisi |
| M5-07 | "Ya şöyle olsaydı?" | 🔒 Bekliyor | Tek karar değişimi + yeniden hesap |
| M5-08 | Skor ve madalyalar | 🔒 Bekliyor | Görev skoru, madalyalar, ayar dosyası |

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

*Son güncelleme: M5-02 Seyir Simülasyonu tamamlandı · Toplam 248 test geçti · 2026-11-01*
