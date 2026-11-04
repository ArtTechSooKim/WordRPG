"""받은 곡을 게임용으로 다듬기 (#166): 끝의 무음을 빼고, 반복 이음새를 찾아 겹치고, 음량을 맞춰 Licensed 폴더에 넣는다.

    python Tools/prepare_music.py [원본 폴더, 기본 Builds/music_pilot]   (pip install imageio-ffmpeg numpy)

결과 = Assets/Resources/Audio/Licensed/{이름}.wav (Unity가 가져올 때 Vorbis로 압축, 이 폴더는 깃 제외 — 백업 필요) +
Builds/music_pilot/이음새_확인.mp3 (곡마다 끝 6초 + 처음 6초를 이어 붙임, 표 순서대로)
표에 없는 곡은 게임에 넣지 않음: Resources 폴더는 전부 앱에 들어가므로 아직 안 쓰는 곡(사막 등)은 두지 말 것
"""
import os
import re
import subprocess
import sys
import tempfile
import wave

import imageio_ffmpeg
import numpy as np

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Licensed")
SEAMS = os.path.join(ROOT, "Builds", "music_pilot", "이음새_확인.mp3")
FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 44100
BATTLE, BOSS = -19.0, -18.0  # 음량(LUFS). 지금 지역 음악이 -13~-23이라 전투에 들어갈 때 소리가 푹 꺼지지 않게. 사용자가 듣고 바꿀 수 있음
FADE = 0.4         # 반복 이음새를 겹치는 길이(초)
MIN_LENGTH = 35.0  # 이음새를 찾을 때 곡이 이보다 짧아지지 않게(초)
COMPARE = 4.0      # 이음새 뒤와 곡 처음을 비교하는 길이(초)

# 게임 이름(Music 열거형 소문자) → (원본, 음량)
TRACKS = {
    "battlemeadow": ("round3_raw/01_초원전투_1.mp3", BATTLE),
    "battlelibrary": ("round3_raw/02_서고전투_2.mp3", BATTLE),
    "battleforest": ("round3_raw/03_숲전투_1.mp3", BATTLE),
    "battlejungle": ("round2_raw/04_정글_전투.mp3", BATTLE),
    "battlecave": ("round2_raw/03_숲_전투.mp3", BATTLE),  # 처음 숲 곡으로 만들었는데 사용자가 동굴에 어울린다고 함 (#165)
    "bosslibrary": ("round4_raw/06_서고보스_까먹대왕.mp3", BOSS),
    "bossforest": ("round4_raw/07_숲보스_헷갈너구리.mp3", BOSS),
    "bossjungle": ("round2_raw/08_정글보스_빈칸두꺼비.mp3", BOSS),
    "bosscave": ("round2_raw/09_동굴보스_말줄임덩굴.mp3", BOSS),
}


def decode(path, rate=SR, channels=2):
    raw = subprocess.run([FF, "-nostdin", "-v", "error", "-i", path, "-f", "f32le", "-ac", str(channels), "-ar", str(rate), "-"], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32)
    return x.reshape(-1, channels).copy() if channels > 1 else x.copy()


def rms_db(x):
    return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)


def usable_range(x):
    """앞의 무음과 끝의 무음·페이드를 뺀 범위 (표본)"""
    m = np.abs(x).max(axis=1)
    start = int(np.argmax(m > 10 ** (-45 / 20)))
    w = SR // 4
    level = np.array([rms_db(x[i:i + w]) for i in range(0, len(x) - w, w)])
    good = np.nonzero(level >= np.median(level) - 10)[0]
    return start, int((good[-1] + 1) * w)


def features(mono, rate):
    """짧은 구간마다 대역별 음량 (리듬·화음이 비슷한지 보려고)"""
    n, hop = 1024, 256
    window = np.hanning(n)
    frames = np.array([np.abs(np.fft.rfft(mono[i:i + n] * window)) for i in range(0, len(mono) - n, hop)])
    edges = np.geomspace(4, frames.shape[1] - 1, 13).astype(int)
    bands = np.stack([frames[:, a:b + 1].sum(axis=1) for a, b in zip(edges[:-1], edges[1:])], axis=1)
    return np.log(bands + 1e-3), rate / hop


