using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Tests
{
    // 크게 선 보스의 몸 (#267 사용자: 빈칸두꺼비 그림은 6칸인데 [확인]은 B 한 칸이라 어떻게 도전하는지 묻게 됨)
    public class BossBodyTests
    {
        //   y=7  #########
        //   y=6  #.......#
        //   y=5  #.......#
        //   y=4  #.......#
        //   y=3  #.......#
        //   y=2  #...B...#   ← 보스 (4,2)
        //   y=1  #P......#
        //   y=0  #########
        private static readonly FieldMap Lair = FieldMap.Parse("#########\n#.......#\n#.......#\n#.......#\n#.......#\n#...B...#\n#P......#\n#########");
        private static readonly Vector2Int B = new Vector2Int(4, 2);

        [Test]
        public void WholePictureIsTheBodyWhenItHasNoClearEdges()
        {
            // 3×3 그림 = B 칸 가운데, B 칸 아래 변부터 위로 → 가로 3..5, 세로 2..4
            var body = BossBody.Of(Lair, B, new Vector2(3, 3));
            Assert.AreEqual(9, body.Cells.Count);
            for (int x = 3; x <= 5; x++)
            for (int y = 2; y <= 4; y++)
                Assert.IsTrue(body.Contains(new Vector2Int(x, y)), $"({x},{y})");
            Assert.IsFalse(body.Contains(new Vector2Int(2, 2)));
            Assert.IsFalse(body.Contains(new Vector2Int(4, 5)));
        }

        [Test]
        public void ClearEdgesOfThePictureAreNotBody()
        {
            // 빈칸두꺼비처럼: 6×6 그림, 보이는 부분 가로 0.1~0.9 · 세로 0.175~0.825 (발이 B 칸보다 한 칸쯤 위)
            var body = BossBody.Of(Lair, B, new Vector2(6, 6), Rect.MinMaxRect(0.1f, 0.175f, 0.9f, 0.825f));
            // 가로 = B.x+0.5 ± 2.4 → 2..6 칸, 세로 = 1.05 ~ 4.95 위 → 3..6 줄 (맵 안쪽 6줄까지)
            for (int x = 2; x <= 6; x++)
            for (int y = 3; y <= 6; y++)
                Assert.IsTrue(body.Contains(new Vector2Int(x, y)), $"({x},{y})");
            Assert.IsTrue(body.Contains(B), "B 칸은 늘 몸");
            Assert.IsFalse(body.Contains(new Vector2Int(3, 2)), "발밑 옆 빈 곳은 걸을 수 있음");
            Assert.AreEqual(5 * 4 + 1, body.Cells.Count);
        }

        [Test]
        public void WallsAndDoorsAreNeverBody()
        {
            // 그림이 벽과 출입구에 걸쳐도 (보스 방 문 밖에서 벽 너머로 [확인]하지 않게)
            var map = FieldMap.Parse("##D##\n#P..#\n#.B.#\n#####");
            var body = BossBody.Of(map, new Vector2Int(2, 1), new Vector2(5, 3));
            Assert.IsFalse(body.Contains(new Vector2Int(2, 3)), "출입구");
            Assert.IsFalse(body.Contains(new Vector2Int(0, 1)), "벽");
            Assert.IsTrue(body.Contains(new Vector2Int(1, 2)) && body.Contains(new Vector2Int(3, 1)));
            Assert.IsTrue(body.Cells.All(c => map.IsWalkable(c) || c == new Vector2Int(2, 1)));
        }

        [Test]
        public void BodyBlocksStepsFromOutsideButLetsAnOldSaveWalkOut()
        {
            var body = BossBody.Of(Lair, B, new Vector2(3, 3));
            Assert.IsTrue(body.Blocks(new Vector2Int(2, 3), new Vector2Int(3, 3)), "밖에서 몸으로");
            Assert.IsFalse(body.Blocks(new Vector2Int(2, 3), new Vector2Int(2, 4)), "몸이 아닌 칸");
            // 1.3까지는 그림 위를 걸을 수 있었음 → 몸 안에 서 있는 세이브는 나올 수 있게
            Assert.IsFalse(body.Blocks(new Vector2Int(4, 3), new Vector2Int(4, 4)));
            Assert.IsFalse(body.Blocks(new Vector2Int(5, 3), new Vector2Int(6, 3)));

            var walker = new FieldWalker(Lair, new Vector2Int(2, 3), occupied: cell => body.Blocks(new Vector2Int(2, 3), cell));
            Assert.AreEqual(StepKind.BlockedByObject, walker.TryStep(Direction.Right).Kind, "부딪히면 [확인]을 알려 주는 막힘");
            Assert.AreEqual(new Vector2Int(2, 3), walker.Position);
        }

        // 실제 지역: 크게 선 보스마다 몸이 여러 칸이고, 보스 방 문을 열기 전에는 몸 어느 칸에도 닿지 않음 (문 밖에서 [확인]으로 싸우지 않게)
        [Test]
        public void RealBigBossesHaveABodyOnlyReachableThroughTheirGate()
        {
            int checkedBosses = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" }))
            {
                var area = AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(guid));
                if (area.Boss == null || !area.Boss.StandsBig || !area.Map.BossPosition.HasValue) continue;
                checkedBosses++;
                var b = area.Map.BossPosition.Value;
                var body = BossBody.Of(area.Map, b, area.Boss.FieldSize, BossBody.VisiblePart(area.Boss.Species.Sprite));
                Assert.Greater(body.Cells.Count, 4, $"{area.AreaId}: 그림만큼 몸");
                var outside = FieldReach.Flood(area.Map, gatesOpen: false);
                foreach (var cell in body.Cells)
                    Assert.IsFalse(outside.Contains(cell) || FieldReach.Touches(outside, cell), $"{area.AreaId}: 몸 {cell}에 문 밖에서 닿음");
                if (area.Boss.Species.SpeciesId == "boss_blank_frog")
                {
                    // 빈칸두꺼비는 발이 B 칸보다 한 칸쯤 위 → B 바로 위는 몸, B 옆은 걸을 수 있는 빈 곳
                    Assert.IsTrue(body.Contains(b + Vector2Int.up));
                    Assert.IsFalse(body.Contains(b + Vector2Int.left) || body.Contains(b + Vector2Int.right));
                }
            }
            Assert.GreaterOrEqual(checkedBosses, 4, "서고·숲·정글·동굴");
        }

        [Test]
        public void ConfirmNextToAnyPartOfTheBodyMeansTheBoss()
        {
            var body = BossBody.Of(Lair, B, new Vector2(3, 3));
            // 옆구리 (2,3) → 오른쪽 (3,3)은 몸 (B 칸이 아님)
            var direction = FieldInteraction.FindTarget(Lair, new Vector2Int(2, 3), Direction.Right, null, body);
            Assert.AreEqual(Direction.Right, direction);
            Assert.AreEqual(B, FieldInteraction.TargetCell(new Vector2Int(2, 3), direction.Value, body), "쓰는 칸은 B");
            // 머리 위 (4,5) → 아래 (4,4)
            Assert.AreEqual(Direction.Down, FieldInteraction.FindTarget(Lair, new Vector2Int(4, 5), Direction.Left, null, body));
            // 몸이 없으면 (예전처럼) 옆구리에서는 쓸 것 없음
            Assert.IsNull(FieldInteraction.FindTarget(Lair, new Vector2Int(2, 3), Direction.Right));
            Assert.IsNull(FieldInteraction.FindTarget(Lair, new Vector2Int(2, 3), Direction.Right, null, BossBody.None));
            Assert.AreEqual(new Vector2Int(3, 3), FieldInteraction.TargetCell(new Vector2Int(2, 3), Direction.Right, BossBody.None));
        }
    }
}
