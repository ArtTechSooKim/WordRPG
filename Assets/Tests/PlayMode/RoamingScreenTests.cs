using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 보이는 몬스터 조우 (#73, 사막부터): 필드에 몬스터가 보이고, 다가가면 쫓아오고, [선제공격]·부딪힘으로 전투
    public class RoamingScreenTests
    {
        // 30×9. 시작 P (2,4). 바로 오른쪽 덤불 띠(3~5) — 보이는 몬스터 지역에서는 밟아도 조우하지 않음.
        // 멀리 (20,4)에 덤불 한 칸 = 몬스터가 태어나는 곳 (시작에서 12칸 넘게 떨어진 서식지는 거기뿐)
        private static string Map()
        {
            var rows = new List<string>();
            for (int r = 0; r < 9; r++)
            {
                var row = new char[30];
                for (int x = 0; x < 30; x++) row[x] = r == 0 || r == 8 || x == 0 || x == 29 ? '#' : ':';
                rows.Add(new string(row));
            }
            var middle = rows[4].ToCharArray(); // y = 4
            middle[2] = 'P';
            middle[3] = middle[4] = middle[5] = ',';
            middle[20] = ',';
            rows[4] = new string(middle);
            return string.Join("\n", rows);
        }

        private static (FieldScreen field, WordDatabase words) Create(GameSession session, bool tutorials = false)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("lizard", new MonsterStats(60, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "도마뱀");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 3, 3, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "sand").Set("regionName", "모래");
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "sand").Set("displayName", "모래벌판").Set("map", Map())
                .Set("encounters", table).Set("words", words)
                .Set("encounterRate", 1f).Set("minStepsBetweenEncounters", 0)
                .Set("encounterStyle", EncounterStyle.Roaming);
            var field = new GameObject("RoamingField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: tutorials);
            return (field, words);
        }

        private static HeroData Hero()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            return TestData.Hero(new MonsterStats(200, 40, 10)).Set("basicSkill", strike);
        }

        [UnityTest]
        public IEnumerator PreemptiveStrikeStartsTheBattleWithTheMonsterHurt()
        {
            var session = GameSession.NewGame(Hero(), 5);
            var (field, words) = Create(session);
            for (int i = 0; i < 5; i++) session.Vocabulary.Discover(words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            yield return null;
            yield return null;
            Assert.AreEqual(1, field.Roaming.Roamers.Count, "멀리 덤불에 몬스터 한 마리");
            Assert.AreEqual(1, field.RoamerViewCount, "필드에 보임");
            var roamer = field.Roaming.Roamers[0];
            Assert.IsFalse(field.IsPreemptAvailable, "멀면 버튼 없음");

            // 덤불 띠를 지나 몬스터 쪽으로 — 덤불을 밟아도 조우하지 않고, 가까워지면 [선제공격]
            yield return HoldStick(field, Direction.Right, () => field.IsPreemptAvailable || field.IsInBattle, 6f);
            Assert.IsFalse(field.IsInBattle, "보이는 몬스터 지역에서는 풀숲 조우가 없음");
            Assert.IsTrue(field.IsPreemptAvailable, "쫓아오는 몬스터가 가까우면 [선제공격]");
            Assert.IsTrue(field.IsPreemptLockVisible, "그 몬스터에 빨간 조준 괄호");
            Assert.IsTrue(roamer.State == RoamerState.Chasing || roamer.State == RoamerState.Noticing, "나를 발견함");
            yield return null;
            Assert.Less(field.Roaming.SlowFactor, 0.5f, "기회가 오면 잠깐 몬스터가 느려짐 (QTE 슬로모션 — 누를 틈)");

            FindButton(field.transform, "PreemptButton").onClick.Invoke();
            yield return WaitFor(() => field.Preempt.IsOpen, 2f);
            Assert.IsTrue(field.Preempt.IsOpen, "누르면 QTE");
            Assert.IsFalse(field.IsPreemptLockVisible, "QTE 중에는 필드 괄호 대신 QTE의 금색 괄호");
            var question = field.Preempt.Question;
            Assert.AreNotEqual(MasteryLevel.New, session.Vocabulary.GetLevel(question.Word.Id), "이미 본 단어만");
            Assert.LessOrEqual(field.Roaming.SlowFactor, 0.2f, "문제를 푸는 동안 몬스터가 느려짐");

            ActiveButton(field.transform, $"PreemptChoice_{question.CorrectIndex}").onClick.Invoke();
            Assert.AreEqual("선제공격 성공!", field.Preempt.StampText, "맞히면 성공 도장");
            Assert.IsFalse(field.Battle.IsRunning, "도장을 잠깐 보여 준 뒤 전투");
            yield return WaitFor(() => field.Battle.IsRunning, 3f);
            Assert.IsTrue(field.Battle.IsRunning, "맞히면 전투 시작");
            Assert.AreEqual(1, field.PreemptStrikes);
            var leader = field.Battle.Engine.Enemies[0].Monster;
            Assert.AreEqual(leader.Stats.MaxHp - Mathf.RoundToInt(leader.Stats.MaxHp * Preemptive.DamageRatio), leader.CurrentHp,
                "맨 앞 몬스터가 HP 20%를 잃은 채 시작");

            // 이기면 그 몬스터는 사라지고 나중에 다시 나타날 차례
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            Assert.AreEqual("승리!", field.Battle.ResultTitle);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);
            Assert.AreEqual(0, field.Roaming.Roamers.Count);
            Assert.AreEqual(0, field.RoamerViewCount);
            Assert.AreEqual(1, field.Roaming.PendingRespawns);
            Object.Destroy(field.gameObject);
        }

        // #79: 처음 [선제공격] 기회 → 화면이 멈추고 세 단계 설명 (사용자가 정한 말) → 마지막은 버튼을 직접 눌러 봐야 끝
        [UnityTest]
        public IEnumerator FirstPreemptChancePausesWithATutorial()
        {
            var session = GameSession.NewGame(Hero(), 5);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass }) session.Tutorials.Mark(id);
            var (field, words) = Create(session, tutorials: true);
            for (int i = 0; i < 5; i++) session.Vocabulary.Discover(words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            yield return null;
            yield return null;

            yield return HoldStick(field, Direction.Right, () => field.PauseTutorial.IsShowing || field.IsInBattle, 6f);
            Assert.IsTrue(field.PauseTutorial.IsShowing, "처음 선제공격 기회 → 화면을 멈추고 설명");
            Assert.AreEqual("정글에 오신걸 환영합니다.\n이제 적이 당신을 직접 싸우러 옵니다.", field.PauseTutorial.Message, "첫 문장 뒤 줄바꿈 (#83)");
            var roamer = field.Roaming.Roamers[0];
            var held = roamer.Position;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(held, roamer.Position, "멈춘 동안 몬스터도 멈춤");
            Assert.IsTrue(field.IsPreemptAvailable, "[선제공격] 버튼은 밝게 그대로");

            ActiveButton(field.transform, "TutorialNextButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual("일정 거리 적과 가까워지면 선제공격을 할 수 있습니다.", field.PauseTutorial.Message);
            Assert.IsTrue(field.IsPreemptRangeVisible, "몬스터 둘레에 선제공격 범위");
            ActiveButton(field.transform, "TutorialNextButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual("버튼을 눌러보세요!", field.PauseTutorial.Message);
            Assert.IsFalse(field.IsPreemptRangeVisible);
            Assert.IsNull(ActiveButton(field.transform, "TutorialNextButton"), "마지막 단계는 [다음] 없이 직접 눌러 보기");

            FindButton(field.transform, "PreemptButton").onClick.Invoke();
            yield return WaitFor(() => !field.PauseTutorial.IsShowing, 2f);
            Assert.IsFalse(field.PauseTutorial.IsShowing);
            Assert.IsTrue(field.Preempt.IsOpen, "누르면 바로 QTE");
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Preempt), "본 것으로 저장 — 다시 안 나옴");
            Object.Destroy(field.gameObject);
        }

        [UnityTest]
        public IEnumerator MissingThePreemptQuizStartsAPlainBattle()
        {
            var session = GameSession.NewGame(Hero(), 5);
            var (field, words) = Create(session);
            for (int i = 0; i < 5; i++) session.Vocabulary.Discover(words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            yield return null;
            yield return null;

            yield return HoldStick(field, Direction.Right, () => field.IsPreemptAvailable || field.IsInBattle, 6f);
            Assert.IsTrue(field.IsPreemptAvailable);
            FindButton(field.transform, "PreemptButton").onClick.Invoke();
            yield return WaitFor(() => field.Preempt.IsOpen, 2f);

            // 틀리면 '놓쳤다!' 도장 → 보너스 없이 그냥 전투 (사용자 결정)
            int wrong = (field.Preempt.Question.CorrectIndex + 1) % field.Preempt.Question.Choices.Count;
            ActiveButton(field.transform, $"PreemptChoice_{wrong}").onClick.Invoke();
            Assert.AreEqual("놓쳤다!", field.Preempt.StampText);
            yield return WaitFor(() => field.Battle.IsRunning, 3f);
            Assert.IsTrue(field.Battle.IsRunning, "틀려도 전투는 시작");
            Assert.AreEqual(0, field.PreemptStrikes);
            var leader = field.Battle.Engine.Enemies[0].Monster;
            Assert.AreEqual(leader.Stats.MaxHp, leader.CurrentHp, "HP 그대로");
            Object.Destroy(field.gameObject);
        }

        [UnityTest]
        public IEnumerator MonsterCatchesYouWithoutPreemptWhenYouHaveSeenNoWords()
        {
            var session = GameSession.NewGame(Hero(), 5);
            var (field, _) = Create(session);
            yield return null;
            yield return null;
            var roamer = field.Roaming.Roamers[0];

            // 본 단어가 없으면 [선제공격] 없음 → 가만히 있으면 쫓아와서 부딪힘 → 전투
            yield return HoldStick(field, Direction.Right, () => roamer.State != RoamerState.Wandering || field.IsInBattle, 6f);
            Assert.IsFalse(field.IsPreemptAvailable, "본 단어가 없으면 선제공격 문제를 낼 수 없음");
            yield return WaitFor(() => field.Battle.IsRunning, 4f);
            Assert.IsTrue(field.Battle.IsRunning, "부딪히면 전투");
            Assert.AreEqual(0, field.PreemptStrikes);

            // 도망치면 그 몬스터는 잠깐 멈춤
            yield return WaitFor(() => ActiveButton(field.Battle.transform, "FleeButton") != null, 4f);
            ActiveButton(field.Battle.transform, "FleeButton").onClick.Invoke();
            yield return WaitFor(() => field.Battle.IsResultVisible, 4f);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);
            Assert.AreEqual(RoamerState.Stunned, roamer.State);
            Object.Destroy(field.gameObject);
        }
    }
}
