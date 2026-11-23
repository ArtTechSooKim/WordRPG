# 1.4 숲 넓히기 + 뒤섞인 이정표 미로 (#279, GDD 9-15) — 숲 36×44 → 72×44 (넓이 2배)
#
#   python Tools/forest_map.py          → Builds/maps/ForestMap.txt (글자 맵) + ForestMap_preview.png (색 미리보기, 이름표)
#   python Tools/forest_map.py --apply  → SampleDataBuilder.ForestMap 상수도 바꿈 (에셋은 Create Sample Data가 위치를 보고 옮김)
#
# 예전 세이브를 지키려고 오른쪽으로만 넓힌다: 맵 좌표는 x = 왼쪽부터, y = 아래부터라서 위·오른쪽에 붙이면
# 있던 칸의 (x, y)가 그대로 → 상자 id("forest:x,y")·탐험 기록·서 있던 자리가 그대로 남는다. 아래로 넓히면 y가 밀려서 안 됨.
#
# 예전 숲(왼쪽 36칸)에서 바꾸는 것 (벽 → 길만, 걸을 수 있던 칸은 막지 않음):
#   · 버섯 공터 오른쪽 벽 (35, 33~34줄) → 미로 입구
#   · 버섯 공터 아래 벽 (34~35, 40~41줄) → 지름길 안개 'M' (미로 끝 공터 안쪽의 이정표를 바로잡으면 열림 — 밖에서는 못 엶)
#   · 가운데 이끼밭 막다른 곳의 열쇠 'K' (14, 14줄) → 이끼 ':' (열쇠는 미로 끝 공터로 옮김)
#
# 새 글자 (게임이 읽는 것은 FieldTiles 표):
#   'X' 이정표 — [확인] → 글자가 뒤섞인 단어의 바른 철자 고르기 (틀려도 벌점 없이 다시) → 바로잡으면 가장 가까운 'M' 덩어리가 걷힘
#   'M' 헷갈 안개 — 바로잡기 전엔 막힘(부딪히면 안개 속을 맴돌다 제자리), 맡은 이정표를 바로잡으면 걸을 수 있음 (덩굴 다리 'H'와 같은 방식)
#   'W' 맴도는 안개 — 늘 막힘, 가짜 길 (부딪히면 제자리)
#   'V' 숨은 안개 (#292) — 가짜 길과 똑같이 보이고 막힘, 이정표를 모두 바로잡으면 걷힘 → 북쪽 열쇠 공터 (보스 방 열쇠)
#   'Z' 보스 뒤 숨은 안개 (#292) — 가짜 길과 똑같이 보이고 막힘, 숲 보스를 이긴 뒤 걷힘 → 오른쪽 끝 출입구 = 비밀의 숲 (황금 너구리)
# 검사: 줄 길이 · 이정표를 차례로 바로잡으면 모두 닿음 · 바로잡기 전엔 열쇠에 못 감 · 안개 덩어리마다 맡은 이정표 하나 ·
#       모든 상자·열쇠가 닿음 · 문을 안 열면 보스에게 못 감 · 예전 칸은 위 세 곳만 바뀜
import os
import re
import sys
from collections import deque

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILDER = os.path.join(ROOT, "Assets", "Scripts", "Editor", "SampleDataBuilder.cs")
OUT = os.path.join(ROOT, "Builds", "maps")
OLD_W, W, H = 36, 72, 44
WALK = set(".:,PDNn")
OPENS_WHEN_FIXED = "M"


def read_const():
    text = open(BUILDER, encoding="utf-8").read()
    m = re.search(r'public const string ForestMap =\s*\n((?:\s*"[^"]*\\n" \+?\s*\n)+?)\s*"[^"]*\\n";', text)
    block = re.search(r'public const string ForestMap =(.*?);\n', text, re.S).group(1)
    rows = re.findall(r'"([^"]*)\\n"', block)
    return text, rows


