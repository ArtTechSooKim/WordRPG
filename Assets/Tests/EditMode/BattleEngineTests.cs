using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WordRPG.Battle;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    public class BattleEngineTests
    {
        private const int Correct = 0; // FakeQuizProvider는 항상 0번이 정답
        private const int Wrong = 1;
        private const float Normal = 5f; // 크리티컬(3초)보다 느리고 제한시간(10초)보다 빠름
        private const float Fast = 1f;

        private BattleConfig config;
        private FakeQuizProvider quiz;
        private SkillData strike;     // 단일 공격 위력 20
        private SkillData blast;      // 전체 공격 위력 20 (한→영)
        private SkillData heal;       // 단일 회복 위력 10
        private SkillData guard;      // 아군 전체 보호막 위력 10
        private SkillData enemyBite;  // 적 단일 공격 위력 10
        private MonsterSpecies hero;  // 30 / 10 / 10
        private MonsterSpecies slime; // 20 / 10 / 10

        [SetUp]
        public void SetUp()
        {
            config = new BattleConfig(answerTimeLimitSeconds: 10f, criticalTimeSeconds: 3f, criticalMultiplier: 1.5f,
                variance: 0f, expPerCorrectAnswer: 2);
            quiz = new FakeQuizProvider();
            strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 20);
            blast = TestData.Skill("blast", SkillKind.Damage, SkillTarget.AllEnemies, 20, QuizDirection.MeaningToEnglish);
            heal = TestData.Skill("heal", SkillKind.Heal, SkillTarget.SingleAlly, 10);
            guard = TestData.Skill("guard", SkillKind.Guard, SkillTarget.AllAllies, 10);
            enemyBite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 10);
            hero = TestData.Species("hero", new MonsterStats(30, 10, 10), new MonsterStats(0, 0, 0), strike, blast, heal, guard);
            slime = TestData.Species("slime", new MonsterStats(20, 10, 10), new MonsterStats(0, 0, 0), enemyBite)
                .Set("expReward", 5).Set("goldReward", 3);
        }

        private BattleEngine Start(IList<MonsterInstance> party, IList<MonsterInstance> enemies)
        {
            return new BattleEngine(party.ToList(), enemies.ToList(), quiz, config, new Random(1));
        }

        private MonsterInstance Hero(int? hp = null) => new MonsterInstance(hero, 1, currentHp: hp);
        private MonsterInstance Slime(int? hp = null) => new MonsterInstance(slime, 1, currentHp: hp);

        [Test]
        public void CorrectAnswerFiresSkill()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            var events = battle.SubmitAnswer(Correct, Normal);

            // 20 x 10 / (10 + 10) = 10
            Assert.AreEqual(10, battle.Enemies[0].Hp);
            var hit = events.Single(e => e.Type == BattleEventType.Damage);
            Assert.AreEqual(10, hit.Amount);
            Assert.IsFalse(hit.IsCritical);
            Assert.AreEqual(1, battle.CorrectAnswers);
        }

        [Test]
        public void FastAnswerIsCritical()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            var events = battle.SubmitAnswer(Correct, Fast);

            Assert.AreEqual(5, battle.Enemies[0].Hp);
            Assert.IsTrue(events.Single(e => e.Type == BattleEventType.Damage).IsCritical);
        }

        [Test]
        public void WrongAnswerMakesSkillFail()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            var events = battle.SubmitAnswer(Wrong, Fast);

            Assert.AreEqual(20, battle.Enemies[0].Hp);
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.SkillFailed));
            Assert.IsFalse(events.Any(e => e.Type == BattleEventType.Damage));
            Assert.AreEqual(1, battle.WrongAnswers);
            CollectionAssert.AreEqual(new[] { false }, quiz.Submitted, "오답이 단어 시스템에 기록돼야 함");
        }

        [Test]
        public void TimeoutCountsAsWrong()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(Correct, 11f);
            Assert.AreEqual(20, battle.Enemies[0].Hp);

            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(-1, 10f);
            Assert.AreEqual(2, battle.WrongAnswers);
        }

        [Test]
        public void QuizDirectionComesFromSkill()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { Slime(hp: 20) });

            battle.SelectSkill(blast);
            Assert.AreEqual(QuizDirection.MeaningToEnglish, quiz.AskedDirections.Last());
        }

        [Test]
        public void AllEnemiesSkillHitsEveryEnemy()
        {
            var battle = Start(new[] { Hero() }, new[] { Slime(), Slime(), Slime() });

            battle.SelectSkill(blast);
            var events = battle.SubmitAnswer(Correct, Normal);

            // 아군이 1마리라 곧바로 적 차례도 이어지므로 아군 공격만 센다
            Assert.AreEqual(3, events.Count(e => e.Type == BattleEventType.Damage && e.Actor.IsPlayerSide));
            Assert.IsTrue(battle.Enemies.All(e => e.Hp == 10));
        }

        [Test]
        public void EnemiesActAfterWholePartyThenNewRoundStarts()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(Wrong, Normal);
            Assert.AreSame(battle.Party[1], battle.CurrentActor);
            Assert.AreEqual(1, battle.Round);

            battle.SelectSkill(strike, battle.Enemies[0]);
            var events = battle.SubmitAnswer(Wrong, Normal);

            // 적 공격: 10 x 10 / 20 = 5
            var enemyHit = events.Single(e => e.Type == BattleEventType.Damage);
            Assert.IsFalse(enemyHit.Actor.IsPlayerSide);
            Assert.AreEqual(5, enemyHit.Amount);
            Assert.AreEqual(55, battle.Party.Sum(p => p.Hp));
            Assert.AreEqual(2, battle.Round);
            Assert.AreSame(battle.Party[0], battle.CurrentActor);
            Assert.AreEqual(BattlePhase.ChoosingSkill, battle.Phase);
        }

        [Test]
        public void GuardAbsorbsEnemyDamageAndExpiresNextRound()
        {
            var battle = Start(new[] { Hero() }, new[] { Slime() });

            battle.SelectSkill(guard);
            var events = battle.SubmitAnswer(Correct, Normal);

            // 보호막 = 10 + 방어 10 / 2 = 15, 적 공격 5는 전부 흡수
            Assert.AreEqual(15, events.Single(e => e.Type == BattleEventType.Shield).Amount);
            var enemyHit = events.Single(e => e.Type == BattleEventType.Damage);
            Assert.AreEqual(0, enemyHit.Amount);
            Assert.AreEqual(5, enemyHit.Absorbed);
            Assert.AreEqual(30, battle.Party[0].Hp);
            Assert.AreEqual(0, battle.Party[0].Shield, "새 라운드에 보호막 소멸");
        }

        [Test]
        public void HealDoesNotExceedMaxHp()
        {
            var battle = Start(new[] { Hero(hp: 25), Hero() }, new[] { Slime() });

            battle.SelectSkill(heal, battle.Party[0]);
            var events = battle.SubmitAnswer(Correct, Normal);

            Assert.AreEqual(5, events.Single(e => e.Type == BattleEventType.Heal).Amount);
            Assert.AreEqual(30, battle.Party[0].Hp);
        }

        [Test]
        public void FaintedPartyMemberIsSkipped()
        {
            var battle = Start(new[] { Hero(), Hero(hp: 0), Hero() }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(Wrong, Normal);

            Assert.AreSame(battle.Party[2], battle.CurrentActor);
        }

        [Test]
        public void VictoryWhenAllEnemiesFall()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { Slime(hp: 10) });

            battle.SelectSkill(strike, battle.Enemies[0]);
            var events = battle.SubmitAnswer(Correct, Normal);

            Assert.AreEqual(BattlePhase.Victory, battle.Phase);
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.Defeated));
            Assert.AreEqual(BattleEventType.Victory, events.Last().Type);
        }

        [Test]
        public void DefeatWhenPartyFalls()
        {
            var battle = Start(new[] { Hero(hp: 1) }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            var events = battle.SubmitAnswer(Wrong, Normal);

            Assert.AreEqual(BattlePhase.Defeat, battle.Phase);
            Assert.AreEqual(BattleEventType.Defeat, events.Last().Type);
            Assert.Throws<InvalidOperationException>(() => battle.SelectSkill(strike, battle.Enemies[0]));
        }

        [Test]
        public void InvalidChoicesThrow()
        {
            var battle = Start(new[] { Hero(), Hero(hp: 0) }, new[] { Slime() });
            var foreignSkill = TestData.Skill("foreign", SkillKind.Damage, SkillTarget.SingleEnemy, 10);

            Assert.Throws<ArgumentException>(() => battle.SelectSkill(strike, null));
            Assert.Throws<ArgumentException>(() => battle.SelectSkill(strike, battle.Party[0]));
            Assert.Throws<ArgumentException>(() => battle.SelectSkill(heal, battle.Party[1]), "기절한 아군은 회복 대상 불가");
            Assert.Throws<ArgumentException>(() => battle.SelectSkill(foreignSkill, battle.Enemies[0]));
            Assert.Throws<InvalidOperationException>(() => battle.SubmitAnswer(Correct, Normal));
        }

        [Test]
        public void BattleDamageStaysOnMonsterAfterBattle()
        {
            var partyMonster = Hero();
            var battle = Start(new[] { partyMonster }, new[] { Slime() });

            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(Wrong, Normal);

            Assert.AreEqual(25, partyMonster.CurrentHp);
        }

        [Test]
        public void RewardIncludesBonusForCorrectAnswers()
        {
            var battle = Start(new[] { Hero(), Hero() }, new[] { new MonsterInstance(slime, 3, currentHp: 10) });

            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(Correct, Normal);
            var reward = battle.CalculateReward();

            // 5 x Lv3 + 정답 1개 x 2
            Assert.AreEqual(17, reward.Exp);
            Assert.AreEqual(9, reward.Gold);
        }
    }

    public class BattleRewardTests
    {
        [Test]
        public void DropsFollowChanceAndHeroGetsExp()
        {
            var always = TestData.Item("always");
            var never = TestData.Item("never");
            var enemy = TestData.Species("slime", new MonsterStats(10, 1, 1), new MonsterStats(0, 0, 0))
                .Set("expReward", 10).Set("goldReward", 4)
                .Set("drops", new List<ItemDrop> { new ItemDrop(always, 1f, 2), new ItemDrop(never, 0f) });
            var hero = new Hero(TestData.Hero(new MonsterStats(30, 10, 10)), 1);
            var inventory = new Inventory();

            var reward = BattleRewardCalculator.Calculate(
                new List<MonsterInstance> { new MonsterInstance(enemy, 1), new MonsterInstance(enemy, 1) },
                correctAnswers: 0, new BattleConfig(), new Random(1));
            int levels = BattleRewardCalculator.Apply(reward, hero, inventory);

            Assert.AreEqual(20, reward.Exp);
            Assert.AreEqual(8, inventory.Gold);
            Assert.AreEqual(4, inventory.GetCount(always));
            Assert.AreEqual(0, inventory.GetCount(never));
            Assert.AreEqual(1, levels, "Lv1→2에 경험치 20");
            Assert.AreEqual(2, hero.Level);
        }
    }

    // 주인공 혼자 싸우기: 기술 = 기본 기술 + 끼운 성유물, 상처약은 문제 없이 한 턴
    public class HeroBattleTests
    {
        private BattleConfig config;
        private FakeQuizProvider quiz;
        private SkillData blast;
        private RelicData quill;
        private ItemData potion;
        private MonsterSpecies slime;

        [SetUp]
        public void SetUp()
        {
            config = new BattleConfig(10f, 3f, 1.5f, 0f, 2);
            quiz = new FakeQuizProvider();
            blast = TestData.Skill("blast", SkillKind.Damage, SkillTarget.AllEnemies, 20, QuizDirection.MeaningToEnglish);
            quill = TestData.Relic("quill", blast, new MonsterStats(0, 10, 0));
            potion = TestData.Potion("potion", 40);
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 10);
            slime = TestData.Species("slime", new MonsterStats(100, 10, 10), new MonsterStats(0, 0, 0), bite);
        }

        private (BattleEngine battle, Hero hero) Start(params RelicData[] relics)
        {
            var hero = new Hero(TestData.Hero(new MonsterStats(100, 10, 10)), 1);
            foreach (var relic in relics) hero.AddRelic(relic);
            var battle = new BattleEngine(new ICombatant[] { hero }, new List<MonsterInstance> { new MonsterInstance(slime, 1) },
                quiz, config, new Random(1));
            return (battle, hero);
        }

        [Test]
        public void HeroUsesBasicAndRelicSkillsWithRelicBonus()
        {
            var (battle, hero) = Start(quill);
            Assert.AreSame(hero, battle.Party[0].Hero);
            Assert.AreEqual(2, battle.Party[0].Skills.Count, "기본 기술 + 깃펜 기술");
            Assert.AreEqual(20, battle.Party[0].Attack, "공격 10 + 성유물 10");

            battle.SelectSkill(blast);
            battle.SubmitAnswer(0, 5f);
            // 20 x 20 / (20 + 10) = 13.3 → 13
            Assert.AreEqual(87, battle.Enemies[0].Hp);
        }

        [Test]
        public void SkillOfUnequippedRelicCannotBeUsed()
        {
            var (battle, hero) = Start(quill);
            hero.Unequip(hero.Relics[0]);
            Assert.Throws<ArgumentException>(() => battle.SelectSkill(blast));
        }

        [Test]
        public void PotionHealsWithoutQuizAndEndsTurn()
        {
            var (battle, hero) = Start();
            hero.TakeDamage(60);
            var inventory = new Inventory();
            inventory.Add(potion, 2);

            var events = battle.UseItem(potion, inventory);

            Assert.AreEqual(0, quiz.AskedDirections.Count, "문제 없이");
            Assert.AreEqual(1, inventory.GetCount(potion));
            var used = events.First(e => e.Type == BattleEventType.ItemUsed);
            Assert.AreEqual(40, used.Amount);
            Assert.AreSame(potion, used.Item);
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.Damage && e.Target == battle.Party[0]), "적 차례가 이어진다");
            Assert.AreEqual(2, battle.Round);
            Assert.AreEqual(BattlePhase.ChoosingSkill, battle.Phase);
        }

        // 도망가기 (#53): 바로 끝나고 보상 없음. 그 전에 쓴 상처약·받은 피해·푼 문제는 그대로
        [Test]
        public void FleeEndsBattleWithoutRewardButKeepsWhatHappened()
        {
            var (battle, hero) = Start();
            hero.TakeDamage(60);
            var inventory = new Inventory();
            inventory.Add(potion, 2);
            battle.UseItem(potion, inventory);
            battle.SelectSkill(battle.Party[0].Skills[0], battle.Enemies[0]);
            battle.SubmitAnswer(0, 5f);
            int hp = hero.CurrentHp;
            Assert.Less(hp, 100, "적에게 맞음");
            Assert.AreEqual(BattlePhase.ChoosingSkill, battle.Phase);

            var events = battle.Flee();

            Assert.AreEqual(BattlePhase.Fled, battle.Phase);
            Assert.IsTrue(battle.IsOver);
            Assert.AreEqual(BattleEventType.Fled, events.Single().Type);
            Assert.AreSame(battle.Party[0], events[0].Actor);
            Assert.Throws<InvalidOperationException>(() => battle.CalculateReward(), "도망치면 보상 없음");
            Assert.AreEqual(1, inventory.GetCount(potion), "쓴 상처약은 돌아오지 않음");
            Assert.AreEqual(hp, hero.CurrentHp, "줄어든 HP 그대로 (회복 없음)");
            Assert.AreEqual(1, battle.CorrectAnswers, "푼 문제 기록은 남음");
            Assert.Throws<InvalidOperationException>(() => battle.Flee(), "끝난 전투");
        }

        // 보스전(canFlee = false): '도망갈 수 없다' 사건만 생기고 차례·적 행동은 그대로
        [Test]
        public void BossBattleCannotBeFled()
        {
            var hero = new Hero(TestData.Hero(new MonsterStats(100, 10, 10)), 1);
            var battle = new BattleEngine(new ICombatant[] { hero }, new List<MonsterInstance> { new MonsterInstance(slime, 1) },
                quiz, config, new Random(1), canFlee: false);
            Assert.IsFalse(battle.CanFlee);

            var events = battle.Flee();

            Assert.AreEqual(BattleEventType.FleeBlocked, events.Single().Type);
            Assert.AreEqual(BattlePhase.ChoosingSkill, battle.Phase);
            Assert.AreEqual(1, battle.Round, "차례를 쓰지 않음");
            Assert.AreEqual(100, hero.CurrentHp, "적도 움직이지 않음");
            battle.SelectSkill(battle.Party[0].Skills[0], battle.Enemies[0]);
            Assert.AreEqual(BattlePhase.AnsweringQuiz, battle.Phase, "그대로 싸운다");
        }

        [Test]
        public void FleeOnlyWhileChoosingSkill()
        {
            var (battle, _) = Start();
            Assert.IsTrue(battle.CanFlee, "보통 전투는 도망칠 수 있음");
            battle.SelectSkill(battle.Party[0].Skills[0], battle.Enemies[0]);
            Assert.Throws<InvalidOperationException>(() => battle.Flee(), "문제를 푸는 중에는 못 도망침");
        }

        [Test]
        public void PotionNeedsStockAndMustHeal()
        {
            var (battle, _) = Start();
            Assert.Throws<InvalidOperationException>(() => battle.UseItem(potion, new Inventory()), "가방에 없음");
            var material = TestData.Item("ink");
            var inventory = new Inventory();
            inventory.Add(material);
            Assert.Throws<ArgumentException>(() => battle.UseItem(material, inventory), "회복 아이템이 아님");
        }
    }
}
