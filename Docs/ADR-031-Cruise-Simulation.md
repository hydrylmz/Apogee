# ADR-031: M5-02 Seyir Simülasyonu ve Karar Kartları (Cruise Simulation & Decision Cards)

## Durum
Kabul Edildi

## Bağlam
GDD §6.2, §8.1, §8.4, §10.1, §10.2, §10.3, §14.1 (#14 Operasyon Ekranı) ve §19.12 uyarınca roket fırlatma sonrası varış noktasına kadar geçen interplanetary/yörüngesel transfer sürecinin simülasyonu gerekmektedir. Bu süreçte araç tasarımı ve sağlık parametrelerine (güvenilirlik, güç, ısıl, radyasyon ve toz tehlikeleri) bağlı olarak deterministik segment olayları, yörünge düzeltme manevraları, alt sistem bozulmaları ve oyuncuya taktiksel seçimler sunan karar kartları (Decision Cards) devreye girmelidir.

## Kararlar
1. **Core Modeli (`Apogee.Core.Simulation.Cruise`)**:
   - `CruiseSimulationParameters`, `CruiseSegmentEvent`, `CruiseSegmentStatus`, `CruiseDecisionCard`, `CruiseDecisionOption` ve `CruiseSimulationResult` modelleri saf C# olarak tasarlandı.
   - `CruiseSimulator`: `SeededRng` tabanlı tohumlu determinizm. Olay olasılığı tasarımdaki zayıflıklara (düşük güvenilirlik, güç/ısı marj açığı, radyasyon/toz tehlikesi) göre dinamik ağırlıklandırıldı.
2. **Strateji & Misyon Durumu (`Apogee.Strategy.Mission`)**:
   - `MissionStatus` yaşam döngüsü enum'ı (InCruise, DecisionPending, Arrival, Failed vb.) tanımlandı.
   - `MissionInstance` kampanya veri modeli: seyir ilerlemesi (`CruiseProgress` 0.0–1.0), olay günlüğü, kararlar ve birikimli cezalar kaydedildi.
   - `AdvanceCruiseCommand`: Seyri adım adım ilerletir; `DecisionRequired` durumunda seyri duraklatıp `DecisionPending` yapar. Simetrik `Undo`/`Redo` destekler.
   - `ResolveCruiseDecisionCommand`: Oyuncunun karar kartındaki seçeneğini (`SAFE_MODE`, `EXTRA_COST`, `RISK_HARDWARE` vb.) uygular ve seyri yeniden `InCruise` durumuna alır.
3. **Presentation / UI Toolkit (`Apogee.UI.Screens.Operations`)**:
   - `OperationsViewModel`, `OperationsNavigationArgs` ve `OperationsScreenController` oluşturuldu. Canlı seyir ilerleme çubuğu, telemetri akışı, karar kartı popup paneli ve navigasyon butonları bağlandı.
   - `ApogeeUIManager`'a `ScreenId.Operations` entegre edildi.
4. **Kalite & Doğrulama**:
   - `check-no-engine-refs.ps1` ile Core/Design/Strategy katmanlarında sıfır `UnityEngine` referansı doğrulandı.
   - `CruiseRunnerTests` ile 9 yeni kapsamlı EditMode testi yazıldı. Toplam doğrulama test sayısı 239'dan **248**'e yükseldi (%100 geçti).
