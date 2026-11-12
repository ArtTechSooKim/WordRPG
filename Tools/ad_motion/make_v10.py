"""광고 시안 10 (#264): 짧은 판 12초 — 사용자 문구 '정답을 맞히면 → 공격할 수 있다! → 영어 단어로 세상을 구해라! → 영단어RPG'
내 컴퓨터에서 합성 (Higgsfield를 쓰지 않음, 크레딧 0). 시안 8은 평균 4초에 넘김 → 짧게, 글은 이어지는 한 문장으로.

    python Tools/ad_motion/make_v10.py           → Builds/ad/wordrpg_ad_motion_v10.mp4
    python Tools/ad_motion/make_v10.py --sheet   → 만든 영상을 0.25초 간격으로 한 장에 (확인용, Builds/ad/v10_sheet.png)
    (pip install imageio-ffmpeg numpy pillow)

입력 = 실제 게임 화면 (PlayMode AdCapture → pack_real.py · pack_adventure.py): real_quiz · real_finisher · real_chest · adv_raft · adv_cave,
       마지막 화면은 시안 8 완성본의 601번 프레임부터 75장 (바둑판이 화면을 다 덮은 뒤 — make_v9.py와 같은 방법).
음악 120BPM(BEAT)에 장면 경계·맞는 순간을 맞춤. 소리는 시안 8과 같은 팩 소리만.
다시 찍으면(AdCapture) 장면의 from(영상 안 초)을 Builds/ad/real/marks.txt·sfx.txt에 맞출 것.
"""
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_v9 import (AD, BEAT, CH, CW, FF, FPS, GOLD, H, MUSIC, NAVY, SFX, V8, V8_TAIL, V8_WIPE_AT, W, WHITE,  # noqa: E402
                     burst, draw_stamp, draw_wipe, ease_out, punch_at, read_frames)

OUT = os.path.join(AD, "wordrpg_ad_motion_v10.mp4")
SILENT = os.path.join(AD, "wordrpg_ad_motion_v10_silent.mp4")
GREEN, RED = (0x40, 0xBF, 0x66), (0xFF, 0x5A, 0x52)
END_FRAMES = 75                    # 시안 8 마지막 화면 2.5초 (앱스토어 알약이 1.5초 보임)

# edit.jsx Shake 종류 (오프셋 초, x, y) — s는 길이 배율
SHAKE = {"xy": [(0, 1, 0.7), (0.03, -0.85, -0.55), (0.07, 0.6, 0.5), (0.11, -0.4, -0.3), (0.15, 0.22, 0.18), (0.2, 0, 0)],
         "x": [(0, 1, 0), (0.04, -0.7, 0), (0.09, 0.4, 0), (0.14, -0.2, 0), (0.2, 0, 0)],
         "y": [(0, 0, 1), (0.05, 0, -0.55), (0.11, 0, 0.25), (0.18, 0, 0)]}


def shake_at(t, at, a, kind="xy", s=1.0):
    keys = [(o * s, x, y) for o, x, y in SHAKE[kind]]
    u = t - at
    if u < 0 or u >= keys[-1][0]:
        return 0.0, 0.0
    for (o0, x0, y0), (o1, x1, y1) in zip(keys, keys[1:]):
        if o0 <= u < o1:
            k = (u - o0) / (o1 - o0)
            return (x0 + (x1 - x0) * k) * a, (y0 + (y1 - y0) * k) * a
    return 0.0, 0.0


def band(h, top=True, stops=((0, 1.0), (0.88, 1.0), (1, 0))):
    """위(또는 아래) 남색 띠 — stops = (위치 0~1, 불투명도). edit.jsx Scrim은 (0 .96, .6 .82, 1 0)"""
    a = np.zeros((h, W), np.uint8)
    for y in range(h):
        f = y / max(1, h - 1)
        for (p0, o0), (p1, o1) in zip(stops, stops[1:]):
            if p0 <= f <= p1:
                a[y, :] = int(255 * (o0 + (o1 - o0) * (f - p0) / max(1e-6, p1 - p0)))
                break
    if not top:
        a = a[::-1]
    img = Image.new("RGBA", (W, h), NAVY + (0,))
    img.putalpha(Image.fromarray(a))
    return img, (0 if top else H - h)


SOLID = ((0, 1.0), (0.88, 1.0), (1, 0))
SCRIM = ((0, 0.96), (0.6, 0.82), (1, 0))


def t_of(beats):
    return round(beats * BEAT * FPS) / FPS


