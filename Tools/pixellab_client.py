"""PixelLab API 도우미 (#94 — 2.1 Under the Sea 그림 시험). 키는 환경변수 PIXELLAB_KEY.
키는 저장소에 적지 말 것 (사용자 PC의 ~/.claude.json, pixellab MCP 설정에 있음). 결과 PNG → 이 파일 옆 out/ (저장소에 안 올라감)

시험해 보고 알게 된 것 (2026-10-16, 무료 체험 40회 중 32회 사용):
- style(): Ninja 그림 1~4장을 예시로 주면 그 느낌으로 16px 64장 (10회) — 몬스터·꾸밈은 이게 제일 좋음 ★
- rotate4(): 고른 정면 그림 → 4방향 (1회). 걷기 프레임은 코드로 (Ninja 몬스터는 1px 위아래)
- tileset(): 이어지는 바닥 타일 16장 (1회, 높이 차 transition 0.25면 4회). 참고 그림을 주면 그 그림을 그대로 베낌 → 글로만
- bitforge(): 16px 한 장 (1회) — 품질 낮음. character(): 사람 뼈대가 기본이라 물고기가 사람 모양 → 쓰지 말 것
- create-tiles-pro(Ninja 느낌 바닥 타일)는 8회로 부족했음 — 크레딧 필요
사용: python Tools/pixellab_client.py balance"""
import base64, io, json, os, sys, time, urllib.request, urllib.error
from PIL import Image

KEY = os.environ["PIXELLAB_KEY"]
BASE = "https://api.pixellab.ai/v2"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "_캡처", "PixelLab")
os.makedirs(OUT, exist_ok=True)
PACK = r"F:\Unity\김수연습\Ninja Adventure - Asset Pack\Ninja Adventure - Asset Pack"
TILES = r"F:\GameProject\WordRPG\Assets\Resources\Art\NinjaAdventure\Tiles"


def call(method, path, payload=None):
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method,
                                 headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=180) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")


def b64(img):
    buf = io.BytesIO(); img.save(buf, "PNG")
    return {"type": "base64", "base64": base64.b64encode(buf.getvalue()).decode()}


def unb64(obj):
    s = obj["base64"] if isinstance(obj, dict) else obj
    if s.startswith("data:"): s = s.split(",", 1)[1]
    return Image.open(io.BytesIO(base64.b64decode(s))).convert("RGBA")


def balance():
    _, b = call("GET", "/balance")
    return b["subscription"]["generations"]


def palette():
    # 팩 색표에서 쓰인 색만 모아 작은 그림으로
    p = Image.open(os.path.join(PACK, "Palette.png")).convert("RGBA")
    cols = sorted({c for c in p.getdata() if c[3] > 0})
    img = Image.new("RGBA", (len(cols), 1))
    img.putdata(cols)
    return img


def tile(name):
    return Image.open(os.path.join(TILES, name)).convert("RGBA")


def wait_job(job_id, label):
    for _ in range(90):
        code, j = call("GET", f"/background-jobs/{job_id}")
        st = j.get("status")
        if st in ("completed", "failed", "error", "cancelled"):
            return j
        time.sleep(4)
    raise TimeoutError(label)


def log(label, before, extra=""):
    after = balance()
    line = f"{label}: {before - after:g} generations (남음 {after:g}) {extra}"
    print(line)
    with open(os.path.join(OUT, "log.txt"), "a", encoding="utf-8") as f:
        f.write(line + "\n")


