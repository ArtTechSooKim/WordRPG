using System;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 수련 창 (#44, Figma 'Field — 수련 (#44)'): 도감 속 단어(이미 발견한 단어)로 허수아비와 싸우며 외운다.
    // [전체적 암기] = 발견한 단어를 골고루, [오답 위주 암기] = 오답 노트부터. 고르면 창을 닫고 onStart(방식)
    public class TrainingView
    {
        private const float PanelWidth = 960f;

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public string InfoText => info.text;

        private RectTransform panel;
        private Text info;
        private Button all, wrong;
        private Text allDetail, wrongDetail;
        private Action<TrainingMode> onStart;

        public static TrainingView Create(Transform parent, Action<TrainingMode> started)
        {
            var view = new TrainingView { onStart = started };
            var root = UiKit.Panel("TrainingView", parent, Palette.Scrim); // 뒤쪽은 눌리지 않게
            view.Root = root.gameObject;

            var panelImage = UiKit.RoundPanel("Panel", root.transform, Palette.Panel, UiKit.RadiusLg, 0.5f, 0.5f, 0.5f, 0.5f);
            view.panel = panelImage.rectTransform;

            float y = 48f;
            var title = UiKit.Display(UiKit.OneLine(UiKit.Label("Title", view.panel, "수련", 80, Palette.Gold, 0, 1, 1, 1)));
            Place(title.rectTransform, ref y, 96f, 8f);
            var subtitle = UiKit.OneLine(UiKit.Label("Subtitle", view.panel, "도감 속 단어를 암기해보아요", 38, Palette.Text, 0, 1, 1, 1));
            Place(subtitle.rectTransform, ref y, 54f, 8f);
            view.info = UiKit.OneLine(UiKit.Label("Info", view.panel, "", 30, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleCenter,
                FontStyle.Bold));
            Place(view.info.rectTransform, ref y, 44f, 24f);

            view.all = Option(view.panel, "TrainingAll", "전체적 암기", Palette.Button, out view.allDetail, ref y);
            view.all.onClick.AddListener(() => view.Start(TrainingMode.All));
            view.wrong = Option(view.panel, "TrainingWrong", "오답 위주 암기", Palette.Confirm, out view.wrongDetail, ref y);
            view.wrong.onClick.AddListener(() => view.Start(TrainingMode.WrongNote));

            y += 4f;
            var note = UiKit.Label("Note", view.panel,
                "허수아비는 쓰러지지 않고 공격도 하지 않아요.\n피해는 실제 전투와 같아요. 새 단어는 나오지 않아요.\n그만하려면 기술 고르기에서 [수련 종료]",
                28, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleCenter, FontStyle.Normal, true, 18);
            Place(note.rectTransform, ref y, 130f, 24f);

            var close = UiKit.MakeButton("TrainingClose", view.panel, "닫기", Palette.Neutral, 44, 0, 1, 1, 1);
            close.onClick.AddListener(view.Hide);
            Place((RectTransform)close.transform, ref y, 110f, 48f);

            view.panel.sizeDelta = new Vector2(PanelWidth, y);
            view.Root.SetActive(false);
            return view;
        }

        // 두 줄 큰 버튼: 위 = 방식(Jua), 아래 = 설명
        private static Button Option(RectTransform parent, string name, string head, Color color, out Text detail, ref float y)
        {
            var image = UiKit.RoundPanel(name, parent, color, UiKit.RadiusMd, 0, 1, 1, 1);
            var button = UiKit.AddButton(image);
            var colors = button.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.6f);
            button.colors = colors;
            var title = UiKit.Display(UiKit.OneLine(UiKit.Label("Head", image.transform, head, 56, Palette.Text, 0, 0.45f, 1, 0.92f,
                TextAnchor.MiddleLeft)));
            title.rectTransform.offsetMin = new Vector2(36, 0);
            detail = UiKit.OneLine(UiKit.Label("Detail", image.transform, "", 30, new Color(1, 1, 1, 0.85f), 0, 0.1f, 1, 0.45f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 20));
            detail.rectTransform.offsetMin = new Vector2(36, 0);
            detail.rectTransform.offsetMax = new Vector2(-24, 0);
            Place(image.rectTransform, ref y, 170f, 20f);
            return button;
        }

        // discovered = 발견한 단어 수, wrongNote = 오답 노트 단어 수 (둘 다 0이면 수련할 단어가 없음)
        public void Show(int discovered, int wrongNote)
        {
            info.text = $"발견한 단어 {discovered}개   ·   오답 노트 {wrongNote}개";
            all.interactable = discovered > 0;
            allDetail.text = discovered > 0 ? $"발견한 단어 {discovered}개를 골고루 — 한 바퀴씩 돌아가며" : "아직 발견한 단어가 없어요 — 모험에서 단어를 만나 보세요";
            wrong.interactable = wrongNote > 0;
            wrongDetail.text = wrongNote > 0 ? $"오답 노트 {wrongNote}개를 먼저, 다 맞히면 약한 단어로" : "오답 노트가 비어 있어요";
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide() => Root.SetActive(false);

        private void Start(TrainingMode mode)
        {
            Hide();
            onStart?.Invoke(mode);
        }

        // 판 안: 위에서 y만큼 내려와 높이 h (좌우 44 여백)
        private static void Place(RectTransform rect, ref float y, float h, float gap)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(44, -(y + h));
            rect.offsetMax = new Vector2(-44, -y);
            y += h + gap;
        }
    }
}
