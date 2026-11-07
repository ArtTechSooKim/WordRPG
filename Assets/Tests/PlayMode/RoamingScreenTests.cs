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
        // 30×9. 시작 P (2,4). 바로 오른쪽 덤불 띠(3~5) — 풀숲 조우 시험(#123)에 씀 (다른 시험은 풀숲 조우 확률 0).
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

        // 30×9. 시작 P (2,4) — 걷는 줄(y=4)엔 풀숲이 없고, 바로 아래 줄(y=3) 3~28이 풀숲 = 몬스터 서식지 (#123 쫓기는 중 시험).
        // 몬스터는 시작에서 12칸 넘게 떨어진 14~28 중 한 곳에서 태어남
        private static string ChaseMap()
        {
            var rows = Map().Split('\n');
            var middle = rows[4].ToCharArray(); // y = 4
            middle[3] = middle[4] = middle[5] = middle[20] = ':';
            rows[4] = new string(middle);
            var below = rows[5].ToCharArray(); // y = 3
            for (int x = 3; x <= 28; x++) below[x] = ',';
            rows[5] = new string(below);
            return string.Join("\n", rows);
        }

        private static (FieldScreen field, WordDatabase words) Create(GameSession session, bool tutorials = false, float grassRate = 0f, string map = null)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("lizard", new MonsterStats(60, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "도마뱀");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 3, 3, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "sand").Set("regionName", "모래");
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "sand").Set("displayName", "모래벌판").Set("map", map ?? Map())
                .Set("encounters", table).Set("words", words)
                .Set("encounterRate", grassRate).Set("minStepsBetweenEncounters", 0)
                .Set("encounterStyle", EncounterStyle.Roaming);
            var field = new GameObject("RoamingField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: tutorials);
            return (field, words);
        }

        // 시험용: 몬스터를 그 칸으로 (위치는 규칙 안에서만 바뀜)
        private static void Place(Roamer roamer, Vector2Int cell) =>
            typeof(Roamer).GetProperty("Position").GetSetMethod(true).Invoke(roamer, new object[] { cell });

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

            // 덤불 띠를 지나 몬스터 쪽으로 (이 시험은 풀숲 조우 확률 0) — 가까워지면 [선제공격]
            yield return HoldStick(field, Direction.Right, () => field.IsPreemptAvailable || field.IsInBattle, 6f);
            Assert.IsFalse(field.IsInBattle, "풀숲 조우 확률 0");
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

        // #79: 처음 [선제공격] 기회 → 화면이 멈추고 세 단계 설명 (사용자가 정한 말).
        // #145: 시간이 완전히 멈춤 — [다음]/[확인]이나 글을 눌러야 넘어가고, 안내가 끝나기 전엔 [선제공격]이 안 눌림, 닫은 뒤에도 잠깐 몬스터가 멈춤
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

            // 읽는 중에 빛나는 버튼을 눌러도 문제가 시작되지 않음 (#145 — 눌리면 안내가 떠 있는 채로 시간이 흘러 실패로 넘어갔음)
            FindButton(field.transform, "PreemptButton").onClick.Invoke();
            yield return null;
            Assert.IsFalse(field.Preempt.IsOpen, "안내가 끝나야 누를 수 있음");
            Assert.AreEqual(0, field.PauseTutorial.Step, "버튼을 눌러도 안내는 그대로");

            // 글을 눌러도 다음으로 (#145)
            FindButton(field.transform, "GuideBox").onClick.Invoke();
            yield return null;
            Assert.AreEqual("일정 거리 적과 가까워지면 선제공격을 할 수 있습니다.", field.PauseTutorial.Message);
            Assert.IsTrue(field.IsPreemptRangeVisible, "몬스터 둘레에 선제공격 범위");
            ActiveButton(field.transform, "TutorialNextButton").onClick.Invoke();
            yield return null;
            StringAssert.Contains("[선제공격] 버튼을 눌러 보세요", field.PauseTutorial.Message);
            Assert.IsFalse(field.IsPreemptRangeVisible);
            var confirm = ActiveButton(field.transform, "TutorialNextButton");
            Assert.AreEqual("확인", UiKit.LabelOf(confirm).text, "마지막은 [확인]");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(field.PauseTutorial.IsShowing, "시간이 지나도 저절로 안 넘어감");
            Assert.IsFalse(field.IsInBattle);

            confirm.onClick.Invoke();
            yield return WaitFor(() => !field.PauseTutorial.IsShowing, 2f);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Preempt), "본 것으로 저장 — 다시 안 나옴");

            // 닫은 뒤에도 잠깐 몬스터가 멈춰 있어 누를 틈이 있음
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(held, roamer.Position, "닫은 직후 몬스터는 그대로");
            Assert.IsFalse(field.IsInBattle);
            Assert.IsTrue(field.IsPreemptAvailable, "이제 누를 수 있음");
            FindButton(field.transform, "PreemptButton").onClick.Invoke();
            yield return WaitFor(() => field.Preempt.IsOpen, 2f);
            Assert.IsTrue(field.Preempt.IsOpen, "안내가 끝나면 눌러서 QTE");
            Object.Destroy(field.gameObject);
        }

        // #232 사용자: 살금살금 물약을 먼저 마셔도 처음 한 번은 몬스터가 쫓아와 선제공격 안내가 나옴. 안내를 본 뒤로는 물약대로 못 알아챔
        [UnityTest]
        public IEnumerator TheFirstPreemptLessonComesEvenWhileSneaking()
        {
            var quiet = TestData.Item("potion_quiet").Set("kind", WordRPG.Items.ItemKind.Consumable)
                .Set("effect", WordRPG.Items.ItemEffect.Quiet).Set("effectAmount", 500);
            var session = GameSession.NewGame(Hero(), 5);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass }) session.Tutorials.Mark(id);
            session.Effects.Apply(quiet);
            var (field, words) = Create(session, tutorials: true);
            for (int i = 0; i < 5; i++) session.Vocabulary.Discover(words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            yield return null;
            yield return null;
            Assert.IsFalse(field.SneakingPastRoamers, "안내를 아직 못 봄 → 물약이 있어도 알아챔");

            yield return HoldStick(field, Direction.Right, () => field.PauseTutorial.IsShowing || field.IsInBattle, 6f);
            Assert.IsTrue(field.PauseTutorial.IsShowing, "물약 중에도 첫 선제공격 안내");
            for (int i = 0; i < 3; i++)
            {
                field.PauseTutorial.PressNext();
                yield return null;
                yield return null;
            }
            Assert.IsFalse(field.SneakingPastRoamers, "안내 뒤 그 기회가 끝날 때까지는 계속 쫓아옴");
            Assert.IsTrue(field.IsPreemptAvailable, "그래서 [선제공격]을 해 볼 수 있음");
            Object.Destroy(field.gameObject);
            yield return null;

            // 안내를 본 사람: 물약대로 몬스터가 못 알아챔
            var (again, _) = Create(session, tutorials: true);
            yield return null;
            Assert.IsTrue(again.SneakingPastRoamers);
            Object.Destroy(again.gameObject);
        }

        // #145 사용자: 딱 붙자 그냥 전투로 넘어감 — 아직 나를 못 본 몬스터에 처음 부딪히면 전투 대신 선제공격 안내, 안내를 본 뒤로는 부딪히면 전투
        [UnityTest]
        public IEnumerator BumpingBeforeTheFirstPreemptShowsTheTutorialInstead()
        {
            var session = GameSession.NewGame(Hero(), 5);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass }) session.Tutorials.Mark(id);
            var (field, words) = Create(session, tutorials: true);
            for (int i = 0; i < 5; i++) session.Vocabulary.Discover(words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            yield return null;
            yield return null;

            // 몬스터를 바로 오른쪽으로: 어슬렁거리는 중이라 아직 [선제공격] 없음
            var roamer = field.Roaming.Roamers[0];
            Place(roamer, field.PlayerCell + Vector2Int.right);
            Assert.AreEqual(RoamerState.Wandering, roamer.State);
            yield return HoldStick(field, Direction.Right, () => field.PauseTutorial.IsShowing || field.IsInBattle, 3f);
            Assert.IsFalse(field.IsInBattle, "처음엔 부딪혀도 바로 전투가 아님");
            Assert.IsTrue(field.PauseTutorial.IsShowing, "대신 안내");
            for (int i = 0; i < 3; i++)
            {
                field.PauseTutorial.PressNext();
                yield return null;
                yield return null;
            }
            Assert.IsFalse(field.PauseTutorial.IsShowing);

            // 닫은 직후 스틱을 밀고 있어도 전투로 안 넘어감
            yield return HoldStick(field, Direction.Right, () => field.IsInBattle, 0.3f);
            Assert.IsFalse(field.IsInBattle, "안내 직후엔 부딪혀도 누를 틈");
            Assert.IsTrue(field.IsPreemptAvailable);
            Object.Destroy(field.gameObject);
        }

        // 안내를 본 사람은 부딪히면 그대로 전투 (GDD 보이는 몬스터 4번)
        [UnityTest]
        public IEnumerator BumpingAfterTheTutorialStartsABattle()
        {
            var session = GameSession.NewGame(Hero(), 5);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Preempt }) session.Tutorials.Mark(id);
            var (field, words) = Create(session, tutorials: true);
            for (int i = 0; i < 5; i++) session.Vocabulary.Discover(words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            yield return null;
            yield return null;
            var roamer = field.Roaming.Roamers[0];
            Place(roamer, field.PlayerCell + Vector2Int.right);
            yield return HoldStick(field, Direction.Right, () => field.IsInBattle, 3f);
            Assert.IsTrue(field.IsInBattle, "부딪히면 전투");
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

        // #123: 보이는 몬스터 지역도 풀숲을 밟으면 확률 조우 (초원·서고처럼). 풀숲 전투는 보이는 몬스터와 따로 —
        // 이겨도 보이는 몬스터는 그대로, 끝나면 잠깐(2초) 몬스터가 새로 알아채지 않음
        [UnityTest]
        public IEnumerator GrassEncounterInRoamingAreaIsSeparateFromTheVisibleMonster()
        {
            var session = GameSession.NewGame(Hero(), 5);
            var (field, _) = Create(session, grassRate: 1f);
            yield return null;
            yield return null;
            var roamer = field.Roaming.Roamers[0];
            Assert.IsFalse(field.Roaming.HuntingPlayer, "몬스터는 멀리서 어슬렁");

            yield return HoldStick(field, Direction.Right, () => field.IsInBattle, 3f);
            Assert.IsTrue(field.IsInBattle, "풀숲을 밟으면 조우");
            Assert.AreEqual(new Vector2Int(3, 4), field.PlayerCell, "첫 풀숲 칸에서");
            yield return WaitFor(() => field.Battle.IsRunning, 3f);
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            Assert.AreEqual("승리!", field.Battle.ResultTitle);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);
            Assert.AreEqual(1, field.Roaming.Roamers.Count, "보이는 몬스터는 그대로");
            Assert.AreSame(roamer, field.Roaming.Roamers[0]);
            Assert.AreEqual(0, field.Roaming.PendingRespawns);
            Assert.Greater(field.Roaming.CalmLeft, 1f, "전투 뒤 잠깐은 몬스터가 새로 알아채지 않음");
            Object.Destroy(field.gameObject);
        }

        // #123: 보이는 몬스터가 나를 쫓는 동안은 풀숲을 밟아도 조우 없음 — 그 싸움은 쫓아오는 몬스터와 (연달아 두 번 싸우지 않게)
        [UnityTest]
        public IEnumerator NoGrassEncounterWhileAMonsterIsChasingYou()
        {
            var session = GameSession.NewGame(Hero(), 5);
            var (field, _) = Create(session, grassRate: 1f, map: ChaseMap());
            yield return null;
            yield return null;
            Assert.AreEqual(1, field.Roaming.Roamers.Count);

            // 풀숲이 없는 줄로 몬스터 쪽으로 → '!' (쫓기 시작)
            yield return HoldStick(field, Direction.Right, () => field.Roaming.HuntingPlayer || field.IsInBattle, 6f);
            Assert.IsFalse(field.IsInBattle);
            Assert.IsTrue(field.Roaming.HuntingPlayer, "몬스터가 나를 발견");
            yield return WaitFor(() => !field.IsMoving, 1f);

            // 바로 아래 풀숲으로 한 칸 — 조우 확률 1이어도 풀숲 조우 없음
            int x = field.PlayerCell.x;
            yield return HoldStick(field, Direction.Down, () => field.PlayerCell.y == 3, 1f);
            yield return WaitFor(() => !field.IsMoving, 1f);
            Assert.AreEqual(new Vector2Int(x, 3), field.PlayerCell);
            Assert.AreEqual(FieldTile.Grass, field.CurrentArea.Map.Get(field.PlayerCell));
            yield return null;
            Assert.IsFalse(field.IsInBattle, "쫓기는 중엔 풀숲 조우 없음");

            // 쫓아온 몬스터와 싸움 → 이기면 그 몬스터가 사라짐 (= 풀숲 전투가 아니라 보이는 몬스터 전투)
            yield return WaitFor(() => field.Battle.IsRunning, 4f);
            Assert.IsTrue(field.Battle.IsRunning, "쫓아온 몬스터에 부딪혀 전투");
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);
            Assert.AreEqual(0, field.Roaming.Roamers.Count, "싸운 건 쫓아온 몬스터");
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
