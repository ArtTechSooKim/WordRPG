"""입장 시네마틱 키 장면 (#128 사용자: Figma에 그려 두면 Flow에서 img to img로 영상을 만듦).

게임 그림(타이틀 초원 풍경 · 몬스터 · Ninja 팩 용 · 주인공 · 깃펜)을 합쳐 세로 1080×1920 장면 6장 → Builds/cinematic/
    python Tools/cinematic_frames.py
글자는 넣지 않음 (자막은 Figma 메모·게임에서 따로). 시놉시스 = 개발현황보고서 #124 작업 로그
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Resources", "Art", "NinjaAdventure")
PACK = r"F:\Unity\김수연습\Ninja Adventure - Asset Pack\Ninja Adventure - Asset Pack"
FONTS = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts")
OUT = os.path.join(ROOT, "Builds", "cinematic")
W, H = 1080, 1920
LETTERS = list("AbcDeFgHiJkLmNoPqRsTuVwXyZ") + list("가나다라마바사아자말글뜻꿈빛") + ["word", "말", "dream", "빛"]


def meadow(scale=5):
    """타이틀 초원 풍경(224×384)을 키워 세로 화면에 맞춤"""
    im = Image.open(os.path.join(ART, "Title", "title_scene.png")).convert("RGBA")
    im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    x = (im.width - W) // 2
    return im.crop((x, 0, x + W, H))


def sprite(path, scale):
    im = Image.open(path).convert("RGBA")
    return im.resize((im.width * scale, im.height * scale), Image.NEAREST)


def silhouette(im, color=(10, 8, 20), alpha=255):
    out = Image.new("RGBA", im.size, color + (0,))
    a = im.getchannel("A").point(lambda v: alpha if v > 0 else 0)
    out.putalpha(a)
    return out


def glow_letters(base, count, rng, colors, area=(0, 0, W, H), size=(46, 96), lift=None):
    """반딧불처럼 빛나는 글자 (부드러운 빛 + 글자)"""
    glow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    sharp = Image.new("RGBA", base.size, (0, 0, 0, 0))
    gd, sd = ImageDraw.Draw(glow), ImageDraw.Draw(sharp)
    for _ in range(count):
        ch = rng.choice(LETTERS)
        s = rng.randint(*size)
        font = ImageFont.truetype(os.path.join(FONTS, "Jua-Regular.ttf"), s)
        x, y = rng.randint(area[0], area[2]), rng.randint(area[1], area[3])
        if lift:
            x, y = lift(x, y)
        color = rng.choice(colors)
        gd.text((x, y), ch, font=font, fill=color + (200,), anchor="mm")
        sd.text((x, y), ch, font=font, fill=(255, 255, 240, 235), anchor="mm")
    glow = glow.filter(ImageFilter.GaussianBlur(14))
    base.alpha_composite(glow)
    base.alpha_composite(glow)
    base.alpha_composite(sharp)
    return base


def tint(im, color, amount):
    over = Image.new("RGBA", im.size, color + (int(255 * amount),))
    out = im.copy()
    out.alpha_composite(over)
    return out


def vignette(im, strength=0.55):
    mask = Image.new("L", im.size, 0)
    d = ImageDraw.Draw(mask)
    for i in range(60):
        k = i / 60
        d.ellipse((-W * 0.3 + k * W * 0.3, -H * 0.2 + k * H * 0.2, W * 1.3 - k * W * 0.3, H * 1.2 - k * H * 0.2), fill=int(255 * k))
    mask = mask.filter(ImageFilter.GaussianBlur(60))
    dark = Image.new("RGBA", im.size, (0, 0, 0, 255))
    dark.putalpha(mask.point(lambda v: int((255 - v) * strength)))
    out = im.copy()
    out.alpha_composite(dark)
    return out


def scene1():
    """① 말로 이루어진 세계: 밝은 초원 위로 글자들이 반딧불처럼"""
    rng = random.Random(1)
    im = meadow()
    im = ImageEnhance.Brightness(im).enhance(1.05)
    im = glow_letters(im, 34, rng, [(255, 220, 90), (140, 230, 255), (255, 170, 210)], area=(60, 80, W - 60, H - 120))
    return vignette(im, 0.35)


def scene2(progress):
    """② 말이 사라지다: 위에서 하얀 빈 페이지가 번지며 색과 글자를 빨아들임 (progress 0 → 1 = 시작 → 끝 장면)"""
    rng = random.Random(2)
    im = meadow()
    edge = int(H * (0.12 + 0.5 * progress))  # 하얀 영역의 아래 끝
    grey = ImageEnhance.Color(im).enhance(0.15)
    mask = Image.new("L", im.size, 0)
    d = ImageDraw.Draw(mask)
    d.rectangle((0, 0, W, edge + 260), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(120))
    im = Image.composite(grey, im, mask)
    # 찢긴 종이 가장자리의 하얀 빈 페이지
    paper = Image.new("RGBA", im.size, (0, 0, 0, 0))
    pd = ImageDraw.Draw(paper)
    pts = [(0, 0), (W, 0)]
    for i in range(W, -1, -36):
        pts.append((i, edge + rng.randint(-34, 34)))
    pd.polygon(pts, fill=(250, 248, 240, 255))
    paper = paper.filter(ImageFilter.GaussianBlur(2))
    im.alpha_composite(paper)
    # 빨려 올라가는 글자 (하얀 쪽으로 갈수록 작고 흐리게)
    def lift(x, y):
        return x + rng.randint(-40, 40), y
    im = glow_letters(im, int(18 + 16 * progress), rng, [(255, 220, 90), (140, 230, 255)],
                      area=(80, edge + 40, W - 80, min(H - 200, edge + 700)), size=(30, 70), lift=lift)
    # 그림자 괴물들 (끝 장면일수록 많이)
    monsters = ["forget_goblin", "ink_slime", "scribble_bat", "forget_goblin", "ink_slime"]
    n = 2 + int(3 * progress)
    for i in range(n):
        m = sprite(os.path.join(ART, "Monsters", monsters[i] + ".png"), 7)
        s = silhouette(m, (12, 10, 24), 235)
        x = 90 + i * 190 + rng.randint(-30, 30)
        y = H - 520 + rng.randint(-120, 120)
        im.alpha_composite(s, (x, y))
        ed = ImageDraw.Draw(im)  # 빨간 눈 두 개 (적의 눈은 모두 빨간색 — 사용자)
        cx, cy = x + s.width // 2, y + int(s.height * 0.42)
        for dx in (-18, 18):
            ed.ellipse((cx + dx - 7, cy - 7, cx + dx + 7, cy + 7), fill=(235, 40, 50, 255))
    return vignette(im, 0.45)


def scene3():
    """③ 백지룡의 그림자: 어두워진 초원 위를 아주 큰 용의 그림자가 지나감 (얼굴은 안 보임)"""
    rng = random.Random(3)
    im = meadow()
    im = ImageEnhance.Color(im).enhance(0.5)
    im = tint(im, (20, 24, 48), 0.55)
    preview = Image.open(os.path.join(PACK, "Actor", "Boss", "DragonBlue", "Preview.png")).convert("RGBA")
    dragon = preview.crop((75, 20, 352, 272))
    bg = preview.getpixel((0, 0))  # 미리보기 그림의 바탕색을 투명하게 (안 그러면 그림자가 네모)
    dragon.putdata([(0, 0, 0, 0) if px[:3] == bg[:3] else px for px in dragon.getdata()])
    ImageDraw.Draw(dragon).rectangle((0, 0, 24, 82), fill=(0, 0, 0, 0))  # 미리보기 왼쪽 위의 눈 확대 그림은 뺌
    dragon = dragon.resize((dragon.width * 5, dragon.height * 5), Image.NEAREST).rotate(-18, expand=True, resample=Image.NEAREST)
    shadow = silhouette(dragon, (4, 4, 10), 190).filter(ImageFilter.GaussianBlur(10))
    im.alpha_composite(shadow, ((W - shadow.width) // 2 + 60, 380))
    # 그림자 쪽으로 빨려 드는 희미한 글자
    im = glow_letters(im, 14, rng, [(200, 210, 255)], area=(80, 300, W - 80, 1500), size=(28, 54))
    return vignette(im, 0.6)


def scene4():
    """④ 깨어나는 모험가: 풀밭에 누운 모험가(옆으로 누운 정면 그림, 어둡게 — 8명 중 고르므로 누군지 흐리게), 손 옆 깃펜이 은은하게 빛남"""
    rng = random.Random(4)
    title = Image.open(os.path.join(ART, "Title", "title_scene.png")).convert("RGBA")
    lawn = title.crop((80, 112, 96, 128)).resize((160, 160), Image.NEAREST)
    tuft = title.crop((48, 64, 64, 80)).resize((160, 160), Image.NEAREST)
    im = Image.new("RGBA", (W, H))
    for y in range(0, H, 160):
        for x in range(0, W, 160):
            im.alpha_composite(lawn, (x, y))
    for _ in range(9):  # 둘레에 풀숲 몇 포기
        x, y = rng.choice([rng.randint(-40, 200), rng.randint(W - 260, W - 80)]), rng.randint(0, H - 160)
        im.alpha_composite(tuft, (x, y))
    im = ImageEnhance.Brightness(im).enhance(0.85)
    sheet = Image.open(os.path.join(ART, "Player", "boy_sheet.png")).convert("RGBA")
    hero = sheet.crop((0, 0, 16, 16)).rotate(90, expand=True).resize((16 * 18, 16 * 18), Image.NEAREST)  # 옆으로 누운 모습
    body = ImageEnhance.Brightness(ImageEnhance.Color(hero).enhance(0.25)).enhance(0.5)
    shadow = silhouette(hero, (0, 0, 0), 90).filter(ImageFilter.GaussianBlur(8))
    hx, hy = (W - body.width) // 2 - 40, 820
    im.alpha_composite(shadow, (hx + 14, hy + 22))
    im.alpha_composite(body, (hx, hy))
    quill = sprite(os.path.join(ART, "Relics", "relic_quill.png"), 11)
    qx, qy = hx + body.width - 30, hy + body.height - 120
    glow = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((qx - 110, qy - 110, qx + quill.width + 110, qy + quill.height + 110), fill=(255, 225, 120, 180))
    glow = glow.filter(ImageFilter.GaussianBlur(70))
    im.alpha_composite(glow)
    im.alpha_composite(quill, (qx, qy))
    return vignette(im, 0.7)


def scene5():
    """⑤ 하얀 빛 → 타이틀: 깃펜의 빛이 화면을 하얗게 채우고 '영단어RPG'"""
    im = meadow()
    light = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(light)
    for i in range(40):
        r = 1400 - i * 32
        d.ellipse((W / 2 - r, H * 0.42 - r, W / 2 + r, H * 0.42 + r), fill=(255, 252, 235, 7 + i * 5))
    light = light.filter(ImageFilter.GaussianBlur(40))
    im.alpha_composite(light)
    im.alpha_composite(light)
    d = ImageDraw.Draw(im)
    font = ImageFont.truetype(os.path.join(FONTS, "Jua-Regular.ttf"), 150)
    d.text((W / 2 + 6, H * 0.42 + 8), "영단어RPG", font=font, fill=(150, 110, 30, 160), anchor="mm")
    d.text((W / 2, H * 0.42), "영단어RPG", font=font, fill=(255, 205, 60, 255), anchor="mm")
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    frames = {
        "1_말로이루어진세계": scene1(),
        "2a_말이사라짐_시작": scene2(0.0),
        "2b_말이사라짐_끝": scene2(1.0),
        "3_백지룡의그림자": scene3(),
        "4_깨어나는모험가": scene4(),
        "5_하얀빛_타이틀": scene5(),
    }
    for name, im in frames.items():
        path = os.path.join(OUT, name + ".png")
        im.convert("RGB").save(path)
        print("saved", path)
    sheet = Image.new("RGB", (6 * 270 + 70, 480 + 20), (20, 22, 30))
    for i, im in enumerate(frames.values()):
        sheet.paste(im.convert("RGB").resize((270, 480)), (10 + i * 280, 10))
    sheet.save(os.path.join(OUT, "_contact.png"))


if __name__ == "__main__":
    main()
