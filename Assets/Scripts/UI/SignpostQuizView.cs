using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 이정표 문제 창 (#279, Figma '이정표 문제 — 뒤섞인 글자 · 바로잡음 (#279)'): 어두운 막 위 나무판.
    // 머리줄(뒤섞인 이정표 · 바로잡은 이정표 n / 5 · ×) · 이정표 판(뒤섞인 글자 · 물음 · 뜻) · 아래 2×2 철자 고르기.
    // 틀리면 그 보기만 빨갛게 흔들림(벌점 없음), 맞히면 바른 철자(금빛)가 톡 커지고 '이정표가 제자리를 찾았다!' + [확인]
    public class SignpostQuizView
    {
        private static readonly Color Wood = new Color32(58, 42, 28, 255);
        private static readonly Color Board = new Color32(40, 29, 20, 255);
        private static readonly Color Moss = new Color32(130, 180, 90, 255);
        private static readonly Color TagWood = new Color32(226, 178, 106, 255);
        private static readonly Color Cream = new Color32(243, 226, 192, 255);
        private static readonly Color AskDim = new Color32(214, 196, 160, 255);
        private static readonly Color Leaf = new Color32(150, 220, 110, 255);

        private GameObject root;
        private Image edge;
        private Text counter, word, ask, meaning, note;
        private Outline glow;
        private readonly List<Button> choices = new List<Button>();
        private Button done;
        private Action<int> onPick;
        private Action onClose;
        private int shaking = -1;
        private float shakeTime, time, pop;
        private Vector2 shakeHome;
        private bool fixedNow;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsFixed => IsOpen && fixedNow;          // 맞혀서 바로잡은 모습 (테스트용)
        public string Word => IsOpen ? word.text : "";
        public string Meaning => IsOpen ? meaning.text : "";
        public string Counter => IsOpen ? counter.text : "";
        public string Note => IsOpen ? note.text : "";
        public string ChoiceText(int index) => index >= 0 && index < choices.Count ? UiKit.LabelOf(choices[index]).text : "";
        public bool ChoiceEnabled(int index) => index >= 0 && index < choices.Count && choices[index].gameObject.activeSelf && choices[index].interactable;

        public static SignpostQuizView Create(RectTransform parent)
        {
            var view = new SignpostQuizView();
            var rect = UiKit.Stretch("SignpostQuiz", parent);
            view.root = rect.gameObject;
            UiKit.Panel("Dim", rect, new Color(0f, 0f, 0f, 0.72f)).raycastTarget = true; // Linear라 Figma보다 진하게

            var slab = UiKit.RoundPanel("Slab", rect, Wood, UiKit.RadiusLg, 0.065f, 0.52f, 0.935f, 0.844f);
            view.edge = UiKit.Outline(UiKit.Panel("Edge", slab.transform, Moss), UiKit.RadiusLg, 6);
            view.edge.raycastTarget = false;
            var tag = UiKit.Pill(UiKit.Panel("Tag", slab.transform, TagWood, 0.043f, 0.845f, 0.27f, 0.93f));
            tag.raycastTarget = false;
            UiKit.OneLine(UiKit.Label("Text", tag.transform, "뒤섞인 이정표", 28, Palette.OnAccent, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            view.counter = UiKit.OneLine(UiKit.Label("Counter", slab.transform, "", 28, Palette.TextDim, 0.45f, 0.845f, 0.86f, 0.93f,
                TextAnchor.MiddleRight, FontStyle.Bold));
            var closeImage = UiKit.Pill(UiKit.Panel("SignpostClose", slab.transform, new Color32(74, 56, 40, 255), 0.957f, 0.888f, 0.957f, 0.888f));
            closeImage.rectTransform.sizeDelta = new Vector2(64, 64);
            closeImage.rectTransform.anchoredPosition = new Vector2(-32, 0);
            var close = UiKit.AddButton(closeImage);
            var x = UiKit.Label("X", closeImage.transform, "×", 30, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            x.font = UiFonts.Bold; // ✕(U+2715)는 Noto에도 없어 ×로 (#48·#93)
            close.onClick.AddListener(() => view.onClose?.Invoke());

            var board = UiKit.RoundPanel("Board", slab.transform, Board, UiKit.RadiusMd, 0.043f, 0.156f, 0.957f, 0.79f);
            board.raycastTarget = false;
            view.word = UiKit.Display(UiKit.Label("Word", board.transform, "", 96, Cream, 0.04f, 0.5f, 0.96f, 0.96f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 40));
            view.glow = view.word.gameObject.AddComponent<Outline>(); // 바로잡으면 글자 둘레 금빛
            view.glow.effectDistance = new Vector2(4f, -4f);
            view.ask = UiKit.OneLine(UiKit.Label("Ask", board.transform, "", 32, AskDim, 0.04f, 0.33f, 0.96f, 0.5f,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            view.meaning = UiKit.Display(UiKit.Label("Meaning", board.transform, "", 52, Palette.Gold, 0.04f, 0.05f, 0.96f, 0.33f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 28));
            view.note = UiKit.OneLine(UiKit.Label("Note", slab.transform, "", 26, Palette.TextDim, 0.043f, 0.03f, 0.957f, 0.12f,
                TextAnchor.MiddleLeft));

            // 2×2 철자 고르기 (Figma 'Answer Choice', 엄지 쪽 — 석판·반딧돌 문제와 같은 자리). 철자가 또렷하게 본문 굵은 글꼴
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float minX = i % 2 == 0 ? 0.065f : 0.509f, maxY = i < 2 ? 0.245f : 0.156f;
                var button = UiKit.MakeButton($"SignpostChoice_{i}", rect, "", Palette.Button, 44, minX, maxY - 0.078f, minX + 0.426f, maxY, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                var colors = button.colors;
                colors.disabledColor = Color.white;
                button.colors = colors;
                button.onClick.AddListener(() => view.onPick?.Invoke(index));
                view.choices.Add(button);
            }
            view.done = UiKit.MakeButton("SignpostDone", rect, "확인", Palette.Gold, 44, 0.222f, 0.125f, 0.778f, 0.1875f);
            UiKit.LabelOf(view.done).color = Palette.OnAccent;
            view.done.onClick.AddListener(() => view.onClose?.Invoke());

            view.root.SetActive(false);
            return view;
        }

        // fixedCount: 지금까지 바로잡은 이정표 수, total: 맵의 이정표 수
        public void Show(SpellingQuestion question, int fixedCount, int total, Action<int> pick, Action closed)
        {
            onPick = pick;
            onClose = closed;
            fixedNow = false;
            time = 0f;
            pop = 0f;
            counter.text = $"바로잡은 이정표 {fixedCount} / {total}";
            word.text = SpellingQuiz.Spaced(question.Scrambled);
            word.color = Cream;
            word.rectTransform.localScale = Vector3.one;
            ask.text = "뒤섞인 글자 — 바른 철자는?";
            ask.color = AskDim;
            meaning.text = question.Meaning;
            note.text = "틀려도 괜찮아요 — 다시 고르면 돼요";
            edge.color = Moss;
            done.gameObject.SetActive(false);
            for (int i = 0; i < choices.Count; i++)
            {
                bool used = i < question.Choices.Count;
                choices[i].gameObject.SetActive(used);
                if (!used) continue;
                UiKit.LabelOf(choices[i]).text = question.Choices[i];
                choices[i].image.color = Palette.Button;
                choices[i].interactable = true;
            }
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        // 틀린 보기만 빨갛게 흔들고 꺼 둔다 (다른 것은 그대로 고를 수 있음)
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

        // 맞힘: 뒤섞인 글자가 바른 철자(금빛)로 톡 + '이정표가 제자리를 찾았다!' → 보기 대신 [확인]
        public void ShowFixed(string answer, int fixedCount, int total)
        {
            StopShake();
            fixedNow = true;
            pop = 1f;
            counter.text = $"바로잡은 이정표 {fixedCount} / {total}";
            word.text = answer;
            word.color = Palette.Gold;
            ask.text = "이정표가 제자리를 찾았다!";
            ask.color = Leaf;
            note.text = fixedCount >= total ? "숲의 이정표를 모두 바로잡았다!" : "헷갈 안개가 걷혀 길이 열려요";
            edge.color = Palette.Gold;
            foreach (var choice in choices) choice.gameObject.SetActive(false);
            done.gameObject.SetActive(true);
        }

        public void Hide()
        {
            StopShake();
            root.SetActive(false);
        }

        // 바로잡으면 글자가 톡 커졌다 돌아오고 둘레가 금빛으로 숨 쉬듯, 틀린 보기 흔들기
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            time += dt;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
            var gold = Palette.Gold;
            glow.effectColor = fixedNow ? new Color(gold.r, gold.g, gold.b, 0.45f + 0.25f * pulse) : new Color(0f, 0f, 0f, 0.35f);
            if (pop > 0f)
            {
                pop = Mathf.Max(0f, pop - dt * 3f);
                word.rectTransform.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(pop * Mathf.PI));
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
        public void Close() => onClose?.Invoke();            // 테스트용 ([확인]·×)

        private void StopShake()
        {
            if (shaking >= 0) choices[shaking].GetComponent<RectTransform>().anchoredPosition = shakeHome;
            shaking = -1;
        }
    }
}
