# 3장 '말모래 사막' 맵 생성 (144×176 = 숲의 16배, GDD 9-5) — 손으로 칠 수 없는 크기라 도형으로 그림
#
#   python Tools/desert_map.py   → Builds/maps/DesertMap.txt (글자 맵) + DesertMap_preview.png (색 미리보기, 이름표)
#
# 글자 (기존 + 사막용 새 글자 — 게임이 읽게 되는 것은 1.1 작업에서):
#   '.' 모래길  ':' 단단한 모래(조우 없음)  ',' 마른 덤불(조우)  '#' 모래 언덕·바위·야자수(막힘)  '~' 물(오아시스·호수)
#   'D' 출입구(숲에서)  'P' 시작(야영지)  'F' 샘  'S' 가게  'E' 제단  'L' 사전 받침대  'C' 상자  'B' 백지룡(최종 보스)
#   새: 'W' 이정표(빠른 이동)  'M' 중간 보스(비석 수호자)  'G' 신전 문(비석 셋이면 열림)  'R' 랜드마크(큰 그림이 덮는 막힌 칸)
# 검사: 줄 길이 · P 하나 · 모든 물건이 시작에서 닿음(신전 문은 열린 것으로) · 못 가는 걸을 칸 없음 · 한 칸 통로는 두 칸으로(widen_maps)
import math
import os
import random
import sys
from collections import deque

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "Tools"))
import widen_maps  # noqa: E402

OUT = os.path.join(ROOT, "Builds", "maps")
W, H = 144, 176
WALK = set(".:,PDG")          # G(신전 문)는 열린 것으로 보고 검사
OBJECTS = set("FSELCBWM")

g = [["#"] * W for _ in range(H)]
rng = random.Random(20261015)
landmarks = []   # (이름, x0, y0, x1, y1) — 미리보기 이름표 · 나중에 게임 데이터
places = []      # (이름, x, y) — 미리보기 이름표


def inside(x, y, margin=0):
    return margin <= x < W - margin and margin <= y < H - margin


def put(x, y, c, margin=2):
    if inside(x, y, margin):
        g[y][x] = c


def rect(x0, y0, x1, y1, c, margin=2):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(x, y, c, margin)


def ellipse(cx, cy, rx, ry, c, only=None, margin=2):
    for y in range(cy - ry, cy + ry + 1):
        for x in range(cx - rx, cx + rx + 1):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0 and inside(x, y, margin):
                if only is None or g[y][x] in only:
                    g[y][x] = c


def path(points, width, c, only=None):
    # 꺾은선을 width 굵기로 (정사각 붓)
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        steps = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(steps + 1):
            x = round(x0 + (x1 - x0) * i / steps)
            y = round(y0 + (y1 - y0) * i / steps)
            for dy in range(width):
                for dx in range(width):
                    px, py = x + dx - width // 2, y + dy - width // 2
                    if inside(px, py, 2) and (only is None or g[py][px] in only):
                        g[py][px] = c


def curve(points, radius, c, only=None, step=0.25):
    # 둥근 붓으로 촘촘히 (곡선이 계단처럼 보이지 않게 — 물음표 협곡 #106)
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


def arc(cx, cy, r, a0, a1, n=80):
    # 원호 위의 점들 (각도는 도, 위가 +y인 수학 각도 — 맵은 아래로 y가 커지므로 뒤집음)
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy - r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


def bezier(p0, p1, p2, n=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (i / n for i in range(n + 1))]


def blob(cx, cy, size, c, only):
    # 울퉁불퉁한 덩어리 (모래 언덕·덤불)
    cells = {(cx, cy)}
    frontier = [(cx, cy)]
    while len(cells) < size and frontier:
        x, y = rng.choice(frontier)
        nx, ny = x + rng.choice((-1, 0, 1)), y + rng.choice((-1, 0, 1))
        if inside(nx, ny, 3) and (nx, ny) not in cells:
            cells.add((nx, ny))
            frontier.append((nx, ny))
    for x, y in cells:
        if g[y][x] in only:
            g[y][x] = c


