using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 바닥 안개 (#102·#103, 사용자 참고 그림): 지역마다 몬스터 칸(풀숲)을 덮는 이어진 안개 한 장을 그려 타일 위에 얹는다.
    // 그림은 FogMap(규칙)이 맵에서 바로 만든다 — 줄줄이 겹친 띠는 "한 줄씩 긋지 말고 합치자"고 해서 한 장으로 (#103).
    // 구름 무늬는 천천히 흘러간다 (#104) — 가장자리는 그대로라 벽 위로 미끄러지지 않음.
    // 지금은 서고만 (StyleFor). 막지 않는 그림일 뿐 (조우는 그대로 풀숲 규칙)
    public class FieldFog
    {
        private const float DriftPixelsPerSecond = 1f; // 구름 무늬가 흘러가는 빠르기 — 1초에 한 번만 다시 칠함 (#105 사용자: 폰 부담이면 느려도 됨, 멈춘 것만 아니면)
        private readonly Transform parent;
        private GameObject layer;
        private Texture2D texture;
        private FogMap.Coverage coverage;
        private Color32[] pixels;
        private Color32 light, shade;
        private float alpha, time;
        private int drawnStep;

        public bool IsShowing => layer != null; // 테스트용
        public int Repaints { get; private set; } // 무늬를 다시 칠한 횟수 (테스트용)

        public FieldFog(Transform parent) => this.parent = parent;

        // 안개를 쓰는 테마와 색 (옅은 곳 · 그늘색 · 가장 짙은 곳의 불투명도) — 테마 표 ThemeLooks (#118)
        public static bool StyleFor(FieldTheme theme, out Color32 light, out Color32 shade, out float alpha)
        {
            var fog = ThemeLooks.Of(theme).Fog;
            light = fog?.Light ?? default;
            shade = fog?.Shade ?? default;
            alpha = fog?.Alpha ?? 0f;
            return fog.HasValue;
        }

        public void Build(FieldMap map, FieldTheme theme)
        {
            Clear();
            if (!StyleFor(theme, out light, out shade, out alpha)) return;
            coverage = FogMap.Prepare(map);
            pixels = new Color32[coverage.Width * coverage.Height];
            FogMap.Paint(coverage, pixels, light, shade, alpha);
            int width = coverage.Width, height = coverage.Height;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{theme}_Fog"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false); // 무늬가 흘러가며 다시 칠하므로 읽기 가능하게 둠
            time = 0f;
            drawnStep = 0;
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, FogMap.Pixels, 0, SpriteMeshType.FullRect);
            layer = new GameObject("Fog", typeof(SpriteRenderer));
            layer.transform.SetParent(parent, false);
            layer.transform.position = Vector3.zero; // 칸 (0,0)의 왼쪽 아래 = 월드 (0,0)
            var renderer = layer.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 3; // 타일·흩뿌림(1) 위, 큰 그림(5)·주인공(10) 아래
        }

        // 구름 무늬를 천천히 흘려보냄 (#104·#105): 한 픽셀 흘러갈 때만 다시 칠한다 (1초에 한 번). 가장자리는 그대로
        public void Tick(float dt)
        {
            if (texture == null) return;
            time += dt;
            int step = Mathf.FloorToInt(time * DriftPixelsPerSecond);
            if (step == drawnStep) return;
            drawnStep = step;
            FogMap.Paint(coverage, pixels, light, shade, alpha, step);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Repaints++;
        }

        public void Clear()
        {
            if (layer != null) Object.Destroy(layer);
            if (texture != null) Object.Destroy(texture);
            layer = null;
            texture = null;
            coverage = null;
            pixels = null;
        }
    }
}
