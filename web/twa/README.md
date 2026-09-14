# twa — talaba Telegram Web App

Kontrakt v2 (haqiqiy manba — backend C# DTO'lari: `src/Amaliyotchi.Application/Features/Student/StudentContracts.cs`,
`Features/Auth/AuthContracts.cs`, controller'lar `src/Amaliyotchi.Api/Controllers/Student/*.cs`).
Enum'lar camelCase string (`present`, `dayOff`, `revisionNeeded` …) — o'zbekcha yorliq/rang **frontend'da**
(`features/*/types.ts`: `ATTENDANCE_STATUS`, `DAY_STATUS`, `DIARY_STATUS`, `LEAVE_STATUS`, `APPLICATION_STATUS`).

```bash
npm run dev -w twa            # http://localhost:5174, /api → http://localhost:5080 (vite proxy)
npm run test -w twa           # vitest + MSW (mock'lar v2 shaklida)
```

## Kirish (Telegram auth)

`RootLayout` → `useAutoLogin` → `POST /api/auth/telegram { initData }` → `AuthResultDto` (refresh token
`sessionStorage`da). Backend 403 (`ProblemDetails.detail` o'zbekcha: imzo noto'g'ri / hisob bog'lanmagan / faol emas)
→ "Telegram hisobingiz bog'lanmagan" ekrani. initData umuman yo'q (oddiy brauzer) → "Kirish imkoni yo'q".

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
   (namuna: `.env.development.local.example`) **yoki** URL'ga qo'shing: `http://localhost:5174/?initData=<satr>`
   (query ustunroq).
3. `VITE_USE_MOCKS=false` bilan `npm run dev -w twa` — real backend (`localhost:5080`) bilan ishlaydi.

Mock rejimi (`VITE_USE_MOCKS=true`): MSW istalgan initData'ni qabul qiladi; `initData=invalid` → 403 "imzo",
`initData=unlinked` → 403 "hisob topilmadi".

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
