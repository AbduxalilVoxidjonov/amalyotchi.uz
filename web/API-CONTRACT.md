# API-CONTRACT v2

Oxirgi yangilanish: 15.09.2026. **Manba — backend kodi** (`src/Amaliyotchi.Api`, `src/Amaliyotchi.Application`,
`src/Amaliyotchi.Domain`, `src/Amaliyotchi.Infrastructure`). v1 frontend mock'lari asosida yozilgan edi; bu hujjat
esa haqiqiy controller/DTO/validator/handler kodidan olingan — har bir maydon, chegara va status kod kodda bor.
Frontend (`web/dashboard`, `web/twa`, `web/shared`) shu shaklga moslanishi kerak; v1 bilan farqlar §5 da.

Jami **69 ta endpoint**: Auth 5 · Admin 39 · Reports 1 · Tutor 13 · Student (TWA) 10 · Files 1.

---

## 1. Umumiy qoidalar

### 1.1 Base path, transport

- Base path: `/api`. Dev API: `http://localhost:5080`. CORS `Cors:Origins` dan (default `http://localhost:5173`).
- So'rov/javob — JSON (`application/json`). Istisno: `POST /api/student/diary` — `multipart/form-data`;
  `GET /api/files/{id}` — fayl (`Content-Disposition: attachment`, range qo'llanadi).
- Health: `GET /health` (auth'siz) → `{ status, durationMs, checks[] }`.
- ID'lar — GUID string (UUIDv7). Route'da `{id:guid}` — noto'g'ri format → 404 (route mos kelmaydi).

### 1.2 Auth

- **Bearer JWT**: `Authorization: Bearer <accessToken>`. Access token muddati **30 daqiqa** (`Jwt:AccessTokenMinutes`),
  refresh token **14 kun** (`Jwt:RefreshTokenDays`). Ikkalasi ham **body'da** keladi (cookie yo'q), mijoz o'zi saqlaydi.
- JWT claim'lar: `sub` (userId), `name` (FISH), `role` — **PascalCase** (`"Admin"` | `"Tutor"` | `"Student"`),
  `faculty_id` (bo'lsa), `jti`, `iss=amaliyotchi`, `aud=amaliyotchi.clients`. Clock skew 30 s.
- **Refresh oqimi**: `POST /api/auth/refresh { refreshToken }` → yangi `AuthResultDto`; eski refresh token **darhol
  bekor qilinadi (rotatsiya)**, bir marta ishlatiladi. Refresh rad etilsa → **403** (401 emas). Mijoz: 401 kelsa bir
  marta refresh qilib so'rovni qaytaradi (single-flight), refresh 403 bersa sessiyani tozalaydi.
- **Login** (`/api/auth/login`) — faqat admin va tyutor (parol). Talaba hisobi parol bilan kira olmaydi → 403.
- **Telegram** (`/api/auth/telegram`) — faqat talaba. Body `{ initData }` — `window.Telegram.WebApp.initData` xom satri.
  Server `hash` ni `HMAC_SHA256(key=HMAC_SHA256("WebAppData", botToken), data_check_string)` bilan tekshiradi,
  `auth_date` 24 soatdan eski yoki 5 daqiqadan ko'p kelajakda bo'lsa rad. Hisob `user.id` (Telegram) bo'yicha
  `Role=Student` foydalanuvchi topiladi. Imzo/hisob/faollik muammosi → **403** (sabab `detail` da).
- Rate limit (IP bo'yicha, 1 daqiqalik oyna): `login`/`telegram` — **10/min**, `refresh` — **60/min**. Oshsa **429**,
  body bo'sh.
- Token yo'q/yaroqsiz → **401** (JwtBearer challenge, **body bo'sh**, `WWW-Authenticate: Bearer`).
  Rol mos kelmasa → **403, body bo'sh** (authorization middleware). Handler ichidagi 403 (login, refresh) — ProblemDetails.

### 1.3 Roles va policy'lar (`Policies.cs`, `AuthorizationSetup.cs`)

| Policy          | Nomi             | Kim                                      |
| --------------- | ---------------- | ---------------------------------------- |
| `AdminOnly`     | `admin.only`     | `Admin`                                  |
| `TutorOnly`     | `tutor.only`     | `Tutor` (admin ham kira olmaydi)         |
| `StudentOnly`   | `student.only`   | `Student`                                |
| `TutorOrAdmin`  | `tutor.or.admin` | `Admin`, `Tutor` — faqat `/api/reports`  |
| `Authenticated` | `authenticated`  | har qanday rol — `logout`, `me`, `files` |

**Ma'lumot ko'lami** (`ScopeResolver`): admin — hamma; tyutor — faol `TutorAssignment` guruhlaridagi talabalar;
talaba — faqat o'zi (Bearer'dan, query'da `studentId`/`groupId` yo'q). Ko'lamdan tashqaridagi yozuv **404** (403 emas —
mavjudligi oshkor qilinmaydi). Tyutorga guruh biriktirilmagan bo'lsa ro'yxatlar bo'sh, xato emas.

### 1.4 JSON konvensiyalari (`JsonConventions.cs`)

- Property'lar **camelCase** (`PropertyNamingPolicy = CamelCase`).
- **Enum'lar — camelCase string** (`JsonStringEnumConverter(CamelCase)`): `"present"`, `"dayOff"`, `"revisionNeeded"`,
  `"largeRadius"`, `"settingsChanged"`. Kirishda ham string qabul qilinadi (case-insensitive), raqam ham o'tadi, lekin
  string ishlating. Noma'lum string → ASP.NET avtomatik 400 (§1.7).
- `null` maydonlar **chiqariladi** (`"company": null`) — `DefaultIgnoreCondition` yo'q. TS'da `T | null`.
- `Dictionary` kalitlari o'zgartirilmaydi (`DictionaryKeyPolicy = null`) → `errors` kalitlari validator qanday
  bergan bo'lsa shunday (FluentValidation — **PascalCase**: `Text`, `Files`, `DateTo`, `RadiusM`, `Checklist[0]`;
  settings — kalit nomi: `geofenceRadius`).
- `double` maydonlar oddiy son (`87.5`), `int` — butun. `long` (`sizeBytes`) — son.

### 1.5 Sana/vaqt

| C# tip                            | JSON                                                      | Misol                                |
| --------------------------------- | --------------------------------------------------------- | ------------------------------------ |
| `DateOnly`                        | `"YYYY-MM-DD"`                                            | `"2026-10-12"`                       |
| `DateTimeOffset`                  | ISO 8601 **offset bilan** (baza UTC → `+00:00`, `Z` emas) | `"2026-10-11T07:42:00.123456+00:00"` |
| `TimeOnly` (oyna, check-in soati) | `"HH:mm"` string, **Toshkent (+05:00)** vaqti             | `"09:02"`                            |
| Oy (`month` query/response)       | `"YYYY-MM"`                                               | `"2026-10"`                          |

