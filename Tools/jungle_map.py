# 1.1 '메아리 정글' 맵 초안 (72×88 = 숲의 4배, GDD 9-6) — 사용자 확인용 미리보기
#
#   python Tools/jungle_map.py   → Builds/maps/JungleMap.txt (글자 맵) + JungleMap_preview.png (색 미리보기, 이름표)
#
# 글자 (기존 + 정글용 새 글자 — 게임이 읽게 되는 것은 ③ 작업에서):
#   '.' 흙길  ':' 이끼 땅(조우 없음)  ',' 짙은 풀숲(보이는 몬스터 서식지)  '#' 정글 나무(막힘)  '~' 깊은 강·골짜기(막힘)
#   'D' 출입구(숲에서)  'P' 시작(야영지)  'F' 샘  'S' 가게  'E' 제단  'L' 사전 받침대  'C' 상자  'B' 빈칸두꺼비(보스)
#   'G' 보스 방 문  'K' 열쇠 받침대 (#76 규칙 그대로)
#   새: '>' '<' '^' 'v' 얕은 강(걸을 수 있음, 그 방향으로 걸으면 빨라짐)
#       'J' 나루터: 여기서 물 쪽으로 가면 뗏목에 탐 → 깊은 강 위를 스틱으로 직접 몰고(사용자 결정), 나루터에 닿으면 내림.
#           강 가운데 모래톱의 상자는 뗏목에 탄 채 [확인]. 강 위에도 보이는 몬스터(흉내문어)
#       'Y' 레버  'H' 덩굴 다리(레버를 당기면 놓임, 그 전엔 막힘)  'U' 유적 돌벽(막힘)  ';' 유적 바닥(조우 없음)
#       'T' 단어 석판  'A' 유적 제단(동료 아라)  'O' 섬의 동굴 입구(1.2 어두운 밀림 동굴 — 1.1에선 막힘)
#       'Q' 꾸밈(#86, 막힘 — 야영지 상자·통나무 / 들판 그루터기·바위 / 유적 기둥·석상, 종류는 게임이 둘레를 보고 고름)
# 마지막에 한 칸 통로를 두 칸으로 (#67 규칙 — widen_maps.py를 나무만 걷게 해서 씀). 남은 진짜 한 칸 통로 수를 알려 줌
# 검사: 줄 길이 · P 하나 · 모든 물건이 시작에서 닿음(다리·문은 열린 것으로, 걷기 + 뗏목) ·
#       문을 안 열면 보스에게 못 감 · 열쇠는 문 없이 닿음 · 한 칸 통로 경고
import os
import random
import sys
from collections import deque

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Builds", "maps")
W, H = 72, 88
WALK = set(".:,;PD<>^v")
OBJECTS = set("FSELCBKTAOYJ")
BLOCK_DECOR = "Q"
DOCKS = [(49, 70), (52, 44), (58, 44), (60, 16)]          # 나루터: 서쪽 기슭(버섯 늪) · 섬 서쪽·동쪽 · 동쪽 기슭
SANDBARS = [(53, 5), (57, 9), (52, 81), (56, 84)]        # 강 위쪽·아래쪽 모래톱 상자 (뗏목으로만, 사용자 요청)

g = [["#"] * W for _ in range(H)]
rng = random.Random(20261016)
places = []   # (이름, x, y) — 미리보기 이름표


def put(x, y, c):
    if 1 <= x < W - 1 and 1 <= y < H - 1:
        g[y][x] = c