# 장면: 영상, 영상 안 시작 초, 끝 박자(누적), 확대 z, 위치 y, 천천히 밀기, 띠, 효과
#   효과 = ("punch", 장면 안 초, 배율) · ("shake", 초, 세기, 종류, 길이 배율) · ("burst", 초, cx, cy, seed, n, spread, 색들) · ("flash", 초, 색, 불투명, 길이)
# 시각 = Builds/ad/real/marks.txt (quiz answered 2.206 · critical 4.070 / finisher 결정타 2.864 / chest confirm 1.724)
SHOTS = [
    # 1 '정답을 맞히면' — 푸는 동안은 화면을 가만히 (시안 8 사용자 의견). 4박째(1.997초)에 정답
    ("real_quiz", 0.209, 5, 1.03, -835, 0, [(400, True, SOLID)],
     [("punch", 1.997, 1.03), ("burst", 1.997, 540, 1071, 11, 16, 360, (GREEN, WHITE, GOLD))]),
    # 2 '공격할 수 있다!' — 같은 전투의 크리티컬 (베기라 좌우로 탁) → 7번째 정답의 결정타
    ("real_quiz", 3.571, 7, 1.6, 216, 0, [(470, True, SCRIM), (380, False, SCRIM)],
     [("shake", 0.499, 46, "x", 1.2), ("punch", 0.499, 1.08), ("flash", 0.499, GOLD, 0.45, 0.2),
      ("burst", 0.499, 255, 960, 32, 26, 560, (RED, GOLD, WHITE))]),
    # 보스 카드가 글 띠 아래로 오게 시안 8(y -232)보다 556 내림 — 결정타 글은 게임 화면 그대로
    ("real_finisher", 1.865, 13, 1.04, 324, 0, [(470, True, SOLID), (380, False, SCRIM)],
     [("flash", 0.0, WHITE, 0.35, 0.12), ("shake", 0.999, 52, "xy", 1.5), ("shake", 1.4, 20, "y", 1.0), ("punch", 0.999, 1.09),
      ("burst", 0.999, 540, 844, 41, 44, 760, (GOLD, RED, WHITE))]),
    # 3 '영어 단어로 세상을 구해라!' — 모험은 잔잔하게: 상자가 열리는 순간만 톡
    ("real_chest", 1.224, 15, 1.3, -650, 0.05, [(470, True, ((0, 0.98), (0.88, 0.98), (1, 0))), (320, False, SCRIM)],
     [("punch", 0.5, 1.05), ("burst", 0.5, 769, 562, 51, 30, 420, (GOLD, WHITE))]),
    ("adv_raft", 0.4, 17, 1.3, -650, 0.04, [(470, True, ((0, 0.98), (0.88, 0.98), (1, 0))), (320, False, SCRIM)],
     [("shake", 0.2, 12, "y", 1.0)]),
    ("adv_cave", 0.05, 19, 1.3, -650, 0.05, [(470, True, ((0, 0.98), (0.88, 0.98), (1, 0))), (320, False, SCRIM)], []),
]

# 글: (문구, cx, cy, 크기, 색, 시작 초, 끝 초, 처음 배율)
STAMPS = [
    ("정답을 맞히면", 540, 250, 120, GOLD, -0.08, t_of(5) - 0.02, 1.3),   # 첫 장면(미리보기)부터 글이 보이게
    ("공격할 수 있다!", 540, 280, 124, GOLD, t_of(5) + 0.05, t_of(13) - 0.02, 2.1),
    ("영어 단어로", 540, 178, 92, WHITE, t_of(13) + 0.05, t_of(19) - 0.02, 1.8),
    ("세상을 구해라!", 540, 318, 128, GOLD, t_of(13) + 0.17, t_of(19) - 0.02, 2.1),
]
J = t_of(19)                                         # 마지막 화면으로 넘어가는 시각
# 바둑판 (seed, 시작 초) — 덮은 동안(시작 + 0.21~0.30초)에 장면이 바뀜. 마지막은 시안 8의 601번 프레임과 이어지게
WIPES = [(21, t_of(5) - 0.25), (23, t_of(13) - 0.22), (25, J - (V8_TAIL / FPS - V8_WIPE_AT))]
TOTAL = round(J * FPS) + END_FRAMES


def clip_frames(name, start, count):
    first = round(start * FPS)
    out = 0
    for n, img in enumerate(read_frames(os.path.join(AD, name + ".mp4"), CW, CH)):
        if first <= n < first + count:   # 끝까지 읽음 — 중간에 끊으면 ffmpeg가 Broken pipe를 찍음
            yield img
            out += 1
    assert out == count, (name, start, count, out)


def shot_frames(index):
    clip, start, end_beat, z, y, push, bands, effects = SHOTS[index]
    at = t_of(SHOTS[index - 1][2]) if index else 0.0
    end = t_of(end_beat)
    count = round(end * FPS) - round(at * FPS)
    dur = count / FPS
    layers = [band(h, top, stops) for h, top, stops in bands]
    for i, frame in enumerate(clip_frames(clip, start, count)):
        t = i / FPS
        fill = frame.getpixel((8, CH - 4))
        s = 1 + push * t / dur
        ox = oy = 0.0
        for e in effects:
            if e[0] == "punch":
                s *= punch_at(t, e[1], e[2])
            elif e[0] == "shake":
                dx, dy = shake_at(t, e[1], e[2], e[3], e[4])
                ox, oy = ox + dx, oy + dy
        # edit.jsx FieldShot: 폭 W*z, x 가운데, 위 끝 y — 밀기·튀어오름은 그 가운데를 기준으로
        mw, mh = CW * z, CH * z
        cx, cy = W / 2 + ox, y + mh / 2 + oy
        st = z * s
        x0, y0 = cx - CW * st / 2, cy - CH * st / 2
        canvas = frame.transform((W, H), Image.AFFINE, (1 / st, 0, -x0 / st, 0, 1 / st, -y0 / st),
                                 resample=Image.BICUBIC, fillcolor=fill).convert("RGBA")
        for e in effects:
            if e[0] == "burst":
                burst(canvas, e[2], e[3], t - e[1], seed=e[4], n=e[5], spread=e[6], colors=e[7])
            elif e[0] == "flash" and e[1] <= t < e[1] + e[4]:
                o = e[3] * (1 - (t - e[1]) / e[4])
                canvas.alpha_composite(Image.new("RGBA", (W, H), e[2] + (int(255 * o),)))
        for img, top in layers:
            canvas.alpha_composite(img, (0, top))
        yield at + t, canvas


