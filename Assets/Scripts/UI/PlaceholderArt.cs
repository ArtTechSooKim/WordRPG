using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 아트가 들어오기 전까지 쓰는 16x16 도트 스프라이트를 코드로 만든다.
    // 실제 아트로 바꿀 때는 이 클래스 대신 스프라이트 에셋을 쓰면 된다
    public static class PlaceholderArt
    {
        private const int Size = 16;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static readonly Color PathColor = new Color(0.80f, 0.71f, 0.50f);
        private static readonly Color PathDark = new Color(0.72f, 0.62f, 0.42f);
        private static readonly Color GrassBase = new Color(0.18f, 0.48f, 0.22f);
        private static readonly Color GrassBlade = new Color(0.36f, 0.72f, 0.33f);
        private static readonly Color Ground = new Color(0.33f, 0.62f, 0.32f);
        private static readonly Color Canopy = new Color(0.10f, 0.36f, 0.17f);
        private static readonly Color CanopyLight = new Color(0.18f, 0.48f, 0.24f);
        private static readonly Color Trunk = new Color(0.42f, 0.27f, 0.14f);
        private static readonly Color WaterColor = new Color(0.20f, 0.45f, 0.85f);
        private static readonly Color WaterWave = new Color(0.55f, 0.75f, 1f);

        public static readonly Color OutsideMap = new Color(0.10f, 0.22f, 0.12f);

        public static Color OutsideColor(FieldTheme theme)
        {
            switch (theme)
            {
                case FieldTheme.Library: return new Color(0.12f, 0.08f, 0.06f);
                case FieldTheme.Forest: return new Color(0.05f, 0.14f, 0.08f);
                default: return OutsideMap;
            }
        }

        // opened: 보물상자는 연 상태, 보스는 쓰러뜨린 뒤 모습
        public static Sprite ForTile(FieldTile tile, FieldTheme theme = FieldTheme.Meadow, bool opened = false)
        {
            bool library = theme == FieldTheme.Library;
            bool forest = theme == FieldTheme.Forest;
            Func<int, int, Color> ground = library ? Planks : forest ? Dirt : (Func<int, int, Color>)Path;
            string t = theme + "_";
            switch (tile)
            {
                case FieldTile.Grass: return library ? Make(t + "grass", Pages) : forest ? Make(t + "grass", Fern) : Make(t + "grass", Grass);
                case FieldTile.Lawn: return library ? Make(t + "lawn", Carpet) : forest ? Make(t + "lawn", Moss) : Make(t + "lawn", Lawn);
                case FieldTile.Wall: return library ? Make(t + "wall", Bookshelf) : forest ? Make(t + "wall", DeepTree) : Make(t + "wall", Tree);
                case FieldTile.Water: return library ? Make(t + "water", InkPool) : forest ? Make(t + "water", Swamp) : Make(t + "water", Water);
                case FieldTile.Door: return Make(t + "door", (x, y) => DoorPixel(x, y, library, forest));
                case FieldTile.Fountain: return Make(t + "fountain", Layer(Fountain, ground));
                case FieldTile.Chest:
                    return opened ? Make(t + "chest_open", Layer(ChestOpen, ground)) : Make(t + "chest", Layer(ChestClosed, ground));
                case FieldTile.Altar: return Make(t + "altar", Layer(Altar, ground));
                case FieldTile.Shop: return Make(t + "shop", Layer(ShopStall, ground));
                case FieldTile.Boss:
                    return opened ? Make(t + "boss_cleared", ground) : Make(t + "boss", Layer(BossPixel, ground)); // 사전은 받침대로 옮겨짐
                case FieldTile.Lectern:
                    return opened ? Make(t + "lectern_book", Layer(OpenBook, Layer(Pedestal, ground))) : Make(t + "lectern", Layer(Pedestal, ground));
                default: return Make(t + "floor", ground);
            }
        }

        // 잠긴 출입구: 출입구 위를 엉킨 가시덤불이 막고 있다
        public static Sprite LockedDoor(FieldTheme theme)
        {
            var door = ForTile(FieldTile.Door, theme).texture;
            return Make(theme + "_door_locked", Layer(Brambles, (x, y) => door.GetPixel(x, y)));
        }

        private static Func<int, int, Color> Layer(Func<int, int, Color> top, Func<int, int, Color> bottom) =>
            (x, y) =>
            {
                var c = top(x, y);
                return c.a > 0f ? c : bottom(x, y);
            };

        public static Sprite Player(Direction facing) => Make("player_" + facing, (x, y) => PlayerPixel(x, y, facing));

        // ------------------------------------------------------------ 픽셀 규칙

        private static Color Path(int x, int y) => Noise(x, y, 7) < 0.12f ? PathDark : PathColor;

        private static Color Grass(int x, int y)
        {
            // 4x4 칸마다 '^' 모양 풀잎
            int lx = x % 4, ly = y % 4;
            bool shift = (y / 4) % 2 == 1;
            int bladeX = shift ? 0 : 2;
            if (lx == bladeX && ly < 3) return GrassBlade;
            if (Math.Abs(lx - bladeX) == 1 && ly == 1) return GrassBlade;
            return GrassBase;
        }

        private static Color Tree(int x, int y)
        {
            if (x >= 6 && x <= 9 && y <= 4) return Trunk;
            float dx = x - 7.5f, dy = y - 9f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d <= 6.5f) return dx < -1f && dy > 1f ? CanopyLight : Canopy;
            return Ground;
        }

        private static Color Water(int x, int y) => (x + y * 3) % 8 == 0 && y % 4 == 1 ? WaterWave : WaterColor;

        // 짧은 잔디 (조우 없음)
        private static Color Lawn(int x, int y) => Noise(x, y, 5) < 0.06f ? GrassBlade : Ground;

        private static Color Brambles(int x, int y)
        {
            if (x < 1 || x > 14 || y > 13) return Color.clear;
            bool vine = (x + y) % 5 == 0 || (x - y + 16) % 6 == 0;
            if (vine) return new Color(0.35f, 0.2f, 0.1f);
            return (x * 3 + y) % 7 == 0 ? new Color(0.75f, 0.2f, 0.15f) : Color.clear; // 가시 끝
        }

        // ------------------------------------------------------------ 숲 테마

        private static readonly Color MossColor = new Color(0.24f, 0.45f, 0.22f);

        private static Color Dirt(int x, int y) =>
            Noise(x, y, 11) < 0.15f ? new Color(0.45f, 0.3f, 0.18f) : new Color(0.55f, 0.38f, 0.24f);

        private static Color Moss(int x, int y) => Noise(x, y, 13) < 0.08f ? new Color(0.3f, 0.55f, 0.27f) : MossColor;

        // 고사리 — 조우 칸
        private static Color Fern(int x, int y)
        {
            int dx = Math.Abs(x - 7);
            if (y >= 2 && y <= 13 && (dx == 0 || y > 4 && dx == (13 - y) / 2)) return new Color(0.12f, 0.35f, 0.14f);
            if (y >= 3 && y <= 12 && dx <= (13 - y) / 2 + 1 && (x + y) % 2 == 0) return new Color(0.35f, 0.62f, 0.28f);
            return MossColor;
        }

        private static Color DeepTree(int x, int y)
        {
            float dx = x - 7.5f, dy = y - 8f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d <= 7.2f) return dx < -1f && dy > 1f ? new Color(0.14f, 0.36f, 0.18f) : new Color(0.07f, 0.24f, 0.11f);
            return new Color(0.12f, 0.28f, 0.14f);
        }

        private static Color Swamp(int x, int y) =>
            (x * 5 + y * 3) % 11 == 0 ? new Color(0.45f, 0.7f, 0.55f) : new Color(0.16f, 0.42f, 0.36f);

        // 서고 열람실 카펫 (조우 없음)
        private static Color Carpet(int x, int y)
        {
            if (x == 0 || y == 0) return new Color(0.32f, 0.42f, 0.32f);
            return (x + y) % 4 == 0 ? new Color(0.42f, 0.52f, 0.4f) : new Color(0.46f, 0.56f, 0.44f);
        }

        private static Color Fountain(int x, int y)
        {
            float d = Distance(x, y);
            if (d <= 1.8f) return Color.white;
            if (d <= 5f) return new Color(0.3f, 0.75f, 0.95f);
            if (d <= 7f) return new Color(0.62f, 0.62f, 0.68f);
            return Color.clear;
        }

        private static Color ChestClosed(int x, int y)
        {
            if (x < 2 || x > 13 || y < 2 || y > 11) return Color.clear;
            if (x == 2 || x == 13 || y == 2 || y == 11) return new Color(0.32f, 0.18f, 0.08f);
            if (x >= 7 && x <= 8 && y >= 5 && y <= 8) return new Color(1f, 0.85f, 0.3f);
            if (y == 7) return new Color(0.95f, 0.75f, 0.25f);
            return new Color(0.58f, 0.35f, 0.15f);
        }

        private static Color ChestOpen(int x, int y)
        {
            if (x < 2 || x > 13 || y < 2 || y > 11) return Color.clear;
            if (x == 2 || x == 13 || y == 2 || y == 11) return new Color(0.25f, 0.15f, 0.07f);
            if (y >= 7) return new Color(0.12f, 0.08f, 0.04f);
            return new Color(0.42f, 0.26f, 0.12f);
        }

        // 성유물 제단: 돌 받침 위에 떠 있는 보라색 수정
        private static Color Altar(int x, int y)
        {
            if (y >= 1 && y <= 5 && x >= 3 && x <= 12) return y == 5 || x == 3 || x == 12 ? new Color(0.45f, 0.45f, 0.52f) : new Color(0.62f, 0.62f, 0.7f);
            int dx = Math.Abs(x * 2 - 15), dy = Math.Abs(y * 2 - 21);
            if (dx + dy <= 8) return dx + dy <= 3 ? new Color(0.95f, 0.8f, 1f) : new Color(0.62f, 0.3f, 0.9f);
            return Color.clear;
        }

        // 상점: 빨강·하양 줄무늬 차양 + 나무 진열대 + 금화
        private static Color ShopStall(int x, int y)
        {
            if (y >= 11 && y <= 14 && x >= 1 && x <= 14) return (x / 2) % 2 == 0 ? new Color(0.85f, 0.2f, 0.2f) : Color.white;
            if (y >= 2 && y <= 7 && x >= 2 && x <= 13)
            {
                if (x >= 6 && x <= 9 && y >= 4 && y <= 6) return new Color(1f, 0.85f, 0.25f);
                return y == 7 ? new Color(0.4f, 0.25f, 0.1f) : new Color(0.6f, 0.4f, 0.2f);
            }
            if ((x == 2 || x == 13) && y >= 8 && y <= 10) return new Color(0.4f, 0.25f, 0.1f);
            return Color.clear;
        }

        // ------------------------------------------------------------ 서고(던전) 테마

        private static Color Planks(int x, int y)
        {
            if (y % 4 == 0) return new Color(0.34f, 0.22f, 0.12f);
            if ((x + (y / 4) * 5) % 8 == 0) return new Color(0.38f, 0.25f, 0.14f);
            return Noise(x, y, 3) < 0.08f ? new Color(0.41f, 0.27f, 0.16f) : new Color(0.46f, 0.31f, 0.19f);
        }

        // 흩어진 책장(종이) — 조우 칸
        private static Color Pages(int x, int y)
        {
            bool sheetA = x >= 1 && x <= 6 && y >= 8 && y <= 13;
            bool sheetB = x >= 8 && x <= 14 && y >= 2 && y <= 7;
            bool sheetC = x >= 9 && x <= 13 && y >= 10 && y <= 14;
            if (sheetA || sheetB || sheetC)
            {
                bool line = y % 2 == 0 && x % 6 != 0;
                return line ? new Color(0.62f, 0.6f, 0.68f) : new Color(0.93f, 0.9f, 0.8f);
            }
            return Planks(x, y);
        }

        private static readonly Color[] Spines =
        {
            new Color(0.7f, 0.2f, 0.2f), new Color(0.2f, 0.35f, 0.7f), new Color(0.2f, 0.55f, 0.3f),
            new Color(0.8f, 0.65f, 0.2f), new Color(0.5f, 0.25f, 0.6f)
        };

        private static Color Bookshelf(int x, int y)
        {
            var frame = new Color(0.25f, 0.15f, 0.08f);
            if (x == 0 || x == 15 || y <= 1 || y == 8 || y >= 15) return frame;
            int shelf = y < 8 ? 0 : 1;
            int book = (x - 1) / 2;
            var spine = Spines[(book + shelf * 2) % Spines.Length];
            int top = shelf == 0 ? 7 : 14;
            if (y == top && (book % 3 == 1)) return new Color(0.18f, 0.1f, 0.05f); // 키 작은 책
            return (x - 1) % 2 == 1 ? spine * 0.8f : spine;
        }

        private static Color InkPool(int x, int y)
        {
            if ((x * 7 + y * 3) % 13 == 0) return new Color(0.32f, 0.28f, 0.58f);
            return new Color(0.11f, 0.09f, 0.24f);
        }

        // 출입구: 초원은 바위 동굴 입구, 서고는 나무 문틀 — 안쪽이 어둡다. 숲은 햇빛 드는 모래길 (초원으로 나가는 길)
        private static Color DoorPixel(int x, int y, bool library, bool forest)
        {
            if (forest) return y >= 10 ? new Color(0.93f, 0.85f, 0.6f) : PathColor;
            var wall = library ? new Color(0.25f, 0.15f, 0.08f) : new Color(0.45f, 0.45f, 0.5f);
            var rim = library ? new Color(0.55f, 0.38f, 0.2f) : new Color(0.32f, 0.32f, 0.36f);
            float dx = x - 7.5f;
            bool inside = x >= 3 && x <= 12 && (y <= 9 || dx * dx + (y - 9f) * (y - 9f) <= 22f);
            bool edge = x >= 2 && x <= 13 && (y <= 10 || dx * dx + (y - 9f) * (y - 9f) <= 34f);
            if (inside) return y <= 1 ? new Color(0.9f, 0.8f, 0.45f) : new Color(0.05f, 0.04f, 0.06f);
            if (edge) return rim;
            return wall;
        }

        // 보스 까먹대왕: 뿔 달린 청록 도깨비
        private static Color BossPixel(int x, int y)
        {
            if ((x == 4 || x == 11) && y >= 13 && y <= 15) return new Color(1f, 0.92f, 0.6f);
            if ((x == 5 || x == 10) && y == 13) return new Color(1f, 0.92f, 0.6f);
            float dx = x - 7.5f, dy = y - 7f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > 6.8f) return Color.clear;
            if (d > 5.8f) return new Color(0.08f, 0.35f, 0.3f);
            if ((x == 5 || x == 10) && (y == 8 || y == 9)) return new Color(0.95f, 0.15f, 0.15f);
            if (y == 4 && x >= 5 && x <= 10) return new Color(0.1f, 0.1f, 0.12f);
            return new Color(0.2f, 0.72f, 0.62f);
        }

        // 사전 받침대: 돌 받침 (가운데가 빈 홈)
        private static Color Pedestal(int x, int y)
        {
            if (x < 2 || x > 13 || y < 1 || y > 12) return Color.clear;
            bool edge = x == 2 || x == 13 || y == 1 || y == 12;
            if (edge) return new Color(0.2f, 0.22f, 0.22f);
            if (x >= 5 && x <= 10 && y >= 6 && y <= 10) return new Color(0.12f, 0.14f, 0.14f);
            return y <= 3 ? new Color(0.42f, 0.48f, 0.44f) : new Color(0.55f, 0.62f, 0.56f);
        }

        // 사전이 놓인 받침대: 빛나는 펼친 책
        private static Color OpenBook(int x, int y)
        {
            bool page = y >= 4 && y <= 10 && x >= 1 && x <= 14 && x != 7 && x != 8;
            if (page) return y % 2 == 0 && x % 5 != 0 ? new Color(0.7f, 0.68f, 0.75f) : new Color(1f, 0.98f, 0.9f);
            if ((x == 7 || x == 8) && y >= 3 && y <= 10) return new Color(0.55f, 0.35f, 0.2f);
            if (Distance(x, y) <= 7.5f) return new Color(1f, 0.9f, 0.45f, 1f);
            return Color.clear;
        }

        private static Color PlayerPixel(int x, int y, Direction facing)
        {
            float d = Distance(x, y);
            if (d > 6.8f) return Color.clear;
            if (d > 5.6f) return new Color(0.55f, 0.22f, 0.05f);

            // 바라보는 방향에 눈 두 개
            Vector2Int[] eyes;
            switch (facing)
            {
                case Direction.Up: return new Color(0.85f, 0.42f, 0.12f); // 뒷모습
                case Direction.Left: eyes = new[] { new Vector2Int(4, 9), new Vector2Int(7, 9) }; break;
                case Direction.Right: eyes = new[] { new Vector2Int(8, 9), new Vector2Int(11, 9) }; break;
                default: eyes = new[] { new Vector2Int(5, 8), new Vector2Int(10, 8) }; break;
            }
            foreach (var eye in eyes)
            {
                if (x == eye.x && (y == eye.y || y == eye.y + 1)) return new Color(0.1f, 0.1f, 0.15f);
            }
            return new Color(1f, 0.58f, 0.22f);
        }

        // ------------------------------------------------------------ 도우미

        private static float Distance(int x, int y)
        {
            float dx = x - 7.5f, dy = y - 7.5f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // 좌표 기반 고정 노이즈 (매번 같은 무늬)
        private static float Noise(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static Sprite Make(string key, Func<int, int, Color> pixel)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "placeholder_" + key
            };
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel(x, y);
            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
