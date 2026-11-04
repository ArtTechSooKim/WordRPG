"""짧은 곡(승리·레벨 업·패배·각성·도감 완성)과 카칭 다듬기 (#177·#178).
빠르기 올리기 → 앞 무음·끝 자르기 → 최고 음량 맞추기 → 높은음 깎기 → 묻히지 않게 조금 들어 올림.
깎은 뒤에 음량을 맞추면 날카로운 소리가 도로 커지므로 이 순서를 지킬 것 (make_skill_sfx.py와 같음).

사용: python Tools/prepare_short_sounds.py   (imageio-ffmpeg·numpy 필요)
원본 = Builds/music_pilot/round9_raw·round10_raw, 결과 = Builds/music_pilot/short/{이름}.wav (깃에 없음).
게임에는 사용자가 듣고 고른 뒤에 Assets/Resources/Audio/Licensed로 옮김 (같은 이름이면 팩 소리 대신 쓰임, Sound.CandidatePaths).

- 음악 모델은 3초보다 짧게 요청할 수 없고 '1초'라고 지시해도 3~4.5초로 만듦 → 빠르기를 올려 약 2초로 (음높이는 그대로)
- 처음 만든 6초 판(round9_raw)은 사용자: 지금 소리가 더 좋음, 너무 길다. 카칭 동전·금전등록기도 안 씀 (#178)
"""
import os
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
PILOT = os.path.join(ROOT, "Builds", "music_pilot")
OUT = os.path.join(PILOT, "short")
NOW_PREEMPT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx", "preempt.wav")  # 코드로 합성한 지금 카칭 (make_preempt_sfx.py)
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 44100
PEAK = -10.0  # 지금 팩 소리와 같은 최고 음량 (파일 그대로 잰 값)
SWORD_PEAK = -7.0
LIFT_PEAK = -4.0  # 들어 올려도 이 최고 음량을 넘지 않음
MELLOW = "highshelf=f=2500:g=-4"  # 곡: 전투곡과 같은 수준으로 살짝
HEAVY = "highshelf=f=1800:g=-10,lowpass=f=6000"  # 칼빛: 원본이 아주 밝음

# 이름 = (원본, 빠르기 배수, 높은음 깎기, 가장 긴 길이(초), 끝 페이드(초), 최고 음량, 들어 올리는 값 dB)
SOUNDS = {
    "victory": ("round10_raw/승리.mp3", 2.25, MELLOW, 2.5, 0.08, PEAK, 0.0),
    "levelup": ("round10_raw/레벨업.mp3", 2.3, MELLOW, 2.0, 0.08, PEAK, 0.0),
    "defeat": ("round10_raw/패배.mp3", 1.0, MELLOW, 3.0, 0.08, PEAK, 0.0),  # 이미 2.4초
    "awaken": ("round10_raw/각성.mp3", 1.95, MELLOW, 2.5, 0.08, PEAK, 0.0),
    "dexcomplete": ("round10_raw/도감완성.mp3", 1.65, MELLOW, 2.5, 0.08, PEAK, 0.0),
    "preempt_sword": ("round9_raw/카칭_칼빛.mp3", 1.0, HEAVY, 1.2, 0.08, SWORD_PEAK, 9.0),  # 밝은 소리라 더 키우면 귀가 아픔
}

# 카칭 = 지금 소리 + 칼빛 (#178 사용자). 지금 소리의 '칭'이 시작하는 곳에 칼빛을 맞춤. 이름 = (지금 소리 dB, 칼빛 dB)
CHING_AT = 0.04
MIXES = {
    "preempt_mix": (-3.0, 4.0),  # 반반: 평균 크기를 같게 맞춘 뒤 합쳐서 지금 소리만큼의 크기로
    "preempt_mix_sword": (-7.0, 6.0),  # 칼빛 많이
    "preempt_mix_now": (-1.5, 0.0),  # 지금 소리 많이
}
MIX_MAX_PEAK = -1.4  # 지금 카칭의 최고 음량을 넘지 않음


def decode(path, flt=None):
    args = [FF, "-nostdin", "-v", "error", "-i", path]
    if flt:
        args += ["-af", flt]
    args += ["-f", "f32le", "-ac", "2", "-ar", str(SR), "-"]
    raw = subprocess.run(args, capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def tone(x, flt):
    p = subprocess.run([FF, "-nostdin", "-v", "error", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-af", flt, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                       input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(p.stdout, dtype=np.float32).reshape(-1, 2).copy()


def trim(x, longest):
    env = np.abs(x).max(axis=1)
    above = np.where(env > env.max() * 0.03)[0]  # 첫소리: 최고의 약 -30dB
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


def fade_ends(x, fade):
    n = min(int(fade * SR), len(x))
    x[-n:] *= np.linspace(1, 0, n)[:, None]
    head = int(0.002 * SR)
    x[:head] *= np.linspace(0, 1, head)[:, None]  # 첫머리 '틱' 방지
    return x


def make(name, file, speed, flt, longest, fade, peak, lift):
    x = trim(decode(os.path.join(PILOT, file), f"atempo={speed}" if speed != 1.0 else None), longest)
    x = x * 10 ** ((peak - db(np.abs(x).max())) / 20)  # 최고 음량 먼저
    x = tone(x, flt)
    x = x * 10 ** (max(0.0, min(lift, LIFT_PEAK - db(np.abs(x).max()))) / 20)
    x = fade_ends(x, fade)
    write_wav(os.path.join(OUT, name + ".wav"), x)
    return x


def make_mix(name, now_db, sword_db, sword):
    now = decode(NOW_PREEMPT) * 10 ** (now_db / 20)
    moved = sword * 10 ** (sword_db / 20)
    start = int(CHING_AT * SR)
    x = np.zeros((max(len(now), start + len(moved)), 2), dtype=np.float32)
    x[:len(now)] += now
    x[start:start + len(moved)] += moved
    x = x * 10 ** (min(0.0, MIX_MAX_PEAK - db(np.abs(x).max())) / 20)
    x = fade_ends(x, 0.08)
    write_wav(os.path.join(OUT, name + ".wav"), x)
    return x


def show(name, x):
    print(f"{name:18s} {len(x) / SR:5.2f}초  최고 {db(np.abs(x).max()):5.1f}dB  평균 {db(np.sqrt(np.mean(x ** 2))):5.1f}dB")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    os.makedirs(OUT, exist_ok=True)
    made = {}
    for name, row in SOUNDS.items():
        made[name] = make(name, *row)
        show(name, made[name])
    for name, (now_db, sword_db) in MIXES.items():
        show(name, make_mix(name, now_db, sword_db, made["preempt_sword"]))
