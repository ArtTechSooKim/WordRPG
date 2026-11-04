"""선제공격 소리: 지금 '띠링'의 '띠'만 실제 칼 부딪힘으로 바꿈 (#185 사용자: 띠(링)에서 띠 할 때 칼 부딪히는 실제 소리를 좀 변조해서 넣으면 어떨까).
'링'은 지금 카칭(make_preempt_sfx.py)과 똑같이 계산함 — 지금 소리에서 '띠'를 뺀 값. 만들 때 그 계산이 지금 파일과 맞는지 검사함.
'띠' 재료 = '20 Sword Sound Effects (Attacks and Clashes)' by StarNinjas, CC0 (Builds/music_pilot/sword_clash, 깃 제외).
변조 = 느리게 재생(음이 낮아져 지금 '띠'의 음에 가까워짐) + 높은음 깎기 + 줄어드는 곡선을 곱함 ('링' 밑으로 조금 이어지게 — 너무 짧으면 지금 소리와 차이가 안 들림).

앞선 시도와 사용자 의견 (#182~#184): 씽 10 흉내 = 치지직 / 부딪힘 없는 맑은 씽 = 지금보다 별로 / 통째로 슬로모션 = 별로.

결과 = Builds/music_pilot/shing/{이름}.wav. 고른 파일은 Assets/Resources/Audio/Sfx/preempt.wav로 (쓰면 LICENSE 외부 자료 목록에 더함).
사용: python Tools/make_shing_sfx.py   (imageio-ffmpeg·numpy 필요)
"""
import os
import random
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
PILOT = os.path.join(ROOT, "Builds", "music_pilot")
OUT = os.path.join(PILOT, "shing")
CLASH = os.path.join(PILOT, "sword_clash", "sword_clash.{}.ogg")
NOW = os.path.join(PILOT, "sword_slide", "now_preempt_original.wav")  # 지금 카칭 사본
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 44100

# make_preempt_sfx.py와 같은 값
LENGTH = 0.75
CHING_AT = 0.045
KA = [(2230, 0.9, 0.018), (3170, 0.8, 0.022), (4930, 0.6, 0.016)]
CHING = [(2960, 1.0, 0.30), (4186, 0.8, 0.26), (5920, 0.62, 0.20), (7459, 0.4, 0.15), (9397, 0.28, 0.10), (11840, 0.16, 0.07)]
BEAT = 6.0

# 이름 = (칼 번호, 재생 속도, 줄어드는 시간 초, '띠' 첫 10ms 크기 dB)
# 지금 '띠'의 첫 10ms는 -14.9dB, 음은 2239·3165·4931Hz
VERSIONS = {
    "ti_5": ("5", 0.50, 0.035, -9.0),  # 음 6395→3200Hz. 가장 빨리 줄어드는 녹음
    "ti_8": ("8", 0.85, 0.030, -9.0),  # 음 3703→3150, 6416→5450Hz. 속도를 조금만 바꿔 실제 소리에 가장 가까움
    "ti_10": ("10", 0.62, 0.030, -9.0),  # 음 5081→3150, 6330→3920Hz
}
TONE = "highshelf=f=5000:g=-6"


def db(v):
    return 20 * np.log10(v + 1e-9)


def decode(path):
    raw = subprocess.run([FF, "-nostdin", "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).copy()


def tone(x):
    p = subprocess.run([FF, "-nostdin", "-v", "error", "-f", "f32le", "-ac", "1", "-ar", str(SR), "-i", "-", "-af", TONE, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"],
                       input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(p.stdout, dtype=np.float32).copy()


def now_parts():
    """지금 카칭을 make_preempt_sfx.py와 똑같이 계산해 '띠'와 '링'으로 나눔 (같은 씨앗·같은 크기)"""
    count = int(SR * LENGTH)
    rng = random.Random(147)
    previous = 0.0
    bright = np.zeros(count)
    for i in range(count):
        noise = rng.uniform(-1, 1)
        bright[i] = noise - previous
        previous = noise
    t = np.arange(count) / SR
    ti = np.zeros(count)
    early = t < 0.08
    for freq, size, decay in KA:
        ti += early * size * np.exp(-t / decay) * np.sin(2 * np.pi * freq * t)
    ti += early * 0.55 * np.exp(-t / 0.008) * bright
    c = t - CHING_AT
    on = c >= 0
    attack = np.minimum(1.0, np.maximum(c, 0) / 0.0015)
    ring = np.zeros(count)
    for freq, size, decay in CHING:
        wave_ = np.sin(2 * np.pi * freq * c) + 0.6 * np.sin(2 * np.pi * (freq + BEAT) * c)
        ring += on * attack * size * 0.6 * np.exp(-np.maximum(c, 0) / decay) * wave_
    ring += on * attack * 0.25 * np.exp(-np.maximum(c, 0) / 0.012) * bright
    peak = np.abs(ti + ring).max()
    fade = np.minimum(1.0, (count - np.arange(count)) / int(SR * 0.05))
    scale = 0.85 / peak * fade
    return ti * scale, ring * scale


def make_ti(number, speed, decay, level):
    x = decode(CLASH.format(number))
    hit = int(np.abs(x).argmax())
    x = x[max(0, hit - int(0.002 * SR)):]
    t = np.arange(0, min(len(x) - 1, int(0.2 * SR)), speed)
    x = np.interp(t, np.arange(len(x)), x)  # 느리게 재생 — 음이 낮아짐
    x = x / (np.abs(x).max() + 1e-9)
    x = tone(x)
    x = x * np.exp(-np.arange(len(x)) / SR / decay)
    head = int(0.001 * SR)
    x[:head] *= np.linspace(0, 1, head)
    x = x * 10 ** ((level - db(np.sqrt(np.mean(x[:int(0.01 * SR)] ** 2)))) / 20)
    return x


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    ti_now, ring_now = now_parts()
    with wave.open(NOW) as w:
        original = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768
    n = min(len(original), len(ring_now))
    error = np.abs(original[:n] - (ti_now + ring_now)[:n]).max()
    print(f"계산한 지금 카칭과 파일의 최대 차이: {error:.5f}")
    if error > 0.001:
        sys.exit("'링'이 지금 소리와 다름 — make_preempt_sfx.py와 값을 맞출 것")
    os.makedirs(OUT, exist_ok=True)
    for name, (number, speed, decay, level) in VERSIONS.items():
        ti = make_ti(number, speed, decay, level)
        x = ring_now.copy()
        m = min(len(ti), len(x))
        x[:m] += ti[:m]
        x = x * 10 ** (min(0.0, -1.4 - db(np.abs(x).max())) / 20)  # 지금 카칭의 최고 음량을 넘지 않음
        with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())
        print(f"{name:6s} {len(x) / SR:4.2f}초 최고 {db(np.abs(x).max()):5.1f}dB")
