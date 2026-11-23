using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Save;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 철마다 여는 선물 퀴즈 (#274 사용자: 12월 1~31일에 켠 사람에게 크리스마스 단어 3문제 — 기본 선물 + 맞힌 문제마다 특제 상처약 1 + 살금살금 물약 1,
    // 받기 전엔 비밀, 받은 뒤 개발자의 편지)
    public class GiftEventTests
    {
        private static readonly DateTime December = new DateTime(2026, 12, 5, 20, 0, 0);

        // 선물 퀴즈 단어장 (다른 테스트가 '지역 단어장' 검사에서 뺄 때 씀 — 지역 도감이 아님)
        public static HashSet<WordDatabase> EventBooks() =>
            new HashSet<WordDatabase>(AssetDatabase.FindAssets("t:GiftEvent", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GiftEvent>(AssetDatabase.GUIDToAssetPath(g)))
                .Select(e => e.Words).Where(b => b != null));

        private static WordDatabase Book(int count)
        {
            var book = ScriptableObject.CreateInstance<WordDatabase>();
            book.ReplaceWords(Enumerable.Range(0, count).Select(i => new WordEntry($"x{i}", $"word{i}", $"뜻{i}", "n")).ToList());
            return book;
        }

        private static GiftEvent Gift(WordDatabase book, ItemData great, ItemData quiet, int fromMonth = 12, int fromDay = 1, int toMonth = 12, int toDay = 31)
        {
            var gift = ScriptableObject.CreateInstance<GiftEvent>();
            gift.Configure("christmas", "크리스마스 선물", "메리 크리스마스!", "크리스마스 선물 퀴즈", "크리스마스 영단어",
                fromMonth, fromDay, toMonth, toDay, 3, book,
                new[] { new ItemGift(great, 1), new ItemGift(quiet, 1) }, new[] { new ItemGift(great, 1), new ItemGift(quiet, 1) });
            return gift;
        }

        private static readonly Heroes.HeroData HeroData = TestData.Hero(new MonsterStats(30, 10, 10));
        private static GameSession Session() => GameSession.NewGame(HeroData);

        [Test]
        public void OpensFromDecemberFirstToTheThirtyFirst()
        {
            var gift = Gift(Book(10), TestData.Item("great"), TestData.Item("quiet"));
            Assert.IsFalse(gift.IsOpen(new DateTime(2026, 11, 30, 23, 59, 59)));
            Assert.IsTrue(gift.IsOpen(new DateTime(2026, 12, 1, 0, 0, 0)));
            Assert.IsTrue(gift.IsOpen(new DateTime(2026, 12, 31, 23, 59, 59)));
            Assert.IsFalse(gift.IsOpen(new DateTime(2027, 1, 1, 0, 0, 0)));
            Assert.AreEqual("christmas_2026", gift.ClaimId(December));
            Assert.AreEqual("12월 31일", gift.EndLabel);

            // 해를 넘기는 기간이면 1월에 받아도 지난해 것과 같은 한 번
            var winter = Gift(Book(10), TestData.Item("great"), TestData.Item("quiet"), 12, 20, 1, 5);
            Assert.IsTrue(winter.IsOpen(new DateTime(2027, 1, 3)));
            Assert.IsFalse(winter.IsOpen(new DateTime(2027, 1, 6)));
            Assert.AreEqual("christmas_2026", winter.ClaimId(new DateTime(2027, 1, 3)));
            Assert.AreEqual("christmas_2026", winter.ClaimId(new DateTime(2026, 12, 24)));
        }

        [Test]
        public void EachSaveGetsItOncePerYear()
        {
            var great = TestData.Item("great");
            var quiet = TestData.Item("quiet");
            var gift = Gift(Book(10), great, quiet);
            var gifts = new[] { gift };
            var session = Session();

            Assert.IsNull(GiftEvents.Offer(session, gifts, new DateTime(2026, 11, 30)), "11월엔 없음");
            Assert.AreSame(gift, GiftEvents.Offer(session, gifts, December));
            GiftEvents.Claim(session, gift, 2, December);
            Assert.IsNull(GiftEvents.Offer(session, gifts, new DateTime(2026, 12, 25)), "올해는 한 번만");
            Assert.IsEmpty(GiftEvents.Claim(session, gift, 3, new DateTime(2026, 12, 25)), "두 번 받지 않음");
            Assert.AreEqual(3, session.Inventory.GetCount(great), "기본 1 + 맞힌 2");

            // 세이브를 거쳐도 그대로, 내년 12월엔 다시
            var loaded = GameSession.FromSaveData(session.ToSaveData(DateTime.UtcNow), null, HeroData);
            Assert.IsNull(GiftEvents.Offer(loaded, gifts, December));
            Assert.AreSame(gift, GiftEvents.Offer(loaded, gifts, new DateTime(2027, 12, 1)));
        }

        // #274 사용자: 하나도 못 맞혀도 기본 선물 — 선물 상자 = 기본 1 + 맞힌 수
        [Test]
        public void BasicGiftPlusOneOfEachPerCorrectAnswer()
        {
            var great = TestData.Item("great");
            var quiet = TestData.Item("quiet");
            var gift = Gift(Book(10), great, quiet);
            Assert.AreEqual(1, GiftEvents.Boxes(gift, 0));
            Assert.AreEqual(4, GiftEvents.Boxes(gift, 3));

            var session = Session();
            var given = GiftEvents.Claim(session, gift, 2, December);
            Assert.AreEqual(3, session.Inventory.GetCount(great));
            Assert.AreEqual(3, session.Inventory.GetCount(quiet));
            CollectionAssert.AreEqual(new[] { "great:3", "quiet:3" }, given.Select(g => $"{g.Item.ItemId}:{g.Count}").ToArray(), "같은 아이템은 한 줄로");

            var none = Session();
            var basic = GiftEvents.Claim(none, gift, 0, December);
            Assert.IsEmpty(none.Mailbox.All.Where(m => !m.Deleted), "편지가 없는 선물 퀴즈면 메일도 없음 (새 게임이 건너뛴 업데이트 안내 기록은 빼고 #286)");
            Assert.AreEqual(1, none.Inventory.GetCount(great), "하나도 못 맞혀도 기본 선물");
            Assert.AreEqual(1, none.Inventory.GetCount(quiet));
            Assert.AreEqual(2, basic.Count);
            Assert.IsNull(GiftEvents.Offer(none, new[] { gift }, December), "올해 한 번은 쓴 것");
        }

        [Test]
        public void QuizAsksDifferentWordsAndTakesOneAnswerEach()
        {
            var book = Book(10);
            var quiz = new GiftQuiz(book.Words, 3, new System.Random(7));
            Assert.AreEqual(3, quiz.Count);
            var asked = new List<string>();
            int correct = 0;
            for (int i = 0; i < 3; i++)
            {
                var question = quiz.Current;
                asked.Add(question.Word.Id);
                Assert.AreEqual(QuizDirection.EnglishToMeaning, question.Direction);
                Assert.AreEqual(4, question.Choices.Count);
                Assert.AreEqual(question.Choices.Count, question.Choices.Distinct().Count());
                int pick = i == 1 ? (question.CorrectIndex + 1) % 4 : question.CorrectIndex; // 두 번째만 틀림
                var answer = quiz.Answer(pick);
                Assert.AreEqual(i == 1 ? GiftAnswer.Wrong : GiftAnswer.Correct, answer);
                if (answer == GiftAnswer.Correct) correct++;
                Assert.AreEqual(GiftAnswer.Ignored, quiz.Answer(question.CorrectIndex), "틀린 뒤 다시 골라도 선물 없음");
                quiz.Next();
            }
            Assert.IsTrue(quiz.IsComplete);
            Assert.AreEqual(3, asked.Distinct().Count(), "같은 단어를 두 번 묻지 않음");
            Assert.AreEqual(2, quiz.CorrectCount);
            Assert.AreEqual(correct, quiz.CorrectCount);
            CollectionAssert.AreEqual(new[] { true, false, true }, quiz.Results.ToArray());
            Assert.AreEqual(GiftAnswer.Ignored, quiz.Answer(0));
        }

        [Test]
        public void QuizDoesNotTouchVocabulary()
        {
            var session = Session();
            var quiz = new GiftQuiz(Book(10).Words, 3, new System.Random(1));
            while (!quiz.IsComplete)
            {
                quiz.Answer(quiz.Current.CorrectIndex);
                quiz.Next();
            }
            Assert.AreEqual(0, session.Vocabulary.Entries.Count, "선물 퀴즈 단어는 도감·숙련도에 들어가지 않음");
        }

        [Test]
        public void TooFewWordsIsNotOffered()
        {
            var gift = Gift(Book(3), TestData.Item("great"), TestData.Item("quiet"));
            Assert.IsFalse(GiftEvents.IsPlayable(gift), "보기 4개를 못 만듦");
            Assert.IsNull(GiftEvents.Offer(Session(), new[] { gift }, December));
        }

        // 실제 데이터: 크리스마스 선물 퀴즈 (12월 1~31일, 3문제, 맞힐 때마다 특제 상처약 1 + 살금살금 물약 1)
        [Test]
        public void RealChristmasGiftIsSetUp()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var gift = database.SeasonalEvents.SingleOrDefault(e => e != null && e.EventId == "christmas");
            Assert.IsNotNull(gift, "GameDatabase에 크리스마스 선물 퀴즈가 없음 (WordRPG > Data > Build Gift Events)");
            Assert.IsTrue(GiftEvents.IsPlayable(gift));
            Assert.AreEqual((12, 1, 12, 31), (gift.StartMonth, gift.StartDay, gift.EndMonth, gift.EndDay));
            Assert.AreEqual(3, gift.Questions);
            CollectionAssert.AreEqual(new[] { "potion_great:1", "potion_quiet:1" },
                gift.BaseGift.Select(r => $"{r.Item.ItemId}:{r.Count}").ToArray(), "기본 선물");
            CollectionAssert.AreEqual(new[] { "potion_great:1", "potion_quiet:1" },
                gift.RewardPerCorrect.Select(r => $"{r.Item.ItemId}:{r.Count}").ToArray(), "맞힐 때마다");
            foreach (var line in gift.BaseGift.Concat(gift.RewardPerCorrect))
                Assert.AreSame(line.Item, database.FindItem(line.Item.ItemId), $"{line.Item.ItemId}가 GameDatabase에 없음");

            // 개발자의 편지 (사용자가 쓴 글) — 받으면 메일함에도 (#275)
            Assert.IsTrue(gift.HasLetter);
            var letter = gift.Letter;
            Assert.AreSame(letter, database.FindMail(letter.MailId), "편지가 GameDatabase에 없음 (Refresh Game Database)");
            Assert.AreEqual("christmas_2026_letter", letter.MailId);
            Assert.AreEqual(MailDelivery.ByEvent, letter.Delivery, "선물을 받을 때 옴 (켜자마자가 아니라)");
            Assert.AreEqual("개발자의 편지", letter.Tag);
            StringAssert.StartsWith("사랑하는 모험가 여러분!", letter.Body);
            StringAssert.Contains("Merry Christmas", letter.Closing);
            StringAssert.StartsWith("From.", letter.Sign);
            Assert.IsNotNull(Resources.Load<Sprite>("Art/Event/" + letter.Art), "편지 머리 그림");

            var words = gift.Words.Words;
            Assert.GreaterOrEqual(words.Count, 20);
            foreach (var word in words)
            {
                StringAssert.StartsWith("xmas_", word.Id, "선물 퀴즈 단어 id는 xmas_로 (지역 단어와 겹치지 않게)");
                Assert.IsFalse(string.IsNullOrWhiteSpace(word.Meaning), $"{word.English} 뜻이 비어 있음");
            }
            for (int i = 0; i < words.Count; i++)
                for (int j = i + 1; j < words.Count; j++)
                    Assert.IsFalse(QuizGenerator.MeaningsOverlap(words[i], words[j]), $"{words[i].English}·{words[j].English} 뜻이 겹침 (정답이 둘이 됨)");

            // 어느 단어가 나와도 보기 4개
            for (int seed = 0; seed < 30; seed++)
            {
                var quiz = new GiftQuiz(words, gift.Questions, new System.Random(seed));
                while (!quiz.IsComplete)
                {
                    Assert.AreEqual(4, quiz.Current.Choices.Count);
                    quiz.Answer(0);
                    quiz.Next();
                }
            }
            Assert.IsFalse(database.Areas.Any(a => a.Words == gift.Words), "선물 퀴즈 단어장은 지역 단어장이 아님");
        }
    }
}
