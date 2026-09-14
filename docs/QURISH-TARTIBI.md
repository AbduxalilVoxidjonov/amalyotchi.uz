# Amaliyotchi — Qurish tartibi va sprint reja

**Ikkinchi hujjat: "qanday quriladi"**
Versiya: 1.0 · Sana: 14.09.2026 · Stek: ASP.NET Core 10 + PostgreSQL/PostGIS

> Birinchi hujjat (`FUNKSIONAL-QOLLANMA.md`) tizim **nima qilishini** belgilaydi.
> Bu hujjat uni **qanday tartibda qurishni** belgilaydi: modullar, bog'liqliklar,
> sprintlar va har bir modulning "tayyor" mezoni.
>
> Bu yerda kod va to'liq API imzolari yo'q — ular har bir modul boshlanishida
> alohida yoziladi. Bu hujjat **rejalashtirish uchun**.

---

## 1. Tanlangan texnik stek

| Qatlam | Texnologiya | Nima uchun |
|---|---|---|
| Backend | **ASP.NET Core 10 (LTS)** | .NET 8 va 9 ning qo'llab-quvvatlashi 2026-yil 10-noyabrda tugaydi; .NET 10 — 2028-yilgacha |
| ORM | **EF Core + Npgsql + NetTopologySuite** | PostGIS geometriyasi bilan to'g'ridan-to'g'ri ishlaydi |
| DB | **PostgreSQL 16 + PostGIS** | `geography` tipi va `ST_DWithin` — masofani qo'lda hisoblash shart emas |
| Fayl saqlash | **MinIO** (keyinchalik S3) | Shartnomalar, hisobot rasmlari, avatarlar |
| Fon vazifalari | **Hangfire** | Kunlik cron ishlari + dashboard orqali kuzatish |
| Kesh / navbat | **Redis** | Bildirishnoma navbati, rate limiting, sessiya |
| Tyutor/Admin UI | **React + Vite + TypeScript** | SPA dashboard; TanStack Query + Zustand |
| Talaba UI | **Telegram Web App (React)** | Alohida kichik bundle, `@twa-dev/sdk` |
| Bot | **Telegram.Bot** (C#) | Webhook rejimida, backend bilan bitta kod bazasida |
| Xarita | **Leaflet + OpenStreetMap** | Bepul, O'zbekiston uchun yetarli; Yandex Maps — muqobil |
| Loglar | **Serilog + Seq** | Tuzilmali loglar, audit bilan aralashtirilmaydi |
| Konteyner | **Docker Compose** | Bitta buyruq bilan lokal muhit |

### Solution strukturasi (Clean Architecture)

```
Amaliyotchi.sln
├── src/
│   ├── Amaliyotchi.Domain          → entity'lar, enum'lar, domain qoidalari
│   ├── Amaliyotchi.Application     → CQRS handler'lar, DTO, validatsiya, interfeyslar
│   ├── Amaliyotchi.Infrastructure  → EF Core, MinIO, Telegram, Hangfire, email/SMS
│   ├── Amaliyotchi.Api             → controller'lar, auth, ProblemDetails, Swagger
│   └── Amaliyotchi.Worker          → fon vazifalari (alohida ishga tushadi)
├── web/
│   ├── dashboard/                  → React SPA (admin + tyutor)
│   └── twa/                        → Telegram Web App (talaba)
└── tests/
    ├── Amaliyotchi.UnitTests
    └── Amaliyotchi.IntegrationTests → Testcontainers (real PostgreSQL+PostGIS)
```

---

## 2. Modullar xaritasi

Tizim 14 ta qurish moduliga bo'lindi. Har bir modul — **mustaqil demo qilish mumkin
bo'lgan** birlik: tugagach, uni buyurtmachiga ko'rsatish mumkin.

| Kod | Modul | Qamrov | Bog'liq | Og'irlik |
|---|---|---|---|---|
| **M01** | Fundament va cross-cutting | Solution skeleti, Docker, DB migratsiya, xatolar formati, audit log, CI | — | O'rta |
| **M02** | Auth va RBAC | JWT, refresh, Telegram `initData` tekshiruvi, rol siyosatlari, ma'lumot ko'lami (data scoping) | M01 | Og'ir |
| **M03** | Tashkiliy struktura | Fakultet → yo'nalish → guruh → o'quv yili; global sozlamalar (radius, kechikish, ish kunlari), hujjat shablonlari, o'quv yilini arxivlash | M02 | Yengil |
| **M04** | Talabalar registri va import | Talaba kartasi, Excel import, taklif tokenlari | M03 | O'rta |
| **M05** | Amaliyot davri va kalendar | Davr yaratish, talabalarni biriktirish, ish kunlari, bayramlar | M04 | O'rta |
| **M06** | Korxonalar va geo-ma'lumot | Korxona kartasi, koordinata, radius, xaritadan tanlash | M02 | O'rta |
| **M07** | Shartnoma moderatsiyasi | Ariza status oqimi, fayl yuklash (MinIO), tyutor qarori | M05, M06 | O'rta |
| **M08** | **Geofence check-in dvigateli** | 5 tekshiruv, `ST_DWithin`, vaqt oynalari, idempotentlik, offline navbat | M07 | **Og'ir — kritik** |
| **M09** | Kundalik hisobot (portfolio) | Hisobot yozish, fayl biriktirish, takror matn aniqlash, tyutor bali | M08 | O'rta |
| **M10** | Telegram bot va TWA qobig'i | Deep-link, telefon ulashish, mini-ilova navigatsiyasi, bildirishnoma yuborish | M02 | Og'ir |
| **M11** | Fon vazifalari va bildirishnomalar | Hangfire jadvali (08:45 → 21:00), outbox pattern, eslatmalar | M10, M08 | O'rta |
| **M12** | Tyutor monitoring dashboardi | "Bugun" ekrani, ro'yxat, kalendar, xarita, filtrlar | M08, M09 | Og'ir |
| **M13** | Ruxsat so'rovlari va qo'lda tuzatish | Ruxsat oqimi, tyutorning qo'lda aralashuvi, audit bilan bog'lash | M08 | Yengil |
| **M14** | Baholash, hisobot va eksport | Ball formulasi, korxona tavsifnomasi, portfolio PDF, davomat Excel, admin statistikasi | M09, M13 | O'rta |

### 2-faza (MVP dan keyin)

`M15` Anti-fraud tahlil · `M16` Admin analitikasi va reyting · `M17` HEMIS integratsiyasi ·
`M18` Korxona mentori roli · `M19` Ko'chma va masofaviy rejimlar

---

## 3. Bog'liqliklar grafi

```
                          ┌──────────────┐
                          │ M01 Fundament│
                          └──────┬───────┘
                                 ▼
                          ┌──────────────┐
                          │ M02 Auth/RBAC│
                          └──┬────────┬──┘
                ┌────────────┘        └────────────┐
                ▼                                  ▼
        ┌───────────────┐                  ┌───────────────┐
        │ M03 Struktura │                  │ M06 Korxonalar│
        └───────┬───────┘                  └───────┬───────┘
                ▼                                  │
        ┌───────────────┐                          │
        │ M04 Talabalar │                          │
        └───────┬───────┘                          │
                ▼                                  │
        ┌───────────────┐                          │
        │ M05 Davr      │                          │
        └───────┬───────┘                          │
                └──────────────┬───────────────────┘
                               ▼
                       ┌───────────────┐      ┌──────────────┐
                       │ M07 Shartnoma │      │ M10 Bot/TWA  │◀── M02
                       └───────┬───────┘      └──────┬───────┘
                               ▼                     │
                       ┌───────────────┐             │
                       │ M08 GEOFENCE  │◀────────────┘
                       └───┬───────┬───┘
              ┌────────────┘       └────────────┐
              ▼                                 ▼
      ┌───────────────┐  ┌──────────────┐  ┌───────────────┐
      │ M09 Kundalik  │  │ M13 Ruxsat   │  │ M11 Fon ishlar│
      └───────┬───────┘  └──────┬───────┘  └───────────────┘
              └────────┬────────┘
                       ▼
              ┌────────────────┐    ┌──────────────────┐
              │ M12 Dashboard  │    │ M14 Baho/Eksport │
              └────────────────┘    └──────────────────┘
```

**Kritik yo'l:** `M01 → M02 → M03 → M04 → M05 → M07 → M08`.
Bu zanjirdagi har qanday kechikish butun loyihani kechiktiradi. M06 va M10 ni
parallel olib borish mumkin.

---

## 4. Qurish tamoyillari

**① Vertikal tilim, gorizontal qatlam emas.**
Barcha jadvallarni birdan yaratib, keyin barcha API larni yozib, keyin UI ga o'tish —
eng keng tarqalgan xato. Har sprint oxirida **ishlaydigan yo'l** bo'lishi kerak:
3-sprintda bitta talaba bitta korxonadan check-in qila olsin, garchi dizayn xom bo'lsa ham.

**② Eng xavfli qismni birinchi sinab ko'rish.**
Geofence — loyihaning yuragi va eng katta noma'lumi. Uni 8-modulda emas, **1-sprintda
texnik spike** sifatida sinab ko'rish kerak: Telegram Web App ichida iPhone va Android'da
GPS `accuracy` qanday qiymat qaytaradi? Agar iOS'da 200 metrdan yomon bo'lsa — butun
mahsulot mantig'i o'zgaradi, buni 3 oydan keyin emas, 3 kunda bilish kerak.

**③ Audit log birinchi kundan.**
Keyin qo'shiladigan narsa emas. M01 da cross-cutting sifatida quriladi, aks holda
har bir handler'ga qo'lda qo'shib chiqishga to'g'ri keladi.

**④ Ma'lumot ko'lami (data scoping) — arxitektura darajasida.**
"Tyutor faqat o'z guruhini ko'radi" degan qoidani har bir so'rovda qo'lda yozish —
xavfsizlik teshigi. M02 da global query filter yoki `ITenantContext` orqali bir marta
hal qilinadi.

**⑤ Vaqt — doimo UTC.**
DB da UTC, foydalanuvchiga Toshkent vaqti. Kunlik oynalar, cron ishlari va "bugun"
tushunchasi shu qoidani buzsa, davomat noto'g'ri hisoblanadi.

---

## 5. Sprint reja

**Taxminlar:** 1 hafta = 1 sprint · 2 kishi (1 backend + 1 frontend) yoki 1 full-stack
dasturchi uchun muddatlar ~1,7 barobar uzayadi.

### Sprint 0 · Tayyorgarlik (3–5 kun)

| Ish | Natija |
|---|---|
| Ochiq savollarga javob olish (funksional qo'llanma, 15-bo'lim) | Kelishilgan qarorlar ro'yxati |
| Repo, branch strategiyasi, CI quvuri | `main` himoyalangan, PR da test ishlaydi |
| Docker Compose: Postgres+PostGIS, MinIO, Redis, Seq | `docker compose up` bilan muhit ko'tariladi |
| **Geofence spike** | iOS va Android TWA da GPS aniqligi o'lchangan hisobot |
| Ma'lumotlar bazasi sxemasi (ERD) tasdiqlangan | Migratsiya yozishga tayyor |

> **To'siq:** spike natijasi yomon chiqsa (iOS aniqligi 150 m dan yomon), bu yerda
> to'xtab qaror qabul qilinadi: radiusni kattalashtirish, Flutter ilovaga o'tish yoki
> qo'shimcha tasdiqlash usuli (selfi, QR) qo'shish.

### Sprint 1 · Fundament va kirish · `M01` `M02`

- Solution skeleti, EF Core, birinchi migratsiya, `ProblemDetails`, Swagger
- Audit log infratuzilmasi (interceptor darajasida)
- JWT + refresh, rol siyosatlari, data scoping mexanizmi
- Telegram `initData` imzosini tekshirish (talaba autentifikatsiyasi)
- **Demo:** admin tizimga kiradi, tyutor kira olmaydi (huquq yo'q) — RBAC ishlayotgani ko'rinadi

### Sprint 2 · Struktura va talabalar · `M03` `M04`

- Fakultet/yo'nalish/guruh/o'quv yili CRUD + admin ekranlari
- Talaba kartasi, Excel import (xato satrlarni ko'rsatish bilan)
- Taklif tokenlari generatsiyasi
- **Demo:** admin struktura yaratadi, tyutor 30 talabani Excel'dan yuklaydi

### Sprint 3 · Davr, korxona va birinchi vertikal tilim · `M05` `M06` `M10`(qism)

- Amaliyot davri yaratish va talabalarni biriktirish
- Korxona kartasi, PostGIS koordinata, xaritadan nuqta tanlash
- Telegram bot: `/start`, deep-link, telefon ulashish, TWA ochilishi
- **Demo:** talaba bot orqali kiradi va o'z davri ma'lumotini ko'radi

### Sprint 4 · Shartnoma oqimi · `M07`

- Fayl yuklash (MinIO), hajm va format cheklovlari
- Ariza status mashinasi: `Qoralama → Ko'rib chiqilmoqda → Tasdiqlangan/Tuzatish/Rad`
- Tyutor moderatsiya ekrani, izoh bilan qaytarish
- **Demo:** talaba shartnoma yuklaydi → tyutor tasdiqlaydi

### Sprint 5–6 · Geofence dvigateli · `M08` *(ikki sprint)*

- `daily_attendances` modeli, kunlik yozuvlarni tayyorlash
- 5 tekshiruv zanjiri, har biri alohida xato kodi bilan
- `ST_DWithin` orqali masofa, `accuracy` filtri
- Idempotentlik (bir kunda bitta check-in, takroriy so'rov xavfsiz)
- Muvaffaqiyatsiz urinishlarni yozish
- Offline navbat: TWA da `localStorage` + `occurred_at` bilan yuborish
- **Demo:** talaba korxonada belgilanadi; 500 metr narida bosganda rad etiladi

> Bu — loyihaning eng qimmat qismi. Ikki sprint ajratilishi tasodifiy emas:
> test holatlari (vaqt chegaralari, kun almashuvi, takroriy so'rov, yomon GPS)
> kodning o'zidan ko'proq vaqt oladi.

### Sprint 7 · Kundalik hisobot va fon vazifalari · `M09` `M11`

- Hisobot yozish, fayl biriktirish, minimal hajm, takror matn aniqlash
- Hangfire jadvali: 08:45, 10:00, 10:30, 16:45, 18:00, 18:30, 21:00
- Bildirishnoma outbox: yuborilmagan xabar yo'qolmaydi
- **Demo:** kun to'liq avtomatik kechadi — eslatmalar keladi, 18:00 da kun yopiladi

### Sprint 8 · Tyutor dashboardi · `M12`

- "Bugun" ekrani: xulosa + diqqat bloki + ro'yxat
- Filtrlar, kalendar ko'rinishi, xarita ko'rinishi
- Kundaliklarni ko'rib chiqish va baholash lentasi
- **Demo:** tyutor 30 talabani bitta ekrandan kuzatadi

### Sprint 9 · Istisnolar va yakunlash · `M13` `M14`

- Ruxsat so'rovi oqimi, tyutorning qo'lda check-in qo'yishi (sabab majburiy)
- Ball formulasi, yakuniy baho
- Portfolio PDF, davomat Excel
- **Demo:** to'liq amaliyot davri boshidan oxirigacha o'ynab ko'riladi

### Sprint 10 · Barqarorlashtirish

- Yuklama testi (200 talaba bir vaqtda 09:00 da check-in bosadi)
- Xavfsizlik tekshiruvi: boshqa tyutorning ma'lumotiga kirib bo'ladimi
- Xatolarni tuzatish, matnlarni sayqallash, foydalanuvchi qo'llanmasi
- Pilot guruhga chiqish

### Jadval xulosasi

| Bosqich | Sprintlar | Muddat (2 kishi) |
|---|---|---|
| Tayyorgarlik | Sprint 0 | 1 hafta |
| Skelet va ma'lumot | 1–2 | 2 hafta |
| Rasmiylashtirish | 3–4 | 2 hafta |
| **Geofence yadrosi** | 5–6 | 2 hafta |
| Kundalik oqim | 7 | 1 hafta |
| Nazorat va yakun | 8–9 | 2 hafta |
| Barqarorlashtirish | 10 | 1 hafta |
| **Jami** | | **~11 hafta** |

---

## 6. Modul kartalari

Har bir modul boshlanishida quyidagi karta to'ldiriladi va jamoa bilan kelishiladi.

### Karta shabloni

```
MODUL: M0X — Nomi
Maqsad:        bir jumlada — nima ishlaydigan bo'ladi
Jadvallar:     yangi/o'zgaradigan jadvallar
API guruhlari: endpoint oilalari (to'liq imzosiz)
Ekranlar:      qaysi UI ekranlari tegishli
Xavflar:       nima noto'g'ri ketishi mumkin
DoD:           tayyor deb hisoblash mezoni
```

### Namuna — M08 (eng kritik modul)

| | |
|---|---|
| **Maqsad** | Talaba korxona hududidan kunlik check-in/check-out qila oladi, tizim har bir urinishni isbot bilan yozadi |
| **Jadvallar** | `daily_attendances`, `attendance_attempts` (muvaffaqiyatsizlar), `companies.location (geography)` indeksi |
| **API guruhlari** | `POST /attendance/check-in`, `POST /attendance/check-out`, `GET /attendance/today`, `POST /attendance/sync` (offline navbat) |
| **Ekranlar** | TWA bosh ekrani (katta tugma + holat), xato holatlari |
| **Xavflar** | iOS GPS aniqligi · soat mintaqasi xatosi · takroriy so'rov · kun almashuvi (23:59) · internet uzilishi |
| **DoD** | Quyidagi 12 test holati o'tadi: radius ichida ✓ · radius chetida (±5 m) · radius tashqarisida · aniqlik yomon · vaqt oynasidan oldin · vaqtdan keyin · dam olish kuni · davr boshlanmagan · shartnoma tasdiqlanmagan · takroriy check-in · check-out siz check-in · offline yuborilgan eski yozuv |

---

## 7. Umumiy Definition of Done

Har bir modul quyidagilarsiz "tayyor" hisoblanmaydi:

- [ ] EF Core migratsiyasi yozilgan va orqaga qaytarish sinab ko'rilgan
- [ ] Asosiy stsenariylar uchun integratsiya testlari (real PostgreSQL + PostGIS ustida)
- [ ] Barcha xatolar `ProblemDetails` formatida, tushunarli matn bilan (foydalanuvchi ko'radigan matn o'zbek tilida)
- [ ] RBAC tekshirilgan: boshqa rol va boshqa guruh ma'lumotiga kirib bo'lmaydi
- [ ] Ma'lumot o'zgartiruvchi har bir amal audit logga tushadi
- [ ] Swagger'da endpoint'lar tavsiflangan
- [ ] UI: loading / xato / bo'sh holat alohida ishlangan (uchalasi ham)
- [ ] TWA ekranlari 360px kenglikda buzilmaydi
- [ ] Demo stsenariysi bajarilib ko'rsatilgan

---

## 8. Xavflar reestri

| Xavf | Ehtimol | Ta'sir | Kamaytirish chorasi |
|---|---|---|---|
| TWA da iOS GPS aniqligi yetarli emas | O'rta | **Juda yuqori** | Sprint 0 da spike; zaxira reja — Flutter ilova yoki QR/selfi bilan tasdiqlash |
| Talabalar soxta GPS ilovasidan foydalanadi | Yuqori | O'rta | Tahlil belgilari (M15), tasodifiy selfi, tyutor tekshiruvi |
| Korxona koordinatasi noto'g'ri belgilanadi | Yuqori | O'rta | Tyutor moderatsiyasi majburiy; birinchi kun "sinov check-in" |
| Talabada internet yo'q / trafik qimmat | O'rta | O'rta | Offline navbat, TWA bundle hajmini kichik ushlash |
| HEMIS API ruxsati berilmaydi | Yuqori | Past | Excel import asosiy yo'l sifatida quriladi, HEMIS — ustama |
| 09:00 da bir vaqtda yuzlab so'rov | O'rta | O'rta | Yuklama testi (Sprint 10), Redis rate limiting, indekslar |
| Shaxsiy ma'lumot (geolokatsiya) huquqiy talablari | O'rta | Yuqori | Elektron rozilik ekrani (S3 qadami), saqlash muddati siyosati |
| Buyurtmachi talablari o'rtada o'zgaradi | Yuqori | O'rta | Har sprint oxirida demo; funksional qo'llanma versiyalanadi |

---

## 9. Birinchi hafta — aniq qadamlar

1. Funksional qo'llanmaning 15-bo'limidagi 10 ta savolni buyurtmachiga yuborish va javob olish
2. `dotnet new` bilan solution skeletini yaratish (5 ta loyiha)
3. `docker-compose.yml`: postgis/postgis:16, minio, redis, seq
4. Birinchi migratsiya: `users`, `roles`, `audit_logs`
5. TWA da geolokatsiya spike: 2 xil telefonda 10 martadan o'lchash, `accuracy` qiymatlarini jadvalga yozish
6. ERD ni yakuniy tasdiqlash (funksional qo'llanmadagi 7 ta jadval + `attendance_attempts`, `permission_requests`, `audit_logs`, `notifications`)
7. Sprint 1 backlog'ini vazifalarga bo'lish

---

*Bu hujjat har sprint oxirida yangilanadi. Modul tugagach, uning qatoriga tugash sanasi va haqiqiy sarflangan vaqt yoziladi — keyingi baholashlar aniqroq bo'lishi uchun.*
