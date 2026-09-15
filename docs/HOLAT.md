# Qurish holati

Oxirgi yangilanish: 15.09.2026

## Tayyor

| Modul | Holat | Izoh |
|---|---|---|
| M01 Fundament | **Tayyor** | Solution, qatlamlar, DbContext (snake_case, soft delete, UTC), `Initial` migratsiya (PostGIS), audit interceptor, ProblemDetails, health, Serilog, rate limiting, Docker Compose, demo seed (`Seed:Demo`, Development) |
| M02 Auth/RBAC | **Tayyor** | JWT + refresh rotatsiyasi, PBKDF2, rol siyosatlari (`admin.only`, `tutor.only`, `student.only`, `tutor.or.admin`), Telegram `initData` HMAC tekshiruvi, data scoping (`ScopeResolver`/`InScope`), `me`/`logout` |
| M03 Tashkiliy struktura | Fakultet CRUD tayyor, yo'nalish/guruh CRUD yo'q | `GET/POST/PUT/DELETE /api/admin/faculties`, `PATCH {id}/status` (faol/faol emas), `GET groups`; sozlamalar `GET`/`PUT /api/admin/settings` (bayramlar, shablonlar o'qiladi); yo'nalish/guruh yaratish-tahrirlash endpoint'lari yo'q |
| M04 Talabalar registri | O'qish, CRUD yo'q | `GET /api/admin/students` (filtr, holat, kartadagi statistika), `GET /api/admin/tutors`; Excel import, taklif tokenlari, talaba yaratish/tahrirlash yo'q |
| M05 Amaliyot davri | O'qish, CRUD yo'q | Davr/kalendar hisoblari (`PracticeCalendar`, `PeriodLookup`) barcha o'qishlarda ishlaydi; davr yaratish/guruh biriktirish endpoint'i yo'q (seed orqali) |
| M06 Korxonalar | O'qish, CRUD yo'q | `GET /api/admin/companies` (radius bayrog'i `largeRadius`, shubhali kunlar); korxona yaratish/tahrirlash yo'q |
| M07 Shartnoma moderatsiyasi | Asosiy amallar tayyor | Tyutor: `GET /api/tutor/applications`, `{id}`, `POST {id}/decision` (approve/return/reject, checklist, radius); talaba: `GET /api/student/place`; fayl: `GET /api/files/{id}`; talaba ariza yuborish endpoint'i yo'q |
| M08 Geofence check-in | **Asosiy oqim tayyor** | `POST /api/student/checkin` / `checkout`, `GET /api/student/today`, `calendar`: `CheckInPolicy` (ariza → davr → ish kuni → ruxsat → takror → oyna → GPS → radius), har urinish `AttendanceEvent`, idempotent `occurredAt`, `SuspiciousDetector` (tezlik / bir xil koordinata), tranzaksiya (`IApplicationDbContext.Database`) |
| M09 Kundalik | Asosiy amallar tayyor | Talaba: `POST /api/student/diary` (multipart, ≤5 fayl), `GET diary`, `portfolio`; tyutor: `GET /api/tutor/diaries`, `POST {id}/review` (seen/approve/rewrite + ball); takror matn aniqlash yo'q |
| M12 Tyutor dashboardi | O'qish tayyor | `GET /api/tutor/today` (stats, alerts, paged rows), `students`, `calendar`, `map` (so'nggi urinish nuqtalari, rad etilganlar "bad") |
| M13 Ruxsat va qo'lda tuzatish | Asosiy amallar tayyor | Talaba: `GET`/`POST /api/student/leave-requests`; tyutor: `GET /api/tutor/leave-requests`, `POST {id}/decision` (tasdiq → davomat `excused`); qo'lda davomat tuzatish endpoint'i yo'q |
| M14 Baholash, hisobot | Qisman | `GET /api/tutor/grading`, `PUT /api/tutor/grading/{studentId}` (`GradeCalculator`), `GET /api/reports` katalogi, `GET /api/admin/dashboard` statistikasi, `GET /api/admin/audit`; PDF/Excel eksport yo'q |
| M10 Telegram bot, M11 fon vazifalari | Boshlanmagan | Worker loyihasi bo'sh; avto-yopish (18:00), eslatmalar yo'q |
| Frontend (`web/`) | Mock bilan tayyor, v2 ga moslash kutilmoqda | Admin 9, tyutor 8, talaba (TWA) 6 ekran — MSW mock (`VITE_USE_MOCKS=true`); haqiqiy shakllar `web/API-CONTRACT.md` **v2** (backend DTO'laridan), v1→v2 farqlar §5 |

Endpoint'lar: **43** (auth 5 · admin 13 · reports 1 · tyutor 13 · talaba 10 · files 1) — to'liq ro'yxat `web/API-CONTRACT.md`.

## Tekshirilgan

| Qism | Holat |
|---|---|
| `dotnet build -warnaserror` (butun solution) | ✅ 0 warning, 0 error |
| `Amaliyotchi.UnitTests` (domain qoidalari) | ✅ 134 test |
| `Amaliyotchi.IntegrationTests` (Testcontainers PostgreSQL+PostGIS, haqiqiy `Program.cs`) | ✅ 161 test — auth, scoping, seed, migratsiya, admin, tyutor, talaba, fayllar, JSON konvensiyalari |
| EF migratsiya | ✅ `Initial` (14.09.2026), `dotnet ef migrations has-pending-model-changes` toza |
| Test infratuzilmasi | `ApiFixture` (collection), `TestClients` (admin/tyutor/talaba yaratish, login), `MutableClock` — `fixture.Clock.Set(...)`/`Reset()` bilan vaqtga bog'liq ssenariylar (`Student/CheckInClockTests`) |
| Demo seed (`Seed:Demo=true`, Development) | ✅ 38 talaba, 6 korxona, 30 tasdiqlangan ariza, ~350 davomat, ~660 davomat hodisasi (+ bugun 3 ta radius tashqarisi), ~280 kundalik, 4 ruxsat, 5 baho; idempotent (eski bazada faqat hodisalar to'ldiriladi) |

Eslatma: talaba integratsiya testlarining bir qismi (CheckIn/CheckOut/Calendar/Today/Portfolio) oynalarni **haqiqiy** vaqt
atrofida quradi — Toshkent vaqti bilan ~22:30–00:30 oralig'ida (oyna yarim tundan oshib ketganda) ular o'tmasligi mumkin.
Yangi vaqtga bog'liq testlar `MutableClock` bilan yozilsin.

## Qoldi

- [ ] Admin CRUD: fakultet/yo'nalish/guruh, talaba (yaratish, tahrirlash, Excel import, taklif tokenlari), tyutor biriktiruvi, korxona, amaliyot davri, bayramlar, shablon yuklash
- [ ] Talaba ariza yuborish (`POST /api/student/place`) va shartnoma fayli yuklash
- [ ] Tyutor: davomatni qo'lda tuzatish (audit `ManualCheckIn`), shubha belgisini olib tashlash
- [ ] M10 Telegram bot (deep-link, telefon ulashish, bildirishnomalar), M11 Worker (18:00 avto-yopish, eslatmalar)
- [ ] M14: PDF portfolio, Excel davomat eksporti, korxona tavsifnomasi
- [ ] Frontend'ni `API-CONTRACT.md` v2 ga o'tkazish (mock'larni o'chirish)
