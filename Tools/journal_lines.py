#!/usr/bin/env python3
"""누군가의 기록 문장 검사 + 단어 CSV에 넣기 (#200).

원문 Docs/GameStory/누군가의기록_문장.tsv (칸: document, heading, word, english, korean)
  document = diary(tier1_common) / survival(tier2_advanced) / plain(기록에 안 넣고 예문만)
  heading  = 그 줄부터 시작하는 묶음의 제목 (비어 있으면 앞 묶음이 이어짐)

  python Tools/journal_lines.py            검사만
  python Tools/journal_lines.py --text     검사를 통과하면 글만 Docs/GameStory/누군가의기록_글.txt로
  python Tools/journal_lines.py --apply    검사를 통과하면 CSV의 example·example_meaning·story·story_heading을 채움 (글 파일도 같이)

한 곳이라도 문제가 있으면 아무 파일도 쓰지 않는다.
"""
import csv
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parent.parent
TSV = ROOT / "Docs" / "GameStory" / "누군가의기록_문장.tsv"
TEXT = ROOT / "Docs" / "GameStory" / "누군가의기록_글.txt"
BOOKS = {"diary": "tier1_common", "survival": "tier2_advanced"}
DOCUMENTS = [("diary", "누군가의 일기 (초원·서고)"), ("survival", "누군가의 생존일지 (숲·정글·동굴)"), ("plain", "기록에 안 들어가는 예문")]
MAX_ENGLISH = 64
MAX_KOREAN = 34
ENGLISH = re.compile(r"^[A-Za-z ,.?!':;-]+$")
PRONOUN = re.compile(r"\b(he|she|his|her|hers|him)\b", re.IGNORECASE)


def read_book(name):
    path = ROOT / "Assets" / "Data" / "Words" / f"{name}.csv"
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    crlf = b"\r\n" in raw
    text = raw.decode("utf-8-sig")
    rows = list(csv.DictReader(text.splitlines()))
    return path, rows, bom, crlf


def write_text(lines):
    """글만 읽을 수 있게 일반 텍스트로 (사용자 요청 #202)"""
    out = ["《 》 = 그 문장에 반드시 들어가야 하는 단어 (게임에서 굵게 보이는 자리, 문장을 고쳐도 이 단어는 그대로)", ""]
    for key, title in DOCUMENTS:
        part = [line for line in lines if line["document"] == key]
        out += ["=" * 60, f" {title} ({len(part)}줄)", "=" * 60, ""]
        for line in part:
            if line["heading"]:
                out += [f"[{line['heading']}]", ""]
            marked = re.sub(rf"\b({re.escape(line['word'])})\b", r"《\1》", line["english"], count=1, flags=re.IGNORECASE)
            out += [marked, line["korean"], ""]
        out.append("")
    TEXT.write_text("\n".join(out).rstrip() + "\n", encoding="utf-8", newline="\r\n")
    print("글", TEXT.name)


def main():
    apply = "--apply" in sys.argv
    lines = list(csv.DictReader(TSV.read_text(encoding="utf-8").splitlines(), delimiter="\t"))
    books = {name: read_book(name) for name in BOOKS.values()}
    owner = {row["english"]: name for name, (_, rows, _, _) in books.items() for row in rows}

    problems = []
    seen = {}
    for line in lines:
        word, en, ko, doc = line["word"], line["english"], line["korean"], line["document"]
        if word in seen:
            problems.append(f"중복: {word}")
        seen[word] = line
        if word not in owner:
            problems.append(f"단어장에 없음: {word}")
        elif doc in BOOKS and owner[word] != BOOKS[doc]:
            problems.append(f"다른 단어장의 단어: {word} ({doc})")
        if not re.search(rf"\b{re.escape(word)}\b", en, re.IGNORECASE):
            problems.append(f"단어가 꼴 그대로 없음: {word} / {en}")
        if not ENGLISH.match(en):
            problems.append(f"영어에 다른 글자: {en}")
        if PRONOUN.search(en):
            problems.append(f"성별 대명사: {en}")
        if len(en) > MAX_ENGLISH:
            problems.append(f"영어가 긺 {len(en)}: {en}")
        if len(ko) > MAX_KOREAN:
            problems.append(f"해석이 긺 {len(ko)}: {ko}")
        if not re.search(r"[가-힣]", ko):
            problems.append(f"해석 없음: {word}")
    missing = [word for word in owner if word not in seen]
    if missing:
        problems.append(f"문장이 없는 단어 {len(missing)}개: {' '.join(missing)}")

    counts = {}
    for line in lines:
        counts[line["document"]] = counts.get(line["document"], 0) + 1
    print("문장", len(lines), counts)
    print("가장 긴 영어", max(len(l["english"]) for l in lines), "가장 긴 해석", max(len(l["korean"]) for l in lines))
    print("문제", len(problems))
    for problem in problems:
        print(" ", problem)
    if problems:
        return 1
    if apply or "--text" in sys.argv:
        write_text(lines)
    if not apply:
        return 0

    order = {}
    for line in lines:
        doc = line["document"]
        if doc in BOOKS:
            order[doc] = order.get(doc, 0) + 1
            line["story"] = order[doc]
    for name, (path, rows, bom, crlf) in books.items():
        columns = ["id", "english", "meaning", "pos", "example", "example_meaning", "story", "story_heading"]
        for row in rows:
            line = seen[row["english"]]
            row["example"] = line["english"]
            row["example_meaning"] = line["korean"]
            row["story"] = line.get("story", "")
            row["story_heading"] = line["heading"]
        with path.open("w", encoding="utf-8-sig" if bom else "utf-8", newline="") as f:
            writer = csv.DictWriter(f, columns, lineterminator="\r\n" if crlf else "\n", extrasaction="ignore")
            writer.writeheader()
            writer.writerows(rows)
        print("넣음", path.name, len(rows))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
