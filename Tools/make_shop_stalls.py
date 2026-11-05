#!/usr/bin/env python3
"""상점 가판대 (#210): 지역마다 3칸 너비 가판대를 팩 그림(TilesetHouse)으로 조립.

사용자: 쉼터 상점을 더 크게 만들어 상인이 안에 있으면 좋겠음, 지역 컨셉에 맞게.
겹쳐 그리는 세 장 (48×48, 같은 자리):
  Tiles/{테마}_Stall_back.png   뒤 벽 (주인공 아래)
  Tiles/{테마}_Stall_front.png  계산대와 물건 (상인 앞)
  Tiles/{테마}_Stall_roof.png   차양 (주인공 위 — 뒤로 지나가면 가려짐)

  python Tools/make_shop_stalls.py [--preview 파일.png]
"""
import os
import sys

from PIL import Image

PACK = os.environ.get("NINJA_PACK", r"F:\Unity\김수연습\Ninja Adventure - Asset Pack\Ninja Adventure - Asset Pack")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art", "NinjaAdventure", "Tiles")
SIZE = 48

# 팩 그림 자리 (TilesetHouse.png, x, y, 너비, 높이)
AWNING = (256, 256, 48, 22)
WALL = (352, 196, 48, 28)
COUNTER = (272, 288, 48, 16)
GOODS = {
    "heart": (336, 192, 16, 16), "jar": (336, 208, 16, 16), "green": (336, 224, 16, 16), "pot": (336, 240, 16, 16),
    "fish": (256, 208, 16, 16), "veg": (272, 208, 16, 16), "fruit": (288, 208, 16, 16),
    "crate_fruit": (304, 224, 16, 16), "crate_bread": (320, 224, 16, 16), "crate": (304, 240, 16, 16), "crate_egg": (320, 240, 16, 16),
    "barrel": (272, 320, 16, 16),
}

# 테마마다: 차양 색 · 벽 색 · 계산대 색 · 계산대 위 물건 (왼쪽, 오른쪽). 색은 곱하기 (R, G, B)
THEMES = {
    "Meadow": ((1.0, 1.0, 1.0), (1.0, 1.0, 1.0), (1.0, 1.0, 1.0), ("heart", "fruit")),   # 밝은 시골 장터
    "Forest": ((0.62, 0.86, 0.5), (0.78, 0.82, 0.62), (0.82, 0.82, 0.68), ("green", "veg")),  # 이끼 낀 나무 가판대
    "Jungle": ((0.5, 0.9, 0.72), (0.72, 0.85, 0.62), (0.86, 0.84, 0.6), ("fish", "crate_fruit")),  # 강가 생선·과일 좌판
    "Cave": ((0.56, 0.58, 0.95), (0.55, 0.58, 0.8), (0.62, 0.62, 0.82), ("pot", "jar")),   # 어두운 곳의 잡상인
}


def tinted(image, rgb):
    r, g, b, a = image.split()
    r = r.point(lambda v: min(255, int(v * rgb[0])))
    g = g.point(lambda v: min(255, int(v * rgb[1])))
    b = b.point(lambda v: min(255, int(v * rgb[2])))
    return Image.merge("RGBA", (r, g, b, a))


def build(house, look):
    awning_tint, wall_tint, counter_tint, goods = look

    def piece(box, tint=(1, 1, 1)):
        x, y, w, h = box
        return tinted(house.crop((x, y, x + w, y + h)), tint)

    back = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    wall = piece(WALL, wall_tint)
    back.alpha_composite(wall, (0, 6))
    # 기둥: 차양 끝에서 계산대까지 어두운 나무
    post = tinted(Image.new("RGBA", (3, 26), (92, 56, 40, 255)), wall_tint)
    back.alpha_composite(post, (1, 14))
    back.alpha_composite(post, (SIZE - 4, 14))

    front = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    front.alpha_composite(piece(COUNTER, counter_tint), (0, SIZE - 16))
    left, right = goods
    front.alpha_composite(piece(GOODS[left]), (2, SIZE - 25))
    front.alpha_composite(piece(GOODS[right]), (SIZE - 18, SIZE - 25))

    roof = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    roof.alpha_composite(piece(AWNING, awning_tint), (0, 0))
    return back, front, roof


def main():
    house = Image.open(os.path.join(PACK, "Backgrounds", "Tilesets", "TilesetHouse.png")).convert("RGBA")
    for theme, look in THEMES.items():
        back, front, roof = build(house, look)
        back.save(os.path.join(OUT, f"{theme}_Stall_back.png"))
        front.save(os.path.join(OUT, f"{theme}_Stall_front.png"))
        roof.save(os.path.join(OUT, f"{theme}_Stall_roof.png"))
        # S 칸 바닥: 예전 한 칸짜리 상점 그림 대신 그 지역 바닥 (가판대가 위를 덮음)
        Image.open(os.path.join(OUT, f"{theme}_Floor.png")).save(os.path.join(OUT, f"{theme}_Shop.png"))
    print("stalls", len(THEMES), "->", OUT)


if __name__ == "__main__":
    sys.exit(main())
