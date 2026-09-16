# Qurish holati

Oxirgi yangilanish: 16.09.2026

## Tayyor

| Modul | Holat | Izoh |
|---|---|---|
| M01 Fundament | **Tayyor** | Solution, qatlamlar, DbContext (snake_case, soft delete, UTC), `Initial` migratsiya (PostGIS), audit interceptor, ProblemDetails, health, Serilog, rate limiting, Docker Compose, demo seed (`Seed:Demo`, Development) |
| M02 Auth/RBAC | **Tayyor** | JWT + refresh rotatsiyasi, PBKDF2, rol siyosatlari (`admin.only`, `tutor.only`, `student.only`, `tutor.or.admin`), Telegram `initData` HMAC tekshiruvi, data scoping (`ScopeResolver`/`InScope`), `me`/`logout` |
| M03 Tashkiliy struktura | **Tayyor** — to'liq ierarxiya CRUD | Fakultet → kafedra (`Department`) → yo'nalish → guruh, har darajada `GET/POST/PUT/DELETE` + `PATCH {id}/status`; guruh yaratishda faol `AcademicYear` talab qilinadi (yo'q bo'lsa 409); sozlamalar `GET`/`PUT /api/admin/settings` (bayramlar, shablonlar o'qiladi) |
| M04 Talabalar registri | Talaba — o'qish; **tyutor — CRUD tayyor** | `GET /api/admin/students` (filtr, holat, kartadagi statistika); tyutor: `GET /api/admin/tutors` (+`facultyId` filtri — fakultetlaridan biri), `GET/POST/PUT /api/admin/tutors[/{id}]` (tyutor **bir nechta fakultetga** biriktiriladi — `facultyIds[]`/`faculties[]`, `tutor_faculties` jadvali, birinchisi asosiy `User.FacultyId`; ko'lami bor fakultetni olib tashlash 409), `PATCH {id}/status` (refresh tokenlar bekor), `POST {id}/password` (parol tiklash, sessiyalar bekor), `PUT {id}/scopes` (ierarxik ko'lam: fakultet/kafedra/yo'nalish/guruh — guruhlarga materializatsiya, kesishmaslik qoidasi, keyin yaratilgan guruh avtomatik qamraladi, tarix saqlanadi), `GET {id}/scope-tree` (har fakultet uchun bittadan daraxt — massiv); **tyutor uchun talaba profili** — `GET /api/tutor/students/{id}` (profil + korxona + ariza + davr + davomat/kundalik statistikasi + joriy baho), `GET {id}/attendance?from=&to=` (kun-bakun davomat: holat, check-in/out vaqti, masofa, koordinata, selfi havolasi, urinishlar/rad etilganlar soni; oraliq ≤ 400 kun), `GET {id}/diaries`; Excel import, taklif tokenlari, talaba yaratish/tahrirlash yo'q |
| M05 Amaliyot davri | O'qish, CRUD yo'q | Davr/kalendar hisoblari (`PracticeCalendar`, `PeriodLookup`) barcha o'qishlarda ishlaydi; davr yaratish/guruh biriktirish endpoint'i yo'q (seed orqali) |
| M06 Korxonalar | **O'qish to'liq** (detal + talabalar), CRUD yo'q | Admin: `GET /api/admin/companies` (+ `maxStudents`/`overLimit`), `GET {id}` (`CompanyDetail` — rekvizit, koordinata, davrlar kesimi), `GET {id}/students` (ariza holati, tyutor, davomat, kundalik); tyutor: `GET /api/tutor/companies`, `{id}`, `{id}/students` — **ko'lam kesimida** (ko'lamda talabasi yo'q korxona → 404); **STIR nazorati** — `maxStudentsPerCompany` sozlamasi (default 10, 1–200) va `tooManyStudents` bayrog'i (ustuvorlik: `suspicious` → `tooManyStudents` → `largeRadius`); korxona yaratish/tahrirlash **hamon yo'q** |
| M07 Shartnoma moderatsiyasi | Asosiy amallar tayyor | Tyutor: `GET /api/tutor/applications`, `{id}`, `POST {id}/decision` (approve/return/reject, checklist, radius); talaba: `GET /api/student/place`; fayl: `GET /api/files/{id}`; talaba ariza yuborish endpoint'i yo'q |
| M08 Geofence check-in | **Asosiy oqim tayyor** | `POST /api/student/checkin` / `checkout`, `GET /api/student/today`, `calendar`: `CheckInPolicy` (ariza → davr → ish kuni → ruxsat → takror → oyna → GPS → radius), har urinish `AttendanceEvent`, idempotent `occurredAt`, `SuspiciousDetector` (tezlik / bir xil koordinata), tranzaksiya (`IApplicationDbContext.Database`); **check-in/check-out selfisi** — `multipart/form-data` (`photo`, ≤ 5 MB, JPEG/PNG/WebP/HEIC; tana ≤ 6 MB), standart bo'yicha **ixtiyoriy**, `checkinPhotoRequired` sozlamasi bilan majburiy qilinadi (rasmsiz urinish → 400 `errors.Photo`); **rad etilgan urinish rasmi ham saqlanadi** (`AttendanceEvent.PhotoFileId`), qabul qilingani `DailyAttendance` da; `application/json` yo'li eski klientlar uchun saqlangan — multipart action `[Consumes]` bilan, JSON action cheklovsiz fallback, shuning uchun noto'g'ri yoki yo'q `Content-Type` → **415** (marshrutlash noaniq emas) |
| M09 Kundalik | Asosiy amallar tayyor | Talaba: `POST /api/student/diary` (multipart, ≤5 fayl), `GET diary`, `portfolio`; tyutor: `GET /api/tutor/diaries`, `POST {id}/review` (seen/approve/rewrite + ball); takror matn aniqlash yo'q |
| M12 Tyutor dashboardi | O'qish tayyor | `GET /api/tutor/today` (stats, alerts, paged rows), `students` (+ `{id}`, `{id}/attendance`, `{id}/diaries`), `calendar`, `map` (so'nggi urinish nuqtalari, rad etilganlar "bad"), **`companies`** (+ `{id}`, `{id}/students`) — ko'lamdagi korxonalar, jamlangan davomat va STIR nazorati bilan |
| M13 Ruxsat va qo'lda tuzatish | Asosiy amallar tayyor | Talaba: `GET`/`POST /api/student/leave-requests`; tyutor: `GET /api/tutor/leave-requests`, `POST {id}/decision` (tasdiq → davomat `excused`); qo'lda davomat tuzatish endpoint'i yo'q |
| M14 Baholash, hisobot | Qisman | `GET /api/tutor/grading`, `PUT /api/tutor/grading/{studentId}` (`GradeCalculator`), `GET /api/reports` katalogi, `GET /api/admin/dashboard` statistikasi, `GET /api/admin/audit`; PDF/Excel eksport yo'q |
| M10 Telegram bot, M11 fon vazifalari | Boshlanmagan | Worker loyihasi bo'sh; avto-yopish (18:00), eslatmalar yo'q |
| Frontend (`web/`) | **v3 sahifalari yozilgan va testdan o'tgan, lekin hamon MSW mock'da** | Admin 9, tyutor 8, talaba (TWA) 6 ekran + **v3 da qo'shilganlari**: `/admin/companies/:id` (korxona detali + talabalar), `/tutor/companies` va `/tutor/companies/:id` (tyutor korxonalar bo'limi — sidebar'ga "Korxonalar" qo'shilgan), `/tutor/students/:id` (talaba profili — davomat jadvali, selfi ko'rish, kundaliklar), TWA check-in/check-out selfi oqimi. Marshrutlar `router.tsx` ga ulangan. **Backend'ga ulanmagan** — hammasi hamon MSW mock'lari bilan ishlaydi (`VITE_USE_MOCKS=true`); haqiqiy shakllar `web/API-CONTRACT.md` **v3**, v1→v2 farqlar §5, v2→v3 §6 |

Endpoint'lar: **77** (auth 5 · admin 41 · reports 1 · tyutor 19 · talaba 10 · files 1) — to'liq ro'yxat `web/API-CONTRACT.md`.
Sanoq `src/Amaliyotchi.Api/Controllers/**` dagi `[Http*]` atributlaridan olingan: atributlar **79 ta**, lekin
`POST /api/student/checkin` va `POST /api/student/checkout` har birida ikkitadan action bor
(`multipart/form-data` va `application/json`, `[Consumes]` bilan ajratilgan) — yo'l bitta, shuning uchun **77**.
Admin 41 ga fakultet→kafedra→yo'nalish→guruh ierarxiyasining 19 ta endpoint'i (§2.3.1), tyutorlar bo'limining
7 ta endpoint'i (§2.3.3) va korxona detali/talabalari (`GET /api/admin/companies/{id}`, `{id}/students`) kiradi;
tyutor 13→19 — talaba profili (3) va korxonalar (3).

## Tekshirilgan

| Qism | Holat |
|---|---|
| `dotnet build -warnaserror` (butun solution) | ✅ 0 warning, 0 error (16.09.2026 da ishga tushirilgan) |
| `Amaliyotchi.UnitTests` (domain qoidalari) | ✅ **201 test** (0 yiqilgan) |
| `Amaliyotchi.IntegrationTests` (Testcontainers PostgreSQL+PostGIS, haqiqiy `Program.cs`) | ✅ **274 test** (0 yiqilgan) — auth, scoping, seed, migratsiya (`tutor_scopes` va `tutor_faculties` backfill testlari; `CheckInPhotos` uchun alohida backfill testi yo'q — migratsiya faqat nullable ustun qo'shadi), admin (fakultet/kafedra/yo'nalish/guruh CRUD, tyutor CRUD + ko'p fakultet + ierarxik ko'lam biriktirish + scope-tree + parol/holat, korxona detali/talabalari), tyutor (talaba profili, kun-bakun davomat, korxonalar), talaba (check-in selfisi — `Student/CheckInPhotoTests`: selfi saqlash/ko'lam, JSON fallback, `Content-Type` marshrutlash, noto'g'ri tur → 415, sarlavhasiz so'rov → 415, OpenAPI generatsiyasi), fayllar, JSON konvensiyalari |
| EF migratsiya | ✅ `Initial` (14.09.2026) … `TutorFaculties` (15.09.2026) … **`20260916115017_CheckInPhotos` (16.09.2026)** — `attendance_events.photo_file_id`, `daily_attendances.check_in_photo_file_id`, `daily_attendances.check_out_photo_file_id` (nullable, `stored_files` ga FK `RESTRICT`, indekslar); `dotnet ef migrations has-pending-model-changes` — 16.09.2026 da qayta ishga tushirildi, toza ("No changes have been made to the model"); jonli bazada (6 fakultet, 38 talaba) sinaldi — ma'lumot yo'qolmadi |
| Frontend testlari (`web/`) | ✅ dashboard **182**, twa **34**, shared **2** — hammasi o'tdi; typecheck / lint / build toza. Mock ma'lumot ustida ishlaydi — haqiqiy backend bilan uchdan-uchga sinov yo'q |
| Test infratuzilmasi | `ApiFixture` (collection), `TestClients` (admin/tyutor/talaba yaratish, login), `MutableClock` — `fixture.Clock.Set(...)`/`Reset()` bilan vaqtga bog'liq ssenariylar (`Student/CheckInClockTests`) |
| Demo seed (`Seed:Demo=true`, Development) | ✅ 38 talaba, 6 korxona, 30 tasdiqlangan ariza, ~350 davomat, ~660 davomat hodisasi (+ bugun 3 ta radius tashqarisi), ~280 kundalik, 4 ruxsat, 5 baho; idempotent (eski bazada faqat hodisalar to'ldiriladi) |

Eslatma: talaba integratsiya testlarining bir qismi (CheckIn/CheckOut/Calendar/Today/Portfolio) oynalarni **haqiqiy** vaqt
atrofida quradi — Toshkent vaqti bilan ~22:30–00:30 oralig'ida (oyna yarim tundan oshib ketganda) ular o'tmasligi mumkin.
Yangi vaqtga bog'liq testlar `MutableClock` bilan yozilsin.

## Qoldi

- [x] Admin CRUD: fakultet → kafedra → yo'nalish → guruh ierarxiyasi (15.09.2026)
- [x] Admin CRUD: tyutor (yaratish, tahrirlash, faol/faol emas, parol tiklash, ierarxik ko'lam biriktiruvi — `PUT /api/admin/tutors/{id}/scopes`, `GET {id}/scope-tree`) (15.09.2026)
- [x] Tyutor uchun talaba profili: `GET /api/tutor/students/{id}`, `{id}/attendance?from=&to=`, `{id}/diaries` (16.09.2026)
- [x] Korxonalarni o'qish: admin detal + talabalar ro'yxati, tyutor korxonalar bo'limi, STIR nazorati (`maxStudentsPerCompany`, `tooManyStudents` bayrog'i) (16.09.2026)
- [x] Check-in/check-out selfisi: multipart `photo`, `checkinPhotoRequired` sozlamasi, rad etilgan urinish rasmini saqlash, `StoredFileKind.CheckInPhoto` (16.09.2026)
- [ ] **Korxona yaratish/tahrirlash hamon yo'q** (admin) — faqat o'qish; korxona seed orqali keladi
- [ ] **Talaba ariza yuborish oqimi hamon yo'q** (`POST /api/student/place`) va shartnoma fayli yuklash
- [ ] Admin CRUD: talaba (yaratish, tahrirlash, Excel import, taklif tokenlari), amaliyot davri, bayramlar, shablon yuklash
- [ ] Tyutor: davomatni qo'lda tuzatish (audit `ManualCheckIn`), shubha belgisini olib tashlash
- [ ] M10 Telegram bot (deep-link, telefon ulashish, bildirishnomalar), M11 Worker (18:00 avto-yopish, eslatmalar)
- [ ] M14: PDF portfolio, Excel davomat eksporti, korxona tavsifnomasi
- [ ] Frontend'ni haqiqiy backend'ga ulash: v3 sahifalari yozilgan va testdan o'tgan, lekin hamon MSW mock'lari
      bilan ishlaydi — `VITE_USE_MOCKS=false` bilan uchdan-uchga sinov qilinmagan
- [ ] **TWA selfi oqimi haqiqiy qurilmada sinalmagan** — kamera ruxsati, HEIC/HEIF, rasm siqish va 6 MB chegarasi
      faqat integratsiya testlarida (server tomonda) tekshirilgan
