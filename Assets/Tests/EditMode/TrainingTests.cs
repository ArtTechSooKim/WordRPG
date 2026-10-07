using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Battle;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 수련 (#44): 도감 속 단어만 내는 문제 · 허수아비(HP가 줄지 않고 공격하지 않음)
    public class TrainingTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);
        private readonly MasteryRules rules = new MasteryRules();
        private List<WordEntry> book;
        private VocabularyProgress progress;

        [SetUp]
        public void SetUp()
        {
            book = TestData.SampleWords();
            progress = new VocabularyProgress();
        }

        private void Discover(params int[] indexes)
        {
            foreach (int i in indexes) progress.RecordAnswer(book[i].Id, true, Now, rules);
        }

        private TrainingQuizProvider Provider(TrainingMode mode, int seed = 1) =>
            new TrainingQuizProvider(new[] { (IReadOnlyList<WordEntry>)book }, progress, rules, mode, new Random(seed), () => Now);

        [Test]
        public void AllModeAsksOnlyDiscoveredWordsAndCoversEachBeforeRepeating()
        {
            Discover(0, 3, 7);
            var discovered = new[] { book[0], book[3], book[7] };
            var quiz = Provider(TrainingMode.All);

            for (int round = 0; round < 3; round++)
            {
                var lap = new List<WordEntry>();
                for (int i = 0; i < discovered.Length; i++)
                {
                    var question = quiz.NextQuestion(QuizDirection.EnglishToMeaning);
                    Assert.IsFalse(question.IsNewWord, "수련에는 새 단어가 나오지 않음");
                    Assert.AreEqual(4, question.Choices.Count, "보기는 단어장에서 채움");
                    lap.Add(question.Word);
                }
                CollectionAssert.AreEquivalent(discovered, lap, "한 바퀴에 발견한 단어가 한 번씩");
            }
        }

        [Test]
        public void WrongNoteModeAsksWrongWordsFirstThenTheWeakest()
        {
            Discover(0, 1, 2);
            progress.RecordAnswer(book[4].Id, false, Now, rules); // 틀려서 오답 노트에
            var quiz = Provider(TrainingMode.WrongNote);

            var first = quiz.NextQuestion(QuizDirection.EnglishToMeaning);
            Assert.AreSame(book[4], first.Word, "오답 노트 단어부터");
            quiz.SubmitAnswer(first, true);
            Assert.IsFalse(progress.Find(book[4].Id).InWrongNote, "맞히면 오답 노트에서 빠짐");

            var known = new[] { book[0], book[1], book[2], book[4] };
            for (int i = 0; i < 10; i++)
            {
                var question = quiz.NextQuestion(QuizDirection.MeaningToEnglish);
                CollectionAssert.Contains(known, question.Word, "오답 노트가 비면 이미 본 단어 중 약한 것");
                Assert.IsFalse(question.IsNewWord);
            }
        }

        [Test]
        public void CountsDiscoveredAndWrongNoteOncePerWord()
        {
            Discover(0, 1);
            progress.RecordAnswer(book[5].Id, false, Now, rules);
            var books = new[] { (IReadOnlyList<WordEntry>)book, book.Take(3).ToList() }; // 겹치는 단어장
            Assert.AreEqual((3, 1), TrainingQuizProvider.Count(books, progress));
        }

        [Test]
        public void CannotTrainWithoutDiscoveredWords()
        {
            Assert.Throws<InvalidOperationException>(() => Provider(TrainingMode.All));
        }

        [Test]
        public void ScarecrowTakesFullDamageButNeverLosesHp()
        {
            var scarecrow = TestData.Species("scarecrow", new MonsterStats(30, 10, 10), new MonsterStats(4, 2, 2)).Set("trainingDummy", true);
            var dummy = new MonsterInstance(scarecrow, 3);
            int hp = dummy.CurrentHp;

            Assert.AreEqual(999, dummy.TakeDamage(999), "받은 피해는 그대로 (숫자 연출용)");
            Assert.AreEqual(hp, dummy.CurrentHp);
            Assert.IsFalse(dummy.IsFainted);
        }

        [Test]
        public void TrainingBattleNeverEndsAndTheScarecrowNeverAttacks()
        {
            Discover(0, 1, 2);
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 20);
            var heroSpecies = TestData.Species("hero", new MonsterStats(30, 10, 10), new MonsterStats(0, 0, 0), strike);
            var scarecrow = TestData.Species("scarecrow", new MonsterStats(30, 10, 10), new MonsterStats(0, 0, 0)).Set("trainingDummy", true);
            var config = new BattleConfig(10f, 3f, 1.5f, 0f, 2, comboBonusPerStep: 0f);
            var hero = new MonsterInstance(heroSpecies, 1);
            var battle = new BattleEngine(new List<ICombatant> { hero }, new List<MonsterInstance> { new MonsterInstance(scarecrow, 1) },
                Provider(TrainingMode.All), config, new Random(1));

            for (int turn = 0; turn < 6; turn++)
            {
                var question = battle.SelectSkill(strike, battle.Enemies[0]);
                var events = battle.SubmitAnswer(question.CorrectIndex, 5f);
                var hit = events.Single(e => e.Type == BattleEventType.Damage);
                Assert.AreEqual(10, hit.Amount, "피해는 실제 전투와 같음 (20 × 10 / (10 + 10))");
                Assert.IsFalse(events.Any(e => e.Type == BattleEventType.Defeated));
                Assert.IsFalse(events.Any(e => e.Type == BattleEventType.Damage && e.Target.IsPlayerSide), "허수아비는 공격하지 않음");
            }
            Assert.IsFalse(battle.IsOver);
            Assert.AreEqual(BattlePhase.ChoosingSkill, battle.Phase);
            Assert.AreEqual(30, battle.Enemies[0].Hp);
            Assert.AreEqual(hero.Stats.MaxHp, hero.CurrentHp);
            Assert.AreEqual(60, battle.Summary.TotalDamage);
            Assert.AreEqual(0, battle.Summary.NewWords.Count);
            CollectionAssert.AreEquivalent(new[] { book[0], book[1], book[2] }, battle.Summary.AnsweredWords, "복습한 단어는 한 번씩");
        }

        [Test]
        public void GameHasOneScarecrowThatCannotAttack()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var dummies = database.Monsters.Where(m => m.IsTrainingDummy).ToList();
            Assert.AreEqual(1, dummies.Count, "수련용 허수아비는 하나");
            var scarecrow = dummies[0];
            Assert.AreEqual("허수아비", scarecrow.DisplayName);
            Assert.AreEqual(0, scarecrow.Skills.Count, "기술이 없어야 공격하지 않음");
            Assert.AreEqual(0, scarecrow.ExpReward);
            Assert.AreEqual(0, scarecrow.GoldReward);
            Assert.IsNotNull(scarecrow.Sprite, "허수아비 그림");
            foreach (var area in database.Areas)
            {
                if (area.Encounters == null) continue;
                foreach (var entry in area.Encounters.Entries)
                    Assert.IsFalse(entry.Species.IsTrainingDummy, $"{area.AreaId} 풀숲에 허수아비가 나오면 안 됨");
            }
        }
    }
}
