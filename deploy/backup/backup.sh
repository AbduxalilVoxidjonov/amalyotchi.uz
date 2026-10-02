#!/usr/bin/env bash
# Amaliyotchi zaxira nusxasi — `backup` konteyneri ichida ishlaydi (postgis/postgis:16 image, pg_dump 16).
#   backup.sh loop  — har kuni BACKUP_TIME_UTC da (default 21:00 UTC); start'da oxirgi 24 soatda zaxira bo'lmasa darhol
#   backup.sh once  — bir martalik zaxira (qo'lda: docker compose ... exec backup /scripts/backup.sh once)
# Natija: /backups/db/amaliyotchi-<UTC vaqt>.dump (pg_dump -Fc), /backups/files/files-<UTC vaqt>.tar.gz (api-data/files)
# Rotatsiya: BACKUP_RETENTION_DAYS kundan eski fayllar o'chiriladi. Muvaffaqiyat belgisi: /backups/.last_success
set -Eeuo pipefail
umask 077

BACKUP_ROOT=/backups
DB_DIR="$BACKUP_ROOT/db"
FILES_DIR="$BACKUP_ROOT/files"
API_DATA=/srv/api-data
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
BACKUP_TIME_UTC="${BACKUP_TIME_UTC:-21:00}"
MARKER="$BACKUP_ROOT/.last_success"

log() { echo "[backup] $(date -u +%Y-%m-%dT%H:%M:%SZ) $*"; }

run_once() {
  local ts tmp
  ts="$(date -u +%Y%m%d-%H%M%S)"
  mkdir -p "$DB_DIR" "$FILES_DIR"

  # 1) PostgreSQL — custom format (siqilgan, pg_restore bilan tanlab tiklash mumkin)
  tmp="$DB_DIR/.amaliyotchi-$ts.dump.partial"
  log "pg_dump boshlandi → $DB_DIR/amaliyotchi-$ts.dump"
  pg_dump --format=custom --compress=6 --no-password --file="$tmp"
  pg_restore --list "$tmp" >/dev/null          # arxiv o'qilishini tekshirish
  mv "$tmp" "$DB_DIR/amaliyotchi-$ts.dump"

  # 2) Yuklangan fayllar (api-data/files) — volume read-only ulangan
  if [ -d "$API_DATA/files" ]; then
    tmp="$FILES_DIR/.files-$ts.tar.gz.partial"
    tar -C "$API_DATA" -czf "$tmp" files
    mv "$tmp" "$FILES_DIR/files-$ts.tar.gz"
    log "fayllar arxivlandi → $FILES_DIR/files-$ts.tar.gz"
  else
    log "$API_DATA/files yo'q — fayl zaxirasi o'tkazib yuborildi"
  fi

  # 3) Rotatsiya (va avvalgi muvaffaqiyatsiz urinishlardan qolgan .partial fayllar)
  find "$DB_DIR" "$FILES_DIR" -maxdepth 1 -type f \
    \( -name 'amaliyotchi-*.dump' -o -name 'files-*.tar.gz' \) \
    -mmin +$((RETENTION_DAYS * 1440)) -print -delete | sed 's/^/[backup] o'"'"'chirildi: /'
  find "$DB_DIR" "$FILES_DIR" -maxdepth 1 -type f -name '.*.partial' -mmin +60 -delete

  touch "$MARKER"
  log "tayyor ($(du -sh "$BACKUP_ROOT" | cut -f1) jami)"
}

seconds_until_next_run() {
  local now target
  now="$(date -u +%s)"
  target="$(date -u -d "today $BACKUP_TIME_UTC" +%s)"
  [ "$target" -le "$now" ] && target=$((target + 86400))
  echo $((target - now))
}

case "${1:-loop}" in
  once)
    run_once
    ;;
  loop)
    # Har zaxira alohida jarayonda (set -e `||` kontekstida o'chib qolmasligi uchun).
    trap 'log "to'"'"'xtatildi"; exit 0' TERM INT
    log "rejim: har kuni $BACKUP_TIME_UTC UTC, saqlash muddati $RETENTION_DAYS kun"
    if [ -z "$(find "$MARKER" -mmin -1440 2>/dev/null)" ]; then
      log "oxirgi 24 soatda zaxira yo'q — hozir olinadi"
      bash "$0" once || log "XATO: zaxira muvaffaqiyatsiz (keyingi rejali vaqtda qayta urinadi)"
    fi
    while true; do
      wait_s="$(seconds_until_next_run)"
      log "keyingi zaxira ${wait_s}s dan keyin"
      sleep "$wait_s" & wait $!
      bash "$0" once || log "XATO: zaxira muvaffaqiyatsiz (keyingi rejali vaqtda qayta urinadi)"
    done
    ;;
  *)
    echo "Foydalanish: backup.sh [loop|once]" >&2
    exit 2
    ;;
esac
