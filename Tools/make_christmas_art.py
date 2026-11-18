"""크리스마스 선물 퀴즈 도트 그림 (#274) — 팩에 선물 상자·나무가 없어 팩 그림체(검은 테두리·납작한 색·밝은 점 하나)에 맞춰 직접 그림.
16×16 → Assets/Resources/Art/Event/gift_box.png · tree.png (UiAssetImporter가 도트 설정: Point, 16px = 1칸)

    python Tools/make_christmas_art.py [--preview 미리보기.png]
"""
import argparse
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Event")

COLORS = {
    "o": (20, 27, 27),     # 테두리 (팩과 같은 색)
    "R": (205, 62, 72),    # 상자 빨강
    "r": (150, 38, 56),    # 그늘
    "H": (240, 128, 120),  # 밝은 면
    "Y": (255, 214, 96),   # 리본 금빛
    "y": (214, 150, 52),   # 리본 그늘
    "W": (255, 246, 214),  # 리본 반짝
    "G": (72, 168, 88),    # 나무 초록
    "g": (40, 112, 64),    # 나무 그늘
    "T": (128, 78, 44),    # 나무 기둥
}

# 선물 상자: 위 = 리본 매듭, 가운데 = 뚜껑, 아래 = 상자 (좌우 대칭)
GIFT_BOX = [
    "................",
    "...ooo....ooo...",
    "..oWYYo..oYYYo..",
    "..oYyYYooYYyYo..",
    "...oYYYyyYYYo...",
    "oooooooYyooooooo",
    "oHHHHHHYyRRRRRro",
    "orrrrrrYyrrrrrro",
    ".ooooooYyoooooo.",
    ".oRHRRRYyRRRRro.",
    ".oRRRRRYyRRRRro.",
    ".oYYYYYWyYYYYyo.",
    ".orrrrrYyrrrrro.",
    ".oRRRRRYyRRRRro.",
    ".orrrrrYyrrrrro.",
    "..oooooooooooo..",
]

# 크리스마스 나무 (개발자의 편지 머리 — 🎄 이모지는 글꼴에 없어서): 금빛 별 · 세 층 잎 · 빨강·금 장식 · 기둥
TREE = [
    ".......oo.......",
    "......oYWo......",
    "......oGgo......",
    ".....oGGGgo.....",
    "....oGGRGGgo....",
    ".....ooGgoo.....",
    "....oGGGGGgo....",
    "...oGGGGGYGgo...",
    "..oGGRGGGGGGgo..",
    "...ooGGGGGgoo...",
    "..oGGGGGGYGGgo..",
    ".oGGYGGGGGGRGgo.",
    "oGGGGGGRGGGGGGgo",
    "oooooooTTooooooo",
    "......oTTo......",
    "......oooo......",
]

ART = {"gift_box": GIFT_BOX, "tree": TREE}


def build(pixels):
    assert len(pixels) == 16 and all(len(row) == 16 for row in pixels), "16×16이어야 함"
    image = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(pixels):
        for x, ch in enumerate(row):
            if ch != ".":
                image.putpixel((x, y), COLORS[ch] + (255,))
    return image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", help="8배로 키운 미리보기(나란히)를 이 경로에 저장 (확인용)")
    args = parser.parse_args()
    os.makedirs(OUT_DIR, exist_ok=True)
    images = []
    for name, pixels in ART.items():
        image = build(pixels)
        path = os.path.join(OUT_DIR, name + ".png")
        image.save(path)
        images.append(image)
        print("saved", path)
    if args.preview:
        back = Image.new("RGBA", (18 * len(images), 16), (60, 60, 70, 255))
        for i, image in enumerate(images):
            back.alpha_composite(image, (i * 18, 0))
        back.resize((back.width * 8, 128), Image.NEAREST).save(args.preview)
        print("preview", args.preview)


if __name__ == "__main__":
    main()
