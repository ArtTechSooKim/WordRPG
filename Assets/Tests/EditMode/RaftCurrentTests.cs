using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Tests
{
    // 뗏목으로 빠른 물살 거스르기 (#235 사용자 ㉛) — 규칙
    public class RaftCurrentTests
    {
        //   y=1  #PJ~~<<<<#
        private static FieldMap Map() => FieldMap.Parse("##########\n#PJ~~<<<<#\n##########");

        [Test]
        public void TheMouthIsThreeCellsDeepAgainstTheFlow()
        {
            var map = Map();
            Assert.AreEqual(0, RaftCurrent.Depth(map, new Vector2Int(4, 1)), "뗏목 물은 어귀가 아님");
            Assert.AreEqual(1, RaftCurrent.Depth(map, new Vector2Int(5, 1)));
            Assert.AreEqual(3, RaftCurrent.Depth(map, new Vector2Int(7, 1)));
            Assert.AreEqual(0, RaftCurrent.Depth(map, new Vector2Int(8, 1)), "4칸째는 너무 멀어서 못 감");
            Assert.IsTrue(RaftCurrent.CanEnter(map, new Vector2Int(4, 1), new Vector2Int(5, 1)));
            Assert.IsFalse(RaftCurrent.CanEnter(map, new Vector2Int(7, 1), new Vector2Int(8, 1)));
            Assert.AreEqual(new Vector2Int(6, 1), RaftCurrent.Downstream(map, new Vector2Int(7, 1)), "물살 쪽으로 밀림");
            Assert.IsNull(RaftCurrent.Downstream(map, new Vector2Int(4, 1)));
        }

        [Test]
        public void TheWalkerRidesInAndDriftsBack()
        {
            var walker = new FieldWalker(Map(), new Vector2Int(4, 1), rafting: true);
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Right).Kind, "뗏목으로 어귀에");
            Assert.IsTrue(walker.Rafting);
            walker.TryStep(Direction.Right);
            walker.TryStep(Direction.Right);
            Assert.AreEqual(new Vector2Int(7, 1), walker.Position);
            Assert.AreEqual(StepKind.Blocked, walker.TryStep(Direction.Right).Kind, "4칸째는 막힘");
            Assert.IsTrue(walker.Drift());
            Assert.AreEqual(new Vector2Int(6, 1), walker.Position);
            Assert.AreEqual(Direction.Right, walker.Facing, "밀려도 바라보는 쪽은 그대로");
            walker.Drift();
            walker.Drift();
            Assert.AreEqual(new Vector2Int(4, 1), walker.Position);
            Assert.IsFalse(walker.Drift(), "뗏목 물에선 안 밀림");
        }

        // 실제 정글: 가운데를 가로지르는 '>' 강이 큰 강으로 흘러드는 어귀 (세 줄 × 3칸)
        [Test]
        public void TheJungleHasOneMouthIntoTheBigRiver()
        {
            var jungle = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/jungle.asset");
            var map = jungle.Map;
            var cells = new List<Vector2Int>();
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                if (RaftCurrent.Depth(map, new Vector2Int(x, y)) > 0) cells.Add(new Vector2Int(x, y));
            Assert.AreEqual(9, cells.Count, "물살 세 줄 × 3칸");
            foreach (var cell in cells) Assert.AreEqual(Direction.Right, map.StreamDirection(cell));
        }
    }
}
