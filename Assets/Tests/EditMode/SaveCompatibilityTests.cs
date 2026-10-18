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

        // 1.1 세이브 → 1.2 (#115): 1.1 세이브엔 본 업데이트 안내(notices)·밝힌 반딧돌(litStones) 칸이 없다.
        // 1.2에서 정글 강 가운데 섬의 'O'가 동굴로 가는 출입구가 되어(위쪽이라 0번) 정글 야영지 출입구는 0번 → 1번
        private static string As11Save(GameSession session)
        {
            string json = JsonUtility.ToJson(session.ToSaveData(Now));
            foreach (var key in new[] { "notices", "litStones" })
            {
                Assert.IsTrue(json.Contains($"\"{key}\""), $"{key} 칸이 세이브에 있어야 지울 수 있음");
                json = Regex.Replace(json, $",\"{key}\":(\\[[^\\]]*\\]|\"[^\"]*\"|true|false|-?\\d+)", "");
            }
            return json;
        }

        [Test]
        public void JungleSaveFrom11KeepsItsDoorsAndOpensTheCave()
        {
            var db = Database();
            var jungle = db.FindArea("jungle");
            var forest = db.FindArea("forest");
            var cave = db.FindArea("cave");

            Assert.AreEqual(2, jungle.Map.Doors.Count, "정글 출입구 = 섬의 동굴 입구 + 야영지");
            var caveMouth = jungle.Map.Doors[0];
            var campDoor = jungle.Map.Doors[1];
            Assert.IsTrue(jungle.Map.IsCaveMouth(caveMouth));
            Assert.IsFalse(jungle.Map.IsCaveMouth(campDoor));
            Assert.AreSame(forest, jungle.GetExit(campDoor).Target, "야영지 출입구는 그대로 숲으로");
            Assert.AreEqual(campDoor, jungle.Map.Doors[forest.Exits[0].TargetDoorIndex], "숲에서 정글로 가면 1.1과 같은 야영지 출입구에 도착");
            Assert.AreSame(cave, jungle.GetExit(caveMouth).Target);
            Assert.AreEqual(caveMouth, jungle.Map.Doors[cave.Exits[0].TargetDoorIndex], "동굴에서 나오면 섬의 동굴 입구");

            // 정글 야영지에 있던 1.1 세이브 (정글 보스는 아직) — 동굴은 입장 조건 없음 (사용자)
            var session = GameSession.NewGame(HeroData(), 14);
            session.World.SetPosition("jungle", jungle.Map.Start);
            var loaded = Load(As11Save(session));
            Assert.IsEmpty(loaded.LoadWarnings, string.Join(" / ", loaded.LoadWarnings));
            Assert.IsTrue(loaded.World.TryGetPosition("jungle", out var position));
            Assert.AreEqual(jungle.Map.Start, position);
            Assert.IsTrue(loaded.World.IsExitOpen(jungle.GetExit(caveMouth)), "정글 보스를 이기기 전에도 동굴이 열려 있음");
            Assert.AreEqual(0, GlowStones.LitCount(cave, loaded.World), "밝힌 반딧돌 없음");
            Assert.AreSame(UpdateNotices.Cave, loaded.Notices.NextUnseen(), "업데이트한 사람 → 타이틀에 동굴 안내");
        }

        // 업데이트 안내 (#115): 새로 시작한 사람에겐 안 뜨고, 본 기록은 세이브에 남으며 [튜토리얼 다시 보기]와 따로
        [Test]
        public void UpdateNoticesAreForUpdatersOnlyAndSurviveSaving()
        {
            var fresh = GameSession.NewGame(HeroData());
            Assert.IsNull(fresh.Notices.NextUnseen(), "새 게임은 안내를 모두 본 것으로");
            Assert.IsNull(Load(JsonUtility.ToJson(fresh.ToSaveData(Now))).Notices.NextUnseen(), "저장했다 불러와도");

            var updater = Load(As11Save(GameSession.NewGame(HeroData())));
            Assert.AreEqual("업데이트 해주셔서 감사합니다", updater.Notices.NextUnseen().Title, "문구는 사용자가 정한 그대로");
            Assert.AreEqual("동굴 지역이 열렸습니다!\n숲 지역에서 확인해보세요!", UpdateNotices.Cave.Message);
            updater.Notices.Mark(UpdateNotices.Cave.Id);
            updater.Tutorials.Clear();
            Assert.IsNull(updater.Notices.NextUnseen(), "튜토리얼 다시 보기로 안내가 다시 뜨지 않음");
            Assert.IsNull(Load(JsonUtility.ToJson(updater.ToSaveData(Now))).Notices.NextUnseen(), "본 기록은 세이브에");
        }
    }
}
