# 필드 맵의 한 칸짜리 통로를 두 칸으로 넓힌다 (#67 — 길이 좁아 이동이 답답하다는 의견)
#
#   python Tools/widen_maps.py            → 무엇이 바뀔지 보고만 (Builds/maps/{맵}_before/after.txt)
#   python Tools/widen_maps.py --apply    → SampleDataBuilder 맵 상수 + Assets/Data/Areas/{지역}.asset map 칸에 같이 적용
#   --only ForestMap                      → 그 맵만 (초원·서고·숲은 #67에 이미 넓혔음 — 다시 돌리면 구석 몇 칸이 더 바뀌므로 새 맵만 고를 것)
#
# 규칙
#  - 통로 옆 벽(#)을 통로와 같은 글자(. : ,)로 바꾼다. 맵 가장자리·물건(C F E S B L D P)은 건드리지 않음
#    → 상자 id(위치 기준)·출입구·받침대 위치, 맵 크기(탐험 기록)는 그대로
#  - 이어진 좁은 칸들(한 구간)은 같은 쪽으로 넓힌다 (지그재그 방지), 벽이 더 많은 쪽을 고름
#  - 진짜 통로만: 구간을 빼고도 짧게 돌아갈 수 있으면(기둥 옆 틈 등) 그대로 둔다. 막다른 길은 통로로 봄
#  - 물(~)은 양옆이 모두 물인 여울(물 건너는 길)일 때만 바꾼다
#  - 지름길 금지: 넓힌 칸이 원래 멀리 돌아가야 하던 다른 길과 맞닿으면 그쪽은 포기
# 새 지역 맵을 만들면 MAPS에 (상수 이름, 에셋 이름)을 더하고 한 번 돌려 볼 것. 적용 뒤 EditMode 테스트(AreaDataTests 등)
import os
import re
import sys
from collections import deque

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILDER = os.path.join(ROOT, "Assets", "Scripts", "Editor", "SampleDataBuilder.cs")
AREAS = os.path.join(ROOT, "Assets", "Data", "Areas")
REPORT = os.path.join(ROOT, "Builds", "maps")
MAPS = [("MeadowMap", "meadow"), ("LibraryMap", "library"), ("ForestMap", "forest")]

WALK = set(".:,DP")


def walkable(g, x, y):
    return 0 <= y < len(g) and 0 <= x < len(g[0]) and g[y][x] in WALK


def dist_from(g, start, blocked=()):
    d = {start: 0}
    q = deque([start])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in d and n not in blocked and walkable(g, *n):
                d[n] = d[(x, y)] + 1
                q.append(n)
    return d


# 위아래(가로 통로) 또는 좌우(세로 통로)가 막힌 칸
def narrow_cells(g):
    hor, ver = set(), set()
    for y in range(len(g)):
        for x in range(len(g[0])):
            if g[y][x] not in WALK:
                continue
            up, dn = walkable(g, x, y - 1), walkable(g, x, y + 1)
            lf, rt = walkable(g, x - 1, y), walkable(g, x + 1, y)
            if not up and not dn and (lf or rt):
                hor.add((x, y))
            if not lf and not rt and (up or dn):
                ver.add((x, y))
    return hor, ver


def segments(cells, horizontal):
    cells = set(cells)
    step = (1, 0) if horizontal else (0, 1)
    segs = []
    while cells:
        c = cells.pop()
        seg = [c]
        for sign in (1, -1):
            n = (c[0] + step[0] * sign, c[1] + step[1] * sign)
            while n in cells:
                cells.remove(n)
                seg.append(n)
                n = (n[0] + step[0] * sign, n[1] + step[1] * sign)
        segs.append(sorted(seg))
    return segs


def convertible(g, x, y):
    if x <= 0 or y <= 0 or x >= len(g[0]) - 1 or y >= len(g) - 1:
        return False
    return g[y][x] in "#~"


def is_corridor(g, seg, horizontal):
    step = (1, 0) if horizontal else (0, 1)
    a = (seg[0][0] - step[0], seg[0][1] - step[1])
    b = (seg[-1][0] + step[0], seg[-1][1] + step[1])
    if not (walkable(g, *a) and walkable(g, *b)):
        return True
    return dist_from(g, a, set(seg)).get(b, 999) > len(seg) + 6


