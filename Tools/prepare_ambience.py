"""지역 환경음 후보 만들기 (#192 사용자: 지역마다 BGM이 깔리는 동시에 배경소리를): 반복되게 다듬고, 그 지역 BGM과 섞은 미리듣기를 만든다.
재료 = Ninja 팩(Audio/Sounds/Ambient) + CC0 녹음 (Builds/music_pilot/ambience/src, 라이선스 페이지 사본은 license_pages — 깃 제외).

    python Tools/prepare_ambience.py   (imageio-ffmpeg·numpy 필요)

결과 = Builds/music_pilot/ambience/out/{이름}.wav (게임에 넣을 파일) + Builds/music_phone/audio/amb_{이름}.mp3 (미리듣기: 처음 8초는 소리만, 그 뒤 BGM이 함께)
소리 크기: 파일 음량 = 그 지역 BGM 음량 + FILE_OFFSET, 게임에서 PLAY_VOLUME으로 틀면 BGM보다 약 11dB 작게 들림.
"""
import os
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prepare_music import FF, ROOT, SR, decode, lufs, write_wav  # noqa: E402

PACK = "F:/Unity/김수연습/Ninja Adventure - Asset Pack/Ninja Adventure - Asset Pack/Audio/Sounds/Ambient/"
BASE = os.path.join(ROOT, "Builds", "music_pilot", "ambience")
SRC = os.path.join(BASE, "src")
OUT = os.path.join(BASE, "out")
PHONE = os.path.join(ROOT, "Builds", "music_phone", "audio")
MUSIC = os.path.join(ROOT, "Assets", "Resources", "Audio", "Music")

MUSIC_VOLUME = 0.45    # 게임 기본 BGM 음량 (0.75 × 0.6)
PLAY_VOLUME = 0.30     # 환경음을 트는 음량 (효과음 기본 0.5 × 0.6)
FILE_OFFSET = -7.5     # 파일 음량 = BGM 음량 - 7.5 → 들리는 크기는 BGM보다 약 11dB 작음
CROSSFADE = 1.5        # 반복 이음새를 겹치는 길이(초)
LOW_CUT = "highpass=f=70"  # 흔들리는 저음·직류 빼기 (폰 스피커로 안 들리는 곳)
SOFT_TOP = "highshelf=f=6000:g=-5"  # 높은음은 조금 깎음 (사용자: 귀가 아픔)

# 이름: (원본, 시작 초, 길이 초(None = 끝까지), 함께 듣는 BGM, 추가 필터)
ITEMS = {
    "wind_a": (PACK + "Wind2.wav", 0, None, "meadow", None),
    "wind_b": (SRC + "/park_ambience_wind.wav", 60, 30, "meadow", "highpass=f=150"),
    "room_a": (SRC + "/fs_456207.mp3", 60, 30, "library", None),  # 빈 사무실 (팬 소리가 대부분)
    "room_c": (SRC + "/01 - LEGIT Audio - TheShopCollection_convenience_store_drinks_fridge_drone.mp3", 0, None, "library", None),  # 편의점 냉장고의 낮은 웅웅거림
    "fog_a": (SRC + "/windy_ambiance_0.ogg", 40, 30, "library", "lowpass=f=1500"),
    "fog_b": (PACK + "Wind.wav", 0, None, "library", None),
    "rain_a": (PACK + "Rain2.wav", 0, None, "forest", None),
    "rain_b": (PACK + "Rain.wav", 0, None, "forest", None),
    "birds_a": (SRC + "/fs_723913.mp3", 0, None, "forest", None),
    "jungle_a": (SRC + "/fs_724054.mp3", 0, None, "jungle", None),
    "jungle_b": (SRC + "/fs_670163.mp3", 40, 40, "jungle", None),
    "jungle_c": (SRC + "/fs_259510.mp3", 60, 40, "jungle", None),
    "cave_a": (SRC + "/dungeon_ambient_1_0.ogg", 0, 45, "cave", None),
    "cave_b": (SRC + "/atmosbasement.mp3_.flac", 0, None, "cave", None),
    "raft_a": (PACK + "River.wav", 0, None, "jungle", None),
    "raft_b": (SRC + "/park_ambience_river.wav", 60, 30, "jungle", None),
    "raft_c": (SRC + "/VistulaShort_0.mp3", 30, 30, "jungle", None),
}
# 공간 소리 + 시계 소리를 작게 (서고)
CLOCK = SRC + "/ticking_clock.wav"
CLOCK_ITEM = ("room_b", "room_a", -14.0)  # 이름, 바탕, 시계 크기(dB)