def landmark(name, x0, y0, x1, y1):
    rect(x0, y0, x1, y1, "R")
    landmarks.append((name, x0, y0, x1, y1))


# ---------------------------------------------------------------- 1) 넓은 모래벌판
rect(3, 3, W - 4, H - 4, ":")
# 가장자리를 울퉁불퉁하게 (절벽·큰 언덕)
for _ in range(60):
    side = rng.choice("NSEW")
    if side == "N":
        blob(rng.randrange(4, W - 4), rng.randrange(3, 6), rng.randrange(6, 22), "#", ":")
    elif side == "S":
        blob(rng.randrange(4, W - 4), rng.randrange(H - 6, H - 3), rng.randrange(6, 22), "#", ":")
    elif side == "W":
        blob(rng.randrange(3, 6), rng.randrange(4, H - 4), rng.randrange(6, 22), "#", ":")
    else:
        blob(rng.randrange(W - 6, W - 3), rng.randrange(4, H - 4), rng.randrange(6, 22), "#", ":")

# ---------------------------------------------------------------- 2) 물음표 협곡 (동쪽) — 바위 덩어리를 '?' 모양 길로 파냄
# #106 사용자: 더 물음표처럼 굴곡 — 둥근 갈고리(원호) → 안쪽으로 휘어 내려오는 목 → 곧은 줄기 → 떨어진 점.
# 입구는 점(상자 공터)으로 들어오고, 점과 줄기는 좁은 모래 틈(두 칸)으로만 이어져 점이 따로 떨어져 보임. 갈고리 끝 = 뿌리 비석 수호자
rect(98, 36, W - 4, 122, "#")
QX, QY, QR = 117, 58, 12                                   # 갈고리 원의 가운데·반지름
hook = arc(QX, QY, QR, 200, -40)                           # 왼쪽 아래에서 시작 → 위로 넘어 → 오른쪽 아래까지
neck = bezier(hook[-1], (QX + 1, QY + 17), (QX, QY + 26))  # 안쪽으로 휘며 가운데로 내려옴
stem = [(QX, QY + 26), (QX, QY + 37)]                      # 곧은 줄기
curve(hook + neck[1:] + stem[1:], 2.3, ",")
ellipse(QX, QY + 46, 5, 4, ":")                            # '?'의 점 — 상자가 있는 둥근 공터
path([(QX, QY + 39), (QX, QY + 42)], 2, ".")              # 점과 줄기 사이 짧고 좁은 틈 (모래길)
hx, hy = hook[0]
ellipse(round(hx) - 1, round(hy) + 2, 5, 4, ":")          # 갈고리 끝 공터 — 뿌리 비석 수호자
path([(97, QY + 46), (QX - 5, QY + 46)], 2, ".")          # 입구: 오아시스 쪽에서 점 공터로
places.append(("물음표 협곡", 118, 70))

# ---------------------------------------------------------------- 3) 신전 (북쪽 끝)
rect(50, 5, 94, 34, "#")
rect(56, 9, 88, 29, ":")
rect(60, 12, 84, 26, ".")
rect(70, 30, 73, 34, ":")               # 문 안쪽 통로
rect(71, 34, 72, 34, "G")               # 신전 문 (두 칸)
rect(48, 35, 96, 47, ":")               # 신전 앞마당
places.append(("잊힌 문자의 신전", 72, 20))

# ---------------------------------------------------------------- 4) 쉼표 오아시스 (가운데 마을)
ellipse(72, 96, 24, 17, ":")
ellipse(72, 96, 9, 5, "~")              # 오아시스 연못
for x0, y0 in [(52, 83), (88, 83), (50, 106), (90, 106)]:
    landmark("모래 집", x0, y0, x0 + 3, y0 + 2)
for x0, y0 in [(66, 88), (77, 88), (66, 103), (77, 103)]:   # 연못가 야자수 (큰 그림 한 장)
    landmark("야자수", x0, y0, x0 + 1, y0 + 1)
places.append(("쉼표 오아시스", 72, 88))

