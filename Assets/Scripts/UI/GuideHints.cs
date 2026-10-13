using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 처음 하는 사람용 안내 (#61, 사용자 피드백 '튜토리얼 글이 너무 많다' → 글은 한 줄, 동작은 보여 주기).
    // 화면을 막지 않는다: 한 줄 알약 + (대상 쪽) 손끝 동그라미가 끌거나 톡톡 누르는 모습 또는 통통 튀는 화살표.
    // 직접 해 보면(DoneWhen) 사라지고 그 id는 본 것으로 저장된다 — 같은 안내는 다시 안 나온다 (설정 > 튜토리얼 다시 보기로 처음부터).
    // 예전 TutorialOverlay(검은 막 + 긴 글 + [다음])를 대신한다. 자기 캔버스(sortingOrder 100)로 필드 HUD·전투 위에 그린다
    public class GuideHints : MonoBehaviour
    {
        public enum Gesture { Point, Tap, Drag, None }

        public class Hint
        {
            public string Text;
            public Func<RectTransform> Target;   // 없으면 화면 위쪽 가운데에 알약만
            public Func<RectTransform> PillNear; // 알약을 따로 놓을 곳 (가리키는 것과 읽어야 할 글이 겹칠 때 — 예: 새 단어 카드 위)
            public Func<bool> DoneWhen;          // 해 보면 끝. 없으면 Seconds 동안만
            public Gesture Gesture = Gesture.Point;
            public Vector2 Drag;                 // Drag: 대상 가운데에서 이만큼 끌기 (캔버스 단위)
            public float Seconds = 4f;
        }

        private const string ArrowPath = "Art/NinjaAdventure/Ui/arrow";

        private Canvas canvas;
        private RectTransform root, pill, arrow, ghost, ghostRing;
        private Text pillText;
        private Image ghostImage, ghostRingImage, arrowImage;
        private CanvasGroup group;
        private Hint current;
        private float shownAt;

        public bool IsShowing => current != null;
        public string CurrentText => current != null ? current.Text : "";
        public bool GhostVisible => current != null && ghost.gameObject.activeSelf;
        public bool ArrowVisible => current != null && arrow.gameObject.activeSelf;
        public Gesture CurrentGesture => current != null ? current.Gesture : Gesture.Point;

        public static GuideHints Create(Transform parent)
        {
            var go = new GameObject("GuideCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var hints = go.AddComponent<GuideHints>();
            hints.canvas = go.GetComponent<Canvas>();
            hints.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hints.canvas.sortingOrder = 100; // 필드 HUD(0)·전투(10) 위. 눌림은 받지 않는다 (레이캐스터 없음)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            hints.Build();
            hints.Hide();
            return hints;
        }

        private void Build()
        {
            root = UiKit.Stretch("Guide", canvas.transform);
            UiKit.ApplySafeArea(root);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // 한 줄 알약 (Figma 키트 'Toast'·이름표와 같은 계열: 어두운 판 + 금색 테두리 + Jua)
            var back = UiKit.Pill(UiKit.Panel("HintPill", root, new Color(Palette.Panel.r, Palette.Panel.g, Palette.Panel.b, 0.94f),
                0.5f, 0.5f, 0.5f, 0.5f));
            back.raycastTarget = false;
            pill = back.rectTransform;
            var border = UiKit.Outline(UiKit.Panel("Border", pill, Palette.Gold), 48, 4);
            border.gameObject.AddComponent<AutoPill>();
            border.raycastTarget = false;
            pillText = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", pill, "", 42, Palette.Text, 0, 0, 1, 1)));

            // 통통 튀는 화살표 (에셋 팩 Ui/Arrow.png — 아래를 가리키는 흰 화살표)
            arrowImage = UiKit.IconImage("Arrow", root, LoadArrow(), 0.5f, 0.5f, 0.5f, 0.5f);
            arrowImage.raycastTarget = false;
            arrow = arrowImage.rectTransform;
            arrow.sizeDelta = new Vector2(84, 84);

            // 손끝: 반투명 흰 동그라미 + 테두리 (누르면 작아지고, 끌면 따라 움직임)
            ghostImage = UiKit.Pill(UiKit.Panel("Ghost", root, new Color(1f, 1f, 1f, 0.55f), 0.5f, 0.5f, 0.5f, 0.5f));
            ghostImage.raycastTarget = false;
            ghost = ghostImage.rectTransform;
            ghost.sizeDelta = new Vector2(96, 96);
            ghostRingImage = UiKit.Outline(UiKit.Panel("Ring", ghost, Color.white), 48, 5);
            ghostRingImage.gameObject.AddComponent<AutoPill>();
            ghostRingImage.raycastTarget = false;
            ghostRing = ghostRingImage.rectTransform;
        }

        private static Sprite LoadArrow()
        {
            var texture = Resources.Load<Texture2D>(ArrowPath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);
        }

        // id를 처음 만날 때만. DoneWhen이 참이 되면(없으면 Seconds가 지나면) 사라지고 본 것으로 저장.
        // cancelWhen이 참이 되면 저장하지 않고 사라진다 (예: [확인] 안내 중에 물건에서 멀어짐 → 다음에 다시)
        public IEnumerator Run(GameSession session, string id, Hint hint, Action save, Func<bool> cancelWhen = null)
        {
            if (session == null || session.Tutorials.Has(id)) yield break;
            if (current != null)
            {
                // 앞 안내가 이미 끝났으면(해 봤으면) 바로 넘겨받고, 아직이면 이번엔 띄우지 않는다 (다음에 그 상황이 오면 다시)
                if (current.DoneWhen != null && current.DoneWhen()) Hide();
                else yield break;
            }
            Show(hint);
            bool done = false;
            while (current == hint)
            {
                if (hint.DoneWhen != null ? hint.DoneWhen() : Time.unscaledTime - shownAt >= hint.Seconds)
                {
                    done = true;
                    break;
                }
                if (cancelWhen != null && cancelWhen()) break;
                yield return null;
            }
            if (current == hint) Hide();
            if (!done && hint.DoneWhen != null && hint.DoneWhen()) done = true; // 다음 안내가 넘겨받은 경우
            if (!done) yield break;
            session.Tutorials.Mark(id);
            save?.Invoke();
        }

        public void Show(Hint hint)
        {
            current = hint;
            shownAt = Time.unscaledTime;
            pillText.text = hint.Text;
            float width = pillText.preferredWidth + 96f;
            pill.sizeDelta = new Vector2(Mathf.Clamp(width, 260f, 1000f), 96f);
            ghost.gameObject.SetActive(hint.Gesture == Gesture.Tap || hint.Gesture == Gesture.Drag);
            arrow.gameObject.SetActive(hint.Gesture == Gesture.Point && hint.Target != null);
            root.gameObject.SetActive(true);
            Place();
        }

        public void Hide()
        {
            current = null;
            if (root != null) root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (current != null) Place();
        }

        // 대상이 화면 아래쪽이면 알약은 대상 위, 위쪽이면 대상 아래. 대상이 없으면 위쪽 가운데
        private void Place()
        {
            var area = root.rect;
            var target = current.Target?.Invoke();
            bool hasTarget = target != null && target.gameObject.activeInHierarchy;
            float t = Time.unscaledTime - shownAt;
            if (!hasTarget)
            {
                pill.anchoredPosition = new Vector2(0, area.yMax - 330f);
                ghost.gameObject.SetActive(false);
                arrow.gameObject.SetActive(false);
                return;
            }

            var (min, max) = Bounds(target);
            var center = (min + max) * 0.5f;
            bool below = center.y < 0f;
            var near = current.PillNear?.Invoke();
            var (pillMin, pillMax) = near != null && near.gameObject.activeInHierarchy ? Bounds(near) : (min, max);
            float pillCenterY = (pillMin.y + pillMax.y) * 0.5f;
            float pillY = pillCenterY < 0f ? pillMax.y + (near != null ? 70f : 150f) : pillMin.y - (near != null ? 70f : 150f);
            pillY = Mathf.Clamp(pillY, area.yMin + 80f, area.yMax - 200f);
            float halfWidth = pill.sizeDelta.x * 0.5f;
            float pillX = (pillMin.x + pillMax.x) * 0.5f;
            pill.anchoredPosition = new Vector2(Mathf.Clamp(pillX, area.xMin + halfWidth + 16f, area.xMax - halfWidth - 16f), pillY);

            switch (current.Gesture)
            {
                case Gesture.Point:
                    // 대상 가장자리에서 통통 (위에 있으면 위를 가리키도록 뒤집음)
                    float bob = Mathf.Abs(Mathf.Sin(t * 5f)) * 18f;
                    arrow.localEulerAngles = new Vector3(0, 0, below ? 0f : 180f);
                    arrow.anchoredPosition = below ? new Vector2(center.x, max.y + 52f + bob) : new Vector2(center.x, min.y - 52f - bob);
                    break;
                case Gesture.Tap:
                    // 0.9초마다 톡: 커졌다가 눌리며 작아지고 테두리가 퍼진다
                    float k = (t % 0.9f) / 0.9f;
                    float press = k < 0.35f ? Mathf.Lerp(1.25f, 0.85f, k / 0.35f) : 1f;
                    ghost.anchoredPosition = center + new Vector2(18f, -18f);
                    ghost.localScale = Vector3.one * press;
                    ghostRing.localScale = Vector3.one * (k < 0.35f ? 1f : Mathf.Lerp(1f, 1.8f, (k - 0.35f) / 0.65f));
                    ghostRingImage.color = new Color(1, 1, 1, k < 0.35f ? 0.9f : Mathf.Lerp(0.9f, 0f, (k - 0.35f) / 0.65f));
                    ghostImage.color = new Color(1, 1, 1, 0.55f);
                    break;
                case Gesture.Drag:
                    // 눌렀다가(0~0.2) 끌고(0.2~1.0) 잠깐 멈췄다가(1.0~1.3) 사라짐(~1.6) — 반복
                    float c = t % 1.6f;
                    var start = center;
                    float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((c - 0.2f) / 0.8f));
                    ghost.anchoredPosition = start + current.Drag * move;
                    ghost.localScale = Vector3.one * (c < 0.2f ? Mathf.Lerp(1.25f, 0.9f, c / 0.2f) : 0.9f);
                    float alpha = c < 1.3f ? 0.6f : Mathf.Lerp(0.6f, 0f, (c - 1.3f) / 0.3f);
                    ghostImage.color = new Color(1, 1, 1, alpha);
                    ghostRingImage.color = new Color(1, 1, 1, alpha + 0.3f);
                    ghostRing.localScale = Vector3.one;
                    break;
            }
        }

        private readonly Vector3[] corners = new Vector3[4];

        // 대상의 네 모서리를 이 안내 층의 좌표로. 필드 HUD·전투·안내 캔버스는 모두 같은 크기·같은 배율의 화면 캔버스라
        // 월드 좌표를 그대로 옮기면 된다 (화면 좌표를 거치지 않아 캔버스 방식이 바뀌어도 맞음)
        private (Vector2 min, Vector2 max) Bounds(RectTransform target)
        {
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                Vector2 local = root.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return (min, max);
        }
    }
}
