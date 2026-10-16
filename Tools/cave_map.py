# 1.2 '어두운 밀림 동굴' 맵 초안 (50×62 ≈ 숲 36×44의 2배 넓이, GDD 9-7) — 사용자 확인용 미리보기
#
#   python Tools/cave_map.py   → Builds/maps/CaveMap.txt (글자 맵) + CaveMap_preview.png (색 미리보기, 이름표)
#                                 + CaveMap_dark.png (어둠 미리보기: 처음 들어왔을 때 / 반딧돌을 다 밝힌 뒤)
#
# 글자 (기존 글자 그대로 + 동굴용 새 글자 하나 — 게임이 읽게 되는 것은 만들기 단계에서):
#   '#' 바위벽(막힘)  ':' 바위 바닥(조우 없음)  '.' 다져진 길(큰길 — 중요한 곳으로 이끎)  ',' 빛이끼 덤불(보이는 몬스터 서식지)
#   '~' 먹물 웅덩이·먹물 개울(막힘 — 먹물도롱뇽이 숨어 있음)  'D' 출입구(정글 섬 동굴에서)  'P' 시작(반딧불 쉼터)
#   'F' 샘  'S' 가게  'E' 성유물 제단  'L' 사전 받침대  'C' 상자  'B' 말줄임덩굴(보스)  'G' 보스 방 문  'K' 열쇠 받침대 (#76 규칙)
#   'Y' 레버 → 'H' 덩굴 다리 (정글 규칙 그대로 — 먹물 개울을 건너 쉼터로 돌아오는 지름길, 레버는 먼 쪽에)
#   'Q' 꾸밈(막힘 — 석순·버섯·광부 상자, 종류는 게임이 둘레를 보고 고름)
#   'O' 사막으로 이어지는 틈 (2.0 — 1.2에선 막힘, 보스를 이기면 무너져 열림)
#   새: 'I' 글자 반딧돌 — [확인] → 이미 본 단어 한 문제(틀려도 벌점 없이 다시) → 영영 밝아져 둘레를 비춤 (세이브)
# 마지막에 한 칸 통로를 두 칸으로 (#67 규칙 — widen_maps.py를 바위만 걷게 해서 씀)
# 검사: 줄 길이 · P 하나 · 모든 물건이 시작에서 닿음 · 다리를 안 놓아도 다 닿음(다리는 지름길일 뿐, 레버도 다리 없이 닿음) ·
#       문을 안 열면 보스에게 못 감 · 열쇠는 문 없이 닿음 · 가장자리는 바위·출입구만
import math
import os
import random
import sys
from collections import deque

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Builds", "maps")
W, H = 50, 62
WALK = set(".:,PD")
OBJECTS = set("FSELCBKYIO")
BLOCK_DECOR = "Q"
HERO_LIGHT = 3.5      # 주인공 둘레 빛 (칸)
STONE_LIGHT = 6.0     # 밝힌 반딧돌 둘레 빛 (칸)
CAMP_LIGHT = 8.5      # 쉼터 모닥불 (처음부터 밝음)

g = [["#"] * W for _ in range(H)]
rng = random.Random(20261017)
places = []   # (이름, x, y) — 미리보기 이름표


def inside(x, y, margin=1):
    return margin <= x < W - margin and margin <= y < H - margin


def put(x, y, c):
    if inside(x, y):
        g[y][x] = c


def rect(x0, y0, x1, y1, c, only=None):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if inside(x, y) and (only is None or g[y][x] in only):
                g[y][x] = c


def ellipse(cx, cy, rx, ry, c, only=None):
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0 and inside(x, y):
                if only is None or g[y][x] in only:
                    g[y][x] = c


