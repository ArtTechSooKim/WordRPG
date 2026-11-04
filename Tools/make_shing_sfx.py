"""선제공격 '씽' 소리 합성 (#182·#183 사용자: 지금 카칭처럼 청아하면서 씽 소리가 났으면 좋겠어).
녹음을 쓰지 않고 코드로 만듦. 처음엔 씽 10(녹음)의 마찰을 잡음으로 흉내 냈는데 '치지직 불붙이는 소리'가 남 (#182) →
잡음을 흔들지 않는다. 맑은 소리의 뿌리 = 지금 카칭의 '칭' 음들 (make_preempt_sfx.py의 CHING과 같은 값):
타격('카')을 빼고, 부드럽게 시작하고, 조금 위로 스치게 하고, 맨 앞에 흔들지 않는 바람 소리를 아주 살짝 얹어 'ㅆ'을 만듦.

결과 = Builds/music_pilot/shing/{이름}.wav. 숫자를 바꿔 다시 돌리면 됨. 고른 파일은 Assets/Resources/Audio/Sfx/preempt.wav로.
사용: python Tools/make_shing_sfx.py   (numpy 필요)
"""
import os
import sys
import wave

import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Builds", "music_pilot", "shing")
SR = 44100
PEAK = -3.0  # 지금 카칭(-1.4)보다 조금 작게 — 부드럽게 시작해서 덜 튐

# (음 높이 Hz, 크기, 사라지는 시간 초) — 지금 카칭의 '칭'
CHING = [(2960, 1.0, 0.30), (4186, 0.8, 0.26), (5920, 0.62, 0.20), (7459, 0.4, 0.15), (9397, 0.28, 0.10), (11840, 0.16, 0.07)]
BEAT = 6.0  # 쇠 울림의 물결 (지금 카칭과 같음)
LONGER_BELOW = 6000  # 울림을 늘일 때 이 Hz 아래만. 높은 음까지 늘이면 귀가 아픔
AIR_BAND = (5000, 10000)  # 'ㅆ' 바람 소리의 대역

# 이름 = (시작이 커지는 시간 초, 위로 스치는 정도(반음), 스치는 시간 초, 음마다 늦게 들어오는 시간 초, 바람 크기 dB(울림 대비), 바람 길이 초, 울림 길이 배수, 전체 길이 초, 최고 음량 dB)
VERSIONS = {
    "a_clear": (0.020, 0.0, 0.05, 0.000, -20.0, 0.04, 1.5, 0.95, PEAK),  # 지금 카칭의 칭을 길고 부드럽게
    "b_rising": (0.030, 1.0, 0.06, 0.000, -16.0, 0.05, 1.4, 0.95, PEAK),  # 위로 스치며 올라가는 씽
    "c_sweep": (0.015, 0.7, 0.06, 0.012, -16.0, 0.06, 1.3, 0.95, PEAK - 2.0),  # 낮은 음부터 차례로 — 칼날을 따라 훑는 느낌
}


def air(rng, length, rise, seconds):
    """흔들지 않는 바람 소리: 잡음을 대역으로 거르고 매끄러운 곡선만 곱함 (치지직이 안 나게)"""
    spec = np.fft.rfft(rng.standard_normal(length))
    f = np.fft.rfftfreq(length, 1 / SR)
    lo, hi = AIR_BAND
    spec *= np.exp(-0.5 * ((np.log(np.maximum(f, 1.0)) - np.log(np.sqrt(lo * hi))) / (0.5 * np.log(hi / lo))) ** 2)
    y = np.fft.irfft(spec, length)
    y /= np.sqrt(np.mean(y ** 2)) + 1e-9
    t = np.arange(length) / SR
    return y * np.clip(t / rise, 0, 1) * np.exp(-np.maximum(t - rise, 0) / seconds)


def tones(length, rise, glide, glide_time, stagger, longer, shift):
    t = np.arange(length) / SR
    y = np.zeros(length)
    for i, (freq, size, decay) in enumerate(CHING):
        at = i * stagger
        c = np.maximum(t - at, 0)
        attack = 0.5 - 0.5 * np.cos(np.pi * np.clip(c / rise, 0, 1))  # 매끄럽게 커짐 — '카' 없음
        bend = 2 ** (-(glide / 12) * np.exp(-c / glide_time))  # 조금 아래에서 제 음으로 올라감
        fade = np.exp(-c / (decay * (longer if freq < LONGER_BELOW else 1.0)))
        main = np.cumsum(2 * np.pi * freq * bend / SR)
        wobble = np.cumsum(2 * np.pi * (freq + BEAT) * bend / SR)
        y += size * 0.6 * attack * fade * (np.sin(main + shift * i) + 0.6 * np.sin(wobble + shift * i)) * (t >= at)
    return y


def make(name, rise, glide, glide_time, stagger, air_db, air_time, longer, seconds, peak):
    rng = np.random.default_rng(183)
    length = int(SR * seconds)
    left, right = tones(length, rise, glide, glide_time, stagger, longer, 0.0), tones(length, rise, glide, glide_time, stagger, longer, 0.25)
    level = np.sqrt(np.mean(left[:int(0.1 * SR)] ** 2)) * 10 ** (air_db / 20)
    shared = air(rng, length, 0.004, air_time) * level
    x = np.stack([left + shared, right + shared], axis=1)
    x *= 10 ** ((peak - 20 * np.log10(np.abs(x).max() + 1e-9)) / 20)
    tail = int(0.05 * SR)
    x[-tail:] *= np.linspace(1, 0, tail)[:, None]
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    for name, row in VERSIONS.items():
        make(name, *row)
        print("만듦", name)
