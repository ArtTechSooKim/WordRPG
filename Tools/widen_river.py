# 메아리 정글 큰 강 넓히기 + 나루터를 2×2 나무 부두로 (#121 사용자: 섬 나루터의 뗏목이 뭍과 섬을 잇는 다리처럼 보였음 —
# "나루터인 것을 알 수 있도록 타일을 더 쓰고, 강을 양옆으로 늘리자 (맵 전체가 늘어나는 것 맞음)")
#
#   python Tools/widen_river.py            → 바꾼 맵을 Builds/maps/JungleMap_river_before/after.txt 로 보고만
#   python Tools/widen_river.py --apply    → SampleDataBuilder.JungleMap 상수 + Assets/Data/Areas/jungle.asset map 칸에 같이 적용
#
# 규칙
#  - 강 서쪽 가장자리 줄(x=50, 위아래 끝 말고는 모두 물)을 3번, 동쪽 가장자리 줄(x=59)을 4번 더 끼워 넣음 → 가로 72 → 79,
#    섬 양쪽 물길이 2·1칸 → 5·5칸. 가장자리 줄은 물뿐이라 상자·출입구 같은 물건은 복사되지 않음 (스크립트가 확인)
#  - 강 서쪽(x<50)은 위치 그대로 (상자·석판·레버·출입구 id 그대로), 강 안은 +3, 강 동쪽은 +7 (그쪽 상자 id는 새로 — 1.1 출시 전이라 괜찮음)
#  - 나루터 4곳을 2×2 부두로: 뭍(또는 섬) 한 줄 + 물 쪽 한 줄 'J' (물에 내민 쪽에 말뚝 — 그림 Jungle_Dock_auto)
# 한 번만 돌릴 것 (다시 돌리면 또 넓어짐 — 스크립트가 가로 72일 때만 적용)
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from widen_maps import BUILDER, REPORT, apply_to_asset, constant_block  # noqa: E402

WEST_EDGE, EAST_EDGE = 50, 59   # 강 가장자리 줄 (옛 좌표)
WEST_ADD, EAST_ADD = 3, 4
# 새 좌표 (x, y — y는 아래가 0). 뭍/섬 쪽 줄 + 물 쪽 줄
PIERS = {
    "남쪽 나루터 (서쪽 강가)": [(49, 17), (49, 18), (50, 17), (50, 18)],
    "북쪽 나루터 (동쪽 강가)": [(66, 71), (66, 72), (67, 71), (67, 72)],
    "섬 서쪽 나루터": [(54, 43), (54, 44), (55, 43), (55, 44)],
    "섬 동쪽 나루터": [(61, 43), (61, 44), (62, 43), (62, 44)],
}


def widen(rows):
    for r, row in enumerate(rows):
        for x in (WEST_EDGE, EAST_EDGE):
            if row[x] not in "~#":
                sys.exit(f"{r}번째 줄 x={x}가 물·벽이 아님 ('{row[x]}') — 끼워 넣으면 물건이 복사됨")
    out = [row[:WEST_EDGE] + row[WEST_EDGE] * WEST_ADD + row[WEST_EDGE:EAST_EDGE + 1] + row[EAST_EDGE] * EAST_ADD + row[EAST_EDGE + 1:]
           for row in rows]
    h = len(out)
    grid = [list(row) for row in out]
    for name, cells in PIERS.items():
        for x, y in cells:
            c = grid[h - 1 - y][x]
            if c not in "~:J":
                sys.exit(f"{name} ({x},{y})가 물·잔디·나루터가 아님 ('{c}')")
            grid[h - 1 - y][x] = "J"
    return ["".join(g) for g in grid]


def main():
    apply = "--apply" in sys.argv
    with open(BUILDER, encoding="utf-8") as f:
        src = f.read()
    m = constant_block(src, "JungleMap")
    before = re.findall(r'"([^"]*)\\n"', m.group(1))
    if len(before[0]) != 72:
        sys.exit(f"정글 맵 가로가 {len(before[0])} — 이미 넓힌 맵 (한 번만 돌릴 것)")
    after = widen(before)
    print(f"JungleMap: {len(before[0])}×{len(before)} → {len(after[0])}×{len(after)}, 나루터 {sum(r.count('J') for r in after)}칸")
    os.makedirs(REPORT, exist_ok=True)
    for tag, rows in (("before", before), ("after", after)):
        with open(os.path.join(REPORT, f"JungleMap_river_{tag}.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(rows) + "\n")
    if apply:
        it = iter(after)
        block = re.sub(r'"([^"]*)\\n"', lambda mm: '"' + next(it) + '\\n"', m.group(1))
        src = src[:m.start(1)] + block + src[m.end(1):]
        apply_to_asset("jungle", before, after)
        with open(BUILDER, "w", encoding="utf-8", newline="\r\n") as f:
            f.write(src)
        print("적용함 → EditMode 테스트(AreaDataTests·JungleTests 등)로 확인할 것")


if __name__ == "__main__":
    main()
