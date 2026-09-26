# Amaliyotchi — Docker bilan ishga tushirish

To'liq stack bitta `docker compose` bilan: PostgreSQL+PostGIS, API (.NET 10), dashboard (admin/tyutor),
TWA (talaba Telegram Mini App), MinIO, Redis, Seq.

## Host portlari

Barcha `ports:` `127.0.0.1` ga bog'lanadi (`deploy/.env` → `HOST_IP`) — servislar faqat lokal mashinadan ochiladi.
Tashqi (global) kirish Cloudflare Tunnel orqali: `cloudflared` konteyneri compose tarmog'ida bo'lib,
`http://dashboard:80` va `http://twa:80` ga to'g'ridan-to'g'ri murojaat qiladi — host portlariga bog'liq emas.

## Tez start

```bash
cp deploy/.env.example deploy/.env
# deploy/.env ichida JWT_SIGNING_KEY ni to'ldiring:
openssl rand -base64 48

docker compose -f deploy/docker-compose.yml up -d --build
docker compose -f deploy/docker-compose.yml ps
```

Birinchi build 3–6 daqiqa (SDK image + NuGet restore + web `npm ci`). Keyingi buildlarda faqat o'zgargan layer'lar.

Ochish: dashboard <http://127.0.0.1:8090> (admin `+998901234567` / `admin12345` — `.env` dagi `ADMIN_*`),
TWA <http://127.0.0.1:8091>, API health <http://127.0.0.1:5080/health>.

Barcha host portlari `deploy/.env` orqali sozlanadi va `HOST_IP` (default `127.0.0.1`) ga bog'lanadi (band bo'lsa o'zgartiring) — konteyner ichidagi portlar hech qachon o'zgarmaydi,
servislar bir-biriga ichki nom bilan ulanadi (`postgres:5432`, `redis:6379`, `minio:9000`, `api:8080`).

## Portlar

| Servis    | Konteyner              | Host port (`127.0.0.1:`) | Ichki | Izoh                                      |
|-----------|------------------------|-----------|-------|-------------------------------------------|
| dashboard | amaliyotchi-dashboard  | 8090 (`DASHBOARD_PORT`) | 80    | nginx, `/api/` → `api:8080`     |
| twa       | amaliyotchi-twa        | 8091 (`TWA_PORT`)       | 80    | nginx, `/api/` → `api:8080`     |
| api       | amaliyotchi-api        | 5080 (`API_PORT`)       | 8080  | to'g'ridan-to'g'ri debug, `/health` |
| postgres  | amaliyotchi-postgres   | 55432 (`POSTGRES_PORT`) | 5432  | `amaliyotchi` / `POSTGRES_PASSWORD`; 5432 odatda band |
| seq       | amaliyotchi-seq        | 5341 (`SEQ_PORT`)       | 80    | loglar UI                                 |
| minio     | amaliyotchi-minio      | 9000 (`MINIO_PORT`) / 9001 (`MINIO_CONSOLE_PORT`) | 9000/9001 | S3 API / konsol (`amaliyotchi`/`amaliyotchi123`) |
| redis     | amaliyotchi-redis      | 56379 (`REDIS_PORT`)    | 6379  | 6379 odatda band                          |

## Muhit o'zgaruvchilari (`deploy/.env`)

