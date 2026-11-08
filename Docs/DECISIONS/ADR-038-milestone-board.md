# ADR-038: Kilometre Taşı ve Görev Tahtası Sistemi (M6-01)

## Durum
Kabul Edildi

## Bağlam
GDD §4.4 (Kampanya Hedefleri / Kilometre Taşları), §14.1 (Görev Tahtası), §19.13 (Kampanya Durumu) ve §21 M6-01 gereğince:
Ajansın kampanya boyunca izleyebileceği, zorunlu sıralı olmayan ancak öneri listesi olarak sunulan 8 resmi kilometre taşının (`milestones.json` / `MilestoneRecord`) tanımlanması, tamamlanan başarılı misyonlara göre otomatik değerlendirilmesi, tamamlanan taşlar için bildirim üretilmesi, ödüllerin (`ClaimMilestoneRewardCommand`) bütçe/itibar/bilgi puanı olarak kampanyaya aktarılması ve simetrik Undo/Redo ile geri alınabilmesi gerekiyordu.

## Alınan Kararlar

1. **Veri Modeli ve Katalog (`MilestoneRecord.cs`, `milestones.json`):**
   - 8 resmi kilometre taşı (İlk Yörünge Uydusu, Ay'a İlk Sonda, Ay Yüzeyine Robotik İniş, İlk Gezegenler Arası Sonda, Mars Rover, İnsanlı Ay Görevi, Numune Getirme, İnsanlı Mars Görevi).
   - Önkoşul kilometre taşı kimlikleri (`required_milestone_ids`), hedef ve görev tipi filtreleri, insanlı/numune bayrakları ve ödül miktarları (Bütçe, İtibar, Bilgi Puanı).

2. **Değerlendirme ve İlerleme Mantığı (`MilestoneEvaluator`, `MilestoneProgress`, `MilestoneManager`):**
   - **`MilestoneStatus`:** `Locked`, `Available`, `CompletedPendingClaim`, `Claimed`.
   - **`MilestoneEvaluator`:** Misyonun genel başarı durumunu, hedefini, görev tipini, insanlı olma durumunu saf C# fonksiyonlarıyla kontrol eder.
   - **`MilestoneManager`:** Kampanyadaki tüm kilometre taşlarının durumunu tutar, tamamlanma ve ödül alma olaylarını (`OnMilestoneCompleted`, `OnMilestoneClaimed`) reaktif tetikler.
   - **`CompleteMissionCommand` Entegrasyonu:** Bir misyon başarıyla sonuçlandığında `CampaignState.Milestones.EvaluateProgress` otomatik çağrılır.

3. **Komut ve Ekonomi Entegrasyonu (`ClaimMilestoneRewardCommand`):**
   - `AgencyDirector`, `LeadEngineer`, `ScienceDirector` yetkisiyle çalışır.
   - Bütçe, itibar (maksimum 100) ve bilgi puanı ödüllerini kampanyaya ekler.
   - Ekonomi yöneticisi (`EconomyManager`) aktifse muhasebe defterine (`Ledger`) gelir kaydı işler.
   - Simetrik `Undo` ile ödülleri geri alır ve durumu önceki haline döndürür.

4. **Kullanıcı Arayüzü / ViewModel (`MilestoneBoardViewModel`, `MilestoneItemViewModel`):**
   - Görev tahtasındaki kilometre taşlarını sıralı liste olarak sunar.
   - İlerleme oranı (ör. `1 / 8`), aktif odak hedefi ve tamamlanma bildirimlerini yönetir.
   - "Ödülü Al" eylemi ile tek tıkla ödül toplama sağlar.

5. **Test ve Doğrulama:**
   - 7 yeni test senaryosu (`MilestoneLocalRunnerTests` & `MilestoneRunnerTests`):
     - Başlangıç durumu ve önkoşul kilit uygunluğu
     - Başarılı misyon eşleşmesi doğrulaması
     - Başarısız veya hedefi uyuşmayan misyon reddi
     - İlerleme değerlendirmesi ile `CompletedPendingClaim` geçişi
     - Ödül alma komutunun bütçe/itibar/bilgi puanı artırması ve sonraki taşı açması
     - Ödül alma komutunun geri alınması (Undo) ile kaynak ve durum restorasyonu
     - `MilestoneBoardViewModel` veri bağlama ve reaktif ödül talep testi
   - Toplam test sayısı 302'den **309**'a yükseldi (%100 başarıyla geçti, 0 hata).
