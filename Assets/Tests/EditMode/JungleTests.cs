using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 1.1 메아리 정글의 맵 글자와 규칙 (#80, GDD 9-6): 얕은 강(물살) · 나루터와 뗏목(직접 몰기) · 레버와 덩굴 다리 · 유적 석판과 제단
    public class JungleMapTests
    {
        //  y=6  ##########
        //  y=5  #U;;;;T;U#      유적: 석판 (6,5)
        //  y=4  #U;A;;;;U#      제단 (3,4)
        //  y=3  #~~HH~~~##      골짜기 + 덩굴 다리 (3,3)(4,3) — 나루터가 없는 물이라 뗏목은 못 감
        //  y=2  #Y:>>>v:J~      레버 (1,2), 얕은 강 →→→↓, 나루터 (8,2) 옆 깊은 강 (9,2)
        //  y=1  #P::::v:#~      시작 (1,1)
        //  y=0  #######O#~      동굴 입구 (7,0)
        private const string Map =
            "##########\n" +
            "#U;;;;T;U#\n" +
            "#U;A;;;;U#\n" +
            "#~~HH~~~##\n" +
            "#Y:>>>v:J~\n" +
            "#P::::v:#~\n" +
            "#######O#~";

        private static FieldMap Parse() => FieldMap.Parse(Map);

        [Test]
        public void ParsesTheJungleGlyphs()
        {
            var map = Parse();
            Assert.AreEqual(FieldTile.RuinWall, map.Get(new Vector2Int(1, 5)));
            Assert.AreEqual(FieldTile.RuinFloor, map.Get(new Vector2Int(2, 5)));
            Assert.AreEqual(FieldTile.Tablet, map.Get(new Vector2Int(6, 5)));
            Assert.AreEqual(FieldTile.Shrine, map.Get(new Vector2Int(3, 4)));
            Assert.AreEqual(FieldTile.Bridge, map.Get(new Vector2Int(3, 3)));
            Assert.AreEqual(FieldTile.Lever, map.Get(new Vector2Int(1, 2)));
            Assert.AreEqual(FieldTile.Stream, map.Get(new Vector2Int(3, 2)));
            Assert.AreEqual(FieldTile.Dock, map.Get(new Vector2Int(8, 2)));
            Assert.AreEqual(FieldTile.Cave, map.Get(new Vector2Int(7, 0)));

            Assert.AreEqual(Direction.Right, map.StreamDirection(new Vector2Int(3, 2)));
            Assert.AreEqual(Direction.Down, map.StreamDirection(new Vector2Int(6, 2)));
            Assert.IsNull(map.StreamDirection(new Vector2Int(2, 2)), "얕은 강이 아니면 물살 없음");

            foreach (var walkable in new[] { FieldTile.Stream, FieldTile.Dock, FieldTile.RuinFloor })
                Assert.IsTrue(FieldMap.IsWalkable(walkable), $"{walkable}은 걸을 수 있음");
            foreach (var blocked in new[] { FieldTile.RuinWall, FieldTile.Bridge, FieldTile.Lever, FieldTile.Tablet, FieldTile.Shrine, FieldTile.Cave })
                Assert.IsFalse(FieldMap.IsWalkable(blocked), $"{blocked}은 막힘");
            foreach (var usable in new[] { FieldTile.Lever, FieldTile.Tablet, FieldTile.Shrine, FieldTile.Cave })
                Assert.IsTrue(FieldMap.IsInteractive(usable), $"{usable}은 [확인]으로 씀");
        }

        [Test]
        public void JungleTilesBelongToTheJungleThemeOnly()
        {
            Assert.IsTrue(FieldThemes.Supports(FieldTheme.Jungle, FieldTile.Stream));
            Assert.IsTrue(FieldThemes.Supports(FieldTheme.Jungle, FieldTile.Chest), "정글도 기본 칸은 다 씀");
            Assert.IsFalse(FieldThemes.Supports(FieldTheme.Forest, FieldTile.Dock));
            Assert.IsTrue(FieldThemes.Supports(FieldTheme.Meadow, FieldTile.Key));
        }

        [Test]
        public void WalkingWithTheStreamIsTheFastWay()
        {
            var map = Parse();
            Assert.IsTrue(map.IsWithStream(new Vector2Int(3, 2), Direction.Right), "물살 따라 →");
            Assert.IsFalse(map.IsWithStream(new Vector2Int(4, 2), Direction.Left), "거슬러 가면 보통 속도");
            Assert.IsFalse(map.IsWithStream(new Vector2Int(3, 2), Direction.Up), "옆으로 가면 보통 속도");
            Assert.IsTrue(map.IsWithStream(new Vector2Int(6, 2), Direction.Down), "꺾이는 물살 ↓");
            Assert.IsFalse(map.IsWithStream(new Vector2Int(6, 1), Direction.Down), "다음 칸이 얕은 강이 아니면(벽) 아님");
        }

        [Test]
        public void RaftBoardsAtADockSailsTheRiverAndLandsOnlyAtADock()
        {
            var map = Parse();
            Assert.IsTrue(map.IsRaftWater(new Vector2Int(9, 2)), "나루터 옆 깊은 강");
            Assert.IsTrue(map.IsRaftWater(new Vector2Int(9, 0)), "이어진 강 전부");
            Assert.IsFalse(map.IsRaftWater(new Vector2Int(2, 3)), "나루터가 없는 골짜기 물은 아님");
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(9, 2), new Vector2Int(9, 1), new Vector2Int(9, 0) }, map.RaftWaterCells());

            var walker = new FieldWalker(map, new Vector2Int(7, 2));
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Right).Kind, "나루터로 걸어감");
            Assert.IsFalse(walker.Rafting);
            var board = walker.TryStep(Direction.Right);
            Assert.IsTrue(board.Boarded, "나루터에서 강 쪽으로 → 뗏목");
            Assert.IsTrue(walker.Rafting);
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Down).Kind, "강 위를 직접 몬다");
            Assert.AreEqual(StepKind.Blocked, walker.TryStep(Direction.Left).Kind, "땅(벽)에는 못 올라감");
            Assert.AreEqual(new Vector2Int(9, 1), walker.Position);
            walker.TryStep(Direction.Up);
            var land = walker.TryStep(Direction.Left);
            Assert.IsTrue(land.Landed, "나루터에 닿으면 내림");
            Assert.IsFalse(walker.Rafting);
            Assert.AreEqual(new Vector2Int(8, 2), walker.Position);

            var onFoot = new FieldWalker(map, new Vector2Int(1, 1));
            onFoot.TryStep(Direction.Up);
            Assert.AreEqual(StepKind.BlockedByObject, onFoot.TryStep(Direction.Up).Kind, "레버는 [확인]으로");

            var saved = new FieldWalker(map, new Vector2Int(9, 0), rafting: true);
            Assert.IsTrue(saved.Rafting, "세이브가 강 위였으면 뗏목에 탄 채로");
            Assert.Throws<ArgumentException>(() => new FieldWalker(map, new Vector2Int(9, 0)), "걸어서는 물 위에 못 섬");
        }

        [Test]
        public void LeverBuildsTheNearestBridge()
        {
            var area = Area();
            var map = area.Map;
            var lever = new Vector2Int(1, 2);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(3, 3), new Vector2Int(4, 3) }, map.BridgeOf(lever));
            Assert.AreEqual(lever, map.LeverOf(new Vector2Int(4, 3)));
            Assert.IsNull(map.LeverOf(new Vector2Int(2, 2)), "다리가 아님");
            Assert.IsFalse(map.IsBridgeHorizontal(new Vector2Int(3, 3)), "골짜기를 세로로 건너는 다리");
            Assert.IsTrue(FieldMap.Parse("#####\n#.H.#\n#P###").IsBridgeHorizontal(new Vector2Int(2, 1)), "양옆이 땅이면 가로 다리");

            var session = Session();
            var world = session.World;
            Assert.IsFalse(Levers.IsBridgeBuilt(area, world, new Vector2Int(3, 3)));
            Assert.IsFalse(CellState.IsPassable(area, session, new Vector2Int(3, 3)), "당기기 전엔 막힘");
            var walker = new FieldWalker(map, new Vector2Int(3, 2), cleared: cell => CellState.IsPassable(area, session, cell));
            Assert.AreEqual(StepKind.Blocked, walker.TryStep(Direction.Up).Kind);

            Assert.AreEqual(2, Levers.Pull(area, world, lever).Count, "당기면 다리 두 칸이 놓임");
            Assert.AreEqual(0, Levers.Pull(area, world, lever).Count, "한 번만");
            Assert.IsTrue(CellState.IsDone(area, session, lever), "당긴 레버 그림");
            Assert.IsTrue(CellState.IsDone(area, session, new Vector2Int(4, 3)), "놓인 다리 그림");
            Assert.AreEqual(StepKind.Moved, walker.TryStep(Direction.Up).Kind, "다리를 건넘");
        }

        [Test]
        public void ReachIncludesRaftWaterAndRespectsBridges()
        {
            var map = Parse();
            var open = FieldReach.Flood(map);
            Assert.IsTrue(open.Contains(new Vector2Int(9, 0)), "뗏목으로 강 끝까지");
            Assert.IsTrue(FieldReach.Touches(open, new Vector2Int(6, 5)), "다리를 놓으면 석판에 감");
            Assert.IsTrue(FieldReach.Touches(open, new Vector2Int(7, 0)), "동굴 입구 앞");
            var closed = FieldReach.Flood(map, bridgesOpen: false);
            Assert.IsFalse(FieldReach.Touches(closed, new Vector2Int(6, 5)), "다리 전엔 유적에 못 감");
            Assert.IsFalse(closed.Contains(new Vector2Int(2, 3)), "골짜기 물엔 뗏목도 못 감");
        }

        [Test]
        public void ShrineAwakensAfterAllTabletsAndEnoughWords()
        {
            var area = Area(shrineWords: 2);
            var session = Session();
            var tablet = new Vector2Int(6, 5);
            Assert.AreEqual(ShrineState.NeedTablets, Ruins.State(area, session));
            Assert.AreEqual(0, Ruins.TabletsSolved(area, session.World));
            Ruins.SolveTablet(area, session.World, tablet);
            Assert.AreEqual(1, Ruins.TabletsSolved(area, session.World));
            Assert.IsTrue(CellState.IsDone(area, session, tablet), "푼 석판 그림");
            Assert.AreEqual(ShrineState.NeedWords, Ruins.State(area, session), "석판은 다 풀었지만 단어가 모자람");
            for (int i = 0; i < 2; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, DateTime.UtcNow);
            Assert.AreEqual(ShrineState.Ready, Ruins.State(area, session));
            session.AddCompanion(area.ShrineCompanion);
            Assert.AreEqual(ShrineState.Awakened, Ruins.State(area, session), "동료를 얻으면 빈 제단");
            Assert.IsTrue(CellState.IsDone(area, session, new Vector2Int(3, 4)));

            var noShrine = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "plain").Set("map", "###\n#P#\n###");
            Assert.AreEqual(ShrineState.None, Ruins.State(noShrine, session));
        }

        [Test]
        public void LeversTabletsAndRaftPositionSurviveSave()
        {
            var world = new WorldState();
            world.MarkLeverPulled("jungle:1,2");
            world.MarkTabletSolved("jungle:6,5");
            world.SetPosition("jungle", new Vector2Int(9, 0), onRaft: true);
            var loaded = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(world));
            Assert.IsTrue(loaded.IsLeverPulled("jungle:1,2"));
            Assert.IsTrue(loaded.IsTabletSolved("jungle:6,5"));
            Assert.IsFalse(loaded.IsTabletSolved("jungle:1,1"));
            Assert.IsTrue(loaded.Rafting);
            loaded.SetPosition("jungle", new Vector2Int(8, 2));
            Assert.IsFalse(loaded.Rafting, "걸어서 저장하면 뗏목 아님");

            var old = JsonUtility.FromJson<WorldState>("{\"areaId\":\"forest\",\"hasPosition\":true}");
            Assert.IsFalse(old.IsLeverPulled("jungle:1,2"), "예전 세이브(칸 없음)도 안전");
            Assert.IsFalse(old.Rafting);
        }

        private static FieldArea Area(int shrineWords = 60)
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var ara = ScriptableObject.CreateInstance<CompanionData>()
                .Set("companionId", "ara").Set("displayName", "아라")
                .Set("baseStats", new MonsterStats(50, 10, 10)).Set("growthPerLevel", new MonsterStats(6, 2, 2))
                .Set("basicSkill", TestData.Skill("ara_pebble", SkillKind.Damage, SkillTarget.SingleEnemy, 12));
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "jungle").Set("displayName", "메아리 정글").Set("theme", FieldTheme.Jungle)
                .Set("map", Map).Set("words", words).Set("shrineCompanion", ara).Set("shrineWords", shrineWords);
        }

        private static GameSession Session() => GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
    }
}
