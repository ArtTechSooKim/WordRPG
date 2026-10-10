"""Ninja Adventure 에셋 팩(CC0)에서 이 게임이 쓰는 그림만 골라 프로젝트로 복사한다.

    python Tools/import_ninja_art.py ["팩 폴더 경로"]

- 몬스터: 시트에서 정면(첫 칸) 한 프레임만 잘라 Monsters/{speciesId}.png (파일 이름 = speciesId)
  → Unity 메뉴 WordRPG > Data > Link Monster Art 가 같은 이름의 몬스터 에셋에 연결한다
- 주인공: 4방향 × 걷기 4프레임 시트 그대로 (게임이 잘라 씀)
- 성유물: Relics/{relicId}.png (파일 이름 = relicId) — 게임이 RelicData.icon이 비어 있으면 이 그림을 쓴다
- 상처약 등 아이템 도트: Items/{itemId}.png (Figma로 그린 UI/Icons/Items/{itemId}.png가 없을 때 쓰임)
필요한 Pillow: pip install pillow
"""
import os
import shutil
import sys

from PIL import Image

DEFAULT_PACK = r"F:\Unity\김수연습\Ninja Adventure - Asset Pack\Ninja Adventure - Asset Pack"
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(PROJECT, "Assets", "Resources", "Art", "NinjaAdventure")

# speciesId: (팩 안의 시트 경로, 한 칸 크기) — 적 몬스터만 (아군 몬스터는 성유물로 바뀜)
MONSTERS = {
    "ink_slime": ("Actor/Monsters/Slime/Slime.png", 16),                 # 잉크 슬라임
    "scribble_bat": ("Actor/Monsters/BlueBat/SpriteSheet.png", 16),      # 낙서 박쥐
    "forget_goblin": ("Actor/Monsters/KappaGreen/SpriteSheet.png", 16),  # 까먹깨비
    "boss_forget_king": ("Actor/Boss/GiantSpirit/Idle.png", 50),         # 까먹대왕
    # 숲 (두 번째 지역)
    "spell_mushroom": ("Actor/Monsters/Mushroom/mushroom.png", 16),      # 철자버섯
    "squiggle_snake": ("Actor/Monsters/Snake2/Snake2.png", 16),          # 꼬부랑뱀
    "question_owl": ("Actor/Monsters/Owl/Owl.png", 16),                  # 물음표부엉이
    "boss_muddle_raccoon": ("Actor/Boss/GiantRacoon/Idle.png", 60),      # 헷갈너구리
}

PLAYER = "Actor/Characters/Boy/SpriteSheet.png"

# relicId: (그림 경로, 시트에서 자를 한 칸 크기 — None이면 그대로)
RELICS = {
    "relic_quill": ("Items/Resource/feather.png", None),                  # 깃펜 (예전 펜촉이)
    "relic_book": ("Items/Object/Book.png", None),                        # 백과사전 (예전 책껍질)
    "relic_lantern": ("Actor/Monsters/LanternRed/SpriteSheet.png", 16),   # 등불 (예전 등불이)
    "relic_wand": ("Items/Weapons/MagicWand/Sprite.png", None),           # 마법 지팡이
    "relic_grail": ("Items/Treasure/GoldCup.png", None),                  # 기억의 성배 (서고 보스 보상)
    "relic_whip": ("Items/Weapons/Whip/Sprite.png", None),                # 덩굴 채찍 (숲 상자)
    "relic_hourglass": ("Items/Object/Hourglass.png", None),              # 시간의 모래시계 (숲 상자)
    "relic_leaf": ("Items/Food/TeaLeaf.png", None),                       # 세계수 잎 (숲 보스 보상)
}

