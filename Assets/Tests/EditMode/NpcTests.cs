using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 말을 잃어 가는 사람들 (#209, GDD 9-10)
    public class NpcTests
    {
        private static NpcData Npc(params NpcStage[] stages)
        {
            var npc = ScriptableObject.CreateInstance<NpcData>();
            npc.Configure("npc_test", "시험", 0, stages);
            return npc;
        }

        private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        [Test]
        public void TheLineComesBackAfterTheBossOrTheWords()
        {
            var boss = ScriptableObject.CreateInstance<FieldArea>();
            var first = new NpcStage("……", NpcMotionKind.Sit);
            var words = new NpcStage("단어", NpcMotionKind.Pace, null, 40);
            var after = new NpcStage("보스", NpcMotionKind.Stand, boss);
            var npc = Npc(first, words, after);

            Assert.AreSame(first, NpcLines.Current(npc, _ => false, 39));
            Assert.AreSame(words, NpcLines.Current(npc, _ => false, 40));
            Assert.AreSame(after, NpcLines.Current(npc, area => area == boss, 0), "채운 것 중 맨 뒤");
        }

        [Test]
        public void TheLineCanUseThePlayersName()
        {
            Assert.AreEqual("수연! 그래, 그 이름이야!", NpcLines.Render("{이름}! 그래, 그 이름이야!", "수연"));
        }

        [TestCase(NpcMotionKind.Pace, NpcMotion.PaceRadius)]
        [TestCase(NpcMotionKind.Stroll, NpcMotion.PaceRadius)]
        [TestCase(NpcMotionKind.Wander, NpcMotion.WanderRadius)]
        public void WalkersStayNearHomeAndOnlyOnCellsTheyMayStandOn(NpcMotionKind kind, int radius)
        {
            var home = new Vector2Int(10, 10);
            var wall = new Vector2Int(11, 10);
            var motion = new NpcMotion(home, kind, new Random(7));
            bool moved = false;
            for (int i = 0; i < 4000; i++)
            {
                motion.Tick(0.1f, cell => cell != wall);
                Assert.AreNotEqual(wall, motion.Cell, "못 서는 칸");
                Assert.LessOrEqual(Distance(home, motion.Cell), radius, "집에서 멀어지지 않음");
                moved |= motion.Cell != home;
            }
            Assert.IsTrue(moved, "실제로 움직임");
        }

        [Test]
        public void StandersSittersAndHoppersStayOnTheirCell()
        {
            foreach (var kind in new[] { NpcMotionKind.Stand, NpcMotionKind.Sit, NpcMotionKind.Busy, NpcMotionKind.Hop })
            {
                var home = new Vector2Int(3, 3);
                var motion = new NpcMotion(home, kind, new Random(3));
                var poses = new System.Collections.Generic.HashSet<NpcPose>();
                float highest = 0f;
                for (int i = 0; i < 2000; i++)
                {
                    motion.Tick(0.05f, _ => true);
                    Assert.AreEqual(home, motion.Cell, kind.ToString());
                    poses.Add(motion.Pose);
                    highest = Mathf.Max(highest, motion.HopHeight);
                }
                if (kind == NpcMotionKind.Sit) CollectionAssert.Contains(poses, NpcPose.Crouch);
                if (kind == NpcMotionKind.Busy) CollectionAssert.IsSupersetOf(poses, new[] { NpcPose.Crouch, NpcPose.Item });
                if (kind == NpcMotionKind.Hop) Assert.Greater(highest, 0.1f, "콩콩 뜀");
            }
        }

        // 성격에 맞는 빠르기 (#211): 같은 왕복이어도 느긋한 인물은 덜 걷고, 쉬는 시간도 길다
        [TestCase(NpcMotionKind.Pace)]
        [TestCase(NpcMotionKind.Stroll)]
        [TestCase(NpcMotionKind.Wander)]
        public void AHigherPaceTakesMoreStepsInTheSameTime(NpcMotionKind kind)
        {
            int Steps(float pace)
            {
                var motion = new NpcMotion(new Vector2Int(10, 10), kind, new Random(11), null, pace);
                for (int i = 0; i < 6000; i++) motion.Tick(0.05f, _ => true);
                return motion.StepsTaken;
            }

            int slow = Steps(0.6f), normal = Steps(1f), fast = Steps(1.6f);
            Assert.Greater(normal, slow, "보통이 느긋한 인물보다 많이 걸음");
            Assert.Greater(fast, normal, "빠른 인물이 보통보다 많이 걸음");
        }

        [Test]
        public void ChangingStagesAlsoChangesThePace()
        {
            var motion = new NpcMotion(new Vector2Int(1, 1), NpcMotionKind.Pace, new Random(1), null, 0.7f);
            Assert.AreEqual(0.7f, motion.Pace);
            motion.SetKind(NpcMotionKind.Pace, 1.4f);
            Assert.AreEqual(1.4f, motion.Pace, "움직임 종류가 같아도 빠르기는 바뀜");
        }

        [Test]
        public void HoldingStopsWalkingAndFacesThePlayer()
        {
            var motion = new NpcMotion(new Vector2Int(5, 5), NpcMotionKind.Wander, new Random(1));
            motion.Hold(Direction.Up);
            var cell = motion.Cell;
            for (int i = 0; i < 200; i++) motion.Tick(0.1f, _ => true);
            Assert.AreEqual(cell, motion.Cell);
            Assert.AreEqual(Direction.Up, motion.Facing);
            motion.Release();
            bool moved = false;
            for (int i = 0; i < 200; i++)
            {
                motion.Tick(0.1f, _ => true);
                moved |= motion.Cell != cell;
            }
            Assert.IsTrue(moved, "놓으면 다시 움직임");
        }

        [Test]
        public void AWalkerOccupiesBothCellsWhileStepping()
        {
            var motion = new NpcMotion(new Vector2Int(5, 5), NpcMotionKind.Wander, new Random(2));
            for (int i = 0; i < 500 && !motion.Walking; i++) motion.Tick(0.05f, _ => true);
            Assert.IsTrue(motion.Walking);
            Assert.AreNotEqual(motion.Previous, motion.Cell);
            Assert.IsTrue(motion.Occupies(motion.Previous));
            Assert.IsTrue(motion.Occupies(motion.Cell));
        }

        [Test]
        public void TheWalkerCannotWalkThroughAnNpc()
        {
            var map = FieldMap.Parse("#####\n#P.N#\n#####");
            var spot = map.NpcSpots.Single();
            Assert.AreEqual(FieldTile.Floor, map.Get(spot), "N은 바닥");
            var walker = new FieldWalker(map, map.Start, occupied: cell => cell == spot);
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Right).Kind);
            Assert.AreEqual(StepKind.BlockedByObject, walker.TryStep(Direction.Right).Kind);
        }

        [Test]
        public void LowercaseNStandsOnLawnAndSpotsAreInReadingOrder()
        {
            var map = FieldMap.Parse("#####\n#n.N#\n#P.n#\n#####");
            Assert.AreEqual(3, map.NpcSpots.Count);
            Assert.AreEqual(FieldTile.Lawn, map.Get(map.NpcSpots[0]));
            Assert.AreEqual(FieldTile.Floor, map.Get(map.NpcSpots[1]));
            Assert.AreEqual(new Vector2Int(3, 1), map.NpcSpots[2]);
        }

        // --- 실제 데이터 ---

        private static FieldArea[] RealAreas() =>
            AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data/Areas" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();

        [Test]
        public void EveryMapSpotHasAnNpcAndTheyCanBeReached()
        {
            int total = 0;
            foreach (var area in RealAreas())
            {
                Assert.AreEqual(area.Map.NpcSpots.Count, area.Npcs.Count, $"{area.name}: N·n 수 = 인물 수");
                var reach = FieldReach.Flood(area.Map);
                foreach (var spot in area.Map.NpcSpots)
                    Assert.IsTrue(reach.Contains(spot), $"{area.name} {spot}: 걸어서 닿음");
                total += area.Npcs.Count;
            }
            Assert.AreEqual(11, total, "초원 3 · 서고 1 · 숲 2 · 정글 4(개구리 셋) · 동굴 1");
        }

        // 걷는 인물들의 빠르기가 성격마다 다름 (#211 사용자)
        [Test]
        public void WalkersHaveDifferentPaces()
        {
            var paces = RealAreas().SelectMany(a => a.Npcs).Distinct().SelectMany(n => n.Stages)
                .Where(s => s.Motion == NpcMotionKind.Pace || s.Motion == NpcMotionKind.Stroll || s.Motion == NpcMotionKind.Wander)
                .Select(s => s.Pace).Distinct().ToList();
            Assert.GreaterOrEqual(paces.Count, 5, string.Join(", ", paces));
            Assert.Less(paces.Min(), 1f);
            Assert.Greater(paces.Max(), 1f);
        }

        [Test]
        public void EveryNpcHasArtAndAFirstLineThatNeedsNothing()
        {
            foreach (var npc in RealAreas().SelectMany(a => a.Npcs).Distinct())
            {
                Assert.IsNotEmpty(npc.NpcId, npc.name);
                Assert.IsNotEmpty(npc.DisplayName, npc.name);
                Assert.IsNotNull(Resources.Load<Texture2D>("Art/NinjaAdventure/Npcs/" + npc.NpcId), $"{npc.NpcId}: 그림 (import_ninja_art.py)");
                Assert.IsNotEmpty(npc.Stages);
                Assert.IsNull(npc.Stages[0].BossOf, $"{npc.NpcId}: 첫 말은 조건 없이");
                Assert.AreEqual(0, npc.Stages[0].WordsNeeded, npc.NpcId);
                foreach (var stage in npc.Stages)
                    Assert.IsNotEmpty(stage.Line, npc.NpcId);
            }
        }
    }
    // #234 사용자 ⑲: 숲 나무꾼 컷씬 — 길 찾기와 실제 대사 데이터
    public class NpcCutsceneTests
    {
        [Test]
        public void PathToGoesAroundWalls()
        {
            //  y=2  #....#
            //  y=1  #.##.#
            //  y=0  ######  (맵 밖은 벽)
            var map = FieldMap.Parse("######\n#P...#\n#.##.#\n######"); // P = 시작 칸 (걸을 수 있음)
            var path = FieldReach.PathTo(map, new Vector2Int(1, 1), new Vector2Int(4, 1), map.IsWalkable);
            Assert.AreEqual(new Vector2Int(4, 1), path.Last(), "목표까지");
            Assert.AreEqual(5, path.Count, "벽을 돌아 위로 (시작 칸은 빼고)");
            Assert.IsFalse(path.Contains(new Vector2Int(2, 1)), "벽은 지나지 않음");
            Assert.IsEmpty(FieldReach.PathTo(map, new Vector2Int(1, 1), new Vector2Int(1, 1), map.IsWalkable), "제자리면 빈 길");
            Assert.IsEmpty(FieldReach.PathTo(map, new Vector2Int(1, 1), new Vector2Int(4, 1), cell => cell == new Vector2Int(1, 1)), "못 가면 빈 길");
        }

        [Test]
        public void TheForestWoodcutterBriefsAndSeesTheHeroOff()
        {
            var forest = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/forest.asset");
            var woodcutter = forest.Npcs.Single(npc => npc.NpcId == "npc_woodcutter");
            Assert.IsTrue(woodcutter.BriefsOnEntry, "숲에 처음 들어오면 알려 줌");
            Assert.IsTrue(woodcutter.SeesOffAfterBoss, "헷갈너구리 뒤 배웅");
            CollectionAssert.AreEqual(new[] { "이정표가 돌아왔군!", "고맙네. 덕분에 이제 길을 헷갈릴 일이 없겠어." }, woodcutter.BossThanksLines, "사용자가 정한 말");
            Assert.AreEqual("행운을 빌겠네!", woodcutter.ExitStoryLines.Last());
            Assert.AreEqual(1, forest.Npcs.Count(npc => npc.BriefsOnEntry), "숲에 알려 주는 인물은 하나");
        }

        // #238 사용자 ⑩: 정글은 순례자가 (사용자가 쓴 7줄 — 빈칸두꺼비 탓에 말에 빈칸)
        [Test]
        public void TheJunglePilgrimBriefsOnEntry()
        {
            var jungle = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/jungle.asset");
            var pilgrim = jungle.Npcs.First(npc => npc.NpcId == "npc_pilgrim");
            Assert.IsTrue(pilgrim.BriefsOnEntry);
            Assert.AreEqual(7, pilgrim.EntryLines.Count);
            Assert.AreEqual("숲을 지나온 건가? 그런 건가? 오랜만에 사람을 만나니 반갑네", pilgrim.EntryLines[0]);
            Assert.IsTrue(pilgrim.EntryLines.All(line => line.Contains("_") || line == pilgrim.EntryLines[0]), "첫 줄 말고는 말에 빈칸");
            Assert.IsFalse(pilgrim.SeesOffAfterBoss, "보스 뒤 배웅은 없음");
            Assert.AreEqual(1, jungle.Npcs.Distinct().Count(npc => npc.BriefsOnEntry), "정글에 알려 주는 인물은 하나 (개구리는 아님)");
        }
    }
}
