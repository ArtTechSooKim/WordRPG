# 비밀의 숲 (#292, GDD 9-17) — 숲의 이정표 5개를 모두 바로잡으면 숨은 안개가 걷히고 그 너머 출입구로 오는 곳.
# 사용자: "나무로 둘러싸인 넓은 공터" — 황금 너구리가 하루 한 번 나타나 춤추다 도망감 (자리 4곳 = 맵의 1·2·3·4)
#
#   python Tools/secret_forest_map.py          → Builds/maps/SecretForestMap.txt + SecretForestMap_preview.png (색 미리보기)
#   python Tools/secret_forest_map.py --apply  → SampleDataBuilder.SecretForestMap 상수 + 너구리 자리(GoldRaccoonSpots)도 바꿈
#
# 글자: # 나무  : 이끼 땅(조우 없음)  . 흙길  ~ 연못  F 회복의 샘  D 출입구(위 끝 = 숲으로)  P 시작
#       1~4 황금 너구리가 도망가 서는 자리 (맵에는 이끼 ':'로 남고, 자리는 상수 GoldRaccoonSpots로 — 맵 좌표 x = 왼쪽부터, y = 아래부터)
# 검사: 줄 길이 · 샘 하나 · 출입구 하나(위 끝) · 모든 칸이 시작에서 닿음 · 너구리 자리는 이끼 땅이고 둘레 8칸이 열려 있음 · 통로는 두 칸 너비
import math
import os
import random
import re
import sys
from collections import deque

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILDER = os.path.join(ROOT, "Assets", "Scripts", "Editor", "SampleDataBuilder.cs")
OUT = os.path.join(ROOT, "Builds", "maps")
W, H = 32, 30
WALK = set(".:PD1234")
STEPS = ((0, 1), (0, -1), (1, 0), (-1, 0))


def build():
    rng = random.Random(20261124)
    g = [["#"] * W for _ in range(H)]

    def put(x, y, c):
        g[y][x] = c

    def rect(x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                g[y][x] = c

    # 넓은 공터: 가장자리가 들쭉날쭉한 둥근 빈터 (나무가 빙 둘러섬)
    cx, cy, rx, ry = 15.5, 16.5, 13.0, 11.0
    wobble = [rng.uniform(-0.06, 0.06) for _ in range(16)]
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            angle = math.atan2(dy, dx)
            k = int((angle + math.pi) / (2 * math.pi) * len(wobble)) % len(wobble)
            if dx * dx + dy * dy <= (1.0 + wobble[k]) ** 2:
                g[y][x] = ":"
    # 가장자리 다듬기: 들쭉날쭉한 한 칸 틈·외톨이 칸이 없게 (둘레 8칸 중 많은 쪽을 따름)
    for _ in range(3):
        nxt = [row[:] for row in g]
        for y in range(1, H - 1):
            for x in range(1, W - 1):
                open8 = sum(g[y + dy][x + dx] == ":" for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dx, dy) != (0, 0))
                if g[y][x] == "#" and open8 >= 5:
                    nxt[y][x] = ":"
                elif g[y][x] == ":" and open8 <= 3:
                    nxt[y][x] = "#"
        g[:] = nxt
    # 들어오는 길 (왼쪽 끝 출입구 → 공터), 두 칸 너비 — 숲의 오른쪽 끝에서 오므로 이쪽은 왼쪽 끝 (#292 사용자: 너구리 공터는 오른쪽)
    put(0, 15, "D")
    rect(1, 15, 9, 16, ".")                 # 흙길이 공터 가장자리까지
    put(2, 15, "P")
    # 샘 (들어오자마자 위쪽 — 쉼터)
    put(8, 11, "F")
    # 연못 (왼쪽 아래)
    for y in range(19, 24):
        for x in range(4, 11):
            if ((x - 7) / 3.4) ** 2 + ((y - 21.3) / 2.3) ** 2 <= 1.0:
                g[y][x] = "~"
    # 공터 안 나무 몇 그루 (2×2) — 너구리가 그 뒤로 돌아 숨는 느낌
    for (x, y) in ((20, 9), (13, 9), (25, 19), (15, 23)):
        rect(x, y, x + 1, y + 1, "#")
    # 황금 너구리 자리 1~4: 입구 앞 공터(카메라가 돌아와도 보이게) → 오른쪽 위 → 오른쪽 아래 → 연못 옆 (공터를 한 바퀴 뛰어다니게)
    for mark, (x, y) in zip("1234", ((10, 18), (24, 12), (22, 23), (12, 21))):
        put(x, y, mark)
    return g


