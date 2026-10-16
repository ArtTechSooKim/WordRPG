using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 길·물가 자동 테두리: 맵 글자의 이웃 모양 → 마스크 → 그림 조각
    public class FieldAutotileTests
    {
        //   y=4  #######
        //   y=3  #::.::#
        //   y=2  #.....#   ← 가운데 (3,2)는 위·아래·왼·오른 모두 길
        //   y=1  #::.::#
        //   y=0  ###D###
        private static readonly FieldMap Cross = FieldMap.Parse("#######\n#::.::#\n#..P..#\n#::.::#\n###D###");

        [Test]
        public void CrossroadsConnectFourWaysWithoutCorners()
        {
            int center = FieldAutotile.Mask(Cross, new Vector2Int(3, 2));
            Assert.AreEqual(FieldAutotile.N | FieldAutotile.E | FieldAutotile.S | FieldAutotile.W, center,
                "대각선 칸은 잔디라 모서리는 안 셈 (안쪽 모서리 조각)");
            Assert.AreEqual(FieldAutotile.E, FieldAutotile.Mask(Cross, new Vector2Int(1, 2)), "왼쪽 끝");
            Assert.AreEqual(FieldAutotile.S, FieldAutotile.Mask(Cross, new Vector2Int(3, 3)), "위쪽 끝 (맵 밖·벽으로는 안 이어짐)");
            Assert.AreEqual(FieldAutotile.N | FieldAutotile.S, FieldAutotile.Mask(Cross, new Vector2Int(3, 1)), "아래 출입구까지 이어짐");
        }

        [Test]
        public void CornersCountOnlyWhenBothSidesConnect()
        {
            var block = FieldMap.Parse("#####\n#...#\n#.P.#\n#...#\n#####");
            Assert.AreEqual(255, FieldAutotile.Mask(block, new Vector2Int(2, 2)), "둘레 8칸이 모두 길 = 가운데 조각");
            Assert.AreEqual(FieldAutotile.E | FieldAutotile.S | FieldAutotile.SE, FieldAutotile.Mask(block, new Vector2Int(1, 3)),
                "왼쪽 위 모퉁이");
            Assert.AreEqual(FieldAutotile.N | FieldAutotile.E, FieldAutotile.Normalize(FieldAutotile.N | FieldAutotile.E | FieldAutotile.SE),
                "한쪽 변만 이어진 모서리는 지움");
        }

        [Test]
        public void WaterRunsOffTheMapEdgeButPathsDoNot()
        {
            var river = FieldMap.Parse("~~~\n:P:");
            int left = FieldAutotile.Mask(river, new Vector2Int(0, 1));
            Assert.IsTrue((left & FieldAutotile.W) != 0, "맵 밖으로 물이 이어짐");
            Assert.IsTrue((left & FieldAutotile.N) != 0);
            Assert.IsTrue((left & FieldAutotile.S) == 0, "아래는 잔디");
            Assert.AreEqual(0, FieldAutotile.Mask(FieldMap.Parse("P"), Vector2Int.zero), "길은 맵 밖과 안 이어짐");
            Assert.IsFalse(FieldAutotile.IsAutotiled(FieldTile.Lawn));
            Assert.IsTrue(FieldAutotile.IsAutotiled(FieldTile.Wall), "덤불 벽도 이웃 모양으로 (#100)");
            Assert.IsTrue(FieldAutotile.Connects(FieldTile.Wall, FieldTile.Wall));
            Assert.IsFalse(FieldAutotile.Connects(FieldTile.Wall, FieldTile.Lawn), "잔디 쪽은 둥근 풀 테두리");
            Assert.IsTrue(FieldAutotile.Connects(FieldTile.Grass, FieldTile.Grass), "풀숲끼리만 이어짐 (#102)");
            foreach (var other in new[] { FieldTile.Lawn, FieldTile.Wall, FieldTile.Water, FieldTile.Floor, FieldTile.Chest })
                Assert.IsFalse(FieldAutotile.Connects(FieldTile.Grass, other), $"{other} 쪽은 풀잎 모양 테두리 — 짙은 네모 바탕이 안 보이게");
            Assert.IsFalse(FieldAutotile.Connects(FieldTile.Floor, FieldTile.Chest), "물건 칸은 잔디 위 — 길 테두리가 감쌈");
        }

        [Test]
        public void MeadowAndForestHavePathAndShoreArt()
        {
            foreach (var theme in new[] { FieldTheme.Meadow, FieldTheme.Forest })
            foreach (var tile in new[] { FieldTile.Floor, FieldTile.Water })
            {
                var texture = Resources.Load<Texture2D>($"Art/NinjaAdventure/Tiles/{theme}_{tile}_auto");
                Assert.IsNotNull(texture, $"{theme}_{tile}_auto 없음 (python Tools/import_ninja_art.py)");
                Assert.AreEqual(256, texture.width);
                Assert.AreEqual(256, texture.height);
                Assert.AreEqual(FilterMode.Point, texture.filterMode);
                var single = FieldArt.AutoTile(tile, theme, 0);
                var middle = FieldArt.AutoTile(tile, theme, 255);
                Assert.IsNotNull(single);
                Assert.AreNotSame(single, middle);
                Assert.AreEqual(16f, middle.rect.width);
            }
            Assert.IsNull(FieldArt.AutoTile(FieldTile.Floor, FieldTheme.Library, 0), "서고는 돌바닥 한 칸 그림");
        }
    }
}