Kirishda `occurredAt` uchun `"Z"` ham, offset ham qabul qilinadi. "Bugun", "ish kuni", oynalar — **Toshkent** bo'yicha
(`PracticeTime.Offset = +05:00`, yozgi vaqt yo'q). `new Date(iso)` ikkala shaklni ham o'qiydi.

### 1.6 Sahifalash (`Paged<T>`, `PagedQuery`)

```ts
// So'rov: ?q=<matn>&page=<1..>&pageSize=<1..100>
// q bo'sh/whitespace → e'tiborsiz (null). page < 1 → 1. pageSize 1..100 dan tashqari → 20 (xato EMAS).
// q > 100 belgi → 400 (errors.Q). Admin ro'yxatlarida validator bor; tutor/today'da validator YO'Q (faqat normalizatsiya).
interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
```

Qidiruv `q` — case-insensitive `LIKE %q%` (`%`, `_`, `\` ekranlanadi), maydonlar har endpoint'da ko'rsatilgan.

### 1.7 Xato shakli (`GlobalExceptionHandler.cs`)

Barcha handler xatolari — RFC 7807 `application/problem+json`:

```ts
interface ProblemDetails {
  status: number; // 400 | 401 | 403 | 404 | 409 | 500
  title: string; // o'zbekcha: "Ma'lumotlar noto'g'ri" | "Avtorizatsiya talab qilinadi" | "Topilmadi" | "Ziddiyat" | "Ruxsat yo'q" | "Noto'g'ri amal" | "Ichki xatolik"
  detail: string; // o'zbekcha xabar (foydalanuvchiga ko'rsatsa bo'ladi)
  traceId: string; // extensions — log'dan topish uchun
  errors?: Record<string, string[]>; // faqat 400 validation (FluentValidation)
  type?: string; // ASP.NET avtomatik 400 da bo'ladi (pastga qarang)
}
```

| Exception (backend)                                 | Status  | `title`                      | Qachon                                                                      |
| --------------------------------------------------- | ------- | ---------------------------- | --------------------------------------------------------------------------- |
| `ValidationException` (FluentValidation quvuri)     | **400** | Ma'lumotlar noto'g'ri        | `errors` dict bilan; `detail` = "Kiritilgan ma'lumotlarda xatolik bor."     |
| `DomainException`                                   | **400** | Noto'g'ri amal               | biznes qoida (oyna yopiq, davr yo'q, sanalar davr tashqarisida…)            |
| `UnauthorizedException`                             | **401** | Avtorizatsiya talab qilinadi | (hozir handler'larda ishlatilmaydi — 401 asosan middleware'dan, body bo'sh) |
| `ForbiddenException`                                | **403** | Ruxsat yo'q                  | login/refresh/telegram rad; `currentUser.UserId` yo'q                       |
| `NotFoundException`                                 | **404** | Topilmadi                    | yozuv yo'q yoki ko'lamdan tashqarida                                        |
| `ConflictException`                                 | **409** | Ziddiyat                     | allaqachon hal qilingan/yuborilgan, radius tashqarisi, kesishuvchi ruxsat   |
| `DbUpdateConcurrencyException`, unique/FK violation | **409** | Ziddiyat                     | —                                                                           |
| boshqa                                              | **500** | Ichki xatolik                | `detail` umumiy, stack yo'q                                                 |
| Rate limit                                          | **429** | —                            | body bo'sh                                                                  |

**ASP.NET avtomatik 400** (`[ApiController]`): JSON sintaksisi buzuq, enum'ga noma'lum string (`?status=foo`,
`"decision":"x"`), `DateOnly`/`Guid` parse xatosi, majburiy `[FromForm]` maydon yo'q → `ValidationProblemDetails`:
`{ type: "https://tools.ietf.org/html/rfc9110#section-15.5.1", title: "One or more validation errors occurred.",
status: 400, errors: { "status": ["..."] | "$.decision": ["..."] }, traceId }`. Mijoz `errors` borligiga qarab
`validation` deb ajratsa ikkalasini ham qamrab oladi.

---

## 2. Endpoint'lar

Yozuv: **Method · Path · Policy**. Body/Query jadvalida: tip · majburiy · chegara (validator'dan).
Response — TypeScript uslubida. Enum qiymatlari §3 da.

### 2.1 Auth — `AuthController` (`/api/auth`)

#### POST `/api/auth/login` · AllowAnonymous · rate `auth` 10/min

| Maydon     | Tip    | Majburiy | Validatsiya                                                                                                            |
| ---------- | ------ | -------- | ---------------------------------------------------------------------------------------------------------------------- |
| `hemisId`  | string | ha       | bo'sh emas, faqat raqamlar, 5–20 xonali (odatda 12 xonali). Noto'g'ri qiymat 403 beradi (mavjudligi oshkor qilinmaydi) |
| `password` | string | ha       | ≥ 8 belgi                                                                                                              |

Response 200 `AuthResultDto`. Xatolar: 400 `errors.HemisId` / `errors.Password`; **403** — HEMIS ID/parol noto'g'ri,
talaba hisobi, parolsiz hisob (`detail`: "HEMIS ID yoki parol noto'g'ri.") yoki hisob faol emas
("Hisobingiz faol emas. Administratorga murojaat qiling."); 429.

```ts
interface AuthResultDto {
  accessToken: string;
  accessTokenExpiresAt: string /*ISO*/;
  refreshToken: string;
  user: UserSummaryDto;
}
interface UserSummaryDto {
  id: string;
  fullName: string;
  role: 'admin' | 'tutor' | 'student'; // camelCase (JWT claim'da "Admin")
  facultyId: string | null; // tyutor — majburiy; admin/talaba null bo'lishi mumkin
  phoneNumber: string | null; // E.164 xom: "+998901234567" (ma'lumot maydoni, login uchun emas; talabada null bo'lishi mumkin)
  groupId: string | null;
  groupName: string | null;
  course: number | null;
  hemisId: string | null; // login identifikatori — admin/tyutor/talaba barchasida bo'lishi mumkin
}
```

**Mock/seed HEMIS ID'lar** (dev): admin `100000000001`; tyutorlar — Nodira Saidova `100000000002`,
Baxtiyor Rasulov `100000000003`, Dilshod Ergashev `100000000004`.

#### POST `/api/auth/telegram` · AllowAnonymous · rate `auth` 10/min

| Maydon     | Tip    | Majburiy | Validatsiya              |
| ---------- | ------ | -------- | ------------------------ |
| `initData` | string | ha       | bo'sh emas, ≤ 8192 belgi |

Response 200 `AuthResultDto` (`user.role = "student"`). Xatolar: 400 `errors.InitData`; **403** — imzo/`auth_date`
("Telegram imzosi tasdiqlanmadi. Ilovani qaytadan oching."), hisob bog'lanmagan ("Hisob topilmadi — tyutoringizdan
taklif havolasini oling."), faol emas ("Hisobingiz faol emas. Tyutoringizga murojaat qiling."); 429.
Har muvaffaqiyatsiz urinish audit'ga `loginFailed` sifatida yoziladi.

#### POST `/api/auth/refresh` · AllowAnonymous · rate `refresh` 60/min

Body `{ refreshToken: string }`. Response 200 `AuthResultDto` — **yangi** refresh token (eskisi `Revoke`, qayta
ishlatib bo'lmaydi). Xatolar: **403** — token yo'q/bekor qilingan/muddati o'tgan ("Sessiya muddati tugagan. Qaytadan
kiring.") yoki hisob faol emas; 429.

#### POST `/api/auth/logout` · Authenticated

Body `{ refreshToken: string }` + Bearer. Response **204** har doim (token topilmasa ham). Faqat o'z tokenini bekor qiladi.

#### GET `/api/auth/me` · Authenticated

Response 200 `UserSummaryDto`. 401 (token yo'q), 404 (foydalanuvchi o'chirilgan).

---

### 2.2 Files — `FilesController`

#### GET `/api/files/{id}` · Authenticated

Response 200 — fayl oqimi, `Content-Type` bazadan, `Content-Disposition: attachment; filename="<asl nom>"`,
`Accept-Ranges`. **404** — fayl yo'q, diskda yo'q yoki ko'lamdan tashqarida. Ko'lam: shablon (`kind=template`) — hamma
autentifikatsiyalangan; admin — hammasi; talaba — o'zi yuklagan yoki o'z arizasi/kundaligi/ruxsatiga biriktirilgan;
tyutor — ko'lamdagi talabalar fayllari.

> DTO'lardagi barcha `url`/`templateUrl` qiymatlari **nisbiy**: `"/api/files/<guid>"`. Bearer kerak — oddiy
> `<a href>` ishlamaydi, `fetch` + `Authorization` + blob URL orqali oching.

---

### 2.3 Admin — `Controllers/Admin/*` · hammasi `AdminOnly`

Ro'yxatlar `?q&page&pageSize` (§1.6) → `Paged<T>`. 400 — `q` > 100 belgi. 403 — admin emas.

#### GET `/api/admin/dashboard`

Response 200 `AdminDashboardDto`. Bo'sh bazada ham 200 (0 / `[]`).

```ts
interface AdminDashboardDto {
  date: string; // bugun (Toshkent), DateOnly
  stats: DashboardStatsDto; // XOM raqamlar — matn/foiz formatini frontend yasaydi
  faculties: FacultyAttendanceDto[]; // nom bo'yicha
  tutors: TutorActivityDto[]; // late → pendingCount desc → name
  audit: AuditEntryDto[]; // so'nggi 8 ta
}
interface DashboardStatsDto {
  studentsTotal: number;
  studentsLinked: number;
  studentsUnlinked: number; // Telegram bog'langan/bog'lanmagan
  faculties: number;
  groups: number;
  companiesActive: number;
  applicationsPending: number; // status = submitted
  applicationsOverdue: number; // submitted va 48 soatdan ko'p javobsiz
  contractsApproved: number; // approved + completed
  contractsRevision: number;
  contractsRejected: number;
  contractsMissing: number; // faol davr guruhidagi talaba, arizasi umuman yo'q
  expectedToday: number; // bugun ish kuni bo'lgan faol davr guruhlaridagi talabalar
  presentToday: number;
  lateToday: number;
  absentToday: number;
  excusedToday: number;
  noDiaryToday: number; // max(0, (present+late) − bugungi kundaliklar)
  attendanceTodayPct: number; // int 0..100 = (present+late)/expectedToday
  attendanceYesterdayPct: number;
}
interface FacultyAttendanceDto {
  id: string;
  name: string;
  code: string;
  studentCount: number;
  expectedToday: number;
  attendedToday: number;
  attendancePct: number;
}
interface TutorActivityDto {
  id: string;
  name: string;
  facultyCode: string | null;
  groups: string[];
  studentCount: number;
  pendingCount: number;
  oldestPendingAt: string | null /*ISO*/;
  avgDecisionHours: number | null /*1 kasr, so'nggi 60 kun*/;
  lastActiveAt: string | null /*ISO: max(lastLoginAt, oxirgi audit)*/;
  status: TutorStatus;
}
```

#### GET `/api/admin/faculties` — `q`: nom, kod

```ts
interface FacultyRow {
  id: string;
  name: string;
  code: string;
  directions: number;
  groups: number;
  students: number;
  tutors: number;
  attendancePct: number /*bugungi, int*/;
  status: FacultyStatus /*expectedToday>0 && pct<70 → attention*/;
  isActive: boolean;
}
```

#### POST `/api/admin/faculties` · 201

| Maydon | Tip    | Majburiy | Validatsiya                                                                           |
| ------ | ------ | -------- | ------------------------------------------------------------------------------------- |
| `name` | string | ha       | trim 2–150 belgi                                                                      |
| `code` | string | ha       | trim 2–10 ta lotin harf/raqam (`^[A-Za-z0-9]+$`), katalogda unikal (case-insensitive) |

Response 201 `FacultyDto`. Xatolar: 400 `errors.Name`/`errors.Code`; **409** — kod takror (`detail`: "'{CODE}' kodli
fakultet allaqachon mavjud.").

```ts
interface FacultyDto {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}
```

#### PUT `/api/admin/faculties/{id}` · 200

Body — POST bilan bir xil (`name`, `code`). Response 200 `FacultyDto`. Xatolar: 400 `errors.Name`/`errors.Code`;
**404** (`detail`: "Fakultet topilmadi."); **409** kod boshqa fakultetda band (yuqoridagi kabi).

#### PATCH `/api/admin/faculties/{id}/status` · 200

Body `{ isActive: boolean }`. Response 200 `FacultyDto`. Xatolar: **404**.

#### DELETE `/api/admin/faculties/{id}` · 204

Xatolar: **404**; **409** — fakultetga guruh/tyutor/talaba biriktirilgan (`detail`: "Fakultetga guruhlar, tyutorlar
yoki talabalar biriktirilgan — avval ularni boshqa fakultetga ko'chiring.").

#### GET `/api/admin/faculties/{id}` · 200

Response `FacultyDto` (breadcrumb uchun — `FacultiesPage` ichidagi ierarxiya sahifalari). **404** (`detail`:
"Fakultet topilmadi.").

---

### 2.3.1 Fakultet ierarxiyasi — Kafedra → Yo'nalish → Guruh

> ⚠️ **Status:** frontend shu kontraktga (`scratchpad/hierarchy-contract.md`, P52) qarab qurilgan; backend
> tomoni parallel agent tomonidan amalga oshirilmoqda — bu bo'lim hali `src/Amaliyotchi.*` kodida to'liq
> tasdiqlanmagan bo'lishi mumkin. Barcha endpoint'lar `AdminOnly`. O'chirish qoidasi — har darajada bir xil:
> o'chirilmagan bolasi bo'lsa **409**, aks holda soft-delete **204**. Kod takrori — ota ichida, case-insensitive,
> faol yozuvlar orasida → **409**.

```ts
interface DepartmentRow {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  directions: number;
  groups: number;
  students: number;
}
interface DepartmentDto {
  id: string;
  facultyId: string;
  facultyName: string;
  name: string;
  code: string;
  isActive: boolean;
}
interface DirectionRow {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
  groups: number;
  students: number;
}
interface DirectionDto {
  id: string;
  departmentId: string;
  departmentName: string;
  facultyId: string;
  facultyName: string;
  name: string;
  code: string;
  isActive: boolean;
}
interface GroupDto {
  id: string;
  directionId: string;
  name: string;
  course: number;
  isActive: boolean;
  academicYear: string; // faol `AcademicYear` — "2026-2027"
}
```

#### GET/POST `/api/admin/faculties/{facultyId}/departments` — `q`: nom yoki kod

GET → `Paged<DepartmentRow>`. POST body `{ name, code }` (validatsiya — pastda) → 201 `DepartmentDto`. 400
`errors.Name`/`errors.Code`; 404 (`facultyId` topilmasa, `detail`: "Fakultet topilmadi."); 409 — kod takror
(`detail`: "'{CODE}' kodli kafedra bu fakultetda allaqachon mavjud.").

#### GET/PUT `/api/admin/departments/{id}` · PATCH `.../status` · DELETE

GET/PUT → `DepartmentDto` (PUT body — POST bilan bir xil). PATCH body `{ isActive }` → 200 `DepartmentDto`. DELETE
→ 204. Xatolar: 404 (`detail`: "Kafedra topilmadi."); PUT 409 — kod takror (yuqoridagi kabi); DELETE 409 — kafedrada
o'chirilmagan yo'nalish bor (`detail`: "Kafedrada yo'nalishlar bor — avval ularni o'chiring.").

#### GET/POST `/api/admin/departments/{departmentId}/directions` — `q`: nom yoki kod

GET → `Paged<DirectionRow>`. POST body `{ name, code }` → 201 `DirectionDto`. 400 `errors.Name`/`errors.Code`; 404
(`departmentId` topilmasa, `detail`: "Kafedra topilmadi."); 409 — kod takror (`detail`: "'{CODE}' kodli yo'nalish bu
kafedrada allaqachon mavjud.").

#### GET/PUT `/api/admin/directions/{id}` · PATCH `.../status` · DELETE

GET/PUT → `DirectionDto`. PATCH `{ isActive }` → 200 `DirectionDto`. DELETE → 204. Xatolar: 404 (`detail`:
"Yo'nalish topilmadi."); PUT 409 — kod takror; DELETE 409 — yo'nalishda o'chirilmagan guruh bor (`detail`:
"Yo'nalishda guruhlar bor — avval ularni o'chiring.").

#### GET/POST `/api/admin/directions/{directionId}/groups` — `q`: guruh nomi, tyutor ismi

GET → `Paged<GroupRow>` (pastdagi §2.3.2 shakli bilan bir xil, endi `+isActive`). POST body `{ name, course }` → 201
`GroupDto`. Validatsiya: `name` trim 2–20 `^[A-Za-z0-9-]+$` ("412-22" uslubida); `course` int 1–6. 400
`errors.Name`/`errors.Course`. 404 (`directionId` topilmasa, `detail`: "Yo'nalish topilmadi."). 409 — nom takror
(`detail`: "'{NAME}' guruhi bu yo'nalishda allaqachon mavjud.") **yoki** faol `AcademicYear` yo'q (`detail`: "Faol
o'quv yili yo'q — avval o'quv yilini faollashtiring."). `academicYear` body'da yuborilmaydi — faol o'quv yildan
olinadi. Yo'nalishni qamrab oluvchi faol tyutor ko'lami (fakultet/kafedra/yo'nalish, §2.3.3) bo'lsa yangi guruh o'sha
tyutorga avtomatik biriktiriladi.

#### GET/PUT `/api/admin/groups/{id}` · PATCH `.../status` · DELETE

GET/PUT → `GroupDto` (PUT body — POST bilan bir xil, `{ name, course }`). PATCH `{ isActive }` → 200 `GroupDto`.
DELETE → 204. Xatolar: 404 (`detail`: "Guruh topilmadi."); PUT 409 — nom takror; DELETE 409 — guruhda o'chirilmagan
talaba (`StudentProfile.StudentGroupId`) yoki aynan shu **guruh darajasidagi** faol tyutor ko'lami bor (`detail`: "Guruhda
talabalar yoki biriktirilgan tyutor bor — avval ularni ko'chiring."). Ota ko'lamdan (fakultet/kafedra/yo'nalish) kelib
chiqqan biriktiruv esa guruh bilan birga faolsizlantiriladi — 204.

Frontend marshrutlari: `/admin/faculties/:facultyId` (kafedralar) → `.../departments/:departmentId`
(yo'nalishlar) → `.../directions/:directionId` (guruhlar). Breadcrumb — har sahifa faqat o'z darajasining
`GET .../{id}` chaqiradi (ota nomlari DTO ichida keladi: `facultyName`, `departmentName`).

---

### 2.3.2 Global guruhlar ro'yxati

#### GET `/api/admin/groups` — `q`: guruh nomi, yo'nalish, fakultet nomi/kodi, tyutor ismi

> ℹ️ **Frontend'da ishlatilmaydi** (P52) — sidebar'dagi global "Guruhlar" sahifasi olib tashlandi, guruhlar endi
> faqat ierarxiya ichida (`.../directions/{id}/groups`, §2.3.1) ko'rinadi. Endpoint backendda boshqa
> integratsiyalar (masalan hisobotlar) uchun qoladi.

```ts
interface GroupRow {
  id: string;
  code: string /*guruh nomi "412-22"*/;
  course: number;
  direction: string;
  faculty: string;
  facultyCode: string;
  tutorId: string | null;
  tutor: string | null /*to'liq FISH*/;
  students: number;
  attendancePct: number /*int; faol davr boshidan kechagacha, maxraj = o'tgan ish kunlari × arizasi tasdiqlangan talabalar*/;
  period: {
    id: string;
    name: string;
    status: PracticePeriodStatus;
    startDate: string;
    endDate: string;
  } | null;
  isActive: boolean; // P52 qo'shildi — CRUD hierarchy
}
```

### 2.3.3 Tyutorlar — `AdminTutorsController` (`/api/admin/tutors`)

Hammasi `AdminOnly`. O'chirish (DELETE) endpoint'i **yo'q** — tyutor faqat faol emas qilinadi (`PATCH .../status`).
Semantika: tyutor **ierarxik ko'lam** bilan biriktiriladi (`TutorScope`) — fakultet, kafedra, yo'nalish yoki guruh
darajasida, bir tyutorda bir nechta ko'lam bo'lishi mumkin (masalan 2 ta kafedra + 1 ta guruh). Ko'lam guruhlarga
**materializatsiya** qilinadi (`TutorAssignment` — `groups`): fakultet → fakultetdagi barcha faol guruhlar, kafedra →
undagi barcha yo'nalish/guruhlar, yo'nalish → undagi guruhlar, guruh → o'zi. Ko'lam ichida **keyin yaratilgan** guruh
avtomatik qamrab olinadi. **Kesishmaslik qoidasi:** ikki xil tyutorning faol ko'lamlari kesishmaydi (teng, ota yoki
bola — masalan A fakultetga, B shu fakultetdagi guruhga bo'lolmaydi) → 409. Bir tyutorning o'z tanlovlarida ota
tanlangan bo'lsa bolalari jimgina tashlab yuboriladi. Tarix saqlanadi (ajratilganda ko'lam ham, biriktiruv ham
o'chirilmaydi, faolsizlantiriladi).

#### GET `/api/admin/tutors` — `q`: ism, telefon, fakultet kodi/nomi; `&facultyId=<guid>` (ixtiyoriy filtr)

```ts
interface TutorRow {
  id: string;
  fullName: string;
  phone: string | null /*E.164 xom*/;
  facultyId: string | null;
  facultyCode: string | null;
  facultyName: string | null;
  groups: string[]; /*faol biriktiruvlar — guruh nomlari*/
  students: number;
  pending: number;
  oldestPendingAt: string | null;
  avgDecisionHours: number | null;
  lastActiveAt: string | null;
  isActive: boolean;
  status: TutorStatus; /*eng eski pending > 48h → late*/
}
```

#### GET `/api/admin/tutors/{id}` · 200

Response `TutorDetail`. **404** (`detail`: "Tyutor topilmadi.") — yo'q, o'chirilgan yoki roli `Tutor` emas.

```ts
interface TutorDetail {
  id: string;
  fullName: string;
  hemisId: string; /*login identifikatori*/
  phone: string | null /*E.164*/;
  facultyId: string;
  facultyCode: string;
  facultyName: string;
  isActive: boolean;
  lastLoginAt: string | null /*ISO*/;
  createdAt: string /*ISO*/;
  scopes: TutorScopeDto[]; /*admin tanlagan FAOL ko'lamlar; daraja → nom bo'yicha*/
  groups: TutorGroupDto[]; /*ko'lamlardan materializatsiya qilingan FAOL biriktiruvlar; yo'nalish → guruh nomi bo'yicha*/
}
type TutorScopeLevel = 'faculty' | 'department' | 'direction' | 'group';
interface TutorScopeDto {
  id: string; /*TutorScope.Id*/
  level: TutorScopeLevel;
  facultyId: string; /*doim — tyutor fakulteti*/
  departmentId: string | null; /*department/direction/group darajasida*/
  directionId: string | null; /*direction/group darajasida*/
  groupId: string | null; /*faqat group darajasida*/
  name: string; /*tanlangan tugun nomi (fakultet/kafedra/yo'nalish nomi yoki guruh nomi)*/
  path: string; /*ota tugunlar " › " bilan, tugunning o'zisiz: "Axborot texnologiyalari › Umumiy kafedra"; fakultetda ""*/
  groups: number; /*qamrab olingan faol guruhlar soni*/
  students: number; /*shu guruhlardagi talabalar*/
}
interface TutorGroupDto {
  assignmentId: string; /*TutorAssignment.Id*/
  groupId: string;
  groupName: string;
  course: number;
  directionName: string;
  students: number; /*guruhdagi talabalar (StudentProfile) soni*/
  academicYearName: string; /*biriktiruv qilingan o'quv yili, "2026-2027"*/
  isActive: boolean; /*GURUHNING o'zi faolmi (biriktiruv doimo faol — aks holda ro'yxatga tushmaydi)*/
}
```

#### POST `/api/admin/tutors` · 201 (+ `Location: /api/admin/tutors/{id}`)

| Maydon      | Tip            | Majburiy | Validatsiya                                                                 |
| ----------- | -------------- | -------- | --------------------------------------------------------------------------- |
| `fullName`  | string         | ha       | trim 2–150 belgi                                                            |
| `hemisId`   | string         | ha       | trim, 5–20 ta raqam (`HemisId` VO); unikal — **o'chirilgan hisoblar ham**   |
| `phone`     | string \| null | yo'q     | bo'sh → null; aks holda O'zbekiston raqami (`+998901234567`, `901234567`, bo'shliqli variantlar → E.164 ga normallashadi); faol hisoblar orasida unikal |
| `password`  | string         | ha       | 8–128 belgi                                                                 |
| `facultyId` | guid           | ha       | mavjud va faol fakultet                                                     |

Response 201 `TutorDetail` (`scopes: []`, `groups: []`). Xatolar: 400 `errors.FullName`/`errors.HemisId`/`errors.Phone`/
`errors.Password`/`errors.FacultyId`; **404** (`detail`: "Fakultet topilmadi."); **409** — HEMIS ID band (`detail`: "Bu
HEMIS ID bilan foydalanuvchi mavjud."), telefon band (`detail`: "Bu telefon raqami bilan foydalanuvchi mavjud."),
fakultet faol emas (`detail`: "Fakultet faol emas."). Yangi tyutor darhol `POST /api/auth/login` (hemisId + password)
bilan kira oladi.

#### PUT `/api/admin/tutors/{id}` · 200

Body `{ fullName, phone?, facultyId }` (validatsiya — POST bilan bir xil; `hemisId` va parol bu yerdan
o'zgartirilmaydi). Response 200 `TutorDetail`. Xatolar: 400; **404** (tyutor yoki yangi fakultet topilmadi); **409** —
fakultet o'zgartirilmoqda-yu tyutorda faol ko'lamlar bor (`detail`: "Tyutorga ko'lam biriktirilgan — avval uni
ajrating."), yangi fakultet faol emas, telefon band. Fakultet o'zgarmasa ko'lam/biriktiruvlarga tegilmaydi.

#### PATCH `/api/admin/tutors/{id}/status` · 204

Body `{ isActive: boolean }`. `false` → hisob yopiladi **va barcha refresh tokenlari bekor qilinadi** (refresh → 403,
login → 403 "Hisobingiz faol emas…"); `true` → qayta ochiladi. Ko'lam/biriktiruvlarga tegilmaydi (faol emas tyutorning
`scopes`/`groups` ro'yxati saqlanadi). Xatolar: **404**. Holat o'zgarmasa (allaqachon shunday) ham 204.

#### POST `/api/admin/tutors/{id}/password` · 204

Body `{ password: string }` (8–128 belgi, `errors.Password`). Yangi parol o'rnatiladi, tyutorning **barcha refresh
tokenlari bekor qilinadi** — eski sessiya refresh qila olmaydi (403), eski parol bilan login 403, yangisi bilan 200.
Xatolar: 400; **404**.

#### PUT `/api/admin/tutors/{id}/scopes` · 200

Body `{ scopes: { level: TutorScopeLevel; id: string }[] }` (takrorlar e'tiborsiz; `[]` → hammasi ajratiladi; `id` —
shu darajadagi tugun: fakultet/kafedra/yo'nalish/guruh id'si; `faculty` darajasida `id` tyutorning o'z fakulteti
bo'lishi shart). Faol ko'lamlar to'plamini **almashtiradi**: ro'yxatda bo'lmaganlar faolsizlantiriladi (yozuv qoladi),
ilgari ajratilgan tugun qaytsa — o'sha yozuv qayta faollashadi (`scopes[].id` o'zgarmaydi), yangilari yaratiladi. So'ng
guruh biriktiruvlari sinxronlanadi (`groups`): yangilari **faol `AcademicYear`** bilan yaratiladi, qaytganlari qayta
faollashadi (`assignmentId` o'zgarmaydi), ortiqchalari faolsizlantiriladi. Ota tanlangan bo'lsa bolalari jimgina
tashlanadi (masalan `[direction X, group X.1]` → faqat `direction X`). Tyutor faol bo'lmasa ham ruxsat. Response 200
`TutorDetail`.

Xatolar (birinchi uchragani): **404** tyutor (`detail`: "Tyutor topilmadi."); **400** `errors.Scopes[i].Id` (bo'sh guid) /
`errors.Scopes[i].Level` yoki `DomainException` — tugun topilmadi (`detail`: "{Daraja} topilmadi." — daraja: `Fakultet`/
`Kafedra`/`Yo'nalish`/`Guruh`; o'chirilgan tugun ham "topilmadi"), faol emas (`detail`: "{Daraja} faol emas: {nom}"),
tyutor fakultetiga tegishli emas (`detail`: "{Daraja} tyutor fakultetiga tegishli emas: {nom}"; zanjir
Group → Direction → Department → Faculty); **409** — boshqa tyutorning faol ko'lami bilan kesishadi (`detail`:
"{nom} ({daraja}) {tyutor FISH} tyutoriga biriktirilgan." — `nom`/`daraja` kesishgan **boshqa** tyutor ko'lamining nomi
va darajasi kichik harf bilan: `fakultet`/`kafedra`/`yo'nalish`/`guruh`, masalan "412-22 (guruh) Nodira Saidova tyutoriga
biriktirilgan."), yangi biriktiruv kerak-u faol o'quv yili yo'q (`detail`: "Faol o'quv yili yo'q."; faqat ajratish bo'lsa
o'quv yili talab qilinmaydi). Xato bo'lsa hech narsa yozilmaydi.

