# Amaliyotchi

Talabalar amaliyotini geo-lokatsiya orqali nazorat qiluvchi platforma.

- **Nima qiladi** → [`docs/FUNKSIONAL-QOLLANMA.md`](docs/FUNKSIONAL-QOLLANMA.md)
- **Qanday quriladi** → [`docs/QURISH-TARTIBI.md`](docs/QURISH-TARTIBI.md)

Joriy holat: **backend — Sprint 1 (M01 fundament, M02 auth/RBAC); frontend — Sprint 2–3 ekranlari (admin, tyutor, talaba) MSW mock bilan tayyor**, backend endpoint'lari kutilmoqda.

---

## Tez ishga tushirish

Talab qilinadi: **.NET 10 SDK** va **Docker**.

```bash
# 1. Infratuzilma (PostgreSQL+PostGIS, MinIO, Redis, Seq)
docker compose -f deploy/docker-compose.yml up -d

# 2. Paketlar va qurish
dotnet restore
dotnet build

# 3. Ma'lumotlar bazasi
dotnet tool install --global dotnet-ef          # bir marta
dotnet ef migrations add Initial -p src/Amaliyotchi.Infrastructure -s src/Amaliyotchi.Api
dotnet ef database update -p src/Amaliyotchi.Infrastructure -s src/Amaliyotchi.Api

# 4. API
dotnet run --project src/Amaliyotchi.Api
# http://localhost:5080/health
# http://localhost:5080/openapi/v1.json   (faqat Development)
```

### Maxfiy kalitlar

`appsettings.json` ichida hech qanday haqiqiy parol yoki kalit saqlanmaydi.
Lokal ishlash uchun user-secrets:

```bash
cd src/Amaliyotchi.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:SigningKey" "kamida-32-belgidan-iborat-tasodifiy-kalit"
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=amaliyotchi;Username=amaliyotchi;Password=amaliyotchi"
```

Ishlab chiqarish muhitida — muhit o'zgaruvchilari yoki vault.

### Telegram bot tokeni

`appsettings.Development.json` da `Telegram:BotToken` = `1234567890:DEV-TEST-TOKEN-amaliyotchi` — bu **soxta dev token**:
TWA frontend'i lokal ishlashda shu token bilan `initData` imzolaydi (`POST /api/auth/telegram`), haqiqiy Telegram bilan
bog'liq emas. Production'da haqiqiy BotFather tokeni faqat muhit o'zgaruvchisi `Telegram__BotToken` (yoki vault) orqali
beriladi; `appsettings.json` da u bo'sh qoladi. Token bo'sh bo'lsa ilova ishga tushadi, lekin `/api/auth/telegram` 403 qaytaradi.

---

## Loyiha tuzilmasi

```
src/
├── Amaliyotchi.Domain          entity'lar va domain qoidalari — tashqi paketga bog'liq emas
├── Amaliyotchi.Application     CQRS handler'lar, DTO, validatsiya, interfeyslar
├── Amaliyotchi.Infrastructure  EF Core, PostgreSQL, audit interceptor, JWT, parol xeshi
├── Amaliyotchi.Api             controller'lar, avtorizatsiya, ProblemDetails, health
└── Amaliyotchi.Worker          fon vazifalari (M11 da to'ldiriladi)
tests/
├── Amaliyotchi.UnitTests       domain qoidalari
└── Amaliyotchi.IntegrationTests  real PostgreSQL ustida (Testcontainers)
deploy/docker-compose.yml       lokal infratuzilma
```

Bog'liqlik yo'nalishi bir tomonlama: `Api → Infrastructure → Application → Domain`.
Buni tekshirish:

```bash
grep -rn "Microsoft.EntityFrameworkCore\|Npgsql" src/Amaliyotchi.Domain/ && echo "QATLAM BUZILGAN"
```

---

## web/ — frontend

