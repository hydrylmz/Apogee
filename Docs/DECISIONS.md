# ADR-028: M4-09 Kampanya Olay Sistemi (Event Director, Decision Cards, Effects)

## Durum
Kabul Edildi

## Bağlam
GDD §6.3, §9.1, §10.2, §10.3, §14.1 (#15 Olay Penceresi) ve §19.10 gereğince strateji katmanında zaman akarken (günlük/aylık tick adımlarında) ajansı etkileyen kampanya olaylarının tetiklenmesi, oyuncuya görünür artı/eksi etki seçenekleri sunulması ve kararların deterministik olarak simülasyona yansıması gerekiyordu.

## Alınan Kararlar
1. **Veri Modeli (`campaign_events.json` ve `CampaignEventRecord`):**
   - 15 adet zengin kampanya ve tedarikçi olayı tanımlandı (bütçe hibe bonusu, tedarikçi teslimat gecikmesi ve kalite denetimi, araştırma atılımı, jeopolitik zirve, halk ilgisi dalgası, işçi grevi tehdidi, Kestrel stratejik ortaklık teklifi, yetenek transferi, tedarik zinciri kıtlığı, aviyonik hatası, sensör ortak fonlama, motor test atışı, kompozit yeniliği, mali denetim).
   - Her olay; kategori, taban ağırlık (`baseWeight`), tekrarlanabilirlik kuralı (`isRepeatable`), bekleme süresi (`cooldownDays`), tetikleme koşulları (`EventTriggerCondition`) ve en az iki seçenek (`EventOptionRecord`) içerir.
   - Her seçenek oyuncuya görünür özet (`visibleEffectSummary`) ve arka planda somut `EventOutcomeRecord` (bütçe, itibar, araştırma, bilgi puanı, tedarikçi ilişkisi) taşır.

2. **Olay Yönetmeni (`CampaignEventManager` & `EventEvaluator`):**
   - Tetikleme koşulları (`IsEventEligible`): Yıl aralığı, bütçe ve itibar sınırları, zorunlu tedarikçi, aktif araştırma veya üretim varlığı kontrol edilir.
   - Tedarikçi eğilimleri (`CalculateEffectiveWeight`): Şirketlerin `eventWeightModifiers` çarpanları (ör. Helion Dynamics kalite ve gecikme riski) olay taban ağırlıklarını dinamik olarak ölçeklendirir.
   - Deterministik tohumlu RNG (`RngStreamType.Events`): Olay tetiklemeleri ve ağırlıklı seçimler izole tohumla çalışır; simülasyonun diğer sistemlerini bozmaz.

3. **Komut ve Durum Entegrasyonu (`ResolveEventOptionCommand`):**
   - Oyuncu bir opsiyonu seçtiğinde bütçe, itibar (0-100 clamp), araştırma ve bilgi puanları ile tedarikçi ilişki puanları atomik olarak güncellenir.
   - Bütçe hareketleri `EconomyManager` işlem defterine `FinancialRecordType.EventImpact` olarak kaydedilir.
   - Tam simetrik `Undo/Redo` desteği sağlanmıştır.
   - `AdvanceTimeCommand` zaman ilerledikçe bekleme sürelerini ve olay tetiklemelerini yürütür, geri almada önceki bekleyen durumu eksiksiz geri yükler.

4. **UI Toolkit Ekran ve ViewModel:**
   - `EventPopupViewModel`: Reaktif başlık, kategori rozeti, anlatı metni, tarih ve `EventOptionItemViewModel` seçenek listesi.
   - `EventPopupScreenController`: Modal karar kartı, amber vurgulu kenarlıklar, koyu lacivert tema, şık seçenek butonları ve mint/cyan renkli görünür etki özetleri.

5. **Mimari Doğrulama:**
   - `Apogee.Strategy` katmanı tamamen `noEngineReferences = true` ilkesine uymaktadır.
   - EditMode yerel doğrulama testleri 222'den **230**'a çıktı (%100 geçti).
