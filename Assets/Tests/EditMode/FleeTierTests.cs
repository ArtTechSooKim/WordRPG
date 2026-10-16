using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Battle;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.Words;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 몬스터 등급별 도망 (#84, 2026-10-16 사용자 결정): 약함 = 항상 / 보통 = 1/4 확률로 단어 문제 / 강함 = 늘 단어 문제, 틀리면 차례를 놓침
    public class FleeTierTests
    {
        private static MonsterSpecies Species(string id, FleeTier tier) =>
            TestData.Species(id, new MonsterStats(500, 5, 10), new MonsterStats(0, 0, 0),
                TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 5)).Set("fleeTier", tier);

        private static (BattleEngine battle, Hero hero, FakeQuizProvider quiz) Start(float normalChance, params MonsterSpecies[] enemies)
        {
            var hero = new Hero(TestData.Hero(new MonsterStats(100, 10, 10)).Set("basicSkill",
                TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 10)), 5);
            var quiz = new FakeQuizProvider(); // 정답 = 0번
            var config = new BattleConfig(10f, 3f, 1.5f, 0f, 2, normalFleeQuizChance: normalChance);
            var battle = new BattleEngine(new ICombatant[] { hero }, enemies.Select(s => new MonsterInstance(s, 3)).ToList(), quiz, config, new Random(1));
            return (battle, hero, quiz);
        }

        [Test]
        public void WeakMonstersAlwaysLetYouGo()
        {
            var (battle, _, quiz) = Start(1f, Species("slime", FleeTier.Weak));
            Assert.AreEqual(FleeTier.Weak, battle.EscapeTier);
            var events = battle.Flee();
            Assert.AreEqual(BattlePhase.Fled, battle.Phase, "약한 몬스터는 바로 도망");
            Assert.AreEqual(BattleEventType.Fled, events.Single().Type);
            Assert.IsEmpty(quiz.AskedDirections, "문제 없음");
        }

        [Test]
        public void NormalMonstersAskAWordOnlySometimes()
        {
            var (never, _, _) = Start(0f, Species("snake", FleeTier.Normal));
            never.Flee();
            Assert.AreEqual(BattlePhase.Fled, never.Phase, "3/4 쪽이 걸리면 바로 도망");

            var (always, _, quiz) = Start(1f, Species("snake", FleeTier.Normal));
            var events = always.Flee();
            Assert.AreEqual(BattlePhase.AnsweringQuiz, always.Phase, "1/4 쪽이 걸리면 단어 문제");
            Assert.IsTrue(always.IsFleeQuiz);
            Assert.AreEqual(BattleEventType.FleeQuiz, events.Single().Type);
            Assert.AreEqual(QuizDirection.EnglishToMeaning, quiz.AskedDirections.Single());

            // 기본 확률 1/4: 많이 해 보면 대략 그 정도
            int quizzes = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                var hero = new Hero(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
                var battle = new BattleEngine(new ICombatant[] { hero }, new List<MonsterInstance> { new MonsterInstance(Species("snake", FleeTier.Normal), 3) },
                    new FakeQuizProvider(), new BattleConfig(), new Random(seed));
                battle.Flee();
                if (battle.IsFleeQuiz) quizzes++;
            }
            Assert.That(quizzes, Is.InRange(60, 140), $"400번 중 단어 문제 {quizzes}번 (기대 약 100)");
        }

        [Test]
        public void StrongMonsterNeedsTheWordAndAMissCostsTheTurn()
        {
            var (battle, hero, quiz) = Start(0f, Species("slime", FleeTier.Weak), Species("goblin", FleeTier.Strong));
            Assert.AreEqual(FleeTier.Strong, battle.EscapeTier, "여러 마리면 가장 강한 몬스터 기준");
            var events = battle.Flee();
            Assert.AreEqual(BattleEventType.FleeQuiz, events.Single().Type);
            Assert.AreSame(battle.Enemies[1], events[0].Target, "길을 막은 몬스터");

            // 틀림 → 차례를 놓침 (적이 공격하고 다음 라운드)
            var miss = battle.SubmitAnswer(2, 3f);
            Assert.IsTrue(miss.Any(e => e.Type == BattleEventType.FleeFailed), "도망 실패");
            Assert.IsTrue(miss.Any(e => e.Type == BattleEventType.Damage && e.Target == battle.Party[0]), "적 차례가 이어짐");
            Assert.IsFalse(battle.IsFleeQuiz);
            Assert.AreEqual(BattlePhase.ChoosingSkill, battle.Phase);
            Assert.AreEqual(2, battle.Round);
            Assert.Less(hero.CurrentHp, 100);
            Assert.AreEqual(1, battle.WrongAnswers, "틀린 단어는 오답으로 기록");

            // 다시 → 맞히면 도망
            battle.Flee();
            var hit = battle.SubmitAnswer(0, 3f);
            Assert.AreEqual(BattlePhase.Fled, battle.Phase);
            Assert.IsTrue(hit.Any(e => e.Type == BattleEventType.Fled));
            Assert.AreEqual(1, battle.CorrectAnswers);
            CollectionAssert.AreEqual(new[] { false, true }, quiz.Submitted, "숙련도에도 기록");
        }

        [Test]
        public void DefeatedStrongMonsterNoLongerBlocks()
        {
            var (battle, _, _) = Start(1f, Species("goblin", FleeTier.Strong), Species("slime", FleeTier.Weak));
            battle.Enemies[0].Monster.TakeDamage(9999);
            Assert.AreEqual(FleeTier.Weak, battle.EscapeTier, "쓰러진 몬스터는 세지 않음");
        }
    }

    // 실제 몬스터의 등급 (사용자 결정 반영 — SampleDataBuilder.SetFleeTier)
    public class FleeTierDataTests
    {
        [TestCase("ink_slime", FleeTier.Weak)]
        [TestCase("scribble_bat", FleeTier.Weak)]
        [TestCase("question_owl", FleeTier.Normal)]
        [TestCase("tail_lizard", FleeTier.Normal)]
        [TestCase("mimic_octopus", FleeTier.Normal)]
        [TestCase("forget_goblin", FleeTier.Strong)]
        [TestCase("batchim_kappa", FleeTier.Strong)]
        public void MonsterHasItsFleeTier(string speciesId, FleeTier expected)
        {
            var species = AssetDatabase.LoadAssetAtPath<MonsterSpecies>($"Assets/Data/Monsters/{speciesId}.asset");
            Assert.IsNotNull(species, speciesId);
            Assert.AreEqual(expected, species.FleeTier);
        }
    }
}