#### GET `/api/admin/tutors/{id}/scope-tree` · 200

Response `TutorScopeTree` — tyutor fakultetining daraxti, faqat **faol** kafedra/yo'nalish/guruhlar, nom bo'yicha
tartib (guruh: kurs, nom). **404** tyutor topilmasa.

```ts
interface TutorScopeTree {
  id: string; /*fakultet*/
  name: string;
  code: string;
  tutorId: string | null; /*AYNAN shu tugunda faol ko'lami bor tyutor — so'ralayotgan tyutorning o'zi ham; yo'q bo'lsa null*/
  tutorName: string | null;
  departments: {
    id: string; name: string; tutorId: string | null; tutorName: string | null;
    directions: {
      id: string; name: string; tutorId: string | null; tutorName: string | null;
      groups: { id: string; name: string; course: number; students: number; tutorId: string | null; tutorName: string | null }[];
    }[];
  }[];
}
```

Biriktirish oynasi: `tutorId` faqat ko'lam **tanlangan tugunda** to'ldiriladi (ota ko'lam bolalarga "yoyilmaydi" —
fakultet darajasida tanlangan bo'lsa `departments[].tutorId` null). `tutorId === id` → shu tyutorniki (belgilangan);
boshqa → band; boshqa tyutorning tuguni ostidagi yoki ustidagi tugunni tanlash `PUT .../scopes` da 409 beradi — avval
egasidan ajratish kerak.

#### GET `/api/admin/students` — `q`: FISH, HEMIS ID, telefon, guruh

```ts
interface StudentRow {
  id: string /*User.Id — tyutor endpoint'laridagi studentId bilan bir xil*/;
  fullName: string;
  hemisId: string;
  groupId: string;
  group: string;
  course: number;
  faculty: string;
  company: string | null /*eng so'nggi tasdiqlangan ariza*/;
  attendancePct: number /*int*/;
  suspiciousDays: number;
  telegramLinked: boolean;
  status: AdminStudentStatus; /*!telegramLinked → unlinked; suspiciousDays≥2 || (elapsed>0 && pct<70) → flagged; aks holda active*/
}
```

#### GET `/api/admin/companies` — `q`: nom, STIR, manzil

```ts
interface CompanyRow {
  id: string;
  name: string;
  tin: string /*9 raqam xom "304512889"*/;
  activity: string;
  address: string;
  radiusM: number;
  students: number /*arizasi approved*/;
  suspiciousDays: number;
  isActive: boolean;
  flag: CompanyFlag | null; /*suspiciousDays≥3 → suspicious; radiusM>500 → largeRadius; aks holda null*/
}
```

#### GET `/api/admin/audit` — `q`: entityName, entityId, reason, foydalanuvchi ismi; `&action=<AuditAction>`

`action` — enum string (`settingsChanged`); noma'lum → 400. Yangisi birinchi.

```ts
interface AuditEntryDto {
  id: string;
  at: string /*ISO*/;
  action: AuditAction;
  entityName: string /*"User","AppSetting","PracticeApplication"…*/;
  entityId: string | null;
  reason: string | null;
  changes: string | null /*JSON matn: {"geofenceRadius":{"old":"200","new":"250"}}*/;
  userId: string | null;
  userName: string | null;
  userRole: 'admin' | 'tutor' | 'student' | null;
}
```

#### GET `/api/admin/settings` · PUT `/api/admin/settings`

PUT body: `{ values: Record<string, string> }` — faqat o'zgargan kalitlar, **xom** qiymat (`"250"`, `"true"`,
`"1,2,3,4,5"`). Validatsiya (400): `values` bo'sh → `errors.Values`; noma'lum kalit → `errors.<key>`; qiymat bo'sh /
int emas / `min..max` tashqarisida / bool emas / ish kunlari 1–7 emas → `errors.<key>` (`SettingDefinition.Validate`).
Bool qabul: `true|1|yes|ha` / `false|0|no|yo'q` → `"true"`/`"false"` ga normalizatsiya. Haqiqiy o'zgarish audit'ga
`settingsChanged`. Ikkalasi ham 200 `AdminSettingsDto` (PUT — yangilangan holat).

