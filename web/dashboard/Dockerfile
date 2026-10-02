# syntax=docker/dockerfile:1.7
# Amaliyotchi — dashboard (admin + tyutor SPA).
# Build context: web/  →  docker build -f dashboard/Dockerfile -t amaliyotchi-dashboard .
#
# Build-arg'lar (VITE_* faqat build vaqtida bundle'ga kiradi):
#   VITE_API_URL   — bo'sh (default) → client relative `/api` ishlatadi, nginx uni `api:8080` ga proxy qiladi.
#                    To'ldirilsa (https://api.example.uz) — brauzer to'g'ridan-to'g'ri o'sha manzilga boradi (CORS kerak).
#   VITE_TWA_URL   — talaba ilovasining ommaviy manzili (parol oynasi va login'dagi havola); bo'sh → ko'rsatilmaydi.
#   VITE_USE_MOCKS — berilmaydi (MSW faqat dev).

# ---------- 1) build ----------
# Aniq versiya — takrorlanuvchan build; yangilash: Docker Hub `node` teglari (22 LTS).
FROM node:22.23.3-alpine3.24 AS build
WORKDIR /app
# NODE_ENV=development faqat npm ci (devDependencies) uchun; vite build quyida NODE_ENV=production bilan.
ENV CI=true \
    NODE_ENV=development

# Layer cache: avval faqat manifestlar → npm ci (workspaces: shared, dashboard, twa).
COPY package.json package-lock.json .npmrc ./
COPY shared/package.json shared/
COPY dashboard/package.json dashboard/
COPY twa/package.json twa/
RUN --mount=type=cache,target=/root/.npm \
    npm ci --no-audit --no-fund \
      --fetch-retries=5 --fetch-retry-mintimeout=20000 --fetch-retry-maxtimeout=120000 --fetch-timeout=600000

# Manba (shared TS manba sifatida import qilinadi — alohida build shart emas).
COPY shared/ shared/
COPY dashboard/ dashboard/

ARG VITE_API_URL=""
ARG VITE_TWA_URL=""
ENV VITE_API_URL=$VITE_API_URL \
    VITE_TWA_URL=$VITE_TWA_URL \
    VITE_USE_MOCKS=false

RUN NODE_ENV=production npm run build -w dashboard \
    # Source map va MSW service worker prod image'ga kirmaydi.
    && find dashboard/dist -name '*.map' -delete \
    && rm -f dashboard/dist/mockServiceWorker.js

# ---------- 2) runtime ----------
# nginx stable (1.30.x) — aniq patch; yangilash: Docker Hub `nginx` `stable-alpine` tegi qaysi versiyaga ishora qilsa.
FROM nginx:1.30.5-alpine3.24 AS runtime
RUN rm -f /etc/nginx/conf.d/default.conf
# Konfiguratsiya shablon sifatida: image entrypoint'i (20-envsubst-on-templates.sh) start'da
# /etc/nginx/templates/*.template → /etc/nginx/conf.d/ ga render qiladi. Filtr — faqat NGINX_REAL_IP_FROM
# almashtiriladi ($host, $remote_addr kabi nginx o'zgaruvchilari tegilmaydi).
#   NGINX_REAL_IP_FROM — CF-Connecting-IP / X-Forwarded-Proto ga ishoniladigan manba (cloudflared turgan
#   compose tarmog'i subnet'i). compose'da DOCKER_SUBNET bilan bir xil berilishi kerak.
ENV NGINX_ENVSUBST_FILTER="^NGINX_REAL_IP_FROM" \
    NGINX_REAL_IP_FROM=172.30.0.0/16
COPY dashboard/nginx.conf /etc/nginx/templates/dashboard.conf.template
COPY --from=build /app/dashboard/dist /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1/healthz >/dev/null || exit 1
