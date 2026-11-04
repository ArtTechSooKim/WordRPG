"""광고용 모험 장면 묶기 (#158): AdCapture.Adventure가 찍은 프레임 → 장면별 영상 + zip (Higgsfield 샌드박스로 옮기는 용도).

    python Tools/ad_motion/pack_adventure.py   (pip install imageio-ffmpeg)

Builds/ad/adventure/{장면}/f0000.jpg → Builds/ad/adv_{장면}.mp4, Builds/ad/adventure_kit.zip
장면마다 쓰는 프레임 범위는 아래 표 (30fps, 음악 한 박자 = 15프레임). 다시 찍으면 marks.txt를 보고 맞출 것
"""
import glob
import os
import subprocess
import sys
import zipfile

import imageio_ffmpeg

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Builds", "ad", "adventure")
OUT = os.path.join(ROOT, "Builds", "ad")
FPS = 30

# 장면: (시작 프레임, 프레임 수) — 여유로 1장 더
CLIPS = {
    "walk": (25, 46),     # 아라와 카메라 쪽으로 걸어오며 얕은 강을 건넘
    "raft": (10, 46),     # 나루터까지 걸어가 뗏목을 타고 큰 강으로
    "cave": (8, 31),      # 어두운 동굴 (주인공 빛)
    "preempt": (24, 46),  # [선제공격] → 단어 문제 → 성공 도장 → 전투로 번쩍
}

names = []
for name, (start, count) in CLIPS.items():
    frames = sorted(glob.glob(os.path.join(SRC, name, "f*.jpg")))[start:start + count]
    assert len(frames) == count, (name, len(frames))
    listing = os.path.join(OUT, f"{name}_list.txt")
    with open(listing, "w", encoding="utf-8") as f:
        for path in frames:
            f.write(f"file '{path.replace(os.sep, '/')}'\nduration {1 / FPS:.6f}\n")
        f.write(f"file '{frames[-1].replace(os.sep, '/')}'\n")
    mp4 = os.path.join(OUT, f"adv_{name}.mp4")
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-nostdin", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", listing,
                    "-r", str(FPS), "-c:v", "libx264", "-crf", "17", "-preset", "medium", "-pix_fmt", "yuv420p", mp4], check=True)
    os.remove(listing)
    names.append(mp4)
    print(name, count, "frames", os.path.getsize(mp4) // 1024, "KB")

with zipfile.ZipFile(os.path.join(OUT, "adventure_kit.zip"), "w", zipfile.ZIP_STORED) as z:
    for path in names:
        z.write(path, os.path.basename(path))
print("zip", os.path.getsize(os.path.join(OUT, "adventure_kit.zip")) // 1024, "KB")
