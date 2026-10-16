using UnityEngine;

namespace WordRPG.Field
{
    // 바닥 안개 한 장 (#103, 사용자: "한 줄씩 긋지 말고 그냥 합쳐버리자"): 몬스터 칸(풀숲)을 모두 덮는 이어진 안개.
    // 안쪽은 구름 무늬로 짙기만 조금씩 다르고(맵 픽셀 좌표 기준이라 칸마다 되풀이되지 않음 — 고른 막은 유리 같다고 함),
    // 풀숲 밖(벽은 빼고)으로 fade px에 걸쳐 4단계 디더로 점점이 옅어진다. 결과 = (맵 너비×16) × (맵 높이×16) RGBA, 아래 줄부터 (16px = 1칸).
    // 막지 않는 그림일 뿐 — 조우는 그대로 풀숲 규칙
    public static class FogMap
    {
        public const int Pixels = 16;
        private static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

        public static Color32[] Render(FieldMap map, Color32 light, Color32 shade, float alpha, float fade = 6f)
        {
            int width = map.Width * Pixels, height = map.Height * Pixels;
            var pixels = new Color32[width * height];
            for (int cy = 0; cy < map.Height; cy++)
            for (int cx = 0; cx < map.Width; cx++)
            {
                var tile = map.Get(new Vector2Int(cx, cy));
                bool inside = tile == FieldTile.Grass;
                if (!inside && (tile == FieldTile.Wall || !NearGrass(map, cx, cy))) continue; // 벽(책장)에는 번지지 않음 — 벽이 또렷하게
                for (int py = 0; py < Pixels; py++)
                for (int px = 0; px < Pixels; px++)
                {
                    int wx = cx * Pixels + px, wy = cy * Pixels + py;
                    float d = inside ? 0f : DistanceToGrass(map, cx, cy, wx + 0.5f, wy + 0.5f);
                    if (d >= fade) continue;
                    float n1 = Noise(wx, wy * 1.6f, 34f, 1);   // 큰 뭉게 (가로로 조금 길게)
                    float n2 = Noise(wx, wy * 1.6f, 13f, 2);   // 잔 무늬
                    float density = Mathf.Clamp01(0.62f + 0.38f * n1 + 0.16f * (n2 - 0.5f));
                    float level = density * (1f - d / fade) * 4f;
                    int steps = Mathf.FloorToInt(level);
                    if (Bayer[wy & 3, wx & 3] / 16f < level - steps) steps++;
                    if (steps <= 0) continue;
                    float k = Mathf.Clamp01(1.1f - n1 * 1.3f) * 0.6f;   // 옅은 곳은 살짝 그늘색
                    var c = Color32.Lerp(light, shade, k);
                    c.a = (byte)Mathf.RoundToInt(255f * alpha * Mathf.Min(4, steps) / 4f);
                    pixels[wy * width + wx] = c;
                }
            }
            return pixels;
        }

        private static bool NearGrass(FieldMap map, int cx, int cy)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (map.Get(new Vector2Int(cx + dx, cy + dy)) == FieldTile.Grass) return true;
            return false;
        }

        // 둘레 8칸 중 풀숲 칸(사각형)까지의 거리 (픽셀)
        private static float DistanceToGrass(FieldMap map, int cx, int cy, float x, float y)
        {
            float best = float.MaxValue;
            for (int ny = cy - 1; ny <= cy + 1; ny++)
            for (int nx = cx - 1; nx <= cx + 1; nx++)
            {
                if (map.Get(new Vector2Int(nx, ny)) != FieldTile.Grass) continue;
                float dx = Mathf.Max(nx * Pixels - x, 0f, x - (nx * Pixels + Pixels));
                float dy = Mathf.Max(ny * Pixels - y, 0f, y - (ny * Pixels + Pixels));
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
            }
            return best;
        }

        // 값 노이즈 0..1 (좌표 해시 — 늘 같은 무늬)
        private static float Noise(float x, float y, float period, int seed)
        {
            float gx = x / period, gy = y / period;
            int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
            float fx = gx - x0, fy = gy - y0;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            float top = a + (b - a) * sx, bottom = c + (d - c) * sx;
            return top + (bottom - top) * sy;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint v = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                v = (v ^ (v >> 13)) * 1274126177u;
                return ((v ^ (v >> 16)) & 0xffff) / 65535f;
            }
        }
    }
}
