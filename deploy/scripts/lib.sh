# shellcheck shell=bash
# Amaliyotchi deploy skriptlari uchun umumiy funksiyalar (deploy.sh, reset-data.sh `source` qiladi).
#
# Sozlash (sinov nusxasi yoki boshqa stack uchun; default — production):
#   COMPOSE_PROJECT_NAME   compose loyiha nomi           (default: amaliyotchi)
#   DEPLOY_COMPOSE_FILE    compose fayl                  (default: deploy/docker-compose.yml)
#   DEPLOY_ENV_FILE        .env fayl                     (default: deploy/.env)
#   HEALTH_TIMEOUT         healthy kutish, soniya        (default: 300)

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck disable=SC2034  # deploy.sh ishlatadi
ROOT_DIR="$(cd "$DEPLOY_DIR/.." && pwd)"
PROJECT="${COMPOSE_PROJECT_NAME:-amaliyotchi}"
ENV_FILE="${DEPLOY_ENV_FILE:-$DEPLOY_DIR/.env}"
COMPOSE_FILE_MAIN="${DEPLOY_COMPOSE_FILE:-$DEPLOY_DIR/docker-compose.yml}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-300}"
COMPOSE=(docker compose -p "$PROJECT" -f "$COMPOSE_FILE_MAIN" --env-file "$ENV_FILE")
# healthcheck'i uzoq start_period'li (birinchi tekshiruv 5 daqiqadan keyin) — "running" yetarli.
RUNNING_ONLY_SERVICES=" backup "

step() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
info() { echo "    $*"; }
warn() { echo "  [OGOH] $*" >&2; }
die()  { echo "  [XATO] $*" >&2; exit 1; }

# .env dan qiymat (oxirgi uchragani, qo'shtirnoqlarsiz). Faylni `source` qilmaydi.
env_get() {
  local line
  line="$(grep -E "^[[:space:]]*$1=" "$ENV_FILE" 2>/dev/null | tail -n 1)" || true
  line="${line#*=}"
  line="${line%$'\r'}"
  line="${line#\"}"; line="${line%\"}"; line="${line#\'}"; line="${line%\'}"
  printf '%s' "$line"
}

require_docker() {
  command -v docker >/dev/null 2>&1 || die "docker topilmadi (README: Docker o'rnatish)"
  docker compose version >/dev/null 2>&1 || die "'docker compose' (v2 plagin) topilmadi"
  docker info >/dev/null 2>&1 || die "Docker daemon'ga ulanib bo'lmadi (sudo? yoki: sudo usermod -aG docker \$USER && qayta login)"
  [ -f "$ENV_FILE" ] || die "$ENV_FILE yo'q — avval: deploy/scripts/init-env.sh"
}

# Servis konteyneri ishlayaptimi (running).
svc_running() { [ -n "$("${COMPOSE[@]}" ps --status running -q "$1" 2>/dev/null)" ]; }

# Loyihaning nomlangan volume'i (label bo'yicha; topilmasa <loyiha>_<nom>). Mavjud bo'lmasa bo'sh.
project_volume() {
  local v
  v="$(docker volume ls -q --filter "label=com.docker.compose.project=$PROJECT" \
        --filter "label=com.docker.compose.volume=$1" | head -n 1)"
  if [ -z "$v" ] && docker volume inspect "${PROJECT}_$1" >/dev/null 2>&1; then v="${PROJECT}_$1"; fi
  printf '%s' "$v"
}

