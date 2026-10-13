# 2022 개정 영어과 교육과정 [별표 3] 기본 어휘 목록 (PDF 264~296쪽 = 교육과정 258~290쪽) → CSV
# 쓰는 법: python Tools/extract_basic_vocab.py "[별책14]+영어과+교육과정.pdf" 결과.csv   (pypdf 필요)
# 교육부 고시 제2022-33호 [별책 14] — 고시라 저작권법 제7조로 보호받지 않음 (GDD 11-2). 결과 3,001개 = * 801 · ** 1,200 · 표시 없음 1,000
# PDF가 글자를 한 칸씩 띄워 뽑히는 줄("a b a n d o n")이 있어 붙이고, 괄호가 다음 줄로 넘어간 것은 앞 단어에 붙인다
import re, csv, sys
from pypdf import PdfReader

pdf, out = sys.argv[1], sys.argv[2]
reader = PdfReader(pdf)
lines = []
for i in range(263, 296):  # 0부터 센 페이지 번호 263 = 264쪽
    text = reader.pages[i].extract_text() or ""
    lines.extend(text.split("\n"))


def collapse(line):
    # "a b o r t  ( a b o r t i o n )" → "abort (abortion)": 두 칸 띄움은 낱말 사이, 한 칸 띄움은 글자 사이
    s = line.strip()
    if re.fullmatch(r"(\S ?)+", s) is None and "  " not in s:
        return s
    tokens = s.split(" ")
    # 글자 하나씩 띄운 줄인지: 공백으로 나눈 조각이 대부분 한 글자
    singles = sum(1 for t in tokens if len(t) == 1)
    if tokens and singles >= len([t for t in tokens if t]) * 0.6:
        s = re.sub(r"  +", "\u0001", s)
        s = s.replace(" ", "")
        s = s.replace("\u0001", " ")
    return re.sub(r"\s+", " ", s).strip()


entries = []
entry_re = re.compile(r"^([A-Za-z][A-Za-z'\-\. ]*?)(\*{1,2})?(\s*/\s*[A-Za-z][A-Za-z'\-\. ]*?\*{0,2})*\s*(\((.*)\))?\s*$")
skip_re = re.compile(r"^(\d+|[A-Z]|영어과 교육과정|\[별표\] 영역|기|본|어|휘|목|록|영|과|교|육|정|2|0|)$")
for raw in lines:
    line = collapse(raw)
    if not line or skip_re.match(line) or re.search(r"[가-힣]", line):
        continue
    if entries and (line.endswith(")") and "(" not in line or line.startswith("/")):  # 앞 줄 괄호의 나머지
        e = entries[-1]
        tail = line.rstrip(") ").lstrip("/ ").strip()
        e["derived"] = (e["derived"] + (" / " if line.startswith("/") else ", ") + tail) if e["derived"] else tail
        continue
    if line.startswith("(") and entries:  # 앞 줄에 이어지는 파생형
        entries[-1]["derived"] = (entries[-1]["derived"] + ", " if entries[-1]["derived"] else "") + line.strip("() ")
        continue
    head, _, rest = line.partition("(")
    derived = rest.rstrip(") ").strip() if rest else ""
    variants = [v.strip() for v in head.split("/") if v.strip()]
    first = variants[0]
    m = re.match(r"^(.*?)(\*{1,2})?$", first)
    word, stars = m.group(1).strip(), m.group(2) or ""
    if not re.fullmatch(r"[A-Za-z][A-Za-z'\-\. ]*", word):
        print("?? skip", repr(raw), "->", repr(line))
        continue
    others = [re.sub(r"\*+$", "", v).strip() for v in variants[1:]]
    level = {"*": "elementary", "**": "middle_high"}.get(stars, "advanced")
    entries.append({"word": word, "stars": stars, "level": level, "variants": " / ".join(others), "derived": derived})

with open(out, "w", encoding="utf-8", newline="") as f:
    w = csv.DictWriter(f, fieldnames=["word", "stars", "level", "variants", "derived"])
    w.writeheader()
    w.writerows(entries)
from collections import Counter
print("total", len(entries), Counter(e["level"] for e in entries))
