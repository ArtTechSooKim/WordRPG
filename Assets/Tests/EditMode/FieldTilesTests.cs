using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 맵 글자 표 (#116): 칸 종류마다 한 줄이 있고, 화면 쪽 표(미니맵 색·임시 그림)도 빠진 칸이 없어야 한다
    public class FieldTilesTests
    {
        private static IEnumerable<FieldTile> AllTiles() => (FieldTile[])Enum.GetValues(typeof(FieldTile));

        [Test]
        public void EveryTileKindHasExactlyOneRow()
        {
            var seen = new HashSet<FieldTile>();
            foreach (var definition in FieldTiles.All)
                Assert.IsTrue(seen.Add(definition.Tile), $"{definition.Tile}이(가) 표에 두 번 있음");
            foreach (var tile in AllTiles())
            {
                Assert.IsTrue(seen.Contains(tile), $"FieldTiles 표에 {tile} 줄이 없음 — 새 칸 종류는 표에 한 줄 더할 것");
                Assert.AreEqual(tile, FieldTiles.Of(tile).Tile);
            }
        }

        [Test]
        public void EverySymbolBelongsToOneTileAndParsesBack()
        {
            var owner = new Dictionary<char, FieldTile>();
            foreach (var definition in FieldTiles.All)
            {
                Assert.IsNotEmpty(definition.Symbols, $"{definition.Tile}: 맵 글자가 없음");
                foreach (char symbol in definition.Symbols)
                {
                    Assert.IsFalse(owner.ContainsKey(symbol), $"글자 '{symbol}'가 {definition.Tile}와 {(owner.ContainsKey(symbol) ? owner[symbol] : definition.Tile)}에 같이 있음");
                    owner[symbol] = definition.Tile;
                }
            }

            foreach (var pair in owner)
            {
                if (pair.Key == FieldTiles.StartSymbol) continue;
                var map = FieldMap.Parse($"{FieldTiles.StartSymbol}{pair.Key}");
                Assert.AreEqual(pair.Value, map.Get(new Vector2Int(1, 0)), $"'{pair.Key}'");
            }
            var start = FieldMap.Parse($"{FieldTiles.StartSymbol}.");
            Assert.AreEqual(FieldTile.Floor, start.Get(start.Start), "시작 칸은 길");
            Assert.Throws<FormatException>(() => FieldMap.Parse("P@"), "표에 없는 글자");
        }

        [Test]
        public void SpecialSymbolsKeepTheirMeaning()
        {
            var map = FieldMap.Parse("PDOC\n><^v");
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 1), new Vector2Int(2, 1) }, map.Doors, "'D' 'O' 둘 다 출입구, 위→아래·왼→오른 순");
            Assert.IsFalse(map.IsCaveMouth(new Vector2Int(1, 1)));
            Assert.IsTrue(map.IsCaveMouth(new Vector2Int(2, 1)), "'O'는 그림만 동굴 입구");
            CollectionAssert.AreEqual(new[] { new Vector2Int(3, 1) }, map.Chests);
            Assert.AreEqual(Direction.Right, map.StreamDirection(new Vector2Int(0, 0)));
            Assert.AreEqual(Direction.Left, map.StreamDirection(new Vector2Int(1, 0)));
            Assert.AreEqual(Direction.Up, map.StreamDirection(new Vector2Int(2, 0)));
            Assert.AreEqual(Direction.Down, map.StreamDirection(new Vector2Int(3, 0)));
            Assert.Throws<FormatException>(() => FieldMap.Parse("PB\nB."), "보스는 하나만");
            Assert.Throws<FormatException>(() => FieldMap.Parse("PP"), "시작은 하나만");
        }

        // 성질끼리 맞지 않는 줄을 막는다 (예: 걸을 수 있는데 [확인] 칸, 이름표가 있는데 글자가 없음)
        [Test]
        public void TraitsAgreeWithEachOther()
        {
            foreach (var d in FieldTiles.All)
            {
                if (d.Interactive) Assert.IsFalse(d.Walkable, $"{d.Tile}: [확인] 칸은 막힌 칸");
                if (d.RoamerWalkable) Assert.IsTrue(d.Walkable, $"{d.Tile}: 몬스터가 걷는 칸은 주인공도 걸음");
                if (d.OpensWhenDone) Assert.IsTrue(d.HasDoneLook, $"{d.Tile}: 끝나면 열리는 칸은 끝난 모습이 있음");
                if (d.IsCamp) Assert.IsTrue(d.Interactive, $"{d.Tile}: 쉼터 물건은 [확인] 칸");
                bool dynamicName = d.Tile == FieldTile.Boss || d.Tile == FieldTile.Door; // 보스 이름 · 도착 지역 이름
                if (d.HasNameTag && !dynamicName) Assert.IsNotEmpty(d.Label, $"{d.Tile}: 이름표 글자가 없음");
                if (!d.HasNameTag) Assert.IsNull(d.Label, $"{d.Tile}: 이름표가 없는데 글자가 있음");
            }
        }

        // 정글·동굴 물건의 이름표 (#80·#112): 표에서 한 단어만 빠져도 이름표가 조용히 사라짐 (#116 버그 심기 검사에서 빠져 있던 것)
        [Test]
        public void JungleAndCavePlacesShowTheirNameTags()
        {
            var area = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "test").Set("map", "PJ.Y.T.A.I");
            var expected = new Dictionary<Vector2Int, string>
            {
                [new Vector2Int(1, 0)] = "나루터",
                [new Vector2Int(3, 0)] = "레버",
                [new Vector2Int(5, 0)] = "단어 석판",
                [new Vector2Int(7, 0)] = "유적 제단",
                [new Vector2Int(9, 0)] = "글자 반딧돌",
            };
            CollectionAssert.AreEquivalent(expected.Keys, FieldInteraction.Landmarks(area.Map), "이름표를 다는 칸");
            foreach (var pair in expected)
                Assert.AreEqual(pair.Value, area.LandmarkName(pair.Key));
            UnityEngine.Object.DestroyImmediate(area);
        }

        [Test]
        public void MinimapHasAColorForEveryTileAThemeUses()
        {
            foreach (FieldTheme theme in Enum.GetValues(typeof(FieldTheme)))
            foreach (var tile in AllTiles())
            {
                if (tile == FieldTile.Floor || !FieldThemes.Supports(theme, tile)) continue; // 길 = 바닥색
                Assert.IsTrue(MinimapArt.HasColor(tile, theme), $"{theme}: {tile}의 미니맵 색이 없음 (MinimapArt 표)");
            }
        }

        // 그림 파일이 없을 때 쓰는 임시 도트: 칸마다 자기 모양이 있어야 함 (빠지면 길 그림으로 보임)
        [Test]
        public void PlaceholderArtDrawsEveryTileKind()
        {
            foreach (FieldTheme theme in Enum.GetValues(typeof(FieldTheme)))
            {
                string floor = PlaceholderArt.ForTile(FieldTile.Floor, theme).texture.name;
                foreach (var tile in AllTiles())
                {
                    if (tile == FieldTile.Floor || !FieldThemes.Supports(theme, tile)) continue;
                    Assert.AreNotEqual(floor, PlaceholderArt.ForTile(tile, theme).texture.name, $"{theme}: {tile}의 임시 그림이 없음 (PlaceholderArt.ForTile)");
                }
            }
        }
    }
}
