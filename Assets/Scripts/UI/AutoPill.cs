using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 둥근 9-slice 이미지의 모서리를 짧은 변의 절반으로 맞춘다 → 크기가 바뀌어도 항상 알약 모양 (세로 막대도 — 스크롤 막대 #122)
    [RequireComponent(typeof(Image))]
    public class AutoPill : MonoBehaviour
    {
        private const float SpriteRadius = 34f; // UiKit.RoundedSprite(32)의 border

        private Image image;

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange() => Apply();

        private void Apply()
        {
            if (image == null) image = GetComponent<Image>();
            var rect = ((RectTransform)transform).rect;
            float side = Mathf.Min(rect.width, rect.height);
            if (side > 0f) image.pixelsPerUnitMultiplier = SpriteRadius * 2f / side;
        }
    }
}
