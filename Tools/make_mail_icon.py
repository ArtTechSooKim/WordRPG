"""메일함 아이콘 (#275) — 기존 UI 아이콘(가방·휴지통)과 같은 모양: 흰 덩어리 + 옅은 남보라 선, 128px"""
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

S = 4  # 4배로 그린 뒤 줄여서 가장자리를 부드럽게
WHITE = (242, 245, 255, 255)
DIM = (166, 179, 209, 255)
img = Image.new("RGBA", (128 * S, 128 * S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle([14 * S, 28 * S, 114 * S, 102 * S], radius=12 * S, fill=WHITE)
w = 8 * S
d.line([(24 * S, 40 * S), (64 * S, 70 * S), (104 * S, 40 * S)], fill=DIM, width=w, joint="curve")
for x, y in ((24, 40), (64, 70), (104, 40)):
    d.ellipse([(x - 4) * S, (y - 4) * S, (x + 4) * S, (y + 4) * S], fill=DIM)
img = img.resize((128, 128), Image.LANCZOS)
img.save(os.path.join(ROOT, "Assets", "Resources", "UI", "Icons", "mail.png"))
print(img.getbbox())
