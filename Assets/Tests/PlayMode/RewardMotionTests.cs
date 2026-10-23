using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 레벨 업·골드가 보이게 (#127 사용자: "레벨 오르는 게 무슨 의미인지 의문 — 공격력 등이 오를 때 반짝이며 숫자가 오르게, 돈 버는 것도"):
    // 결산의 능력치 칩(HP·공격·방어 이전 → 새 값) · 보유 골드 줄, 필드 HUD 골드가 굴러 올라가며 '+NG'
    public class RewardMotionTests
    {
        private static WordDatabase Words()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.ReplaceWords(TestData.SampleWords());
            return db;
        }

        [UnityTest]
        public IEnumerator LevelUpShowsRisingStatsAndGoldTotal()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var hero = TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike).Set("growthPerLevel", new MonsterStats(8, 3, 2));
            var enemy = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), strike)
                .Set("expReward", 30).Set("goldReward", 7);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var session = GameSession.NewGame(hero, 1);
            session.Inventory.AddGold(100);
            var before = session.Hero.Stats;
            var screen = new GameObject("RewardBattle").AddComponent<BattleScreen>();
            screen.Configure(table, Words(), session, animScale: 0.01f);
            yield return null;
            yield return null;
            yield return PlayUntilResult(screen, answerCorrectly: true);
            Assert.AreEqual("승리!", screen.ResultTitle);
            Assert.AreEqual(2, session.Hero.Level, "레벨 업");
            yield return new WaitForSecondsRealtime(0.3f); // 연출이 끝날 때까지 (animScale 0.01)

            var growth = screen.transform.Find("BattleCanvas/SafeArea/ResultPanel/Card/Growth");
            var rise = growth.Find("StatRise");
            Assert.IsTrue(rise.gameObject.activeSelf, "레벨이 오르면 능력치 칩");
            var after = session.Hero.Stats;
            var text = AllText(rise);
            StringAssert.Contains($"{before.MaxHp} → {after.MaxHp}", text);
            StringAssert.Contains($"{before.Attack} → {after.Attack}", text);
            StringAssert.Contains($"{before.Defense} → {after.Defense}", text);
            StringAssert.Contains("▲8", text);
            StringAssert.Contains("▲3", text);
            StringAssert.Contains("▲2", text);

            var result = AllText(screen.transform.Find("BattleCanvas/SafeArea/ResultPanel"));
            StringAssert.Contains("+7G", result, "골드는 끝 값까지 굴러 올라감");
            StringAssert.Contains("보유 골드", result);
            StringAssert.Contains("100 → 107G", result);
            Object.Destroy(screen.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoLevelUpMeansNoStatChips()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var hero = TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike).Set("growthPerLevel", new MonsterStats(8, 3, 2));
            var enemy = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), strike)
                .Set("expReward", 1).Set("goldReward", 0);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var session = GameSession.NewGame(hero, 1);
            var screen = new GameObject("RewardBattle").AddComponent<BattleScreen>();
            screen.Configure(table, Words(), session, animScale: 0.01f);
            yield return null;
            yield return null;
            yield return PlayUntilResult(screen, answerCorrectly: true);
            Assert.AreEqual(1, session.Hero.Level);
            var panel = screen.transform.Find("BattleCanvas/SafeArea/ResultPanel/Card");
            Assert.IsFalse(panel.Find("Growth/StatRise").gameObject.activeSelf, "레벨이 그대로면 칩 없음");
            Assert.IsFalse(panel.Find("GoldTotal").gameObject.activeSelf, "번 골드가 없으면 보유 골드 줄 없음");
            Object.Destroy(screen.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HudGoldRollsUpWithAFloatingGain()
        {
            var words = Words().Set("regionId", "test").Set("regionName", "테스트");
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "test").Set("displayName", "테스트 들판").Set("map", "#####\n#P..#\n#####").Set("words", words);
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(50, 10, 10)), 1);
            session.Inventory.AddGold(20);
            var field = new GameObject("GoldField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.3f);
            yield return null;
            yield return null;
            Assert.AreEqual(20, field.ShownGold, "처음엔 바로");

            session.Inventory.AddGold(50);
            typeof(FieldScreen).GetMethod("RefreshHud", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(field, null);
            yield return null;
            Assert.Less(field.ShownGold, 70, "한 번에 바뀌지 않고 굴러 올라감");
            StringAssert.Contains("+50G", AllText(field.transform), "'+50G'가 떠오름");
            yield return WaitFor(() => field.ShownGold == 70, 2f);
            Assert.AreEqual(70, field.ShownGold);
            StringAssert.Contains("70G", AllText(field.transform));

            Assert.IsTrue(session.Inventory.TrySpendGold(30));
            typeof(FieldScreen).GetMethod("RefreshHud", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(field, null);
            Assert.AreEqual(40, field.ShownGold, "쓴 돈은 바로");
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
