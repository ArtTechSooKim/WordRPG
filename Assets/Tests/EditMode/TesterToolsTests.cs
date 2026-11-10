using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.EditorTools;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 테스터 메뉴 규칙 (#229): 실제 데이터로 — 진짜 게임 규칙(경험치·보스 보상·발견)을 거쳐 세이브가 어긋나지 않는지
    public class TesterToolsTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

        private static GameDatabase Database()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            Assert.IsNotNull(database, "Assets/Data/GameDatabase.asset 이 없습니다 (WordRPG > Data > Refresh Game Database)");
            return database;
        }

        private static GameSession NewSession(int level = 1) =>
            GameSession.NewGame(AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset"), level);

        [Test]
        public void OnlyTheTesterBuildHasTheMenu()
        {
            Assert.AreEqual("WORDRPG_TESTER", MobileBuild.TesterDefine, "TesterTools.cs·TesterMenuView.cs의 #if와 같은 이름");
            Assert.IsFalse(TesterBuild.Enabled, "에디터에서는 기본 꺼짐 (테스트에서만 켬)");
        }

        [Test]
        public void LevelUpRaisesHeroAndCompanionsAndHeals()
        {
            var session = NewSession();
            var ara = Database().Companions.First();
            var companion = session.AddCompanion(ara);
            session.Hero.TakeDamage(5);

            Assert.AreEqual(TesterTools.LevelStep, TesterTools.LevelUp(session));
            Assert.AreEqual(1 + TesterTools.LevelStep, session.Hero.Level);
            Assert.AreEqual(1 + TesterTools.LevelStep, companion.Level, "동료도 같이");
            Assert.AreEqual(0, session.Hero.Exp, "레벨 경계에 딱 맞춤");
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp, "HP 회복");

            var top = NewSession(LevelCurve.MaxLevel);
            Assert.AreEqual(0, TesterTools.LevelUp(top), "최고 레벨이면 그대로");
            Assert.AreEqual(LevelCurve.MaxLevel, top.Hero.Level);
        }

        // #251 사용자: 레벨 줄이기
        [Test]
        public void LevelDownLowersHeroAndCompanionsButNotBelowOne()
        {
            var session = NewSession(12);
            var companion = session.AddCompanion(Database().Companions.First());
            TesterTools.LevelUp(session);
            int hero = session.Hero.Level, friend = companion.Level;
            session.Hero.GainExp(1);
            session.Hero.TakeDamage(1);

            Assert.AreEqual(TesterTools.LevelStep, TesterTools.LevelDown(session));
            Assert.AreEqual(hero - TesterTools.LevelStep, session.Hero.Level);
            Assert.AreEqual(Math.Max(1, friend - TesterTools.LevelStep), companion.Level, "동료도 같이");
            Assert.AreEqual(0, session.Hero.Exp, "그 레벨의 처음");
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp, "낮아진 최대 HP에 맞음");

            var low = NewSession(3);
            Assert.AreEqual(2, TesterTools.LevelDown(low), "1에서 멈춤");
            Assert.AreEqual(1, low.Hero.Level);
            Assert.AreEqual(0, TesterTools.LevelDown(low));
        }

        // #251 사용자: 살금살금을 상시로
        [Test]
        public void AlwaysQuietLastsUntilTurnedOff()
        {
            var session = NewSession();
            Assert.IsFalse(TesterTools.IsAlwaysQuiet(session));
            Assert.IsTrue(TesterTools.ToggleAlwaysQuiet(session));
            for (int i = 0; i < 2000; i++) Assert.IsFalse(session.Effects.TakeStep(), "걸어도 끝나지 않음");
            Assert.IsTrue(session.Effects.Quiet);
            Assert.IsTrue(TesterTools.IsAlwaysQuiet(session));
            Assert.IsFalse(TesterTools.ToggleAlwaysQuiet(session));
            Assert.IsFalse(session.Effects.Quiet);
        }

        private class CountingStore : IStore
        {
            public int Buys, Restores;
            public bool IsReady => false;
            public string FullVersionPrice => null;
            public void BuyFullVersion(Action<StoreResult> done) { Buys++; done?.Invoke(StoreResult.Failed); }
            public void Restore(Action<StoreResult> done) { Restores++; done?.Invoke(StoreResult.NothingToRestore); }
        }

        // #251 사용자: 이미 산 계정으로 무료 상태를 볼 수 있게 — 앱을 다시 켜도 유지, 사면 결제 없이 돌아오고 특전을 다시 받음
        [Test]
        public void FreeStateHidesTheFullVersionUntilBoughtAgain()
        {
            var session = NewSession();
            var gift = Database().FullVersionGift;
            var owned = new Entitlements();
            owned.Grant(Entitlements.FullVersion);
            Assert.Greater(FullVersionGift.Claim(session, owned.HasFullVersion, gift).Count, 0);

            TesterTools.GoFree(session, owned);
            Assert.IsFalse(owned.HasFullVersion);
            Assert.IsTrue(owned.TesterFree);
            Assert.IsFalse(FullVersionGift.Claimed(session), "다시 사면 특전을 받음");
            Assert.IsFalse(owned.Grant(Entitlements.FullVersion), "앱을 켤 때 스토어 기록이 들어와도 무료 상태 그대로");
            Assert.IsFalse(owned.HasFullVersion);

            var reopened = Entitlements.FromStorage(owned.ToStorage());
            Assert.IsTrue(reopened.TesterFree, "앱을 다시 켜도 유지");
            Assert.IsFalse(reopened.HasFullVersion);

            var real = new CountingStore();
            var store = new TesterFreeStore(real, owned);
            Assert.IsTrue(store.IsReady, "진짜 스토어 연결과 상관없이 살 수 있음");
            StoreResult? result = null;
            store.BuyFullVersion(r => result = r);
            Assert.AreEqual(StoreResult.Purchased, result);
            Assert.AreEqual(0, real.Buys, "무료 상태에서는 실제 결제 없음");
            Assert.IsTrue(owned.HasFullVersion);
            Assert.IsFalse(owned.TesterFree);
            Assert.Greater(FullVersionGift.Claim(session, owned.HasFullVersion, gift).Count, 0, "특전을 다시 받음");

            store.BuyFullVersion(null);
            store.Restore(null);
            Assert.AreEqual(1, real.Buys, "무료 상태가 끝나면 진짜 스토어로");
            Assert.AreEqual(1, real.Restores);
        }

        [Test]
        public void GoldAndMaterialsOnlyUpgradeMaterials()
        {
            var session = NewSession();
            int gold = session.Inventory.Gold;
            TesterTools.GiveGold(session);
            Assert.AreEqual(gold + TesterTools.GoldStep, session.Inventory.Gold);

            var items = Database().Items;
            int kinds = TesterTools.GiveMaterials(session, items);
            Assert.Greater(kinds, 0);
            foreach (var item in items)
            {
                int expected = item.Kind == ItemKind.Material ? TesterTools.MaterialStep : 0;
                Assert.AreEqual(expected, session.Inventory.GetCount(item), $"{item.ItemId}: 강화 재료만");
            }
            Assert.AreEqual(items.Count(i => i.Kind == ItemKind.Material), kinds);
        }

        [Test]
        public void SkipToBeatsEveryEarlierBossOnceWithRewardsAndShrineCompanion()
        {
            var areas = Database().Areas;
            var story = TesterTools.StoryAreas(areas);
            Assert.AreEqual(areas.Count, story.Count, "모든 지역이 건너뛰기 목록에");
            var target = story.Last();
            var earlier = story.Where(a => a.StoryOrder < target.StoryOrder).ToList();
            var session = NewSession();

            int bosses = TesterTools.SkipTo(session, areas, target);
            Assert.AreEqual(earlier.Count(a => a.Boss != null), bosses);
            foreach (var area in earlier)
            {
                if (area.Boss != null)
                {
                    Assert.IsTrue(session.World.IsBossDefeated(area.BossId), $"{area.AreaId} 보스");
                    if (area.Boss.RewardRelic != null) Assert.IsTrue(session.Hero.Owns(area.Boss.RewardRelic), $"{area.AreaId} 보스 성유물");
                    if (area.Boss.RewardItem != null) Assert.AreEqual(1, session.Inventory.GetCount(area.Boss.RewardItem), $"{area.AreaId} 보스 아이템");
                }
                if (area.ShrineCompanion != null) Assert.IsTrue(session.HasCompanion(area.ShrineCompanion), $"{area.AreaId} 유적 동료");
            }
            if (target.Boss != null) Assert.IsFalse(session.World.IsBossDefeated(target.BossId), "고른 지역의 보스는 남겨 둠");
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp, "성유물로 늘어난 최대 HP까지 회복");
            Assert.IsTrue(earlier.Any(a => a.ShrineCompanion != null), "마지막 지역 앞에는 유적 동료가 있음 (정글)");
            foreach (var area in story)
                foreach (var exit in area.Exits.Where(e => e.OpenedByBossOf != null && e.OpenedByBossOf.StoryOrder < target.StoryOrder))
                    Assert.IsTrue(session.World.IsExitOpen(exit), $"{area.AreaId}: 보스가 여는 길이 열림");

            // 다시 눌러도 보상을 두 번 주지 않음
            int relics = session.Hero.Relics.Count, companions = session.Companions.Count;
            Assert.AreEqual(0, TesterTools.SkipTo(session, areas, target));
            Assert.AreEqual(relics, session.Hero.Relics.Count);
            Assert.AreEqual(companions, session.Companions.Count);
            foreach (var area in earlier.Where(a => a.Boss?.RewardItem != null))
                Assert.AreEqual(1, session.Inventory.GetCount(area.Boss.RewardItem));

            // 첫 지역은 건너뛸 것이 없음
            var fresh = NewSession();
            Assert.AreEqual(0, TesterTools.SkipTo(fresh, areas, story.First()));
            Assert.AreEqual(0, fresh.Companions.Count);
        }

        [Test]
        public void DiscoverWordsInBookOrder()
        {
            var areas = Database().Areas;
            var books = TesterTools.Books(areas);
            Assert.AreEqual(books.Count, books.Distinct().Count());
            Assert.GreaterOrEqual(books.Count, 2, "단어장 2개를 지역들이 같이 씀 (#147)");
            Assert.AreSame(TesterTools.StoryAreas(areas).First().Words, books[0], "이야기 순서대로");

            var session = NewSession();
            var book = books[0];
            Assert.AreEqual(TesterTools.WordStep, TesterTools.DiscoverWords(session, book, TesterTools.WordStep, Now));
            Assert.AreEqual(TesterTools.WordStep, Dex.GetProgress(book, session.Vocabulary).Discovered);
            for (int i = 0; i < TesterTools.WordStep; i++)
                Assert.IsTrue(session.Vocabulary.GetLevel(book.Words[i].Id) > Words.MasteryLevel.New, "단어장 순서대로");
            Assert.AreEqual(0, Dex.GetProgress(books[1], session.Vocabulary).Discovered, "다른 단어장은 그대로");

            int rest = book.Words.Count - TesterTools.WordStep;
            Assert.AreEqual(rest, TesterTools.DiscoverWords(session, book, book.Words.Count, Now), "모두");
            Assert.IsTrue(Dex.GetProgress(book, session.Vocabulary).IsComplete);
            Assert.AreEqual(0, TesterTools.DiscoverWords(session, book, book.Words.Count, Now), "이미 다 찾음");
        }
    }
}