# ---------------------------------------------------------------- 5) 큰길 (모래길, 두 칸)
roads = [
    [(3, 158), (20, 158), (40, 150), (58, 128), (68, 114), (72, 108)],   # 입구 야영지 → 오아시스 남쪽
    [(60, 96), (46, 92), (34, 84), (24, 78)],                            # 오아시스 서쪽 → 쌍둥이 탑
    [(84, 96), (92, 100), (97, 104)],                                    # 오아시스 동쪽 → 협곡 입구(물음표의 점)
    [(82, 106), (90, 124), (92, 140)],                                   # 오아시스 남동 → 마침표 호수
    [(72, 84), (64, 74), (64, 56), (68, 48), (72, 47)],                  # 오아시스 북쪽 → 느낌표 바위 옆 → 신전 앞마당
]
for r in roads:
    path(r, 2, ".", only=":,")

# ---------------------------------------------------------------- 6) 랜드마크
landmark("느낌표 바위", 70, 58, 75, 68)     # 막대
rect(70, 69, 75, 70, ":")
landmark("느낌표 바위", 71, 71, 74, 73)     # 점 (같은 이름 — 그림 하나로 덮음)
ellipse(23, 70, 18, 15, ":")
landmark("따옴표 쌍둥이 탑", 15, 58, 19, 66)
landmark("따옴표 쌍둥이 탑", 27, 58, 31, 66)

# ---------------------------------------------------------------- 7) 마침표 호수 (남동)
ellipse(114, 150, 16, 12, "~")
ellipse(114, 150, 5, 4, ":")            # 섬
path([(92, 150), (110, 150)], 2, ".")   # 여울 (두 칸)
places.append(("마침표 호수", 114, 136))

# ---------------------------------------------------------------- 8) 입구 야영지 (남서)
ellipse(12, 158, 9, 8, ":")
g[158][0] = "D"                         # 숲에서 오는 출입구 (서쪽 가장자리, 한 칸 — 다른 맵과 같음)
g[158][1] = g[158][2] = g[157][1] = g[157][2] = "."
places.append(("모래바람 야영지", 12, 148))

# ---------------------------------------------------------------- 9) 마른 덤불 (조우) — 큰길마다 한 번은 지나가는 띠 + 벌판 군데군데
for cx, cy, rx, ry in [(46, 140, 6, 5), (40, 90, 5, 6), (92, 120, 6, 5), (64, 62, 6, 5), (95, 99, 3, 4)]:
    ellipse(cx, cy, rx, ry, ",", only=":.")
NO_BRUSH = [(46, 77, 98, 115), (2, 148, 22, 168), (46, 33, 98, 50)]   # 마을 · 야영지 · 신전 앞마당은 조우 없음
for _ in range(70):
    x, y = rng.randrange(6, W - 6), rng.randrange(6, H - 6)
    if any(x0 - 6 <= x <= x1 + 6 and y0 - 6 <= y <= y1 + 6 for x0, y0, x1, y1 in NO_BRUSH):
        continue
    blob(x, y, rng.randrange(8, 40), ",", only=":")

# ---------------------------------------------------------------- 10) 모래 언덕 (막힘, 풍경) — 길·마을·물건 근처는 비움
protected = set()
for y in range(H):
    for x in range(W):
        if g[y][x] in ".~R":
            for dy in range(-3, 4):
                for dx in range(-3, 4):
                    protected.add((x + dx, y + dy))
for x0, y0, x1, y1 in [(45, 76, 99, 116), (2, 145, 24, 170), (46, 33, 98, 50), (0, 50, 46, 90), (90, 130, 140, 170), (96, 34, 143, 124)]:
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            protected.add((x, y))
def ridge(x, y, length, angle, thick):
    for _ in range(length):
        for dy in range(-thick, thick + 1):
            for dx in range(-thick, thick + 1):
                if dx * dx + dy * dy <= thick * thick + rng.random() * 2:
                    px, py = round(x) + dx, round(y) + dy
                    if inside(px, py, 3) and g[py][px] in ":," and (px, py) not in protected:
                        g[py][px] = "#"
        angle += rng.uniform(-0.35, 0.35)
        x += math.cos(angle)
        y += math.sin(angle)


