using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Battle;
using WordRPG.Monsters;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 말줄임덩굴 기믹 (#246 사용자): 매 차례 덩굴이 없으면 양옆에 덩굴 둘 — Lv5 · HP 50 · 약함, 광역에 한 번에 쓰러짐
    public class VineGimmickTests
    {
        private const int Correct = 0; // FakeQuizProvider는 항상 0번이 정답
        private const int Wrong = 1;

        private static BattleEngine Start(int heroAttack = 10, int bossHp = 999)
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 60);
            var sweep = TestData.Skill("sweep", SkillKind.Damage, SkillTarget.AllEnemies, 60);
            var hero = TestData.Species("hero", new MonsterStats(5000, heroAttack, 10), new MonsterStats(0, 0, 0), strike, sweep);
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var sprout = TestData.Species("sprout", new MonsterStats(50, 1, 0), new MonsterStats(0, 0, 0), bite).Set("displayName", "덩굴");
            var vine = TestData.Species("vine", new MonsterStats(bossHp, 1, 0), new MonsterStats(0, 0, 0), bite)
                .Set("displayName", "말줄임덩굴").Set("gimmick", BossGimmick.Overgrow).Set("minion", sprout).Set("minionLevel", 5);
            return new BattleEngine(new List<MonsterInstance> { new MonsterInstance(hero, 1) }, new List<MonsterInstance> { new MonsterInstance(vine, 1) },
                new FakeQuizProvider(), new BattleConfig(10f, 3f, 1.5f, 0f, 2), new Random(1), canFlee: false);
        }

        private static List<BattleUnit> Alive(BattleEngine battle) => battle.Enemies.Where(e => e.IsSummoned && !e.IsDefeated).ToList();

        private static IReadOnlyList<BattleEvent> Use(BattleEngine battle, int skill, int answer = Correct)
        {
            var hero = battle.Party[0];
            var target = hero.Skills[skill].Target == SkillTarget.SingleEnemy ? battle.Enemies[0] : null;
            battle.SelectSkill(hero.Skills[skill], target);
            return battle.SubmitAnswer(answer, 1f);
        }

        [Test]
        public void TwoVinesGrowOnTheBossTurnAndActFromTheNextTurn()
        {
            var battle = Start();
            Assert.AreEqual(1, battle.Enemies.Count, "처음엔 보스 혼자");
            var events = Use(battle, 0, Wrong);
            var grown = events.Single(e => e.Type == BattleEventType.MinionsGrown);
            Assert.AreEqual(2, grown.Summoned.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, grown.Summoned.Select(u => u.Index), "보스 양옆 자리");
            Assert.AreEqual(5, grown.Summoned[0].Level);
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.SkillUsed && e.Actor == battle.Enemies[0]), "보스는 그 차례에도 공격");
            Assert.IsFalse(events.Any(e => e.Type == BattleEventType.SkillUsed && e.Actor.IsSummoned), "자란 차례엔 덩굴이 공격하지 않음");

            events = Use(battle, 0, Wrong);
            Assert.IsFalse(events.Any(e => e.Type == BattleEventType.MinionsGrown), "덩굴이 남아 있으면 안 자람");
            Assert.AreEqual(2, events.Count(e => e.Type == BattleEventType.SkillUsed && e.Actor.IsSummoned), "다음 차례부터 공격");
        }

        [Test]
        public void VinesGrowBackOnlyWhenNoneAreLeft()
        {
            var battle = Start();
            Use(battle, 0, Wrong);
            var events = Use(battle, 1); // 광역 60 ≥ HP 50 → 덩굴 둘 다 한 번에
            Assert.AreEqual(2, events.Count(e => e.Type == BattleEventType.Defeated && e.Target.IsSummoned), "광역에 한 번에 쓰러짐");
            Assert.AreEqual(1, events.Count(e => e.Type == BattleEventType.MinionsGrown), "다 없어진 그 차례에 다시 자람");
            Assert.AreEqual(2, Alive(battle).Count);
        }

        [Test]
        public void WhenTheBossFallsTheVinesWitherAndGiveNoReward()
        {
            var battle = Start(bossHp: 80);
            Use(battle, 0, Wrong);
            Assert.AreEqual(2, Alive(battle).Count);
            var hero = battle.Party[0];
            battle.SelectSkill(hero.Skills[0], battle.Enemies[0]);
            battle.SubmitAnswer(Correct, 1f);
            if (battle.Phase != BattlePhase.Victory)
            {
                battle.SelectSkill(hero.Skills[0], battle.Enemies[0]);
                var last = battle.SubmitAnswer(Correct, 1f);
                Assert.AreEqual(2, last.Count(e => e.Type == BattleEventType.Defeated && e.Target.IsSummoned), "덩굴도 시듦");
            }
            Assert.AreEqual(BattlePhase.Victory, battle.Phase);
            var reward = battle.CalculateReward();
            Assert.AreEqual(5 * 1, reward.Gold, "골드는 보스 것만 (TestData 기본 5 × Lv1)");
        }

        [Test]
        public void TheRealVineMatchesWhatTheUserAskedFor()
        {
            var boss = AssetDatabase.LoadAssetAtPath<MonsterSpecies>("Assets/Data/Monsters/boss_ellipsis_vine.asset");
            Assert.AreEqual(BossGimmick.Overgrow, boss.Gimmick);
            StringAssert.Contains("광역", boss.GimmickHint);
            Assert.AreEqual(5, boss.MinionLevel);
            var stats = boss.Minion.GetStats(boss.MinionLevel);
            Assert.AreEqual(50, stats.MaxHp, "체력 50");
            Assert.Less(stats.Attack, boss.GetStats(20).Attack / 4, "공격력은 약하게");
            Assert.AreEqual(0, boss.Minion.ExpReward);
            Assert.AreEqual(0, boss.Minion.GoldReward);
        }
    }
}
