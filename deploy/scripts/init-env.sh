#!/usr/bin/env bash
# Amaliyotchi — deploy/.env ni yaratish yoki xavfsiz yangilash (yangi server ham, eski demo .env ham).
#
#   deploy/scripts/init-env.sh                       # interaktiv: yetishmayotgan qiymatlarni so'raydi
#   ADMIN_HEMIS_ID=... TELEGRAM_BOT_TOKEN=... CLOUDFLARE_TUNNEL_TOKEN=... \
#     deploy/scripts/init-env.sh --non-interactive   # hech narsa so'ramaydi (yetishmasa — xato, fayl o'zgarmaydi)
#
#   --non-interactive   so'ramaslik (stdin terminal bo'lmasa avtomatik)
#   --no-preflight      oxirida preflight.sh ni chaqirmaslik
#   --env-file <yo'l>   boshqa fayl (default: deploy/.env; yoki DEPLOY_ENV_FILE)
#
# Qoidalar:
#  - .env yo'q bo'lsa .env.example dan yaratiladi; bor bo'lsa o'zgarishdan oldin .env.bak-<sana> zaxirasi olinadi.
#  - FAQAT bo'sh / demo / zaif / noto'g'ri formatdagi qiymatlar almashtiriladi, qolganiga tegilmaydi.
#  - Generatsiya: JWT_SIGNING_KEY, POSTGRES_PASSWORD (hex), SEQ_ADMIN_PASSWORD (hex), ADMIN_PASSWORD (≥ 24 belgi,
#    ekranga BIR MARTA chiqadi). So'raladi: ADMIN_HEMIS_ID, TELEGRAM_BOT_TOKEN, CLOUDFLARE_TUNNEL_TOKEN
#    (muhit o'zgaruvchisi sifatida berilsa — o'sha qiymat ishlatiladi va mavjudini almashtiradi).
#  - Default: DASHBOARD_PUBLIC_URL, TWA_PUBLIC_URL, API_ALLOWED_HOSTS; SEED_DEMO har doim false.
#  - POSTGRES_PASSWORD almashsa va baza volume'i mavjud bo'lsa: bazadagi parolni deploy.sh o'zi moslaydi (ALTER USER).
set -Eeuo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${DEPLOY_ENV_FILE:-$DEPLOY_DIR/.env}"
EXAMPLE="$DEPLOY_DIR/.env.example"
INTERACTIVE=1; PREFLIGHT=1

while [ $# -gt 0 ]; do
  case "$1" in
    --non-interactive) INTERACTIVE=0 ;;
    --no-preflight) PREFLIGHT=0 ;;
    --env-file) [ $# -ge 2 ] || { echo "--env-file fayl yo'lini talab qiladi" >&2; exit 2; }; ENV_FILE="$2"; shift ;;
    -h|--help) sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "noma'lum parametr: $1 (--help)" >&2; exit 2 ;;
  esac
  shift
done
[ -t 0 ] || INTERACTIVE=0

DEFAULT_DASHBOARD_URL="https://amalyotchi.uz"
DEFAULT_TWA_URL="https://app.amalyotchi.uz"
DEFAULT_ALLOWED_HOSTS="amalyotchi.uz;app.amalyotchi.uz;localhost"
DEMO_HEMIS_ID="100000000001"
DEV_JWT_KEY="faqat-lokal-muhit-uchun-kalit-kamida-32-belgi!"
TG_RE='^[0-9]+:[A-Za-z0-9_-]{30,}$'
CF_RE='^[A-Za-z0-9+/=_-]{50,}$'
HEMIS_RE='^[0-9]{5,20}$'   # domen: HemisId 5–20 raqam

# Muhitdan berilgan qiymatlar (non-interaktiv rejim yoki aniq almashtirish uchun).
IN_HEMIS="${ADMIN_HEMIS_ID:-}"
IN_TG="${TELEGRAM_BOT_TOKEN:-}"
IN_CF="${CLOUDFLARE_TUNNEL_TOKEN:-}"

die() { echo "[XATO] $*" >&2; exit 1; }