def rect(x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(x, y, c)


def ellipse(cx, cy, rx, ry, c, only=None):
    for y in range(cy - ry, cy + ry + 1):
        for x in range(cx - rx, cx + rx + 1):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0 and 1 <= x < W - 1 and 1 <= y < H - 1:
                if only is None or g[y][x] in only:
                    g[y][x] = c


def path(points, width, c, only=None):
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        steps = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(steps + 1):
            x = round(x0 + (x1 - x0) * i / steps)
            y = round(y0 + (y1 - y0) * i / steps)
            for dy in range(width):
                for dx in range(width):
                    px, py = x + dx, y + dy
                    if 1 <= px < W - 1 and 1 <= py < H - 1 and (only is None or g[py][px] in only):
                        g[py][px] = c


def blob(cx, cy, size, c, only):
    cells = {(cx, cy)}
    frontier = [(cx, cy)]
    while len(cells) < size and frontier:
        x, y = rng.choice(frontier)
        nx, ny = x + rng.choice((-1, 0, 1)), y + rng.choice((-1, 0, 1))
        if 2 <= nx < W - 2 and 2 <= ny < H - 2 and (nx, ny) not in cells:
            cells.add((nx, ny))
            frontier.append((nx, ny))
    for x, y in cells:
        if g[y][x] in only:
            g[y][x] = c


# ---------------------------------------------------------------- 1) 땅 · 강
rect(2, 2, W - 3, H - 3, ":")
rect(50, 2, 59, H - 3, "~")                      # 깊은 강 (동쪽, 남→북) — 뗏목으로만
ellipse(55, 44, 3, 5, ":")                       # 강 가운데 섬
put(55, 39, "~"); put(55, 49, "~")               # 섬 위·아래 끝의 한 칸 뾰족한 곳은 물로 (#86)
rect(3, 46, 49, 48, ">")                         # 얕은 강 (서→동, 깊은 강으로 흘러듦) — 물살 따라 걸으면 빨라짐
places += [("깊은 강 (뗏목 직접 몰기)", 55, 24), ("얕은 강 → 물살", 18, 45), ("섬 · 동굴 입구 (1.2)", 55, 37)]

# ---------------------------------------------------------------- 2) 고대 유적 (북서) — 석판 4개 + 안쪽 제단
rect(3, 3, 28, 31, "#")
rect(4, 4, 27, 30, "U")
rect(5, 5, 26, 29, ";")
rect(10, 5, 19, 13, "U"); rect(11, 6, 18, 12, ";"); rect(14, 13, 15, 13, ";")      # 안쪽 성소 (제단)
for x0, x1, door in ((5, 9, 9), (20, 26, 20)):                                    # 석판 방 4개 (왼쪽 둘·오른쪽 둘)
    for y0, y1 in ((15, 20), (23, 29)):           # 위 방은 한 줄 올려서 방 사이 통로가 두 줄 (#86)
        rect(x0, y0, x1, y1, "U")
        rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, ";")
        rect(door, y0 + 2, door, y0 + 3, ";")
rect(14, 30, 15, 30, ";")                                                          # 유적 입구 (두 칸)
rect(3, 31, 30, 31, "#"); rect(14, 31, 15, 31, ";")
rect(3, 32, 30, 33, "~")                                                           # 골짜기
rect(2, 31, 2, 33, "#")                                                            # 골짜기 서쪽 끝 한 칸 길 막기 — 다리를 거치지 않고 돌아가지 못하게 (#86)
rect(14, 32, 15, 33, "H")                                                          # 덩굴 다리 (레버로)
places += [("고대 유적", 15, 2), ("덩굴 다리", 21, 33)]

# ---------------------------------------------------------------- 3) 보스 공터 (북쪽 가운데) — 보스 방 문 3칸
rect(29, 2, 49, 33, "#")
ellipse(39, 11, 8, 6, ":")
rect(38, 17, 40, 18, ":")
rect(38, 19, 40, 19, "G")
rect(38, 20, 40, 33, ".")
places.append(("빈칸두꺼비 공터", 39, 3))

# ---------------------------------------------------------------- 4) 남쪽: 야영지 · 골짜기 · 버섯 늪
rect(2, 59, 49, 59, "#"); rect(11, 59, 13, 59, "."); rect(40, 59, 42, 59, ".")   # 남쪽 띠와 강 사이 나무 줄 (빈틈 둘)
rect(27, 60, 28, H - 3, "~")                                                       # 야영지와 늪 사이 골짜기
rect(27, 74, 28, 75, "H")                                                          # 덩굴 다리 (지름길)
ellipse(13, 76, 10, 8, ":")
g[78][0] = "D"; rect(1, 78, 2, 79, ".")                                             # 숲에서 오는 출입구
places += [("메아리 야영지", 13, 66), ("버섯 늪", 39, 63), ("동쪽 기슭", 65, 26)]

# ---------------------------------------------------------------- 5) 흙길 (두 칸)
roads = [
    [(2, 78), (8, 78), (12, 70), (12, 60), (12, 56), (22, 52), (22, 49)],   # 야영지 → 남쪽 띠 → 얕은 강 건너는 곳
    [(22, 45), (22, 40), (14, 36), (14, 34)],                               # 강 북쪽 → 유적 다리 앞
    [(23, 42), (32, 38), (38, 34)],                                         # 강 북쪽 → 보스 길
    [(23, 54), (34, 56), (41, 58), (41, 60), (44, 66), (47, 70)],           # 남쪽 띠 → 버섯 늪 → 서쪽 나루터
    [(29, 74), (36, 76), (44, 72)],                                         # 지름길 다리 → 늪
]
for r in roads:
    path(r, 2, ".", only=":,")

