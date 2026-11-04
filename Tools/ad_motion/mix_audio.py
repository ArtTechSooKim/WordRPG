"""광고 영상에 소리 넣기 (#155·#156): 소리 없는 영상 + CC0 음악·효과음 → 완성 영상.

    python Tools/ad_motion/mix_audio.py 소리없는.mp4 완성.mp4   (pip install imageio-ffmpeg)

소리는 Assets/Resources/Audio의 팩 소리만 씀 (Licensed는 쓰지 않음). 시간표는 edit.jsx와 맞출 것.
사용자(#156): 쿵쿵 소리(hit·critical — 대부분 300Hz 아래 저음)는 귀가 아픔 → 적을 쓰러뜨릴 때 한 번만.
기술 나열 구간은 효과음 없이 음악만 — 전환이 음악 박자(120BPM)에 맞음
"""
import os
import subprocess
import sys

import imageio_ffmpeg

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SFX = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx")
MUSIC = os.path.join(ROOT, "Assets", "Resources", "Audio", "Music", "battle.ogg")
LATE = 3.04  # 기술 나열 뒤 장면이 밀린 시간 (edit.jsx LATE)
T = 18.0 + LATE

# 앞 장면: 문제 도전 → 정답 → 공격
EV = [("click", .05, .6), ("click", .28, .6), ("click", .5, .8), ("click", 1.25, .8), ("click", 2.0, .8),
      ("correct", 2.75, 1), ("combo", 4.05, .4), ("combo", 4.88, .5)]
# 뒤 장면 (edit.jsx의 묶음 안 시간 + LATE)
EV += [("combo", LATE + t, .4 + i * .04) for i, t in enumerate([6.8, 7.0, 7.18, 7.34, 7.48, 7.6, 7.72])]
EV += [("critical", LATE + 7.95, .9), ("hit", LATE + 7.95, .9)]  # 결정타 = 적을 쓰러뜨리는 순간, 쿵은 여기만
EV += [("click", LATE + 9.25, .6), ("click", LATE + 10.15, .6), ("click", LATE + 11.05, .6)]
EV += [("click", LATE + t, .7) for t in (12.45, 12.75, 13.05, 13.35)]
EV += [("levelup", LATE + 14.75, 1), ("click", LATE + 15.6, .7)]

src, out = sys.argv[1], sys.argv[2]
args = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-v", "error", "-i", src, "-stream_loop", "-1", "-i", MUSIC]
fc = [f"[1:a]atrim=0:{T},afade=t=out:st={T - 1.3}:d=1.3,volume=0.62,aresample=44100,aformat=channel_layouts=stereo[m]"]
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
