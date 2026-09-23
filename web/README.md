# Amaliyotchi — web

npm workspaces monorepo: `dashboard` (admin + tyutor SPA), `twa` (talaba Telegram Web App),
`shared` (ikkalasi uchun umumiy tokenlar, bazaviy UI kit, API client, auth). Backend — `../src` (.NET, `http://127.0.0.10:5080`).

Holat (14.09.2026): admin 9, tyutor 8, talaba 6 ekran — barchasi MSW mock bilan ishlaydi.
Backend'da faqat auth endpoint'lari bor; qolgan kontrakt — [`API-CONTRACT.md`](API-CONTRACT.md).
Testlar: shared 2, dashboard 65, twa 17 (`npm test`).

Talab: Node ≥ 22.12, npm ≥ 10 (pnpm ishlatilmaydi).

## Ishga tushirish

```bash
cd web
npm install                      # barcha workspace'lar bir joyga o'rnatiladi

npm run dev                      # dashboard → http://127.0.0.10:5173 (/api → 127.0.0.10:5080 proxy)
npm run dev:twa                  # twa       → http://127.0.0.10:5174

npm run typecheck | lint | test | build   # har workspace'da (npm run -ws ...)
npm run check                    # to'rttasi ketma-ket — PR oldidan
npm run format                   # prettier

npm run test:watch -w dashboard  # bitta workspace'da watch rejimi (twa uchun -w twa)
npm run msw:init -w dashboard    # public/mockServiceWorker.js ni qayta yaratish (msw yangilanganda)
```

### Mock rejimi (backend'siz)

Backend'da hozircha faqat `AuthController` bor. Ekranlar MSW mock'lari bilan ishlaydi — har feature'ning
`mocks.ts` fayli `/api/...` so'rovlariga javob beradi (holat xotirada, sahifa yangilanganda tiklanadi):

```bash
cd dashboard && cp .env.example .env    # VITE_USE_MOCKS=true qiling
cd ../twa && cp .env.example .env       # TWA uchun ham shu
npm run dev        # dashboard → http://127.0.0.10:5173/login
npm run dev:twa    # twa       → http://127.0.0.10:5174
```

Mock foydalanuvchilar (`dashboard/src/mocks/data.ts`):

| Rol    | Telefon        | Parol        | Kirgandan keyin |
|--------|----------------|--------------|-----------------|
| Admin  | +998901234567  | admin12345   | `/admin` (Umumiy dashboard) |
| Tyutor | +998907654321  | tutor12345   | `/tutor` (Bugun) |

Talaba (`twa/src/mocks/data.ts`): Akmal Aliyev, `412-22` guruhi — mock rejimida `POST /api/auth/telegram`
istalgan `initData` bilan talaba sessiyasini beradi (`initData: "invalid"` → 403). Oddiy brauzerda
(Telegram tashqarisida) `initData` bo'sh bo'ladi — `window.Telegram.WebApp` yo'qligi uchun "Kirish imkoni yo'q"
ko'rinadi; sinash uchun Telegram test-botidan oching yoki `@twa-dev/sdk` ni stub qiling (testlarda shunday qilingan).
Check-in mock'i haqiqiy masofani hisoblaydi — korxona nuqtasi `41.3111, 69.2797`, radius 150 m
(DevTools → Sensors bilan joylashuv qo'ying; tashqarida bo'lsangiz 409).

`public/mockServiceWorker.js` — `npm run msw:init` bilan yaratilgan; `msw` yangilanganda qayta ishga tushiring.

Production build'da `VITE_USE_MOCKS` berilmasa MSW kodi bundle'ga umuman kirmaydi (dead-code).

### `/dev/kit` — UI kit ko'rgazmasi

