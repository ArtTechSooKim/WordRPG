using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WordRPG.Battle;
using WordRPG.Monsters;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 힘 모으기 기술 (#120 사용자: "까먹대왕이 힘을 모으고 있다" → 다음 차례 "까먹대왕의 기억상실!" 큰 한 방).
    // 고른 차례엔 Charging만, ChargeTurns만큼 차례를 넘긴 뒤 발동. 그 사이 보호막을 쳐 두면 막을 수 있음 (보호막은 전투 내내 남음)
    public class ChargeSkillTests
    {
        private const int Correct = 0; // FakeQuizProvider는 항상 0번이 정답
        private const int Wrong = 1;
        private const float Normal = 5f;

        private static readonly BattleConfig NoVariance = new BattleConfig(10f, 3f, 1.5f, 0f, 2);

        private static SkillData Amnesia(int chargeTurns) =>
            TestData.Skill("amnesia", SkillKind.Damage, SkillTarget.SingleEnemy, 60).Set("displayName", "기억상실").Set("chargeTurns", chargeTurns);

        private static BattleEngine Start(SkillData bossSkill, out SkillData guard)
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            guard = TestData.Skill("guard", SkillKind.Guard, SkillTarget.Self, 40);
            var hero = TestData.Species("hero", new MonsterStats(500, 10, 10), new MonsterStats(0, 0, 0), strike, guard);
            var boss = TestData.Species("boss", new MonsterStats(999, 30, 10), new MonsterStats(0, 0, 0), bossSkill).Set("displayName", "까먹대왕");
            return new BattleEngine(new List<MonsterInstance> { new MonsterInstance(hero, 1) }, new List<MonsterInstance> { new MonsterInstance(boss, 1) },
                new FakeQuizProvider(), NoVariance, new Random(1));
        }

        // 아군 차례를 한 번 넘김 (틀려서 기술 실패) → 적 차례 사건만 돌려줌
        private static List<BattleEvent> PassTurn(BattleEngine battle)
        {
            battle.SelectSkill(battle.Party[0].Skills[0], battle.Enemies[0]);
            return battle.SubmitAnswer(Wrong, Normal).Where(e => e.Actor == battle.Enemies[0] || e.Target == battle.Party[0]).ToList();
        }

        [Test]
        public void ChargingSkillWaitsOneTurnThenHits()
        {
            var battle = Start(Amnesia(1), out _);
            var boss = battle.Enemies[0];

            var first = PassTurn(battle);
            Assert.AreEqual(BattleEventType.Charging, first.Single().Type, "첫 차례는 힘만 모음 (피해 없음)");
            Assert.AreEqual("기억상실", first[0].Skill.DisplayName);
            Assert.IsTrue(battle.IsCharging(boss));
            Assert.AreEqual(500, battle.Party[0].Hp);

            var second = PassTurn(battle);
            CollectionAssert.AreEqual(new[] { BattleEventType.SkillUsed, BattleEventType.Damage }, second.Select(e => e.Type).ToArray(),
                "다음 차례에 발동: 기술 → 피해");
            Assert.AreEqual(60 * 30 / (30 + 10), second[1].Amount); // 위력 × 공격 / (공격 + 방어)
            Assert.IsFalse(battle.IsCharging(boss));

            Assert.AreEqual(BattleEventType.Charging, PassTurn(battle).Single().Type, "또 고르면 다시 모으기부터");
        }

        [Test]
        public void LongerChargeWaitsThatManyTurns()
        {
            var battle = Start(Amnesia(2), out _);
            Assert.AreEqual(BattleEventType.Charging, PassTurn(battle).Single().Type);
            Assert.AreEqual(BattleEventType.Charging, PassTurn(battle).Single().Type);
            Assert.IsTrue(PassTurn(battle).Any(e => e.Type == BattleEventType.Damage), "두 차례 모은 뒤 발동");
        }

        // 보스가 힘을 모으는 사이 보호막을 쳐 두면, 다음 라운드의 큰 한 방을 그 보호막이 막는다
        [Test]
        public void ShieldRaisedWhileTheBossChargesBlocksTheBigHit()
        {
            var battle = Start(Amnesia(1), out var guard);
            var hero = battle.Party[0];

            battle.SelectSkill(guard);
            battle.SubmitAnswer(Correct, Normal);              // 보호막 40 + 방어 10 / 2 = 45, 보스는 힘을 모음
            Assert.AreEqual(45, hero.Shield, "라운드가 바뀌어도 보호막이 남음");

            // 기억상실 = 60 × 30 / (30 + 10) = 45 → 남은 보호막 45가 전부 막음
            var hit = PassTurn(battle).Single(e => e.Type == BattleEventType.Damage);
            Assert.AreEqual(45, hit.Absorbed, "남아 있던 보호막이 큰 한 방을 막음");
            Assert.AreEqual(0, hit.Amount);
            Assert.AreEqual(500, hero.Hp);
            Assert.AreEqual(0, hero.Shield, "막은 만큼 보호막이 깎임");
        }
    }
}
