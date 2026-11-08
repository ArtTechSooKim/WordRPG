using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Battle;
using WordRPG.Monsters;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 빈칸두꺼비의 빈칸 울음 (#237 사용자 ㉝): 맞기 전에 보호막이 깨지고, 바로 다음 아군 차례엔 문제 단어에 빈칸
    public class BlankCroakTests
    {
        private const int Correct = 0; // FakeQuizProvider는 항상 0번이 정답
        private const int Wrong = 1;

        private static BattleEngine Start(out SkillData guard)
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            guard = TestData.Skill("guard", SkillKind.Guard, SkillTarget.Self, 40);
            var croak = TestData.Skill("croak", SkillKind.Damage, SkillTarget.AllEnemies, 10).Set("displayName", "빈칸 울음")
                .Set("shattersShields", true).Set("blanksWords", true);
            var hero = TestData.Species("hero", new MonsterStats(500, 10, 10), new MonsterStats(0, 0, 0), strike, guard);
            var frog = TestData.Species("frog", new MonsterStats(999, 30, 10), new MonsterStats(0, 0, 0), croak).Set("displayName", "빈칸두꺼비");
            return new BattleEngine(new List<MonsterInstance> { new MonsterInstance(hero, 1) }, new List<MonsterInstance> { new MonsterInstance(frog, 1) },
                new FakeQuizProvider(), new BattleConfig(10f, 3f, 1.5f, 0f, 2), new Random(1));
        }

        [Test]
        public void TheCroakShattersTheShieldAndBlanksTheNextTurn()
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

        [Test]
        public void TheRealFrogHasTheBlankGimmick()
        {
            var frog = AssetDatabase.LoadAssetAtPath<MonsterSpecies>("Assets/Data/Monsters/boss_blank_frog.asset");
            Assert.AreEqual(BossGimmick.Blank, frog.Gimmick);
            StringAssert.Contains("빈칸", frog.GimmickHint);
            var croak = frog.Skills.Single(s => s.SkillId == "frog_blank_croak");
            Assert.IsTrue(croak.ShattersShields, "빈칸 울음은 보호막을 깸");
            Assert.IsTrue(croak.BlanksWords, "빈칸 울음 뒤 단어에 빈칸");
            Assert.IsFalse(frog.Skills.Where(s => s != croak).Any(s => s.ShattersShields || s.BlanksWords), "다른 기술은 그대로");
        }
    }
}
