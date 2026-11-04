#!/bin/bash
# 광고 4분할 칸 영상 자르기 (#157): Higgsfield 샌드박스의 proj/assets에서 실행 (skills_reel.mp4가 있는 곳)
# 렌더러는 같은 영상을 동시에 여러 지점에서 틀면 칸들에 같은 프레임을 보여 주므로, 칸마다 파일을 따로 만든다.
# 시작 프레임 = edit.jsx의 START[기술] + GRIDS의 시작 프레임 / 31장 = 두 박자(0.998초) + 여유
while read -r clip start; do
  ffmpeg -nostdin -y -v error -i skills_reel.mp4 -vf "trim=start_frame=${start}:end_frame=$((start + 31)),setpts=PTS-STARTPTS" \
    -an -c:v libx264 -crf 16 -pix_fmt yuv420p -r 30 "cell_${clip}.mp4"
done <<'TABLE'
quill_ink_storm 137
gem_letter_shower 170
whip_thorn_storm 249
ara_pebble 69
doc_highlight_sweep 214
lamp_firefly_swarm 292
doc_finish_sentence 31
wand_thunder_spell 99
TABLE
