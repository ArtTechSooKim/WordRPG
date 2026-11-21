"""의학 용어 단어장 = 의대생 자료 (#280 사용자, 2026-11-21) → 게임 CSV.

원본 Docs/의학용어_원본.html ('의학용어 암기장' — 의대생이 외우는 용어 14묶음 2,720줄 + 장별 확인문제 221개).
사용자: "그대로 넣으면 됨, 아무것도 건들이지 말고" → 영어·뜻·설명·문제 글자는 원본 그대로 (앞뒤 빈칸만 지움).

- 같은 용어가 여러 장에 또 나오면(279줄) 처음 나온 것만 (한 단어장에 같은 단어 둘은 못 넣음)
- 장 순서대로: 초원·서고 단어장 150 (medical_tier1) → 숲·정글·동굴 단어장 300 (medical_tier2) → 나머지는 예비
  (Docs/단어_예비_의학.csv — 버전마다 위에서부터, group = 장 이름)
- 설명(예: oste(bone·뼈) + itis(inflammation·염증))은 note 열 → 도감·단어 카드
- 확인문제 → Assets/Data/Exams/medical_exam.csv (id·장 번호·장 이름·번호·문제·정답, 글자 그대로). 5지선다 보기는 게임이 문제 글에서 읽음 (ExamQuestion)
- 품사는 원본에 없어 비워 둠 (화면에도 안 나옴)

사용: python Tools/medical_course.py [--apply]   (--apply 없으면 검사·미리보기만)
"""
import csv
import html
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "Docs", "의학용어_원본.html")
COURSES = os.path.join(ROOT, "Assets", "Data", "Words", "Courses")
EXAMS = os.path.join(ROOT, "Assets", "Data", "Exams")
RESERVE = os.path.join(ROOT, "Docs", "단어_예비_의학.csv")
TIER1, TIER2 = 150, 300

BOOK_HEADER = ["id", "english", "meaning", "pos", "example", "example_meaning", "story", "story_heading", "note"]
RESERVE_HEADER = ["id", "english", "meaning", "pos", "example", "example_meaning", "group", "note"]
EXAM_HEADER = ["id", "chapter", "chapter_title", "number", "question", "answer"]

CHAPTER = re.compile(r'<details class="ch" id="c(\d+)"><summary>(.*?)<small>.*?</small></summary>(.*?)(?=<details class="ch" id="c\d+">|</body>)', re.S)
ITEM = re.compile(
    r'<h3>(?P<section>.*?)</h3>'
    r'|<div class="t"><b>(?P<en>.*?)</b> — (?P<ko>.*?)(?:<details class="d"><summary>설명</summary><p>(?P<note>.*?)</p></details>)?</div>'
    r'|<div class="q"><b>(?P<n>\d+)\.</b> (?P<q>.*?)<details class="a"><summary>정답 보기</summary><p>(?P<a>.*?)</p></details></div>', re.S)


def text(s):
    return html.unescape(s or "").strip()


def chapter_number(title):
    m = re.match(r"제(\d+)장", title)
    return int(m.group(1)) if m else 0  # '도해: 해부 그림'은 0


def parse():
    source = io.open(SOURCE, encoding="utf-8").read()
    chapters = []
    for m in CHAPTER.finditer(source):
        title = text(m.group(2))
        terms, questions = [], []
        for item in ITEM.finditer(m.group(3)):
            if item.group("en") is not None:
                terms.append((text(item.group("en")), text(item.group("ko")), text(item.group("note"))))
            elif item.group("q") is not None:
                questions.append((int(item.group("n")), text(item.group("q")), text(item.group("a"))))
        chapters.append((title, terms, questions))
    return chapters


def slug(english):
    s = re.sub(r"[^a-z0-9]+", "_", english.lower()).strip("_")
    return s or "term"


def build(chapters):
    words, seen, ids = [], set(), set()
    for title, terms, _ in chapters:
        for english, meaning, note in terms:
            key = english.lower()
            if key in seen:
                continue
            seen.add(key)
            base = "med_" + slug(english)
            wid, k = base, 2
            while wid in ids:
                wid, k = f"{base}_{k}", k + 1
            ids.add(wid)
            words.append({"id": wid, "english": english, "meaning": meaning, "note": note, "group": title})
    exams = []
    for title, _, questions in chapters:
        no = chapter_number(title)
        for n, q, a in questions:
            exams.append({"id": f"medexam_{no:02d}_{n:02d}", "chapter": no, "title": title, "number": n, "question": q, "answer": a})
    return words, exams


def check(chapters, words, exams):
    problems = []
    rows = sum(len(t) for _, t, _ in chapters)
    if rows != 2720: problems.append(f"용어 줄 {rows} (원본은 2,720)")
    if sum(len(q) for _, _, q in chapters) != 221: problems.append("확인문제 221개가 아님")
    if len(words) < TIER1 + TIER2: problems.append("단어가 450개보다 적음")
    for w in words:
        if not w["english"] or not w["meaning"]: problems.append(f"{w['id']}: 빈 칸")
        if "," in w["english"] and w["english"].count("(") == 0: pass
    for e in exams:
        if not e["question"] or not e["answer"]: problems.append(f"{e['id']}: 빈 문제")
    return problems


def write(path, header, rows):
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        out = csv.writer(f, lineterminator="\n")
        out.writerow(header)
        out.writerows(rows)


def main():
    chapters = parse()
    words, exams = build(chapters)
    problems = check(chapters, words, exams)
    tier1, tier2, reserve = words[:TIER1], words[TIER1:TIER1 + TIER2], words[TIER1 + TIER2:]
    print(f"장 {len(chapters)} · 용어 {sum(len(t) for _, t, _ in chapters)}줄 → 겹침 빼고 {len(words)}개 "
          f"(초원·서고 {len(tier1)} · 숲~동굴 {len(tier2)} · 예비 {len(reserve)}) · 확인문제 {len(exams)}")
    print("  초원·서고:", tier1[0]["english"], "…", tier1[-1]["english"], f"({tier1[-1]['group']})")
    print("  숲~동굴  :", tier2[0]["english"], "…", tier2[-1]["english"], f"({tier2[-1]['group']})")
    print("  예비 첫 줄:", reserve[0]["english"], f"({reserve[0]['group']})")
    for p in problems: print("  문제:", p)
    if "--apply" not in sys.argv:
        print("(미리보기 — --apply로 씀)")
        return
    if problems: sys.exit("문제가 있어 쓰지 않음")
    book = lambda ws: [[w["id"], w["english"], w["meaning"], "", "", "", "", "", w["note"]] for w in ws]
    write(os.path.join(COURSES, "medical_tier1.csv"), BOOK_HEADER, book(tier1))
    write(os.path.join(COURSES, "medical_tier2.csv"), BOOK_HEADER, book(tier2))
    write(RESERVE, RESERVE_HEADER, [[w["id"], w["english"], w["meaning"], "", "", "", w["group"], w["note"]] for w in reserve])
    os.makedirs(EXAMS, exist_ok=True)
    write(os.path.join(EXAMS, "medical_exam.csv"), EXAM_HEADER,
          [[e["id"], e["chapter"], e["title"], e["number"], e["question"], e["answer"]] for e in exams])
    print("썼음: medical_tier1·tier2.csv · 단어_예비_의학.csv · Exams/medical_exam.csv")


if __name__ == "__main__":
    main()