# ---------------------------------------------------------------- 6) 짙은 풀숲 (보이는 몬스터 서식지) · 나무
NO_BRUSH = [(2, 60, 26, 86), (3, 3, 28, 33), (29, 2, 49, 33), (52, 39, 58, 49)]   # 야영지 · 유적 · 보스 · 섬
def free(x, y):
    return not any(x0 <= x <= x1 and y0 <= y <= y1 for x0, y0, x1, y1 in NO_BRUSH)
for cx, cy in [(8, 52), (34, 51), (44, 55), (8, 40), (30, 42), (44, 38), (35, 68), (44, 80), (64, 60), (65, 74), (64, 40),
               (16, 55), (30, 37), (36, 80), (63, 10), (66, 30), (62, 52)]:
    if free(cx, cy):
        blob(cx, cy, rng.randrange(18, 36), ",", only=":")
protected = set()
for y in range(H):
    for x in range(W):
        if g[y][x] in ".;UHG~><^v":
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    protected.add((x + dx, y + dy))
for _ in range(170):                   # 빽빽한 정글 — 나무 무더기를 많이
    x, y = rng.randrange(3, W - 3), rng.randrange(3, H - 3)
    if (x, y) in protected or not free(x, y):
        continue
    blob(x, y, rng.randrange(4, 16), "#", only=":,")


# 나무 사이 한 칸 틈 → 나무 하나를 걷어 두 칸으로 (#67 규칙 — 강·유적 벽은 건드리지 않음)
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


# ---------------------------------------------------------------- 7) 물건
def obj(c, x, y):
    if g[y][x] not in ":.,;":
        near = [(abs(xx - x) + abs(yy - y), xx, yy) for yy in range(y - 5, y + 6) for xx in range(x - 5, x + 6)
                if 2 <= xx < W - 2 and 2 <= yy < H - 2 and g[yy][xx] in ":,;"]
        assert near, (c, x, y, g[y][x])
        _, x, y = min(near)
    g[y][x] = c


obj("P", 6, 78); obj("F", 10, 71); obj("S", 17, 73); obj("E", 5, 73); obj("L", 15, 82); obj("C", 23, 84)   # 야영지
obj("Y", 24, 72)                                                    # 지름길 다리 레버 (야영지 쪽)
obj("Y", 18, 36)                                                    # 유적 다리 레버 (골짜기 앞)
for x, y in ((6, 16), (25, 16), (6, 24), (25, 24)):                 # 단어 석판 4개 — 방 위쪽 벽에 붙여서 옆에 한 칸 구석이 안 생기게 (#86)
    g[y][x] = "T"
g[8][14] = "A"                                                      # 유적 제단 (아라)
g[6][6] = "C"; g[6][25] = "C"                                       # 유적 보물
g[9][39] = "B"                                                      # 빈칸두꺼비
g[41][55] = "O"                                                     # 섬의 동굴 입구 (1.2)
for x, y in DOCKS:
    g[y][x] = "J"
for x, y in SANDBARS:                                               # 강 한가운데 모래톱 위 상자 — 뗏목에서 [확인]
    g[y][x] = "C"
obj("K", 66, 7)                                                     # 보스 방 열쇠 — 동쪽 기슭 끝 (뗏목으로, 아라가 힌트)
for x, y in [(33, 82), (47, 78), (64, 66), (67, 82), (45, 36), (5, 36), (35, 50), (66, 44)]:
    obj("C", x, y)
places += [("석판", 6, 13), ("석판", 25, 13), ("제단 (아라)", 14, 5), ("열쇠", 66, 4), ("나루터", 46, 70),
           ("강 위쪽 모래톱", 55, 12), ("강 아래쪽 모래톱", 54, 78)]


# ---------------------------------------------------------------- 7-2) 꾸밈 'Q' (#86) — 길·물건을 막지 않게 8방향이 모두 탁 트인 칸에만
def open_around(x, y):
    return all(g[y + dy][x + dx] in ":," for dy in (-1, 0, 1) for dx in (-1, 0, 1))


def deco(x, y):
    if 2 <= x < W - 2 and 2 <= y < H - 2 and open_around(x, y):
        g[y][x] = BLOCK_DECOR
        return True
    return False


def ruin_deco(x, y):
    # 유적 안: 둘레 8칸이 모두 유적 바닥일 때
    if all(g[y + dy][x + dx] == ";" for dy in (-1, 0, 1) for dx in (-1, 0, 1)):
        g[y][x] = BLOCK_DECOR


