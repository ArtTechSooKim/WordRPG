using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Battle;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 동료 (#77, 1.1 메아리 정글 — 사용자 결정): 파티 = 주인공 + 동료 1명, 동료도 자기 차례에 기술 + 단어 문제,
    // 경험치를 같이 받아 각자 레벨업(레벨에 따라 새 기술), 성유물은 주인공만
    public class CompanionTests
    {
        private SkillData pebble, sprout, echo;

        [SetUp]
        public void SetUp()
        {
            pebble = TestData.Skill("ara_pebble", SkillKind.Damage, SkillTarget.SingleEnemy, 12).Set("displayName", "돌팔매");
            sprout = TestData.Skill("ara_sprout", SkillKind.Heal, SkillTarget.SingleAlly, 20).Set("displayName", "새싹 치유");
            echo = TestData.Skill("ara_echo", SkillKind.Guard, SkillTarget.AllAllies, 8).Set("displayName", "메아리 방패");
        }

        private CompanionData Ara() =>
            ScriptableObject.CreateInstance<CompanionData>()
                .Set("companionId", "ara").Set("displayName", "아라")
                .Set("baseStats", new MonsterStats(50, 10, 10)).Set("growthPerLevel", new MonsterStats(6, 2, 2))
                .Set("basicSkill", pebble)
                .Set("learnedSkills", new List<CompanionSkill> { new CompanionSkill(1, sprout), new CompanionSkill(8, echo) });

        private static GameSession Session(int level = 5) =>
            GameSession.NewGame(TestData.Hero(new MonsterStats(80, 20, 10)).Set("basicSkill",
                TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 30)), level);

        [Test]
        public void SkillsGrowWithLevel()
        {
            var ara = Ara();
            CollectionAssert.AreEqual(new[] { pebble, sprout }, ara.SkillsAt(5));
            CollectionAssert.AreEqual(new[] { pebble, sprout, echo }, ara.SkillsAt(8));
            CollectionAssert.AreEqual(new[] { echo }, ara.SkillsLearnedBetween(7, 9), "레벨업 때 새로 배운 기술");
            Assert.IsEmpty(ara.SkillsLearnedBetween(8, 9));
            Assert.AreEqual(new MonsterStats(74, 18, 18), ara.GetStats(5));
        }

        [Test]
        public void CompanionLevelsUpLikeTheHero()
        {
            var companion = new Companion(Ara(), 5);
            Assert.AreEqual(companion.Stats.MaxHp, companion.CurrentHp, "처음엔 HP 가득");
            companion.TakeDamage(10);
            int levels = companion.GainExp(LevelCurve.ExpToNextLevel(5));
            Assert.AreEqual(1, levels);
            Assert.AreEqual(6, companion.Level);
            Assert.AreEqual(companion.Stats.MaxHp - 10, companion.CurrentHp, "늘어난 최대 HP만큼 현재 HP도");
            companion.TakeDamage(999);
            Assert.IsTrue(companion.IsFainted);
            Assert.AreEqual(0, companion.Heal(10), "쓰러지면 회복 안 됨");
            companion.ReviveIfFainted();
            Assert.AreEqual(1, companion.CurrentHp, "이긴 뒤 HP 1로 일어남");
        }

        [Test]
        public void PartyIsHeroPlusOneCompanion()
        {
            var session = Session(level: 6);
            Assert.AreEqual(1, session.PartyMembers.Count, "처음엔 주인공 혼자");
            var ara = Ara();
            var companion = session.AddCompanion(ara);
            Assert.AreEqual(6, companion.Level, "주인공 레벨로 합류");
            Assert.AreSame(companion, session.PartyCompanion, "자리가 비어 있으면 바로 함께 감");
            CollectionAssert.AreEqual(new ICombatant[] { session.Hero, companion }, session.PartyMembers, "차례: 주인공 → 동료");
            Assert.AreSame(companion, session.AddCompanion(ara), "같은 동료는 한 번만");
            Assert.AreEqual(1, session.Companions.Count);

            Assert.IsTrue(session.SetPartyCompanion(null), "쉬게 하기");
            Assert.AreEqual(1, session.PartyMembers.Count);
            Assert.IsFalse(session.SetPartyCompanion(new Companion(Ara(), 3)), "얻지 않은 동료는 못 데려감");
            Assert.IsTrue(session.SetPartyCompanion(companion));
        }

        [Test]
        public void RecoveryAndBattleEndApplyToCompanions()
        {
            var session = Session();
            var companion = session.AddCompanion(Ara());
            companion.TakeDamage(999);
            session.AfterBattle();
            Assert.AreEqual(1, companion.CurrentHp, "전투가 끝나면 쓰러진 동료는 HP 1");
            session.RestoreHero();
            Assert.AreEqual(companion.Stats.MaxHp, companion.CurrentHp, "샘·패배 뒤 회복은 동료도");
        }

        [Test]
        public void CompanionGetsTheSameExp()
        {
            var session = Session();
            var companion = session.AddCompanion(Ara());
            var reward = new BattleReward(LevelCurve.ExpToNextLevel(companion.Level), 0, new List<RewardItem>());
            BattleRewardCalculator.Apply(reward, session.Hero, session.Inventory);
            Assert.AreEqual(1, BattleRewardCalculator.ApplyTo(reward, companion));
            Assert.AreEqual(0, BattleRewardCalculator.ApplyTo(reward, null), "동료가 없으면 아무 일 없음");
        }

        [Test]
        public void CompanionsSurviveSaveAndLoad()
        {
            var ara = Ara();
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], null, null, new[] { ara });
            var heroData = TestData.Hero(new MonsterStats(80, 20, 10));
            var session = GameSession.NewGame(heroData, 5);
            var companion = session.AddCompanion(ara);
            companion.GainExp(7);
            companion.TakeDamage(5);
            var json = JsonUtility.ToJson(session.ToSaveData(DateTime.UtcNow));

            var loaded = GameSession.FromSaveData(JsonUtility.FromJson<Save.SaveData>(json), database, heroData);
            Assert.AreEqual(1, loaded.Companions.Count);
            var back = loaded.Companions[0];
            Assert.AreSame(ara, back.Data);
            Assert.AreEqual(companion.Level, back.Level);
            Assert.AreEqual(companion.Exp, back.Exp);
            Assert.AreEqual(companion.CurrentHp, back.CurrentHp);
            Assert.AreSame(back, loaded.PartyCompanion, "함께 다니던 동료 그대로");

            // 쉬게 한 채 저장 → 쉬는 채로
            loaded.SetPartyCompanion(null);
            var resting = GameSession.FromSaveData(JsonUtility.FromJson<Save.SaveData>(JsonUtility.ToJson(loaded.ToSaveData(DateTime.UtcNow))), database, heroData);
            Assert.IsNull(resting.PartyCompanion);
            Assert.AreEqual(1, resting.Companions.Count);

            // 동료 데이터가 없어진 세이브 → 빼고 경고
            var missing = GameSession.FromSaveData(JsonUtility.FromJson<Save.SaveData>(json), ScriptableObject.CreateInstance<GameDatabase>(), heroData);
            Assert.AreEqual(0, missing.Companions.Count);
            Assert.IsTrue(missing.LoadWarnings.Any(w => w.Contains("ara")));

            // 예전 세이브(동료 칸 없음) → 동료 없이
            var old = GameSession.FromSaveData(JsonUtility.FromJson<Save.SaveData>(json.Replace("\"companions\"", "\"_old\"")), database, heroData);
            Assert.IsNull(old.PartyCompanion);
        }

        [Test]
        public void CompanionTakesItsOwnTurnWithAWordQuestion()
        {
            var session = Session();
            var companion = session.AddCompanion(Ara());
            session.Hero.TakeDamage(30);
            var slime = TestData.Species("slime", new MonsterStats(500, 1, 10), new MonsterStats(0, 0, 0),
                TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1));
            var quiz = new FakeQuizProvider();
            var config = new BattleConfig(answerTimeLimitSeconds: 10f, criticalTimeSeconds: 3f, criticalMultiplier: 1.5f, variance: 0f, expPerCorrectAnswer: 2);
            var battle = new BattleEngine(session.PartyMembers, new List<MonsterInstance> { new MonsterInstance(slime, 1) }, quiz, config, new Random(1));

            Assert.AreSame(session.Hero, battle.CurrentActor.Combatant, "주인공 차례부터");
            battle.SelectSkill(session.Hero.Skills[0], battle.Enemies[0]);
            battle.SubmitAnswer(0, 5f);
            Assert.AreSame(companion, battle.CurrentActor.Combatant, "다음은 동료 차례");
            CollectionAssert.Contains(companion.Skills, sprout);

            int hpBefore = session.Hero.CurrentHp;
            battle.SelectSkill(sprout, battle.Party[0]);
            Assert.AreEqual(2, quiz.AskedDirections.Count, "동료 기술도 단어 문제");
            battle.SubmitAnswer(0, 5f);
            Assert.Greater(session.Hero.CurrentHp, hpBefore, "동료의 새싹 치유로 주인공 회복");
        }
    }

    // 실제 동료 데이터: id·이름·기본 기술·얼굴 그림, GameDatabase에 있어야 세이브에서 되살릴 수 있음 (Refresh Game Database)
    public class CompanionDataTests
    {
        [Test]
        public void EveryCompanionIsCompleteAndInTheDatabase()
        {
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var guids = UnityEditor.AssetDatabase.FindAssets("t:CompanionData", new[] { "Assets/Data" });
            Assert.Greater(guids.Length, 0, "동료 데이터가 없음 (아라)");
            var ids = new HashSet<string>();
            foreach (var guid in guids)
            {
                var companion = UnityEditor.AssetDatabase.LoadAssetAtPath<CompanionData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsFalse(string.IsNullOrEmpty(companion.CompanionId), $"{companion.name}: companionId 비어 있음");
                Assert.IsTrue(ids.Add(companion.CompanionId), $"동료 id '{companion.CompanionId}' 중복");
                Assert.IsFalse(string.IsNullOrEmpty(companion.DisplayName), $"{companion.name}: 이름 비어 있음");
                Assert.IsNotNull(companion.BasicSkill, $"{companion.name}: 기본 기술 없음");
                Assert.AreSame(companion, database.FindCompanion(companion.CompanionId), $"{companion.name}이 GameDatabase에 없음 (Refresh Game Database)");
                Assert.IsNotNull(UI.UiKit.CompanionPortrait(companion), $"{companion.name}: 얼굴 그림 없음 (Art/NinjaAdventure/Companions/{companion.CompanionId}.png)");
            }
        }
    }
}
