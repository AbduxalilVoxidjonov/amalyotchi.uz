#!/usr/bin/env bash
# Amaliyotchi — production'ga chiqarishdan oldingi tekshiruv.
#   deploy/scripts/preflight.sh [yo'l/.env]      (default: deploy/.env)
# Tekshiradi: majburiy qiymatlar, JWT ≥ 32 belgi, default/demo parollar yo'qligi, URL'lar HTTPS,
# SEED_DEMO o'chiq, qat'iy IP'lar DOCKER_SUBNET ichida, .env ruxsatlari, `docker compose config`, disk joyi.
# Xato bo'lsa exit 1.
set -uo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${1:-$DEPLOY_DIR/.env}"
errors=0; warnings=0
err()  { echo "  [XATO]  $*"; errors=$((errors + 1)); }
warn() { echo "  [OGOH]  $*"; warnings=$((warnings + 1)); }
ok()   { echo "  [OK]    $*"; }

[ -f "$ENV_FILE" ] || { echo "Topilmadi: $ENV_FILE (cp deploy/.env.example deploy/.env)"; exit 1; }

# .env dan qiymat o'qish (oxirgi uchragani; atrofidagi qo'shtirnoqlar olib tashlanadi). Faylni `source` qilmaydi.
get() {
  local line
  line="$(grep -E "^[[:space:]]*$1=" "$ENV_FILE" | tail -n 1)" || true
  line="${line#*=}"
  line="${line%$'\r'}"
  line="${line#\"}"; line="${line%\"}"; line="${line#\'}"; line="${line%\'}"
  printf '%s' "$line"
}

echo "== $ENV_FILE"

for v in JWT_SIGNING_KEY POSTGRES_PASSWORD ADMIN_HEMIS_ID ADMIN_PASSWORD SEQ_ADMIN_PASSWORD \
         CLOUDFLARE_TUNNEL_TOKEN DASHBOARD_PUBLIC_URL TWA_PUBLIC_URL; do
  if [ -z "$(get "$v")" ]; then err "$v bo'sh (majburiy)"; else ok "$v berilgan"; fi
done

jwt="$(get JWT_SIGNING_KEY)"
[ -n "$jwt" ] && [ "${#jwt}" -lt 32 ] && err "JWT_SIGNING_KEY ${#jwt} belgi — kamida 32 kerak (openssl rand -base64 48)"

pg="$(get POSTGRES_PASSWORD)"
case "$pg" in
  amaliyotchi|postgres|password|CHANGE_ME) err "POSTGRES_PASSWORD default/demo qiymat ('$pg')" ;;
esac
[ -n "$pg" ] && [ "${#pg}" -lt 16 ] && warn "POSTGRES_PASSWORD qisqa (${#pg} belgi, ≥ 16 tavsiya)"
[[ "$pg" == *";"* ]] && err "POSTGRES_PASSWORD da ';' bor — API ulanish satrini buzadi"

adm="$(get ADMIN_PASSWORD)"
case "$adm" in
  admin12345|admin|password|12345678|tutor12345|talaba12345) err "ADMIN_PASSWORD demo qiymat ('$adm')" ;;
esac
[ -n "$adm" ] && [ "${#adm}" -lt 12 ] && err "ADMIN_PASSWORD qisqa (${#adm} belgi) — API Production'da kamida 12 talab qiladi, ishga tushmaydi"

hemis="$(get ADMIN_HEMIS_ID)"
[ -n "$hemis" ] && ! [[ "$hemis" =~ ^[0-9]+$ ]] && err "ADMIN_HEMIS_ID faqat raqamlardan iborat bo'lishi kerak"
[ "$hemis" = "100000000001" ] && warn "ADMIN_HEMIS_ID demo qiymat (100000000001) — o'zingiznikini bering"

seqpw="$(get SEQ_ADMIN_PASSWORD)"
[ -n "$seqpw" ] && [ "${#seqpw}" -lt 12 ] && warn "SEQ_ADMIN_PASSWORD qisqa (${#seqpw} belgi)"