for x, y in [(8, 70), (18, 70), (20, 76), (17, 81), (8, 84), (5, 80), (21, 79), (11, 73)]:   # 야영지 — 상자·통·통나무
    deco(x, y)
for x, y in [(7, 8), (23, 8), (7, 11), (23, 11), (12, 15), (17, 15), (12, 27), (17, 27)]:  # 유적 — 기둥·석상·돌무더기
    ruin_deco(x, y)
placed = 0
for _ in range(600):                                                                     # 들판 — 그루터기·통나무·바위
    if placed >= 26:
        break
    x, y = rng.randrange(3, W - 3), rng.randrange(3, H - 3)
    if not free(x, y) or any(g[yy][xx] == BLOCK_DECOR for yy in range(y - 4, y + 5) for xx in range(x - 4, x + 5)
                             if 0 <= xx < W and 0 <= yy < H):
        continue
    if deco(x, y):
        placed += 1

# ---------------------------------------------------------------- 7-3) 한 칸 통로 → 두 칸 (#67 규칙) — widen_maps.py를 나무(#)만 걷게
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import widen_maps
widen_maps.WALK = set(".:,DP;<>^vJ")
widen_maps.convertible = lambda grid, x, y: 0 < x < len(grid[0]) - 1 and 0 < y < len(grid) - 1 and grid[y][x] == "#"
widened, kept = widen_maps.widen(["".join(r) for r in g])
g = [list(r) for r in widened]


# ---------------------------------------------------------------- 8) 검사
def river_cells():
    # 나루터에 닿는 깊은 물 덩어리 (골짜기 물은 따로라 뗏목이 못 감)
    seeds = [(x + dx, y + dy) for x, y in DOCKS for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if g[y + dy][x + dx] == "~"]
    seen, q = set(seeds), deque(seeds)
    while q:
        x, y = q.popleft()
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if n not in seen and 0 <= n[0] < W and 0 <= n[1] < H and g[n[1]][n[0]] == "~":
                seen.add(n)
                q.append(n)
    return seen


def flood(start, open_gate=True, open_bridges=True):
    # 걷기 + 뗏목: 나루터(J)에서 강으로 들어가 타고, 강에서 나루터로 들어가 내린다. 돌려주는 값 = 걸은 칸 ∪ 뗏목 칸
    walk = set(WALK) | {"J"} | ({"G"} if open_gate else set()) | ({"H"} if open_bridges else set())
    river = river_cells()
    seen = {("foot", start)}
    q = deque(seen)
    while q:
        mode, (x, y) = q.popleft()
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if not (0 <= n[0] < W and 0 <= n[1] < H):
                continue
            c = g[n[1]][n[0]]
            if mode == "foot":
                nxt = ("foot", n) if c in walk else ("raft", n) if n in river and g[y][x] == "J" else None
            else:
                nxt = ("raft", n) if n in river else ("foot", n) if c == "J" else None
            if nxt and nxt not in seen:
                seen.add(nxt)
                q.append(nxt)
    return {cell for _, cell in seen}


start = next((x, y) for y in range(H) for x in range(W) if g[y][x] == "P")
reach = flood(start)
# 못 가는 땅은 나무로 (외딴 구석이 남지 않게)
for y in range(H):
    for x in range(W):
        if g[y][x] in ":.,;" and (x, y) not in reach:
            g[y][x] = "#"
reach = flood(start)


def touches(cells, x, y):
    return any((x + dx, y + dy) in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))


problems = []
for y in range(H):
    for x in range(W):
        c = g[y][x]
        if (c in OBJECTS or c in "GH") and not touches(reach, x, y):
            problems.append((c, x, y))
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
narrow = kept   # 넓히지 못한 진짜 한 칸 통로 (물건 옆 구석·막다른 곳은 통로로 치지 않음 — widen_maps 기준)

text = "\n".join("".join(r) for r in g) + "\n"
os.makedirs(OUT, exist_ok=True)
with open(os.path.join(OUT, "JungleMap.txt"), "w", encoding="utf-8") as f:
    f.write(text)
walk = sum(1 for y in range(H) for x in range(W) if g[y][x] in WALK)
counts = {c: text.count(c) for c in "CTJYHKGOBAQ"}
print(f"{W}x{H} 걸을 칸 {walk} (숲 709칸의 약 {walk / 709:.1f}배) · 물건 {counts} · 남은 한 칸 통로 {len(narrow)}곳",
      [(seg[0], len(seg)) for seg in narrow])