`npm run dev` → `http://127.0.0.10:5173/dev/kit` (login shart emas). `dashboard/src/dev/KitPage.tsx` —
dizayn tizimidagi barcha komponentlar holatlari bilan (Storybook o'rniga). Faqat `import.meta.env.DEV`
da mavjud: production build'da marshrut ham, chunk ham yaratilmaydi.

## Backend auth kontrakti (haqiqiy)

Manba: `src/Amaliyotchi.Api/Controllers/AuthController.cs`, `Application/Features/Auth/*`.
Qolgan barcha endpoint'lar (admin 9, tyutor 13, talaba 10, `/api/auth/telegram`, `/api/reports`) — backend'da
hali yo'q; frontend kutayotgan aniq shakl [`API-CONTRACT.md`](API-CONTRACT.md) da.

| Endpoint | So'rov | Javob |
|---|---|---|
| `POST /api/auth/login` | `{ phoneNumber, password }` | `AuthResultDto` \| 403 ProblemDetails |
| `POST /api/auth/refresh` | `{ refreshToken }` | `AuthResultDto` (token rotatsiya) \| 403 |
| `POST /api/auth/logout` | `{ refreshToken }` + Bearer | 204 |
| `GET /api/auth/me` | Bearer | `UserSummaryDto` |

`AuthResultDto = { accessToken, accessTokenExpiresAt, refreshToken, user: UserSummaryDto }`,
`UserSummaryDto = { id, fullName, role (1=Admin, 2=Tutor, 3=Student — raqam!), facultyId, phoneNumber }`.

- Refresh token **body'da** keladi (cookie emas). Dashboard uni `localStorage` da, TWA `sessionStorage` da saqlaydi;
  access token faqat xotirada.
- Access token 30 daqiqa, refresh 14 kun. JWT claim'lari: `sub`, `name`, `role` ("Admin"/"Tutor"/"Student"), `faculty_id`, `exp`.
- Xatolar — RFC 7807 `ProblemDetails` (`status`, `title`, `detail`, `traceId`, validatsiyada `errors: { Field: [] }`).
  Login xatosi ham **403** (401 emas). 401 faqat JwtBearer'dan, body'siz.
- `POST /api/auth/telegram` — backend'da **hali yo'q** (M02), TWA'da faqat mock.

## Papka tuzilmasi

```
web/
├── shared/src/            @amaliyotchi/shared — TS manba sifatida import qilinadi (build yo'q)
│   ├── api/               createApiClient (fetch, JSON, ProblemDetails→ApiError, 401→refresh single-flight)
│   ├── auth/              parseJwt, UserRole, DTO tiplari, createAuthStore (zustand fabrikasi)
│   ├── styles/tokens.css  dizayn tokenlari (SPEC-TOKENS) — ikkala paket @import qiladi
│   └── ui/                bazaviy UI kit (Button, Badge, Card, Input/Textarea/Select, ProgressBar, Avatar, Eyebrow)
├── dashboard/src/
│   ├── app/               App, providers (QueryClient), router (lazy routes + RequireRole guard)
│   │   ├── nav.ts         rol bo'yicha sidebar ro'yxatlari (SPEC-NAV), crumb/title
│   │   └── layout/        AppShell (sidebar + Topbar + content), Sidebar, usePageHeader, TopbarActions
│   ├── dev/KitPage.tsx    /dev/kit — UI kit ko'rgazmasi (faqat DEV, Storybook o'rniga)
│   ├── features/          (ro'yxat pastda) api.ts · types.ts · hooks.ts · mocks.ts · *Page.tsx · components/
│   ├── pages/             RootRedirect, ComingSoonPage, NotFoundPage
│   ├── shared/api/        sozlangan `api` instance, query-keys konvensiyasi
│   ├── shared/auth/       useAuthStore, useAuth, RequireRole, jwt
│   ├── shared/ui/         dizayn tizimi: shared/ui re-export + DataTable, Pill, StatTile, Topbar... (README ga qarang)
│   ├── shared/lib/        cn, date, env
│   ├── mocks/             MSW: handlers.ts (auth + feature handler'lar yig'indisi), browser.ts, server.ts (test), data.ts
│   ├── styles/            tokens.css (→ shared tokens @import), globals.css (reset + body/link/selection)
│   └── test/setup.ts      vitest + MSW + jest-dom
└── twa/src/               (pastda "TWA tuzilmasi")
```

### Dashboard feature'lari (`dashboard/src/features/`)

| Papka | Marshrut | Ekran | API |
|---|---|---|---|
| `auth/` | `/login` | Kirish (telefon + parol, zod) | `POST /api/auth/login` (haqiqiy backend) |
| `admin/dashboard/` | `/admin` | Umumiy dashboard (stat, fakultetlar, tyutorlar, audit) | `GET /api/admin/dashboard` |
| `admin/faculties/` | `/admin/faculties` | Fakultetlar jadvali | `GET /api/admin/faculties` |
| `admin/groups/` | `/admin/groups` | Guruhlar | `GET /api/admin/groups` |
| `admin/tutors/` | `/admin/tutors` | Tyutorlar | `GET /api/admin/tutors` |
| `admin/companies/` | `/admin/companies` | Korxonalar | `GET /api/admin/companies` |
| `admin/students/` | `/admin/students` | Talabalar | `GET /api/admin/students` |
| `admin/placements/` | `/admin/placements` | Amaliyotchiga (placeholder, mazmuni keyin aniqlanadi) | — |
| `admin/audit/` | `/admin/audit` | Audit jurnali | `GET /api/admin/audit` |
| `admin/settings/` | `/admin/settings` | Sozlamalar (form, bayramlar, shablonlar) | `GET/PUT /api/admin/settings` |
| `reports/` | `/admin/reports`, `/tutor/reports` | Hisobotlar katalogi (ikkala rol) | `GET /api/reports` |
| `tutor/today/` | `/tutor` | Bugun (davomat, filtr, ogohlantirish) | `GET /api/tutor/today` |
| `tutor/applications/` | `/tutor/applications` | Arizalar (tab'lar, detal panel, checklist, radius) | `GET/POST /api/tutor/applications*` |
| `tutor/students/` | `/tutor/students` | Talabalarim | `GET /api/tutor/students` |
| `tutor/diaries/` | `/tutor/diaries` | Kundalik hisobotlar (tasdiqlash/baho/qayta yozish) | `GET/POST /api/tutor/diaries*` |
| `tutor/calendar/` | `/tutor/calendar` | Kalendar (guruh × kunlar) | `GET /api/tutor/calendar` |
| `tutor/map/` | `/tutor/map` | Xarita (nuqtalar ro'yxati, placeholder) | `GET /api/tutor/map` |
| `tutor/leave-requests/` | `/tutor/leave-requests` | Ruxsat so'rovlari | `GET/POST /api/tutor/leave-requests*` |
| `tutor/grading/` | `/tutor/grading` | Baholash (100 ballik) | `GET/PUT /api/tutor/grading*` |

Umumiy yordamchilar: `admin/shared/` (`Paged<T>`, `useListParams` — qidiruv debounce + sahifa, `paginateMock`),
`admin/components/` (`AdminTable`, `PageStatus`), `tutor/query-keys.ts`, `tutor/components/QueryState.tsx`.
Admin/tyutor `mocks.ts` fayllari feature handler'larini yig'ib `src/mocks/handlers.ts` ga beradi.

### TWA tuzilmasi (`twa/src/`)

```
twa/src/
├── app/
│   ├── App.tsx · providers.tsx · query-client.ts
│   ├── router.tsx         6 marshrut, lazy; RootLayout — Telegram initData bilan avtomatik kirish (login sahifasi yo'q)
│   ├── nav.ts             STUDENT_NAV (6 bo'lim), PRIMARY_TAB_COUNT = 4 (qolgani "Yana" ichida)
│   └── layout/            AppShell (header + content + TabBar), TabBar, icons
├── features/
│   ├── auth/              api.ts (POST /api/auth/telegram, me, refresh) · hooks.ts (useAutoLogin)
│   ├── today/             Bosh ekran: check-in/check-out (geolocation), CheckinCard, PlaceSummary
│   ├── place/             Amaliyot joyim (korxona, shartnoma)
│   ├── diary/             Kundaligim: ro'yxat + DiaryForm (multipart, ≥150 belgi, ≤5 fayl)
│   ├── calendar/          Kalendarim: MonthGrid (DayCode)
│   ├── leave/             Ruxsat so'rash: LeaveRequestForm + LeaveRequestList
│   └── portfolio/         Portfolio (stat, baho tarkibi, xulosa, PDF havola)
│       ↳ har birida api.ts · types.ts · hooks.ts · mocks.ts (auth'dan tashqari) · components/
├── pages/                 HomePage, PlacePage, DiaryPage, CalendarPage, LeaveRequestPage, PortfolioPage, NotFoundPage (+ *.test.tsx)
├── shared/
│   ├── api/               client.ts (createApiClient + refresh), endpoints.ts (STUDENT_ENDPOINTS — barcha /api/student/* yo'llari)
│   ├── auth/              store.ts (sessionStorage'da refresh token), telegram.ts (@twa-dev/sdk o'rami)
│   ├── lib/               env, format, geolocation (getCurrentPosition → GeoPoint, GeoError)
│   └── ui/                shared/ui re-export + lokal: Chip/ChipRow/FileBox, FactGrid, MapPlaceholder, Empty/Error/LoadingState (README bor)
├── mocks/                 handlers.ts (auth + 6 feature), data.ts (mockStudent, resetMockState), problem.ts (ProblemDetails, requireBearer), browser.ts, server.ts
├── styles/                tokens.css, globals.css
└── test/                  setup.ts, render-app.tsx, telegram-stub.ts
```

Marshrutlar (SPEC-NAV 3.2): `/` Bosh ekran · `/joyim` · `/kundalik` · `/kalendar` · `/ruxsat` · `/portfolio`.

## Konvensiyalar

- **Feature-based**: har domen (`students`, `places`, `reports`...) `src/features/<name>/` ichida:
  `api.ts` (faqat so'rovlar), `hooks.ts` (`useXxxQuery`/`useXxxMutation`), `schema.ts` (zod),
  `types.ts`, `mocks.ts` (MSW handler'lar → `src/mocks/handlers.ts` ro'yxatiga qo'shiladi), sahifalar.
- **Container / presentation**: `*Page.tsx` (container — hook'lar, routing, holat) ⟶
  `components/*.tsx` (presentation — faqat props, server bilan ishlamaydi). `shared/ui` — umumiy dumb komponentlar.
- **TanStack Query kalitlari**: `[feature, entity, params]`, masalan
  `['students', 'list', { page, q }]`, `['students', 'detail', id]`. Har feature `xxxKeys` obyektini eksport qiladi
  (`shared/api/query-keys.ts` dagi namuna). Server holati faqat Query'da; Zustand — faqat klient holati (auth, UI).
- **Xatolar**: `instanceof ApiError` → `kind` (`validation` | `forbidden` | ...), `fieldError('phoneNumber')`,
  `errorMessage(err)`. Har ekran loading / error / empty holatini aniq ko'rsatadi.
- **Import**: `@/` — `src/` alias. Shared'dan: `import { ... } from '@amaliyotchi/shared'`.
- **Stil**: CSS Modules + faqat `tokens.css` custom property'lari (xom rang/px komponent ichida yo'q).
  Variant/holat — `data-*` atributlar. UI kit: `dashboard/src/shared/ui/README.md`; ko'rgazma: `/dev/kit`.
- **Shell**: `/admin/*`, `/tutor/*` — `AppShell` ichida (`RequireRole` + `<Outlet/>`). Sahifa sarlavhasi
  nav'dan avtomatik; o'zgartirish — `usePageHeader({ title })`, tugmalar — `<TopbarActions>`.
- **Testlar**: fayl yonida `*.test.ts(x)`; tarmoq — faqat MSW orqali (`server.use(...)` bilan override).

## Versiyalar bo'yicha eslatma

`vitest` `~4.0.x` ga qotirilgan: `vitest@4.1.x` peer-dependency to'plami npm 10.9.2 dagi arborist xatosini
(`Cannot read properties of null (reading 'edgesOut')`) keltirib chiqaradi. npm yangilangach ko'tarish mumkin.