for _ in range(40):                     # 길게 이어진 모래 언덕 줄기
    x, y = rng.randrange(8, W - 8), rng.randrange(8, H - 8)
    if (x, y) not in protected:
        ridge(x, y, rng.randrange(10, 28), rng.uniform(0, 2 * math.pi), rng.choice((1, 1, 2)))
for _ in range(14):                     # 바위 무더기
    x, y = rng.randrange(8, W - 8), rng.randrange(8, H - 8)
    if (x, y) not in protected:
        ellipse(x, y, rng.randrange(2, 5), rng.randrange(2, 4), "#", only=":,")
for _ in range(2):                      # 외톨이 점 지우기 (걸을 때 자꾸 걸리지 않게)
    for y in range(3, H - 3):
        for x in range(3, W - 3):
            if g[y][x] == "#" and (x, y) not in protected:
                n = sum(g[y + dy][x + dx] == "#" for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dx or dy)
                if n < 3:
                    g[y][x] = ":"

# ---------------------------------------------------------------- 11) 물건
def obj(c, x, y):
    # 그 칸이 막혀 있으면 가장 가까운 모래 칸에 (반경 6칸 안)
    if g[y][x] not in ":.,":
        near = [(abs(xx - x) + abs(yy - y), xx, yy) for yy in range(y - 6, y + 7) for xx in range(x - 6, x + 7)
                if inside(xx, yy, 3) and g[yy][xx] in ":,"]
        assert near, (c, x, y, g[y][x])
        _, x, y = min(near)
    g[y][x] = c


obj("P", 8, 158)
obj("F", 12, 152); obj("W", 16, 163); obj("C", 5, 165)                     # 야영지
obj("F", 62, 88); obj("S", 82, 88); obj("E", 62, 104); obj("L", 82, 104)   # 오아시스 마을
obj("W", 72, 110)
obj("W", 23, 80); obj("M", 23, 66); obj("C", 23, 59)                        # 쌍둥이 탑 (앞말 비석)
obj("M", 104, 66); obj("C", 117, 104); obj("W", 95, 106)                    # 물음표 협곡 (뿌리 비석 — 갈고리 끝, 상자 — 점)
obj("M", 114, 150); obj("W", 90, 146); obj("C", 117, 152)                   # 마침표 호수 (뒷말 비석)
obj("W", 60, 40); obj("B", 72, 15)                                          # 신전
places += [("앞말 비석", 23, 63), ("뿌리 비석", 104, 57), ("뒷말 비석", 114, 146), ("백지룡", 72, 12)]


def flood(start):
    seen = {start}
    q = deque([start])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in seen and 0 <= n[0] < W and 0 <= n[1] < H and g[n[1]][n[0]] in WALK:
                seen.add(n)
                q.append(n)
    return seen


def start_cell():
    return next((x, y) for y in range(H) for x in range(W) if g[y][x] == "P")


# 숨은 상자들 (구석·언덕 뒤) — 시작에서 닿는 모래 칸 중 가장 가까운 곳
reach = flood(start_cell())
for x, y in [(8, 8), (136, 8), (8, 120), (40, 170), (136, 170), (60, 160), (140, 128), (90, 60), (36, 40),
             (52, 30), (92, 30), (120, 30), (130, 92)]:
    best = min((abs(xx - x) + abs(yy - y), xx, yy) for (xx, yy) in reach if g[yy][xx] == ":")
    obj("C", best[1], best[2])


# ---------------------------------------------------------------- 12) 정리 · 검사


def cleanup():
    reach = flood(start_cell())
    for y in range(H):
        for x in range(W):
            if g[y][x] in ":.," and (x, y) not in reach:
                g[y][x] = "#"
    return reach


cleanup()
# 한 칸 통로 → 두 칸 (#67 규칙)
rows, _ = widen_maps.widen(["".join(r) for r in g])
g = [list(r) for r in rows]
reach = cleanup()

