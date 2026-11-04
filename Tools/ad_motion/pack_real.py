"""광고용 실제 시간 장면 묶기 (#159): AdCapture.RealClips가 찍은 프레임 → 찍힌 시각 그대로 30fps 영상 + zip.

    python Tools/ad_motion/pack_real.py   (pip install imageio-ffmpeg)

Builds/ad/real/{장면}/f0000.jpg + times.txt → Builds/ad/real_{장면}.mp4, Builds/ad/real_kit.zip
"""
import glob
import os
import subprocess
import sys
import zipfile

import imageio_ffmpeg

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Builds", "ad", "real")
OUT = os.path.join(ROOT, "Builds", "ad")

names = []
for name in sorted(d for d in os.listdir(SRC) if os.path.isdir(os.path.join(SRC, d))):
    frames = sorted(glob.glob(os.path.join(SRC, name, "f*.jpg")))
    with open(os.path.join(SRC, name, "times.txt"), encoding="utf-8") as f:
        times = [float(line) for line in f if line.strip()]
    assert len(times) == len(frames), (name, len(times), len(frames))
    listing = os.path.join(OUT, f"{name}_list.txt")
    with open(listing, "w", encoding="utf-8") as f:
        # 첫 프레임 앞의 시간까지 채워 시각이 marks.txt와 맞게
        f.write(f"file '{frames[0].replace(os.sep, '/')}'\nduration {max(times[0], 0.001):.4f}\n")
        for i, path in enumerate(frames):
            d = (times[i + 1] - times[i]) if i + 1 < len(times) else 1 / 30
            f.write(f"file '{path.replace(os.sep, '/')}'\nduration {max(d, 0.001):.4f}\n")
        f.write(f"file '{frames[-1].replace(os.sep, '/')}'\n")
    mp4 = os.path.join(OUT, f"real_{name}.mp4")
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-nostdin", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", listing,
                    "-vf", "fps=30", "-c:v", "libx264", "-crf", "17", "-preset", "medium", "-pix_fmt", "yuv420p", mp4], check=True)
    os.remove(listing)
    names.append(mp4)
    print(name, len(frames), "frames", f"{times[-1]:.2f}s", f"{len(frames) / max(times[-1], 0.01):.1f}fps", os.path.getsize(mp4) // 1024, "KB")

with zipfile.ZipFile(os.path.join(OUT, "real_kit.zip"), "w", zipfile.ZIP_STORED) as z:
    for path in names:
        z.write(path, os.path.basename(path))
print("zip", os.path.getsize(os.path.join(OUT, "real_kit.zip")) // 1024, "KB")
