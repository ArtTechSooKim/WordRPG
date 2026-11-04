"""광고용 기술 연출 묶기 (#155): AdCapture가 찍은 프레임 → 영상 한 편 + 기술별 시작 시간표.

    python Tools/ad_motion/pack_skill_clips.py   (pip install imageio-ffmpeg)

Builds/ad/skills/{번호}_{기술 id}/f0000.jpg + sfx.txt → Builds/ad/skills_reel.mp4 + skills_reel.json
시간표 한 줄 = id · 영상 안 시작 프레임 · 프레임 수 · 효과음(기술 안의 프레임, 소리 이름)
프레임은 약 19장/초로 찍혔으므로 30fps로 틀면 살짝 느린 화면이 된다 (빠르게 넘기는 구간에서 움직임이 잘 보임)
"""
import glob
import json
import os
import subprocess
import sys

import imageio_ffmpeg

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Builds", "ad", "skills")
OUT = os.path.join(ROOT, "Builds", "ad")
FPS = 30

sfx = {}
with open(os.path.join(SRC, "sfx.txt"), encoding="utf-8") as f:
    for line in f:
        clip, frame, name = line.rstrip("\n").split("\t")
        sfx.setdefault(clip, []).append([int(frame), name])

table, listing, cursor = [], [], 0
for clip in sorted(d for d in os.listdir(SRC) if os.path.isdir(os.path.join(SRC, d))):
    frames = sorted(glob.glob(os.path.join(SRC, clip, "f*.jpg")))
    table.append({"clip": clip, "id": clip[3:], "start": cursor, "frames": len(frames), "sfx": sfx.get(clip, [])})
    for path in frames:
        listing.append(f"file '{path.replace(os.sep, '/')}'\nduration {1 / FPS:.6f}\n")
    cursor += len(frames)
listing.append(f"file '{frames[-1].replace(os.sep, '/')}'\n")

list_path = os.path.join(OUT, "skills_list.txt")
with open(list_path, "w", encoding="utf-8") as f:
    f.writelines(listing)
mp4 = os.path.join(OUT, "skills_reel.mp4")
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", list_path,
                "-r", str(FPS), "-c:v", "libx264", "-crf", "17", "-preset", "medium", "-pix_fmt", "yuv420p", mp4], check=True)
os.remove(list_path)
with open(os.path.join(OUT, "skills_reel.json"), "w", encoding="utf-8") as f:
    json.dump(table, f, ensure_ascii=False, indent=1)
print(len(table), "clips", cursor, "frames", os.path.getsize(mp4) // (1024 * 1024), "MB")