def find_loop(x):
    """곡 끝쪽에서 '여기서 처음으로 돌아가면 가장 자연스러운 자리'를 찾음. 돌려주는 값: 표본 위치, 비슷한 정도(0~1)"""
    rate = 11025
    mono = np.interp(np.arange(0, len(x), SR / rate), np.arange(len(x)), x.mean(axis=1))
    f, fps = features(mono, rate)
    length = int(COMPARE * fps)
    head = f[:length]
    head = (head - head.mean()) / (head.std() + 1e-9)
    best, best_score = None, -2.0
    for i in range(int(MIN_LENGTH * fps), len(f) - length):
        part = f[i:i + length]
        part = (part - part.mean()) / (part.std() + 1e-9)
        score = float((head * part).mean())
        if score >= best_score:  # 같으면 더 뒤쪽(긴 곡)
            best, best_score = i, score
    coarse = int(best / fps * SR)
    # 박자를 표본 단위까지 맞춤
    n, spread = SR, int(0.03 * SR)
    a = x[:n].mean(axis=1)
    b = x[max(coarse - spread, 0):coarse + n + spread].mean(axis=1)
    corr = np.correlate(b, a, "valid")
    return max(coarse - spread, 0) + int(np.argmax(corr)), best_score


def make_loop(x, end):
    fade = int(FADE * SR)
    t = np.linspace(0, 1, fade, endpoint=False)[:, None]
    out = x[:end].copy()
    out[:fade] = x[:fade] * np.sqrt(t) + x[end:end + fade] * np.sqrt(1 - t)  # 끝에서 넘어오면 x[end] 다음이 이어지는 것처럼
    return out


def lufs(path):
    log = subprocess.run([FF, "-nostdin", "-hide_banner", "-i", path, "-af", "ebur128", "-f", "null", "-"], capture_output=True, text=True, encoding="utf-8", errors="replace").stderr
    return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", log)[-1])


def write_wav(path, x):
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


if __name__ == "__main__":
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Builds", "music_pilot")
    os.makedirs(OUT, exist_ok=True)
    seams = []
    gap = np.zeros((SR, 2), dtype=np.float32)
    with tempfile.TemporaryDirectory() as tmp:
        for name, (file, target) in TRACKS.items():
            x = decode(os.path.join(src, file))
            start, stop = usable_range(x)
            x = x[start:stop]
            end, score = find_loop(x)
            loop = make_loop(x, end)
            probe = os.path.join(tmp, "probe.wav")
            write_wav(probe, loop)
            gain = 10 ** ((target - lufs(probe)) / 20)
            loop = loop * gain
            path = os.path.join(OUT, name + ".wav")
            write_wav(path, loop)
            six = 6 * SR
            seams += [loop[-six:], loop[:six], gap]
            jump = np.abs(loop[-1] - loop[0]).max()
            half = SR // 2
            print(f"{name:14s} {len(loop) / SR:5.1f}초  비슷함 {score:.2f}  음량 {lufs(path):.1f} LUFS  최고 {20 * np.log10(np.abs(loop).max()):5.1f}dB  "
                  f"이음새 앞뒤 {rms_db(loop[-half:]):5.1f}/{rms_db(loop[:half]):5.1f}dB  끝-처음 차이 {jump:.3f}  {os.path.getsize(path) // (1024 * 1024)}MB")
        whole = os.path.join(tmp, "seams.wav")
        write_wav(whole, np.concatenate(seams))
        subprocess.run([FF, "-nostdin", "-y", "-v", "error", "-i", whole, "-c:a", "libmp3lame", "-b:a", "192k", SEAMS], check=True)
    print("이음새 확인:", SEAMS)
