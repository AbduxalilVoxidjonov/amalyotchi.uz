#!/usr/bin/env bash
# Amaliyotchi — BUTUN ma'lumotni o'chirib noldan boshlash (demo-only serverni toza production'ga o'tkazish).
#
#   deploy/scripts/reset-data.sh [--yes-delete-all-data] [--include-seq] [--skip-backup]
#
# 1) zaxira (BACKUP_DIR ga: baza dump'i; backup servisi ishlasa — fayllar arxivi ham);
# 2) stack to'xtatiladi (docker compose down — volume'lar saqlanadi);
# 3) FAQAT `postgres-data` va `api-data` volume'lari o'chiriladi (`--include-seq` bilan `seq-data` ham);
# 4) stack qayta ko'tariladi: migratsiyalar + birinchi admin .env dagi ADMIN_HEMIS_ID / ADMIN_PASSWORD bilan.
# Tasdiq: interaktiv "HAMMASINI O'CHIR" yozish yoki --yes-delete-all-data.
# Sinov/boshqa stack: COMPOSE_PROJECT_NAME, DEPLOY_COMPOSE_FILE, DEPLOY_ENV_FILE, HEALTH_TIMEOUT (deploy/scripts/lib.sh).
set -Eeuo pipefail
# shellcheck source=deploy/scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

YES=0; SEQ=0; BACKUP=1
for a in "$@"; do
  case "$a" in
    --yes-delete-all-data) YES=1 ;;
    --include-seq) SEQ=1 ;;
    --skip-backup) BACKUP=0 ;;
    -h|--help) sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "noma'lum parametr: $a (--help)" ;;
  esac
done

require_docker
"$DEPLOY_DIR/scripts/preflight.sh" "$ENV_FILE" >/dev/null \
  || die "preflight xato topdi (deploy/scripts/preflight.sh) — noto'g'ri .env bilan qayta yaratilgan baza ham ishlamaydi"

vols=(postgres-data api-data)
[ "$SEQ" = 1 ] && vols+=(seq-data)

step "DIQQAT: '$PROJECT' stack'ining BARCHA ma'lumoti o'chiriladi"
info "volume'lar: ${vols[*]}  (baza, yuklangan fayllar$([ "$SEQ" = 1 ] && echo ", loglar"))"
info "keyin birinchi admin: HEMIS ID $(env_get ADMIN_HEMIS_ID) / .env dagi ADMIN_PASSWORD"
if [ "$YES" != 1 ]; then
  [ -t 0 ] || die "interaktiv bo'lmagan rejimda --yes-delete-all-data kerak"
  read -r -p "    Tasdiqlash uchun HAMMASINI O'CHIR deb yozing: " ans
  [ "$ans" = "HAMMASINI O'CHIR" ] || die "bekor qilindi"
fi

step "Zaxira"
if [ "$BACKUP" = 0 ]; then
  warn "--skip-backup: zaxira olinmadi"
elif [ -n "$(project_volume postgres-data)" ]; then
  svc_running postgres || start_postgres || die "postgres ishga tushmadi — zaxira olinmadi (majburan: --skip-backup)"
  backup_now prereset || die "zaxira olinmadi — o'chirish bekor qilindi (majburan: --skip-backup)"
else
  info "postgres-data volume yo'q — zaxira shart emas"
fi

step "Stack'ni to'xtatish"
"${COMPOSE[@]}" down --remove-orphans

step "Volume'larni o'chirish"
for v in "${vols[@]}"; do
  name="$(project_volume "$v")"
  if [ -z "$name" ]; then info "$v — yo'q"; continue; fi
  docker volume rm "$name" >/dev/null
  info "o'chirildi: $name"
done

step "Stack'ni qayta ko'tarish"
compose_up_bounded "$HEALTH_TIMEOUT" -d --remove-orphans || warn "'up' xato bilan tugadi — holat tekshirilmoqda"
wait_healthy "$HEALTH_TIMEOUT" || die "stack healthy bo'lmadi"
"${COMPOSE[@]}" ps

users="$("${COMPOSE[@]}" exec -T postgres psql -U amaliyotchi -d amaliyotchi -tAc \
  "select string_agg(hemis_id || ' (role ' || role || ')', ', ') from users" 2>/dev/null || true)"
step "Tayyor"
info "bazadagi foydalanuvchilar: ${users:-?}"
info "kirish: $(env_get DASHBOARD_PUBLIC_URL) → HEMIS ID $(env_get ADMIN_HEMIS_ID) / ADMIN_PASSWORD (.env)"
info "zaxira: $(backup_dir)"
