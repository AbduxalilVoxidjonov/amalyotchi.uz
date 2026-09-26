# API-CONTRACT v3.15

Oxirgi yangilanish: 26.09.2026. **Manba — backend kodi** (`src/Amaliyotchi.Api`, `src/Amaliyotchi.Application`,
`src/Amaliyotchi.Domain`, `src/Amaliyotchi.Infrastructure`). v1 frontend mock'lari asosida yozilgan edi; bu hujjat
esa haqiqiy controller/DTO/validator/handler kodidan olingan — har bir maydon, chegara va status kod kodda bor.
Frontend (`web/dashboard`, `web/twa`, `web/shared`) shu shaklga moslanishi kerak; v1 bilan farqlar §5 da,
**v2 bilan farqlar §6 da**, v3.1 da qo'shilganlari — §6.6, v3.2 (talabalar Excel importi) — §6.8,
v3.3 (korxona CRUD, STIR oqimi, ommaviy biriktirish) — §6.9,
v3.4 (admin amaliyot davrlari) — §6.10,
v3.5 (bir guruhda bir nechta davr: davr tanlash qoidasi, talaba profilida davr tanlagichi) — §6.11,
v3.6 (admin davr statistikasi) — §6.12,
v3.7 (amaliyot joyida QR kod bilan check-in/check-out) — §6.13,
v3.8 (talaba brauzerda HEMIS ID + parol bilan kiradi, parol almashtirish, talaba kabineti) — §6.14,
v3.9 (talaba Telegram'ni HEMIS ID + parol bilan bog'laydi — `POST /api/auth/telegram/link`) — §6.15,
v3.10 (ruxsat so'rash moduli olib tashlandi — talaba va tyutor `leave-requests` endpoint'lari) — §6.16,
v3.11 (talaba bosh ekrani: davrning har bir kuni — `GET /api/student/period-days`) — §6.17,
v3.12 (admin davr yaratish/tahrirlashda ish kunlari va kunlik ish vaqti) — §6.18,
v3.13 (talabani bitta-bitta korxonaga biriktirish/o'tkazish, `ApplicationStatus.transferred`) — §6.19,
v3.14 ("aktiv korxona" qoidasi: ro'yxatlarda `company` faqat aktiv korxona, profilda yangi `activeCompany`) — §6.20,
v3.15 (korxona sahifalari — faqat hozir aktiv amaliyot o'tayotgan talabalar; o'chirish xabari) — §6.21.

Jami **108 ta endpoint**: Auth 7 · Admin 67 · Reports 1 · Tutor 20 · Student (TWA) 11 · Files 1 · Companies 1.

> Kontrollerlarda `[Http*]` atributlari **110 ta**: `POST /api/student/checkin` va `POST /api/student/checkout`
> har birida ikkitadan action bor (`multipart/form-data` va `application/json` — `[Consumes]` bilan ajratiladi,
> §2.6), lekin yo'l bitta. Shuning uchun endpoint (yo'l + metod) soni — **108**.

---

## 1. Umumiy qoidalar

### 1.1 Base path, transport

- Base path: `/api`. Dev API: `http://127.0.0.1:5080`. CORS `Cors:Origins` dan (default `http://127.0.0.1:5173`).
- So'rov/javob — JSON (`application/json`). Istisno: `POST /api/student/diary` — `multipart/form-data`;
  `POST /api/student/checkin` va `POST /api/student/checkout` — `multipart/form-data` **yoki** `application/json`
  (§2.6); `GET /api/files/{id}` — fayl (`Content-Disposition: attachment`, range qo'llanadi).
- **`Content-Type` har doim aniq yuborilsin.** Checkin/checkout yo'llarida sarlavha yo'q yoki qo'llab-quvvatlanmaydigan
  tur (masalan `text/plain`) bo'lsa → **415** (§2.6), so'rov bajarilmaydi.
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
- **Login** (`/api/auth/login`) — HEMIS ID + parol: admin, tyutor va (v3.8) **talaba** — xodim unga parol o'rnatgan
  bo'lsa (`POST /api/{admin|tutor}/students/{id}/password`). Talaba profil HEMIS ID'si bilan kiradi (brauzer rejimi —
  bot tayyor bo'lguncha). Paroli yo'q talaba → 403 umumiy xabar. `AuthResultDto.mustChangePassword = true` bo'lsa mijoz
  foydalanuvchini avval `POST /api/auth/change-password` ga yo'naltiradi.
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

Hisob qidiruvi (v3.8): avval `User.HemisId` (admin/tyutor), topilmasa — talaba `StudentProfile.HemisId` (o'chirilmagan
profil). Parol faollikdan **oldin** tekshiriladi (mavjud qoida).

Response 200 `AuthResultDto`. Xatolar: 400 `errors.HemisId` / `errors.Password`; **403** — HEMIS ID/parol noto'g'ri,
parolsiz hisob (masalan faqat Telegram'li talaba) (`detail`: "HEMIS ID yoki parol noto'g'ri.") yoki hisob faol emas
("Hisobingiz faol emas. Administratorga murojaat qiling."; talabada — "…Tyutoringizga murojaat qiling."); 429.
Har muvaffaqiyatsiz urinish audit'ga `loginFailed` sifatida yoziladi (talaba uchun ham).

```ts
interface AuthResultDto {
  accessToken: string;
  accessTokenExpiresAt: string /*ISO*/;
  refreshToken: string;
  user: UserSummaryDto;
  mustChangePassword: boolean; // v3.8 — parolni xodim o'rnatgan; login/refresh/telegram — barchasida keladi
}
interface UserSummaryDto {
  id: string;
  fullName: string;
  role: 'admin' | 'tutor' | 'student'; // camelCase (JWT claim'da "Admin")
  facultyId: string | null; // tyutor — ASOSIY fakulteti (bir nechta bo'lsa `faculties[0]`, §2.3.3); admin/talaba null bo'lishi mumkin
  phoneNumber: string | null; // E.164 xom: "+998901234567" (ma'lumot maydoni, login uchun emas; talabada null bo'lishi mumkin)
  groupId: string | null;
  groupName: string | null;
  course: number | null;
  hemisId: string | null; // login identifikatori — admin/tyutor/talaba barchasida bo'lishi mumkin
}
```

**Mock/seed HEMIS ID'lar** (dev): admin `100000000001`; tyutorlar — Nodira Saidova `100000000002`,
Baxtiyor Rasulov `100000000003`, Dilshod Ergashev `100000000004`. **Demo talaba** (v3.8, `SEED_DEMO=true`, brauzer
login'i): Aliyev Akmal — HEMIS ID `341030`, parol `talaba12345` (`mustChangePassword = false`).

#### POST `/api/auth/telegram` · AllowAnonymous · rate `auth` 10/min

| Maydon     | Tip    | Majburiy | Validatsiya              |
| ---------- | ------ | -------- | ------------------------ |
| `initData` | string | ha       | bo'sh emas, ≤ 8192 belgi |

Response 200 `AuthResultDto` (`user.role = "student"`). Xatolar: 400 `errors.InitData`; **403** — imzo/`auth_date`
("Telegram imzosi tasdiqlanmadi. Ilovani qaytadan oching."), hisob bog'lanmagan ("Hisob topilmadi — tyutoringizdan
taklif havolasini oling."), faol emas ("Hisobingiz faol emas. Tyutoringizga murojaat qiling."); 429.
Har muvaffaqiyatsiz urinish audit'ga `loginFailed` sifatida yoziladi.

#### POST `/api/auth/telegram/link` · AllowAnonymous · rate `auth` 10/min — v3.9

Talaba Mini App'ni birinchi marta ochganda (`/api/auth/telegram` → 403 "Hisob topilmadi…"): Telegram hisobini
HEMIS ID + parol (xodim o'rnatgan) bilan **bog'laydi va kiritadi**.

| Maydon     | Tip    | Majburiy | Validatsiya                                                    |
| ---------- | ------ | -------- | -------------------------------------------------------------- |
| `initData` | string | ha       | bo'sh emas, ≤ 8192 belgi (`/api/auth/telegram` bilan bir xil)  |
| `hemisId`  | string | ha       | bo'sh emas (format/qidiruv — `/api/auth/login` bilan bir xil)  |
| `password` | string | ha       | ≥ 8 belgi                                                      |

Response 200 `AuthResultDto` (`user.role = "student"`, `mustChangePassword` — login bilan bir xil; `true` bo'lsa mijoz
`change-password` ga yo'naltiradi). Keyingi ochilishlarda oddiy `POST /api/auth/telegram` ishlaydi. Shu Telegram id
bilan allaqachon bog'langan talaba qayta yuborsa — oddiy kirish (200, idempotent). Telefon `initData` da yo'q —
talabaning mavjud `phoneNumber` i o'zgarmaydi.

Tekshiruv tartibi: imzo → HEMIS ID/parol → faollik → rol → bog'lanish.

| Status | `detail`                                                                                     | Holat                                            |
| ------ | -------------------------------------------------------------------------------------------- | ------------------------------------------------ |
| 400    | `errors.InitData` / `errors.HemisId` / `errors.Password`                                     | validatsiya                                      |
| 403    | "Telegram imzosi tasdiqlanmadi. Ilovani qaytadan oching."                                    | imzo/`auth_date` yaroqsiz                        |
| 403    | "HEMIS ID yoki parol noto'g'ri."                                                             | HEMIS ID/parol noto'g'ri yoki parolsiz hisob     |
| 403    | "Hisobingiz faol emas. Tyutoringizga murojaat qiling."                                       | faol emas (xodimda "…Administratorga…")          |
| 403    | "Telegram faqat talaba hisobiga bog'lanadi."                                                 | admin/tyutor hisobi                              |
| 409    | "Bu Telegram akkaunti boshqa hisobga bog'langan."                                            | shu Telegram `user.id` boshqa hisobda            |
| 409    | "Bu hisobga boshqa Telegram akkaunti bog'langan."                                            | talaba allaqachon boshqa Telegram'ga bog'langan  |
| 429    |                                                                                              | rate limit                                       |

Audit: muvaffaqiyatda `telegramLinked` (+ `loggedIn`); har muvaffaqiyatsiz urinish (403/409) — `loginFailed`.

#### POST `/api/auth/refresh` · AllowAnonymous · rate `refresh` 60/min

Body `{ refreshToken: string }`. Response 200 `AuthResultDto` — **yangi** refresh token (eskisi `Revoke`, qayta
ishlatib bo'lmaydi). Xatolar: **403** — token yo'q/bekor qilingan/muddati o'tgan ("Sessiya muddati tugagan. Qaytadan
kiring.") yoki hisob faol emas; 429.

#### POST `/api/auth/logout` · Authenticated

Body `{ refreshToken: string }` + Bearer. Response **204** har doim (token topilmasa ham). Faqat o'z tokenini bekor qiladi.

#### POST `/api/auth/change-password` · Authenticated · rate `auth` 10/min — v3.8

| Maydon            | Tip    | Majburiy | Validatsiya                                                                                      |
| ----------------- | ------ | -------- | ------------------------------------------------------------------------------------------------ |
| `currentPassword` | string | ha       | bo'sh emas (`errors.CurrentPassword`); noto'g'ri yoki hisobda parol yo'q → 400 shu kalit bilan   |
| `newPassword`     | string | ha       | 8–128 belgi (tyutor paroli qoidasi), joriy paroldan farqli (`errors.NewPassword`)                |
| `refreshToken`    | string | yo'q     | joriy sessiyaning refresh tokeni — berilsa **saqlanadi**, qolgan barcha refresh tokenlar bekor   |

Har qanday rol (paroli bor). Response **204**: yangi parol, `mustChangePassword = false`, boshqa sessiyalar (refresh
tokenlar) bekor, audit `passwordChanged`. `refreshToken` yuborilmasa **barcha** refresh tokenlar bekor — access token
tugagach qayta login kerak bo'ladi, shuning uchun mijoz joriy `refreshToken` ni yuborsin. Xatolar: **400**
`errors.CurrentPassword` ("Joriy parol noto'g'ri.") / `errors.NewPassword`; 401; 429.

#### GET `/api/auth/me` · Authenticated

Response 200 `UserSummaryDto`. 401 (token yo'q), 404 (foydalanuvchi o'chirilgan).

---

### 2.2 Files — `FilesController`

#### GET `/api/files/{id}` · Authenticated

Response 200 — fayl oqimi, `Content-Type` bazadan, `Content-Disposition: attachment; filename="<asl nom>"`,
`Accept-Ranges`. **404** — fayl yo'q, diskda yo'q yoki ko'lamdan tashqarida. Ko'lam: shablon (`kind=template`) — hamma
autentifikatsiyalangan; admin — hammasi; talaba — o'zi yuklagan yoki o'z arizasi/kundaligi/ruxsati/**check-in
selfisiga** biriktirilgan; tyutor — ko'lamdagi talabalar fayllari.

`kind = checkInPhoto` (§3.1) fayllari uchun ega — selfi biriktirilgan `AttendanceEvent` ning talabasi (rad etilgan
urinishniki ham). Havola shakli o'zgarmagan: `"/api/files/<guid>"`, Bearer talab qiladi — oddiy `<img src>` ishlamaydi.

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
  companiesActive: number; // isActive korxonalar soni (holat bo'yicha, talabaga bog'liq emas)
  applicationsPending: number; // status = submitted
  applicationsOverdue: number; // submitted va 48 soatdan ko'p javobsiz
  contractsApproved: number; // approved + completed
  contractsRevision: number;
  contractsRejected: number;
  contractsMissing: number; // davri bor guruhdagi talaba, guruhning sukut bo'yicha davrida (§4.6) arizasi yo'q
  expectedToday: number; // bugun ish kuni bo'lgan (davom etayotgan, yopilmagan) davr guruhlaridagi talabalar
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
  facultyCode: string | null; /*tyutor fakultetlarining kodlari nom tartibida, ", " bilan: "AT, IM"*/
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
  attendancePct: number /*int; `period` davri boshidan kechagacha (davr oxiridan oshmaydi), maxraj = o'tgan ish kunlari × shu davrda arizasi tasdiqlangan talabalar*/;
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

`period` — guruhning **sukut bo'yicha davri** (§4.6: davom etayotgan → oxirgi tugagan → eng yaqin kelgusi); `status`
hisoblangan (`planned`/`active`/`closed`). Ikki davr oralig'ida tugagan (yoki yopilgan) davr qoladi — `attendancePct`
kelajakdagi bo'sh davrga o'tib 0 bo'lib qolmaydi. Davri yo'q guruhda `null`.

### 2.3.3 Tyutorlar — `AdminTutorsController` (`/api/admin/tutors`)

Hammasi `AdminOnly`. O'chirish (DELETE) endpoint'i **yo'q** — tyutor faqat faol emas qilinadi (`PATCH .../status`).
Semantika: tyutor **bir yoki bir nechta fakultetga** biriktiriladi (`faculties[]`, `TutorFaculty`; ro'yxatning
birinchisi — "asosiy" fakultet, u `User.facultyId` sifatida auth javobida/JWT'da qoladi) va o'z fakultetlari ichida
**ierarxik ko'lam** bilan biriktiriladi (`TutorScope`) — fakultet, kafedra, yo'nalish yoki guruh
darajasida, bir tyutorda bir nechta ko'lam bo'lishi mumkin (masalan 2 ta kafedra + 1 ta guruh, turli fakultetlardan ham). Ko'lam guruhlarga
**materializatsiya** qilinadi (`TutorAssignment` — `groups`): fakultet → fakultetdagi barcha faol guruhlar, kafedra →
undagi barcha yo'nalish/guruhlar, yo'nalish → undagi guruhlar, guruh → o'zi. Ko'lam ichida **keyin yaratilgan** guruh
avtomatik qamrab olinadi. **Kesishmaslik qoidasi:** ikki xil tyutorning faol ko'lamlari kesishmaydi (teng, ota yoki
bola — masalan A fakultetga, B shu fakultetdagi guruhga bo'lolmaydi) → 409. Bir tyutorning o'z tanlovlarida ota
tanlangan bo'lsa bolalari jimgina tashlab yuboriladi. Tarix saqlanadi (ajratilganda ko'lam ham, biriktiruv ham
o'chirilmaydi, faolsizlantiriladi).

#### GET `/api/admin/tutors` — `q`: ism, telefon, fakultetlaridan birining kodi/nomi; `&facultyId=<guid>` (ixtiyoriy filtr — tyutor fakultetlaridan **biri** mos bo'lsa)

```ts
interface FacultyRef {
  id: string;
  code: string;
  name: string;
}
interface TutorRow {
  id: string;
  fullName: string;
  phone: string | null /*E.164 xom*/;
  faculties: FacultyRef[]; /*biriktirilgan fakultetlar (kamida 1), nom bo'yicha tartib*/
  groups: string[]; /*faol biriktiruvlar — guruh nomlari (barcha fakultetlardan)*/
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
  faculties: FacultyRef[]; /*biriktirilgan fakultetlar (kamida 1), nom bo'yicha tartib*/
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
  facultyId: string; /*doim — ko'lam tugunining fakulteti (tyutor fakultetlaridan biri)*/
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
| `facultyIds`| guid[]         | ha       | bo'sh emas, har biri bo'sh guid emas, takror yo'q (`errors.FacultyIds`); hammasi mavjud va faol; **birinchisi — asosiy** fakultet |

Response 201 `TutorDetail` (`scopes: []`, `groups: []`; `faculties` — nom bo'yicha tartib). Xatolar: 400
`errors.FullName`/`errors.HemisId`/`errors.Phone`/`errors.Password`/`errors.FacultyIds` (bo'sh ro'yxat: "Kamida bitta
fakultet tanlang."; bo'sh guid: "Fakultet ko'rsatilmagan."; takror: "Fakultet takrorlangan."); **404** (`detail`:
"Fakultet topilmadi." — ro'yxatdagi birortasi yo'q/o'chirilgan); **409** — HEMIS ID band (`detail`: "Bu HEMIS ID bilan
foydalanuvchi mavjud."), telefon band (`detail`: "Bu telefon raqami bilan foydalanuvchi mavjud."), fakultet faol emas
(`detail`: "Fakultet faol emas: {nom}" — ro'yxat tartibida birinchi uchragani). Yangi tyutor darhol
`POST /api/auth/login` (hemisId + password) bilan kira oladi; auth javobidagi `user.facultyId` — asosiy fakultet.

#### PUT `/api/admin/tutors/{id}` · 200

Body `{ fullName, phone?, facultyIds }` (validatsiya — POST bilan bir xil; `hemisId` va parol bu yerdan
o'zgartirilmaydi). Fakultetlar to'plami **almashtiriladi**: qo'shish erkin (ko'lam/biriktiruvlarga tegilmaydi),
olib tashlash — faqat o'sha fakultetda tyutorning faol ko'lami bo'lmasa. Response 200 `TutorDetail`. Xatolar: 400;
**404** (tyutor yoki fakultetlardan biri topilmadi); **409** — olib tashlanayotgan fakultetda faol ko'lam bor (`detail`:
"{Fakultet nomi} fakultetida tyutorga ko'lam biriktirilgan — avval uni ajrating."), fakultet faol emas (`detail`:
"Fakultet faol emas: {nom}"), telefon band. Xato bo'lsa hech narsa yozilmaydi.

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
shu darajadagi tugun: fakultet/kafedra/yo'nalish/guruh id'si; tugun **tyutor fakultetlaridan biriga** tegishli bo'lishi
shart — `faculty` darajasida `id` `faculties[]` dan biri; turli fakultetlardan ko'lamlar aralash bo'lishi mumkin). Faol ko'lamlar to'plamini **almashtiradi**: ro'yxatda bo'lmaganlar faolsizlantiriladi (yozuv qoladi),
ilgari ajratilgan tugun qaytsa — o'sha yozuv qayta faollashadi (`scopes[].id` o'zgarmaydi), yangilari yaratiladi. So'ng
guruh biriktiruvlari sinxronlanadi (`groups`): yangilari **faol `AcademicYear`** bilan yaratiladi, qaytganlari qayta
faollashadi (`assignmentId` o'zgarmaydi), ortiqchalari faolsizlantiriladi. Ota tanlangan bo'lsa bolalari jimgina
tashlanadi (masalan `[direction X, group X.1]` → faqat `direction X`). Tyutor faol bo'lmasa ham ruxsat. Response 200
`TutorDetail`.

Xatolar (birinchi uchragani): **404** tyutor (`detail`: "Tyutor topilmadi."); **400** `errors.Scopes[i].Id` (bo'sh guid) /
`errors.Scopes[i].Level` yoki `DomainException` — tugun topilmadi (`detail`: "{Daraja} topilmadi." — daraja: `Fakultet`/
`Kafedra`/`Yo'nalish`/`Guruh`; o'chirilgan tugun ham "topilmadi"), faol emas (`detail`: "{Daraja} faol emas: {nom}"),
tyutor fakultetlaridan biriga tegishli emas (`detail`: "{Daraja} tyutor fakultetiga tegishli emas: {nom}"; zanjir
Group → Direction → Department → Faculty); **409** — boshqa tyutorning faol ko'lami bilan kesishadi (tekshiruv tyutorning
barcha fakultetlari bo'yicha; `detail`:
"{nom} ({daraja}) {tyutor FISH} tyutoriga biriktirilgan." — `nom`/`daraja` kesishgan **boshqa** tyutor ko'lamining nomi
va darajasi kichik harf bilan: `fakultet`/`kafedra`/`yo'nalish`/`guruh`, masalan "412-22 (guruh) Nodira Saidova tyutoriga
biriktirilgan."), yangi biriktiruv kerak-u faol o'quv yili yo'q (`detail`: "Faol o'quv yili yo'q."; faqat ajratish bo'lsa
o'quv yili talab qilinmaydi). Xato bo'lsa hech narsa yozilmaydi.

#### GET `/api/admin/tutors/{id}/scope-tree` · 200

Response `TutorScopeTree[]` — tyutorning **har bir fakulteti** uchun bittadan daraxt (`faculties[]` bilan bir xil
tartib — fakultet nomi bo'yicha), har birida faqat **faol** kafedra/yo'nalish/guruhlar, nom bo'yicha tartib (guruh: kurs,
nom). **404** tyutor topilmasa.

```ts
type TutorScopeTreeResponse = TutorScopeTree[];
interface TutorScopeTree {
  id: string; /*fakultet — faculties[i].id*/
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
  company: string | null /*v3.14 — faqat AKTIV korxona nomi (§4.7); aktivi bo'lmasa null, oldingi korxonaga fallback yo'q*/;
  attendancePct: number /*int*/;
  suspiciousDays: number;
  telegramLinked: boolean;
  status: AdminStudentStatus; /*!telegramLinked → unlinked; suspiciousDays≥2 || (elapsed>0 && pct<70) → flagged; aks holda active*/
}
```

#### GET `/api/admin/students/import/template` · 200

To'ldirish uchun **`.xlsx` shablon** (`Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`,
`Content-Disposition: attachment; filename="talabalar-import-shablon.xlsx"`). Uchta varaq:

| Varaq | Mazmuni |
|---|---|
| **Talabalar** | Faqat sarlavha qatori: `FISH *` · `HEMIS ID *` · `Guruh *` · `Telefon`. HEMIS ID va telefon ustunlari **matn** formatida (uzun raqam `3,42201E+11` ga aylanib ketmasin) |
| **Yo'riqnoma** | To'ldirish qoidalari va namuna qatorlar |
| **Guruhlar** | Mavjud **faol** guruhlar: `Guruh · Kurs · Yo'nalish · Kafedra · Fakultet` (faol o'quv yilidagi, zanjiri to'liq faol bo'lganlari) |

Endpoint `AdminOnly`, ya'ni oddiy `<a href>` bilan ochilmaydi (401) — frontend `shared/files/downloadAuthFile`
orqali token bilan oladi (§6.7 bilan bir xil sabab).

#### POST `/api/admin/students/import` · `multipart/form-data` · 200 · 400

Forma: **`file`** — shablon bo'yicha to'ldirilgan `.xlsx` (≤ 5 MB, ≤ 1000 ma'lumot qatori).

Fayl **ustun tartibi bo'yicha emas, sarlavha nomlari bo'yicha** o'qiladi: varaq "Talabalar" deb nomlangan bo'lsa
o'sha, aks holda birinchi varaq; sarlavha qatori birinchi 20 qator ichidan qidiriladi (tepada sarlavha/izoh bo'lishi mumkin);
ustun nomi normallashtiriladi (faqat harf-raqam, katta-kichik farqsiz), shuning uchun `FISH`, `F.I.SH *`, `FIO`
yoki `HEMIS ID`, `hemisid` — bir xil. Tanilmagan ustunlar (masalan `Kurs`, `T/R`) e'tiborsiz qoldiriladi —
**kurs guruhdan olinadi**. Butunlay bo'sh qatorlar tashlab yuboriladi, raqam sifatida kiritilgan HEMIS ID/telefon
butun son ko'rinishiga keltiriladi.

```ts
interface StudentImportError {
  row: number    /*Exceldagi qator raqami*/;
  column: string /*"FISH" | "HEMIS ID" | "Guruh" | "Telefon"*/;
  value: string | null;
  message: string;
}

interface StudentImportResult {
  totalRows: number /*o'qilgan ma'lumot qatorlari*/;
  created: number;
  failed: number    /*created + failed === totalRows*/;
  errors: StudentImportError[] /*bir qatorda bir nechta xato bo'lsa — bir nechta yozuv*/;
}
```

**Qisman import:** xato qatorlar tashlab yuboriladi, to'g'rilari saqlanadi — javob doimo **200**
(hatto hammasi xato bo'lsa ham; `created: 0`). Qator xatolari:

| Ustun | Xato |
|---|---|
| FISH | `"FISH bo'sh."` · `"FISH 200 ta belgidan oshmasligi kerak."` |
| HEMIS ID | `"HEMIS ID bo'sh."` · `"HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak."` · `"Bu HEMIS ID faylda takrorlanmoqda."` · `"Bu HEMIS ID bilan talaba allaqachon bor."` |
| Guruh | `"Guruh bo'sh."` · `"Bunday faol guruh yo'q — shablonning «Guruhlar» varag'idan tanlang."` · bir nechta yo'nalishda bir xil nomli guruh bo'lsa — `"Bu nomli guruh bir nechta yo'nalishda bor (…) — nomini aniqlashtiring."` |
| Telefon | `"Telefon raqami noto'g'ri. Namuna: +998901234567"` · `"Bu telefon raqami faylda takrorlanmoqda."` · `"Bu telefon raqami bilan foydalanuvchi bor."` |

**400** (butun faylga tegishli): fayl tanlanmagan/bo'sh, hajmi 5 MB dan katta, kengaytmasi `.xlsx` emas
(validatsiya — `errors` bilan); fayl o'qilmadi (`"Faylni o'qib bo'lmadi — u haqiqiy .xlsx (Excel) fayli bo'lishi kerak."`),
sarlavha qatori topilmadi, qatorlar 1000 tadan ko'p.

Yaratilgan talaba: `User` (rol `student`, parolsiz, Telegramsiz — ro'yxatda holati **`unlinked`**), fakulteti
guruh zanjiridan olinadi; `StudentProfile` (`hemisId`, guruh, holat `active`). Import audit jurnaliga bitta
`studentsImported` yozuvi bilan tushadi (`changes`: fayl nomi va sanoqlar). Taklif tokeni hozircha berilmaydi.

#### POST `/api/admin/students/assign-company` · 200 · 400 · 404 · 409

Talabalar ro'yxatida belgilangan (checkbox) talabalarni korxonaga **biriktiradi**: har biriga
**tasdiqlangan** ariza yaratiladi (talaba ariza bermaydi, tyutor moderatsiyasi talab qilinmaydi;
qaror izohi — `"Admin tomonidan biriktirildi."`, radius korxonanikidan olinadi).

```ts
// so'rov
{ studentIds: string[] /*1..200*/, companyId: string }

interface AssignCompanyError { studentId: string; studentName: string; message: string }
interface AssignCompanyResult {
  total: number; assigned: number; skipped: number;  // assigned + skipped === total
  companyName: string;
  errors: AssignCompanyError[];
}
```

**404** — korxona yo'q; **409** — korxona faol emas; **400** — ro'yxat bo'sh yoki 200 tadan ko'p.
Alohida talaba sabab bilan tashlab yuboriladi (qisman bajarilish): `"Allaqachon shu korxonaga biriktirilgan."` ·
`"Boshqa korxonaga biriktirilgan: <nom>."` · `"Guruhiga faol amaliyot davri biriktirilmagan."` ·
`"Ko'rib chiqilmagan arizasi bor — avval tyutor qaror qabul qilsin."` · `"Talaba hisobi faol emas."` ·
`"Talaba topilmadi."`

#### POST `/api/admin/students/{id}/company` · 200 · 400 · 404 · 409

Talaba profilidan **bitta** talabani korxonaga biriktirish yoki **boshqa korxonaga o'tkazish**.

```ts
// so'rov
{ companyId: string; comment?: string | null /*≤ 500*/ }
// javob 200 — AdminStudentDetail (GET /api/admin/students/{id} — periodId'siz, sukut davri — bilan aynan bir xil)
```

- Biriktirish davri — talaba guruhining **ariza davri** (`enrollment`: davom etayotgan → eng yaqin kelgusi), `assign-company`
  bilan bir xil. Javob esa **sukut davri** (`default`: davom etayotgan → oxirgi tugagan → kelgusi) bo'yicha — odatda ular
  bir xil; faqat davom etayotgan davr yo'q va tugagan + kelgusi davr bo'lsa, javobda tugagan davr ko'rinadi (yangi korxona
  kelgusi davr uchun — `?periodId=` bilan GET orqali ko'rinadi).
- Talabaning shu davrdagi ochiq arizasi (`submitted` / `revisionNeeded` / `approved`) bo'lsa — u **`transferred`**
  holatiga o'tadi (qaror maydonlari: kim/qachon + izoh, izoh berilmasa `"Boshqa korxonaga o'tkazildi: <nom>."`),
  keyin yangi korxonaga **tasdiqlangan** ariza yaratiladi (izoh — `comment` yoki `"Admin tomonidan biriktirildi."`).
  Ochiq ariza yo'q bo'lsa — shunchaki tasdiqlangan ariza (birinchi biriktirish).
- Tarix (davomat, kundalik, check-in hodisalari) eski ariza/korxonaga bog'langan holda qoladi; check-in (QR va geofence)
  darhol yangi korxona bo'yicha ishlaydi — eski korxona QR'i `qrInvalid` bilan rad etiladi.
- Xatolar: **404** — talaba yoki korxona yo'q; **409** — `"Korxona faol emas — avval uni faollashtiring."` ·
  `"Talaba hisobi faol emas."` · `"Talaba guruhiga faol amaliyot davri biriktirilmagan."` ·
  `"Talaba allaqachon shu korxonaga biriktirilgan."` · (davrdagi ariza `completed` bo'lsa) `"Talabaning bu davrdagi
  amaliyoti yakunlangan — korxonani o'zgartirib bo'lmaydi."`; **400** — `companyId` bo'sh (`errors.CompanyId`),
  `comment` 500 belgidan uzun (`errors.Comment`).
- Audit: `studentCompanyReassigned` (`entityName: "User"`, `entityId` — talaba; `changes`: `fromCompanyId`, `fromCompany`,
  `fromStatus`, `toCompanyId`, `toCompany`, `periodId`; `reason` — izoh).
- `transferred` ariza hech qayerda "joriy korxona" sifatida olinmaydi: talaba ilovasi, tyutor/admin ro'yxatlari, statistika
  (`withCompanyCount`, `pendingApplicationsCount`), korxona talabalar soni faqat `approved`/`completed` ni hisoblaydi;
  tyutor moderatsiya navbati (`GET /api/tutor/applications` `status`siz) `transferred` ni ko'rsatmaydi.
  v3.15 dan korxona talabalar ro'yxati (`GET /api/admin/companies/{id}/students`) faqat aktiv talabalarni ko'rsatadi —
  o'tkazilgan talaba eski korxonada **chiqmaydi**, yangisida chiqadi (§6.21).

#### GET `/api/admin/students/{id}?periodId=` · 200 · 404

404 — talaba yo'q (`detail`: `"Talaba topilmadi (id: …)."`) yoki `periodId` talabaga tegishli emas
(`"Amaliyot davri topilmadi."`). `periodId` / `periods` / `selectedPeriodId` — tyutornikidek (§2.5). Umumiy bloklar tyutor profili bilan **bir xil**
(`GET /api/tutor/students/{id}`, §2.5 — ayni handler hisoblaydi), admin ko'lami cheklovsiz bo'lgani uchun
har qanday talaba ko'rinadi. Farqi — quyidagi qo'shimcha maydonlar.

```ts
interface AdminStudentTutor {
  id: string /*tyutorning User.Id — `/admin/tutors/{id}` ga havola*/;
  fullName: string;
  phone: string | null;
}

interface AdminStudentDetail extends /* TutorStudentDetail maydonlari, §2.5 */ {
  groupId: string;
  department: string /*kafedra nomi — tyutor profilida yo'q*/;
  adminStatus: AdminStudentStatus /*ro'yxatdagi holat bilan bir xil qoida (`AdminStudentStatusRule`)*/;
  telegramLinked: boolean;
  tutor: AdminStudentTutor | null /*guruhga biriktirilgan faol tyutor; bir nechta bo'lsa FISH bo'yicha birinchisi*/;
}
```

`adminStatus` ro'yxatdagi (`StudentRow.status`) bilan **bir qoidadan** hisoblanadi, lekin davomat foizi
profil statistikasidan olinadi (`attendance.attendancePct`, `attendance.totalDays`) — ro'yxatdagi
`elapsed` asosidagi yaxlitlash bilan bir necha foizga farq qilishi mumkin.

#### GET `/api/admin/students/{id}/attendance?periodId=&from=&to=` · 200 · 400 · 404

#### GET `/api/admin/students/{id}/diaries?periodId=` · 200 · 404

Ikkalasi ham tyutornikidek (`StudentAttendanceDay[]`, `TutorDiaryEntry[]` — §2.5), shu jumladan chegaralar:
davr — `periodId` (berilmasa sukut bo'yicha davr), oraliq berilmasa davr boshidan `min(bugun, davr oxiri)` gacha va
davr chegaralariga qisiladi, teskari yoki 400 kundan uzun oraliq → 400, davr bo'lmasa — bo'sh massiv,
begona `periodId` → 404.

#### POST `/api/admin/students/{id}/password` · 204 · 400 · 404 — v3.8

Body `{ password: string }` (8–128 belgi, `errors.Password`). Talabaga brauzer orqali (HEMIS ID + parol) kirish uchun
**vaqtinchalik** parol o'rnatiladi: `mustChangePassword = true` (talaba birinchi kirishda o'zi almashtiradi), talabaning
**barcha refresh tokenlari bekor** (Telegram sessiyasi ham — qayta kiradi), audit `studentPasswordSet`.
Talaba topilmasa (yoki id talabaniki emas) → **404**. Profilda `hasPassword` (§2.5 `TutorStudentDetail`).

#### POST `/api/admin/diaries/{id}/review` · 200 · 400 · 404 · 409

Tyutornikidek (`POST /api/tutor/diaries/{id}/review`, §2.5) — **ayni buyruq va qoidalar**:
`approve` (ball ixtiyoriy) · `score` (ball 1–5 majburiy) · `rewrite` (izoh majburiy);
ko'rib chiqilgan yozuvni qayta baholash → **409**. Tekshiruvchi sifatida joriy foydalanuvchi
(admin) yoziladi, audit jurnaliga `DiaryReviewed` tushadi. Admin ko'lami cheklovsiz.

Javob — `TutorDiaryEntry`. Frontend'da ikkala rol bitta komponentdan foydalanadi
(talaba profilidagi kun oynasi), yo'l `area: 'tutor' | 'admin'` bilan tanlanadi.

#### GET `/api/admin/companies` — `q`: nom, STIR, manzil

```ts
interface CompanyRow {
  id: string;
  name: string;
  tin: string /*9 raqam xom "304512889"*/;
  activity: string;
  address: string;
  radiusM: number;
  students: number /*v3.15 — HOZIR shu korxonada aktiv amaliyot o'tayotgan talabalar (§4.7)*/;
  suspiciousDays: number /*v3.15 — shu aktiv talabalarning aktiv davrdagi shubhali kunlari*/;
  isActive: boolean;
  maxStudents: number /*amaldagi `maxStudentsPerCompany` sozlamasi — hamma qatorda bir xil*/;
  overLimit: boolean /*students > maxStudents (aktiv son)*/;
  flag: CompanyFlag | null; /*ustuvorlik: suspicious → tooManyStudents → largeRadius → null*/
}
```

Bayroq (`CompanyFlags.Resolve`) — **aynan shu tartibda**, birinchi mos kelgani qaytadi:
`suspiciousDays ≥ 3` → `suspicious`; `overLimit` → `tooManyStudents`; `radiusM > 500` → `largeRadius`; aks holda `null`.
`maxStudents` — global `maxStudentsPerCompany` sozlamasi (§3.1, default `10`), har qatorga takrorlanadi.

#### GET `/api/admin/companies/{id}` · 200 · 404

404 — korxona yo'q yoki o'chirilgan (`detail`: `"Korxona topilmadi (id: …)."`). Shakl tyutornikidek
(`GET /api/tutor/companies/{id}`, §2.5) — farq faqat ko'lamda: admin uchun `students === totalStudents`.

```ts
interface CompanyDetail {
  id: string;
  name: string;
  tin: string;
  activity: string;
  address: string;
  lat: number;
  lng: number;
  radiusM: number;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  isActive: boolean;
  students: number /*v3.15 — so'rovchi ko'lamidagi AKTIV talabalar (§4.7); admin uchun = totalStudents*/;
  totalStudents: number /*v3.15 — butun tizim bo'yicha AKTIV talabalar — STIR nazorati shu songa tayanadi*/;
  suspiciousDays: number /*v3.15 — aktiv talabalarning aktiv davrdagi shubhali kunlari (ko'lam kesimida)*/;
  maxStudents: number;
  overLimit: boolean /*totalStudents > maxStudents*/;
  flag: CompanyFlag | null;
  /** v3.15 — faqat HOZIR davom etayotgan ochiq davr(lar): shu davrda shu korxonadagi aktiv talabalar soni (ko'lam kesimida).
   *  students === 0 bo'lgan davr kirmaydi; yopilgan/tugagan/kelgusi davrlar chiqmaydi (bo'sh massiv — normal). startDate desc, keyin nom. */
  periods: { id: string; name: string; startDate: string; endDate: string; students: number }[];
}
```

#### POST `/api/admin/companies` · 201 · 400 · 409

Korxonani **admin oldindan** kiritadi — talaba keyin faqat STIR yozadi (§2.7, §2.6).

```ts
interface CompanyInput {
  name: string;            // ≤ 200
  tin: string;             // 9 raqam (bo'shliq/tire tozalanadi)
  activity: string;        // ≤ 200
  address: string;         // ≤ 500
  lat: number;             // -90..90
  lng: number;             // -180..180
  radiusM?: number | null; // 50..1000; berilmasa `geofenceRadius` sozlamasidan
  supervisorName: string;
  supervisorPhone: string; // +998901234567 (9 xonali ham qabul qilinadi)
  mentorName?: string | null;
  mentorPhone?: string | null;
}
```

Javob — `CompanyDetail` (yuqoridagi shakl). **409** — STIR band: `"STIR 123456789 bilan korxona allaqachon mavjud."`
(arxivlangan korxonaning STIR'i qayta ishlatilishi mumkin). **400** — `errors` kalitlari kichik harf bilan:
`name · tin · activity · address · lat · lng · radiusM · supervisorName · supervisorPhone · mentorPhone`.

#### PUT `/api/admin/companies/{id}` · 200 · 400 · 404 · 409

Body — `CompanyInput` (id route'dan). Radius o'zgarsa audit jurnaliga `radiusChanged` ham tushadi.

#### PATCH `/api/admin/companies/{id}/status` · 200 · 404

`{ isActive }` → `CompanyDetail`. **Faolsizlantirilgan korxona STIR qidiruvida ko'rinmaydi**
(`GET /api/companies/lookup` → 404), ya'ni talaba uni tanlay olmaydi; mavjud arizalar va davomat buzilmaydi.

#### DELETE `/api/admin/companies/{id}` · 204 · 404 · 409

Soft delete (arxivlash), ikki qavat himoya:
- korxona hali **faol** bo'lsa → 409 `"Avval korxonani faolsizlantiring — keyin o'chirish mumkin."`
- unga qoralamadan boshqa **arizasi bor** talaba bo'lsa (tarix, istalgan davr — himoya o'zgarmagan) → 409
  (davomat/kundalik tarixi korxonaga bog'liq). Xabar (v3.15) UI dagi son bilan mos:
  - hozir aktiv talabalar bor (N = `students`) → `"Korxonaga N ta talaba biriktirilgan — uni o'chirib bo'lmaydi. …"`;
  - aktiv talaba yo'q, faqat tarix → `"Korxonada amaliyot tarixi (arizalar) bor — uni o'chirib bo'lmaydi, nofaol qiling."`

#### GET `/api/admin/companies/import/template` · 200 · POST `/api/admin/companies/import` · 200 · 400

Ko'p korxonani bir faylda yuklash. Shablon — `.xlsx`, ikki varaq: **"Korxonalar"** (sarlavha qatori:
`Nomi *` · `STIR *` · `Faoliyat turi *` · `Manzil *` · `Kenglik (lat) *` · `Uzunlik (lng) *` · `Radius (m)` ·
`Rahbar FISH *` · `Rahbar telefoni *` · `Mentor FISH` · `Mentor telefoni`) va **"Yo'riqnoma"**.
Import talabalar importi bilan **bir xil qoidaga** bo'ysunadi (§2.3, sarlavha nomlari bo'yicha o'qish,
≤ 1000 qator, ≤ 5 MB, qisman import) va **`ImportResult`** qaytaradi. Qator xatolari: STIR bo'sh/noto'g'ri/takror
(faylda yoki bazada), majburiy maydon bo'sh, kenglik/uzunlik oralig'idan tashqarida, radius 50–1000 emas,
telefon formati. Yuklangan korxona darhol **faol** bo'ladi va STIR qidiruvida chiqadi.

#### GET `/api/admin/companies/{id}/students` · 200 · 404

Sahifalanmagan massiv. **v3.15:** ro'yxatga faqat shu korxonada **hozir aktiv amaliyot o'tayotgan** talabalar kiradi —
aktiv korxonasi (§4.7) aynan shu korxona. Yopilgan/tugagan/kelgusi davr arizalari, `submitted`/`revisionNeeded`/
`rejected`/`transferred`/`completed` — **chiqmaydi**. Ro'yxat uzunligi = `CompanyRow.students` = `CompanyDetail.students`.
Aktiv talaba yo'q → `[]` (200). Tartib: **FISH**, keyin `hemisId`. 404 — korxona yo'q.

```ts
interface CompanyStudent {
  studentId: string /*User.Id*/;
  name: string;
  hemisId: string;
  group: string;
  course: number;
  faculty: string;
  tutorName: string | null /*guruhga biriktirilgan faol tyutor; bir nechta bo'lsa alifbo bo'yicha birinchisi*/;
  applicationStatus: ApplicationStatus /*v3.15 — amalda doim "approved" (maydon shakli o'zgarmadi)*/;
  periodName: string | null /*v3.15 — aktiv davr nomi*/;
  attendancePct: number /*1 kasr*/;
  attendedDays: number;
  totalDays: number;
  diaryCount: number;
  state: StudentState /*'active' | 'redFlag' | 'suspicious'*/;
  suspiciousCount: number;
}
```

`attendancePct` / `attendedDays` / `totalDays` / `diaryCount` / `suspiciousCount` — talabaning **aktiv davri** bo'yicha
(v3.15; `periodName` bilan bir xil; `StudentStatsCalculator`).
`state` — o'sha qoida: `totalDays>0 && pct<70` → `redFlag`; `suspiciousCount≥1` → `suspicious`; aks holda `active`.

#### GET `/api/admin/companies/{id}/checkin-qr` · 200 · 404 — v3.7

Korxonada osiladigan **check-in QR kodi** (chop etish uchun). QR ichidagi satr — `payload`; frontend uni o'zi QR
rasmga aylantiradi. Korxona yo'q (yoki arxivlangan) → 404.

```ts
interface CompanyCheckInQrDto {
  companyId: string;
  companyName: string;
  payload: string /*"AMLQR:1:<32 belgili kichik hex token>" — QR ichiga aynan shu satr*/;
  rotatedAt: string /*ISO — token yaratilgan/oxirgi almashtirilgan vaqt*/;
}
```

Format: `AMLQR:1:{token}` — prefiks `AMLQR:`, versiya `1`, token 32 ta `[0-9a-f]` (128 bit, `RandomNumberGenerator`).
Har korxona yaratilganda (qo'lda, Excel importda) avtomatik token oladi; v3.7 migratsiyasi mavjud korxonalarga ham
token berdi.

#### POST `/api/admin/companies/{id}/checkin-qr/rotate` · 200 · 404 — v3.7

Tanasiz. Yangi token yaratadi → 200 `CompanyCheckInQrDto` (yangi `payload`, `rotatedAt`). **Eski QR darhol
yaroqsiz** — osilgan qog'ozni almashtirish kerak (eski QR bilan urinish → 409 `qrInvalid`). Audit:
`companyQrRotated` (`entityName: "Company"`, `entityId` — korxona; token auditga yozilmaydi).

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
`dailyReportRequired` bool `true` · `minReportLength` int chars `150` 0–5000 · **`diaryPdfRequired`** bool `false` ·
`checkInWindow` int min `90` 15–480 ·
**`checkinPhotoRequired`** bool **`true`** (v3.7 gacha `false`) · **`checkinQrRequired`** bool `true` (v3.7) ·
**`maxStudentsPerCompany`** int `10` 1–200.
Ro'yxat tartibi — shu; `checkinPhotoRequired`/`maxStudentsPerCompany` v3 da qo'shildi (§6); `diaryPdfRequired` — keyinroq
(`minReportLength` dan keyin); `checkinQrRequired` — v3.7 (§6.13).
Bazada yo'q kalit default bilan qaytadi (`updatedAt: null`).

| Kalit                   | Ta'sir                                                                                                            |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `checkinPhotoRequired`  | `true` bo'lsa `POST /api/student/checkin` va `/checkout` selfisiz qabul qilinmaydi → 400 `errors.Photo` (§2.6); talabaga `TodayDto.checkin.photoRequired` |
| `checkinQrRequired`     | `true` bo'lsa check-in/check-out `qr` siz qabul qilinmaydi → 400 `errors.Qr` (§2.6); talabaga `TodayDto.checkin.qrRequired`. `false` bo'lsa ham **yuborilgan** `qr` tekshiriladi |
| `diaryPdfRequired`      | `true` bo'lsa `POST /api/student/diary` kamida bitta PDF'siz qabul qilinmaydi → 400 `errors.Files`; talabaga `TodayDto.diary.pdfRequired` |
| `maxStudentsPerCompany` | `CompanyRow`/`CompanyDetail`/`TutorCompany` dagi `maxStudents` va `overLimit`; bayroq `tooManyStudents` (§2.3)      |

---

### 2.3.4 Amaliyot davrlari — `AdminPracticePeriodsController` (`/api/admin/practice-periods`)

Admin davr yaratadi, sanalarini o'zgartiradi, guruhlarni biriktiradi/ajratadi, yopadi va o'chiradi. Ro'yxat
**sahifalanmagan** (davrlar o'nlab). Sanalar `YYYY-MM-DD`, vaqt `HH:mm`.

```ts
type PracticePeriodStatus = 'planned' | 'active' | 'closed';

interface PracticePeriodListItem {
  id: string;
  name: string;
  startDate: string;          // YYYY-MM-DD
  endDate: string;
  status: PracticePeriodStatus;
  groupsCount: number;
  studentsCount: number;      // biriktirilgan guruhlardagi faol (StudentStatus.active) talabalar
  createdAt: string;          // ISO datetime
}

interface PracticePeriodGroup {
  id: string;                 // StudentGroup id
  code: string;               // "412-22"
  course: number;
  studentsCount: number;
  facultyId: string;  facultyName: string;
  departmentId: string; departmentName: string;
  directionId: string;  directionName: string;
}

interface PracticePeriodDetail extends PracticePeriodListItem {
  dailyStart: string;         // "09:00"
  dailyEnd: string;           // "17:00"
  workDays: string;           // "1,2,3,4,5,6"
  requiredDays: number;
  dailyReportRequired: boolean;
  groups: PracticePeriodGroup[]; // code bo'yicha tartiblangan
}

interface PracticePeriodCreate {
  name: string; startDate: string; endDate: string; groupIds: string[];
  dailyStart?: string | null; // "HH:mm" (Toshkent); yo'q/null → "09:00"
  dailyEnd?: string | null;   // "HH:mm"; yo'q/null → "17:00"
  workDays?: string | null;   // "1,2,3,4,5" (1=Du … 7=Ya, kamida bitta); yo'q/null → global `workDays` sozlamasi
}
interface PracticePeriodUpdate {
  name: string; startDate: string; endDate: string;
  dailyStart?: string | null; // yo'q/null → o'zgarmaydi
  dailyEnd?: string | null;   // yo'q/null → o'zgarmaydi
  workDays?: string | null;   // yo'q/null → o'zgarmaydi
}
interface PracticePeriodGroupsUpdate { groupIds: string[]; } // to'liq ro'yxat (set semantikasi)
```

**Holat (`status`) — hisoblanadi**: `closed` — faqat `/close` orqali; aks holda `startDate > bugun` (Toshkent) →
`planned`, qolgani → `active` (tugash sanasi o'tgan, lekin yopilmagan davr ham `active`). Bazada saqlanadigan
`PracticePeriod.Status` — hayot sikli: admin yaratgan davr darhol `Active` (ochiq) saqlanadi, `/close` → `Closed`.
Talaba/tyutor oqimlari qaysi davrni ishlatishini sanadan hisoblaydi (§4.6, v3.5): yopilgan davrlar tarix va statistika
uchun yuklanadi, check-in va ariza uchun emas; boshlanmagan davrda check-in rad etiladi, ariza esa oldindan topshiriladi.
Bitta guruh sanalari kesishmaydigan bir nechta ochiq davrga biriktirilishi mumkin (kesishsa → 409).

#### GET `/api/admin/practice-periods?status=planned|active|closed` · 200 · 400

`PracticePeriodListItem[]`, `startDate` kamayish tartibida (keyin nom). `status` ixtiyoriy, hisoblangan holat bo'yicha;
noma'lum qiymat → 400 (ASP.NET, §1.7). O'chirilgan davrlar chiqmaydi.

#### GET `/api/admin/practice-periods/{id}` · 200 · 404

`PracticePeriodDetail`. 404 — yo'q yoki o'chirilgan (`detail`: "Amaliyot davri topilmadi."). Keyin o'chirilgan guruh
ham tafsilotda ko'rinadi (tarix).

#### POST `/api/admin/practice-periods` · 201 · 400 · 409

Body `PracticePeriodCreate` → 201 `PracticePeriodDetail` (+ `Location: /api/admin/practice-periods/{id}`).
- Validatsiya (400 `errors`): `Name` trim 1–200; `StartDate`/`EndDate` majburiy, `EndDate >= StartDate`;
  `GroupIds` kamida 1 ta, bo'sh GUID yo'q (takrorlar olib tashlanadi).
- **Jadval** (v3.12, ixtiyoriy): `DailyStart`/`DailyEnd` — `HH:mm` (aks holda "Vaqtni HH:mm formatida kiriting.");
  ikkalasi berilsa `DailyEnd > DailyStart` → aks holda `errors.DailyEnd` ("Ish tugash vaqti boshlanish vaqtidan keyin
  bo'lishi kerak."); `WorkDays` — 1..7 raqamlar vergul bilan (noto'g'ri → `errors.WorkDays` "Ish kunlari 1 (Dushanba)
  dan 7 (Yakshanba) gacha bo'lgan raqamlar ro'yxati bo'lishi kerak."), bo'sh satr → `errors.WorkDays` ("Kamida bitta
  ish kunini tanlang."). Yangi soatlar davrning kechikish/check-in oynasi daqiqalari bilan birga tekshiriladi
  (`CheckInRules`): bittasi yuborilgan bo'lsa ikkinchisi — standart/joriy qiymat; check-in oynasi ish tugashigacha
  sig'masa yoki tugash boshlanishdan oldin bo'lsa → 400 `errors.DailyEnd` (domain xabari, masalan "Check-in oynasi ish
  tugashidan oldin yopilishi kerak.").
- Guruh topilmasa yoki faol emas → 400 `errors.GroupIds` (`detail`: "N ta guruh topilmadi." / "Faol bo'lmagan
  guruhlar: 412-22.").
- Joriy (faol) o'quv yili yo'q → 400 (`detail`: "Joriy (faol) o'quv yili yo'q — avval o'quv yilini faollashtiring.").
- **Ustma-ust** → 409 (pastda).
- Nusxalanadigan qiymatlar (keyin sozlama o'zgarsa davr o'zgarmaydi): `lateTolerance`, `checkInWindow`,
  `autoCheckout`, `dailyReportRequired` va (body'da yo'q bo'lsa) `workDays` — global sozlamalardan (§2.3 settings);
  `dailyStart`/`dailyEnd` body'da yo'q bo'lsa — standart `09:00`/`17:00` (sozlama kaliti yo'q).
  `requiredDays` = `startDate..endDate` dagi ish kunlari (yakuniy `workDays` bo'yicha, bayramlarsiz).
- O'quv yili body'da yuborilmaydi — joriy faol o'quv yili olinadi.

#### PUT `/api/admin/practice-periods/{id}` · 200 · 400 · 404 · 409

Body `PracticePeriodUpdate` (`id` route'dan) → `PracticePeriodDetail`.
- Yopilgan davr → 409 ("Yopilgan davrni tahrirlab bo'lmaydi.").
- `active` davrda `startDate` o'zgarsa → 400 ("Faol davrning boshlanish sanasini o'zgartirib bo'lmaydi.");
  `endDate` uzaytiriladi/qisqartiriladi, lekin **bugundan oldin emas** → aks holda 400.
- `planned` davrda ikkala sana ham o'zgaradi (faqat `endDate >= startDate`).
- Sana o'zgarsa: ustma-ust qayta tekshiriladi (409).
- **Jadval** (v3.12, ixtiyoriy): `dailyStart`/`dailyEnd`/`workDays` — POST dagi validatsiya bilan bir xil; yuborilmasa
  (null) o'zgarmaydi. Soat o'zgarsa davrning `lateTolerance`/`checkInWindow`/`autoCheckout` daqiqalari saqlanadi va
  yangi soatlar ular bilan tekshiriladi (sig'masa → 400 `errors.DailyEnd`). Faol davrda ham o'zgartirish mumkin.
- Sana yoki `workDays` o'zgarsa `requiredDays` qayta hisoblanadi (bayramlarsiz). Davomat yozuvlari o'zgarmaydi — ish
  kuni belgisi har so'rovda davrdan o'qiladi (yozuvsiz o'tgan kunlarning `absent`/`dayOff` ko'rinishi yangi `workDays`
  bo'yicha qayta hisoblanadi).

#### PUT `/api/admin/practice-periods/{id}/groups` · 200 · 400 · 404 · 409

Body `{ groupIds }` — **to'liq ro'yxat**: yo'qlari ajratiladi, yangilari biriktiriladi → `PracticePeriodDetail`.
Bo'sh ro'yxat ruxsat etiladi. Yopilgan davr → 409. Yangi guruh topilmasa/faol emas → 400 `errors.GroupIds`;
yangi guruh ustma-ust tushsa → 409. Ajratilayotgan guruh talabalarining **shu davrda davomat yozuvi** bo'lsa → 409
(`detail`: "Quyidagi guruhlar talabalarining shu davrda davomat yozuvlari bor — ularni ajratib bo'lmaydi: 412-22").

#### POST `/api/admin/practice-periods/{id}/close` · 200 · 404 · 409

Body yo'q → `PracticePeriodDetail` (`status: 'closed'`). Allaqachon yopilgan → 409 ("Davr allaqachon yopilgan.").
Yopilgan davr talaba/tyutor oqimlaridan chiqadi (check-in to'xtaydi), tarix saqlanadi. Qayta ochish yo'q.

#### DELETE `/api/admin/practice-periods/{id}` · 204 · 404 · 409

Soft delete. Davrda **birorta davomat yozuvi** bo'lsa → 409 ("Davrda davomat yozuvlari bor — uni o'chirib bo'lmaydi,
"Yopish" dan foydalaning."). O'chirilgan davr ro'yxatda, ustma-ust tekshiruvida va talaba oqimida ko'rinmaydi.

#### GET `/api/admin/practice-periods/{id}/stats` · 200 · 404

Davr ichidagi natijalar: har guruh va jami. Hisob **faqat shu davr** (`periodId == id`) yozuvlari bo'yicha — sukut
bo'yicha davr tanlash (§4.6) ishlatilmaydi. Talabalar — guruhdagi joriy faol (`StudentStatus.active`, o'chirilmagan)
profillar (`PracticePeriodGroup.studentsCount` bilan bir xil). 404 — davr yo'q yoki o'chirilgan.

```ts
interface GradeDistribution { excellent: number; good: number; satisfactory: number; unsatisfactory: number; retake: number; }

interface GroupMetrics {
  studentsCount: number;
  attendancePct: number;            // int — talabalar attendancePct o'rtachasi (yaxlitlangan); talaba yo'q → 0
  lowAttendanceCount: number;       // attendancePct < 70 (MinAttendancePct); elapsedWorkDays == 0 → 0
  suspiciousDays: number;           // isSuspicious davomat kunlari yig'indisi
  withCompanyCount: number;         // shu davrda approved (yoki completed) arizasi bor talabalar
  pendingApplicationsCount: number; // shu davrdagi eng so'nggi arizasi submitted | revisionNeeded
  diaryCount: number;               // kundalik yozuvlari
  diaryApprovedCount: number;       // status == approved
  diaryAvgScore: number;            // baholangan yozuvlar o'rtachasi (1 xona); yo'q → 0
  avgTotal: number | null;          // GradeCalculator jami ball o'rtachasi (1 xona); talaba yo'q → null
  finalizedCount: number;           // PracticeGrade yakunlangan
  grades: GradeDistribution;        // joriy (jonli) hisob: 5/4/3/2; retake — baho null (davomat < 70%)
}

interface PeriodGroupStats extends GroupMetrics {
  groupId: string; code: string; course: number; directionName: string;
}

interface PracticePeriodStats {
  periodId: string;
  elapsedWorkDays: number;          // davr boshidan o'tgan ish kunlari: kechagacha + bugun (check-in oynasi yopilgan bo'lsa)
  requiredDays: number;
  totals: GroupMetrics;             // barcha guruhlar talabalari bo'yicha (o'rtachalar talabalar bo'yicha)
  groups: PeriodGroupStats[];       // davrdagi har guruh (0 talabali ham, keyin o'chirilgani ham), code bo'yicha
}
```

- Talaba `attendancePct` — §4.2 formulasi (`StudentStatsCalculator`); `elapsedWorkDays` — uning maxraji asosi
  (sababli kunlar talaba bo'yicha chiqariladi). Boshlanmagan davrda `elapsedWorkDays = 0`, hamma talabada foiz 0.
- Ballar va baho — §4.4 (`GradeCalculator`, tyutor baholash jadvali bilan bir xil), saqlangan `tutorPoints` /
  `referencePoints` bilan; yakunlanmagan baho ham jonli hisoblanadi.

#### GET `/api/admin/practice-periods/{id}/groups/{groupId}/students` · 200 · 404

Guruh talabalarining shu davrdagi natijalari. 404 — davr yo'q/o'chirilgan yoki guruh shu davrga biriktirilmagan
(`detail`: "Guruh shu amaliyot davriga biriktirilmagan.").

```ts
interface PeriodGroupStudentRow {
  id: string;                       // User.Id
  fullName: string;
  hemisId: string;
  company: string | null;           // shu davrdagi approved/completed ariza korxonasi (eng so'nggi qaror)
  applicationStatus: ApplicationStatus | null; // shu davrdagi eng so'nggi ariza holati
  attendancePct: number;            // 1 xona (§4.2)
  presentDays: number;              // o'z vaqtida kelgan
  lateDays: number;                 // kech kelgan
  absentDays: number;               // kelmagan (hisobga olinadigan kunlardan, sababsiz)
  excusedDays: number;              // sababli (maxrajdan chiqariladi)
  suspiciousDays: number;
  diaryCount: number;
  diaryAvg: number;                 // baholangan yozuvlar o'rtachasi (1 xona); yo'q → 0
  attendancePoints: number;         // 0..40
  reportPoints: number;             // 0..30
  tutorPoints: number | null;       // 0..20, qo'yilmagan → null
  referencePoints: number | null;   // 0..10
  total: number;                    // 0..100
  grade: 2 | 3 | 4 | 5 | null;      // null — qayta topshiradi
  finalized: boolean;
}

interface PeriodGroupStudents {
  period: { id: string; name: string; status: PracticePeriodStatus; startDate: string; endDate: string };
  group: { id: string; code: string; course: number; facultyName: string; directionName: string };
  elapsedWorkDays: number;
  metrics: GroupMetrics;            // /stats dagi shu guruh qatori bilan bir xil hisob
  students: PeriodGroupStudentRow[]; // fullName bo'yicha
}
```

`presentDays + lateDays + absentDays + excusedDays` = talaba uchun hisobga olingan kunlar (odatda `elapsedWorkDays`;
bugun oyna yopilmasdan belgilangan kun ham qo'shiladi).

**Ustma-ust tushish (409)** — bitta guruh sanalari kesishadigan (`a.start <= b.end && a.end >= b.start`, chegaralar
kiradi) ikki **yopilmagan, o'chirilmagan** davrda bo'la olmaydi. Create, PUT (sana o'zgarsa) va PUT `/groups`
(yangi guruhlar uchun) da tekshiriladi. `detail`:
`"Quyidagi guruhlar shu sanalarda boshqa davrga biriktirilgan: 412-22 (Kuzgi amaliyot 2026), 413-22 (…)"`.

**Audit**: `practicePeriodCreated` · `practicePeriodUpdated` · `practicePeriodGroupsChanged`
(`changes`: `{"added":[…],"removed":[…]}`) · `practicePeriodClosed` · `practicePeriodDeleted`.

**Guruh tanlash** (mavjud endpointlar, §2.3.1): `GET /api/admin/faculties` → `.../departments` → `.../directions` →
`GET /api/admin/directions/{directionId}/groups`. `GroupRow.period` — guruhning sukut bo'yicha davri (§4.6: davom
etayotgan → oxirgi tugagan → eng yaqin rejalashtirilgan). Bitta guruh kesishmaydigan bir nechta davrga biriktirilishi
mumkin (kuzgi + bahorgi); kesishadigan ochiq davrga → 409.

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
  dateTo: string | null /*ko'lamdagi guruhlarning sukut bo'yicha davrlari (§4.6) min/max; yo'q → null*/;
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
  kind: 'outOfRadius' | 'notCheckedIn' | 'newApplications' /*v3.10: 'newLeaveRequests' olib tashlandi*/;
  count: number;
  href: string /*"/tutor/map" | "/tutor?status=absent" | "/tutor/applications"*/;
  maxDistanceM: number | null; /*faqat outOfRadius*/
}
interface AttendanceRow {
  studentId: string;
  name: string;
  group: string;
  company: string | null /*v3.14 — aktiv korxona (§4.7)*/;
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
Ko'rsatkichlar — har talaba guruhining **sukut bo'yicha davri** bo'yicha (§4.6). `company` (v3.14) — davrdan mustaqil,
faqat talabaning **aktiv korxonasi** nomi (§4.7), aktivi bo'lmasa `null`.

#### GET `/api/tutor/students/{id}?periodId=`

Ko'lamdan tashqari (yoki mavjud bo'lmagan) `id` → **404** (`"Talaba topilmadi (id: …)."` — mavjudligi oshkor qilinmaydi).
`periodId` (ixtiyoriy) — ko'rsatiladigan davr; berilmasa sukut bo'yicha davr (§4.6). `periodId` talabaning davrlaridan
(`periods`) biri bo'lmasa → **404** (`"Amaliyot davri topilmadi."`).

```ts
interface TutorStudentDetail {
  id: string;
  name: string;
  hemisId: string;
  group: string;
  course: number;
  faculty: string;
  direction: string;
  status: StudentStatus /*'active' | 'suspended' | 'graduated' — akademik holat*/;
  phone: string | null;
  state: StudentState /*'active' | 'redFlag' | 'suspicious'*/;
  suspiciousCount: number;
  company: StudentCompany | null;
  application: StudentApplication | null;
  period: StudentPeriod | null;
  attendance: AttendanceSummary;
  diary: DiarySummary;
  grade: StudentGrade | null;
  periods: StudentPeriodOption[] /*davr tanlagichi, startDate kamayish tartibida*/;
  selectedPeriodId: string | null /*javobdagi davrga bog'liq bloklar shu davr bo'yicha; davr yo'q → null*/;
  hasPassword: boolean /*v3.8 — talabaga brauzer login'i uchun parol o'rnatilgan (AdminStudentDetail'da ham)*/;
  activeCompany: ActiveCompanyRef | null /*v3.14 — tanlangan davrdan MUSTAQIL, §4.7 (AdminStudentDetail'da ham)*/;
}

interface ActiveCompanyRef {
  id: string /*korxona id*/;
  name: string;
  periodId: string /*aktiv arizaning davri*/;
  periodName: string;
}

interface StudentPeriodOption {
  id: string;
  name: string;
  startDate: string /*DateOnly*/;
  endDate: string;
  status: 'planned' | 'active' | 'closed' /*hisoblangan: yopilgan → closed; boshlanmagan → planned; aks holda active (tugagan, lekin yopilmagan davr ham active)*/;
  isDefault: boolean /*periodId berilmaganda tanlanadigan davr*/;
}

interface StudentCompany {
  id: string;
  name: string;
  tin: string;
  activity: string;
  address: string;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
  lat: number;
  lng: number;
  radiusM: number /*korxonaning JORIY geofence radiusi, ariza taklifi emas*/;
}

interface StudentApplication {
  id: string;
  status: ApplicationStatus;
  submittedAt: string /*ISO*/;
  decidedAt: string | null;
  comment: string | null /*DecisionComment*/;
  contract: { name: string; pages: number | null; sizeBytes: number; url: string } | null;
}

interface StudentPeriod {
  id: string;
  name: string;
  startDate: string /*DateOnly*/;
  endDate: string;
  dailyStart: string /*"09:00" (Toshkent)*/;
  dailyEnd: string /*"17:00"*/;
  workDays: number[] /*1 = Dushanba … 7 = Yakshanba*/;
  requiredDays: number;
}

interface AttendanceSummary {
  totalDays: number /*hisobga olinadigan ish kunlari (sababli kunlarsiz)*/;
  attendedDays: number /*present + late*/;
  lateDays: number;
  excusedDays: number;
  absentDays: number /*totalDays − attendedDays*/;
  suspiciousDays: number;
  attendancePct: number /*1 kasr*/;
}

interface DiarySummary {
  count: number;
  scoredCount: number;
  avg: number /*1 kasr, faqat baholanganlar bo'yicha*/;
}

interface StudentGrade {
  total: number /*har doim son*/;
  grade: 2 | 3 | 4 | 5 | null /*null — davomat 70% dan past, qayta topshiradi*/;
}
```

Qoidalar:

- `state` — `GET /api/tutor/students` dagi bilan **bir xil** (`StudentStateRule`): `totalDays>0 && pct<70` → `redFlag`;
  `suspiciousCount≥1` → `suspicious`; aks holda `active`.
- `periods` — talaba guruhiga biriktirilgan barcha davrlar ∪ talabaning `PeriodId` li yozuvlari (ariza, davomat, kundalik,
  baho, ruxsat) bor davrlar (guruh almashgan talaba eski davrini yo'qotmaydi); o'chirilgan davrlar kirmaydi.
- `period` / `selectedPeriodId` — tanlangan davr: `periodId` yoki sukut bo'yicha (§4.6, guruh davrlaridan; guruhda
  davr bo'lmasa — `periods` dan). Davr yo'q bo'lsa `period = null`, `grade = null`, `attendance` nollar bilan keladi.
- Davrga bog'liq **hamma** bloklar (`application`, `company`, `attendance`, `diary`, `grade`, `state`, `suspiciousCount`)
  tanlangan davr bo'yicha. Rejalashtirilgan (boshlanmagan) davrda — bo'sh: `application/company/grade = null`, nollar.
- `application` — tanlangan davrdagi ariza: `approved`/`completed` ustun, bo'lmasa eng so'nggisi (`submittedAt` desc).
  Davrda ariza yo'q → `null` (boshqa davr arizasi ko'rsatilmaydi).
- `company` — faqat tanlangan davrdagi `approved` yoki `completed` arizadagi korxona; aks holda `null`.
- `activeCompany` (v3.14) — `periodId` dan **mustaqil**: talabaning hozir amaliyot o'tayotgan korxonasi (§4.7), aks holda
  `null`. Ro'yxatlardagi `company` bilan bir qoida — "hozir qayerda" savoliga shu maydon javob beradi; `company` esa
  tanlangan davr tarixi (masalan yopilgan davr tanlansa — o'sha davr korxonasi, `activeCompany` esa `null` bo'lishi mumkin).
- `grade` — davr boshlangan bo'lsa (`startDate ≤ bugun`); aks holda `null`.
- `attendance` / `diary` — `StudentStatsCalculator` (ro'yxat va baholash bilan bir xil manba).
- `grade` — `GradeCalculator.Compute(…)` (§4.4).
- `contract.url` — `"/api/files/<guid>"`, Bearer kerak (§2.2).

#### GET `/api/tutor/students/{id}/attendance` — `?periodId=&from=&to=`

| Query      | Tip        | Majburiy | Default                            |
| ---------- | ---------- | -------- | ---------------------------------- |
| `periodId` | GUID       | yo'q     | sukut bo'yicha davr (§4.6)         |
| `from`     | `DateOnly` | yo'q     | davrning boshlanish sanasi         |
| `to`       | `DateOnly` | yo'q     | `min(bugun, davr tugash sanasi)`   |

Har **kalendar kun** uchun bitta element (dam olish kunlari ham), `date` bo'yicha o'sish tartibida.
`from`/`to` davr chegaralariga **qisiladi** (`from < startDate` → `startDate`, `to > endDate` → `endDate`) — oy
navigatsiyasi davrdan chiqmaydi. Qatorlar, kundaliklar, ruxsatlar va radius faqat shu davrniki.
Talabaning davri bo'lmasa yoki davr hali boshlanmagan bo'lsa (`from > to`) — **bo'sh massiv** (xato emas).
Begona `periodId` → **404**.

```ts
interface StudentAttendanceDay {
  date: string /*"2026-10-12"*/;
  status: AttendanceStatus /*pending | present | late | absent | excused | dayOff*/;
  isWorkDay: boolean;
  checkIn: AttendancePunch | null;
  checkOut: AttendancePunch | null;
  autoClosed: boolean;
  suspicious: boolean;
  suspiciousReason: string | null;
  manual: boolean;
  manualReason: string | null;
  leaveRequestId: string | null;
  diary: { id: string; status: DiaryStatus; score: number | null } | null;
  attempts: number /*shu kundagi urinishlar (AttendanceEvent)*/;
  rejectedAttempts: number /*ulardan rad etilganlari*/;
  events: StudentAttendanceEvent[] /*kundagi BARCHA urinishlar, vaqt bo'yicha o'sish; bo'lmasa []*/;
}

interface StudentAttendanceEvent {
  id: string /*AttendanceEvent id*/;
  kind: AttendanceEventKind /*checkIn | checkOut*/;
  at: string /*"08:58" (Toshkent) — server qabul qilgan vaqt, AttendancePunch.at bilan bir xil manba*/;
  atIso: string /*ReceivedAt, to'liq ISO +05:00*/;
  accepted: boolean;
  rejectReason: CheckInRejectReason | null /*§3.1; qabul qilinganda null*/;
  rejectMessage: string | null /*§3.2 o'zbekcha matni; qabul qilinganda null*/;
  distanceM: number;
  accuracyM: number;
  radiusM: number /*urinish paytidagi korxona radiusi*/;
  lat: number;
  lng: number;
  photoUrl: string | null /*"/api/files/<guid>" — selfi (rad etilgani ham), Bearer kerak*/;
}

interface AttendancePunch {
  at: string /*"09:02" (Toshkent)*/;
  atIso: string /*to'liq ISO, +05:00*/;
  distanceM: number | null;
  accuracyM: number | null;
  lat: number | null;
  lng: number | null;
  photoUrl: string | null /*"/api/files/<guid>" — check-in selfisi, Bearer kerak*/;
  outOfRadius: boolean /*distanceM > korxona radiusM*/;
}
```

Qoidalar:

- Bazada qatori yo'q kun ham qaytariladi — holat `AttendanceStatusResolver` bilan hisoblanadi (ish kuni emas →
  `dayOff`; tasdiqlangan ruxsat → `excused`; o'tgan kun → `absent`; bugun oyna yopilmagan bo'lsa → `pending`).
- `checkIn`/`checkOut` — **qabul qilingan** belgilar (`DailyAttendance`). Rad etilgan urinishlar
  `attempts` / `rejectedAttempts` (faqat check-in soni) va `events` da aks etadi.
- `events` — shu kundagi barcha check-in **va** check-out urinishlari (`AttendanceEvent`, qabul qilingan ham, rad
  etilgan ham) rasmi bilan — "talaba qanday rasmga tushdi". Tartib: `atIso` (server qabul qilgan vaqt) bo'yicha
  o'sish. Faqat shu kun sanasidagi yozuvlar; so'rov oralig'i davr chegarasiga qisilgani uchun boshqa davr urinishlari
  aralashmaydi. Admin endpoint'i (`/api/admin/students/{id}/attendance`) ham aynan shu shaklni qaytaradi.
- `lat`/`lng` — `AttendanceEvent.Location` dan; `DailyAttendance` da koordinata saqlanmaydi, shuning uchun qo'lda
  kiritilgan kunda `null`.
- `photoUrl` — check-in/check-out selfisi (`StoredFileKind.checkInPhoto`, §2.6). Rasm bo'lmasa `null`.
- `outOfRadius` — tasdiqlangan arizadagi korxona radiusi bilan solishtiriladi; korxona yo'q bo'lsa `false`.
- `leaveRequestId` — kun sababli bo'lsa: qatordagi ruxsat yoki kunni qoplagan tasdiqlangan ruxsat.

Xatolar:

| Holat                                      | Status  | `detail` / `errors`                                                        |
| ------------------------------------------ | ------- | -------------------------------------------------------------------------- |
| `from > to`                                | **400** | "'from' sanasi 'to' sanasidan keyin bo'lishi mumkin emas." (`errors.To`)   |
| oraliq > **400 kun**                       | **400** | "So'ralgan oraliq 400 kundan uzun bo'lmasligi kerak." (`errors.From`)      |
| sana formati noto'g'ri                     | **400** | ASP.NET model binding (`type` bilan)                                       |
| ko'lamdan tashqari / mavjud bo'lmagan `id` | **404** | "Talaba topilmadi (id: …)."                                                |

> Bitta chegara berilganda ikkinchisi **bugungi kun** bilan taxminlanadi (validator), shuning uchun faqat `from`
> yuborilsa ham 400 kunlik chegara ishlaydi.

#### GET `/api/tutor/students/{id}/diaries` — `?periodId=`

Javob — **mavjud** `TutorDiaryEntry[]` (`GET /api/tutor/diaries` bilan bir xil shakl, fayl havolalari bilan) —
tanlangan davr (`periodId`, berilmasa sukut bo'yicha; talabaning umuman davri bo'lmasa — hammasi) yozuvlari.
Tartib: `date` desc, keyin `submittedAt` desc. Ko'lamdan tashqari talaba yoki begona `periodId` → **404**.

#### POST `/api/tutor/students/{id}/password` · 204 · 400 · 404 — v3.8

Admin'nikidek (`POST /api/admin/students/{id}/password`, §2.3) — ayni buyruq. Ko'lamdan tashqari talaba → **404**.

#### GET `/api/tutor/companies`

**v3.15:** ko'lamdagi talabalar **hozir aktiv amaliyot o'tayotgan** korxonalar (§4.7), **nom** bo'yicha. Ko'lamda bitta
ham aktiv talabasi bo'lmagan korxona ro'yxatga **kirmaydi** (masalan, faqat yopilgan davrdagi `approved` ariza — kirmaydi;
bo'sh massiv bo'lishi mumkin — xato emas). Sahifalanmagan.

```ts
interface TutorCompany {
  id: string;
  name: string;
  tin: string;
  address: string;
  lat: number;
  lng: number;
  radiusM: number;
  students: number /*FAQAT ko'lamdagi AKTIV talabalar (v3.15)*/;
  totalStudents: number /*butun tizim bo'yicha shu korxonadagi AKTIV talabalar (v3.15)*/;
  maxStudents: number;
  overLimit: boolean /*totalStudents > maxStudents — ko'lamdagi son EMAS*/;
  attendancePct: number /*ko'lamdagi aktiv talabalar jamlanmasi (aktiv davr): ∑kelgan / ∑hisobga olingan kun × 100, 1 kasr*/;
  suspiciousDays: number /*ko'lamdagi aktiv talabalarning aktiv davrdagi shubhali kunlari*/;
  flag: CompanyFlag | null;
}
```

#### GET `/api/tutor/companies/{id}` · 200 · 404

Shakl — `CompanyDetail` (§2.3, admin bilan **bir xil**). Farqi: `students`, `suspiciousDays` va `periods[].students`
**ko'lam kesimida**; `totalStudents` va `overLimit` esa butun tizim bo'yicha (STIR nazorati). Sonlar — faqat aktiv (v3.15).
Kirish darvozasi (o'zgarmagan): ko'lamda shu korxonaga **tasdiqlangan** (`approved`, istalgan davr) arizasi bor talaba
bo'lmasa (yoki `id` yo'q) → **404**. Shu bois tarixi bor, hozir aktivi yo'q korxona → **200**, `students: 0`, `periods: []`.

#### GET `/api/tutor/companies/{id}/students` · 200 · 404

Shakl — `CompanyStudent[]` (§2.3 bilan bir xil), faqat **ko'lamdagi aktiv** talabalar (v3.15). FISH bo'yicha tartib.
404 — tafsilotdagi darvoza bilan bir xil; darvozadan o'tib aktiv talabasi yo'q → `[]`.

#### GET `/api/tutor/companies/{id}/checkin-qr` · POST `/api/tutor/companies/{id}/checkin-qr/rotate` · 200 · 404 — v3.7

Shakl va xulq — admin'dagi bilan bir xil (`CompanyCheckInQrDto`, §2.3; rotate → yangi token, eski QR yaroqsiz, audit
`companyQrRotated` tyutor nomidan). Ko'lam — `GET /api/tutor/companies/{id}` dagidek: ko'lamda shu korxonaga
**tasdiqlangan** arizasi bor talaba bo'lmasa (yoki `id` yo'q) → **404**.

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

#### ~~`GET /api/tutor/leave-requests` · `POST /api/tutor/leave-requests/{id}/decision`~~ — v3.10 da olib tashlandi

Ruxsat so'rash moduli foydalanuvchi qarori bilan olib tashlandi (§6.16). Yo'llar endi **404**. Yangi `excused` kun faqat
qo'lda tuzatish orqali paydo bo'ladi (Domain `DailyAttendance.ManualFix`; uning API endpoint'i **hozircha yo'q**). Bazadagi eski tasdiqlangan ruxsatlar hisoblarda
(`excused`, `onLeave`, `leaveRequestId`) avvalgidek hisobga olinadi.

#### GET `/api/tutor/grading`

Har talaba guruhining **sukut bo'yicha davri** (§4.6) bo'yicha — tanaffusda tugagan davr baholanadi. Davri bo'lmagan
talaba qatorga **kirmaydi**.

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
  period: StudentPeriodOption | null /*v3.5: ko'rsatilayotgan davr (§4.6 "current"), isDefault=true; davr yo'q → null*/;
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
  photoRequired: boolean /*v3.7 — sozlama checkinPhotoRequired; true → selfi majburiy*/;
  qrRequired: boolean /*v3.7 — sozlama checkinQrRequired; true → amaliyot joyidagi QR skanerlanishi majburiy*/;
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
  pdfRequired: boolean /*sozlama diaryPdfRequired; true → hisobotga kamida bitta PDF shart*/;
}
```

`place` — faqat ko'rsatilayotgan davrdagi ariza `approved` va korxona bor bo'lsa. Davr yo'q → `checkin.status="pending"`,
`note="Faol amaliyot davri yo'q."`, `place=null`, `period=null`.

Davr (v3.5, §4.6): davom etayotgan → eng yaqin kelgusi → oxirgi tugagan. Belgilanish faqat **davom etayotgan** davrda
mumkin; aks holda `window.isOpen=false`, `checkin.status="dayOff"` va `note`:
- kelgusi davr bor: `"Amaliyot davri hali boshlanmagan: <nom>, <dd.MM.yyyy> dan boshlanadi."` (`period.status="planned"`);
- faqat tugagan davr: `"Amaliyot davri tugagan: <nom>."`.
"Chiqdi" holati alohida status emas — `checkOutAt !== null` (yoki `autoClosed`).

#### POST `/api/student/checkin` · POST `/api/student/checkout` · multipart **yoki** JSON

Ikkala endpoint ham **ikki formatni** qabul qiladi. Yo'l bitta, ammo kontrollerda ikkita action bor va ular
`[Consumes]` bilan ajratiladi — shuning uchun `Content-Type` **aniq yuborilishi shart** (pastdagi "Transport xatolari").

| `Content-Type`          | Rasm                | Izoh                                                                       |
| ----------------------- | ------------------- | -------------------------------------------------------------------------- |
| `multipart/form-data`   | ixtiyoriy `photo`   | **Asosiy yo'l** — TWA doim shu bilan yuboradi                               |
| `application/json`      | yo'q                | Eski klientlar va offline navbat; `checkinPhotoRequired=true` (sukut) bo'lsa → 400 |

Maydonlar (ikkalasi bir xil, `GeoRequestValidator`):

| Maydon       | Tip          | Majburiy         | Validatsiya                                                                     |
| ------------ | ------------ | ---------------- | ------------------------------------------------------------------------------- |
| `lat`        | number       | ha               | −90..90 (`errors.Lat`)                                                          |
| `lng`        | number       | ha               | −180..180 (`errors.Lng`)                                                        |
| `accuracy`   | number (m)   | ha               | 0..100000 (`errors.Accuracy`)                                                   |
| `occurredAt` | ISO datetime | ha               | ≤ server + 1 min; ≥ server − 10 min (`errors.OccurredAt`)                       |
| `photo`      | fayl         | sozlamaga qarab  | faqat multipart; ≤ **5 MB**, `image/jpeg,image/png,image/webp,image/heic,image/heif` |
| `qr`         | string       | sozlamaga qarab  | v3.7 · skanerlangan QR satri `AMLQR:1:{token}` (multipart maydoni yoki JSON `qr`); ≤ 256 belgi (`errors.Qr`) |

So'rov tanasining chegarasi — **6 MB** (`RequestSizeLimit` + `RequestFormLimits`), oshsa **413** (Kestrel, ProblemDetails'siz).

```ts
/** Multipart tana; `photo` — File yoki Blob. */
interface CheckinFormData {
  lat: number;
  lng: number;
  accuracy: number;
  occurredAt: string; // ISO 8601
  photo?: File; // checkinPhotoRequired=true bo'lsa majburiy
  qr?: string; // v3.7 — skanerlangan QR satri; checkinQrRequired=true bo'lsa majburiy
}
```

```ts
const body = new FormData();
body.append('lat', String(lat));
body.append('lng', String(lng));
body.append('accuracy', String(accuracy));
body.append('occurredAt', new Date().toISOString());
if (photo) body.append('photo', photo, 'selfi.jpg');
if (qr) body.append('qr', qr); // QR skaneridan kelgan satr, o'zgartirmasdan
await api.post('/api/student/checkin', body); // Content-Type'ni brauzer o'zi qo'yadi (boundary bilan)
```

Response 200 `TodayDto` (yangilangan, **ikkala format uchun bir xil**). **Har urinish** (rad etilgani ham)
`AttendanceEvent` ga yoziladi — rasmi bilan birga. Idempotent: bir xil `occurredAt` bilan qabul qilingan urinish
qayta kelsa — xato emas, joriy holat (takror rasm ham saqlanmaydi).

Xato → status (`CheckInRejectReason.ToException`), `detail` = §3.2 xabari:

| Check-in                                | Status  | Check-out                               | Status  |
| --------------------------------------- | ------- | --------------------------------------- | ------- |
| davom etayotgan davr yo'q (§4.6) — `detail` = `today.checkin.note` matni: "Faol amaliyot davri yo'q." · "Amaliyot davri hali boshlanmagan: <nom>, <dd.MM.yyyy> dan boshlanadi." · "Amaliyot davri tugagan: <nom>." (hodisa yozilmaydi) | 400     | xuddi shunday                           | 400     |
| `notApproved` (korxona/ariza yo'q)      | 400     | `noCheckIn`                             | **409** |
| `periodNotStarted` / `periodEnded`      | 400     | `alreadyCheckedOut` (yoki `autoClosed`) | **409** |
| `notWorkDay`                            | 400     | `windowNotOpen` (17:00 dan oldin)       | 400     |
| `onLeave`                               | 400     | `windowClosed` (18:00 dan keyin)        | 400     |
| `alreadyCheckedIn`                      | **409** | `poorAccuracy`                          | 400     |
| `windowNotOpen` (09:00 dan oldin)       | 400     | `outOfRadius`                           | **409** |
| `windowClosed` (10:30 dan keyin)        | 400     | `qrInvalid` (v3.7; tartibi — pastda)    | **409** |
| `qrInvalid` (v3.7, QR boshqa joyniki)   | **409** |                                         |         |
| `poorAccuracy` (accuracy > 100 m)       | 400     |                                         |         |
| `outOfRadius` (masofa > radius)         | **409** |                                         |         |

Tekshiruv tartibi aynan shu (birinchi mos kelgan sabab qaytadi). Qabul: `localNow ≥ 09:15` → `late`, aks holda `present`.
Check-out tartibi: `noCheckIn` → `alreadyCheckedOut` → `windowNotOpen` → `windowClosed` → `qrInvalid` → `poorAccuracy` → `outOfRadius`.

**QR (v3.7).** `qr` yuborilgan bo'lsa u **har doim** (sozlamadan qat'i nazar) talabaning shu davrdagi tasdiqlangan
arizasi korxonasining joriy tokeni bilan solishtiriladi. Mos kelmasa (boshqa korxona QR'i, almashtirilgan eski QR,
noto'g'ri format — `AMLQR:1:` prefiksi va 32 hex belgidan boshqa har qanday satr) → urinish **rad etiladi**:
**409**, `detail` = "QR kod bu amaliyot joyiga tegishli emas.", hodisa `rejectReason: "qrInvalid"` bilan selfisi birga
saqlanadi (tyutor `events[]` da ko'radi). QR tekshiruvi ariza/davr/ish kuni/takror/oyna tekshiruvlaridan **keyin**,
GPS aniqligi va radiusdan **oldin** — ya'ni QR noto'g'ri bo'lsa, radius tashqarisida bo'lsa ham sabab `qrInvalid`.

**Majburiy qismlar** (`checkinPhotoRequired`, `checkinQrRequired` — ikkalasi sukut bo'yicha `true`): policy'dan
**oldin** tekshiriladi, hodisa **yozilmaydi**, **400** `ValidationException`. Ikkalasi ham yetishmasa `errors` da
ikkala kalit bo'ladi, `detail` — rasm xabari.

| Holat                                                      | `detail` / `errors.Qr`                   |
| ---------------------------------------------------------- | ---------------------------------------- |
| `checkinQrRequired=true`, `qr` yuborilmagan yoki bo'sh      | "Amaliyot joyidagi QR kodni skanerlang." |
| `qr` 256 belgidan uzun                                     | "QR kod satri juda uzun."                |

**Rasm (selfi) xatolari** — hammasi **400**, `errors.Photo`:

| Holat                                                                  | `detail` / `errors.Photo`                                        |
| ---------------------------------------------------------------------- | ----------------------------------------------------------------- |
| `checkinPhotoRequired=true`, rasm yuborilmagan (JSON yoki bo'sh `photo`) | "Check-in uchun rasm majburiy." / "Check-out uchun rasm majburiy." |
| Noto'g'ri fayl turi                                                    | "Faqat rasm (JPEG, PNG, WebP, HEIC) qabul qilinadi."              |
| 5 MB dan katta yoki bo'sh fayl                                         | "Rasm 5 MB dan oshmasligi va bo'sh bo'lmasligi kerak."            |
| Fayl nomi bo'sh yoki 255 belgidan uzun                                 | "Rasm nomi bo'sh yoki juda uzun."                                 |

**Muhim:** rad etilgan urinishning rasmi ham **saqlanadi** (policy 400/409 da) — tyutor shubhani tekshirishi uchun;
u `GET /api/tutor/students/{id}/attendance` da `attempts`/`rejectedAttempts` orqali ko'rinadi. Faqat validatsiya
xatosida (`errors.Photo`) hech narsa saqlanmaydi. Qabul qilingan urinish rasmi esa `AttendancePunch.photoUrl` da.

**Transport xatolari** (`Content-Type` bilan bog'liq):

| So'rov                                                                | Status      | Izoh                                                              |
| --------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------ |
| `Content-Type: multipart/form-data`                                   | 200/400/409 | selfi bilan yoki selfisiz — asosiy yo'l                            |
| `Content-Type: application/json`                                      | 200/400/409 | rasmsiz JSON action                                                |
| Boshqa tur (masalan `text/plain`, `application/x-www-form-urlencoded`) | **415**     | `[Consumes]` rad etadi, javob — ASP.NET'ning `ProblemDetails` i    |
| `Content-Type` **umuman yo'q**                                        | **415**     | bir xil — boshqa hech qanday qo'shimcha xulq yo'q                  |

Kontrollerda multipart action `[Consumes("multipart/form-data")]` bilan, JSON action esa **`[Consumes]` siz**
(cheklovsiz fallback) — shuning uchun marshrutlash noaniq bo'lmaydi va noto'g'ri tur har doim toza **415** beradi.

> **Baribir `Content-Type` ni har doim yuboring** — 415 ham xato, so'rov bajarilmaydi.
> `FormData` bilan `fetch` buni o'zi qo'yadi (boundary bilan); JSON yuborganda
> `headers: { 'Content-Type': 'application/json' }` ni **qo'lda** qo'shing — aks holda `fetch`
> `text/plain;charset=UTF-8` qo'yadi va **415** keladi.

#### POST `/api/student/place` · 201 · 400 · 404 · 409

Talaba amaliyot joyini **STIR orqali** tanlaydi — korxona nomi, manzili, koordinatasi va radiusi
QO'LDA kiritilmaydi, admin oldindan yaratgan yozuvdan olinadi (§2.7).

```ts
// so'rov
{ tin: string }   // 9 raqam
```

Javob — `PracticePlaceDto` (quyidagi shakl), `status: "submitted"` — ariza tyutorga boradi,
`proposedRadiusM` korxonanikidan olinadi, shartnoma fayli bu bosqichda biriktirilmaydi.

- **404** — STIR bilan **faol** korxona yo'q (faolsizlantirilgan/arxivlangani ham topilmaydi)
- Ariza davri (v3.5, §4.6 "enrollment"): davom etayotgan, bo'lmasa **eng yaqin kelgusi** davr — ikki davr oralig'ida
  talaba bahorgi davrga oldindan ariza beradi (ariza `periodId` = bahorgi). Tugagan/yopilgan davrga ariza berilmaydi.
  "Allaqachon"/"ko'rib chiqilmoqda" tekshiruvlari faqat shu davr arizalari bo'yicha.
- **409** — `"Sizga faol amaliyot davri biriktirilmagan — tyutoringizga murojaat qiling."` (davom etayotgan ham, kelgusi ham yo'q) ·
  `"Arizangiz ko'rib chiqilmoqda — tyutor qaroridan keyin o'zgartirish mumkin."` ·
  `"Sizga allaqachon amaliyot joyi biriktirilgan. O'zgartirish uchun tyutoringizga murojaat qiling."`
- Ariza **qayta ishlashga qaytarilgan** (`revisionNeeded`) bo'lsa — yangi STIR bilan qayta yuboriladi
  (server o'zi `Resubmit` qiladi), rad etilgan bo'lsa yangi ariza yaratiladi.

#### GET `/api/student/place` · 404

404 — davr yoki ariza yoki korxona yo'q ("Amaliyot joyi hali biriktirilmagan."). Davr — §4.6 "current"
(davom etayotgan → eng yaqin kelgusi → oxirgi tugagan): tanaffusda bahorgi davrga ariza berilmagan bo'lsa 404 (TWA ariza
formasini ko'rsatadi), berilgan bo'lsa — bahorgi ariza; `periodFrom/periodTo` — shu davr sanalari.

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

Barcha davrlar, `date` desc → `submittedAt` desc. Har yozuvda `periodId`/`periodName` (v3.5) — TWA tarixni davr
bo'yicha guruhlashi mumkin.

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
  periodId: string /*v3.5*/;
  periodName: string | null; /*v3.5; o'chirilgan davr → null*/
}
```

#### POST `/api/student/diary` · `multipart/form-data` · 201

| Form maydoni | Tip                     | Majburiy | Validatsiya                                                                                                        |
| ------------ | ----------------------- | -------- | ------------------------------------------------------------------------------------------------------------------ |
| `text`       | string                  | ha       | bo'sh emas; trim uzunligi ≥ `minReportLength` (default 150); ≤ 10000 (`errors.Text`)                               |
| `learned`    | string                  | yo'q     | ≤ 10000 (`errors.Learned`)                                                                                         |
| `files`      | file[] (bir nomda ko'p) | yo'q     | ≤ 5 ta; har biri > 0 va ≤ 5 MB; `image/jpeg,png,webp,heic,heif` yoki `application/pdf`; nom ≤ 255 (`errors.Files`) |

Butun so'rov ≤ 30 MB (`RequestSizeLimit`) — oshsa 413. Response **201** `DiaryEntryDto`.
Xatolar: 400 validation; **400** davr yo'q / davom etayotgan davr yo'q ("Amaliyot davri bugunni o'z ichiga olmaydi.") / fayllar jami > 5; **409** — bugungi
hisobot allaqachon bor ("Bugungi hisobot allaqachon yuborilgan."). Istisno: bugungisi `rewrite` holatida → qayta yoziladi
(`Resubmit`, fayllar qo'shiladi, status → `submitted`), 201.
**400** `diaryPdfRequired=true` va `files` orasida PDF yo'q (PDF = content-type `application/pdf` yoki `.pdf` kengaytma;
qayta yozishda yozuvda qolgan PDF ham hisob): `detail` = `errors.Files[0]` = "Hisobotga PDF fayl biriktirilishi shart."

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

Davr yo'q: kelajak → `future`, qolgani `dayOff`. Aks holda §4.1 `DayStatus` qoidasi — har kun **o'zini o'z ichiga olgan
davr** bilan (v3.5: oy kuzgi va bahorgi davrni qamrasa ikkalasi ham ko'rinadi; davrlar tashqarisi — `dayOff`/`future`).

#### GET `/api/student/period-days` — `?periodId=` · 200 · 404 — v3.11

Bosh ekran paneli: tanlangan davrning **har bir kalendar kuni** (`startDate..endDate`, o'sish tartibida) bitta so'rovda.
`periodId` berilmasa — `GET /api/student/today` ko'rsatadigan davr (§4.6 `Current`: davom etayotgan → eng yaqin kelgusi →
oxirgi tugagan; joriy guruhda bo'lmasa — `Default`, eski guruh davri ham). Talabaga tegishli bo'lmagan `periodId` → **404**.
Davr umuman yo'q → 200 `{ today, period: null, periods: [], days: [] }`.

```ts
interface StudentPeriodDaysDto {
  today: string;                       // "YYYY-MM-DD" (Toshkent)
  period: {
    id: string; name: string;
    status: PracticePeriodStatus;      // effektiv: planned | active | closed
    startDate: string; endDate: string;
    requiredDays: number;              // davrdagi ish kunlari (bayramsiz) — PracticePeriod.RequiredDays
    elapsedWorkDays: number;           // tyutor statistikasi maxraji (§4.2): o'tgan ish kunlari, sababli kunlarsiz
  } | null;
  periods: StudentPeriodOption[];      // tanlagich (profil/portfolio bilan bir xil), startDate kamayish tartibida
  days: {
    date: string;
    weekday: number;                   // ISO: 1 = Du … 7 = Ya
    isWorkDay: boolean;                // davr workDays ga kiradi va bayram emas
    holiday: string | null;            // bayram nomi
    status: CalendarDayStatus;         // AYNAN GET /api/student/calendar dagi qiymat (§4.1 DayStatus)
    checkInAt: string | null;          // "HH:mm" (Toshkent)
    checkOutAt: string | null;         // "HH:mm"; avto-yopilganda — yopilish vaqti
    autoClosed: boolean;
    suspicious: boolean;
    manual: boolean;                   // tyutor qo'lda kiritgan/tuzatgan
    diary: { id: string; status: DiaryStatus; score: number | null } | null;
  }[];
}
```

`status`: bugun belgilanmagan va oyna ochiq → `pending` (oyna yopilgan → `absent`); kelgusi ish kuni → `future`,
kelgusi dam olish/bayram → `dayOff`. Vaqtlar, `autoClosed/suspicious/manual`, `diary` — tyutor
`GET /api/tutor/students/{id}/attendance` bilan bir manbadan.

#### ~~`GET /api/student/leave-requests` · `POST /api/student/leave-requests`~~ — v3.10 da olib tashlandi

Talaba endi ruxsat so'ramaydi (§6.16); yo'llar **404**. `LeaveRequestDto` olib tashlandi.

#### GET `/api/student/portfolio` — `?periodId=` · 404

Tanlangan davr (`periodId`; berilmasa sukut bo'yicha — §4.6 "default": davom etayotgan → oxirgi tugagan → kelgusi).
404 — davr yo'q yoki `periodId` talabaga tegishli emas. `periods` — talabaning barcha davrlari (tarix uchun tanlagich).

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
  periodId: string /*v3.5: javob shu davr bo'yicha*/;
  periods: StudentPeriodOption[] /*v3.5: §2.5 bilan bir xil shakl, startDate kamayish tartibida*/;
}
```

---


### 2.7 Companies — `CompaniesController` (`/api/companies`) · `Authenticated`

#### GET `/api/companies/lookup?tin=123456789` · 200 · 400 · 404

Har qanday rol uchun ochiq STIR qidiruvi — **talaba korxona ma'lumotini qo'lda kiritmasligi** uchun
(TWA: STIR → korxona kartasi → tasdiqlash → `POST /api/student/place`).

```ts
interface CompanyLookupDto {
  id: string;
  name: string;
  tin: string;
  activity: string;
  address: string;
  lat: number;
  lng: number;
  radiusM: number;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
}
```

Faqat **faol** korxona qaytadi. Faolsizlantirilgan (`PATCH …/status { isActive: false }`) yoki arxivlangan
korxona bu yerda umuman ko'rinmaydi → **404** `"Bu STIR bilan faol korxona topilmadi. Korxona avval tizimga
kiritilishi kerak — tyutoringizga murojaat qiling."` STIR formati noto'g'ri bo'lsa → **400**.

#### GET `/api/student/profile` · 200 — v3.8

Talaba kabineti (brauzer va TWA). Ko'rsatkichlar tyutor profilidagi (`GET /api/tutor/students/{id}`, sukut bo'yicha
davr) bilan **ayni handler'dan** olinadi — farq qilmaydi. Admin/tyutor → 403 (body bo'sh).

```ts
interface StudentProfileDto {
  id: string;
  fullName: string;
  hemisId: string;
  phoneNumber: string | null;
  faculty: string;
  department: string;
  direction: string;
  group: string;
  course: number;
  tutor: { fullName: string; phoneNumber: string | null } | null; // guruhga biriktirilgan faol tyutor; bir nechta bo'lsa FISH bo'yicha birinchisi
  telegramLinked: boolean;
  hasPassword: boolean;
  mustChangePassword: boolean;
  practice: {
    period: { id: string; name: string; status: 'planned' | 'active' | 'closed'; startDate: string; endDate: string };
    company: { id: string; name: string; address: string | null } | null; // shu davrdagi tasdiqlangan ariza korxonasi
    elapsedWorkDays: number; // davr boshidan o'tgan ish kunlari (sababli kunlar bilan; bugun — oyna yopilgan yoki belgilangan bo'lsa)
    attendancePct: number; // sababli kunlar maxrajdan chiqariladi
    suspiciousDays: number;
    total: number; // joriy ball 0–100 (GradeCalculator); davr boshlanmagan bo'lsa 0
    grade: number | null; // 2–5; davomat yetarli emas yoki davr boshlanmagan → null
    finalized: boolean; // tyutor bahoni yakunlagan
  } | null; // sukut bo'yicha davr (davom etayotgan → oxirgi tugagan → eng yaqin kelgusi) yo'q → null
}
```


## 3. Enum'lar (JSON — camelCase string)

### 3.1 Domain va Application enum'lari

| Enum                               | Qiymatlar                                                                                                                                                                                                                                                                                                                                 | Qayerda                                                                                |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `UserRole`                         | `admin` · `tutor` · `student`                                                                                                                                                                                                                                                                                                             | `UserSummaryDto.role`, `AuditEntryDto.userRole` (JWT claim: `Admin`/`Tutor`/`Student`) |
| `AttendanceStatus`                 | `pending` · `present` · `late` · `absent` · `excused` · `dayOff`                                                                                                                                                                                                                                                                          | tutor today `rows[].status`, TWA `checkin.status`                                      |
| `CalendarDayStatus`                | `future` · `pending` · `present` · `late` · `absent` · `excused` · `dayOff`                                                                                                                                                                                                                                                               | tutor/TWA kalendar                                                                     |
| `AttendanceEventKind`              | `checkIn` · `checkOut`                                                                                                                                                                                                                                                                                                                    | `StudentAttendanceDay.events[].kind`                                                   |
| `CheckInRejectReason`              | `none` · `notApproved` · `notWorkDay` · `periodNotStarted` · `periodEnded` · `windowNotOpen` · `windowClosed` · `poorAccuracy` · `outOfRadius` · `alreadyCheckedIn` · `noCheckIn` · `alreadyCheckedOut` · `onLeave` · `qrInvalid`                                                                                                                     | xabar (§3.2); `events[].rejectReason`                                                              |
| `DiaryStatus`                      | `submitted` · `seen` · `rewrite` · `approved`                                                                                                                                                                                                                                                                                             | diary'lar                                                                              |
| `DiaryReviewAction` (request)      | `approve` · `score` · `rewrite`                                                                                                                                                                                                                                                                                                           | tutor review                                                                           |
| `DiaryState`                       | `written` · `pending` (+ `null`)                                                                                                                                                                                                                                                                                                          | tutor today `rows[].diary`                                                             |
| `LeaveRequestStatus`               | `pending` · `approved` · `rejected`                                                                                                                                                                                                                                                                                                       | ichki (tarix, v3.10 dan API javoblarida yo'q)                                          |
| `ApplicationStatus`                | `draft` · `submitted` · `revisionNeeded` · `approved` · `rejected` · `completed` · `transferred` (admin boshqa korxonaga o'tkazgan — "Ko'chirilgan")                                                                                                                                                                                      | applications, TWA place                                                                |
| `ApplicationDecision` (request)    | `approve` · `return` · `reject`                                                                                                                                                                                                                                                                                                           | tutor decision                                                                         |
| `PracticePeriodStatus`             | `planned` · `active` · `closed` — admin API'da hisoblanadi (§2.3.4)                                                                                                                                                                                                                                                                       | admin groups `period.status`, admin practice-periods                                   |
| `WorkDays`                         | bitmask; sozlamada `"1,2,3,4,5,6"` (1=Du … 7=Ya)                                                                                                                                                                                                                                                                                          | settings `workDays`                                                                    |
| `StudentStatus` (domain, akademik) | `active` · `suspended` · `graduated`                                                                                                                                                                                                                                                                                                      | `TutorStudentDetail.status`                                                            |
| `TodayFilter` (query)              | `present` · `late` · `absent` · `excused` · `pending` · `suspicious`                                                                                                                                                                                                                                                                      | tutor today `?status=`                                                                 |
| `TodayAlertKind`                   | `outOfRadius` · `notCheckedIn` · `newApplications` (v3.10: `newLeaveRequests` olib tashlandi)                                                                                                                                                                                                                                                                   | tutor today alerts                                                                     |
| `StudentState`                     | `active` · `redFlag` · `suspicious`                                                                                                                                                                                                                                                                                                       | tutor students (+ detail), company students                                            |
| `MapPointKind`                     | `ok` · `late` · `bad`                                                                                                                                                                                                                                                                                                                     | tutor map                                                                              |
| `FacultyStatus`                    | `active` · `attention`                                                                                                                                                                                                                                                                                                                    | admin faculties                                                                        |
| `TutorStatus`                      | `active` · `late`                                                                                                                                                                                                                                                                                                                         | admin tutors, dashboard                                                                |
| `TutorScopeLevel`                  | `faculty` · `department` · `direction` · `group`                                                                                                                                                                                                                                                                                          | admin tutors `scopes[].level`, `PUT .../scopes` body                                   |
| `AdminStudentStatus`               | `active` · `flagged` · `unlinked`                                                                                                                                                                                                                                                                                                         | admin students                                                                         |
| `CompanyFlag`                      | `suspicious` · `tooManyStudents` · `largeRadius` · `null` — ustuvorlik aynan shu tartibda                                                                                                                                                                                                                                                 | admin companies, tutor companies                                                       |
| `AuditAction`                      | `created` · `updated` · `deleted` · `manualOverride` · `loggedIn` · `loginFailed` · `manualCheckIn` · `radiusChanged` · `applicationApproved` · `applicationReturned` · `applicationRejected` · `leaveApproved` · `leaveRejected` · `diaryReviewed` · `gradeChanged` · `gradeReverted` · `settingsChanged` · `attendanceMarkedSuspicious` · `faculty/department/direction/group` × `Created/Updated/Deleted/Activated/Deactivated` (masalan `facultyCreated`, `groupDeactivated`) · `tutorCreated` · `tutorUpdated` · `tutorActivated` · `tutorDeactivated` · `tutorPasswordReset` · `tutorScopesChanged` · `studentsImported` · `company*` (§6.9) · `studentsAssignedToCompany` · `practicePeriodCreated` · `practicePeriodUpdated` · `practicePeriodGroupsChanged` · `practicePeriodClosed` · `practicePeriodDeleted` · `companyQrRotated` (v3.7) · `studentPasswordSet` · `passwordChanged` (v3.8) · `telegramLinked` (v3.9) · `studentCompanyReassigned` | admin audit `action`, `?action=`                                                       |
| `SettingType`                      | `int` · `bool` · `weekdays`                                                                                                                                                                                                                                                                                                               | settings `type`                                                                        |
| `SettingKey` (string const)        | `geofenceRadius` · `lateTolerance` · `minGpsAccuracy` · `autoCheckout` · `workDays` · `dailyReportRequired` · `minReportLength` · `diaryPdfRequired` · `checkInWindow` · `checkinPhotoRequired` · `checkinQrRequired` · `maxStudentsPerCompany`                                                                                                                                    | settings                                                                               |
| `DocumentTemplateKind`             | `contract` · `referral` · `reference`                                                                                                                                                                                                                                                                                                     | settings templates                                                                     |
| `StoredFileKind`                   | `contract` · `diaryAttachment` · `leaveDocument` · `template` · `checkInPhoto`                                                                                                                                                                                                                                                            | ichki (files ko'lami); `AttendancePunch.photoUrl`                                      |
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
| `qrInvalid`         | **409** | QR kod bu amaliyot joyiga tegishli emas.                            |

Shu xabarlar `TodayDto.checkin.note` da ham keladi (amal hozir mumkin bo'lmasa).

### 3.3 Konstantalar (backend)

`DIARY_MIN_CHARS` — sozlama `minReportLength` (default **150**, `TodayDto.diary.minChars` dan oling) · `DIARY_MAX_CHARS 10000` ·
`DIARY_MAX_FILES 5` · `DIARY_MAX_FILE_BYTES 5 MB` · `RADIUS 50..1000 / step 50` · `DEFAULT_RADIUS 200` ·
`CHECKLIST_ITEMS 7` (indeks 0..6) · `COMMENT_MAX 1000` (ariza, kundalik) ·
`TUTOR_POINTS 0..20` · `REFERENCE_POINTS 0..10` · `DIARY_SCORE 1..5` · `PAGE_SIZE default 20, max 100` · `Q_MAX 100` ·
`CHECKIN_PHOTO_MAX_BYTES 5 MB` · `CHECKIN_REQUEST_MAX_BYTES 6 MB` · `ATTENDANCE_RANGE_MAX_DAYS 400` (`GET /api/tutor/students/{id}/attendance`) ·
`maxStudentsPerCompany` — sozlama (default **10**, 1..200).

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

### 4.6 Davr tanlash qoidasi (v3.5, `PeriodSelection` — Domain)

Bitta guruhga bir o'quv yilida bir nechta **kesishmaydigan** davr biriktirilishi mumkin (kuzgi + bahorgi). Guruh G va
sana D (Toshkent) uchun (o'chirilgan davrlar hisobga olinmaydi; yopilganlari — tarix va statistika uchun olinadi):

- **ongoing** — D ni o'z ichiga olgan, yopilmagan davr (ko'pi bilan bitta);
- **lastEnded** — tugagan davrlardan eng so'nggisi (`endDate` bo'yicha): `endDate < D`, yoki muddatidan oldin yopilgan
  (`closed`, `startDate ≤ D`);
- **upcoming** — `startDate > D` bo'lgan eng yaqin yopilmagan davr.

| Maqsad (`PeriodPurpose`) | Tartib                              | Qayerda                                                                                                   |
| ------------------------ | ----------------------------------- | --------------------------------------------------------------------------------------------------------- |
| `ongoing`                | ongoing                             | check-in / check-out, kundalik yozish                                                                     |
| `default`                | ongoing → lastEnded → upcoming      | admin/tyutor ro'yxatlari, statistika, dashboard, guruhlar, baholash, hisobotlar, talaba profili, portfolio |
| `enrollment`             | ongoing → upcoming                  | `POST /api/student/place`, admin `assign-company`                                                         |
| `current`                | ongoing → upcoming → lastEnded      | TWA `today`, `GET /api/student/place`                                                                     |

Natija: ikki davr oralig'ida statistika/profil tugagan kuzgi davr bo'yicha qoladi (kelajakdagi bo'sh davrga o'tib
ketmaydi), check-in rad etiladi ("hali boshlanmagan: <nom>, <sana>"), ariza esa bahorgi davrga beriladi.
Kalendarlar (tyutor/TWA) har kunni o'zini o'z ichiga olgan davr bilan chizadi.

### 4.7 Aktiv korxona (v3.14, `ActiveCompanyQueries` — Application/Common/Practice)

Talabaning **hozir** amaliyot o'tayotgan korxonasi. Ariza quyidagilarning **hammasiga** mos bo'lishi kerak:

- `status == approved` (`submitted`/`revisionNeeded`/`rejected`/`transferred`/`completed` emas);
- davr o'chirilmagan va yopilmagan (`status != closed`);
- bugun (Toshkent) `startDate ≤ bugun ≤ endDate`;
- talabaning joriy guruhi hali shu davrga biriktirilgan.

Bir nechta mos kelsa — eng so'nggi `decidedAt`. Mos ariza yo'q → `null`; oldingi (yopilgan/tugagan) yoki kelgusi davr
korxonasiga **fallback yo'q**. Qayerda: `GET /api/admin/students` (`company`), `GET /api/tutor/students` (`company`),
`GET /api/tutor/today` (`rows.items[].company`), `GET /api/admin/students/{id}` · `GET /api/tutor/students/{id}` ·
`POST /api/admin/students/{id}/company` (`activeCompany`); v3.15 dan korxona sahifalari ham: `GET /api/admin/companies`
(`students`, `suspiciousDays`, `overLimit`), `GET /api/admin/companies/{id}` · `GET /api/tutor/companies/{id}` (`students`,
`totalStudents`, `suspiciousDays`, `periods`), `…/companies/{id}/students` (admin, tyutor), `GET /api/tutor/companies`.
Korxona bo'yicha sanashda har talaba ko'pi bilan **bitta** korxonada hisoblanadi (o'sha "eng so'nggi `decidedAt`").
Tarix ko'rinishlari (profildagi davrga bog'liq `company`, davr statistikasi
`…/practice-periods/{id}/groups/{groupId}/students`) — o'z davri bo'yicha, bu qoidaga bo'ysunmaydi.

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
| A7  | `Tutor`                      | `phone:"+998 90 111 22 33"`, `assigned:"AT · 412-22, 413-22"`                                                       | `phone: string\|null` (E.164 xom), `faculties: FacultyRef[]` (`{id,code,name}`, nom bo'yicha), `groups: string[]`; + `oldestPendingAt`, `avgDecisionHours`, `lastActiveAt`, `isActive` | formatlashni frontend qilsin (kodlar: `faculties.map(f => f.code).join(', ')`)   |
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

---

## 6. v2 → v3 o'zgarishlar (frontend agentlari uchun)

15.09.2026 (v2) dan keyin backend'ga tushgan hamma narsa. **Breaking** — mavjud frontend kodini buzadi.

### 6.1 Yangi endpoint'lar (8 ta)

| #   | Endpoint                                            | Policy      | Javob                                   | Bo'lim |
| --- | --------------------------------------------------- | ----------- | --------------------------------------- | ------ |
| N1  | `GET /api/admin/companies/{id}`                     | `AdminOnly` | `CompanyDetail` · 404                  | §2.3   |
| N2  | `GET /api/admin/companies/{id}/students`            | `AdminOnly` | `CompanyStudent[]` · 404               | §2.3   |
| N3  | `GET /api/tutor/students/{id}`                      | `TutorOnly` | `TutorStudentDetail` · 404             | §2.5   |
| N4  | `GET /api/tutor/students/{id}/attendance?from=&to=` | `TutorOnly` | `StudentAttendanceDay[]` · 400 · 404  | §2.5   |
| N5  | `GET /api/tutor/students/{id}/diaries`              | `TutorOnly` | `TutorDiaryEntry[]` · 404              | §2.5   |
| N6  | `GET /api/tutor/companies`                          | `TutorOnly` | `TutorCompany[]`                        | §2.5   |
| N7  | `GET /api/tutor/companies/{id}`                     | `TutorOnly` | `CompanyDetail` · 404                  | §2.5   |
| N8  | `GET /api/tutor/companies/{id}/students`            | `TutorOnly` | `CompanyStudent[]` · 404               | §2.5   |

Jami endpoint: **69 → 77** (Admin 39 → 41, Tutor 13 → 19; Auth/Reports/Student/Files o'zgarmadi).

Yangi TS tiplar: `CompanyDetail`, `CompanyStudent`, `TutorCompany`, `TutorStudentDetail`, `StudentCompany`,
`StudentApplication`, `StudentPeriod`, `AttendanceSummary`, `DiarySummary`, `StudentGrade`, `StudentAttendanceDay`,
`AttendancePunch`, `CheckinFormData`.

### 6.2 Qo'shilgan maydonlar va qiymatlar (buzmaydi)

| #   | Qayer                                | O'zgarish                                                                                          | Nima qilish                                                     |
| --- | ------------------------------------ | ---------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| A1  | `CompanyRow` (`GET /api/admin/companies`) | + `maxStudents: number`, + `overLimit: boolean`                                                  | tipga qo'shing; `overLimit` da ogohlantirish belgisi              |
| A2  | `CompanyFlag`                        | + **`tooManyStudents`**; ustuvorlik `suspicious` → `tooManyStudents` → `largeRadius` → `null`      | union tipga qiymat va label/rang qo'shing                        |
| A3  | `SettingKey`                         | + **`checkinPhotoRequired`** (bool, default `false`), + **`maxStudentsPerCompany`** (int, `10`, 1–200) | union tipga qo'shing; settings sahifasida ikkita qator ko'payadi |
| A4  | `StoredFileKind`                     | + **`checkInPhoto`**                                                                                | `GET /api/files/{id}` ko'lami kengaydi (§2.2)                    |
| A5  | `AdminSettingsDto.settings[]`        | ro'yxat oxiriga 2 element qo'shildi                                                                 | ro'yxatni qotirmang — serverdan kelganini ko'rsating            |
| A6  | `StudentStatus`                      | ilgari API'ga chiqmasdi, endi `TutorStudentDetail.status` da keladi                                 | `active \| suspended \| graduated` label'ini qo'shing             |

### 6.3 Breaking — check-in/check-out `multipart/form-data` ga o'tdi

| #   | Nima                                                                                         | Ta'sir                                                                                          |
| --- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| B1  | `POST /api/student/checkin` va `/checkout` endi ikkita action: `multipart/form-data` (`[Consumes]` bilan) va JSON (cheklovsiz fallback) | `Content-Type` **majburiy** bo'lib qoldi                 |
| B2  | **`Content-Type` yuborilmasa yoki noto'g'ri bo'lsa → 415** (v2 da tana JSON deb qabul qilinardi) | `fetch`/axios sozlamasini tekshiring; xulq to'g'ri va integratsiya testi bilan qoplangan        |
| B3  | JSON yuborishda sarlavha qo'lda qo'yilmasa `fetch` `text/plain` qo'yadi → **415**              | `headers: { 'Content-Type': 'application/json' }` ni aniq yozing                                  |
| B4  | `checkinPhotoRequired=true` bo'lsa **JSON yo'li umuman ishlamaydi** → 400 `errors.Photo`       | TWA'da selfi oqimini yoqing; offline navbat ham multipart bo'lsin                                 |
| B5  | Multipart tana 6 MB dan oshsa → **413** (ProblemDetails'siz, Kestrel)                         | rasmni klientda 5 MB gacha siqing                                                                 |

Qolganlari o'zgarmagan: javob — o'sha `TodayDto`; `lat`/`lng`/`accuracy`/`occurredAt` maydonlari, ularning
validatsiyasi, rad etish sabablari (§3.2) va idempotentlik ham avvalgidek.

### 6.4 Xulq-atvor o'zgarishlari

- Rad etilgan check-in urinishining **rasmi ham saqlanadi** (tyutor shubhani tekshirishi uchun); faqat
  `errors.Photo` validatsiyasida hech narsa saqlanmaydi.
- `GET /api/files/{id}` ko'lami `checkInPhoto` ni ham qamraydi: talaba — o'z selfilari, tyutor — ko'lamidagi
  talabalar selfilari, admin — hammasi; aks holda **404**.
- Korxona endpoint'larida tyutor uchun **404 (403 emas)**: ko'lamida biriktirilgan talabasi bo'lmagan korxona
  "yo'q" hisoblanadi.
- Yangi migratsiya: `20260916115017_CheckInPhotos` (`attendance_events.photo_file_id`,
  `daily_attendances.check_in_photo_file_id`, `daily_attendances.check_out_photo_file_id`).

### 6.5 Hali ham yo'q (frontend mock'da qolsin)

Korxona **yaratish/tahrirlash** (admin), talaba **ariza yuborish** (`POST /api/student/place`) va shartnoma
fayli yuklash, davomatni qo'lda tuzatish — endpoint'lari hali yo'q.

### 6.6 v3 → v3.1 (17.09.2026): admin talaba profili

Admin panelida talabaning "ichiga kirish" yo'li yo'q edi — `GET /api/admin/students` faqat ro'yxat berardi.
Uchta endpoint qo'shildi (Admin 41 → 44, jami 77 → 80):

| #   | Endpoint                                             | Policy      | Javob                                  | Bo'lim |
| --- | ---------------------------------------------------- | ----------- | -------------------------------------- | ------ |
| N9  | `GET /api/admin/students/{id}`                       | `AdminOnly` | `AdminStudentDetail` · 404             | §2.3   |
| N10 | `GET /api/admin/students/{id}/attendance?from=&to=`  | `AdminOnly` | `StudentAttendanceDay[]` · 400 · 404   | §2.3   |
| N11 | `GET /api/admin/students/{id}/diaries`               | `AdminOnly` | `TutorDiaryEntry[]` · 404              | §2.3   |

- Yangi TS tiplar: `AdminStudentDetail`, `AdminStudentTutor`.
- Javob shakli tyutor profili bilan bir xil (backend'da ayni handler), shuning uchun frontend'da
  `StudentDetailView`/`StudentAttendanceSection`/`StudentDiarySection` komponentlari qayta ishlatiladi —
  davomat/kundalik so'rovlari `area: 'tutor' | 'admin'` bilan yo'naltiriladi.
- Ro'yxatdagi talaba ismi endi `/admin/students/{id}` ga havola (tyutor/korxona jadvallaridagidek).
- **Korxona detalidagi talabalar jadvalida** ham ism profil havolasi: admin → `/admin/students/{studentId}`,
  tyutor → `/tutor/students/{studentId}` (`CompanyStudent.studentId` allaqachon javobda bor edi).
  UI kitdagi `PersonCell` ixtiyoriy `to` prop oldi.
- UI: "Kun-bakun davomat" bo'limi **"Kundalik jadval"** deb nomlandi (ikkala profilda). Sana bosilsa —
  kun **oynasi** (modal): kirish va chiqish alohida (vaqt, masofa, aniqlik, **talaba yuborgan koordinata** +
  xarita, **yuborgan selfi**) va **shu kunga yozgan kundaligi** — matn, o'rgangani, **biriktirilgan fayllar
  joyida ochiq** (PDF `<iframe>`, rasm `<img>` — bosish kerak emas) + **fayl nomi havola**: bosilsa blob
  yangi oynada to'liq ochiladi ("Yangi oynada ochish" / "Yuklab olish"), ball va tyutor izohi.
  Endpoint'lar o'zgarmadi — hammasi `StudentAttendanceDay` va `TutorDiaryEntry` ichidagi maydonlardan.
- Profildagi alohida **"Kundaliklar" ro'yxati olib tashlandi** (ikkala profilda): o'sha ma'lumot kundalik
  jadvalining kun oynasida, sana bo'yicha ko'rinadi. `GET .../diaries` endpoint'i o'zgarmadi — kun oynasi
  o'sha javobdan kerakli kunni oladi (bitta so'rov, TanStack keshi jadval bilan bo'linadi).
- **Kundalikni baholash kun oynasidan** — tyutor ham, admin ham: `POST /api/{tutor|admin}/diaries/{id}/review`
  (N12, quyida). Ball qo'yilgach jadvaldagi "Kundalik" ustuni, kundalik statistikasi va yakuniy baho yangilanadi.

| #   | Endpoint                                | Policy      | Javob                                | Bo'lim |
| --- | --------------------------------------- | ----------- | ------------------------------------ | ------ |
| N12 | `POST /api/admin/diaries/{id}/review`   | `AdminOnly` | `TutorDiaryEntry` · 400 · 404 · 409  | §2.3   |

### 6.7 Fayllarni ochish — `/api/files/{id}` token talab qiladi

`GET /api/files/{id}` — `Authenticated` policy ostida va javobda `Content-Disposition: attachment`.
Demak oddiy `<a href="/api/files/…" target="_blank">` **ishlamaydi**: yangi oynaga token ketmaydi (401),
ketgan taqdirda ham brauzer ko'rsatmay yuklab oladi. Talaba kundalikni daftardan **rasmga olib PDF qilib**
yuboradi, tyutor/admin esa uni ko'rishi kerak — shuning uchun dashboard'da barcha fayl havolalari
`shared/files/AuthFileButton` ga o'tkazildi:

1. token bilan `fetch` → `blob` → `URL.createObjectURL`;
2. `application/pdf` → modal ichida `<iframe>`, `image/*` → `<img>`, boshqasi → yuklab olish;
3. `frame-src 'self' blob:` — `dashboard/nginx.conf` CSP'siga qo'shildi (aks holda blob iframe bloklanadi).

Ta'sir qilgan joylar: kundalik fayllari (tyutor sahifasi va talaba profili), shartnoma ("shartnomani ochish",
"brauzerda ochish"), ruxsat so'rovi hujjati, admin sozlamalaridagi shablonlar. **TWA'da o'sha naqsh hali eski**
(`twa/src/features/diary/components/DiaryEntryCard.tsx`, `leave/components/LeaveRequestList.tsx`).

Demo seed endi har 3-kundalikka bir betli PDF biriktiradi (`DemoFiles.OnePagePdf`) — stendda fayl oqimini
ko'rish uchun; selfi va shartnoma fayllari seed qilinmaydi (ular haqiqiy yuklashdan keladi).

### 6.8 v3.1 → v3.2 (17.09.2026): talabalar Excel importi

- **HEMIS'dan tortish yo'q.** `/admin/students` toolbar'idagi "HEMIS dan tortish" tugmasi olib tashlandi —
  HEMIS API integratsiyasi 2-faza (`QURISH-TARTIBI` M17). Talabalar bazasi **Excel import** orqali quriladi.
- Yangi endpoint'lar (2 ta):

| #   | Endpoint                                       | Policy      | Javob                                  | Bo'lim |
| --- | ---------------------------------------------- | ----------- | -------------------------------------- | ------ |
| N13 | `GET /api/admin/students/import/template`      | `AdminOnly` | `.xlsx` fayl (3 varaq)                 | §2.3   |
| N14 | `POST /api/admin/students/import` (multipart)  | `AdminOnly` | `StudentImportResult` · 400            | §2.3   |

- Frontend: `/admin/students` toolbar'ida **"Shablon"** (yuklab olish) va **"Excel import"** (modal) tugmalari;
  hech narsa qilmaydigan "Excel" (eksport) tugmasi olib tashlandi — davomat eksporti M14 da (`GET /api/reports`).
  Modal: shablonni yuklab olish → faylni tanlash → import → hisobot (qo'shildi / qabul qilinmadi / jami +
  rad etilgan qatorlar jadvali). Shablon token talab qilgani uchun `shared/files/downloadAuthFile` bilan olinadi.
- `AuditAction` ga `studentsImported = 48` qo'shildi (baza qiymatlari o'zgarmagan).
- **Kun oynasi tartibi o'zgardi** (faqat UI, API shakli emas): chap ustunda — kun ma'lumotlari (vaqt,
  masofa, aniqlik, xarita, sanoqlar, kundalik matni va baholash); o'ng ustunda (kengroq) — talaba yuborgan
  fayllar bir joyda va aniq ko'rinadigan o'lchamda: check-in/check-out selfisi va kundalik fayllari
  (`DayFilesPanel`). Oyna deyarli butun ekran eniga cho'ziladi (`min(1720px, 98vw)`), fayl ustuni
  kengroq ulush oladi (1fr/1.35fr). Shu sababli `DiaryDayCard` endi `showFiles={false}` bilan
  chaqiriladi — fayllar kartada takrorlanmaydi; `AuthFileEmbed` ga `size="lg"` (PDF/rasm
  `min(78vh, 720px)`) qo'shildi, boshqa joylardagi fayllar avvalgi 420px o'lchamida qoldi.

### 6.9 v3.2 → v3.3 (18.09.2026): korxona CRUD, STIR oqimi va ommaviy biriktirish

Endi korxonani **admin oldindan kiritadi**, talaba esa faqat STIR yozadi — qo'lda ma'lumot kiritish yo'q.
Ikkinchi yo'l: admin talabalar ro'yxatidan bir nechtasini belgilab, to'g'ridan-to'g'ri korxonaga biriktiradi.

| #   | Endpoint                                             | Policy          | Javob                        | Bo'lim |
| --- | ---------------------------------------------------- | --------------- | ---------------------------- | ------ |
| N15 | `POST /api/admin/companies`                          | `AdminOnly`     | `CompanyDetail` · 400 · 409  | §2.3   |
| N16 | `PUT /api/admin/companies/{id}`                      | `AdminOnly`     | `CompanyDetail` · 404 · 409  | §2.3   |
| N17 | `PATCH /api/admin/companies/{id}/status`             | `AdminOnly`     | `CompanyDetail` · 404        | §2.3   |
| N18 | `DELETE /api/admin/companies/{id}`                   | `AdminOnly`     | 204 · 404 · 409              | §2.3   |
| N19 | `GET /api/admin/companies/import/template`           | `AdminOnly`     | `.xlsx`                      | §2.3   |
| N20 | `POST /api/admin/companies/import` (multipart)       | `AdminOnly`     | `ImportResult` · 400         | §2.3   |
| N21 | `POST /api/admin/students/assign-company`            | `AdminOnly`     | `AssignCompanyResult` · 409  | §2.3   |
| N21a | `POST /api/admin/students/{id}/company`             | `AdminOnly`     | `AdminStudentDetail` · 400 · 404 · 409 | §2.3 |
| N22 | `GET /api/companies/lookup?tin=`                     | `Authenticated` | `CompanyLookupDto` · 404     | §2.7   |
| N23 | `POST /api/student/place`                            | `StudentOnly`   | `PracticePlaceDto` · 409     | §2.6   |

- **Nofaol korxona ko'rinmaydi**: `GET /api/companies/lookup` faqat `isActive` korxonani topadi, shuning uchun
  faolsizlantirish — talabalarni yangi arizalardan to'sishning oddiy yo'li. O'chirish esa faqat
  faolsizlantirilgan va **talabasi yo'q** korxona uchun mumkin (409 qoidalari §2.3).
- `ImportResult`/`ImportError` endi **umumiy shakl** (`Common/Models`): talabalar importi ham, korxonalar
  importi ham shu javobni qaytaradi (JSON maydonlari o'zgarmagan).
- Yangi `AuditAction` qiymatlari: `companyCreated=49 · companyUpdated=50 · companyActivated=51 ·
  companyDeactivated=52 · companyDeleted=53 · companiesImported=54 · studentsAssignedToCompany=55`.
- Frontend: korxonalar toolbar'idan ishlamaydigan "Excel" va "Shubhali to'planishlar" tugmalari olib tashlandi,
  o'rniga "Yangi korxona", "Shablon", "Excel import"; talabalar jadvalida `№` ustuni, qator belgilash (checkbox)
  va "Korxonaga biriktirish"; TWA'da STIR orqali joy tanlash oqimi.
- **Koordinata qo'lda yozilmaydi**: korxona formasida `lat`/`lng` inputlari o'rniga **xarita** (Leaflet + OSM,
  `@/shared/ui/map-picker`) — klik yoki markerni sudrash bilan nuqta tanlanadi, radius doira bo'lib ko'rinadi.
  **API shakli o'zgarmagan** — serverga baribir `lat`/`lng` sonlari ketadi; Excel importda koordinata ustunlari
  qoladi (u yerda xarita yo'q). Nuqta tanlanmasa klient validatsiyasi to'xtatadi:
  `"Xaritadan korxona joylashuvini belgilang."` Xarita ostida **"Hozirgi joylashuvim"** tugmasi bor —
  brauzer geolokatsiyasi (faqat `https` yoki loopback — `localhost`/`127.x`); ruxsat berilmasa yoki aniqlik yomon bo'lsa
  foydalanuvchiga izoh chiqadi, marker baribir qo'lda sudraladi.
- **Koordinata qo'lda yozilmaydi**: korxona formasida `lat`/`lng` inputlari o'rniga **xarita** (Leaflet + OSM,
  `@/shared/ui/map-picker`) — klik yoki markerni sudrash bilan nuqta tanlanadi, radius doira bo'lib ko'rinadi.
  **API shakli o'zgarmagan** — serverga baribir `lat`/`lng` sonlari ketadi; Excel importda koordinata ustunlari
  qoladi (u yerda xarita yo'q). Nuqta tanlanmasa 400 emas, klient validatsiyasi:
  `"Xaritadan korxona joylashuvini belgilang."`

### 6.10 v3.3 → v3.4 (23.09.2026): admin amaliyot davrlari

Davrlar endi faqat seed'da emas — admin o'zi yaratadi va boshqaradi (§2.3.4).

| #   | Endpoint                                             | Policy      | Javob                                      | Bo'lim  |
| --- | ---------------------------------------------------- | ----------- | ------------------------------------------ | ------- |
| N24 | `GET /api/admin/practice-periods?status=`            | `AdminOnly` | `PracticePeriodListItem[]` · 400           | §2.3.4  |
| N25 | `GET /api/admin/practice-periods/{id}`               | `AdminOnly` | `PracticePeriodDetail` · 404               | §2.3.4  |
| N26 | `POST /api/admin/practice-periods`                   | `AdminOnly` | 201 `PracticePeriodDetail` · 400 · 409     | §2.3.4  |
| N27 | `PUT /api/admin/practice-periods/{id}`               | `AdminOnly` | `PracticePeriodDetail` · 400 · 404 · 409   | §2.3.4  |
| N28 | `PUT /api/admin/practice-periods/{id}/groups`        | `AdminOnly` | `PracticePeriodDetail` · 400 · 404 · 409   | §2.3.4  |
| N29 | `POST /api/admin/practice-periods/{id}/close`        | `AdminOnly` | `PracticePeriodDetail` · 404 · 409         | §2.3.4  |
| N30 | `DELETE /api/admin/practice-periods/{id}`            | `AdminOnly` | 204 · 404 · 409                            | §2.3.4  |

- Jami endpoint: **92 → 99** (Admin 54 → 61).
- Yangi `AuditAction` qiymatlari: `practicePeriodCreated=56 · practicePeriodUpdated=57 · practicePeriodGroupsChanged=58 ·
  practicePeriodClosed=59 · practicePeriodDeleted=60` (bazada int — migratsiya yo'q).
- `PracticePeriodStatus` admin API'da **hisoblanadi** (`planned` = boshlanmagan ochiq davr); bazadagi qiymat va
  talaba/tyutor oqimlari o'zgarmagan.
- `GroupRow.period` (§2.3.2): faol davri yo'q guruhda endi eng yaqin `planned` davr keladi (avval `null` edi) — shakl
  o'zgarmagan, faqat qiymat.
- Qo'shimcha qoidalar: qayta `/close` → 409; PUT `/groups` da bo'sh ro'yxat ruxsat etiladi.

### 6.11 v3.4 → v3.5 (23.09.2026): bir guruhda bir nechta davr

Bitta guruh bir o'quv yilida bir nechta davrga (kuzgi, bahorgi) biriktiriladi. Barcha sirtlar endi yagona qoidadan
foydalanadi (§4.6); avvalgi "guruhning bitta faol davri" farazi (va `status == active` filtri) olib tashlandi.
Yangi endpoint yo'q, migratsiya yo'q; barcha o'zgarishlar **qo'shimcha** (buzmaydi).

| Endpoint                                                          | O'zgarish                                                                                           |
| ----------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `GET /api/admin/students/{id}` · `GET /api/tutor/students/{id}`   | `?periodId=` (ixtiyoriy, begona → 404); javobda `periods: StudentPeriodOption[]`, `selectedPeriodId` |
| `GET /api/{admin,tutor}/students/{id}/attendance`                 | `?periodId=`; `from/to` davr chegaralariga qisiladi; begona → 404                                    |
| `GET /api/{admin,tutor}/students/{id}/diaries`                    | `?periodId=` — tanlangan davr yozuvlari (avval — hammasi); begona → 404                              |
| `GET /api/student/today`                                          | `period: StudentPeriodOption \| null`; tanaffusda `note` = "…hali boshlanmagan: <nom>, <sana> dan boshlanadi." |
| `POST /api/student/checkin` · `checkout`                          | faqat davom etayotgan davr; aks holda 400 shu `note` matni bilan (hodisa yozilmaydi)               |
| `POST /api/student/place`                                         | davr = davom etayotgan → eng yaqin kelgusi (bahorgi davrga oldindan ariza)                          |
| `GET /api/student/place`                                          | davr = davom etayotgan → kelgusi → oxirgi tugagan                                                   |
| `GET /api/student/portfolio`                                      | `?periodId=`; javobda `periodId`, `periods`                                                         |
| `GET /api/student/diary`                                          | har yozuvda `periodId`, `periodName`                                                                |
| `GET /api/student/calendar`                                       | har kun o'z davri bilan (oy ikki davrni qamrashi mumkin)                                            |
| ~~`POST /api/student/leave-requests`~~ (v3.10 da olib tashlandi)  | davr — sanalarni qamragan yopilmagan guruh davri (kelgusi ham)                                      |
| `GET /api/admin/groups` · `students` · `dashboard` · `faculties`  | sukut bo'yicha davr: tanaffusda tugagan davr (`period.status` `closed` bo'lishi mumkin)             |
| `GET /api/tutor/students` · `grading` · `today` · `companies`     | sukut bo'yicha davr; korxona statistikasi — ariza davri bo'yicha                                     |
| `GET /api/reports`                                                | `filter.dateFrom/dateTo` — ko'lam guruhlarining sukut bo'yicha davrlari                              |

- `GroupRow.period` — endi sukut bo'yicha davr (§4.6); v3.4 dagi "faol yo'q bo'lsa eng yaqin planned" o'rniga
  "davom etayotgan → oxirgi tugagan → eng yaqin planned". Yopilgan davr ham qaytishi mumkin (`status: "closed"`).
- `StudentPeriodOption.status` — `PracticePeriod.ResolveStatus` (admin API bilan bir xil): tugagan, lekin yopilmagan davr
  `active` bo'lib qoladi — frontend "tugagan" belgisini `endDate < bugun` dan chiqarishi mumkin.
- Demo seed (faqat bo'sh baza): ikkinchi davr "Bahorgi amaliyot 2027" (2027-02-01…2027-03-15), 412-22 va 413-22 guruhlari.

### 6.12 v3.5 → v3.6 (24.09.2026): admin davr statistikasi

Admin "Amaliyot davrlari" → davr ichida guruhlar va talabalar natijalari (§2.3.4). Faqat qo'shimcha (buzmaydi),
migratsiya yo'q.

| #   | Endpoint                                                          | Policy      | Javob                          | Bo'lim  |
| --- | ----------------------------------------------------------------- | ----------- | ------------------------------ | ------- |
| N31 | `GET /api/admin/practice-periods/{id}/stats`                      | `AdminOnly` | `PracticePeriodStats` · 404    | §2.3.4  |
| N32 | `GET /api/admin/practice-periods/{id}/groups/{groupId}/students`  | `AdminOnly` | `PeriodGroupStudents` · 404    | §2.3.4  |

- Jami endpoint: **99 → 101** (Admin 61 → 63).
- Hisob faqat aniq shu davr yozuvlari bo'yicha (sukut bo'yicha davr tanlash §4.6 ishlatilmaydi); davomat §4.2, ball/baho §4.4.

### 6.x (24.09.2026): `StudentAttendanceDay.events` — kundagi barcha urinishlar rasmi bilan

Faqat qo'shimcha maydon (buzmaydi), yangi endpoint va migratsiya yo'q. `GET /api/tutor/students/{id}/attendance` va
`GET /api/admin/students/{id}/attendance` javobidagi har kunga `events: StudentAttendanceEvent[]` qo'shildi (§2.5):
qabul qilingan va rad etilgan check-in/check-out urinishlari — vaqt, sabab (`rejectReason`/`rejectMessage`),
masofa/aniqlik/radius, koordinata va selfi (`photoUrl`). Mavjud maydonlar o'zgarmadi.

### 6.13 v3.6 → v3.7 (24.09.2026): amaliyot joyida QR kod bilan check-in/check-out

Oqim: talaba korxonaga keladi → korxonada osilgan QR'ni TWA'da skanerlaydi → GPS → selfi → check-in (check-out ham
xuddi shunday). Backend QR'ni korxona tokeni bilan solishtiradi.

- **Yangi endpoint'lar (4):** `GET/POST /api/admin/companies/{id}/checkin-qr[/rotate]` (§2.3) va
  `GET/POST /api/tutor/companies/{id}/checkin-qr[/rotate]` (§2.5, ko'lam — korxona tafsilotidagidek).
  Javob — `CompanyCheckInQrDto { companyId, companyName, payload: "AMLQR:1:<32 hex>", rotatedAt }`.
  Jami endpoint: **101 → 105** (Admin 63 → 65, Tutor 19 → 21).
- **Check-in/check-out:** yangi ixtiyoriy maydon `qr` (multipart va JSON). Noto'g'ri/begona/eskirgan QR →
  **409** `qrInvalid` ("QR kod bu amaliyot joyiga tegishli emas."), urinish selfisi bilan `AttendanceEvent` ga
  yoziladi. QR majburiy va yo'q → **400** `errors.Qr` ("Amaliyot joyidagi QR kodni skanerlang."), hodisa yozilmaydi.
- **Enum'lar:** `CheckInRejectReason` + `qrInvalid` (13); `AuditAction` + `companyQrRotated` (61).
- **Sozlamalar:** yangi `checkinQrRequired` (bool, sukut `true`); **`checkinPhotoRequired` sukuti `false` → `true`**
  (yangi o'rnatishlar uchun; mavjud bazadagi qator qiymati o'zgarmaydi — admin sozlamalar sahifasida yoqadi).
- **TWA:** `TodayDto.checkin.photoRequired` va `TodayDto.checkin.qrRequired` (bool) — talaba qaysi qadamlar
  majburiyligini shu yerdan biladi (avval `checkinPhotoRequired` talabaga umuman ko'rsatilmas edi).
- **Breaking (amalda):** sukut bo'yicha rasm ham, QR ham majburiy — rasmsiz JSON yo'li faqat
  `checkinPhotoRequired=false` bo'lsa ishlaydi (`qr` JSON'da ham yuboriladi); TWA multipart + `photo` + `qr` yuborsin.
- **Migratsiya** `CompanyCheckInQr`: `companies.check_in_qr_token` (varchar 64, unique) va `check_in_qr_rotated_at`;
  mavjud korxonalarga token backfill qilinadi.

### 6.14 v3.7 → v3.8 (24.09.2026): talaba brauzerda (HEMIS ID + parol), parol almashtirish, talaba kabineti

Bot tayyor bo'lguncha talaba TWA'ni oddiy brauzerda ham ishlatadi.

- **`POST /api/auth/login`** endi talabani ham qabul qiladi — profil HEMIS ID'si + xodim o'rnatgan parol. Paroli yo'q
  talaba → 403 "HEMIS ID yoki parol noto'g'ri." (hisob borligi oshkor qilinmaydi). Xato kodi — mavjud qoida bo'yicha
  **403** (401 emas).
- **`AuthResultDto.mustChangePassword: boolean`** — login/refresh/telegram javoblarining barchasida.
- **Yangi endpoint'lar (4):** `POST /api/auth/change-password` (§2.1), `POST /api/admin/students/{id}/password` (§2.3),
  `POST /api/tutor/students/{id}/password` (§2.5), `GET /api/student/profile` (§2.6).
  Jami endpoint: **105 → 109** (Auth 5 → 6, Admin 65 → 66, Tutor 21 → 22, Student 11 → 12).
- **`hasPassword: boolean`** — `TutorStudentDetail` va `AdminStudentDetail` da.
- **Xato kalitlari PascalCase** (FluentValidation konvensiyasi, §1.7): `errors.CurrentPassword`, `errors.NewPassword`,
  `errors.Password`.
- **Enum'lar:** `AuditAction` + `studentPasswordSet` (62), `passwordChanged` (63).
- **Migratsiya** `StudentWebLogin`: `users.must_change_password` (bool, not null, sukut `false`).
- **Demo seed:** talaba `341030` / `talaba12345` (mavjud demo bazada ham, parol bo'lmasa, qayta ishga tushganda o'rnatiladi).

### 6.15 v3.8 → v3.9 (24.09.2026): Telegram'ni HEMIS ID + parol bilan bog'lash

Muammo: haqiqiy talabani Telegram'ga bog'laydigan oqim yo'q edi — `POST /api/auth/telegram` bog'lanmagan talabaga
403 berardi.

- **Yangi endpoint:** `POST /api/auth/telegram/link` (§2.1) — body `{ initData, hemisId, password }` → 200
  `AuthResultDto` (bog'laydi va kiritadi). Jami endpoint: **109 → 110** (Auth 6 → 7).
- **TWA oqimi:** `POST /api/auth/telegram` 403 "Hisob topilmadi…" → HEMIS ID + parol formasi → `telegram/link` →
  `mustChangePassword` bo'lsa `change-password`. Keyingi safar `POST /api/auth/telegram` darhol 200.
- **Xatolar:** 403 (imzo / HEMIS ID-parol / faol emas / xodim hisobi), 409 (Telegram akkaunti band yoki talaba boshqa
  Telegram'ga bog'langan). Parallel so'rovlar poygasida ham 409 (unikal indeks `users.telegram_user_id`).
- **Enum:** `AuditAction` + `telegramLinked` (64).
- Migratsiya yo'q (unikal indeks `telegram_user_id` avvaldan bor).

### 6.16 v3.9 → v3.10 (24.09.2026): ruxsat so'rash moduli olib tashlandi

Foydalanuvchi qarori: talabalar ruxsat (leave) so'ramaydi — funksiya butunlay olib tashlandi.

- **Olib tashlangan endpoint'lar (4):** `GET /api/student/leave-requests`, `POST /api/student/leave-requests`,
  `GET /api/tutor/leave-requests`, `POST /api/tutor/leave-requests/{id}/decision` → endi **404**.
  Jami endpoint: **110 → 106** (Tutor 22 → 20, Student 12 → 10; `[Http*]` atributlari 112 → 108).
- **DTO/enum:** `LeaveRequestDto`, `LeaveDocumentDto`, `TutorLeaveRequest`, `LeaveDocument`, `LeaveDecision` olib
  tashlandi. `TodayAlertKind.newLeaveRequests` olib tashlandi — `GET /api/tutor/today` endi bu alertni qaytarmaydi
  (raqam `3` bo'sh qoldirildi, qayta ishlatilmaydi). Konstantalar `LEAVE_REASON`, `LEAVE_MAX_DAYS` — kontraktdan chiqdi.
- **Saqlanadi (o'zgarmaydi):** `LeaveRequest` jadvali va entity (tarix; **migratsiya yo'q**), `StudentAttendanceDay.leaveRequestId`,
  `AttendanceStatus.excused`, `CheckInRejectReason.onLeave`, bazadagi **tasdiqlangan** ruxsatlarni davomat/statistika/
  kalendar/portfolio/baholash hisoblarida hisobga olish, `GET /api/files/{id}` da ruxsat hujjatiga kirish,
  `AuditAction.leaveApproved/leaveRejected` (audit tarixi uchun; raqamlar o'zgarmadi), Domain `DailyAttendance.ManualFix`
  (qo'lda tuzatish — API endpoint'i hozircha yo'q, alohida vazifa).
- **Demo seed:** kutilayotgan (pending) ruxsat so'rovlari endi yaratilmaydi; tarix uchun 1 tasdiqlangan (+ `excused` kun)
  va 1 rad etilgan so'rov qoladi.
- **Dashboard:** tyutor "Ruxsat so'rovlari" sahifasi va nav bandi olib tashlandi; `/tutor/leave-requests` → `/tutor`
  redirect. Talaba tafsilotida `excused` kun va "Ruxsat so'rovi" manba belgisi (eski yozuvlar) saqlanadi.

### 6.17 v3.10 → v3.11 (24.09.2026): talaba bosh ekrani — davrning har bir kuni

- **Yangi endpoint:** `GET /api/student/period-days?periodId=` (§2.6) — davr ma'lumoti (`requiredDays`,
  `elapsedWorkDays`), davr tanlagichi va davrning har bir kuni (holat, check-in/out `HH:mm`, `autoClosed`,
  `suspicious`, `manual`, kundalik). Jami endpoint: **106 → 107** (Student 10 → 11; `[Http*]` atributlari 108 → 109).
- **Holat manbai:** talaba kalendari (§4.1 `AttendanceCalendar.DayStatus`) — TWA kalendar va bosh ekran bir xil ko'rsatadi.
  Tyutor kun-bakun davomatidan farqi: kelgusi ish kuni tyutorda `pending`, bu yerda `future`; tasdiqlangan ruxsat
  dam olish kuniga tushsa bu yerda `excused`, tyutorda `dayOff`.
- Mavjud endpoint'lar o'zgarmadi, migratsiya yo'q.

### 6.18 v3.11 → v3.12 (26.09.2026): davr jadvali — ish kunlari va kunlik ish vaqti

- `POST /api/admin/practice-periods` va `PUT /api/admin/practice-periods/{id}` body'lariga ixtiyoriy
  `dailyStart` / `dailyEnd` (`"HH:mm"`) va `workDays` (`"1,2,3,4,5"`, 1=Du … 7=Ya) qo'shildi (§2.3.4). Yuborilmasa:
  POST — standart `09:00`/`17:00` va global `workDays`; PUT — o'zgarmaydi. Eski klientlar buzilmaydi.
- Xatolar: 400 `errors.DailyStart` / `errors.DailyEnd` / `errors.WorkDays` (PascalCase, §1.4/§1.7 konvensiyasi; `web/shared` `ApiError.field()` camelCase kalitni ham topadi);
  check-in oynasi sig'masa → 400 `errors.DailyEnd`. Yopilgan davr → 409 (o'zgarmadi).
- Response (`PracticePeriodDetail`) o'zgarmadi. Endpoint soni o'zgarmadi, migratsiya yo'q.

### 6.19 v3.12 → v3.13 (26.09.2026): talabani bitta-bitta korxonaga biriktirish / o'tkazish

- **Yangi endpoint:** `POST /api/admin/students/{id}/company` (§2.3) — `{ companyId, comment? }` → 200 `AdminStudentDetail`.
  Jami endpoint: **107 → 108** (Admin 66 → 67; `[Http*]` atributlari 109 → 110).
- **Enum'lar:** `ApplicationStatus` + `transferred` (7) — admin boshqa korxonaga o'tkazgan, yopilgan ariza (UI: "Ko'chirilgan");
  `AuditAction` + `studentCompanyReassigned` (65).
- `transferred` joriy korxona sifatida hech qayerda tanlanmaydi; tyutor moderatsiya navbati (`status`siz) uni ko'rsatmaydi;
  `counts` o'zgarmadi. Bir davrda bitta `transferred` + bitta `approved` ariza bo'lishi mumkin (unikal indeks filtri
  `submitted/revisionNeeded/approved` — o'zgarmadi), **migratsiya yo'q**.
- `assign-company` xatti-harakati o'zgarmadi (`transferred` ariza "joriy" hisoblanmaydi).

### 6.20 v3.13 → v3.14 (26.09.2026): aktiv korxona

- **Semantika o'zgardi:** `GET /api/admin/students` va `GET /api/tutor/students` dagi `company: string | null` — endi faqat
  talabaning **aktiv korxonasi** nomi (§4.7). Avval admin ro'yxati istalgan davrdagi eng so'nggi `approved` arizaga
  (yopilgan davr ham) fallback qilardi, tyutor ro'yxati esa sukut davri (oxirgi tugagan/yopilgan ham) bo'yicha olardi —
  natijada ro'yxatda eski korxona chiqib, profilda chiqmasdi. Endi aktivi bo'lmasa `null`. `GET /api/tutor/today`
  dagi `rows.items[].company` ham shu qoidaga o'tdi (davom etayotgan davr bo'lsa natija avvalgidek).
- **Yangi maydon:** `activeCompany: { id, name, periodId, periodName } | null` — `GET /api/admin/students/{id}`,
  `GET /api/tutor/students/{id}` va `POST /api/admin/students/{id}/company` javoblarida (`AdminStudentDetail` /
  `TutorStudentDetail`). `periodId` dan mustaqil.
- Davrga bog'liq `company` / `application` / `period` bloklari **o'zgarmadi** (tanlangan davr tarixi).
- Sukut davri tanlovi (§4.6) o'zgarmadi: yopilgan davr hech qachon `ongoing` hisoblanmaydi — guruhda ochiq davom etayotgan
  davr bo'lsa, sukut shu davr.
- Endpoint soni o'zgarmadi, migratsiya yo'q.

### 6.21 v3.14 → v3.15 (26.09.2026): korxona sahifalari — faqat aktiv talabalar

- **Semantika o'zgardi (shakllar o'zgarmadi):** korxonadagi "biriktirilgan talabalar" endi **tarix emas**, faqat hozir shu
  korxonada aktiv amaliyot o'tayotganlar (aktiv korxonasi = shu korxona, §4.7). Amaliyot davri faqat aktiv biriktirishni
  aniqlash uchun ishlatiladi.
  - `GET /api/admin/companies/{id}/students`, `GET /api/tutor/companies/{id}/students` — avval qoralamadan boshqa HAMMA
    arizalar (submitted, rejected, transferred, yopilgan davrlar) chiqardi; endi faqat aktiv talabalar. `applicationStatus`
    amalda doim `approved`, `periodName` va ko'rsatkichlar — aktiv davr bo'yicha.
  - `GET /api/admin/companies` (`students`, `suspiciousDays`, `overLimit`, `flag`), `GET /api/admin/companies/{id}` va
    `GET /api/tutor/companies/{id}` (`students`, `totalStudents`, `suspiciousDays`, `overLimit`, `flag`),
    `GET /api/tutor/companies` (`students`, `totalStudents`, `attendancePct`, `suspiciousDays`, `overLimit`, `flag`) — faqat
    aktiv talabalar va ularning aktiv davri. `tooManyStudents` / `suspicious` bayroqlari ham shu sonlardan.
    Mutatsiya javoblari (`POST`/`PUT /api/admin/companies[/{id}]`, `PATCH …/status`) — o'sha `CompanyDetail`.
  - `CompanyDetail.periods` — faqat hozir davom etayotgan **ochiq** davr(lar), `students` > 0; yopilgan/tugagan/kelgusi
    davrlar chiqmaydi (bo'sh massiv normal holat).
  - `GET /api/tutor/companies` — ko'lamdagi talabalar **hozir aktiv** bo'lgan korxonalar (avval: istalgan davrda `approved`).
    Tyutor tafsiloti/talabalar/QR darvozasi o'zgarmagan (ko'lamda `approved` ariza, istalgan davr) — tarixi bor, aktivi yo'q
    korxona tafsiloti 200 va nollar bilan ochiladi.
- **`DELETE /api/admin/companies/{id}`**: himoya tarix bo'yicha qoldi (qoralamadan boshqa arizasi bor → 409). Aktiv talaba
  0 bo'lsa xabar yangi: `"Korxonada amaliyot tarixi (arizalar) bor — uni o'chirib bo'lmaydi, nofaol qiling."`; aktivlar
  bo'lsa — avvalgi xabar, N = aktiv son.
- `GET /api/admin/dashboard` `companiesActive` — o'zgarmadi: bu korxona **holati** (`isActive`) bo'yicha son, talabaga bog'liq emas.
- Endpoint soni o'zgarmadi, migratsiya yo'q.