def cavern(cx, cy, rx, ry, c=":", wobble=0.16, only="#"):
    # 울퉁불퉁한 동굴 방: 타원 가장자리를 각도마다 조금씩 들쭉날쭉하게 (늘 같은 모양 — 씨앗 고정)
    p = [rng.random() * math.tau for _ in range(3)]
    for y in range(int(cy - ry * 1.3) - 1, int(cy + ry * 1.3) + 2):
        for x in range(int(cx - rx * 1.3) - 1, int(cx + rx * 1.3) + 2):
            nx, ny = (x - cx) / rx, (y - cy) / ry
            a = math.atan2(ny, nx)
            edge = 1 + wobble * (0.5 * math.sin(3 * a + p[0]) + 0.3 * math.sin(5 * a + p[1]) + 0.2 * math.sin(7 * a + p[2]))
            if nx * nx + ny * ny <= edge * edge and inside(x, y, 2) and (only is None or g[y][x] in only):
                g[y][x] = c


def bezier(p0, p1, p2, n=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (i / n for i in range(n + 1))]


def curve(points, radius, c, only=None, step=0.25):
    # 둥근 붓으로 촘촘히 — 굴 통로 (지그재그 계단 없이)
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
        for i in range(n + 1):
            x = x0 + (x1 - x0) * i / n
            y = y0 + (y1 - y0) * i / n
            for py in range(int(y - radius) - 1, int(y + radius) + 2):
                for px in range(int(x - radius) - 1, int(x + radius) + 2):
                    if (px + 0.5 - x) ** 2 + (py + 0.5 - y) ** 2 <= radius * radius and inside(px, py, 2):
                        if only is None or g[py][px] in only:
                            g[py][px] = c


def tunnel(points, radius=1.5, c=":", only="#,"):
    # 꺾이는 점들을 부드러운 곡선으로 이어 판 굴 (가운데 점들 = 제어점)
    pts = [points[0]]
    for i in range(1, len(points) - 1):
        a = ((points[i - 1][0] + points[i][0]) / 2, (points[i - 1][1] + points[i][1]) / 2)
        b = ((points[i][0] + points[i + 1][0]) / 2, (points[i][1] + points[i + 1][1]) / 2)
        pts += bezier(a, points[i], b, n=16)
    pts.append(points[-1])
    curve(pts, radius, c, only)


def blob(cx, cy, size, c, only):
    cells = {(cx, cy)}
    frontier = [(cx, cy)]
    while len(cells) < size and frontier:
        x, y = rng.choice(frontier)
        nx, ny = x + rng.choice((-1, 0, 1)), y + rng.choice((-1, 0, 1))
        if inside(nx, ny, 2) and (nx, ny) not in cells:
            cells.add((nx, ny))
            frontier.append((nx, ny))
    for x, y in cells:
        if g[y][x] in only:
            g[y][x] = c


# ---------------------------------------------------------------- 1) 동굴 방들 (아래 → 위)
cavern(25, 53, 8, 5)            # 반딧불 쉼터 (입구 바로 안 — 모닥불로 처음부터 밝음)
cavern(26, 32, 11, 7)           # 대동굴 (가운데 큰 방 — 모든 길이 지나감)
cavern(25, 22.5, 4.5, 2.2)      # 문 앞 광장
cavern(10, 41, 7, 8)            # 먹물 호수 (서쪽)
cavern(8, 54, 5.5, 4.5)         # 개울가 굴 (남서 — 레버)
cavern(9, 20, 6, 5)             # 버섯 굴 (북서)
cavern(9, 8, 4, 3)              # 옛 광부 쉼터 (북서 끝 — 숨은 상자)
cavern(42, 46, 5.5, 4.5)        # 거미 굴 (동쪽 아래)
cavern(43, 31, 4.5, 4.5)        # 거미 굴 깊은 곳 (동쪽 가운데)
cavern(43, 56.5, 4.5, 2.8)      # 무너진 갱도 (남동 — 숨은 상자)
cavern(34, 20, 3, 2.2)          # 숨은 틈 (대동굴 북동 — 숨은 상자, 보스 방과는 안 닿음)
cavern(41, 12, 5, 4)            # 수정 굴 (북동 끝 — 보스 방 열쇠)
places += [("반딧불 쉼터", 25, 47.2), ("대동굴", 21, 25.2), ("무너진 갱도", 43, 60.2), ("숨은 틈", 34, 16.6), ("먹물 호수", 10, 31.5), ("개울가 굴", 7, 48.5), ("버섯 굴", 9, 13.5),
           ("옛 광부 쉼터", 9, 3.5), ("거미 굴", 42, 40.5), ("거미 굴 깊은 곳", 43, 26.5), ("수정 굴 (열쇠)", 41, 6.5)]

