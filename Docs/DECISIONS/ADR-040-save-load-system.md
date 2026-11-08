# ADR-040: Sürümlü Kayıt/Yükleme ve Misyon Kodu (Save/Load & Serialization)

**Tarih:** 2026-11-08  
**Durum:** Kabul Edildi  
**İlgili:** GDD §17.5, §17.9, §19.13, §21 M6-04

---

## Bağlam

GDD §17.5 ve §19.13 uyarınca tüm kampanya durumu tek bir `CampaignState` nesnesi olarak yönetilir. WebGL platformunun kısıtları (System.IO dosya kilidi/thread yokluğu, tarayıcı yerel depolama ya da sunucu üzerinden aktarım) gereği:
- Kayıt ve yükleme akışı string/JSON tabanlı ve pure C# olmalıdır.
- Sürüm takibi (`SchemaVersion`) ile ileride yapılacak veri şeması değişikliklerinde otomatik geçiş (migration) kancası bulunmalıdır.
- Gidiş-dönüş eşitliği (Round-trip equality: Save -> Json -> Restore -> Json) garanti altına alınmalıdır.
- Araç tasarımları ve misyonlar kompakt, paylaşılabilir `MissionCodeCodec` (`APG1-...`) kodları üzerinden kaydedilmelidir.

---

## Karar

1. **Newtonsoft.Json Entegrasyonu:**
   - `Apogee.Strategy.asmdef` dosyasına `overrideReferences: true` ve `Newtonsoft.Json.dll` precompiled referansı eklendi. `noEngineReferences: true` kuralı bozulmadan korundu.

2. **Kayıt Veri Modeli (`Apogee.Strategy.Persistence`):**
   - `CampaignSaveData` (Sürüm 1): CampaignId, Seed, AgencyName, EndStatus, EndReport, Zaman (ElapsedDays, CurrentTick), Kaynaklar (Budget, Reputation, ResearchPoints, KnowledgePoints), Strateji Haritası (SelectedRegionId, ActiveMapLens), Üsler, Araştırma Düğümleri ve Kuyruk, Kilometre Taşları, Aktif Tasarım ve Tamamlanan Misyonlar.
   - `BaseFacilitySaveData`, `ResearchNodeSaveData`, `MilestoneProgressSaveData`, `MissionInstanceSaveData`, `CampaignEndReportSaveData`.

3. **Serileştirici ve Durum Restorasyonu (`CampaignSaveSerializer`):**
   - `CreateSaveData(CampaignState)`: Mevcut durumdan tam snapshot oluşturur.
   - `ToJson(CampaignState)` / `FromJson(string)`: Formatlı JSON üretimi, sürüm denetimi ve `MigrateSaveData` desteği.
   - `RestoreFromSaveData(CampaignState, CampaignSaveData)`: CampaignState içine zaman, kaynak, üs, teknoloji, kilometre taşı ve misyon verilerini geri yükler.
   - `CampaignTime.RestoreState`, `ResearchNodeState.RestoreSavedState`, `ResearchManager.RestoreNodeState` ve `RestoreQueue` metotları ile encapsulation korunarak kontrollü geri yükleme sağlandı.

4. **Doğrulama ve Gidiş-Dönüş Testleri (`SaveLoadLocalRunnerTests`):**
   - 9 yeni doğrulama testi eklendi:
     - Kaynaklar ve ajans bilgileri gidiş-dönüşü.
     - Zaman sistemi (tick, date, elapsed days) gidiş-dönüşü.
     - Üsler ve seviyeler gidiş-dönüşü.
     - Araştırma ağacı durumu ve kuyruğu gidiş-dönüşü.
     - Kilometre taşları gidiş-dönüşü.
     - Misyonlar ve sonuç raporları gidiş-dönüşü.
     - Aktif tasarım misyon kodu gidiş-dönüşü.
     - Tamamlanmış kampanya sonu raporu gidiş-dönüşü.
     - Çift serileştirme çıktılarının tam eşitliği (idempotency).

---

## Sonuçlar

- Toplam test sayısı 317'den **326**'ya yükseldi (%100 başarı, 0 hata).
- `noEngineReferences` kuralı eksiksiz korundu.
- WebGL uyumu doğrulandı.
