#!/usr/bin/env bash
# Makes the store screenshots from the emulator shots in design/app-shots-v38/final (1..7, 1280x2856, Pixel 9 Pro, app version 38):
# cuts the Android status bar (top) and the gesture bar (bottom), scales the picture to fit a canvas and fills the free strip by
# stretching the edge row / column of the picture (the header stays orange, the page stays its own colour). RGB, no alpha.
#   - App Store: AppStore/screenshots/<device>/NN-name.png (4 sizes)
#   - Google Play phone (9:16, 1080x1920): PlayStore/screenshots-ru/N-name.png
# Run in Git Bash from the repo root:  bash AppStore/make-screenshots.sh
set -e
SRC=design/app-shots-v38/final
TOP=130; BOT=56                       # px cut at the top / bottom of the 1280x2856 shot
CW=1280; CH=$((2856 - TOP - BOT))     # 1280 x 2670 after the cut
NAMES=(1-home:01-home 2-book:02-book 3-topic:03-topic 4-game:04-game 5-phrase:05-phrase 6-awards:06-awards 7-dictionary:07-dictionary)

# render <in> <out> <W> <H>
render() {
  local in=$1 out=$2 W=$3 H=$4 SW SH PX PXR PY vf
  if [ $(( CH * W / CW )) -le "$H" ]; then SW=$W; SH=$(( CH * W / CW )); else SH=$H; SW=$(( CW * H / CH )); fi
  PX=$(( (W - SW) / 2 )); PXR=$(( W - SW - PX )); PY=$(( H - SH ))
  if [ "$PY" -gt 0 ]; then
    vf="[0:v]crop=$CW:$CH:0:$TOP,scale=$SW:$SH:flags=lanczos,split[a][b];[b]crop=iw:1:0:0,scale=iw:$PY[t];[t][a]vstack[c]"
  else
    vf="[0:v]crop=$CW:$CH:0:$TOP,scale=$SW:$SH:flags=lanczos[c]"
  fi
  if [ "$PX" -gt 0 ]; then
    vf="$vf;[c]split=3[m][l][r];[l]crop=1:ih:0:0,scale=$PX:ih[lf];[r]crop=1:ih:iw-1:0,scale=$PXR:ih[rf];[lf][m][rf]hstack=3[d]"
  else
    vf="$vf;[c]null[d]"
  fi
  ffmpeg -v error -y -i "$in" -filter_complex "$vf;[d]format=rgb24[o]" -map "[o]" -frames:v 1 "$out"
}

for set in iphone-6.7:1290:2796 iphone-6.5:1284:2778 iphone-5.5:1242:2208 ipad-12.9:2048:2732; do
  IFS=: read dev W H <<<"$set"
  dir="AppStore/screenshots/$dev-${W}x${H}"; mkdir -p "$dir"
  for pair in "${NAMES[@]}"; do IFS=: read f n <<<"$pair"; render "$SRC/$f.png" "$dir/$n.png" "$W" "$H"; done
  echo "App Store $dev ${W}x${H}"
done

mkdir -p PlayStore/screenshots-ru
for pair in "${NAMES[@]}"; do IFS=: read f n <<<"$pair"; render "$SRC/$f.png" "PlayStore/screenshots-ru/${n#0}.png" 1080 1920; done
echo "Google Play phone 1080x1920"
