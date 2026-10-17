using System;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 동굴 어둠 (#112, GDD 9-7 · Figma '필드 — 동굴 어둠 (#111)'): 맵 크기의 작은 어둠 그림 한 장(칸마다 2×2픽셀, 부드럽게 늘림)을
    // 주인공·동료·몬스터 위에 얹는다. 빛 계산은 CaveLight·DarknessMap(규칙), 여기서는 칠하기만.
    // 폰 부담을 줄이려고 주인공 빛이 1/4칸 넘게 움직였을 때만 다시 칠하고, 늘 켜진 빛(반딧돌·쉼터·지나간 곳)은 Refresh 때만 다시 계산
    public class FieldDarkness
    {
        private const int PixelsPerCell = 2;
        private const float Alpha = 1f;                          // 처음 가는 곳은 캄캄 — Linear 색공간이라 0.97만 돼도 바닥이 비쳐 보임 (#112 캡처)
        private static readonly Color32 Dark = new Color32(6, 6, 12, 255);
        private const float MoveStep = 0.25f;                     // 이만큼 움직이면 다시 칠함 (칸)

        private readonly Transform parent;
        private GameObject layer;
        private Texture2D texture;
        private Color32[] pixels;
        private DarknessMap darkness;
        private FieldMap map;
        private Func<Vector2Int, bool> isStoneLit, explored;
        private Func<bool> allLit;
        private Vector2Int? growing;                             // 막 밝힌 반딧돌 — 빛이 퍼져 나가는 중 (#113)
        private float growRadius;
        private LightSource hero;
        private Vector2Int drawnStep = new Vector2Int(int.MinValue, int.MinValue);
        private float drawnRadius = -1f;

        public bool IsShowing => layer != null;          // 테스트용
        public bool IsGrowing => growing.HasValue;       // 테스트용
        public int Repaints { get; private set; }        // 어둠을 다시 칠한 횟수 (테스트용)

        public FieldDarkness(Transform parent) => this.parent = parent;

        // 어두운 지역인지 — 지금은 동굴 테마만
        public static bool IsDark(FieldTheme theme) => theme == FieldTheme.Cave;

        // everyStoneLit: 반딧돌을 모두 밝혔는지 (그러면 동굴 전체가 은은하게 — CaveLight.AllLitGlow)
        public void Build(FieldMap fieldMap, FieldTheme theme, Func<Vector2Int, bool> stoneLit, Func<Vector2Int, bool> exploredCell, Func<bool> everyStoneLit = null)
        {
            Clear();
            if (!IsDark(theme)) return;
            map = fieldMap;
            isStoneLit = stoneLit;
            explored = exploredCell;
            allLit = everyStoneLit;
            darkness = new DarknessMap(map.Width, map.Height, PixelsPerCell);
            pixels = new Color32[darkness.Width * darkness.Height];
            texture = new Texture2D(darkness.Width, darkness.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, // 칸 2×2픽셀을 부드럽게 늘려 빛 가장자리가 둥글게
                wrapMode = TextureWrapMode.Clamp,
                name = "CaveDarkness"
            };
            var sprite = Sprite.Create(texture, new Rect(0, 0, darkness.Width, darkness.Height), Vector2.zero, PixelsPerCell, 0, SpriteMeshType.FullRect);
            layer = new GameObject("Darkness", typeof(SpriteRenderer));
            layer.transform.SetParent(parent, false);
            layer.transform.position = Vector3.zero; // 칸 (0,0)의 왼쪽 아래 = 월드 (0,0)
            var renderer = layer.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 14; // 주인공(10)·동료(9·11)·몬스터(9) 위 — 어둠 속 몬스터 눈(15)과 '!'(16)만 그 위
            Refresh();
        }

        // 늘 켜진 빛을 다시 계산 (반딧돌을 밝혔을 때, 새 칸을 지나갔을 때)
        public void Refresh()
        {
            if (darkness == null) return;
            var lights = CaveLight.StaticLights(map, cell => isStoneLit != null && isStoneLit(cell) && cell != growing);
            if (growing.HasValue) lights.Add(new LightSource(new Vector2(growing.Value.x + 0.5f, growing.Value.y + 0.5f), growRadius));
            darkness.SetStatic(lights, explored, allLit != null && allLit() ? CaveLight.AllLitGlow : 0f);
            Repaint();
        }

        // 막 밝힌 반딧돌: 빛이 0칸에서 6칸까지 퍼져 나간다 (Tick이 키움)
        public void Grow(Vector2Int stone)
        {
            if (darkness == null) return;
            growing = stone;
            growRadius = 0.5f;
            Refresh();
        }

        // 매 프레임: 주인공 빛이 1/4칸 넘게 움직였거나 횃불(동료)이 바뀌었을 때만 다시 칠함
        public void Tick(Vector3 heroWorld, bool withCompanion)
        {
            if (darkness == null) return;
            hero = CaveLight.Hero(heroWorld, withCompanion);
            if (growing.HasValue)
            {
                growRadius += Time.unscaledDeltaTime * CaveLight.StoneRadius / CaveLight.StoneGrowSeconds;
                if (growRadius >= CaveLight.StoneRadius) growing = null;
                Refresh();
                return;
            }
            var step = new Vector2Int(Mathf.RoundToInt(heroWorld.x / MoveStep), Mathf.RoundToInt(heroWorld.y / MoveStep));
            if (step == drawnStep && Mathf.Approximately(hero.Radius, drawnRadius)) return;
            Repaint();
        }

        // 그 자리의 밝기 0~1 (어둠 속 몬스터는 눈만 보이게 — FieldRoamerViews)
        public float Brightness(Vector3 world) => darkness != null ? darkness.Brightness(world, hero) : 1f;

        private void Repaint()
        {
            drawnStep = new Vector2Int(Mathf.RoundToInt(hero.Center.x / MoveStep), Mathf.RoundToInt(hero.Center.y / MoveStep));
            drawnRadius = hero.Radius;
            darkness.Paint(hero, pixels, Dark, Alpha);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Repaints++;
        }

        public void Clear()
        {
            if (layer != null) UnityEngine.Object.Destroy(layer);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            layer = null;
            texture = null;
            pixels = null;
            darkness = null;
            map = null;
            growing = null;
            drawnStep = new Vector2Int(int.MinValue, int.MinValue);
        }
    }
}
