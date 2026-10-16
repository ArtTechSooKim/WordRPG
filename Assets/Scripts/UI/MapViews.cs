using System;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 미니맵·지도 그림: 글자 맵(FieldMap)을 한 칸 = 한 점으로 그린다 → 새 지역도 따로 손대지 않고 자동으로 나온다.
    // 지형은 은은한 색, 상자·샘·제단·상점·출입구·보스는 밝은 색 (Figma 'Minimap' / '지도')
    public static class MinimapArt
    {
        public static readonly Color32 Chest = new Color32(255, 209, 64, 255);
        public static readonly Color32 Fountain = new Color32(111, 227, 245, 255);
        public static readonly Color32 Altar = new Color32(182, 123, 230, 255);
        public static readonly Color32 Shop = new Color32(229, 83, 75, 255);
        public static readonly Color32 Door = new Color32(255, 255, 255, 255);
        public static readonly Color32 Boss = new Color32(255, 107, 138, 255);
        public static readonly Color32 Lectern = new Color32(240, 150, 70, 255); // 사전 받침대 (#45)
        public static readonly Color32 Gate = new Color32(214, 120, 60, 255);    // 보스 방 문 (#76)
        public static readonly Color32 Key = new Color32(255, 214, 64, 255);     // 열쇠 받침대 (#76)
        public static readonly Color32 Lever = new Color32(255, 140, 40, 255);   // 레버 (#80) — 멀리서도 눈에 띄게
        public static readonly Color32 Tablet = new Color32(240, 240, 232, 255); // 단어 석판
        public static readonly Color32 Shrine = new Color32(255, 128, 200, 255); // 유적 제단 (동료)
        public static readonly Color32 Cave = new Color32(28, 26, 30, 255);      // 동굴 입구 (1.2)
        public static readonly Color32 Dock = new Color32(176, 112, 56, 255);    // 나루터
        public static readonly Color32 Fog = new Color32(20, 22, 34, 255); // 아직 안 가 본 칸

        // done: 연 상자·쓰러뜨린 보스 → 바닥색으로
        public static Color32 ColorOf(FieldTile tile, FieldTheme theme, bool done)
        {
            if (theme == FieldTheme.Jungle) return JungleColorOf(tile, done);
            bool library = theme == FieldTheme.Library;
            bool forest = theme == FieldTheme.Forest;
            var floor = library ? new Color32(74, 63, 69, 255) : forest ? new Color32(150, 100, 64, 255) : new Color32(205, 140, 100, 255); // 초원 길 = 흙길
            switch (tile)
            {
                case FieldTile.Wall: return library ? new Color32(154, 78, 38, 255) : forest ? new Color32(18, 52, 26, 255) : new Color32(36, 80, 42, 255);
                case FieldTile.Grass: return library ? new Color32(110, 98, 115, 255) : forest ? new Color32(70, 128, 58, 255) : new Color32(63, 122, 53, 255);
                case FieldTile.Lawn: return library ? new Color32(88, 104, 86, 255) : forest ? new Color32(48, 92, 44, 255) : new Color32(150, 190, 70, 255);
                case FieldTile.Water: return library ? new Color32(90, 63, 138, 255) : forest ? new Color32(60, 150, 130, 255) : new Color32(79, 182, 224, 255);
                case FieldTile.Chest: return done ? floor : Chest;
                case FieldTile.Boss: return done ? floor : Boss;
                case FieldTile.Fountain: return Fountain;
                case FieldTile.Altar: return Altar;
                case FieldTile.Shop: return Shop;
                case FieldTile.Door: return Door;
                case FieldTile.Lectern: return Lectern;
                case FieldTile.Gate: return done ? floor : Gate;
                case FieldTile.Key: return done ? Lectern : Key; // 가져가면 빈 받침대
                default: return floor;
            }
        }

        // 정글 (#80): 짙은 초록 바탕에 진흙길, 얕은 강은 밝은 하늘색(깊은 강보다 옅게), 유적은 돌빛
        private static Color32 JungleColorOf(FieldTile tile, bool done)
        {
            var floor = new Color32(140, 96, 62, 255);
            var water = new Color32(38, 104, 150, 255);
            var ruinFloor = new Color32(168, 170, 140, 255);
            switch (tile)
            {
                case FieldTile.Wall: return new Color32(14, 58, 30, 255);
                case FieldTile.Grass: return new Color32(82, 150, 52, 255);
                case FieldTile.Lawn: return new Color32(44, 108, 52, 255);
                case FieldTile.Water: return water;
                case FieldTile.Stream: return new Color32(104, 182, 222, 255);
                case FieldTile.Bridge: return done ? new Color32(160, 116, 60, 255) : water; // 놓이기 전엔 물(골짜기)로
                case FieldTile.RuinWall: return new Color32(104, 104, 96, 255);
                case FieldTile.RuinFloor: return ruinFloor;
                case FieldTile.Dock: return Dock;
                case FieldTile.Lever: return Lever;
                case FieldTile.Tablet: return done ? ruinFloor : Tablet;
                case FieldTile.Shrine: return done ? ruinFloor : Shrine;
                case FieldTile.Cave: return Cave;
                case FieldTile.Decor: return new Color32(92, 74, 50, 255); // 꾸밈 (#86) — 막힌 칸, 은은한 갈색
                case FieldTile.Chest: return done ? floor : Chest;
                case FieldTile.Boss: return done ? floor : Boss;
                case FieldTile.Fountain: return Fountain;
                case FieldTile.Altar: return Altar;
                case FieldTile.Shop: return Shop;
                case FieldTile.Door: return Door;
                case FieldTile.Lectern: return Lectern;
                case FieldTile.Gate: return done ? floor : Gate;
                case FieldTile.Key: return done ? Lectern : Key;
                default: return floor;
            }
        }

        // 미니맵에서 '끝난' 모습이 따로 있는 칸 (연 상자·쓰러뜨린 보스·연 문·가져간 열쇠·놓인 다리·푼 석판·깨어난 제단)
        public static bool HasDoneLook(FieldTile tile) =>
            tile == FieldTile.Chest || tile == FieldTile.Boss || tile == FieldTile.Gate || tile == FieldTile.Key
            || tile == FieldTile.Bridge || tile == FieldTile.Tablet || tile == FieldTile.Shrine;

        // explored: 가 본 칸인지 (null = 전부 보임). 안 가 본 칸은 안개색 — 그 안의 상자·보스도 숨는다
        public static Texture2D Build(FieldMap map, FieldTheme theme, Func<Vector2Int, bool> done, Texture2D reuse = null,
            Func<Vector2Int, bool> explored = null)
        {
            var texture = reuse;
            if (texture == null || texture.width != map.Width || texture.height != map.Height)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "minimap"
                };
            }
            var pixels = new Color32[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var tile = map.Get(cell);
                bool isDone = HasDoneLook(tile) && done != null && done(cell);
                bool seen = explored == null || explored(cell);
                pixels[y * map.Width + x] = seen ? ColorOf(tile, theme, isDone) : Fog;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }

    // 지도 그림 + 내 위치 점 (금테 흰 점). 미니맵과 큰 지도가 같이 쓴다.
    // window = 그림에서 보여 줄 칸 범위 (큰 지역의 미니맵은 주인공 둘레만, 큰 지도는 전체)
    public class MapPicture
    {
        public RectTransform Rect { get; private set; }
        private RawImage image;
        private RectTransform player, goal;
        private Vector2Int? goalCell;
        private int width = 1, height = 1;
        private RectInt window = new RectInt(0, 0, 1, 1);

        public static MapPicture Create(Transform parent, float minX, float minY, float maxX, float maxY, float dotSize)
        {
            var picture = new MapPicture();
            picture.Rect = UiKit.Rect("Map", parent, minX, minY, maxX, maxY);
            picture.image = picture.Rect.gameObject.AddComponent<RawImage>();
            picture.image.raycastTarget = false;
            var ring = UiKit.Pill(UiKit.Panel("Player", picture.Rect, Palette.Gold, 0, 0, 0, 0));
            ring.raycastTarget = false;
            ring.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
            var dot = UiKit.Pill(UiKit.Panel("Dot", ring.transform, Color.white));
            dot.raycastTarget = false;
            UiKit.Pad(dot.rectTransform, dotSize * 0.18f);
            picture.player = ring.rectTransform;
            // 목표 칸 (#61 목표 한 줄): 금색 별. 보이는 범위 밖이면 가장자리에 붙어 그쪽을 가리킨다
            var star = UiKit.OneLine(UiKit.Label("Goal", picture.Rect, "★", Mathf.RoundToInt(dotSize * 1.9f), Palette.Gold, 0, 0, 0, 0,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            star.raycastTarget = false;
            star.rectTransform.sizeDelta = new Vector2(dotSize * 2.4f, dotSize * 2.4f);
            var outline = star.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(26, 20, 48, 255);
            outline.effectDistance = new Vector2(2, -2);
            star.gameObject.SetActive(false);
            picture.goal = star.rectTransform;
            ring.transform.SetAsLastSibling(); // 내 위치 점이 별 위에
            return picture;
        }

        public void SetTexture(Texture2D texture)
        {
            image.texture = texture;
            width = Mathf.Max(1, texture.width);
            height = Mathf.Max(1, texture.height);
            SetWindow(new RectInt(0, 0, width, height));
        }

        public void SetWindow(RectInt cells)
        {
            window = cells;
            image.uvRect = new Rect((float)cells.x / width, (float)cells.y / height,
                (float)cells.width / width, (float)cells.height / height);
            PlaceGoal();
        }

        public void SetGoal(Vector2Int? cell)
        {
            goalCell = cell;
            PlaceGoal();
        }

        public bool GoalVisible => goal.gameObject.activeSelf;

        private void PlaceGoal()
        {
            goal.gameObject.SetActive(goalCell.HasValue);
            if (!goalCell.HasValue) return;
            var cell = goalCell.Value;
            var anchor = new Vector2(Mathf.Clamp01((cell.x - window.x + 0.5f) / window.width),
                Mathf.Clamp01((cell.y - window.y + 0.5f) / window.height));
            goal.anchorMin = goal.anchorMax = anchor;
            goal.anchoredPosition = Vector2.zero;
        }

        public void SetPlayer(Vector2Int cell)
        {
            var anchor = new Vector2((cell.x - window.x + 0.5f) / window.width, (cell.y - window.y + 0.5f) / window.height);
            player.anchorMin = player.anchorMax = anchor;
            player.anchoredPosition = Vector2.zero;
        }

        public Vector2 PlayerAnchor => player.anchorMin;
        public RectInt Window => window;
    }

    // 필드 왼쪽 위 미니맵 (누르면 큰 지도). 한 칸 = 8px. 작은 지역은 맵 전체,
    // 가로 26칸·세로 30칸보다 큰 지역은 주인공 둘레만 보여 주고 걸을 때마다 따라 움직인다
    public class MinimapView
    {
        public const float CellPixels = 8f;
        public const int MaxCellsAcross = 26;
        public const int MaxCellsHigh = 30;
        public Button Button { get; private set; }
        public MapPicture Picture { get; private set; }

        private RectTransform root;
        private Texture2D texture;
        private FieldMap map;
        private FieldTheme theme;
        private Func<Vector2Int, bool> done;
        private Func<Vector2Int, bool> explored;
        private Vector2Int viewSize = Vector2Int.one;
        private Vector2Int playerCell;

        public float Height => root.sizeDelta.y;

        public void SetGoal(Vector2Int? cell) => Picture.SetGoal(cell);

        public static MinimapView Create(Transform parent)
        {
            var view = new MinimapView();
            var panel = UiKit.RoundPanel("Minimap", parent, Palette.Scrim, UiKit.RadiusMd, 0, 0.865f, 0, 0.865f);
            view.root = panel.rectTransform;
            view.root.pivot = new Vector2(0, 1);
            view.root.anchoredPosition = new Vector2(24, 0);
            view.Button = UiKit.AddButton(panel);
            view.Picture = MapPicture.Create(panel.transform, 0, 0, 1, 1, 14);
            UiKit.Pad(view.Picture.Rect, 12);
            return view;
        }

        public void SetArea(FieldMap fieldMap, FieldTheme fieldTheme, Func<Vector2Int, bool> isDone,
            Func<Vector2Int, bool> isExplored = null)
        {
            map = fieldMap;
            theme = fieldTheme;
            done = isDone;
            explored = isExplored;
            viewSize = new Vector2Int(Mathf.Min(map.Width, MaxCellsAcross), Mathf.Min(map.Height, MaxCellsHigh));
            root.sizeDelta = new Vector2(viewSize.x * CellPixels + 24, viewSize.y * CellPixels + 24);
            Redraw();
        }

        // 걸음마다: 내 위치 점 + (큰 지역이면) 보이는 범위를 주인공 둘레로
        public void SetPlayer(Vector2Int cell)
        {
            playerCell = cell;
            if (map == null) return;
            Picture.SetWindow(Window(map.Width, map.Height, viewSize.x, viewSize.y, cell));
            Picture.SetPlayer(cell);
        }

        // 맵에서 view 크기만큼, center가 가운데 오게 (맵 끝에서는 맵 안으로 붙인다)
        public static RectInt Window(int mapWidth, int mapHeight, int viewWidth, int viewHeight, Vector2Int center)
        {
            viewWidth = Mathf.Min(viewWidth, mapWidth);
            viewHeight = Mathf.Min(viewHeight, mapHeight);
            int x = Mathf.Clamp(center.x - viewWidth / 2, 0, mapWidth - viewWidth);
            int y = Mathf.Clamp(center.y - viewHeight / 2, 0, mapHeight - viewHeight);
            return new RectInt(x, y, viewWidth, viewHeight);
        }

        // 상자를 열거나 보스를 쓰러뜨렸을 때, 새로 탐험했을 때
        public void Redraw()
        {
            if (map == null) return;
            texture = MinimapArt.Build(map, theme, done, texture, explored);
            Picture.SetTexture(texture);
            SetPlayer(playerCell);
        }

        public Texture2D Texture => texture;
    }

    // 큰 지도 (Figma '지도'): 지역 이름, 지도, 범례, 남은 보물상자
    public class MapView
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        private Text title, info;
        private MapPicture picture;
        private RectTransform frame;

        public static MapView Create(Transform parent)
        {
            var view = new MapView();
            var root = UiKit.Stretch("MapView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background;

            view.title = UiKit.Display(UiKit.Label("Title", root, "지도", 56, Palette.Gold, 0, 0.93f, 1, 0.99f));
            var panel = UiKit.RoundPanel("MapPanel", root, Palette.Panel, UiKit.RadiusLg, 0.5f, 0.6f, 0.5f, 0.6f);
            panel.raycastTarget = false;
            view.frame = panel.rectTransform;
            view.picture = MapPicture.Create(panel.transform, 0, 0, 1, 1, 36);
            UiKit.Pad(view.picture.Rect, 24);

            // 범례 두 줄
            var legend = new (string name, Color color)[]
            {
                ("나", Palette.Gold), ("보물상자", MinimapArt.Chest), ("회복의 샘", MinimapArt.Fountain), ("성유물 제단", MinimapArt.Altar),
                ("상점", MinimapArt.Shop), ("출입구", MinimapArt.Door), ("보스", MinimapArt.Boss), ("풀숲 (몬스터)", new Color32(63, 122, 53, 255)),
                ("사전", MinimapArt.Lectern), ("아직 안 가 본 곳", MinimapArt.Fog),
            };
            int[] rowStart = { 0, 5, legend.Length };
            for (int row = 0; row < 2; row++)
            {
                var line = UiKit.Rect($"Legend_{row}", root, 0.03f, 0.205f - row * 0.04f, 0.97f, 0.24f - row * 0.04f);
                var layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.spacing = 16;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                for (int i = rowStart[row]; i < rowStart[row + 1]; i++)
                {
                    var chip = UiKit.Pill(UiKit.Panel($"Chip_{i}", line, Palette.Panel));
                    chip.raycastTarget = false;
                    var chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                    chipLayout.padding = new RectOffset(16, 20, 8, 8);
                    chipLayout.spacing = 10;
                    chipLayout.childAlignment = TextAnchor.MiddleCenter;
                    chipLayout.childControlWidth = chipLayout.childControlHeight = true;
                    chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
                    var swatch = UiKit.Round(UiKit.Panel("Swatch", chip.transform, legend[i].color), UiKit.RadiusSm);
                    swatch.raycastTarget = false;
                    var size = swatch.gameObject.AddComponent<LayoutElement>();
                    size.preferredWidth = size.preferredHeight = 28;
                    var label = UiKit.Label("Name", chip.transform, legend[i].name, 28, Palette.Text, 0, 0, 1, 1);
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
            }
            view.info = UiKit.Label("Info", root, "", 34, Palette.TextDim, 0.03f, 0.11f, 0.97f, 0.155f);

            var close = UiKit.MakeButton("MapCloseButton", root, "닫기", Palette.Neutral, 44, 0.05f, 0.015f, 0.95f, 0.095f);
            close.onClick.AddListener(view.Hide);
            view.Root.SetActive(false);
            return view;
        }

        public void Show(FieldArea area, Texture2D texture, Vector2Int playerCell, GameSession session, Vector2Int? goal = null)
        {
            title.text = $"지도 · {area.DisplayName}";
            picture.SetTexture(texture);
            picture.SetPlayer(playerCell);
            picture.SetGoal(goal);
            // 지도는 가로 936·세로 1000 안에 꽉 차게, 한 칸은 정수 px로
            float cell = Mathf.Floor(Mathf.Min(936f / texture.width, 1000f / texture.height));
            frame.sizeDelta = new Vector2(texture.width * cell + 48, texture.height * cell + 48);

            var (seen, total) = session.ExplorationProgress(area);
            string explore = $"탐험 {(total > 0 ? seen * 100 / total : 100)}%   ·   ";
            int chests = session.RemainingChests(area);
            string boss = area.Boss != null
                ? (session.World.IsBossDefeated(area.BossId) ? "   ·   보스 쓰러뜨림 ★" : $"   ·   보스 {area.Boss.Species.DisplayName}")
                : "";
            info.text = explore + (chests > 0 ? $"남은 보물상자 {chests}개" : "보물상자를 모두 열었어요") + boss;
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide() => Root.SetActive(false);
    }
}
