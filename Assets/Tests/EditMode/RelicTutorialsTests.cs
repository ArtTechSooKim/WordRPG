using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 성유물 안내 규칙 (#142): 가방 빨간 점 · 제단 안내를 언제 하는지 · 가장 가까운 제단
    public class RelicTutorialsTests
    {
        private static readonly MonsterStats NoBonus = new MonsterStats(0, 0, 0);

        private static RelicData Relic(string id, Items.ItemData material = null)
        {
            var skill = TestData.Skill(id + "_skill", SkillKind.Damage, SkillTarget.SingleEnemy, 5);
            return TestData.Relic(id, skill, NoBonus, material);
        }

        private static GameSession Session(params RelicData[] relics) =>
            GameSession.NewGame(TestData.Hero(new MonsterStats(100, 5, 5), relics), 1);

        [Test]
        public void BagBadgesLightUpWhenARelicArrivesAndWhenSlotsAreFull()
        {
            var session = Session(Relic("a"));
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.RelicNew, session), "처음 성유물뿐이면 점 없음");

            session.GrantRelic(Relic("b"));
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.RelicNew, session), "성유물을 더 얻으면 빨간 점");
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.RelicSwap, session), "빈 칸에 끼워졌으니 바꿀 것은 아직 없음");
            Assert.IsNull(RelicTutorials.NewestUnequipped(session.Hero));
            Assert.AreEqual("b", RelicTutorials.Newest(session.Hero).Data.RelicId);

            session.GrantRelic(Relic("c"));
            var fourth = session.GrantRelic(Relic("d"));
            Assert.IsTrue(MenuBadges.ShouldShow(MenuBadges.RelicSwap, session), "칸이 가득 차서 못 끼운 성유물이 생기면 빨간 점");
            Assert.AreSame(fourth, RelicTutorials.NewestUnequipped(session.Hero));

            session.Tutorials.Mark(MenuBadges.RelicNew);
            session.Tutorials.Mark(MenuBadges.RelicSwap);
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.RelicNew, session), "한 번 열면 꺼짐");
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.RelicSwap, session));
            CollectionAssert.Contains(TutorialProgress.All, TutorialProgress.Altar);
        }

        [Test]
        public void AltarGuideWaitsForMaterialsAndGoldAndHappensOnce()
        {
            var ink = TestData.Item("ink");
            var session = Session(Relic("a", ink));
            Assert.IsFalse(RelicTutorials.AltarDue(session), "재료가 없으면 아직");

            session.Inventory.Add(ink, 1);
            Assert.IsFalse(RelicTutorials.AltarDue(session), "재료만 있고 골드가 모자라면 강화할 수 없으니 아직");
            session.Inventory.AddGold(30);
            Assert.IsTrue(RelicTutorials.AltarDue(session), "+1 비용(재료 1개 + 30G)이 모임");
            Assert.AreEqual("a", RelicTutorials.FirstUpgradable(session).Data.RelicId);

            session.Tutorials.Mark(TutorialProgress.Altar);
            Assert.IsFalse(RelicTutorials.AltarDue(session), "한 번만");
        }

        [Test]
        public void AltarGuideSkipsPeopleWhoAlreadyUpgraded()
        {
            var ink = TestData.Item("ink");
            var session = Session(Relic("a", ink));
            session.Inventory.Add(ink, 10);
            session.Inventory.AddGold(1000);
            Assert.IsTrue(RelicUpgrade.TryUpgrade(session.Hero.Relics[0], session.Inventory));
            Assert.IsTrue(RelicTutorials.AnyUpgraded(session.Hero));
            Assert.IsFalse(RelicTutorials.AltarDue(session), "이미 강화해 본 사람에게는 안 함 (1.1에서 온 세이브도)");
        }

        private static FieldArea Area(string id, string map, bool full = false) =>
            ScriptableObject.CreateInstance<FieldArea>().Set("areaId", id).Set("displayName", id).Set("map", map).Set("requiresFullVersion", full);

        [Test]
        public void NearestAltarFollowsOpenExitsAndSkipsAreasYouCannotEnter()
        {
            var session = Session(Relic("a"));
            var dungeon = Area("dungeon", "###\n#P#\n#D#\n#D#\n###");
            var camp = Area("camp", "#####\n#.E.#\n#.P.#\n##D##");
            var paid = Area("paid", "#####\n#.E.#\n#.P.#\n##D##", true);
            dungeon.Set("exits", new List<AreaExit> { new AreaExit(paid, 0), new AreaExit(camp, 0) });
            camp.Set("exits", new List<AreaExit> { new AreaExit(dungeon, 0) });
            paid.Set("exits", new List<AreaExit> { new AreaExit(dungeon, 1) });

            Assert.AreSame(camp, RelicTutorials.NearestAltarArea(camp, session, null), "지금 지역에 있으면 여기");
            Assert.AreSame(paid, RelicTutorials.NearestAltarArea(dungeon, session, null), "먼저 만나는 제단");
            Assert.AreSame(camp, RelicTutorials.NearestAltarArea(dungeon, session, a => !a.RequiresFullVersion), "정식판 지역은 건너뜀");
            Assert.IsNull(RelicTutorials.NearestAltarArea(Area("alone", "###\n#P#\n###"), session, null), "제단이 없으면 null");
        }

        [Test]
        public void NearestAltarDoesNotWalkThroughABossLockedExit()
        {
            var session = Session(Relic("a"));
            var king = TestData.Species("king", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0));
            var lair = Area("lair", "#####\n#.B.#\n#.P.#\n##D##").Set("boss", new BossEncounter(king, 5));
            var camp = Area("camp", "#####\n#.E.#\n#.P.#\n##D##");
            var start = Area("start", "###\n#P#\n#D#\n###");
            start.Set("exits", new List<AreaExit> { new AreaExit(camp, 0, lair) });

            Assert.IsNull(RelicTutorials.NearestAltarArea(start, session, null), "보스가 열어야 하는 길은 잠겨 있음");
            session.World.MarkBossDefeated(lair.BossId);
            Assert.AreSame(camp, RelicTutorials.NearestAltarArea(start, session, null), "보스를 이기면 열림");
        }

        [Test]
        public void StandsBelowTheAltarFacingItOrBesideWhenBlocked()
        {
            var open = FieldMap.Parse("#####\n#.E.#\n#.P.#\n#####");
            var altar = open.Find(FieldTile.Altar).Value;
            Assert.AreEqual(altar + Vector2Int.down, RelicTutorials.StandBefore(open, altar, out var facing), "아래쪽");
            Assert.AreEqual(Direction.Up, facing);

            var blocked = FieldMap.Parse("#####\n#.EP#\n#####");
            altar = blocked.Find(FieldTile.Altar).Value;
            Assert.AreEqual(altar + Vector2Int.right, RelicTutorials.StandBefore(blocked, altar, out facing), "아래가 막혔으면 옆");
            Assert.AreEqual(Direction.Left, facing, "오른쪽에 서면 왼쪽(제단)을 봄");

            var walled = FieldMap.Parse("#####\n##E##\n#####\n#P..#\n#####");
            Assert.IsNull(RelicTutorials.StandBefore(walled, walled.Find(FieldTile.Altar).Value, out _), "둘레가 다 막힘");
        }

        // 실제 데이터: 어느 지역에서든 제단으로 갈 수 있고, 정식판이 없어도 (서고에서 초원으로)
        [Test]
        public void RealAreasAlwaysReachAnAltar()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var session = Session(Relic("a"));
            foreach (var area in db.Areas)
                session.World.MarkBossDefeated(area.BossId);
            foreach (var area in db.Areas)
            {
                var found = RelicTutorials.NearestAltarArea(area, session, null);
                Assert.IsNotNull(found, $"{area.AreaId}에서 제단을 못 찾음");
                Assert.IsTrue(RelicTutorials.StandBefore(found.Map, found.Map.Find(FieldTile.Altar).Value, out _).HasValue, $"{found.AreaId} 제단 앞 칸");
            }
            var free = RelicTutorials.NearestAltarArea(db.FindArea("library"), session, a => !a.RequiresFullVersion);
            Assert.AreEqual("meadow", free.AreaId, "정식판 없이 서고에서 → 초원");
        }
    }
}
