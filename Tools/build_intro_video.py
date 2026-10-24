"""입장 시네마틱 영상 만들기 (#129 — 사용자가 Google Flow로 만든 컷들을 한 편으로).

컷 원본(저장소 밖) = Builds/cinematic_pixellab/Videos/ (Flow에서 받은 mp4, 이름은 아래 CUTS의 match로 찾음)
→ 잘라 이어 붙임(컷 사이 0.5초 겹쳐 바뀜) · 처음은 검정에서 밝아짐 · 끝은 하얀 빛으로 · 세로 720×1280 · 소리 없음(게임이 타이틀 음악)
→ Assets/Resources/Video/intro.mp4 + 자막 시간표 intro_captions.json (게임 IntroCinematicView가 읽음)
    python Tools/build_intro_video.py [컷 원본 폴더]   (pip install imageio-ffmpeg — ffmpeg가 들어 있음)
    (release-1.1 폴더처럼 원본이 없는 곳에서는 main 폴더의 Builds/cinematic_pixellab/Videos를 넘긴다)
④ 백지룡의 날개 = fbee97c9…mp4 (2026-10-24 사용자 추가). 새 컷은 CUTS에 이름 일부를 적으면 끼워짐
#133 사용자: ⑤ 손과 깃펜은 팔이 잘린 앞 2.5초를 빼고 끝까지 · 세계관 설명이 부족 → 자막 3줄에서 10줄로 (영상도 약 35초로 늘림)
"""
import glob
import json
import os
import subprocess
import sys

import imageio_ffmpeg

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Builds", "cinematic_pixellab", "Videos")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Video")
W, H, FPS, FADE = 720, 1280, 24, 0.5

# (컷, 원본 이름에 들어 있는 글자들, 시작 초, 길이 초, 끝 장면을 멈춰 두는 초) — 순서 = 영상 순서
# 오른쪽 아래에 'Veo' 표시가 있는 컷(Flow 8초 영상)은 WATERMARKED — 왼쪽 위 기준 94%만 잘라 써서 표시를 화면 밖으로
WATERMARKED = {"Glowing_letters_drift_over_village", "Glowing_letters_floating_from_book", "Villager_looking_at_sky"}
CUTS = [
    ("① 서고 탑과 마을", ["Glowing_letters_drift_over_village"], 0.0, 7.5, 0.0),
    ("② 서고 안", ["Glowing_letters_floating_from_book"], 0.5, 7.0, 0.0),
    ("③ 말이 사라짐", ["Villager_looking_at_sky"], 0.0, 7.5, 0.0),
    ("④ 백지룡의 날개", ["fbee97c9", "dragon", "Dragon", "날개", "_wing", "Wing"], 0.0, 4.0, 0.0),  # 'wing'만 찾으면 Glowing에 걸림
    ("⑤ 손과 깃펜", ["38c82cb6"], 2.5, 2.65, 0.0),  # 원본 5.17초 — 2.5초부터 끝까지 (#133)
    ("⑥ 깨어남", ["a40b9981"], 0.0, 4.0, 0.0),
    ("⑦ 떠나는 모험가", ["127a8d90"], 0.0, 4.0, 1.0),  # 마지막 장면을 1초 멈춰 두고 하얀 빛으로 (마지막 자막을 읽을 시간)
]

