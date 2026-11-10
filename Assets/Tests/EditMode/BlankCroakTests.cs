using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Battle;
using WordRPG.Monsters;
using WordRPG.Words;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 빈칸두꺼비 (#237 사용자 ㉝ → #245: 빈칸 울음 뒤만이 아니라 싸우는 동안 늘 문제 단어에 빈칸).
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
                .Set("shattersShields", shatters).Set("shieldBonusDamage", shieldBonus);
            var hero = TestData.Species("hero", new MonsterStats(500, 10, 10), new MonsterStats(0, 0, 0), strike, guard);
            var frog = TestData.Species("frog", new MonsterStats(999, 30, 10), new MonsterStats(0, 0, 0), croak).Set("displayName", "빈칸두꺼비");
            return new BattleEngine(new List<MonsterInstance> { new MonsterInstance(hero, 1) }, new List<MonsterInstance> { new MonsterInstance(frog, 1) },
                new FakeQuizProvider(), new BattleConfig(10f, 3f, 1.5f, 0f, 2), new Random(1));
        }

        // 보호막을 깨는 기술 (지금은 쓰는 기술 없음 — 사막용으로 규칙만 남김)
        [Test]
        public void AShatteringSkillBreaksTheShield()
        {
            var battle = Start(out var guard);
            var hero = battle.Party[0];

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
        }

        // #245 사용자: 다른 보스 기믹처럼 싸우는 동안 늘 — 첫 차례부터, 두꺼비를 쓰러뜨리기 전까지
        [Test]
        public void WordsStayBlankedForTheWholeFrogFight()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var hero = TestData.Species("hero", new MonsterStats(500, 10, 10), new MonsterStats(0, 0, 0), strike);
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var frog = TestData.Species("frog", new MonsterStats(999, 1, 10), new MonsterStats(0, 0, 0), bite).Set("gimmick", BossGimmick.Blank);
            var slime = TestData.Species("slime", new MonsterStats(999, 1, 10), new MonsterStats(0, 0, 0), bite);
            BattleEngine Fight(MonsterSpecies enemy) => new BattleEngine(new List<MonsterInstance> { new MonsterInstance(hero, 1) },
                new List<MonsterInstance> { new MonsterInstance(enemy, 1) }, new FakeQuizProvider(), new BattleConfig(10f, 3f, 1.5f, 0f, 2), new Random(1));

            var battle = Fight(frog);
            Assert.IsTrue(battle.WordsBlanked, "첫 차례부터");
            battle.SelectSkill(battle.Party[0].Skills[0], battle.Enemies[0]);
            battle.SubmitAnswer(Wrong, 1f);
            Assert.IsTrue(battle.WordsBlanked, "다음 차례도");
            Assert.IsFalse(Fight(slime).WordsBlanked, "다른 몬스터는 빈칸 없음");
        }

        [Test]
        public void FilledWordColorsOnlyTheBlanks()
        {
            Assert.AreEqual("s<color=#FFCC00>e</color>cur<color=#FFCC00>i</color>ty", WordBlanks.Filled("security", "s_cur_ty", "FFCC00"));
            Assert.AreEqual("well-being", WordBlanks.Filled("well-being", "well-being", "FFCC00"), "가린 글자가 없으면 그대로");
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
            Assert.IsFalse(frog.Skills.Where(s => s != croak).Any(s => s.ShattersShields || s.ShieldBonusDamage > 0), "다른 기술은 그대로 — 혀 날름은 그냥 강한 공격 (#245 사용자)");
        }
    }
}
