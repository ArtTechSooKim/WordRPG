using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 먹물도롱뇽 (#113, 1.2 동굴 — 보관했던 잉크 몬스터 아이디어): 나루터가 없는 맵의 물(먹물 웅덩이)에 숨어 있다가
    // 가까이(2칸) 오면 거품 → 튀어나와 땅 위에서 쫓아옴 → 놓치면 웅덩이로 돌아가 다시 숨음
    public class LurkerTests
    {
        private static readonly MonsterSpecies Salamander = TestData.Species("ink_salamander", new MonsterStats(30, 6, 5), new MonsterStats(5, 1, 1));
        private static readonly MonsterSpecies Spider = TestData.Species("tangle_spider", new MonsterStats(30, 6, 5), new MonsterStats(5, 1, 1));

        //  40×9: 왼쪽 위 작은 웅덩이 (3,6)~(5,6) 3칸, 오른쪽 끝 큰 웅덩이(35~38열 × 7줄 = 28칸), 나머지는 바위 바닥
        private static FieldMap Map()
        {
            var rows = new List<string>();
            for (int r = 0; r < 9; r++)
            {
                var row = new char[40];
                for (int x = 0; x < 40; x++)
                {
                    int y = 8 - r;
                    row[x] = r == 0 || r == 8 || x == 0 || x == 39 ? '#'
                        : (y == 6 && x >= 3 && x <= 5) || (x >= 35 && x <= 38) ? '~' : ':';
                }
                rows.Add(new string(row));
            }
            var start = rows[7].ToCharArray(); // y = 1
            start[18] = 'P';
            rows[7] = new string(start);
            return FieldMap.Parse(string.Join("\n", rows));
        }

        private static RoamingMonsters Roaming(FieldMap map, int seed = 4) =>
            new RoamingMonsters(map, r => new List<MonsterInstance> { new MonsterInstance(Spider, 5) }, new System.Random(seed), null,
                r => new List<MonsterInstance> { new MonsterInstance(Salamander, 5) });

        private static Roamer SmallPoolLurker(RoamingMonsters roaming) => roaming.Roamers[0].Home.x < 10 ? roaming.Roamers[0] : roaming.Roamers[1];

        [Test]
        public void LurkersHideInEveryInkPool()
        {
            var map = Map();
            var roaming = Roaming(map);
            Assert.AreEqual(2, roaming.PoolCount, "웅덩이 두 개 (나루터가 없으니 뗏목 강이 아님)");
            Assert.AreEqual(0, roaming.WaterHabitat.Count, "뗏목 강 몬스터(흉내문어)는 없음");
            roaming.Populate(map.Start);
            Assert.AreEqual(2, roaming.Roamers.Count, "웅덩이마다 한 마리 (서식지 덤불이 없어 땅 몬스터는 없음)");
            foreach (var lurker in roaming.Roamers)
            {
                Assert.IsTrue(lurker.Lurker);
                Assert.AreEqual(RoamerState.Lurking, lurker.State);
                Assert.IsTrue(lurker.Hidden, "숨어 있어 안 보임");
                Assert.AreEqual(FieldTile.Water, map.Get(lurker.Position), "웅덩이 속");
                Assert.AreEqual(lurker.Home, lurker.Position);
            }
            Assert.IsNull(roaming.PreemptTarget(new Vector2Int(4, 5)), "숨은 몬스터는 선제공격 대상이 아님");
        }

        [Test]
        public void ComingCloseMakesBubblesThenItJumpsOutAndChases()
        {
            var map = Map();
            var roaming = Roaming(map);
            roaming.Populate(map.Start);
            var lurker = SmallPoolLurker(roaming);
            var near = lurker.Home + new Vector2Int(0, -2); // 웅덩이 아래 2칸
            Assert.IsEmpty(roaming.Tick(0.1f, map.Start), "멀면 그대로 숨어 있음");

            var events = new List<RoamerEvent>(roaming.Tick(0.1f, near));
            Assert.IsTrue(events.Exists(e => e.Kind == RoamerEventKind.Surfaced && e.Roamer == lurker), "거품");
            Assert.AreEqual(RoamerState.Surfacing, lurker.State);
            Assert.IsTrue(lurker.Hidden, "거품이 오르는 동안은 아직 안 보임");
            Assert.IsEmpty(roaming.Tick(0.5f, near), "0.8초 경고 동안은 기다림");

            events = new List<RoamerEvent>(roaming.Tick(0.4f, near));
            Assert.IsTrue(events.Exists(e => e.Kind == RoamerEventKind.Moved && e.Roamer == lurker && map.Get(e.From) == FieldTile.Water), "웅덩이에서 튀어나옴");
            Assert.IsTrue(events.Exists(e => e.Kind == RoamerEventKind.Noticed && e.Roamer == lurker), "'!'");
            Assert.AreNotEqual(FieldTile.Water, map.Get(lurker.Position), "땅 위");
            Assert.AreEqual(1, (lurker.Position - lurker.Home).sqrMagnitude, "웅덩이 바로 옆 칸");
            Assert.IsFalse(lurker.Hidden);

            roaming.Tick(0.7f, near);
            Assert.AreEqual(RoamerState.Chasing, lurker.State, "쫓아옴");
            Assert.AreEqual(lurker, roaming.PreemptTarget(near), "튀어나오면 선제공격 대상");
        }

        // #123 풀숲 조우와 겹치지 않게: 거품이 오르는 동안은 '쫓기는 중' (풀숲 조우 없음), 전투 뒤 2초는 거품도 안 올림
        [Test]
        public void BubblingCountsAsHuntingAndWaitsAfterABattle()
        {
            var map = Map();
            var roaming = Roaming(map);
            roaming.Populate(map.Start);
            var lurker = SmallPoolLurker(roaming);
            var near = lurker.Home + new Vector2Int(0, -2);
            Assert.IsFalse(roaming.HuntingPlayer, "숨어만 있으면 풀숲 조우 가능");

            roaming.Calm();
            for (float t = 0; t < roaming.Rules.CalmSeconds - 0.1f; t += 0.05f) roaming.Tick(0.05f, near);
            Assert.AreEqual(RoamerState.Lurking, lurker.State, "전투 뒤 잠깐은 가까이 가도 거품 없음");

            for (float t = 0; t < 0.3f; t += 0.05f) roaming.Tick(0.05f, near);
            Assert.AreEqual(RoamerState.Surfacing, lurker.State, "지나면 평소처럼 거품");
            Assert.IsTrue(roaming.HuntingPlayer, "거품이 오르는 동안은 쫓기는 중");
        }

        [Test]
        public void LosingTheHeroSendsItBackIntoThePool()
        {
            var map = Map();
            var roaming = Roaming(map);
            roaming.Populate(map.Start);
            var lurker = SmallPoolLurker(roaming);
            var near = lurker.Home + new Vector2Int(0, -2);
            roaming.Tick(0.1f, near);
            roaming.Tick(1f, near);
            roaming.Tick(0.7f, near);
            Assert.AreEqual(RoamerState.Chasing, lurker.State);

            var far = new Vector2Int(30, 1); // 포기 거리(10칸) 밖
            bool dived = false;
            for (int i = 0; i < 200 && !dived; i++)
                foreach (var e in roaming.Tick(0.1f, far))
                    if (e.Kind == RoamerEventKind.Dived && e.Roamer == lurker) dived = true;
            Assert.IsTrue(dived, "웅덩이로 돌아가 뛰어듦");
            Assert.AreEqual(RoamerState.Lurking, lurker.State, "다시 숨음");
            Assert.AreEqual(lurker.Home, lurker.Position);
        }

        [Test]
        public void MapsWithDocksKeepSwimmingWaterMonsters()
        {
            var map = FieldMap.Parse(
                "#########\n" +
                "#P:J~~~~#\n" +
                "#:::~~~~#\n" +
                "#########");
            var roaming = Roaming(map);
            Assert.AreEqual(0, roaming.PoolCount, "나루터가 있으면 숨는 웅덩이가 아니라 뗏목 강");
            Assert.Greater(roaming.WaterHabitat.Count, 0);
        }

        [Test]
        public void DefeatedLurkerComesBackLaterInAPool()
        {
            var map = Map();
            var roaming = Roaming(map);
            roaming.Populate(map.Start);
            var lurker = roaming.Roamers[0];
            roaming.Defeat(lurker);
            Assert.AreEqual(1, roaming.PendingRespawns);
            roaming.Tick(roaming.Rules.RespawnSeconds + 0.1f, map.Start);
            Assert.AreEqual(2, roaming.Roamers.Count, "다른 때 다시 숨어 있음");
            Assert.IsTrue(new List<Roamer>(roaming.Roamers).TrueForAll(r => r.Lurker && r.State == RoamerState.Lurking));
        }

        // 글자 반딧돌 문제 (#113): 석판과 같은 규칙(틀려도 벌점 없이 다시, 숙련도는 처음 답만)으로 한 문제 — 영단어를 보고 뜻 고르기
        [Test]
        public void GlowStoneQuizIsOneEnglishToMeaningQuestion()
        {
            var provider = new FakeQuizProvider(); // 정답 = 0번
            var quiz = new TabletQuiz(provider, 1, QuizDirection.EnglishToMeaning);
            Assert.AreEqual(QuizDirection.EnglishToMeaning, provider.AskedDirections[0], "영단어 → 뜻 (석판과 반대)");
            Assert.AreEqual(TabletAnswer.Wrong, quiz.Answer(3));
            Assert.IsFalse(quiz.IsComplete);
            Assert.AreEqual(TabletAnswer.Correct, quiz.Answer(0));
            Assert.IsTrue(quiz.IsComplete, "한 문제면 끝");
            quiz.Next();
            Assert.AreEqual(1, provider.AskedDirections.Count, "문제는 하나만");
            CollectionAssert.AreEqual(new[] { false }, provider.Submitted, "숙련도엔 처음 고른 답만");
        }

        // 목표 한 줄 (#113, Figma #111): 동굴에서 열쇠를 찾기 전까지 '반딧돌을 찾아 불을 밝혀 보자 n/8' (별 없음), 다 밝히거나 열쇠를 찾으면 보스 목표
        [Test]
        public void CaveObjectiveIsLightingStonesUntilTheKey()
        {
            const string map =
                "#########\n" +
                "#..B....#\n" +
                "##GG#####\n" +
                "#I:::::I#\n" +
                "#P:::::K#\n" +
                "#########";
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", Monsters.SkillKind.Damage, Monsters.SkillTarget.SingleEnemy, 1);
            var vine = TestData.Species("ellipsis_vine", new MonsterStats(40, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "말줄임덩굴");
            var key = TestData.Item("cave_key").Set("displayName", "동굴 열쇠").Set("kind", Items.ItemKind.Key);
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "cave_goal").Set("displayName", "어두운 밀림 동굴").Set("theme", FieldTheme.Cave).Set("map", map)
                .Set("words", words).Set("boss", new BossEncounter(vine, 5)).Set("bossKey", key).Set("bossGateWords", 4);
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            session.Record.RecordBattle(true, 1, 0);

            var goal = Objectives.Current(session, area);
            Assert.AreEqual(ObjectiveKind.LightStones, goal.Kind, "처음엔 반딧돌 밝히기");
            Assert.IsNull(goal.Cell, "별은 없음 — 꺼진 반딧돌의 희미한 빛이 길잡이");
            GlowStones.Light(area, session.World, new Vector2Int(1, 2));
            Assert.AreEqual(ObjectiveKind.LightStones, Objectives.Current(session, area).Kind, "1/2");
            GlowStones.Light(area, session.World, new Vector2Int(7, 2));
            Assert.AreEqual(ObjectiveKind.FindKey, Objectives.Current(session, area).Kind, "다 밝히면 열쇠 찾기");

            var other = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            other.Record.RecordBattle(true, 1, 0);
            Assert.IsTrue(BossGate.TakeKey(area, other));
            Assert.AreNotEqual(ObjectiveKind.LightStones, Objectives.Current(other, area).Kind, "열쇠를 먼저 찾으면 반딧돌 목표는 건너뜀");
        }

        [Test]
        public void EveryStoneLitMakesTheWholeCaveFaintlyBright()
        {
            var darkness = new DarknessMap(20, 5, 2);
            darkness.SetStatic(null, null);
            Assert.AreEqual(0f, darkness.StaticAt(new Vector2(15.5f, 2.5f)), "처음엔 캄캄");
            darkness.SetStatic(null, null, CaveLight.AllLitGlow);
            Assert.AreEqual(CaveLight.AllLitGlow, darkness.StaticAt(new Vector2(15.5f, 2.5f)), 1e-4f, "모두 밝히면 어디든 은은하게");
            Assert.Greater(CaveLight.AllLitGlow, CaveLight.EyesBelow, "그러면 몬스터 몸도 보임");
        }
    }
}
