using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WordRPG.Heroes;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
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
                .Set("rewards", new List<DexReward> { new DexReward("meadow", "초원", keepsake, 500) });
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
            var noId = ScriptableObject.CreateInstance<WordDatabase>().Set("rewards", new List<DexReward> { new DexReward("", "", null, 99) });
            noId.ReplaceWords(TestData.SampleWords().Take(1));
            var empty = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "empty")
                .Set("rewards", new List<DexReward> { new DexReward("empty", "", null, 99) });
            session.Vocabulary.RecordAnswer(noId.Words[0].Id, true, T0, rules);

            Assert.AreEqual(0, session.ClaimDexRewards(new[] { noId, empty }).Count);
            Assert.AreEqual(0, session.Inventory.Gold);
        }

        // 단어장 하나에 보상 여러 개 (#147): 발견한 수에 닿을 때마다 하나씩, 마지막은 단어장을 다 채웠을 때
        [Test]
        public void RewardsComeAtEachWordCount()
        {
            var first = TestData.Item("keepsake_first");
            var last = TestData.Item("keepsake_last");
            var book = ScriptableObject.CreateInstance<WordDatabase>().Set("regionName", "초원·서고").Set("rewards", new List<DexReward>
            {
                new DexReward("first", "초원", first, 100, 1, 1), new DexReward("last", "서고", last, 200, 0, 2),
            });
            book.ReplaceWords(TestData.SampleWords().Take(3));
            var session = NewSession();
            void Find(int index) => session.Vocabulary.RecordAnswer(book.Words[index].Id, true, T0, rules);

            Assert.AreEqual(0, session.ClaimDexRewards(new[] { book }).Count, "아직 하나도 못 찾음");
            Assert.AreEqual("first", Dex.NextReward(book, session.Record).RegionId);

            Find(0);
            var earned = session.ClaimDexRewards(new[] { book });
            Assert.AreEqual(1, earned.Count);
            Assert.AreEqual("★ 단어 1개 발견!", earned[0].Headline);
            Assert.AreEqual(100, session.Inventory.Gold);
            Assert.AreEqual(1, session.Inventory.GetCount(first));
            Assert.AreEqual(0, session.ClaimDexRewards(new[] { book }).Count, "같은 보상을 또 주지 않음");
            Assert.AreEqual("last", Dex.NextReward(book, session.Record).RegionId);

            Find(1);
            Assert.AreEqual(0, session.ClaimDexRewards(new[] { book }).Count, "다 채우기 전엔 마지막 보상 없음");
            Find(2);
            earned = session.ClaimDexRewards(new[] { book });
            Assert.AreEqual("★ 초원·서고 도감 완성!", earned[0].Headline);
            Assert.AreEqual(300, session.Inventory.Gold);
            Assert.AreEqual(1, session.Inventory.GetCount(last));
            Assert.IsNull(Dex.NextReward(book, session.Record), "다 받음");
        }

        // 예전 세이브를 불러와 이미 여러 구간을 넘었으면 한꺼번에 받음
        [Test]
        public void PassingSeveralCountsAtOnceGivesThemAll()
        {
            var book = ScriptableObject.CreateInstance<WordDatabase>().Set("rewards", new List<DexReward>
            {
                new DexReward("a", "", TestData.Item("keepsake_a"), 10, 1), new DexReward("b", "", TestData.Item("keepsake_b"), 20, 2),
            });
            book.ReplaceWords(TestData.SampleWords().Take(3));
            var session = NewSession();
            foreach (var word in book.Words.Take(2)) session.Vocabulary.RecordAnswer(word.Id, true, T0, rules);

            Assert.AreEqual(2, session.ClaimDexRewards(new[] { book }).Count);
            Assert.AreEqual(30, session.Inventory.Gold);
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
            var rewardIds = new HashSet<string>();
            var books = AssetDatabase.FindAssets("t:WordDatabase", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WordDatabase>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.Greater(books.Count, 0);

            foreach (var book in books)
            {
                Assert.IsFalse(string.IsNullOrEmpty(book.RegionId), $"{book.name}: regionId 비어 있음 (도감 보상을 못 받음)");
                Assert.IsFalse(string.IsNullOrEmpty(book.RegionName), $"{book.name}: regionName 비어 있음");
                Assert.IsTrue(seen.Add(book.RegionId), $"regionId '{book.RegionId}' 중복");
                Assert.Greater(book.Rewards.Count, 0, $"{book.name}: 도감 보상이 없음");
                int before = 0;
                foreach (var reward in book.Rewards)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(reward.RegionId), $"{book.name}: 보상 id가 비어 있음 (세이브에 기록 못 함)");
                    Assert.IsTrue(rewardIds.Add(reward.RegionId), $"보상 id '{reward.RegionId}' 중복");
                    Assert.IsNotNull(reward.Keepsake, $"{reward.RegionId}: 징표가 없음");
                    Assert.AreEqual(ItemKind.Keepsake, reward.Keepsake.Kind, $"{reward.RegionId}: 징표 아이템의 종류가 Keepsake가 아님");
                    Assert.Greater(reward.Gold, 0, $"{reward.RegionId}: 골드가 0");
                    Assert.Greater(reward.StoryOrder, 0, $"{reward.RegionId}: storyOrder(이야기 순서)가 없음 — 징표 진열장 순서 (#97)");
                    Assert.AreSame(reward.Keepsake, database.FindItem(reward.Keepsake.ItemId),
                        $"{reward.RegionId}: 징표가 GameDatabase에 없음 (WordRPG > Data > Refresh Game Database)");
                    int needed = reward.WordsNeeded(book.Words.Count);
                    Assert.Greater(needed, before, $"{reward.RegionId}: 보상은 적은 수부터");
                    before = needed;
                }
                Assert.AreEqual(book.Words.Count, before, $"{book.name}: 마지막 보상은 단어장을 다 채웠을 때");
            }
            // 출시된 세이브에 적혀 있는 id — 바꾸면 받은 징표를 또 받음
            foreach (var shipped in new[] { "meadow", "library", "forest", "jungle" })
                Assert.IsTrue(rewardIds.Contains(shipped), $"출시된 보상 id '{shipped}'가 사라짐");
        }

        // #97: 진열장은 이야기 순서 — 전에는 에셋 이름 순(숲·정글·서고·초원)이라 칸 3개에 초원이 안 보였음
        [Test]
        public void KeepsakeShelfFollowsTheStory()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var books = AssetDatabase.FindAssets("t:WordDatabase", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WordDatabase>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var rewards = books.SelectMany(b => b.Rewards).ToList();
            Assert.AreEqual(rewards.Count, rewards.Select(r => r.StoryOrder).Distinct().Count(), "storyOrder가 겹침");

            var hero = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            var shelf = Keepsakes.Collect(database, GameSession.NewGame(hero, 1)).Select(e => e.Reward.RegionId).ToList();
            CollectionAssert.AreEqual(new[] { "meadow", "library", "forest", "jungle", "cave" }, shelf.Take(5).ToArray(), "초원 → 서고 → 숲 → 정글 → 동굴");
        }

        // #147 사용자: 단어를 합쳐서 초원·서고 150 + 숲·정글·동굴 300만 씀 (나머지는 다음 버전 — Docs/단어_예비_*.csv).
        // 같은 단어장을 쓰니 단어 찾는 개수는 뒤로 갈수록 많이: 서고 40 / 숲 40 · 정글 80 · 유적 100 · 동굴 120
        [Test]
        public void RegionsShareTwoWordBooksWithRisingWordGoals()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            FieldArea Area(string id) => database.FindArea(id);

            var common = Area("meadow").Words;
            var advanced = Area("forest").Words;
            Assert.AreSame(common, Area("library").Words);
            Assert.AreSame(advanced, Area("jungle").Words);
            Assert.AreSame(advanced, Area("cave").Words);
            Assert.AreEqual(150, common.Words.Count);
            Assert.AreEqual(300, advanced.Words.Count);

            Assert.AreEqual(40, Area("library").BossGateWords);
            Assert.AreEqual(40, Area("forest").BossGateWords);
            Assert.AreEqual(80, Area("jungle").BossGateWords);
            Assert.AreEqual(100, Area("jungle").ShrineWords);
            Assert.AreEqual(120, Area("cave").BossGateWords);
        }

        // 1.1 세이브: 받은 보상 기록은 그대로라 다시 받지 않고, 진열장에서도 획득으로 보임. 단어장에서 빠진 단어의 기록이 남아 있어도 숫자에 안 들어감
        [Test]
        public void OldClaimsStayAfterWordBooksMerged()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            var session = GameSession.NewGame(hero, 10);
            var now = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
            session.Record.MarkRegionClaimed("meadow");
            session.Record.MarkRegionClaimed("library");
            session.Inventory.Add(database.FindItem("keepsake_library"));
            session.Vocabulary.Discover("word_that_left_the_book", now);
            var common = database.FindArea("meadow").Words;
            Assert.AreEqual(0, Dex.GetProgress(common, session.Vocabulary).Discovered, "빠진 단어는 안 셈");

            foreach (var word in common.Words) session.Vocabulary.Discover(word.Id, now);
            Assert.AreEqual(0, session.ClaimDexRewards(new[] { common }).Count, "이미 받은 보상");
            Assert.AreEqual(0, session.Inventory.Gold);

            var saved = session.ToSaveData(now);
            var loaded = GameSession.FromSaveData(SaveData.FromJson(saved.ToJson()), database, hero);
            var shelf = Keepsakes.Collect(database, loaded);
            Assert.IsTrue(shelf[0].Owned && shelf[1].Owned, "초원·서고 징표 그대로");
            Assert.IsFalse(shelf[2].Owned);
            Assert.AreEqual(150, Dex.GetProgress(common, loaded.Vocabulary).Discovered);
        }

        // #62 교육과정 기본 어휘로 바꾼 뒤: 단어는 한 단어장에만 (도감이 겹치지 않게), 뜻은 비어 있지 않음
        [Test]
        public void EveryAreaHasAWordBookAndBooksDoNotShareWords()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var books = new HashSet<WordDatabase>();
            foreach (var area in database.Areas)
            {
                Assert.IsNotNull(area.Words, $"{area.AreaId}: 단어장 없음");
                books.Add(area.Words);
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
