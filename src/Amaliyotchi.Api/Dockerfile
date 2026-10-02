# syntax=docker/dockerfile:1
# Amaliyotchi API — multi-stage build. Context: loyiha ildizi (../.. shu fayldan), qarang deploy/docker-compose.yml.
#   docker build -f src/Amaliyotchi.Api/Dockerfile -t amaliyotchi-api .

# ---------- 1. Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Restore alohida layer: faqat props/csproj o'zgarganda qayta ishlaydi (tests/ va Worker kerak emas —
# Api → Infrastructure → Application → Domain; Worker'ga bog'liqlik yo'q).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Amaliyotchi.Domain/Amaliyotchi.Domain.csproj                 src/Amaliyotchi.Domain/
COPY src/Amaliyotchi.Application/Amaliyotchi.Application.csproj       src/Amaliyotchi.Application/
COPY src/Amaliyotchi.Infrastructure/Amaliyotchi.Infrastructure.csproj src/Amaliyotchi.Infrastructure/
COPY src/Amaliyotchi.Api/Amaliyotchi.Api.csproj                       src/Amaliyotchi.Api/
# NuGet paketlari layer ichida qoladi (cache mount EMAS): aks holda restore layer'i keshdan olinib, mount
# tozalangan bo'lsa `publish --no-restore` paketlarni topmay yiqiladi.
RUN dotnet restore src/Amaliyotchi.Api

COPY src/ src/
# UseAppHost=false — native launcher kerak emas (ENTRYPOINT `dotnet *.dll`), image kichikroq.
RUN dotnet publish src/Amaliyotchi.Api -c Release -o /app --no-restore -p:UseAppHost=false

# ---------- 2. Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl — faqat HEALTHCHECK uchun (aspnet image'da yo'q).
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Hostdagi fayl rejimi (masalan 0600) COPY da saqlanadi — non-root "app" o'qiy olishi uchun a+rX.
# Storage:RootPath=/app/data/files — volume; "app" (image'da tayyor) yoza olishi uchun egalik.
RUN chmod -R a+rX /app \
 && mkdir -p /app/data && chown -R app:app /app/data
VOLUME ["/app/data"]
USER app

# DOTNET_EnableDiagnostics=0 — debugger/profiler/EventPipe IPC socket'lari ochilmaydi (attack surface).
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_NOLOGO=1

EXPOSE 8080

# start-period: birinchi startda migratsiya + seed (demo bilan ~30-60 s) tugashini kutamiz.
HEALTHCHECK --interval=15s --timeout=5s --start-period=90s --retries=5 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Amaliyotchi.Api.dll"]
