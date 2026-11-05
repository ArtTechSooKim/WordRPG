#!/usr/bin/env python3
"""누군가의 기록(#200): 아직 못 읽는 줄에 얹는 '일그러진 필기' 그림.

줄마다 글자를 실시간으로 뭉개면 줄이 많아 폰에 부담이 되므로 그림 한 장을 미리 만든다.
읽을 수 없는 러시아 글자·뒤섞은 한글을 쓰고(사용자 아이디어), 글자 모양을 일그러뜨리고, 흐린다.
줄은 곧게 (사용자: 줄이 삐뚤면 어지럽다): 낱말은 같은 기준선에 놓고 줄 전체를 휘게 하지 않는다.
규격은 JournalView와 맞출 것: 1024×1024, 위에서부터 영어 줄 8개(64px) + 한글 줄 8개(52px).

  python Tools/make_journal_smudge.py   → Assets/Resources/UI/Journal/smudge.png (흰 글자 + 투명)

Pillow 필요. 글꼴은 Tools/FontSource의 전체 Noto (게임 글꼴은 러시아 글자가 없을 수 있어서).
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONT = ROOT / "Tools" / "FontSource" / "NotoSansKR-Regular.otf"
OUT = ROOT / "Assets" / "Resources" / "UI" / "Journal" / "smudge.png"
SIZE = 1024
EN_ROWS, EN_HEIGHT = 8, 64
KO_ROWS, KO_HEIGHT = 8, 52
CYRILLIC = "абвгдежзиклмнопрстуфхцчшщыэюя"
SYLLABLES = "기억말글이름잊은바람숲길강돌빛밤꿈손눈물새비집문책장속끝첫너나우리어디왜다시멀리오래된그날"
BLUR = 2.6  # 획이 뭉개지는 정도. 줄이 곧으니 예전보다 조금 약해도 못 읽음


def word(rng, alphabet, low, high):
    return "".join(rng.choice(alphabet) for _ in range(rng.randint(low, high)))


def stamp(canvas, text, font, x, baseline, rng):
    """낱말 하나를 기준선에 맞춰 찍는다. 글자를 조금 눕히기만 하고 위아래로 움직이지 않는다."""
    left, _, right, _ = font.getbbox(text)
    w, h = right - left + 16, canvas.height
    tile = Image.new("L", (w, h), 0)
    ImageDraw.Draw(tile).text((8 - left, baseline), text, font=font, fill=255, anchor="ls")
    shear = rng.uniform(-0.22, 0.22)
    sheared = tile.transform(tile.size, Image.AFFINE, (1, shear, -shear * baseline, 0, 1, 0), Image.BICUBIC)
    canvas.paste(255, (x, 0), sheared)
    return x + w


def ripple(image, rng):
    """글자 안쪽만 잔물결로 일그러뜨린다: 가로로만 밀어서 줄의 높이는 그대로"""
    width, height = image.size
    phase = rng.uniform(0, 6.28)
    out = Image.new("L", image.size, 0)
    for y in range(height):
        dx = int(3 * math.sin(y * 0.55 + phase))
        out.paste(image.crop((0, y, width, y + 1)), (dx, y))
    return out


def strip(rng, height, font_size, alphabet, low, high, gap):
    canvas = Image.new("L", (SIZE, height), 0)
    font = ImageFont.truetype(str(FONT), font_size)
    baseline = height // 2 + font_size // 3
    x = -rng.randint(0, 20)
    while x < SIZE:
        x = stamp(canvas, word(rng, alphabet, low, high), font, x, baseline, rng) + rng.randint(*gap)
    return ripple(canvas, rng).filter(ImageFilter.GaussianBlur(BLUR))


def main():
    rng = random.Random(200)
    alpha = Image.new("L", (SIZE, SIZE), 0)
    y = 0
    for _ in range(EN_ROWS):
        alpha.paste(strip(rng, EN_HEIGHT, 34, CYRILLIC, 2, 8, (6, 16)), (0, y))
        y += EN_HEIGHT
    for _ in range(KO_ROWS):
        alpha.paste(strip(rng, KO_HEIGHT, 27, SYLLABLES, 1, 4, (6, 14)), (0, y))
        y += KO_HEIGHT
    OUT.parent.mkdir(parents=True, exist_ok=True)
    image = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    image.putalpha(alpha)
    image.save(OUT, optimize=True)
    print(OUT, OUT.stat().st_size // 1024, "KB, 사용한 높이", y)


if __name__ == "__main__":
    main()
