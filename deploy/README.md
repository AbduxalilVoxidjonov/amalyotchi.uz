# Amaliyotchi — Docker bilan deploy

Stack (`deploy/docker-compose.yml`, production rejimi):

| Servis      | Image                                   | Vazifa                                                           |
|-------------|-----------------------------------------|------------------------------------------------------------------|
| postgres    | `postgis/postgis:16-3.4`                | PostgreSQL 16 + PostGIS, volume `postgres-data`                  |
| api         | `amaliyotchi-api:local` (build)         | .NET 10 API, fayllar volume `api-data` (`/app/data/files`)      |
| dashboard   | `amaliyotchi-dashboard:local` (build)   | nginx: admin/tyutor SPA, `/api/` → `api:8080`                   |
| twa         | `amaliyotchi-twa:local` (build)         | nginx: talaba Telegram Mini App, `/api/` → `api:8080`           |
| cloudflared | `cloudflare/cloudflared:2026.9.3`       | Cloudflare Tunnel — **yagona tashqi kirish**                     |
| seq         | `datalust/seq:2026.1.17182`             | markazlashgan loglar (UI faqat `127.0.0.1:5341`)                |
| backup      | `postgis/postgis:16-3.4`                | kunlik `pg_dump` + fayllar arxivi, N kunlik rotatsiya            |

Xavfsizlik modeli: hostga **hech qaysi servis porti ochilmaydi** (faqat Seq UI `127.0.0.1` da). Internetdan kirish —
faqat `cloudflared` konteyneri orqali, u compose tarmog'ida `http://dashboard:80` va `http://twa:80` ga murojaat qiladi.
Serverda tashqariga faqat SSH ochiq bo'ladi. Barcha konteynerlarda log rotatsiyasi (json-file `10m × 5`), xotira
chegarasi, `no-new-privileges` va `restart: unless-stopped`.

---

## Serverga chiqarish

### 1. Server talablari

- Linux VPS (Ubuntu 22.04/24.04 yoki Debian 12), x86_64 yoki arm64.
- **RAM: kamida 2 GB** (tavsiya 4 GB). Birinchi build (.NET SDK + `npm ci`) eng ko'p xotira yeydi;
  2 GB da swap qo'shing (pastda).
- Disk: kamida 20 GB bo'sh (image'lar ~3 GB, build kesh, baza, fayllar, zaxiralar).
- Domen Cloudflare'da (`amalyotchi.uz`) — DNS'ni Cloudflare boshqaradi.
- Kiruvchi portlar: faqat SSH. 80/443 kerak emas (tunnel chiquvchi ulanish ishlatadi).

```bash
# (2 GB RAM bo'lsa) 2 GB swap
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

# Vaqt zonasi UTC qolsin (konteynerlar va zaxira jadvali UTC da)
timedatectl
```

### 2. Firewall (ufw: faqat SSH)

```bash
sudo apt-get update && sudo apt-get install -y ufw
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow OpenSSH            # SSH boshqa portda bo'lsa: sudo ufw allow 2222/tcp
sudo ufw enable
sudo ufw status verbose
```

> Docker `ports:` bilan ochilgan portlar ufw'ni chetlab o'tadi (iptables DOCKER zanjiri). Shuning uchun compose'da
> host portlari yo'q, Seq esa faqat `127.0.0.1` ga bog'langan — bu tashqaridan ko'rinmaydi.

