using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 칸 그림 변형 (#98): 같은 종류 칸이 한 그림만 되풀이되지 않게 칸 위치로 0(기본) ~ n 중 하나
    public class TileVariantTests
    {
        [Test]
        public void VariantDependsOnlyOnTheCellAndUsesEveryPicture()
        {
            var seen = new HashSet<int>();
            for (int x = 0; x < 30; x++)
            for (int y = 0; y < 30; y++)
            {
                var cell = new Vector2Int(x, y);
                int v = FieldDecor.TileVariant(cell, 2);
                Assert.AreEqual(v, FieldDecor.TileVariant(cell, 2), "같은 칸은 늘 같은 그림 (다시 그려도 안 바뀜)");
                Assert.That(v, Is.InRange(0, 2));
                seen.Add(v);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, seen, "기본 그림과 변형 둘 다 쓰임");
            Assert.AreEqual(0, FieldDecor.TileVariant(new Vector2Int(3, 4), 0), "변형이 없으면 기본 그림");
        }

        [Test]
        public void LibraryHasShelfAndPaperVariantsButOtherKindsStayPlain()
        {
            Assert.AreEqual(2, FieldArt.VariantCount(FieldTile.Wall, FieldTheme.Library), "책장 3가지 = 기본 + _v1·_v2");
            Assert.AreEqual(0, FieldArt.VariantCount(FieldTile.Grass, FieldTheme.Library), "몬스터 칸은 바닥 안개 (#101)");
            Assert.IsNotNull(FieldArt.AutoTile(FieldTile.Grass, FieldTheme.Library, 255), "안개 자리 (아주 옅게)");
            Assert.IsNotNull(FieldArt.AutoTile(FieldTile.Grass, FieldTheme.Meadow, 0), "초원 풀숲 테두리 따기 (#102)");
            Assert.IsNull(FieldArt.AutoTile(FieldTile.Grass, FieldTheme.Forest, 0), "숲 고사리는 바탕이 잔디와 같아 그대로");
            Assert.AreEqual(0, FieldArt.VariantCount(FieldTile.Floor, FieldTheme.Library));
            Assert.IsNotNull(FieldArt.ForTileVariant(FieldTile.Wall, FieldTheme.Library, 2));
            Assert.IsNotNull(FieldArt.AutoTile(FieldTile.Water, FieldTheme.Library, 0), "잉크 웅덩이 물가 테두리 (#98)");
        }

        // #102 서고 바닥 안개 띠: 풀숲 칸마다 한 장, 양옆도 풀숲이면 넓은 띠, 자리는 늘 같음. 그림은 서고에만
        [Test]
        public void FogPuffsSitOnGrassAndOnlyTheLibraryHasFogArt()
        {
            var map = FieldMap.Parse("#####\n#,,,#\n#.,P#\n#####");
            var puffs = FieldDecor.FogPuffs(map);
            Assert.AreEqual(4, puffs.Count, "풀숲 4칸");
            foreach (var p in puffs)
            {
                Assert.AreEqual(FieldTile.Grass, map.Get(p.Cell));
                Assert.That(p.Offset.x, Is.InRange(-4, 4));
                Assert.That(p.Offset.y, Is.InRange(-2, 2));
            }
            Assert.IsTrue(puffs.Find(p => p.Cell == new Vector2Int(2, 2)).Wide, "가운데 칸은 양옆이 풀숲 → 넓은 띠");
            Assert.IsFalse(puffs.Find(p => p.Cell == new Vector2Int(1, 2)).Wide, "책장 옆은 작은 것");
            CollectionAssert.AreEqual(puffs.ConvertAll(p => p.Variant), FieldDecor.FogPuffs(map).ConvertAll(p => p.Variant), "다시 그려도 같은 자리");

            Assert.AreEqual(4, FieldArt.Fog(FieldTheme.Library, true).Length);
            Assert.AreEqual(3, FieldArt.Fog(FieldTheme.Library, false).Length);
            Assert.AreEqual(16f, FieldArt.Fog(FieldTheme.Library, true)[0].pixelsPerUnit, "칸보다 큰 그림 — 16px = 1칸");
            Assert.AreEqual(0, FieldArt.Fog(FieldTheme.Meadow, true).Length, "초원엔 안개 없음");
        }

        // #99: 숲은 나무·풀숲 3가지씩, 초원·숲 잔디 흩뿌림. #100: 초원은 섞지 않고 덤불 벽 자동 테두리 (벽·땅이 헷갈리지 않게)
        [Test]
        public void ForestMixesTreesMeadowKeepsOneBushWithShadedEdges()
        {
            Assert.AreEqual(2, FieldArt.VariantCount(FieldTile.Wall, FieldTheme.Forest), "숲 나무 3가지");
            Assert.AreEqual(2, FieldArt.VariantCount(FieldTile.Grass, FieldTheme.Forest), "숲 풀숲 3가지");
            Assert.AreEqual(0, FieldArt.VariantCount(FieldTile.Wall, FieldTheme.Meadow), "초원 덤불은 한 가지");
            Assert.AreEqual(0, FieldArt.VariantCount(FieldTile.Grass, FieldTheme.Meadow));
            Assert.IsNotNull(FieldArt.AutoTile(FieldTile.Wall, FieldTheme.Meadow, 0), "초원 덤불 그늘 테두리 (#100)");
            Assert.IsNull(FieldArt.AutoTile(FieldTile.Wall, FieldTheme.Forest, 0), "숲은 그대로 (사용자 OK)");
            foreach (var theme in new[] { FieldTheme.Meadow, FieldTheme.Forest })
                Assert.GreaterOrEqual(FieldArt.Scatter(theme, FieldTile.Lawn).Length, 5, $"{theme} 잔디 흩뿌림");
        }
    }
}
