"""광고 시안 9 (#259, 1.3): 시안 8 + 보스 기믹 4장면 — 내 컴퓨터에서 합성 (Higgsfield를 쓰지 않음, 크레딧 0)

    python Tools/ad_motion/make_v9.py            → Builds/ad/wordrpg_ad_motion_v9.mp4
    python Tools/ad_motion/make_v9.py --check    → 이어 붙이는 무늬가 시안 8과 같은지 검사만
    가로판 = python Tools/ad_motion/make_landscape.py Builds/ad/wordrpg_ad_motion_v9.mp4 Builds/ad/wordrpg_ad_motion_v9_landscape.mp4
    (pip install imageio-ffmpeg numpy pillow)

입력: Builds/ad/wordrpg_ad_motion_v8.mp4 (시안 8 완성본 — 화면만 씀)
      Builds/ad/gimmick_{forget,shuffle,blank,vine}.mp4 (PlayMode AdCapture.BossGimmicks → python Tools/ad_motion/pack_real.py Builds/ad/gimmick)
구성: 시안 8의 0~19.5초(보스 카드 4장까지) → 기믹 4장면(까먹대왕·빈칸두꺼비 3박 · 나머지 2박, 음악 120BPM) → 시안 8의 마지막 화면.
이어 붙이는 곳은 edit.jsx의 Wipe와 같은 바둑판 무늬(같은 rng)로 덮어서 티가 안 나게. 소리는 mix_audio.py와 같은 팩 소리만.
다시 찍으면(BossGimmicks) 장면 시각이 바뀌므로 SHOTS의 start·효과 시각을 Builds/ad/gimmick/marks.txt·sfx.txt에 맞출 것.
"""
import math
import os
import subprocess
import sys

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
AD = os.path.join(ROOT, "Builds", "ad")
V8 = os.path.join(AD, "wordrpg_ad_motion_v8.mp4")
OUT = os.path.join(AD, "wordrpg_ad_motion_v9.mp4")
SILENT = os.path.join(AD, "wordrpg_ad_motion_v9_silent.mp4")
JUA = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "Jua-Regular.ttf")
SFX = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx")
MUSIC = os.path.join(ROOT, "Assets", "Resources", "Audio", "Music", "battle.ogg")
WHOOSH = os.path.join(HERE, "sfx", "whoosh2.wav")
FF = imageio_ffmpeg.get_ffmpeg_exe()

W, H, FPS = 1080, 1920, 30
CW, CH = 1080, 2346  # AdCapture 화면 크기
NAVY, GOLD, WHITE, OUTLINE = (0x12, 0x14, 0x24), (0xFF, 0xD1, 0x40), (0xF6, 0xF7, 0xFF), (0x0B, 0x0C, 0x18)
CORAL = (0xFF, 0x8A, 0x80)  # 게임의 까먹대왕 글 색 — 보스의 방해는 이 색
BEAT = 0.4992

# 시안 8 (edit.jsx): 보스 묶음 LATE2 = 5.39, 마지막 화면 앞 Wipe(seed 25) = 14.42 + LATE2
V8_KEEP = 585                     # 시안 8의 0~584 프레임(19.5초까지)을 그대로 — 보스 카드 4장이 다 나온 뒤
V8_TAIL = 601                     # 시안 8의 이 프레임부터 끝까지 (바둑판이 화면을 다 덮은 첫 프레임)
V8_WIPE_AT = 14.42 + 5.39
WIPE_IN = (27, 19.27)             # 보스 카드 → 기믹: 19.5초에 화면을 다 덮게 (seed, 시작 초)

