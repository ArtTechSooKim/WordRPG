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
BATTLE, BOSS = -21.0, -20.0  # 음량(LUFS). #166에 -19·-18로 올렸더니 기술 소리가 묻혀서 낮춤 (#167 사용자). 지역 음악은 -13~-23
FADE = 0.4         # 반복 이음새를 겹치는 길이(초)
MIN_LENGTH = 35.0  # 이음새를 찾을 때 곡이 이보다 짧아지지 않게(초)
COMPARE = 4.0      # 이음새 뒤와 곡 처음을 비교하는 길이(초)

# 게임 이름(Music 열거형 소문자) → (원본, 음량)
TRACKS = {
    "battlemeadow": ("round3_raw/01_초원전투_1.mp3", BATTLE),
    "battlelibrary": ("round3_raw/02_서고전투_2.mp3", BATTLE),
    "battleforest": ("round6_raw/03_숲전투_4.mp3", BATTLE),  # 처음 고른 03_숲전투_1은 너무 통통 튐 (#170·#171 사용자). 03_숲전투_3은 나중에 쓸 곡
    "battlejungle": ("round2_raw/04_정글_전투.mp3", BATTLE),
    "battlecave": ("round2_raw/03_숲_전투.mp3", BATTLE),  # 처음 숲 곡으로 만들었는데 사용자가 동굴에 어울린다고 함 (#165)
    "battlecave2": ("round2_raw/05_동굴_전투.mp3", BATTLE),  # 동굴은 두 곡을 번갈아 (#171 사용자)
    "bosslibrary": ("round4_raw/06_서고보스_까먹대왕.mp3", BOSS),
    "bossforest": ("round4_raw/07_숲보스_헷갈너구리.mp3", BOSS),
    "bossjungle": ("round2_raw/08_정글보스_빈칸두꺼비.mp3", BOSS),
    "bosscave": ("round2_raw/09_동굴보스_말줄임덩굴.mp3", BOSS),
}
# 곡을 손본 것 (#167 사용자: 초원은 이음새가 튀고, 지금보다 덜 평온하게) — speed = 빠르기(음도 같이 올라감) · crush = 8비트 가공 · careful = 이음새를 더 꼼꼼히 찾음
# 다른 곡은 사용자가 이음새를 확인했으므로 careful을 켜지 않음 (켜면 반복 자리가 바뀜)
LIGHT, STRONG = (8, 3, 0.45, 10000), (6, 5, 0.8, 8000)  # 8비트 가공: 비트 · 건너뛰는 표본 · 섞는 비율 · 고음 자르기 Hz. 사막 A·B는 STRONG (#165)
TWEAKS = {
    "battlemeadow": {"speed": 1.18, "careful": True},  # 사용자가 후보 A·B·C 중 B를 고름 (#168)
    "battleforest": {"careful": True},
    "battlecave2": {"careful": True},
}


def decode(path, rate=SR, channels=2, speed=1.0):
    speedup = ["-af", f"asetrate={int(SR * speed)},aresample={SR}"] if speed != 1.0 else []
    raw = subprocess.run([FF, "-nostdin", "-v", "error", "-i", path] + speedup + ["-f", "f32le", "-ac", str(channels), "-ar", str(rate), "-"], capture_output=True, check=True).stdout
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


