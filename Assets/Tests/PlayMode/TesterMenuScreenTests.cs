using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.UI;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 테스터 메뉴 (#229): 테스트 빌드에서만 설정에 [테스터 메뉴] → 레벨·단어 → 지역까지 건너뛰면 그 지역 시작 지점으로
    public class TesterMenuScreenTests
    {
        [TearDown]
        public void TurnOff() => TesterBuild.Enabled = false;

        private static FieldScreen MakeField(GameDatabase database, GameSession session, System.Action saved)
        {
            var meadow = TesterTools.StoryAreas(database.Areas).First();
            var field = new GameObject("TesterUnderTest").AddComponent<FieldScreen>();
            field.Configure(meadow, session, saved, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            return field;
        }

        [UnityTest]
        public IEnumerator TesterMenuLevelsUpFindsWordsAndSkipsToARegion()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var session = GameSession.NewGame(AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset"), 1);
            int saves = 0;

            // 앱스토어 빌드처럼 꺼져 있으면 버튼이 없음
            var plain = MakeField(database, session, () => saves++);
            yield return null;
            yield return null;
            var plainHud = plain.transform.Find("FieldHud/SafeArea");
            FindButton(plainHud, "SettingsButton").onClick.Invoke();
            Assert.IsNull(ActiveButton(plainHud.Find("SettingsView"), "TesterMenuButton"), "테스트 빌드가 아니면 [테스터 메뉴] 없음");
            Object.Destroy(plain.gameObject);
            yield return null;

            TesterBuild.Enabled = true;
            var field = MakeField(database, session, () => saves++);
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "SettingsButton").onClick.Invoke();
            var settings = hud.Find("SettingsView");
            ActiveButton(settings, "TesterMenuButton").onClick.Invoke();
            var menu = field.TesterMenu;
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(field.IsPanelOpen);
            StringAssert.Contains("Lv.1", menu.Status);

            int before = saves;
            ActiveButton(menu.Root.transform, "TesterLevelUp").onClick.Invoke();
            Assert.AreEqual(1 + TesterTools.LevelStep, session.Hero.Level);
            StringAssert.Contains($"Lv.{1 + TesterTools.LevelStep}", menu.Status);
            StringAssert.Contains($"레벨 1 → {1 + TesterTools.LevelStep}", menu.Message);
            Assert.Greater(saves, before, "바로 저장");

            var book = TesterTools.Books(database.Areas)[0];
            ActiveButton(menu.Root.transform, $"TesterWords_{book.RegionId}").onClick.Invoke();
            Assert.AreEqual(TesterTools.WordStep, Dex.GetProgress(book, session.Vocabulary).Discovered);
            StringAssert.Contains($"찾은 단어 {TesterTools.WordStep} / {book.Words.Count}", AllText(menu.Root.transform));

            // 뒤로가기: 테스터 메뉴 → 설정 순서로 닫힘
            field.HandleBack();
            Assert.IsFalse(menu.IsOpen);
            Assert.IsTrue(settings.gameObject.activeSelf, "닫으면 설정으로 돌아옴");
            ActiveButton(settings, "TesterMenuButton").onClick.Invoke();
            Assert.IsTrue(menu.IsOpen);

            // 정글까지 건너뛰기: 서고·숲 보스를 이긴 것으로 → 정글 시작 지점으로
            var story = TesterTools.StoryAreas(database.Areas);
            var jungle = story.First(a => a.AreaId == "jungle");
            ActiveButton(menu.Root.transform, "TesterSkip_jungle").onClick.Invoke();
            Assert.IsFalse(menu.IsOpen);
            Assert.IsFalse(settings.gameObject.activeSelf, "설정도 닫힘");
            yield return WaitFor(() => field.CurrentArea == jungle && !field.IsInBattle);
            Assert.AreSame(jungle, field.CurrentArea);
            Assert.AreEqual(jungle.Map.Start, field.PlayerCell);
            foreach (var area in story.Where(a => a.StoryOrder < jungle.StoryOrder && a.Boss != null))
                Assert.IsTrue(session.World.IsBossDefeated(area.BossId), $"{area.AreaId} 보스");
            Assert.IsFalse(session.World.IsBossDefeated(jungle.BossId), "정글 보스는 남음");
            Assert.AreEqual("jungle", session.World.AreaId, "세이브 위치도 정글");

            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
