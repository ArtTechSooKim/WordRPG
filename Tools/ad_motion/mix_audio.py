"""광고 영상에 소리 넣기 (#155~#159): 소리 없는 영상 + CC0 음악·효과음 → 완성 영상.

    python Tools/ad_motion/mix_audio.py 소리없는.mp4 완성.mp4   (pip install imageio-ffmpeg)

소리는 Assets/Resources/Audio의 팩 소리 + Tools/ad_motion/sfx(같은 팩, CC0)만 씀 (Licensed는 쓰지 않음). 시간표는 edit.jsx와 맞출 것.
사용자(#156): 쿵쿵 소리(hit·critical — 대부분 300Hz 아래 저음)는 귀가 아픔 → 적을 쓰러뜨릴 때 한 번만.
사용자(#158·#159): 보스 카드의 띡띡 소리가 별로 → 저음 없는 휙 소리(Whoosh2), 높은음도 거슬리지 않게 깎음.
전투·모험 구간은 효과음을 줄이고 음악 위주 — 전환이 음악 박자(120BPM)에 맞음
"""
import os
import subprocess
import sys

import imageio_ffmpeg

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SFX = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx")
MUSIC = os.path.join(ROOT, "Assets", "Resources", "Audio", "Music", "battle.ogg")
WHOOSH = os.path.join(HERE, "sfx", "whoosh2.wav")
LATE = 1.55   # 결정타·모험 묶음이 밀린 시간 (edit.jsx LATE)
LATE2 = 5.39  # 보스·마지막 화면 묶음이 밀린 시간 (edit.jsx LATE2)
T = 18.0 + LATE2
BEAT = 0.4992
FINISHER_AT = LATE + 6.45             # 결정타 장면 시작 (영상의 1.866초부터)
ADVENTURE_AT = LATE + 9.45            # 모험 시작 (상자 5박 · 뗏목 3박 · 동굴 2박 · 선제공격 3박)
PREEMPT_AT = ADVENTURE_AT + BEAT * 10

# (소리 파일, 시간, 음량, 높이) — 문제 도전 → 정답(2.21초) → 공격(3.47초, 실제 크리티컬이지만 쿵은 뺌)
EV = [("click", .05, .6, 1), ("click", .28, .6, 1), ("click", .3, .8, 1), ("click", .9, .8, 1), ("click", 1.5, .8, 1),
      ("correct", 2.21, 1, 1), ("combo", 3.47, .45, 1)]
# 결정타: 콤보 글자 → 적을 쓰러뜨리는 순간만 쿵 → 쓰러지는 소리
EV += [("combo", FINISHER_AT + (1.873 - 1.866), .6, 1), ("critical", FINISHER_AT + (2.864 - 1.866), .9, 1), ("hit", FINISHER_AT + (2.864 - 1.866), .9, 1),
       ("faint", FINISHER_AT + (4.427 - 1.866), .6, 1)]
# 상자가 열리는 순간 (영상의 1.723초, 0.24초부터 씀)
EV += [("evolve", ADVENTURE_AT + (1.731 - 0.24), .7, 1)]
# 선제공격: 버튼(5프레임 뒤) · 정답(27프레임 뒤)
EV += [("preempt", PREEMPT_AT + 5 / 30, .45, 1), ("correct", PREEMPT_AT + 27 / 30, .9, 1)]
# 보스 카드 (묶음 안 시간 + LATE2): 휙, 뒤로 갈수록 살짝 높게
EV += [(WHOOSH, LATE2 + t, 1.0, p) for t, p in zip((12.6, 12.85, 13.1, 13.35), (1.0, 1.06, 1.12, 1.19))]
# 마지막 화면
EV += [("levelup", LATE2 + 14.75, 1, 1), ("click", LATE2 + 15.6, .7, 1)]

src, out = sys.argv[1], sys.argv[2]
args = [imageio_ffmpeg.get_ffmpeg_exe(), "-nostdin", "-y", "-v", "error", "-i", src, "-stream_loop", "-1", "-i", MUSIC]
fc = [f"[1:a]atrim=0:{T},afade=t=out:st={T - 1.3}:d=1.3,volume=0.62,aresample=44100,aformat=channel_layouts=stereo[m]"]
labels = ["[m]"]
for i, (name, at, vol, pitch) in enumerate(EV):
    args += ["-i", name if os.path.isabs(name) else os.path.join(SFX, name + ".wav")]
    ms = int(at * 1000)
    shift = f"asetrate={int(44100 * pitch)},aresample=44100," if pitch != 1 else ""
    # 사용자(#159): 휙 소리는 좋지만 높은음이 거슬리지 않게 깎음
    soften = "highshelf=f=2500:g=-8,lowpass=f=7000," if name == WHOOSH else ""
    fc.append(f"[{i + 2}:a]aresample=44100,{shift}{soften}aformat=channel_layouts=stereo,volume={vol},adelay={ms}|{ms}[s{i}]")
    labels.append(f"[s{i}]")
fc.append("".join(labels) + f"amix=inputs={len(labels)}:duration=first:normalize=0,alimiter=limit=0.95[a]")
args += ["-filter_complex", ";".join(fc), "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
         "-t", str(T), "-movflags", "+faststart", out]
subprocess.run(args, check=True)
print("mixed", out, os.path.getsize(out) // (1024 * 1024), "MB")
