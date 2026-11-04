"""선제공격 '씽' 소리 합성 (#182 사용자: 씽 10을 참고해서 네가 만들어 볼래?). 녹음을 쓰지 않고 코드로 만듦.
참고한 소리(StarNinjas CC0 sword.10)를 재 보니 두 단계: ① 앞 0.3초 = 칼이 긁히는 마찰(넓은 잡음, 점점 커짐, 빠르게 떨림)
② 그 뒤 = 쇠 울림 (5254·5642Hz가 나란히 오래 남고 나머지는 먼저 사라짐). 아래 표의 숫자가 그 측정값.

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
PEAK = -5.0  # 참고한 소리와 같은 최고 음량
FRAME, HOP = 1024, 256

# ① 마찰: 시각(초)마다 대역별 크기(dB). 대역 경계는 EDGES. 측정값에서 쇠 울림 몫을 뺀 값
EDGES = [0, 500, 1000, 2000, 3000, 4000, 6000, 8000, 10000, 14000, 22050]
SCRAPE = [
    (0.00, [8, 13, 21, 21, 25, 24, 27, 24, 22, 1]),
    (0.05, [5, 16, 22, 18, 26, 28, 27, 28, 22, 5]),
    (0.10, [3, 11, 16, 16, 18, 22, 23, 24, 20, 12]),
    (0.15, [1, 13, 17, 23, 26, 30, 32, 29, 27, 14]),
    (0.20, [1, 12, 26, 30, 32, 30, 37, 33, 29, 17]),
    (0.25, [4, 13, 23, 28, 32, 32, 33, 36, 32, 16]),
    (0.30, [0, 4, 3, 16, 20, 25, 24, 26, 18, 4]),
    (0.35, [2, 1, -1, 12, 14, 18, 18, 22, 8, -9]),
    (0.45, [1, 4, -1, 6, 6, 10, 8, 8, -6, -20]),
    (0.60, [1, 2, -3, 2, 2, 8, 2, -8, -18, -32]),
    (0.75, [4, 2, -2, -7, -4, 4, -6, -18, -22, -32]),
]
SHAKE = 0.35  # 마찰이 떨리는 깊이 (측정 0.2 + 잡음 자체가 고른 만큼 더함)

# ② 쇠 울림: (음 높이 Hz, 시작 크기 dB, 줄어드는 시간 초). 시작 시각은 RING_AT
RING_AT = 0.22
RING = [
    (5642, -24.0, 0.170),
    (5254, -25.0, 0.135),
    (7730, -27.0, 0.075),
    (6309, -35.0, 0.190),
    (8419, -31.0, 0.055),
    (3919, -38.0, 0.200),
    (2950, -40.0, 0.190),
    (7300, -33.0, 0.060),
]
BEAT = 5.0  # 쇠 소리의 물결: 같은 음을 이만큼 어긋나게 작게 한 번 더

# 이름 = (마찰 시간 배수, 마찰 dB, 울림 dB, 울림 길이 배수, 이 Hz보다 높은 울림은 뺌, 길이 초)
VERSIONS = {
    "a_like10": (1.0, 0.0, 0.0, 1.0, 99999, 0.90),  # 씽 10처럼
    "b_clear": (1.0, -5.0, 3.0, 1.25, 7000, 1.00),  # 긁힘을 줄이고 맑은 울림을 길게, 아주 높은 음은 뺌
    "c_quick": (0.4, 0.0, 0.0, 0.85, 99999, 0.62),  # 긁힘을 빠르게 — 첫소리가 바로 커짐
}


def band_gain(times, bins_hz, table):
    """시각·주파수별 잡음 크기. 대역 크기(dB)를 칸마다의 밀도로 바꿔 시간으로 잇는다"""
    centers = np.array([(a + b) / 2 for a, b in zip(EDGES[:-1], EDGES[1:])])
    widths = np.array([b - a for a, b in zip(EDGES[:-1], EDGES[1:])], dtype=float)
    keys = np.array([t for t, _ in table])
    levels = np.array([v for _, v in table], dtype=float) - 10 * np.log10(widths / widths[2])
    in_time = np.stack([np.interp(times, keys, levels[:, i]) for i in range(levels.shape[1])], axis=1)
    where = np.log(np.maximum(bins_hz, 20.0))
    gain = np.stack([np.interp(where, np.log(centers), row) for row in in_time])
    return 10 ** (gain / 20)


def shaped_noise(rng, length, table):
    """흰 잡음을 STFT로 표대로 빚는다"""
    window = np.sqrt(np.hanning(FRAME))
    noise = rng.standard_normal(length + FRAME)
    bins = np.fft.rfftfreq(FRAME, 1 / SR)
    starts = np.arange(0, length, HOP)
    gain = band_gain((starts + FRAME / 2) / SR, bins, table)
    out = np.zeros(length + FRAME)
    for k, s in enumerate(starts):
        spec = np.fft.rfft(noise[s:s + FRAME] * window)
        out[s:s + FRAME] += np.fft.irfft(spec * gain[k], FRAME) * window
    return out[:length]


def shake(rng, length):
    """칼이 걸렸다 미끄러지는 떨림 (측정: 40~70Hz와 200~400Hz)"""
    def band(lo, hi):
        spec = np.fft.rfft(rng.standard_normal(length))
        f = np.fft.rfftfreq(length, 1 / SR)
        spec[(f < lo) | (f > hi)] = 0
        y = np.fft.irfft(spec, length)
        return y / (np.std(y) + 1e-9)
    return np.clip(1 + SHAKE * (0.7 * band(40, 70) + 0.7 * band(200, 400)), 0.1, 2.5)


def ring(rng, length, at, gain_db, stretch, top_hz, channel_shift):
    t = np.arange(length) / SR
    c = t - at
    y = np.zeros(length)
    for freq, level, decay in RING:
        if freq > top_hz:
            continue
        amp = 10 ** ((level + gain_db) / 20)
        attack = np.clip(c / 0.012, 0, 1)
        fade = np.exp(-np.maximum(c, 0) / (decay * stretch))
        phase = rng.uniform(0, 2 * np.pi)
        body = np.sin(2 * np.pi * freq * c + phase + channel_shift) + 0.45 * np.sin(2 * np.pi * (freq + BEAT) * c + phase * 0.7)
        y += amp * attack * fade * body * (c >= 0)
    return y


def make(name, time_scale, scrape_db, ring_db, ring_stretch, top_hz, seconds):
    rng = np.random.default_rng(182)
    length = int(SR * seconds)
    table = [(t * time_scale, [v + scrape_db for v in row]) for t, row in SCRAPE]
    # 양쪽 귀에 조금 다르게: 공통 잡음 + 채널별 잡음
    common = shaped_noise(rng, length, table)
    left = shaped_noise(rng, length, table)
    right = shaped_noise(rng, length, table)
    move = shake(rng, length)
    noise_scale = 10 ** (NOISE_TRIM / 20)
    at = RING_AT * time_scale
    state = rng.bit_generator.state
    ring_l = ring(rng, length, at, ring_db, ring_stretch, top_hz, 0.0)
    rng.bit_generator.state = state
    ring_r = ring(rng, length, at, ring_db, ring_stretch, top_hz, 0.35)
    l = noise_scale * move * (common + 0.5 * left) + ring_l
    r = noise_scale * move * (common + 0.5 * right) + ring_r
    x = np.stack([l, r], axis=1)
    x *= 10 ** ((PEAK - 20 * np.log10(np.abs(x).max() + 1e-9)) / 20)
    head, tail = int(0.002 * SR), int(0.04 * SR)
    x[:head] *= np.linspace(0, 1, head)[:, None]
    x[-tail:] *= np.linspace(1, 0, tail)[:, None]
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


NOISE_TRIM = -60.0  # 표의 dB를 파일 크기로 옮기는 값 (참고한 소리와 비교해 맞춤)

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    for name, row in VERSIONS.items():
        make(name, *row)
        print("만듦", name)
