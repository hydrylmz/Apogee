# APOGEE — Denge ve Ekonomi Notları (M4-08)

> Bu doküman GDD §4.3, §6.4, §8.1, §16.8 ve §21 M4-08 kapsamında ajans ekonomisi, yıllık bütçe tahsisatı, itibar çarpanları ve üs bakım gideri dengesini açıklar.

---

## 1. Yıllık Bütçe Tahsisatı (Annual Budget Allocation)

Ajans konsorsiyumu her yılbaşında (1 Ocak / Yıl sınır geçişi) ajansa yeni bütçe tahsisi sağlar (GDD §4.3).
Tahsisat miktarı ajansın güncel itibar puanına (`Reputation` [0 – 100]) bağlıdır:

$$\text{Allocation} = \text{MinimumAnnualBudget} + (\text{MaximumAnnualBudget} - \text{MinimumAnnualBudget}) \times \left(\frac{\text{Reputation}}{100}\right)^{\text{Exponent}}$$

### Varsayılan Parametreler (`EconomySettings`):
- **Minimum Yıllık Bütçe Tabanı (`MinimumAnnualBudget`):** $50.0\text{ M\$}$  
  *(En kötü performansta dahi ajansın kilitlenmesini engelleyen acil durum taban fonu)*
- **Maksimum Yıllık Bütçe Tavanı (`MaximumAnnualBudget`):** $800.0\text{ M\$}$  
  *(100 itibar ile küresel ajans zirve finansmanı)*
- **Taban Referans Bütçe (`BaseAnnualBudget`):** $250.0\text{ M\$}$
- **İtibar Eğrisi Üssü (`ReputationCurveExponent`):** $1.0$ (Doğrusal)

### Başlangıç ve Örnek Seviyeler:
| İtibar (`Reputation`) | Durum Açıklaması | Yıllık Bütçe Tahsisi |
|---|---|---|
| $0$ | Ağır kriz / İflas eşiği | $50.0\text{ M\$}$ |
| $30$ | **Kampanya Başlangıcı** (GDD §4.2) | $275.0\text{ M\$}$ |
| $50$ | İstikrarlı orta ölçek ajans | $425.0\text{ M\$}$ |
| $75$ | Başarılı Ay inişleri / Mars sondaları | $612.5\text{ M\$}$ |
| $100$ | Tarihi zirve / İnsanlı derin uzay | $800.0\text{ M\$}$ |

---

## 2. Üs ve Altyapı Bakım Giderleri (Maintenance Costs)

Her ay başında (`GameDate.Month` geçişi), ajansın dünya genelindeki tüm aktif tesisleri için seviyelerine göre bakım kesintisi yapılır (GDD §6.1, §16.7).

### Tesis Başına Aylık Bakım Maliyetleri:
Formül: `BaseLevelSpec.GetMonthlyMaintenanceCost(type, level)`
- **Fırlatma Rampası (`LaunchPad`):**  
  - Seviye 1: $1.50\text{ M\$/ay}$  
  - Seviye 2: $2.70\text{ M\$/ay}$  
  - Seviye 3: $4.86\text{ M\$/ay}$  
  - Seviye 4: $8.75\text{ M\$/ay}$  
  - Seviye 5: $15.75\text{ M\$/ay}$
- **Fabrika / Entegrasyon (`Factory`):**  
  - Seviye 1: $2.00\text{ M\$/ay}$ ($3.40$, $5.78$, $9.83$, $16.71$)
- **Araştırma Merkezi (`ResearchCenter`):**  
  - Seviye 1: $1.80\text{ M\$/ay}$ ($3.15$, $5.51$, $9.65$, $16.88$)
- **Test Tesisi (`TestFacility`):**  
  - Seviye 1: $1.20\text{ M\$/ay}$ ($1.98$, $3.27$, $5.39$, $8.90$)
- **Yer İstasyonu (`GroundStation`):**  
  - Seviye 1: $0.80\text{ M\$/ay}$ ($1.28$, $2.05$, $3.28$, $5.24$)

### Başlangıç Üs Portföyü Bakım Dengesi (GDD §4.2):
- Cape Peninsula: 1× Fırlatma Rampası (L1: $1.50$) + 1× Fabrika (L1: $2.00$) + 1× Araştırma Merkezi (L1: $1.80$)
- **Aylık Toplam Sabit Gider:** $5.30\text{ M\$/ay}$
- **Yıllık Toplam Sabit Gider:** $5.30 \times 12 = 63.60\text{ M\$/yıl}$
- **Net Yıllık Akış (Başlangıçta):**
  - Başlangıç Yıllık Tahsisi ($275.0\text{ M\$}$) − Sabit Bakım ($63.6\text{ M\$}$) = $+211.4\text{ M\$/yıl}$ net serbest nakit akışı.
  - Bu serbest bütçe roket tasarımı, modül alımları, araştırma ve ilk LEO / Ay sondası operasyonları için makul bir marj sunar.

---

## 3. İflas ve Başarısızlık Koşulu (GDD §4.5)

Ajans aşağıdaki iki şart aynı anda sağlandığında iflas eder ve kampanya sona erer:
1. `Budget < BankruptcyDebtLimit` (Varsayılan: $-100.0\text{ M\$}$)
2. `Reputation <= 0.0`