# 기믹 장면: (영상, 영상 안 시작 초, 프레임 수, 확대, 위치 y, 이 줄 위는 지움, 큰 글, 보스 이름,
#            효과 [(종류, 영상 안 초, 세기)], 소리 [(팩 소리, 영상 안 초, 음량, 높이)])
# 시각 = Builds/ad/gimmick/marks.txt · sfx.txt (forget vanished 3.075 · shuffle 2.027 · blank answered 1.312 · vine vinegrow 2.984)
# 소리는 게임에서 그 순간 나는 소리. 빈칸 장면은 답한 뒤 약 0.8초면 문제 창이 닫히므로 2.05초 전에 끝냄
SHOTS = [
    ("forget", 2.25, 45, 1.25, -1412, 1512, "정답이 사라지고", "까먹대왕", [("punch", 3.075, 1.03)], [("evolvelight", 3.030, .7, 1)]),
    ("shuffle", 1.45, 30, 1.25, -1412, 1512, "보기가 뒤섞이고", "헷갈너구리", [("punch", 2.027, 1.03)], [("flee", 2.027, .6, 1.3)]),
    ("blank", 0.55, 45, 1.5, -1655, 1525, "빈칸이 생기고", "빈칸두꺼비", [("punch", 1.312, 1.04), ("burst", 1.312, 0)], [("correct", 1.321, .9, 1)]),
    ("vine", 2.65, 30, 1.0, 380, 135, "덩굴이 자라나요!", "말줄임덩굴", [("shake", 2.984, 14), ("punch", 2.984, 1.04)], [("vinegrow", 2.984, .45, 1)]),  # .8이면 최고 -5dB로 혼자 튐
]
SECTION = sum(s[2] for s in SHOTS)
TOTAL = V8_KEEP + SECTION + (702 - V8_TAIL)
SHIFT = (V8_KEEP + SECTION - V8_TAIL) / FPS   # 시안 8의 마지막 화면이 밀린 시간
# 이어 붙인 뒤 첫 프레임(시안 8의 601번)과 바둑판 진행이 같도록 시작 시각을 맞춤
WIPE_OUT = (25, (V8_KEEP + SECTION) / FPS - (V8_TAIL / FPS - V8_WIPE_AT))


def rng(seed):
    """edit.jsx의 rng (mulberry32) 그대로"""
    a = seed & 0xFFFFFFFF

    def nxt():
        nonlocal a
        a = (a + 0x6D2B79F5) & 0xFFFFFFFF
        t = a
        t = ((t ^ (t >> 15)) * (t | 1)) & 0xFFFFFFFF
        t = (t ^ ((t + (((t ^ (t >> 7)) * (t | 61)) & 0xFFFFFFFF)) & 0xFFFFFFFF)) & 0xFFFFFFFF
        return ((t ^ (t >> 14)) & 0xFFFFFFFF) / 4294967296
    return nxt


def wipe_tiles(seed):
    r, out = rng(seed), []
    for y in range(16):
        for x in range(9):
            d = (y / 16) * 0.14 + r() * 0.07
            gold = r() < 0.13
            out.append((x, y, d, gold))
    return out


def draw_wipe(img, seed, at, t):
    draw = ImageDraw.Draw(img)
    for x, y, d, gold in wipe_tiles(seed):
        if at + d <= t + 1e-6 < at + d + 0.3:
            draw.rectangle((x * 120, y * 120, x * 120 + 119, y * 120 + 119), fill=GOLD if gold else NAVY)


def ease_out(x):
    return 1 - (1 - min(max(x, 0), 1)) ** 3


def overshoot(x, c=2.2):
    x = min(max(x, 0), 1) - 1
    return 1 + (c + 1) * x ** 3 + c * x ** 2


_fonts = {}


def stamp_image(s, size, color, sw):
    key = (s, size, color, sw)
    if key not in _fonts:
        font = ImageFont.truetype(JUA, size)
        box = font.getbbox(s, stroke_width=sw, anchor="mm")
        w, h = box[2] - box[0] + 8, box[3] - box[1] + 8
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(img).text((w / 2, h / 2), s, font=font, fill=color, anchor="mm", stroke_width=sw, stroke_fill=OUTLINE)
        _fonts[key] = img
    return _fonts[key]


