using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 누군가의 기록 (#200): 도감을 채우다 보면 저절로 읽히는 이스터에그
    public class JournalTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

        private static WordDatabase Book(params WordEntry[] words)
        {
            var book = ScriptableObject.CreateInstance<WordDatabase>();
            book.ReplaceWords(words);
            return book;
        }

        private static IEnumerable<WordDatabase> RealBooks() =>
            AssetDatabase.FindAssets("t:WordDatabase", new[] { "Assets/Data/Words" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<WordDatabase>(AssetDatabase.GUIDToAssetPath(guid)));

        private static IEnumerable<FieldArea> RealAreas() =>
            AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data/Areas" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(guid)));

        [Test]
        public void LinesFollowTheStoryOrderAndLeavePlainWordsOut()
        {
            var book = Book(
                new WordEntry("a", "apple", "사과", "n", "An apple.", "사과.", 2),
                new WordEntry("b", "bread", "빵", "n", "Plain bread.", "빵."),
                new WordEntry("c", "cat", "고양이", "n", "A cat.", "고양이.", 1, "첫날"));

            var lines = Journals.Lines(book, new VocabularyProgress());

            CollectionAssert.AreEqual(new[] { "c", "a" }, lines.Select(l => l.Word.Id).ToArray(), "기록 순서대로, 예문만 있는 단어는 빠짐");
            Assert.AreEqual("첫날", lines[0].Heading);
            Assert.AreEqual("", lines[1].Heading);
        }

        [Test]
        public void ALineBecomesReadableOnceItsWordIsFoundEvenIfAnsweredWrong()
        {
            var book = Book(
                new WordEntry("a", "apple", "사과", "n", "An apple.", "사과.", 1),
                new WordEntry("b", "bread", "빵", "n", "Some bread.", "빵.", 2),
                new WordEntry("c", "cat", "고양이", "n", "A cat.", "고양이.", 3));
            var vocabulary = new VocabularyProgress();
            Assert.IsFalse(Journals.Lines(book, vocabulary).Any(l => l.Readable), "처음에는 모두 못 읽음");

            var rules = new MasteryRules();
            vocabulary.RecordAnswer("a", true, Now, rules);
            vocabulary.RecordAnswer("b", false, Now, rules);

            CollectionAssert.AreEqual(new[] { true, true, false }, Journals.Lines(book, vocabulary).Select(l => l.Readable).ToArray(),
                "도감의 '발견'과 같은 기준: 맞히든 틀리든 만나면 읽힘");
        }

        [Test]
        public void HavingTheJournalMeansHavingItsItem()
        {
            var item = TestData.Item("journal_test");
            var book = Book();
            var inventory = new Inventory();
            Assert.IsFalse(Journals.Has(book, inventory), "기록이 없는 단어장");
            Assert.IsFalse(Journals.Has(null, inventory));

            book.Set("journal", item);
            Assert.IsFalse(Journals.Has(book, inventory), "아직 상자에서 못 얻음");
            inventory.Add(item);
            Assert.IsTrue(Journals.Has(book, inventory));
        }

        [TestCase("We lose a word.", "lose", 3)]
        [TestCase("Language keeps us.", "language", 0)]
        [TestCase("The scar is on my car.", "car", 18)]
        [TestCase("Nothing here.", "cat", -1)]
        public void WordSpanFindsTheWholeWord(string sentence, string word, int expected)
        {
            Assert.AreEqual(expected, Journals.WordSpan(sentence, word).start);
        }

        [Test]
        public void TheSameWordAlwaysGetsTheSameSmudge()
        {
            Assert.AreEqual(Journals.StableHash("lose"), Journals.StableHash("lose"));
            Assert.AreNotEqual(Journals.StableHash("lose"), Journals.StableHash("loss"));
        }

        // --- 실제 데이터 ---

        [Test]
        public void EveryWordHasAnExampleThatContainsIt()
        {
            foreach (var book in RealBooks())
            foreach (var word in book.Words)
            {
                Assert.IsNotEmpty(word.Example, $"{word.Id}: 예문");
                Assert.IsNotEmpty(word.ExampleMeaning, $"{word.Id}: 해석");
                Assert.GreaterOrEqual(Journals.WordSpan(word.Example, word.English).start, 0, $"{word.Id}: 예문에 단어가 그대로 ({word.Example})");
                Assert.IsFalse(Regex.IsMatch(word.Example, @"\b(he|she|his|her|him)\b", RegexOptions.IgnoreCase), $"{word.Id}: 성별 대명사 ({word.Example})");
            }
        }

        [Test]
        public void EachBookTellsOneStoryWithoutGapsFromItsFirstHeading()
        {
            int books = 0;
            foreach (var book in RealBooks())
            {
                var lines = Journals.Lines(book, new VocabularyProgress());
                Assert.IsNotNull(book.Journal, $"{book.name}: 기록 아이템");
                CollectionAssert.AreEqual(Enumerable.Range(1, lines.Count).ToArray(), lines.Select(l => l.Word.Story).ToArray(), $"{book.name}: 줄 번호 1부터 빠짐없이");
                Assert.IsNotEmpty(lines[0].Heading, $"{book.name}: 첫 줄에 묶음 제목");
                books++;
            }
            Assert.AreEqual(2, books, "지금은 일기·생존일지 둘");
        }

        [Test]
        public void EachJournalIsAJournalItemInAChestOfItsOwnRegion()
        {
            foreach (var book in RealBooks())
            {
                var journal = book.Journal;
                Assert.AreEqual(ItemKind.Journal, journal.Kind, journal.name);
                Assert.IsTrue(journal.IsJournal);
                var chests = RealAreas().Where(area => area.Words == book)
                    .SelectMany(area => area.ChestContents).Where(chest => chest.Item == journal).ToList();
                Assert.AreEqual(1, chests.Count, $"{journal.DisplayName}: 이 단어장을 쓰는 지역의 상자 하나에서 얻음");
                Assert.AreEqual(1, chests[0].Count);
            }
        }

        // 있던 상자의 내용물은 그대로 (이미 연 사람이 못 얻거나 자리가 밀리면 안 됨)
        [Test]
        public void TheNewChestsDoNotMoveTheOldChestContents()
        {
            var library = RealAreas().First(a => a.name == "library");
            var forest = RealAreas().First(a => a.name == "forest");
            Assert.AreEqual(6, library.ChestContents.Count);
            Assert.AreEqual(7, forest.ChestContents.Count);
            Assert.AreEqual(120, library.ChestContents[0].Gold, "서고 첫 상자");
            Assert.IsTrue(library.ChestContents[1].Item.IsJournal, "서고 둘째 줄의 새 상자");
            Assert.AreEqual("relic_lantern", library.ChestContents[2].Relic.RelicId, "서쪽 열람실");
            Assert.IsTrue(forest.ChestContents[5].Item.IsJournal, "야영지 아래 새 상자");
            Assert.AreEqual(150, forest.ChestContents[6].Gold, "버섯 공터 아래 구석");
        }

        [Test]
        public void TheSmudgeSheetIsThere()
        {
            var sheet = Resources.Load<Texture2D>("UI/Journal/smudge");
            Assert.IsNotNull(sheet, "python Tools/make_journal_smudge.py");
            Assert.AreEqual(1024, sheet.width);
            Assert.AreEqual(1024, sheet.height);
        }
    }
}
