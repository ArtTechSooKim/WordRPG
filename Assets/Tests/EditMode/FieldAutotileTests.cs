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

        // 지역 사이 길의 출입구 (#120 사용자: 숲·초원 입구가 옆 칸과 따로 놀았음): 바닥은 옆 길과 이어지고 맵 밖으로도 이어짐, 빛은 바깥 쪽에서
        [Test]
        public void ExitDoorFloorJoinsThePathAndRunsOffTheMap()
        {
            //   y=2  ####
            //   y=1  P..D   ← 오른쪽 끝 출입구: 왼쪽 길 + 맵 밖(오른쪽)과 이어짐, 위·아래 덤불과는 안 이어짐
            //   y=0  ##:#
            var map = FieldMap.Parse("####\nP..D\n##:#");
            var door = new Vector2Int(3, 1);
            Assert.AreEqual(FieldAutotile.E | FieldAutotile.W, FieldAutotile.DoorFloorMask(map, door));
            Assert.AreEqual(Vector2Int.right, FieldAutotile.Outward(map, door), "빛은 맵 밖 쪽에서");

            //   한가운데 출입구(맵 끝이 아님): 걸어 들어오는 쪽의 반대가 바깥
            var inner = FieldMap.Parse("###\n#D#\n#P#");
            Assert.AreEqual(Vector2Int.up, FieldAutotile.Outward(inner, new Vector2Int(1, 1)));
        }

        [Test]
        public void OnlyRoadsBetweenOverlandRegionsGlow()
        {
            Assert.IsTrue(FieldArt.ExitGlow(FieldTheme.Meadow, FieldTheme.Forest).HasValue, "초원 → 숲");
            Assert.IsTrue(FieldArt.ExitGlow(FieldTheme.Forest, FieldTheme.Meadow).HasValue, "숲 → 초원");
            Assert.IsTrue(FieldArt.ExitGlow(FieldTheme.Forest, FieldTheme.Jungle).HasValue, "숲 → 정글");
            Assert.IsTrue(FieldArt.ExitGlow(FieldTheme.Jungle, FieldTheme.Forest).HasValue, "정글 → 숲");
            Assert.IsFalse(FieldArt.ExitGlow(FieldTheme.Meadow, FieldTheme.Library).HasValue, "서고 입구는 원래 그림(동굴 입구)");
            Assert.IsFalse(FieldArt.ExitGlow(FieldTheme.Library, FieldTheme.Meadow).HasValue, "서고 안 출입구도 원래 그림");
            Assert.IsFalse(FieldArt.ExitGlow(FieldTheme.Meadow, null).HasValue);
            Assert.IsNotNull(FieldArt.ExitGlowSprite());
        }

        // 가방 > 성유물 칸 (#120 사용자: 이름이 자물쇠에 가려짐): 늘 두 줄 안에 — 10개까지 5칸씩, 넘으면 한 줄 칸을 늘림
        [Test]
        public void RelicGridAlwaysFitsInTwoRows()
        {
            Assert.AreEqual(5, RelicPage.ColumnsFor(3));
            Assert.AreEqual(5, RelicPage.ColumnsFor(10));
            Assert.AreEqual(6, RelicPage.ColumnsFor(11), "1.1: 성유물 11개");
            Assert.AreEqual(7, RelicPage.ColumnsFor(13), "1.2: 성유물 13개");
            foreach (int count in new[] { 3, 8, 10, 11, 12, 13, 14, 16 })
                Assert.LessOrEqual((count + RelicPage.ColumnsFor(count) - 1) / RelicPage.ColumnsFor(count), 2, $"{count}개");
        }
    }
}
