"""기술 소리 미리보기 영상 (#164): AdCapture.SkillSoundReel이 찍은 프레임 + 게임이 실제로 낸 소리 기록 → 이름표 달린 영상 한 편.

    python Tools/ad_motion/skill_sound_preview.py [초당 장수, 기본 19]   (pip install imageio-ffmpeg pillow)

Builds/ad/skill_sounds/{번호}_{기술 id}/f0000.jpg + sfx.txt + names.txt → Builds/ad/skill_sounds_preview.mp4
프레임이 실제로는 초당 19장쯤 찍혀서 그 속도로 묶어야 실제 게임 속도에 가깝다. 소리는 찍은 프레임 번호에 맞춰 놓는다 (배경 음악 없음 — 효과음만 잘 듣게).
음량은 게임의 효과음 기본값(GameSettings.DefaultSfxVolume)과 같게 — #165 전에는 2배 크게 나왔음
"""
import glob
import os
import subprocess
import sys
import tempfile

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Builds", "ad", "skill_sounds")
OUT = os.path.join(ROOT, "Builds", "ad", "skill_sounds_preview.mp4")
SFX = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx")
FONT = os.path.join(ROOT, "Assets", "Resources", "UI", "Fonts", "Jua-Regular.ttf")
FF = imageio_ffmpeg.get_ffmpeg_exe()
FPS = float(sys.argv[1]) if len(sys.argv) > 1 else 19.0
GAP = 12  # 기술 사이 쉬는 프레임 (마지막 장을 더 보여 줌)
SFX_VOLUME = 0.5  # GameSettings.DefaultSfxVolume
W, H = 720, 1564


def label_png(path, name, fx):
    im = Image.new("RGBA", (W, 150), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((20, 14, W - 20, 136), 28, fill=(11, 12, 24, 215))
    d.text((W / 2, 56), name, font=ImageFont.truetype(FONT, 50), fill=(255, 209, 64), anchor="mm")
    d.text((W / 2, 108), fx, font=ImageFont.truetype(FONT, 30), fill=(190, 200, 225), anchor="mm")
    im.save(path)


names = {}
with open(os.path.join(SRC, "names.txt"), encoding="utf-8") as f:
    for line in f:
        clip, name, fx = line.rstrip("\n").split("\t")
        names[clip] = (name, fx)
sfx = {}
with open(os.path.join(SRC, "sfx.txt"), encoding="utf-8") as f:
    for line in f:
        clip, frame, name = line.rstrip("\n").split("\t")
        sfx.setdefault(clip, []).append((int(frame), name))

with tempfile.TemporaryDirectory() as tmp:
    listing, cursor, labels, hits = [], 0, [], []
    for clip in sorted(d for d in os.listdir(SRC) if os.path.isdir(os.path.join(SRC, d))):
        frames = sorted(glob.glob(os.path.join(SRC, clip, "f*.jpg")))
        for path in frames:
            listing.append(f"file '{path.replace(os.sep, '/')}'\nduration {1 / FPS:.6f}\n")
        listing.append(f"file '{frames[-1].replace(os.sep, '/')}'\nduration {GAP / FPS:.6f}\n")
        start, end = cursor / FPS, (cursor + len(frames) + GAP) / FPS
        png = os.path.join(tmp, f"{clip}.png")
        label_png(png, *names[clip])
        labels.append((png, start, end))
        for frame, name in sfx.get(clip, []):
            hits.append((start + frame / FPS, name))
        cursor += len(frames) + GAP
        print(f"{clip}: {names[clip][0]} ({names[clip][1]})  {len(frames)}장  소리 {[n for _, n in sfx.get(clip, [])]}")
    listing.append(f"file '{frames[-1].replace(os.sep, '/')}'\n")
    list_path = os.path.join(tmp, "list.txt")
    with open(list_path, "w", encoding="utf-8") as f:
        f.writelines(listing)
    total = cursor / FPS

    # 소리 먼저: 기록된 시각에 그 파일을 놓음
    args, parts = [], []
    for i, (at, name) in enumerate(hits):
        args += ["-i", os.path.join(SFX, name + ".wav")]
        parts.append(f"[{i}:a]aformat=sample_rates=44100:channel_layouts=stereo,adelay={int(at * 1000)}:all=1[s{i}]")
    mix = "".join(f"[s{i}]" for i in range(len(hits)))
    graph = ";".join(parts) + f";{mix}amix=inputs={len(hits)}:duration=longest:normalize=0,apad,atrim=0:{total:.3f},volume={SFX_VOLUME}[out]"
    graph_path = os.path.join(tmp, "audio.txt")
    with open(graph_path, "w", encoding="utf-8") as f:
        f.write(graph)
    audio = os.path.join(tmp, "audio.wav")
    subprocess.run([FF, "-nostdin", "-y", "-v", "error"] + args + ["-filter_complex_script", graph_path, "-map", "[out]", audio], check=True)

    # 화면: 줄인 영상 위에 기술마다 이름표
    vargs = ["-f", "concat", "-safe", "0", "-i", list_path, "-i", audio]
    vparts = [f"[0:v]fps=30,scale={W}:{H}[v0]"]
    for i, (png, start, end) in enumerate(labels):
        vargs += ["-i", png]
        vparts.append(f"[v{i}][{i + 2}:v]overlay=0:40:enable='between(t,{start:.3f},{end:.3f})'[v{i + 1}]")
    vgraph = ";".join(vparts)
    vgraph_path = os.path.join(tmp, "video.txt")
    with open(vgraph_path, "w", encoding="utf-8") as f:
        f.write(vgraph)
    subprocess.run([FF, "-nostdin", "-y", "-v", "error"] + vargs + ["-filter_complex_script", vgraph_path, "-map", f"[v{len(labels)}]", "-map", "1:a",
                    "-c:v", "libx264", "-crf", "22", "-preset", "medium", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-shortest", "-movflags", "+faststart", OUT], check=True)
print(f"{OUT}: {total:.1f}초, {len(labels)}개 기술, 소리 {len(hits)}번, {os.path.getsize(OUT) // (1024 * 1024)}MB")
