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
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 보스 방 문 (#76, 사용자 결정): 맵에 숨긴 열쇠(K) + 그 지역 단어 40개 발견 → 보스 방 문(G)이 열린다
    public class BossGateTests
    {
        //  y=3  #..B..#   보스 (3,3)
        //  y=2  ##GGG##   보스 방 문
        //  y=1  #K...P#   열쇠 받침대 (1,1), 시작 (5,1)
        private const string Map = "#######\n#..B..#\n##GGG##\n#K...P#\n#######";
        private static readonly DateTime Now = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

        private static FieldArea Area(int needWords = 3)
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var king = TestData.Species("king", new MonsterStats(40, 1, 5), new MonsterStats(0, 0, 0), bite);
            var key = TestData.Item("gate_key").Set("displayName", "서고 열쇠").Set("kind", ItemKind.Key);
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "gate_test").Set("displayName", "잊혀진 서고").Set("map", Map).Set("words", words)
                .Set("boss", new BossEncounter(king, 3)).Set("bossKey", key).Set("bossGateWords", needWords);
        }

        private static GameSession Session() =>
            GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);

        private static void Discover(GameSession session, FieldArea area, int count)
        {
            for (int i = 0; i < count; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, Now);
        }

        [Test]
        public void GateOpensOnlyWithTheKeyAndEnoughWords()
        {
            var area = Area(needWords: 3);
            var session = Session();
            Assert.AreEqual(BossGateState.NeedKey, BossGate.State(area, session), "처음엔 열쇠가 필요");
            Discover(session, area, 3);
            Assert.AreEqual(BossGateState.NeedKey, BossGate.State(area, session), "단어만 모아도 열쇠가 없으면 안 열림");
            Assert.IsFalse(BossGate.TryOpen(area, session));

            session = Session();
            Assert.IsTrue(BossGate.TakeKey(area, session), "받침대에서 열쇠를 얻음");
            Assert.IsFalse(BossGate.TakeKey(area, session), "열쇠는 한 번만");
            Assert.AreEqual(1, session.Inventory.GetCount(area.BossKey));
            Assert.AreEqual(BossGateState.NeedWords, BossGate.State(area, session), "열쇠만 있고 단어가 모자람");
            Discover(session, area, 2);
            Assert.AreEqual(2, BossGate.WordsFound(area, session));
            Assert.AreEqual(BossGateState.NeedWords, BossGate.State(area, session));
            Assert.IsFalse(BossGate.TryOpen(area, session));

            Discover(session, area, 3);
            Assert.AreEqual(BossGateState.Ready, BossGate.State(area, session));
            Assert.IsTrue(BossGate.TryOpen(area, session));
            Assert.AreEqual(BossGateState.Open, BossGate.State(area, session));
            Assert.IsTrue(session.World.IsGateOpened(area.AreaId), "연 문은 세이브에 남음");
            Assert.IsFalse(BossGate.TryOpen(area, session), "이미 열림");
        }

        [Test]
        public void WordsFromOtherWordBooksDoNotCount()
        {
            var area = Area(needWords: 1);
            var session = Session();
            BossGate.TakeKey(area, session);
            session.Vocabulary.Discover("some_other_region_word", Now);
            Assert.AreEqual(0, BossGate.WordsFound(area, session), "다른 지역 단어는 세지 않음");
            Assert.AreEqual(BossGateState.NeedWords, BossGate.State(area, session));
        }

        [Test]
        public void AlreadyDefeatedBossKeepsTheGateOpen()
        {
            // 문이 생기기 전에 보스를 이긴 예전 세이브: 열쇠·단어 없이도 열려 있음
            var area = Area();
            var session = Session();
            session.World.MarkBossDefeated(area.BossId);
            Assert.AreEqual(BossGateState.Open, BossGate.State(area, session));
        }

        [Test]
        public void AreaWithoutGateHasNoGateState()
        {
            var area = Area().Set("map", "#####\n#B.P#\n#####");
            Assert.AreEqual(BossGateState.None, BossGate.State(area, Session()));
        }

        [Test]
        public void MapReadsGateAndKeyTiles()
        {
            var map = FieldMap.Parse(Map);
            Assert.AreEqual(FieldTile.Key, map.Get(new Vector2Int(1, 1)));
            Assert.AreEqual(3, map.FindAll(FieldTile.Gate).Count);
            Assert.IsFalse(map.IsWalkable(new Vector2Int(3, 2)), "닫힌 문은 막힘 (열리면 FieldWalker의 cleared로 지나감)");
            Assert.IsTrue(FieldMap.IsInteractive(FieldTile.Gate));
            Assert.IsTrue(FieldMap.IsInteractive(FieldTile.Key));
            var area = Area();
            Assert.AreEqual("보스 방 문", area.LandmarkName(new Vector2Int(3, 2)), "여러 칸짜리 문은 가운데 칸에 이름표");
            Assert.IsNull(area.LandmarkName(new Vector2Int(2, 2)));
            Assert.AreEqual("서고 열쇠", area.LandmarkName(new Vector2Int(1, 1)));
        }

        [Test]
        public void ObjectiveGoesKeyThenWordsThenGateThenBoss()
        {
            var area = Area(needWords: 2);
            var session = Session();
            session.Record.RecordBattle(true, 1, 0);
            Assert.AreEqual(ObjectiveKind.FindKey, Objectives.Current(session, area).Kind);
            Assert.IsNull(Objectives.Current(session, area).Cell, "열쇠 자리는 별로 알려 주지 않음 (찾는 재미)");
            BossGate.TakeKey(area, session);
            Assert.AreEqual(ObjectiveKind.GatherWords, Objectives.Current(session, area).Kind);
            Discover(session, area, 2);
            var open = Objectives.Current(session, area);
            Assert.AreEqual(ObjectiveKind.OpenGate, open.Kind);
            Assert.AreEqual(new Vector2Int(3, 2), open.Cell, "문 가운데 칸에 별");
            BossGate.TryOpen(area, session);
            Assert.AreEqual(ObjectiveKind.DefeatBoss, Objectives.Current(session, area).Kind);
        }

        [Test]
        public void OpenedGatesSurviveSaveAndLoad()
        {
            var world = new WorldState();
            world.MarkGateOpened("library");
            var loaded = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(world));
            Assert.IsTrue(loaded.IsGateOpened("library"));
            Assert.IsFalse(loaded.IsGateOpened("forest"));
            var old = JsonUtility.FromJson<WorldState>("{\"areaId\":\"library\"}");
            Assert.IsFalse(old.IsGateOpened("library"), "예전 세이브(칸 없음)도 안전");
        }
    }

    // 실제 지역 데이터: 보스가 있는 맵마다 문·열쇠가 있고, 열쇠는 문 밖에, 보스는 문 안에
    public class BossGateDataTests
    {
        private static List<FieldArea> Areas() =>
            AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(g))).ToList();

        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        private static HashSet<Vector2Int> Flood(FieldMap map, bool gateOpen)
        {
            var seen = new HashSet<Vector2Int> { map.Start };
            var queue = new Queue<Vector2Int>(seen);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in Steps)
                {
                    var next = cell + d;
                    bool passable = map.IsWalkable(next) || gateOpen && map.Get(next) == FieldTile.Gate;
                    if (passable && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return seen;
        }

        private static bool Touches(HashSet<Vector2Int> reachable, Vector2Int cell) => Steps.Any(d => reachable.Contains(cell + d));

        [Test]
        public void EveryBossRoomHasAGateAndAKeyOutsideIt()
        {
            int checkedAreas = 0;
            foreach (var area in Areas())
            {
                var map = area.Map;
                var gates = map.FindAll(FieldTile.Gate);
                var keys = map.FindAll(FieldTile.Key);
                if (area.Boss == null)
                {
                    Assert.IsEmpty(gates, $"{area.name}: 보스가 없는데 보스 방 문이 있음");
                    Assert.IsEmpty(keys, $"{area.name}: 보스가 없는데 열쇠가 있음");
                    continue;
                }
                checkedAreas++;
                Assert.IsNotEmpty(gates, $"{area.name}: 보스 방 문(G)이 없음");
                Assert.AreEqual(1, keys.Count, $"{area.name}: 열쇠 받침대(K)는 하나");
                Assert.IsNotNull(area.BossKey, $"{area.name}: 열쇠 아이템(bossKey)이 비어 있음");
                Assert.AreEqual(ItemKind.Key, area.BossKey.Kind, $"{area.name}: 열쇠 아이템 종류가 Key가 아님");
                Assert.That(area.BossGateWords, Is.InRange(1, area.Words.Words.Count), $"{area.name}: 필요한 단어 수가 단어장보다 많음");

                var closed = Flood(map, gateOpen: false);
                var open = Flood(map, gateOpen: true);
                Assert.IsTrue(Touches(closed, keys[0]), $"{area.name}: 문을 열기 전에 열쇠에 갈 수 없음");
                Assert.IsTrue(gates.Any(g => Touches(closed, g)), $"{area.name}: 문 앞에 갈 수 없음");
                Assert.IsFalse(Touches(closed, map.BossPosition.Value), $"{area.name}: 문을 안 열어도 보스에게 갈 수 있음 (다른 길이 있음)");
                Assert.IsTrue(Touches(open, map.BossPosition.Value), $"{area.name}: 문을 열어도 보스에게 갈 수 없음");
            }
            Assert.Greater(checkedAreas, 0);
        }
    }
}
