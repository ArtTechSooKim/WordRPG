using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 단어 석판 문제 창 (#81, Figma '석판 문제 — 1/3 · 맞힘 · 다 채움 (#81)'): 어두운 막 위에 이끼 낀 돌판.
    // 머리줄(단어 석판 · 석판 n / 4 · ✕) · 진행 점(문제 n / 3) · 새겨진 칸(뜻 · 영단어 빈칸 · 물음) · 아래 2×2 선택지.
    // 틀리면 그 선택지만 빨갛게 흔들림(벌점 없음), 맞히면 빈칸에 금빛 영단어 + '정답!', 다 채우면 금빛 테두리 + [확인]
    public class TabletQuizView
    {
        private static readonly Color Stone = new Color32(0x58, 0x5C, 0x4E, 255);
        private static readonly Color StoneDark = new Color32(0x3B, 0x3E, 0x34, 255);
        private static readonly Color MossEdge = new Color32(0x86, 0xB0, 0x5E, 255);

        private GameObject root;
        private Image edge;
        private Text counter, progress, meaning, blank, ask, note;
        private readonly List<Image> dots = new List<Image>();
        private readonly List<Button> choices = new List<Button>();
        private Button close, done;
        private RectTransform stamp;
        private Action<int> onPick;
        private Action onClose;
        private int shaking = -1;
        private float shakeTime;
        private Vector2 shakeHome;

        public bool IsOpen => root != null && root.activeSelf;
        public string Meaning => IsOpen ? meaning.text : "";
        public string Blank => IsOpen ? blank.text : "";
        public string Counter => IsOpen ? counter.text : "";
        public string Progress => IsOpen ? progress.text : "";
        public string Note => IsOpen ? note.text : "";
        public bool StampVisible => IsOpen && stamp.gameObject.activeSelf;
        public bool IsComplete => IsOpen && done.gameObject.activeSelf;

        public static TabletQuizView Create(RectTransform parent)
        {
            var view = new TabletQuizView();
            var rect = UiKit.Stretch("TabletQuiz", parent);
            view.root = rect.gameObject;
            UiKit.Panel("Dim", rect, new Color(0f, 0f, 0f, 0.8f)).raycastTarget = true; // Linear라 Figma 62%보다 진하게

            // 돌판
            var slab = UiKit.RoundPanel("Slab", rect, Stone, UiKit.RadiusLg, 0.065f, 0.49f, 0.935f, 0.844f);
            view.edge = UiKit.Outline(UiKit.Panel("Edge", slab.transform, MossEdge), UiKit.RadiusLg, 6);
            view.edge.raycastTarget = false;
            var tag = UiKit.Pill(UiKit.Panel("Tag", slab.transform, Palette.Gold, 0.043f, 0.885f, 0.205f, 0.955f));
            tag.raycastTarget = false;
            UiKit.OneLine(UiKit.Label("Text", tag.transform, "단어 석판", 28, Palette.OnAccent, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            view.counter = UiKit.OneLine(UiKit.Label("Counter", slab.transform, "", 28, Palette.TextDim, 0.45f, 0.885f, 0.86f, 0.955f,
                TextAnchor.MiddleRight, FontStyle.Bold));
            var closeImage = UiKit.Pill(UiKit.Panel("TabletClose", slab.transform, StoneDark, 0.957f, 0.92f, 0.957f, 0.92f));
            closeImage.rectTransform.sizeDelta = new Vector2(64, 64);
            closeImage.rectTransform.anchoredPosition = new Vector2(-32, 0);
            view.close = UiKit.AddButton(closeImage);
            var x = UiKit.Label("X", closeImage.transform, "×", 30, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            x.font = UiFonts.Bold; // ✕(U+2715)는 Noto에도 없어 ×로 (#48·#93)
            view.close.onClick.AddListener(() => view.onClose?.Invoke());

            for (int i = 0; i < 3; i++)
            {
                var dot = UiKit.Pill(UiKit.Panel($"Dot_{i}", slab.transform, Palette.Gold, 0.043f + i * 0.04f, 0.825f, 0.043f + i * 0.04f, 0.825f));
                dot.rectTransform.sizeDelta = new Vector2(26, 26);
                dot.rectTransform.anchoredPosition = new Vector2(13, 0);
                dot.raycastTarget = false;
                view.dots.Add(dot);
            }
            view.progress = UiKit.OneLine(UiKit.Label("Progress", slab.transform, "", 26, Palette.TextDim, 0.18f, 0.8f, 0.6f, 0.85f,
                TextAnchor.MiddleLeft, FontStyle.Bold));

            var carved = UiKit.RoundPanel("Carved", slab.transform, StoneDark, UiKit.RadiusMd, 0.043f, 0.13f, 0.957f, 0.775f);
            carved.raycastTarget = false;
            view.meaning = Outlined(UiKit.Display(UiKit.Label("Meaning", carved.transform, "", 100, Palette.Text, 0.04f, 0.5f, 0.96f, 0.95f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 48)));
            view.blank = UiKit.Display(UiKit.Label("Blank", carved.transform, "", 68, Palette.Gold, 0.04f, 0.27f, 0.96f, 0.52f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 36));
            view.ask = UiKit.OneLine(UiKit.Label("Ask", carved.transform, "빈칸에 들어갈 영단어는?", 32, Palette.TextDim, 0.04f, 0.06f, 0.96f, 0.25f,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            view.note = UiKit.OneLine(UiKit.Label("Note", slab.transform, "", 26, Palette.TextDim, 0.043f, 0.02f, 0.957f, 0.11f,
                TextAnchor.MiddleLeft));

            // 정답 도장 (10° 기울임)
            view.stamp = UiKit.Rect("Stamp", rect, 0.82f, 0.7f, 0.82f, 0.7f);
            view.stamp.sizeDelta = new Vector2(280, 120);
            view.stamp.localEulerAngles = new Vector3(0, 0, 10);
            Outlined(UiKit.Display(UiKit.OneLine(UiKit.Label("Text", view.stamp, "정답!", 72, Palette.Good, 0, 0, 1, 1))));

            // 2×2 선택지 (Figma 'Answer Choice', 엄지 쪽)
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float minX = i % 2 == 0 ? 0.065f : 0.509f, maxY = i < 2 ? 0.245f : 0.156f;
                var button = UiKit.MakeButton($"TabletChoice_{i}", rect, "", Palette.Button, 42, minX, maxY - 0.078f, minX + 0.426f, maxY, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                var colors = button.colors;
                colors.disabledColor = Color.white; // 정답·오답·꺼짐 색이 흐려지지 않게
                button.colors = colors;
                button.onClick.AddListener(() => view.onPick?.Invoke(index));
                view.choices.Add(button);
            }
            view.done = UiKit.MakeButton("TabletDone", rect, "확인", Palette.Gold, 44, 0.222f, 0.125f, 0.778f, 0.1875f);
            UiKit.LabelOf(view.done).color = Palette.OnAccent;
            view.done.onClick.AddListener(() => view.onClose?.Invoke());

            view.root.SetActive(false);
            return view;
        }

        // tabletNumber: 지금 푸는 석판이 몇 번째인지 (푼 석판 수 + 1), total: 맵의 석판 수
        public void Show(int tabletNumber, int total, Action<int> pick, Action closed)
        {
            onPick = pick;
            onClose = closed;
            counter.text = $"석판 {tabletNumber} / {total}";
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void ShowQuestion(QuizQuestion question, int solved, string footnote)
        {
            meaning.text = question.Prompt;
            meaning.fontSize = 100;
            blank.text = BlankFor(question.CorrectAnswer);
            ask.text = "빈칸에 들어갈 영단어는?";
            note.text = footnote;
            SetProgress(solved, solved + 1);
            stamp.gameObject.SetActive(false);
            done.gameObject.SetActive(false);
            edge.color = MossEdge;
            for (int i = 0; i < choices.Count; i++)
            {
                bool used = i < question.Choices.Count;
                choices[i].gameObject.SetActive(used);
                if (!used) continue;
                UiKit.LabelOf(choices[i]).text = question.Choices[i];
                choices[i].image.color = Palette.Button;
                choices[i].interactable = true;
            }
        }

        // 틀린 선택지만 빨갛게 흔들고 꺼 둔다 (다른 것은 그대로 고를 수 있음)
        public void MarkWrong(int index)
        {
            if (index < 0 || index >= choices.Count) return;
            choices[index].image.color = Palette.Bad;
            choices[index].interactable = false;
            StopShake();
            shaking = index;
            shakeTime = 0.35f;
            shakeHome = choices[index].GetComponent<RectTransform>().anchoredPosition;
        }

        public void MarkCorrect(int index, string answer, int solved)
        {
            StopShake();
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].interactable = false;
                choices[i].image.color = i == index ? Palette.Good : Palette.Disabled;
            }
            if (index >= 0 && index < choices.Count) UiKit.LabelOf(choices[index]).text = "✓ " + answer;
            blank.text = answer;
            SetProgress(solved, solved);
            stamp.gameObject.SetActive(true);
        }

        // 다 채움: 금빛 테두리, 채운 단어들, 남은 석판·단어 수, [확인]
        public void ShowComplete(IReadOnlyList<string> filled, string footnote)
        {
            StopShake();
            meaning.text = "빈칸이 모두\n채워졌다!";
            meaning.fontSize = 84;
            blank.text = string.Join(" · ", filled);
            ask.text = "석판에 글자가 돌아와 금빛으로 빛난다";
            note.text = footnote;
            SetProgress(3, 3);
            edge.color = Palette.Gold;
            stamp.gameObject.SetActive(false);
            foreach (var choice in choices) choice.gameObject.SetActive(false);
            done.gameObject.SetActive(true);
        }

        public void Hide()
        {
            StopShake();
            root.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (shaking < 0) return;
            shakeTime -= dt;
            var rt = choices[shaking].GetComponent<RectTransform>();
            if (shakeTime <= 0f)
            {
                StopShake();
                return;
            }
            rt.anchoredPosition = shakeHome + new Vector2(Mathf.Sin(shakeTime * 60f) * 14f * (shakeTime / 0.35f), 0f);
        }

        public void Pick(int index) => onPick?.Invoke(index); // 테스트용

        private void StopShake()
        {
            if (shaking >= 0) choices[shaking].GetComponent<RectTransform>().anchoredPosition = shakeHome;
            shaking = -1;
        }

        private void SetProgress(int filled, int current)
        {
            for (int i = 0; i < dots.Count; i++)
            {
                bool full = i < filled;
                dots[i].color = full ? Palette.Gold : new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, i < current ? 0.85f : 0.3f);
            }
            progress.text = $"문제 {Mathf.Clamp(current, 1, 3)} / 3";
        }

        // 영단어 글자 수만큼 밑줄 (띄어쓰기는 그대로)
        public static string BlankFor(string english)
        {
            if (string.IsNullOrEmpty(english)) return "";
            var parts = new List<string>();
            foreach (char c in english) parts.Add(c == ' ' ? " " : "_");
            return string.Join(" ", parts);
        }

        private static Text Outlined(Text label)
        {
            var outline = label.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(3f, -3f);
            return label;
        }
    }
}
