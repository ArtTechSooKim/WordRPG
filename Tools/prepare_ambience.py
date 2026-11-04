"""지역 환경음 만들기 (#192·#193 사용자: 지역마다 BGM이 깔리는 동시에 배경소리를): 반복되게 다듬어 게임에 넣고, 그 지역 BGM과 섞은 미리듣기를 만든다.
재료 = Ninja 팩(Audio/Sounds/Ambient) + CC0 녹음 (Builds/music_pilot/ambience/src, 라이선스 페이지 사본은 license_pages — 깃 제외. 출처 표는 Docs/음악계획.md).

    python Tools/prepare_ambience.py   (imageio-ffmpeg·numpy 필요)

결과 = Assets/Resources/Audio/Ambience/{이름}.ogg (Ambience 열거형 소문자, CC0라 깃에 올림) + Builds/music_phone/audio/amb_{이름}.mp3 (미리듣기: 처음 8초는 소리만, 그 뒤 BGM이 함께)
    KEEP 표의 소리는 아직 안 쓰는 것 → Builds/music_pilot/ambience/out/{이름}.wav만
소리 크기: 파일 음량 = 그 지역 BGM 음량 + FILE_OFFSET, 게임에서 PLAY_VOLUME(Sound.AmbienceBase × 효과음 기본 0.5)으로 틀면 BGM보다 약 11dB 작게 들림.
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
GAME = os.path.join(ROOT, "Assets", "Resources", "Audio", "Ambience")
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
    "library": (SRC + "/fs_456207.mp3", 60, 30, "library", None),  # 빈 사무실 녹음 + 아래 시계 소리
    "forest": (SRC + "/fs_723913.mp3", 0, None, "forest", None),  # 숲 새소리
    "jungle": (SRC + "/fs_670163.mp3", 40, 40, "jungle", None),  # 아마존 녹음: 새·원숭이·귀뚜라미
    "cave": (SRC + "/dungeon_ambient_1_0.ogg", 0, 45, "cave", None),  # 낮은 바람 + 물방울
    "raft": (PACK + "River.wav", 0, None, "jungle", None),  # 강물
}
# 사용자: 바람은 나중에 하늘 신전에서 (#193 — 아직 게임에 없음, 어느 쪽을 쓸지도 그때)
KEEP = {
    "wind_a": (PACK + "Wind2.wav", 0, None, "meadow", None),
    "wind_b": (SRC + "/park_ambience_wind.wav", 60, 30, "meadow", "highpass=f=150"),
}
# 서고의 시계는 계속이 아니라 가끔 (#193 사용자): 공간 소리를 두 번 이어 붙이고 시계를 두 자리에만
CLOCK = SRC + "/ticking_clock.wav"
CLOCK_DB = -14.0       # 공간 소리에 대한 크기
CLOCK_SECONDS = 5.0    # 한 번에 이어지는 길이 (1초에 한 번 똑딱)
CLOCK_AT = (3.0, 34.0) # 곡 안의 자리(초) — 간격이 일정하지 않게
PREVIEW_SECONDS = {"library": 45}  # 시계가 두 번 다 들리게


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


def with_clock(room):
    """공간 소리 두 번 + 시계를 정해진 자리에만"""
    x = np.concatenate([room, room])
    tick = filtered(decode(CLOCK), LOW_CUT + "," + SOFT_TOP)[:int(CLOCK_SECONDS * SR)].copy()
    edge = SR // 10
    tick[:edge] *= np.linspace(0, 1, edge)[:, None]
    tick[-edge:] *= np.linspace(1, 0, edge)[:, None]
    for at in CLOCK_AT:
        a = int(at * SR)
        x[a:a + len(tick)] += tick * 10 ** (CLOCK_DB / 20)
    return x


def preview(name, amb, bgm):
    total, alone, rise = PREVIEW_SECONDS.get(name, 30) * SR, 8 * SR, int(1.5 * SR)
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


def make(src, start, length, extra):
    x = decode(src)
    a = int(start * SR)
    b = len(x) if length is None else min(len(x), a + int(length * SR))
    chain = ",".join(c for c in [LOW_CUT, SOFT_TOP, extra] if c)
    return loop_even(filtered(x[a:b], chain))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(GAME, exist_ok=True)
    os.makedirs(PHONE, exist_ok=True)
    bgm = {}
    loud = {}
    for theme in {v[3] for v in list(ITEMS.values()) + list(KEEP.values())}:
        bgm[theme] = decode(os.path.join(MUSIC, theme + ".ogg"))
        probe = os.path.join(OUT, "_bgm.wav")
        write_wav(probe, bgm[theme])
        loud[theme] = lufs(probe)
        os.remove(probe)
    print("BGM 음량", {k: round(v, 1) for k, v in loud.items()})
    for name, (src, start, length, theme, extra) in ITEMS.items():
        x = make(src, start, length, extra)
        if name == "library":
            x = with_clock(x)
        x = level_file(x, loud[theme] + FILE_OFFSET)
        wav = os.path.join(OUT, name + ".wav")
        write_wav(wav, x)
        subprocess.run([FF, "-nostdin", "-y", "-v", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "7", os.path.join(GAME, name + ".ogg")], check=True)
        preview(name, x, bgm[theme])
        size = os.path.getsize(os.path.join(GAME, name + ".ogg")) / 1e6
        print(f"{name:9s} {len(x) / SR:5.1f}초 음량 {lufs(wav):6.1f} LUFS 최고 {20 * np.log10(np.abs(x).max()):5.1f}dB (BGM {theme}) ogg {size:.2f}MB")
    for name, (src, start, length, theme, extra) in KEEP.items():
        x = level_file(make(src, start, length, extra), loud[theme] + FILE_OFFSET)
        write_wav(os.path.join(OUT, name + ".wav"), x)
        print(f"{name:9s} {len(x) / SR:5.1f}초 (보관)")