def crush(x, level):
    bits, hold, mix, cut = level
    q = 2 ** (bits - 1)
    y = np.round(x[(np.arange(len(x)) // hold) * hold] * q) / q
    y = mix * y + (1 - mix) * x
    pcm = (np.clip(y, -1, 1) * 32767).astype(np.int16).tobytes()
    raw = subprocess.run([FF, "-nostdin", "-v", "error", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-af", f"lowpass=f={cut}", "-f", "f32le", "-"], input=pcm, capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).copy()


def find_loop_careful(x, max_start=8.0):
    """시작 자리도 함께 찾고(앞의 도입을 건너뜀), 음량·대역이 다른 자리는 점수를 깎음 (find_loop는 모양만 봐서 음량이 달라도 높게 나옴)"""
    rate = 11025
    mono = np.interp(np.arange(0, len(x), SR / rate), np.arange(len(x)), x.mean(axis=1))
    f, fps = features(mono, rate)
    length = int(COMPARE * fps)
    ends = np.arange(int(MIN_LENGTH * fps), len(f) - length)
    parts = np.stack([f[e:e + length] for e in ends])
    means = parts.mean(axis=1)
    norm = (parts - parts.mean(axis=(1, 2), keepdims=True)) / (parts.std(axis=(1, 2), keepdims=True) + 1e-9)
    candidates = []
    for s in range(0, int(max_start * fps), 5):
        head = f[s:s + length]
        h = (head - head.mean()) / (head.std() + 1e-9)
        scores = (norm * h).mean(axis=(1, 2))
        # 대역별 음량 차이 1dB마다 0.05점 깎음 (로그 값 × 8.686 = dB)
        scores = scores - 0.05 * 8.686 * np.abs(means - head.mean(axis=0)).mean(axis=1)
        scores = np.where(ends >= s + int(MIN_LENGTH * fps), scores, -9.0)
        for i in np.argsort(scores)[-3:]:
            candidates.append((float(scores[i]), s, int(ends[i])))
    # 점수가 높은 후보를 파형까지 맞춰 보고, 겹치는 구간이 실제로 닮고 음량이 같은 자리를 고름
    best = None
    fade, half = int(FADE * SR), SR // 2
    for score, s, e in sorted(candidates, reverse=True)[:24]:
        start, coarse = int(s / fps * SR), int(e / fps * SR)
        n, spread = SR, int(0.03 * SR)
        a = x[start:start + n].mean(axis=1)
        b = x[max(coarse - spread, 0):coarse + n + spread].mean(axis=1)
        end = max(coarse - spread, 0) + int(np.argmax(np.correlate(b, a, "valid")))
        u, v = x[start:start + fade], x[end:end + fade]
        rho = float((u * v).sum() / (np.sqrt((u ** 2).sum() * (v ** 2).sum()) + 1e-9))
        gap = abs(rms_db(x[start:start + half]) - rms_db(x[end:end + half]))
        total = score + 0.3 * rho - 0.05 * gap
        if best is None or total > best[0]:
            best = (total, start, end, score)
    return best[1], best[2], best[3]


def make_loop_even(x, start, end):
    """겹치는 두 구간이 닮았으면 합이 커지므로(같은 소리면 +3dB) 닮은 정도만큼 줄여 음량을 일정하게 (#167 초원 이음새가 튄 원인)"""
    fade = int(FADE * SR)
    t = np.linspace(0, 1, fade, endpoint=False)[:, None]
    u, v = x[start:start + fade], x[end:end + fade]
    rho = float((u * v).sum() / (np.sqrt((u ** 2).sum() * (v ** 2).sum()) + 1e-9))
    a, b = np.sqrt(t), np.sqrt(1 - t)
    out = x[start:end].copy()
    out[:fade] = (u * a + v * b) / np.sqrt(1 + 2 * rho * a * b)
    return out, rho


def process(file, target, src, speed=1.0, crush_level=None, careful=False):
    """원본 하나 → 반복되는 곡 (음량까지 맞춤). 돌려주는 값: 곡, 비슷한 정도, 설명"""
    x = decode(os.path.join(src, file), speed=speed)
    if crush_level:
        x = crush(x, crush_level)
    start, stop = usable_range(x)
    x = x[start:stop]
    if careful:
        s, end, score = find_loop_careful(x)
        loop, rho = make_loop_even(x, s, end)
        note = f"시작 {s / SR:.2f}초 · 겹침 닮은 정도 {rho:.2f}"
    else:
        end, score = find_loop(x)
        loop, note = make_loop(x, end), ""
    with tempfile.TemporaryDirectory() as tmp:
        probe = os.path.join(tmp, "probe.wav")
        write_wav(probe, loop)
        loop = loop * 10 ** ((target - lufs(probe)) / 20)
    return loop, score, note


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
            tweak = TWEAKS.get(name, {})
            loop, score, note = process(file, target, src, tweak.get("speed", 1.0), tweak.get("crush"), tweak.get("careful", False))
            path = os.path.join(OUT, name + ".wav")
            write_wav(path, loop)
            six = 6 * SR
            seams += [loop[-six:], loop[:six], gap]
            jump = np.abs(loop[-1] - loop[0]).max()
            half = SR // 2
            print(f"{name:14s} {len(loop) / SR:5.1f}초  비슷함 {score:.2f}  음량 {lufs(path):.1f} LUFS  최고 {20 * np.log10(np.abs(loop).max()):5.1f}dB  "
                  f"이음새 앞뒤 {rms_db(loop[-half:]):5.1f}/{rms_db(loop[:half]):5.1f}dB  끝-처음 차이 {jump:.3f}  {os.path.getsize(path) // (1024 * 1024)}MB  {note}")
        whole = os.path.join(tmp, "seams.wav")
        write_wav(whole, np.concatenate(seams))
        subprocess.run([FF, "-nostdin", "-y", "-v", "error", "-i", whole, "-c:a", "libmp3lame", "-b:a", "192k", SEAMS], check=True)
    print("이음새 확인:", SEAMS)
