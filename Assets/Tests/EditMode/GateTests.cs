using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 보스를 물리쳐야 열리는 출입구 · 짧은 잔디 · 큰 지역 미니맵
    public class GateTests
    {
        //  마을   y=3  ##D##   ← 0번 출입구: 숲 (보스 방의 보스를 물리쳐야 열림)
        //         y=2  #:,:#   ← 잔디 · 풀숲 · 잔디
        //         y=1  #.P.#
        //         y=0  ##D##   ← 1번 출입구: 보스 방
        private const string TownMap = "##D##\n#:,:#\n#.P.#\n##D##";

        private static (FieldArea town, FieldArea forest, FieldArea lair) Areas()
        {
            var king = TestData.Species("king", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0));
            var town = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "town").Set("displayName", "마을").Set("map", TownMap);
            var forest = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "forest").Set("displayName", "숲")
                .Set("map", "###\n#P#\n#D#");
            var lair = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "lair").Set("displayName", "보스 방")
                .Set("map", "#####\n#.B.#\n#.P.#\n##D##").Set("boss", new BossEncounter(king, 5));
            town.Set("exits", new List<AreaExit> { new AreaExit(forest, 0, lair), new AreaExit(lair, 0) });
            forest.Set("exits", new List<AreaExit> { new AreaExit(town, 0) });
            lair.Set("exits", new List<AreaExit> { new AreaExit(town, 1) });
            return (town, forest, lair);
        }

        [Test]
        public void LawnIsWalkableWithoutEncounters()
        {
            var map = FieldMap.Parse(TownMap);
            Assert.AreEqual(FieldTile.Lawn, map.Get(new Vector2Int(1, 2)));
            Assert.IsTrue(map.IsWalkable(new Vector2Int(1, 2)));

            var walker = new FieldWalker(map, new Vector2Int(1, 1));
            var step = walker.TryStep(Direction.Up);
            Assert.AreEqual(StepKind.Moved, step.Kind);
            Assert.IsFalse(step.EnteredGrass, "잔디는 조우 없음");
            Assert.IsTrue(walker.TryStep(Direction.Right).EnteredGrass, "옆의 풀숲은 조우");
        }

        [Test]
        public void ExitOpensOnlyAfterItsBossIsDefeated()
        {
            var (town, _, lair) = Areas();
            var world = new WorldState();
            Assert.IsFalse(world.IsExitOpen(town.Exits[0]), "보스 전에는 잠김");
            Assert.IsTrue(world.IsExitOpen(town.Exits[1]), "여는 보스가 없는 출입구는 처음부터 열림");
            Assert.IsTrue(world.IsExitOpen(null), "연결 안 된 출입구는 잠김 처리하지 않음 (따로 '닫혀 있다' 안내)");

            world.MarkBossDefeated(lair.BossId);
            Assert.IsTrue(world.IsExitOpen(town.Exits[0]));
        }

        [Test]
        public void ExitWithoutRealBossIsNeverLocked()
        {
            var noBoss = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "empty").Set("map", "#P#");
            var exit = new AreaExit(noBoss, 0, noBoss);
            Assert.IsNull(exit.OpenedByBossOf, "보스가 없는 지역을 지정하면 영영 못 여니 무시");
            Assert.IsTrue(new WorldState().IsExitOpen(exit));
        }

        [Test]
        public void WalkerIsBlockedByLockedDoorOnly()
        {
            var (town, _, lair) = Areas();
            var world = new WorldState();
            var walker = new FieldWalker(town.Map, new Vector2Int(2, 2), cell => !world.IsExitOpen(town.GetExit(cell)));

            var blocked = walker.TryStep(Direction.Up);
            Assert.AreEqual(StepKind.BlockedByGate, blocked.Kind);
            Assert.AreEqual(new Vector2Int(2, 2), walker.Position, "잠긴 출입구 앞에서 멈춤");
            Assert.AreEqual(Direction.Up, walker.Facing);

            world.MarkBossDefeated(lair.BossId);
            Assert.IsTrue(walker.TryStep(Direction.Up).EnteredDoor, "보스를 물리치면 지나감");
        }

        [Test]
        public void GatesOpenedByBossAreFoundAcrossAreas()
        {
            var (town, forest, lair) = Areas();
            var gates = FieldArea.GatesOpenedBy(lair, new[] { town, forest, lair });

            Assert.AreEqual(1, gates.Count);
            Assert.AreSame(town, gates[0].Area);
            Assert.AreEqual(new Vector2Int(2, 3), gates[0].Cell, "마을 맨 위 출입구");
            Assert.AreSame(forest, gates[0].Exit.Target);
            Assert.AreEqual(0, FieldArea.GatesOpenedBy(town, new[] { town, forest, lair }).Count, "마을은 보스가 없음");
        }

        [Test]
        public void MinimapWindowFollowsPlayerInsideMap()
        {
            Assert.AreEqual(new RectInt(0, 0, 19, 23), MinimapView.Window(19, 23, 26, 30, new Vector2Int(5, 5)), "작은 맵은 전체");
            Assert.AreEqual(new RectInt(0, 0, 26, 30), MinimapView.Window(38, 46, 26, 30, new Vector2Int(2, 3)), "왼쪽 아래 끝");
            Assert.AreEqual(new RectInt(12, 16, 26, 30), MinimapView.Window(38, 46, 26, 30, new Vector2Int(37, 45)), "오른쪽 위 끝");
            Assert.AreEqual(new RectInt(7, 8, 26, 30), MinimapView.Window(38, 46, 26, 30, new Vector2Int(20, 23)), "가운데");
        }

        [Test]
        public void LawnHasItsOwnMinimapColor()
        {
            foreach (FieldTheme theme in System.Enum.GetValues(typeof(FieldTheme)))
            {
                Assert.AreNotEqual(MinimapArt.ColorOf(FieldTile.Grass, theme, false), MinimapArt.ColorOf(FieldTile.Lawn, theme, false),
                    $"{theme}: 잔디와 풀숲(조우)은 미니맵에서도 구분");
                Assert.AreNotEqual(MinimapArt.ColorOf(FieldTile.Wall, theme, false), MinimapArt.ColorOf(FieldTile.Lawn, theme, false));
            }
        }
    }

    // 실제 데이터: 잠긴 출입구는 진짜 보스가 열고, 시작 지역에서 모든 지역에 갈 수 있어야 한다
    public class GateDataTests
    {
        private static List<FieldArea> Areas() =>
            AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(g))).ToList();

        [Test]
        public void LockedGatesAreOpenedByARealBossThatCanBeReachedFirst()
        {
            foreach (var area in Areas())
            {
                for (int i = 0; i < area.Exits.Count; i++)
                {
                    var exit = area.Exits[i];
                    var boss = exit.OpenedByBossOf;
                    if (boss == null) continue;
                    // 자기 지역 보스로 잠가도 됨 (#80 숲 보스 공터 위의 정글 출입구) — 보스는 그 지역 맵 안에 있고,
                    // 그 지역에는 다른 출입구로 들어오므로(아래 Reachable) 잠긴 문을 지나지 않고 보스에게 갈 수 있다
                    Assert.IsTrue(Reachable(boss, ignoreLocks: false), $"{area.name}의 {i}번 출입구: {boss.name} 보스에 앞 보스들을 물리치며 갈 수 있어야 함 (이 문 없이)");
                }
            }
        }

        [Test]
        public void EveryAreaCanBeReachedFromTheStartArea()
        {
            var meadow = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/meadow.asset");
            Assert.IsNotNull(meadow);
            foreach (var area in Areas())
                Assert.IsTrue(Reachable(area, ignoreLocks: true, from: meadow), $"초원에서 {area.name}에 갈 길이 없음");
        }

        [Test]
        public void ForestIsTheSecondRegionBehindTheLibraryBoss()
        {
            var meadow = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/meadow.asset");
            var library = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/library.asset");
            var forest = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/forest.asset");
            Assert.AreEqual(FieldTheme.Forest, forest.Theme);
            Assert.AreEqual(2, forest.Words.Tier, "숲은 2단계 단어");
            Assert.AreNotSame(meadow.Words, forest.Words);
            var gate = FieldArea.GatesOpenedBy(library, Areas());
            Assert.AreEqual(1, gate.Count, "서고 보스가 여는 길 하나");
            Assert.AreSame(meadow, gate[0].Area);
            Assert.AreSame(forest, gate[0].Exit.Target);
            Assert.IsNotNull(forest.Boss, "숲에도 보스");
            Assert.IsNotNull(forest.Shop, "숲 야영지 상점");
        }

        // 시작 지역(없으면 초원)에서 출입구를 따라 target까지 갈 수 있는지.
        // ignoreLocks가 아니면 정상 진행대로: 갈 수 있는 지역의 보스를 물리치면 그 보스가 여는 문이 열리고, 다시 넓혀 본다
        // (서고 보스 → 숲, 숲 보스 → 정글처럼 이어지는 잠금, #80)
        private static bool Reachable(FieldArea target, bool ignoreLocks, FieldArea from = null)
        {
            from = from ?? AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/meadow.asset");
            var defeated = new HashSet<FieldArea>();
            while (true)
            {
                var seen = new HashSet<FieldArea> { from };
                var queue = new Queue<FieldArea>(seen);
                while (queue.Count > 0)
                {
                    var area = queue.Dequeue();
                    if (area == target) return true;
                    foreach (var exit in area.Exits)
                    {
                        if (exit.Target == null) continue;
                        if (!ignoreLocks && exit.OpenedByBossOf != null && !defeated.Contains(exit.OpenedByBossOf)) continue;
                        if (seen.Add(exit.Target)) queue.Enqueue(exit.Target);
                    }
                }
                int before = defeated.Count;
                foreach (var area in seen)
                    if (area.Boss != null) defeated.Add(area);
                if (defeated.Count == before) return false;
            }
        }
    }
}