def draw_stamp(canvas, s, cx, cy, size, color, t, dur, start=2.1, sw=None):
    """edit.jsx Stamp: 크게 시작해 톡 튀며 자리 잡고, 끝 0.1초에 살짝 커지며 사라짐"""
    if t < 0 or t >= dur:
        return
    sw = sw if sw is not None else max(5, round(size * 0.075))
    base = stamp_image(s, size, color, sw)
    scale, alpha = 1.0, 1.0
    if t < 0.2:
        scale = start + (1 - start) * overshoot(t / 0.2)
    if dur > 0.45 and t > dur - 0.1:
        k = (t - (dur - 0.1)) / 0.1
        scale, alpha = 1 + 0.08 * k, 1 - k
    w, h = max(1, round(base.width * scale)), max(1, round(base.height * scale))
    img = base.resize((w, h), Image.BICUBIC)
    if alpha < 1:
        img.putalpha(img.getchannel("A").point(lambda v: int(v * alpha)))
    canvas.alpha_composite(img, (round(cx - w / 2), round(cy - h / 2)))


def burst(canvas, cx, cy, t, seed=71, n=18, spread=320, colors=(GOLD, WHITE)):
    """edit.jsx Burst: 네모 조각이 튀어 나갔다가 떨어지며 사라짐"""
    if not 0 <= t < 0.62:
        return
    r, draw = rng(seed), ImageDraw.Draw(canvas)
    for i in range(n):
        a = r() * math.pi * 2
        d = spread * (0.35 + r() * 0.65)
        s = [14, 20, 28, 36][int(r() * 4)]
        dx, dy = math.cos(a) * d, math.sin(a) * d
        x = cx + dx * ease_out(t / 0.6)
        y = cy + (dy * ease_out(t / 0.32) if t < 0.32 else dy + 150 * (t - 0.32) / 0.28)
        op = 1 if t < 0.38 else max(0, 1 - (t - 0.38) / 0.22)
        c = colors[i % len(colors)]
        draw.rectangle((x - s / 2, y - s / 2, x + s / 2, y + s / 2), fill=c + (int(255 * op),))


SHAKE = {"xy": [(0, 1, 0.7), (0.03, -0.85, -0.55), (0.07, 0.6, 0.5), (0.11, -0.4, -0.3), (0.15, 0.22, 0.18), (0.2, 0, 0)],
         "y": [(0, 0, 1), (0.05, 0, -0.55), (0.11, 0, 0.25), (0.18, 0, 0)]}


def shake_at(t, at, a, kind="y"):
    keys = SHAKE[kind]
    if t < at or t >= at + keys[-1][0]:
        return 0.0, 0.0
    u = t - at
    for (o0, x0, y0), (o1, x1, y1) in zip(keys, keys[1:]):
        if o0 <= u < o1:
            k = (u - o0) / (o1 - o0)
            return (x0 + (x1 - x0) * k) * a, (y0 + (y1 - y0) * k) * a
    return 0.0, 0.0


def punch_at(t, at, p):
    if t < at or t >= at + 0.16:
        return 1.0
    return p + (1 - p) * ease_out((t - at) / 0.16)


def scrim(h=520, solid=0.85):
    img = Image.new("RGBA", (W, h), NAVY + (0,))
    a = np.zeros((h, W), np.uint8)
    for y in range(h):
        f = 1.0 if y < h * solid else max(0.0, 1 - (y - h * solid) / (h * (1 - solid)))
        a[y, :] = int(255 * f)  # 꽉 막지 않으면 게임 카드 테두리가 비쳐 보임
    img.putalpha(Image.fromarray(a))
    return img


