"""지역 단어장을 150개로 줄이고 나머지는 다음 지역용 예비 단어장으로 옮긴다 (#76, 2026-10-16 사용자 결정).

- 지역 성격 유지: 초원·서고는 지금 단어장(중·고 공통)에서, 숲은 지금 숲 단어장(고급)에서 각각 가장 자주 쓰이는 단어 150개.
- '자주 쓰이는' 순위는 wordfreq(공개 영어 빈도 자료)의 zipf 값으로만 매긴다 — 단어 목록·뜻은 그대로 우리 것(교육과정 기본 어휘 + 직접 쓴 뜻).
- 남은 단어는 버리지 않는다:
    Docs/단어_예비_중고공통.csv = 초원·서고에서 빠진 단어 (자주 쓰이는 순)
    Docs/단어_예비_고급.csv     = 숲에서 빠진 단어 + 예전 '다음 지역' 500개 (자주 쓰이는 순)
  새 지역을 만들 땐 예비 파일 위에서부터 150개를 가져가면 된다.

사용: pip install wordfreq → python Tools/trim_wordbooks.py [--apply]   (--apply 없으면 미리보기만)
"""
import csv
import io
import os
import sys

from wordfreq import zipf_frequency

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WORDS = os.path.join(ROOT, "Assets", "Data", "Words")
DOCS = os.path.join(ROOT, "Docs")
KEEP = 150


def read(path):
    with io.open(path, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.reader(f))
    return rows[0], rows[1:]


def write(path, header, rows):
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(header)
        writer.writerows(rows)


CONTENT = {"n", "v", "adj"}  # 명사·동사·형용사 먼저 — which·though 같은 기능어는 뜻이 애매해 4지선다에 안 맞음
# 품사는 동사·형용사여도 기능어에 가까운 것 (조동사·한정사 같은 말)
FUNCTION_LIKE = {"would", "could", "should", "might", "must", "shall", "other", "such", "own", "few", "several", "many", "much",
                 "each", "every"}


def by_frequency(rows):
    # 내용어 먼저, 그 안에서 자주 쓰이는 순, 같으면 알파벳 순 (항상 같은 결과)
    return sorted(rows, key=lambda r: (r[3] not in CONTENT or r[1] in FUNCTION_LIKE, -zipf_frequency(r[1], "en"), r[1]))


def main(apply):
    header = None
    reserve_common, reserve_advanced = [], []
    for name, reserve in (("tier1_meadow", reserve_common), ("tier1_library", reserve_common), ("tier2_forest", reserve_advanced)):
        path = os.path.join(WORDS, name + ".csv")
        header, rows = read(path)
        if len(rows) <= KEEP:
            print(f"{name}: 이미 {len(rows)}개 — 그대로")
            continue
        ranked = by_frequency(rows)
        keep = sorted(ranked[:KEEP], key=lambda r: r[1])  # 단어장 안은 알파벳 순 (원래처럼)
        reserve.extend(ranked[KEEP:])
        cut = zipf_frequency(ranked[KEEP - 1][1], "en")
        print(f"{name}: {len(rows)} → {KEEP} (마지막으로 남은 단어 '{ranked[KEEP - 1][1]}' zipf {cut:.2f})")
        print("   남김 예:", ", ".join(r[1] for r in ranked[:8]), "…", ", ".join(r[1] for r in ranked[KEEP - 4:KEEP]))
        print("   뺌 예:  ", ", ".join(r[1] for r in ranked[KEEP:KEEP + 6]))
        if apply:
            write(path, header, keep)

    old_next = os.path.join(DOCS, "단어_3단계_다음지역.csv")
    if os.path.exists(old_next):
        reserve_advanced.extend(read(old_next)[1])
    if not reserve_common and not reserve_advanced:
        return
    reserve_common, reserve_advanced = by_frequency(reserve_common), by_frequency(reserve_advanced)
    print(f"예비 중·고 공통 {len(reserve_common)}개, 예비 고급 {len(reserve_advanced)}개")
    if apply:
        write(os.path.join(DOCS, "단어_예비_중고공통.csv"), header, reserve_common)
        write(os.path.join(DOCS, "단어_예비_고급.csv"), header, reserve_advanced)
        if os.path.exists(old_next):
            os.remove(old_next)
        print("적용함")


if __name__ == "__main__":
    main("--apply" in sys.argv)
