using System;
using System.Linq;
using NUnit.Framework;
using WordRPG.Battle;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 연속 정답 콤보: 단계·글자·추가 피해, 엔진의 연속 정답 수
    public class ComboTests
    {
        private const int Correct = 0; // FakeQuizProvider는 항상 0번이 정답
        private const int Wrong = 1;
        private const float Normal = 5f;

        private BattleConfig config;
        private SkillData strike;
        private SkillData heal;
        private MonsterSpecies hero;
        private MonsterSpecies dummy;

        [SetUp]
        public void SetUp()
        {
            config = new BattleConfig(10f, 3f, 1.5f, variance: 0f, expPerCorrectAnswer: 2, comboBonusPerStep: 0.05f);
            strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            heal = TestData.Skill("heal", SkillKind.Heal, SkillTarget.Self, 10);
            hero = TestData.Species("hero", new MonsterStats(100, 10, 10), new MonsterStats(0, 0, 0), strike, heal);
            // 맞아도 쓰러지지 않는 큰 HP, 공격은 약하게
            dummy = TestData.Species("dummy", new MonsterStats(9999, 1, 10), new MonsterStats(0, 0, 0),
                TestData.Skill("poke", SkillKind.Damage, SkillTarget.SingleEnemy, 1));
        }

        private BattleEngine Start(int startStreak = 0) =>
            new BattleEngine(new ICombatant[] { new MonsterInstance(hero, 1) }, new[] { new MonsterInstance(dummy, 1) },
                new FakeQuizProvider(), config, new Random(1), startStreak);

        private static int HitAmount(System.Collections.Generic.IReadOnlyList<BattleEvent> events) =>
            events.First(e => e.Type == BattleEventType.Damage && e.Actor.IsPlayerSide).Amount;

        [TestCase(0, 0, null)]
        [TestCase(1, 0, null)]
        [TestCase(2, 1, "Combo!")]
        [TestCase(3, 2, "Good!")]
        [TestCase(4, 3, "Very Good!")]
        [TestCase(5, 4, "Excellent!")]
        [TestCase(6, 5, "Outstanding!")]
        [TestCase(7, 6, "Exceptional!")]
        [TestCase(30, 6, "Exceptional!")]
        public void StepsAndLabelsFollowTheStreak(int streak, int step, string label)
        {
            Assert.AreEqual(step, Combo.Step(streak));
            Assert.AreEqual(label, Combo.Label(streak));
        }

        [Test]
        public void BonusGrowsFivePercentPerStepUpToThirty()
        {
            Assert.AreEqual(0f, Combo.DamageBonus(1, 0.05f), 1e-6);
            Assert.AreEqual(0.05f, Combo.DamageBonus(2, 0.05f), 1e-6);
            Assert.AreEqual(0.30f, Combo.DamageBonus(7, 0.05f), 1e-6);
            Assert.AreEqual(0.30f, Combo.DamageBonus(50, 0.05f), 1e-6, "최대 단계에서 멈춤");
        }

        [Test]
        public void ConsecutiveCorrectAnswersBuildComboAndHitHarder()
        {
            var battle = Start();
            var damages = new int[4];
            for (int i = 0; i < 4; i++)
            {
                battle.SelectSkill(strike, battle.Enemies[0]);
                var events = battle.SubmitAnswer(Correct, Normal);
                damages[i] = HitAmount(events);
                Assert.AreEqual(i + 1, battle.Streak);

                var combo = events.Where(e => e.Type == BattleEventType.Combo).ToList();
                if (i == 0) Assert.AreEqual(0, combo.Count, "첫 정답은 콤보 아님");
                else
                {
                    Assert.AreEqual(1, combo.Count);
                    Assert.AreEqual(i + 1, combo[0].Amount);
                    var order = events.Select(e => e.Type).ToList();
                    Assert.Less(order.IndexOf(BattleEventType.Combo), order.IndexOf(BattleEventType.SkillUsed), "콤보 글자가 기술보다 먼저");
                }
            }
            // 40 x 10 / (10 + 10) = 20 → 콤보 단계마다 +5%
            CollectionAssert.AreEqual(new[] { 20, 21, 22, 23 }, damages);
        }

        [Test]
        public void WrongAnswerOrTimeoutResetsCombo()
        {
            var battle = Start();
            for (int i = 0; i < 3; i++)
            {
                battle.SelectSkill(strike, battle.Enemies[0]);
                battle.SubmitAnswer(Correct, Normal);
            }
            Assert.AreEqual(3, battle.Streak);

            battle.SelectSkill(strike, battle.Enemies[0]);
            var missed = battle.SubmitAnswer(Wrong, Normal);
            Assert.AreEqual(0, battle.Streak, "틀리면 처음부터");
            Assert.IsFalse(missed.Any(e => e.Type == BattleEventType.Combo));

            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(Correct, Normal);
            battle.SelectSkill(strike, battle.Enemies[0]);
            battle.SubmitAnswer(Correct, 11f);
            Assert.AreEqual(0, battle.Streak, "시간 초과도 끊김");
        }

        [Test]
        public void ComboCarriesOverFromPreviousBattle()
        {
            var battle = Start(startStreak: 4);
            battle.SelectSkill(strike, battle.Enemies[0]);
            var events = battle.SubmitAnswer(Correct, Normal);
            Assert.AreEqual(5, battle.Streak);
            Assert.AreEqual(5, events.Single(e => e.Type == BattleEventType.Combo).Amount, "이어서 Excellent!");
            Assert.AreEqual(24, HitAmount(events), "20 x 1.2");
        }

        [Test]
        public void ComboBoostsDamageOnlyNotHealing()
        {
            var battle = Start(startStreak: 6);
            battle.SelectSkill(heal);
            var events = battle.SubmitAnswer(Correct, Normal);
            Assert.AreEqual(7, battle.Streak, "회복 기술로 맞혀도 콤보는 이어짐");
            var healed = events.FirstOrDefault(e => e.Type == BattleEventType.Heal);
            Assert.IsNotNull(healed);
            // 회복량 = 위력 10 + 공격 10/2 = 15 (콤보 보너스 없음, HP가 가득이면 0)
            Assert.LessOrEqual(healed.Amount, 15);
        }

        [Test]
        public void ComboExpiresThreeMinutesAfterTheLastBattleEnds()
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 3);
            var t0 = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(0, session.StartBattleCombo(t0), "첫 전투");
            session.ComboStreak = 4;
            session.EndBattleCombo(t0.AddSeconds(30));
            Assert.AreEqual(4, session.StartBattleCombo(t0.AddSeconds(30 + 179)), "끝나고 3분 안 = 이어짐");
            session.ComboStreak = 6;
            session.EndBattleCombo(t0.AddMinutes(5));
            Assert.AreEqual(0, session.StartBattleCombo(t0.AddMinutes(8).AddSeconds(1)), "끝나고 3분이 넘으면 0부터");
        }

        // #83 사용자 결정: 수련에 들어갈 때·나올 때 콤보 초기화
        [Test]
        public void TrainingStartsAndEndsWithNoCombo()
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 3);
            var t0 = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
            session.ComboStreak = 5;
            session.EndBattleCombo(t0);
            session.ResetCombo();
            Assert.AreEqual(0, session.StartBattleCombo(t0.AddSeconds(10)), "3분 안이어도 0부터");
            session.ComboStreak = 7; // 수련에서 쌓음
            session.ResetCombo();
            Assert.AreEqual(0, session.StartBattleCombo(t0.AddSeconds(20)), "수련 콤보는 실제 전투로 안 이어짐");
        }

        [Test]
        public void LongBattleDoesNotResetCombo()
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 3);
            var t0 = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
            session.ComboStreak = 3;
            session.EndBattleCombo(t0);
            Assert.AreEqual(3, session.StartBattleCombo(t0.AddMinutes(1)));
            session.ComboStreak = 9; // 전투가 10분 걸려도 그동안 계속 맞힘
            session.EndBattleCombo(t0.AddMinutes(11));
            Assert.AreEqual(9, session.StartBattleCombo(t0.AddMinutes(12)), "시간은 전투가 끝난 뒤부터 잼 — 긴 전투 중엔 안 끊김");
        }

        [Test]
        public void SessionRemembersStreakButSaveDoesNot()
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 3);
            Assert.AreEqual(0, session.ComboStreak);
            session.ComboStreak = 5;
            var data = session.ToSaveData(DateTime.UtcNow);
            StringAssert.DoesNotContain("combo", UnityEngine.JsonUtility.ToJson(data).ToLowerInvariant(),
                "콤보는 세이브에 넣지 않음 (게임을 다시 켜면 0부터)");
        }
    }
}
