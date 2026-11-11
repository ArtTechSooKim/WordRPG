"""유튜브 채널 배너 (2048×1152): 개발자 이름 + 앱 아이콘 + 몬스터 (#261).

    python Tools/ad_motion/make_banner.py [출력.png]   (pip install pillow)

유튜브는 기기마다 잘라서 보여 줌: TV = 전체, 컴퓨터 = 가운데 가로 띠(2048×338), 폰 = 그 띠의 가운데 1235×338.
그래서 이름·글은 가운데 1235×338 안에, 몬스터는 컴퓨터에서만 보이는 띠 양옆에 둠. 보스 그림은 넣지 않음 (광고 스포 금지)
"""
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
JUA = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "Jua-Regular.ttf")
ICON = os.path.join(ROOT, "Assets", "Branding", "AppIcon.png")
MONSTERS = os.path.join(ROOT, "Assets", "Resources", "Art", "NinjaAdventure", "Monsters")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Builds", "ad", "youtube_banner.png")

W, H = 2048, 1152
BAND = (407, 745)                       # 컴퓨터에서 보이는 띠
SAFE = (406, 1642)                      # 폰에서도 보이는 가로 범위
TOP_BG, BOTTOM_BG = (18, 20, 36), (38, 30, 72)
GOLD, WHITE, OUTLINE, LETTER = (255, 209, 64), (246, 247, 255), (11, 12, 24), (52, 50, 96)
# (몬스터, 가운데 x, 가운데 y, 배율) — 띠 양옆
SIDE_MONSTERS = [
    ("ink_slime", 120, 600, 7), ("scribble_bat", 250, 500, 7), ("question_owl", 330, 640, 6),
    ("spell_mushroom", 1720, 640, 6), ("tangle_spider", 1820, 500, 7), ("squiggle_snake", 1940, 610, 7),
]


def font(size):
    return ImageFont.truetype(JUA, size)


def background():
    bg = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(bg)
    for y in range(H):
        t = y / (H - 1)
        d.line([(0, y), (W, y)], fill=tuple(int(TOP_BG[i] + (BOTTOM_BG[i] - TOP_BG[i]) * t) for i in range(3)))
    rng = random.Random(11)
    for _ in range(70):
        ch = rng.choice("ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        size = rng.randint(54, 120)
        px, py = rng.randint(-size, W - size), rng.randint(-size, H - size)
        if px + size * 2 > SAFE[0] and px < SAFE[1] and py + size * 2 > BAND[0] and py < BAND[1]:
            continue                    # 이름·글 뒤는 비움
        layer = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
        ImageDraw.Draw(layer).text((size, size), ch, font=font(size), fill=LETTER + (255,), anchor="mm")
        layer = layer.rotate(rng.uniform(-25, 25), resample=Image.BICUBIC)
        bg.paste(layer, (px, py), layer)
    return bg


def main():
    canvas = background()
    d = ImageDraw.Draw(canvas)
    mid = (BAND[0] + BAND[1]) // 2

    for name, cx, cy, scale in SIDE_MONSTERS:
        sprite = Image.open(os.path.join(MONSTERS, name + ".png")).convert("RGBA")
        sprite = sprite.resize((sprite.width * scale, sprite.height * scale), Image.NEAREST)
        canvas.paste(sprite, (cx - sprite.width // 2, cy - sprite.height // 2), sprite)

    # 앱 아이콘 + 이름 두 줄 (가운데 안전 영역)
    icon_size, gap = 220, 48
    title, subs = "아트텍수", ("1인 게임 개발", "새 게임과 업데이트 소식")   # 사이 점은 그림으로 (Jua에 '·'가 없음)
    title_font, sub_font = font(132), font(50)
    dot_gap = 44
    sub_w = sum(sub_font.getlength(s) for s in subs) + dot_gap
    text_w = max(title_font.getlength(title), sub_w)
    row_w = icon_size + gap + text_w
    x = round((W - row_w) / 2)
    assert x >= SAFE[0] and x + row_w <= SAFE[1], "글이 폰에서 잘림"
    icon = Image.open(ICON).convert("RGBA").resize((icon_size, icon_size), Image.LANCZOS)
    mask = Image.new("L", icon.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, icon_size - 1, icon_size - 1), 48, fill=255)
    d.rounded_rectangle((x - 8, mid - icon_size // 2 - 8, x + icon_size + 8, mid + icon_size // 2 + 8), 54, fill=GOLD)
    canvas.paste(icon, (x, mid - icon_size // 2), mask)
    tx = x + icon_size + gap
    d.text((tx, mid - 6), title, font=title_font, fill=GOLD, anchor="ls", stroke_width=10, stroke_fill=OUTLINE)
    sy = mid + 76
    d.text((tx, sy), subs[0], font=sub_font, fill=WHITE, anchor="ls", stroke_width=6, stroke_fill=OUTLINE)
    dx = tx + sub_font.getlength(subs[0]) + dot_gap / 2
    d.ellipse((dx - 7, sy - 25, dx + 7, sy - 11), fill=GOLD, outline=OUTLINE, width=3)
    d.text((dx + dot_gap / 2, sy), subs[1], font=sub_font, fill=WHITE, anchor="ls", stroke_width=6, stroke_fill=OUTLINE)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.save(OUT)
    print("banner", OUT, "| row x", x, "~", round(x + row_w))


if __name__ == "__main__":
    main()