# ---------------------------------------------------------------- 2) 굴 (두 칸 이상, 둥글게 굽이침)
tunnel([(25, 49), (21, 45), (27, 41), (25, 37)], 1.6)              # 쉼터 → 대동굴 (큰길 — 굽이굽이)
tunnel([(25, 27), (25, 24)], 1.6)                                  # 대동굴 → 문 앞 광장
tunnel([(32, 54), (37, 55), (40, 50)], 1.4)                        # 쉼터 동쪽 → 거미 굴
tunnel([(44, 42), (46, 38), (44, 35)], 1.3)                        # 거미 굴 → 깊은 곳
tunnel([(39, 32), (37, 31), (35, 33)], 1.3)                        # 깊은 곳 → 대동굴 동쪽 (고리)
tunnel([(18, 35), (15.5, 37.5), (13, 38)], 1.3)                    # 대동굴 서쪽 아래 → 먹물 호수 (짧은 길)
tunnel([(39, 51), (41, 54)], 1.2)                                  # 거미 굴 길 → 무너진 갱도
tunnel([(32, 27), (34, 24), (34, 21)], 1.1)                        # 대동굴 북동 → 숨은 틈
tunnel([(44, 28), (46, 23), (43, 18), (41, 15)], 1.3)              # 깊은 곳 → 수정 굴 (먼 길)
tunnel([(16, 30), (13, 27), (11, 24)], 1.4)                        # 대동굴 서쪽 → 버섯 굴
tunnel([(8, 25), (6, 29), (8, 34)], 1.4)                           # 버섯 굴 → 먹물 호수 (서쪽 둘레)
tunnel([(9, 15), (10, 12), (9, 10)], 1.2)                          # 버섯 굴 → 옛 광부 쉼터
tunnel([(6, 48), (5, 51)], 1.4)                                    # 먹물 호수 → 개울가 굴

# ---------------------------------------------------------------- 3) 먹물 — 호수 · 개울(쉼터와 서쪽을 가름) · 작은 웅덩이 (먹물도롱뇽이 숨음)
ellipse(11, 41, 3.2, 4.2, "~")                                      # 먹물 호수
curve([(12, 45), (13.5, 48), (15, 50), (15, 58.5)], 1.0, "~", only="#:,")   # 먹물 개울: 호수 → 남쪽 끝까지 (돌아가는 길 없음)
put(13, 45, "~")                                                    # 호수와 개울 사이 한 칸 구석 메움
for cx, cy, n in [(31, 35, 7), (19, 30, 6), (41, 47, 6)]:                    # 작은 웅덩이 (대동굴 둘 · 거미 굴 하나)
    blob(cx, cy, n, "~", only=":")
rect(16, 53, 16, 54, ":")                                          # 쉼터 서쪽 끝 → 다리 머리
rect(14, 53, 15, 54, "H")                                          # 덩굴 다리 (레버로 — 쉼터로 돌아오는 지름길)
rect(12, 53, 13, 54, ":")
places += [("먹물 개울", 15, 59.6), ("덩굴 다리 (지름길)", 14, 51.4)]

# ---------------------------------------------------------------- 4) 보스 방 · 문 (3칸) — 문 말고는 바위로 둘러쌈
cavern(25, 8, 8, 5, wobble=0.1)
rect(24, 13, 26, 15, ":")
rect(24, 16, 26, 16, "G")
rect(24, 17, 26, 21, ".")
places.append(("말줄임덩굴의 방", 25, 1.6))

