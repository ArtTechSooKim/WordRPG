"""기술마다 다른 소리 만들기 (#164 사용자: "모든 스킬 이펙트에 사운드도 따로"). Ninja Adventure 팩(CC0) 소리를 겹쳐 만든다.

    python Tools/make_skill_sfx.py [팩 경로]   (pip install imageio-ffmpeg)

표 한 줄 = 소리 이름(Sfx 열거형을 소문자로): 겹칠 소리들((팩 Sounds 아래 파일, 크기 dB, 늦게 ms)) + 고음을 더 자르기 Hz(0 = TONE만) + 최고 음량 dB.
결과 = Assets/Resources/Audio/Sfx/skill*.wav (모노, 끝의 무음은 잘라냄). 숫자를 바꿔 다시 돌리면 됨.
쓰는 곳 = 표 SkillLooks의 Sound 칸 — 새 기술 연출 종류를 더하면 여기 한 줄 + Sfx 열거형 맨 뒤 + SkillLooks 한 줄 (SkillLooksTests·AudioArtTests가 잡음).
더 좋은 소리를 받으면 Resources/Audio/Licensed/{같은 이름}.wav에 두면 먼저 쓰임.
GuardBubble은 원래 있던 shield 소리를 그대로 씀 (막음과 같은 소리, 지금 이 연출을 쓰는 기술은 없음).
#165 사용자: "전체적으로 하이를 깎으면 좋겠어, 귀가 너무 아파" → 모든 소리에 TONE을 씀. 순서가 중요: 최고 음량을 맞춘 '다음에' 깎는다
(깎은 뒤에 맞추면 날카로운 소리만 도로 커져서 효과가 없음). 더 깎으려면 TONE 숫자만 바꿔 다시 돌리면 됨.
#167 사용자: "스킬 이펙트가 BGM에 많이 묻힘" → 깎은 뒤 LIFT_MAX까지 들어 올림 + 곡 음량을 낮춤(Tools/prepare_music.py).
"""
import os
import re
import subprocess
import sys

import imageio_ffmpeg

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_PACK = r"F:\Unity\김수연습\Ninja Adventure - Asset Pack\Ninja Adventure - Asset Pack"
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Sfx")
FF = imageio_ffmpeg.get_ffmpeg_exe()

SLASH, HIT, MAGIC, ELEMENT, BONUS, ALERT, BOUNCE = "Whoosh & Slash", "Hit & Impact", "Magic & Skill", "Elemental", "Bonus", "Alert", "Jump & Bounce"
PEAK = -7.0  # 팩 소리들의 최고 음량과 같게
TONE = "highshelf=f=1800:g=-10,lowpass=f=5500"  # 2kHz 위(귀가 가장 예민한 곳)를 깎음 — #165. #166에 1.2배 더 깎았더니 음악에 묻혀서 되돌림 (#167 사용자)
LIFT_MAX = 3.0  # 깎아서 작아진 소리를 최고 음량까지 되돌리되 이만큼(dB)까지만 — 더 올리면 날카로운 소리가 도로 커짐 (#167)

