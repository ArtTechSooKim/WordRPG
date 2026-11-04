"""광고 영상에 소리 넣기 (#155~#158): 소리 없는 영상 + CC0 음악·효과음 → 완성 영상.

    python Tools/ad_motion/mix_audio.py 소리없는.mp4 완성.mp4   (pip install imageio-ffmpeg)

소리는 Assets/Resources/Audio의 팩 소리 + Tools/ad_motion/sfx(같은 팩, CC0)만 씀 (Licensed는 쓰지 않음). 시간표는 edit.jsx와 맞출 것.
사용자(#156): 쿵쿵 소리(hit·critical — 대부분 300Hz 아래 저음)는 귀가 아픔 → 적을 쓰러뜨릴 때 한 번만.
사용자(#158): 보스 카드의 띡띡 소리가 별로 → 저음 없는 휙 소리(Whoosh2)를 뒤로 갈수록 살짝 높게.
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
LATE = 3.04   # 전투 구간 뒤 묶음(콤보·모험)이 밀린 시간 (edit.jsx LATE)
LATE2 = 5.88  # 보스·마지막 화면 묶음이 밀린 시간 (edit.jsx LATE2)
T = 18.0 + LATE2
BEAT = 0.4992
PREEMPT_AT = LATE + 9.45 + BEAT * 8  # 선제공격 장면 시작 (걷기 3박 + 뗏목 3박 + 동굴 2박 뒤)

# (소리 파일, 시간, 음량, 높이) — 앞 장면: 문제 도전 → 정답 → 공격
EV = [("click", .05, .6, 1), ("click", .28, .6, 1), ("click", .5, .8, 1), ("click", 1.25, .8, 1), ("click", 2.0, .8, 1),
      ("correct", 2.75, 1, 1), ("combo", 4.05, .4, 1), ("combo", 4.88, .5, 1)]
# 콤보 → 결정타 (묶음 안 시간 + LATE). 쿵은 적을 쓰러뜨리는 여기만
EV += [("combo", LATE + t, .4 + i * .04, 1) for i, t in enumerate([6.8, 7.0, 7.18, 7.34, 7.48, 7.6, 7.72])]
EV += [("critical", LATE + 7.95, .9, 1), ("hit", LATE + 7.95, .9, 1)]
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
    fc.append(f"[{i + 2}:a]aresample=44100,{shift}aformat=channel_layouts=stereo,volume={vol},adelay={ms}|{ms}[s{i}]")
    labels.append(f"[s{i}]")
fc.append("".join(labels) + f"amix=inputs={len(labels)}:duration=first:normalize=0,alimiter=limit=0.95[a]")
args += ["-filter_complex", ";".join(fc), "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
         "-t", str(T), "-movflags", "+faststart", out]
subprocess.run(args, check=True)
print("mixed", out, os.path.getsize(out) // (1024 * 1024), "MB")
