using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.EditorTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Save;

namespace WordRPG.Tests
{
    // 1.0 세이브 → 1.1 이어하기 (#89, B-4): 1.0(태그 v1.0-build3) 세이브에는 동료(companions·partyCompanionId)·뗏목 위치(rafting)·
    // 당긴 레버(pulledLevers)·푼 석판(solvedTablets) 칸이 없다. 1.1에서 숲 보스 공터 위에 정글 출입구가 생겨 숲 출입구 번호가 하나씩 밀렸다
    public class SaveCompatibilityTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

        private static GameDatabase Database() => AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
        private static HeroData HeroData() => AssetDatabase.LoadAssetAtPath<HeroData>(SampleDataBuilder.HeroPath);

        // 1.1에만 있는 칸을 지운 JSON = 1.0이 저장했을 모양
        private static string As10Save(GameSession session)
        {
            string json = JsonUtility.ToJson(session.ToSaveData(Now));
            foreach (var key in new[] { "companions", "partyCompanionId", "rafting", "pulledLevers", "solvedTablets" })
            {
                Assert.IsTrue(json.Contains($"\"{key}\""), $"{key} 칸이 세이브에 있어야 지울 수 있음");
                json = Regex.Replace(json, $",\"{key}\":(\\[[^\\]]*\\]|\"[^\"]*\"|true|false|-?\\d+)", "");
            }
            return json;
        }

        private static GameSession Load(string json) =>
            GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(json), Database(), HeroData());

        [Test]
        public void ForestSaveFrom10KeepsItsPlaceAndDoors()
        {
            var db = Database();
            var forest = db.FindArea("forest");
            var meadow = db.FindArea("meadow");
            var library = db.FindArea("library");

            // 1.0의 숲 맵 = 지금 맵에서 맨 위 줄의 정글 출입구(D)만 없던 것
            var rows = SampleDataBuilder.ForestMap.Split('\n');
            rows[0] = rows[0].Replace('D', '#');
            var oldForest = FieldMap.Parse(string.Join("\n", rows));
            Assert.AreEqual(1, oldForest.Doors.Count, "1.0 숲 출입구는 초원 쪽 하나");
            var campDoor = oldForest.Doors[0];
            Assert.AreEqual(campDoor, forest.Map.Doors[meadow.Exits[1].TargetDoorIndex], "초원에서 숲으로 가면 1.0과 같은 야영지 출입구에 도착");
            Assert.AreSame(meadow, forest.GetExit(campDoor).Target, "야영지 출입구는 그대로 초원으로");

            // 숲 야영지에 서 있던 1.0 세이브 (서고 보스는 이김, 숲 보스는 아직)
            var session = GameSession.NewGame(HeroData(), 9);
            session.World.MarkBossDefeated(library.BossId);
            var spot = campDoor + Vector2Int.right * 2;
            Assert.IsTrue(forest.Map.IsWalkable(spot));
            session.World.SetPosition("forest", spot);
            session.World.Reveal("forest", forest.Map.Width, forest.Map.Height, spot);
            session.World.MarkChestOpened(forest.ChestId(forest.Map.Chests[0]));
            var loaded = Load(As10Save(session));

            Assert.IsEmpty(loaded.LoadWarnings, string.Join(" / ", loaded.LoadWarnings));
            Assert.IsTrue(loaded.World.TryGetPosition("forest", out var position));
            Assert.AreEqual(spot, position);
            Assert.IsTrue(forest.Map.IsWalkable(position), "1.1 숲 맵에서도 걸을 수 있는 칸");
            Assert.IsFalse(loaded.World.Rafting);
            Assert.IsTrue(loaded.World.IsExplored("forest", spot), "탐험 기록 그대로 (맵 크기 같음)");
            Assert.IsTrue(loaded.World.IsChestOpened(forest.ChestId(forest.Map.Chests[0])), "연 상자 그대로");
            Assert.IsEmpty(loaded.Companions, "동료 없음");
            Assert.IsNull(loaded.PartyCompanion);
            Assert.IsFalse(MenuBadges.ShouldShow(MenuBadges.Companion, loaded), "동료 빨간 점 없음");
            var jungleDoor = forest.Map.Doors[0];
            Assert.AreEqual("jungle", forest.GetExit(jungleDoor).Target.AreaId);
            Assert.IsFalse(loaded.World.IsExitOpen(forest.GetExit(jungleDoor)), "숲 보스를 이기기 전엔 정글 길이 막힘");
        }

        [Test]
        public void PlayersWhoBeatTheForestBossIn10FindTheJungleOpen()
        {
            var db = Database();
            var forest = db.FindArea("forest");
            var session = GameSession.NewGame(HeroData(), 14);
            session.Record.RecordBattle(true, 10, 2);
            foreach (var id in new[] { "meadow", "library", "forest" })
            {
                var area = db.FindArea(id);
                if (area.Boss != null) session.World.MarkBossDefeated(area.BossId);
            }
            session.World.MarkGateOpened("forest");
            session.World.SetPosition("forest", forest.Map.BossPosition.Value);
            var loaded = Load(As10Save(session));

            Assert.IsEmpty(loaded.LoadWarnings);
            Assert.IsTrue(loaded.World.IsExitOpen(forest.GetExit(forest.Map.Doors[0])), "1.0에서 숲 보스를 이겼으면 1.1 정글 출입구가 바로 열려 있음");
            var goal = Objectives.Current(loaded, forest);
            Assert.AreEqual(ObjectiveKind.GoToArea, goal.Kind, "목표 한 줄 = 정글로 가자");
            Assert.AreEqual("jungle", goal.Destination.AreaId);
            Assert.AreEqual(forest.Map.Doors[0], goal.Cell, "숲 보스 공터 위 출입구에 별");
        }
    }
}