def cells(g, c):
    return [(x, y) for y in range(H) for x in range(W) if g[y][x] == c]


def flood(g, start):
    seen, q = {start}, deque([start])
    while q:
        x, y = q.popleft()
        for dx, dy in STEPS:
            n = (x + dx, y + dy)
            if 0 <= n[0] < W and 0 <= n[1] < H and n not in seen and g[n[1]][n[0]] in WALK:
                seen.add(n)
                q.append(n)
    return seen


def check(g):
    problems = []
    if len(g) != H or any(len(r) != W for r in g):
        problems.append("크기")
    if len(cells(g, "F")) != 1:
        problems.append("샘은 하나")
    doors = cells(g, "D")
    if doors != [(0, 15)]:
        problems.append(f"출입구 {doors}")
    start = cells(g, "P")[0]
    reach = flood(g, start)
    lonely = [(x, y) for y in range(H) for x in range(W) if g[y][x] in WALK and (x, y) not in reach]
    if lonely:
        problems.append(f"닿지 않는 칸 {lonely[:6]}")
    f = cells(g, "F")[0]
    if not any((f[0] + dx, f[1] + dy) in reach for dx, dy in STEPS):
        problems.append("샘에 못 감")
    for mark in "1234":
        spot = cells(g, mark)
        if len(spot) != 1:
            problems.append(f"너구리 자리 {mark}")
            continue
        x, y = spot[0]
        around = [g[y + dy][x + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dx, dy) != (0, 0)]
        if any(c not in WALK for c in around):
            problems.append(f"너구리 자리 {mark} 둘레가 막힘")
    # 한 칸 통로 (#67): 걷는 칸인데 좌우·위아래 둘 다 막힌 곳
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if g[y][x] not in WALK or g[y][x] == "D":
                continue
            if g[y][x - 1] not in WALK and g[y][x + 1] not in WALK and g[y - 1][x] != "D" and g[y + 1][x] != "D":
                problems.append(f"한 칸 통로 {(x, y)}")
    return problems


def spots(g):
    # 맵 좌표: x = 왼쪽부터, y = 아래부터
    return [(cells(g, m)[0][0], H - 1 - cells(g, m)[0][1]) for m in "1234"]


COLORS = {"#": (24, 62, 34), ":": (96, 150, 72), ".": (176, 146, 98), "~": (70, 130, 120), "F": (90, 180, 230),
          "D": (240, 220, 140), "P": (176, 146, 98)}


def preview(g, path):
    s = 14
    img = Image.new("RGB", (W * s, H * s), (0, 0, 0))
    d = ImageDraw.Draw(img)
    for y in range(H):
        for x in range(W):
            c = g[y][x]
            d.rectangle([x * s, y * s, x * s + s - 1, y * s + s - 1], fill=COLORS.get(c, (230, 190, 60)))
            if c in "1234":
                d.text((x * s + 4, y * s + 1), c, fill=(60, 30, 0))
    img.save(path)


def main():
    g = build()
    problems = check(g)
    lines = ["".join(r).translate(str.maketrans("1234", "::::")) for r in g]
    os.makedirs(OUT, exist_ok=True)
    open(os.path.join(OUT, "SecretForestMap.txt"), "w", encoding="utf-8").write("\n".join(lines) + "\n")
    preview(g, os.path.join(OUT, "SecretForestMap_preview.png"))
    print(f"{W}x{H}  너구리 자리 {spots(g)}")
    for p in problems:
        print("문제:", p)
    if "--apply" in sys.argv:
        if problems:
            sys.exit("문제가 있어 상수를 바꾸지 않음")
        text = open(BUILDER, encoding="utf-8").read()
        body = "".join(f'                "{line}\\n" +\n' for line in lines[:-1]) + f'                "{lines[-1]}\\n";'
        text, n = re.subn(r'(public const string SecretForestMap =\n)(.*?);\n', lambda m: m.group(1) + body + "\n", text, count=1, flags=re.S)
        if n != 1:
            sys.exit("SecretForestMap 상수를 찾지 못함")
        spot_text = ", ".join(f"new Vector2Int({x}, {y})" for x, y in spots(g))
        text, n = re.subn(r'(public static readonly Vector2Int\[\] GoldRaccoonSpots = \{ )(.*?)( \};)', lambda m: m.group(1) + spot_text + m.group(3), text, count=1)
        if n != 1:
            sys.exit("GoldRaccoonSpots를 찾지 못함")
        open(BUILDER, "w", encoding="utf-8", newline="").write(text)
        print("SampleDataBuilder.SecretForestMap · GoldRaccoonSpots 바꿈")


if __name__ == "__main__":
    main()
