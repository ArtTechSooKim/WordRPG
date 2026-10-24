using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Save;

namespace WordRPG.Tests
{
    public class FieldMapTests
    {
        //   y=3  #####
        //   y=2  #C,~#
        //   y=1  #.PF#
        //   y=0  ##C##
        private const string Map = "#####\n#C,~#\n#.PF#\n##C##";

        [Test]
        public void ParsesWithYUpAndStart()
        {
            var map = FieldMap.Parse(Map);

            Assert.AreEqual(5, map.Width);
            Assert.AreEqual(4, map.Height);
            Assert.AreEqual(new Vector2Int(2, 1), map.Start);
            Assert.AreEqual(FieldTile.Floor, map.Get(new Vector2Int(2, 1)), "시작 칸은 길");
            Assert.AreEqual(FieldTile.Grass, map.Get(new Vector2Int(2, 2)));
            Assert.AreEqual(FieldTile.Water, map.Get(new Vector2Int(3, 2)));
            Assert.AreEqual(FieldTile.Fountain, map.Get(new Vector2Int(3, 1)));
            Assert.AreEqual(FieldTile.Wall, map.Get(new Vector2Int(-1, 0)), "맵 밖은 벽");
        }

        [Test]
        public void ChestsAreInReadingOrder()
        {
            var map = FieldMap.Parse(Map);

            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 2), new Vector2Int(2, 0) }, map.Chests);
            Assert.AreEqual(1, map.ChestIndex(new Vector2Int(2, 0)));
            Assert.AreEqual(-1, map.ChestIndex(new Vector2Int(2, 1)));
        }

        [Test]
        public void IgnoresBlankLinesAndWindowsNewlines()
        {
            var map = FieldMap.Parse("\r\n#.P#\r\n\r\n#,,#\r\n");
            Assert.AreEqual(2, map.Height);
            Assert.AreEqual(new Vector2Int(2, 1), map.Start);
        }

        [TestCase("", "비어")]
        [TestCase("#.#\n#..#", "길이")]
        [TestCase("#.#", "'P'가 없")]
        [TestCase("#PP#", "두 개")]
        [TestCase("#P?#", "알 수 없는")]
        public void BadMapsExplainWhatIsWrong(string text, string expected)
        {
            var e = Assert.Throws<FormatException>(() => FieldMap.Parse(text));
            StringAssert.Contains(expected, e.Message);
        }
    }

    public class FieldWalkerTests
    {
        private FieldWalker walker;

        [SetUp]
        public void SetUp() => walker = new FieldWalker(FieldMap.Parse("#####\n#C,~#\n#.PF#\n##C##"), new Vector2Int(2, 1));

        [Test]
        public void MovesOntoFloorAndGrass()
        {
            var left = walker.TryStep(Direction.Left);
            Assert.AreEqual(StepKind.Moved, left.Kind);
            Assert.IsFalse(left.EnteredGrass);
            Assert.AreEqual(new Vector2Int(1, 1), walker.Position);

            walker.WarpTo(new Vector2Int(2, 1));
            var up = walker.TryStep(Direction.Up);
            Assert.IsTrue(up.EnteredGrass);
            Assert.AreEqual(new Vector2Int(2, 2), walker.Position);
        }

        [Test]
        public void BlockedTurnsButDoesNotMove()
        {
            walker.TryStep(Direction.Up); // (2,2) 풀숲
            var outcome = walker.TryStep(Direction.Right); // 물

            Assert.AreEqual(StepKind.Blocked, outcome.Kind);
            Assert.AreEqual(Direction.Right, walker.Facing);
            Assert.AreEqual(new Vector2Int(2, 2), walker.Position);
            Assert.AreEqual(StepKind.Blocked, walker.TryStep(Direction.Up).Kind, "나무");
        }

        [Test]
        public void BumpingChestAndFountain()
        {
            var chest = walker.TryStep(Direction.Down);
            var fountain = walker.TryStep(Direction.Right);

            Assert.AreEqual(StepKind.BlockedByObject, chest.Kind);
            Assert.AreEqual(FieldTile.Chest, chest.TargetTile);
            Assert.AreEqual(StepKind.BlockedByObject, fountain.Kind);
            Assert.AreEqual(FieldTile.Fountain, fountain.TargetTile);
            Assert.AreEqual(new Vector2Int(2, 1), walker.Position, "부딪혀도 제자리");
        }

        [Test]
        public void AltarAndShopAreInteractive()
        {
            var town = new FieldWalker(FieldMap.Parse("#####\n#ES.#\n#.P.#\n#####"), new Vector2Int(2, 1));

            var shop = town.TryStep(Direction.Up);
            town.TryStep(Direction.Left);
            var altar = town.TryStep(Direction.Up);

            Assert.AreEqual(FieldTile.Shop, shop.TargetTile);
            Assert.AreEqual(StepKind.BlockedByObject, shop.Kind);
            Assert.AreEqual(FieldTile.Altar, altar.TargetTile);
            Assert.AreEqual(StepKind.BlockedByObject, altar.Kind);
        }

        [Test]
        public void CannotWarpIntoWall()
        {
            Assert.Throws<ArgumentException>(() => walker.WarpTo(new Vector2Int(0, 0)));
        }
    }

    public class EncounterCounterTests
    {
        [Test]
        public void NoEncounterOffGrass()
        {
            var counter = new EncounterCounter(1f, 0);
            Assert.IsFalse(counter.OnStep(false, new System.Random(1)));
            Assert.AreEqual(0, counter.StepsSinceLast);
        }

        [Test]
        public void SafeStepsAfterEachEncounter()
        {
            var counter = new EncounterCounter(1f, 3);
            var rng = new System.Random(1);
            var results = Enumerable.Range(0, 8).Select(_ => counter.OnStep(true, rng)).ToArray();

            // 3걸음 안전 → 4번째 조우 → 다시 3걸음 안전 → 8번째 조우
            CollectionAssert.AreEqual(new[] { false, false, false, true, false, false, false, true }, results);
        }

        [Test]
        public void RateControlsFrequency()
        {
            var never = new EncounterCounter(0f, 0);
            var sometimes = new EncounterCounter(0.12f, 0);
            var rng = new System.Random(42);
            int hits = 0;
            for (int i = 0; i < 5000; i++)
            {
                Assert.IsFalse(never.OnStep(true, rng));
                if (sometimes.OnStep(true, rng)) hits++;
            }
            Assert.That(hits, Is.InRange(450, 750), "약 12%");
        }

        // #133 사용자: 처음 하는 사람은 첫 풀숲에서 몬스터를 못 만나면 뭘 하라는 건지 모름 → 풀숲 두 번째 칸에서 반드시
        [Test]
        public void FirstFightIsGuaranteedOnTheSecondGrassStep()
        {
            var rng = new System.Random(1);
            var counter = new EncounterCounter(0f, 4); // 확률 0, 안전 걸음 4 — 원래라면 절대 안 나옴
            counter.GuaranteeWithin(EncounterCounter.FirstFightGrassSteps);
            Assert.AreEqual(2, EncounterCounter.FirstFightGrassSteps);
            Assert.IsTrue(counter.IsGuaranteed);

            Assert.IsFalse(counter.OnStep(false, rng), "풀숲이 아닌 칸은 세지 않음");
            Assert.IsFalse(counter.OnStep(true, rng), "첫 번째 풀숲 칸");
            Assert.IsTrue(counter.OnStep(true, rng), "두 번째 풀숲 칸에서 반드시 (확률·안전 걸음과 상관없이)");
            Assert.IsFalse(counter.IsGuaranteed, "한 번만");
            Assert.AreEqual(0, counter.StepsSinceLast);
            for (int i = 0; i < 50; i++) Assert.IsFalse(counter.OnStep(true, rng), "그다음은 원래 규칙 (확률 0)");

            // 그 전에 확률로 먼저 만나면 반드시 만나기도 끝 (두 번 연달아 나오지 않게)
            var lucky = new EncounterCounter(1f, 0);
            lucky.GuaranteeWithin(3);
            Assert.IsTrue(lucky.OnStep(true, rng));
            Assert.IsFalse(lucky.IsGuaranteed);
        }
    }

    // 가장 가까운 칸까지 걷는 길 (#133 처음 하는 사람에게 풀숲 가는 길을 보여 주는 분신)
    public class FieldPathTests
    {
        //   y=4  #########
        //   y=3  #,,,#..,#   ← 왼쪽 풀숲은 벽 너머 (돌아가야 함), 오른쪽 풀숲 (7,3)
        //   y=2  #...#.,,#
        //   y=1  #P......#
        //   y=0  #########
        private const string Map = "#########\n#,,,#..,#\n#...#.,,#\n#P......#\n#########";

        [Test]
        public void WalksToTheNearestGrassThenOneMoreInside()
        {
            var map = FieldMap.Parse(Map);
            var walker = new FieldWalker(map, map.Start);
            var path = FieldReach.PathToNearest(map, map.Start, c => walker.CanStand(c), t => t == FieldTile.Grass);

            Assert.AreEqual(new Vector2Int(1, 3), path[path.Count - 1], "가장 가까운 풀숲 (위로 두 칸)");
            Assert.AreEqual(2, path.Count, "시작 칸은 빼고");
            for (int i = 0; i < path.Count; i++)
                Assert.AreEqual(1, ((i == 0 ? map.Start : path[i - 1]) - path[i]).sqrMagnitude, "한 칸씩 이어짐");

            // 풀숲에 닿은 뒤 같은 방향으로 더 들어갈 풀숲이 없으면 거기서 멈춤 (위는 벽)
            var deeper = FieldReach.PathToNearest(map, map.Start, c => walker.CanStand(c), t => t == FieldTile.Grass, extra: 1);
            CollectionAssert.AreEqual(path, deeper);

            // 오른쪽 끝에서 출발하면 바로 위 풀숲 → 같은 방향(위)으로 이어진 풀숲 한 칸 더 안으로
            var right = new Vector2Int(7, 1);
            var inside = FieldReach.PathToNearest(map, right, c => walker.CanStand(c), t => t == FieldTile.Grass, extra: 1);
            CollectionAssert.AreEqual(new[] { new Vector2Int(7, 2), new Vector2Int(7, 3) }, inside);
            // 그 옆 (6,1)에서는 위 풀숲 (6,2) 다음 칸이 길이라 한 칸 더 가지 않음
            var beside = FieldReach.PathToNearest(map, new Vector2Int(6, 1), c => walker.CanStand(c), t => t == FieldTile.Grass, extra: 1);
            CollectionAssert.AreEqual(new[] { new Vector2Int(6, 2) }, beside);
        }

        [Test]
        public void NoPathWhenNothingIsReachable()
        {
            var map = FieldMap.Parse("#####\n#P.##\n###,#\n#####");
            var walker = new FieldWalker(map, map.Start);
            Assert.IsEmpty(FieldReach.PathToNearest(map, map.Start, c => walker.CanStand(c), t => t == FieldTile.Grass));
        }
    }

    public class ChestAndWorldTests
    {
        private ItemData ink;
        private FieldArea area;
        private MonsterSpecies nib;

        [SetUp]
        public void SetUp()
        {
            ink = TestData.Item("shiny_ink");
            area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "meadow").Set("map", "#####\n#C.C#\n#.P.#\n#####")
                .Set("chests", new List<ChestContent> { new ChestContent(ink, 2), new ChestContent(null, 0, 150) });
            nib = TestData.Species("nib", new MonsterStats(30, 14, 8), new MonsterStats(4, 3, 1));
        }

        [Test]
        public void ChestGivesItsContentOnce()
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1);

            var first = session.OpenChest(area, new Vector2Int(1, 2));
            var again = session.OpenChest(area, new Vector2Int(1, 2));
            var gold = session.OpenChest(area, new Vector2Int(3, 2));

            Assert.IsFalse(first.WasEmpty);
            Assert.AreSame(ink, first.Item);
            Assert.IsTrue(again.WasEmpty);
            Assert.AreEqual(2, session.Inventory.GetCount(ink));
            Assert.AreEqual(150, gold.Gold);
            Assert.AreEqual(150, session.Inventory.Gold);
        }

        [Test]
        public void PositionAndOpenedChestsSurviveSave()
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1);
            session.OpenChest(area, new Vector2Int(1, 2));
            session.World.SetPosition("meadow", new Vector2Int(3, 1));

            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { nib }, new[] { ink });
            var json = session.ToSaveData(DateTime.UtcNow).ToJson();
            var loaded = GameSession.FromSaveData(SaveData.FromJson(json), database, TestData.Hero(new MonsterStats(30, 14, 8)));

            Assert.IsTrue(loaded.World.TryGetPosition("meadow", out var position));
            Assert.AreEqual(new Vector2Int(3, 1), position);
            Assert.IsFalse(loaded.World.TryGetPosition("dungeon", out _), "다른 지역 위치는 없음");
            Assert.IsTrue(loaded.OpenChest(area, new Vector2Int(1, 2)).WasEmpty, "불러온 뒤에도 이미 연 상자");
            Assert.AreEqual(2, loaded.Inventory.GetCount(ink));
        }

        [Test]
        public void OldSaveWithoutWorldStartsFresh()
        {
            var data = SaveData.FromJson("{\"version\":1}");
            Assert.IsFalse(data.World.TryGetPosition("meadow", out _));
            Assert.AreEqual(0, data.World.OpenedChests.Count);
        }
    }

    // 실제 프로젝트의 지역 데이터 점검
    public class AreaDataTests
    {
        [Test]
        public void EveryAreaIsPlayable()
        {
            var areas = AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.Greater(areas.Count, 0, "지역이 하나도 없음");
            var ids = new HashSet<string>();

            foreach (var area in areas)
            {
                Assert.IsFalse(string.IsNullOrEmpty(area.AreaId), $"{area.name}: areaId 비어 있음");
                Assert.IsTrue(ids.Add(area.AreaId), $"areaId '{area.AreaId}' 중복");
                Assert.IsNotNull(area.Encounters, $"{area.name}: 출현표 없음");
                Assert.IsNotNull(area.Words, $"{area.name}: 단어장 없음");

                var map = area.Map; // 형식이 틀리면 여기서 예외 (어느 줄이 틀렸는지 메시지에 나옴)
                for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    Assert.IsTrue(FieldThemes.Supports(area.Theme, map.Get(new Vector2Int(x, y))),
                        $"{area.name}: {area.Theme} 테마에서 쓰지 않는 칸 {map.Get(new Vector2Int(x, y))} ({x},{y}) — FieldThemes.Supports에 더하고 그림을 만들 것");
                Assert.AreEqual(map.Chests.Count, area.ChestContents.Count,
                    $"{area.name}: 맵의 보물상자 {map.Chests.Count}개, 내용물 {area.ChestContents.Count}개 — 개수가 같아야 함");
                foreach (var content in area.ChestContents)
                    Assert.IsTrue(content.Item != null && content.Count > 0 || content.Gold > 0 || content.Relic != null,
                        $"{area.name}: 빈 보물상자 내용물");

                // 시작 위치에서 모든 보물상자·회복의 샘(정글은 뗏목으로 가는 모래톱까지)에 갈 수 있어야 함 — 보스 방 문·덩굴 다리는 연 뒤 기준
                var reachable = FieldReach.Flood(map);
                for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var tile = map.Get(cell);
                    if (!FieldMap.IsInteractive(tile)) continue;
                    bool adjacent = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
                        .Any(d => reachable.Contains(cell + d));
                    Assert.IsTrue(adjacent, $"{area.name}: {cell}의 {tile}에 갈 수 없음");
                }
            }
        }

    }
}
