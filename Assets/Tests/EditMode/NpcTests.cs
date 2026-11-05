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
}
