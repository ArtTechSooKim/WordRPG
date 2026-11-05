using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 어느 샘으로 갈지 고르는 창 (#216, Figma 'Field — 샘: 지역 고르기 (#216)'): 지역 버튼 + [취소]. 지역 수에 따라 창 높이가 변함
    public class SpringWarpView
    {
        private const float PanelWidth = 936f, Padding = 44f, TitleHeight = 80f, RowHeight = 102f, Gap = 20f;

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        private RectTransform panel, title;
        private Transform panelTransform;
        private Button cancel;
        private readonly List<Button> rows = new List<Button>();
        private Action<FieldArea> onChosen;

        public static SpringWarpView Create(Transform parent)
        {
            var view = new SpringWarpView();
            var root = UiKit.Panel("SpringWarpView", parent, Palette.Scrim); // 뒤쪽 버튼을 막는다
            view.Root = root.gameObject;
            var image = UiKit.RoundPanel("Panel", root.transform, Palette.PanelLight, UiKit.RadiusLg, 0.5f, 0.5f, 0.5f, 0.5f);
            view.panel = image.rectTransform;
            view.panelTransform = image.transform;
            view.title = UiKit.Display(UiKit.Label("Title", image.transform, "어느 샘으로 갈까요?", 52, Palette.Gold, 0, 0, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 28)).rectTransform;
            view.cancel = UiKit.MakeButton("SpringWarpCancel", image.transform, "취소", Palette.Neutral, 44, 0, 0, 1, 1);
            view.cancel.onClick.AddListener(view.Hide);
            view.Root.SetActive(false);
            return view;
        }

        public void Show(IReadOnlyList<FieldArea> destinations, Action<FieldArea> chosen)
        {
            onChosen = chosen;
            float height = Padding * 2f + TitleHeight + Gap + (destinations.Count + 1) * RowHeight + destinations.Count * Gap;
            panel.sizeDelta = new Vector2(PanelWidth, height);

            float top = Padding;
            Place(title, top, TitleHeight, height);
            top += TitleHeight + Gap;
            for (int i = 0; i < destinations.Count; i++)
            {
                if (i == rows.Count) rows.Add(UiKit.MakeButton("SpringWarpRow", panelTransform, "", Palette.Button, 44, 0, 0, 1, 1));
                var row = rows[i];
                var target = destinations[i];
                row.name = $"SpringWarp_{target.AreaId}";
                UiKit.LabelOf(row).text = target.DisplayName;
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() => Choose(target));
                row.gameObject.SetActive(true);
                Place((RectTransform)row.transform, top, RowHeight, height);
                top += RowHeight + Gap;
            }
            for (int i = destinations.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            Place((RectTransform)cancel.transform, top, RowHeight, height);

            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide()
        {
            onChosen = null;
            Root.SetActive(false);
        }

        private void Choose(FieldArea target)
        {
            var chosen = onChosen;
            Hide();
            chosen?.Invoke(target);
        }

        // 창 위에서 top만큼 내려온 자리에 좌우 여백을 두고 놓음
        private static void Place(RectTransform rect, float top, float rowHeight, float panelHeight)
        {
            float side = Padding / PanelWidth;
            rect.anchorMin = new Vector2(side, 1f - (top + rowHeight) / panelHeight);
            rect.anchorMax = new Vector2(1f - side, 1f - top / panelHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
