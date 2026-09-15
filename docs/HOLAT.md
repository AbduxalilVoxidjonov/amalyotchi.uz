# Qurish holati

Oxirgi yangilanish: 15.09.2026

## Tayyor

| Modul | Holat | Izoh |
|---|---|---|
| M01 Fundament | **Tayyor** | Solution, qatlamlar, DbContext (snake_case, soft delete, UTC), `Initial` migratsiya (PostGIS), audit interceptor, ProblemDetails, health, Serilog, rate limiting, Docker Compose, demo seed (`Seed:Demo`, Development) |
| M02 Auth/RBAC | **Tayyor** | JWT + refresh rotatsiyasi, PBKDF2, rol siyosatlari (`admin.only`, `tutor.only`, `student.only`, `tutor.or.admin`), Telegram `initData` HMAC tekshiruvi, data scoping (`ScopeResolver`/`InScope`), `me`/`logout` |
| M03 Tashkiliy struktura | **Tayyor** — to'liq ierarxiya CRUD | Fakultet → kafedra (`Department`) → yo'nalish → guruh, har darajada `GET/POST/PUT/DELETE` + `PATCH {id}/status`; guruh yaratishda faol `AcademicYear` talab qilinadi (yo'q bo'lsa 409); sozlamalar `GET`/`PUT /api/admin/settings` (bayramlar, shablonlar o'qiladi) |
| M04 Talabalar registri | Talaba — o'qish; **tyutor — CRUD tayyor** | `GET /api/admin/students` (filtr, holat, kartadagi statistika); tyutor: `GET /api/admin/tutors` (+`facultyId` filtri), `GET/POST/PUT /api/admin/tutors[/{id}]`, `PATCH {id}/status` (refresh tokenlar bekor), `POST {id}/password` (parol tiklash, sessiyalar bekor), `PUT {id}/scopes` (ierarxik ko'lam: fakultet/kafedra/yo'nalish/guruh — guruhlarga materializatsiya, kesishmaslik qoidasi, keyin yaratilgan guruh avtomatik qamraladi, tarix saqlanadi), `GET {id}/scope-tree`; Excel import, taklif tokenlari, talaba yaratish/tahrirlash yo'q |
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

Endpoint'lar: **69** (auth 5 · admin 39 · reports 1 · tyutor 13 · talaba 10 · files 1) — to'liq ro'yxat `web/API-CONTRACT.md`.
Admin 39 ga fakultet→kafedra→yo'nalish→guruh ierarxiyasining 19 ta endpoint'i (§2.3.1) va tyutorlar bo'limining
7 ta yangi endpoint'i (`/api/admin/tutors/{id}`, `POST`, `PUT`, `PATCH status`, `POST password`, `PUT scopes`,
`GET scope-tree` — §2.3.3) kiradi.

## Tekshirilgan

| Qism | Holat |
|---|---|
| `dotnet build -warnaserror` (butun solution) | ✅ 0 warning, 0 error |
| `Amaliyotchi.UnitTests` (domain qoidalari) | ✅ 185 test |
| `Amaliyotchi.IntegrationTests` (Testcontainers PostgreSQL+PostGIS, haqiqiy `Program.cs`) | ✅ 241 test — auth, scoping, seed, migratsiya (+ `tutor_scopes` backfill), admin (fakultet/kafedra/yo'nalish/guruh CRUD, tyutor CRUD + ierarxik ko'lam biriktirish + scope-tree + parol/holat), tyutor, talaba, fayllar, JSON konvensiyalari |
| EF migratsiya | ✅ `Initial` (14.09.2026) … `DepartmentsHierarchy` (15.09.2026, `Department` jadvali + ma'lumot ko'chirish), `dotnet ef migrations has-pending-model-changes` toza; jonli bazada (6 fakultet, 38 talaba) sinaldi — ma'lumot yo'qolmadi |
| Test infratuzilmasi | `ApiFixture` (collection), `TestClients` (admin/tyutor/talaba yaratish, login), `MutableClock` — `fixture.Clock.Set(...)`/`Reset()` bilan vaqtga bog'liq ssenariylar (`Student/CheckInClockTests`) |
| Demo seed (`Seed:Demo=true`, Development) | ✅ 38 talaba, 6 korxona, 30 tasdiqlangan ariza, ~350 davomat, ~660 davomat hodisasi (+ bugun 3 ta radius tashqarisi), ~280 kundalik, 4 ruxsat, 5 baho; idempotent (eski bazada faqat hodisalar to'ldiriladi) |

Eslatma: talaba integratsiya testlarining bir qismi (CheckIn/CheckOut/Calendar/Today/Portfolio) oynalarni **haqiqiy** vaqt
atrofida quradi — Toshkent vaqti bilan ~22:30–00:30 oralig'ida (oyna yarim tundan oshib ketganda) ular o'tmasligi mumkin.
Yangi vaqtga bog'liq testlar `MutableClock` bilan yozilsin.

## Qoldi

- [x] Admin CRUD: fakultet → kafedra → yo'nalish → guruh ierarxiyasi (15.09.2026)
- [x] Admin CRUD: tyutor (yaratish, tahrirlash, faol/faol emas, parol tiklash, ierarxik ko'lam biriktiruvi — `PUT /api/admin/tutors/{id}/scopes`, `GET {id}/scope-tree`) (15.09.2026)
- [ ] Admin CRUD: talaba (yaratish, tahrirlash, Excel import, taklif tokenlari), korxona, amaliyot davri, bayramlar, shablon yuklash
- [ ] Talaba ariza yuborish (`POST /api/student/place`) va shartnoma fayli yuklash
- [ ] Tyutor: davomatni qo'lda tuzatish (audit `ManualCheckIn`), shubha belgisini olib tashlash
- [ ] M10 Telegram bot (deep-link, telefon ulashish, bildirishnomalar), M11 Worker (18:00 avto-yopish, eslatmalar)
- [ ] M14: PDF portfolio, Excel davomat eksporti, korxona tavsifnomasi
- [ ] Frontend'ni `API-CONTRACT.md` v2 ga o'tkazish (mock'larni o'chirish)
