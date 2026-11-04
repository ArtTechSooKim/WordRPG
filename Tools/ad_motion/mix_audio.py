"""광고 영상에 소리 넣기 (#155): 소리 없는 영상 + CC0 음악·효과음 → 완성 영상.

    python Tools/ad_motion/mix_audio.py 소리없는.mp4 완성.mp4   (pip install imageio-ffmpeg)

소리는 Assets/Resources/Audio의 팩 소리만 씀 (Licensed는 쓰지 않음). 시간표는 edit.jsx와 맞출 것.
"""
import os
import subprocess
import sys

import imageio_ffmpeg

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SFX = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx")
MUSIC = os.path.join(ROOT, "Assets", "Resources", "Audio", "Music", "battle.ogg")
LATE = 6.4  # 기술 나열 뒤 장면이 밀린 시간 (edit.jsx LATE)
T = 18.0 + LATE

# 앞 장면: 문제 도전 → 공격
EV = [("click", .05, .7), ("click", .28, .7), ("click", .6, .9), ("click", 1.27, .9), ("click", 1.94, .9),
      ("correct", 2.62, 1), ("critical", 2.62, .8), ("click", 3.85, .7),
      ("critical", 4.05, 1), ("hit", 4.05, .8), ("critical", 4.88, 1), ("critical", 5.28, 1)]
# 기술 나열: 칸마다 첫 효과음 (edit.jsx REEL과 같은 순서·길이)
REEL = [(.65, "hit"), (.62, "hit"), (.58, "hit"), (.55, "hit"), (.52, "hit"), (.5, "hit"),
        (.48, "heal"), (.46, "heal"), (.44, "heal"),
        (.42, "shield"), (.42, "shield"), (.42, "shield"), (.42, "shield")]
c = 5.62
for d, name in REEL:
    EV.append((name, c + 4 / 30, .9))
    c += d
EV += [("critical", c + 4 / 30, 1), ("hit", c + 4 / 30, 1)]  # 마지막 낙뢰
# 뒤 장면 (edit.jsx의 묶음 안 시간 + LATE)
EV += [("combo", LATE + t, .55 + i * .05) for i, t in enumerate([6.8, 7.0, 7.18, 7.34, 7.48, 7.6, 7.72])]
EV += [("critical", LATE + 8.12, 1), ("hit", LATE + 8.12, 1), ("click", LATE + 9.25, .7), ("click", LATE + 10.15, .7), ("click", LATE + 11.05, .7)]
EV += [("hit", LATE + t, .9) for t in (12.45, 12.75, 13.05, 13.35)]
EV += [("levelup", LATE + 14.75, 1), ("click", LATE + 15.6, .8)]

src, out = sys.argv[1], sys.argv[2]
args = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-v", "error", "-i", src, "-stream_loop", "-1", "-i", MUSIC]
fc = [f"[1:a]atrim=0:{T},afade=t=out:st={T - 1.3}:d=1.3,volume=0.42,aresample=44100,aformat=channel_layouts=stereo[m]"]
labels = ["[m]"]
for i, (name, at, vol) in enumerate(EV):
    args += ["-i", os.path.join(SFX, name + ".wav")]
    ms = int(at * 1000)
    fc.append(f"[{i + 2}:a]aresample=44100,aformat=channel_layouts=stereo,volume={vol},adelay={ms}|{ms}[s{i}]")
    labels.append(f"[s{i}]")
fc.append("".join(labels) + f"amix=inputs={len(labels)}:duration=first:normalize=0,alimiter=limit=0.95[a]")
args += ["-filter_complex", ";".join(fc), "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
         "-t", str(T), "-movflags", "+faststart", out]
subprocess.run(args, check=True)
print("mixed", out, os.path.getsize(out) // (1024 * 1024), "MB")