# 자막: (컷 번호 1~7, 그 컷이 시작한 뒤 나오는 초, 사라지는 초, 글) — 사라지는 초는 다음 컷으로 넘어가도 됨.
# 한 줄은 18자 안팎, 두 줄까지 (화면 아래 자막 칸). 이야기 기준 = Docs/GameStory/영단어RPG_스토리_통합.md 2장
CAPTIONS = [
    (1, 0.7, 3.6, "아주 먼 옛날, 이 세계는\n말로 이루어져 있었습니다."),
    (1, 3.9, 7.0, "누군가의 이름을 부르는 일도\n모두 말의 힘이었습니다."),
    (2, 0.5, 3.4, "서고의 책 속 글자마다\n사람들의 기억이 깃들어 있었습니다."),
    (2, 3.7, 6.6, "말을 잊지 않는 한,\n기억도 사라지지 않았습니다."),
    (3, 0.5, 3.5, "그러던 어느 날,\n하늘이 하얀 종이처럼 찢어지고"),
    (3, 3.8, 7.0, "글자들이 하나둘 빨려 들어가\n사라지기 시작했습니다."),
    (4, 0.4, 3.8, "거대한 하얀 날개가 지나간 마을에서\n사람들은 서로의 이름마저 잊어 갔습니다."),
    (5, 0.5, 3.6, "모든 것이 지워져 가던 그때에도\n깃펜 하나만은 빛을 잃지 않았습니다."),
    (6, 1.6, 4.2, "그리고 그 깃펜을 쥔\n한 모험가가 깨어납니다."),
    (7, 0.9, 3.8, "잃어버린 말을 되찾는 모험이\n지금 시작됩니다."),
]


def find(names):
    for f in sorted(glob.glob(os.path.join(SRC, "*.mp4"))):
        if any(n in os.path.basename(f) for n in names):
            return f
    return None


def main():
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    cuts = []  # (CUTS 안 번호, 컷, 원본, 시작 초, 길이 초, 멈춤 초)
    for number, (label, names, start, length, hold) in enumerate(CUTS, 1):
        path = find(names)
        if path is None:
            print("없음 (건너뜀):", label)
            continue
        cuts.append((number, label, path, start, length, hold))
        print("컷", label, "<-", os.path.basename(path))

    args, filters = [ff, "-y", "-hide_banner", "-loglevel", "error"], []
    for i, (_, _, path, start, length, hold) in enumerate(cuts):
        args += ["-ss", str(start), "-t", str(length), "-i", path]
        trim = "crop=iw*0.94:ih*0.94:0:0," if any(m in os.path.basename(path) for m in WATERMARKED) else ""
        pad = f",tpad=stop_mode=clone:stop_duration={hold}" if hold > 0 else ""
        filters.append(f"[{i}:v]{trim}scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},setpts=PTS-STARTPTS,settb=AVTB,fps={FPS},format=yuv420p{pad}[v{i}]")
    # 컷 사이 0.5초 겹쳐 바뀜 (xfade), 컷마다 영상 속 시작 시각을 자막 표에
    lengths = [length + hold for (_, _, _, _, length, hold) in cuts]
    prev, t, starts = "v0", lengths[0], [0.0]
    for i in range(1, len(cuts)):
        offset = t - FADE
        filters.append(f"[{prev}][v{i}]xfade=transition=fade:duration={FADE}:offset={offset:.3f}[x{i}]")
        starts.append(offset)
        prev, t = f"x{i}", offset + lengths[i]
    total = t
    filters.append(f"[{prev}]fade=t=in:st=0:d=0.6,fade=t=out:st={total - 1.2:.3f}:d=1.2:color=white[out]")
    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, "intro.mp4")
    args += ["-filter_complex", ";".join(filters), "-map", "[out]", "-an", "-c:v", "libx264", "-preset", "slow", "-crf", "27",
             "-profile:v", "high", "-level", "4.0", "-pix_fmt", "yuv420p", "-movflags", "+faststart", out]
    subprocess.run(args, check=True)

    cut_start = {number: start for (number, *_), start in zip(cuts, starts)}
    captions = []
    for number, appear, vanish, text in CAPTIONS:
        if number in cut_start:
            captions.append({"start": round(cut_start[number] + appear, 2),
                             "end": round(min(cut_start[number] + vanish, total), 2), "text": text})
    json.dump({"duration": round(total, 2), "captions": captions},
              open(os.path.join(OUT_DIR, "intro_captions.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"→ {out} ({os.path.getsize(out) / 1e6:.1f}MB, {total:.1f}초), 자막 {len(captions)}줄")


if __name__ == "__main__":
    main()