# ---------------------------------------------------------------- 미리보기
COLORS = {"#": (34, 92, 46), ":": (120, 170, 80), ".": (176, 128, 84), ",": (168, 190, 60), "~": (40, 90, 150),
          ">": (110, 180, 220), "<": (110, 180, 220), "^": (110, 180, 220), "v": (110, 180, 220), "P": (176, 128, 84),
          "D": (90, 60, 40), "U": (120, 116, 104), ";": (196, 188, 160), "H": (150, 110, 60), "G": (200, 40, 40),
          "Q": (110, 78, 48)}
OBJ_COLORS = {"C": (255, 220, 40), "T": (255, 255, 255), "A": (255, 120, 200), "B": (200, 0, 0), "F": (0, 200, 255),
              "S": (0, 160, 80), "E": (160, 60, 255), "L": (250, 250, 250), "K": (255, 170, 0), "Y": (255, 90, 0),
              "J": (140, 90, 40), "O": (20, 20, 20)}
S = 10
LEGEND_W = 320
img = Image.new("RGB", (W * S + LEGEND_W, H * S), (240, 244, 236))
d = ImageDraw.Draw(img)
for y in range(H):
    for x in range(W):
        c = g[y][x]
        if c in OBJ_COLORS:
            d.rectangle([x * S, y * S, x * S + S - 1, y * S + S - 1], fill=(120, 170, 80))
            d.ellipse([x * S - 2, y * S - 2, x * S + S + 1, y * S + S + 1], fill=OBJ_COLORS[c], outline=(0, 0, 0))
        else:
            d.rectangle([x * S, y * S, x * S + S - 1, y * S + S - 1], fill=COLORS.get(c, (255, 0, 255)))
        if c in "><^v":
            d.text((x * S + 2, y * S - 2), {">": "›", "<": "‹", "^": "ˆ", "v": "ˇ"}[c], fill=(40, 90, 150))
for x, y in SANDBARS:   # 모래톱 (상자 둘레)
    d.ellipse([x * S - 6, y * S - 6, x * S + S + 5, y * S + S + 5], outline=(240, 220, 150), width=3)
try:
    font = ImageFont.truetype(os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "NotoSansKR-Bold.otf"), 17)
except OSError:
    font = None
for name, x, y in places:
    d.text((x * S, y * S), name, font=font, fill=(0, 0, 0), anchor="mm", stroke_width=3, stroke_fill=(255, 255, 255))
legend = [("C", "보물상자"), ("T", "단어 석판 (4)"), ("A", "유적 제단 (아라)"), ("B", "빈칸두꺼비 (보스)"), ("K", "보스 방 열쇠"),
          ("Y", "레버"), ("J", "나루터 (뗏목 타기·내리기)"), ("O", "동굴 입구 (1.2)"), ("F", "회복의 샘"), ("S", "가게"),
          ("E", "성유물 제단"), ("L", "사전 받침대")]
lx, ly = W * S + 16, 20
d.text((lx, ly), "메아리 정글 72×88 (초안)", font=font, fill=(0, 0, 0)); ly += 36
for c, label in legend:
    d.ellipse([lx, ly, lx + 14, ly + 14], fill=OBJ_COLORS[c], outline=(0, 0, 0))
    d.text((lx + 22, ly - 3), label, font=font, fill=(0, 0, 0)); ly += 27
for c, label in [(",", "짙은 풀숲 (몬스터 서식지)"), (".", "흙길"), (":", "이끼 땅 (조우 없음)"), ("#", "정글 나무 (막힘)"),
                 (">", "얕은 강 (물살 → 빨라짐)"), ("~", "깊은 강 · 골짜기"), ("H", "덩굴 다리 (레버)"), ("U", "유적 돌벽"),
                 (";", "유적 바닥"), ("G", "보스 방 문")]:
    d.rectangle([lx, ly, lx + 14, ly + 14], fill=COLORS[c], outline=(0, 0, 0))
    d.text((lx + 22, ly - 3), label, font=font, fill=(0, 0, 0)); ly += 27
d.text((lx, ly + 6), "깊은 강 = 나루터에서 뗏목을 타고", font=font, fill=(0, 0, 0))
d.text((lx, ly + 32), "직접 몰기 (모래톱 상자는 뗏목에서)", font=font, fill=(0, 0, 0))
img.save(os.path.join(OUT, "JungleMap_preview.png"))
print(os.path.join(OUT, "JungleMap_preview.png"))