# ---------------------------------------------------------------- 5) 큰길 '.' (쉼터 → 대동굴 → 문 / 쉼터 → 거미 굴) · 빛이끼 덤불 ','
g[H - 1][25] = "D"; rect(24, 58, 26, 60, ".")                      # 정글 섬 동굴에서 오는 출입구
curve([(25.5, 58), (25.5, 53), (22.5, 48.5), (21.5, 45), (27, 41.5), (26.5, 37), (24, 33), (25.5, 27), (25.5, 22)], 0.9, ".", only=":")
curve([(32, 54.5), (37, 55.5), (40.5, 50)], 0.8, ".", only=":")
NO_BRUSH = [(16, 47, 34, 60), (16, 2, 34, 22), (35, 6, 47, 17), (3, 4, 14, 11), (38, 53, 48, 60)]   # 쉼터 · 보스 방·문 앞 · 수정 굴 · 옛 광부 쉼터 · 갱도


def free(x, y):
    return not any(x0 <= x <= x1 and y0 <= y <= y1 for x0, y0, x1, y1 in NO_BRUSH)


for cx, cy in [(19, 35), (31, 29), (22, 28), (5, 41), (14, 36), (8, 57), (12, 21), (5, 23), (40, 44), (45, 48), (42, 31),
               (45, 34), (46, 25), (13, 28)]:
    if free(cx, cy):
        blob(cx, cy, rng.randrange(10, 22), ",", only=":")


# 바위 사이 한 칸 틈 → 바위 하나를 걷어 두 칸으로 (#67 규칙 — 먹물·문은 건드리지 않음)
def blocked(x, y):
    return not (0 <= x < W and 0 <= y < H) or g[y][x] not in WALK


for _ in range(4):
    for y in range(2, H - 2):
        for x in range(2, W - 2):
            if g[y][x] not in ":,.":
                continue
            if blocked(x, y - 1) and blocked(x, y + 1) and not (blocked(x - 1, y) and blocked(x + 1, y)):
                for ny in (y + 1, y - 1):
                    if g[ny][x] == "#" and 2 <= ny < H - 2:
                        g[ny][x] = ":"
                        break
            if blocked(x - 1, y) and blocked(x + 1, y) and not (blocked(x, y - 1) and blocked(x, y + 1)):
                for nx in (x + 1, x - 1):
                    if g[y][nx] == "#" and 2 <= nx < W - 2:
                        g[y][nx] = ":"
                        break


# ---------------------------------------------------------------- 6) 물건
def obj(c, x, y):
    if g[y][x] not in ":.,":
        near = [(abs(xx - x) + abs(yy - y), xx, yy) for yy in range(y - 5, y + 6) for xx in range(x - 5, x + 6)
                if inside(xx, yy, 2) and g[yy][xx] in ":,"]
        assert near, (c, x, y, g[y][x])
        _, x, y = min(near)
    g[y][x] = c


obj("P", 25, 56); obj("F", 20, 52); obj("S", 30, 51); obj("E", 21, 56); obj("L", 29, 56)     # 반딧불 쉼터
obj("Y", 10, 56)                                                    # 레버 — 개울 건너 먼 쪽 (서쪽을 다 돌고 나서 쉼터로 지름길)
g[8][25] = "B"                                                      # 말줄임덩굴
g[4][25] = "O"                                                      # 사막으로 이어지는 틈 (보스 방 맨 위, 2.0)
obj("K", 43, 10)                                                    # 보스 방 열쇠 — 수정 굴 (가장 먼 곳)
STONES = [(25, 30), (4, 38), (8, 18), (5, 55), (45, 45), (46, 31), (38, 13), (21, 22)]
for x, y in STONES:                                                 # 글자 반딧돌 8개 — 방마다 하나, 벽 가까이
    obj("I", x, y)
