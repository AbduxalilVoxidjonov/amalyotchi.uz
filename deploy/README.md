# Amaliyotchi — Docker bilan ishga tushirish

To'liq stack bitta `docker compose` bilan: PostgreSQL+PostGIS, API (.NET 10), dashboard (admin/tyutor),
TWA (talaba Telegram Mini App), MinIO, Redis, Seq.

## Tez start

```bash
cp deploy/.env.example deploy/.env
# deploy/.env ichida JWT_SIGNING_KEY ni to'ldiring:
openssl rand -base64 48

docker compose -f deploy/docker-compose.yml up -d --build
docker compose -f deploy/docker-compose.yml ps
```

Birinchi build 3–6 daqiqa (SDK image + NuGet restore + web `npm ci`). Keyingi buildlarda faqat o'zgargan layer'lar.

Ochish: dashboard <http://localhost:8090> (admin `+998901234567` / `admin12345` — `.env` dagi `ADMIN_*`),
TWA <http://localhost:8091>, API health <http://localhost:5080/health>.

Host portlari band bo'lsa `deploy/.env` da `DASHBOARD_PORT` / `TWA_PORT` / `API_PORT` ni o'zgartiring (konteyner ichidagi portlar o'zgarmaydi).

## Portlar

| Servis    | Konteyner              | Host port | Ichki | Izoh                                      |
|-----------|------------------------|-----------|-------|-------------------------------------------|
| dashboard | amaliyotchi-dashboard  | 8090 (`DASHBOARD_PORT`) | 80    | nginx, `/api/` → `api:8080`     |
| twa       | amaliyotchi-twa        | 8091 (`TWA_PORT`)       | 80    | nginx, `/api/` → `api:8080`     |
| api       | amaliyotchi-api        | 5080 (`API_PORT`)       | 8080  | to'g'ridan-to'g'ri debug, `/health` |
| postgres  | amaliyotchi-postgres   | 5432      | 5432  | `amaliyotchi` / `POSTGRES_PASSWORD`       |
| seq       | amaliyotchi-seq        | 5341      | 80    | loglar UI                                 |
| minio     | amaliyotchi-minio      | 9000/9001 | —     | S3 API / konsol (`amaliyotchi`/`amaliyotchi123`) |
| redis     | amaliyotchi-redis      | 6379      | 6379  |                                           |

## Muhit o'zgaruvchilari (`deploy/.env`)

| O'zgaruvchi          | Default              | Izoh                                                        |
|----------------------|----------------------|-------------------------------------------------------------|
| `JWT_SIGNING_KEY`    | **majburiy**         | ≥ 32 belgi, `openssl rand -base64 48`                        |
| `POSTGRES_PASSWORD`  | `amaliyotchi`        | postgres va API ulanish satri                               |
| `TELEGRAM_BOT_TOKEN` | bo'sh                | BotFather tokeni; bo'sh bo'lsa `/api/auth/telegram` → 403    |
| `SEED_DEMO`          | `true`               | demo ma'lumot — **production'da `false`**                    |
| `ADMIN_HEMIS_ID/PASSWORD` | `100000000001` / `admin12345` | birinchi admin (baza bo'sh bo'lganda bir marta), HEMIS ID — login  |
| `DOCKER_SUBNET`      | `172.30.0.0/16`      | compose tarmog'i = API `ForwardedHeaders:KnownNetworks`      |
| `DASHBOARD_PORT`     | `8090`               | dashboard host porti (ixtiyoriy; hostda band bo'lsa o'zgartiring) |
| `TWA_PORT`           | `8091`               | TWA host porti (ixtiyoriy)                                  |
| `API_PORT`           | `5080`               | API debug host porti (ixtiyoriy); konteyner ichida hamma vaqt `8080` |

API konfiguratsiyasi to'liq muhit o'zgaruvchilari orqali (`Section__Key`), `appsettings.Docker.json` yo'q.
Boshqa kalitlar — `src/Amaliyotchi.Api/appsettings.json`.

## Migratsiya va seed

API konteyneri startup'da o'zi bajaradi (`Seed__Enabled=true`):

1. `dotnet ef` migratsiyalari (`__migrations` jadvali, idempotent — qayta start xavfsiz);
2. asosiy seed: sozlama default'lari, birinchi admin (`ADMIN_HEMIS_ID/PASSWORD`, admin bo'lmasa), bayramlar;
3. `SEED_DEMO=true` bo'lsa demo ma'lumot (AT fakulteti, 412-22/413-22 guruhlari, tyutor `+998907654321`/`tutor12345`,
   38 talaba, korxonalar, davomat, kundaliklar, baholar). Idempotent — demo tyutor bor bo'lsa qayta yuklanmaydi.

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
curl -s localhost:5080/health
curl -s localhost:5080/api/auth/login -H 'Content-Type: application/json' \
  -d '{"phoneNumber":"+998901234567","password":"admin12345"}'
```

## Telegram TWA uchun HTTPS

Telegram Mini App faqat HTTPS manzilni ochadi. Lokal stendni tunnel bilan chiqaring va BotFather'da
Web App URL sifatida bering:

```bash
# ngrok (TWA host porti — .env dagi TWA_PORT, default 8091)
ngrok http 8091
# yoki cloudflared (hisobsiz vaqtinchalik domen)
cloudflared tunnel --url http://localhost:8091
```

`TELEGRAM_BOT_TOKEN` ni `.env` ga yozib `api` ni qayta ko'taring: `docker compose -f deploy/docker-compose.yml up -d api`.
Tunnel `X-Forwarded-Proto: https` yuboradi — nginx uni API ga uzatadi; API compose tarmog'idan
(`KnownNetworks`) kelgan sarlavhalarga ishonadi.

## Production eslatmalari

- **`JWT_SIGNING_KEY`** — tasodifiy, ≥ 32 belgi, hech qachon git'da emas. Almashtirilsa barcha sessiyalar bekor bo'ladi.
- **`POSTGRES_PASSWORD`, `ADMIN_PASSWORD`** — default'larni albatta o'zgartiring; admin bir marta yaratiladi, keyin parolni
  dashboard orqali almashtiring.
- **`SEED_DEMO=false`** — demo ma'lumot faqat stend uchun (API `Seed:Demo` default `false`, compose'da lokal qulaylik uchun `true`).
- **`ForwardedHeaders:KnownNetworks`** — faqat haqiqiy proksi turgan tarmoq (compose subneti). Tashqi reverse proxy
  (Caddy/nginx hostda) bo'lsa uning IP sini `ForwardedHeaders__KnownProxies__0` ga qo'shing. `API_PORT` (default `5080`) portini production'da
  yopib qo'ying (`ports` ni olib tashlang) — u proksisiz to'g'ridan-to'g'ri kirish.
- Postgres/MinIO/Redis/Seq portlarini tashqariga ochmang (`ports` ni olib tashlang yoki `127.0.0.1:` bilan cheklang).
- Fayllar `api-data` volume'da (`/app/data/files`) — zaxira nusxasini `postgres-data` bilan birga oling.
- HTTPS — hostdagi Caddy/nginx yoki cloud load balancer; konteynerlar ichida faqat HTTP.
