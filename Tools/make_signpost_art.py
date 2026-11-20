# 숲 이정표 미로 그림 (#279, GDD 9-15) — 팩에 화살표 이정표가 없어 팩 색으로 직접 그림 (16px = 1칸, 바닥 = Forest_Lawn)
#
#   python Tools/make_signpost_art.py   → Assets/Resources/Art/NinjaAdventure/Tiles/
#       Forest_Signpost.png       뒤섞인 이정표: 판자 두 장이 비뚤어지고 글자 조각이 바닥에 떨어짐
#       Forest_Signpost_done.png  바로잡은 이정표: 판자가 곧게, 글자가 제자리 + 잎 하나
#       Forest_Haze.png           헷갈 안개 (바로잡기 전) — 이웃 칸과 이어지는 무늬, 가운데 옅은 소용돌이
#       Forest_Haze_done.png      걷힌 안개 = 이끼 땅 + 남은 안개 몇 점
#       Forest_LoopHaze.png       맴도는 안개 (가짜 길) — 헷갈 안개와 똑같이 (미리 알 수 없게)
#   + _캡처/279_이정표_그림.png (8배 미리보기)
# 효과는 은은하게 (사용자 — 주변 색 ± 한두 톤), 외곽선은 팩과 같은 짙은 색
import math
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, "Assets", "Resources", "Art", "NinjaAdventure", "Tiles")
PREVIEW = os.path.join(ROOT, "_캡처", "279_이정표_그림.png")

PAL = {
    "o": (20, 27, 27, 255),     # 팩 외곽선
    "y": (226, 182, 120, 255),  # 판자 윗면 (밝은 나무)
    "L": (189, 135, 88, 255),   # 판자
    "D": (128, 82, 58, 255),    # 판자 그늘
    "W": (163, 117, 78, 255),   # 기둥
    "V": (110, 70, 52, 255),    # 기둥 그늘
    "k": (64, 40, 34, 255),     # 새긴 글자
    "s": (40, 70, 40, 110),     # 그림자
    "g": (150, 214, 96, 255),   # 잎 (바로잡음)
    "G": (96, 160, 64, 255),    # 잎 그늘
}

SOLVED = [
    "................",
    "...........gg...",
    "..ooooooooooGo..",
    "..oyyyyyyyyyyo..",
    "..oLkLkkLkLkLLo.",
    "..oDDDDDDDDDDo..",
    "..ooooooooooo...",
    "......oWVo......",
    "...ooooooooooo..",
    "..oyyyyyyyyyyo..",
    ".oLLkLkkLkLLLo..",
    "..oDDDDDDDDDDo..",
    "...ooooooooooo..",
    "......oWVo......",
    ".....soWVos.....",
    ".....sooooss....",
]

# 뒤섞임: 위 판자는 왼쪽으로 기울어 아래를, 아래 판자는 거꾸로 위를 가리킴 · 떨어진 글자 조각
SCRAMBLED = [
    "................",
    "..oo............",
    ".oyyoo..........",
    ".oLLyyoo........",
    "..oDkLyyoo......",
    "...oDDLkLyoo....",
    "....ooDDkLLyo...",
    "......ooDDDLo...",
    "......oWVooo....",
    "....ooooooooooo.",
    "...oyyyyyyyyyyyo",
    "..oLLkLLkkLLkLo.",
    "...oDDDDDDDDDDo.",
    "....oooWVooooo..",
    "..k..soWVos..k..",
    ".....sooooss.k..",
]


def lawn():
    return Image.open(os.path.join(TILES, "Forest_Lawn.png")).convert("RGBA").resize((16, 16))


def draw(rows, base):
    img = base.copy()
    for y, row in enumerate(rows):
        assert len(row) == 16, (y, row, len(row))
        for x, c in enumerate(row):
            if c == ".":
                continue
            r, g, b, a = PAL[c]
            if a < 255:
                br, bg, bb, _ = img.getpixel((x, y))
                t = a / 255
                img.putpixel((x, y), (round(br + (r - br) * t), round(bg + (g - bg) * t), round(bb + (b - bb) * t), 255))
            else:
                img.putpixel((x, y), (r, g, b, 255))
    return img


def fog_alpha(x, y):
    # 주기 16 → 옆 칸과 이어짐. 3단계로 끊어 도트 느낌
    v = 0.62 + 0.14 * math.sin(2 * math.pi * (x + 2 * y) / 16) + 0.1 * math.sin(2 * math.pi * (3 * x - y) / 16)
    return round(v * 4) / 4


SWIRL = {(7, 5), (8, 5), (9, 6), (9, 7), (9, 8), (8, 9), (7, 9), (6, 8), (6, 7), (7, 7), (8, 7)}


def haze(base, swirl=True):
    img = base.copy()
    mist = (208, 228, 196)
    for y in range(16):
        for x in range(16):
            a = min(0.74, max(0.36, fog_alpha(x, y) - 0.08))
            if swirl and (x, y) in SWIRL:
                a = min(0.86, a + 0.14)
            r, g, b, _ = img.getpixel((x, y))
            img.putpixel((x, y), (round(r + (mist[0] - r) * a), round(g + (mist[1] - g) * a), round(b + (mist[2] - b) * a), 255))
    return img


def cleared(base):
    img = base.copy()
    mist = (226, 238, 214)
    for x, y in ((3, 4), (4, 4), (11, 10), (12, 10), (12, 11)):
        r, g, b, _ = img.getpixel((x, y))
        img.putpixel((x, y), (round(r + (mist[0] - r) * 0.3), round(g + (mist[1] - g) * 0.3), round(b + (mist[2] - b) * 0.3), 255))
    return img


def main():
    base = lawn()
    out = {
        "Forest_Signpost": draw(SCRAMBLED, base),
        "Forest_Signpost_done": draw(SOLVED, base),
        "Forest_Haze": haze(base),
        "Forest_Haze_done": cleared(base),
        "Forest_LoopHaze": haze(base),
    }
    for name, img in out.items():
        img.save(os.path.join(TILES, name + ".png"))
    # 미리보기: 각 그림 + 안개 2×3 덩어리 + 이끼 둘레
    sheet = Image.new("RGBA", (16 * 12, 16 * 5), (0, 0, 0, 255))
    for i, name in enumerate(out):
        sheet.paste(out[name], (i * 16 * 2 + 8, 8))
    for gy in range(4):
        for gx in range(5):
            tile = base
            if 1 <= gx <= 2 and 1 <= gy <= 3:
                tile = out["Forest_Haze"]
            sheet.paste(tile, (16 * 7 + gx * 16 - 8, 16 + gy * 16 - 8 + 8))
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sheet.resize((sheet.width * 8, sheet.height * 8), Image.NEAREST).save(PREVIEW)
    print("저장:", ", ".join(out), "+", PREVIEW)


if __name__ == "__main__":
    main()