npm workspaces monorepo (Node ≥ 22.12): `dashboard` — admin (9 ekran) va tyutor (8 ekran) SPA (React + Vite),
`twa` — talaba Telegram Web App (6 ekran, pastki tab-bar), `shared` — tokenlar, bazaviy UI kit, API client, auth.

```bash
cd web && npm install
cp dashboard/.env.example dashboard/.env   # VITE_USE_MOCKS=true — backend'siz ishlaydi
npm run dev        # dashboard → http://localhost:5173 (/api → localhost:5080 proxy)
npm run dev:twa    # twa       → http://localhost:5174
npm run check      # typecheck + lint + test + build
```

Mock rejimida barcha ekranlar MSW handler'lari bilan ishlaydi (kirish: `+998901234567 / admin12345`,
`+998907654321 / tutor12345`). Backend'da hozircha faqat `AuthController` bor — frontend kutayotgan
qolgan 34 endpoint'ning aniq spetsifikatsiyasi [`web/API-CONTRACT.md`](web/API-CONTRACT.md) da
(backend modullarini qurishda manba). Batafsil: [`web/README.md`](web/README.md).

---

## Qaysi qaror nima uchun qabul qilingan

| Qaror | Sabab |
|---|---|
| **.NET 10** | .NET 8 va 9 ning qo'llab-quvvatlashi 2026-yil 10-noyabrda tugaydi. .NET 10 — LTS, 2028-yilgacha |
| **UUIDv7 (`Guid.CreateVersion7`)** | Vaqt bo'yicha tartiblangan — PostgreSQL indeksida tasodifiy GUID'dan sezilarli tez |
| **snake_case nomlar** | PostgreSQL uslubi; qo'shimcha paketsiz, `OnModelCreating` da bir marta qo'llaniladi |
| **`timestamptz` + UTC** | Kunlik oynalar va cron ishlari soat mintaqasiga bog'liq bo'lmasligi uchun |
| **Soft delete** | Ma'lumot o'chirilmaydi, arxivlanadi — global query filter buni har bir so'rovda ta'minlaydi |
| **Audit interceptor** | Kim-nima-qachon yozuvi handler'ga bog'liq emas: uni yozishni unutib bo'lmaydi |
| **PBKDF2 (BCL)** | Tashqi paketsiz, 210 000 iteratsiya, doimiy vaqtli taqqoslash, formatni yangilash imkoni |
| **Refresh token rotatsiyasi** | Har yangilashda eski token bekor qilinadi — o'g'irlangan token uzoq yashamaydi |
| **Rate limiting** | `/api/auth/login` — bitta IP dan daqiqasiga 10 urinish |
| **MediatR 12.4.1** | 13-versiyadan boshlab litsenziya tijoriy; 12.x — Apache 2.0 |

---

## Kirish nuqtalari (hozircha)

| Metod | Manzil | Kim | Tavsif |
|---|---|---|---|
| POST | `/api/auth/login` | hamma | Telefon + parol (admin, tyutor) |
| POST | `/api/auth/refresh` | hamma | Token yangilash (rotatsiya bilan) |
| POST | `/api/auth/logout` | avtorizatsiyalangan | Refresh tokenni bekor qilish |
| GET | `/api/auth/me` | avtorizatsiyalangan | Joriy foydalanuvchi |
| GET | `/health` | hamma | Tizim va DB holati |

Talaba Telegram orqali kiradi — u yo'l M10 da (Sprint 3) qo'shiladi.

---

## Tekshirish

```bash
dotnet build -warnaserror
dotnet test
```

---

## Keyingi qadam (Sprint 2 · M03–M04)

1. Fakultet / yo'nalish / guruh CRUD va admin ekranlari
2. Talaba kartasi va Excel import (xato satrlarni ko'rsatish bilan)
3. Taklif tokenlari generatsiyasi
4. Tyutorning ma'lumot ko'lami bo'yicha integratsiya testlari