def makes_shortcut(g, seg, targets):
    tset, sset = set(targets), set(seg)
    d = dist_from(g, seg[0])
    for tx, ty in targets:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (tx + dx, ty + dy)
            if n in tset or n in sset or not walkable(g, *n):
                continue
            if d.get(n, 999) > len(seg) + 4:
                return True
    return False


def widen(rows):
    original = [list(r) for r in rows]
    g = [r[:] for r in original]
    hor, ver = narrow_cells(original)
    kept = []
    for horizontal, cells in ((True, hor), (False, ver)):
        for seg in segments(cells, horizontal):
            if not is_corridor(original, seg, horizontal):
                continue
            sides = ((0, -1), (0, 1)) if horizontal else ((-1, 0), (1, 0))
            ford = all(original[y + sy][x + sx] == "~" for x, y in seg for sx, sy in sides
                       if 0 <= y + sy < len(original) and 0 <= x + sx < len(original[0]))
            best = None
            for sx, sy in sides:
                targets = [(x + sx, y + sy) for x, y in seg]
                ok = all(convertible(original, *t) and (original[t[1]][t[0]] == "#" or ford) for t in targets)
                if not ok or makes_shortcut(original, seg, targets):
                    continue
                score = sum(1 for t in targets if original[t[1]][t[0]] == "#")
                if best is None or score > best[0]:
                    best = (score, targets)
            if best is None:
                kept.append(seg)
                continue
            for (cx, cy), (tx, ty) in zip(seg, best[1]):
                if g[ty][tx] not in WALK:
                    g[ty][tx] = original[cy][cx] if original[cy][cx] in ".:," else "."
    return ["".join(r) for r in g], kept


def constant_block(src, name):
    m = re.search(name + r' =\n((?:\s*"[^"]*\\n"(?: \+|;)\n)+)', src)
    if m is None:
        sys.exit(f"SampleDataBuilder에서 {name} 상수를 찾지 못함")
    return m


def apply_to_asset(asset, before, after):
    path = os.path.join(AREAS, asset + ".asset")
    with open(path, encoding="utf-8", newline="") as f:
        lines = f.read().split("\n")
    k = next(i for i, l in enumerate(lines) if l.startswith("  map: '"))
    for r, (b, a) in enumerate(zip(before, after)):
        prefix = "  map: '" if r == 0 else "    "
        if lines[k].rstrip("\r") != prefix + b:
            sys.exit(f"{asset}.asset 맵이 SampleDataBuilder와 다름 ({r}번째 줄) — 먼저 맞춰 놓을 것")
        lines[k] = prefix + a + ("\r" if lines[k].endswith("\r") else "")
        k += 2  # 작은따옴표 YAML 문자열은 줄 사이에 빈 줄
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write("\n".join(lines))


def main():
    apply = "--apply" in sys.argv
    only = sys.argv[sys.argv.index("--only") + 1] if "--only" in sys.argv else None
    with open(BUILDER, encoding="utf-8") as f:
        src = f.read()
    os.makedirs(REPORT, exist_ok=True)
    for name, asset in MAPS:
        if only and name != only:
            continue
        m = constant_block(src, name)
        before = re.findall(r'"([^"]*)\\n"', m.group(1))
        after, kept = widen(before)
        changed = sum(x != y for a, b in zip(before, after) for x, y in zip(a, b))
        print(f"{name}: {changed}칸 넓힘, 못 넓힌 좁은 구간 {len(kept)}개", [(s[0], len(s)) for s in kept])
        for tag, rows in (("before", before), ("after", after)):
            with open(os.path.join(REPORT, f"{name}_{tag}.txt"), "w", encoding="utf-8") as f:
                f.write("\n".join(rows) + "\n")
        if apply and changed:
            it = iter(after)
            block = re.sub(r'"([^"]*)\\n"', lambda mm: '"' + next(it) + '\\n"', m.group(1))
            src = src[:m.start(1)] + block + src[m.end(1):]
            apply_to_asset(asset, before, after)
    if apply:
        with open(BUILDER, "w", encoding="utf-8", newline="\r\n") as f:
            f.write(src)
        print("적용함 → EditMode 테스트로 확인할 것")


if __name__ == "__main__":
    main()