```ts
interface AdminSettingsDto {
  settings: SettingDto[];
  holidays: HolidayDto[];
  templates: DocTemplateDto[];
}
interface SettingDto {
  key: SettingKey;
  label: string;
  value: string /*xom*/;
  type: 'int' | 'bool' | 'weekdays';
  unit: 'm' | 'min' | 'chars' | null;
  note: string;
  min: number | null;
  max: number | null /*faqat int*/;
  updatedAt: string | null;
}
interface HolidayDto {
  id: string;
  date: string /*DateOnly*/;
  name: string;
  isRecurring: boolean;
}
interface DocTemplateDto {
  id: string;
  name: string;
  kind: 'contract' | 'referral' | 'reference';
  fileName: string;
  url: string; /*"/api/files/<id>"*/
}
```

Sozlamalar (kalit · tur · birlik · default · min–max): `geofenceRadius` int m `200` 50–1000 · `lateTolerance` int min `15` 0–120 ·
`minGpsAccuracy` int m `100` 10–1000 · `autoCheckout` int min `60` 0–360 · `workDays` weekdays `1,2,3,4,5,6` ·
`dailyReportRequired` bool `true` · `minReportLength` int chars `150` 0–5000 · `checkInWindow` int min `90` 15–480.
Bazada yo'q kalit default bilan qaytadi (`updatedAt: null`).

---

### 2.4 Reports — `ReportsController` · `TutorOrAdmin`

#### GET `/api/reports`

```ts
interface ReportsCatalog {
  filter: ReportFilter;
  reports: ReportCard[];
}
interface ReportFilter {
  dateFrom: string | null;
  dateTo: string | null /*faol davrlar min/max; yo'q → null*/;
  scope: string /*admin: "Barcha fakultetlar"; tyutor: "412-22, 413-22"*/;
  groups: string[] /*admin: []*/;
  studentCount: number;
}
interface ReportCard {
  id: 'attendance' | 'portfolio' | 'company-reference' | 'diaries';
  name: string;
  formats: ('pdf' | 'xlsx')[];
  desc: string;
  available: boolean /*hozir hammasi false*/;
  note: string | null; /*"Fayl generatsiyasi keyingi bosqichda (M14) qo'shiladi."*/
}
```

`GET /api/reports/{id}/download` — **yo'q** (M14).

---

### 2.5 Tutor — `Controllers/Tutor/*` · hammasi `TutorOnly`

