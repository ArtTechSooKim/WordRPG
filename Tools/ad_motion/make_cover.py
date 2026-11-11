"""릴스 커버 (9:16, 1080×1920): 제목 + 실제 단어 문제 화면 + 앱 이름 (#261).

    python Tools/ad_motion/make_cover.py [출력.png]   (pip install pillow)

게임 화면은 앱스토어 스크린샷 원본(Builds/store/raw/2_quiz.png — Tools/store_screenshots.py 참고)에서
몬스터 카드 줄과 단어·보기 줄만 잘라 붙임. 인스타그램 프로필 칸은 커버의 가운데 3:4(위아래 240px씩 잘림)만 보이므로
제목·게임 화면·앱 이름은 모두 그 안에 둠
"""
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONTS = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts")
JUA = os.path.join(FONTS, "Jua-Regular.ttf")
QUIZ = os.path.join(ROOT, "Builds", "store", "raw", "2_quiz.png")
ICON = os.path.join(ROOT, "Assets", "Branding", "AppIcon.png")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Builds", "ad", "wordrpg_reel_cover.png")

W, H = 1080, 1920
SAFE_TOP, SAFE_BOTTOM = 240, 1680                  # 프로필 칸(3:4)에 보이는 곳
TOP_BG, BOTTOM_BG = (18, 20, 36), (38, 30, 72)     # 스토어 스크린샷과 같은 바탕
GOLD, WHITE, OUTLINE, LETTER = (255, 209, 64), (246, 247, 255), (11, 12, 24), (52, 50, 96)
# 게임 화면에서 쓸 줄 (원본 1320×2868 기준 위·아래)
STRIPS = [(176, 1084), (1912, 2850)]


def font(size):
    return ImageFont.truetype(JUA, size)


def fit(text, size, max_w):
    while font(size).getlength(text) > max_w:
        size -= 2
    return font(size)


def background():
    bg = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(bg)
    for y in range(H):
        t = y / (H - 1)
        d.line([(0, y), (W, y)], fill=tuple(int(TOP_BG[i] + (BOTTOM_BG[i] - TOP_BG[i]) * t) for i in range(3)))
    # 광고 마지막 화면처럼 흩어진 영어 글자 (흐리게)
    rng = random.Random(7)
    for _ in range(46):
        ch = rng.choice("ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        size = rng.randint(54, 120)
        layer = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
        ImageDraw.Draw(layer).text((size, size), ch, font=font(size), fill=LETTER + (255,), anchor="mm")
        layer = layer.rotate(rng.uniform(-25, 25), resample=Image.BICUBIC)
        bg.paste(layer, (rng.randint(-size, W - size), rng.randint(-size, H - size)), layer)
    return bg


def game_shot(width):
    src = Image.open(QUIZ).convert("RGB")
    parts = [src.crop((0, top, src.width, bottom)) for top, bottom in STRIPS]
    gap = 10
    shot = Image.new("RGB", (src.width, sum(p.height for p in parts) + gap * (len(parts) - 1)), (18, 20, 36))
    y = 0
    for p in parts:
        shot.paste(p, (0, y))
        y += p.height + gap
    return shot.resize((width, round(shot.height * width / shot.width)), Image.LANCZOS)


def rounded(im, radius):
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, im.width - 1, im.height - 1), radius, fill=255)
    return mask


def main():
    canvas = background()
    d = ImageDraw.Draw(canvas)

    # 제목 두 줄
    d.text((W // 2, SAFE_TOP + 92), "게임하다 보니", font=fit("게임하다 보니", 88, 900), fill=WHITE,
           anchor="mm", stroke_width=8, stroke_fill=OUTLINE)
    d.text((W // 2, SAFE_TOP + 206), "영단어가 외워져요", font=fit("영단어가 외워져요", 118, 960), fill=GOLD,
           anchor="mm", stroke_width=10, stroke_fill=OUTLINE)

    # 게임 화면 (금색 테두리 + 그림자 — 스토어 스크린샷과 같은 모양)
    shot = game_shot(712)
    x, y, radius, border = (W - shot.width) // 2, SAFE_TOP + 318, 40, 8
    shadow = Image.new("L", (shot.width + 120, shot.height + 120), 0)
    ImageDraw.Draw(shadow).rounded_rectangle((60, 60, shot.width + 60, shot.height + 60), radius, fill=190)
    canvas.paste((0, 0, 0), (x - 60, y - 40), shadow.filter(ImageFilter.GaussianBlur(26)))
    d.rounded_rectangle((x - border, y - border, x + shot.width + border, y + shot.height + border),
                        radius + border, fill=GOLD)
    canvas.paste(shot, (x, y), rounded(shot, radius))
    bottom = y + shot.height + border

    # 앱 아이콘 + 이름
    icon_size = 84
    name_font = font(62)
    name_w = name_font.getlength("영단어RPG")
    row_w = icon_size + 22 + name_w
    cy = (bottom + SAFE_BOTTOM) // 2
    ix = round((W - row_w) / 2)
    icon = Image.open(ICON).convert("RGBA").resize((icon_size, icon_size), Image.LANCZOS)
    canvas.paste(icon, (ix, cy - icon_size // 2), rounded(icon, 22))
    d.text((ix + icon_size + 22, cy), "영단어RPG", font=name_font, fill=WHITE, anchor="lm",
           stroke_width=6, stroke_fill=OUTLINE)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.save(OUT)
    print("cover", OUT, "| game", y, "~", bottom, "| name row", cy)


if __name__ == "__main__":
    main()
