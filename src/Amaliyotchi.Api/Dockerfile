# syntax=docker/dockerfile:1
# Amaliyotchi API — multi-stage build. Context: loyiha ildizi (../.. shu fayldan), qarang deploy/docker-compose.yml.
#   docker build -f src/Amaliyotchi.Api/Dockerfile -t amaliyotchi-api .

# ---------- 1. Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore alohida layer: faqat solution/props/csproj o'zgarganda qayta ishlaydi (tests/ kerak emas).
COPY Amaliyotchi.slnx Directory.Build.props Directory.Packages.props ./
COPY src/Amaliyotchi.Domain/Amaliyotchi.Domain.csproj                 src/Amaliyotchi.Domain/
COPY src/Amaliyotchi.Application/Amaliyotchi.Application.csproj       src/Amaliyotchi.Application/
COPY src/Amaliyotchi.Infrastructure/Amaliyotchi.Infrastructure.csproj src/Amaliyotchi.Infrastructure/
COPY src/Amaliyotchi.Api/Amaliyotchi.Api.csproj                       src/Amaliyotchi.Api/
COPY src/Amaliyotchi.Worker/Amaliyotchi.Worker.csproj                 src/Amaliyotchi.Worker/
RUN dotnet restore src/Amaliyotchi.Api

COPY src/ src/
RUN dotnet publish src/Amaliyotchi.Api -c Release -o /app --no-restore

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

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# start-period: birinchi startda migratsiya + seed (demo bilan ~30-60 s) tugashini kutamiz.
HEALTHCHECK --interval=15s --timeout=5s --start-period=90s --retries=5 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Amaliyotchi.Api.dll"]
