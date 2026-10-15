using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 두께가 일정한 원 테두리 (#74 QTE: 줄어드는 원 · 점선 과녁 · [선제공격] 맥박 고리 — Figma 'QTE Ring'·'Preempt Prompt').
    // 둥근 9-slice 테두리는 크기를 바꾸면 두께도 같이 변해서, 원을 메시로 직접 그린다. 지름 = 사각형의 짧은 변
    [RequireComponent(typeof(CanvasRenderer))]
    public class UiRing : MaskableGraphic
    {
        private const int Segments = 96;

        [SerializeField] private float thickness = 8f;
        [SerializeField] private int dashes; // 0 = 이어진 원, n = 점선 n개

        public float Thickness
        {
            get => thickness;
            set
            {
                if (Mathf.Approximately(thickness, value)) return;
                thickness = value;
                SetVerticesDirty();
            }
        }

        public int Dashes
        {
            get => dashes;
            set
            {
                if (dashes == value) return;
                dashes = value;
                SetVerticesDirty();
            }
        }

        // 부모 가운데에 지름 diameter로 (크기는 rectTransform.sizeDelta로 바꾼다)
        public static UiRing Create(string name, Transform parent, Color color, float thickness, float diameter, int dashes = 0)
        {
            var rt = UiKit.Rect(name, parent, 0.5f, 0.5f, 0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            rt.gameObject.AddComponent<CanvasRenderer>(); // 코드로 만든 Graphic 파생 클래스는 저절로 붙지 않음 (안 붙으면 아무것도 안 그려짐)
            var ring = rt.gameObject.AddComponent<UiRing>();
            ring.color = color;
            ring.thickness = thickness;
            ring.dashes = dashes;
            ring.raycastTarget = false;
            return ring;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (outer <= 0f) return;
            float inner = Mathf.Max(0f, outer - thickness);
            var center = rect.center;
            Color32 tint = color;
            int per = dashes > 0 ? Mathf.Max(2, Segments / dashes) : 0;
            for (int i = 0; i < Segments; i++)
            {
                if (per > 0 && i % per >= per * 0.6f) continue; // 점선: 한 칸의 60%만 그림
                float a0 = i * Mathf.PI * 2f / Segments, a1 = (i + 1) * Mathf.PI * 2f / Segments;
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                int start = vh.currentVertCount;
                vh.AddVert(center + d0 * inner, tint, Vector2.zero);
                vh.AddVert(center + d0 * outer, tint, Vector2.zero);
                vh.AddVert(center + d1 * outer, tint, Vector2.zero);
                vh.AddVert(center + d1 * inner, tint, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