def compose():
    enc = subprocess.Popen([FF, "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                            "-vf", "scale=out_color_matrix=bt709:out_range=tv", "-c:v", "libx264", "-crf", "16", "-preset", "medium",
                            "-pix_fmt", "yuv420p", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", SILENT],
                           stdin=subprocess.PIPE)
    written = 0
    for k in range(len(SHOTS)):
        for t, canvas in shot_frames(k):
            for text, cx, cy, size, color, a, b, start in STAMPS:
                draw_stamp(canvas, text, cx, cy, size, color, t - a, b - a, start=start)
            canvas = canvas.convert("RGB")
            for seed, at in WIPES:
                draw_wipe(canvas, seed, at, t)
            enc.stdin.write(canvas.tobytes())
            written += 1
    assert written == round(J * FPS), (written, J)
    for n, img in enumerate(read_frames(V8, W, H)):
        if V8_TAIL <= n < V8_TAIL + END_FRAMES:
            enc.stdin.write(img.tobytes())
            written += 1
    enc.stdin.close()
    enc.wait()
    assert written == TOTAL, (written, TOTAL)
    print("화면", written, "프레임", f"{written / FPS:.2f}초")


def mix():
    """게임에서 그 순간 나는 소리 (시안 8 mix_audio.py와 같은 소리·음량)"""
    def at(shot, clip_t):
        return (t_of(SHOTS[shot - 1][2]) if shot else 0.0) + clip_t - SHOTS[shot][1]
    v8_end = V8_TAIL / FPS                           # 시안 8 시간 → 이 영상 시간
    ev = [("correct", at(0, 2.214), 1, 1), ("combo", at(1, 4.070), .45, 1),
          ("combo", at(2, 1.873), .6, 1), ("critical", at(2, 2.864), .9, 1), ("hit", at(2, 2.864), .9, 1), ("faint", at(2, 4.427), .6, 1),
          ("evolve", at(3, 1.731), .7, 1),
          ("levelup", J + (5.39 + 14.75) - v8_end, 1, 1), ("click", J + (5.39 + 15.6) - v8_end, .7, 1)]
    total = TOTAL / FPS
    args = [FF, "-nostdin", "-y", "-v", "error", "-i", SILENT, "-stream_loop", "-1", "-i", MUSIC]
    fc = [f"[1:a]atrim=0:{total},afade=t=out:st={total - 1.0}:d=1.0,volume=0.62,aresample=44100,aformat=channel_layouts=stereo[m]"]
    labels = ["[m]"]
    for i, (name, t, vol, pitch) in enumerate(ev):
        args += ["-i", os.path.join(SFX, name + ".wav")]
        ms = int(t * 1000)
        fc.append(f"[{i + 2}:a]aresample=44100,aformat=channel_layouts=stereo,volume={vol},adelay={ms}|{ms}[s{i}]")
        labels.append(f"[s{i}]")
    fc.append("".join(labels) + f"amix=inputs={len(labels)}:duration=first:normalize=0,alimiter=limit=0.95[a]")
    args += ["-filter_complex", ";".join(fc), "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
             "-t", f"{total:.3f}", "-movflags", "+faststart", OUT]
    subprocess.run(args, check=True)
    os.remove(SILENT)
    print("완성", OUT, os.path.getsize(OUT) // (1024 * 1024), "MB", f"{total:.2f}초")
    for name, t, *_ in ev:
        print(f"  소리 {name} {t:.3f}")


def sheet():
    out = os.path.join(AD, "v10_sheet.png")
    frames = [img.resize((216, 384)) for n, img in enumerate(read_frames(OUT, W, H)) if n % 8 == 0]
    cols = 8
    rows = (len(frames) + cols - 1) // cols
    s = Image.new("RGB", (216 * cols, 384 * rows), (255, 255, 255))
    for i, f in enumerate(frames):
        s.paste(f, (216 * (i % cols), 384 * (i // cols)))
        ImageDraw.Draw(s).text((216 * (i % cols) + 4, 384 * (i // cols) + 370), f"{i * 8 / FPS:.2f}", fill=(255, 0, 0))
    s.save(out)
    print("확인용", out)


if __name__ == "__main__":
    if "--sheet" in sys.argv:
        sheet()
    else:
        compose()
        mix()
        sheet()
