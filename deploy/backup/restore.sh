#!/usr/bin/env bash
# Amaliyotchi — zaxiradan tiklash (HOSTDA, loyiha ildizidan yoki istalgan joydan ishga tushiriladi).
#
#   deploy/backup/restore.sh db    deploy/backups/db/amaliyotchi-20261001-210000.dump
#   deploy/backup/restore.sh files deploy/backups/files/files-20261001-210000.tar.gz
#
# db    — api va backup to'xtatiladi, `amaliyotchi` bazasi O'CHIRILIB qayta yaratiladi, dump tiklanadi, api qayta yoqiladi.
# files — api to'xtatiladi, joriy api-data/files → api-data/files.old ga ko'chiriladi, arxiv ochiladi, api qayta yoqiladi.
# Tasdiqsiz ishlatish: RESTORE_YES=1 deploy/backup/restore.sh ...
# Boshqa stack'ga (masalan sinov nusxasi) yo'naltirish: COMPOSE_PROJECT_NAME, RESTORE_COMPOSE_FILE, RESTORE_ENV_FILE.
set -Eeuo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="${COMPOSE_PROJECT_NAME:-amaliyotchi}"
COMPOSE=(docker compose -p "$PROJECT" -f "${RESTORE_COMPOSE_FILE:-$DEPLOY_DIR/docker-compose.yml}")
[ -n "${RESTORE_ENV_FILE:-}" ] && COMPOSE+=(--env-file "$RESTORE_ENV_FILE")
PG_IMAGE="postgis/postgis:16-3.4"

usage() { echo "Foydalanish: $0 db <fayl.dump> | files <fayl.tar.gz>" >&2; exit 2; }
confirm() {
  [ "${RESTORE_YES:-}" = "1" ] && return 0
  read -r -p "$1 Davom etilsinmi? [yes/N] " ans
  [ "$ans" = "yes" ] || { echo "Bekor qilindi."; exit 1; }
}

[ $# -eq 2 ] || usage
MODE="$1"
SRC="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"
[ -s "$SRC" ] || { echo "Fayl topilmadi yoki bo'sh: $2" >&2; exit 1; }

case "$MODE" in
  db)
    confirm "DIQQAT: 'amaliyotchi' bazasi butunlay o'chiriladi va $SRC dan tiklanadi."
    "${COMPOSE[@]}" stop api backup
    # template_postgis (postgis image yaratadi) bo'lsa undan — PostGIS kengaytmalari tayyor bo'ladi.
    tpl="$("${COMPOSE[@]}" exec -T postgres psql -U amaliyotchi -d postgres -tAc \
      "SELECT datname FROM pg_database WHERE datname='template_postgis'")"
    tpl="${tpl:-template1}"
    "${COMPOSE[@]}" exec -T postgres dropdb -U amaliyotchi --maintenance-db=postgres --force --if-exists amaliyotchi
    "${COMPOSE[@]}" exec -T postgres createdb -U amaliyotchi --maintenance-db=postgres -O amaliyotchi -T "$tpl" amaliyotchi
    # Xatolar (masalan "already exists" — PostGIS ob'ektlari) ogohlantirish sifatida chiqadi; jadval ma'lumotlari tiklanadi.
    if ! "${COMPOSE[@]}" exec -T postgres pg_restore -U amaliyotchi -d amaliyotchi --no-owner < "$SRC"; then
      echo "OGOHLANTIRISH: pg_restore xatolar bilan tugadi — yuqoridagi xabarlarni tekshiring." >&2
    fi
    "${COMPOSE[@]}" start api backup
    echo "Tayyor. Tekshirish: ${COMPOSE[*]} logs --tail 50 api"
    ;;
  files)
    confirm "DIQQAT: api-data/files $SRC dan tiklanadi (joriy nusxa files.old ga ko'chiriladi)."
    vol="$(docker volume ls -q --filter label=com.docker.compose.project="$PROJECT" \
      --filter label=com.docker.compose.volume=api-data)"
    [ -n "$vol" ] || { echo "api-data volume topilmadi" >&2; exit 1; }
    "${COMPOSE[@]}" stop api
    docker run --rm -v "$vol:/app/data" -v "$SRC:/restore.tar.gz:ro" --entrypoint bash "$PG_IMAGE" -c '
      set -e
      rm -rf /app/data/files.old
      if [ -d /app/data/files ]; then mv /app/data/files /app/data/files.old; fi
      tar -xzpf /restore.tar.gz -C /app/data   # root sifatida: egalik (uid) arxivdagidek saqlanadi
    '
    "${COMPOSE[@]}" start api
    echo "Tayyor. Eski nusxa volume ichida: files.old (tekshirib bo'lgach o'chirish:"
    echo "  docker run --rm -v $vol:/app/data --entrypoint rm $PG_IMAGE -rf /app/data/files.old )"
    ;;
  *) usage ;;
esac
