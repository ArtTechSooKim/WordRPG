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
            Assert.IsNull(FieldArt.AutoTile(FieldTile.Grass, FieldTheme.Library, 255), "서고 몬스터 칸은 돌 + 위에 안개 한 장 (#103)");
            Assert.IsNotNull(FieldArt.AutoTile(FieldTile.Grass, FieldTheme.Meadow, 0), "초원 풀숲 테두리 따기 (#102)");
            Assert.IsNull(FieldArt.AutoTile(FieldTile.Grass, FieldTheme.Forest, 0), "숲 고사리는 바탕이 잔디와 같아 그대로");
            Assert.AreEqual(0, FieldArt.VariantCount(FieldTile.Floor, FieldTheme.Library));
            Assert.IsNotNull(FieldArt.ForTileVariant(FieldTile.Wall, FieldTheme.Library, 2));
            Assert.IsNotNull(FieldArt.AutoTile(FieldTile.Water, FieldTheme.Library, 0), "잉크 웅덩이 물가 테두리 (#98)");
        }

        // #103 서고 바닥 안개 한 장: 풀숲을 다 덮고(구름 무늬로 짙기만 다름), 풀숲 밖으로 몇 px만 옅어짐, 늘 같은 그림. 서고만
        [Test]
        public void FogCoversGrassAsOneSheetAndFadesJustOutside()
        {
            var map = FieldMap.Parse("######\n#,,,.#\n#.,.P#\n######");
            var light = new Color32(206, 218, 232, 255);
            var shade = new Color32(150, 168, 192, 255);
            var pixels = FogMap.Render(map, light, shade, 0.5f);
            int width = map.Width * 16;
            byte A(int px, int py) => pixels[py * width + px].a;
            Assert.AreEqual(map.Width * 16 * map.Height * 16, pixels.Length);
            // 풀숲 칸(2,2) 안쪽은 거의 다 안개 (디더 빈칸만 조금)
            int covered = 0;
            for (int y = 32; y < 48; y++) for (int x = 32; x < 48; x++) if (A(x, y) > 0) covered++;
            Assert.Greater(covered, 200, "풀숲 칸은 안개로 덮임");
            Assert.LessOrEqual(A(40, 40), (byte)128, "가장 짙어도 50%");
            Assert.AreEqual(0, A(4 * 16 + 12, 2 * 16 + 8), "풀숲에서 6px 넘게 떨어진 길은 맑음");
            Assert.AreEqual(0, A(5 * 16 + 8, 1 * 16 + 8), "멀리 떨어진 칸은 안개 없음");
            Assert.AreEqual(0, A(15, 2 * 16 + 8), "풀숲 바로 옆 벽(책장)에도 안 번짐 — 벽이 또렷하게");
            Assert.Greater(A(4 * 16 + 1, 2 * 16 + 8) + A(4 * 16 + 2, 2 * 16 + 8) + A(4 * 16 + 3, 2 * 16 + 9), 0, "길 쪽으로는 몇 px 번짐");
            CollectionAssert.AreEqual(pixels, FogMap.Render(map, light, shade, 0.5f), "다시 그려도 같은 그림");
            Assert.IsTrue(FieldFog.StyleFor(FieldTheme.Library, out _, out _, out _));
            Assert.IsFalse(FieldFog.StyleFor(FieldTheme.Meadow, out _, out _, out _), "초원엔 안개 없음");
        }

        // #104 안개 무늬가 흘러감: 덮는 자리(가장자리)는 그대로, 짙은 곳·옅은 곳만 바뀜
        [Test]
        public void FogPatternDriftsButItsOutlineStays()
        {
            var map = FieldMap.Parse(MapText(new[] { "########", "#,,,,,.#", "#,,,,,P#", "########" }));
            var light = new Color32(206, 218, 232, 255);
            var shade = new Color32(150, 168, 192, 255);
            var coverage = FogMap.Prepare(map);
            var still = new Color32[coverage.Width * coverage.Height];
            var moved = new Color32[still.Length];
            FogMap.Paint(coverage, still, light, shade, 0.5f, 0f);
            FogMap.Paint(coverage, moved, light, shade, 0.5f, 12f);
            int changed = 0;
            var covered = new System.Collections.Generic.HashSet<int>(coverage.Index);
            for (int i = 0; i < still.Length; i++)
            {
                if (!covered.Contains(i))
                {
                    Assert.AreEqual(0, still[i].a); Assert.AreEqual(0, moved[i].a, "덮는 자리 밖은 흘러가도 맑음 (벽 위로 안 미끄러짐)");
                }
                else if (!still[i].Equals(moved[i])) changed++;
            }
            Assert.Greater(changed, 20, "구름 무늬는 바뀜");
            CollectionAssert.AreEqual(FogMap.Render(map, light, shade, 0.5f), still, "흘러가기 전 = 처음 그림");
        }

        private static string MapText(string[] rows) => string.Join("\n", rows);

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
