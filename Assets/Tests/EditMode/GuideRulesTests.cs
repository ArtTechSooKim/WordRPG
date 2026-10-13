using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // #61 튜토리얼 줄이기: 목표 한 줄(Objectives) · 메뉴 빨간 점(MenuBadges) · 강도 고르기는 전투를 해 본 뒤(BattleUnlocks)
    public class GuideRulesTests
    {
        //  마을  y=3 ##D##  ← 0번: 숲 (서고 보스를 물리쳐야 열림)     서고 y=3 #####      숲 y=3 #####
        //        y=2 #...#                                             y=2 #.B.#          y=2 #.B.#
        //        y=1 #.P.#                                             y=1 #.P.#          y=1 #.P.#
        //        y=0 ##D##  ← 1번: 서고                                 y=0 ##D##          y=0 ##D##
        private FieldArea town, library, forest;
        private GameSession session;

        [SetUp]
        public void SetUp()
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var king = TestData.Species("king", new MonsterStats(40, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "까먹대왕");
            var raccoon = TestData.Species("raccoon", new MonsterStats(40, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "헷갈너구리");
            town = Area("meadow", "초원", "##D##\n#...#\n#.P.#\n##D##");
            library = Area("library", "잊혀진 서고", "#####\n#.B.#\n#.P.#\n##D##").Set("boss", new BossEncounter(king, 7));
            forest = Area("forest", "숲", "#####\n#.B.#\n#.P.#\n##D##").Set("boss", new BossEncounter(raccoon, 12));
            town.Set("exits", new List<AreaExit> { new AreaExit(forest, 0, library), new AreaExit(library, 0) });
            library.Set("exits", new List<AreaExit> { new AreaExit(town, 1) });
            forest.Set("exits", new List<AreaExit> { new AreaExit(town, 0) });
            session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 1);
        }

        private static FieldArea Area(string id, string name, string map) =>
            ScriptableObject.CreateInstance<FieldArea>().Set("areaId", id).Set("displayName", name).Set("map", map);

        [Test]
        public void ObjectiveLeadsFromFirstBattleThroughBossesToTheDex()
        {
            Assert.AreEqual(ObjectiveKind.FirstBattle, Objectives.Current(session, town).Kind, "처음엔 풀숲에서 첫 전투");

            session.Record.RecordBattle(true, 1, 0);
            var goal = Objectives.Current(session, town);
            Assert.AreEqual(ObjectiveKind.GoToArea, goal.Kind, "숲은 잠겨 있으니 서고로");
            Assert.AreSame(library, goal.Next);
            Assert.AreSame(library, goal.Destination);
            Assert.AreEqual(new Vector2Int(2, 0), goal.Cell, "서고로 가는 아래 출입구");

            goal = Objectives.Current(session, library);
            Assert.AreEqual(ObjectiveKind.DefeatBoss, goal.Kind);
            Assert.AreEqual(new Vector2Int(2, 2), goal.Cell, "보스 칸");

            session.World.MarkBossDefeated(library.BossId);
            goal = Objectives.Current(session, library);
            Assert.AreEqual(ObjectiveKind.GoToArea, goal.Kind, "서고 보스를 이기면 숲으로 — 초원을 지나서");
            Assert.AreSame(town, goal.Next);
            Assert.AreSame(forest, goal.Destination);
            Assert.AreEqual(new Vector2Int(2, 0), goal.Cell, "서고의 출입구");

            goal = Objectives.Current(session, town);
            Assert.AreSame(forest, goal.Next);
            Assert.AreEqual(new Vector2Int(2, 3), goal.Cell, "열린 숲 출입구");

            session.World.MarkBossDefeated(forest.BossId);
            Assert.AreEqual(ObjectiveKind.FillDex, Objectives.Current(session, town).Kind, "보스를 다 이기면 도감 채우기");
            Assert.IsNull(Objectives.Current(session, town).Cell);
        }

        [Test]
        public void MenuBadgesTurnOnWhenTheMenuMeansSomethingAndOffOnceSeen()
        {
            foreach (var id in MenuBadges.All) Assert.IsFalse(MenuBadges.ShouldShow(id, session), $"처음엔 {id} 점 없음");

            var now = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
            session.Vocabulary.Discover("w1", now);
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.Dex, session), "단어를 처음 만나면 [도감]");
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.Training, session));
            for (int i = 2; i <= MenuBadges.TrainingWords; i++) session.Vocabulary.Discover($"w{i}", now);
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.Training, session), "단어가 5개 모이면 [수련]");

            session.Inventory.Add("shiny_ink");
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.Bag, session), "아이템이 생기면 [가방]");

            session.World.Reveal("meadow", 5, 4, new Vector2Int(2, 1));
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.Map, session));
            session.World.Reveal("library", 5, 4, new Vector2Int(2, 1));
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.Map, session), "두 번째 지역에 들어가면 지도");

            session.Tutorials.Mark(MenuBadges.Dex);
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.Dex, session), "한 번 열면 꺼짐");
            session.Tutorials.Clear();
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.Dex, session), "튜토리얼 다시 보기면 다시 켜짐");
            CollectionAssert.IsSubsetOf(MenuBadges.All, TutorialProgress.All);
        }

        [Test]
        public void IntensityUnlocksAfterAFewBattles()
        {
            var record = session.Record;
            Assert.IsFalse(BattleUnlocks.IntensityUnlocked(record), "첫 전투에서는 강도 고르기 없음");
            record.RecordBattle(false, 0, 1);
            Assert.IsFalse(BattleUnlocks.IntensityUnlocked(record));
            record.RecordFled(1, 0);
            Assert.IsTrue(BattleUnlocks.IntensityUnlocked(record), $"{BattleUnlocks.IntensityAfterBattles}번 싸우면(도망도 셈) 열림");
        }
    }
}
