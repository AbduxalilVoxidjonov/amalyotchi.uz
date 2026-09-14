# syntax=docker/dockerfile:1.7
# Amaliyotchi — dashboard (admin + tyutor SPA).
# Build context: web/  →  docker build -f dashboard/Dockerfile -t amaliyotchi-dashboard .
#
# Build-arg'lar (VITE_* faqat build vaqtida bundle'ga kiradi):
#   VITE_API_URL   — bo'sh (default) → client relative `/api` ishlatadi, nginx uni `api:8080` ga proxy qiladi.
#                    To'ldirilsa (https://api.example.uz) — brauzer to'g'ridan-to'g'ri o'sha manzilga boradi (CORS kerak).
#   VITE_USE_MOCKS — berilmaydi (MSW faqat dev).

# ---------- 1) build ----------
FROM node:22-alpine AS build
WORKDIR /app
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
ENV VITE_API_URL=$VITE_API_URL \
    VITE_USE_MOCKS=false

RUN npm run build -w dashboard \
    # Source map va MSW service worker prod image'ga kirmaydi.
    && find dashboard/dist -name '*.map' -delete \
    && rm -f dashboard/dist/mockServiceWorker.js

# ---------- 2) runtime ----------
FROM nginx:1.27-alpine AS runtime
RUN rm -f /etc/nginx/conf.d/default.conf
COPY dashboard/nginx.conf /etc/nginx/conf.d/dashboard.conf
COPY --from=build /app/dashboard/dist /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1/healthz >/dev/null || exit 1
