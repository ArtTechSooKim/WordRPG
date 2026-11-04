"""영상용 그림을 입장 영상 컷과 같은 도트로 정리 (#187): AI 그림은 도트 크기가 들쭉날쭉하니 한 번 줄였다가 그대로 키운다.
가로세로 224×400으로 줄임 → 색을 줄임 → 5배로 키움 → 가운데를 1080×1920으로 자름 (입장 영상 컷과 같은 크기, 작업메모 #128).

사용: python Tools/pixelize_stills.py [원본 폴더] [결과 폴더]   (Pillow 필요)
기본 = Builds/cinematic_desert/raw → Builds/cinematic_desert. 원본 폴더 바로 아래 PNG만 씀.
"""
import glob
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SMALL = (224, 400)
FINAL = (1080, 1920)
SCALE = 5
COLORS = 48


def pixelize(im):
    small = im.convert("RGB").resize(SMALL, Image.BOX)
    small = small.quantize(COLORS, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    big = small.resize((SMALL[0] * SCALE, SMALL[1] * SCALE), Image.NEAREST)
    left = (big.width - FINAL[0]) // 2
    top = (big.height - FINAL[1]) // 2
    return big.crop((left, top, left + FINAL[0], top + FINAL[1]))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    default = os.path.join(ROOT, "Builds", "cinematic_desert")
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(default, "raw")
    out = sys.argv[2] if len(sys.argv) > 2 else default
    os.makedirs(out, exist_ok=True)
    for path in sorted(glob.glob(os.path.join(src, "*.png"))):
        result = pixelize(Image.open(path))
        result.save(os.path.join(out, os.path.basename(path)))
        print(os.path.basename(path), result.size, "색", len(result.getcolors(COLORS + 1) or []))