| O'zgaruvchi          | Default              | Izoh                                                        |
|----------------------|----------------------|-------------------------------------------------------------|
| `JWT_SIGNING_KEY`    | **majburiy**         | ≥ 32 belgi, `openssl rand -base64 48`                        |
| `POSTGRES_PASSWORD`  | `amaliyotchi`        | postgres va API ulanish satri                               |
| `TELEGRAM_BOT_TOKEN` | bo'sh                | BotFather tokeni; bo'sh bo'lsa `/api/auth/telegram` → 403    |
| `TELEGRAM_BOT_ENABLED` | `true`             | bot long polling (`/start`, `/help`, Menu Button); token va `TWA_PUBLIC_URL` bo'sh bo'lsa baribir o'chiq |
| `TWA_PUBLIC_URL`     | bo'sh                | TWA ommaviy HTTPS manzili (masalan `https://app.amalyotchi.uz`) — bot tugmasi, Menu Button, CORS |
| `SEED_DEMO`          | `true`               | demo ma'lumot — **production'da `false`**                    |
| `ADMIN_HEMIS_ID/PASSWORD` | `100000000001` / `admin12345` | birinchi admin (baza bo'sh bo'lganda bir marta), HEMIS ID — login  |
| `HOST_IP`            | `127.0.0.1`         | barcha `ports:` bog'lanadigan host IP (faqat lokal) |
| `DOCKER_SUBNET`      | `172.30.0.0/16`      | compose tarmog'i = API `ForwardedHeaders:KnownNetworks`      |
| `DASHBOARD_PORT`     | `8090`               | dashboard host porti (ixtiyoriy; hostda band bo'lsa o'zgartiring) |
| `TWA_PORT`           | `8091`               | TWA host porti (ixtiyoriy)                                  |
| `API_PORT`           | `5080`               | API debug host porti (ixtiyoriy); konteyner ichida hamma vaqt `8080` |
| `POSTGRES_PORT`      | `55432`              | postgres host porti (ixtiyoriy); ichida hamma vaqt `5432`   |
| `REDIS_PORT`         | `56379`              | redis host porti (ixtiyoriy); ichida hamma vaqt `6379`      |
| `SEQ_PORT`           | `5341`               | Seq UI host porti (ixtiyoriy)                               |
| `CORS_ORIGIN`        | `http://127.0.0.1:5173` | Vite dev server manzili (to'g'ridan-to'g'ri `:5080` ga so'rov uchun) |
| `MINIO_PORT` / `MINIO_CONSOLE_PORT` | `9000` / `9001` | MinIO S3 API va konsol host portlari (ixtiyoriy) |

API konfiguratsiyasi to'liq muhit o'zgaruvchilari orqali (`Section__Key`), `appsettings.Docker.json` yo'q.
Boshqa kalitlar — `src/Amaliyotchi.Api/appsettings.json`.

## Migratsiya va seed

API konteyneri startup'da o'zi bajaradi (`Seed__Enabled=true`):

1. `dotnet ef` migratsiyalari (`__migrations` jadvali, idempotent — qayta start xavfsiz);
2. asosiy seed: sozlama default'lari, birinchi admin (`ADMIN_HEMIS_ID/PASSWORD`, admin bo'lmasa), bayramlar;
3. `SEED_DEMO=true` bo'lsa demo ma'lumot (AT fakulteti, 412-22/413-22 guruhlari, tyutor `+998907654321`/`tutor12345`,
   38 talaba — brauzer login'i uchun demo talaba HEMIS ID `341030` / `talaba12345`, korxonalar, davomat, kundaliklar, baholar). Idempotent — demo tyutor bor bo'lsa qayta yuklanmaydi.

Healthcheck `start_period` 90 s — shu davrda migratsiya/seed tugaydi; `dashboard`/`twa` API `healthy` bo'lgach ko'tariladi.

```bash
docker compose -f deploy/docker-compose.yml logs -f api        # "Migratsiyalar qo'llandi", "Seed: ..."
docker compose -f deploy/docker-compose.yml logs -f --tail 100 dashboard twa
```

Bazani noldan boshlash: `docker compose -f deploy/docker-compose.yml down -v` (barcha volume'lar, jumladan fayllar `api-data`).

## Foydali buyruqlar

```bash
docker compose -f deploy/docker-compose.yml up -d --build api            # faqat API ni qayta qurish
docker compose -f deploy/docker-compose.yml restart api
docker compose -f deploy/docker-compose.yml exec postgres psql -U amaliyotchi -d amaliyotchi
docker compose -f deploy/docker-compose.yml down                          # ma'lumotlar saqlanadi
curl -s http://127.0.0.1:5080/health
curl -s http://127.0.0.1:5080/api/auth/login -H 'Content-Type: application/json' \
  -d '{"phoneNumber":"+998901234567","password":"admin12345"}'
```

## Telegram TWA uchun HTTPS

Telegram Mini App faqat HTTPS manzilni ochadi. Lokal stendni tunnel bilan chiqaring va BotFather'da
Web App URL sifatida bering:

```bash
# ngrok (TWA host porti — .env dagi TWA_PORT, default 8091)
ngrok http 127.0.0.1:8091
# yoki cloudflared (hisobsiz vaqtinchalik domen)
cloudflared tunnel --url http://127.0.0.1:8091
```

`TELEGRAM_BOT_TOKEN` ni `.env` ga yozib `api` ni qayta ko'taring: `docker compose -f deploy/docker-compose.yml up -d api`.
Tunnel `X-Forwarded-Proto: https` yuboradi — nginx uni API ga uzatadi; API compose tarmog'idan
(`KnownNetworks`) kelgan sarlavhalarga ishonadi.

## Telegram bot (long polling)

API ichida `TelegramBotService` (BackgroundService) ishlaydi — **webhook emas, long polling**, shuning uchun
tunnel/nginx sozlamasiga bog'liq emas. Ishga tushishda: `deleteWebhook`, `setMyCommands` (`/start`, `/help`),
`setChatMenuButton` (“Amalyotchi” → `TWA_PUBLIC_URL`). Javoblar faqat shaxsiy chat'larda: `/start` (parametrli ham) —
salom + “Ilovani ochish” tugmasi, `/help` — qisqa yordam, boshqa matn — qisqa javob + tugma; guruhlar e'tiborsiz.

Shartlar: `Telegram__BotEnabled=true` + `TELEGRAM_BOT_TOKEN` + `TWA_PUBLIC_URL` (HTTPS). Biror biri yo'q bo'lsa
log'da bitta `Telegram bot o'chiq: ...` yozuvi chiqadi va xizmat to'xtaydi (API ishlayveradi).

- **Bir vaqtda faqat bitta instansiya polling qilishi kerak.** Ikkinchisi (masalan dev'da lokal API ham shu token
  bilan yoqilgan bo'lsa) Telegram'dan `409 Conflict` oladi — log'da Warning, xizmat backoff bilan qayta urinadi.
  API ni bir nechta replika bilan ko'tarsangiz, faqat bittasida `TELEGRAM_BOT_ENABLED=true` qoldiring.
- Lokal dev'da (`dotnet run`) `Telegram:BotEnabled` — `false` (appsettings.Development.json, token soxta);
  prod botni lokal sinash kerak bo'lsa, avval Docker'dagi API da `TELEGRAM_BOT_ENABLED=false` qiling.
- Tekshirish: `docker compose -f deploy/docker-compose.yml logs api | grep -i "telegram bot"` →
  `Telegram bot @... long polling rejimida ishga tushdi.`

## Production eslatmalari

- **`JWT_SIGNING_KEY`** — tasodifiy, ≥ 32 belgi, hech qachon git'da emas. Almashtirilsa barcha sessiyalar bekor bo'ladi.
- **`POSTGRES_PASSWORD`, `ADMIN_PASSWORD`** — default'larni albatta o'zgartiring; admin bir marta yaratiladi, keyin parolni
  dashboard orqali almashtiring.
- **`SEED_DEMO=false`** — demo ma'lumot faqat stend uchun (API `Seed:Demo` default `false`, compose'da lokal qulaylik uchun `true`).
- **`ForwardedHeaders:KnownNetworks`** — faqat haqiqiy proksi turgan tarmoq (compose subneti). Tashqi reverse proxy
  (Caddy/nginx hostda) bo'lsa uning IP sini `ForwardedHeaders__KnownProxies__0` ga qo'shing. `API_PORT` (default `5080`) portini production'da
  yopib qo'ying (`ports` ni olib tashlang) — u proksisiz to'g'ridan-to'g'ri kirish.
- Postgres/MinIO/Redis/Seq portlarini tashqariga ochmang (`ports` ni olib tashlang; lokal stendda ular allaqachon faqat loopback `HOST_IP` ga bog'langan).
- Fayllar `api-data` volume'da (`/app/data/files`) — zaxira nusxasini `postgres-data` bilan birga oling.
- HTTPS — hostdagi Caddy/nginx yoki cloud load balancer; konteynerlar ichida faqat HTTP.