### 3. Docker o'rnatish

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"     # keyin qayta login qiling (yoki: newgrp docker)
docker version                      # Server: 29.x
docker compose version              # v5.x
sudo systemctl enable --now docker  # server qayta yuklanganda stack o'zi ko'tariladi (restart: unless-stopped)
```

### 4. Repo klonlash

```bash
sudo mkdir -p /opt/amaliyotchi && sudo chown "$USER": /opt/amaliyotchi
git clone <repo-url> /opt/amaliyotchi
cd /opt/amaliyotchi
```

Bundan keyingi barcha buyruqlar `/opt/amaliyotchi` dan. Qisqartma (ixtiyoriy, `~/.bashrc` ga):

```bash
alias dc='docker compose -f /opt/amaliyotchi/deploy/docker-compose.yml'
```

### 5. Cloudflare Tunnel

1. Cloudflare dashboard → **Zero Trust → Networks → Tunnels → Create a tunnel** → turi **Cloudflared** →
   nom: `amaliyotchi`.
2. "Install and run a connector" sahifasida buyruqdagi `--token` dan keyingi uzun qiymatni nusxalang —
   bu `CLOUDFLARE_TUNNEL_TOKEN` (connector'ni hostga o'rnatmang, u konteynerda ishlaydi).
3. **Public Hostname** qo'shing (tunnel → Configure → Public Hostname → Add):

   | Subdomain | Domain           | Type | URL              |
   |-----------|------------------|------|------------------|
   | *(bo'sh)* | `amalyotchi.uz`  | HTTP | `dashboard:80`   |
   | `app`     | `amalyotchi.uz`  | HTTP | `twa:80`         |

   Cloudflare DNS yozuvlarini (CNAME → `<tunnel-id>.cfargotunnel.com`) o'zi yaratadi. Domenda eski A-yozuv bo'lsa,
   avval o'chiring. TLS Cloudflare'da tugaydi; konteynerlar ichida faqat HTTP.
4. (Tavsiya) SSL/TLS → Edge Certificates → **Always Use HTTPS** yoqilgan bo'lsin.

### 6. `.env` ni to'ldirish

```bash
cp deploy/.env.example deploy/.env
chmod 600 deploy/.env
```

Har bir sirni generatsiya qiling va `deploy/.env` ga yozing (qo'shtirnoqsiz):

| O'zgaruvchi               | Qanday olinadi                                                  |
|---------------------------|-----------------------------------------------------------------|
| `JWT_SIGNING_KEY`         | `openssl rand -base64 48`                                       |
| `POSTGRES_PASSWORD`       | `openssl rand -hex 24`  (`$` va `;` bo'lmasin — hex xavfsiz)    |
| `ADMIN_HEMIS_ID`          | birinchi admin login'i (faqat raqamlar, masalan o'z HEMIS ID'ingiz) |
| `ADMIN_PASSWORD`          | `openssl rand -base64 18`                                       |
| `SEQ_ADMIN_PASSWORD`      | `openssl rand -hex 16`                                          |
| `CLOUDFLARE_TUNNEL_TOKEN` | 5-qadam, 2-band                                                 |
| `TELEGRAM_BOT_TOKEN`      | @BotFather → `/newbot` yoki `/token`                            |
| `DASHBOARD_PUBLIC_URL`    | `https://amalyotchi.uz`                                         |
| `TWA_PUBLIC_URL`          | `https://app.amalyotchi.uz`                                     |

Bir buyruqda (bo'sh qatorlarni to'ldiradi, mavjud qiymatlarga tegmaydi):

```bash
f=deploy/.env
fill() { grep -q "^$1=." "$f" || sed -i "s|^$1=.*|$1=$2|" "$f"; }
fill JWT_SIGNING_KEY    "$(openssl rand -base64 48 | tr -d '\n')"
fill POSTGRES_PASSWORD  "$(openssl rand -hex 24)"
fill ADMIN_PASSWORD     "$(openssl rand -base64 18)"
fill SEQ_ADMIN_PASSWORD "$(openssl rand -hex 16)"
nano "$f"   # ADMIN_HEMIS_ID, CLOUDFLARE_TUNNEL_TOKEN, TELEGRAM_BOT_TOKEN ni qo'lda
grep -E '^(ADMIN_PASSWORD|SEQ_ADMIN_PASSWORD)=' "$f"   # parollarni parol menejeriga saqlang
```

Barcha o'zgaruvchilar va default'lari — `deploy/.env.example` izohlarida (xotira chegaralari, PG tuning, zaxira jadvali).

Tekshiruv (majburiy qiymatlar, JWT ≥ 32, demo parollar yo'qligi, HTTPS URL'lar, `compose config`):

```bash
deploy/scripts/preflight.sh
```

### 7. Birinchi ishga tushirish

```bash
docker compose -f deploy/docker-compose.yml up -d --build
docker compose -f deploy/docker-compose.yml ps
```

Birinchi build 5–10 daqiqa. Ishga tushish tartibi: `postgres` (healthy) → `api` (migratsiya + seed, `start_period` 90 s)
→ `dashboard`/`twa` (healthy) → `cloudflared`. `backup` API healthy bo'lgach (migratsiya tugagan — bo'sh sxemasiz
baza zaxiralanmaydi) darhol birinchi zaxirani oladi. `seq` ham healthcheck'ga ega (`/health`).

