using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 처음에 외울 단어장 고르기 (#273 사용자, GDD 9-12 — Figma '단어장 고르기 (#273)' · 카드 = 'Course Card').
    // 위: 제목·부제 / 가운데: 단어장 카드(이름 · 단어 수 · 한 줄 설명 · 예시 단어 3개) / 아래: '한 번 고르면 바꿀 수 없어요' + [이 단어장으로 시작]
    // → 확인 창 '○○로 시작할까요?' [다시 고르기]/[시작]. 입장 영상 뒤, 모습 고르기 앞에 한 번 (FieldScreen.Prologue)
    public class CourseSelectView
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public WordCourse Selected { get; private set; }
        public ConfirmDialog Dialog { get; private set; }
        public IReadOnlyList<WordCourse> Courses => courses;

        private readonly List<WordCourse> courses = new List<WordCourse>();
        private readonly List<Image> cardEdges = new List<Image>();
        private readonly List<Text> cardTitles = new List<Text>();
        private RectTransform cards;
        private Action<WordCourse> chosen;

        public static CourseSelectView Create(Transform parent)
        {
            var view = new CourseSelectView();
            var root = UiKit.Stretch("CourseSelectView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background; // 뒤 화면(필드)을 가리고 터치도 막음

            UiKit.Display(UiKit.Label("Title", root, "어떤 영단어를 외울까요?", 60, Palette.Gold, 0, 0.92f, 1, 0.985f));
            UiKit.Label("Subtitle", root, "고른 단어가 몬스터와 싸울 때 문제로 나와요", 30, Palette.TextDim, 0, 0.88f, 1, 0.92f);
            view.cards = UiKit.Rect("Cards", root, 0.056f, 0.40f, 0.944f, 0.87f);
            UiKit.Label("Note", root, "한 번 고르면 바꿀 수 없어요", 26, Palette.TextDim, 0, 0.12f, 1, 0.15f);
            var start = UiKit.MakeButton("CourseStartButton", root, "이 단어장으로 시작", Palette.Evolve, 52, 0.056f, 0.035f, 0.944f, 0.1f);
            start.onClick.AddListener(view.Confirm);
            view.Dialog = ConfirmDialog.Create(root); // 카드 위
            view.Root.SetActive(false);
            return view;
        }

        // baseBooks: 지역에 붙은 기본 단어장 (단어 수·예시 단어를 셈)
        public void Show(IReadOnlyList<WordCourse> options, IReadOnlyList<WordDatabase> baseBooks, Action<WordCourse> onChosen)
        {
            courses.Clear();
            foreach (var course in options) if (course != null) courses.Add(course);
            chosen = onChosen;
            BuildCards(baseBooks ?? new List<WordDatabase>());
            Dialog.Hide();
            Root.SetActive(true);
            Root.transform.SetAsLastSibling();
            if (courses.Count > 0) Select(courses[0].CourseId);
        }

        public void Select(string courseId)
        {
            int index = courses.FindIndex(c => c.CourseId == courseId);
            if (index < 0) return;
            Selected = courses[index];
            for (int i = 0; i < cardEdges.Count; i++)
            {
                cardEdges[i].enabled = i == index;
                cardTitles[i].color = i == index ? Palette.Gold : Palette.Text;
            }
        }

        // [이 단어장으로 시작] → 확인 창 (나중에 바꿀 수 없어서 한 번 더 묻는다)
        public void Confirm()
        {
            if (!IsOpen || Selected == null || Dialog.IsOpen) return;
            var course = Selected;
            Dialog.Show($"{UiKit.WithJosa(course.DisplayName, "으로", "로")} 시작할까요?",
                "고른 단어장은 나중에 바꿀 수 없어요.\n다른 단어장은 처음부터 다시 해야 해요.", "시작", false, () =>
                {
                    Root.SetActive(false);
                    chosen?.Invoke(course);
                }, "dex", "다시 고르기");
        }

        private void BuildCards(IReadOnlyList<WordDatabase> baseBooks)
        {
            foreach (Transform child in cards) UnityEngine.Object.Destroy(child.gameObject);
            cardEdges.Clear();
            cardTitles.Clear();
            // Figma: 카드 높이 260 · 사이 28 (단어장이 3개일 때 카드 영역을 꽉 채움)
            int count = Mathf.Max(3, courses.Count);
            float slot = 1f / count, gap = 0.03f;
            for (int i = 0; i < courses.Count; i++)
            {
                var course = courses[i];
                float top = 1f - i * slot, bottom = top - slot + gap;
                var card = UiKit.MakeButton($"CourseCard_{course.CourseId}", cards, "", Palette.PanelLight, 30, 0, bottom, 1, top);
                ((RectTransform)card.transform).sizeDelta = Vector2.zero;
                var title = UiKit.Display(UiKit.OneLine(UiKit.Label("Name", card.transform, course.DisplayName, 52, Palette.Text,
                    0.042f, 0.6f, 0.78f, 0.88f, TextAnchor.MiddleLeft)));
                cardTitles.Add(title);

                var tag = UiKit.Pill(UiKit.Panel("CountTag", card.transform, Palette.Panel, 0.79f, 0.66f, 0.958f, 0.84f));
                tag.raycastTarget = false;
                UiKit.OneLine(UiKit.Label("Count", tag.transform, $"단어 {course.WordCount(baseBooks)}개", 24, Palette.TextDim, 0, 0, 1, 1,
                    TextAnchor.MiddleCenter, FontStyle.Bold, true, 16));

                UiKit.OneLine(UiKit.Label("Description", card.transform, course.Description, 30, Palette.TextDim,
                    0.042f, 0.38f, 0.958f, 0.58f, TextAnchor.MiddleLeft));
                BuildSampleWords(card.transform, course, baseBooks);

                var edge = UiKit.Outline(UiKit.Panel("Edge", card.transform, Palette.Gold), UiKit.RadiusLg, 6);
                edge.raycastTarget = false;
                edge.enabled = false;
                cardEdges.Add(edge);
                string id = course.CourseId;
                card.onClick.AddListener(() => Select(id));
            }
        }

        // 예시 단어 칩 3개: 'invoice · 청구서' — 글자 길이만큼 (가로 레이아웃)
        private static void BuildSampleWords(Transform card, WordCourse course, IReadOnlyList<WordDatabase> baseBooks)
        {
            var row = UiKit.Rect("SampleWords", card, 0.042f, 0.12f, 0.958f, 0.33f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            foreach (var wordId in course.SampleWordIds)
            {
                var word = course.FindWord(wordId, baseBooks);
                if (word == null) continue;
                var chip = UiKit.Pill(UiKit.Panel($"Word_{wordId}", row, Palette.Panel));
                chip.raycastTarget = false;
                var inner = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                inner.padding = new RectOffset(22, 22, 4, 4);
                inner.childAlignment = TextAnchor.MiddleCenter;
                inner.childControlWidth = inner.childControlHeight = true;
                inner.childForceExpandWidth = false;
                UiKit.OneLine(UiKit.Label("Text", chip.transform, $"{word.English} · {FirstMeaning(word.Meaning)}", 26, Palette.Text,
                    0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            }
        }

        // 뜻이 여럿이면 첫 뜻만 ("이익, 혜택" → "이익")
        public static string FirstMeaning(string meaning)
        {
            if (string.IsNullOrEmpty(meaning)) return "";
            int comma = meaning.IndexOf(',');
            return (comma > 0 ? meaning.Substring(0, comma) : meaning).Trim();
        }
    }
}
