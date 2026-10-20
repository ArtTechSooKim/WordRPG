using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Tests
{
    // 메아리 정글 나루터 (#121 사용자: 섬 나루터의 뗏목이 뭍과 섬을 잇는 다리처럼 보여 지나가려다 막힘 →
    // "나루터인 것을 알 수 있도록 타일을 더 쓰자, 강을 양옆으로 늘리자"): 2×2 나무 부두 · 넓어진 강 · 묶인 뗏목은 부두 앞 넓은 물
    public class JungleDockTests
    {
        private static FieldArea Jungle => AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/jungle.asset");

        [Test]
        public void EveryDockIsATwoByTwoPier()
        {
            var map = Jungle.Map;
            var groups = map.FindAll(FieldTile.Dock).Select(c => map.DockGroup(c)[0]).Distinct().ToList();
            Assert.AreEqual(4, groups.Count, "나루터 4곳 (남쪽 · 북쪽 · 섬 양쪽)");
            foreach (var anchor in groups)
            {
                var group = map.DockGroup(anchor);
                Assert.AreEqual(4, group.Count, $"{anchor} 부두는 2×2");
                int w = group.Max(c => c.x) - group.Min(c => c.x) + 1, h = group.Max(c => c.y) - group.Min(c => c.y) + 1;
                Assert.AreEqual((2, 2), (w, h), $"{anchor} 부두 모양");
                Assert.IsTrue(group.Any(c => new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down }
                    .Any(d => map.IsRaftWater(c + d))), $"{anchor} 부두에서 뗏목을 탈 수 있음");
            }
        }

        // 묶인 뗏목(그림 2¼칸)은 부두마다 하나, 앞이 트인 물에 — 맞은편 뭍·섬과 1칸 넘게 떨어져 다리처럼 보이지 않게
        [Test]
        public void MooredRaftsSitInOpenWaterAwayFromTheOtherBank()
        {
            var map = Jungle.Map;
            var spots = map.MooringSpots();
            Assert.AreEqual(4, spots.Count, "부두마다 뗏목 하나");
            foreach (var (water, step) in spots)
            {
                Assert.IsTrue(map.IsRaftWater(water));
                for (int i = 1; i <= 2; i++)
                    Assert.IsTrue(map.IsRaftWater(water + step * i), $"{water} 뗏목 앞 {i}칸은 물 (맞은편 땅에 닿지 않음)");
                // 부두 끝(강 쪽)에 묶임 — 강가에 붙어 부두 위에 얹혀 보이지 않게 (#121 캡처: 처음엔 부두 위쪽 강가 물에 묶였음)
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var next = water + d;
                    Assert.IsFalse(map.Get(next) != FieldTile.Dock && map.IsWalkable(next), $"{water} 뗏목이 땅 {next}에 붙어 있음");
                }
            }
            Assert.IsTrue(spots.Any(s => s.water.x == 51 && (s.water.y == 17 || s.water.y == 18) && s.step == Vector2Int.right),
                "남쪽 부두(49~50, 17~18): 강 쪽(동쪽) 끝에 가로로");
        }

        // 강은 맵 위아래로 이어진 넓은 물: 섬 양쪽 물길이 4칸 이상 (부두 앞 물)
        [Test]
        public void IslandChannelsAreWide()
        {
            var map = Jungle.Map;
            var west = map.DockGroup(new Vector2Int(54, 43));
            var east = map.DockGroup(new Vector2Int(62, 43));
            Assert.AreEqual(4, west.Count, "섬 서쪽 부두 (54~55, 43~44)");
            Assert.AreEqual(4, east.Count, "섬 동쪽 부두 (61~62, 43~44)");
            int Gap(Vector2Int from, Vector2Int dir)
            {
                int n = 0;
                while (map.Get(from + dir * (n + 1)) == FieldTile.Water) n++;
                return n;
            }
            Assert.GreaterOrEqual(Gap(new Vector2Int(54, 43), Vector2Int.left), 4, "섬 서쪽 부두와 서쪽 강가 사이 물");
            Assert.GreaterOrEqual(Gap(new Vector2Int(62, 43), Vector2Int.right), 4, "섬 동쪽 부두와 동쪽 강가 사이 물");
        }

        [Test]
        public void EachPierShowsOneNameTag()
        {
            var jungle = Jungle;
            int tags = jungle.Map.FindAll(FieldTile.Dock).Count(c => jungle.LandmarkName(c) == "나루터");
            Assert.AreEqual(4, tags, "2×2 부두마다 '나루터' 이름표 하나");
        }

        [Test]
        public void PierEdgesFaceOnlyTheWater()
        {
            //   y=2  ::~~
            //   y=1  :JJ~   ← 나루터는 나루터·뭍과 이어지고, 물·얕은 강 쪽만 부두 끝(말뚝)
            //   y=0  :JJ>
            var map = FieldMap.Parse("::~~\nPJJ~\n:JJ>");
            Assert.IsTrue(FieldAutotile.IsAutotiled(FieldTile.Dock));
            int inner = FieldAutotile.Mask(map, new Vector2Int(1, 1));
            Assert.AreEqual(FieldAutotile.N | FieldAutotile.E | FieldAutotile.S | FieldAutotile.W, inner & (FieldAutotile.N | FieldAutotile.E | FieldAutotile.S | FieldAutotile.W),
                "뭍 쪽 칸: 위(잔디)·왼(시작 길)·오른·아래(나루터) 모두 이어짐");
            int outer = FieldAutotile.Mask(map, new Vector2Int(2, 0));
            Assert.AreEqual(0, outer & FieldAutotile.E, "얕은 강 쪽은 부두 끝");
            Assert.AreEqual(0, outer & FieldAutotile.S, "맵 밖도 부두 끝");
            Assert.AreNotEqual(0, outer & FieldAutotile.W, "옆 나루터와 이어짐");
            Assert.IsTrue(FieldAutotile.Connects(FieldTile.Water, FieldTile.Dock), "물은 부두 밑으로 이어짐 (부두를 감싸는 물가 풀 테두리 없음)");
        }
    }
}
