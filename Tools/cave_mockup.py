"""1.2 동굴 그림 시안 (#111) — 팩 타일로 임시 합성한 동굴 바닥·바위벽 + 어둠 (GDD 9-7)

   python Tools/cave_mockup.py   → _캡처/111_동굴시안/ (밝은 화면 · 어둠 화면 · 반딧돌 꺼짐/켜짐 · 처음 들어선 어둠)

Figma 화면(작업메모 #111) 배경과 '벽과 땅이 한눈에 구분되는지'(사용자 #100) 확인용. 게임에 들어갈 그림은 만들기 단계에서 import_ninja_art.py에.
쓴 조각: 바위 바닥 = TilesetFloor (15,19) · 다져진 길 = (11,19) 어둡게 · 바위벽 윗면 = 짙은 청회색 + 바닥 쪽 밝은 테 ·
절벽면(아래가 바닥) = TilesetRelief (5,1) 청회색 · 먹물 = TilesetWater (11,2) 남보라 + 둥근 모서리 · 빛이끼 = 청록 포기 · 반딧돌 = TilesetDungeon (3,2)/(4,2)
맵은 Builds/maps/CaveMap.txt (먼저 python Tools/cave_map.py)"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = r"F:\Unity\김수연습\Ninja Adventure - Asset Pack\Ninja Adventure - Asset Pack"
T = os.path.join(PACK, "Backgrounds", "Tilesets")
OUT = os.path.join(ROOT, "_캡처", "111_동굴시안")
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, os.path.join(ROOT, "Tools"))

g = [list(r) for r in open(os.path.join(ROOT, "Builds", "maps", "CaveMap.txt"), encoding="utf-8").read().split("\n") if r]
H, W = len(g), len(g[0])


def sheet(name):
    return Image.open(os.path.join(T, name)).convert("RGBA")


def tile(img, c, r, w=1, h=1):
    return img.crop((c * 16, r * 16, (c + w) * 16, (r + h) * 16))


def tint(t, mul, add=(0, 0, 0)):
    out = t.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, gg, b, a = px[x, y]
            px[x, y] = (min(255, int(r * mul[0] + add[0])), min(255, int(gg * mul[1] + add[1])), min(255, int(b * mul[2] + add[2])), a)
    return out


def recolor(t, dark, light):
    out = t.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, gg, b, a = px[x, y]
            k = (r + gg + b) / 765
            px[x, y] = tuple(int(dark[i] + (light[i] - dark[i]) * k) for i in range(3)) + (a,)
    return out


floor_sheet = sheet("TilesetFloor.png")
relief = sheet("TilesetRelief.png")
dungeon = sheet("TilesetDungeon.png")
water = sheet("TilesetWater.png")

# 바닥: 팩의 회갈색 흙 블록 (11~21열 14~20행) — 바위 바닥(밝은 회갈) · 다져진 길(짙은 회갈)
rock_floor = tint(tile(floor_sheet, 15, 19), (0.8, 0.79, 0.8))            # 밝은 회갈 바위 바닥 (조우 없음) — 잔무늬 조금
path_floor = tint(tile(floor_sheet, 11, 19), (0.7, 0.68, 0.68))           # 짙은 회갈 다져진 길 (민무늬 — 게임에선 테두리 자동 조각)
# 바위벽: 윗면 = 짙은 청회색 바위(점점이 결) + 바닥 쪽 가장자리 밝은 테 / 아래가 바닥이면 절벽면(팩 Relief 회녹색 세로 줄무늬를 청회색으로)
import random as _r
_rng = _r.Random(7)
def make_top(seed):
    t = Image.new("RGBA", (16, 16), (40, 43, 53, 255))
    p = t.load()
    rr = _r.Random(seed)
    for _ in range(14):
        x, y = rr.randrange(16), rr.randrange(16)
        p[x, y] = (52, 56, 68, 255) if rr.random() < 0.6 else (31, 33, 41, 255)
    return t
wall_tops = [make_top(i) for i in range(4)]
face = recolor(tile(relief, 5, 1), (30, 34, 42), (112, 122, 136))
# 먹물: 서고 잉크 색 (남보라)
ink = recolor(tile(water, 11, 2), (26, 20, 52), (96, 76, 140))
# 빛이끼 덤불 (몬스터 서식지): 바위 바닥 위에 은은한 청록 이끼 포기 몇 개 (칸 전체를 칠하지 않음 — 네모로 보이지 않게)
def make_moss(seed):
    t = Image.blend(rock_floor, tint(rock_floor, (0.8, 1.02, 0.98)), 0.7)   # 바탕도 아주 살짝 청록 (서식지가 어디까지인지 보이게, 은은하게)
    p = t.load()
    rr = _r.Random(100 + seed)
    for _ in range(4):
        cx, cy = rr.randrange(2, 14), rr.randrange(3, 15)
        for dx, dy, col in ((0, 0, (150, 225, 196)), (-1, 0, (78, 168, 146)), (1, 0, (78, 168, 146)), (0, -1, (96, 190, 164)),
                            (-1, 1, (52, 120, 108)), (1, 1, (52, 120, 108)), (0, 1, (60, 140, 124))):
            if 0 <= cx + dx < 16 and 0 <= cy + dy < 16:
                p[cx + dx, cy + dy] = col + (255,)
    return t
mosses = [make_moss(i) for i in range(4)]
orb_off, orb_on = tile(dungeon, 3, 2), tile(dungeon, 4, 2)
chest = Image.open(os.path.join(PACK, "Items", "Treasure", "LittleTreasureChest.png")).convert("RGBA").crop((0, 0, 16, 16))
stalag = recolor(tile(sheet("TilesetReliefDetail.png"), 4, 0), (40, 42, 50), (130, 130, 140))
boy = Image.open(os.path.join(ROOT, "Assets", "Resources", "Art", "NinjaAdventure", "Player", "Boy.png")).convert("RGBA")
ara = Image.open(os.path.join(ROOT, "Assets", "Resources", "Art", "NinjaAdventure", "Companions", "ara_sheet.png")).convert("RGBA")
salamander = recolor(Image.open(os.path.join(PACK, "Actor", "Monsters", "AxolotBlue", "SpriteSheet.png")).convert("RGBA").crop((0, 0, 16, 16)),
                     (20, 14, 40), (150, 120, 200))
salamander.save(os.path.join(OUT, "salamander_ink.png"))


def is_wall(x, y):
    return not (0 <= x < W and 0 <= y < H) or g[y][x] in "#"


def base_of(c, x=0, y=0):
    return path_floor if c in ".PD" else mosses[(x * 5 + y * 11) % 4] if c == "," else rock_floor


def render(lit_stones=()):
    img = Image.new("RGBA", (W * 16, H * 16))
    for y in range(H):
        for x in range(W):
            c = g[y][x]
            if c == "#":
                if not is_wall(x, y + 1):
                    t = face
                else:
                    t = wall_tops[(x * 7 + y * 13) % 4].copy()
                    tp = t.load()
                    for (nx, ny, edge) in ((x, y - 1, "n"), (x - 1, y, "w"), (x + 1, y, "e")):
                        if not is_wall(nx, ny):        # 바닥에 닿은 쪽은 밝은 바위 테 (벽 모양이 한눈에)
                            for i in range(16):
                                xx, yy = {"n": (i, 0), "w": (0, i), "e": (15, i)}[edge]
                                tp[xx, yy] = (96, 104, 120, 255)
                                xx2, yy2 = {"n": (i, 1), "w": (1, i), "e": (14, i)}[edge]
                                tp[xx2, yy2] = (66, 72, 86, 255)
            elif c == "~":
                t = ink.copy()
                tp = t.load()
                wet = lambda nx, ny: 0 <= nx < W and 0 <= ny < H and g[ny][nx] not in "~#"
                n, so, we, ea = wet(x, y - 1), wet(x, y + 1), wet(x - 1, y), wet(x + 1, y)
                fl = rock_floor.load()
                for i in range(16):        # 바닥에 닿은 쪽: 젖은 돌 테 1px + 둥근 모서리 (네모 웅덩이로 보이지 않게)
                    if n: tp[i, 0] = (70, 62, 96, 255)
                    if so: tp[i, 15] = (70, 62, 96, 255)
                    if we: tp[0, i] = (70, 62, 96, 255)
                    if ea: tp[15, i] = (70, 62, 96, 255)
                for (cx, cy, a, b) in ((0, 0, n, we), (15, 0, n, ea), (0, 15, so, we), (15, 15, so, ea)):
                    if a and b:
                        for k in range(3):
                            for j in range(3 - k):
                                xx = cx + (k if cx == 0 else -k); yy = cy + (j if cy == 0 else -j)
                                tp[xx, yy] = fl[xx, yy]
            else:
                t = base_of(c, x, y).copy()
                if c == "I":
                    t.alpha_composite(orb_on if (x, y) in lit_stones else orb_off)
                elif c == "C":
                    t.alpha_composite(chest)
                elif c == "Q":
                    t.alpha_composite(stalag)
            img.alpha_composite(t, (x * 16, y * 16))
    # 바위 윗면 아래쪽 1px 그늘 줄 (절벽 위 모서리)
    return img


def lightmap(lights, explored, memory=0.12):
    # 픽셀마다 밝기 (16px 단위가 아니라 4px 단위로 계산해 부드럽게)
    S = 4
    lw, lh = W * 16 // S, H * 16 // S
    lm = Image.new("L", (lw, lh))
    px = lm.load()
    for y in range(lh):
        for x in range(lw):
            cx, cy = (x + 0.5) * S / 16, (y + 0.5) * S / 16
            v = 0.0
            for lx, ly, r in lights:
                d = math.hypot(cx - lx, cy - ly)
                v = max(v, min(1.0, max(0.0, (r - d) / 1.6)))
            if (int(cx), int(cy)) in explored:
                v = max(v, memory)
            px[x, y] = int(255 * v)
    return lm.resize((W * 16, H * 16), Image.BILINEAR).filter(ImageFilter.GaussianBlur(3))


def darken(img, lm, warm=None):
    dark = Image.new("RGBA", img.size, (6, 6, 12, 255))
    out = Image.composite(img, dark, lm)
    return out


def sprite_at(img, spr, x, y, dy=-4):
    img.alpha_composite(spr, (int(x * 16), int(y * 16) + dy))


# ---------------------------------------------------------------- 장면: 주인공이 대동굴로 올라가는 굴, 아라는 뒤에서 횃불 · 반딧돌 하나는 밝힘 · 먹물 웅덩이에 거품 · 어둠 속 눈
HERO, ARA = (29.0, 37.0), (29.0, 38.0)
LIT = {(25, 30)}
scene = render(LIT)
sprite_at(scene, boy.crop((16, 0, 32, 16)), *HERO)                 # 뒷모습 (위로 걸음)
sprite_at(scene, ara.crop((16, 0, 32, 16)), *ARA)
d = ImageDraw.Draw(scene)
tx, ty = int(ARA[0] * 16) + 13, int(ARA[1] * 16) - 2                 # 아라 횃불 (손 위 작은 불꽃)
d.rectangle([tx, ty + 4, tx + 1, ty + 9], fill=(110, 70, 40))
d.rectangle([tx - 1, ty + 1, tx + 2, ty + 4], fill=(255, 170, 40))
d.rectangle([tx, ty, tx + 1, ty + 2], fill=(255, 236, 140))
# 먹물 웅덩이 거품 (31~33, 34~36 근처 웅덩이)
pool = [(x, y) for y in range(33, 38) for x in range(29, 34) if g[y][x] == "~"]
for i, (x, y) in enumerate(pool[:3]):
    bx, by = x * 16 + 5 + (i * 3) % 7, y * 16 + 5 + (i * 5) % 6
    d.ellipse([bx, by, bx + 4, by + 4], outline=(190, 170, 235), width=1)
    d.ellipse([bx + 6, by - 3, bx + 8, by - 1], outline=(170, 150, 220), width=1)
explored = {(x, y) for y in range(36, 50) for x in range(20, 32)} | {(x, y) for y in range(26, 37) for x in range(18, 34)}
lights = [(HERO[0] + 0.5, HERO[1] + 0.5, 4.5), (25.5, 30.5, 6.0)]
lm = lightmap(lights, explored)
dark = darken(scene, lm)
# 어둠 속 몬스터 눈 두 개 (빛 밖) — 엉킴거미 하나, 깜빡유령 하나
dd = ImageDraw.Draw(dark)
for ex, ey, col in [(33.3, 28.6, (255, 90, 70)), (24.6, 45.2, (255, 90, 70))]:
    px_, py_ = int(ex * 16), int(ey * 16)
    for k in (0, 5):
        dd.rectangle([px_ + k, py_, px_ + k + 1, py_ + 1], fill=col)
dark.save(os.path.join(OUT, "cave_dark_full.png"))
scene.save(os.path.join(OUT, "cave_lit_full.png"))

# 화면 크기로 자르기 (가로 11.25칸 = 96px/칸, 세로 20칸) — 주인공이 화면 가운데쯤
SCALE = 6
cw, ch = 1080 / (16 * SCALE), 1920 / (16 * SCALE)
x0 = HERO[0] + 0.5 - cw / 2
y0 = HERO[1] + 0.5 - ch * 0.55
box = (int(x0 * 16), int(y0 * 16), int(x0 * 16) + 180, int(y0 * 16) + 320)
for name, im in (("cave_dark_screen.png", dark), ("cave_lit_screen.png", scene)):
    im.crop(box).resize((1080, 1920), Image.NEAREST).convert("RGB").save(os.path.join(OUT, name))
print("ok", box)


# ---------------------------------------------------------------- 장면 B: 반딧돌 앞 (꺼짐 → 밝힘) — 반딧돌이 화면 (539, 1127)에 오게 (석판 문제 화면과 같은 자리)
STONE = (25, 30)
HB, AB = (26.0, 30.0), (27.0, 30.0)   # 반딧돌 오른쪽에서 왼쪽을 봄, 아라는 뒤
x0b, y0b = STONE[0] + 0.5 - 539 / 96, STONE[1] + 0.5 - 1127 / 96
boxb = (int(round(x0b * 16)), int(round(y0b * 16)), int(round(x0b * 16)) + 180, int(round(y0b * 16)) + 320)
for name, lit in (("cave_stone_off.png", False), ("cave_stone_on.png", True)):
    sc = render({STONE} if lit else set())
    sprite_at(sc, boy.crop((32, 0, 48, 16)), *HB)
    sprite_at(sc, ara.crop((32, 0, 48, 16)), *AB)
    dr = ImageDraw.Draw(sc)
    tx, ty = int(AB[0] * 16) + 1, int(AB[1] * 16) - 2
    dr.rectangle([tx, ty + 4, tx + 1, ty + 9], fill=(110, 70, 40))
    dr.rectangle([tx - 1, ty + 1, tx + 2, ty + 4], fill=(255, 170, 40))
    dr.rectangle([tx, ty, tx + 1, ty + 2], fill=(255, 236, 140))
    ls = [(HB[0] + 0.5, HB[1] + 0.5, 4.5)] + ([(STONE[0] + 0.5, STONE[1] + 0.5, 6.0)] if lit else [])
    expl = {(x, y) for y in range(30, 50) for x in range(20, 30)}
    out = darken(sc, lightmap(ls, expl))
    out.crop(boxb).resize((1080, 1920), Image.NEAREST).convert("RGB").save(os.path.join(OUT, name))
print("ok B", boxb)


# ---------------------------------------------------------------- 장면 A2: 처음 대동굴로 들어서는 순간 — 반딧돌은 아직 꺼짐(어둠 속에서 아주 희미하게 깜빡), 몬스터는 눈만, 웅덩이 거품
sa = render(set())
sprite_at(sa, boy.crop((16, 0, 32, 16)), *HERO)
sprite_at(sa, ara.crop((16, 0, 32, 16)), *ARA)
da = ImageDraw.Draw(sa)
tx, ty = int(ARA[0] * 16) + 13, int(ARA[1] * 16) - 2
da.rectangle([tx, ty + 4, tx + 1, ty + 9], fill=(110, 70, 40))
da.rectangle([tx - 1, ty + 1, tx + 2, ty + 4], fill=(255, 170, 40))
da.rectangle([tx, ty, tx + 1, ty + 2], fill=(255, 236, 140))
for i, (x, y) in enumerate(pool[:3]):
    bx, by = x * 16 + 5 + (i * 3) % 7, y * 16 + 5 + (i * 5) % 6
    da.ellipse([bx, by, bx + 4, by + 4], outline=(190, 170, 235), width=1)
    da.ellipse([bx + 6, by - 3, bx + 8, by - 1], outline=(170, 150, 220), width=1)
expl_a = {(x, y) for y in range(38, 50) for x in range(20, 32)}
la = lightmap([(HERO[0] + 0.5, HERO[1] + 0.5, 4.5)], expl_a)
out_a = darken(sa, la)
# 꺼진 반딧돌: 어둠 속에서도 아주 희미한 청록 빛 (찾을 수 있게)
glow = Image.new("RGBA", out_a.size, (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
sx, sy = 25 * 16 + 8, 30 * 16 + 7
gd.ellipse([sx - 9, sy - 9, sx + 9, sy + 9], fill=(90, 200, 220, 60))
gd.ellipse([sx - 4, sy - 4, sx + 4, sy + 4], fill=(150, 230, 240, 110))
out_a.alpha_composite(glow.filter(ImageFilter.GaussianBlur(2)))
oa = ImageDraw.Draw(out_a)
oa.rectangle([sx - 1, sy - 3, sx, sy - 2], fill=(190, 240, 250))
for ex, ey, col in [(27.6, 27.4, (255, 90, 70)), (24.3, 31.4, (255, 90, 70))]:
    px_, py_ = int(ex * 16), int(ey * 16)
    for k in (0, 5):
        oa.rectangle([px_ + k, py_, px_ + k + 1, py_ + 1], fill=col)
X0, Y0 = 21.0, 24.25
boxa = (int(X0 * 16), int(Y0 * 16), int(X0 * 16) + 180, int(Y0 * 16) + 320)
out_a.crop(boxa).resize((1080, 1920), Image.NEAREST).convert("RGB").save(os.path.join(OUT, "cave_first_dark.png"))
print("ok A2", boxa)
