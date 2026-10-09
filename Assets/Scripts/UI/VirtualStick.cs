using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 필드 가상 스틱 (Figma 'Virtual Stick' #34 — Idle/Walk/Run).
    // 영역 안 아무 곳이나 엄지를 대면 그 자리로 받침이 옮겨 오고, 끄는 방향으로 손잡이가 따라간다.
    // Value = 손잡이 위치(-1~1, 받침 가장자리 = 길이 1). 방향·달리기 판단은 StickInput(순수 로직).
    // 손가락 하나만 따라가므로 다른 손가락으로 [확인]을 눌러도 스틱이 끊기지 않는다
    public class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // 받침·손잡이가 움직이는 거리는 2026-10-09 기기 점검 뒤 1.25배 (사용자: "너무 쉽게 달린다, 원이 1.25배 컸으면")
        public const float BaseSize = 375f;
        public const float KnobSize = 132f;
        public const float Travel = 137.5f; // 손잡이가 움직이는 최대 거리 (받침 반지름 - 여유) — 달리기는 이 거리의 85% 이상
        private static readonly Vector2 HomeAnchor = new Vector2(0.52f, 0.45f); // 손을 떼면 돌아가는 자리 (영역 비율)

        private RectTransform zone, stickRoot, knob;
        private Image baseImage, ring, knobImage, glow;
        private Text[] arrows;
        private GameObject runLabel;
        private Canvas canvas;
        private int pointerId;
        private bool held;
        private int shownState = -1;

        public Vector2 Value { get; private set; }
        public bool IsHeld => held;
        public RectTransform Zone => zone;

        public static VirtualStick Create(Transform parent, float minX, float minY, float maxX, float maxY)
        {
            var zoneImage = UiKit.Panel("StickZone", parent, Color.clear, minX, minY, maxX, maxY); // 투명하지만 터치는 받음
            var stick = zoneImage.gameObject.AddComponent<VirtualStick>();
            stick.zone = zoneImage.rectTransform;
            stick.zone.pivot = Vector2.zero; // 지역 좌표 = 영역 왼쪽 아래부터

            stick.stickRoot = UiKit.Rect("Stick", stick.zone, 0, 0, 0, 0);
            stick.stickRoot.sizeDelta = new Vector2(BaseSize, BaseSize);

            stick.glow = UiKit.IconImage("Glow", stick.stickRoot, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            stick.glow.rectTransform.sizeDelta = new Vector2(288, 288);
            stick.glow.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.55f);

            stick.baseImage = UiKit.Pill(UiKit.Panel("Base", stick.stickRoot, Color.white));
            stick.baseImage.raycastTarget = false;
            stick.ring = UiKit.Outline(UiKit.Panel("Ring", stick.stickRoot, Color.white), 32, 3);
            stick.ring.gameObject.AddComponent<AutoPill>();
            stick.ring.raycastTarget = false;

            float edge = 147.5f / BaseSize; // 받침 가장자리 쪽 방향 표시 (Noto 글꼴 — Jua에는 도형 기호가 없음)
            stick.arrows = new[]
            {
                Arrow(stick.stickRoot, "▲", 0.5f, 0.5f + edge), Arrow(stick.stickRoot, "▼", 0.5f, 0.5f - edge),
                Arrow(stick.stickRoot, "◀", 0.5f - edge, 0.5f), Arrow(stick.stickRoot, "▶", 0.5f + edge, 0.5f)
            };

            stick.knobImage = UiKit.Pill(UiKit.Panel("Knob", stick.stickRoot, Color.white, 0.5f, 0.5f, 0.5f, 0.5f));
            stick.knobImage.raycastTarget = false;
            stick.knob = stick.knobImage.rectTransform;
            stick.knob.sizeDelta = new Vector2(KnobSize, KnobSize);
            var knobRing = UiKit.Outline(UiKit.Panel("Ring", stick.knob, new Color(1, 1, 1, 0.9f)), 32, 3);
            knobRing.gameObject.AddComponent<AutoPill>();
            knobRing.raycastTarget = false;

            var label = UiKit.Pill(UiKit.Panel("RunLabel", stick.stickRoot, Palette.Gold, 0.5f, 0f, 0.5f, 0f));
            label.raycastTarget = false;
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(140, 46);
            label.rectTransform.anchoredPosition = new Vector2(0, -6);
            UiKit.Display(UiKit.Label("Text", label.transform, "달리기", 30, Palette.OnAccent, 0, 0, 1, 1));
            stick.runLabel = label.gameObject;

            stick.Release();
            return stick;
        }

        private static Text Arrow(Transform parent, string arrow, float x, float y)
        {
            var text = UiKit.Label("Arrow", parent, arrow, 30, Color.white, x, y, x, y, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.sizeDelta = new Vector2(44, 44);
            return text;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (held) return; // 다른 손가락은 무시
            held = true;
            pointerId = eventData.pointerId;
            var local = ToLocal(eventData.position);
            stickRoot.anchoredPosition = ClampInside(local);
            MoveKnob(local);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (held && eventData.pointerId == pointerId) MoveKnob(ToLocal(eventData.position));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (held && eventData.pointerId == pointerId) Release();
        }

        private void OnDisable() => Release(); // 창이 열리거나 화면이 바뀌면 손을 뗀 것으로

        // 화면 크기·안전 영역이 정해진 뒤에도 제자리가 맞게
        private void OnRectTransformDimensionsChange()
        {
            if (!held && knob != null) Release();
        }

        // 손을 떼면: 멈추고 받침은 제자리로
        public void Release()
        {
            held = false;
            Value = Vector2.zero;
            if (knob == null) return; // 만드는 중 (부품이 아직 다 없음)
            stickRoot.anchoredPosition = Vector2.Scale(zone.rect.size, HomeAnchor);
            knob.anchoredPosition = Vector2.zero;
            Refresh();
        }

        // 테스트·연출용: 받침이 제자리에 있을 때 스틱 값 value가 되는 화면 좌표
        public Vector2 ScreenPointFor(Vector2 value)
        {
            var local = Vector2.Scale(zone.rect.size, HomeAnchor) + value * Travel;
            return RectTransformUtility.WorldToScreenPoint(EventCamera(), zone.TransformPoint(local));
        }

        private void MoveKnob(Vector2 local)
        {
            var offset = Vector2.ClampMagnitude(local - stickRoot.anchoredPosition, Travel);
            knob.anchoredPosition = offset;
            Value = offset / Travel;
            Refresh();
        }

        // 받침이 영역 밖으로 반쯤 나가지 않게 (영역 가장자리를 눌러도 받침은 안쪽에)
        private Vector2 ClampInside(Vector2 local)
        {
            var size = zone.rect.size;
            float half = BaseSize * 0.5f;
            return new Vector2(Mathf.Clamp(local.x, Mathf.Min(half, size.x * 0.5f), Mathf.Max(size.x - half, size.x * 0.5f)),
                Mathf.Clamp(local.y, Mathf.Min(half, size.y * 0.5f), Mathf.Max(size.y - half, size.y * 0.5f)));
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, screen, EventCamera(), out var local);
            return local;
        }

        private Camera EventCamera()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        // 대기(반투명) · 걷기(손잡이 진하게) · 달리기(금색 + '달리기')
        private void Refresh()
        {
            int state = !held || Value.magnitude < StickInput.DeadZone ? 0 : StickInput.IsRunning(Value) ? 2 : 1;
            if (state == shownState) return;
            shownState = state;
            bool run = state == 2;
            baseImage.color = new Color(1, 1, 1, state == 0 ? 0.1f : 0.16f);
            ring.color = run ? new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.9f) : new Color(1, 1, 1, 0.35f);
            knobImage.color = run ? Palette.Gold : new Color(1, 1, 1, state == 0 ? 0.55f : 0.85f);
            foreach (var arrow in arrows) arrow.color = new Color(1, 1, 1, 0.5f);
            glow.enabled = run;
            runLabel.SetActive(run);
        }
    }
}
