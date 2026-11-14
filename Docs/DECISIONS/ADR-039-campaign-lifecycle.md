# ADR-039: Campaign Lifecycle — Start & End System

**Tarih:** 2026-11-08
**Durum:** Kabul Edildi
**İlgili:** GDD §4.1, §4.5, §21 M6-02

---

## Bağlam

Kampanya başlangıcı ve bitişi GDD §4.1–§4.5 kapsamındadır:

- **§4.1** — 30 yıllık kampanya penceresi (2031–2061)
- **§4.2** — Ajans kurulumu: isim, başlangıç üsleri, kaynaklar
- **§4.4** — Tüm kilometre taşları tamamlandığında zafer
- **§4.5** — Bitiş koşulları: iflas, süre dolumu, zafer, gönüllü emeklilik

---

## Karar

### Yeni Tipler

| Dosya | Namespace | Amaç |
|---|---|---|
| `CampaignEndStatus.cs` | `Apogee.Strategy.Campaign` | 5 durum enum'u: Active, Bankruptcy, TimeExpired, AllMilestonesCompleted, VoluntaryRetirement |
| `CampaignEndReport.cs` | `Apogee.Strategy.Campaign` | İmmutable bitiş raporu: kampanya istatistikleri, kilometre taşları, bilim verisi |
| `CampaignEndEvaluator.cs` | `Apogee.Strategy.Campaign` | Pure-static: koşul değerlendirme + rapor üretimi |
| `EndCampaignCommand.cs` | `Apogee.Strategy.Campaign` | Kampanyayı bitirir, rapor üretir, Undo destekler |
| `CampaignSetupArgs.cs` | `Apogee.Strategy.Campaign` | Ajans kurulum parametreleri (isim, başlangıç bölgesi, seed) |
| `CampaignEndViewModel.cs` | `Apogee.UI.Campaign` | Raporu UI'ye bağlar (ObservableValue) |
| `CampaignEndScreenController.cs` | `Apogee.UI.Campaign` | UI Toolkit ekran kontrolcüsü (ScreenId.CampaignEnd = 22) |

### Değiştirilen Tipler

| Dosya | Değişiklik |
|---|---|
| `CampaignState.cs` | `AgencyName`, `EndStatus`, `EndReport` alanları eklendi; `using Apogee.Strategy.Campaign` |
| `AdvanceTimeCommand.cs` | Her zaman adımından sonra `ApplyEndConditionIfTriggered()` çağrılır; Undo'da EndStatus/EndReport snapshot restore edilir |
| `ScreenId.cs` | `MilestoneBoard = 21`, `CampaignEnd = 22` eklendi |

### Bitiş Koşulları (Öncelik Sırası)

```
1. Bankruptcy -> Budget ≤ 0 AND Reputation ≤ 0
2. TimeExpired -> state.Time.IsExpired (2061-01-01)
3. AllMilestonesCompleted -> tüm kayıtlı milestone.Status == Claimed
(VoluntaryRetirement -> yalnızca oyuncu EndCampaignCommand(VoluntaryRetirement) çağırdığında)
```

### Otomatik Tetikleme

`AdvanceTimeCommand.Execute()` sonunda `CampaignEndEvaluator.EvaluateEndCondition()` çağrılır.
Koşul aktifleşirse `state.EndStatus` ve `state.EndReport` doğrudan set edilir.
`Undo` bu değerleri snapshot'tan geri yükler — CommandBus tarihine iç içe kayıt yazılmaz.

### Simetrik Undo

`EndCampaignCommand` ve `AdvanceTimeCommand` her ikisi de `EndStatus` / `EndReport` snapshot'ı tutar.
Undo işleminde Active duruma ve null rapora tam geri dönüş sağlanır.

---

## Sonuçlar

- Tüm bitiş mantığı pure-C# / Unity bağımlılığı yok OK
- WebGL uyumlu (System.IO, Thread yok) OK
- 8 yeni test eklendi -> toplam **317 test, %100 geçiyor** OK
- `noEngineReferences` kuralı korundu (Core/Design/Strategy assemblyleri) OK
