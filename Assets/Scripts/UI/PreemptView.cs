using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // [선제공격] 문제 창 (#73, GDD 9-5): 화면 아래 '선제공격!' + 영단어 + 4지선다 + 시간 막대 (전투 문제와 같은 모양).
    // 그동안 필드 몬스터는 느려진다(FieldScreen). 고르면 정답·오답 색을 잠깐 보여 준 뒤 결과를 알린다 (-1 = 시간 초과).
    // 단어는 이미 본 단어만 (Preemptive.QuizBooks — 사용자 요청). Figma 연결이 끊겨 UI 키트 모양대로 만들고 나중에 Figma에 옮김
    public class PreemptView
    {
        private const float ResultSeconds = 0.45f;

        private GameObject root;
        private Text word, hint;
        private RectTransform timerFill;
        private Image timerFillImage;
        private readonly List<Button> choices = new List<Button>();
        private QuizQuestion question;
        private Action<int> answered;
        private float limit, elapsed, resultTimer;
        private int picked = int.MinValue;

        public bool IsOpen => root != null && root.activeSelf;
        public QuizQuestion Question => question;
        public string WordText => IsOpen ? word.text : "";

        public static PreemptView Create(RectTransform parent)
        {
            var view = new PreemptView();
            view.root = UiKit.Stretch("PreemptView", parent).gameObject;
            var dim = UiKit.Panel("Dim", view.root.transform, new Color(0f, 0f, 0f, 0.35f));
            dim.raycastTarget = true; // 필드를 누르지 못하게

            var card = UiKit.RoundPanel("Card", view.root.transform, Palette.Panel, UiKit.RadiusLg, 0.03f, 0.03f, 0.97f, 0.56f);
            var tag = UiKit.Pill(UiKit.Panel("Tag", card.transform, Palette.Attack, 0.3f, 0.9f, 0.7f, 0.98f));
            UiKit.Display(UiKit.OneLine(UiKit.Label("Label", tag.transform, "선제공격!", 44, Palette.Text, 0, 0, 1, 1)));
            view.word = UiKit.Display(UiKit.Label("Word", card.transform, "", 84, Palette.Text, 0.04f, 0.72f, 0.96f, 0.88f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 30));
            view.hint = UiKit.OneLine(UiKit.Label("Hint", card.transform, "", 30, Palette.TextDim, 0.04f, 0.67f, 0.96f, 0.72f));
            var timerBack = UiKit.Pill(UiKit.Panel("TimerBack", card.transform, Palette.Track, 0.05f, 0.63f, 0.95f, 0.655f));
            view.timerFillImage = UiKit.Pill(UiKit.Panel("TimerFill", timerBack.transform, Palette.Attack));
            view.timerFill = view.timerFillImage.rectTransform;

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float top = 0.6f - i * 0.145f;
                var button = UiKit.MakeButton($"PreemptChoice_{i}", card.transform, "", Palette.Button, 40, 0.04f, top - 0.13f, 0.96f, top,
                    bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                var colors = button.colors;
                colors.disabledColor = Color.white; // 정답/오답 색이 흐려지지 않게
                button.colors = colors;
                button.onClick.AddListener(() => view.Pick(index));
                view.choices.Add(button);
            }
            view.root.SetActive(false);
            return view;
        }

        public void Show(QuizQuestion quiz, float seconds, Action<int> onAnswered)
        {
            question = quiz;
            answered = onAnswered;
            limit = Mathf.Max(0.5f, seconds);
            elapsed = 0f;
            resultTimer = 0f;
            picked = int.MinValue;
            word.text = quiz.Prompt;
            hint.text = "뜻을 맞히면 먼저 한 방!";
            for (int i = 0; i < choices.Count; i++)
            {
                bool has = i < quiz.Choices.Count;
                choices[i].gameObject.SetActive(has);
                if (!has) continue;
                UiKit.LabelOf(choices[i]).text = quiz.Choices[i];
                choices[i].image.color = Palette.Button;
                choices[i].interactable = true;
            }
            timerFill.anchorMax = new Vector2(1, 1);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        // FieldScreen.Update에서 (실제 시간으로 — 느려지는 것은 몬스터뿐)
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            if (picked != int.MinValue)
            {
                resultTimer -= dt;
                if (resultTimer <= 0f) Finish();
                return;
            }
            elapsed += dt;
            timerFill.anchorMax = new Vector2(Mathf.Clamp01(1f - elapsed / limit), 1);
            if (elapsed >= limit) Pick(-1);
        }

        public void Pick(int index)
        {
            if (!IsOpen || picked != int.MinValue) return;
            picked = index;
            bool correct = question.IsCorrect(index);
            Sound.Play(correct ? Sfx.Correct : Sfx.Wrong);
            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].interactable = false;
                if (i == question.CorrectIndex) choices[i].image.color = Palette.Good;
                else if (i == index) choices[i].image.color = Palette.Bad;
            }
            hint.text = correct ? "정답! 선제공격 성공" : index < 0 ? "시간 초과…" : "아쉽다! 그냥 싸우자";
            resultTimer = ResultSeconds;
        }

        private void Finish()
        {
            root.SetActive(false);
            var callback = answered;
            answered = null;
            callback?.Invoke(picked);
        }
    }
}
