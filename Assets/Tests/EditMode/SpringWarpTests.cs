using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.Tests
{
    // 샘으로 다른 지역 가기 (#216): 이미 본 샘만, 정식판 지역은 샀을 때만, 도착 칸은 진행을 건너뛰지 않는 곳
    public class SpringWarpTests
    {
        private static Dictionary<string, FieldArea> RealAreas() =>
            AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data/Areas" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToDictionary(area => area.AreaId);

        private static void See(WorldState world, FieldArea area)
        {
            var map = area.Map;
            world.Reveal(area.AreaId, map.Width, map.Height, SpringWarp.SpringOf(map).Value);
        }

        [Test]
        public void EveryRegionHasOneSpringAndASafePlaceToArrive()
        {
            foreach (var area in RealAreas().Values)
            {
                var map = area.Map;
                var springs = new List<Vector2Int>();
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.Get(new Vector2Int(x, y)) == FieldTile.Fountain) springs.Add(new Vector2Int(x, y));
                Assert.AreEqual(1, springs.Count, $"{area.name}: 샘은 지역마다 하나");
                Assert.AreEqual(springs[0], SpringWarp.SpringOf(map));

                var arrival = SpringWarp.ArrivalCell(map, springs[0]);
                Assert.AreEqual(1, Mathf.Abs(arrival.x - springs[0].x) + Mathf.Abs(arrival.y - springs[0].y), $"{area.name}: 샘 바로 옆");
                Assert.IsTrue(map.IsWalkable(arrival), area.name);
                Assert.AreNotEqual(FieldTile.Door, map.Get(arrival), $"{area.name}: 출입구에 내리지 않음");
                Assert.AreEqual(springs[0], arrival + SpringWarp.FacingSpring(arrival, springs[0]).ToOffset(), $"{area.name}: 샘을 봄");

                // 문·다리를 열기 전에도 시작 칸에서 갈 수 있는 곳 → 이동으로 진행을 건너뛰지 않음
                Assert.IsTrue(FieldReach.Flood(map, gatesOpen: false, bridgesOpen: false).Contains(arrival), $"{area.name}: 문 밖 쉼터의 샘");
            }
        }

        [Test]
        public void RegionsAreListedInStoryOrder()
        {
            var areas = RealAreas();
            CollectionAssert.AllItemsAreUnique(areas.Values.Select(area => area.StoryOrder));
            Assert.IsTrue(areas.Values.All(area => area.StoryOrder > 0), "새 지역은 순서 번호가 필요");

            var world = new WorldState();
            foreach (var area in areas.Values) See(world, area);
            var list = SpringWarp.Destinations(areas.Values.OrderBy(area => area.AreaId), areas["cave"], world, true);
            CollectionAssert.AreEqual(new[] { "meadow", "library", "forest", "jungle" }, list.Select(area => area.AreaId));
        }

        [Test]
        public void OnlySeenSpringsAndOwnedRegionsCanBeReached()
        {
            var areas = RealAreas();
            var all = areas.Values.ToList();
            var world = new WorldState();
            Assert.IsEmpty(SpringWarp.Destinations(all, areas["meadow"], world, true), "처음엔 갈 곳 없음");
            Assert.IsEmpty(SpringWarp.Destinations(null, areas["meadow"], world, true), "데이터가 없어도 안전");

            See(world, areas["meadow"]);
            Assert.IsEmpty(SpringWarp.Destinations(all, areas["meadow"], world, true), "지금 있는 지역은 안 나옴");

            // 지역에 들어갔어도 샘을 아직 못 봤으면 안 나옴
            var library = areas["library"];
            world.Reveal(library.AreaId, library.Map.Width, library.Map.Height, library.Map.Doors[0]);
            bool sawSpringAtDoor = world.IsExplored(library.AreaId, SpringWarp.SpringOf(library.Map).Value);
            Assert.AreEqual(sawSpringAtDoor, SpringWarp.Destinations(all, areas["meadow"], world, true).Contains(library));
            See(world, library);
            CollectionAssert.Contains(SpringWarp.Destinations(all, areas["meadow"], world, true), library);

            // 정식판 지역 (샘을 봤어도 정식판이 없으면 안 나옴 — 환불 등)
            See(world, areas["forest"]);
            Assert.IsTrue(areas["forest"].RequiresFullVersion);
            CollectionAssert.Contains(SpringWarp.Destinations(all, areas["meadow"], world, true), areas["forest"]);
            CollectionAssert.DoesNotContain(SpringWarp.Destinations(all, areas["meadow"], world, false), areas["forest"]);
            CollectionAssert.Contains(SpringWarp.Destinations(all, areas["meadow"], world, false), library, "무료 지역은 그대로");
        }
    }
}
