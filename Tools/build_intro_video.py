"""입장 시네마틱 영상 만들기 (#129 — 사용자가 Google Flow로 만든 컷들을 한 편으로).

컷 원본(저장소 밖) = Builds/cinematic_pixellab/Videos/ (Flow에서 받은 mp4, 이름은 아래 CUTS의 match로 찾음)
→ 잘라 이어 붙임(컷 사이 0.5초 겹쳐 바뀜) · 처음은 검정에서 밝아짐 · 끝은 하얀 빛으로 · 세로 720×1280 · 소리 없음(게임이 타이틀 음악)
→ Assets/Resources/Video/intro.mp4 + 자막 시간표 intro_captions.json (게임 IntroCinematicView가 읽음)
    python Tools/build_intro_video.py        (pip install imageio-ffmpeg — ffmpeg가 들어 있음)
④ 백지룡의 날개 컷을 Videos/에 넣으면(이름에 'dragon'·'Wing'·'날개') 자동으로 끼워짐
"""
import glob
import json
import os
import subprocess

import imageio_ffmpeg

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Builds", "cinematic_pixellab", "Videos")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Video")
W, H, FPS, FADE = 720, 1280, 24, 0.5

# (컷, 원본 이름에 들어 있는 글자들, 시작 초, 길이 초, 자막) — 순서 = 영상 순서
# 오른쪽 아래에 'Veo' 표시가 있는 컷(Flow 8초 영상)은 WATERMARKED — 왼쪽 위 기준 94%만 잘라 써서 표시를 화면 밖으로
WATERMARKED = {"Glowing_letters_drift_over_village", "Glowing_letters_floating_from_book", "Villager_looking_at_sky"}
CUTS = [
    ("① 서고 탑과 마을", ["Glowing_letters_drift_over_village"], 0.0, 6.0, "이 세계는 말로 이루어져 있었습니다."),
    ("② 서고 안", ["Glowing_letters_floating_from_book"], 0.5, 4.5, None),
    ("③ 말이 사라짐", ["Villager_looking_at_sky"], 0.0, 6.0, "어느 날, 말이 사라지기 시작했습니다."),
    ("④ 백지룡의 날개", ["dragon", "Dragon", "날개", "_wing", "Wing"], 0.0, 4.5, None),  # 'wing'만 찾으면 Glowing에 걸림
    ("⑤ 손과 깃펜", ["38c82cb6"], 0.0, 4.5, None),
    ("⑥ 깨어남", ["a40b9981"], 0.0, 4.0, "그리고, 한 모험가가 깨어납니다."),
    ("⑦ 떠나는 모험가", ["127a8d90"], 0.0, 4.0, None),
]


def find(names):
    for f in sorted(glob.glob(os.path.join(SRC, "*.mp4"))):
        if any(n in os.path.basename(f) for n in names):
            return f
    return None


def main():
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    cuts = []
    for label, names, start, length, caption in CUTS:
        path = find(names)
        if path is None:
            print("없음 (건너뜀):", label)
            continue
        cuts.append((label, path, start, length, caption))
        print("컷", label, "<-", os.path.basename(path))

    args, filters = [ff, "-y", "-hide_banner", "-loglevel", "error"], []
    for i, (_, path, start, length, _) in enumerate(cuts):
        args += ["-ss", str(start), "-t", str(length), "-i", path]
        trim = "crop=iw*0.94:ih*0.94:0:0," if any(m in os.path.basename(path) for m in WATERMARKED) else ""
        filters.append(f"[{i}:v]{trim}scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},setpts=PTS-STARTPTS,settb=AVTB,fps={FPS},format=yuv420p[v{i}]")
    # 컷 사이 0.5초 겹쳐 바뀜 (xfade), 컷마다 영상 속 시작 시각을 자막 표에
    prev, t, starts = "v0", cuts[0][3], [0.0]
    for i in range(1, len(cuts)):
        offset = t - FADE
        filters.append(f"[{prev}][v{i}]xfade=transition=fade:duration={FADE}:offset={offset:.3f}[x{i}]")
        starts.append(offset)
        prev, t = f"x{i}", offset + cuts[i][3]
    total = t
    filters.append(f"[{prev}]fade=t=in:st=0:d=0.6,fade=t=out:st={total - 1.2:.3f}:d=1.2:color=white[out]")
    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, "intro.mp4")
    args += ["-filter_complex", ";".join(filters), "-map", "[out]", "-an", "-c:v", "libx264", "-preset", "slow", "-crf", "27",
             "-profile:v", "high", "-level", "4.0", "-pix_fmt", "yuv420p", "-movflags", "+faststart", out]
    subprocess.run(args, check=True)

    captions = []
    for (label, _, _, length, caption), start in zip(cuts, starts):
        if caption:
            captions.append({"start": round(start + 0.6, 2), "end": round(start + length - 0.6, 2), "text": caption})
    json.dump({"duration": round(total, 2), "captions": captions},
              open(os.path.join(OUT_DIR, "intro_captions.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"→ {out} ({os.path.getsize(out) / 1e6:.1f}MB, {total:.1f}초), 자막 {len(captions)}줄")


if __name__ == "__main__":
    main()
