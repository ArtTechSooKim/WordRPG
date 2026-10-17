using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Field
{
    // 빛 하나: 가운데(월드 좌표 — 칸 (x,y)의 가운데는 x+0.5, y+0.5)와 반지름(칸)
    public struct LightSource
    {
        public Vector2 Center;
        public float Radius;

        public LightSource(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        // 그 점의 밝기 0~1: 가운데는 1, 반지름 바깥은 0, 가장자리 Falloff칸에 걸쳐 부드럽게
        public float At(Vector2 point)
        {
            float d = Vector2.Distance(Center, point);
            return Mathf.Clamp01((Radius - d) / CaveLight.Falloff);
        }
    }

    // 1.2 어두운 밀림 동굴의 빛 규칙 (#112, GDD 9-7 — 사용자 확정): 주인공 둘레 3.5칸, 동료를 데려가면 횃불로 +1칸.
    // 밝힌 글자 반딧돌은 둘레 6칸을 영영 밝히고, 쉼터 물건(샘·가게·제단·사전 받침대)은 모닥불처럼 둘레 4칸, 출입구는 바깥 빛 2.5칸.
    // 지나간 곳은 아주 희미하게 남는다(Memory). 그리기는 UI의 FieldDarkness — 여기는 숫자와 계산만
    public static class CaveLight
    {
        public const float HeroRadius = 3.5f;
        public const float TorchBonus = 1f;    // 동료(아라)가 든 횃불
        public const float StoneRadius = 6f;
        public const float CampRadius = 4f;
        public const float DoorRadius = 2.5f;
        public const float Memory = 0.08f;     // 지나간 곳의 밝기 (처음 가는 곳은 0 = 캄캄). Linear 색공간이라 작아도 꽤 보임
        public const float Falloff = 1.6f;     // 빛 가장자리가 옅어지는 폭 (칸)
        public const float EyesBelow = 0.3f;   // 이보다 어두운 곳의 몬스터는 눈 두 개만 보인다

        public static float HeroLightRadius(bool withCompanion) => HeroRadius + (withCompanion ? TorchBonus : 0f);

        public static LightSource Hero(Vector2 world, bool withCompanion) => new LightSource(world, HeroLightRadius(withCompanion));

        // 늘 켜진 빛: 밝힌 반딧돌 · 쉼터 물건 · 출입구
        public static List<LightSource> StaticLights(FieldMap map, Func<Vector2Int, bool> isStoneLit)
        {
            var lights = new List<LightSource>();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var center = new Vector2(x + 0.5f, y + 0.5f);
                switch (map.Get(cell))
                {
                    case FieldTile.GlowStone:
                        if (isStoneLit != null && isStoneLit(cell)) lights.Add(new LightSource(center, StoneRadius));
                        break;
                    case FieldTile.Fountain:
                    case FieldTile.Shop:
                    case FieldTile.Altar:
                    case FieldTile.Lectern:
                        lights.Add(new LightSource(center, CampRadius));
                        break;
                    case FieldTile.Door:
                        lights.Add(new LightSource(center, DoorRadius));
                        break;
                }
            }
            return lights;
        }
    }

    // 맵 전체의 '늘 켜진 밝기'를 칸보다 잘게(칸마다 PixelsPerCell × PixelsPerCell) 들고 있다가, 주인공 빛과 합쳐 어둠 그림을 칠한다.
    // 픽셀 (0,0) = 맵 왼쪽 아래 (월드 좌표와 같은 방향). 늘 켜진 밝기는 반딧돌을 밝히거나 새 칸을 지나갈 때만 다시 계산
    public class DarknessMap
    {
        private readonly float[] staticLight;

        public int Width { get; }        // 픽셀
        public int Height { get; }
        public int PixelsPerCell { get; }

        public DarknessMap(int mapWidth, int mapHeight, int pixelsPerCell = 2)
        {
            PixelsPerCell = Mathf.Max(1, pixelsPerCell);
            Width = mapWidth * PixelsPerCell;
            Height = mapHeight * PixelsPerCell;
            staticLight = new float[Width * Height];
        }

        private Vector2 PixelCenter(int px, int py) => new Vector2((px + 0.5f) / PixelsPerCell, (py + 0.5f) / PixelsPerCell);

        public void SetStatic(IEnumerable<LightSource> lights, Func<Vector2Int, bool> explored)
        {
            for (int py = 0; py < Height; py++)
            for (int px = 0; px < Width; px++)
                staticLight[py * Width + px] = explored != null && explored(new Vector2Int(px / PixelsPerCell, py / PixelsPerCell)) ? CaveLight.Memory : 0f;
            if (lights == null) return;
            foreach (var light in lights)
                ForEachPixelIn(light, (index, value) => { if (value > staticLight[index]) staticLight[index] = value; });
        }

        // 늘 켜진 빛 + 지나간 곳 (주인공 빛 제외)
        public float StaticAt(Vector2 world)
        {
            int px = Mathf.FloorToInt(world.x * PixelsPerCell), py = Mathf.FloorToInt(world.y * PixelsPerCell);
            if (px < 0 || py < 0 || px >= Width || py >= Height) return 0f;
            return staticLight[py * Width + px];
        }

        public float Brightness(Vector2 world, LightSource hero) => Mathf.Max(StaticAt(world), hero.At(world));

        // 어둠 그림: 밝기 b인 픽셀의 불투명도 = alpha × (1 - b), 색은 dark
        public void Paint(LightSource hero, Color32[] pixels, Color32 dark, float alpha)
        {
            if (pixels == null || pixels.Length != Width * Height) throw new ArgumentException("픽셀 배열 크기가 맞지 않습니다");
            for (int i = 0; i < staticLight.Length; i++)
                pixels[i] = new Color32(dark.r, dark.g, dark.b, (byte)Mathf.RoundToInt(255f * alpha * (1f - staticLight[i])));
            ForEachPixelIn(hero, (index, value) =>
            {
                float b = Mathf.Max(staticLight[index], value);
                pixels[index].a = (byte)Mathf.RoundToInt(255f * alpha * (1f - b));
            });
        }

        // 빛의 반지름 안 픽셀만 돈다
        private void ForEachPixelIn(LightSource light, Action<int, float> visit)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((light.Center.x - light.Radius) * PixelsPerCell));
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt((light.Center.x + light.Radius) * PixelsPerCell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((light.Center.y - light.Radius) * PixelsPerCell));
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt((light.Center.y + light.Radius) * PixelsPerCell));
            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                float value = light.At(PixelCenter(px, py));
                if (value > 0f) visit(py * Width + px, value);
            }
        }
    }
}