API startup'da o'zi bajaradi: EF migratsiyalari (idempotent), sozlama default'lari, birinchi admin
(`ADMIN_HEMIS_ID`/`ADMIN_PASSWORD`, faqat admin bo'lmasa), bayramlar. Demo ma'lumot production'da yuklanmaydi
(`SEED_DEMO` default `false`).

### 8. Tekshirish

```bash
dc ps                                                    # hammasi "running (healthy)"
dc logs --tail 50 api | grep -Ei "migratsiya|seed|telegram bot|error"
dc exec api curl -fsS http://localhost:8080/health       # API (host porti yo'q)
dc exec cloudflared cloudflared tunnel --metrics 127.0.0.1:20241 ready && echo tunnel OK
curl -sI https://amalyotchi.uz | head -1                 # HTTP/2 200
curl -s  https://amalyotchi.uz/healthz                   # dashboard nginx (tunnel orqali)
curl -sI https://app.amalyotchi.uz | head -1
ss -tlnp | grep -v 127.0.0.1                             # tashqi interfeysda faqat :22 bo'lishi kerak
```

Keyin:

1. `https://amalyotchi.uz` → `ADMIN_HEMIS_ID` / `ADMIN_PASSWORD` bilan kiring → **Sozlamalar**da parolni almashtiring.
2. @BotFather → `/mybots` → bot → **Bot Settings → Configure Mini App / Menu Button** → URL `https://app.amalyotchi.uz`
   (API ham ishga tushishda Menu Button'ni o'rnatadi). `/setdomain` → `amalyotchi.uz` (Telegram Login uchun).
3. Botga `/start` yuboring → "Ilovani ochish" tugmasi TWA'ni ochishi kerak.

### 9. Yangilash

```bash
cd /opt/amaliyotchi
dc exec backup /scripts/backup.sh once        # yangilashdan oldin qo'shimcha zaxira
git pull --ff-only
deploy/scripts/preflight.sh                   # .env.example'da yangi majburiy o'zgaruvchi bo'lsa ushlaydi
dc up -d --build --remove-orphans
dc ps
docker image prune -f                         # eski (dangling) image'lar
docker builder prune -f --filter until=168h   # 7 kundan eski build kesh
```

Migratsiyalar API startup'da avtomatik qo'llanadi. `TWA_PUBLIC_URL` o'zgarsa `dashboard` ham qayta build bo'ladi
(`VITE_TWA_URL` build arg). Orqaga qaytish: `git checkout <oldingi-commit> && dc up -d --build`
(migratsiya qaytarilmaydi — kerak bo'lsa bazani zaxiradan tiklang).

### 10. Zaxira va tiklash

`backup` servisi har kuni `BACKUP_TIME_UTC` da (default `21:00` UTC = 02:00 Toshkent) oladi:

- `deploy/backups/db/amaliyotchi-YYYYmmdd-HHMMSS.dump` — `pg_dump -Fc` (siqilgan, arxiv `pg_restore --list` bilan tekshiriladi);
- `deploy/backups/files/files-YYYYmmdd-HHMMSS.tar.gz` — `api-data/files` (yuklangan fayllar);
- `api-data/keys` (ASP.NET DataProtection kalitlari, `DataProtection__KeysPath`) zaxiraga **kirmaydi** — ular
  volume'da saqlanadi va konteyner qayta yaratilganda yo'qolmaydi; yo'qolsa API yangisini yaratadi (API ular bilan
  uzoq muddatli ma'lumot shifrlamaydi — JWT/refresh tokenlarga ta'sir qilmaydi). Kalitlar diskda shifrlanmagan
  holda turadi — birinchi yaratilishda bir martalik `No XML encryptor configured` WRN kutilgan holat;
- `BACKUP_RETENTION_DAYS` (default 14) kundan eskilari o'chiriladi; oxirgi muvaffaqiyat 26 soatdan eski bo'lsa
  `backup` konteyneri `unhealthy` bo'ladi.

```bash
dc exec backup /scripts/backup.sh once        # hozir qo'lda zaxira
dc logs --tail 20 backup
ls -lh deploy/backups/db deploy/backups/files
```

**Serverdan tashqariga nusxa** (server diski yo'qolsa ham ma'lumot qolsin) — kamida haftada bir, masalan
o'z kompyuteringizdan:

```bash
rsync -avz user@server:/opt/amaliyotchi/deploy/backups/ ~/amaliyotchi-backups/
```

(yoki serverda cron + `rclone` bilan S3/R2/Google Drive'ga). Papka `BACKUP_DIR` bilan boshqa diskka ko'chiriladi.

**Tiklash** (`deploy/backup/restore.sh`, tasdiq so'raydi):

```bash
# Baza: api/backup to'xtatiladi, `amaliyotchi` bazasi o'chirib qayta yaratiladi, dump tiklanadi, api qayta yoqiladi
deploy/backup/restore.sh db deploy/backups/db/amaliyotchi-20261001-210000.dump

# Fayllar: joriy files → files.old, arxiv ochiladi
deploy/backup/restore.sh files deploy/backups/files/files-20261001-210000.tar.gz
```

Qo'lda (skriptsiz) baza tiklash:

```bash
dc stop api backup
dc exec -T postgres dropdb   -U amaliyotchi --maintenance-db=postgres --force amaliyotchi
dc exec -T postgres createdb -U amaliyotchi --maintenance-db=postgres -O amaliyotchi -T template_postgis amaliyotchi
dc exec -T postgres pg_restore -U amaliyotchi -d amaliyotchi --no-owner < deploy/backups/db/<fayl>.dump
dc start api backup
```

Yangi serverga ko'chirish: 1–7-qadamlar (eski `.env` bilan), keyin ikkala `restore.sh` buyrug'i.
Tiklashni vaqti-vaqti bilan sinab ko'ring — tekshirilmagan zaxira zaxira emas.

### 11. Loglar

```bash
dc logs -f --tail 100 api                     # konteyner loglari (json-file, 10m × 5 rotatsiya)
dc logs --since 1h dashboard twa cloudflared
docker system df                              # image/volume/log hajmi
```

**Seq** (strukturalangan API loglari, qidiruv/filtr): port faqat serverning `127.0.0.1:5341` ida.
O'z kompyuteringizdan SSH tunnel:

```bash
ssh -N -L 5341:127.0.0.1:5341 user@server
# brauzer: http://127.0.0.1:5341 → login `admin` / SEQ_ADMIN_PASSWORD
```

**Birinchi kirishda Seq parolni almashtirishni talab qiladi** (`SEQ_FIRSTRUN_ADMINPASSWORD` bilan yaratilgan
`admin` — MustChangePassword): yangi kuchli parol bering va uni parol menejerida saqlang. `.env` dagi
`SEQ_ADMIN_PASSWORD` shundan keyin ishlatilmaydi (faqat `seq-data` volume yangi yaratilganda qo'llanadi) —
yangi parolni yo'qotsangiz `.env` qiymati yordam bermaydi.

Seq'da **Settings → Retention** da saqlash muddatini qo'ying (masalan 30 kun), aks holda `seq-data` o'sib boradi.

### 12. Boshqa amallar

```bash
dc exec postgres psql -U amaliyotchi -d amaliyotchi   # SQL konsol
dc restart api
dc down                                               # to'xtatish (volume'lar saqlanadi)
docker stats --no-stream                              # xotira/CPU (chegaralar .env: *_MEM_LIMIT)
```

- **Postgres parolini almashtirish** (`POSTGRES_PASSWORD` faqat birinchi init'da qo'llanadi):
  `dc exec postgres psql -U amaliyotchi -d postgres -c "ALTER USER amaliyotchi PASSWORD '<yangi>'"`,
  keyin `.env` da yangilang va `dc up -d`.
- **JWT kalitini almashtirish** — barcha foydalanuvchilar qayta kirishi kerak bo'ladi.
- **Telegram bot** — long polling, bir vaqtda faqat **bitta** instansiya. Lokal dev'da shu token bilan bot
  yoqilsa ikkalasi `409 Conflict` oladi (`TELEGRAM_BOT_ENABLED=false` bilan o'chiring).
- **Haqiqiy klient IP zanjiri:** cloudflared (`CLOUDFLARED_IP`, default `172.30.0.250`) → nginx `NGINX_REAL_IP_FROM`
  faqat shundan `CF-Connecting-IP` ni qabul qiladi → dashboard/twa (`DASHBOARD_IP` `172.30.0.248`, `TWA_IP`
  `172.30.0.249`) → API `ForwardedHeaders:KnownProxies` faqat shu ikki IP'dan `X-Forwarded-For` ni qabul qiladi.
  Tarmoqdagi boshqa konteyner (masalan seq, backup) yoki lokal host porti orqali `api:8080` ga kelgan soxta
  `X-Forwarded-For` e'tiborsiz qoldiriladi. `DOCKER_SUBNET` hostda band bo'lsa `.env` da uchala IP bilan birga
  o'zgartiring — `preflight.sh` ularning subnet ichida va farqli ekanini tekshiradi.
- `down -v` **barcha ma'lumotni o'chiradi** (baza, fayllar, Seq) — production'da ishlatmang.

---

## Lokal demo / stend

`deploy/docker-compose.dev.yml` override: demo seed (`SEED_DEMO` default `true`), barcha servislar `127.0.0.1`
portlarida, Seq autentifikatsiyasiz, CORS'ga Vite dev server qo'shilgan, `cloudflared` va `backup` faqat profil bilan.

```bash
cp deploy/.env.example deploy/.env
# Majburiy qiymatlar lokal uchun ham kerak (compose override'dan oldin tekshiradi) — soxta bo'lsa ham bo'ladi:
#   JWT_SIGNING_KEY=$(openssl rand -base64 48)   POSTGRES_PASSWORD=amaliyotchi   ADMIN_HEMIS_ID=100000000001
#   ADMIN_PASSWORD=admin12345   SEQ_ADMIN_PASSWORD=local   CLOUDFLARE_TUNNEL_TOKEN=none
#   DASHBOARD_PUBLIC_URL=http://127.0.0.1:8090   TWA_PUBLIC_URL=http://127.0.0.1:8091

docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml up -d --build
# Cloudflare Tunnel ham kerak bo'lsa (haqiqiy token bilan):
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.dev.yml --profile tunnel up -d --build
```

| Servis    | Host manzil                     | Izoh                                                  |
|-----------|---------------------------------|-------------------------------------------------------|
| dashboard | <http://127.0.0.1:8090>         | `DASHBOARD_PORT`                                      |
| twa       | <http://127.0.0.1:8091>         | `TWA_PORT`                                            |
| api       | <http://127.0.0.1:5080/health>  | `API_PORT`                                            |
| postgres  | `127.0.0.1:55432`               | `POSTGRES_PORT`; `amaliyotchi` / `POSTGRES_PASSWORD`  |
| seq       | <http://127.0.0.1:5341>         | `SEQ_PORT`                                            |

Demo login'lar: admin — `.env` dagi `ADMIN_HEMIS_ID`/`ADMIN_PASSWORD`; tyutor HEMIS ID `100000000002` / `tutor12345`;
talaba HEMIS ID `341030` / `talaba12345`. Bazani noldan: `... down -v`.

Telegram Mini App faqat HTTPS ochadi — lokal sinash uchun vaqtinchalik tunnel:
`cloudflared tunnel --url http://127.0.0.1:8091` (chiqqan `https://...trycloudflare.com` ni `TWA_PUBLIC_URL` ga).

Ildizdagi `compose.yaml` — `deploy/docker-compose.yml` ni (production rejimi, `deploy/.env` bilan) `include` qiladi:
ildizdan `docker compose up -d --build` ham ishlaydi; dev override bilan:
`docker compose -f compose.yaml -f deploy/docker-compose.dev.yml up -d --build`.

### Eski stack'dan o'tish (MinIO/Redis olib tashlandi)

Kod MinIO/Redis ishlatmaydi (fayllar — `LocalFileStorage`, `api-data` volume), ular stack'dan olib tashlandi.
Eski konteyner va volume'larni tozalash:

```bash
docker compose -f deploy/docker-compose.yml up -d --remove-orphans   # amaliyotchi-minio/redis o'chiriladi
docker volume rm amaliyotchi_minio-data                              # bo'sh MinIO ma'lumoti
```

Mavjud bazada `POSTGRES_PASSWORD` o'zgarmaydi — `.env` ga eski parolni (`amaliyotchi`, agar oldin default bo'lgan bo'lsa)
yozing yoki yuqoridagi `ALTER USER` bilan almashtiring. Seq volume avval autentifikatsiyasiz yaratilgan bo'lsa,
`SEQ_ADMIN_PASSWORD` qo'llanmaydi — Seq UI → Settings → Users'da autentifikatsiyani yoqing (yoki `seq-data` ni o'chiring).
