#!/usr/bin/env bash
# Amaliyotchi — bir buyruqli deploy / yangilash (yangi server ham, eski versiya ishlab turgan server ham).
#
#   deploy/scripts/deploy.sh [--pull] [--no-build] [--skip-backup]
#
#   --pull         avval `git pull --ff-only`
#   --no-build     image'larni qayta build qilmaslik (faqat konfiguratsiya o'zgarganda)
#   --skip-backup  yangilashdan oldingi zaxirani o'tkazib yuborish (tavsiya etilmaydi)
#
# Qadamlar: preflight → (stack ishlasa) zaxira → build → postgres + parol sinxronizatsiyasi (ALTER USER) →
# up -d --remove-orphans (eski redis/minio konteynerlari o'chadi, volume'larga tegilmaydi) → healthy kutish → hisobot.
# Sinov/boshqa stack: COMPOSE_PROJECT_NAME, DEPLOY_COMPOSE_FILE, DEPLOY_ENV_FILE, HEALTH_TIMEOUT (deploy/scripts/lib.sh).
set -Eeuo pipefail
# shellcheck source=deploy/scripts/lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

PULL=0; BUILD=1; BACKUP=1
for a in "$@"; do
  case "$a" in
    --pull) PULL=1 ;;
    --no-build) BUILD=0 ;;
    --skip-backup) BACKUP=0 ;;
    -h|--help) sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "noma'lum parametr: $a (--help)" ;;
  esac
done

if [ "$PULL" = 1 ]; then
  step "git pull --ff-only"
  git -C "$ROOT_DIR" pull --ff-only
  # Yangilangan skript bilan davom etish (pull deploy.sh ning o'zini o'zgartirgan bo'lishi mumkin).
  args=()
  [ "$BUILD" = 0 ] && args+=(--no-build)
  [ "$BACKUP" = 0 ] && args+=(--skip-backup)
  exec "$0" ${args[@]+"${args[@]}"}
fi

require_docker

step "Preflight ($ENV_FILE)"
"$DEPLOY_DIR/scripts/preflight.sh" "$ENV_FILE" || die "preflight xato topdi — deploy/scripts/init-env.sh bilan tuzating"

step "Zaxira (yangilashdan oldin)"
if [ "$BACKUP" = 0 ]; then
  warn "--skip-backup: zaxira olinmadi"
elif svc_running postgres; then
  backup_now predeploy || die "zaxira olinmadi — sababini tuzating yoki --skip-backup bilan (xavfli) davom eting"
elif [ -n "$(project_volume postgres-data)" ]; then
  info "postgres to'xtagan — ko'tarib zaxira olinadi"
  start_postgres || die "postgres ishga tushmadi"
  backup_now predeploy || die "zaxira olinmadi"
else
  info "baza hali yo'q (yangi server) — zaxira shart emas"
fi

if [ "$BUILD" = 1 ]; then
  step "Image'larni build qilish (birinchi marta 5–10 daqiqa)"
  "${COMPOSE[@]}" build
fi

step "PostgreSQL va parol sinxronizatsiyasi"
start_postgres || die "postgres healthy bo'lmadi"
sync_pg_password

step "Stack'ni ko'tarish (up -d --build --remove-orphans)"
# --build qayta (kesh tayyor — soniyalar): compose faqat shunda yangi image'li servislarni qayta yaratadi
# (alohida `build` + `up --no-build` env'i o'zgarmagan dashboard/twa ni eski image'da qoldiradi).
up_args=(-d --remove-orphans)
if [ "$BUILD" = 1 ]; then up_args+=(--build); else up_args+=(--no-build); fi
if ! compose_up_bounded "$HEALTH_TIMEOUT" "${up_args[@]}"; then
  warn "'up' xato bilan tugadi — holat tekshirilmoqda"
fi

step "Healthy bo'lishini kutish (${HEALTH_TIMEOUT}s gacha)"
wait_healthy "$HEALTH_TIMEOUT" || die "deploy muvaffaqiyatsiz — yuqoridagi log'larni ko'ring"

step "Holat"
"${COMPOSE[@]}" ps

# --- Eslatmalar ---
q() { "${COMPOSE[@]}" exec -T postgres psql -U amaliyotchi -d amaliyotchi -tAc "$1" 2>/dev/null | tr -d '[:space:]'; }
demo="$(q "select count(*) from users where hemis_id in ('100000000001','100000000002','100000000003','100000000004','341030') and not is_deleted")"
admin_hemis="$(env_get ADMIN_HEMIS_ID)"
admin_ok=""
if [[ "$admin_hemis" =~ ^[0-9]+$ ]]; then   # faqat raqam — SQL'ga xavfsiz qo'shiladi (role 1 = Admin)
  admin_ok="$(q "select count(*) from users where hemis_id = '$admin_hemis' and role = 1 and not is_deleted")"
fi

step "Keyingi qadamlar"
if [ -n "$demo" ] && [ "$demo" != 0 ]; then
  echo "  [!] Bazada DEMO hisoblar bor ($demo ta: 100000000001.., 341030 ...) — ular ochiq parollar bilan!"
  if [ -x "$DEPLOY_DIR/scripts/purge-demo.sh" ]; then
    echo "      Faqat demo'ni olib tashlash:  deploy/scripts/purge-demo.sh          (dry-run — nima o'chishini ko'rsatadi)"
    echo "                                    deploy/scripts/purge-demo.sh --apply  (o'chiradi)"
  fi
  echo "      Butunlay noldan boshlash:     deploy/scripts/reset-data.sh   (zaxira oladi, bazani tozalaydi, admin .env dan)"
fi
if [ "$admin_ok" = 0 ]; then
  echo "  [!] .env dagi admin ($admin_hemis) bazada yo'q: birinchi admin faqat bazada admin bo'lmasa yaratiladi."
  echo "      Eski (demo) admin bilan kiring yoki reset-data.sh / purge-demo.sh dan foydalaning."
fi
old_vols="$(docker volume ls -q | grep -E "^${PROJECT}_(minio-data|redis)" || true)"
if [ -n "$old_vols" ]; then
  echo "  Eski stack'dan qolgan (endi ishlatilmaydi) volume'lar — tekshirib, qo'lda o'chiring:"
  for v in $old_vols; do echo "      docker volume rm $v"; done
fi
cat <<EOF
  Tekshirish: $(env_get DASHBOARD_PUBLIC_URL) → admin login ($admin_hemis);
              BotFather → Menu Button URL = $(env_get TWA_PUBLIC_URL)
  Loglar:     docker compose -f deploy/docker-compose.yml logs -f --tail 100 api
  Tozalash:   docker image prune -f && docker builder prune -f --filter until=168h
EOF
echo
echo "Deploy tayyor."