# ---------- tasodifiy qiymatlar (openssl, bo'lmasa /dev/urandom) ----------
if command -v openssl >/dev/null 2>&1; then
  rand_b64() { openssl rand -base64 "$1" | tr -d '\n'; }
  rand_hex() { openssl rand -hex "$1"; }
else
  if ! { [ -r /dev/urandom ] && command -v base64 >/dev/null 2>&1 && command -v od >/dev/null 2>&1; }; then
    die "openssl ham, /dev/urandom + base64/od ham topilmadi — openssl o'rnating (apt-get install -y openssl)"
  fi
  echo "[i] openssl yo'q — /dev/urandom ishlatiladi"
  rand_b64() { head -c "$1" /dev/urandom | base64 | tr -d '\n'; }
  rand_hex() { head -c "$1" /dev/urandom | od -An -tx1 -v | tr -d ' \n'; }
fi
gen_admin_password() { rand_b64 18 | tr '+/' '-_'; }   # 24 belgi, URL-xavfsiz, `$` siz

# ---------- ishchi nusxa ----------
EXISTED=0
[ -f "$ENV_FILE" ] && EXISTED=1
if [ "$EXISTED" = 0 ]; then
  [ -f "$EXAMPLE" ] || die "$EXAMPLE topilmadi"
fi
umask 077
WORK="$(mktemp "${TMPDIR:-/tmp}/amaliyotchi-env.XXXXXX")"
trap 'rm -f "$WORK" "$WORK".*' EXIT
if [ "$EXISTED" = 1 ]; then cat "$ENV_FILE" >"$WORK"; else cat "$EXAMPLE" >"$WORK"; fi
# Oxirgi qatorda \n bo'lmasa — qo'shiladi (append to'g'ri ishlashi uchun).
[ -s "$WORK" ] && [ "$(tail -c 1 "$WORK" | od -An -c | tr -d ' ')" != '\n' ] && echo >>"$WORK"

get() {
  local line
  line="$(grep -E "^[ 	]*$1=" "$WORK" | tail -n 1)" || true
  line="${line#*=}"
  line="${line%$'\r'}"
  line="${line#\"}"; line="${line%\"}"; line="${line#\'}"; line="${line%\'}"
  printf '%s' "$line"
}

# KEY=VALUE ni yozadi (sed emas — qiymatda / + = & \ bo'lishi mumkin; awk ENVIRON orqali, escape talqinisiz):
# faol qator bo'lsa birinchisi almashtiriladi (takrorlari o'chiriladi); bo'lmasa `# KEY=` izohidan keyin
# qo'yiladi; u ham bo'lmasa fayl oxiriga.
set_kv() {
  local key="$1" val="$2" mode=append tmp
  if grep -Eq "^[ 	]*$key=" "$WORK"; then mode=replace
  elif grep -Eq "^#[ 	]*$key=" "$WORK"; then mode=after_comment; fi
  tmp="$WORK.new"
  K="$key" V="$val" M="$mode" awk '
    BEGIN { k = ENVIRON["K"]; v = ENVIRON["V"]; m = ENVIRON["M"]; done = 0 }
    m == "replace" && $0 ~ ("^[ \t]*" k "=") { if (!done) { print k "=" v; done = 1 } ; next }
    { print }
    m == "after_comment" && !done && $0 ~ ("^#[ \t]*" k "=") { print k "=" v; done = 1 }
    END { if (m == "append") print k "=" v }
  ' "$WORK" >"$tmp"
  mv "$tmp" "$WORK"
}

CHANGES=()
change() { set_kv "$1" "$2"; CHANGES+=("$1 — $3"); }
lc() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }

# Interaktiv so'rash (regex bilan tekshiruv); non-interaktiv — bo'sh qaytaradi.
ask() {
  local prompt="$1" re="$2" bad="${3:-}" ans
  [ "$INTERACTIVE" = 1 ] || return 0
  while :; do
    read -r -p "  $prompt: " ans </dev/tty || return 0
    ans="$(printf '%s' "$ans" | tr -d '[:space:]')"
    if [[ "$ans" =~ $re ]] && [ "$ans" != "$bad" ]; then printf '%s' "$ans"; return 0; fi
    echo "    noto'g'ri format, qaytadan kiriting" >/dev/tty
  done
}

