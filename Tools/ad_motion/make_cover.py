"""릴스 커버 (9:16, 1080×1920): 제목 + 실제 단어 문제 화면 + 앱 이름 (#261).

    python Tools/ad_motion/make_cover.py [출력.png]           (pip install pillow)
    python Tools/ad_motion/make_cover.py --thumb [출력.png]   → 유튜브 가로 영상 썸네일 1280×720 (같은 모양)

게임 화면은 앱스토어 스크린샷 원본(Builds/store/raw/2_quiz.png — Tools/store_screenshots.py 참고)에서
몬스터 카드 줄과 단어·보기 줄만 잘라 붙임. 인스타그램 프로필 칸은 커버의 가운데 3:4(위아래 240px씩 잘림)만 보이므로
제목·게임 화면·앱 이름은 모두 그 안에 둠. 썸네일은 오른쪽 아래에 영상 길이가 덮이므로 그 구석을 비움
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
THUMB = "--thumb" in sys.argv
_args = [a for a in sys.argv[1:] if a != "--thumb"]
OUT = _args[0] if _args else os.path.join(ROOT, "Builds", "ad", "youtube_thumbnail.png" if THUMB else "wordrpg_reel_cover.png")

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


def background(w=W, h=H, count=46, seed=7):
    bg = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(bg)
    for y in range(h):
        t = y / (h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(TOP_BG[i] + (BOTTOM_BG[i] - TOP_BG[i]) * t) for i in range(3)))
    # 광고 마지막 화면처럼 흩어진 영어 글자 (흐리게)
    rng = random.Random(seed)
    for _ in range(count):
        ch = rng.choice("ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        size = rng.randint(54, 120)
        layer = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
        ImageDraw.Draw(layer).text((size, size), ch, font=font(size), fill=LETTER + (255,), anchor="mm")
        layer = layer.rotate(rng.uniform(-25, 25), resample=Image.BICUBIC)
        bg.paste(layer, (rng.randint(-size, w - size), rng.randint(-size, h - size)), layer)
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

    # 게임 화면
    shot = game_shot(712)
    x, y = (W - shot.width) // 2, SAFE_TOP + 318
    bottom = framed(canvas, shot, x, y, 40)

    # 앱 아이콘 + 이름
    icon_size = 84
    name_w = font(62).getlength("영단어RPG")
    cy = (bottom + SAFE_BOTTOM) // 2
    app_row(canvas, round((W - (icon_size + 22 + name_w)) / 2), cy, icon_size, 62)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.save(OUT)
    print("cover", OUT, "| game", y, "~", bottom, "| name row", cy)


def framed(canvas, shot, x, y, radius, border=8):
    """금색 테두리 + 그림자 (스토어 스크린샷과 같은 모양) — 테두리 아래 끝 y를 돌려줌"""
    d = ImageDraw.Draw(canvas)
    shadow = Image.new("L", (shot.width + 120, shot.height + 120), 0)
    ImageDraw.Draw(shadow).rounded_rectangle((60, 60, shot.width + 60, shot.height + 60), radius, fill=190)
    canvas.paste((0, 0, 0), (x - 60, y - 40), shadow.filter(ImageFilter.GaussianBlur(26)))
    d.rounded_rectangle((x - border, y - border, x + shot.width + border, y + shot.height + border),
                        radius + border, fill=GOLD)
    canvas.paste(shot, (x, y), rounded(shot, radius))
    return y + shot.height + border


def app_row(canvas, x, cy, icon_size, name_size):
    icon = Image.open(ICON).convert("RGBA").resize((icon_size, icon_size), Image.LANCZOS)
    canvas.paste(icon, (x, cy - icon_size // 2), rounded(icon, round(icon_size * 0.26)))
    ImageDraw.Draw(canvas).text((x + icon_size + 22, cy), "영단어RPG", font=font(name_size), fill=WHITE,
                                anchor="lm", stroke_width=6, stroke_fill=OUTLINE)


def thumbnail():
    tw, th = 1280, 720
    canvas = background(tw, th, 22, 5)
    d = ImageDraw.Draw(canvas)
    shot = game_shot(427)                         # 높이 약 600 — 아래 끝이 영상 길이 표시(오른쪽 아래)보다 위
    x, y = tw - 70 - shot.width, 50
    bottom = framed(canvas, shot, x, y, 28, 6)
    assert bottom <= 662, "게임 화면이 영상 길이 표시에 가림"
    left = 70
    d.text((left, 150), "게임하다 보니", font=fit("게임하다 보니", 84, x - left - 60), fill=WHITE, anchor="lm",
           stroke_width=8, stroke_fill=OUTLINE)
    for i, line in enumerate(["영단어가", "외워져요"]):
        d.text((left, 290 + i * 150), line, font=font(150), fill=GOLD, anchor="lm", stroke_width=12, stroke_fill=OUTLINE)
    app_row(canvas, left, 610, 84, 60)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    canvas.save(OUT)
    print("thumbnail", OUT, "| game", x, y, "~", bottom)


if __name__ == "__main__":
    thumbnail() if THUMB else main()
