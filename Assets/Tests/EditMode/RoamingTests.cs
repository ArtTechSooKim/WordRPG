using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 보이는 몬스터 조우 (#73, 사막부터 — GDD 9-5): 어슬렁 → 발견("!") → 쫓기 → 부딪힘/포기, 선제공격
    public class RoamingTests
    {
        private static readonly MonsterSpecies Slime = TestData.Species("slime", new MonsterStats(50, 6, 5), new MonsterStats(5, 1, 1));

        // 테두리는 '#', 안은 ':'(모래). cells = (x, y, 글자) — y는 위쪽이 큰 FieldMap 좌표. P는 (1,1)
        private static FieldMap Map(int width, int height, params (int x, int y, char c)[] cells)
        {
            var rows = new char[height][];
            for (int r = 0; r < height; r++)
            {
                rows[r] = new char[width];
                for (int x = 0; x < width; x++)
                    rows[r][x] = r == 0 || r == height - 1 || x == 0 || x == width - 1 ? '#' : ':';
            }
            rows[height - 2][1] = 'P';
            foreach (var (x, y, c) in cells) rows[height - 1 - y][x] = c;
            return FieldMap.Parse(string.Join("\n", rows.Select(r => new string(r))));
        }

        private static RoamingMonsters Roaming(FieldMap map, RoamingRules rules = null, int seed = 1) =>
            new RoamingMonsters(map, rng => new List<MonsterInstance> { new MonsterInstance(Slime, 5) }, new System.Random(seed), rules);

        // 서식지 한 칸에 한 마리 (주인공은 멀리)
        private static (RoamingMonsters roaming, Roamer roamer) One(FieldMap map, Vector2Int farPlayer, RoamingRules rules = null)
        {
            var roaming = Roaming(map, rules);
            roaming.Populate(farPlayer);
            Assert.AreEqual(1, roaming.Roamers.Count);
            return (roaming, roaming.Roamers[0]);
        }

        private static List<RoamerEvent> Run(RoamingMonsters roaming, Vector2Int player, float seconds, float dt = 0.02f)
        {
            var events = new List<RoamerEvent>();
            for (float t = 0; t < seconds; t += dt) events.AddRange(roaming.Tick(dt, player));
            return events;
        }

        [Test]
        public void MonstersAppearOnlyInTheirHabitatAwayFromThePlayer()
        {
            var cells = new List<(int, int, char)>();
            for (int x = 5; x < 75; x++)
            for (int y = 5; y < 55; y += 2)
                cells.Add((x, y, ','));
            var map = Map(80, 60, cells.ToArray());
            var roaming = Roaming(map);
            var player = new Vector2Int(40, 30);
            roaming.Populate(player);

            Assert.AreEqual(Mathf.Min(roaming.Habitat.Count / roaming.Rules.CellsPerRoamer, roaming.Rules.MaxRoamers), roaming.Roamers.Count);
            foreach (var roamer in roaming.Roamers)
            {
                Assert.AreEqual(FieldTile.Grass, map.Get(roamer.Position), "서식지(마른 덤불)에서 태어남");
                Assert.GreaterOrEqual((roamer.Position - player).magnitude, roaming.Rules.SpawnMinDistance, "화면 밖에서 나타남");
                foreach (var other in roaming.Roamers)
                    if (other != roamer)
                        Assert.GreaterOrEqual((other.Position - roamer.Position).magnitude, roaming.Rules.MinSpacing, "몰려 있지 않음");
            }
        }

        // 유적 바닥(';')·나루터('J')에도 들어오지 않음 (#80 — #116 버그 심기 검사에서 빠져 있던 것)
        [Test]
        public void WanderingMonstersStayNearHomeAndNeverStandOnDoorsObjectsOrRuins()
        {
            var cells = new List<(int, int, char)> { (20, 15, ','), (22, 15, 'C'), (39, 15, 'D'), (18, 17, '~'), (18, 13, 'J'), (19, 13, 'J') };
            for (int y = 12; y <= 18; y++) cells.Add((23, y, ';'));
            var map = Map(40, 30, cells.ToArray());
            var (roaming, roamer) = One(map, new Vector2Int(2, 2), new RoamingRules { ActiveRadius = 100 });
            for (int i = 0; i < 3000; i++)
            {
                roaming.Tick(0.05f, new Vector2Int(2, 2));
                Assert.LessOrEqual((roamer.Position - roamer.Home).sqrMagnitude, roaming.Rules.LeashRadius * roaming.Rules.LeashRadius);
                var tile = map.Get(roamer.Position);
                Assert.IsTrue(tile == FieldTile.Lawn || tile == FieldTile.Grass || tile == FieldTile.Floor, tile.ToString());
            }
        }

        [Test]
        public void MonsterNoticesThePlayerThenChasesAndBumpsIntoThem()
        {
            var map = Map(40, 20, (20, 10, ','));
            var (roaming, roamer) = One(map, new Vector2Int(2, 2));
            var player = roamer.Position + new Vector2Int(4, 0);

            var events = roaming.Tick(0.01f, player);
            Assert.IsTrue(events.Any(e => e.Kind == RoamerEventKind.Noticed), "발견 영역(5칸)에 들어오면 '!'");
            Assert.AreEqual(RoamerState.Noticing, roamer.State);
            Assert.IsNull(roaming.At(player));

            events = Run(roaming, player, 2f);
            var caught = events.FirstOrDefault(e => e.Kind == RoamerEventKind.Caught);
            Assert.AreEqual(RoamerEventKind.Caught, caught.Kind, "쫓아와서 부딪힘 → 전투");
            Assert.AreEqual(1, (roamer.Position - player).sqrMagnitude, "바로 옆 칸에서 부딪힘");
        }

        [Test]
        public void ChaseSpeedIsBetweenWalkingAndRunning()
        {
            var rules = new RoamingRules();
            Assert.Less(rules.ChaseStepSeconds, FieldScreen.WalkStepSeconds, "걷기보다 빠름 — 걸어서는 못 따돌림");
            Assert.Greater(rules.ChaseStepSeconds, FieldScreen.WalkStepSeconds * FieldScreen.RunStepRatio, "달리기보다 느림 — 달리면 따돌림");
        }

        // 긴 복도에서 주인공이 일정한 속도로 도망: 달리면 놓치고(포기), 걸으면 잡힌다
        [TestCase(FieldScreen.WalkStepSeconds * FieldScreen.RunStepRatio, false)]
        [TestCase(FieldScreen.WalkStepSeconds, true)]
        public void RunningAwayLosesTheMonsterButWalkingDoesNot(float playerStepSeconds, bool expectCaught)
        {
            var map = Map(150, 7, (10, 3, ','));
            var (roaming, roamer) = One(map, new Vector2Int(140, 3));
            var player = roamer.Position + new Vector2Int(4, 0);
            bool caught = false, gaveUp = false;
            float untilStep = playerStepSeconds;
            for (float t = 0; t < 20f && !caught && !gaveUp; t += 0.01f)
            {
                untilStep -= 0.01f;
                if (untilStep <= 0f && player.x < 147)
                {
                    player += Vector2Int.right;
                    untilStep += playerStepSeconds;
                }
                foreach (var e in roaming.Tick(0.01f, player))
                {
                    caught |= e.Kind == RoamerEventKind.Caught;
                    gaveUp |= e.Kind == RoamerEventKind.GaveUp;
                }
            }
            Assert.AreEqual(expectCaught, caught);
            Assert.AreEqual(!expectCaught, gaveUp, "놓치면 포기하고 돌아감");
        }

        [Test]
        public void MonsterThatLosesThePlayerGoesBackHome()
        {
            var map = Map(60, 20, (10, 10, ','));
            var (roaming, roamer) = One(map, new Vector2Int(55, 2));
            Run(roaming, roamer.Position + new Vector2Int(4, 0), 0.7f); // 발견 → 쫓기 시작
            Assert.AreEqual(RoamerState.Chasing, roamer.State);
            var events = Run(roaming, new Vector2Int(58, 18), 0.1f);   // 순간 멀어짐
            Assert.IsTrue(events.Any(e => e.Kind == RoamerEventKind.GaveUp));
            Run(roaming, new Vector2Int(58, 18), 10f);
            Assert.AreEqual(RoamerState.Wandering, roamer.State);
            Assert.LessOrEqual((roamer.Position - roamer.Home).sqrMagnitude, roaming.Rules.LeashRadius * roaming.Rules.LeashRadius);
        }

        [Test]
        public void MonsterChasesAroundWalls()
        {
            // 몬스터와 주인공 사이에 벽, 위쪽에만 틈
            var cells = new List<(int, int, char)> { (10, 5, ',') };
            for (int y = 1; y < 9; y++) cells.Add((13, y, '#'));
            var map = Map(30, 12, cells.ToArray());
            var (roaming, roamer) = One(map, new Vector2Int(28, 1));
            var events = Run(roaming, new Vector2Int(15, 5), 4f);
            Assert.IsTrue(events.Any(e => e.Kind == RoamerEventKind.Caught), "벽을 돌아서 쫓아옴");
        }

        [Test]
        public void FleeingStunsTheMonsterForAWhile()
        {
            var map = Map(40, 20, (20, 10, ','));
            var (roaming, roamer) = One(map, new Vector2Int(2, 2));
            var player = roamer.Position + new Vector2Int(2, 0);
            Run(roaming, player, 0.7f);
            roaming.Stun(roamer);
            var events = Run(roaming, player, roaming.Rules.StunSeconds - 0.1f);
            Assert.IsFalse(events.Any(e => e.Kind == RoamerEventKind.Moved || e.Kind == RoamerEventKind.Caught), "멈춘 동안 움직이지 않음");
            Assert.AreEqual(RoamerState.Stunned, roamer.State);
        }

        // 풀숲 조우 (#123): 보이는 몬스터가 나를 발견("!")했거나 쫓는 동안만 '쫓기는 중' — 그동안 풀숲 조우 없음
        [Test]
        public void HuntingOnlyWhileAMonsterHasNoticedOrIsChasing()
        {
            var map = Map(60, 20, (10, 10, ','));
            var (roaming, roamer) = One(map, new Vector2Int(55, 2));
            Run(roaming, new Vector2Int(55, 2), 1f);
            Assert.IsFalse(roaming.HuntingPlayer, "멀리서 어슬렁 — 풀숲 조우 가능");

            var near = roamer.Position + new Vector2Int(4, 0);
            roaming.Tick(0.01f, near);
            Assert.AreEqual(RoamerState.Noticing, roamer.State);
            Assert.IsTrue(roaming.HuntingPlayer, "'!' 뜬 순간부터");
            Run(roaming, near, 0.7f);
            Assert.AreEqual(RoamerState.Chasing, roamer.State);
            Assert.IsTrue(roaming.HuntingPlayer, "쫓는 동안");

            Run(roaming, new Vector2Int(58, 18), 0.1f); // 달아나서 놓침
            Assert.AreEqual(RoamerState.Returning, roamer.State);
            Assert.IsFalse(roaming.HuntingPlayer, "놓치고 돌아가면 다시 풀숲 조우 가능");

            roaming.Stun(roamer);
            Assert.IsFalse(roaming.HuntingPlayer, "도망친 뒤 멈춘 몬스터도 쫓지 않음");
        }

        // 전투가 끝나면 (#123) 2초 동안은 가까이 있어도 새로 알아채지 않음 — 풀숲 전투에서 나오자마자 덮치지 않게
        [Test]
        public void AfterABattleMonstersWaitBeforeNoticing()
        {
            var map = Map(40, 20, (20, 10, ','));
            var (roaming, roamer) = One(map, new Vector2Int(2, 2), new RoamingRules { LeashRadius = 1 }); // 늘 발견 거리 안에 있게
            var player = roamer.Position + new Vector2Int(3, 0);
            roaming.Calm();
            Assert.AreEqual(roaming.Rules.CalmSeconds, roaming.CalmLeft);

            var events = Run(roaming, player, roaming.Rules.CalmSeconds - 0.1f);
            Assert.IsFalse(events.Any(e => e.Kind == RoamerEventKind.Noticed || e.Kind == RoamerEventKind.Caught), "잠깐은 못 알아챔");
            Assert.IsFalse(roaming.HuntingPlayer);

            events = Run(roaming, player, 0.3f);
            Assert.IsTrue(events.Any(e => e.Kind == RoamerEventKind.Noticed), "지나면 평소처럼 '!'");
        }

        [Test]
        public void CalmDoesNotStopAMonsterAlreadyChasing()
        {
            var map = Map(40, 20, (20, 10, ','));
            var (roaming, roamer) = One(map, new Vector2Int(2, 2));
            var player = roamer.Position + new Vector2Int(4, 0);
            Run(roaming, player + new Vector2Int(0, 3), 0.7f);
            Assert.AreEqual(RoamerState.Chasing, roamer.State);
            roaming.Calm();
            var events = Run(roaming, player, 1.5f);
            Assert.IsTrue(events.Any(e => e.Kind == RoamerEventKind.Caught), "이미 쫓던 몬스터는 그대로 쫓아옴");
        }

        [Test]
        public void DefeatedMonsterComesBackLaterSomewhereFar()
        {
            var cells = new List<(int, int, char)>();
            for (int x = 3; x < 57; x += 3) cells.Add((x, 10, ','));
            var map = Map(60, 20, cells.ToArray());
            var player = new Vector2Int(30, 10);
            var roaming = Roaming(map, new RoamingRules { CellsPerRoamer = 1000 });
            roaming.Populate(player);
            var roamer = roaming.Roamers[0];
            roaming.Defeat(roamer);
            Assert.AreEqual(0, roaming.Roamers.Count);
            Assert.AreEqual(1, roaming.PendingRespawns);

            var events = Run(roaming, player, roaming.Rules.RespawnSeconds - 0.5f, 0.1f);
            Assert.IsFalse(events.Any(e => e.Kind == RoamerEventKind.Spawned), "바로 다시 나오지 않음");
            events = Run(roaming, player, 1f, 0.1f);
            var spawned = events.First(e => e.Kind == RoamerEventKind.Spawned).Roamer;
            Assert.GreaterOrEqual((spawned.Position - player).magnitude, roaming.Rules.SpawnMinDistance);
        }

        [Test]
        public void SlowMotionSlowsTheChase()
        {
            int Steps(float slow)
            {
                var map = Map(60, 9, (5, 4, ','));
                var (roaming, roamer) = One(map, new Vector2Int(55, 4));
                Run(roaming, roamer.Position + new Vector2Int(4, 0), 0.7f); // 발견하고 쫓기 시작
                roaming.SlowFactor = slow;
                return Run(roaming, new Vector2Int(roamer.Position.x + 6, 4), 0.5f).Count(e => e.Kind == RoamerEventKind.Moved);
            }
            Assert.Less(Steps(0.15f), Steps(1f), "[선제공격] 문제를 푸는 동안 느려짐");
        }

        [Test]
        public void PreemptButtonTargetsOnlyAMonsterThatNoticedYouAndIsClose()
        {
            var map = Map(40, 20, (20, 10, ','));
            var (roaming, roamer) = One(map, new Vector2Int(2, 2));
            var near = roamer.Position + new Vector2Int(2, 0);
            Assert.IsNull(roaming.PreemptTarget(near), "아직 발견 전");
            roaming.Tick(0.01f, near);
            Assert.AreSame(roamer, roaming.PreemptTarget(near));
            Assert.IsNull(roaming.PreemptTarget(roamer.Position + new Vector2Int(roaming.Rules.PreemptRadius + 1, 0)), "멀면 버튼 없음");
        }

        [Test]
        public void PreemptiveStrikeTakesAFifthOfTheLeadersHp()
        {
            var monster = new MonsterInstance(Slime, 5);
            int max = monster.Stats.MaxHp;
            int preview = Preemptive.Damage(monster);
            Assert.AreEqual(max, monster.CurrentHp, "미리 보기는 HP를 깎지 않음 (QTE 결과 화면의 숫자)");
            int dealt = Preemptive.Strike(monster);
            Assert.AreEqual(Mathf.RoundToInt(max * Preemptive.DamageRatio), dealt);
            Assert.AreEqual(preview, dealt, "QTE에 띄운 숫자 = 실제 피해");
            Assert.AreEqual(max - dealt, monster.CurrentHp);
        }

        // 선제공격 문제는 이미 본 단어만 (사용자 요청 — 새 단어 카드·도감 발견과 혼선 없게)
        [Test]
        public void PreemptiveQuizUsesOnlyWordsAlreadySeen()
        {
            var region = TestData.SampleWords().Take(5).ToList();
            var other = TestData.SampleWords().Skip(5).ToList();
            var progress = new VocabularyProgress();
            var now = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);

            Assert.IsEmpty(Preemptive.QuizBooks(region, new[] { region, other }, progress), "본 단어가 없으면 버튼 없음");

            progress.Discover(other[0].Id, now);
            CollectionAssert.AreEqual(new[] { other }, Preemptive.QuizBooks(region, new[] { region, other }, progress), "이 지역 단어를 아직 안 봤으면 본 단어가 있는 지역에서");

            progress.Discover(region[1].Id, now);
            CollectionAssert.AreEqual(new[] { region }, Preemptive.QuizBooks(region, new[] { region, other }, progress), "이 지역에서 본 단어가 있으면 이 지역");

            var quiz = new TrainingQuizProvider(Preemptive.QuizBooks(region, new[] { region, other }, progress), progress, new MasteryRules(),
                TrainingMode.All, new System.Random(3), () => now);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(region[1].Id, quiz.NextQuestion(QuizDirection.EnglishToMeaning).Word.Id, "새 단어는 나오지 않음");
        }
    }
}