for x, y in [(9, 7), (4, 21), (3, 57), (38, 45), (40, 35), (36, 10), (17, 33), (20, 57), (46, 57), (35, 19)]:
    obj("C", x, y)                                                  # 상자 10
STONES = [(x, y) for y in range(H) for x in range(W) if g[y][x] == "I"]


# ---------------------------------------------------------------- 6-2) 꾸밈 'Q' — 길·물건을 막지 않게 8방향이 모두 탁 트인 칸에만 (석순·버섯·광부 상자)
def open_around(x, y):
    return all(g[y + dy][x + dx] in ":," for dy in (-1, 0, 1) for dx in (-1, 0, 1))


def deco(x, y):
    if inside(x, y, 2) and open_around(x, y):
        g[y][x] = BLOCK_DECOR
        return True
    return False


for x, y in [(19, 55), (31, 53), (22, 50), (28, 50)]:              # 쉼터 — 광부 상자·통·모닥불 둘레 돌
    deco(x, y)
placed = 0
for _ in range(800):                                               # 방 안 — 석순·버섯
    if placed >= 18:
        break
    x, y = rng.randrange(3, W - 3), rng.randrange(3, H - 3)
    if not free(x, y) or any(g[yy][xx] in BLOCK_DECOR + "ICKYFSELP" for yy in range(y - 3, y + 4) for xx in range(x - 3, x + 4)
                             if 0 <= xx < W and 0 <= yy < H):
        continue
    if deco(x, y):
        placed += 1

# ---------------------------------------------------------------- 7) 한 칸 통로 → 두 칸 (#67 규칙) — widen_maps.py를 바위(#)만 걷게
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import widen_maps
widen_maps.WALK = set(".:,DP")
widen_maps.convertible = lambda grid, x, y: 0 < x < len(grid[0]) - 1 and 0 < y < len(grid) - 1 and grid[y][x] == "#"
widened, kept = widen_maps.widen(["".join(r) for r in g])
g = [list(r) for r in widened]


# ---------------------------------------------------------------- 8) 검사
def flood(start, open_gate=True, open_bridges=True):
    walk = set(WALK) | ({"G"} if open_gate else set()) | ({"H"} if open_bridges else set())
    seen = {start}
    q = deque([start])
    while q:
        x, y = q.popleft()
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if n not in seen and 0 <= n[0] < W and 0 <= n[1] < H and g[n[1]][n[0]] in walk:
                seen.add(n)
                q.append(n)
    return seen


start = next((x, y) for y in range(H) for x in range(W) if g[y][x] == "P")
reach = flood(start)
for y in range(H):                     # 못 가는 땅은 바위로 (외딴 구석이 남지 않게)
    for x in range(W):
        if g[y][x] in ":.," and (x, y) not in reach:
            g[y][x] = "#"
reach = flood(start)


def touches(cells, x, y):
    return any((x + dx, y + dy) in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))


problems = []
no_bridge = flood(start, open_bridges=False)
for y in range(H):
    for x in range(W):
        c = g[y][x]
        if (c in OBJECTS or c in "GH") and not touches(reach, x, y):
            problems.append((c, x, y))
        if c in OBJECTS and c != "B" and c != "O" and not touches(no_bridge, x, y):
            problems.append(("다리 없이 못 감", c, x, y))
        if (x in (0, W - 1) or y in (0, H - 1)) and c not in "#D":
            problems.append(("가장자리", c, x, y))
closed = flood(start, open_gate=False)
boss = next((x, y) for y in range(H) for x in range(W) if g[y][x] == "B")
key = next((x, y) for y in range(H) for x in range(W) if g[y][x] == "K")
if touches(closed, *boss):
    problems.append(("문을 안 열어도 보스에게 감",))
if not touches(closed, *key):
    problems.append(("열쇠에 문 없이 못 감",))
