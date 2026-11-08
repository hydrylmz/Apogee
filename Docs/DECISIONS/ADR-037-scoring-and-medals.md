# ADR-037: Misyon Skoru ve Madalyalar (M5-08)

## Durum
Kabul Edildi

## Bağlam
GDD §11 (Skor ve İlerleme), §11.1 (Misyon Skoru 5 Temel Direk), §11.2 (Madalyalar), §14.1 (#16 Sonuç Raporu) ve §21 M5-08 gereğince:
Tamamlanan veya sonuçlanan misyonların 5 çekirdek performans direği (Bilim Teslimi %35, Amaç Başarısı %20, Kaynak Verimliliği %15, Karar Kalitesi %15, Planlama Verimliliği %15) üzerinden ağırlıklı olarak puanlanması, zorluk çarpanı (`RewardMultiplier` / claim level) ile ölçeklenmesi, aşırı marj israfının cezalandırılması, belirlenen başarı başarımları (madalyalar) için bonus puan ve rozet atanması ve sonuç raporunda gösterilmesi gerekiyordu.

## Alınan Kararlar

1. **Ayar Dosyası ve Veri Modeli (`scoring_config.json` / `Apogee.Core.Simulation.Scoring`):**
   - **`ScoringConfig`:** Ağırlıkların kodda gömülü kalmaması için JSON tabanlı konfigürasyon yapısı.
   - **`MedalDefinition` & `AwardedMedal`:** Madalyaların kimliği, adı, açıklaması, simgesi, kategorisi ve bonus puanı.
   - **`MissionScoreBreakdown`:** 5 direğin oranları, taban puan, zorluk katsayısı, madalya bonusları, nihai puan (`FinalScore`) ve harf notu derecelendirmesi (`Grade`: S, A, B, C, D, F).

2. **Çekirdek Skor Hesaplayıcı (`MissionScoreCalculator`):**
   - **Bilim Teslimi (%35):** Yere inen veri / üretilen veri oranı.
   - **Amaç Başarısı (%20):** Sağlanan başarı kriterlerinin oranı.
   - **Kaynak Verimliliği (%15):** Güç ve Δv marjlarının ideal aralıkta (%10–25) olması; aşırı marj israfı için kademeli ceza.
   - **Karar Kalitesi (%15):** Fırlatma/EDL kaza cezaları, yıpranma oranı ve zararlı olay kararlarının oranı.
   - **Planlama Verimliliği (%15):** Pencere yakalama ve görev icra bütünlüğü.
   - **Madalya Kuralları (GDD §11.2):** "Sıfır kayıp veri", "Pencereyi yakaladı", "Fırtınayı atlattı", "İlk insanlı", "Ucuz ama etkili", "Tek kaynak yok", "Hafif ve hızlı".

3. **Sonuç Raporu ve Arayüz Entegrasyonu (`MissionReport`, `MissionReportViewModel`, `MissionReportScreenController`):**
   - `MissionReport` modeline `ScoreBreakdown` özelliği eklendi. `MissionReportEvaluator` raporu üretirken skoru ve madalyaları otomatik bağlar.
   - `MissionReportViewModel` reaktif `FinalScoreText`, `GradeText`, `ScorePillarsSummaryText` ve `AwardedMedalsList` alanları ile donatıldı.
   - `MissionReportScreenController` başlık barında Skor ve Harf Derecesi rozetlerini gösterir; sağ sütunda ise "6. KAZANILAN MADALYALAR" bölümünü ve direklerin özet dökümünü çizer.

4. **Test ve Doğrulama:**
   - 9 yeni test (`ScoringRunnerTests`):
     - Varsayılan ayar yüklemesi ve madalya listesi
     - Nominal görevde yüksek puan ve S/A derecesi
     - Başarısız görevde düşük puan ve F derecesi
     - Aşırı marj israfı cezası mantığı
     - Zorluk çarpanının puanı doğru ölçeklemesi
     - "Sıfır kayıp veri" madalyası kazanımı
     - "İlk insanlı" madalyası kazanımı
     - "Fırtınayı atlattı" güvenli mod kararı madalyası kazanımı
     - `MissionReportViewModel` skor ve madalya bağlama doğrulaması
   - Toplam test sayısı 293'ten **302**'ye yükseldi (%100 başarıyla geçti, 0 hata).
   - `noEngineReferences` kuralı doğrulandı.
