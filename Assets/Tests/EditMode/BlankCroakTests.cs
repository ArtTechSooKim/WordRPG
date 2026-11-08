using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Battle;
using WordRPG.Monsters;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 빈칸두꺼비의 빈칸 울음 (#237 사용자 ㉝): 바로 다음 아군 차례엔 문제 단어에 빈칸.
    // #239 사용자: 보호막을 깨는 건(흡수) 너무 일찍 → 3장 사막에서. 빈칸 울음은 대신 보호막이 있으면 피해 40 더 (보호막은 그대로 먼저 막음)
    public class BlankCroakTests
    {
        private const int Correct = 0; // FakeQuizProvider는 항상 0번이 정답
        private const int Wrong = 1;

        private static BattleEngine Start(out SkillData guard, bool shatters = true, int shieldBonus = 0)
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            guard = TestData.Skill("guard", SkillKind.Guard, SkillTarget.Self, 40);
            var croak = TestData.Skill("croak", SkillKind.Damage, SkillTarget.AllEnemies, 10).Set("displayName", "빈칸 울음")
                .Set("shattersShields", shatters).Set("blanksWords", true).Set("shieldBonusDamage", shieldBonus);
            var hero = TestData.Species("hero", new MonsterStats(500, 10, 10), new MonsterStats(0, 0, 0), strike, guard);
            var frog = TestData.Species("frog", new MonsterStats(999, 30, 10), new MonsterStats(0, 0, 0), croak).Set("displayName", "빈칸두꺼비");
            return new BattleEngine(new List<MonsterInstance> { new MonsterInstance(hero, 1) }, new List<MonsterInstance> { new MonsterInstance(frog, 1) },
                new FakeQuizProvider(), new BattleConfig(10f, 3f, 1.5f, 0f, 2), new Random(1));
        }

        // 보호막을 깨는 기술 (지금은 쓰는 기술 없음 — 사막용으로 규칙만 남김)
        [Test]
        public void AShatteringSkillBreaksTheShieldAndBlanksTheNextTurn()
        {
            var battle = Start(out var guard);
            var hero = battle.Party[0];
            Assert.IsFalse(battle.WordsBlanked);

            battle.SelectSkill(guard, hero);
            var events = battle.SubmitAnswer(Correct, 1f);
            var types = events.Select(e => e.Type).ToList();
            int shield = types.IndexOf(BattleEventType.Shield);
            int shattered = types.IndexOf(BattleEventType.ShieldShattered);
            int damage = types.LastIndexOf(BattleEventType.Damage);
            Assert.Greater(shattered, shield, "보호막을 친 뒤 울음에 깨짐");
            Assert.Less(shattered, damage, "깨진 다음에 맞음 (보호막이 막지 못함)");
            Assert.Greater(events[shattered].Amount, 0);
            Assert.AreEqual(0, hero.Shield);
            Assert.AreEqual(0, events[damage].Absorbed, "보호막 흡수 없음");
            Assert.IsTrue(types.Contains(BattleEventType.WordsBlanked));
            Assert.IsTrue(battle.WordsBlanked, "바로 다음 아군 차례엔 빈칸");

            battle.SelectSkill(hero.Skills[0], battle.Enemies[0]);
            battle.SubmitAnswer(Wrong, 1f);
            Assert.IsTrue(battle.WordsBlanked, "또 울면 다음 차례도 빈칸 (두꺼비 기술이 이것뿐)");
        }

        // 보호막 추가 피해 (#239): 같은 울음이 보호막 있는 주인공에겐 40 더 — 보호막이 먼저 막고 나머지가 HP로, 깨지는 사건은 따로 없음
        [Test]
        public void TheCroakHitsAShieldedHero40Harder()
        {
            int Croak(bool guarded, out BattleEvent hit)
            {
                var battle = Start(out var guard, shatters: false, shieldBonus: 40);
                var hero = battle.Party[0];
                if (guarded) battle.SelectSkill(guard, hero);
                else battle.SelectSkill(hero.Skills[0], battle.Enemies[0]);
                var events = battle.SubmitAnswer(Correct, 1f);
                Assert.IsFalse(events.Any(e => e.Type == BattleEventType.ShieldShattered), "깨지 않음");
                hit = events.Last(e => e.Type == BattleEventType.Damage && e.Target == hero);
                return hit.Amount + hit.Absorbed;
            }

            int plain = Croak(false, out var plainHit);
            int guarded = Croak(true, out var guardedHit);
            Assert.AreEqual(0, plainHit.ShieldBonus, "보호막이 없으면 그대로");
            Assert.AreEqual(40, guardedHit.ShieldBonus);
            Assert.AreEqual(plain + 40, guarded, "보호막이 있으면 40 더");
            Assert.Greater(guardedHit.Absorbed, 0, "보호막은 그대로 먼저 막음");
        }

        [Test]
        public void TheRealFrogHasTheBlankGimmick()
        {
            var frog = AssetDatabase.LoadAssetAtPath<MonsterSpecies>("Assets/Data/Monsters/boss_blank_frog.asset");
            Assert.AreEqual(BossGimmick.Blank, frog.Gimmick);
            StringAssert.Contains("빈칸", frog.GimmickHint);
            var croak = frog.Skills.Single(s => s.SkillId == "frog_blank_croak");
            Assert.IsFalse(croak.ShattersShields, "#239 보호막을 깨지 않음 (사막에서)");
            Assert.AreEqual(40, croak.ShieldBonusDamage, "#239 보호막이 있으면 피해 40 더");
            StringAssert.Contains("보호막", croak.Description);
            Assert.IsTrue(croak.BlanksWords, "빈칸 울음 뒤 단어에 빈칸");
            Assert.IsFalse(frog.Skills.Where(s => s != croak).Any(s => s.ShattersShields || s.BlanksWords || s.ShieldBonusDamage > 0), "다른 기술은 그대로");
        }
    }
}