# itemId: 그림 경로 (도트 아이템)
ITEMS = {
    "potion": "Items/Potion/Medipack.png",       # 상처약
    "potion_large": "Items/Potion/LifePot.png",  # 큰 상처약
    "keepsake_forest": "Items/Food/Nut.png",     # 도토리 책갈피 (숲 도감 완성 징표)
    # 기술문서 (두루마리)
    "skilldoc_cram": "Items/Scroll/ScrollThunder.png",     # 벼락치기 (서고 보스)
    "skilldoc_highlight": "Items/Scroll/ScrollPlant.png",  # 형광펜 긋기 (숲 보스)
    "skilldoc_pencilcase": "Items/Scroll/ScrollRock.png",  # 필통 방패 (초원 숨은 상자)
}

# 소리: 파일 이름 = 게임 코드의 이름(Music·Sfx 열거형을 소문자로)
MUSIC = {
    "title": "Audio/Musics/1 - Adventure Begin.ogg",
    "meadow": "Audio/Musics/5 - Peaceful.ogg",
    "library": "Audio/Musics/13 - Mystical.ogg",
    "battle": "Audio/Musics/17 - Fight.ogg",
    "boss": "Audio/Musics/28 - Tension.ogg",
    "forest": "Audio/Musics/11 - Clearing.ogg",
}
SFX = {
    "click": "Audio/Sounds/Menu/Move2.wav",  # 가벼운 찰칵 (0.04초)
    "correct": "Audio/Sounds/Bonus/Bonus.wav",
    "wrong": "Audio/Sounds/Alert/Alert.wav",
    "hit": "Audio/Sounds/Hit & Impact/Hit1.wav",
    "critical": "Audio/Sounds/Hit & Impact/Impact.wav",
    "heal": "Audio/Sounds/Magic & Skill/Heal.wav",
    "shield": "Audio/Sounds/Magic & Skill/Magic1.wav",
    "fail": "Audio/Sounds/Menu/Cancel.wav",
    "faint": "Audio/Sounds/Hit & Impact/Hit5.wav",
    "encounter": "Audio/Sounds/Whoosh & Slash/Whoosh.wav",
    "victory": "Audio/Jingles/Success1.wav",
    "defeat": "Audio/Jingles/GameOver.wav",
    "levelup": "Audio/Jingles/LevelUp1.wav",
    "newword": "Audio/Jingles/Secret3.wav",  # 새 단어 '발견!' 징글 (나오는 동안 배경 음악을 잠깐 멈춤)
    "coin": "Audio/Sounds/Bonus/Coin.wav",
    "fountain": "Audio/Sounds/Magic & Skill/Heal2.wav",
    "evolvelight": "Audio/Sounds/Magic & Skill/Spirit.wav",
    "evolve": "Audio/Jingles/Secret1.wav",
    "dexcomplete": "Audio/Jingles/Success3.wav",
    "door": "Audio/Sounds/Whoosh & Slash/Whoosh2.wav",
    "gateopen": "Audio/Jingles/Secret2.wav",  # 보스를 물리쳐 새 길이 열릴 때
    "combo": "Audio/Sounds/Bonus/PowerUp1.wav",  # 연속 정답 콤보 (단계마다 음높이를 올려 재생)
    "respawn": "Audio/Sounds/Magic & Skill/Heal3.wav",  # 지고 나서 시작 지점에서 빛과 함께 다시 일어남
    "flee": "Audio/Sounds/Jump & Bounce/Jump2.wav",  # 전투에서 도망침 (#53)
}
AUDIO_OUT = os.path.join(PROJECT, "Assets", "Resources", "Audio")

# 전투 효과: (시트, 프레임 수[, 첫 프레임[, "gray"]]). 프레임을 정사각형으로 맞춰 가로로 다시 붙인다 → 게임은 높이 = 한 칸 크기로 자름.
# "gray" = 회색조로 바꿔 저장 (가장 밝은 점 = 흰색) → 게임이 색을 곱해 입힌다 (흙먼지 = 밟은 바닥 색, FieldArt.DustColor)
FX = {
    "slash": ("FX/SlashFx/Slash/SpriteSheet.png", 4),          # 아군 공격
    "claw": ("FX/SlashFx/Claw/SpriteSheet.png", 4),            # 적 공격
    "explosion": ("FX/Elemental/Explosion/SpriteSheet.png", 9),  # 크리티컬
    "heal": ("FX/Magic/Circle/SpriteSheetSpark.png", 6),       # 회복
    "shield": ("FX/Magic/Shield/SpriteSheetBlue.png", 6),      # 보호막 · 막음
    "smoke": ("FX/Smoke/Smoke/SpriteSheet.png", 6),            # 쓰러짐
    "sparkle": ("FX/Magic/Spark/SpriteSheet.png", 10),         # 다시 일어날 때 반짝이 (필드)
    "dust": ("FX/Elemental/Explosion/SpriteSheet.png", 9, 6, "gray"),  # 달릴 때 뒤로 튀는 흙먼지 — 폭발의 마지막 3칸 (#51 사용자 지정), 색은 바닥 따라 (#53)
}


