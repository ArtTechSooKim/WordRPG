"""선제공격 소리 '카칭!' 만들기 (#147 사용자: 날카로운 카칭 소리). 팩에 금속음이 없어서 코드로 합성한다 — 표준 라이브러리만 씀.
'카' = 아주 짧은 금속 타격 + 잡음, '칭' = 배음이 맞지 않는 높은 음들이 울리다 사라짐 (옆 음과 살짝 어긋나 쇠 울림처럼 들림).
결과 = Assets/Resources/Audio/Sfx/preempt.wav. 숫자를 바꿔 다시 돌리면 됨. 더 좋은 소리를 받으면 Resources/Audio/Licensed/preempt.wav에 두면 먼저 쓰임.
사용: python Tools/make_preempt_sfx.py
"""
import math
import os
import random
import struct
import wave

RATE = 44100
LENGTH = 0.75
CHING_AT = 0.045  # '카' 다음 '칭'이 시작되는 시각

# (음 높이 Hz, 크기, 사라지는 시간 초)
KA = [(2230, 0.9, 0.018), (3170, 0.8, 0.022), (4930, 0.6, 0.016)]
CHING = [(2960, 1.0, 0.30), (4186, 0.8, 0.26), (5920, 0.62, 0.20), (7459, 0.4, 0.15), (9397, 0.28, 0.10), (11840, 0.16, 0.07)]
BEAT = 6.0  # 쇠 울림: 같은 음을 이만큼 어긋나게 한 번 더


def sample(t, rng_value):
    value = 0.0
    if t < 0.08:
        for freq, size, decay in KA:
            value += size * math.exp(-t / decay) * math.sin(2 * math.pi * freq * t)
        value += 0.55 * math.exp(-t / 0.008) * rng_value  # 부딪히는 순간의 잡음
    if t >= CHING_AT:
        c = t - CHING_AT
        attack = min(1.0, c / 0.0015)
        for freq, size, decay in CHING:
            ring = math.sin(2 * math.pi * freq * c) + 0.6 * math.sin(2 * math.pi * (freq + BEAT) * c)
            value += attack * size * 0.6 * math.exp(-c / decay) * ring
        value += attack * 0.25 * math.exp(-c / 0.012) * rng_value
    return value


def main():
    rng = random.Random(147)
    count = int(RATE * LENGTH)
    previous = 0.0
    samples = []
    for i in range(count):
        noise = rng.uniform(-1, 1)
        bright = noise - previous  # 높은 소리 쪽 잡음만
        previous = noise
        samples.append(sample(i / RATE, bright))

    peak = max(abs(v) for v in samples)
    fade = int(RATE * 0.05)
    frames = bytearray()
    for i, value in enumerate(samples):
        gain = min(1.0, (count - i) / fade)
        frames += struct.pack("<h", int(value / peak * 0.85 * gain * 32767))

    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Audio", "Sfx", "preempt.wav")
    with wave.open(path, "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(bytes(frames))
    # 소리를 들을 수 없을 때 확인용: 처음·울림 끝의 크기
    def level(start, end):
        part = samples[int(start * RATE):int(end * RATE)]
        return math.sqrt(sum(v * v for v in part) / len(part)) / peak
    print(f"{os.path.normpath(path)}: {LENGTH}초, 크기 0~0.05초 {level(0, 0.05):.2f} / 0.05~0.2초 {level(0.05, 0.2):.2f} / 0.5~0.7초 {level(0.5, 0.7):.3f}")


if __name__ == "__main__":
    main()
