using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 미니맵은 맵 글자에서 자동으로 그린다 — 새 지역도 따로 손댈 필요 없음
    public class MinimapTests
    {
        //   y=2  #C~#
        //   y=1  #,P#
        //   y=0  ####
        private const string Map = "####\n#C~#\n#,P#\n####";

        private static FieldArea Area() =>
            ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "test").Set("map", Map);

        [Test]
        public void TextureHasOnePixelPerCellWithLandmarkColors()
        {
            var area = Area();
            var texture = MinimapArt.Build(area.Map, FieldTheme.Meadow, _ => false);
            Assert.AreEqual(area.Map.Width, texture.width);
            Assert.AreEqual(area.Map.Height, texture.height);
            Assert.AreEqual(FilterMode.Point, texture.filterMode, "확대해도 또렷하게");

            var chest = area.Map.Chests[0];
            Assert.AreEqual((Color)MinimapArt.Chest, (Color)texture.GetPixel(chest.x, chest.y), "상자는 금색 점");
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void OpenedChestTurnsIntoFloorColor()
        {
            var floor = MinimapArt.ColorOf(FieldTile.Floor, FieldTheme.Meadow, false);
            Assert.AreEqual(floor, MinimapArt.ColorOf(FieldTile.Chest, FieldTheme.Meadow, true));
            Assert.AreNotEqual(floor, MinimapArt.ColorOf(FieldTile.Chest, FieldTheme.Meadow, false));
        }

        [Test]
        public void LandmarksStandOutFromTerrainInEveryTheme()
        {
            foreach (FieldTheme theme in System.Enum.GetValues(typeof(FieldTheme)))
            foreach (var landmark in new[] { FieldTile.Chest, FieldTile.Fountain, FieldTile.Altar, FieldTile.Shop, FieldTile.Door, FieldTile.Boss, FieldTile.Lectern })
            foreach (var terrain in new[] { FieldTile.Floor, FieldTile.Lawn, FieldTile.Grass, FieldTile.Wall, FieldTile.Water })
                Assert.AreNotEqual(MinimapArt.ColorOf(terrain, theme, false), MinimapArt.ColorOf(landmark, theme, false),
                    $"{theme}: {landmark}와 {terrain} 색이 같음");
        }

        [Test]
        public void RemainingChestsCountsUnopenedChestTiles()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(10, 5, 5)), 1);
            Assert.AreEqual(1, session.RemainingChests(area));
            session.OpenChest(area, area.Map.Chests[0]);
            Assert.AreEqual(0, session.RemainingChests(area));
        }
    }
}
