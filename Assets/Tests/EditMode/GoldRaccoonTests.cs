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
    // 비밀의 숲 황금 너구리 (#292 사용자): 하루 한 번 · 100 · 150 · 200 · 1000골드 · 중간에 나가도 그다음 자리부터
    public class GoldRaccoonTests
    {
        private static readonly DateTime Day = new DateTime(2026, 12, 3, 23, 50, 0);

        private static FieldArea Area(params int[] gold) =>
            ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "secret_test").Set("map", "#####\n#...#\n#.P.#\n#####")
                .Set("goldRaccoonSpots", new List<Vector2Int> { new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(1, 1) }.Take(gold.Length).ToList())
                .Set("goldRaccoonGold", gold.ToList());

        private static GameSession Session() => GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);

        [Test]
        public void FourCatchesGiveTheGoldInOrderThenItIsGoneForTheDay()
        {
            var area = Area(100, 150, 200, 1000);
            var session = Session();
            int start = session.Inventory.Gold;
            Assert.IsTrue(GoldRaccoon.LivesIn(area));
            Assert.IsTrue(GoldRaccoon.IsHere(area, session.World, Day));
            var got = new List<int>();
            var spots = new List<Vector2Int>();
            var last = new List<bool>();
            while (GoldRaccoon.IsHere(area, session.World, Day))
            {
                spots.Add(GoldRaccoon.Spot(area, session.World, Day).Value);
                last.Add(GoldRaccoon.IsLastSpot(area, session.World, Day));
                Assert.AreEqual(GoldRaccoon.NextGold(area, session.World, Day), got.Count < 4 ? new[] { 100, 150, 200, 1000 }[got.Count] : 0);
                got.Add(GoldRaccoon.Catch(area, session, Day));
            }
            CollectionAssert.AreEqual(new[] { 100, 150, 200, 1000 }, got, "#292 사용자: 100, 150, 200, 1000골드");
            CollectionAssert.AreEqual(area.GoldRaccoonSpots.ToArray(), spots.ToArray(), "자리 차례대로");
            CollectionAssert.AreEqual(new[] { false, false, false, true }, last, "마지막 자리 = 파바바박");
            Assert.AreEqual(start + 1450, session.Inventory.Gold);
            Assert.AreEqual(0, GoldRaccoon.Catch(area, session, Day), "오늘은 더 없음");
            Assert.IsNull(GoldRaccoon.Spot(area, session.World, Day));
            Assert.AreEqual(start + 1450, session.Inventory.Gold);
        }

        [Test]
        public void ANewLocalDayBringsItBackFromTheFirstSpot()
        {
            var area = Area(100, 150, 200, 1000);
            var session = Session();
            GoldRaccoon.Catch(area, session, Day);
            GoldRaccoon.Catch(area, session, Day);
            Assert.AreEqual(2, GoldRaccoon.CaughtToday(session.World, Day));
            Assert.AreEqual(area.GoldRaccoonSpots[2], GoldRaccoon.Spot(area, session.World, Day.AddMinutes(5)), "같은 날은 이어서");
            var tomorrow = Day.AddMinutes(15); // 자정을 넘김 (현지 날짜 — 사전과 같은 기준)
            Assert.AreEqual(0, GoldRaccoon.CaughtToday(session.World, tomorrow), "하루에 한 번씩 리셋");
            Assert.AreEqual(area.GoldRaccoonSpots[0], GoldRaccoon.Spot(area, session.World, tomorrow));
            Assert.AreEqual(100, GoldRaccoon.Catch(area, session, tomorrow), "다음 날은 다시 100부터");
        }

        [Test]
        public void ItSurvivesSavesAndOldSavesStartFresh()
        {
            var area = Area(100, 150, 200, 1000);
            var session = Session();
            GoldRaccoon.Catch(area, session, Day);
            var loaded = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(session.World));
            Assert.AreEqual(1, GoldRaccoon.CaughtToday(loaded, Day));
            var old = JsonUtility.FromJson<WorldState>("{\"areaId\":\"forest\"}");
            Assert.AreEqual(0, GoldRaccoon.CaughtToday(old, Day), "1.4 세이브(칸 없음)는 오늘 아직 안 잡음");
        }

        [Test]
        public void AreasWithoutMatchingSpotsAndGoldHaveNoRaccoon()
        {
            var session = Session();
            Assert.IsFalse(GoldRaccoon.LivesIn(ScriptableObject.CreateInstance<FieldArea>().Set("map", "###\n#P#\n###")));
            var broken = Area(100, 150).Set("goldRaccoonGold", new List<int> { 100 });
            Assert.IsFalse(GoldRaccoon.LivesIn(broken), "자리와 골드 수가 다르면 없음");
            Assert.AreEqual(0, GoldRaccoon.Catch(broken, session, Day));
        }
    }

    // 숨은 안개 'V' (#292): 이정표를 모두 바로잡기 전엔 가짜 길처럼 막히고 맴돎 → 모두 바로잡으면 걷힘
    public class SecretHazeTests
    {
        //  y=3  #..MM.X.#   헷갈 안개 (3,3)(4,3) · 이정표 ② (6,3)
        //  y=2  ###V#X..#   숨은 안개 (3,2) · 이정표 ① (5,2)
        //  y=1  #.....P.#
        private const string Map = "#########\n#..MM.X.#\n###V#X..#\n#.....P.#\n#########";

        private static FieldArea Area() => ScriptableObject.CreateInstance<FieldArea>()
            .Set("areaId", "secret_haze_test").Set("map", Map).Set("theme", FieldTheme.Forest);

        [Test]
        public void TheSecretHazeClearsOnlyWhenEverySignpostIsFixed()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);
            var secret = new Vector2Int(3, 2);
            var below = new Vector2Int(3, 1);
            CollectionAssert.AreEqual(new[] { secret }, Signposts.SecretHaze(area).ToArray());
            Assert.IsTrue(Signposts.Loops(area, session.World, secret), "바로잡기 전 = 가짜 길처럼 맴돎");
            Assert.IsFalse(CellState.IsPassable(area, session, secret));
            Assert.IsNull(area.Map.SignpostOf(secret), "맡은 이정표가 따로 없음 (모두가 맡음)");

            Signposts.Fix(area, session.World, new Vector2Int(5, 2));
            Assert.IsTrue(Signposts.Loops(area, session.World, secret), "하나만 바로잡으면 그대로");
            Assert.IsFalse(Signposts.Reach(area, session).Contains(secret), "아직 못 지나감");

            Signposts.Fix(area, session.World, new Vector2Int(6, 3));
            Assert.IsTrue(Signposts.AllFixed(area, session.World));
            Assert.IsFalse(Signposts.Loops(area, session.World, secret), "모두 바로잡으면 걷힘");
            Assert.IsTrue(CellState.IsPassable(area, session, secret));
            Assert.IsTrue(CellState.IsDone(area, session, secret), "걷힌 그림");
            Assert.IsTrue(Signposts.Reach(area, session).Contains(secret));
            var walker = new FieldWalker(area.Map, below, cleared: cell => CellState.IsPassable(area, session, cell));
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Up).Kind);
        }
    }

    // 보스 뒤 숨은 안개 'Z' (#292 사용자: 너구리 공터는 숲 보스를 이긴 뒤): 보스 전엔 가짜 길처럼 맴돎 → 이긴 뒤 가까이 가면 걷히는 모습(이벤트) → 지나감
    public class BossHazeTests
    {
        //  y=3  #ZZ....B#   보스 뒤 숨은 안개 (1,3)(2,3) · 보스 (7,3)
        //  y=2  #.......#
        //  y=1  #......P#
        private const string Map = "#########\n#ZZ....B#\n#.......#\n#......P#\n#########";

        [Test]
        public void ItOpensAfterTheBossOnceYouComeClose()
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var raccoon = TestData.Species("raccoon", new MonsterStats(40, 1, 5), new MonsterStats(0, 0, 0), bite);
            var area = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "boss_haze_test").Set("map", Map)
                .Set("theme", FieldTheme.Forest).Set("boss", new BossEncounter(raccoon, 3));
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);
            var haze = new Vector2Int(1, 3);
            Assert.IsTrue(Signposts.Loops(area, session.World, haze), "보스 전 = 가짜 길처럼 맴돎");
            Assert.IsFalse(Signposts.BossHazeDue(area, session.World, new Vector2Int(1, 2)), "보스 전엔 가까이 가도 안 걷힘");

            session.World.MarkBossDefeated(area.BossId);
            Assert.IsTrue(Signposts.Loops(area, session.World, haze), "이겼어도 걷히는 모습을 보기 전엔 그대로");
            Assert.IsFalse(Signposts.BossHazeDue(area, session.World, new Vector2Int(7, 1)), "멀면 아직");
            Assert.IsTrue(Signposts.BossHazeDue(area, session.World, new Vector2Int(4, 2)), $"{Signposts.BossHazeNoticeRange}칸 안이면 걷힐 차례");

            session.World.MarkEvent(Signposts.BossHazeEventId(area));
            Assert.IsFalse(Signposts.BossHazeDue(area, session.World, new Vector2Int(1, 2)), "한 번만");
            Assert.IsFalse(Signposts.Loops(area, session.World, haze));
            Assert.IsTrue(CellState.IsPassable(area, session, haze));
            Assert.IsTrue(CellState.IsDone(area, session, haze), "걷힌 그림");
        }
    }

    // 실제 비밀의 숲 (#292): 숲의 세 번째 출입구(오른쪽 끝) ↔ 비밀의 숲 출입구(왼쪽 끝) · 열쇠 공터는 이정표 모두 · 너구리 공터는 보스 뒤 · 너구리 자리·골드
    public class SecretForestDataTests
    {
        private static FieldArea Load(string id) => AssetDatabase.LoadAssetAtPath<FieldArea>($"Assets/Data/Areas/{id}.asset");

        [Test]
        public void TheSecretForestIsAQuietClearingWithTheGoldRaccoon()
        {
            var secret = Load("secret_forest");
            Assert.IsNotNull(secret, "비밀의 숲 에셋 (Create Sample Data)");
            Assert.AreEqual("secret_forest", secret.AreaId, "세이브에 적히는 id — 출시 뒤 바꾸지 말 것");
            Assert.AreEqual("비밀의 숲", secret.DisplayName);
            Assert.AreEqual(FieldTheme.Forest, secret.Theme);
            Assert.IsTrue(secret.RequiresFullVersion, "숲 안이라 정식판");
            Assert.Greater(secret.StoryOrder, 0);
            Assert.AreEqual(0, secret.Map.FindAll(FieldTile.Grass).Count, "풀숲 없음 — 몬스터가 안 나오는 고요한 공터");
            Assert.IsNull(secret.Boss);
            CollectionAssert.AreEqual(new[] { 100, 150, 200, 1000 }, secret.GoldRaccoonGold.ToArray(), "#292 사용자");
            Assert.IsTrue(GoldRaccoon.LivesIn(secret));
            var reach = FieldReach.Flood(secret.Map);
            foreach (var spot in secret.GoldRaccoonSpots)
            {
                Assert.AreEqual(FieldTile.Lawn, secret.Map.Get(spot), $"너구리 자리 {spot}는 이끼 땅");
                Assert.IsTrue(reach.Contains(spot), $"너구리 자리 {spot}에 닿음");
            }
            CollectionAssert.AreEqual(WordRPG.EditorTools.SampleDataBuilder.GoldRaccoonSpots, secret.GoldRaccoonSpots.ToArray(), "에셋 = 원본 상수");
            var source = FieldMap.Parse(WordRPG.EditorTools.SampleDataBuilder.SecretForestMap);
            Assert.AreEqual(source.Width, secret.Map.Width);
            Assert.AreEqual(source.Height, secret.Map.Height);
            for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                Assert.AreEqual(source.Get(new Vector2Int(x, y)), secret.Map.Get(new Vector2Int(x, y)), $"에셋 맵 = 원본 상수 ({x},{y})");
        }

        // #292 사용자: 열쇠 공터(이정표 모두) · 너구리 공터(숲 보스 뒤)를 따로 — 콘텐츠를 한 번에 소비하지 않게
        [Test]
        public void TheKeyGladeNeedsEverySignpostAndTheRaccoonDoorNeedsTheBoss()
        {
            var forest = Load("forest");
            var secret = Load("secret_forest");
            Assert.AreEqual(3, forest.Map.Doors.Count, "숲 출입구 = 정글 · 초원 · 비밀의 숲 (앞의 번호는 그대로)");
            var door = forest.Map.Doors[2];
            Assert.AreEqual(forest.Map.Width - 1, door.x, "너구리 공터 출입구는 오른쪽 끝 (사용자)");
            Assert.AreEqual(secret, forest.Exits[2].Target);
            Assert.AreEqual(0, forest.Exits[2].TargetDoorIndex);
            Assert.AreEqual(forest, secret.Exits[0].Target);
            Assert.AreEqual(2, secret.Exits[0].TargetDoorIndex, "서로 왕복");
            Assert.AreEqual(0, secret.Map.Doors[0].x, "비밀의 숲은 왼쪽 끝으로 들어옴");
            Assert.AreEqual("jungle", forest.Exits[0].Target.AreaId, "예전 연결은 그대로");
            Assert.AreEqual("meadow", forest.Exits[1].Target.AreaId);

            var map = forest.Map;
            var key = map.Find(FieldTile.Key).Value;
            Assert.Less(key.x, 36, "열쇠 공터 = 예전 숲 북쪽 (사용자: 1.3 때 열쇠 자리의 위쪽)");
            Assert.Greater(key.y, 29, "1.3 열쇠 자리(14, 29)보다 위");
            bool HazeOnly(Vector2Int cell) => map.Get(cell) == FieldTile.Haze;
            bool AndSignposts(Vector2Int cell) => map.Get(cell) != FieldTile.BossHaze;
            var some = FieldReach.Flood(map, true, true, null, HazeOnly);
            Assert.IsFalse(FieldReach.Touches(some, key), "이정표를 다 바로잡기 전엔 열쇠 공터에 못 감");
            Assert.IsFalse(some.Contains(door));
            var signposts = FieldReach.Flood(map, true, true, null, AndSignposts);
            Assert.IsTrue(FieldReach.Touches(signposts, key), "이정표를 모두 바로잡으면 열쇠 공터");
            Assert.IsFalse(signposts.Contains(door), "보스를 이기기 전엔 너구리 공터에 못 감");
            Assert.IsTrue(FieldReach.Flood(map, true, true, null, _ => true).Contains(door), "보스 뒤 숨은 안개가 걷히면 닿음");
            Assert.AreEqual(2, Signposts.SecretHaze(forest).Count, "열쇠 공터 숨은 안개 2칸");
            Assert.AreEqual(4, Signposts.BossHaze(forest).Count, "보스 뒤 숨은 안개 2×2");
            Assert.AreEqual(5, Signposts.All(forest).Count);
        }
    }
}
