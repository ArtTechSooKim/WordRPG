using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 설정 값 · 징표 진열장 · 소지품 '쓰는 곳' 규칙
    public class MenuTests
    {
        private static WordDatabase Region(string id, string name, ItemData keepsake, int gold = 0)
        {
            var region = ScriptableObject.CreateInstance<WordDatabase>()
                .Set("regionId", id).Set("regionName", name).Set("rewards", new List<DexReward> { new DexReward(id, name, keepsake, gold) });
            region.ReplaceWords(TestData.SampleWords());
            return region;
        }

        private static ItemData Keepsake(string id) => TestData.Item(id).Set("kind", ItemKind.Keepsake);

        // ------------------------------------------------------------------ 설정

        [Test]
        public void SettingsHaveFriendlyDefaults()
        {
            var settings = new GameSettings();
            Assert.AreEqual(GameSettings.DefaultMusicVolume, settings.MusicVolume);
            Assert.AreEqual(GameSettings.DefaultSfxVolume, settings.SfxVolume);
            Assert.IsTrue(settings.Vibration);
        }

        [Test]
        public void VolumeIsClampedToZeroToOne()
        {
            var settings = new GameSettings { MusicVolume = 1.5f, SfxVolume = -0.2f };
            Assert.AreEqual(1f, settings.MusicVolume);
            Assert.AreEqual(0f, settings.SfxVolume);
            settings.MusicVolume = float.NaN;
            Assert.AreEqual(GameSettings.DefaultMusicVolume, settings.MusicVolume, "이상한 값이면 기본값");
        }

        // 전투 강도 (#147): 처음엔 공격할 때마다 고름, 예전 설정(칸 없음)도. 저장·불러오기 그대로
        [Test]
        public void BattleIntensitySettingDefaultsToAskingEveryTime()
        {
            Assert.AreEqual(0, new GameSettings().BattleIntensity);
            Assert.AreEqual(0, GameSettings.FromJson("{\"musicVolume\":0.2}").BattleIntensity, "예전 설정");
            Assert.AreEqual(3, GameSettings.FromJson(new GameSettings { BattleIntensity = 3 }.ToJson()).BattleIntensity);
            Assert.AreEqual(0, new GameSettings { BattleIntensity = -2 }.BattleIntensity);
            Assert.AreEqual(0, GameSettings.FromJson("{\"battleIntensity\":-5}").BattleIntensity, "깨진 값");
        }

        [Test]
        public void SettingsSurviveJsonRoundTrip()
        {
            var settings = new GameSettings { MusicVolume = 0.2f, SfxVolume = 0.9f, Vibration = false };
            var loaded = GameSettings.FromJson(settings.ToJson());
            Assert.AreEqual(0.2f, loaded.MusicVolume, 0.0001f);
            Assert.AreEqual(0.9f, loaded.SfxVolume, 0.0001f);
            Assert.IsFalse(loaded.Vibration);
        }

        [Test]
        public void BrokenSettingsFallBackToDefaults()
        {
            Assert.AreEqual(GameSettings.DefaultMusicVolume, GameSettings.FromJson("").MusicVolume);
            Assert.AreEqual(GameSettings.DefaultMusicVolume, GameSettings.FromJson("{not json").MusicVolume);
            Assert.AreEqual(1f, GameSettings.FromJson("{\"musicVolume\":5}").MusicVolume, "범위 밖 값은 잘라냄");
        }

        // ------------------------------------------------------------------ 징표 진열장

        [Test]
        public void KeepsakeShelfHasOneSlotPerRegionWithKeepsake()
        {
            var meadow = Region("meadow", "초원", Keepsake("keepsake_meadow"), 500);
            var noKeepsake = Region("plain", "들판", null);
            var library = Region("library", "서고", Keepsake("keepsake_library"));
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1);

            var shelf = Keepsakes.Collect(new[] { meadow, meadow, noKeepsake, null, library }, session);

            Assert.AreEqual(2, shelf.Count, "같은 지역은 한 번만, 징표 없는 지역은 제외");
            Assert.AreEqual("초원", shelf[0].RegionName);
            Assert.AreEqual("서고", shelf[1].RegionName);
            Assert.IsFalse(shelf[0].Owned);
            Assert.AreEqual(TestData.SampleWords().Count, shelf[0].Progress.Total);
        }

        [Test]
        public void KeepsakeIsOwnedWhenClaimedOrInInventory()
        {
            var meadow = Region("meadow", "초원", Keepsake("keepsake_meadow"));
            var library = Region("library", "서고", Keepsake("keepsake_library"));
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1);

            session.Record.MarkRegionClaimed("meadow");
            session.Inventory.Add(library.Rewards[0].Keepsake);
            var shelf = Keepsakes.Collect(new[] { meadow, library }, session);

            Assert.IsTrue(shelf[0].Owned, "보상을 받은 지역");
            Assert.IsTrue(shelf[1].Owned, "소지품에 징표가 있음");
        }

        [Test]
        public void AreasSharingWordBookMakeOneShelfSlot()
        {
            var meadow = Region("meadow", "초원", Keepsake("keepsake_meadow"));
            var field = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "meadow").Set("words", meadow);
            var dungeon = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "library").Set("words", meadow);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], new[] { field, dungeon });

            var shelf = Keepsakes.Collect(database, GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1));

            Assert.AreEqual(1, shelf.Count, "던전이 초원 단어장을 같이 써도 징표 칸은 하나");
            Assert.AreEqual(0, Keepsakes.Collect((GameDatabase)null, GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1)).Count);
        }
    }
}
