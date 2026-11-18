using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 크리스마스 선물 퀴즈 (#274): 12월에 필드에 서 있으면 안내 창(선물은 비밀) → [퀴즈 풀기] → 3문제(문제마다 한 번)
    // → 선물 상자(기본 1 + 맞힌 수) [열어 보기] → 특제 상처약·살금살금 물약 → [편지 읽기] → 개발자의 편지 → 올해는 다시 안 뜸
    public class GiftQuizScreenTests
    {
        private const string Map = "#######\n#..P..#\n#######";
        private static readonly DateTime December = new DateTime(2026, 12, 5, 20, 0, 0, DateTimeKind.Local);

        private GameObject go;
        private ItemData great, quiet;
        private GiftEvent gift;
        private MailData letter;

        private FieldScreen CreateField(GameSession session, DateTime now)
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "gift_test").Set("displayName", "초원").Set("map", Map).Set("words", words);

            great = TestData.Potion("potion_great", 60).Set("displayName", "특제 상처약");
            quiet = TestData.Item("potion_quiet").Set("displayName", "살금살금 물약");
            var christmas = ScriptableObject.CreateInstance<WordDatabase>();
            christmas.ReplaceWords(new List<WordEntry>
            {
                new WordEntry("xmas_reindeer", "reindeer", "순록", "n"),
                new WordEntry("xmas_sleigh", "sleigh", "썰매", "n"),
                new WordEntry("xmas_chimney", "chimney", "굴뚝", "n"),
                new WordEntry("xmas_wreath", "wreath", "화환", "n"),
                new WordEntry("xmas_candle", "candle", "양초", "n"),
                new WordEntry("xmas_bell", "bell", "종", "n"),
            });
            gift = ScriptableObject.CreateInstance<GiftEvent>();
            gift.Configure("christmas", "크리스마스 선물", "메리 크리스마스!", "크리스마스 선물 퀴즈", "크리스마스 영단어",
                12, 1, 12, 31, 3, christmas,
                new[] { new ItemGift(great, 1), new ItemGift(quiet, 1) }, new[] { new ItemGift(great, 1), new ItemGift(quiet, 1) });
            letter = ScriptableObject.CreateInstance<MailData>();
            letter.Configure("christmas_letter_test", "개발자", "크리스마스 편지", "개발자의 편지", "A Christmas Letter from the Developer", "tree",
                "사랑하는 모험가 여러분!\n저희 게임과 함께해 주셔서 진심으로 감사합니다.", "Merry Christmas & Happy New Adventure!", "From. Your Game Developer");
            gift.ConfigureLetter(letter);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new[] { great, quiet }, new[] { area }, newGiftEvents: new[] { gift },
                newMails: new[] { letter });

            go = new GameObject("GiftFieldUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            field.LocalClock = () => now;
            return field;
        }

        private static GameSession Session(bool played)
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)), 3);
            if (played) session.Record.RecordBattle(true, 3, 0);
            return session;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        private IEnumerator Press(FieldScreen field, string name)
        {
            Button button = null;
            yield return WaitFor(() => (button = ActiveButton(field.transform, name)) != null, 3f);
            Assert.IsNotNull(button, $"{name} 버튼이 없음");
            button.onClick.Invoke();
            yield return null;
        }

        // 지금 문제의 정답 칸 (영단어 → 뜻)
        private int CorrectChoice(GiftQuizView view)
        {
            string meaning = gift.Words.Words.Single(w => w.English == view.Word).Meaning;
            for (int i = 0; i < 4; i++)
                if (view.ChoiceText(i) == meaning) return i;
            Assert.Fail($"{view.Word}의 정답 칸이 없음");
            return -1;
        }

        [UnityTest]
        public IEnumerator ThreeQuestionsGiveOneOfEachPerCorrectAnswer()
        {
            var session = Session(played: true);
            var field = CreateField(session, December);
            yield return WaitFor(() => field.GiftOpen, 3f);
            Assert.IsTrue(field.GiftOpen, "12월에 필드에 서 있으면 안내 창");
            var view = field.GiftView;
            Assert.AreEqual(GiftQuizView.Stage.Intro, view.Current);
            Assert.AreEqual("메리 크리스마스!", view.Title);
            StringAssert.Contains("12월 31일까지", view.Note);
            Assert.AreEqual(4, view.MysteryBoxCount, "기본 1 + 3문제");
            StringAssert.DoesNotContain("상처약", view.AllIntroText, "선물은 받기 전까지 비밀 (사용자)");
            StringAssert.DoesNotContain("물약", view.AllIntroText);
            Assert.IsTrue(field.IsPanelOpen, "창이 떠 있는 동안 움직이지 않음");

            yield return Press(field, "GiftStartButton");
            Assert.AreEqual(GiftQuizView.Stage.Question, view.Current);
            Assert.AreEqual("1 / 3", view.Counter);
            Assert.AreEqual("뜻은?", view.Ask);

            // 1번 맞힘
            int correct = CorrectChoice(view);
            yield return Press(field, $"GiftChoice_{correct}");
            Assert.AreEqual(GiftQuizView.Stage.Answered, view.Current);
            StringAssert.StartsWith("정답!", view.Ask);
            Assert.AreEqual(Palette.Good, view.ChoiceColor(correct));
            Assert.IsNull(ActiveButton(field.transform, $"GiftChoice_{(correct + 1) % 4}"), "답한 뒤엔 다시 못 고름");
            Assert.AreEqual("다음", view.NextLabel);
            yield return Press(field, "GiftNextButton");

            // 2번 틀림 → 고른 것 빨강, 정답 초록
            Assert.AreEqual("2 / 3", view.Counter);
            correct = CorrectChoice(view);
            int wrong = (correct + 1) % 4;
            yield return Press(field, $"GiftChoice_{wrong}");
            StringAssert.StartsWith("아쉬워요!", view.Ask);
            Assert.AreEqual(Palette.Bad, view.ChoiceColor(wrong));
            Assert.AreEqual(Palette.Good, view.ChoiceColor(correct));
            Assert.IsNotEmpty(view.Meaning, "정답 뜻을 보여 줌");
            yield return Press(field, "GiftNextButton");

            // 3번 맞힘 → [선물 받기]
            Assert.AreEqual("3 / 3", view.Counter);
            yield return Press(field, $"GiftChoice_{CorrectChoice(view)}");
            Assert.AreEqual("선물 받기", view.NextLabel);
            Assert.AreEqual(0, session.Inventory.GetCount(great), "다 풀기 전엔 아직 안 넣음");
            yield return Press(field, "GiftNextButton");

            // 선물 상자 = 기본 1 + 맞힌 2. 이미 가방에 넣고 저장했지만 무엇인지는 열 때까지 비밀
            Assert.AreEqual(GiftQuizView.Stage.Unwrap, view.Current);
            Assert.AreEqual("선물 상자 3개!", view.Title);
            Assert.AreEqual(3, view.BoxCount);
            Assert.AreEqual(3, session.Inventory.GetCount(great), "기본 1 + 맞힌 2");
            Assert.AreEqual(3, session.Inventory.GetCount(quiet));
            Assert.IsTrue(session.World.HasEvent("christmas_2026"));

            yield return Press(field, "GiftOpenButton");
            Assert.AreEqual(GiftQuizView.Stage.Opening, view.Current, "흔들리다 터짐");
            yield return WaitFor(() => view.Current == GiftQuizView.Stage.Result, 3f);
            Assert.AreEqual("선물을 받았어요!", view.Title);
            StringAssert.Contains("특제 상처약", view.Rewards);
            StringAssert.Contains("×3", view.Rewards);
            Assert.AreEqual("편지 읽기", view.ResultButtonLabel);

            yield return Press(field, "GiftDoneButton");
            Assert.AreEqual(GiftQuizView.Stage.Letter, view.Current);
            Assert.AreEqual("A Christmas Letter from the Developer", view.Title);
            StringAssert.StartsWith("사랑하는 모험가 여러분!", view.LetterText);
            StringAssert.Contains("From. Your Game Developer", view.LetterText);
            Assert.IsNotNull(session.Mailbox.Find(letter.MailId), "편지는 메일함에도 (#275)");
            Assert.IsTrue(session.Mailbox.Find(letter.MailId).Read, "여기서 읽었으니 읽음");
            Assert.IsFalse(field.MailDotVisible);
            field.HandleBack(); // 편지에서 뒤로가기 = 닫기
            yield return null;
            Assert.IsFalse(field.GiftOpen);

            // 메일함에서 다시 읽기
            yield return Press(field, "MailButton");
            var mailbox = field.MailboxScreen;
            Assert.AreEqual(1, mailbox.RowCount);
            StringAssert.Contains("크리스마스 편지", mailbox.RowText(0));
            Assert.IsFalse(mailbox.RowIsUnread(0));
            yield return Press(field, "MailRow_christmas_letter_test");
            Assert.IsTrue(mailbox.IsReading);
            Assert.AreEqual("A Christmas Letter from the Developer", mailbox.LetterTitle);
            StringAssert.StartsWith("사랑하는 모험가 여러분!", mailbox.LetterText);
            field.HandleBack(); // 편지 → 메일함
            yield return null;
            Assert.IsFalse(mailbox.IsReading);
            Assert.IsTrue(mailbox.IsOpen);
            field.HandleBack(); // 메일함 닫기
            yield return null;
            Assert.IsFalse(field.IsPanelOpen);
            Assert.IsFalse(field.IsPanelOpen);
            Assert.IsNull(GiftEvents.Offer(session, new[] { gift }, December), "올해는 다시 안 뜸");
        }

        // #275 사용자: 그 시기에 들어오면 바로 (싸운 적이 없어도)
        [UnityTest]
        public IEnumerator ComesUpRightAwayAndCanBePutOff()
        {
            var session = Session(played: false);
            var field = CreateField(session, December);
            yield return WaitFor(() => field.GiftOpen, 2f);
            Assert.IsTrue(field.GiftOpen, "들어오자마자 뜸");

            field.HandleBack(); // 뒤로가기 = [나중에]
            yield return null;
            Assert.IsFalse(field.GiftOpen);
            Assert.IsFalse(session.World.HasEvent("christmas_2026"), "나중에 = 받지 않은 것");
            Assert.AreEqual(0, session.Inventory.GetCount(great));
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsFalse(field.GiftOpen, "이번에 켠 동안은 다시 안 뜸 (다음에 켜면)");
        }

        [UnityTest]
        public IEnumerator NotOfferedOutsideDecember()
        {
            var field = CreateField(Session(played: true), new DateTime(2026, 11, 30, 22, 0, 0, DateTimeKind.Local));
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsFalse(field.GiftOpen);
        }
    }
}
