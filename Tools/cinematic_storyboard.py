"""입장 시네마틱 키 장면 v2 — 구도 스케치 (#128 사용자: "v1 6장은 전환 느낌이지 시네마틱이 아님").

옆에서 보는 장면 · 와이드/로우 앵글/클로즈업/역광 같은 영화 구도를 색 덩어리·실루엣·빛으로 그리고 도트처럼 줄였다 키움 →
Flow img to img의 입력 그림 (AI가 이 구도·빛을 살려 완성 그림·영상으로). 글자(자막)는 넣지 않음.
    python Tools/cinematic_storyboard.py   → Builds/cinematic_v2/
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts")
OUT = os.path.join(ROOT, "Builds", "cinematic_v2")
W, H = 1080, 1920
PIXEL = 4  # 도트 느낌: 1/4로 줄였다 키움
LETTERS = list("AbcDeFgHiJkLmNoPqRsTuVwXyZ") + list("가나다라마바사아자말글뜻꿈빛책")


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def sky(top, bottom, horizon=1.0):
    im = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(im)
    for y in range(H):
        t = min(1.0, y / (H * horizon))
        d.line((0, y, W, y), fill=lerp(top, bottom, t) + (255,))
    return im


def ridge(im, base_y, amp, color, rng, step=60, jag=0.5):
    """언덕·산 능선 실루엣"""
    pts = [(0, H)]
    x, phase = 0, rng.random() * 6
    while x <= W + step:
        y = base_y - amp * (0.5 + 0.5 * math.sin(x / 260 + phase)) - rng.uniform(0, amp * jag * 0.4)
        pts.append((x, y))
        x += step
    pts.append((W, H))
    ImageDraw.Draw(im).polygon(pts, fill=color)


def glow(im, box, color, blur, layers=1):
    g = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(g).ellipse(box, fill=color)
    g = g.filter(ImageFilter.GaussianBlur(blur))
    for _ in range(layers):
        im.alpha_composite(g)


def letters(im, count, rng, area, colors, size=(26, 64), alpha=230):
    g = Image.new("RGBA", im.size, (0, 0, 0, 0))
    s = Image.new("RGBA", im.size, (0, 0, 0, 0))
    gd, sd = ImageDraw.Draw(g), ImageDraw.Draw(s)
    for _ in range(count):
        font = ImageFont.truetype(os.path.join(FONTS, "Jua-Regular.ttf"), rng.randint(*size))
        x, y = rng.randint(area[0], area[2]), rng.randint(area[1], area[3])
        ch = rng.choice(LETTERS)
        gd.text((x, y), ch, font=font, fill=rng.choice(colors) + (220,), anchor="mm")
        sd.text((x, y), ch, font=font, fill=(255, 252, 235, alpha), anchor="mm")
    g = g.filter(ImageFilter.GaussianBlur(12))
    im.alpha_composite(g)
    im.alpha_composite(g)
    im.alpha_composite(s)


def rays(im, origin, angles, length, color, width=90, blur=30):
    r = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(r)
    ox, oy = origin
    for a in angles:
        a1, a2 = math.radians(a - 2.5), math.radians(a + 2.5)
        d.polygon([(ox, oy), (ox + math.cos(a1) * length, oy + math.sin(a1) * length),
                   (ox + math.cos(a2) * length, oy + math.sin(a2) * length)], fill=color)
    im.alpha_composite(r.filter(ImageFilter.GaussianBlur(blur)))


def tower(im, x, base, height, width, color, window):
    """언덕 위 오래된 서고 탑: 몸통 + 뾰족 지붕 + 불 켜진 창"""
    d = ImageDraw.Draw(im)
    d.rectangle((x - width // 2, base - height, x + width // 2, base), fill=color)
    d.polygon([(x - width // 2 - 24, base - height), (x + width // 2 + 24, base - height), (x, base - height - width * 1.3)], fill=color)
    for i in range(3):
        wy = base - height + 60 + i * 110
        d.rectangle((x - 14, wy, x + 14, wy + 40), fill=window)
    glow(im, (x - 90, base - height - 40, x + 90, base - height + 360), window[:3] + (90,), 40)


def houses(im, y, count, rng, color, window):
    d = ImageDraw.Draw(im)
    x = 20
    for _ in range(count):
        w = rng.randint(90, 150)
        h = rng.randint(70, 120)
        d.rectangle((x, y - h, x + w, y), fill=color)
        d.polygon([(x - 12, y - h), (x + w + 12, y - h), (x + w / 2, y - h - w * 0.55)], fill=color)
        if rng.random() < 0.7:
            d.rectangle((x + w / 2 - 12, y - h + 25, x + w / 2 + 12, y - h + 55), fill=window)
        x += w + rng.randint(20, 60)
        if x > W:
            break


def person(im, x, y, scale, color, look_up=False):
    d = ImageDraw.Draw(im)
    r = 14 * scale
    hy = y - 70 * scale
    d.ellipse((x - r, hy - r - (6 * scale if look_up else 0), x + r, hy + r - (6 * scale if look_up else 0)), fill=color)
    d.rounded_rectangle((x - 18 * scale, hy + r, x + 18 * scale, y), radius=int(10 * scale), fill=color)


def pixelate(im):
    small = im.convert("RGB").resize((W // PIXEL, H // PIXEL), Image.LANCZOS)
    small = small.quantize(colors=48, method=Image.MEDIANCUT).convert("RGB")
    return small.resize((W, H), Image.NEAREST)


def shot1():
    """① 와이드 — 말로 이루어진 세계: 새벽 하늘에 별 대신 글자, 언덕 위 서고 탑, 아래 마을, 첫 햇빛"""
    rng = random.Random(11)
    im = sky((28, 34, 92), (255, 196, 140), 0.72)
    glow(im, (W * 0.55, H * 0.52, W * 1.15, H * 0.82), (255, 230, 170, 200), 90, 2)  # 떠오르는 해
    letters(im, 40, rng, (40, 60, W - 40, int(H * 0.55)), [(255, 230, 140), (170, 220, 255), (255, 190, 220)], (24, 52))
    ridge(im, H * 0.70, 90, (96, 92, 140, 255), rng)
    ridge(im, H * 0.76, 70, (60, 72, 96, 255), rng)
    tower(im, int(W * 0.30), int(H * 0.705), 420, 110, (34, 36, 58, 255), (255, 214, 120, 255))
    ridge(im, H * 0.84, 50, (40, 56, 52, 255), rng, jag=0.2)
    houses(im, int(H * 0.88), 8, rng, (26, 34, 34, 255), (255, 200, 110, 255))
    ridge(im, H * 0.95, 30, (18, 26, 24, 255), rng, jag=0.1)
    rays(im, (W * 0.85, H * 0.67), range(185, 260, 12), 1400, (255, 230, 170, 40))
    return im


def shot2():
    """② 미디엄 — 서고 안: 높은 책장 사이, 펼친 책에서 빛나는 글자가 반딧불처럼 흘러나옴, 창으로 빛줄기"""
    rng = random.Random(12)
    im = sky((24, 18, 30), (70, 46, 40))
    d = ImageDraw.Draw(im)
    for side in (0, 1):  # 양옆 책장 (안쪽으로 좁아지는 원근)
        for i in range(9):
            y0 = 120 + i * 190
            for j in range(10):
                x0 = 30 + j * 34 if side == 0 else W - 30 - (j + 1) * 34
                c = rng.choice([(110, 46, 40), (60, 70, 110), (120, 96, 50), (50, 90, 70)])
                d.rectangle((x0, y0, x0 + 28, y0 + 150), fill=c + (255,))
            d.rectangle((0 if side == 0 else W - 380, y0 + 150, 380 if side == 0 else W, y0 + 172), fill=(44, 28, 22, 255))
    d.polygon([(380, 0), (W - 380, 0), (W - 380, H), (380, H)], fill=(40, 28, 30, 255))
    d.rectangle((440, 220, W - 440, 700), fill=(150, 170, 210, 255))  # 높은 창
    rays(im, (W / 2, 260), range(70, 112, 7), 1700, (255, 236, 190, 60), blur=26)
    d.polygon([(330, 1500), (W - 330, 1500), (W - 260, 1640), (260, 1640)], fill=(84, 54, 36, 255))  # 독서대
    d.polygon([(400, 1430), (W / 2, 1470), (W - 400, 1430), (W - 380, 1520), (W / 2, 1550), (380, 1520)], fill=(245, 236, 210, 255))  # 펼친 책
    glow(im, (300, 1200, W - 300, 1600), (255, 214, 120, 150), 70, 2)
    letters(im, 36, rng, (300, 500, W - 300, 1420), [(255, 220, 120), (150, 220, 255)], (22, 48))
    return im


def shot3():
    """③ 로우 앵글 — 말이 사라지다: 하늘이 하얀 빈 종이처럼 찢어지며 번지고, 마을 간판·책의 글자가 실처럼 하늘로 빨려감"""
    rng = random.Random(13)
    im = sky((120, 130, 150), (200, 190, 170))
    tear = Image.new("RGBA", im.size, (0, 0, 0, 0))
    pts = [(0, 0), (W, 0)]
    for x in range(W, -1, -40):
        pts.append((x, H * 0.36 + 120 * math.sin(x / 180) + rng.randint(-40, 40)))
    ImageDraw.Draw(tear).polygon(pts, fill=(252, 250, 244, 255))
    glow(im, (-200, -200, W + 200, H * 0.55), (255, 255, 250, 160), 80)
    im.alpha_composite(tear)
    d = ImageDraw.Draw(im)
    for _ in range(26):  # 땅에서 하늘로 빨려가는 글자 실
        x = rng.randint(80, W - 80)
        y0 = rng.randint(int(H * 0.62), int(H * 0.82))
        d.line((x, y0, x + rng.randint(-60, 60), H * 0.38), fill=(255, 240, 200, 120), width=3)
    letters(im, 30, rng, (60, int(H * 0.38), W - 60, int(H * 0.75)), [(255, 230, 150)], (22, 46), alpha=200)
    # 올려다보는 구도: 가까운 지붕·간판 실루엣이 아래에서 크게
    d.polygon([(0, H), (0, H * 0.70), (260, H * 0.60), (520, H * 0.72), (520, H)], fill=(34, 32, 40, 255))
    d.polygon([(560, H), (560, H * 0.74), (820, H * 0.62), (W, H * 0.70), (W, H)], fill=(28, 26, 34, 255))
    d.rectangle((140, H * 0.73, 380, H * 0.79), fill=(70, 56, 48, 255))  # 빈 간판 (글자가 빠져나감)
    person(im, 700, int(H * 0.97), 3.4, (12, 12, 18, 255), look_up=True)
    return im


def shot4():
    """④ 하이 앵글 — 백지룡의 그림자: 밤 마을 위를 거대한 날개의 그림자가 덮고 지나감 (날개 끝만, 얼굴 없음)"""
    rng = random.Random(14)
    im = sky((14, 16, 34), (40, 44, 80))
    letters(im, 18, rng, (40, 40, W - 40, int(H * 0.4)), [(170, 190, 255)], (18, 34), alpha=150)
    ridge(im, H * 0.55, 60, (30, 34, 56, 255), rng)
    houses(im, int(H * 0.70), 9, rng, (22, 24, 40, 255), (255, 196, 100, 255))
    houses(im, int(H * 0.86), 8, rng, (16, 18, 30, 255), (255, 186, 90, 255))
    for i in range(6):
        person(im, 120 + i * 160, int(H * 0.93), 1.4, (8, 8, 14, 255), look_up=True)
    wing = Image.new("RGBA", im.size, (0, 0, 0, 0))  # 프레임 위쪽을 가로지르는 거대한 날개 (뼈대 + 막)
    wd = ImageDraw.Draw(wing)
    pts = [(-200, 120), (W * 0.5, 40), (W + 200, 260), (W * 0.85, 520), (W * 0.62, 430), (W * 0.45, 660), (W * 0.28, 520), (W * 0.08, 700), (-200, 560)]
    wd.polygon(pts, fill=(235, 236, 245, 235))
    for bx in (W * 0.62, W * 0.45, W * 0.28, W * 0.08):
        wd.line((W * 0.5, 40, bx, 640), fill=(200, 200, 215, 255), width=10)
    im.alpha_composite(wing.filter(ImageFilter.GaussianBlur(3)))
    shadow = Image.new("RGBA", im.size, (0, 0, 0, 0))  # 지붕 위로 드리운 그림자
    ImageDraw.Draw(shadow).polygon([(-100, H * 0.52), (W + 100, H * 0.58), (W + 100, H * 0.80), (-100, H * 0.74)], fill=(0, 0, 8, 120))
    im.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(40)))
    return im


def shot5():
    """⑤ 클로즈업 — 풀밭 위 손, 옆에 떨어진 깃펜이 희미하게 빛남, 손가락이 움찔"""
    rng = random.Random(15)
    im = sky((60, 96, 50), (98, 140, 64))
    d = ImageDraw.Draw(im)
    for _ in range(420):  # 풀잎
        x, y = rng.randint(0, W), rng.randint(0, H)
        h = rng.randint(30, 90)
        d.line((x, y, x + rng.randint(-14, 14), y - h), fill=lerp((70, 120, 50), (150, 190, 90), rng.random()) + (255,), width=6)
    # 손 (손바닥 + 손가락 넷 + 엄지), 소매
    d.rounded_rectangle((90, 1060, 520, 1330), radius=60, fill=(70, 60, 90, 255))
    d.rounded_rectangle((470, 1080, 700, 1300), radius=70, fill=(232, 190, 150, 255))
    for i, (fy, fl) in enumerate([(1090, 210), (1140, 230), (1190, 215), (1240, 180)]):
        d.rounded_rectangle((640, fy, 640 + fl, fy + 42), radius=21, fill=(225, 182, 142, 255))
    d.rounded_rectangle((560, 1040, 700, 1086), radius=23, fill=(225, 182, 142, 255))
    # 깃펜 (깃털 + 촉)
    q = Image.new("RGBA", im.size, (0, 0, 0, 0))
    qd = ImageDraw.Draw(q)
    qd.polygon([(760, 1500), (980, 1020), (1010, 1060), (820, 1520)], fill=(250, 246, 232, 255))
    qd.line((790, 1510, 995, 1040), fill=(200, 180, 140, 255), width=6)
    qd.polygon([(760, 1500), (820, 1520), (770, 1590)], fill=(60, 60, 80, 255))
    glow(im, (640, 900, 1120, 1700), (255, 222, 120, 200), 80, 2)
    im.alpha_composite(q)
    return im


def shot6():
    """⑥ 미디엄 역광 — 모험가가 몸을 일으킴: 해를 등져 얼굴은 실루엣, 손의 깃펜이 따뜻하게 빛남"""
    rng = random.Random(16)
    im = sky((255, 214, 160), (255, 160, 120), 0.6)
    glow(im, (W * 0.2, H * 0.2, W * 0.8, H * 0.6), (255, 250, 230, 230), 70, 2)
    rays(im, (W * 0.5, H * 0.40), range(0, 360, 15), 1600, (255, 240, 200, 45))
    ridge(im, H * 0.70, 40, (90, 120, 70, 255), rng, jag=0.2)
    d = ImageDraw.Draw(im)
    body = (30, 26, 40, 255)
    d.ellipse((W * 0.5 - 95, H * 0.47 - 95, W * 0.5 + 95, H * 0.47 + 95), fill=body)  # 머리
    d.polygon([(W * 0.5 - 220, H * 0.80), (W * 0.5 - 150, H * 0.53), (W * 0.5 + 150, H * 0.53), (W * 0.5 + 240, H * 0.80)], fill=body)  # 어깨·상체
    d.polygon([(W * 0.5 + 150, H * 0.56), (W * 0.5 + 330, H * 0.62), (W * 0.5 + 310, H * 0.66), (W * 0.5 + 140, H * 0.62)], fill=body)  # 팔
    glow(im, (W * 0.5 + 250, H * 0.55, W * 0.5 + 470, H * 0.70), (255, 214, 110, 230), 50, 2)
    d.polygon([(W * 0.5 + 330, H * 0.64), (W * 0.5 + 420, H * 0.52), (W * 0.5 + 432, H * 0.535), (W * 0.5 + 345, H * 0.65)], fill=(255, 248, 225, 255))
    d.rectangle((0, H * 0.80, W, H), fill=(40, 60, 36, 255))
    for _ in range(160):
        x, y = rng.randint(0, W), rng.randint(int(H * 0.80), H)
        d.line((x, y, x + rng.randint(-10, 10), y - rng.randint(30, 80)), fill=(56, 84, 48, 255), width=5)
    return im


def shot7():
    """⑦ 와이드 — 언덕 위 모험가가 멀리 서고 탑을 바라봄, 글자 몇 개가 주위를 돎 → 하얀 빛으로 → 타이틀"""
    rng = random.Random(17)
    im = sky((60, 110, 190), (255, 220, 170), 0.8)
    letters(im, 16, rng, (60, 120, W - 60, int(H * 0.45)), [(255, 230, 150), (180, 230, 255)], (22, 42))
    ridge(im, H * 0.58, 60, (130, 150, 190, 255), rng)
    tower(im, int(W * 0.68), int(H * 0.585), 260, 70, (90, 100, 140, 255), (255, 220, 140, 255))
    ridge(im, H * 0.68, 50, (90, 130, 100, 255), rng)
    d = ImageDraw.Draw(im)
    d.polygon([(0, H), (0, H * 0.80), (W * 0.35, H * 0.74), (W * 0.62, H * 0.82), (W, H * 0.86), (W, H)], fill=(52, 92, 52, 255))  # 앞 언덕
    hx, hy = W * 0.34, H * 0.745  # 뒷모습 모험가
    d.ellipse((hx - 30, hy - 200, hx + 30, hy - 140), fill=(30, 30, 44, 255))
    d.polygon([(hx - 46, hy), (hx - 36, hy - 140), (hx + 36, hy - 140), (hx + 46, hy)], fill=(30, 30, 44, 255))
    glow(im, (hx + 20, hy - 130, hx + 120, hy - 30), (255, 214, 110, 220), 25, 2)
    letters(im, 7, rng, (int(hx - 160), int(hy - 360), int(hx + 160), int(hy - 150)), [(255, 220, 120)], (20, 34))
    glow(im, (-300, -300, W + 300, H * 0.6), (255, 252, 240, 120), 120)  # 하얀 빛으로 번지기 시작
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    shots = {"1_와이드_말의세계": shot1(), "2_미디엄_서고": shot2(), "3_로우앵글_말이사라짐": shot3(),
             "4_하이앵글_백지룡의그림자": shot4(), "5_클로즈업_손과깃펜": shot5(), "6_역광_깨어남": shot6(),
             "7_와이드_떠나는모험가": shot7()}
    for name, im in shots.items():
        pixelate(im).save(os.path.join(OUT, name + ".png"))
        print("saved", name)
    sheet = Image.new("RGB", (7 * 230 + 80, 410 + 20), (20, 22, 30))
    for i, name in enumerate(shots):
        sheet.paste(Image.open(os.path.join(OUT, name + ".png")).resize((230, 409)), (10 + i * 240, 10))
    sheet.save(os.path.join(OUT, "_contact.png"))


if __name__ == "__main__":
    main()
