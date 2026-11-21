using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 글자 반딧돌 문제 창 (#113, Figma '반딧돌 문제 — 떠오른 글자 · 밝아짐 (#111)'): 어두운 막 위 짙은 남색 돌판.
    // 머리줄(글자 반딧돌 · 밝힌 반딧돌 n / 8 · ×) · 돌 칸(희미하게 빛나는 영단어 · 물음) · 아래 2×2 뜻 고르기 (영→한 — 석판과 반대).
    // 틀리면 그 선택지만 빨갛게 흔들림(벌점 없음), 맞히면 글자가 환해지고 뜻(금빛) + '반딧돌이 밝아졌다!' + [확인]
    public class GlowStoneQuizView
    {
        private static readonly Color Slab = new Color32(30, 36, 52, 255);
        private static readonly Color StoneDark = new Color32(18, 22, 36, 255);
        private static readonly Color Edge = new Color32(90, 170, 210, 255);
        private static readonly Color Cyan = new Color32(120, 220, 255, 255);
        private static readonly Color FaintWord = new Color(150 / 255f, 220 / 255f, 240 / 255f, 0.5f);
        private static readonly Color BrightWord = new Color32(232, 250, 255, 255);

        private GameObject root;
        private Image edge;
        private Text counter, word, meaning, ask, note;
        private Outline glow;
        private readonly List<Button> choices = new List<Button>();
        private Button done;
        private Action<int> onPick;
        private Action onClose;
        private int shaking = -1;
        private float shakeTime, time;
        private Vector2 shakeHome;
        private bool lit;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsLit => IsOpen && lit;               // 맞혀서 밝아진 모습 (테스트용)
        public string Word => IsOpen ? word.text : "";
        public string Meaning => IsOpen ? meaning.text : "";
        public string Counter => IsOpen ? counter.text : "";
        public string Note => IsOpen ? note.text : "";

        public static GlowStoneQuizView Create(RectTransform parent)
        {
            var view = new GlowStoneQuizView();
            var rect = UiKit.Stretch("GlowStoneQuiz", parent);
            view.root = rect.gameObject;
            UiKit.Panel("Dim", rect, new Color(0f, 0f, 0f, 0.78f)).raycastTarget = true; // Linear라 Figma 62%보다 진하게

            var slab = UiKit.RoundPanel("Slab", rect, Slab, UiKit.RadiusLg, 0.065f, 0.52f, 0.935f, 0.844f);
            view.edge = UiKit.Outline(UiKit.Panel("Edge", slab.transform, Edge), UiKit.RadiusLg, 6);
            view.edge.raycastTarget = false;
            var tag = UiKit.Pill(UiKit.Panel("Tag", slab.transform, Cyan, 0.043f, 0.845f, 0.235f, 0.93f));
            tag.raycastTarget = false;
            UiKit.OneLine(UiKit.Label("Text", tag.transform, "글자 반딧돌", 28, Palette.OnAccent, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            view.counter = UiKit.OneLine(UiKit.Label("Counter", slab.transform, "", 28, Palette.TextDim, 0.45f, 0.845f, 0.86f, 0.93f,
                TextAnchor.MiddleRight, FontStyle.Bold));
            var closeImage = UiKit.Pill(UiKit.Panel("StoneClose", slab.transform, new Color32(44, 50, 68, 255), 0.957f, 0.888f, 0.957f, 0.888f));
            closeImage.rectTransform.sizeDelta = new Vector2(64, 64);
            closeImage.rectTransform.anchoredPosition = new Vector2(-32, 0);
            var close = UiKit.AddButton(closeImage);
            var x = UiKit.Label("X", closeImage.transform, "×", 30, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            x.font = UiFonts.Bold; // ✕(U+2715)는 Noto에도 없어 ×로 (#48·#93)
            close.onClick.AddListener(() => view.onClose?.Invoke());

            var stone = UiKit.RoundPanel("Stone", slab.transform, StoneDark, UiKit.RadiusMd, 0.043f, 0.156f, 0.957f, 0.79f);
            stone.raycastTarget = false;
            view.word = UiKit.Display(UiKit.Label("Word", stone.transform, "", 104, FaintWord, 0.04f, 0.45f, 0.96f, 0.95f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 48));
            view.glow = view.word.gameObject.AddComponent<Outline>(); // 글자 둘레 청록 빛
            view.glow.effectDistance = new Vector2(4f, -4f);
            view.meaning = UiKit.Display(UiKit.Label("Meaning", stone.transform, "", 60, Palette.Gold, 0.04f, 0.27f, 0.96f, 0.48f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 32));
            view.ask = UiKit.OneLine(UiKit.Label("Ask", stone.transform, "", 32, Palette.TextDim, 0.04f, 0.06f, 0.96f, 0.25f,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            view.note = UiKit.OneLine(UiKit.Label("Note", slab.transform, "", 26, Palette.TextDim, 0.043f, 0.03f, 0.957f, 0.12f,
                TextAnchor.MiddleLeft));

            // 2×2 뜻 고르기 (Figma 'Answer Choice', 엄지 쪽 — 석판 문제와 같은 자리)
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float minX = i % 2 == 0 ? 0.065f : 0.509f, maxY = i < 2 ? 0.245f : 0.156f;
                var button = UiKit.MakeButton($"StoneChoice_{i}", rect, "", Palette.Button, 40, minX, maxY - 0.078f, minX + 0.426f, maxY, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                var colors = button.colors;
                colors.disabledColor = Color.white;
                button.colors = colors;
                button.onClick.AddListener(() => view.onPick?.Invoke(index));
                view.choices.Add(button);
            }
            view.done = UiKit.MakeButton("StoneDone", rect, "확인", Palette.Gold, 44, 0.222f, 0.125f, 0.778f, 0.1875f);
            UiKit.LabelOf(view.done).color = Palette.OnAccent;
            view.done.onClick.AddListener(() => view.onClose?.Invoke());

            view.root.SetActive(false);
            return view;
        }

        // litCount: 지금까지 밝힌 반딧돌 수, total: 맵의 반딧돌 수
        public void Show(QuizQuestion question, int litCount, int total, Action<int> pick, Action closed)
        {
            onPick = pick;
            onClose = closed;
            lit = false;
            time = 0f;
            counter.text = $"밝힌 반딧돌 {litCount} / {total}";
            word.text = question.Prompt;
            word.color = FaintWord;
            meaning.text = "";
            ask.text = "반딧돌에 떠오른 글자 — 뜻은?";
            ask.color = Palette.TextDim;
            note.text = "틀려도 괜찮아요 — 다시 고르면 돼요";
            edge.color = Edge;
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

        // 맞힘: 글자가 환하게 + 뜻(금빛) + '반딧돌이 밝아졌다!' → 선택지 대신 [확인]
        public void ShowLit(string answer, int litCount, int total)
        {
            StopShake();
            lit = true;
            counter.text = $"밝힌 반딧돌 {litCount} / {total}";
            word.color = BrightWord;
            meaning.text = answer;
            ask.text = "반딧돌이 밝아졌다!";
            ask.color = Cyan;
            note.text = litCount >= total ? "반딧돌을 모두 밝혔다! 동굴 천장의 반딧불이 깨어난다" : "둘레가 영영 밝아지고 지도에도 드러나요";
            edge.color = Cyan;
            foreach (var choice in choices) choice.gameObject.SetActive(false);
            done.gameObject.SetActive(true);
        }

        public void Hide()
        {
            StopShake();
            root.SetActive(false);
        }

        // 글자 둘레 빛이 은은하게 숨 쉬듯 (밝아지면 더 환하게), 틀린 선택지 흔들기
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            time += dt;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
            glow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, lit ? 0.75f + 0.2f * pulse : 0.2f + 0.15f * pulse);
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
