# 앱스토어 스크린샷 (아이폰 6.9인치 1320×2868 + 6.3인치 1206×2622): 위에 문구 + 아래 게임 화면 (Pillow 필요)
#
#   1) Unity 배치모드로 게임 화면을 찍는다 (그래픽 필요 → -nographics 빼기):
#      Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter WordRPG.Tests.StoreScreenshots
#      → Builds/store/raw/{1_field,2_quiz,...}.png  (Assets/Tests/PlayMode/StoreScreenshots.cs)
#   2) python Tools/store_screenshots.py → Builds/store/final/01_*.png … (6.9인치)
#      + Builds/store/final_1206x2622/ (앱스토어 커넥트 'Dynamic Island 지원 iPhone(중형 디스플레이)' 칸 — 이 순서로 올림)
#
# 문구 글꼴 = 게임과 같은 Jua(제목)·Noto Sans KR Bold(설명). Jua에 없는 기호(· 등)는 제목에 쓰지 말 것 — 아래에서 검사함
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(ROOT, "Builds", "store", "raw")
OUT = os.path.join(ROOT, "Builds", "store", "final")
# 앱스토어 커넥트 칸마다 받는 크기가 다름 → 같은 그림을 줄여서도 저장 (비율 차이 0.1% 미만)
EXTRA_SIZES = [(1206, 2622)]
FONTS = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts")

W, H = 1320, 2868
SCALE = 0.8                                       # 게임 화면 크기 (나머지는 문구 자리)
TOP_BG, BOTTOM_BG = (18, 20, 36), (38, 30, 72)    # Palette.Background → 보라
GOLD, TEXT = (255, 209, 64), (242, 245, 255)      # Palette.Gold, Palette.Text

# (게임 화면, 제목, 설명) — 앱스토어에 보이는 순서
SHOTS = [
    ("2_quiz", "기술을 쓰려면 영단어를 맞혀라!", "4지선다 단어 문제 · 빨리 맞히면 크리티컬"),
    ("3_new_word", "처음 만난 단어는 뜻부터", "발견한 단어는 도감에 차곡차곡"),
    ("1_field", "넓은 맵을 탐험하는 RPG", "숨은 보물상자 · 미니맵 · 지역마다 새 단어"),
    ("4_finisher", "보스를 물리치고 새 길을 열자", "연속으로 맞히면 콤보, 마지막은 결정타!"),
    ("5_summary", "한 판마다 외운 단어 정리", "틀린 단어는 오답 노트로, 곧 다시 나와요"),
    ("6_altar", "성유물을 모아 강화하고 각성!", "+3이 되면 기술이 더 강해져요"),
    ("7_dex", "교육과정 필수 어휘 1,700개", "숙련도 별을 모아 도감 완성"),
]

TITLE_FONT = os.path.join(FONTS, "Jua-Regular.ttf")
SUB_FONT = os.path.join(FONTS, "NotoSansKR-Bold.otf")


def check_glyphs():
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        return
    title_map = TTFont(TITLE_FONT).getBestCmap()
    sub_map = TTFont(SUB_FONT).getBestCmap()
    for name, title, sub in SHOTS:
        missing = [c for c in title if ord(c) not in title_map] + [c for c in sub if ord(c) not in sub_map]
        if missing:
            sys.exit(f"{name}: 글꼴에 없는 글자 {missing}")


def background():
    bg = Image.new("RGB", (W, H))
    draw = ImageDraw.Draw(bg)
    for y in range(H):
        t = y / (H - 1)
        draw.line([(0, y), (W, y)], fill=tuple(int(TOP_BG[i] + (BOTTOM_BG[i] - TOP_BG[i]) * t) for i in range(3)))
    return bg


def rounded_mask(size, radius):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius, fill=255)
    return mask


def main():
    check_glyphs()
    title_font = ImageFont.truetype(TITLE_FONT, 92)
    sub_font = ImageFont.truetype(SUB_FONT, 46)
    os.makedirs(OUT, exist_ok=True)
    base = background()
    for i, (name, title, sub) in enumerate(SHOTS, 1):
        path = os.path.join(RAW, name + ".png")
        if not os.path.exists(path):
            sys.exit(f"{path} 없음 — 먼저 StoreScreenshots 테스트로 게임 화면을 찍을 것")
        canvas = base.copy()
        draw = ImageDraw.Draw(canvas)
        draw.text((W // 2, 190), title, font=title_font, fill=GOLD, anchor="mm")
        draw.text((W // 2, 315), sub, font=sub_font, fill=TEXT, anchor="mm")

        shot = Image.open(path).convert("RGB")
        sw, sh = int(W * SCALE), int(H * SCALE)
        shot = shot.resize((sw, sh), Image.LANCZOS)
        x, y, radius, border = (W - sw) // 2, 430, 64, 8
        shadow = Image.new("L", (sw + 120, sh + 120), 0)
        ImageDraw.Draw(shadow).rounded_rectangle((60, 60, sw + 60, sh + 60), radius, fill=170)
        canvas.paste((0, 0, 0), (x - 60, y - 40), shadow.filter(ImageFilter.GaussianBlur(28)))
        draw.rounded_rectangle((x - border, y - border, x + sw + border, y + sh + border), radius + border, fill=GOLD)
        canvas.paste(shot, (x, y), rounded_mask(shot.size, radius))
        out = os.path.join(OUT, f"{i:02d}_{name}.png")
        canvas.save(out)
        print(out)
        for ew, eh in EXTRA_SIZES:
            folder = f"{OUT}_{ew}x{eh}"
            os.makedirs(folder, exist_ok=True)
            canvas.resize((ew, eh), Image.LANCZOS).save(os.path.join(folder, f"{i:02d}_{name}.png"))


if __name__ == "__main__":
    main()
