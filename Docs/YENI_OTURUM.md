# Yeni Oturum Başlatma Rehberi

## Yeni bir AI oturumu açtığında ne yaparsın?

### 1. GDD'yi ver

Sohbete şunu yaz (veya dosyayı sürükle):

```
Docs/APOGEE_GDD.md
```

Ya da kopyala-yapıştır ile şu prompt'u kullan:

---

### 2. Kopyala-yapıştır hazır prompt

```
APOGEE projesinde çalışıyoruz.

Tek doğruluk kaynağın: Docs/APOGEE_GDD.md
Mevcut durum: Docs/PROGRESS.md
Kararlar: Docs/DECISIONS.md
Standartlar: Docs/STANDARDS.md

Önce bu dört dosyayı oku. Sonra PROGRESS.md'deki sıradaki görevi yap.
Mimari ilkeler GDD §0.1'dedir. Her değişikliği PROGRESS.md'ye işle.
Benimle Türkçe konuş; kod ve tanımlayıcılar İngilizce.
```

---

### 3. Belirli bir görev yapmak istiyorsan

GDD §21'deki görev ID'sini söylersen daha hızlı:

```
M1-01'i yap (Deterministik RNG).
GDD §0.2 şablonunu uygula.
```

---

## Özet akış

```
Yeni oturum aç
     ↓
4 Docs dosyasını ver (veya hazır prompt'u yapıştır)
     ↓
AI okur → planını yazar → onayını ister → uygular
     ↓
Bitti → PROGRESS.md güncellenir → sıradaki görev belli olur
```

---

## Önemli hatırlatmalar

- **Dosyaları vermeyi unutma.** AI önceki oturumu hatırlamaz;
  Docs klasörü onun hafızasıdır.
- **"Mimari ilkelere uy"** demek yeterli — §0.1 kuralları geçerlidir demek.
- **Emin olmadığı şeyi sormadan yapmaması için** GDD §0.3'teki "Altın Kurallar" bölümünü göster.
- **Onay ver:** AI planını yazdıktan sonra "devam et" de, başlamadan önce.

---

*Bu dosya: Docs/YENI_OTURUM.md · APOGEE GDD v1.0*
