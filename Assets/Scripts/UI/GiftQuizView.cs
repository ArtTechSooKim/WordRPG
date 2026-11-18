using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 크리스마스 선물 퀴즈 창 (#274, Figma '크리스마스 선물 — 안내 · 문제 · 정답 · 틀림 · 선물 열기 · 받음 · 개발자의 편지 (#274)').
    // 필드 위 어두운 막 + 천천히 내리는 눈. 선물은 받기 전까지 비밀 (사용자) — 상자 속을 '?'로만 보여 줌.
    // 안내([퀴즈 풀기]/[나중에]) → 문제(선물 칸 3개 · 영단어 · 아래 2×2 뜻) → 답(정답 초록 / 고른 오답 빨강 · 뜻 · [다음])
    // → 선물 열기(기본 1개 + 맞힌 수만큼 상자 · [열어 보기] → 흔들리다 터짐) → 받음(받은 것 · [편지 읽기]) → 개발자의 편지([닫기])
    public class GiftQuizView
    {
        public enum Stage { Closed, Intro, Question, Answered, Unwrap, Opening, Result, Letter }

        private static readonly Color Red = new Color32(205, 62, 72, 255);   // 크리스마스 빨강 (꼬리표·판 테두리)
        private const float ScreenW = 1080f, ScreenH = 1920f, PanelW = 960f;
        private const float IntroH = 1192f, QuizH = 740f, UnwrapH = 674f, ResultH = 710f, LetterH = 1600f;
        private const int SnowCount = 26, MaxBoxes = 4;
        private const float ShakeTime = 0.45f, BurstTime = 0.3f, PopTime = 0.25f;

        private class Slot
        {
            public RectTransform Cell;
            public Image Gift, Ring, Badge;
            public Text Mark;
        }

        private class Flake
        {
            public RectTransform Rect;
            public float U, V, Speed, Phase;
        }

        private GameObject root, intro, quiz, unwrap, result, letter;
        private Image introGift;
        private Text introTag, introTitle, introBody, introNote, mysteryRule;
        private readonly List<GameObject> mysteryBoxes = new List<GameObject>();
        private Text quizTag, counter, word, meaning, ask, quizNote;
        private readonly List<Slot> quizSlots = new List<Slot>();
        private readonly List<Button> choices = new List<Button>();
        private Button next;
        private Text unwrapTag, unwrapTitle, unwrapSub;
        private readonly List<Slot> boxSlots = new List<Slot>();
        private Text resultTag, resultNote;
        private Button resultButton;
        private readonly List<RectTransform> resultRows = new List<RectTransform>();
        private readonly List<Image> resultIcons = new List<Image>();
        private readonly List<Text> resultLabels = new List<Text>();
        private readonly List<Image> sparkles = new List<Image>();
        private LetterPaper letterPaper;
        private readonly List<Flake> snow = new List<Flake>();
        private Action onStart, onLater, onNext, onDone, onLetter;
        private Action<int> onPick;
        private GiftEvent shownGift;
        private int boxCount;
        private float time, stageTime;

        public Stage Current { get; private set; }
        public bool IsOpen => root != null && root.activeSelf;
        // 테스트용
        public string Title => Current == Stage.Intro ? introTitle.text : Current == Stage.Unwrap || Current == Stage.Opening ? unwrapTitle.text
            : Current == Stage.Result ? "선물을 받았어요!" : Current == Stage.Letter ? letterPaper.TitleText : "";
        public string Counter => counter.text;
        public string Word => word.text;
        public string Meaning => meaning.text;
        public string Ask => ask.text;
        public string Note => Current == Stage.Intro ? introNote.text : Current == Stage.Result ? resultNote.text : quizNote.text;
        public string Rewards => string.Join(" / ", resultLabels.ConvertAll(l => l.gameObject.activeSelf ? l.text : "").FindAll(s => s != ""));
        public string ChoiceText(int index) => UiKit.LabelOf(choices[index]).text;
        public Color ChoiceColor(int index) => choices[index].image.color;
        public string NextLabel => UiKit.LabelOf(next).text;
        public string ResultButtonLabel => UiKit.LabelOf(resultButton).text;
        public int BoxCount => boxCount;
        public int MysteryBoxCount => mysteryBoxes.FindAll(b => b.activeSelf).Count;
        public string LetterText => letterPaper.AllText;
        public string AllIntroText => introTitle.text + "\n" + introBody.text + "\n" + mysteryRule.text + "\n" + introNote.text;

        public static GiftQuizView Create(RectTransform parent)
        {
            var view = new GiftQuizView();
            var rect = UiKit.Stretch("GiftQuiz", parent);
            view.root = rect.gameObject;
            UiKit.Panel("Dim", rect, Palette.Scrim).raycastTarget = true; // 뒤 필드를 막는다

            // 천천히 내리는 눈 (은은하게 — 흰 점, 반투명)
            var snowLayer = UiKit.Stretch("Snow", rect);
            var rng = new System.Random(274);
            for (int i = 0; i < SnowCount; i++)
            {
                float size = rng.NextDouble() < 0.3 ? 12f : 8f;
                var flake = UiKit.Panel($"Flake_{i}", snowLayer, new Color(1f, 1f, 1f, 0.35f + 0.4f * (float)rng.NextDouble()));
                flake.raycastTarget = false;
                var drop = new Flake
                {
                    Rect = flake.rectTransform, U = (float)rng.NextDouble(), V = (float)rng.NextDouble(),
                    Speed = 0.018f + 0.018f * (float)rng.NextDouble(), Phase = (float)(rng.NextDouble() * Math.PI * 2)
                };
                drop.Rect.anchorMin = drop.Rect.anchorMax = new Vector2(drop.U, drop.V); // 점 하나 (화면 전체로 늘어나지 않게)
                drop.Rect.sizeDelta = new Vector2(size, size);
                view.snow.Add(drop);
            }

            view.BuildIntro(rect);
            view.BuildQuiz(rect);
            view.BuildUnwrap(rect);
            view.BuildResult(rect);
            view.BuildLetter(rect);
            view.root.SetActive(false);
            return view;
        }

        // ── 만들기 ─────────────────────────────────────────────

        private void BuildIntro(RectTransform rect)
        {
            var panel = Panel(rect, "GiftIntro", 340, IntroH, Palette.Panel);
            intro = panel.gameObject;
            introTag = Tag(panel, IntroH, 340, 280);
            introGift = Picture(panel, IntroH, "Gift", UiKit.ArtSprite("Event/gift_box"), PanelW / 2, 140 + 96, 192);
            introTitle = UiKit.OneLine(UiKit.Display(PanelLabel(panel, IntroH, "Title", 64, Palette.Text, 0, 352, PanelW, 80)));
            introBody = PanelLabel(panel, IntroH, "Body", 34, Palette.TextDim, 56, 444, 848, 100);

            // 선물 상자 속은 비밀 (사용자): 기본 상자 + '?' 상자들
            const float boxH = 252f, boxW = 848f;
            var box = Box(panel, IntroH, "Mystery", 56, 568, boxW, boxH);
            var caption = UiKit.OneLine(PanelLabel(box, boxH, "Caption", 30, Palette.Text, 0, 20, boxW, 44, boxW, "선물 상자 속은 비밀!"));
            caption.font = UiFonts.Bold;
            const float size = 112f, gap = 20f;
            float left = (boxW - (MaxBoxes * size + (MaxBoxes - 1) * gap)) / 2f;
            for (int i = 0; i < MaxBoxes; i++)
            {
                float cx = left + i * (size + gap) + size / 2f, cy = 76 + size / 2f;
                var cell = UiKit.RoundPanel($"MysteryBox_{i}", box, Palette.Track, UiKit.RadiusMd, cx / boxW, 1f - cy / boxH, cx / boxW, 1f - cy / boxH);
                cell.rectTransform.sizeDelta = new Vector2(size, size);
                cell.raycastTarget = false;
                var gift = UiKit.IconImage("Gift", cell.transform, UiKit.ArtSprite("Event/gift_box"), 0.5f, 0.5f, 0.5f, 0.5f);
                gift.rectTransform.sizeDelta = new Vector2(80, 80);
                if (i == 0)
                {
                    var pill = UiKit.Pill(UiKit.Panel("Basic", cell.transform, Palette.Gold, 0.5f, 0f, 0.5f, 0f));
                    pill.rectTransform.sizeDelta = new Vector2(72, 32);
                    pill.rectTransform.anchoredPosition = new Vector2(0, 20);
                    pill.raycastTarget = false;
                    UiKit.OneLine(UiKit.Label("Text", pill.transform, "기본", 22, Palette.OnAccent, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
                }
                else
                {
                    gift.color = new Color(1f, 1f, 1f, 0.35f);
                    UiKit.OneLine(UiKit.Display(UiKit.Label("Question", cell.transform, "?", 60, Palette.Gold, 0, 0, 1, 1)));
                }
                mysteryBoxes.Add(cell.gameObject);
            }
            mysteryRule = UiKit.OneLine(PanelLabel(box, boxH, "Rule", 28, Palette.TextDim, 0, 200, boxW, 36, boxW));
            introNote = UiKit.OneLine(PanelLabel(panel, IntroH, "Note", 28, Palette.TextDim, 0, 844, PanelW, 40));

            var start = PanelButton(panel, IntroH, "GiftStartButton", "퀴즈 풀기", Palette.Gold, 56, 908, 848, 102);
            UiKit.LabelOf(start).color = Palette.OnAccent;
            start.onClick.AddListener(() => onStart?.Invoke());
            var later = PanelButton(panel, IntroH, "GiftLaterButton", "나중에", Palette.Neutral, 56, 1034, 848, 102);
            later.onClick.AddListener(() => onLater?.Invoke());
        }

        private void BuildQuiz(RectTransform rect)
        {
            var group = UiKit.Stretch("GiftQuestion", rect);
            quiz = group.gameObject;
            var panel = Panel(group, "GiftQuizPanel", 260, QuizH, Palette.Panel);
            quizTag = Tag(panel, QuizH, 56, 380);
            counter = UiKit.OneLine(UiKit.Display(PanelLabel(panel, QuizH, "Counter", 40, Palette.TextDim, 600, 56, 304, 56)));
            counter.alignment = TextAnchor.MiddleRight;
            Slots(panel, QuizH, 144, 3, quizSlots);

            var box = UiKit.RoundPanel("Word", panel, Palette.Track, UiKit.RadiusMd);
            Place(box.rectTransform, PanelW, QuizH, 56, 308, 848, 300);
            box.raycastTarget = false;
            word = UiKit.Display(UiKit.Label("English", box.transform, "", 104, Palette.Text, 0.03f, 0.55f, 0.97f, 0.95f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 48));
            meaning = UiKit.Display(UiKit.Label("Meaning", box.transform, "", 60, Palette.Gold, 0.03f, 0.3f, 0.97f, 0.55f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 30));
            ask = UiKit.OneLine(UiKit.Label("Ask", box.transform, "", 32, Palette.TextDim, 0.03f, 0.06f, 0.97f, 0.28f,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 22));
            quizNote = UiKit.OneLine(PanelLabel(panel, QuizH, "Note", 28, Palette.TextDim, 0, 636, PanelW, 40));

            // 2×2 뜻 고르기 — 엄지 쪽 (Figma 'Answer Choice', 반딧돌·석판과 같은 자리)
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float x = i % 2 == 0 ? 60f : 552f, y = i < 2 ? 1360f : 1512f;
                var (minX, minY, maxX, maxY) = Anchors(ScreenW, ScreenH, x, y, 468, 128);
                var button = UiKit.MakeButton($"GiftChoice_{i}", group, "", Palette.Button, 40, minX, minY, maxX, maxY, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold;
                var colors = button.colors;
                colors.disabledColor = Color.white; // 답한 뒤 정답·오답 색을 그대로 보이게
                button.colors = colors;
                button.onClick.AddListener(() => onPick?.Invoke(index));
                choices.Add(button);
            }
            var (nx, ny, nX, nY) = Anchors(ScreenW, ScreenH, 240, 1696, 600, 102);
            next = UiKit.MakeButton("GiftNextButton", group, "다음", Palette.Gold, 44, nx, ny, nX, nY);
            UiKit.LabelOf(next).color = Palette.OnAccent;
            next.onClick.AddListener(() => onNext?.Invoke());
        }

        private void BuildUnwrap(RectTransform rect)
        {
            var panel = Panel(rect, "GiftUnwrap", 560, UnwrapH, Palette.Panel);
            unwrap = panel.gameObject;
            unwrapTag = Tag(panel, UnwrapH, 340, 280);
            unwrapTitle = UiKit.OneLine(UiKit.Display(PanelLabel(panel, UnwrapH, "Title", 64, Palette.Text, 0, 140, PanelW, 80)));
            unwrapSub = UiKit.OneLine(PanelLabel(panel, UnwrapH, "Sub", 30, Palette.TextDim, 0, 228, PanelW, 44));
            Slots(panel, UnwrapH, 296, MaxBoxes, boxSlots);
            UiKit.OneLine(PanelLabel(panel, UnwrapH, "Note", 28, Palette.TextDim, 0, 452, PanelW, 40, PanelW, "무엇이 들어 있을까요?"));
            var open = PanelButton(panel, UnwrapH, "GiftOpenButton", "열어 보기", Palette.Gold, 56, 516, 848, 102);
            UiKit.LabelOf(open).color = Palette.OnAccent;
            open.onClick.AddListener(StartOpening);
        }

        private void BuildResult(RectTransform rect)
        {
            var panel = Panel(rect, "GiftResult", 560, ResultH, Palette.Panel);
            result = panel.gameObject;
            resultTag = Tag(panel, ResultH, 340, 280);
            UiKit.OneLine(UiKit.Display(PanelLabel(panel, ResultH, "Title", 64, Palette.Text, 0, 140, PanelW, 80, PanelW, "선물을 받았어요!")));
            // 반짝이 (금빛 점 몇 개가 숨 쉬듯 — 은은하게)
            foreach (var (x, y, s) in new[] { (120f, 60f, 14f), (830f, 90f, 10f), (780f, 40f, 8f), (180f, 150f, 8f), (880f, 200f, 12f) })
            {
                var spark = UiKit.Panel("Sparkle", panel, Palette.Gold, x / PanelW, 1f - y / ResultH, x / PanelW, 1f - y / ResultH);
                spark.rectTransform.sizeDelta = new Vector2(s, s);
                spark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                spark.raycastTarget = false;
                sparkles.Add(spark);
            }

            const float boxH = 220f, boxW = 848f;
            var box = Box(panel, ResultH, "Rewards", 56, 244, boxW, boxH);
            for (int i = 0; i < 2; i++)
            {
                float y = 28 + i * 88;
                var row = UiKit.Rect($"Row_{i}", box, 0, 1f - (y + 80) / boxH, 1, 1f - y / boxH);
                resultRows.Add(row);
                resultIcons.Add(Picture(row, 80, "Icon", null, 80, 40, 80, boxW));
                var label = PanelLabel(row, 80, "Label", 32, Palette.Text, 140, 0, 680, 80, boxW);
                label.font = UiFonts.Bold;
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                resultLabels.Add(label);
            }
            resultNote = UiKit.OneLine(PanelLabel(panel, ResultH, "Note", 28, Palette.TextDim, 0, 488, PanelW, 40));
            resultButton = PanelButton(panel, ResultH, "GiftDoneButton", "확인", Palette.Gold, 56, 552, 848, 102);
            UiKit.LabelOf(resultButton).color = Palette.OnAccent;
            resultButton.onClick.AddListener(AfterResult);
        }

        // 개발자의 편지 (종이는 메일함과 같은 LetterPaper — #275) + [닫기]
        private void BuildLetter(RectTransform rect)
        {
            var group = UiKit.Stretch("GiftLetter", rect);
            letter = group.gameObject;
            letterPaper = LetterPaper.Create(group, 120, LetterH);
            var (cx, cy, cX, cY) = Anchors(ScreenW, ScreenH, 240, 1760, 600, 102);
            var close = UiKit.MakeButton("GiftLetterCloseButton", group, "닫기", Palette.Gold, 44, cx, cy, cX, cY);
            UiKit.LabelOf(close).color = Palette.OnAccent;
            close.onClick.AddListener(() => onDone?.Invoke());
        }

        // ── 보이기 ─────────────────────────────────────────────

        public void ShowIntro(GiftEvent gift, Action start, Action later)
        {
            shownGift = gift;
            onStart = start;
            onLater = later;
            introTag.text = gift.DisplayName;
            introTitle.text = gift.Title;
            introBody.text = $"{gift.WordsName} {gift.Questions}문제를 풀어 보세요\n맞힐 때마다 선물 상자가 하나씩 늘어나요";
            int most = GiftEvents.Boxes(gift, gift.Questions);
            for (int i = 0; i < mysteryBoxes.Count; i++) mysteryBoxes[i].SetActive(i < Mathf.Min(most, MaxBoxes));
            bool basic = GiftEvents.Boxes(gift, 0) > 0;
            mysteryRule.text = basic ? "기본 1개 + 맞힐 때마다 1개 더" : "맞힐 때마다 1개";
            introNote.text = $"{gift.EndLabel}까지 · 한 번만 받을 수 있어요";
            Open(Stage.Intro);
        }

        public void ShowQuestion(GiftEvent gift, GiftQuiz giftQuiz, Action<int> pick)
        {
            onPick = pick;
            var question = giftQuiz.Current;
            quizTag.text = gift.QuizName;
            counter.text = $"{giftQuiz.Index + 1} / {giftQuiz.Count}";
            FillSlots(quizSlots, giftQuiz);
            word.text = question.Prompt;
            meaning.text = "";
            ask.text = "뜻은?";
            ask.color = Palette.TextDim;
            quizNote.text = "맞히면 선물 상자 하나 더!";
            for (int i = 0; i < choices.Count; i++)
            {
                bool used = i < question.Choices.Count;
                choices[i].gameObject.SetActive(used);
                if (!used) continue;
                UiKit.LabelOf(choices[i]).text = question.Choices[i];
                choices[i].image.color = Palette.Button;
                choices[i].interactable = true;
            }
            next.gameObject.SetActive(false);
            Open(Stage.Question);
        }

        // 답한 뒤: 정답은 초록, 틀렸으면 고른 것만 빨강, 나머지는 흐리게 + 뜻 + [다음] (마지막이면 [선물 받기])
        public void ShowAnswer(GiftEvent gift, GiftQuiz giftQuiz, Action nextQuestion)
        {
            onNext = nextQuestion;
            var question = giftQuiz.Current;
            bool correct = question.IsCorrect(giftQuiz.LastPick);
            FillSlots(quizSlots, giftQuiz);
            meaning.text = question.CorrectAnswer;
            ask.text = correct ? "정답! 선물 상자 하나 더!" : $"아쉬워요! 정답은 '{question.CorrectAnswer}'";
            ask.color = correct ? Palette.Good : Palette.Bad;
            bool last = giftQuiz.Index + 1 >= giftQuiz.Count;
            quizNote.text = correct || last ? "" : "다음 문제에서 또 받을 수 있어요";
            for (int i = 0; i < choices.Count; i++)
            {
                if (!choices[i].gameObject.activeSelf) continue;
                choices[i].interactable = false;
                choices[i].image.color = question.IsCorrect(i) ? Palette.Good : i == giftQuiz.LastPick ? Palette.Bad : Palette.Disabled;
            }
            UiKit.LabelOf(next).text = last ? "선물 받기" : "다음";
            next.gameObject.SetActive(true);
            Current = Stage.Answered;
        }

        // 다 풀었음: 받은 선물 상자(기본 1 + 맞힌 수)를 닫힌 채로 → [열어 보기] → 받음 → (편지 — 열면 letterOpened) → done
        public void ShowUnwrap(GiftEvent gift, GiftQuiz giftQuiz, IReadOnlyList<ItemGift> given, Action done, Action letterOpened = null)
        {
            shownGift = gift;
            onDone = done;
            onLetter = letterOpened;
            int correct = giftQuiz.CorrectCount;
            boxCount = Mathf.Max(1, GiftEvents.Boxes(gift, correct));
            unwrapTag.text = resultTag.text = gift.DisplayName;
            unwrapTitle.text = $"선물 상자 {boxCount}개!";
            bool basic = GiftEvents.Boxes(gift, 0) > 0;
            unwrapSub.text = !basic ? $"맞힌 선물 {correct}개" : correct > 0 ? $"기본 1개 + 맞힌 선물 {correct}개" : "기본 선물 1개";

            // 상자를 가운데 정렬로 (최대 4개 — 넘으면 4개만 그림)
            int shown = Mathf.Min(boxCount, MaxBoxes);
            const float size = 132f, gap = 24f;
            float left = (PanelW - (shown * size + (shown - 1) * gap)) / 2f;
            for (int i = 0; i < boxSlots.Count; i++)
            {
                var slot = boxSlots[i];
                bool on = i < shown;
                slot.Cell.gameObject.SetActive(on);
                if (!on) continue;
                float cx = (left + i * (size + gap) + size / 2f) / PanelW;
                slot.Cell.anchorMin = slot.Cell.anchorMax = new Vector2(cx, slot.Cell.anchorMin.y);
                slot.Cell.localRotation = Quaternion.identity;
                slot.Cell.localScale = Vector3.one;
                slot.Ring.enabled = false;
                slot.Badge.gameObject.SetActive(false);
                slot.Gift.color = Color.white;
            }

            for (int i = 0; i < resultRows.Count; i++)
            {
                bool used = given != null && i < given.Count;
                resultRows[i].gameObject.SetActive(used);
                resultIcons[i].enabled = used && UiKit.ItemIcon(given[i].Item) != null;
                if (used) resultIcons[i].sprite = UiKit.ItemIcon(given[i].Item);
                resultLabels[i].gameObject.SetActive(used);
                resultLabels[i].text = used
                    ? $"{given[i].Item.DisplayName}  <color=#{ColorUtility.ToHtmlStringRGB(Palette.Gold)}>×{given[i].Count}</color>" : "";
            }
            resultNote.text = gift.HasLetter ? "가방에 넣었어요 · 편지는 메일함에서도 읽을 수 있어요" : $"가방에 넣었어요 · {gift.Title}";
            UiKit.LabelOf(resultButton).text = gift.HasLetter ? "편지 읽기" : "확인";
            letterPaper.Fill(gift.Letter);
            Open(Stage.Unwrap);
        }

        public void Hide()
        {
            Current = Stage.Closed;
            if (root != null) root.SetActive(false);
        }

        // 뒤로가기: 안내 = [나중에] · 선물 열기 = [열어 보기] · 받음 = 다음(편지/확인) · 편지 = [닫기]. 문제 중에는 닫지 않음 (끝까지 풀어야 선물을 셈)
        public void Back()
        {
            switch (Current)
            {
                case Stage.Intro: onLater?.Invoke(); break;
                case Stage.Unwrap: StartOpening(); break;
                case Stage.Result: AfterResult(); break;
                case Stage.Letter: onDone?.Invoke(); break;
            }
        }

        // 눈이 천천히 내리고, 선물 상자가 살짝 들썩이고, 열 때 흔들리다 터짐
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            time += dt;
            stageTime += dt;
            foreach (var flake in snow)
            {
                flake.V -= flake.Speed * dt;
                if (flake.V < -0.02f) flake.V += 1.04f;
                float u = flake.U + 0.008f * Mathf.Sin(time * 0.9f + flake.Phase);
                flake.Rect.anchorMin = flake.Rect.anchorMax = new Vector2(u, flake.V);
            }
            switch (Current)
            {
                case Stage.Intro:
                    introGift.rectTransform.anchoredPosition = new Vector2(0f, 6f * Mathf.Abs(Mathf.Sin(time * 2.2f)));
                    break;
                case Stage.Unwrap:
                    for (int i = 0; i < boxSlots.Count; i++) // 차례로 통통
                        boxSlots[i].Gift.rectTransform.anchoredPosition = new Vector2(0f, 5f * Mathf.Max(0f, Mathf.Sin(time * 3f - i * 0.6f)));
                    break;
                case Stage.Opening:
                    TickOpening();
                    break;
                case Stage.Result:
                    float pop = Mathf.Clamp01(stageTime / PopTime);
                    float scale = Mathf.LerpUnclamped(0.6f, 1f, 1f - (1f - pop) * (1f - pop)); // 톡 커짐
                    foreach (var row in resultRows) row.localScale = new Vector3(scale, scale, 1f);
                    for (int i = 0; i < sparkles.Count; i++)
                    {
                        var c = sparkles[i].color;
                        c.a = 0.45f + 0.45f * Mathf.Sin(time * 2.5f + i * 1.3f);
                        sparkles[i].color = c;
                    }
                    break;
            }
        }

        // 테스트용 (버튼을 누른 것과 같음)
        public void Start() => onStart?.Invoke();
        public void Later() => onLater?.Invoke();
        public void Pick(int index) => onPick?.Invoke(index);
        public void Next() => onNext?.Invoke();

        private void StartOpening()
        {
            if (Current != Stage.Unwrap) return;
            Current = Stage.Opening;
            stageTime = 0f;
        }

        private void TickOpening()
        {
            for (int i = 0; i < boxSlots.Count; i++)
            {
                var cell = boxSlots[i].Cell;
                if (stageTime < ShakeTime)
                {
                    float shake = Mathf.Sin(stageTime * 60f + i) * 9f * (0.4f + stageTime / ShakeTime);
                    cell.localRotation = Quaternion.Euler(0, 0, shake);
                }
                else
                {
                    float t = Mathf.Clamp01((stageTime - ShakeTime) / BurstTime);
                    cell.localRotation = Quaternion.identity;
                    cell.localScale = Vector3.one * (1f + 0.5f * t);
                    boxSlots[i].Gift.color = new Color(1f, 1f, 1f, 1f - t);
                }
            }
            if (stageTime < ShakeTime + BurstTime) return;
            Sound.Play(Sfx.Coin);
            Open(Stage.Result);
        }

        private void AfterResult()
        {
            if (Current != Stage.Result) return;
            if (shownGift != null && shownGift.HasLetter)
            {
                letterPaper.ScrollToTop();
                Open(Stage.Letter);
                onLetter?.Invoke(); // 메일함에서도 읽은 것으로
            }
            else onDone?.Invoke();
        }

        private void Open(Stage stage)
        {
            Current = stage;
            stageTime = 0f;
            intro.SetActive(stage == Stage.Intro);
            quiz.SetActive(stage == Stage.Question || stage == Stage.Answered);
            unwrap.SetActive(stage == Stage.Unwrap || stage == Stage.Opening);
            result.SetActive(stage == Stage.Result);
            letter.SetActive(stage == Stage.Letter);
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        // 선물 칸: 답한 문제 = ✓(맞힘)·×(틀림), 지금 문제 = 금빛 테두리, 아직 = 흐린 상자
        private static void FillSlots(List<Slot> slots, GiftQuiz giftQuiz)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool shown = i < giftQuiz.Count;
                slot.Cell.gameObject.SetActive(shown);
                if (!shown) continue;
                bool answered = i < giftQuiz.Results.Count;
                bool now = !giftQuiz.IsComplete && i == giftQuiz.Index && !giftQuiz.Answered;
                bool won = answered && giftQuiz.Results[i];
                slot.Ring.enabled = now;
                slot.Gift.color = new Color(1f, 1f, 1f, answered && !won || !answered && !now ? 0.35f : 1f);
                slot.Badge.gameObject.SetActive(answered);
                slot.Badge.color = won ? Palette.Good : Palette.Bad;
                slot.Mark.text = won ? "✓" : "×";
            }
        }

        // ── 그리기 도우미 (좌표 = Figma px, 위에서부터) ───────────

        private static (float, float, float, float) Anchors(float pw, float ph, float x, float y, float w, float h) =>
            (x / pw, 1f - (y + h) / ph, (x + w) / pw, 1f - y / ph);

        private static void Place(RectTransform rt, float pw, float ph, float x, float y, float w, float h)
        {
            var (minX, minY, maxX, maxY) = Anchors(pw, ph, x, y, w, h);
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // 화면 위 판 (빨간 테두리) — y·높이는 Figma 화면 기준
        private static RectTransform Panel(RectTransform parent, string name, float y, float height, Color color)
        {
            var (minX, minY, maxX, maxY) = Anchors(ScreenW, ScreenH, 60, y, PanelW, height);
            var panel = UiKit.RoundPanel(name, parent, color, UiKit.RadiusLg, minX, minY, maxX, maxY);
            panel.raycastTarget = true;
            var edge = UiKit.Outline(UiKit.Panel("Edge", panel.transform, Red), UiKit.RadiusLg, 4);
            edge.raycastTarget = false;
            return panel.rectTransform;
        }

        private static RectTransform Box(RectTransform panel, float ph, string name, float x, float y, float w, float h)
        {
            var box = UiKit.RoundPanel(name, panel, Palette.PanelLight, UiKit.RadiusMd);
            box.raycastTarget = false;
            Place(box.rectTransform, PanelW, ph, x, y, w, h);
            return box.rectTransform;
        }

        private static Text PanelLabel(RectTransform parent, float ph, string name, int size, Color color, float x, float y, float w, float h,
            float pw = PanelW, string text = "")
        {
            var (minX, minY, maxX, maxY) = Anchors(pw, ph, x, y, w, h);
            return UiKit.Label(name, parent, text, size, color, minX, minY, maxX, maxY);
        }

        // 크기가 정해진 그림 (화면 비율이 달라도 찌그러지지 않게 가운데 점에 붙임)
        private static Image Picture(RectTransform parent, float ph, string name, Sprite sprite, float cx, float cy, float size, float pw = PanelW)
        {
            var image = UiKit.IconImage(name, parent, sprite, cx / pw, 1f - cy / ph, cx / pw, 1f - cy / ph);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        // 빨간 꼬리표 (글자는 Show에서)
        private static Text Tag(RectTransform panel, float ph, float x, float w)
        {
            var (minX, minY, maxX, maxY) = Anchors(PanelW, ph, x, 56, w, 56);
            var pill = UiKit.Pill(UiKit.Panel("Tag", panel, Red, minX, minY, maxX, maxY));
            pill.raycastTarget = false;
            return UiKit.OneLine(UiKit.Display(UiKit.Label("Text", pill.transform, "", 32, Color.white, 0, 0, 1, 1)));
        }

        private static Button PanelButton(RectTransform panel, float ph, string name, string label, Color color, float x, float y, float w, float h)
        {
            var (minX, minY, maxX, maxY) = Anchors(PanelW, ph, x, y, w, h);
            return UiKit.MakeButton(name, panel, label, color, 44, minX, minY, maxX, maxY);
        }

        // 선물 칸 count개 (132×132, 사이 24) — 가운데 정렬
        private static void Slots(RectTransform panel, float ph, float y, int count, List<Slot> into)
        {
            const float size = 132f, gap = 24f;
            float left = (PanelW - (count * size + (count - 1) * gap)) / 2f;
            for (int i = 0; i < count; i++)
            {
                float cx = left + i * (size + gap) + size / 2f, cy = y + size / 2f;
                var cell = UiKit.RoundPanel($"GiftSlot_{i}", panel, Palette.PanelLight, UiKit.RadiusMd,
                    cx / PanelW, 1f - cy / ph, cx / PanelW, 1f - cy / ph);
                cell.rectTransform.sizeDelta = new Vector2(size, size);
                cell.raycastTarget = false;
                var ring = UiKit.Outline(UiKit.Panel("Ring", cell.transform, Palette.Gold), UiKit.RadiusMd, 4);
                ring.raycastTarget = false;
                var gift = UiKit.IconImage("Gift", cell.transform, UiKit.ArtSprite("Event/gift_box"), 0.5f, 0.5f, 0.5f, 0.5f);
                gift.rectTransform.sizeDelta = new Vector2(96, 96);
                var badge = UiKit.Pill(UiKit.Panel("Badge", cell.transform, Palette.Good, 1f, 1f, 1f, 1f));
                badge.rectTransform.sizeDelta = new Vector2(48, 48);
                badge.rectTransform.anchoredPosition = new Vector2(-28, -28);
                badge.raycastTarget = false;
                var mark = UiKit.OneLine(UiKit.Label("Mark", badge.transform, "", 30, Color.white, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
                into.Add(new Slot { Cell = cell.rectTransform, Gift = gift, Ring = ring, Badge = badge, Mark = mark });
            }
        }
    }
}
