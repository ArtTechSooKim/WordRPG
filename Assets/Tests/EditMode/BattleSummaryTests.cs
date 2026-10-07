using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WordRPG.Battle;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 전투 결산 (#41): 새로 만난 단어 · 틀린 단어 · 기술별 피해 · 가장 활약한 기술 · 콤보 · 크리티컬
    public class BattleSummaryTests
    {
        private const int Correct = 0; // 문제는 항상 0번이 정답
        private const int Wrong = 1;
        private const float Normal = 5f;
        private const float Fast = 1f;

        private static readonly WordEntry Apple = new WordEntry("", "apple", "사과", "n");
        private static readonly WordEntry River = new WordEntry("", "river", "강", "n");
        private static readonly WordEntry Stone = new WordEntry("", "stone", "돌", "n");

        private BattleConfig config;
        private SkillData strike, blast, enemyBite;
        private MonsterSpecies hero, slime;

        [SetUp]
        public void SetUp()
        {
            config = new BattleConfig(answerTimeLimitSeconds: 10f, criticalTimeSeconds: 3f, criticalMultiplier: 1.5f,
                variance: 0f, expPerCorrectAnswer: 2, comboBonusPerStep: 0f); // 콤보 추가 피해 없이 숫자를 정확히
            strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 20);
            blast = TestData.Skill("blast", SkillKind.Damage, SkillTarget.AllEnemies, 20);
            enemyBite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 10);
            hero = TestData.Species("hero", new MonsterStats(30, 10, 10), new MonsterStats(0, 0, 0), strike, blast);
            slime = TestData.Species("slime", new MonsterStats(20, 10, 10), new MonsterStats(0, 0, 0), enemyBite);
        }

        private BattleEngine Start(IQuizProvider quiz, int enemies) =>
            new BattleEngine(new List<MonsterInstance> { new MonsterInstance(hero, 1) },
                Enumerable.Range(0, enemies).Select(_ => new MonsterInstance(slime, 1)).ToList(), quiz, config, new Random(1));

        private static void Act(BattleEngine battle, SkillData skill, int answer, float seconds = Normal)
        {
            battle.SelectSkill(skill, battle.Enemies.First(e => !e.IsDefeated));
            battle.SubmitAnswer(answer, seconds);
        }

        [Test]
        public void NewAndWrongWordsAreListedOncePerBattle()
        {
            // 사과(새 단어) 틀림 → 강(새 단어) 맞힘 → 사과 또 틀림 → 돌(아는 단어) 맞혀서 승리
            var quiz = new QueueQuizProvider((Apple, true), (River, true), (Apple, false), (Stone, false));
            var battle = Start(quiz, 1);

            Act(battle, strike, Wrong);
            Act(battle, strike, Correct);
            Act(battle, strike, Wrong);
            Act(battle, strike, Correct);

            Assert.AreEqual(BattlePhase.Victory, battle.Phase);
            var summary = battle.Summary;
            CollectionAssert.AreEqual(new[] { Apple, River }, summary.NewWords);
            CollectionAssert.AreEqual(new[] { Apple }, summary.WrongWords);
            Assert.AreEqual(2, summary.Correct);
            Assert.AreEqual(2, summary.Wrong);
        }

        [Test]
        public void DamageIsCountedPerSkillOnlyForHitsOnEnemies()
        {
            var quiz = new QueueQuizProvider((Apple, false));
            var battle = Start(quiz, 2);

            battle.SelectSkill(blast, battle.Enemies[0]);
            battle.SubmitAnswer(Correct, Normal); // 둘 다 10씩
            Act(battle, strike, Correct);         // 하나에 10 (쓰러짐)

            var summary = battle.Summary;
            Assert.AreEqual(20, summary.DamageOf(blast));
            Assert.AreEqual(10, summary.DamageOf(strike));
            Assert.AreEqual(30, summary.TotalDamage);
            Assert.AreEqual(0, summary.DamageOf(enemyBite), "적이 주인공에게 준 피해는 세지 않음");
            Assert.AreSame(blast, summary.TopSkill);
            Assert.AreEqual(0, summary.Criticals);
        }

        [Test]
        public void FastAnswersAndStreaksAreRecorded()
        {
            var quiz = new QueueQuizProvider((Apple, false));
            var battle = Start(quiz, 2);

            Act(battle, strike, Correct, Fast);
            Act(battle, strike, Correct);

            Assert.AreEqual(1, battle.Summary.Criticals);
            Assert.GreaterOrEqual(battle.Summary.MaxCombo, Combo.FirstStreak);
        }

        [Test]
        public void TopSkillTiesGoToTheSkillUsedFirstAndNoDamageMeansNone()
        {
            var battle = Start(new QueueQuizProvider((Apple, false)), 1);
            var me = battle.Party[0];
            var foe = battle.Enemies[0];
            var summary = new BattleSummary();
            Assert.IsNull(summary.TopSkill);

            summary.Record(new[]
            {
                BattleEvent.Damage(me, foe, strike, 0, 5, false), // 보호막에 막힘
                BattleEvent.Damage(foe, me, enemyBite, 99, 0, false),
            });
            Assert.IsNull(summary.TopSkill, "적에게 들어간 피해가 0이면 없음");

            summary.Record(new[]
            {
                BattleEvent.Damage(me, foe, blast, 12, 0, false),
                BattleEvent.Damage(me, foe, strike, 12, 0, true),
                BattleEvent.Combo(me, 3),
                BattleEvent.Combo(me, 2),
            });
            Assert.AreSame(strike, summary.TopSkill, "피해가 같으면 먼저 쓴 기술");
            Assert.AreEqual(24, summary.TotalDamage);
            Assert.AreEqual(1, summary.Criticals);
            Assert.AreEqual(3, summary.MaxCombo);
        }

        // 정해진 단어를 차례로 내는 가짜 퀴즈 (다 쓰면 마지막 것을 반복). 항상 0번이 정답
        private class QueueQuizProvider : IQuizProvider
        {
            private readonly Queue<(WordEntry word, bool isNew)> queue;
            private (WordEntry word, bool isNew) last;

            public QueueQuizProvider(params (WordEntry word, bool isNew)[] items)
            {
                queue = new Queue<(WordEntry, bool)>(items);
                last = items[items.Length - 1];
            }

            public QuizQuestion NextQuestion(QuizDirection preferredDirection)
            {
                var next = queue.Count > 0 ? queue.Dequeue() : (word: last.word, isNew: false);
                return new QuizQuestion(next.word, preferredDirection, next.isNew, next.word.English,
                    new[] { next.word.Meaning, "오답1", "오답2", "오답3" }, 0);
            }

            public MasteryChange SubmitAnswer(QuizQuestion question, bool correct) =>
                new MasteryChange(question.Word.Id, MasteryLevel.Learning, MasteryLevel.Learning, correct);
        }
    }
}
