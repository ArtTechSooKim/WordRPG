using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 힐끔해골 확인문제 창 (#281, Figma '힐끔해골 확인문제 (#281)'): 어두운 막 위 보랏빛 판.
    // 머리줄(얼굴 · 힐끔해골 · 혼잣말 · 받은 골드 · ×) · 진행 점(문제 n / 3) · 문제 카드(문제 글 · 맞히면 정답 · 어느 장) · 아래 보기 한 줄씩(4~5개).
    // 틀리면 그 보기만 빨갛게 흔들림(벌점 없음), 맞히면 '+20G'(처음 바로 맞힌 문제만) 또는 '정답!' 도장, 3문제를 마치면 [확인]
    public class SkullQuizView
    {
        private static readonly Color Slab = new Color32(42, 37, 54, 255);
        private static readonly Color CardColor = new Color32(28, 24, 38, 255);
        private static readonly Color Lavender = new Color32(185, 168, 216, 255);
        private static readonly Color Bone = new Color32(233, 228, 214, 255);

        private GameObject root;
        private Image edge;
        private Text line, gold, progress, question, answer, tag, note, stampText;
        private readonly List<Image> dots = new List<Image>();
        private readonly List<Button> choices = new List<Button>();
        private Button done;
        private RectTransform stamp;
        private Action<int> onPick;
        private Action onClose;
        private int shaking = -1;
        private float shakeTime, stampTime;
        private Vector2 shakeHome;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsComplete => IsOpen && done.gameObject.activeSelf;
        public string Question => IsOpen ? question.text : "";
        public string Answer => IsOpen ? answer.text : "";
        public string Line => IsOpen ? line.text : "";
        public string Gold => IsOpen ? gold.text : "";
        public string Tag => IsOpen ? tag.text : "";
        public string Stamp => IsOpen && stamp.gameObject.activeSelf ? stampText.text : "";
        public int ChoiceCount { get { int n = 0; foreach (var c in choices) if (c.gameObject.activeSelf) n++; return n; } }
        public string ChoiceText(int index) => UiKit.LabelOf(choices[index]).text;
        public bool ChoiceEnabled(int index) => choices[index].interactable;

        public static SkullQuizView Create(RectTransform parent)
        {
            var view = new SkullQuizView();
            var rect = UiKit.Stretch("SkullQuiz", parent);
            view.root = rect.gameObject;
            UiKit.Panel("Dim", rect, new Color(0f, 0f, 0f, 0.8f)).raycastTarget = true; // Linear라 Figma보다 진하게

            var slab = UiKit.RoundPanel("Slab", rect, Slab, UiKit.RadiusLg, 0.065f, 0.49f, 0.935f, 0.86f);
            view.edge = UiKit.Outline(UiKit.Panel("Edge", slab.transform, Lavender), UiKit.RadiusLg, 5);
            view.edge.raycastTarget = false;

            // 머리줄: 얼굴(팩 Skeleton Faceset) · 이름 · 혼잣말 · 받은 골드 · ×
            var face = UiKit.IconImage("Face", slab.transform, Resources.Load<Sprite>("Art/NinjaAdventure/Npcs/" + ExamSkull.ArtId + "_face"), 0, 1, 0, 1);
            face.rectTransform.pivot = new Vector2(0, 1);
            face.rectTransform.sizeDelta = new Vector2(100, 100);
            face.rectTransform.anchoredPosition = new Vector2(40, -24);
            face.raycastTarget = false;
            UiKit.Display(UiKit.OneLine(UiKit.Label("Name", slab.transform, ExamSkull.DisplayName, 40, Bone, 0.18f, 0.86f, 0.6f, 0.95f,
                TextAnchor.MiddleLeft)));
            view.line = UiKit.Label("Line", slab.transform, "", 30, Lavender, 0.18f, 0.775f, 0.95f, 0.865f, TextAnchor.UpperLeft, FontStyle.Normal, true, 20);
            view.gold = UiKit.OneLine(UiKit.Label("Gold", slab.transform, "", 28, Palette.TextDim, 0.55f, 0.885f, 0.86f, 0.955f,
                TextAnchor.MiddleRight, FontStyle.Bold));
            var closeImage = UiKit.Pill(UiKit.Panel("SkullClose", slab.transform, CardColor, 0.957f, 0.92f, 0.957f, 0.92f));
            closeImage.rectTransform.sizeDelta = new Vector2(64, 64);
            closeImage.rectTransform.anchoredPosition = new Vector2(-32, 0);
            var close = UiKit.AddButton(closeImage);
            var x = UiKit.Label("X", closeImage.transform, "×", 30, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            x.font = UiFonts.Bold; // ✕는 Noto에도 없어 ×로
            close.onClick.AddListener(() => view.onClose?.Invoke());

            for (int i = 0; i < ExamSkull.QuestionsPerVisit; i++)
            {
                var dot = UiKit.Pill(UiKit.Panel($"Dot_{i}", slab.transform, Palette.Gold, 0.043f + i * 0.04f, 0.735f, 0.043f + i * 0.04f, 0.735f));
                dot.rectTransform.sizeDelta = new Vector2(26, 26);
                dot.rectTransform.anchoredPosition = new Vector2(13, 0);
                dot.raycastTarget = false;
                view.dots.Add(dot);
            }
            view.progress = UiKit.OneLine(UiKit.Label("Progress", slab.transform, "", 26, Palette.TextDim, 0.18f, 0.71f, 0.6f, 0.76f,
                TextAnchor.MiddleLeft, FontStyle.Bold));

            var card = UiKit.RoundPanel("Card", slab.transform, CardColor, UiKit.RadiusMd, 0.043f, 0.13f, 0.957f, 0.69f);
            card.raycastTarget = false;
            view.question = UiKit.Label("Question", card.transform, "", 42, Palette.Text, 0.04f, 0.33f, 0.96f, 0.95f,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 22);
            view.answer = UiKit.Label("Answer", card.transform, "", 44, Palette.Gold, 0.04f, 0.18f, 0.96f, 0.34f,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 22);
            view.tag = UiKit.OneLine(UiKit.Label("Tag", card.transform, "", 30, Lavender, 0.04f, 0.04f, 0.96f, 0.18f,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            view.note = UiKit.OneLine(UiKit.Label("Note", slab.transform, "", 26, Palette.TextDim, 0.043f, 0.02f, 0.957f, 0.11f,
                TextAnchor.MiddleLeft));

            // 도장 (+20G 또는 정답!, 10° 기울임)
            view.stamp = UiKit.Rect("Stamp", rect, 0.8f, 0.72f, 0.8f, 0.72f);
            view.stamp.sizeDelta = new Vector2(300, 120);
            view.stamp.localEulerAngles = new Vector3(0, 0, 10);
            view.stampText = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", view.stamp, "", 72, Palette.Gold, 0, 0, 1, 1)));
            var outline = view.stampText.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            outline.effectDistance = new Vector2(3f, -3f);

            // 보기 한 줄씩 (4~5개, Figma 'Answer Choice')
            for (int i = 0; i < ExamLayout.MaxChoices; i++)
            {
                int index = i;
                var button = UiKit.MakeButton($"SkullChoice_{i}", rect, "", Palette.Button, 40, 0.065f, 0.4f, 0.935f, 0.47f, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                var colors = button.colors;
                colors.disabledColor = Color.white; // 정답·오답·꺼짐 색이 흐려지지 않게
                button.colors = colors;
                button.onClick.AddListener(() => view.onPick?.Invoke(index));
                view.choices.Add(button);
            }
            view.done = UiKit.MakeButton("SkullDone", rect, "확인", Palette.Gold, 44, 0.222f, 0.125f, 0.778f, 0.1875f);
            UiKit.LabelOf(view.done).color = Palette.OnAccent;
            view.done.onClick.AddListener(() => view.onClose?.Invoke());

            view.root.SetActive(false);
            return view;
        }

        public void Show(Action<int> pick, Action closed)
        {
            onPick = pick;
            onClose = closed;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void ShowQuestion(QuizQuestion quiz, int index, int count, int goldEarned, string say)
        {
            StopShake();
            question.text = quiz.Prompt;
            answer.text = "";
            tag.text = ExamLayout.Tag(quiz.Exam);
            line.text = say;
            gold.text = goldEarned > 0 ? $"받은 골드 +{goldEarned}G" : "";
            note.text = $"처음 바로 맞힌 문제마다 {ExamSkull.GoldPerQuestion}G · 틀려도 다시 고르면 돼요";
            SetProgress(index, index + 1, count);
            stamp.gameObject.SetActive(false);
            done.gameObject.SetActive(false);
            edge.color = Lavender;
            ExamLayout.Column(choices, quiz.Choices.Count, 0.065f, 0.935f, 0.075f, 0.47f, 0.012f);
            for (int i = 0; i < choices.Count; i++)
            {
                bool used = i < quiz.Choices.Count;
                choices[i].gameObject.SetActive(used);
                if (!used) continue;
                UiKit.LabelOf(choices[i]).text = quiz.Choices[i];
                choices[i].image.color = Palette.Button;
                choices[i].interactable = true;
            }
        }

        // 틀린 보기만 빨갛게 흔들고 꺼 둔다 (다른 것은 그대로 고를 수 있음)
        public void MarkWrong(int index, string say)
        {
            if (index < 0 || index >= choices.Count) return;
            choices[index].image.color = Palette.Bad;
            choices[index].interactable = false;
            line.text = say;
            StopShake();
            shaking = index;
            shakeTime = 0.35f;
            shakeHome = choices[index].GetComponent<RectTransform>().anchoredPosition;
        }

        public void MarkCorrect(int index, string correctAnswer, int goldNow, int goldEarned, string say)
        {
            StopShake();
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].interactable = false;
                choices[i].image.color = i == index ? Palette.Good : Palette.Disabled;
            }
            answer.text = correctAnswer;
            line.text = say;
            gold.text = goldEarned > 0 ? $"받은 골드 +{goldEarned}G" : "";
            stampText.text = goldNow > 0 ? $"+{goldNow}G" : "정답!";
            stampText.color = goldNow > 0 ? Palette.Gold : Palette.Good;
            stamp.gameObject.SetActive(true);
            stampTime = 0f;
        }

        // 3문제를 마침: 바로 맞힌 수 · 받은 골드 · [확인]
        public void ShowComplete(int firstTry, int count, int goldEarned, string say)
        {
            StopShake();
            question.text = $"바로 맞힌 문제 {firstTry} / {count}";
            answer.text = goldEarned > 0 ? $"+{goldEarned}G" : "";
            tag.text = goldEarned > 0 ? "처음 맞힌 문제의 골드를 받았다" : "골드는 처음 바로 맞힌 문제에만";
            line.text = say;
            gold.text = goldEarned > 0 ? $"받은 골드 +{goldEarned}G" : "";
            SetProgress(count, count, count);
            stamp.gameObject.SetActive(false);
            edge.color = Palette.Gold;
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
            if (!IsOpen) return;
            if (stamp.gameObject.activeSelf && stampTime < 0.3f)
            {
                stampTime += dt;
                float pop = Mathf.Clamp01(stampTime / 0.3f);
                stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, pop);
            }
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

        private void SetProgress(int filled, int current, int count)
        {
            for (int i = 0; i < dots.Count; i++)
            {
                dots[i].gameObject.SetActive(i < count);
                bool full = i < filled;
                dots[i].color = full ? Palette.Gold : new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, i < current ? 0.85f : 0.3f);
            }
            progress.text = $"문제 {Mathf.Clamp(current, 1, count)} / {count}";
        }
    }
}
