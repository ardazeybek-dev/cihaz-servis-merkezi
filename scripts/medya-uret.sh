#!/usr/bin/env bash
# Demo eğitim videolarını üretir: telif sorunu olmayan sentetik görüntü, 360p + 720p HLS, WebVTT altyazı.
# Kullanım: scripts/medya-uret.sh [çıktı klasörü]   (varsayılan: media)
# Gerekenler: ffmpeg (libx264 ile). Windows'ta Git Bash'ten çalıştırılabilir.
set -euo pipefail
cd "$(dirname "$0")/.."

CIKTI="${1:-media}"
SURE="${SURE:-40}"
FONT="src/CihazServis.Infrastructure/Fonts/NotoSans-Bold.ttf"
BASLIKLAR=("Cihazı tanıyın" "Günlük kullanım" "Temizlik ve bakım")
TONLAR=(0 140 260)

command -v ffmpeg >/dev/null || { echo "ffmpeg bulunamadı" >&2; exit 1; }

for i in 1 2 3; do
  hedef="$CIKTI/ders-$i"
  baslik="${BASLIKLAR[$((i-1))]}"
  mkdir -p "$hedef/360p" "$hedef/720p"
  metin="$hedef/.baslik.txt"
  printf 'Ders %s · %s\nÖrnek eğitim videosu' "$i" "$baslik" > "$metin"

  ffmpeg -hide_banner -loglevel error -y \
    -f lavfi -i "testsrc2=size=1280x720:rate=30:duration=$SURE" \
    -f lavfi -i "anullsrc=channel_layout=stereo:sample_rate=48000" -t "$SURE" \
    -filter_complex "[0:v]hue=h=${TONLAR[$((i-1))]},drawbox=x=0:y=ih-190:w=iw:h=190:color=black@0.65:t=fill,drawtext=fontfile=$FONT:textfile=$metin:fontsize=44:fontcolor=white:x=56:y=h-160:line_spacing=18,split=2[a][b];[a]scale=640:360[v360];[b]null[v720]" \
    -map "[v360]" -map 1:a -map "[v720]" -map 1:a \
    -c:v libx264 -preset veryfast -profile:v main -pix_fmt yuv420p -g 60 -keyint_min 60 -sc_threshold 0 \
    -c:a aac -b:a 64k \
    -b:v:0 700k -maxrate:v:0 800k -bufsize:v:0 1400k \
    -b:v:1 2000k -maxrate:v:1 2200k -bufsize:v:1 4000k \
    -f hls -hls_time 4 -hls_playlist_type vod -hls_flags independent_segments \
    -hls_segment_filename "$hedef/%v/seg_%03d.ts" \
    -master_pl_name master.m3u8 \
    -var_stream_map "v:0,a:0,name:360p v:1,a:1,name:720p" \
    "$hedef/%v/index.m3u8"
  rm -f "$metin"

  cat > "$hedef/altyazi.vtt" <<VTT
WEBVTT

00:00:00.500 --> 00:00:08.000
Ders $i. $baslik.

00:00:08.000 --> 00:00:20.000
Bu, örnek uygulama için üretilmiş bir eğitim videosudur.

00:00:20.000 --> 00:00:32.000
Video ileri sarılamaz, izlenen süre sunucuda ölçülür.

00:00:32.000 --> 00:00:${SURE}.000
Video tamamlanınca dersin soruları açılır.
VTT
  echo "hazır: $hedef"
done
