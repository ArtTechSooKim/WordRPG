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
            // 동굴 입구 'O' (#115): 1.2부터 출입구 — 'D'와 같고 그림만 큰 동굴
            Assert.AreEqual(FieldTile.Door, map.Get(new Vector2Int(7, 0)));
            Assert.IsTrue(map.IsCaveMouth(new Vector2Int(7, 0)));
            Assert.AreEqual(0, map.DoorIndex(new Vector2Int(7, 0)), "출입구 번호에 들어감");
            Assert.IsFalse(FieldMap.Parse("#P#\n#D#").IsCaveMouth(new Vector2Int(1, 0)), "'D'는 그냥 출입구");

            Assert.AreEqual(Direction.Right, map.StreamDirection(new Vector2Int(3, 2)));
            Assert.AreEqual(Direction.Down, map.StreamDirection(new Vector2Int(6, 2)));
            Assert.IsNull(map.StreamDirection(new Vector2Int(2, 2)), "얕은 강이 아니면 물살 없음");

            foreach (var walkable in new[] { FieldTile.Stream, FieldTile.Dock, FieldTile.RuinFloor })
                Assert.IsTrue(FieldMap.IsWalkable(walkable), $"{walkable}은 걸을 수 있음");
            foreach (var blocked in new[] { FieldTile.RuinWall, FieldTile.Bridge, FieldTile.Lever, FieldTile.Tablet, FieldTile.Shrine })
                Assert.IsFalse(FieldMap.IsWalkable(blocked), $"{blocked}은 막힘");
            foreach (var usable in new[] { FieldTile.Lever, FieldTile.Tablet, FieldTile.Shrine })
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

        [Test]
        public void BagGetsARedDotWhenTheFirstCompanionJoins()
        {
            var area = Area();
            var session = Session();
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.Companion, session), "동료가 없으면 점 없음");
            session.AddCompanion(area.ShrineCompanion);
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.Companion, session), "동료를 얻으면 가방에 빨간 점 ('동료' 탭)");
            session.Tutorials.Mark(MenuBadges.Companion);
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.Companion, session), "가방을 열어 보면 꺼짐");
            CollectionAssert.Contains(TutorialProgress.All, TutorialProgress.CompanionTurn, "튜토리얼 다시 보기에 포함");
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

    // 단어 석판 문제 (#81): 석판마다 3문제, 틀리면 벌점 없이 다시 고름, 숙련도는 처음 고른 답만
    public class TabletQuizTests
    {
        [Test]
        public void ThreeRightAnswersFillTheTablet()
        {
            var provider = new FakeQuizProvider(); // 정답 = 0번
            var quiz = new TabletQuiz(provider);
            Assert.AreEqual(QuizDirection.MeaningToEnglish, provider.AskedDirections[0], "뜻을 보고 영단어를 고름");
            Assert.AreEqual(TabletAnswer.Wrong, quiz.Answer(2), "틀림");
            Assert.AreEqual(TabletAnswer.Wrong, quiz.Answer(1), "또 틀려도 그대로");
            Assert.AreEqual(0, quiz.Solved, "틀려도 진행이 줄지 않음");
            Assert.AreEqual(TabletAnswer.Correct, quiz.Answer(0));
            Assert.AreEqual(TabletAnswer.Ignored, quiz.Answer(0), "맞힌 뒤엔 Next()까지 무시");
            CollectionAssert.AreEqual(new[] { false }, provider.Submitted, "숙련도엔 처음 고른 답(틀림) 하나만");
            quiz.Next();
            Assert.AreEqual(TabletAnswer.Correct, quiz.Answer(0));
            quiz.Next();
            Assert.IsFalse(quiz.IsComplete);
            Assert.AreEqual(TabletAnswer.Correct, quiz.Answer(0));
            Assert.IsTrue(quiz.IsComplete);
            Assert.AreEqual(3, quiz.Solved);
            Assert.AreEqual(2, quiz.Mistakes);
            quiz.Next();
            Assert.AreEqual(3, provider.AskedDirections.Count, "문제는 3개만");
            CollectionAssert.AreEqual(new[] { false, true, true }, provider.Submitted);
            Assert.AreEqual(TabletAnswer.Ignored, quiz.Answer(0));
        }
    }

    // 물에 사는 보이는 몬스터 (#81 흉내문어): 뗏목이 다니는 강에서만 나타나고 다닌다. 땅 몬스터는 강에 못 들어감
    public class WaterRoamerTests
    {
        private static readonly MonsterSpecies Octopus = TestData.Species("mimic_octopus", new MonsterStats(30, 6, 5), new MonsterStats(5, 1, 1));
        private static readonly MonsterSpecies Lizard = TestData.Species("tail_lizard", new MonsterStats(30, 6, 5), new MonsterStats(5, 1, 1));

        // 왼쪽 땅(서식지 ,) · 가운데 나루터 J · 오른쪽 강(물 ~ 30칸 높이 4칸 폭)
        private static FieldMap Map()
        {
            var rows = new List<string>();
            for (int r = 0; r < 32; r++)
            {
                if (r == 0 || r == 31) { rows.Add(new string('#', 40)); continue; }
                var row = "#" + new string(r % 2 == 0 ? ',' : ':', 30) + (r == 16 ? "J" : "#") + "~~~~~~~" + "#";
                rows.Add(row);
            }
            var bottom = rows[29].ToCharArray(); bottom[1] = 'P'; rows[29] = new string(bottom);
            return FieldMap.Parse(string.Join("\n", rows));
        }

        [Test]
        public void WaterMonstersLiveOnlyOnRaftWater()
        {
            var map = Map();
            var roaming = new RoamingMonsters(map, r => new List<MonsterInstance> { new MonsterInstance(Lizard, 5) }, new System.Random(3),
                new RoamingRules { CellsPerRoamer = 60, WaterCellsPerRoamer = 40, SpawnMinDistance = 2, MinSpacing = 3 },
                r => new List<MonsterInstance> { new MonsterInstance(Octopus, 5) });
            Assert.AreEqual(7 * 30, roaming.WaterHabitat.Count, "강 칸 전부");
            Assert.Greater(roaming.WaterTargetCount, 0);
            roaming.Populate(new Vector2Int(1, 2));
            var water = roaming.Roamers.Where(r => r.Aquatic).ToList();
            var land = roaming.Roamers.Where(r => !r.Aquatic).ToList();
            Assert.AreEqual(roaming.WaterTargetCount, water.Count);
            Assert.AreEqual(roaming.TargetCount, land.Count);
            Assert.IsTrue(water.All(r => r.Leader.Species == Octopus), "물에는 물의 출현표");
            for (float t = 0; t < 30f; t += 0.05f)
            {
                roaming.Tick(0.05f, new Vector2Int(1, 2));
                foreach (var r in roaming.Roamers)
                    Assert.AreEqual(r.Aquatic, map.IsRaftWater(r.Position), $"{r.Leader.Species.SpeciesId} {r.Position}: 물 몬스터는 물에만, 땅 몬스터는 땅에만");
            }

            var dry = new RoamingMonsters(map, r => new List<MonsterInstance> { new MonsterInstance(Lizard, 5) }, new System.Random(3));
            Assert.AreEqual(0, dry.WaterTargetCount, "물의 출현표가 없으면 물엔 없음");
        }

        [Test]
        public void DefeatedWaterMonsterComesBackInTheWater()
        {
            var map = Map();
            var roaming = new RoamingMonsters(map, r => new List<MonsterInstance> { new MonsterInstance(Lizard, 5) }, new System.Random(5),
                new RoamingRules { CellsPerRoamer = 10000, WaterCellsPerRoamer = 1000, SpawnMinDistance = 2, RespawnSeconds = 1f },
                r => new List<MonsterInstance> { new MonsterInstance(Octopus, 5) });
            roaming.Populate(new Vector2Int(1, 2));
            var octopus = roaming.Roamers.First(r => r.Aquatic);
            roaming.Defeat(octopus);
            Assert.IsFalse(roaming.Roamers.Any(r => r.Aquatic));
            for (int i = 0; i < 30; i++) roaming.Tick(0.1f, new Vector2Int(1, 2));
            Assert.AreEqual(1, roaming.Roamers.Count(r => r.Aquatic), "다시 강에서 나타남");
        }
    }

    // 꾸밈 (#86): 꾸밈 칸 'Q'는 막힌 칸이고 둘레를 보고 종류를 고름, 흩뿌림은 걸을 수 있는 바닥 위 그림일 뿐
    public class DecorTests
    {
        //  y=4  #UUUUU::::#
        //  y=3  #U;Q;U:::Q#   유적 안 꾸밈 (3,3) · 들판 꾸밈 (9,3)
        //  y=2  #U;;;U::::#
        //  y=1  #P::::FQ::#   야영지 꾸밈 (7,1) — 샘 (6,1) 바로 옆
        private const string Map =
            "###########\n" +
            "#UUUUU::::#\n" +
            "#U;Q;U:::Q#\n" +
            "#U;;;U::::#\n" +
            "#P::::FQ::#\n" +
            "###########";

        [Test]
        public void DecorBlocksAndPicksItsKindFromTheNeighbourhood()
        {
            var map = FieldMap.Parse(Map);
            Assert.AreEqual(FieldTile.Decor, map.Get(new Vector2Int(3, 3)));
            Assert.IsFalse(FieldMap.IsWalkable(FieldTile.Decor), "꾸밈은 막힘");
            Assert.IsFalse(FieldMap.IsInteractive(FieldTile.Decor), "[확인]할 것 없음");
            Assert.IsTrue(FieldThemes.Supports(FieldTheme.Jungle, FieldTile.Decor));
            Assert.IsFalse(FieldThemes.Supports(FieldTheme.Meadow, FieldTile.Decor), "지금은 정글에서만");

            CollectionAssert.Contains(FieldDecor.Ruins, FieldDecor.Variant(map, new Vector2Int(3, 3)), "유적 옆 = 기둥·석상·돌무더기");
            CollectionAssert.Contains(FieldDecor.Camp, FieldDecor.Variant(map, new Vector2Int(7, 1)), "샘 가까이 = 야영지 상자·통·통나무");
            Assert.AreEqual(FieldDecor.Variant(map, new Vector2Int(9, 3)), FieldDecor.Variant(map, new Vector2Int(9, 3)), "같은 칸은 늘 같은 종류");

            var walker = new FieldWalker(map, new Vector2Int(8, 1));
            Assert.AreEqual(StepKind.Blocked, walker.TryStep(Direction.Left).Kind, "꾸밈에 막힘");
        }

        [Test]
        public void EveryDecorKindAndScatterHasJungleArt()
        {
            foreach (var variant in FieldDecor.Ruins.Concat(FieldDecor.Camp).Concat(FieldDecor.Wild).Distinct())
            {
                Assert.IsNotNull(Resources.Load<Texture2D>($"Art/NinjaAdventure/Tiles/Jungle_Decor_{variant}"), $"Jungle_Decor_{variant}.png 없음");
                if (FieldDecor.IsTall(variant))
                    Assert.IsNotNull(UI.FieldArt.DecorProp(FieldTheme.Jungle, variant), $"{variant} 큰 그림(_prop) 없음");
            }
            foreach (var ground in new[] { FieldTile.Lawn, FieldTile.Water, FieldTile.RuinFloor })
                Assert.Greater(UI.FieldArt.Scatter(FieldTheme.Jungle, ground).Length, 0, $"{ground} 흩뿌림 그림 없음");
        }

        [Test]
        public void ScatterOnlyOnOpenGroundAndNeverBlocks()
        {
            var jungle = UnityEditor.AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/jungle.asset");
            var map = jungle.Map;
            int count = 0;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (FieldDecor.ScatterAt(map, cell, 4) < 0) continue;
                count++;
                var tile = map.Get(cell);
                Assert.IsTrue(tile == FieldTile.Lawn || tile == FieldTile.RuinFloor || map.IsRaftWater(cell), $"{cell}: {tile}에 흩뿌림");
            }
            Assert.Greater(count, 50, "정글 곳곳에 풀·꽃·연잎");
            Assert.Greater(map.FindAll(FieldTile.Decor).Count, 20, "야영지·들판·유적 꾸밈 칸");
        }
    }

    // 목표 한 줄의 유적 단계 (#87): 열쇠를 찾기 전까지 석판 → 단어 → 제단, 동료를 깨우면(또는 열쇠를 먼저 찾으면) 보스 목표로
    public class RuinsObjectiveTests
    {
        //  y=5  #..B....#
        //  y=4  ##GG#####   보스 방 문
        //  y=3  #U;A;T;U#   유적 제단 (3,3) · 석판 (5,3)
        //  y=2  #UUU;UUU#
        //  y=1  #P:::::K#   열쇠 (7,1)
        private const string Map =
            "#########\n" +
            "#..B....#\n" +
            "##GG#####\n" +
            "#U;A;T;U#\n" +
            "#UUU;UUU#\n" +
            "#P:::::K#\n" +
            "#########";

        private static FieldArea Area()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var frog = TestData.Species("frog", new MonsterStats(40, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "빈칸두꺼비");
            var key = TestData.Item("jungle_key").Set("displayName", "정글 열쇠").Set("kind", Items.ItemKind.Key);
            var ara = ScriptableObject.CreateInstance<CompanionData>().Set("companionId", "ara").Set("displayName", "아라")
                .Set("basicSkill", TestData.Skill("ara_pebble", SkillKind.Damage, SkillTarget.SingleEnemy, 12));
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "jungle_goal").Set("displayName", "메아리 정글").Set("theme", FieldTheme.Jungle).Set("map", Map)
                .Set("words", words).Set("boss", new BossEncounter(frog, 5)).Set("bossKey", key).Set("bossGateWords", 4)
                .Set("shrineCompanion", ara).Set("shrineWords", 2);
        }

        [Test]
        public void RuinsComeFirstUntilTheKeyIsFound()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            session.Record.RecordBattle(true, 1, 0);

            var goal = Objectives.Current(session, area);
            Assert.AreEqual(ObjectiveKind.SolveTablets, goal.Kind, "처음엔 유적의 석판");
            Assert.AreEqual(new Vector2Int(5, 3), goal.Cell, "안 푼 석판에 별");

            Ruins.SolveTablet(area, session.World, new Vector2Int(5, 3));
            Assert.AreEqual(ObjectiveKind.GatherEcho, Objectives.Current(session, area).Kind, "석판을 다 풀면 단어 모으기");

            for (int i = 0; i < 2; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, System.DateTime.UtcNow);
            goal = Objectives.Current(session, area);
            Assert.AreEqual(ObjectiveKind.WakeCompanion, goal.Kind);
            Assert.AreEqual(new Vector2Int(3, 3), goal.Cell, "제단에 별");

            session.AddCompanion(area.ShrineCompanion);
            Assert.AreEqual(ObjectiveKind.FindKey, Objectives.Current(session, area).Kind, "동료를 깨우면 보스 목표(열쇠)로");
        }

        [Test]
        public void FindingTheKeyFirstSkipsTheRuins()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            session.Record.RecordBattle(true, 1, 0);
            BossGate.TakeKey(area, session);
            Assert.AreEqual(ObjectiveKind.GatherWords, Objectives.Current(session, area).Kind, "동료는 선택 — 열쇠를 찾았으면 바로 보스 목표");
        }
    }
}