SOUNDS = {
    # 공격: 베는 소리 + 맞는 소리
    "skillslash": ([(SLASH, "Slash", 0, 0), (HIT, "Hit1", -8, 30)], 0, PEAK),
    "skillclaw": ([(SLASH, "Slash3", 0, 0), (HIT, "Hit9", -8, 20)], 4500, PEAK),
    "skillheavy": ([(HIT, "Impact5", 0, 0), (HIT, "Hit3", -6, 0)], 0, -5.0),
    "skillthunder": ([(ELEMENT, "Explosion4", 0, 0), (ELEMENT, "Fireball", -8, 0)], 0, -5.0),
    "skillink": ([(ELEMENT, "Water4", 0, 0), (ELEMENT, "Bubble2", -8, 40)], 0, PEAK),
    "skillwater": ([(ELEMENT, "Water1", 0, 0), (ELEMENT, "WaterDrop", -8, 120)], 0, PEAK),
    "skillplant": ([(ELEMENT, "Grass", 10, 0), (SLASH, "Slash", -6, 0)], 4500, PEAK),
    "skillwhip": ([(SLASH, "Slash5", 0, 0), (HIT, "Hit9", -6, 50)], 4500, PEAK),
    "skillrock": ([(HIT, "Hit7", 0, 0), (HIT, "Impact2", -6, 0)], 0, PEAK),
    "skilllight": ([(MAGIC, "Magic2", 0, 0), (BONUS, "Coin3", -12, 60)], 0, PEAK),
    "skillletters": ([(BONUS, "Coin2", 0, 0), (BONUS, "Coin2", -4, 100), (BONUS, "Coin2", -8, 200)], 0, PEAK),  # 글자가 쏟아짐
    "skillhighlight": ([(SLASH, "Slash4", 0, 0), (ALERT, "Alert6", -10, 0)], 4500, PEAK),
    "skillsound": ([(ALERT, "Alert2", 0, 0), (ALERT, "Alert2", -6, 130), (ALERT, "Alert2", -12, 260)], 0, PEAK),  # 소리 고리 세 겹
    "skillspore": ([(SLASH, "Whoosh", 6, 0), (ELEMENT, "Bubble2", -8, 40)], 0, PEAK),
    "skillswirl": ([(MAGIC, "Fx", 0, 0)], 0, PEAK),
    # 회복
    "skillheallight": ([(MAGIC, "Heal", 0, 0)], 0, PEAK),
    "skillhealleaf": ([(MAGIC, "Heal2", 0, 0), (ELEMENT, "Grass", 4, 40)], 0, PEAK),
    "skillhealwater": ([(ELEMENT, "Water9", -2, 0), (MAGIC, "Heal", -6, 60)], 0, PEAK),
    "skilldrain": ([(MAGIC, "Magic5", 0, 0)], 0, PEAK),
    # 보호막
    "skillguardecho": ([(MAGIC, "Magic2", 0, 0), (MAGIC, "Magic2", -6, 110), (MAGIC, "Magic2", -12, 220)], 0, PEAK),
    "skillguardbook": ([(HIT, "Impact4", 0, 0), (MAGIC, "Magic1", -8, 40)], 0, PEAK),
    "skillguardtime": ([(MAGIC, "Magic4", 0, 0)], 0, PEAK),
    "skillguardseed": ([(BOUNCE, "Bounce5", 0, 0), (MAGIC, "Magic1", -8, 40)], 0, PEAK),
    # 필드 (기술이 아닌 소리도 같은 방식으로)
    # 덩굴 다리가 한 칸씩 놓임 (#232 사용자: 예전 Spirit 소리는 유령 웃음 같음 → 덩굴이 뻗는 풀 소리). 칸마다 울려서 조금 작게
    "vinegrow": ([(ELEMENT, "Grass2", 0, 0), (ELEMENT, "Grass", -6, 60)], 4500, -9.0),
}


def build(name, layers, cut, peak, sounds):
    args, parts = [], []
    for i, (folder, file, gain, delay) in enumerate(layers):
        args += ["-i", os.path.join(sounds, folder, file + ".wav")]
        parts.append(f"[{i}:a]aformat=sample_rates=44100:channel_layouts=mono,volume={gain}dB,adelay={delay}:all=1[a{i}]")
    mix = "".join(f"[a{i}]" for i in range(len(layers)))
    chain = f"{mix}amix=inputs={len(layers)}:duration=longest:normalize=0"
    if cut:
        chain += f",lowpass=f={cut}"
    chain += ",areverse,silenceremove=start_periods=1:start_threshold=-55dB,areverse"
    graph = ";".join(parts) + ";" + chain

    def render(extra, target):
        full = graph + extra + "[out]"
        return subprocess.run([FF, "-nostdin", "-y", "-hide_banner"] + args + ["-filter_complex", full, "-map", "[out]", "-c:a", "pcm_s16le"] + target,
                              capture_output=True, text=True, encoding="utf-8", errors="replace", check=True).stderr

    # 한 번 재서 최고 음량을 맞춘 뒤 고음을 깎음 (TONE 설명 참고)
    log = render(",volumedetect", ["-f", "null", "-"])
    found = float(re.search(r"max_volume: (-?[\d.]+) dB", log).group(1))
    path = os.path.join(OUT, name + ".wav")
    chain = f",volume={peak - found:.2f}dB,{TONE}"
    after = float(re.search(r"max_volume: (-?[\d.]+) dB", render(chain + ",volumedetect", ["-f", "null", "-"])).group(1))
    render(chain + f",volume={max(0.0, min(LIFT_MAX, peak - after)):.2f}dB", [path])
    return path


if __name__ == "__main__":
    pack = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_PACK
    sounds = os.path.join(pack, "Audio", "Sounds")
    for name, (layers, cut, peak) in SOUNDS.items():
        path = build(name, layers, cut, peak, sounds)
        print(f"{name}.wav  {os.path.getsize(path) / 44100 / 2:.2f}초  {os.path.getsize(path) // 1024}KB")
