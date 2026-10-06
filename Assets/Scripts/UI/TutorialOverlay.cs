using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 튜토리얼 안내 (#36, Figma 'Guide Box' · 화면 '튜토리얼 — 필드 스틱 / 전투 강도 고르기 / 전투 문제').
    // 화면 전체를 검은 막으로 덮고 안내하는 곳만 뚫어 금색 테두리로 보여 준다. 캐릭터 얼굴 없이 시스템 안내 말투.
    //  - [다음] 단계(DoneWhen 없음): 읽고 [다음]. 뚫린 곳도 눌리지 않고 뒤 화면은 멈춘다 (IsBlocking)
    //  - 따라 하기 단계(DoneWhen 있음): 뚫린 곳만 눌러 직접 해 보면 넘어간다. 나머지는 막혀 있다
    //  - [건너뛰기]: 남은 안내를 모두 본 것으로 하고 닫는다
    // 자기 캔버스(sortingOrder 100)를 가져서 필드 HUD·전투 화면 위에 덮인다
    public class TutorialOverlay : MonoBehaviour
    {
        public class Step
        {
            public string Text;
            public Func<RectTransform> Target; // null이면 가운데 상자만
            public Func<bool> DoneWhen;        // null이면 [다음]으로 넘기는 단계
            public string Button = "다음";
        }

        private const float Padding = 14f;
        private const float BoxWidth = 920f;
        private const float ArrowGap = 56f;
        private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.78f); // Linear 색공간이라 높게

        private Canvas canvas;
        private RectTransform root, safe, box, arrowRt;
        private Image top, bottom, left, right, holeBlocker, ring;
        private Text message, counter, arrow, hint, nextLabel;
        private Button next;
        private Step current;
        private bool advanced, skipped;
        private readonly Vector3[] corners = new Vector3[4];

        public bool IsShowing => current != null;
        public bool IsBlocking => current != null && current.DoneWhen == null; // [다음] 단계 = 뒤 화면 멈춤
        public string CurrentText => current != null ? current.Text : "";
        public string CurrentCounter => current != null ? counter.text : "";

        public static TutorialOverlay Create(Transform parent)
        {
            var go = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var overlay = go.AddComponent<TutorialOverlay>();
            overlay.canvas = go.GetComponent<Canvas>();
            overlay.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.canvas.sortingOrder = 100; // 필드 HUD(0)·전투(10) 위
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            overlay.Build();
            overlay.Hide();
            return overlay;
        }

        private void Build()
        {
            root = UiKit.Stretch("Overlay", canvas.transform); // 노치까지 덮는 검은 막
            top = Scrim("ScrimTop");
            bottom = Scrim("ScrimBottom");
            left = Scrim("ScrimLeft");
            right = Scrim("ScrimRight");
            holeBlocker = UiKit.Panel("HoleBlocker", root, Color.clear, 0.5f, 0.5f, 0.5f, 0.5f); // [다음] 단계엔 뚫린 곳도 막음
            ring = UiKit.Outline(UiKit.Panel("Ring", root, Palette.Gold, 0.5f, 0.5f, 0.5f, 0.5f), UiKit.RadiusLg, 6);
            ring.raycastTarget = false;
            arrow = UiKit.Label("Arrow", root, "▼", 64, Palette.Gold, 0.5f, 0.5f, 0.5f, 0.5f, TextAnchor.MiddleCenter, FontStyle.Bold);
            arrowRt = arrow.rectTransform;
            arrowRt.sizeDelta = new Vector2(80, 80);

            safe = UiKit.Stretch("SafeArea", root);
            UiKit.ApplySafeArea(safe);

            // 안내 상자 (Figma 'Guide Box' — Next / Action)
            var boxImage = UiKit.RoundPanel("GuideBox", root, Palette.Panel, UiKit.RadiusLg, 0.5f, 0.5f, 0.5f, 0.5f);
            box = boxImage.rectTransform;
            var border = UiKit.Outline(UiKit.Panel("Border", box, Palette.Gold), UiKit.RadiusLg, 4);
            border.raycastTarget = false;
            var tag = UiKit.Pill(UiKit.Panel("Tag", box, Palette.Gold, 0, 1, 0, 1));
            tag.raycastTarget = false;
            tag.rectTransform.pivot = new Vector2(0, 1);
            tag.rectTransform.sizeDelta = new Vector2(96, 46);
            tag.rectTransform.anchoredPosition = new Vector2(40, -32);
            UiKit.Display(UiKit.Label("Text", tag.transform, "안내", 28, Palette.OnAccent, 0, 0, 1, 1));
            counter = UiKit.Label("Counter", box, "", 26, Palette.TextDim, 0.5f, 1, 1, 1, TextAnchor.MiddleRight, FontStyle.Bold);
            counter.rectTransform.pivot = new Vector2(1, 1);
            counter.rectTransform.offsetMin = new Vector2(0, -78);
            counter.rectTransform.offsetMax = new Vector2(-40, -32);
            message = UiKit.Label("Message", box, "", 34, Palette.Text, 0, 1, 1, 1, TextAnchor.UpperLeft);
            message.lineSpacing = 1.15f;
            message.verticalOverflow = VerticalWrapMode.Overflow;
            next = UiKit.MakeButton("TutorialNextButton", box, "다음", Palette.Gold, 40, 1, 0, 1, 0);
            nextLabel = UiKit.LabelOf(next);
            nextLabel.color = Palette.OnAccent;
            var nextRt = (RectTransform)next.transform;
            nextRt.pivot = new Vector2(1, 0);
            nextRt.sizeDelta = new Vector2(240, 88);
            nextRt.anchoredPosition = new Vector2(-40, 32);
            next.onClick.AddListener(() => advanced = true);
            hint = UiKit.Label("Hint", box, "▶ 직접 해 보세요", 30, Palette.Gold, 0.4f, 0, 1, 0, TextAnchor.MiddleRight, FontStyle.Bold);
            hint.rectTransform.pivot = new Vector2(1, 0);
            hint.rectTransform.offsetMin = new Vector2(0, 28);
            hint.rectTransform.offsetMax = new Vector2(-40, 72);

            // 건너뛰기: 오른쪽 위 (아래쪽은 스틱·[확인] 자리)
            var skip = UiKit.MakeButton("TutorialSkipButton", safe, "건너뛰기", Palette.Bad, 32, 1, 1, 1, 1);
            var skipRt = (RectTransform)skip.transform;
            skipRt.pivot = new Vector2(1, 1);
            skipRt.sizeDelta = new Vector2(220, 76);
            skipRt.anchoredPosition = new Vector2(-24, -24);
            skip.onClick.AddListener(() => skipped = true);
        }

        private Image Scrim(string name)
        {
            var image = UiKit.Panel(name, root, ScrimColor, 0.5f, 0.5f, 0.5f, 0.5f); // 막은 눌림을 막는다
            image.rectTransform.pivot = Vector2.zero;
            return image;
        }

        // 묶음(steps)을 차례로. 이미 본 id면 바로 끝. 다 보면 그 id만, [건너뛰기]면 모든 안내를 본 것으로 저장
        public IEnumerator Run(GameSession session, string id, IList<Step> steps, Action save)
        {
            if (session == null || session.Tutorials.Has(id) || current != null) yield break;
            skipped = false;
            for (int i = 0; i < steps.Count && !skipped; i++)
            {
                Show(steps[i], steps.Count > 1 ? $"{i + 1} / {steps.Count}" : "");
                advanced = false;
                while (!advanced && !skipped)
                {
                    if (current.DoneWhen != null && current.DoneWhen()) break;
                    yield return null;
                }
            }
            Hide();
            if (skipped) session.Tutorials.MarkAll();
            else session.Tutorials.Mark(id);
            save?.Invoke();
        }

        public IEnumerator Run(GameSession session, string id, Step step, Action save) => Run(session, id, new[] { step }, save);

        // 테스트용: [다음] / [건너뛰기]를 누른 것과 같음
        public void PressNext() => advanced = true;
        public void PressSkip() => skipped = true;

        private void Show(Step step, string count)
        {
            current = step;
            root.gameObject.SetActive(true);
            message.text = step.Text;
            counter.text = count;
            bool action = step.DoneWhen != null;
            next.gameObject.SetActive(!action);
            nextLabel.text = step.Button;
            hint.gameObject.SetActive(action);
            holeBlocker.raycastTarget = !action;

            // 상자 높이 = 위 여백 + '안내' 줄 + 글 + 아래 버튼 줄
            float textWidth = BoxWidth - 80f;
            box.sizeDelta = new Vector2(BoxWidth, 400f);
            message.rectTransform.offsetMin = new Vector2(40, -1000);
            message.rectTransform.offsetMax = new Vector2(-40, -98);
            var settings = message.GetGenerationSettings(new Vector2(textWidth, 0));
            float textHeight = message.cachedTextGeneratorForLayout.GetPreferredHeight(step.Text, settings) / message.pixelsPerUnit;
            float height = 98f + Mathf.Max(60f, textHeight) + 24f + (action ? 60f : 128f);
            box.sizeDelta = new Vector2(BoxWidth, height);
            message.rectTransform.offsetMin = new Vector2(40, -98 - Mathf.Max(60f, textHeight) - 8f);
            Place();
        }

        private void Hide()
        {
            current = null;
            if (root != null) root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (current != null) Place();
        }

        // 안내하는 곳을 따라 구멍·테두리·화살표·상자를 놓는다 (대상이 움직여도 따라감)
        private void Place()
        {
            var area = root.rect;
            var target = current.Target?.Invoke();
            bool hasHole = target != null && target.gameObject.activeInHierarchy;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            if (hasHole)
            {
                target.GetWorldCorners(corners);
                var cam = CameraOf(target);
                min = new Vector2(float.MaxValue, float.MaxValue);
                max = new Vector2(float.MinValue, float.MinValue);
                foreach (var corner in corners)
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(cam, corner);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, CameraOf(root), out var local);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
                min = Vector2.Max(min - Vector2.one * Padding, area.min);
                max = Vector2.Min(max + Vector2.one * Padding, area.max);
                hasHole = max.x > min.x && max.y > min.y;
            }

            if (hasHole)
            {
                Set(top.rectTransform, new Vector2(area.xMin, max.y), area.max);
                Set(bottom.rectTransform, area.min, new Vector2(area.xMax, min.y));
                Set(left.rectTransform, new Vector2(area.xMin, min.y), new Vector2(min.x, max.y));
                Set(right.rectTransform, new Vector2(max.x, min.y), new Vector2(area.xMax, max.y));
                Set(holeBlocker.rectTransform, min, max);
                Set(ring.rectTransform, min, max);
            }
            else
            {
                Set(top.rectTransform, area.min, area.max);
                foreach (var image in new[] { bottom, left, right, holeBlocker, ring }) Set(image.rectTransform, Vector2.zero, Vector2.zero);
            }
            ring.enabled = hasHole;
            arrow.enabled = hasHole;

            // 상자: 구멍이 아래쪽이면 위에, 위쪽이면 아래에 (안전 영역 안으로)
            safe.GetWorldCorners(corners);
            float safeBottom = root.InverseTransformPoint(corners[0]).y + 24f;
            float safeTop = root.InverseTransformPoint(corners[1]).y - 120f; // 건너뛰기 버튼 자리
            float h = box.sizeDelta.y;
            float centerY;
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 8f;
            if (!hasHole) centerY = 0f;
            else if ((min.y + max.y) * 0.5f < 0f)
            {
                centerY = max.y + ArrowGap * 2f + h * 0.5f;
                arrow.text = "▼";
                arrowRt.anchoredPosition = new Vector2((min.x + max.x) * 0.5f, max.y + ArrowGap + bob);
            }
            else
            {
                centerY = min.y - ArrowGap * 2f - h * 0.5f;
                arrow.text = "▲";
                arrowRt.anchoredPosition = new Vector2((min.x + max.x) * 0.5f, min.y - ArrowGap - bob);
            }
            centerY = Mathf.Clamp(centerY, safeBottom + h * 0.5f, Mathf.Max(safeBottom + h * 0.5f, safeTop - h * 0.5f));
            box.anchoredPosition = new Vector2(0, centerY);
        }

        private static void Set(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = min;
            rt.sizeDelta = Vector2.Max(Vector2.zero, max - min);
        }

        private static Camera CameraOf(Component component)
        {
            var c = component.GetComponentInParent<Canvas>();
            if (c != null) c = c.rootCanvas;
            return c == null || c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
        }
    }
}
