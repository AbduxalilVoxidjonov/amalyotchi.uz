# Live joylashuv — kontrakt (2026-10-14)

Maqsad: talaba telefonining joylashuvi **faqat** amaliyot joyi (korxona geozonasi) atrofidami yoki yo'qmi — shuni aniqlash uchun.
Ikki qism:

1. **Ekranda jonli** — QR/davomat ekrani ochiq turganda joylashuv oqimi (stream): "Siz amaliyot joyidasiz ✓" /
   "Korxonadan 320 m uzoqdasiz" real vaqtda. Belgilash tugmasi faqat radius ichida faol. Hisob klientda
   (Haversine), serverga hech narsa yuborilmaydi — yakuniy qarorni baribir `POST /checkin` qiladi.
2. **Ish vaqtida fonda kuzatish** — muvaffaqiyatli check-in'dan keyin to check-out'gacha (yoki kun avtomatik
   yopilguncha) ilova fonda bo'lsa ham joylashuv har `intervalSec` da (yoki `distanceFilterM` siljishda) to'planib,
   paket bilan serverga yuboriladi. Admin talaba ish joyidan chiqib ketganini ko'radi.

## Qoidalar

- **Sessiya**: kuzatuv faqat bugungi davomat yozuvida `checkInAt` bor va `checkOutAt` yo'q bo'lsa faol. Check-out
  yoki avtomatik yopilish (mavjud auto-close mantig'i) bilan to'xtaydi. Sessiyadan tashqaridagi ping'lar qabul
  qilinmaydi (xato emas — `accepted` ga kirmaydi).
- **Ping holati** (server hisoblaydi, check-in bilan bir xil geozona qoidasi — `docs/FUNKSIONAL-QOLLANMA.md`):
  - `accuracyM > 100` → `inaccurate` (na ichkari, na tashqari hisoblanadi)
  - `distanceM <= radiusM` → `inside`, aks holda `outside`
- **outsideMinutes**: ketma-ket ping'lar orasidagi vaqt, oldingi ping `outside` bo'lsa qo'shiladi; bitta oraliq
  `2 × intervalSec` dan oshsa shu qiymatda kesiladi (signal uzilishi tashqarida bo'lish deb hisoblanmaydi).
- **signalLost**: sessiya faol, lekin oxirgi ping `3 × intervalSec` dan eski (yoki check-in'dan beri ping yo'q va
  shuncha vaqt o'tgan).
- **Rozilik**: fonda kuzatuv uchun talaba bir marta rozilik beradi (ilovadagi ochiq ogohlantirish ekrani →
  server'da vaqti saqlanadi). Rozilik bo'lmasa fonda ping yuborilmaydi; ekrandagi jonli ko'rsatkich rozilik talab qilmaydi.
- **Soxta joylashuv**: klient `isMocked` yuboradi; server saqlaydi va adminga belgi sifatida ko'rsatadi
  (ping rad etilmaydi).
- **Saqlash muddati**: ping'lar 60 kundan keyin o'chiriladi (fon job).
- **Sozlamalar** (admin → Sozlamalar): `trackingEnabled` (sukut `true`), `trackingIntervalSec` (sukut 300,
  ruxsat 60–900), `trackingDistanceFilterM` (sukut 50, 10–500).
- Barcha vaqtlar UTC ISO-8601, JSON camelCase, enum camelCase string, xatolar ProblemDetails.

## Talaba API

### `GET /api/student/today` — yangi maydon
```jsonc
"tracking": {
  "enabled": true,            // sozlamada yoqilgan VA sessiya faol (check-in bor, check-out yo'q)
  "consentGiven": true,       // talaba fonda kuzatuvga rozilik berganmi
  "intervalSec": 300,
  "distanceFilterM": 50,
  "activeUntil": "2026-10-14T13:30:00Z"  // ko'pi bilan qachongacha (auto-close vaqti); null = noma'lum
}
```
`tracking` doim keladi (sessiya yo'q bo'lsa `enabled=false`). `POST /checkin` javobidagi TodayDto ham shu maydonni
oladi — klient check-in'dan keyin darhol fonda kuzatuvni boshlaydi.

### `PUT /api/student/tracking/consent`
Body `{ "consent": true }` → 204. `consent=false` → rozilikni qaytarib olish (keyingi ping'lar qabul qilinmaydi).
`consent` yo'q/false emas bo'lsa 400 `errors.Consent`.

### `POST /api/student/location-pings`
```jsonc
// so'rov (1..100 ta, eskidan yangiga)
{ "pings": [
  { "lat": 41.311, "lng": 69.279, "accuracyM": 12.5,
    "occurredAt": "2026-10-14T09:05:00Z", "isMocked": false, "source": "background" } // source: foreground | background
] }
// javob 200
{ "accepted": 3,
  "tracking": { /* yuqoridagi bilan bir xil */ },
  "last": { "status": "inside", "distanceM": 42, "occurredAt": "2026-10-14T09:15:00Z" } } // last null bo'lishi mumkin
```
- `occurredAt` 24 soatdan eski yoki 2 daqiqadan ko'proq kelajakda → o'sha ping tashlanadi (qolganlari qabul).
- Bir xil `occurredAt` (talaba bo'yicha) takror kelsa — idempotent, ikkinchisi tashlanadi.
- Rozilik yo'q yoki `trackingEnabled=false` → 200, `accepted: 0`, `tracking.enabled` mos holda (klient to'xtaydi).
- Rate limit: talaba bo'yicha daqiqasiga 30 so'rov.

## Admin API

### `GET /api/admin/today` — har qatorga yangi maydon
```jsonc
"presence": {                     // null — bugun check-in yo'q yoki kuzatuv o'chiq
  "state": "inside",              // inside | outside | inaccurate | signalLost | stopped (check-out bo'lgan) | noConsent
  "lastPingAt": "2026-10-14T09:15:00Z",
  "distanceM": 42,
  "outsideMinutes": 18,
  "mocked": false                 // bugun kamida bitta isMocked ping bo'lganmi
}
```

### `GET /api/admin/students/{studentId}/location-pings?date=2026-10-14`
```jsonc
{ "date": "2026-10-14",
  "place": { "name": "Tech Solutions MChJ", "lat": 41.31, "lng": 69.28, "radiusM": 150 },  // null bo'lishi mumkin
  "checkInAt": "...", "checkOutAt": null,
  "consentGiven": true,
  "pings": [ { "occurredAt": "...", "lat": 0, "lng": 0, "accuracyM": 10, "distanceM": 42,
               "status": "inside", "isMocked": false, "source": "background" } ],
  "summary": { "total": 40, "inside": 35, "outside": 4, "inaccurate": 1,
               "outsideMinutes": 18, "longestGapMinutes": 22, "lastPingAt": "..." } }
```
Data scoping: mavjud admin qoidalari (bosh admin / oddiy admin) — boshqa admin endpoint'lari qanday cheklasa, shunday.

### `GET/PUT /api/admin/settings` — yangi maydonlar
`trackingEnabled`, `trackingIntervalSec`, `trackingDistanceFilterM` (validatsiya yuqoridagi oraliqlar).

## Mobil ilova (app/)

- Ruxsatlar: oldin "Ilovadan foydalanganda" (ekrandagi jonli ko'rsatkich va check-in). Fonda kuzatuv uchun —
  check-in'dan keyin, **ochiq ogohlantirish ekrani** (nima, nega, qachon to'xtaydi, faqat ish vaqtida) → rozilik →
  `PUT /tracking/consent` → "Doim" ruxsati (Android 10+ sozlamalar orqali, iOS Always).
- Android: foreground service doimiy bildirishnoma bilan ("Amaliyotchi ish vaqtida joylashuvni tekshirmoqda"),
  `ACCESS_BACKGROUND_LOCATION`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `POST_NOTIFICATIONS`.
  iOS: `UIBackgroundModes: location`, `NSLocationAlwaysAndWhenInUseUsageDescription`, `allowBackgroundLocationUpdates`,
  `showBackgroundLocationIndicator`.
- Ping'lar lokal navbatda (internet yo'q bo'lsa saqlanadi), ≤100 tadan paket qilib yuboriladi; `tracking.enabled=false`
  kelsa yoki `activeUntil` o'tsa yoki check-out bo'lsa — kuzatuv to'xtaydi, navbatdagilar yuborib bo'lingach tozalanadi.
- Cheklov (hujjatlashtiriladi): ilova tizim tomonidan butunlay o'ldirilsa kuzatuv to'xtaydi → admin'da `signalLost`.
  Ilova qayta ochilganda sessiya hali faol bo'lsa kuzatuv avtomatik tiklanadi.

## Backend aniqlashtirishlari (2026-10-14, implementatsiyadan)

Kontrakt o'zgarmadi — faqat noaniq joylar qanday hal qilingani (to'liq: `web/API-CONTRACT.md` §6.35):

- **Sessiya ping bo'yicha**: har ping o'z kunining (Toshkent) davomat yozuviga tegishli bo'lsa qabul qilinadi —
  `checkInAt ≤ occurredAt ≤ (checkOutAt ?? auto-close momenti)`. Shuning uchun check-out'dan keyin yuborilgan
  **navbatdagi** (sessiya ichida yig'ilgan) ping'lar ham qabul qilinadi; check-out'dan keyingi ping'lar — yo'q.
- `inaccurate` chegarasi — check-in bilan bir xil `minGpsAccuracy` sozlamasi (sukut 100 m).
- Idempotentlik kaliti — `occurredAt` millisekundgacha (UTC). Paket ichidagi takror ham bir marta saqlanadi.
- Koordinatasi/aniqligi yaroqsiz ping butun paketni rad etmaydi — o'sha ping tashlanadi (offline navbat tiqilib qolmasin).
  400 faqat `pings` yo'q, bo'sh yoki 100 dan ko'p bo'lsa (`errors.Pings`).
- Vaqtlar JSON'da UTC offset bilan: `"2026-10-14T13:00:00+00:00"` (API'dagi boshqa vaqtlar kabi).
- `presence`: check-in'dan beri hali ping yo'q va `3 × intervalSec` o'tmagan → `inside` (`lastPingAt: null`,
  `distanceM` — check-in masofasi). `outsideMinutes` oxirgi ping'dan keyingi vaqtni hisoblamaydi.
- `trackingIntervalSec` sozlamasining birligi `unit: "sec"`.
- Admin `GET /api/admin/today` ixtiyoriy `?presence=outside|signalLost` filtrini ham qabul qiladi.
