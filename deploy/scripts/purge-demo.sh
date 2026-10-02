#!/usr/bin/env bash
# Amaliyotchi — demo seed ma'lumotini (Seed:Demo=true bilan yuklangan) xavfsiz o'chirish. HOSTDA ishga tushiriladi.
#
#   deploy/scripts/purge-demo.sh                 dry-run: nima o'chishini va to'siqlarni ko'rsatadi (hech narsa o'zgarmaydi)
#   deploy/scripts/purge-demo.sh --apply         zaxira → tasdiq → bitta tranzaksiyada o'chirish (+ demo fayllar diskdan)
#   deploy/scripts/purge-demo.sh --apply --yes --no-backup
#
#   --apply      o'chirish (aks holda dry-run)
#   --yes        interaktiv tasdiqsiz (faqat --apply bilan ma'noli)
#   --no-backup  --apply dan oldingi zaxirani o'tkazib yuborish (TAVSIYA ETILMAYDI)
#
# Ishlayotgan `api` konteyneri ichida: `dotnet Amaliyotchi.Api.dll purge-demo [--apply]` (web host/bot ishga tushmaydi,
# konfiguratsiya API bilan bir xil). Demo ma'lumotga demo bo'lmagan ma'lumot bog'langan bo'lsa hech narsa o'chirilmaydi.
# Chiqish kodlari: 0 ok / demo topilmadi, 1 xato, 2 to'siqlar bor (hech narsa o'chirilmadi), 64 noto'g'ri argument.
#
# Boshqa stack (sinov nusxasi): COMPOSE_PROJECT_NAME (default amaliyotchi), COMPOSE_FILE (berilsa `-f` qo'yilmaydi —
# docker compose o'zi o'qiydi; aks holda deploy/docker-compose.yml), DEPLOY_ENV_FILE (default deploy/.env, bo'lsa).
set -Eeuo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="${COMPOSE_PROJECT_NAME:-amaliyotchi}"
ENV_FILE="${DEPLOY_ENV_FILE:-$DEPLOY_DIR/.env}"

COMPOSE=(docker compose -p "$PROJECT")
[ -n "${COMPOSE_FILE:-}" ] || COMPOSE+=(-f "$DEPLOY_DIR/docker-compose.yml")
[ -f "$ENV_FILE" ] && COMPOSE+=(--env-file "$ENV_FILE")

die()  { echo "  [XATO] $*" >&2; exit 1; }
warn() { echo "  [OGOH] $*" >&2; }

APPLY=0; YES=0; BACKUP=1
for a in "$@"; do
  case "$a" in
    --apply) APPLY=1 ;;
    --yes) YES=1 ;;
    --no-backup) BACKUP=0 ;;
    -h|--help) sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "  [XATO] noma'lum parametr: $a (--help)" >&2; exit 64 ;;
  esac
done

svc_running() { [ -n "$("${COMPOSE[@]}" ps --status running -q "$1" 2>/dev/null)" ]; }

command -v docker >/dev/null 2>&1 || die "docker topilmadi"
svc_running api || die "'api' servisi ishlamayapti ($PROJECT) — avval stack'ni ko'taring (deploy/scripts/deploy.sh)"

purge() { "${COMPOSE[@]}" exec -T api dotnet Amaliyotchi.Api.dll purge-demo "$@"; }

if [ "$APPLY" != 1 ]; then
  echo "==> Dry-run (hech narsa o'zgartirilmaydi)"
  purge
  exit $?
fi

# 1) Avval dry-run: demo yo'q (0 qator) yoki to'siq bo'lsa — zaxira va tasdiqsiz to'xtaymiz.
echo "==> Tekshiruv (dry-run)"
set +e
report="$(purge 2>&1)"; rc=$?
set -e
printf '%s\n' "$report"
[ "$rc" = 0 ] || { echo "Dry-run $rc kodi bilan tugadi — o'chirish boshlanmadi." >&2; exit "$rc"; }
if printf '%s' "$report" | grep -q "Demo ma'lumot topilmadi"; then
  exit 0
fi

# 2) Zaxira (backup servisi — baza dump'i + fayllar arxivi).
if [ "$BACKUP" = 1 ]; then
  echo "==> Zaxira olinmoqda (backup servisi)"
  svc_running backup || die "'backup' servisi ishlamayapti — zaxirasiz o'chirmaymiz. Servisni yoqing yoki ongli ravishda --no-backup bering."
  "${COMPOSE[@]}" exec -T backup /scripts/backup.sh once || die "zaxira muvaffaqiyatsiz — o'chirish boshlanmadi"
else
  warn "--no-backup: zaxirasiz o'chirilmoqda"
fi

# 3) Tasdiq.
if [ "$YES" != 1 ]; then
  [ -t 0 ] || die "interaktiv bo'lmagan rejimda --yes kerak"
  read -r -p "    Yuqoridagi demo ma'lumot O'CHIRILADI. Tasdiqlash uchun DEMONI O'CHIR deb yozing: " ans
  [ "$ans" = "DEMONI O'CHIR" ] || die "bekor qilindi"
fi

# 4) O'chirish (to'siq paydo bo'lsa — exit 2, hech narsa o'chmaydi).
echo "==> O'chirilmoqda"
purge --apply
