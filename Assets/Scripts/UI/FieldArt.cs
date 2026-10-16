using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 필드 타일 그림 (Ninja Adventure 팩에서 Tools/import_ninja_art.py 가 만든 Art/NinjaAdventure/Tiles/{테마}_{종류}[_done]).
    // 길·물은 이웃 모양에 맞춘 자동 테두리 조각({테마}_{종류}_auto)이 있으면 그것을 쓴다 (AutoTile).
    // 한 장 = 한 칸 (보스처럼 큰 그림은 48px이어도 한 칸에 맞춘다). 파일이 없으면 코드로 그린 임시 도트
    public static class FieldArt
    {
        private const string Folder = "Art/NinjaAdventure/Tiles/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // done: 연 보물상자, 쓰러뜨린 보스 자리(빈자리), 사전이 놓인 받침대, 연 보스 방 문(길), 열쇠를 가져간 빈 받침대,
        //       당긴 레버, 놓인 덩굴 다리, 푼 석판, 깨어난 유적 제단(빈 받침) (#80)
        public static Sprite ForTile(FieldTile tile, FieldTheme theme, bool done)
        {
            bool hasDone = done && (tile == FieldTile.Chest || tile == FieldTile.Boss || tile == FieldTile.Lectern
                                    || tile == FieldTile.Gate || tile == FieldTile.Key || tile == FieldTile.Lever
                                    || tile == FieldTile.Bridge || tile == FieldTile.Tablet || tile == FieldTile.Shrine);
            return Load($"{theme}_{tile}{(hasDone ? "_done" : "")}") ?? PlaceholderArt.ForTile(tile, theme, done);
        }

        // 칸 그림 변형 (#98): Tiles/{테마}_{종류}_v1, _v2 … 가 있으면 그 수 (없으면 0 → 한 그림). 고르는 규칙은 FieldDecor.TileVariant
        public static int VariantCount(FieldTile tile, FieldTheme theme)
        {
            string key = $"{theme}_{tile}";
            if (VariantCounts.TryGetValue(key, out int count)) return count;
            while (count < 8 && Load($"{key}_v{count + 1}") != null) count++;
            return VariantCounts[key] = count;
        }

        public static Sprite ForTileVariant(FieldTile tile, FieldTheme theme, int variant) =>
            variant <= 0 ? ForTile(tile, theme, false) : Load($"{theme}_{tile}_v{variant}") ?? ForTile(tile, theme, false);

        private static readonly Dictionary<string, int> VariantCounts = new Dictionary<string, int>();

        // 덩굴 다리 (#80): 놓기 전엔 물 위 희미한 판자 윤곽(무엇이 생기는지 보이게), 놓으면 덩굴 난간 판자 다리. 가로로 건너면 _h 그림
        public static Sprite ForBridge(FieldTheme theme, bool built, bool horizontal) =>
            Load($"{theme}_Bridge{(horizontal ? "_h" : "")}{(built ? "_done" : "")}") ?? ForTile(FieldTile.Bridge, theme, built);

        // 꾸밈 칸 (#86): 종류별 그림 Tiles/{테마}_Decor_{종류}.png (없으면 {테마}_Decor)
        public static Sprite ForDecor(FieldTheme theme, string variant) =>
            Load($"{theme}_Decor_{variant}") ?? ForTile(FieldTile.Decor, theme, false);

        // 꾸밈 중 칸보다 큰 것(이끼 기둥·석상 1×2): Tiles/{테마}_Decor_{종류}_prop.png
        public static Sprite DecorProp(FieldTheme theme, string variant) => FieldDecor.IsTall(variant) ? LoadProp($"{theme}_Decor_{variant}_prop") : null;

        // 흩뿌림 그림 (#86): Tiles/{테마}_Scatter_{바닥}_0, _1 … (없으면 빈 배열)
        public static Sprite[] Scatter(FieldTheme theme, FieldTile ground)
        {
            string key = $"{theme}_Scatter_{ground}";
            if (ScatterCache.TryGetValue(key, out var cached)) return cached;
            var list = new List<Sprite>();
            for (int i = 0; i < 16; i++)
            {
                var sprite = Load($"{key}_{i}");
                if (sprite == null) break;
                list.Add(sprite);
            }
            return ScatterCache[key] = list.ToArray();
        }

        private static readonly Dictionary<string, Sprite[]> ScatterCache = new Dictionary<string, Sprite[]>();

        // 바닥 안개 띠 (#102): Tiles/{테마}_Fog_Wide_0… (넓은 것, 64×20) · _Fog_Small_0… (작은 것). 칸보다 커서 16px = 1칸, 가운데 기준.
        // 없으면 빈 배열 → 안개 없음
        public static Sprite[] Fog(FieldTheme theme, bool wide)
        {
            string key = $"{theme}_Fog_{(wide ? "Wide" : "Small")}";
            if (ScatterCache.TryGetValue(key, out var cached)) return cached;
            var list = new List<Sprite>();
            for (int i = 0; i < 16; i++)
            {
                var texture = Resources.Load<Texture2D>($"{Folder}{key}_{i}");
                if (texture == null) break;
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
                sprite.name = $"{key}_{i}";
                list.Add(sprite);
            }
            return ScatterCache[key] = list.ToArray();
        }

        // 칸보다 큰 그림 (#80): 유적 제단의 잠든 석상(2×2), 동굴 입구(3×3) — Tiles/{테마}_{종류}_prop.png, 16px = 1칸.
        // 바닥 칸 위에 아래 가운데를 맞춰 따로 그린다. 끝난 칸(동료가 깨어난 제단)은 없음
        public static Sprite Prop(FieldTile tile, FieldTheme theme, bool done)
        {
            if (done || (tile != FieldTile.Shrine && tile != FieldTile.Cave)) return null;
            return LoadProp($"{theme}_{tile}_prop");
        }

        // 큰 그림: 16px = 1칸, 아래 가운데 기준 (칸 아래쪽에 맞춰 위로 그림)
        private static Sprite LoadProp(string key)
        {
            if (PropCache.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(Folder + key);
            Sprite sprite = null;
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), 16, 0, SpriteMeshType.FullRect);
                sprite.name = key;
            }
            PropCache[key] = sprite;
            return sprite;
        }

        private static readonly Dictionary<string, Sprite> PropCache = new Dictionary<string, Sprite>();

        // 출입구: 잠겼으면 {테마}_Door_locked(막힌 길), 아니면 도착 지역 테마 전용 그림 {테마}_Door_{도착 테마}
        // (예: 초원 → 숲은 그늘진 숲길 입구)이 있으면 그것, 없으면 {테마}_Door
        public static Sprite ForDoor(FieldTheme theme, FieldTheme? target, bool locked)
        {
            if (locked) return Load($"{theme}_Door_locked") ?? PlaceholderArt.LockedDoor(theme);
            if (target.HasValue && target.Value != theme)
            {
                var special = Load($"{theme}_Door_{target.Value}");
                if (special != null) return special;
            }
            return ForTile(FieldTile.Door, theme, false);
        }

        // 자동 테두리 조각 (Tiles/{테마}_{종류}_auto.png: 16×16칸, 칸 번호 = FieldAutotile 마스크). 아틀라스가 없으면 null
        public static Sprite AutoTile(FieldTile tile, FieldTheme theme, int mask)
        {
            string key = $"{theme}_{tile}_auto";
            if (!Atlases.TryGetValue(key, out var sprites))
            {
                var texture = Resources.Load<Texture2D>(Folder + key);
                sprites = texture != null && texture.width >= 256 && texture.height >= 256 ? new Sprite[256] : null;
                if (sprites != null) AtlasTextures[key] = texture;
                Atlases[key] = sprites;
            }
            if (sprites == null || mask < 0 || mask > 255) return null;
            if (sprites[mask] == null)
            {
                var texture = AtlasTextures[key];
                // PNG 위쪽 줄이 텍스처에서는 아래(y=0이 맨 아래)
                var rect = new Rect((mask % 16) * 16, (15 - mask / 16) * 16, 16, 16);
                sprites[mask] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
                sprites[mask].name = $"{key}_{mask}";
            }
            return sprites[mask];
        }

        // 달리기 흙먼지 색 (#53 사용자 요청: 밟는 바닥에 따라). 먼지 그림(Fx.Dust)은 회색조라 이 색을 곱해 쓴다
        // (밝은 알갱이 = 이 색, 어두운 알갱이 = 이 색의 약 0.5·0.7배). 바닥 타일 색보다 꽤 밝게 잡아야 밝은·어두운 알갱이가 둘 다 보인다.
        // 바닥 색은 Tiles/{테마}_{종류}.png에서 뽑음: 초원 흙길 d3865f · 풀숲 74a334 · 잔디 adbc3a, 서고 돌바닥 4e484a · 카펫 8d977f,
        // 숲 흙길 a3754e · 풀 74a334. 걸을 수 없는 칸이나 처음 보는 바닥은 그 테마의 길 색
        public static Color DustColor(FieldTheme theme, FieldTile tile)
        {
            switch (theme)
            {
                case FieldTheme.Library:
                    switch (tile)
                    {
                        case FieldTile.Grass: return Rgb(0xD2DCE8); // 바닥 안개 — 옅은 푸른 물보라 (#101)
                        case FieldTile.Lawn: return Rgb(0xDCE2D2);  // 카펫·이끼
                        default: return Rgb(0xC9C0BC);              // 돌바닥 (보스 빈자리도 돌바닥)
                    }
                case FieldTheme.Forest:
                    switch (tile)
                    {
                        case FieldTile.Grass:
                        case FieldTile.Lawn:
                        case FieldTile.Boss: return Rgb(0xD2EBA0);  // 숲 풀 (보스 빈자리는 풀밭)
                        default: return Rgb(0xE3CDB8);              // 숲 흙길
                    }
                case FieldTheme.Jungle: // 정글 (#80): 진흙길 · 짙은 풀 · 이끼 땅 · 얕은 강은 물보라 · 유적은 돌가루
                    switch (tile)
                    {
                        case FieldTile.Grass: return Rgb(0xC8E89A);
                        case FieldTile.Lawn:
                        case FieldTile.Boss: return Rgb(0xB5DC8C);
                        case FieldTile.Stream: return Rgb(0xD6F2FF);
                        case FieldTile.RuinFloor:
                        case FieldTile.Bridge: return Rgb(0xDDE0CC);
                        default: return Rgb(0xD9B79A);
                    }
                default:
                    switch (tile)
                    {
                        case FieldTile.Grass: return Rgb(0xD8EC9A);
                        case FieldTile.Lawn:
                        case FieldTile.Boss: return Rgb(0xEEF5B8);  // 잔디 (보스 빈자리는 잔디)
                        default: return Rgb(0xF7DCC8);              // 초원 흙길
                    }
            }
        }

        private static Color Rgb(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);

        private static readonly Dictionary<string, Sprite[]> Atlases = new Dictionary<string, Sprite[]>();
        private static readonly Dictionary<string, Texture2D> AtlasTextures = new Dictionary<string, Texture2D>();

        private static Sprite Load(string key)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(Folder + key);
            Sprite sprite = null;
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                    texture.width, 0, SpriteMeshType.FullRect);
                sprite.name = key;
            }
            Cache[key] = sprite;
            return sprite;
        }
    }
}
