using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 보스의 사전 (#45): 보스를 물리치면 쉼터 받침대에 놓이고, 하루 한 번 아직 못 본 단어 하나를 알려 준다
    public class BossDictionaryTests
    {
        //  y=1  #B.LP#   보스(1,1) · 받침대(3,1) · 시작(4,1)
        private const string Map = "######\n#B.LP#\n######";
        private static readonly DateTime Day1 = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Local);
        private static readonly MasteryRules Rules = new MasteryRules();

        private FieldArea area;
        private GameSession session;

        [SetUp]
        public void SetUp()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var boss = TestData.Species("king", new MonsterStats(50, 10, 10), new MonsterStats(0, 0, 0));
            area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "forest_test").Set("displayName", "숲").Set("map", Map).Set("words", words)
                .Set("boss", new BossEncounter(boss, 5)).Set("dictionaryName", "숲의 사전");
            session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 1);
        }

        private DictionaryRead Read(DateTime localNow, int seed = 1) =>
            BossDictionary.Read(area, session, localNow, localNow.ToUniversalTime(), new System.Random(seed));

        [Test]
        public void EmptyUntilTheBossFalls()
        {
            Assert.AreEqual(DictionaryReadOutcome.Locked, Read(Day1).Outcome);
            Assert.AreEqual(0, session.Vocabulary.DiscoveredCount, "잠겨 있으면 아무 일도 없음");

            session.World.MarkBossDefeated(area.BossId);
            Assert.IsTrue(BossDictionary.IsUnlocked(area, session.World));
            var read = Read(Day1);
            Assert.AreEqual(DictionaryReadOutcome.Discovered, read.Outcome);
            CollectionAssert.Contains(area.Words.Words, read.Word, "그 맵 단어장의 단어");
            Assert.AreEqual(MasteryLevel.Learning, session.Vocabulary.GetLevel(read.Word.Id), "도감에 등록 (학습 중)");
            Assert.AreEqual(1, Dex.GetProgress(area.Words, session.Vocabulary).Discovered);
        }

        [Test]
        public void OnceADayByTheDeviceDate()
        {
            session.World.MarkBossDefeated(area.BossId);
            var first = Read(Day1);
            Assert.AreEqual(DictionaryReadOutcome.Discovered, first.Outcome);

            Assert.AreEqual(DictionaryReadOutcome.AlreadyReadToday, Read(Day1.AddHours(13).AddMinutes(59)).Outcome, "같은 날 밤에도 안 됨");
            Assert.AreEqual(1, session.Vocabulary.DiscoveredCount, "또 읽어도 단어가 늘지 않음");

            var next = Read(Day1.Date.AddDays(1).AddMinutes(1)); // 다음 날 0시 1분
            Assert.AreEqual(DictionaryReadOutcome.Discovered, next.Outcome);
            Assert.AreNotSame(first.Word, next.Word, "이미 발견한 단어는 다시 안 나옴");
            Assert.AreEqual(2, session.Vocabulary.DiscoveredCount);
        }

        [Test]
        public void GivesOnlyUndiscoveredWordsAndStopsWhenAllAreFound()
        {
            session.World.MarkBossDefeated(area.BossId);
            var words = area.Words.Words;
            for (int i = 1; i < words.Count; i++) session.Vocabulary.RecordAnswer(words[i].Id, true, DateTime.UtcNow, Rules);

            Assert.AreSame(words[0], Read(Day1).Word, "남은 단어 하나");
            var done = Read(Day1.AddDays(1));
            Assert.AreEqual(DictionaryReadOutcome.AllDiscovered, done.Outcome);
            Assert.AreEqual(BossDictionary.DayKey(Day1), session.World.DictionaryReadDay(area.AreaId), "다 찾았을 때는 읽은 날로 치지 않음");
        }

        [Test]
        public void ReadDayIsSavedPerArea()
        {
            session.World.MarkDictionaryRead("forest", "2026-10-07");
            session.World.MarkDictionaryRead("library", "2026-10-06");
            session.World.MarkDictionaryRead("forest", "2026-10-08");
            var copy = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(session.World));
            Assert.AreEqual("2026-10-08", copy.DictionaryReadDay("forest"));
            Assert.AreEqual("2026-10-06", copy.DictionaryReadDay("library"));
            Assert.IsNull(copy.DictionaryReadDay("meadow"));
        }

        [Test]
        public void DiscoveringAWordMakesItDueForReview()
        {
            var vocabulary = new VocabularyProgress();
            var now = new DateTime(2026, 10, 7, 1, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(vocabulary.Discover("apple", now));
            Assert.AreEqual(MasteryLevel.Learning, vocabulary.GetLevel("apple"));
            Assert.IsTrue(vocabulary.Find("apple").IsDue(now), "곧 문제로 나옴 (복습)");
            Assert.IsFalse(vocabulary.Discover("apple", now), "이미 본 단어는 그대로");
        }

        [Test]
        public void ClearedBossSpotCanBeWalkedOnAndIsNotATarget()
        {
            var map = area.Map;
            var boss = map.BossPosition.Value;
            bool defeated = false;
            var walker = new FieldWalker(map, new Vector2Int(2, 1), null, cell => map.Get(cell) == FieldTile.Boss && defeated);
            Assert.AreEqual(StepKind.BlockedByObject, walker.TryStep(Direction.Left).Kind, "살아 있는 보스는 막힘");
            Assert.AreEqual(Direction.Left, FieldInteraction.FindTarget(map, walker.Position, Direction.Left, cell => defeated && cell == boss));

            defeated = true;
            Assert.AreEqual(Direction.Right, FieldInteraction.FindTarget(map, walker.Position, Direction.Left, cell => defeated && cell == boss),
                "빈자리는 건너뛰고 옆 받침대");
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Left).Kind, "쓰러뜨린 보스 자리는 지나갈 수 있음");
            Assert.AreEqual(boss, walker.Position);
        }

        [Test]
        public void EveryDungeonBossLeavesADictionaryInItsCamp()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            foreach (var each in database.Areas)
            {
                var map = each.Map;
                int lecterns = 0;
                for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.Get(cell) != FieldTile.Lectern) continue;
                    lecterns++;
                    bool reachable = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right }
                        .Any(d => map.IsWalkable(cell + d.ToOffset()));
                    Assert.IsTrue(reachable, $"{each.AreaId}: 받침대 옆에 설 곳이 있어야 함");
                }
                if (each.Boss != null)
                {
                    Assert.IsTrue(each.HasDictionary, $"{each.AreaId}: 보스가 있으면 사전 이름");
                    Assert.AreEqual(1, lecterns, $"{each.AreaId}: 쉼터에 받침대 'L' 하나");
                }
                else Assert.AreEqual(0, lecterns, $"{each.AreaId}: 보스가 없으면 받침대도 없음");
            }
        }
    }
}
