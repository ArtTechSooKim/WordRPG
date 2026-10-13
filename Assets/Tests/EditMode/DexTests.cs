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
    public class DexTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        private MasteryRules rules;
        private ItemData keepsake;
        private WordDatabase region;
        private MonsterSpecies nib;
        private GameDatabase database;

        [SetUp]
        public void SetUp()
        {
            rules = new MasteryRules();
            keepsake = TestData.Item("keepsake_meadow");
            region = ScriptableObject.CreateInstance<WordDatabase>()
                .Set("regionId", "meadow").Set("regionName", "초원")
                .Set("completionKeepsake", keepsake).Set("completionGold", 500);
            region.ReplaceWords(TestData.SampleWords().Take(3));
            nib = TestData.Species("nib", new MonsterStats(30, 14, 8), new MonsterStats(4, 3, 1));
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { nib }, new[] { keepsake });
        }

        private GameSession NewSession() => GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1);

        private void Discover(GameSession session, params int[] wordIndexes)
        {
            foreach (int i in wordIndexes)
                session.Vocabulary.RecordAnswer(region.Words[i].Id, true, T0, rules);
        }

        [Test]
        public void ProgressCountsDiscoveredAndMastered()
        {
            var session = NewSession();
            Assert.AreEqual(0, Dex.GetProgress(region, session.Vocabulary).Discovered);

            Discover(session, 0);
            session.Vocabulary.RecordAnswer(region.Words[1].Id, false, T0, rules); // 틀려도 '발견'

            var progress = Dex.GetProgress(region, session.Vocabulary);
            Assert.AreEqual(2, progress.Discovered);
            Assert.AreEqual(3, progress.Total);
            Assert.AreEqual(0, progress.Mastered);
            Assert.IsFalse(progress.IsComplete);
            Assert.AreEqual(2f / 3f, progress.Ratio, 0.001f);
        }

        [Test]
        public void IncompleteDexGivesNoReward()
        {
            var session = NewSession();
            Discover(session, 0, 1);

            Assert.AreEqual(0, session.ClaimDexRewards(new[] { region }).Count);
            Assert.AreEqual(0, session.Inventory.Gold);
        }

        [Test]
        public void CompletingTheDexGivesKeepsakeAndGoldExactlyOnce()
        {
            var session = NewSession();
            Discover(session, 0, 1, 2);

            var first = session.ClaimDexRewards(new[] { region });
            var second = session.ClaimDexRewards(new[] { region });

            Assert.AreEqual(1, first.Count);
            Assert.AreSame(region, first[0].Region);
            Assert.AreEqual(0, second.Count, "두 번째에는 지급하지 않는다");
            Assert.AreEqual(500, session.Inventory.Gold);
            Assert.AreEqual(1, session.Inventory.GetCount(keepsake));
        }

        [Test]
        public void ClaimedRewardSurvivesSaveAndIsNotGivenAgain()
        {
            var session = NewSession();
            Discover(session, 0, 1, 2);
            session.ClaimDexRewards(new[] { region });

            var json = session.ToSaveData(T0).ToJson();
            var loaded = GameSession.FromSaveData(SaveData.FromJson(json), database, TestData.Hero(new MonsterStats(30, 14, 8)));

            Assert.IsTrue(loaded.Record.HasClaimedRegion("meadow"));
            Assert.AreEqual(0, loaded.ClaimDexRewards(new[] { region }).Count);
            Assert.AreEqual(500, loaded.Inventory.Gold, "불러온 뒤 중복 지급 없음");
            Assert.AreEqual(1, loaded.Inventory.GetCount(keepsake));
        }

        [Test]
        public void NewWordsAddedLaterDoNotTakeBackTheReward()
        {
            var session = NewSession();
            Discover(session, 0, 1, 2);
            session.ClaimDexRewards(new[] { region });

            region.ReplaceWords(TestData.SampleWords().Take(5)); // 콘텐츠 확장
            Assert.IsFalse(Dex.GetProgress(region, session.Vocabulary).IsComplete);

            Assert.IsTrue(session.Record.HasClaimedRegion("meadow"));
            Assert.AreEqual(1, session.Inventory.GetCount(keepsake));
        }

        [Test]
        public void RegionWithoutIdOrWordsNeverRewards()
        {
            var session = NewSession();
            var noId = ScriptableObject.CreateInstance<WordDatabase>().Set("completionGold", 99);
            noId.ReplaceWords(TestData.SampleWords().Take(1));
            var empty = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "empty").Set("completionGold", 99);
            session.Vocabulary.RecordAnswer(noId.Words[0].Id, true, T0, rules);

            Assert.AreEqual(0, session.ClaimDexRewards(new[] { noId, empty }).Count);
            Assert.AreEqual(0, session.Inventory.Gold);
        }

        [Test]
        public void StarsMatchMasteryLevel()
        {
            Assert.AreEqual("", Dex.Stars(MasteryLevel.New));
            Assert.AreEqual("★☆☆☆", Dex.Stars(MasteryLevel.Learning));
            Assert.AreEqual("★★☆☆", Dex.Stars(MasteryLevel.Reviewing));
            Assert.AreEqual("★★★☆", Dex.Stars(MasteryLevel.Proficient));
            Assert.AreEqual("★★★★", Dex.Stars(MasteryLevel.Mastered));
        }

        [Test]
        public void NextReviewDescription()
        {
            var vocab = new VocabularyProgress();
            vocab.RecordAnswer("abandon", true, T0, rules); // 학습: 5분 뒤 복습
            var p = vocab.Find("abandon");

            StringAssert.Contains("분 후", Dex.DescribeNextReview(p, T0.AddMinutes(1)));
            StringAssert.Contains("복습할 때", Dex.DescribeNextReview(p, T0.AddMinutes(6)));

            vocab.RecordAnswer("abandon", true, T0.AddMinutes(5), rules); // 복습: 하루 뒤
            StringAssert.Contains("시간 후", Dex.DescribeNextReview(vocab.Find("abandon"), T0.AddMinutes(10)));

            vocab.RecordAnswer("budget", false, T0, rules);
            StringAssert.Contains("오답 노트", Dex.DescribeNextReview(vocab.Find("budget"), T0));
            Assert.AreEqual("", Dex.DescribeNextReview(null, T0));
        }
    }

    // 실제 프로젝트의 지역(단어장) 설정 점검
    public class RegionDataTests
    {
        [Test]
        public void EveryWordBookHasUniqueRegionIdAndAReward()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var seen = new HashSet<string>();
            var books = AssetDatabase.FindAssets("t:WordDatabase", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WordDatabase>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.Greater(books.Count, 0);

            foreach (var book in books)
            {
                Assert.IsFalse(string.IsNullOrEmpty(book.RegionId), $"{book.name}: regionId 비어 있음 (도감 보상을 못 받음)");
                Assert.IsFalse(string.IsNullOrEmpty(book.RegionName), $"{book.name}: regionName 비어 있음");
                Assert.IsTrue(seen.Add(book.RegionId), $"regionId '{book.RegionId}' 중복");
                Assert.IsNotNull(book.CompletionKeepsake, $"{book.name}: 완성 징표가 없음");
                Assert.AreEqual(ItemKind.Keepsake, book.CompletionKeepsake.Kind, $"{book.name}: 징표 아이템의 종류가 Keepsake가 아님");
                Assert.Greater(book.CompletionGold, 0, $"{book.name}: 완성 골드가 0");
                Assert.AreSame(book.CompletionKeepsake, database.FindItem(book.CompletionKeepsake.ItemId),
                    $"{book.name}: 징표가 GameDatabase에 없음 (WordRPG > Data > Refresh Game Database)");
            }
        }

        // #62 교육과정 기본 어휘로 바꾼 뒤: 지역마다 자기 단어장, 단어는 한 단어장에만 (도감이 겹치지 않게), 뜻은 비어 있지 않음
        [Test]
        public void EveryAreaHasItsOwnWordBookAndBooksDoNotShareWords()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var books = new HashSet<WordDatabase>();
            foreach (var area in database.Areas)
            {
                Assert.IsNotNull(area.Words, $"{area.AreaId}: 단어장 없음");
                Assert.IsTrue(books.Add(area.Words), $"{area.AreaId}: 다른 지역과 같은 단어장 ({area.Words.name})");
            }
            var owner = new Dictionary<string, string>();
            foreach (var book in books)
            {
                Assert.GreaterOrEqual(book.Words.Count, 100, $"{book.name}: 단어가 너무 적음");
                foreach (var word in book.Words)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(word.Meaning), $"{book.name}: {word.English} 뜻이 비어 있음");
                    Assert.IsTrue(owner.TryAdd(word.Id, book.name), $"'{word.Id}'가 {owner.GetValueOrDefault(word.Id)}와 {book.name}에 모두 있음");
                }
            }
        }
    }
}
