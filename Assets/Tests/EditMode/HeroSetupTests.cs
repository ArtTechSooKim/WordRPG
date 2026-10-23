using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 주인공 모습·이름 (#124 사용자: 처음에 8명 중에서 고르고 이름을 물어 '주인공' 글자를 대신 — 모습만 다르고 능력은 같음)
    public class HeroSetupTests
    {
        private static HeroData HeroWithLooks() =>
            TestData.Hero(new MonsterStats(60, 14, 10)).Set("looks", new List<HeroLook>
            {
                new HeroLook("boy", "소년"), new HeroLook("hunter", "사냥꾼"), new HeroLook("knight", "기사")
            });

        [TestCase("  아린  ", "아린")]
        [TestCase("Alex", "Alex")]
        [TestCase("용사123", "용사123")]
        [TestCase("가나다라마바사아자차", "가나다라마바사아")] // 8글자까지
        [TestCase("김  철수", "김 철수")]                 // 빈칸은 한 칸
        [TestCase("漢字😀별", "별")]                     // 글꼴에 없는 글자는 뺌
        [TestCase("ㅇㅏ린", "린")]                       // 입력하다 남은 자모는 뺌
        [TestCase("   ", "")]
        public void NamesKeepOnlyHangulLettersAndDigits(string raw, string expected)
        {
            Assert.AreEqual(expected, HeroNames.Clean(raw));
            Assert.AreEqual(expected.Length > 0, HeroNames.IsValid(raw));
        }

        [Test]
        public void NewHeroUsesDefaultNameAndLookUntilChosen()
        {
            var session = GameSession.NewGame(HeroWithLooks(), 3);
            Assert.AreEqual("주인공", session.Hero.DisplayName);
            Assert.AreEqual("boy", session.Hero.LookOrDefault, "아직 안 골랐으면 기본 모습으로 그림");
            Assert.IsTrue(session.NeedsHeroSetup, "새 게임은 고르는 화면부터");
            Assert.IsTrue(session.IsFreshStart);

            session.Hero.SetLook("hunter");
            Assert.AreEqual("아린", session.Hero.Rename(" 아린 "));
            Assert.AreEqual("아린", session.Hero.DisplayName);
            Assert.IsFalse(session.NeedsHeroSetup);

            session.Hero.Rename("😀");
            Assert.AreEqual("주인공", session.Hero.DisplayName, "다듬은 뒤 비면 원래 이름");
            session.Hero.SetLook("dragon");
            Assert.AreEqual("boy", session.Hero.LookId, "없는 모습이면 기본 모습");
        }

        [Test]
        public void LookOnlyChangesAppearanceNotStats()
        {
            var data = HeroWithLooks();
            var a = GameSession.NewGame(data, 5);
            var b = GameSession.NewGame(data, 5);
            b.Hero.SetLook("knight");
            Assert.AreEqual(a.Hero.Stats, b.Hero.Stats, "모습만 다르고 능력은 같음 (사용자)");
            CollectionAssert.AreEqual(a.Hero.Skills, b.Hero.Skills);
        }

        [Test]
        public void NameAndLookSurviveSaveAndOldSavesAskOnce()
        {
            var heroData = HeroWithLooks();
            var session = GameSession.NewGame(heroData, 4);
            session.Hero.SetLook("knight");
            session.Hero.Rename("Alex");
            var json = JsonUtility.ToJson(session.ToSaveData(DateTime.UtcNow));

            var loaded = GameSession.FromSaveData(JsonUtility.FromJson<Save.SaveData>(json), ScriptableObject.CreateInstance<GameDatabase>(), heroData);
            Assert.AreEqual("Alex", loaded.Hero.DisplayName);
            Assert.AreEqual("knight", loaded.Hero.LookId);
            Assert.IsFalse(loaded.NeedsHeroSetup);

            // 1.0 세이브(이름·모습 칸 없음) → '주인공'·기본 모습으로 불러오고, 업데이트 뒤 처음 켤 때 한 번 고름
            var old = GameSession.FromSaveData(JsonUtility.FromJson<Save.SaveData>(
                json.Replace("\"heroName\"", "\"_n\"").Replace("\"heroLook\"", "\"_l\"")), ScriptableObject.CreateInstance<GameDatabase>(), heroData);
            Assert.AreEqual("주인공", old.Hero.DisplayName);
            Assert.AreEqual("boy", old.Hero.LookOrDefault);
            Assert.IsTrue(old.NeedsHeroSetup, "업데이트 뒤 한 번 고르는 화면");
        }

        [Test]
        public void JosaFollowsTheChosenName()
        {
            Assert.AreEqual("아린이", UiKit.WithJosa("아린", "이", "가"));
            Assert.AreEqual("하나가", UiKit.WithJosa("하나", "이", "가"));
        }

        [Test]
        public void NewTutorialStepsAreReplayable()
        {
            CollectionAssert.Contains(TutorialProgress.All, TutorialProgress.GrassFight);
            CollectionAssert.Contains(TutorialProgress.All, TutorialProgress.Goal);
        }
    }
}
