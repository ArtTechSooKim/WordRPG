"""선제공격 '카칭' 후보: 실제 칼 부딪히는 소리 다듬기 (#179 사용자: 실제로 칼이 부딪히는 소리였으면 좋겠어).
원본 = '20 Sword Sound Effects (Attacks and Clashes)' by StarNinjas, CC0 — 칼 두 자루를 맞부딪혀 녹음
(https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes). Builds/music_pilot/sword_clash/ 에 받아 둠 (깃 제외).

앞 다듬기(부딪히는 순간이 바로 들리게) → 최고 음량 맞추기 → 높은음 깎기 → 들어 올림. 깎은 뒤에 음량을 맞추면 날카로운 소리가
도로 커지므로 이 순서를 지킬 것 (make_skill_sfx.py와 같음).

사용: python Tools/prepare_sword_sfx.py   (imageio-ffmpeg·numpy 필요) → Builds/music_pilot/sword_clash/out/{번호}.wav
고른 파일을 Assets/Resources/Audio/Sfx/preempt.wav로 옮기면 게임에 들어감 (CC0라 깃에 올려도 됨. LICENSE 외부 자료 목록에 더할 것).
"""
import glob
import os
import re
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = os.path.join(ROOT, "Builds", "music_pilot", "sword_clash")
OUT = os.path.join(SRC, "out")
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 44100
PEAK = -5.0  # 원본은 밝기가 7~9.5kHz라 많이 깎이므로 먼저 크게 맞춤
LIFT = 9.0  # 깎인 만큼 묻히지 않게 들어 올리는 값 — 3dB로는 앞부분이 전투곡 크기(-29dB)와 비슷해 묻힘
TOP = -5.0  # 들어 올려도 이 최고 음량을 넘지 않음 (지금 카칭은 -1.4)
TONE = "highshelf=f=4000:g=-8,lowpass=f=10000"
BEFORE_HIT = 0.03  # 부딪히는 순간 앞에 남기는 시간(초). 판정 소리와 같이 나므로 늦으면 안 됨
LONGEST = 0.8


def decode(path):
    raw = subprocess.run([FF, "-nostdin", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def tone(x):
    p = subprocess.run([FF, "-nostdin", "-v", "error", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-af", TONE, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                       input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(p.stdout, dtype=np.float32).reshape(-1, 2).copy()


def db(v):
    return 20 * np.log10(v + 1e-9)


def make(path):
    x = decode(path)
    env = np.abs(x).max(axis=1)
    start = max(0, int(env.argmax()) - int(BEFORE_HIT * SR))
    alive = np.where(env > env.max() * 10 ** (-45 / 20))[0]
    end = min(alive[-1] + int(0.02 * SR), start + int(LONGEST * SR), len(x))
    x = x[start:end]
    x = x * 10 ** ((PEAK - db(np.abs(x).max())) / 20)
    x = tone(x)
    x = x * 10 ** (min(LIFT, TOP - db(np.abs(x).max())) / 20)
    fade = min(int(0.04 * SR), len(x))
    x[-fade:] *= np.linspace(1, 0, fade)[:, None]
    head = int(0.002 * SR)
    x[:head] *= np.linspace(0, 1, head)[:, None]
    return x


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    os.makedirs(OUT, exist_ok=True)
    for path in sorted(glob.glob(os.path.join(SRC, "sword_clash.*.ogg")), key=lambda p: int(re.search(r"\.(\d+)\.ogg", p)[1])):
        number = re.search(r"\.(\d+)\.ogg", path)[1]
        x = make(path)
        pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
        with wave.open(os.path.join(OUT, number + ".wav"), "wb") as w:
            w.setnchannels(2)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(pcm.tobytes())
        print(f"{number:>2}  {len(x) / SR:4.2f}초  최고 {db(np.abs(x).max()):5.1f}dB  평균 {db(np.sqrt(np.mean(x ** 2))):5.1f}dB")
