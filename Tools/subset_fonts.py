"""본문 글꼴(Noto Sans KR) 줄이기 — 앱 크기 (#93, 사용자 요청 '앱 크기 줄일 방안').

원본(Tools/FontSource/*.otf, 약 4.6MB씩)에서 한자·일본 가나 글리프를 빼고 Assets/Resources/UI/Fonts/에 씁니다 (약 2.1MB씩).
- 한글 음절 11,172자 · 자모 · 영문 · 기호는 전부 남김 → 새 단어 뜻을 넣어도 한글은 늘 나옴
- 단, 게임 데이터(단어장 CSV · 예비 단어장 · 에셋 · 코드)에 실제로 쓰인 한자는 남김 (예: 산(酸), 시장(市長))
  → 단어 뜻에 새 한자를 넣었으면 이 스크립트를 다시 실행 (EditMode FontCoverageTests가 빠진 글자를 잡음)
글꼴 라이선스 SIL OFL 1.1 (예약 글꼴 이름 없음) — 수정·부분 집합 허용, 라이선스 파일은 그대로 함께

사용: python Tools/subset_fonts.py   (fontTools 필요: pip install fonttools)
"""
import glob
import os
import re

from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "Tools", "FontSource")
TARGET = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts")
FONTS = ["NotoSansKR-Regular.otf", "NotoSansKR-Bold.otf"]


def is_dropped(cp):
    """빼는 글자: 한자(통합·확장·호환·부수) · 일본 가나 · 한자 들어간 괄호/원 문자·단위 기호"""
    return (0x4E00 <= cp <= 0x9FFF or 0x3400 <= cp <= 0x4DBF or 0xF900 <= cp <= 0xFAFF or cp >= 0x20000
            or 0x2E80 <= cp <= 0x2FDF or 0x31C0 <= cp <= 0x31EF
            or 0x3040 <= cp <= 0x30FF or 0x31F0 <= cp <= 0x31FF
            or (0x3200 <= cp <= 0x33FF and not (0x3200 <= cp <= 0x321E or 0x3260 <= cp <= 0x327F)))


def used_characters():
    """게임 데이터에 쓰인 글자 (에셋의 백슬래시-u 이스케이프도 풀어서)"""
    escape = re.compile(r"\\u([0-9A-Fa-f]{4})")
    paths = glob.glob(os.path.join(ROOT, "Assets", "**", "*.cs"), recursive=True)
    paths += glob.glob(os.path.join(ROOT, "Assets", "**", "*.csv"), recursive=True)
    paths += glob.glob(os.path.join(ROOT, "Assets", "Data", "**", "*.asset"), recursive=True)
    paths += glob.glob(os.path.join(ROOT, "Docs", "*.csv"))
    used = set()
    for path in paths:
        text = open(path, encoding="utf-8", errors="ignore").read()
        if path.endswith(".asset"):
            text = escape.sub(lambda m: chr(int(m.group(1), 16)), text)
        used.update(ord(c) for c in text)
    return used


def main():
    used = used_characters()
    for name in FONTS:
        src = os.path.join(SOURCE, name)
        font = TTFont(src)
        cmap = font.getBestCmap()
        keep = [cp for cp in cmap if not is_dropped(cp) or cp in used]
        kept_hanja = sorted(chr(cp) for cp in cmap if is_dropped(cp) and cp in used)
        options = subset.Options()
        options.layout_features = ["*"]
        options.name_IDs = ["*"]
        options.name_languages = ["*"]
        options.notdef_outline = True
        options.glyph_names = False
        subsetter = subset.Subsetter(options)
        subsetter.populate(unicodes=keep)
        subsetter.subset(font)
        out = os.path.join(TARGET, name)
        font.save(out)
        print(f"{name}: {os.path.getsize(src) // 1024} KB -> {os.path.getsize(out) // 1024} KB "
              f"(글자 {len(cmap)} -> {len(keep)}, 남긴 한자 {''.join(kept_hanja)})")


if __name__ == "__main__":
    main()