# 타이틀 배경 (Figma '타이틀 (#27)'): 초원 타일로 그린 14×24칸 풍경 — 위는 숲, 가운데 길이 넓어진 공터(주인공 자리),
# 오른쪽 위 호수, 아래 강과 풀숲. 길·물은 자동 테두리. Title/title_scene.png (16px = 1칸, 게임이 화면을 덮게 키움)
TITLE_SCENE = [
    "##############",
    "#####::#######",
    "###::::::#####",
    "##::,,,:::~~##",
    "#::,,,,::~~~~#",
    "#::,,,:.:~~~~#",
    "##:::::.:~~~~#",
    "###::::.:::~##",
    "##,,::..:::::#",
    "#,,,::.::,,,:#",
    "#,,::..::,,,,#",
    "#:::...:::,,:#",
    "##::.....::::#",
    "#:::.....:::##",
    "#::...:...::::",
    "::...:::...:::",
    ":::.::,,::..::",
    "~~~.~~~~~~~.~~",
    "~~~.~~~~~~~.~~",
    "::,.,,,::,,.::",
    ":,,.,,,::,,.,:",
    "##,.,##::##.##",
    "###.#####::.##",
    "###.######:.##",
]


def build_title_scene():
    tiles = os.path.join(OUT, "Tiles")
    floor = Image.open(os.path.join(tiles, "Meadow_Floor_auto.png")).convert("RGBA")
    water = Image.open(os.path.join(tiles, "Meadow_Water_auto.png")).convert("RGBA")
    plain = {k: Image.open(os.path.join(tiles, f"Meadow_{v}.png")).convert("RGBA")
             for k, v in ((":", "Lawn"), (",", "Grass"), ("#", "Wall"))}
    h, w = len(TITLE_SCENE), len(TITLE_SCENE[0])
    dirs = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]
    img = Image.new("RGBA", (w * 16, h * 16))
    for y in range(h):
        for x in range(w):
            ch = TITLE_SCENE[y][x]
            if ch in ".~":
                mask = 0
                for i, (dx, dy) in enumerate(dirs):
                    nx, ny = x + dx, y + dy
                    inside = 0 <= nx < w and 0 <= ny < h
                    if (inside and TITLE_SCENE[ny][nx] == ch) or not inside:
                        mask |= 1 << i
                mask = auto_normalize(mask)
                atlas = floor if ch == "." else water
                t = atlas.crop(((mask % 16) * 16, (mask // 16) * 16, (mask % 16) * 16 + 16, (mask // 16) * 16 + 16))
            else:
                t = plain[ch]
            img.alpha_composite(t, (x * 16, y * 16))
    folder = os.path.join(OUT, "Title")
    os.makedirs(folder, exist_ok=True)
    img.save(os.path.join(folder, "title_scene.png"))
    print("title scene ->", folder)



# 수련용 허수아비 (#44): 팩 TilesetElement의 (15,0) 칸 허수아비 (16×16)
SCARECROW = ("Backgrounds/Tilesets/TilesetElement.png", 15, 0)


def build_scarecrow(pack_root=DEFAULT_PACK):
    rel, col, row = SCARECROW
    sheet = Image.open(os.path.join(pack_root, rel)).convert("RGBA")
    sheet.crop((col * 16, row * 16, col * 16 + 16, row * 16 + 16)).save(os.path.join(OUT, "Monsters", "training_scarecrow.png"))
    print("monster training_scarecrow <-", rel, (col, row))


# 말풍선 (#46): 주인공 머리 위 말풍선 — 팩 Ui/Dialog/DialogInfo(20×16 4칸: 빈 풍선 · . · .. · ...) 그대로,
# 느낌표는 Emote 22(빨간 !)의 살구색 풍선을 DialogInfo와 같은 흰색으로 바꿔서
def build_bubbles(pack_root=DEFAULT_PACK):
    folder = os.path.join(OUT, "Ui")
    os.makedirs(folder, exist_ok=True)
    shutil.copyfile(os.path.join(pack_root, "Ui", "Dialog", "DialogInfo.png"), os.path.join(folder, "bubble.png"))
    emote = Image.open(os.path.join(pack_root, "Ui", "Emote", "emote22.png")).convert("RGBA")
    px = emote.load()
    for y in range(emote.height):
        for x in range(emote.width):
            if px[x, y] == (252, 226, 202, 255):
                px[x, y] = (255, 255, 255, 255)
    emote.save(os.path.join(folder, "bubble_exclaim.png"))
    print("bubbles ->", folder)


def build_fx(pack):
    folder = os.path.join(OUT, "Fx")
    os.makedirs(folder, exist_ok=True)
    for name, spec in FX.items():
        rel, count = spec[0], spec[1]
        first = spec[2] if len(spec) > 2 else 0  # 시트의 앞쪽 프레임을 건너뛸 때
        sheet = pack.image(rel)
        fw, fh = sheet.width // count, sheet.height
        size = max(fw, fh)
        used = count - first
        strip = Image.new("RGBA", (size * used, size), (0, 0, 0, 0))
        for i in range(used):
            frame = sheet.crop(((first + i) * fw, 0, (first + i) * fw + fw, fh))
            strip.alpha_composite(frame, (i * size + (size - fw) // 2, (size - fh) // 2))
        if len(spec) > 3 and spec[3] == "gray":
            strip = to_gray(strip)
        strip.save(os.path.join(folder, name + ".png"))
    print("fx", len(FX), "->", folder)


# 밝기만 남긴 흰~회색 그림 (가장 밝은 점이 흰색). 투명도는 그대로
def to_gray(image):
    pixels = image.load()
    w, h = image.size
    lum = lambda r, g, b: 0.299 * r + 0.587 * g + 0.114 * b
    brightest = max((lum(*pixels[x, y][:3]) for x in range(w) for y in range(h) if pixels[x, y][3] > 0), default=255) or 255
    out = Image.new("RGBA", image.size, (0, 0, 0, 0))
    dst = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a:
                v = min(255, round(255 * lum(r, g, b) / brightest))
                dst[x, y] = (v, v, v, a)
    return out


def copy_audio(pack):
    for folder, table in (("Music", MUSIC), ("Sfx", SFX)):
        os.makedirs(os.path.join(AUDIO_OUT, folder), exist_ok=True)
        for name, rel in table.items():
            ext = os.path.splitext(rel)[1]
            shutil.copyfile(os.path.join(pack, rel), os.path.join(AUDIO_OUT, folder, name + ext))
    print("audio", len(MUSIC), "music +", len(SFX), "sfx ->", AUDIO_OUT)

TILESETS = "Backgrounds/Tilesets/"


def crop_front(src, size, dst):
    sheet = Image.open(src).convert("RGBA")
    sheet.crop((0, 0, size, size)).save(dst)


# ------------------------------------------------------------------ 필드 타일
# 맵 글자 한 칸 = 16×16 한 장. 바닥 위에 물건을 겹쳐 그린 결과를 Tiles/{테마}_{종류}[_done].png 로 저장
# (_done: 연 보물상자, 쓰러뜨린 보스 자리). 보스는 그림이 커서 3·4배(48·64px) 타일로 만든다
# 출입구: Door_locked = 보스를 물리쳐야 열리는 막힌 길, Door_{도착 테마} = 그 지역으로 가는 출입구만 다른 그림

class Pack:
    def __init__(self, root):
        self.root = root
        self.cache = {}

    def image(self, rel):
        if rel not in self.cache:
            self.cache[rel] = Image.open(os.path.join(self.root, rel)).convert("RGBA")
        return self.cache[rel]

    def tile(self, sheet, col, row):
        return self.image(TILESETS + sheet).crop((col * 16, row * 16, col * 16 + 16, row * 16 + 16))

    def frame(self, rel, index, width, height=None):
        img = self.image(rel)
        height = height or img.height
        return img.crop((index * width, 0, index * width + width, height))


def over(base, top, scale=1):
    """base(16×16) 위에 top을 가운데·아래 맞춰 겹친다. scale>1이면 바닥을 키워서(도트 유지) 큰 그림을 올린다"""
    out = base.resize((16 * scale, 16 * scale), Image.NEAREST) if scale > 1 else base.copy()
    x = (out.width - top.width) // 2
    y = max(0, out.height - top.height - (1 if top.height < out.height else 0))
    out.alpha_composite(top, (x, y) if top.height < out.height else (x, (out.height - top.height) // 2))
    return out


def ink(tile):
    """물 타일을 어두운 잉크색으로 다시 칠한다 (서고의 잉크 웅덩이)"""
    dark, light = (26, 20, 52), (110, 86, 150)
    out = tile.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            t = (r + g + b) / (3 * 255)
            px[x, y] = tuple(int(dark[i] + (light[i] - dark[i]) * t) for i in range(3)) + (a,)
    return out


def tint(tile, mul):
    """색을 채널별로 곱해 어둡게·물들인다 (숲의 짙은 나무·늪)"""
    out = tile.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            px[x, y] = (min(255, int(r * mul[0])), min(255, int(g * mul[1])), min(255, int(b * mul[2])), a)
    return out


def shade_top(tile, rows, mul):
    """윗부분 몇 줄을 어둡게(mul<1: 나무 그늘이 드리운 숲길 입구) 또는 밝게(mul>1: 햇빛 드는 출구)"""
    out = tile.copy()
    px = out.load()
    for y in range(rows):
        k = mul + (1 - mul) * y / rows
        for x in range(out.width):
            r, g, b, a = px[x, y]
            px[x, y] = (min(255, int(r * k)), min(255, int(g * k)), min(255, int(b * k)), a)
    return out


# ------------------------------------------------------------------ 자동 테두리 (길·물가)
# 팩의 바닥·물 블록(11×5칸)은 47가지 이음 모양 조각이 같은 배치로 들어 있다 (3×3 덩어리, 1칸 폭 세로·가로, 한 칸짜리, 안쪽 모서리 조각들).
# 흙길(연두) 블록의 조각마다 8방향(위·오른위·오른·오른아래·아래·왼아래·왼·왼위)이 흙인지 픽셀로 읽어 '방향표'를 만들고,
# 같은 배치의 다른 블록(진초록 흙길, 물가)에도 그대로 쓴다. 이웃 마스크 256가지 → 조각을 골라 16×16칸 아틀라스
# Tiles/{테마}_{종류}_auto.png 로 저장 → 게임(FieldArt.AutoTile)이 맵 글자의 이웃 모양으로 칸을 고른다
AUTO_REGIONS = [(6, 0, 9, 2), (13, 0, 15, 2), (13, 6, 15, 9), (13, 13, 15, 15),
                (6, 13, 9, 15), (0, 13, 2, 15), (0, 6, 2, 9), (0, 0, 2, 2)]  # 위 오른위 오른 오른아래 아래 왼아래 왼 왼위
AUTO_N, AUTO_NE, AUTO_E, AUTO_SE, AUTO_S, AUTO_SW, AUTO_W, AUTO_NW = (1 << i for i in range(8))


def auto_normalize(mask):
    """모서리는 맞닿은 두 변이 모두 이어질 때만 센다 (게임의 FieldAutotile.Normalize와 같은 규칙)"""
    for corner, a, b in ((AUTO_NE, AUTO_N, AUTO_E), (AUTO_SE, AUTO_S, AUTO_E),
                         (AUTO_SW, AUTO_S, AUTO_W), (AUTO_NW, AUTO_N, AUTO_W)):
        if not (mask & a and mask & b):
            mask &= ~corner
    return mask


def auto_template(pack):
    floor = pack.image(TILESETS + "TilesetFloor.png")

    def is_dirt(px):
        r, g, b, a = px
        return a > 0 and r > g + 25 and r > 110

    template = []
    cells = [(c, r) for r in range(4) for c in range(11)] + [(c, 4) for c in range(4, 11)]
    for c, r in cells:
        t = floor.crop((c * 16, (7 + r) * 16, c * 16 + 16, (8 + r) * 16))
        if t.getbbox() is None:
            continue
        px = t.load()
        sig = 0
        for bit, (x0, y0, x1, y1) in enumerate(AUTO_REGIONS):
            area = [(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)]
            if sum(1 for xy in area if is_dirt(px[xy])) * 2 >= len(area):
                sig |= 1 << bit
        template.append(((c, r), auto_normalize(sig)))
    return template


def auto_atlas(pack, template, sheet, col0, row0, recolor=None):
    img = pack.image(TILESETS + sheet)

    def cost(mask, sig):
        diff = mask ^ sig
        edges = bin(diff & (AUTO_N | AUTO_E | AUTO_S | AUTO_W)).count("1")
        return edges * 4 + bin(diff).count("1") - edges

    out = Image.new("RGBA", (256, 256))
    for mask in range(256):
        m = auto_normalize(mask)
        (c, r), _ = min(template, key=lambda e: cost(m, e[1]))
        t = img.crop(((col0 + c) * 16, (row0 + r) * 16, (col0 + c + 1) * 16, (row0 + r + 1) * 16))
        if recolor:
            t = recolor(t)
        out.alpha_composite(t, ((mask % 16) * 16, (mask // 16) * 16))
    return out


def forest_water(t):
    """연두 물가를 숲의 진초록으로, 물은 초록빛 늪으로"""
    out = t.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if b > r + 50:  # 물
                px[x, y] = (int(r * 0.55), int(g * 0.85), int(b * 0.7), a)
            elif g > r and g > b + 30:  # 연두 풀 → 이끼색
                px[x, y] = (int(r * 0.62), int(g * 0.82), int(b * 0.8), a)
    return out


def pond_on(base, pond):
    """작은 연못 그림에서 둘레 모래를 지우고 바닥 위에 올린다 (회복의 샘)"""
    cut = pond.copy()
    px = cut.load()
    for y in range(cut.height):
        for x in range(cut.width):
            r, g, b, a = px[x, y]
            if r > 200 and g > 120 and b < 150:  # 주황 모래
                px[x, y] = (0, 0, 0, 0)
    return over(base, cut)


def copy_relics_and_items(pack):
    for folder in ("Relics", "Items"):
        os.makedirs(os.path.join(OUT, folder), exist_ok=True)
    for relic_id, (rel, size) in RELICS.items():
        dst = os.path.join(OUT, "Relics", relic_id + ".png")
        if size:
            crop_front(os.path.join(pack, rel), size, dst)
        else:
            shutil.copyfile(os.path.join(pack, rel), dst)
    for item_id, rel in ITEMS.items():
        shutil.copyfile(os.path.join(pack, rel), os.path.join(OUT, "Items", item_id + ".png"))
    print("relics", len(RELICS), "+ items", len(ITEMS), "->", OUT)


def build_tiles(pack):
    boss = pack.frame("Actor/Boss/GiantSpirit/Idle.png", 0, 50).crop((1, 1, 49, 49))
    raccoon = pack.frame("Actor/Boss/GiantRacoon/Idle.png", 0, 60)
    raccoon = raccoon.crop(raccoon.getbbox())
    brambles = pack.tile("TilesetNature.png", 4, 9)  # 엉킨 마른 덤불 = 막힌 길
    book = pack.image("Items/Object/Book.png")
    big_chest = [pack.frame("Items/Treasure/BigTreasureChest.png", i, 16) for i in range(2)]
    small_chest = [pack.frame("Items/Treasure/LittleTreasureChest.png", i, 16) for i in range(2)]
    gem = pack.tile("TilesetNature.png", 2, 15)
    stall = pack.tile("TilesetElement.png", 14, 0)
    pedestal = pack.tile("TilesetDungeon.png", 2, 3)  # 가운데가 빈 돌 받침 = 사전 받침대 (#45)

    def lectern(base, with_book):
        """받침대를 2배로(도트 유지) 깔고, 사전이 놓였으면 책(원래 크기)을 위에 올린다 → 32×32 한 칸"""
        out = base.resize((32, 32), Image.NEAREST)
        out.alpha_composite(pedestal.resize((32, 32), Image.NEAREST), (0, 0))
        if with_book:
            out.alpha_composite(book, (8, 1))
        return out

    themes = {}
    autos = {}
    template = auto_template(pack)
    pond = pack.tile("TilesetWater.png", 3, 3)
    # 초원: 흙길(연두 풀 테두리 — 자동), 짧은 잔디, 진한 풀숲(긴 풀잎), 덤불 벽, 물가(자동), 굴 입구, 작은 연못(회복의 샘).
    # 상자·제단·상점·보스는 잔디 위 (길이 아닌 칸이라 길 테두리가 둘레를 감싼다)
    sand = pack.tile("TilesetFloor.png", 1, 1)
    lime = pack.tile("TilesetField.png", 1, 4)
    path = pack.tile("TilesetFloor.png", 1, 8)  # 흙길 가운데 조각
    themes["Meadow"] = {
        "Floor": path,
        "Lawn": lime,  # 짧은 잔디 (조우 없음)
        "Grass": over(pack.tile("TilesetField.png", 1, 7), pack.tile("TilesetNature.png", 7, 10)),
        "Wall": over(lime, pack.tile("TilesetNature.png", 1, 10)),
        "Water": pack.tile("TilesetWater.png", 1, 7),
        "Door": over(lime, pack.tile("TilesetNature.png", 7, 13)),
        "Door_locked": over(lime, brambles),
        "Door_Forest": shade_top(path, 9, 0.45),  # 그늘진 숲길 입구
        "Fountain": pond_on(lime, pond),
        "Chest": over(lime, big_chest[0]),
        "Chest_done": over(lime, big_chest[1]),
        "Altar": over(lime, gem),
        "Shop": over(lime, stall),
        "Boss": over(lime, boss, scale=3),
        "Boss_done": lime.copy(),  # 사전은 쉼터 받침대로 옮겨져 빈자리 (#45)
        "Lectern": lectern(lime, False),
        "Lectern_done": lectern(lime, True),
    }
    autos["Meadow_Floor_auto"] = auto_atlas(pack, template, "TilesetFloor.png", 0, 7)
    autos["Meadow_Water_auto"] = auto_atlas(pack, template, "TilesetWater.png", 0, 6)
    # 서고: 어두운 돌바닥(책장과 잘 구분되게), 책장 벽, 잉크 웅덩이, 나무 문,
    #       받침대 위 파란 구슬(회복 지점), 작은 트렁크. 풀숲 = 바닥에 흩어진 하얀 종이 조각
    stone = pack.tile("Interior/TilesetInteriorFloor.png", 16, 13)
    themes["Library"] = {
        "Floor": stone,
        "Lawn": pack.tile("Interior/TilesetInteriorFloor.png", 12, 7),  # 열람실 초록 카펫 (조우 없음)
        "Grass": over(stone, pack.tile("TilesetFloorDetail.png", 1, 3)),  # 흩어진 종이 조각
        "Wall": over(stone, pack.tile("TilesetElement.png", 3, 8)),
        "Water": ink(pack.tile("TilesetWater.png", 11, 2)),
        "Door": over(stone, pack.tile("TilesetElement.png", 8, 13)),
        "Door_locked": over(stone, pack.tile("TilesetElement.png", 1, 11)),  # 쇠창살
        "Fountain": over(stone, pack.tile("TilesetDungeon.png", 4, 2)),
        "Chest": over(stone, small_chest[0]),
        "Chest_done": over(stone, small_chest[1]),
        "Altar": over(stone, gem),
        "Shop": over(stone, stall),
        "Boss": over(stone, boss, scale=3),
        "Boss_done": stone.copy(),  # 사전은 쉼터 받침대로 옮겨져 빈자리 (#45)
        "Lectern": lectern(stone, False),
        "Lectern_done": lectern(stone, True),
    }

    # 숲: 흙길, 진한 풀 위 고사리(조우), 짙은 덤불 벽, 초록빛 늪, 햇빛 드는 출구(초원으로)
    dirt = pack.tile("TilesetFloor.png", 12, 8)
    moss = pack.tile("TilesetField.png", 1, 7)
    deep = tint(moss, (0.62, 0.72, 0.62))
    themes["Forest"] = {
        "Floor": dirt,
        "Lawn": moss,  # 이끼 낀 땅 (조우 없음)
        "Grass": over(moss, pack.tile("TilesetNature.png", 4, 11)),
        "Wall": over(deep, tint(pack.tile("TilesetNature.png", 10, 9), (0.55, 0.75, 0.55))),
        "Water": forest_water(pack.tile("TilesetWater.png", 1, 7)),
        "Door": shade_top(sand, 16, 1.25),  # 밝은 모래길 = 초원으로 나가는 길
        "Door_locked": over(moss, brambles),
        "Fountain": pond_on(moss, pond),
        "Chest": over(moss, big_chest[0]),
        "Chest_done": over(moss, big_chest[1]),
        "Altar": over(moss, gem),
        "Shop": over(moss, stall),
        "Boss": over(moss, raccoon, scale=4),
        "Boss_done": moss.copy(),  # 사전은 쉼터 받침대로 옮겨져 빈자리 (#45)
        "Lectern": lectern(moss, False),
        "Lectern_done": lectern(moss, True),
    }
    # 숲: 흙길(진초록 테두리), 늪 물가(연두를 이끼색으로)
    autos["Forest_Floor_auto"] = auto_atlas(pack, template, "TilesetFloor.png", 11, 7)
    autos["Forest_Water_auto"] = auto_atlas(pack, template, "TilesetWater.png", 0, 6, forest_water)

    folder = os.path.join(OUT, "Tiles")
    os.makedirs(folder, exist_ok=True)
    for theme, tiles in themes.items():
        for kind, img in tiles.items():
            img.save(os.path.join(folder, f"{theme}_{kind}.png"))
    for name, img in autos.items():
        img.save(os.path.join(folder, name + ".png"))
    print("tiles", sum(len(t) for t in themes.values()), "+ autotile", len(autos), "->", folder)


def main():
    pack = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_PACK
    if not os.path.isdir(pack):
        sys.exit(f"팩 폴더가 없습니다: {pack}")

    os.makedirs(os.path.join(OUT, "Monsters"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "Player"), exist_ok=True)

    for species_id, (rel, size) in MONSTERS.items():
        crop_front(os.path.join(pack, rel), size, os.path.join(OUT, "Monsters", species_id + ".png"))
        print("monster", species_id, "<-", rel)
    build_scarecrow(pack)
    build_bubbles(pack)

    shutil.copyfile(os.path.join(pack, PLAYER), os.path.join(OUT, "Player", "Boy.png"))
    print("player <-", PLAYER)

    copy_relics_and_items(pack)

    art = Pack(pack)
    build_tiles(art)
    build_title_scene()
    build_fx(art)
    copy_audio(pack)

    for name in ("LICENSE.txt", "README.md"):
        shutil.copyfile(os.path.join(pack, name), os.path.join(OUT, name))
    print("done ->", OUT)


if __name__ == "__main__":
    main()
