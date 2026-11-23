using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.UI;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // #287 사용자: 단어장 고르기의 비즈니스 예시 단어가 'invoice ·' / '청구서'처럼 이상하게 두 줄로 꺾임 → 통일감 있게 한 줄로.
    // 다른 화면도 같은 문제(bestFit이 줄을 바꿔 큰 글자를 먼저 고름)가 있어 단어·뜻 칸에 OneLineFit을 붙임.
    // 아이폰 화면 너비(1080 기준 캔버스에서 978)로 놓고 잰다 — 배치 모드 화면은 가로라 캔버스가 넓어서 그대로는 안 꺾임
    public class OneLineTextTests
    {
        private GameObject root;
        private RectTransform phone;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("OneLineTest", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            phone = UiKit.Rect("Phone", root.transform, 0.5f, 0.5f, 0.5f, 0.5f);
            phone.sizeDelta = new Vector2(978, 2118); // 아이폰 6.7인치 비율의 캔버스 크기
        }

        [TearDown]
        public void TearDown() => Object.Destroy(root);

        private static int Lines(Text text)
        {
            Canvas.ForceUpdateCanvases();
            var generator = new TextGenerator();
            generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
            return generator.lineCount;
        }

        // 석판 보기 버튼과 같은 칸 (385×157, 42 → bestFit 22)
        private Text ChoiceLabel(int minSize)
        {
            var box = UiKit.Rect("Choice", phone, 0, 0, 0, 0);
            box.sizeDelta = new Vector2(385, 157);
            var label = UiKit.Label("Label", box, "", 42, Color.white, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold, true, 22);
            return OneLineFit.Apply(label, minSize);
        }

        [UnityTest]
        public IEnumerator WordsThatFitAtASmallerSizeStayOnOneLine()
        {
            var label = ChoiceLabel(OneLineFit.ReadableMin(42));
            label.text = "deadline";
            yield return null;
            Assert.AreEqual(42, label.fontSize, "짧은 단어는 원래 크기");
            Assert.AreEqual(1, Lines(label));

            label.text = "computed tomo/graphy"; // 예전: 'computed' / 'tomo/graphy' 두 줄
            yield return null;
            Assert.AreEqual(1, Lines(label), "한 줄로");
            Assert.Less(label.fontSize, 42, "조금 줄여서");
            Assert.GreaterOrEqual(label.fontSize, OneLineFit.ReadableMin(42));
            Assert.LessOrEqual(label.preferredWidth, label.rectTransform.rect.width, "칸 안에");

            label.text = "Submandibular gland (submaxillary gland, Wharton's duct)";
            yield return null;
            Assert.IsTrue(label.resizeTextForBestFit, "가장 작게도 안 들어가면 예전처럼 여러 줄(bestFit)");
            Assert.AreEqual(HorizontalWrapMode.Wrap, label.horizontalOverflow);

            label.text = "deadline";
            yield return null;
            Assert.AreEqual(42, label.fontSize, "다시 짧은 단어면 원래대로");
            Assert.AreEqual(HorizontalWrapMode.Overflow, label.horizontalOverflow);
        }

        // 결산의 단어 줄 (#287 캡처: 두 줄이 되어 아래 줄과 겹쳤음) — 한 줄 칸은 가장 작게도 넘치면 끝을 '...'
        [UnityTest]
        public IEnumerator OneLineRowsCutTheEndInsteadOfSpillingIntoTheNextRow()
        {
            var box = UiKit.Rect("Row", phone, 0, 0, 0, 0);
            box.sizeDelta = new Vector2(308, 56);
            var label = UiKit.Display(UiKit.OneLine(UiKit.Label("English", box, "", 46, Color.white, 0, 0, 1, 1,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 26)));
            OneLineFit.Apply(label, -1, OneLineFit.WhenTooLong.Ellipsis);
            label.text = "transverse plane (horizontal plane)";
            yield return null;
            Assert.AreEqual(1, Lines(label), "한 줄");
            Assert.AreEqual(26, label.fontSize);
            StringAssert.EndsWith(OneLineFit.Dots, label.text);
            StringAssert.StartsWith("transverse plane", label.text);
            Assert.LessOrEqual(label.preferredWidth, 308f, "칸 안에");
            yield return null;
            StringAssert.EndsWith(OneLineFit.Dots, label.text, "자른 글을 다시 자르지 않음");
            Assert.AreEqual(label.text.IndexOf(OneLineFit.Dots), label.text.LastIndexOf(OneLineFit.Dots), "'...' 한 번");

            label.text = "due";
            yield return null;
            Assert.AreEqual("due", label.text, "새 글은 그대로");
            Assert.AreEqual(46, label.fontSize);
        }

        // 단어장 고르기 카드의 예시 단어 칩: 모두 한 줄 · 모든 카드가 같은 글자 크기 · 칩이 줄 안에
        [UnityTest]
        public IEnumerator CourseSampleWordsStayOnOneLineWithOneSize()
        {
            WordDatabase Book(params WordEntry[] words)
            {
                var book = ScriptableObject.CreateInstance<WordDatabase>();
                book.ReplaceWords(words);
                return book;
            }
            var basic = Book(new WordEntry("access", "access", "접근, 접속"), new WordEntry("choice", "choice", "선택"), new WordEntry("evidence", "evidence", "증거"));
            var business = Book(new WordEntry("biz_invoice", "invoice", "청구서, 송장"), new WordEntry("biz_deadline", "deadline", "마감 기한"),
                new WordEntry("biz_negotiate", "negotiate", "협상하다"));
            WordCourse Course(string id, string name, int order, string[] samples, params CourseBook[] books)
            {
                var course = ScriptableObject.CreateInstance<WordCourse>();
                course.Configure(id, name, name + " 설명", order, samples, books);
                return course;
            }
            var courses = new[]
            {
                Course("suneung", "수능·내신 영단어", 1, new[] { "access", "choice", "evidence" }),
                Course("business", "비즈니스 영단어", 2, new[] { "biz_invoice", "biz_deadline", "biz_negotiate" }, new CourseBook(basic, business)),
            };
            var view = CourseSelectView.Create(phone);
            view.Show(courses, new[] { basic }, _ => { });
            yield return null;
            yield return null;

            var chips = view.Root.GetComponentsInChildren<Text>().Where(t => t.transform.parent.name.StartsWith("Word_")).ToList();
            Assert.AreEqual(6, chips.Count);
            foreach (var chip in chips) Assert.AreEqual(1, Lines(chip), $"{chip.text} 한 줄");
            Assert.AreEqual(1, chips.Select(c => c.fontSize).Distinct().Count(), "모든 카드가 같은 크기 (통일감)");
            Assert.AreEqual(view.SampleWordFontSize, chips[0].fontSize);
            Assert.Less(view.SampleWordFontSize, 26, "이 너비에선 비즈니스 칩이 넘쳐서 조금 줄임");
            Assert.IsTrue(view.SampleWordsFit, "줄마다 칩이 다 들어감");
            foreach (var chip in chips)
            {
                var pill = (RectTransform)chip.transform.parent;
                var row = (RectTransform)pill.parent;
                Assert.LessOrEqual(pill.offsetMax.x, row.rect.width + 0.5f, $"{chip.text} 칩이 줄 밖으로 안 나감");
                Assert.LessOrEqual(chip.preferredWidth, pill.rect.width + 0.5f, $"{chip.text} 글이 칩 안에");
            }
        }
    }
}
