"""광고 가로판 만들기 (#161): 세로 완성 영상 → 유튜브 채널용 1920×1080.

    python Tools/ad_motion/make_landscape.py 세로.mp4 가로.mp4   (pip install imageio-ffmpeg pillow)

가운데 세로 영상 + 양옆은 같은 영상을 흐리고 어둡게 + 왼쪽 앱 이름·오른쪽 안내 (Jua · 앱 아이콘은 저장소에서).
세로 영상을 채널에 그대로 올리면 쇼츠로 분류되므로, 일반 영상 칸에는 이 가로판을 씀
"""
import os
import subprocess
import sys
import tempfile

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONT = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "Jua-Regular.ttf")
ICON = os.path.join(ROOT, "Assets", "Branding", "AppIcon.png")
W, H = 1920, 1080
CENTER_W = 608  # 세로 영상 폭 (1080 × 9/16)
PANEL_W = (W - CENTER_W) // 2
GOLD, WHITE, DIM, OUTLINE, NAVY = (255, 209, 64), (246, 247, 255), (190, 200, 225), (11, 12, 24), (18, 20, 36)


def text(draw, center, s, size, fill, stroke=6):
    font = ImageFont.truetype(FONT, size)
    draw.text(center, s, font=font, fill=fill, anchor="mm", stroke_width=stroke, stroke_fill=OUTLINE, align="center")


def panels(path):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 왼쪽: 앱 아이콘 · 이름 · 부제
    cx = PANEL_W // 2
    icon = Image.open(ICON).convert("RGBA").resize((250, 250), Image.LANCZOS)
    mask = Image.new("L", (250, 250), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, 249, 249), 56, fill=255)
    d.rounded_rectangle((cx - 133, 257, cx + 133, 523), 62, fill=GOLD)
    im.paste(icon, (cx - 125, 265), mask)
    text(d, (cx, 610), "영단어RPG", 92, GOLD, 8)
    text(d, (cx, 720), "게임하다 보니", 44, WHITE)
    text(d, (cx, 776), "외워지는 영단어", 44, WHITE)
    # 오른쪽: 게임 소개 · 안내
    cx = W - PANEL_W // 2
    for i, s in enumerate(["단어를 맞히면 공격!", "다양한 스킬로 전투", "숲과 강, 동굴을 모험"]):  # 보스 기믹은 넣지 않음 (#260 사용자: 스포)
        text(d, (cx, 350 + i * 76), s, 44, WHITE)
    d.rounded_rectangle((cx - 270, 600, cx + 270, 696), 48, fill=GOLD)
    font = ImageFont.truetype(FONT, 46)
    d.text((cx, 648), "앱스토어에서 무료로 시작", font=font, fill=NAVY, anchor="mm")
    text(d, (cx, 745), "숲 지역부터 정식판 (앱 내 구매)", 28, DIM, 4)
    im.save(path)


src, out = sys.argv[1], sys.argv[2]
with tempfile.TemporaryDirectory() as tmp:
    png = os.path.join(tmp, "panels.png")
    panels(png)
    fc = ("[0:v]split[a][b];"
          f"[a]scale={W}:-2,crop={W}:{H},gblur=sigma=28,eq=brightness=-0.32:saturation=0.8[bg];"
          f"[b]scale={CENTER_W}:{H}[fg];"
          f"[bg][fg]overlay=x={PANEL_W}:y=0[v0];[v0][1:v]overlay=0:0,format=yuv420p[v]")
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-nostdin", "-y", "-v", "error", "-i", src, "-i", png,
                    "-filter_complex", fc, "-map", "[v]", "-map", "0:a", "-c:v", "libx264", "-crf", "18", "-preset", "medium",
                    "-c:a", "copy", "-movflags", "+faststart", out], check=True)
print("landscape", out, os.path.getsize(out) // (1024 * 1024), "MB")