for v in DASHBOARD_PUBLIC_URL TWA_PUBLIC_URL; do
  u="$(get "$v")"
  [ -z "$u" ] && continue
  [[ "$u" == https://* ]] || err "$v HTTPS bo'lishi kerak ('$u')"
  [[ "$u" == */ ]] && warn "$v oxiridagi '/' ni olib tashlang"
  [[ "$u" == *127.0.0.1* || "$u" == *localhost* ]] && err "$v lokal manzil ('$u')"
done

demo="$(get SEED_DEMO)"
[ "$demo" = "true" ] && err "SEED_DEMO=true — production'da demo ma'lumot yuklanadi"

[ -z "$(get TELEGRAM_BOT_TOKEN)" ] && err "TELEGRAM_BOT_TOKEN bo'sh — API Production'da ishga tushmaydi"

ah="$(get API_ALLOWED_HOSTS)"
if [ -z "$ah" ]; then warn "API_ALLOWED_HOSTS bo'sh — API istalgan Host'ni qabul qiladi ('*')"
elif [[ ";$ah;" != *";localhost;"* ]]; then err "API_ALLOWED_HOSTS da 'localhost' yo'q — healthcheck 400 oladi, API unhealthy bo'ladi"; fi

# Qat'iy IP'lar (cloudflared, dashboard, twa) DOCKER_SUBNET ichida, bir-biridan farqli va network/broadcast/gateway emas.
ip2int() {
  local a b c d
  IFS=. read -r a b c d <<<"$1"
  for o in "$a" "$b" "$c" "$d"; do [[ "$o" =~ ^[0-9]+$ ]] && [ "$o" -le 255 ] || return 1; done
  echo $(( (a << 24) | (b << 16) | (c << 8) | d ))
}
subnet="$(get DOCKER_SUBNET)"; subnet="${subnet:-172.30.0.0/16}"
net_ip="${subnet%/*}"; prefix="${subnet#*/}"
if ! net_int="$(ip2int "$net_ip")" || ! [[ "$prefix" =~ ^[0-9]+$ ]] || [ "$prefix" -lt 8 ] || [ "$prefix" -gt 29 ]; then
  err "DOCKER_SUBNET noto'g'ri ('$subnet') — masalan 172.30.0.0/16"
else
  mask=$(( (0xFFFFFFFF << (32 - prefix)) & 0xFFFFFFFF ))
  first=$(( net_int & mask )); last=$(( first | (~mask & 0xFFFFFFFF) ))
  seen=" "
  for pair in CLOUDFLARED_IP:172.30.0.250 DASHBOARD_IP:172.30.0.248 TWA_IP:172.30.0.249; do
    v="${pair%%:*}"; ip="$(get "$v")"; ip="${ip:-${pair#*:}}"
    if ! ip_int="$(ip2int "$ip")"; then err "$v noto'g'ri IPv4 ('$ip')"; continue; fi
    if [ $(( ip_int & mask )) -ne "$first" ]; then
      err "$v=$ip DOCKER_SUBNET ($subnet) ichida emas — compose ishga tushmaydi yoki proksi ishonchi buziladi"
    elif [ "$ip_int" -le $(( first + 1 )) ] || [ "$ip_int" -ge "$last" ]; then
      err "$v=$ip — subnet manzili, gateway yoki broadcast bo'lishi mumkin emas"
    elif [[ "$seen" == *" $ip "* ]]; then
      err "$v=$ip boshqa servis IP si bilan bir xil"
    else
      ok "$v=$ip ($subnet ichida)"
    fi
    seen="$seen$ip "
  done
fi

if grep -Ev '^[[:space:]]*#' "$ENV_FILE" | grep -q '\$'; then
  warn ".env da '\$' belgisi bor — compose uni o'zgaruvchi sifatida almashtiradi (qiymatni tekshiring)"
fi

perm="$(stat -c '%a' "$ENV_FILE" 2>/dev/null || stat -f '%Lp' "$ENV_FILE")"
[ "$perm" != "600" ] && [ "$perm" != "400" ] && warn ".env ruxsati $perm — chmod 600 $ENV_FILE"

echo "== docker compose config"
if out="$(docker compose -f "$DEPLOY_DIR/docker-compose.yml" --env-file "$ENV_FILE" config -q 2>&1)"; then
  ok "compose konfiguratsiyasi to'g'ri"
else
  err "compose config: $out"
fi

echo "== server"
avail_kb="$(df -Pk "$DEPLOY_DIR" | awk 'NR==2 {print $4}')"
if [ -n "$avail_kb" ] && [ "$avail_kb" -lt $((5 * 1024 * 1024)) ]; then
  warn "diskda $((avail_kb / 1024)) MB bo'sh — kamida 5 GB tavsiya (image'lar + build kesh + zaxiralar)"
else
  ok "disk joyi yetarli"
fi

echo
echo "Natija: $errors xato, $warnings ogohlantirish"
[ "$errors" -eq 0 ]
