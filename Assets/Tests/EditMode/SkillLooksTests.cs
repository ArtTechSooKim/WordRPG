using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Monsters;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 기술 연출 표 (#137 — 기술마다 다른 효과, 회복·보호막이 더 역동적으로): 빠진 종류·그림이 없고, 기술 종류에 맞는 모습인지
    public class SkillLooksTests
    {
        private static SkillData[] AllSkills() =>
            AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Data" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();

        [Test]
        public void EveryKindOfLookIsInTheTableAndHasItsPictures()
        {
            foreach (SkillFx fx in Enum.GetValues(typeof(SkillFx)))
            {
                Assert.IsTrue(SkillLooks.Has(fx), $"SkillLooks 표에 {fx} 줄이 없음");
                if (fx == SkillFx.Auto) continue;
                var look = SkillLooks.Of(fx);
                Assert.IsNotNull(BattleFx.Frames(look.Hit), $"{fx}: 효과 그림 {look.Hit}");
                if (look.Projectile is Fx projectile) Assert.IsNotNull(BattleFx.Frames(projectile), $"{fx}: 날아가는 그림 {projectile}");
                if (look.Particle is Fx particle) Assert.IsNotNull(BattleFx.Frames(particle), $"{fx}: 알갱이 {particle}");
            }
            // 그림은 Tools/import_ninja_art.py FX 표가 만든다 (Art/NinjaAdventure/Fx/{소문자}.png)
            foreach (Fx fx in Enum.GetValues(typeof(Fx)))
                Assert.IsNotNull(BattleFx.Frames(fx), $"Fx/{fx.ToString().ToLowerInvariant()}.png");
        }

        [Test]
        public void AutoFollowsTheKindOfSkillAndTheSide()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 10);
            Assert.AreEqual(SkillFx.Slash, SkillLooks.Resolve(strike, true), "아군 공격 = 휘두르기");
            Assert.AreEqual(SkillFx.Claw, SkillLooks.Resolve(strike, false), "적 공격 = 할퀴기");
            Assert.AreEqual(SkillFx.HealLight, SkillLooks.Resolve(TestData.Skill("mend", SkillKind.Heal, SkillTarget.Self, 10), true));
            Assert.AreEqual(SkillFx.GuardBubble, SkillLooks.Resolve(TestData.Skill("guard", SkillKind.Guard, SkillTarget.Self, 10), true));
            Assert.AreEqual(SkillFx.Claw, SkillLooks.Resolve(null, false), "기술이 없어도 멈추지 않음");
        }

        // 게임의 모든 기술은 모습이 정해져 있고(Auto 아님), 회복 기술은 회복 모습 · 보호막은 보호막 · 공격은 공격 모습
        [Test]
        public void EveryGameSkillHasALookThatMatchesItsKind()
        {
            var skills = AllSkills();
            Assert.Greater(skills.Length, 50);
            foreach (var skill in skills)
            {
                Assert.AreNotEqual(SkillFx.Auto, skill.Fx, $"{skill.SkillId}: 연출이 정해지지 않음 (SampleDataBuilder.SkillFxById)");
                string name = skill.Fx.ToString();
                bool heal = name.StartsWith("Heal") || skill.Fx == SkillFx.Drain, guard = name.StartsWith("Guard");
                switch (skill.Kind)
                {
                    case SkillKind.Heal: Assert.IsTrue(heal, $"{skill.SkillId}: 회복 기술인데 {skill.Fx}"); break;
                    case SkillKind.Guard: Assert.IsTrue(guard, $"{skill.SkillId}: 보호막 기술인데 {skill.Fx}"); break;
                    default: Assert.IsFalse(heal || guard, $"{skill.SkillId}: 공격 기술인데 {skill.Fx}"); break;
                }
            }
        }

        // 주인공이 쓰는 공격(성유물·기술문서·기본)은 같은 모습만 되풀이하지 않음 — 적어도 6가지
        [Test]
        public void HeroAttacksLookDifferent()
        {
            var heroAttacks = AllSkills().Where(s => s.Kind == SkillKind.Damage && !IsMonsterSkill(s)).Select(s => s.Fx).Distinct().Count();
            Assert.GreaterOrEqual(heroAttacks, 6);
        }

        private static bool IsMonsterSkill(SkillData skill) =>
            AssetDatabase.FindAssets("t:MonsterSpecies", new[] { "Assets/Data" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<MonsterSpecies>(AssetDatabase.GUIDToAssetPath(guid)))
                .Any(m => m.Skills.Contains(skill));
    }
}