def filtered(x, chain):
    p = subprocess.run([FF, "-nostdin", "-v", "error", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-af", chain, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                       input=x.astype(np.float32).tobytes(), capture_output=True, check=True)
    return np.frombuffer(p.stdout, dtype=np.float32).reshape(-1, 2).copy()


def loop_even(x):
    """끝과 처음을 겹쳐 반복되게 (서로 다른 잡음이므로 힘이 같게 겹침)"""
    n = min(int(CROSSFADE * SR), len(x) // 3)
    t = np.linspace(0, 1, n)[:, None]
    out = x[:len(x) - n].copy()
    out[:n] = out[:n] * np.sqrt(t) + x[len(x) - n:] * np.sqrt(1 - t)
    return out


def level_file(x, target):
    path = os.path.join(OUT, "_probe.wav")
    write_wav(path, x)
    x = x * 10 ** ((target - lufs(path)) / 20)
    os.remove(path)
    peak = 20 * np.log10(np.abs(x).max() + 1e-9)
    return x * 10 ** (min(0.0, -3.0 - peak) / 20)


def tile(x, n):
    return np.concatenate([x] * (n // len(x) + 1))[:n]


def preview(name, amb, bgm):
    total, alone, rise = 30 * SR, 8 * SR, int(1.5 * SR)
    a = tile(amb, total) * PLAY_VOLUME
    m = tile(bgm, total - alone) * MUSIC_VOLUME
    m[:rise] *= np.linspace(0, 1, rise)[:, None]
    mix = a.copy()
    mix[alone:] += m
    tail = SR // 2
    mix[-tail:] *= np.linspace(1, 0, tail)[:, None]
    wav = os.path.join(OUT, "_mix.wav")
    write_wav(wav, np.clip(mix, -1, 1))
    subprocess.run([FF, "-nostdin", "-y", "-v", "error", "-i", wav, "-c:a", "libmp3lame", "-b:a", "128k", os.path.join(PHONE, f"amb_{name}.mp3")], check=True)
    os.remove(wav)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(PHONE, exist_ok=True)
    bgm = {}
    loud = {}
    for theme in {v[3] for v in ITEMS.values()}:
        path = os.path.join(MUSIC, theme + ".ogg")
        bgm[theme] = decode(path)
        probe = os.path.join(OUT, "_bgm.wav")
        write_wav(probe, bgm[theme])
        loud[theme] = lufs(probe)
        os.remove(probe)
    print("BGM 음량", {k: round(v, 1) for k, v in loud.items()})
    made = {}
    for name, (src, start, length, theme, extra) in ITEMS.items():
        x = decode(src)
        a = int(start * SR)
        b = len(x) if length is None else min(len(x), a + int(length * SR))
        chain = ",".join(c for c in [LOW_CUT, SOFT_TOP, extra] if c)
        x = filtered(x[a:b], chain)
        x = loop_even(x)
        x = level_file(x, loud[theme] + FILE_OFFSET)
        made[name] = x
        write_wav(os.path.join(OUT, name + ".wav"), x)
        preview(name, x, bgm[theme])
        print(f"{name:9s} {len(x) / SR:5.1f}초 음량 {lufs(os.path.join(OUT, name + '.wav')):6.1f} LUFS 최고 {20 * np.log10(np.abs(x).max()):5.1f}dB (BGM {theme})")
    name, base, clock_db = CLOCK_ITEM
    theme = ITEMS[base][3]
    tick = filtered(decode(CLOCK), LOW_CUT + "," + SOFT_TOP)
    x = made[base]
    x = level_file(tile(x, 2 * len(tick)) + tile(tick, 2 * len(tick)) * 10 ** (clock_db / 20), loud[theme] + FILE_OFFSET)
    write_wav(os.path.join(OUT, name + ".wav"), x)
    preview(name, x, bgm[theme])
    print(f"{name:9s} {len(x) / SR:5.1f}초 음량 {lufs(os.path.join(OUT, name + '.wav')):6.1f} LUFS (BGM {theme})")