assert sum(r.count("P") for r in g) == 1
if problems:
    sys.exit(f"문제: {problems}")

text = "\n".join("".join(r) for r in g) + "\n"
os.makedirs(OUT, exist_ok=True)
with open(os.path.join(OUT, "CaveMap.txt"), "w", encoding="utf-8") as f:
    f.write(text)
walk = sum(1 for y in range(H) for x in range(W) if g[y][x] in WALK)
counts = {c: text.count(c) for c in "CIYHKGOBQ~,"}
print(f"{W}x{H} 걸을 칸 {walk} (숲 709칸의 약 {walk / 709:.1f}배) · {counts} · 남은 한 칸 통로 {len(kept)}곳",
      [(seg[0], len(seg)) for seg in kept])

# ---------------------------------------------------------------- 미리보기 (색 + 이름표 + 반딧돌 빛 범위)
COLORS = {"#": (58, 62, 70), ":": (128, 118, 104), ".": (170, 150, 116), ",": (96, 150, 120), "~": (36, 32, 58),
          "P": (170, 150, 116), "D": (90, 60, 40), "H": (150, 110, 60), "G": (200, 40, 40), "Q": (92, 84, 74)}
OBJ_COLORS = {"C": (255, 220, 40), "I": (120, 220, 255), "B": (200, 0, 0), "F": (0, 200, 255), "S": (0, 160, 80),
              "E": (160, 60, 255), "L": (250, 250, 250), "K": (255, 170, 0), "Y": (255, 90, 0), "O": (240, 200, 120)}
S = 14
LEGEND_W = 360
try:
    font = ImageFont.truetype(os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "NotoSansKR-Bold.otf"), 17)
    small = ImageFont.truetype(os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "NotoSansKR-Bold.otf"), 14)
except OSError:
    font = small = None


def draw_map(d):
    for y in range(H):
        for x in range(W):
            c = g[y][x]
            if c in OBJ_COLORS:
                d.rectangle([x * S, y * S, x * S + S - 1, y * S + S - 1], fill=COLORS[":"])
                d.ellipse([x * S - 2, y * S - 2, x * S + S + 1, y * S + S + 1], fill=OBJ_COLORS[c], outline=(0, 0, 0))
            else:
                d.rectangle([x * S, y * S, x * S + S - 1, y * S + S - 1], fill=COLORS.get(c, (255, 0, 255)))


img = Image.new("RGB", (W * S + LEGEND_W, H * S), (236, 234, 230))
d = ImageDraw.Draw(img)
draw_map(d)
for x, y in STONES:                    # 밝힌 반딧돌이 비추는 범위 (점선 원)
    cx, cy, r = (x + 0.5) * S, (y + 0.5) * S, STONE_LIGHT * S
    for i in range(0, 360, 12):
        d.arc([cx - r, cy - r, cx + r, cy + r], i, i + 6, fill=(120, 220, 255), width=2)
for name, x, y in places:
    d.text((x * S, y * S), name, font=font, fill=(0, 0, 0), anchor="mm", stroke_width=3, stroke_fill=(255, 255, 255))
legend = [("I", "글자 반딧돌 (8) — 단어 한 문제로 밝힘"), ("C", "보물상자 (10)"), ("K", "보스 방 열쇠"), ("B", "말줄임덩굴 (보스)"),
          ("Y", "레버 (먼 쪽 → 지름길 다리)"), ("O", "사막으로 이어지는 틈 (2.0)"), ("F", "회복의 샘"), ("S", "가게"),
          ("E", "성유물 제단"), ("L", "사전 받침대")]
lx, ly = W * S + 16, 20
d.text((lx, ly), f"어두운 밀림 동굴 {W}×{H} (초안)", font=font, fill=(0, 0, 0)); ly += 36
for c, label in legend:
    d.ellipse([lx, ly, lx + 14, ly + 14], fill=OBJ_COLORS[c], outline=(0, 0, 0))
    d.text((lx + 22, ly - 3), label, font=font, fill=(0, 0, 0)); ly += 27