# Hostdagi zaxira papkasi (BACKUP_DIR — deploy/ ga nisbatan nisbiy yoki absolyut).
backup_dir() {
  local d
  d="$(env_get BACKUP_DIR)"; d="${d:-./backups}"
  case "$d" in /*) ;; *) d="$DEPLOY_DIR/${d#./}" ;; esac
  printf '%s' "$d"
}

# Zaxira: backup servisi ishlasa — uning skripti (baza + fayllar), aks holda postgres'dan to'g'ridan-to'g'ri pg_dump.
# $1 — fayl nomi qo'shimchasi (masalan predeploy). Muvaffaqiyatsiz bo'lsa 1 qaytaradi.
backup_now() {
  local tag="$1" dir ts out
  if svc_running backup && "${COMPOSE[@]}" exec -T backup /scripts/backup.sh once; then
    info "zaxira olindi (backup servisi) → $(backup_dir)"
    return 0
  fi
  svc_running postgres || { warn "postgres ishlamayapti — zaxira olib bo'lmadi"; return 1; }
  dir="$(backup_dir)/db"; ts="$(date -u +%Y%m%d-%H%M%S)"
  out="$dir/amaliyotchi-$ts-$tag.dump"
  mkdir -p "$dir" || return 1
  info "pg_dump (postgres konteyneridan) → $out"
  if ( umask 077; "${COMPOSE[@]}" exec -T postgres pg_dump -U amaliyotchi -d amaliyotchi --format=custom --compress=6 >"$out.partial" ) \
     && [ -s "$out.partial" ]; then
    mv "$out.partial" "$out"
    info "zaxira olindi: $out ($(du -h "$out" | cut -f1))"
    return 0
  fi
  rm -f "$out.partial"
  warn "pg_dump muvaffaqiyatsiz"
  return 1
}

# Postgres'ni ko'tarib healthy bo'lguncha kutadi.
start_postgres() {
  "${COMPOSE[@]}" up -d --no-deps postgres || return 1
  wait_healthy 120 postgres
}

# .env dagi POSTGRES_PASSWORD bazadagi bilan mosligini tekshiradi; mos bo'lmasa unix socket orqali (konteyner ichida
# `local trust`) ALTER USER bilan bazani .env ga moslaydi. Parol psql o'zgaruvchisi (:'pw') sifatida uzatiladi —
# SQL'ga matn sifatida qo'shilmaydi.
sync_pg_password() {
  local pw
  pw="$(env_get POSTGRES_PASSWORD)"
  [ -n "$pw" ] || die "POSTGRES_PASSWORD bo'sh"
  [[ "$pw" =~ ^[A-Za-z0-9._~+/=-]+$ ]] || die "POSTGRES_PASSWORD da ruxsat etilmagan belgi bor (faqat hex/base64): deploy/scripts/init-env.sh"
  # Tarmoq orqali (servis nomi → konteyner IP) — API aynan shunday ulanadi (scram-sha-256). 127.0.0.1 emas:
  # postgres image'da localhost ham `trust`, u parolni tekshirmaydi.
  if "${COMPOSE[@]}" exec -T -e PGPASSWORD="$pw" postgres \
       psql -h postgres -U amaliyotchi -d amaliyotchi -w -tAc 'select 1' >/dev/null 2>&1; then
    info "postgres paroli .env bilan mos"
    return 0
  fi
  warn "bazadagi postgres paroli .env dagidan farq qiladi — ALTER USER bilan yangilanmoqda"
  printf '%s\n' '\getenv pw NEW_PG_PASSWORD' "ALTER USER amaliyotchi PASSWORD :'pw';" |
    "${COMPOSE[@]}" exec -T -e NEW_PG_PASSWORD="$pw" postgres \
      psql -U amaliyotchi -d postgres -v ON_ERROR_STOP=1 -q >/dev/null \
    || die "ALTER USER muvaffaqiyatsiz"
  "${COMPOSE[@]}" exec -T -e PGPASSWORD="$pw" postgres \
    psql -h postgres -U amaliyotchi -d amaliyotchi -w -tAc 'select 1' >/dev/null \
    || die "parol yangilandi, lekin tarmoq orqali ulanish baribir muvaffaqiyatsiz"
  info "postgres paroli yangilandi (.env dagi qiymat)"
}

# Muammoli servis(lar) holati va oxirgi 50 qator log'i.
show_failure() {
  local s
  "${COMPOSE[@]}" ps -a >&2 || true
  for s in "$@"; do
    echo >&2
    echo "------ $s: oxirgi 50 qator log ------" >&2
    "${COMPOSE[@]}" logs --no-color --tail 50 "$s" >&2 || true
  done
}

# Servislar (berilmasa — compose'dagi barcha faol servislar) healthy bo'lishini kutadi.
# $1 — timeout (soniya). Xato/timeout bo'lsa holat va log chiqariladi, 1 qaytaradi.
wait_healthy() {
  local timeout="$1"; shift
  local services=("$@") deadline s cid state health restarts pending failed
  if [ ${#services[@]} -eq 0 ]; then
    # shellcheck disable=SC2207
    services=($("${COMPOSE[@]}" config --services))
  fi
  deadline=$((SECONDS + timeout))
  while :; do
    pending=(); failed=()
    for s in "${services[@]}"; do
      cid="$("${COMPOSE[@]}" ps -a -q "$s" 2>/dev/null | head -n 1)"
      if [ -z "$cid" ]; then pending+=("$s(konteyner yo'q)"); continue; fi
      read -r state health restarts < <(docker inspect -f \
        '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}} {{.RestartCount}}' "$cid")
      case "$state/$health" in
        running/healthy|running/none) ;;
        running/starting)
          [[ "$RUNNING_ONLY_SERVICES" == *" $s "* ]] || pending+=("$s(starting)") ;;
        */unhealthy|exited/*|dead/*) failed+=("$s") ;;
        restarting/*)
          if [ "${restarts:-0}" -ge 3 ]; then failed+=("$s"); else pending+=("$s(restarting)"); fi ;;
        *) pending+=("$s($state)") ;;
      esac
      # Qayta-qayta qulab turgan (crash loop) konteyner — kutishning ma'nosi yo'q.
      if [ "${restarts:-0}" -ge 3 ] && [ "$health" != healthy ] && [[ " ${failed[*]} " != *" $s "* ]]; then
        failed+=("$s")
      fi
    done
    if [ ${#failed[@]} -gt 0 ]; then
      echo "  [XATO] ishga tushmadi: ${failed[*]}" >&2
      show_failure "${failed[@]}"
      return 1
    fi
    if [ ${#pending[@]} -eq 0 ]; then
      info "healthy: ${services[*]}"
      return 0
    fi
    if [ "$SECONDS" -ge "$deadline" ]; then
      echo "  [XATO] ${timeout}s ichida healthy bo'lmadi: ${pending[*]}" >&2
      local names=()
      for s in "${pending[@]}"; do names+=("${s%%(*}"); done
      show_failure "${names[@]}"
      return 1
    fi
    printf '    kutilmoqda: %s\r' "${pending[*]}"
    sleep 5
  done
}

# `docker compose up` ni watchdog bilan: depends_on (service_healthy) qulab turgan servisda cheksiz kutib qolmasin.
compose_up_bounded() {
  local timeout="$1"; shift
  "${COMPOSE[@]}" up "$@" &
  local pid=$! deadline=$((SECONDS + timeout))
  while kill -0 "$pid" 2>/dev/null; do
    if [ "$SECONDS" -ge "$deadline" ]; then
      kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null
      echo "  [XATO] 'docker compose up' ${timeout}s ichida tugamadi" >&2
      return 1
    fi
    sleep 2
  done
  wait "$pid"
}