MISSING=()
# Foydalanuvchi beradigan qiymat: env > mavjud to'g'ri qiymat > so'rash.
user_value() {
  local key="$1" in="$2" re="$3" bad="$4" prompt="$5" cur new
  cur="$(get "$key")"
  if [ -n "$in" ]; then
    [[ "$in" =~ $re ]] && [ "$in" != "$bad" ] || die "$key (muhitdan) noto'g'ri formatda"
    [ "$in" != "$cur" ] && change "$key" "$in" "muhit o'zgaruvchisidan"
    return 0
  fi
  if [[ "$cur" =~ $re ]] && [ "$cur" != "$bad" ]; then return 0; fi
  new="$(ask "$prompt" "$re" "$bad")"
  if [ -n "$new" ]; then
    change "$key" "$new" "kiritildi"
  else
    MISSING+=("$key")
  fi
}

echo "== $ENV_FILE ($([ "$EXISTED" = 1 ] && echo "mavjud — faqat kerakli qiymatlar yangilanadi" || echo "yangi, .env.example dan"))"

# ---------- foydalanuvchi beradigan qiymatlar ----------
user_value ADMIN_HEMIS_ID "$IN_HEMIS" "$HEMIS_RE" "$DEMO_HEMIS_ID" \
  "Birinchi admin HEMIS ID (login, faqat raqamlar; demo $DEMO_HEMIS_ID emas)"
user_value TELEGRAM_BOT_TOKEN "$IN_TG" "$TG_RE" "" \
  "Telegram bot tokeni (@BotFather, 123456:ABC...)"
user_value CLOUDFLARE_TUNNEL_TOKEN "$IN_CF" "$CF_RE" "" \
  "Cloudflare Tunnel tokeni (Zero Trust → Tunnels → --token qiymati)"

if [ ${#MISSING[@]} -gt 0 ]; then
  die "berilmagan yoki noto'g'ri: ${MISSING[*]} — interaktiv ishga tushiring yoki muhit orqali bering:
  ADMIN_HEMIS_ID=... TELEGRAM_BOT_TOKEN=... CLOUDFLARE_TUNNEL_TOKEN=... $0 --non-interactive
(.env o'zgartirilmadi)"
fi

# ---------- sirlar ----------
v="$(get JWT_SIGNING_KEY)"
if [ -z "$v" ] || [ "${#v}" -lt 32 ] || [ "$v" = "$DEV_JWT_KEY" ] || [[ "$(lc "$v")" == *change_me* ]] || [[ "$v" == *'$'* ]]; then
  change JWT_SIGNING_KEY "$(rand_b64 48)" "generatsiya (bo'sh/qisqa/dev kalit edi; barcha sessiyalar bekor bo'ladi)"
fi

PG_OLD="$(get POSTGRES_PASSWORD)"; PG_CHANGED=0
case "$(lc "$PG_OLD")" in amaliyotchi|postgres|password|change_me) pg_weak=1 ;; *) pg_weak=0 ;; esac
if [ "$pg_weak" = 1 ] || [ "${#PG_OLD}" -lt 16 ] || ! [[ "$PG_OLD" =~ ^[A-Za-z0-9._~+/=-]+$ ]]; then
  change POSTGRES_PASSWORD "$(rand_hex 24)" "generatsiya (bo'sh/default/qisqa yoki xavfli belgi)"
  PG_CHANGED=1
fi

ADMIN_GENERATED=""
v="$(get ADMIN_PASSWORD)"
case "$(lc "$v")" in
  admin12345|tutor12345|talaba12345|password|password123|123456789012|admin|administrator|amaliyotchi) adm_weak=1 ;;
  *) adm_weak=0 ;;
esac
if [ "$adm_weak" = 1 ] || [ "${#v}" -lt 12 ] || [[ "$(lc "$v")" == *change_me* ]] || [[ "$v" == *'$'* ]]; then
  ADMIN_GENERATED="$(gen_admin_password)"
  change ADMIN_PASSWORD "$ADMIN_GENERATED" "generatsiya (bo'sh/demo/qisqa edi)"
fi

