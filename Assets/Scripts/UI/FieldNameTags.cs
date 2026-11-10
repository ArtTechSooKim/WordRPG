using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 필드 오브젝트 위 이름표 (Figma 'Name Tag'): 성유물 제단·상점·회복의 샘·보스·출입구에
    // 가로·세로 2칸 안으로 다가가면 서서히 나타난다. 이름표 칸은 맵 글자에서 자동으로 찾으므로 지역이 늘어나도 그대로 동작
    public class FieldNameTags
    {
        private const float FadeSpeed = 8f;   // 1초에 알파 변화량
        private const float PointerSize = 24f; // 꼬리 그림 크기 (위쪽 절반만 삼각형)

        private class Tag
        {
            public Vector2Int Cell;
            public RectTransform Root;
            public Text Label;
            public CanvasGroup Group;
            public bool Visible;
        }

        private readonly RectTransform layer;
        private readonly List<Tag> tags = new List<Tag>();
        private Func<Vector2Int, float> topOf; // 칸 아래 변에서 이름표 꼬리까지 높이 (칸) — 크게 선 보스는 머리 위 (#233)

        private FieldNameTags(RectTransform layer)
        {
            this.layer = layer;
        }

        // HUD에서 가장 먼저 만들어야 다른 HUD·창 아래에 깔린다
        public static FieldNameTags Create(Transform parent)
        {
            var layer = UiKit.Stretch("NameTags", parent);
            layer.pivot = new Vector2(0.5f, 0.5f);
            return new FieldNameTags(layer);
        }

        public IEnumerable<string> VisibleNames
        {
            get
            {
                foreach (var tag in tags)
                {
                    if (tag.Visible) yield return tag.Label.text;
                }
            }
        }

        // 지역이 바뀌면 이름표를 다시 만든다 (이름이 없는 칸 — 연결 안 된 출입구 등 — 은 건너뜀).
        // lockedDoor: 잠긴 출입구는 '숲 · 잠김'처럼 표시. rematch: 다시 싸울 수 있는 보스 칸은 '보스의 기억 · 다시 싸우기' (#251·#255)
        public void SetArea(FieldArea area, Func<Vector2Int, bool> lockedDoor = null, Func<Vector2Int, float> top = null, Func<Vector2Int, bool> rematch = null)
        {
            topOf = top;
            foreach (var tag in tags) UnityEngine.Object.Destroy(tag.Root.gameObject);
            tags.Clear();
            foreach (var cell in FieldInteraction.Landmarks(area.Map))
            {
                string name = area.LandmarkName(cell);
                if (string.IsNullOrEmpty(name)) continue;
                if (area.Map.Get(cell) == FieldTile.Door && lockedDoor != null && lockedDoor(cell)) name += " · 잠김";
                if (area.Map.Get(cell) == FieldTile.Boss && rematch != null && rematch(cell)) name += "의 기억 · 다시 싸우기";
                tags.Add(MakeTag(cell, name));
            }
        }

        // 걸음이 끝날 때마다: 가까운 이름표만 보이게. hidden = 지금은 감출 칸 (연 문 등)
        public void Refresh(Vector2Int player, Func<Vector2Int, bool> hidden)
        {
            foreach (var tag in tags)
                tag.Visible = FieldInteraction.IsNear(player, tag.Cell) && (hidden == null || !hidden(tag.Cell));
        }

        // 매 프레임: 카메라를 따라 자리 맞추기 + 서서히 나타나기/사라지기
        public void Tick(Camera cam, Canvas canvas, float deltaTime)
        {
            if (cam == null) return;
            var canvasCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var tag in tags)
            {
                float target = tag.Visible ? 1f : 0f;
                tag.Group.alpha = Mathf.MoveTowards(tag.Group.alpha, target, FadeSpeed * deltaTime);
                if (tag.Group.alpha <= 0f) continue;

                // 오브젝트 칸의 윗변 가운데 → 꼬리 끝이 거기에 오게
                var world = new Vector3(tag.Cell.x + 0.5f, tag.Cell.y + (topOf != null ? topOf(tag.Cell) : 1f), 0f);
                var screen = cam.WorldToScreenPoint(world);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, canvasCamera, out var local))
                    tag.Root.anchoredPosition = local + new Vector2(0f, PointerSize / 2f + 2f);
            }
        }

        private Tag MakeTag(Vector2Int cell, string name)
        {
            var image = UiKit.Pill(UiKit.Panel($"NameTag_{name}", layer, Palette.Scrim, 0.5f, 0.5f, 0.5f, 0.5f));
            image.raycastTarget = false;
            var root = image.rectTransform;
            root.pivot = new Vector2(0.5f, 0f);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = UiKit.Label("Text", root, name, 30, Palette.Gold, 0, 0, 1, 1);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow; // 줄 높이가 칸과 딱 맞으면 Truncate가 글자를 지워 버린다

            var pointer = UiKit.IconImage("Pointer", root, UiKit.PointerSprite(), 0.5f, 0f, 0.5f, 0f);
            pointer.color = Palette.Scrim;
            pointer.preserveAspect = false;
            pointer.rectTransform.pivot = new Vector2(0.5f, 1f);
            pointer.rectTransform.sizeDelta = new Vector2(PointerSize, PointerSize);
            pointer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return new Tag { Cell = cell, Root = root, Label = label, Group = group };
        }
    }
}