def read_frames(path, w, h):
    proc = subprocess.Popen([FF, "-v", "error", "-i", path, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE)
    size = w * h * 3
    while True:
        buf = proc.stdout.read(size)
        if len(buf) < size:
            break
        yield Image.frombuffer("RGB", (w, h), buf, "raw", "RGB", 0, 1)
    proc.wait()


def shot_frames(clip, start, count, z, y, crop_top, main, name, effects, sounds):
    effects = [(kind, at - start, v) for kind, at, v in effects]  # 장면 안 시각으로
    src = list(read_frames(os.path.join(AD, f"gimmick_{clip}.mp4"), CW, CH))
    first = round(start * FPS)
    assert first + count <= len(src), (clip, first + count, len(src))
    dur = count / FPS
    top = scrim()
    for i in range(count):
        t = i / FPS
        frame = src[first + i].copy()
        fill = frame.getpixel((8, CH - 4))
        if crop_top:
            ImageDraw.Draw(frame).rectangle((0, 0, CW, crop_top), fill=fill)
        s = 1 + 0.03 * t / dur  # 천천히 밀어 들어가기
        ox = oy = 0.0
        for kind, at, v in effects:
            if kind == "punch":
                s *= punch_at(t, at, v)
            elif kind == "shake":
                dx, dy = shake_at(t, at, v)
                ox, oy = ox + dx, oy + dy
        # 화면 위치: edit.jsx FieldShot (x = (W - W*z)/2, y, 폭 W*z) — 확대는 그 가운데를 기준으로
        mw, mh = CW * z, CH * z
        cx, cy = (W - mw) / 2 + mw / 2 + ox, y + mh / 2 + oy
        st = z * s
        x0, y0 = cx - CW * st / 2, cy - CH * st / 2
        canvas = frame.transform((W, H), Image.AFFINE, (1 / st, 0, -x0 / st, 0, 1 / st, -y0 / st), resample=Image.BICUBIC, fillcolor=fill).convert("RGBA")
        for kind, at, v in effects:
            if kind == "burst":
                burst(canvas, 540, round(y0 + 1610 * st), t - at)  # 빈칸 단어 자리 (게임 화면 y 1610)
        canvas.alpha_composite(top, (0, 0))
        draw_stamp(canvas, main, 540, 250, 112, CORAL, t, dur - 0.02)
        draw_stamp(canvas, name, 540, 378, 56, WHITE, t - 0.08, dur - 0.1, start=1.3, sw=5)
        yield canvas.convert("RGB")


def check():
    """시안 8의 바둑판(seed 25)을 이 파일의 rng로 그린 것과 비교 — 598(덮는 중)·601(다 덮음) 프레임의 금색 칸"""
    frames = {}
    for n, img in enumerate(read_frames(V8, W, H)):
        if n in (598, 601):
            frames[n] = img
    ok = True
    for n, img in frames.items():
        t = n / FPS
        want = {(x, y) for x, y, d, g in wipe_tiles(25) if g and V8_WIPE_AT + d <= t < V8_WIPE_AT + d + 0.3}
        got = set()
        for y in range(16):
            for x in range(9):
                r, g, b = img.getpixel((x * 120 + 60, y * 120 + 60))
                if r > 200 and g > 160 and b < 120:
                    got.add((x, y))
        print(f"frame {n}: 금색 칸 예상 {len(want)} · 실제 {len(got)} · 다른 칸 {sorted(want ^ got)}")
        ok &= want == got
    print("같음" if ok else "다름")
    return ok


def compose():
    enc = subprocess.Popen([FF, "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                            "-vf", "scale=out_color_matrix=bt709:out_range=tv", "-c:v", "libx264", "-crf", "16", "-preset", "medium",
                            "-pix_fmt", "yuv420p", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", SILENT],
                           stdin=subprocess.PIPE)
    written = 0

    def put(img):
        nonlocal written
        enc.stdin.write(img.tobytes())
        written += 1

    for n, img in enumerate(read_frames(V8, W, H)):
        if n < V8_KEEP:
            if n / FPS >= WIPE_IN[1]:
                img = img.copy()
                draw_wipe(img, WIPE_IN[0], WIPE_IN[1], n / FPS)
            put(img)
            continue
        if n == V8_KEEP:
            for shot in SHOTS:
                for img2 in shot_frames(*shot):
                    t = written / FPS
                    draw_wipe(img2, WIPE_IN[0], WIPE_IN[1], t)
                    draw_wipe(img2, WIPE_OUT[0], WIPE_OUT[1], t)
                    put(img2)
            print("기믹 장면", SECTION, "프레임")
        if n >= V8_TAIL:
            put(img)
    enc.stdin.close()
    enc.wait()
    assert written == TOTAL, (written, TOTAL)
    print("화면", written, "프레임", f"{written / FPS:.2f}초")


def mix():
    """mix_audio.py와 같은 소리 (시안 8 시간표) + 기믹 장면 소리, 마지막 화면 소리는 SHIFT만큼 뒤로"""
    late, late2 = 1.55, 5.39
    fin, adv = late + 6.45, late + 9.45
    pre = adv + BEAT * 10
    ev = [("click", .75, .5, 1), ("click", 1.48, .5, 1), ("correct", 2.21, 1, 1), ("combo", 3.47, .45, 1),
          ("combo", fin + (1.873 - 1.866), .6, 1), ("critical", fin + (2.864 - 1.866), .9, 1), ("hit", fin + (2.864 - 1.866), .9, 1),
          ("faint", fin + (4.427 - 1.866), .6, 1), ("evolve", adv + (1.731 - 0.24), .7, 1),
          ("preempt", pre + 5 / 30, .45, 1), ("correct", pre + 27 / 30, .9, 1)]
    ev += [(WHOOSH, late2 + t, 1.0, p) for t, p in zip((12.6, 12.85, 13.1, 13.35), (1.0, 1.06, 1.12, 1.19))]
    # 기믹 장면 — 까먹대왕 웃음 · 보기가 섞일 때 휙(게임과 같은 높이) · 빈칸이 채워질 때 · 덩굴
    at = V8_KEEP / FPS
    for shot in SHOTS:
        clip_start, count, sounds = shot[1], shot[2], shot[9]
        ev += [(name, at + t - clip_start, vol, pitch) for name, t, vol, pitch in sounds]
        at += count / FPS
    ev += [("levelup", late2 + 14.75 + SHIFT, 1, 1), ("click", late2 + 15.6 + SHIFT, .7, 1)]
    total = TOTAL / FPS
    args = [FF, "-nostdin", "-y", "-v", "error", "-i", SILENT, "-stream_loop", "-1", "-i", MUSIC]
    fc = [f"[1:a]atrim=0:{total},afade=t=out:st={total - 1.3}:d=1.3,volume=0.62,aresample=44100,aformat=channel_layouts=stereo[m]"]
    labels = ["[m]"]
    for i, (name, t, vol, pitch) in enumerate(ev):
        args += ["-i", name if os.path.isabs(name) else os.path.join(SFX, name + ".wav")]
        ms = int(t * 1000)
        shift = f"asetrate={int(44100 * pitch)},aresample=44100," if pitch != 1 else ""
        soften = "highshelf=f=2500:g=-8,lowpass=f=7000," if name == WHOOSH or name == "flee" else ""  # 높은음을 깎음 (사용자 #159·#165)
        fc.append(f"[{i + 2}:a]aresample=44100,{shift}{soften}aformat=channel_layouts=stereo,volume={vol},adelay={ms}|{ms}[s{i}]")
        labels.append(f"[s{i}]")
    fc.append("".join(labels) + f"amix=inputs={len(labels)}:duration=first:normalize=0,alimiter=limit=0.95[a]")
    args += ["-filter_complex", ";".join(fc), "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
             "-t", f"{total:.3f}", "-movflags", "+faststart", OUT]
    subprocess.run(args, check=True)
    os.remove(SILENT)
    print("완성", OUT, os.path.getsize(OUT) // (1024 * 1024), "MB", f"{total:.2f}초")


if __name__ == "__main__":
    if not check():
        sys.exit("바둑판 무늬가 시안 8과 다름 — rng·시각을 확인할 것")
    if "--check" not in sys.argv:
        compose()
        mix()