def tileset(label, lower, upper, lower_ref, upper_ref, transition=0.0):
    before = balance()
    code, j = call("POST", "/create-tileset", {
        "lower_description": lower, "upper_description": upper,
        "tile_size": {"width": 16, "height": 16}, "view": "high top-down",
        "outline": "lineless", "shading": "basic shading", "detail": "medium detail",
        "transition_size": transition,
        "lower_reference_image": b64(lower_ref), "upper_reference_image": b64(upper_ref),
        "seed": 7,
    })
    if code not in (200, 202):
        print(label, code, json.dumps(j)[:400]); return
    tid = j.get("tileset_id") or j.get("id") or j.get("data", {}).get("tileset_id")
    for _ in range(90):
        code, t = call("GET", f"/tilesets/{tid}")
        if code == 200 and t.get("tileset"): break
        time.sleep(4)
    tiles = t["tileset"]["tiles"]
    sheet = Image.new("RGBA", (16 * 4, 16 * ((len(tiles) + 3) // 4)))
    meta = []
    for i, tl in enumerate(tiles):
        sheet.alpha_composite(unb64(tl["image"]), ((i % 4) * 16, (i // 4) * 16))
        meta.append({k: tl.get(k) for k in ("corners", "pattern_4x4", "name")})
    sheet.save(os.path.join(OUT, f"{label}.png"))
    json.dump(meta, open(os.path.join(OUT, f"{label}.json"), "w"), ensure_ascii=False)
    log(label, before)


def bitforge(label, desc, w, h, style=None, strength=40, negative=""):
    before = balance()
    payload = {"description": desc, "negative_description": negative, "image_size": {"width": w, "height": h},
               "view": "high top-down", "no_background": True, "outline": "single color black outline",
               "shading": "basic shading", "detail": "medium detail", "color_image": b64(palette()), "seed": 11}
    if style is not None:
        payload["style_image"] = b64(style); payload["style_strength"] = strength
    code, j = call("POST", "/create-image-bitforge", payload)
    if code != 200:
        print(label, code, json.dumps(j)[:400]); return
    unb64(j["image"]).save(os.path.join(OUT, f"{label}.png"))
    log(label, before)


def character(label, desc, size=16):
    before = balance()
    code, j = call("POST", "/create-character-with-4-directions", {
        "description": desc, "image_size": {"width": size, "height": size},
        "view": "low top-down", "outline": "single color black outline", "shading": "basic shading",
        "detail": "low detail", "color_image": b64(palette()), "seed": 5})
    if code != 200:
        print(label, code, json.dumps(j)[:400]); return
    wait_job(j["background_job_id"], label)
    code, c = call("GET", f"/characters/{j['character_id']}")
    urls = c.get("rotation_urls") or {}
    imgs = []
    for d in ("south", "west", "east", "north"):
        u = urls.get(d)
        if not u: continue
        with urllib.request.urlopen(u, timeout=60) as r:
            imgs.append(Image.open(io.BytesIO(r.read())).convert("RGBA"))
    if imgs:
        w, h = imgs[0].size
        sheet = Image.new("RGBA", (w * len(imgs), h))
        for i, im in enumerate(imgs): sheet.alpha_composite(im, (i * w, 0))
        sheet.save(os.path.join(OUT, f"{label}.png"))
    json.dump({"character_id": j["character_id"]}, open(os.path.join(OUT, f"{label}.json"), "w"))
    log(label, before, f"character_id={j['character_id']}")



def style(label, desc, refs, style_desc, seed=1):
    """Ninja 그림 refs(1~4장, PIL) 느낌으로 desc를 그림. 16~32px면 64장 → out/label/00.png… + 격자 out/label.png"""
    before = balance()
    code, j = call("POST", "/generate-with-style-v2", {
        "style_images": [{"image": b64(r), "width": r.width, "height": r.height} for r in refs],
        "description": desc, "style_description": style_desc, "seed": seed, "no_background": True})
    if code not in (200, 202):
        print(label, code, json.dumps(j)[:400]); return None
    job = wait_job(j["background_job_id"], label)
    ims = [unb64(x) for x in (job.get("last_response") or {}).get("images", [])]
    if not ims:
        print(label, job.get("status")); return None
    os.makedirs(os.path.join(OUT, label), exist_ok=True)
    for i, im in enumerate(ims): im.save(os.path.join(OUT, label, f"{i:02d}.png"))
    log(label, before, f"{len(ims)} images {ims[0].width}x{ims[0].height}")
    return ims


def rotate4(label, image, desc):
    """정면(아래 보는) 그림 → 4방향 시트 (v3 회전)"""
    import zipfile
    before = balance()
    code, j = call("POST", "/create-character-v3", {"description": desc, "reference_image": b64(image),
                                                    "n_directions": 4, "view": "low top-down", "no_background": True})
    if code not in (200, 202):
        print(label, code, json.dumps(j)[:400]); return None
    wait_job(j["background_job_id"], label)
    req = urllib.request.Request(f"{BASE}/characters/{j['character_id']}/spritesheet", headers={"Authorization": f"Bearer {KEY}"})
    z = zipfile.ZipFile(io.BytesIO(urllib.request.urlopen(req, timeout=60).read()))
    sheet = Image.open(io.BytesIO(z.read(next(n for n in z.namelist() if n.endswith(".png"))))).convert("RGBA")
    sheet.save(os.path.join(OUT, f"{label}.png"))
    log(label, before)
    return sheet


if __name__ == "__main__":
    if sys.argv[1:] == ["balance"]:
        print(call("GET", "/balance")[1])