def build(old_rows):
    old = [list(r[:OLD_W]) for r in old_rows]
    g = [row + ["#"] * (W - OLD_W) for row in old]
    places = []

    def put(x, y, c):
        g[y][x] = c

    def rect(x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                g[y][x] = c

    def L(x):  # 새 땅 안의 x (0 = 예전 숲 오른쪽 벽 바로 옆)
        return OLD_W + x

    # 예전 숲에서 바꾸는 세 곳
    rect(35, 33, 35, 34, ":")               # 미로 입구
    rect(34, 40, 35, 41, "M")               # 지름길 안개 (안쪽 이정표 ⑤)
    if g[14][14] == "K":
        put(14, 14, ":")                    # 열쇠는 미로 끝으로

    # ① 입구 고사리밭 (새 땅 왼쪽 아래)
    rect(L(0), 33, L(1), 34, ":")
    rect(L(1), 29, L(9), 35, ",")
    rect(L(2), 31, L(4), 33, ":")
    rect(L(7), 34, L(8), 35, ":")
    places.append(("입구 고사리밭", L(5), 32))
    put(L(4), 28, "X")                      # 이정표 ①
    rect(L(5), 27, L(6), 28, "M")
    rect(L(1), 26, L(2), 28, "W")
    rect(L(5), 23, L(6), 26, ".")

    # ② 늪가 덤불 (왼쪽 가운데)
    rect(L(1), 15, L(10), 22, ",")
    rect(L(3), 17, L(8), 20, ":")
    rect(L(6), 18, L(7), 19, "~")
    put(L(1), 14, "C")                      # 늪가 상자
    places.append(("늪가 덤불", L(5), 21))
    put(L(8), 14, "X")                      # 이정표 ②
    rect(L(9), 13, L(10), 14, "M")
    rect(L(5), 12, L(6), 14, "W")
    rect(L(9), 11, L(10), 12, ".")

    # ③ 이끼 언덕 (왼쪽 위)
    rect(L(1), 2, L(12), 10, ":")
    rect(L(1), 6, L(5), 10, ",")
    rect(L(8), 2, L(12), 4, ",")
    put(L(1), 1, "C")                       # 언덕 끝 상자
    places.append(("이끼 언덕", L(6), 6))
    put(L(13), 4, "X")                      # 이정표 ③
    rect(L(13), 5, L(14), 6, "M")
    rect(L(15), 5, L(15), 6, ".")
    rect(L(13), 8, L(14), 9, "W")

    # ④ 고사리 능선 (오른쪽 위)
    rect(L(16), 2, L(33), 9, ",")
    rect(L(16), 5, L(22), 6, ":")
    rect(L(27), 2, L(33), 4, ":")
    put(L(33), 1, "C")                      # 능선 끝 상자
    places.append(("고사리 능선", L(25), 5))
    put(L(24), 10, "X")                     # 이정표 ④
    rect(L(25), 10, L(26), 11, "M")
    rect(L(25), 12, L(26), 13, ".")
    rect(L(20), 10, L(21), 12, "W")
    rect(L(30), 10, L(31), 11, "W")

    # ⑤ 안개 골짜기 (오른쪽 큰 골짜기 — 늪 웅덩이)
    rect(L(14), 14, L(33), 36, ",")
    rect(L(16), 16, L(31), 34, ":")
    for y in range(18, 33):
        for x in range(L(18), L(31)):
            if ((x - L(24)) / 5.5) ** 2 + ((y - 25) / 5.0) ** 2 <= 1.0:
                put(x, y, "~")
    rect(L(14), 14, L(15), 20, ",")
    rect(L(30), 27, L(33), 33, ",")
    put(L(34), 20, "C")                     # 골짜기 동쪽 상자
    places.append(("안개 골짜기", L(24), 33))
    rect(L(14), 37, L(15), 40, ".")
    rect(L(10), 39, L(15), 40, ".")

    # ⑥ 열쇠 공터 (왼쪽 아래 — 버섯 공터 바로 옆, 안쪽 이정표 ⑤로 지름길)
    rect(L(0), 38, L(9), 42, ":")
    put(L(5), 37, "K")
    put(L(9), 42, "C")                      # 공터 상자
    put(L(0), 42, "X")                      # 이정표 ⑤ (지름길)
    places.append(("열쇠 공터", L(5), 40))
    roughen(g)
    # 비밀 공터 둘 (#292 사용자: 열쇠 공터와 너구리 공터를 따로 — 콘텐츠를 한 번에 소비하지 않게). 다듬기(roughen) 뒤라 미로 모양은 그대로
    # ① 열쇠 공터 (사용자: 1.3 때 열쇠 자리의 위쪽): 가운데 이끼밭 위 고사리밭 → 숨은 안개 'V' → 북쪽 벽 속 공터. 보스 방 열쇠가 미로 끝에서 이리로
    put(L(5), 37, ":")                      # 미로 끝 공터의 열쇠 받침대 → 이끼 (공터·상자·지름길 이정표 ⑤는 그대로)
    rect(11, 1, 19, 1, ":")
    rect(9, 2, 21, 3, ":")
    put(15, 2, "K")
    rect(10, 4, 11, 4, "V")
    places.append(("열쇠 공터", 15, 2))
    # ② 너구리 공터 가는 길 (사용자: 오른쪽): 안개 골짜기 오른쪽 아래 → 보스 뒤 숨은 안개 'Z' → 맵 오른쪽 끝 출입구 (숲의 세 번째 출입구 —
    #    앞의 둘(정글 0 · 초원 1)보다 아래라 번호가 안 밀림)
    rect(68, 37, 69, 38, "Z")
    rect(68, 39, 70, 40, ".")
    put(71, 39, "D")
    places.append(("비밀의 숲으로", 66, 40))
    return g, places


SPECIAL = set("XMWCKD.")


def roughen(g, seed=20261121):
    # 네모난 새 땅을 예전 숲처럼 들쭉날쭉하게: 가장자리 덤불이 파고들고 · 안쪽에 나무 덤불 몇 무더기.
    # 통로는 두 칸 너비 그대로 (#67) — 깊이가 3칸 넘는 곳만 파고들고, 덤불 둘레 2칸은 비워 둠
    import random
    rng = random.Random(seed)

    def at(x, y):
        return g[y][x] if 0 <= x < W and 0 <= y < H else "#"

    def near_special(x, y, r):
        return any(at(x + dx, y + dy) in SPECIAL for dy in range(-r, r + 1) for dx in range(-r, r + 1))

    open_ = set(",:")
    bites = []
    for y in range(1, H - 1):
        for x in range(OLD_W + 1, W - 1):
            if at(x, y) not in open_ or near_special(x, y, 2):
                continue
            for dx, dy in STEPS:
                if at(x + dx, y + dy) == "#" and all(at(x - dx * k, y - dy * k) in open_ for k in (1, 2, 3)):
                    if rng.random() < 0.28:
                        bites.append((x, y))
                    break
    for x, y in bites:
        g[y][x] = "#"
    # 파고든 칸 옆이 한 칸 틈이 되면 되돌림
    for x, y in bites:
        for dx, dy in STEPS:
            nx, ny = x + dx, y + dy
            if at(nx, ny) in open_ and at(nx + dx, ny + dy) == "#":
                g[y][x] = ","
                break
    # 안쪽 나무 덤불 (2×2) — 둘레 2칸이 모두 열린 땅일 때만
    tries = 0
    placed = 0
    while tries < 400 and placed < 9:
        tries += 1
        x, y = rng.randrange(OLD_W + 2, W - 3), rng.randrange(2, H - 3)
        if all(at(x + dx, y + dy) in open_ for dy in range(-2, 4) for dx in range(-2, 4)):
            for dy in (0, 1):
                for dx in (0, 1):
                    g[y + dy][x + dx] = "#"
            placed += 1


# ---------- 검사 ----------
STEPS = ((0, 1), (0, -1), (1, 0), (-1, 0))


def cells(g, c):
    return [(x, y) for y in range(H) for x in range(W) if g[y][x] == c]


def groups(g, c):
    seen, out = set(), []
    for start in cells(g, c):
        if start in seen:
            continue
        seen.add(start)
        group, q = [start], deque([start])
        while q:
            x, y = q.popleft()
            for dx, dy in STEPS:
                n = (x + dx, y + dy)
                if 0 <= n[0] < W and 0 <= n[1] < H and n not in seen and g[n[1]][n[0]] == c:
                    seen.add(n)
                    group.append(n)
                    q.append(n)
        out.append(group)
    return out


def haze_of(g, post):
    # 게임과 같은 규칙: 이정표에서 가장 가까운 'M' 덩어리 (같으면 먼저 찾은 것 — 맵을 위→아래로 볼 때)
    best, best_d = None, 10 ** 9
    for group in groups(g, "M"):
        for (x, y) in group:
            d = abs(x - post[0]) + abs(y - post[1])
            if d < best_d:
                best, best_d = group, d
    return best or []


def flood(g, open_haze, gate_open=False, secret_open=False, boss_open=False):
    start = cells(g, "P")[0]
    seen, q = {start}, deque([start])
    while q:
        x, y = q.popleft()
        for dx, dy in STEPS:
            n = (x + dx, y + dy)
            if not (0 <= n[0] < W and 0 <= n[1] < H) or n in seen:
                continue
            c = g[n[1]][n[0]]
            if c in WALK or (c == "M" and n in open_haze) or (c == "G" and gate_open) or (c == "V" and secret_open) or (c == "Z" and boss_open):
                seen.add(n)
                q.append(n)
    return seen


def touches(reach, cell):
    return any((cell[0] + dx, cell[1] + dy) in reach for dx, dy in STEPS)


def check(g, old_rows):
    problems = []
    if len(g) != H or any(len(r) != W for r in g):
        problems.append("크기")
    posts = cells(g, "X")
    owner = {}
    for post in posts:
        for cell in haze_of(g, post):
            owner.setdefault(cell, []).append(post)
    for group in groups(g, "M"):
        owners = {tuple(owner.get(c, [])) for c in group}
        if owners != {tuple(owners.copy().pop())} or not owners.copy().pop():
            problems.append(f"안개 덩어리 {group[0]}의 이정표가 하나가 아님: {owners}")
        elif len(owners.copy().pop()) != 1:
            problems.append(f"안개 덩어리 {group[0]}를 이정표 둘이 맡음")

    # 차례로 바로잡기
    fixed, order = set(), []
    while True:
        open_haze = {c for p in fixed for c in haze_of(g, p)}
        reach = flood(g, open_haze)
        new = [p for p in posts if p not in fixed and touches(reach, p)]
        if not new:
            break
        for p in new:
            fixed.add(p)
            order.append(p)
    if len(fixed) != len(posts):
        problems.append(f"닿지 않는 이정표: {[p for p in posts if p not in fixed]}")
    all_open = {c for p in posts for c in haze_of(g, p)}
    reach = flood(g, all_open, secret_open=True, boss_open=True)
    for c in cells(g, "C") + cells(g, "K") + cells(g, "F") + cells(g, "E") + cells(g, "L") + cells(g, "G"):
        if not touches(reach, c):
            problems.append(f"닿지 않음 {g[c[1]][c[0]]} {c}")
    if not any(touches(reach, c) for c in cells(g, "S")):
        problems.append("상점에 못 감")
    for d in cells(g, "D"):
        if d not in flood(g, all_open, gate_open=True, secret_open=True, boss_open=True):
            problems.append(f"출입구 {d}")
    secret_door = (71, 39)
    if secret_door in flood(g, all_open, gate_open=True, secret_open=True):
        problems.append("숲 보스를 이기기 전에 비밀의 숲 출입구에 감")
    if g[39][71] != "D" or cells(g, "D")[-1] != secret_door:
        problems.append("비밀의 숲 출입구가 맨 뒤 번호가 아님")
    key = cells(g, "K")[0]
    if touches(flood(g, all_open, gate_open=True, boss_open=True), key):
        problems.append("이정표를 다 바로잡기 전에 열쇠 공터에 감")
    full = flood(g, all_open, gate_open=True, secret_open=True, boss_open=True)
    lonely = [(x, y) for y in range(H) for x in range(OLD_W, W) if g[y][x] in WALK and (x, y) not in full]
    if lonely:
        problems.append(f"외딴 칸 {len(lonely)}개: {lonely[:6]}")
    boss = cells(g, "B")[0]
    if touches(flood(g, all_open, gate_open=False), boss):
        problems.append("문을 안 열어도 보스에게 감")
    if not touches(flood(g, all_open, gate_open=True), boss):
        problems.append("문을 열어도 보스에게 못 감")
    key = cells(g, "K")[0]
    if touches(flood(g, set()), key):
        problems.append("이정표를 안 바로잡아도 열쇠에 감")
    # 예전 칸
    allowed = {(35, 33), (35, 34), (34, 40), (35, 40), (34, 41), (35, 41), (14, 14)}
    allowed |= {(x, y) for y in range(1, 5) for x in range(9, 22) if old_rows[y][x] == "#"}  # 열쇠 공터 (#292) — 벽을 파기만 함
    for y in range(H):
        for x in range(OLD_W):
            if old_rows[y][x] != g[y][x] and (x, y) not in allowed:
                problems.append(f"예전 칸이 바뀜 {(x, y)} {old_rows[y][x]}→{g[y][x]}")
    return problems, order


# ---------- 미리보기 ----------
COLORS = {
    "#": (38, 74, 44), "~": (70, 120, 90), ".": (176, 146, 98), ":": (120, 160, 80), ",": (74, 128, 58),
    "P": (176, 146, 98), "N": (176, 146, 98), "n": (120, 160, 80), "D": (240, 220, 140),
    "F": (90, 180, 230), "S": (200, 120, 60), "E": (230, 200, 90), "L": (180, 140, 220), "C": (230, 180, 60),
    "B": (200, 70, 70), "G": (150, 100, 60), "K": (240, 240, 250),
    "X": (240, 150, 60), "M": (205, 235, 215), "W": (215, 205, 235), "V": (250, 230, 150), "Z": (250, 200, 120),
}


def font(size):
    for path in (os.path.join(ROOT, "Tools", "FontSource", "NotoSansKR-Bold.ttf"),
                 os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "NotoSansKR-Bold.ttf"),
                 "C:/Windows/Fonts/malgunbd.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def preview(g, places, order, path):
    s = 14
    img = Image.new("RGB", (W * s, H * s + 40), (20, 30, 24))
    d = ImageDraw.Draw(img)
    for y in range(H):
        for x in range(W):
            d.rectangle([x * s, y * s, x * s + s - 1, y * s + s - 1], fill=COLORS.get(g[y][x], (255, 0, 255)))
    d.line([OLD_W * s, 0, OLD_W * s, H * s], fill=(255, 255, 255), width=1)
    f, small = font(13), font(11)
    for i, p in enumerate(order):
        d.text((p[0] * s + 2, p[1] * s - 1), "①②③④⑤⑥⑦⑧"[i] if i < 8 else str(i + 1), fill=(60, 20, 0), font=small)
    for c in cells(g, "C"):
        d.text((c[0] * s + 3, c[1] * s - 1), "C", fill=(80, 40, 0), font=small)
    for c in cells(g, "K"):
        d.text((c[0] * s + 3, c[1] * s - 1), "K", fill=(0, 0, 0), font=small)
    for name, x, y in places:
        d.text((x * s, y * s), name, fill=(255, 255, 255), font=f, anchor="mm", stroke_width=2, stroke_fill=(0, 0, 0))
    d.text((6, H * s + 10), "흰 줄 왼쪽 = 예전 숲 · 주황 = 이정표(차례) · 연두 안개 = 바로잡으면 걷힘 · 보라 안개 = 가짜 길(늘 제자리)",
           fill=(230, 230, 230), font=f)
    img.save(path)


def main():
    text, rows = read_const()
    old_rows = [r[:OLD_W] for r in rows]
    if any(len(r) < OLD_W for r in rows):
        sys.exit("ForestMap 상수를 읽지 못함")
    g, places = build(rows)
    problems, order = check(g, old_rows)
    os.makedirs(OUT, exist_ok=True)
    lines = ["".join(r) for r in g]
    open(os.path.join(OUT, "ForestMap.txt"), "w", encoding="utf-8").write("\n".join(lines) + "\n")
    preview(g, places, order, os.path.join(OUT, "ForestMap_preview.png"))
    print(f"{W}x{H}  이정표 {len(cells(g, 'X'))}  상자 {len(cells(g, 'C'))}  차례 {order}")
    for p in problems:
        print("문제:", p)
    if "--apply" in sys.argv:
        if problems:
            sys.exit("문제가 있어 상수를 바꾸지 않음")
        body = "".join(f'                "{line}\\n" +\n' for line in lines[:-1]) + f'                "{lines[-1]}\\n";'
        new = re.sub(r'(public const string ForestMap =\n)(.*?);\n', lambda m: m.group(1) + body + "\n", text, count=1, flags=re.S)
        open(BUILDER, "w", encoding="utf-8").write(new)
        print("SampleDataBuilder.ForestMap 바꿈")


if __name__ == "__main__":
    main()