for c, label in [(",", "빛이끼 덤불 (몬스터 서식지)"), (".", "다져진 길 (큰길)"), (":", "바위 바닥 (조우 없음)"), ("#", "바위벽 (막힘)"),
                 ("~", "먹물 웅덩이·개울 (도롱뇽이 숨음)"), ("H", "덩굴 다리 (레버)"), ("G", "보스 방 문"), ("Q", "석순·버섯 (꾸밈)")]:
    d.rectangle([lx, ly, lx + 14, ly + 14], fill=COLORS[c], outline=(0, 0, 0))
    d.text((lx + 22, ly - 3), label, font=font, fill=(0, 0, 0)); ly += 27
ly += 8
for line in ["점선 원 = 밝힌 반딧돌이 비추는 범위", "정글 섬 동굴(아래 출입구)로 들어옴",
             "쉼터 → 대동굴 → 문, 서쪽 고리(버섯 굴 →", "먹물 호수 → 개울가 굴 → 레버 → 다리로 쉼터)",
             "동쪽 고리(거미 굴 → 깊은 곳 → 대동굴),", "가장 먼 수정 굴에 열쇠"]:
    d.text((lx, ly), line, font=small, fill=(40, 40, 40)); ly += 22
img.save(os.path.join(OUT, "CaveMap_preview.png"))


# ---------------------------------------------------------------- 어둠 미리보기 — 왼쪽: 처음 들어왔을 때(쉼터 모닥불 + 주인공 빛), 오른쪽: 반딧돌 8개를 다 밝힌 뒤
def lightmap(lights):
    # 칸마다 밝기 0~1 (빛 가장자리 1.5칸은 부드럽게)
    lm = [[0.0] * W for _ in range(H)]
    for lx_, ly_, r in lights:
        for y in range(max(0, int(ly_ - r) - 2), min(H, int(ly_ + r) + 3)):
            for x in range(max(0, int(lx_ - r) - 2), min(W, int(lx_ + r) + 3)):
                dist = math.hypot(x - lx_, y - ly_)
                lm[y][x] = max(lm[y][x], min(1.0, max(0.0, (r - dist) / 1.5)))
    return lm


def dark_panel(lights, title):
    S2 = 8
    base = Image.new("RGB", (W * S, H * S))
    draw_map(ImageDraw.Draw(base))
    base = base.resize((W * S2, H * S2), Image.BILINEAR)
    lm = lightmap(lights)
    mask = Image.new("L", (W, H))
    mask.putdata([int(255 * (0.08 + 0.92 * lm[y][x])) for y in range(H) for x in range(W)])
    mask = mask.resize((W * S2, H * S2), Image.BILINEAR).filter(ImageFilter.GaussianBlur(3))
    dark = Image.new("RGB", base.size, (8, 8, 14))
    out = Image.composite(base, dark, mask)
    dd = ImageDraw.Draw(out)
    dd.text((8, 6), title, font=small, fill=(255, 255, 255), stroke_width=2, stroke_fill=(0, 0, 0))
    return out


camp = (25, 53, CAMP_LIGHT)
hero = (38, 52, HERO_LIGHT)                   # 주인공이 거미 굴로 가는 굴 속에 있을 때
a = dark_panel([camp, hero], "처음: 쉼터 모닥불 + 주인공 빛")
b = dark_panel([camp, hero] + [(x, y, STONE_LIGHT) for x, y in STONES], "반딧돌 8개를 다 밝힌 뒤")
both = Image.new("RGB", (a.width * 2 + 12, a.height), (236, 234, 230))
both.paste(a, (0, 0)); both.paste(b, (a.width + 12, 0))
both.save(os.path.join(OUT, "CaveMap_dark.png"))
print(os.path.join(OUT, "CaveMap_preview.png"))
print(os.path.join(OUT, "CaveMap_dark.png"))
