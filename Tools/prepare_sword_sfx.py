"""선제공격 소리 후보: 칼 두 자루를 긁는 '씽' 소리 다듬기 (#180 사용자: 카칭보다는 씽 소리).
원본 = '20 Sword Sound Effects (Attacks and Clashes)' by StarNinjas, CC0 — 칼 두 자루를 맞대고 움직여 녹음
(https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes). 공격 10개 = Builds/music_pilot/sword_slide/ (깃 제외).
부딪힘 10개는 Builds/music_pilot/sword_clash/에 남아 있음 (SRC·FILES를 바꾸면 같은 방법으로 다듬어짐).

앞 무음만 빼기(긁히며 커지는 앞부분은 살림) → 최고 음량 맞추기 → 높은음 깎기 → 들어 올림. 깎은 뒤에 음량을 맞추면 날카로운 소리가
도로 커지므로 이 순서를 지킬 것 (make_skill_sfx.py와 같음).

사용: python Tools/prepare_sword_sfx.py   (imageio-ffmpeg·numpy 필요) → Builds/music_pilot/sword_slide/out/{번호}.wav
고른 파일을 Assets/Resources/Audio/Sfx/preempt.wav로 옮기면 게임에 들어감 (CC0라 깃에 올려도 됨. LICENSE 외부 자료 목록에 더할 것).
"""
import glob
import os
import re
import shutil
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = os.path.join(ROOT, "Builds", "music_pilot", "sword_slide")
FILES = "sword.*.ogg"
OUT = os.path.join(SRC, "out")
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 44100
PEAK = -5.0  # 원본은 밝기가 6~10kHz라 많이 깎이므로 먼저 크게 맞춤
LIFT = 9.0  # 깎인 만큼 묻히지 않게 들어 올리는 값
TOP = -5.0  # 들어 올려도 이 최고 음량을 넘지 않음 (지금 카칭은 -1.4)
TONE = "highshelf=f=4000:g=-8,lowpass=f=10000"
FIRST_SOUND = 0.02  # 최고 크기의 이만큼(-34dB)을 넘는 곳을 첫소리로
LONGEST = 0.9

# 씽 + 지금 카칭 조합 (#181 사용자: 씽 10을 지금 소리와 조합해 보고 싶어). 이름 = (씽 번호, 지금 소리 dB, 씽 dB, 지금 소리를 늦추는 시간(초))
# 지금 카칭 원본은 make_preempt_sfx.py가 만든 소리. Sfx/preempt.wav를 바꿔도 다시 합칠 수 있게 처음 한 번 복사해 둠
NOW_PREEMPT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx", "preempt.wav")
NOW_COPY = os.path.join(SRC, "now_preempt_original.wav")
MIX_TOP = -1.4  # 지금 카칭의 최고 음량을 넘지 않음
MIXES = {
    "mix_together": ("10", -3.0, 2.0, 0.0),  # 같이 시작, 비슷한 크기
    "mix_shing": ("10", -7.0, 4.0, 0.0),  # 같이 시작, 씽이 더 큼
    "mix_landing": ("10", -3.0, 2.0, 0.24),  # 씽이 먼저 긁히고 가장 커질 때(0.24초) 카칭이 떨어짐
}


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
    win = int(0.005 * SR)
    smooth = np.convolve(env, np.ones(win) / win, mode="same")  # 잡음 한 점에 속지 않게
    above = np.where(smooth > smooth.max() * FIRST_SOUND)[0]
    start = max(0, above[0] - int(0.003 * SR))
    alive = np.where(smooth > smooth.max() * 10 ** (-40 / 20))[0]
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


def save(name, x):
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"{name:>12}  {len(x) / SR:4.2f}초  최고 {db(np.abs(x).max()):5.1f}dB  평균 {db(np.sqrt(np.mean(x ** 2))):5.1f}dB")


def mix(shing, now_db, shing_db, now_delay):
    now = decode(NOW_COPY) * 10 ** (now_db / 20)
    moved = shing * 10 ** (shing_db / 20)
    start = int(now_delay * SR)
    x = np.zeros((max(len(moved), start + len(now)), 2), dtype=np.float32)
    x[:len(moved)] += moved
    x[start:start + len(now)] += now
    x = x * 10 ** (min(0.0, MIX_TOP - db(np.abs(x).max())) / 20)
    fade = min(int(0.04 * SR), len(x))
    x[-fade:] *= np.linspace(1, 0, fade)[:, None]
    return x


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    os.makedirs(OUT, exist_ok=True)
    if not os.path.exists(NOW_COPY):
        shutil.copyfile(NOW_PREEMPT, NOW_COPY)
    made = {}
    for path in sorted(glob.glob(os.path.join(SRC, FILES)), key=lambda p: int(re.search(r"\.(\d+)\.ogg", p)[1])):
        number = re.search(r"\.(\d+)\.ogg", path)[1]
        made[number] = make(path)
        save(number, made[number])
    for name, (number, now_db, shing_db, now_delay) in MIXES.items():
        save(name, mix(made[number], now_db, shing_db, now_delay))