Ko'lam — biriktirilgan guruhlar. Hamma ro'yxat (today'dan tashqari) sahifalanmagan massiv.

#### GET `/api/tutor/today` — `?status=&q=&page=&pageSize=`

| Query              | Tip                                                   | Izoh                                                                                      |
| ------------------ | ----------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `status`           | `present\|late\|absent\|excused\|pending\|suspicious` | ixtiyoriy; `suspicious` = `suspicious \|\| outOfRadius` bayrog'i bo'yicha; noma'lum → 400 |
| `q`                | string                                                | ism bo'yicha `includes` (xotirada)                                                        |
| `page`, `pageSize` | int                                                   | §1.6; validator yo'q                                                                      |

```ts
interface TodayResponse {
  date: string;
  stats: TodayStats;
  alerts: TodayAlert[];
  rows: Paged<AttendanceRow>;
}
interface TodayStats {
  present: number;
  late: number;
  absent: number;
  excused: number;
  pending: number;
  diaries: number /*written*/;
  total: number;
} // filtr QO'LLANMAGAN barcha qatorlar bo'yicha
interface TodayAlert {
  kind: 'outOfRadius' | 'notCheckedIn' | 'newLeaveRequests' | 'newApplications';
  count: number;
  href: string /*"/tutor/map" | "/tutor?status=absent" | "/tutor/leave-requests" | "/tutor/applications"*/;
  maxDistanceM: number | null; /*faqat outOfRadius*/
}
interface AttendanceRow {
  studentId: string;
  name: string;
  group: string;
  company: string | null;
  checkIn: string | null /*"09:02"*/;
  checkOut: string | null;
  diary: 'written' | 'pending' | null;
  distanceM: number | null /*check-in masofasi yoki rad etilgan urinishning maksimal masofasi*/;
  outOfRadius: boolean;
  status: AttendanceStatus /*pending|present|late|absent|excused|dayOff*/;
  suspicious: boolean;
  manual: boolean;
  autoClosed: boolean;
}
```

`status` hisobi (qator bazada yo'q bo'lsa): davr yo'q/ish kuni emas → `dayOff`; tasdiqlangan ruxsat → `excused`;
oyna (10:30) yopilgan → `absent`, aks holda `pending`. `diary`: bugun yozuv bor → `written`; ish kuni va status
present/late/pending → `pending`; aks holda `null`.

#### GET `/api/tutor/applications` — `?status=<ApplicationStatus>`

`status` ixtiyoriy (`submitted|revisionNeeded|approved|rejected`; texnik jihatdan `draft`/`completed` ham o'tadi).
Tartib: `submitted` birinchi (eng eski `submittedAt` tepada), keyin `decidedAt` desc.

```ts
interface ApplicationListResponse {
  counts: { submitted: number; revisionNeeded: number; approved: number; rejected: number };
  items: ApplicationSummary[];
}
interface ApplicationSummary {
  id: string;
  studentId: string;
  name: string;
  group: string;
  course: number;
  hemisId: string;
  company: string;
  status: ApplicationStatus;
  submittedAt: string /*ISO — "2 soat oldin" ni frontend hisoblaydi*/;
  decidedAt: string | null;
}
```

#### GET `/api/tutor/applications/{id}` · 404

```ts
interface ApplicationDetail extends ApplicationSummary {
  coords: { lat: number; lng: number }; // korxona
  radiusM: number; // proposedRadiusM (tasdiqlangach tyutor qiymati)
  companyDetails: {
    name: string;
    tin: string;
    activity: string;
    address: string;
    supervisorName: string;
    supervisorPhone: string;
    mentorName: string | null;
    mentorPhone: string | null;
  };
  contract: { name: string; pages: number | null; sizeBytes: number; url: string } | null;
  comment: string | null; // qaror izohi
  checklist: number[]; // belgilangan punkt indekslari 0..6
  revisionCount: number;
}
```

#### POST `/api/tutor/applications/{id}/decision`

| Maydon      | Tip                                 | Majburiy                    | Validatsiya                                                         |
| ----------- | ----------------------------------- | --------------------------- | ------------------------------------------------------------------- |
| `decision`  | `'approve' \| 'return' \| 'reject'` | ha                          | enum                                                                |
| `radiusM`   | int                                 | `approve` da **ha**         | 50–1000, 50 qadam (`errors.RadiusM`)                                |
| `checklist` | int[]                               | yo'q                        | har element 0..6 (`errors["Checklist[i]"]`); `approve` da saqlanadi |
| `comment`   | string                              | `return`/`reject` da **ha** | ≤ 1000 belgi (`errors.Comment`)                                     |

Response 200 `{ id: string; status: ApplicationStatus }` — `approve→approved`, `return→revisionNeeded`,
`reject→rejected`. `approve`: radius korxonaga yoziladi (o'zgarsa audit `radiusChanged`).
Xatolar: 400 validation; **404** ko'lamdan tashqari; **409** status `submitted` emas ("Ariza allaqachon hal qilingan…").

#### GET `/api/tutor/students`

```ts
interface TutorStudent {
  id: string;
  name: string;
  hemisId: string;
  group: string;
  company: string | null;
  attendancePct: number /*1 kasr*/;
  attendedDays: number;
  totalDays: number;
  diaryCount: number;
  diaryAvg: number /*1 kasr, baholanganlar bo'yicha*/;
  state: 'active' | 'redFlag' | 'suspicious';
  suspiciousCount: number;
}
```

`state`: `totalDays>0 && pct<70` → `redFlag`; `suspiciousCount≥1` → `suspicious`; aks holda `active`. FISH bo'yicha tartib.

#### GET `/api/tutor/diaries` — `?status=<DiaryStatus>`

Tartib: `submitted`/`seen` birinchi, keyin `submittedAt` desc.

```ts
interface TutorDiaryEntry {
  id: string;
  studentId: string;
  studentName: string;
  group: string;
  date: string /*DateOnly*/;
  submittedAt: string;
  status: DiaryStatus;
  text: string;
  learned: string | null;
  files: { name: string; url: string }[];
  score: number | null /*1..5*/;
  comment: string | null;
  reviewedAt: string | null;
}
```

#### POST `/api/tutor/diaries/{id}/review`

| Maydon    | Tip                                 | Majburiy            | Validatsiya                                                                          |
| --------- | ----------------------------------- | ------------------- | ------------------------------------------------------------------------------------ |
| `action`  | `'approve' \| 'score' \| 'rewrite'` | ha                  | enum                                                                                 |
| `score`   | int                                 | `score` da **ha**   | 1–5 (`errors.Score`); `approve` da ixtiyoriy; `rewrite` da e'tiborsiz (null bo'ladi) |
| `comment` | string                              | `rewrite` da **ha** | ≤ 1000 (`errors.Comment`)                                                            |

Response 200 `TutorDiaryEntry` (yangilangan; `approve`/`score` → `approved`, `rewrite` → `rewrite`).
Xatolar: 400; **404**; **409** — status `submitted`/`seen` emas ("Hisobot allaqachon ko'rib chiqilgan…").

#### GET `/api/tutor/calendar` — `?month=YYYY-MM` (berilmasa joriy oy)

400 — `month` formati (`errors.Month`).

```ts
interface CalendarResponse {
  month: string;
  days: number[] /*1..N*/;
  rows: { studentId: string; name: string; days: CalendarDayStatus[] /*uzunligi N*/ }[];
}
```

#### GET `/api/tutor/map` — `?date=YYYY-MM-DD` (berilmasa bugun)

Har talaba uchun shu kundagi **oxirgi check-in urinishi** (qabul yoki rad). Urinish yo'q → nuqta yo'q.

```ts
interface MapResponse {
  date: string;
  points: MapPoint[];
}
interface MapPoint {
  studentId: string;
  name: string;
  company: string;
  distanceM: number /*butun*/;
  radiusM: number;
  rejected: boolean;
  time: string /*"09:02" — server qabul qilgan vaqt*/;
  kind: 'ok' | 'late' | 'bad' /*rad → bad; late → late*/;
  lat: number;
  lng: number;
}
```

#### GET `/api/tutor/leave-requests` — `?status=<LeaveRequestStatus>`

Tartib: `pending` birinchi, keyin `createdAt` desc.

```ts
interface TutorLeaveRequest {
  id: string;
  studentId: string;
  studentName: string;
  group: string;
  dateFrom: string;
  dateTo: string /*bir kunlik → dateFrom bilan teng, null EMAS*/;
  reason: string;
  document: { name: string; url: string | null } | null;
  status: LeaveRequestStatus;
  comment: string | null;
  createdAt: string;
  decidedAt: string | null;
}
```

#### POST `/api/tutor/leave-requests/{id}/decision`

| Maydon     | Tip                     | Majburiy | Validatsiya |
| ---------- | ----------------------- | -------- | ----------- |
| `decision` | `'approve' \| 'reject'` | ha       | enum        |
| `comment`  | string                  | yo'q     | ≤ 1000      |

Response 200 `TutorLeaveRequest`. `approve` → oraliqdagi har ish kuni `excused` (`DailyAttendance` yaratiladi/yangilanadi).
Xatolar: 400; **404**; **409** — status `pending` emas.

#### GET `/api/tutor/grading`

Faol davri bo'lmagan talaba qatorga **kirmaydi**.

```ts
interface GradingRow {
  studentId: string;
  name: string;
  attendance: { points: number /*0..40, 1 kasr*/; pct: number };
  reports: { points: number /*0..30*/; avg: number };
  tutorPoints: number | null /*0..20*/;
  referencePoints: number | null /*0..10*/;
  recommended: { tutorPoints: number; referencePoints: number };
  total: number /*0..100, 1 kasr*/;
  grade: 2 | 3 | 4 | 5 | null; /*null → davomat < 70%, qayta topshiradi*/
}
```

#### PUT `/api/tutor/grading/{studentId}`

Body `{ tutorPoints: number | null; referencePoints: number | null }` — 0–20 / 0–10 (`errors.TutorPoints`, `errors.ReferencePoints`).
Response 200 `GradingRow` (qayta hisoblangan). Xatolar: 400 validation; **400** faol davr yo'q ("Talabaning faol amaliyot
davri yo'q…"); **404** ko'lamdan tashqari; **409** baho yakunlangan (`isFinalized`).

---

### 2.6 Student (TWA) — `Controllers/Student/*` · hammasi `StudentOnly`

Hammasi Bearer. Profil yo'q → 404 ("Talaba profili topilmadi.").

#### GET `/api/student/today`

```ts
interface TodayDto {
  date: string;
  window: TodayWindowDto;
  checkin: TodayCheckInDto;
  place: TodayPlaceDto | null;
  diary: TodayDiaryDto;
}
interface TodayWindowDto {
  start: string /*"09:00" check-in ochiladi*/;
  end: string /*"09:15" shundan late*/;
  closesAt: string /*"10:30" check-in yopiladi*/;
  checkoutAt: string /*"17:00" check-out ochiladi (18:00 gacha)*/;
  isOpen: boolean; /*hozirgi bosqich (check-in yoki check-out) mumkinmi*/
}
interface TodayCheckInDto {
  status: AttendanceStatus /*pending|present|late|absent|excused|dayOff*/;
  checkInAt: string | null /*ISO*/;
  checkOutAt: string | null;
  distanceM: number | null;
  radiusM: number | null /*korxona yo'q → null*/;
  gpsAccuracyM: number | null;
  suspicious: boolean;
  autoClosed: boolean;
  note: string | null; /*amal mumkin bo'lmasa sabab: "Amaliyot joyingiz hali tasdiqlanmagan." …*/
}
interface TodayPlaceDto {
  company: string;
  address: string;
  radiusM: number;
  attendancePct: number /*1 kasr*/;
  daysPresent: number;
  daysTotal: number;
  reports: number;
  avgScore: number;
}
interface TodayDiaryDto {
  submittedToday: boolean;
  minChars: number /*sozlama minReportLength*/;
  maxFiles: number; /*5*/
}
```

`place` — faqat ariza `approved` va korxona bor bo'lsa. Davr yo'q → `checkin.status="pending"`, `note="Faol amaliyot davri yo'q."`, `place=null`.
"Chiqdi" holati alohida status emas — `checkOutAt !== null` (yoki `autoClosed`).

#### POST `/api/student/checkin` · POST `/api/student/checkout`

Body (ikkalasi bir xil, `GeoRequestValidator`):

| Maydon       | Tip          | Majburiy | Validatsiya                                               |
| ------------ | ------------ | -------- | --------------------------------------------------------- |
| `lat`        | number       | ha       | −90..90 (`errors.Lat`)                                    |
| `lng`        | number       | ha       | −180..180 (`errors.Lng`)                                  |
| `accuracy`   | number (m)   | ha       | 0..100000 (`errors.Accuracy`)                             |
| `occurredAt` | ISO datetime | ha       | ≤ server + 1 min; ≥ server − 10 min (`errors.OccurredAt`) |

Response 200 `TodayDto` (yangilangan). **Har urinish** (rad etilgani ham) `AttendanceEvent` ga yoziladi. Idempotent:
bir xil `occurredAt` bilan qabul qilingan urinish qayta kelsa — xato emas, joriy holat.

Xato → status (`CheckInRejectReason.ToException`), `detail` = §3.2 xabari:

| Check-in                                | Status  | Check-out                               | Status  |
| --------------------------------------- | ------- | --------------------------------------- | ------- |
| davr yo'q ("Faol amaliyot davri yo'q.") | 400     | davr yo'q                               | 400     |
| `notApproved` (korxona/ariza yo'q)      | 400     | `noCheckIn`                             | **409** |
| `periodNotStarted` / `periodEnded`      | 400     | `alreadyCheckedOut` (yoki `autoClosed`) | **409** |
| `notWorkDay`                            | 400     | `windowNotOpen` (17:00 dan oldin)       | 400     |
| `onLeave`                               | 400     | `windowClosed` (18:00 dan keyin)        | 400     |
| `alreadyCheckedIn`                      | **409** | `poorAccuracy`                          | 400     |
| `windowNotOpen` (09:00 dan oldin)       | 400     | `outOfRadius`                           | **409** |
| `windowClosed` (10:30 dan keyin)        | 400     |                                         |         |
| `poorAccuracy` (accuracy > 100 m)       | 400     |                                         |         |
| `outOfRadius` (masofa > radius)         | **409** |                                         |         |

Tekshiruv tartibi aynan shu (birinchi mos kelgan sabab qaytadi). Qabul: `localNow ≥ 09:15` → `late`, aks holda `present`.

#### GET `/api/student/place` · 404

404 — davr yoki ariza yoki korxona yo'q ("Amaliyot joyi hali biriktirilmagan.").

```ts
interface PracticePlaceDto {
  status: ApplicationStatus;
  comment: string | null /*tyutor qaror izohi*/;
  company: string;
  tin: string /*9 raqam*/;
  activity: string;
  address: string;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  radiusM: number;
  lat: number;
  lng: number;
  periodFrom: string;
  periodTo: string;
  contract: {
    fileId: string;
    fileName: string;
    pages: number | null;
    sizeBytes: number;
    uploadedAt: string /*ISO datetime*/;
    approvedAt: string | null /*faqat approved*/;
    approvedBy: string | null /*FISH*/;
    templateUrl: string | null; /*faol contract shabloni "/api/files/<id>"*/
  } | null;
}
```

#### GET `/api/student/diary`

Barcha davrlar, `date` desc → `submittedAt` desc.

```ts
interface DiaryEntryDto {
  id: string;
  date: string /*DateOnly*/;
  submittedAt: string;
  status: DiaryStatus;
  text: string;
  learned: string | null;
  files: { id: string /*storedFileId*/; name: string; url: string }[];
  score: number | null;
  comment: string | null; /*tyutor*/
}
```

#### POST `/api/student/diary` · `multipart/form-data` · 201

| Form maydoni | Tip                     | Majburiy | Validatsiya                                                                                                        |
| ------------ | ----------------------- | -------- | ------------------------------------------------------------------------------------------------------------------ |
| `text`       | string                  | ha       | bo'sh emas; trim uzunligi ≥ `minReportLength` (default 150); ≤ 10000 (`errors.Text`)                               |
| `learned`    | string                  | yo'q     | ≤ 10000 (`errors.Learned`)                                                                                         |
| `files`      | file[] (bir nomda ko'p) | yo'q     | ≤ 5 ta; har biri > 0 va ≤ 5 MB; `image/jpeg,png,webp,heic,heif` yoki `application/pdf`; nom ≤ 255 (`errors.Files`) |

Butun so'rov ≤ 30 MB (`RequestSizeLimit`) — oshsa 413. Response **201** `DiaryEntryDto`.
Xatolar: 400 validation; **400** davr yo'q / davr bugunni o'z ichiga olmaydi / fayllar jami > 5; **409** — bugungi
hisobot allaqachon bor ("Bugungi hisobot allaqachon yuborilgan."). Istisno: bugungisi `rewrite` holatida → qayta yoziladi
(`Resubmit`, fayllar qo'shiladi, status → `submitted`), 201.

#### GET `/api/student/calendar` — `?month=YYYY-MM` (berilmasa joriy oy)

400 — format (`errors.Month`). Oyning **har kuni**.

```ts
interface CalendarMonthDto {
  month: string;
  studentName: string;
  groupName: string;
  days: { date: string; status: CalendarDayStatus }[];
}
```

Davr yo'q: kelajak → `future`, qolgani `dayOff`. Aks holda §4.1 `DayStatus` qoidasi.

#### GET `/api/student/leave-requests` · POST `/api/student/leave-requests` (201)

POST body:

| Maydon             | Tip      | Majburiy | Validatsiya                                              |
| ------------------ | -------- | -------- | -------------------------------------------------------- |
| `dateFrom`         | DateOnly | ha       | bo'sh emas (`errors.DateFrom`)                           |
| `dateTo`           | DateOnly | ha       | ≥ `dateFrom`; oraliq ≤ 31 kun (`errors.DateTo`)          |
| `reason`           | string   | ha       | trim ≥ 10, ≤ 1000 (`errors.Reason`)                      |
| `attachmentName`   | string   | yo'q     | ≤ 255 (fayl yuklanmaydi — faqat nom)                     |
| `attachmentFileId` | GUID     | yo'q     | o'zi yuklagan `StoredFile` bo'lishi shart, aks holda 404 |

Response 201 `LeaveRequestDto`. Xatolar: 400 validation; **400** davr yo'q / sanalar davr tashqarisida ("Ruxsat sanalari
amaliyot davri ichida bo'lishi kerak."); **409** — `rejected` bo'lmagan kesishuvchi so'rov bor.

```ts
interface LeaveRequestDto {
  id: string;
  dateFrom: string;
  dateTo: string;
  reason: string;
  status: LeaveRequestStatus;
  comment: string | null;
  document: { name: string; url: string | null } | null;
  createdAt: string; /*ISO datetime*/
}
```

#### GET `/api/student/portfolio` · 404

404 — faol davr yo'q.

```ts
interface PortfolioDto {
  student: string;
  group: string;
  practiceTitle: string /*davr nomi*/;
  company: string | null;
  periodFrom: string;
  periodTo: string;
  stats: {
    attendancePct: number;
    daysPresent: number;
    daysTotal: number;
    late: number;
    excused: number;
    reports: number;
    avgScore: number;
  };
  score: {
    key: 'attendance' | 'reports' | 'tutor' | 'reference';
    weightPct: 40 | 30 | 20 | 10;
    points: number;
  }[];
  total: number;
  grade: number | null;
  finalized: boolean;
  conclusion: { text: string; author: string; date: string /*ISO*/ } | null;
  pdfUrl: string | null; /*hozir null*/
}
```

---

## 3. Enum'lar (JSON — camelCase string)

### 3.1 Domain va Application enum'lari

| Enum                               | Qiymatlar                                                                                                                                                                                                                                                                                                                                 | Qayerda                                                                                |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `UserRole`                         | `admin` · `tutor` · `student`                                                                                                                                                                                                                                                                                                             | `UserSummaryDto.role`, `AuditEntryDto.userRole` (JWT claim: `Admin`/`Tutor`/`Student`) |
| `AttendanceStatus`                 | `pending` · `present` · `late` · `absent` · `excused` · `dayOff`                                                                                                                                                                                                                                                                          | tutor today `rows[].status`, TWA `checkin.status`                                      |
| `CalendarDayStatus`                | `future` · `pending` · `present` · `late` · `absent` · `excused` · `dayOff`                                                                                                                                                                                                                                                               | tutor/TWA kalendar                                                                     |
| `AttendanceEventKind`              | `checkIn` · `checkOut`                                                                                                                                                                                                                                                                                                                    | ichki                                                                                  |
| `CheckInRejectReason`              | `none` · `notApproved` · `notWorkDay` · `periodNotStarted` · `periodEnded` · `windowNotOpen` · `windowClosed` · `poorAccuracy` · `outOfRadius` · `alreadyCheckedIn` · `noCheckIn` · `alreadyCheckedOut` · `onLeave`                                                                                                                       | API'da faqat xabar (§3.2)                                                              |
| `DiaryStatus`                      | `submitted` · `seen` · `rewrite` · `approved`                                                                                                                                                                                                                                                                                             | diary'lar                                                                              |
| `DiaryReviewAction` (request)      | `approve` · `score` · `rewrite`                                                                                                                                                                                                                                                                                                           | tutor review                                                                           |
| `DiaryState`                       | `written` · `pending` (+ `null`)                                                                                                                                                                                                                                                                                                          | tutor today `rows[].diary`                                                             |
| `LeaveRequestStatus`               | `pending` · `approved` · `rejected`                                                                                                                                                                                                                                                                                                       | leave                                                                                  |
| `LeaveDecision` (request)          | `approve` · `reject`                                                                                                                                                                                                                                                                                                                      | tutor leave decision                                                                   |
| `ApplicationStatus`                | `draft` · `submitted` · `revisionNeeded` · `approved` · `rejected` · `completed`                                                                                                                                                                                                                                                          | applications, TWA place                                                                |
| `ApplicationDecision` (request)    | `approve` · `return` · `reject`                                                                                                                                                                                                                                                                                                           | tutor decision                                                                         |
| `PracticePeriodStatus`             | `planned` · `active` · `closed`                                                                                                                                                                                                                                                                                                           | admin groups `period.status`                                                           |
| `WorkDays`                         | bitmask; sozlamada `"1,2,3,4,5,6"` (1=Du … 7=Ya)                                                                                                                                                                                                                                                                                          | settings `workDays`                                                                    |
| `StudentStatus` (domain, akademik) | `active` · `suspended` · `graduated`                                                                                                                                                                                                                                                                                                      | API'ga chiqmaydi                                                                       |
| `TodayFilter` (query)              | `present` · `late` · `absent` · `excused` · `pending` · `suspicious`                                                                                                                                                                                                                                                                      | tutor today `?status=`                                                                 |
| `TodayAlertKind`                   | `outOfRadius` · `notCheckedIn` · `newLeaveRequests` · `newApplications`                                                                                                                                                                                                                                                                   | tutor today alerts                                                                     |
| `StudentState`                     | `active` · `redFlag` · `suspicious`                                                                                                                                                                                                                                                                                                       | tutor students                                                                         |
| `MapPointKind`                     | `ok` · `late` · `bad`                                                                                                                                                                                                                                                                                                                     | tutor map                                                                              |
| `FacultyStatus`                    | `active` · `attention`                                                                                                                                                                                                                                                                                                                    | admin faculties                                                                        |
| `TutorStatus`                      | `active` · `late`                                                                                                                                                                                                                                                                                                                         | admin tutors, dashboard                                                                |
| `TutorScopeLevel`                  | `faculty` · `department` · `direction` · `group`                                                                                                                                                                                                                                                                                          | admin tutors `scopes[].level`, `PUT .../scopes` body                                   |
| `AdminStudentStatus`               | `active` · `flagged` · `unlinked`                                                                                                                                                                                                                                                                                                         | admin students                                                                         |
| `CompanyFlag`                      | `largeRadius` · `suspicious` · `null`                                                                                                                                                                                                                                                                                                     | admin companies                                                                        |
| `AuditAction`                      | `created` · `updated` · `deleted` · `manualOverride` · `loggedIn` · `loginFailed` · `manualCheckIn` · `radiusChanged` · `applicationApproved` · `applicationReturned` · `applicationRejected` · `leaveApproved` · `leaveRejected` · `diaryReviewed` · `gradeChanged` · `gradeReverted` · `settingsChanged` · `attendanceMarkedSuspicious` · `faculty/department/direction/group` × `Created/Updated/Deleted/Activated/Deactivated` (masalan `facultyCreated`, `groupDeactivated`) · `tutorCreated` · `tutorUpdated` · `tutorActivated` · `tutorDeactivated` · `tutorPasswordReset` · `tutorScopesChanged` | admin audit `action`, `?action=`                                                       |
| `SettingType`                      | `int` · `bool` · `weekdays`                                                                                                                                                                                                                                                                                                               | settings `type`                                                                        |
| `SettingKey` (string const)        | `geofenceRadius` · `lateTolerance` · `minGpsAccuracy` · `autoCheckout` · `workDays` · `dailyReportRequired` · `minReportLength` · `checkInWindow`                                                                                                                                                                                         | settings                                                                               |
| `DocumentTemplateKind`             | `contract` · `referral` · `reference`                                                                                                                                                                                                                                                                                                     | settings templates                                                                     |
| `StoredFileKind`                   | `contract` · `diaryAttachment` · `leaveDocument` · `template`                                                                                                                                                                                                                                                                             | ichki (files ko'lami)                                                                  |
| Grade                              | `2` · `3` · `4` · `5` · `null`                                                                                                                                                                                                                                                                                                            | grading, portfolio (number)                                                            |

### 3.2 `CheckInRejectReason` xabarlari (`detail` da keladi)

| Sabab               | Status  | `detail`                                                            |
| ------------------- | ------- | ------------------------------------------------------------------- |
| `notApproved`       | 400     | Amaliyot joyingiz hali tasdiqlanmagan.                              |
| `notWorkDay`        | 400     | Bugun ish kuni emas.                                                |
| `periodNotStarted`  | 400     | Amaliyot davri hali boshlanmagan.                                   |
| `periodEnded`       | 400     | Amaliyot davri tugagan.                                             |
| `windowNotOpen`     | 400     | Belgilanish oynasi hali ochilmagan.                                 |
| `windowClosed`      | 400     | Bugungi belgilanish oynasi yopilgan.                                |
| `poorAccuracy`      | 400     | GPS aniqligi yetarli emas. Ochiq joyga chiqib qayta urinib ko'ring. |
| `outOfRadius`       | **409** | Siz amaliyot joyida emassiz.                                        |
| `alreadyCheckedIn`  | **409** | Bugun allaqachon belgilangansiz.                                    |
| `noCheckIn`         | **409** | Avval kelganingizni belgilang.                                      |
| `alreadyCheckedOut` | **409** | Ketish allaqachon belgilangan.                                      |
| `onLeave`           | 400     | Bu kunga ruxsat tasdiqlangan — belgilanish shart emas.              |

Shu xabarlar `TodayDto.checkin.note` da ham keladi (amal hozir mumkin bo'lmasa).

### 3.3 Konstantalar (backend)

`DIARY_MIN_CHARS` — sozlama `minReportLength` (default **150**, `TodayDto.diary.minChars` dan oling) · `DIARY_MAX_CHARS 10000` ·
`DIARY_MAX_FILES 5` · `DIARY_MAX_FILE_BYTES 5 MB` · `RADIUS 50..1000 / step 50` · `DEFAULT_RADIUS 200` ·
`CHECKLIST_ITEMS 7` (indeks 0..6) · `COMMENT_MAX 1000` (ariza, kundalik, ruxsat) · `LEAVE_REASON 10..1000` · `LEAVE_MAX_DAYS 31` ·
`TUTOR_POINTS 0..20` · `REFERENCE_POINTS 0..10` · `DIARY_SCORE 1..5` · `PAGE_SIZE default 20, max 100` · `Q_MAX 100`.

---

## 4. Hisob qoidalari

### 4.1 Kun holati (`AttendanceCalendar.DayStatus` — TWA; `AttendanceStatusResolver` — tyutor)

Tartib: **bazadagi qator** (`present/late/absent/excused/dayOff`) → tasdiqlangan ruxsat qamrasa `excused` →
ish kuni emas (davr tashqarisi, `workDays` da yo'q, bayram) → `dayOff` (TWA'da davr tashqarisidagi kelajak kun → `future`) →
kelajak → `future` (tyutor today'da `pending`) → **bugun**: `localNow ≥ 10:30` (`WindowEnd`) → `absent`, aks holda `pending` →
o'tgan kun → `absent`. Bazada faqat hodisali kunlar (`present/late/excused`, qo'lda) saqlanadi; `pending/absent/dayOff`
o'qishda hisoblanadi.

### 4.2 Davomat foizi

Uch hisoblagich bor, formulasi bir xil: **`pct = attended / (countable − excused) × 100`**, `attended = present + late`,
maxraj ≤ 0 → 0.

| Qayerda                                                                        | `countable`                                                                                                                                                                                      | Yaxlitlash  |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------- |
| TWA `today.place`, `portfolio.stats` (`AttendanceCalendar.ComputeStats`)       | davr boshidan bugungacha ish kunlari + davr ichidagi yozuvli kunlar; bugun — yozuv bo'lsa yoki oyna (10:30) yopilgan bo'lsa                                                                      | 1 kasr      |
| Tyutor `students`, `grading` (`StudentStatsCalculator`)                        | o'tgan ish kunlari; bugun — oyna yopilgan bo'lsa; bugun `present/late` bo'lsa har doim                                                                                                           | 1 kasr      |
| Admin `students/groups/faculties/dashboard` (`PracticeCalendar.AttendancePct`) | `elapsedWorkDays` = davr boshidan **kechagacha** ish kunlari (bugun kirmaydi); guruhda × arizasi `approved` talabalar; bugungi fakultet/dashboard foizi = `attended / expectedToday` (excused 0) | int, 0..100 |

`late` — `attended` ichida (kelgan deb hisoblanadi). `excused` — maxrajdan chiqariladi. `daysTotal`/`totalDays` = `countable − excused`.

### 4.3 Chegaralar (kodda qotirilgan)

| Nima                                      | Qiymat                                                                                                                                        | Manba                                                                         |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| Fakultet `attention`                      | bugungi pct < **70** (expected > 0)                                                                                                           | `AdminThresholds.AttentionAttendancePct`                                      |
| Tyutor `late`                             | eng eski `submitted` ariza > **48 soat**                                                                                                      | `AdminThresholds.PendingApplicationLateAfter`                                 |
| Talaba `flagged` (admin)                  | shubhali kunlar ≥ **2** yoki (elapsed > 0 va pct < **70**)                                                                                    | `FlaggedSuspiciousDays`, `FlaggedAttendancePct`                               |
| Talaba `redFlag` (tyutor)                 | totalDays > 0 va pct < **70**                                                                                                                 | `GradeThresholds.MinAttendancePct`                                            |
| Talaba `suspicious` (tyutor)              | shubhali kunlar ≥ **1**                                                                                                                       | `GetTutorStudentsQueryHandler.SuspiciousMinCount`                             |
| Korxona `largeRadius`                     | radius > **500 m**                                                                                                                            | `AdminThresholds.LargeRadiusM`                                                |
| Korxona `suspicious`                      | shubhali kunlar ≥ **3**                                                                                                                       | `AdminThresholds.SuspiciousCompanyEvents`                                     |
| Dashboard audit                           | so'nggi **8**                                                                                                                                 | `DashboardAuditCount`                                                         |
| Qaror tezligi oynasi                      | so'nggi **60 kun**                                                                                                                            | `DecisionSpeedWindow`                                                         |
| Check-in default (`CheckInRules.Default`) | start **09:00**, late **09:15** (+15), yopiladi **10:30** (+90), check-out **17:00**, auto-close **18:00** (+60), GPS ≤ **100 m**             | davr `PracticePeriod` o'z qiymatlarini beradi; GPS — sozlama `minGpsAccuracy` |
| Shubha (`SuspiciousDetector`)             | oldingi urinishdan ≤ **6 soat** ichida ≥ **1 km** sakrash va tezlik > **150 km/soat**; yoki oldingi kunlar bilan **aynan bir xil koordinata** | `MarkSuspicious` — jazo emas, faqat bayroq                                    |
| `occurredAt`                              | ≤ +1 min, ≥ −10 min                                                                                                                           | `GeoRequestValidator`                                                         |
| Telegram `auth_date`                      | ≤ 24 soat, ≥ −5 min                                                                                                                           | `TelegramOptions.MaxAgeSeconds`                                               |

### 4.4 Baho (`GradeCalculator`)

```
attendancePoints = round1(attendancePct / 100 × 40)
reportPoints     = diaryCount == 0 ? 0 : round1(diaryAvg / 5 × 30)     // diaryAvg — baholangan kundaliklar o'rtachasi
tutorPoints      = tutorPoints ?? 0        (0..20)
referencePoints  = referencePoints ?? 0    (0..10)
total            = round1(sum)              (0..100)
grade            = attendancePct < 70 ? null : total ≥ 86 → 5 | ≥ 71 → 4 | ≥ 56 → 3 | aks holda 2
recommended.tutorPoints     = diaryCount == 0 ? 0 : round(diaryAvg / 5 × 20)
recommended.referencePoints = round(attendancePct / 100 × 10)
```

Tyutor `grading` da `diaryCount` = **baholangan** yozuvlar soni (`ScoredCount`); TWA `portfolio` da — barcha yozuvlar soni
(`avgScore` esa baholanganlar bo'yicha). Yakunlangan (`isFinalized`) bahoni PUT qilib bo'lmaydi → 409.

### 4.5 Ariza checklist va qaror

- `checklist` — 7 punkt (indeks 0..6), tyutor `approve` da belgilaydi; saqlanadi, `ApplicationDetail.checklist` da qaytadi.
  Punkt matnlari backend'da yo'q — frontend'da.
- `approve`: `radiusM` majburiy (50–1000, 50 qadam) → `ProposedRadiusM` va `Company.RadiusM`. `return`: `revisionCount++`,
  status `revisionNeeded`, izoh majburiy. `reject`: izoh majburiy. Qaror faqat `submitted` holatidan; aks holda 409.
- Talaba `place` uchun: davr bo'yicha `approved` ariza ustun, bo'lmasa eng so'nggisi (rad/qaytarilganini ko'rsatish uchun).

---

## 5. v1 → v2 farqlar (frontend agentlari uchun)

Har qator: **v1 shakl → v2 haqiqiy shakl → nima qilish kerak**. Ustun "Qayer" — frontend fayli.

### 5.1 Umumiy

| #   | Qayer                  | v1                            | v2                                                                                                                              | Nima qilish                                                                   |
| --- | ---------------------- | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| U1  | `shared/auth/roles.ts` | `UserRole` DTO'da `1/2/3`     | `"admin" \| "tutor" \| "student"` (camelCase string); JWT claim `"Admin"`                                                       | `toUserRole` string'ni asosiy qilsin                                          |
| U2  | `shared/api/client.ts` | 401 body'siz; 403 login       | 401/403 middleware'dan **body'siz**; handler 403 — ProblemDetails; **429** body'siz                                             | `ApiError` body bo'sh bo'lsa ham `kind` ni statusdan olsin                    |
| U3  | xato `errors`          | PascalCase                    | PascalCase (FluentValidation) **+** settings'da kalit nomi (`geofenceRadius`) **+** ASP.NET avtomatik 400 (`$.field`, `status`) | `fieldError` 3 shaklni ham qidirsin                                           |
| U4  | `DateTimeOffset`       | `"…Z"`                        | `"…+00:00"`                                                                                                                     | `new Date()` bilan parse — o'zgarish shart emas, test fixture'larni yangilang |
| U5  | fayl URL               | `url?` ba'zan yo'q            | har doim `"/api/files/<guid>"`, **Bearer kerak**                                                                                | `fetch`+blob helper yozing; `<a href>` ishlatmang                             |
| U6  | `Paged<T>`             | `{items,page,pageSize,total}` | bir xil; `pageSize` > 100 → 20 ga tushadi (xato yo'q)                                                                           | —                                                                             |
| U7  | endpoint soni          | 4 + 34                        | 39 (+ `GET /api/files/{id}`)                                                                                                    | fayl endpoint'ini qo'shing                                                    |

### 5.2 Admin (`dashboard/src/features/admin/*`)

| #   | Qayer                        | v1                                                                                                                  | v2                                                                                                                                                                    | Nima qilish                                                                         |
| --- | ---------------------------- | ------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| A1  | dashboard `stats`            | `AdminStat[] {label,value:"1 284",note,noteKind}`                                                                   | **obyekt** `DashboardStatsDto` — 20 ta xom raqam (`studentsTotal`, `presentToday`, `attendanceTodayPct`…)                                                             | kartalarni frontend yasaydi: label/format/noteKind lokal                            |
| A2  | dashboard `faculties[]`      | `{id,name,pct,count:"286 talaba"}`                                                                                  | `{id,name,code,studentCount,expectedToday,attendedToday,attendancePct}`                                                                                               | `pct→attendancePct`, `count` matnini yasang                                         |
| A3  | dashboard `tutors[]`         | `{id,name,scope,pending:"0 ariza",speed:"o'rtacha 4 soat",pendingKind}`                                             | `{id,name,facultyCode,groups[],studentCount,pendingCount,oldestPendingAt,avgDecisionHours,lastActiveAt,status:'active'\|'late'}`                                      | matnlarni yasang; `pendingKind` → `status`                                          |
| A4  | dashboard `audit[]`          | `{id,at,text,who}`                                                                                                  | `AuditEntryDto` (xom: `action`, `entityName`, `userName`, `userRole`, `reason`, `changes`)                                                                            | matnni `action`+`entityName` dan yasang; + `date` maydoni                           |
| A5  | `Faculty`                    | `{…, directions, groups, students, attendancePct, status}`                                                          | + `code`, `tutors`                                                                                                                                                    | qo'shing                                                                            |
| A6  | `Group`                      | `tutor:"N. Saidova"`                                                                                                | `tutorId: string\|null`, `tutor: string\|null` (to'liq FISH); + `faculty`, `facultyCode`, `period{…}\|null`                                                           | qisqartirishni frontend qilsin; `null` holatini ko'rsating                          |
| A7  | `Tutor`                      | `phone:"+998 90 111 22 33"`, `assigned:"AT · 412-22, 413-22"`                                                       | `phone: string\|null` (E.164 xom), `facultyCode`, `groups: string[]`; + `facultyId`, `facultyName`, `oldestPendingAt`, `avgDecisionHours`, `lastActiveAt`, `isActive` | formatlashni frontend qilsin                                                        |
| A8  | `Student`                    | `{id,fullName,group,faculty,company,attendancePct,status}`                                                          | + `hemisId`, `groupId`, `course`, `suspiciousDays`, `telegramLinked`                                                                                                  | qo'shing                                                                            |
| A9  | `Company`                    | `tin:"304 512 889"`, `flag:'large-radius'\|'suspicious'\|null`                                                      | `tin` 9 raqam xom; **`flag:'largeRadius'\|'suspicious'\|null`**; + `activity`, `suspiciousDays`, `isActive`                                                           | enum qiymatini `largeRadius` ga o'zgartiring; STIR formatini frontend qilsin        |
| A10 | `AuditEntry` / `AuditAction` | `{id,at,action,detail,who}`; `manual-checkin\|radius-changed\|application-rejected\|status-changed\|grade-reverted` | `AuditEntryDto`; **18 ta camelCase** qiymat (`manualCheckIn`, `radiusChanged`, `settingsChanged`…)                                                                    | enum'ni to'liq almashtiring; `?action=` ishlaydi                                    |
| A11 | `Setting`                    | `{key, k, v:"200 m", note}`                                                                                         | `{key, label, value:"200" (xom), type, unit, note, min, max, updatedAt}`                                                                                              | `k→label`, `v→value+unit`; input turini `type` dan; **yangi kalit `checkInWindow`** |
| A12 | `Holiday`                    | `date:"08.03"`                                                                                                      | `date:"YYYY-MM-DD"`, + `isRecurring`                                                                                                                                  | format qiling                                                                       |
| A13 | `DocTemplate`                | `{id,name,file}`                                                                                                    | `{id,name,kind,fileName,url}`                                                                                                                                         | `file→fileName`, `url` orqali yuklab olish                                          |
| A14 | `PUT settings`               | `values: {key: "200 m"}`                                                                                            | `values: {key: "200"}` — xom, tiplangan tekshiruv, 400 `errors.<key>`                                                                                                 | birlikni yubormang; xatoni kalit bo'yicha ko'rsating                                |

### 5.3 Reports (`dashboard/src/features/reports`)

| #   | v1                                                                         | v2                                                                   | Nima qilish                            |
| --- | -------------------------------------------------------------------------- | -------------------------------------------------------------------- | -------------------------------------- |
| R1  | `filter:{dateRange:"01.10.2026 — 15.11.2026", scope:"412-22 · 38 talaba"}` | `{dateFrom, dateTo (DateOnly\|null), scope, groups[], studentCount}` | matnni yasang                          |
| R2  | `ReportCard.fmt:"PDF / Excel"`                                             | `formats:('pdf'\|'xlsx')[]`, + `available:false`, `note`             | tugmani `available` bo'yicha o'chiring |
| R3  | `GET /reports/{id}/download`                                               | **yo'q**                                                             | chaqirmang                             |

### 5.4 Tyutor (`dashboard/src/features/tutor/*`)

| #   | Qayer                                                       | v1                                                                                                                | v2                                                                                                                                                  | Nima qilish                                                             |
| --- | ----------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| T1  | today `?status=`                                            | `present\|late\|absent\|suspicious`                                                                               | `present\|late\|absent\|excused\|pending\|suspicious`; + `q`, `page`, `pageSize`                                                                    | filtr ro'yxatini kengaytiring                                           |
| T2  | today `stats`                                               | `TodayStat[] {key,label,value,note,dot}`                                                                          | **obyekt** `{present,late,absent,excused,pending,diaries,total}`                                                                                    | kartalarni frontend yasaydi                                             |
| T3  | today `alerts[]`                                            | `{id,text,action,href?}`                                                                                          | `{kind,count,href,maxDistanceM}`                                                                                                                    | matnni `kind` dan yasang                                                |
| T4  | today `rows` + `pagination:{page,pageSize,shown,total}`     | **`rows: Paged<AttendanceRow>`** `{items,page,pageSize,total}`                                                    | `rows.items` ga o'ting; `shown = items.length`                                                                                                      |
| T5  | `AttendanceRow.status`                                      | `present\|late\|absent\|suspicious\|excused`                                                                      | `AttendanceStatus` (`pending`, `dayOff` qo'shildi, **`suspicious` yo'q**); + `suspicious: boolean`, `manual`, `autoClosed`; `company: string\|null` | shubhani bayroqdan oling                                                |
| T6  | applications `?status=` / `counts`                          | `new\|fixing\|approved\|rejected`                                                                                 | `submitted\|revisionNeeded\|approved\|rejected`; `counts` kalitlari shu                                                                             | tab kalitlarini almashtiring (`new→submitted`, `fixing→revisionNeeded`) |
| T7  | `ApplicationSummary.waited:"2 soat oldin"`                  | **yo'q**; `submittedAt`, `decidedAt` ISO; `course: number`                                                        | "oldin" matnini frontend hisoblasin                                                                                                                 |
| T8  | `ApplicationDetail.fields[] {k,v}`                          | **`companyDetails`** obyekt (`name,tin,activity,address,supervisorName,supervisorPhone,mentorName,mentorPhone`)   | jadvalni obyektdan yasang                                                                                                                           |
| T9  | `ApplicationDetail.contract {name,pages,size:"1.2 MB",url}` | `{name, pages: number\|null, sizeBytes, url} \| null`; + `comment`, `checklist: number[]`, `revisionCount`        | hajmni formatlang; `null` holati                                                                                                                    |
| T10 | decision body                                               | `radiusM` va `checklist` majburiy                                                                                 | `radiusM` faqat `approve` da majburiy; `checklist` ixtiyoriy; `comment` **`return`/`reject` da majburiy**; 409 hal qilingan                         | validatsiyani moslang                                                   |
| T11 | `TutorStudent.state`                                        | `'red_flag'`                                                                                                      | **`'redFlag'`**; `suspiciousCount` har doim `number`                                                                                                | qiymatni almashtiring                                                   |
| T12 | `DiaryEntry`                                                | `{…, files:{name,url?}, score}`                                                                                   | + `date`, `learned`, `reviewedAt`, `comment`; `files[].url` har doim bor; `?status=` filtr                                                          | maydonlarni qo'shing                                                    |
| T13 | `CalendarRow.days: DayCode[]` (`k\|l\|a\|s\|d\|n`)          | **`CalendarDayStatus[]`** (`future\|pending\|present\|late\|absent\|excused\|dayOff`)                             | `DayCode` mapping'ini olib tashlang                                                                                                                 |
| T14 | map `?date=`                                                | yuborilmaydi                                                                                                      | ishlaydi (`YYYY-MM-DD`)                                                                                                                             | sana tanlash qo'shsa bo'ladi                                            |
| T15 | `LeaveRequest.dateTo: string\|null`                         | **`dateTo` har doim string** (bir kunlik → `dateFrom` ga teng); + `comment`, `createdAt`, `decidedAt`; `?status=` | `null` tekshiruvini `dateFrom===dateTo` ga almashtiring                                                                                             |
| T16 | grading PUT                                                 | 400/404                                                                                                           | + **400** faol davr yo'q, **409** baho yakunlangan; faol davrsiz talaba GET'da yo'q                                                                 | holatlarni ko'rsating                                                   |

### 5.5 Talaba / TWA (`twa/src/features/*`)

| #   | Qayer                                | v1                                                                                 | v2                                                                                                                                                              | Nima qilish                                                        |
| --- | ------------------------------------ | ---------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| S1  | `TodayDto.checkin.status`            | `CheckinStatus 'pending'\|'in'\|'late'\|'out'`                                     | **`AttendanceStatus`** `pending\|present\|late\|absent\|excused\|dayOff`; "chiqdi" = `checkOutAt !== null`                                                      | enum'ni almashtiring; `out` ni `checkOutAt`/`autoClosed` dan oling |
| S2  | `TodayDto.window`                    | `{start,end,checkoutAt,isOpen}`                                                    | + **`closesAt`** ("10:30")                                                                                                                                      | ko'rsating                                                         |
| S3  | `TodayDto.checkin`                   | `radiusM: number`                                                                  | `radiusM: number\|null`; + `suspicious`, `autoClosed`, **`note: string\|null`** (nega mumkin emas)                                                              | `note` ni tugma ostida ko'rsating                                  |
| S4  | `TodayDto.place`                     | har doim                                                                           | **`null`** (ariza tasdiqlanmagan/davr yo'q)                                                                                                                     | bo'sh holat                                                        |
| S5  | `TodayDto.diary.minChars`            | 150 qotirilgan                                                                     | sozlamadan (`minReportLength`)                                                                                                                                  | frontend konstantasini DTO qiymati bilan almashtiring              |
| S6  | `PracticePlaceDto.status/statusKind` | label string + `statusKind`                                                        | **`status: ApplicationStatus`** enum, + `comment`; `statusKind` yo'q                                                                                            | label/rangni frontend hisoblasin                                   |
| S7  | `PracticePlaceDto.supervisor/mentor` | `"Islomov B. · +998…"` string                                                      | `supervisorName`, `supervisorPhone`, `mentorName\|null`, `mentorPhone\|null`; + `periodFrom`, `periodTo`                                                        | birlashtiring                                                      |
| S8  | `PracticePlaceDto.contract`          | `{fileName,pages,sizeBytes,uploadedAt:DateOnly,approvedAt,approvedBy,templateUrl}` | `{fileId, fileName, pages:number\|null, sizeBytes, uploadedAt: ISO datetime, approvedAt: ISO\|null, approvedBy\|null, templateUrl\|null} \| null`               | tiplarni yangilang                                                 |
| S9  | `DiaryEntryDto.status/statusKind`    | label + `statusKind`                                                               | **`status: DiaryStatus`**; + `date`; `files[]` = `{id,name,url}`; `statusKind` yo'q                                                                             | label'ni frontend                                                  |
| S10 | `POST diary`                         | 201/400                                                                            | + **409** bugungi bor (rewrite'dan tashqari); `files` turi/hajmi chegaralari; 413 > 30 MB                                                                       | 409 ni "allaqachon yuborilgan" deb ko'rsating                      |
| S11 | `CalendarMonthDto`                   | `{month,rowLabel:"Akmal · 412-22",days:{date,code:DayCode}}`                       | `{month, studentName, groupName, days:{date, status: CalendarDayStatus}}`                                                                                       | `DayCode` → enum; label'ni yasang                                  |
| S12 | `LeaveRequestDto`                    | `{id,from,to,reason,status:label,statusKind,createdAt:DateOnly}`                   | `{id, dateFrom, dateTo, reason, status: LeaveRequestStatus, comment, document\|null, createdAt: ISO}`                                                           | `from/to → dateFrom/dateTo`; label'ni frontend                     |
| S13 | `LeaveRequestCreate`                 | `{from,to,reason,attachmentName?}`                                                 | `{dateFrom, dateTo, reason, attachmentName?, attachmentFileId?}`; `errors.DateFrom/DateTo/Reason`; 400 davr tashqarisi; **409** kesishuv                        | nomlarni almashtiring                                              |
| S14 | `PortfolioDto.score[]`               | `{label,weightPct,points}`                                                         | `{key:'attendance'\|'reports'\|'tutor'\|'reference', weightPct, points}`; `grade: number\|null`; + `finalized`; `company\|null`; `conclusion.date` ISO datetime | label'ni `key` dan; `grade=null` → "qayta topshiradi"              |
| S15 | `GET portfolio`/`place`              | 200 har doim                                                                       | **404** faol davr/ariza yo'q                                                                                                                                    | bo'sh holat                                                        |
| S16 | `CheckinRequest.occurredAt`          | ISO                                                                                | ≤ +1 min / ≥ −10 min, aks holda 400 `errors.OccurredAt`                                                                                                         | offline navbatda eski urinishni yubormang                          |

### 5.6 v1 §7 ochiq savollar — javoblar

1. Bir resurs ikki shakl → **backend bitta enum** qaytaradi, label/rangni ikkala frontend ham o'zi hisoblaydi.
2. Davomat modeli → bitta `AttendanceStatus` (TWA `checkin.status` va tyutor `rows[].status`).
3. Kalendar → ikkalasi `CalendarDayStatus`; tyutor `days:number[] + rows[].days[]`, TWA `days:{date,status}[]` (shakl saqlangan).
4. Sahifalash → tyutor today ham `Paged<T>` (`rows`); qolgan tyutor ro'yxatlari massiv.
5. Server formatlaydigan matnlar → **yo'q**, hamma qiymat xom (raqam, E.164, 9 raqamli STIR, ISO).
6. Sozlamalar → tiplangan (`type/unit/min/max`), qiymat xom string.
7. Ruxsat hujjati → `attachmentName` + ixtiyoriy `attachmentFileId`; upload endpoint'i hali yo'q.
8. `map?date=` → ishlaydi. 9. decision 409 → bor. 10. download/`?action=` → download yo'q, `?action=` bor.
9. `radiusM` dublikati → saqlangan (`checkin.radiusM` nullable, `place.radiusM`). 12. Nav badge'lar → `tutor/today.alerts`
   va `applications.counts` dan olsa bo'ladi; alohida endpoint yo'q.
