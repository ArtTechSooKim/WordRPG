"""짧은 곡(승리·레벨 업·패배·각성·도감 완성)과 카칭 후보 다듬기 (#177).
앞 무음을 자르고 → 끝을 찾아 자르고 → 최고 음량을 맞춘 뒤 → 높은음을 깎고 → 묻히지 않게 조금 들어 올림.
깎은 뒤에 음량을 맞추면 날카로운 소리가 도로 커지므로 이 순서를 지킬 것 (make_skill_sfx.py와 같음).

사용: python Tools/prepare_short_sounds.py   (imageio-ffmpeg·numpy 필요)
원본 = Builds/music_pilot/round9_raw, 결과 = Builds/music_pilot/short/{이름}.wav (깃에 없음).
게임에는 사용자가 듣고 고른 뒤에 Assets/Resources/Audio/Licensed로 옮김 (같은 이름이면 팩 소리 대신 쓰임, Sound.CandidatePaths).
"""
import os
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
RAW = os.path.join(ROOT, "Builds", "music_pilot", "round9_raw")
OUT = os.path.join(ROOT, "Builds", "music_pilot", "short")
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 44100
PEAK = -7.0  # 팩 소리·기술 소리와 같은 최고 음량
LIFT_MAX = 3.0  # 깎은 만큼 묻히지 않게 들어 올리는 한도 (make_skill_sfx.py와 같음)
LIFT_PEAK = -4.0  # 들어 올려도 이 최고 음량을 넘지 않음
MELLOW = "highshelf=f=2500:g=-4"  # 곡: 전투곡과 같은 수준으로 살짝
SOFT = "highshelf=f=2500:g=-6"  # 카칭: 동전
HEAVY = "highshelf=f=1800:g=-10,lowpass=f=6000"  # 카칭: 원본이 아주 밝은 것

# 이름 = (원본, 높은음 깎기, 가장 긴 길이(초), 끝 페이드(초), 들어 올리는 값 dB — None = 깎여서 줄어든 만큼 LIFT_MAX까지)
SOUNDS = {
    "victory": ("승리.mp3", MELLOW, 7.0, 0.15, None),
    "levelup": ("레벨업.mp3", MELLOW, 7.0, 0.15, None),
    "defeat": ("패배.mp3", MELLOW, 7.0, 0.15, None),
    "awaken": ("각성.mp3", MELLOW, 7.0, 0.3, None),  # 끝이 빨리 끊겨서 페이드를 길게
    "dexcomplete": ("도감완성.mp3", MELLOW, 7.0, 0.3, None),
    "preempt_coin": ("카칭_동전.mp3", SOFT, 1.2, 0.08, None),
    "preempt_register": ("카칭_금전등록기.mp3", HEAVY, 1.0, 0.08, 8.8),  # 많이 깎여 작아짐 — 최고 음량 -6dB까지
    "preempt_sword": ("카칭_칼빛.mp3", HEAVY, 1.2, 0.08, 9.0),  # 밝은 소리라 더 키우면 귀가 아픔. 이 값이면 지금 카칭보다 2~6kHz가 약 8dB 작음
}


def decode(path, flt=None):
    args = [FF, "-nostdin", "-v", "error", "-i", path]
    if flt:
        args += ["-af", flt]
    args += ["-f", "f32le", "-ac", "2", "-ar", str(SR), "-"]
    raw = subprocess.run(args, capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def tone(x, flt):
    """높은음 깎기 — 앞 무음을 자른 소리에 필터를 걺"""
    p = subprocess.run([FF, "-nostdin", "-v", "error", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-af", flt, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                       input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(p.stdout, dtype=np.float32).reshape(-1, 2).copy()


def trim(x, longest):
    env = np.abs(x).max(axis=1)
    above = np.where(env > env.max() * 0.03)[0]  # 첫소리·끝: 최고의 약 -30dB
    start = max(0, above[0] - int(0.005 * SR))
    win = int(0.05 * SR)
    power = np.sqrt(np.convolve((env ** 2), np.ones(win) / win, mode="same"))
    alive = np.where(power > power.max() * 10 ** (-42 / 20))[0]
    end = min(alive[-1] + win, start + int(longest * SR), len(x))
    return x[start:end]


def db(v):
    return 20 * np.log10(v + 1e-9)


def write_wav(path, x):
    pcm = np.clip(x, -1, 1)
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((pcm * 32767).astype("<i2").tobytes())


def make(name, file, flt, longest, fade, lift):
    x = trim(decode(os.path.join(RAW, file)), longest)
    x = x * 10 ** ((PEAK - db(np.abs(x).max())) / 20)  # 최고 음량 먼저
    before = np.sqrt(np.mean(x ** 2))
    x = tone(x, flt)
    if lift is None:
        lift = min(LIFT_MAX, db(before) - db(np.sqrt(np.mean(x ** 2))))
    x = x * 10 ** (max(0.0, min(lift, LIFT_PEAK - db(np.abs(x).max()))) / 20)
    n = min(int(fade * SR), len(x))
    x[-n:] *= np.linspace(1, 0, n)[:, None]
    x[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))[:, None]  # 첫머리 '틱' 방지
    write_wav(os.path.join(OUT, name + ".wav"), x)
    return x


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    os.makedirs(OUT, exist_ok=True)
    for name, (file, flt, longest, fade, lift) in SOUNDS.items():
        x = make(name, file, flt, longest, fade, lift)
        print(f"{name:18s} {len(x) / SR:5.2f}초  최고 {db(np.abs(x).max()):5.1f}dB  평균 {db(np.sqrt(np.mean(x ** 2))):5.1f}dB")
