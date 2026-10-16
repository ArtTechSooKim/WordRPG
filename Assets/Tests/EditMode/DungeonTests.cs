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
    public class DoorAndBossMapTests
    {
        //   y=3  #D#D#
        //   y=2  #.B.#
        //   y=1  #.P.#
        //   y=0  ##D##
        private const string Map = "#D#D#\n#.B.#\n#.P.#\n##D##";

        [Test]
        public void DoorsAreWalkableAndInReadingOrder()
        {
            var map = FieldMap.Parse(Map);

            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 3), new Vector2Int(3, 3), new Vector2Int(2, 0) }, map.Doors);
            Assert.IsTrue(map.IsWalkable(new Vector2Int(2, 0)));
            Assert.AreEqual(2, map.DoorIndex(new Vector2Int(2, 0)));
            Assert.AreEqual(new Vector2Int(2, 2), map.BossPosition);
        }

        [Test]
        public void OnlyOneBossPerMap()
        {
            var e = Assert.Throws<FormatException>(() => FieldMap.Parse("#BB#\n#.P#"));
            StringAssert.Contains("하나만", e.Message);
        }

        [Test]
        public void WalkingOntoDoorAndBumpingBoss()
        {
            var walker = new FieldWalker(FieldMap.Parse(Map), new Vector2Int(2, 1));

            var boss = walker.TryStep(Direction.Up);
            Assert.AreEqual(StepKind.BlockedByObject, boss.Kind);
            Assert.AreEqual(FieldTile.Boss, boss.TargetTile);

            var door = walker.TryStep(Direction.Down);
            Assert.IsTrue(door.EnteredDoor);
            Assert.AreEqual(new Vector2Int(2, 0), walker.Position);
        }

        [Test]
        public void AreaFindsExitForEachDoor()
        {
            var other = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "other");
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "here").Set("map", Map)
                .Set("exits", new List<AreaExit> { new AreaExit(other, 0), new AreaExit(other, 1) });

            Assert.AreSame(other, area.GetExit(new Vector2Int(1, 3)).Target);
            Assert.AreEqual(1, area.GetExit(new Vector2Int(3, 3)).TargetDoorIndex);
            Assert.IsNull(area.GetExit(new Vector2Int(2, 0)), "세 번째 D는 연결 없음");
            Assert.IsNull(area.GetExit(new Vector2Int(2, 1)), "D가 아닌 칸");
            Assert.AreEqual("here:boss", area.BossId);
            Assert.IsNull(area.Boss, "보스 종이 없으면 보스 없음");
        }
    }

    public class DungeonSaveTests
    {
        [Test]
        public void DefeatedBossAndCurrentAreaSurviveSave()
        {
            var nib = TestData.Species("nib", new MonsterStats(30, 14, 8), new MonsterStats(4, 3, 1));
            var library = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "library");
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { nib }, new ItemData[0], new[] { library });

            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(30, 14, 8)), 1);
            session.World.MarkBossDefeated("library:boss");
            session.World.SetPosition("library", new Vector2Int(8, 2));

            var json = session.ToSaveData(DateTime.UtcNow).ToJson();
            var loaded = GameSession.FromSaveData(SaveData.FromJson(json), database, TestData.Hero(new MonsterStats(30, 14, 8)));

            Assert.IsTrue(loaded.World.IsBossDefeated("library:boss"));
            Assert.IsFalse(loaded.World.IsBossDefeated("meadow:boss"));
            Assert.AreEqual("library", loaded.World.AreaId);
            Assert.AreSame(library, database.FindArea(loaded.World.AreaId));
            Assert.IsNull(database.FindArea("nowhere"));
        }
    }

    // 실제 프로젝트의 지역 연결·보스 데이터 점검
    public class DungeonDataTests
    {
        private static List<FieldArea> Areas() =>
            AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(g))).ToList();

        [Test]
        public void EveryDoorLeadsSomewhereAndBack()
        {
            foreach (var area in Areas())
            {
                var doors = area.Map.Doors;
                Assert.AreEqual(doors.Count, area.Exits.Count,
                    $"{area.name}: 맵의 출입구 {doors.Count}개, 연결 {area.Exits.Count}개 — 개수가 같아야 함");

                for (int i = 0; i < area.Exits.Count; i++)
                {
                    var exit = area.Exits[i];
                    Assert.IsNotNull(exit.Target, $"{area.name}: {i}번 출입구의 도착 지역이 비어 있음");
                    var targetDoors = exit.Target.Map.Doors;
                    Assert.That(exit.TargetDoorIndex, Is.InRange(0, targetDoors.Count - 1),
                        $"{area.name}: {i}번 출입구가 {exit.Target.name}의 없는 출입구({exit.TargetDoorIndex}번)를 가리킴");

                    var back = exit.Target.Exits[exit.TargetDoorIndex];
                    Assert.AreSame(area, back.Target, $"{area.name} → {exit.Target.name}: 돌아오는 출입구가 다른 곳으로 연결됨");
                    Assert.AreEqual(i, back.TargetDoorIndex, $"{area.name} → {exit.Target.name}: 돌아오면 다른 출입구로 나옴");
                }
            }
        }

        [Test]
        public void BossDataMatchesMap()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            foreach (var area in Areas())
            {
                bool hasBossTile = area.Map.BossPosition.HasValue;
                Assert.AreEqual(hasBossTile, area.Boss != null,
                    hasBossTile ? $"{area.name}: 맵에 B가 있는데 보스가 지정되지 않음" : $"{area.name}: 보스가 지정됐는데 맵에 B가 없음");
                if (area.Boss == null) continue;

                Assert.Greater(area.Boss.Level, 0);
                Assert.Greater(area.Boss.Species.Skills.Count, 0, "보스에게 기술이 없음");
                Assert.AreSame(area.Boss.Species, database.FindMonster(area.Boss.Species.SpeciesId), "보스가 GameDatabase에 없음");
            }
        }

        [Test]
        public void GameDatabaseKnowsEveryArea()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            foreach (var area in Areas())
                Assert.AreSame(area, database.FindArea(area.AreaId),
                    $"{area.name}이 GameDatabase에 없음 — 이 지역에서 저장하면 다음에 시작 지역으로 돌아감 (Refresh Game Database)");
        }

        [Test]
        public void EveryDoorIsReachable()
        {
            foreach (var area in Areas())
            {
                var map = area.Map;
                var seen = FieldReach.Flood(map); // 보스 방 문·덩굴 다리를 연 뒤 기준 (숲의 정글 출입구는 보스 공터 안, #80)
                foreach (var door in map.Doors)
                    Assert.IsTrue(seen.Contains(door), $"{area.name}: {door}의 출입구에 갈 수 없음");
            }
        }
    }
}
