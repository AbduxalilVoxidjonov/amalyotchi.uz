#!/usr/bin/env bash
# "Yuzni tasdiqlash" ONNX modellarini yuklaydi (repoda saqlanmaydi): OpenCV Zoo YuNet (MIT) va SFace (Apache-2.0).
#   deploy/scripts/fetch-face-models.sh [papka]     (default: <repo>/.models/face — appsettings.Development.json shu yerni ko'radi)
# URL'lar opencv_zoo'ning QAT'IY commit'iga bog'langan va SHA256 tekshiriladi; fayl allaqachon to'g'ri bo'lsa qayta yuklanmaydi.
# Docker image ham shu skriptni ishlatadi (src/Amaliyotchi.Api/Dockerfile → /app/models/face).
# Eslatma: InsightFace (buffalo_l/ArcFace) modellari ataylab ishlatilmaydi — litsenziyasi notijorat.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEST="${1:-$ROOT_DIR/.models/face}"
ZOO_COMMIT="47534e27c9851bb1128ccc0102f1145e27f23f98"
ZOO="https://github.com/opencv/opencv_zoo/raw/${ZOO_COMMIT}/models"

# fayl nomi | URL | SHA256
MODELS=(
  "face_detection_yunet_2023mar.onnx|${ZOO}/face_detection_yunet/face_detection_yunet_2023mar.onnx|8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4"
  "face_recognition_sface_2021dec.onnx|${ZOO}/face_recognition_sface/face_recognition_sface_2021dec.onnx|0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79"
)

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1
  else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

mkdir -p "$DEST"
for entry in "${MODELS[@]}"; do
  IFS='|' read -r name url expected <<<"$entry"
  target="$DEST/$name"
  if [ -f "$target" ] && [ "$(sha256 "$target")" = "$expected" ]; then
    echo "  [OK]    $name (mavjud)"
    continue
  fi
  echo "  [YUKLASH] $name"
  tmp="$target.part"
  curl -fsSL --retry 5 --retry-delay 3 --connect-timeout 20 -o "$tmp" "$url"
  actual="$(sha256 "$tmp")"
  if [ "$actual" != "$expected" ]; then
    rm -f "$tmp"
    echo "  [XATO]  $name: SHA256 mos emas (kutilgan $expected, keldi $actual)" >&2
    exit 1
  fi
  mv "$tmp" "$target"
  echo "  [OK]    $name"
done
echo "Yuz modellari tayyor: $DEST"