problems = []
for y in range(H):
    for x in range(W):
        c = g[y][x]
        if c in OBJECTS or c == "G":
            if not any((x + dx, y + dy) in reach for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                problems.append((c, x, y))
        if (x in (0, W - 1) or y in (0, H - 1)) and c not in "#D":
            problems.append(("가장자리", x, y))
assert sum(r.count("P") for r in g) == 1
if problems:
    sys.exit(f"닿지 않는 물건: {problems}")

text = "\n".join("".join(r) for r in g) + "\n"
os.makedirs(OUT, exist_ok=True)
with open(os.path.join(OUT, "DesertMap.txt"), "w", encoding="utf-8") as f:
    f.write(text)

counts = {c: text.count(c) for c in "CWMBFSELDG"}
walk = sum(1 for y in range(H) for x in range(W) if g[y][x] in ":.,PD")
print(f"{W}x{H} 걸을 칸 {walk} (숲 709칸의 약 {walk / 709:.0f}배) · 물건 {counts}")

# ---------------------------------------------------------------- 미리보기
COLORS = {"#": (176, 132, 82), ":": (240, 214, 160), ".": (224, 190, 128), ",": (150, 160, 80), "~": (90, 170, 220),
          "P": (224, 190, 128), "D": (90, 60, 40), "R": (120, 80, 50), "G": (200, 40, 40)}
OBJ_COLORS = {"C": (255, 220, 40), "W": (40, 120, 255), "M": (220, 40, 200), "B": (200, 0, 0), "F": (0, 200, 255),
              "S": (0, 160, 80), "E": (160, 60, 255), "L": (255, 255, 255)}
S = 6
LEGEND_W = 210
img = Image.new("RGB", (W * S + LEGEND_W, H * S), (250, 246, 236))
d = ImageDraw.Draw(img)
for y in range(H):
    for x in range(W):
        c = g[y][x]
        if c in OBJ_COLORS:
            d.rectangle([x * S, y * S, x * S + S - 1, y * S + S - 1], fill=(240, 214, 160))
            d.ellipse([x * S - 2, y * S - 2, x * S + S + 1, y * S + S + 1], fill=OBJ_COLORS[c], outline=(0, 0, 0))
        else:
            d.rectangle([x * S, y * S, x * S + S - 1, y * S + S - 1], fill=COLORS.get(c, (255, 0, 255)))
try:
    font = ImageFont.truetype(os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "NotoSansKR-Bold.otf"), 15)
except OSError:
    font = None
for name, x, y in places:
    d.text((x * S, y * S), name, font=font, fill=(0, 0, 0), anchor="mm", stroke_width=3, stroke_fill=(255, 255, 255))
done = {"야자수", "모래 집"}   # 작은 장식은 이름표 생략
for name, x0, y0, x1, y1 in landmarks:
    if name in done:
        continue
    done.add(name)
    d.text(((x0 + x1) / 2 * S, (y0 - 2) * S), name, font=font, fill=(80, 20, 0), anchor="mm", stroke_width=3, stroke_fill=(255, 255, 255))
legend = [("C", "보물상자"), ("W", "이정표 (빠른 이동)"), ("M", "중간 보스 (비석)"), ("B", "백지룡"), ("F", "회복의 샘"),
          ("S", "가게"), ("E", "성유물 제단"), ("L", "사전 받침대")]
lx, ly = W * S + 16, 20
d.text((lx, ly), "말모래 사막 144×176", font=font, fill=(0, 0, 0)); ly += 34
for c, label in legend:
    d.ellipse([lx, ly, lx + 14, ly + 14], fill=OBJ_COLORS[c], outline=(0, 0, 0))
    d.text((lx + 22, ly - 2), label, font=font, fill=(0, 0, 0)); ly += 26
for c, label in [(",", "마른 덤불 (조우)"), (".", "모래길"), (":", "모래 (조우 없음)"), ("#", "언덕·바위 (막힘)"),
                 ("~", "물"), ("R", "랜드마크"), ("G", "신전 문")]:
    d.rectangle([lx, ly, lx + 14, ly + 14], fill=COLORS[c], outline=(0, 0, 0))
    d.text((lx + 22, ly - 2), label, font=font, fill=(0, 0, 0)); ly += 26
img.save(os.path.join(OUT, "DesertMap_preview.png"))
print(os.path.join(OUT, "DesertMap_preview.png"))
