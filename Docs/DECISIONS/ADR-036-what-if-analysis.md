# ADR-036: "Ya Şöyle Olsaydı?" (What-If) Analizi (M5-07)

## Durum
Kabul Edildi

## Bağlam
GDD §10.5 ("Ya şöyle olsaydı?"), §13.2 ("Tek karar değiştirip karşılaştır"), §14.1 (#16 Sonuç Raporu Ekranı) ve §21 M5-07 gereğince:
Tamamlanan bir misyonun ardından oyuncunun, operasyon sırasında verilmiş kritik kararlardan (karar kartları/olay seçenekleri) birini değiştirip deterministik simülasyon sayesinde ucuz ve anlık olarak raporu yeniden hesaplayabilmesi, orijinal sonuç ile hipotetik sonuç arasındaki farkı (başarı durumu, bütçe, itibar, bilim verisi ve araştırma puanı) karşılaştırmalı olarak görebilmesi hedeflenmiştir.

## Kararlar

1. **Çekirdek Simülatör ve Karşılaştırma Modeli (`Apogee.Core.Simulation.Report`):**
   - **`WhatIfChange`:** Değiştirilen olayın kimliğini, başlığını, orijinal seçimi ve denenmek istenen alternatif seçeneği saklar.
   - **`WhatIfComparison`:** Orijinal rapor ile hipotetik raporu yan yana koyarak sonuç değişimini (`OutcomeChanged`), net bütçe, itibar, bilim verisi ve araştırma puanı farklarını ve sade dilde özetini (`DeltaSummaryText`) hesaplar.
   - **`WhatIfSimulator`:** Saf C# fonksiyonel modülüdür. Orijinal kampanya veya misyon verilerini asla mutasyona uğratmaz. Seçilen alternatif kararın farklarını (bilim verisi deltası, güvenilirlik yıpranması vb.) ve yamalı olay günlüğünü (`BuildPatchedEventLog`) kullanarak `MissionReportEvaluator.Evaluate` üzerinden deterministik olarak yeni rapor üretir.

2. **Kullanıcı Arayüzü Modeli ve Ekran Entegrasyonu (`Apogee.UI`):**
   - **`WhatIfViewModel`:** Yeniden karar verilebilir olayları filtreler (`EligibleEvents`), oyuncunun seçtiği karara göre alternatif seçenekleri listeler, `RunWhatIf` ile hesaplamayı tetikler ve `ClearWhatIf` ile orijinal duruma dönülmesini sağlar.
   - **`MissionReportViewModel`:** Bünyesinde `WhatIf` alt modelini barındırır ve misyon bağlandığında otomatik olarak devreye alır.
   - **`MissionReportScreenController`:** Sonuç Raporu ekranının altına ayrılmış "Ya Şöyle Olsaydı?"paneli eklenmiştir. Olay seçim butonları, alternatif karar seçenekleri, sonuç rozeti ve fark telemetrisi reaktif olarak güncellenir.

3. **Doğrulama ve Testler:**
   - 9 yeni test (`WhatIfRunnerTests`):
     - Yeniden seçilebilir olayların doğru filtrelenmesi (tek seçenekli veya beklemede olanların elenmesi)
     - Alternatif seçeneklerin orijinali hariç tutarak listelenmesi
     - Karar değişimiyle başarısız bir misyonun başarıya dönüşebilmesi (`OutcomeFlipsToSuccess`)
     - Net deltalara (bütçe, itibar, bilim verisi) göre doğru hesaplama yapılması
     - Orijinal misyon ve rapor verilerinin bozulmadan korunması (immutability)
     - Deterministik tekrarlanabilirlik
     - `WhatIfViewModel` veri bağlama ve seçim akışı
     - `RunWhatIf` yürütmesi ve reaktif rozet/özet güncellemeleri
     - `ClearWhatIf` ile temizleme ve başlangıç durumuna dönüş
   - Toplam test sayısı 284'ten **293**'e yükseldi (%100 geçti, 0 hata).
   - `noEngineReferences` kuralı doğrulandı.