v="$(get SEQ_ADMIN_PASSWORD)"
if [ "${#v}" -lt 12 ] || [[ "$v" == *'$'* ]]; then
  change SEQ_ADMIN_PASSWORD "$(rand_hex 16)" "generatsiya (bo'sh/qisqa edi)"
fi

# ---------- manzillar va xavfsiz default'lar ----------
for pair in "DASHBOARD_PUBLIC_URL|$DEFAULT_DASHBOARD_URL" "TWA_PUBLIC_URL|$DEFAULT_TWA_URL"; do
  key="${pair%%|*}"; def="${pair#*|}"; v="$(get "$key")"
  if [[ "$v" != https://* ]] || [[ "$v" == *localhost* ]] || [[ "$v" == *127.0.0.1* ]]; then
    change "$key" "$def" "default (bo'sh yoki HTTPS emas: '${v}')"
  elif [[ "$v" == */ ]]; then
    change "$key" "${v%/}" "oxiridagi '/' olib tashlandi"
  fi
done

v="$(get API_ALLOWED_HOSTS)"
if [ -z "$v" ] || [ "$v" = "*" ]; then
  change API_ALLOWED_HOSTS "$DEFAULT_ALLOWED_HOSTS" "default"
elif [[ ";$v;" != *";localhost;"* ]]; then
  change API_ALLOWED_HOSTS "$v;localhost" "'localhost' qo'shildi (healthcheck uchun shart)"
fi

v="$(get SEED_DEMO)"
if [ "$v" != "false" ]; then
  [ -n "$v" ] || v="berilmagan"
  change SEED_DEMO false "production'da demo seed o'chiq (avval: $v)"
fi

# ---------- yozish ----------
if [ ${#CHANGES[@]} -eq 0 ] && [ "$EXISTED" = 1 ]; then
  echo "O'zgarish kerak emas — barcha qiymatlar joyida."
else
  if [ "$EXISTED" = 1 ]; then
    bak="$ENV_FILE.bak-$(date +%Y%m%d-%H%M%S)"
    cp -p "$ENV_FILE" "$bak"; chmod 600 "$bak"
    echo "Zaxira nusxa: $bak"
  fi
  cat "$WORK" >"$ENV_FILE.tmp.$$" && mv "$ENV_FILE.tmp.$$" "$ENV_FILE"
  echo "Yangilangan kalitlar:"
  for c in "${CHANGES[@]}"; do echo "  - $c"; done
fi
chmod 600 "$ENV_FILE"

# ---------- ogohlantirishlar ----------
if [ "$PG_CHANGED" = 1 ] && [ "$EXISTED" = 1 ] && command -v docker >/dev/null 2>&1; then
  project="${COMPOSE_PROJECT_NAME:-amaliyotchi}"
  if docker volume inspect "${project}_postgres-data" >/dev/null 2>&1; then
    cat <<EOF

[!] POSTGRES_PASSWORD almashtirildi, '${project}_postgres-data' volume esa mavjud — bazadagi parol eski.
    deploy/scripts/deploy.sh postgres'ni ko'tarib, parolni o'zi moslaydi (unix socket + ALTER USER).
    Shuning uchun stack'ni 'docker compose up' bilan emas, deploy.sh bilan ko'taring.
EOF
  fi
fi
if [ -n "$ADMIN_GENERATED" ]; then
  cat <<EOF

============================================================
 Admin paroli (FAQAT HOZIR ko'rsatiladi — parol menejeriga saqlang):
   HEMIS ID: $(get ADMIN_HEMIS_ID)
   Parol:    $ADMIN_GENERATED
 Eslatma: admin faqat bazada admin bo'lmasa yaratiladi. Eski (demo) baza
 saqlansa, bu parol emas — eski admin amal qiladi (reset-data.sh / purge-demo.sh).
============================================================
EOF
fi

rm -f "$WORK"
if [ "$PREFLIGHT" = 1 ]; then
  echo
  if command -v docker >/dev/null 2>&1; then
    exec "$DEPLOY_DIR/scripts/preflight.sh" "$ENV_FILE"
  else
    echo "[i] docker topilmadi — preflight o'tkazib yuborildi (Docker o'rnatgach: deploy/scripts/preflight.sh)"
  fi
fi
