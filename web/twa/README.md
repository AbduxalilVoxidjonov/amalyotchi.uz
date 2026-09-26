# twa — talaba Telegram Web App

Kontrakt v2 (haqiqiy manba — backend C# DTO'lari: `src/Amaliyotchi.Application/Features/Student/StudentContracts.cs`,
`Features/Auth/AuthContracts.cs`, controller'lar `src/Amaliyotchi.Api/Controllers/Student/*.cs`).
Enum'lar camelCase string (`present`, `dayOff`, `revisionNeeded` …) — o'zbekcha yorliq/rang **frontend'da**
(`features/*/types.ts`: `ATTENDANCE_STATUS`, `DAY_STATUS`, `DIARY_STATUS`, `LEAVE_STATUS`, `APPLICATION_STATUS`).

```bash
npm run dev -w twa            # http://127.0.0.1:5174, /api → http://127.0.0.1:5080 (vite proxy)
npm run test -w twa           # vitest + MSW (mock'lar v2 shaklida)
```

## Kirish (Telegram auth)

`RootLayout` → `useAutoLogin` → `POST /api/auth/telegram { initData }` → `AuthResultDto` (refresh token
`sessionStorage`da). Backend 403 (`ProblemDetails.detail` o'zbekcha: imzo noto'g'ri / hisob bog'lanmagan / faol emas)
→ "Hisobingizni bog'lang" formasi (quyida). Boshqa xato (tarmoq, 500) → "Kirish imkoni yo'q".

### Telegram tashqarisida sinash (dev initData)

Haqiqiy Telegram'da `WebApp.initData` ishlatiladi. Oddiy brauzerda **faqat `import.meta.env.DEV`** rejimida
uni qo'lda berish mumkin (production bundle'da bu kod yo'q):

1. Imzolangan initData yasang (bot tokeni dev: `1234567890:DEV-TEST-TOKEN-amaliyotchi`; algoritm —
   `TelegramInitDataValidator.cs` bilan bir xil, `auth_date` — hozir, 24 soat amal qiladi):
   ```bash
   node scratchpad/twa-initdata.mjs 100000001        # demo talabalar: 100000001..100000006
   TG_BOT_TOKEN=... node scratchpad/twa-initdata.mjs 100000004
   ```
2. Natijani `twa/.env.development.local` ga `VITE_DEV_INIT_DATA=...` qilib yozing
   (namuna: `.env.development.local.example`) **yoki** URL'ga qo'shing: `http://127.0.0.1:5174/?initData=<satr>`
   (query ustunroq).
3. `VITE_USE_MOCKS=false` bilan `npm run dev -w twa` — real backend (`127.0.0.1:5080`) bilan ishlaydi.

Mock rejimi (`VITE_USE_MOCKS=true`): MSW istalgan initData'ni qabul qiladi; `initData=invalid` → 403 "imzo",
`initData=unlinked` → 403 "hisob topilmadi" → bog'lash formasi (`http://127.0.0.1:5174/?initData=unlinked`).
Bog'lash mock'i: `341030`/`talaba12345` → 200 · `341031`/`vaqtincha1` → 200 + majburiy parol · noto'g'ri parol → 403 ·
`341099` → 409 · `341429` → 429.

### Birinchi kirish: hisobni bog'lash

Telegram ichida `/api/auth/telegram` 403 qaytarsa (akkaunt hali bog'lanmagan) — "Hisobingizni bog'lang" formasi
(web `LoginScreen` qayta ishlatiladi): HEMIS ID + parol → `POST /api/auth/telegram/link { initData, hemisId, password }`
→ `AuthResultDto` (login bilan bir xil; `mustChangePassword` → majburiy parol ekrani; xodim roli rad etiladi).
Xatolar — `detail`; 409 da qo'shimcha "Tyutoringizga murojaat qiling.". Boshqa xato (tarmoq, 500) → "Kirish imkoni yo'q".
Keyingi ochilishlarda avtomatik kiriladi.

## Telegram'da sinash (HTTPS tunnel)

Telegram Mini App faqat `https://` manzilni ochadi — dev server tunnel orqali beriladi. `/api` Vite proxy orqali
o'tadi (`127.0.0.1:5080`), shuning uchun **bitta tunnel yetadi** (API uchun alohida tunnel kerak emas).
`vite.config.ts` `server.allowedHosts` da `.trycloudflare.com`, `.ngrok-free.app`, `.ngrok.app` ruxsat etilgan (faqat dev).

1. `brew install cloudflared`
2. API va TWA dev'ni ishga tushiring (real backend, mock emas):
   ```bash
   dotnet run --project src/Amaliyotchi.Api               # http://127.0.0.1:5080 (repo ildizidan)
   VITE_USE_MOCKS=false npm run dev -w twa                # http://127.0.0.1:5174 (web/ dan)
   ```
3. Tunnel: `cloudflared tunnel --url http://127.0.0.1:5174` → `https://<tasodifiy>.trycloudflare.com` chiqadi
   (ngrok: `ngrok http 127.0.0.1:5174`).
4. BotFather → `/mybots` → bot → *Bot Settings* → *Menu Button* → shu https manzil.
5. API haqiqiy bot tokeni bilan ishlashi kerak (aks holda initData imzosi tasdiqlanmaydi → 403). `Amaliyotchi.Api`
   loyihasida user-secrets sozlanmagan (`UserSecretsId` yo'q), shuning uchun env orqali bering:
   ```bash
   Telegram__BotToken='123456:ABC...' dotnet run --project src/Amaliyotchi.Api
   ```
   (`appsettings.Development.json` dagi dev token faqat `scratchpad/twa-initdata.mjs` bilan ishlaydi.)
6. Telegram'da botning Menu tugmasini bosing. Talaba birinchi ochganda "Hisobingizni bog'lang" formasi chiqadi —
   tyutor bergan HEMIS ID + parol bilan bog'laydi; keyingi safar avtomatik kiradi.

Eslatma: cloudflared quick-tunnel manzili har ishga tushirishda o'zgaradi — BotFather'da yangilang.

## Endpoint'lar (`shared/api/endpoints.ts`)

| Endpoint | Javob | Xatolar |
|---|---|---|
| `GET /api/student/today` | `TodayDto` (`checkin.status`, `checkInAt/checkOutAt`, `window`, `place\|null`, `note`) | — |
| `POST /api/student/checkin` · `/checkout` | `{lat,lng,accuracy,occurredAt}` → `TodayDto` | 400 oyna/GPS · 409 radius/allaqachon |
| `GET /api/student/place` | `PracticePlaceDto` (`status`, `periodFrom/To`, `contract.fileId`) | 404 biriktirilmagan |
| `GET/POST /api/student/diary` | `DiaryEntryDto[]` · multipart `text, learned?, files[]` → 201 | 400 `errors.Text` · 409 bugungisi bor |
| `GET /api/student/calendar?month=` | `{month, studentName, groupName, days[{date,status}]}` | 400 `errors.Month` |
| `GET/POST /api/student/leave-requests` | `LeaveRequestDto[]` · `{dateFrom,dateTo,reason,attachmentFileId?}` → 201 | 400 · 409 kesishuvchi |
| `GET /api/student/portfolio` | `PortfolioDto` (`score[].key`, `grade\|null`, `finalized`) | 404 davr yo'q |
