"""선제공격 소리: 슬로모션으로 들은 칼 부딪힘 (#184 사용자: 칼 부딪히는 게 슬로모션으로 들으면 씽 소리처럼 들리니까 씽을 표현하라고 한 거야).
부딪힘을 빼고 '씽'만 남기면 안 됨 (#183 → 지금 카칭보다 별로). 잡음을 흔들어 마찰을 흉내 내면 치지직이 남 (#182).
세 가지로 만듦:
 a_slow_now = 지금 카칭(make_preempt_sfx.py의 소리)을 그대로 느리게
 b_ring     = 부딪힘은 살리고 맑은 울림만 길게 — 코드로 합성 (음 높이는 지금 카칭과 같음)
 c_recorded = 실제 칼 부딪힘 녹음을 느리게 (StarNinjas CC0, sword_clash.1 — 쓰면 LICENSE 외부 자료 목록에 더함)

결과 = Builds/music_pilot/shing/{이름}.wav. 고른 파일은 Assets/Resources/Audio/Sfx/preempt.wav로.
사용: python Tools/make_shing_sfx.py   (imageio-ffmpeg·numpy 필요)
"""
import os
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
PILOT = os.path.join(ROOT, "Builds", "music_pilot")
OUT = os.path.join(PILOT, "shing")
NOW = os.path.join(PILOT, "sword_slide", "now_preempt_original.wav")  # 지금 카칭 사본 (Sfx/preempt.wav를 바꿔도 남음)
CLASH = os.path.join(PILOT, "sword_clash", "sword_clash.1.ogg")
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 44100

# 지금 카칭과 같은 음 (make_preempt_sfx.py). (음 높이 Hz, 크기, 사라지는 시간 초)
KA = [(2230, 0.9, 0.018), (3170, 0.8, 0.022), (4930, 0.6, 0.016)]
CHING = [(2960, 1.0, 0.30), (4186, 0.8, 0.26), (5920, 0.62, 0.20), (7459, 0.4, 0.15), (9397, 0.28, 0.10), (11840, 0.16, 0.07)]

SLOW_NOW = 0.6  # a: 재생 속도 (느린 만큼 음도 낮아짐)
SLOW_CLASH = 0.5  # c: 재생 속도
STRIKE_SLOW = 2.0  # b: 부딪힘이 사라지는 시간 배수
RING_LOW = 3.0  # b: 울림 시간 배수 (6kHz 아래)
RING_HIGH = 2.0  # b: 울림 시간 배수 (6kHz 위 — 많이 늘이면 귀가 아픔)
RING_AT = 0.06  # b: 부딪힘 다음 울림이 시작되는 시각 (지금 카칭 0.045를 늦춤)
RING_RISE = 0.025
BEAT = 3.0  # b: 쇠 울림의 물결 — 지금 카칭의 6Hz를 절반으로
LENGTH = 1.3
PEAKS = {"a_slow_now": -3.0, "b_ring": -4.5, "c_recorded": -3.0}  # b는 2~6kHz가 지금 카칭보다 크지 않게
TONES = {"c_recorded": "highshelf=f=5000:g=-5"}  # 녹음은 느려져도 아주 높은 소리가 남음


def decode(path):
    raw = subprocess.run([FF, "-nostdin", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def slow(x, speed):
    """테이프를 느리게 돌리듯: 길어지고 음도 낮아짐"""
    t = np.arange(0, len(x) - 1, speed)
    return np.stack([np.interp(t, np.arange(len(x)), x[:, c]) for c in range(2)], axis=1)


def tone(x, flt):
    p = subprocess.run([FF, "-nostdin", "-v", "error", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-af", flt, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                       input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(p.stdout, dtype=np.float32).reshape(-1, 2).copy()


def synth(shift):
    """b: 부딪힘 + 긴 맑은 울림"""
    n = int(LENGTH * SR)
    t = np.arange(n) / SR
    y = np.zeros(n)
    for freq, size, decay in KA:
        y += size * np.exp(-t / (decay * STRIKE_SLOW)) * np.sin(2 * np.pi * freq * t + shift)
    c = np.maximum(t - RING_AT, 0)
    attack = 0.5 - 0.5 * np.cos(np.pi * np.clip(c / RING_RISE, 0, 1))
    for i, (freq, size, decay) in enumerate(CHING):
        fade = np.exp(-c / (decay * (RING_LOW if freq < 6000 else RING_HIGH)))
        body = np.sin(2 * np.pi * freq * c + shift * i) + 0.6 * np.sin(2 * np.pi * (freq + BEAT) * c + shift * i)
        y += attack * size * 0.6 * fade * body * (t >= RING_AT)
    return y


def finish(name, x):
    x = x[:int(LENGTH * SR)]
    if name in TONES:
        x = tone(x * 10 ** ((PEAKS[name] - 20 * np.log10(np.abs(x).max() + 1e-9)) / 20), TONES[name])
    x = x * 10 ** ((PEAKS[name] - 20 * np.log10(np.abs(x).max() + 1e-9)) / 20)
    tail = int(0.08 * SR)
    x[-tail:] *= np.linspace(1, 0, tail)[:, None]
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    finish("a_slow_now", slow(decode(NOW), SLOW_NOW))
    finish("b_ring", np.stack([synth(0.0), synth(0.25)], axis=1))
    clash = decode(CLASH)
    hit = int(np.abs(clash).max(axis=1).argmax())
    finish("c_recorded", slow(clash[max(0, hit - int(0.015 * SR)):], SLOW_CLASH))
    print("만듦")
