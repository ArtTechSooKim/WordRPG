using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 바닥 안개 (#102·#103, 사용자 참고 그림): 지역마다 몬스터 칸(풀숲)을 덮는 이어진 안개 한 장을 그려 타일 위에 얹는다.
    // 그림은 FogMap(규칙)이 맵에서 바로 만든다 — 줄줄이 겹친 띠는 "한 줄씩 긋지 말고 합치자"고 해서 한 장으로 (#103).
    // 지금은 서고만 (StyleFor). 막지 않는 그림일 뿐 (조우는 그대로 풀숲 규칙)
    public class FieldFog
    {
        private readonly Transform parent;
        private GameObject layer;
        private Texture2D texture;

        public bool IsShowing => layer != null; // 테스트용

        public FieldFog(Transform parent) => this.parent = parent;

        // 안개를 쓰는 테마와 색 (옅은 푸른 회색 · 옅은 곳의 그늘색 · 가장 짙은 곳의 불투명도)
        public static bool StyleFor(FieldTheme theme, out Color32 light, out Color32 shade, out float alpha)
        {
            light = new Color32(206, 218, 232, 255);
            shade = new Color32(150, 168, 192, 255);
            alpha = 0.5f;
            return theme == FieldTheme.Library;
        }

        public void Build(FieldMap map, FieldTheme theme)
        {
            Clear();
            if (!StyleFor(theme, out var light, out var shade, out float alpha)) return;
            int width = map.Width * FogMap.Pixels, height = map.Height * FogMap.Pixels;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"{theme}_Fog"
            };
            texture.SetPixels32(FogMap.Render(map, light, shade, alpha));
            texture.Apply(false, true); // 다 그렸으면 메모리 아끼게 읽기 끔
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, FogMap.Pixels, 0, SpriteMeshType.FullRect);
            layer = new GameObject("Fog", typeof(SpriteRenderer));
            layer.transform.SetParent(parent, false);
            layer.transform.position = Vector3.zero; // 칸 (0,0)의 왼쪽 아래 = 월드 (0,0)
            var renderer = layer.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 3; // 타일·흩뿌림(1) 위, 큰 그림(5)·주인공(10) 아래
        }

        public void Clear()
        {
            if (layer != null) Object.Destroy(layer);
            if (texture != null) Object.Destroy(texture);
            layer = null;
            texture = null;
        }
    }
}
