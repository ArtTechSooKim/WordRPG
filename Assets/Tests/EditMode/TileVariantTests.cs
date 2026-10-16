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
            Assert.AreEqual(2, FieldArt.VariantCount(FieldTile.Grass, FieldTheme.Library), "바랜 종이 3가지");
            Assert.AreEqual(0, FieldArt.VariantCount(FieldTile.Floor, FieldTheme.Library));
            Assert.IsNotNull(FieldArt.ForTileVariant(FieldTile.Wall, FieldTheme.Library, 2));
            Assert.IsNotNull(FieldArt.AutoTile(FieldTile.Water, FieldTheme.Library, 0), "잉크 웅덩이 물가 테두리 (#98)");
        }

        // #99: 초원·숲 나무·풀숲 3가지씩 + 잔디 흩뿌림
        [Test]
        public void MeadowAndForestMixTreesAndScatterOnLawn()
        {
            foreach (var theme in new[] { FieldTheme.Meadow, FieldTheme.Forest })
            {
                Assert.AreEqual(2, FieldArt.VariantCount(FieldTile.Wall, theme), $"{theme} 나무 3가지");
                Assert.AreEqual(2, FieldArt.VariantCount(FieldTile.Grass, theme), $"{theme} 풀숲 3가지");
                Assert.GreaterOrEqual(FieldArt.Scatter(theme, FieldTile.Lawn).Length, 5, $"{theme} 잔디 흩뿌림");
            }
        }
    }
}
