using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 타이틀: 저장 없음 → [시작하기], 저장 있음 → [이어하기] + 요약, [처음부터]는 확인 창, 설정에서 저장 지우기
    public class TitleScreenTests
    {
        private string dir;
        private GameDatabase database;
        private HeroData hero;

        [SetUp]
        public void SetUp()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WordRPG_Title_" + System.Guid.NewGuid().ToString("N"));
            GameManager.SaveDirectoryOverride = dir;
            var quill = TestData.Relic("relic_quill", TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 10),
                new MonsterStats(0, 2, 0)).Set("displayName", "깃펜");
            hero = TestData.Hero(new MonsterStats(30, 10, 5), quill).Set("startLevel", 2);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], null, new[] { quill });
        }

        [TearDown]
        public void TearDown()
        {
            if (GameManager.Instance != null) Object.DestroyImmediate(GameManager.Instance.gameObject);
            GameManager.SaveDirectoryOverride = null;
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
        }

        private GameManager StartManager()
        {
            var managerGo = new GameObject("GameManager");
            managerGo.SetActive(false);
            managerGo.AddComponent<GameManager>().Configure(database, hero);
            managerGo.SetActive(true);
            return GameManager.Instance;
        }

        private static TitleScreen MakeTitle(System.Action<bool> onStart)
        {
            var title = new GameObject("TitleUnderTest").AddComponent<TitleScreen>();
            title.Configure(onStart);
            return title;
        }

        [UnityTest]
        public IEnumerator NoSaveShowsStartOnly()
        {
            var manager = StartManager();
            bool? started = null;
            var title = MakeTitle(newGame => started = newGame);
            yield return null;
            yield return null;
            var root = title.transform;

            Assert.IsFalse(manager.HasSave);
            Assert.AreEqual("영단어와 함께 떠나는 모험", root.Find("TitleCanvas/SafeArea/Subtitle").GetComponent<UnityEngine.UI.Text>().text);
            Assert.IsNull(root.Find("TitleCanvas/SafeArea/Word_apple"), "떠다니는 영단어(apple·memory 등)는 없앰 (2026-10-04)");
            Assert.IsNull(ActiveButton(root, "ContinueButton"), "저장이 없으면 이어하기 없음");
            Assert.AreEqual("시작하기", UiKit.LabelOf(FindButton(root, "NewGameButton")).text);

            FindButton(root, "NewGameButton").onClick.Invoke();
            Assert.AreEqual(true, started, "확인 창 없이 바로 새 게임");
            Assert.IsTrue(manager.HasSave, "시작하면 바로 저장 → 다음에 켜면 이어하기");

            Object.Destroy(title.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedGameContinuesOrRestartsAfterConfirm()
        {
            var manager = StartManager();
            manager.MarkPlaying();
            manager.Session.Inventory.AddGold(77);
            manager.Save();

            // 이어하기
            bool? started = null;
            var title = MakeTitle(newGame => started = newGame);
            yield return null;
            yield return null;
            var root = title.transform;
            Assert.IsNotNull(ActiveButton(root, "ContinueButton"));
            Assert.AreEqual("처음부터", UiKit.LabelOf(FindButton(root, "NewGameButton")).text);
            var summary = AllText(root.Find("TitleCanvas/SafeArea/SaveCard"));
            StringAssert.Contains("주인공 Lv2  ·  성유물 1개", summary);
            StringAssert.Contains("발견한 단어 0", summary);
            FindButton(root, "ContinueButton").onClick.Invoke();
            Assert.AreEqual(false, started);
            Assert.AreEqual(77, manager.Session.Inventory.Gold, "이어하기는 기록 그대로");
            Object.Destroy(title.gameObject);
            yield return null;

            // 처음부터: 확인 창에서 취소 → 그대로, [처음부터] → 지우고 새 게임
            started = null;
            title = MakeTitle(newGame => started = newGame);
            yield return null;
            yield return null;
            root = title.transform;
            var dialog = root.Find("TitleCanvas/SafeArea/ConfirmDialog").gameObject;
            FindButton(root, "NewGameButton").onClick.Invoke();
            Assert.IsTrue(dialog.activeSelf);
            Assert.IsNull(started, "확인 전에는 시작하지 않음");
            FindButton(dialog.transform, "DialogCancelButton").onClick.Invoke();
            Assert.AreEqual(77, manager.Session.Inventory.Gold);

            FindButton(root, "NewGameButton").onClick.Invoke();
            FindButton(dialog.transform, "DialogConfirmButton").onClick.Invoke();
            Assert.AreEqual(true, started);
            Assert.AreEqual(0, manager.Session.Inventory.Gold, "새 게임");
            Assert.IsTrue(manager.HasSave);

            Object.Destroy(title.gameObject);
            yield return null;
        }

        // 안드로이드 뒤로가기: 설정을 닫고, 아무것도 없으면 끝낼지 물음
        [UnityTest]
        public IEnumerator BackButtonClosesSettingsThenAsksToQuit()
        {
            StartManager();
            var title = MakeTitle(_ => { });
            yield return null;
            yield return null;
            var root = title.transform;
            var dialog = root.Find("TitleCanvas/SafeArea/ConfirmDialog").gameObject;

            FindButton(root, "SettingsButton").onClick.Invoke();
            title.HandleBack();
            Assert.IsFalse(title.IsSettingsOpen, "뒤로가기 → 설정 닫힘");
            Assert.IsFalse(title.IsDialogOpen);

            title.HandleBack();
            Assert.IsTrue(title.IsDialogOpen);
            StringAssert.Contains("게임을 끝낼까요?", AllText(dialog.transform));
            title.HandleBack();
            Assert.IsFalse(title.IsDialogOpen, "한 번 더 누르면 취소");

            Object.Destroy(title.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeletingSaveInSettingsShowsStartAgain()
        {
            var manager = StartManager();
            manager.MarkPlaying();
            manager.Save();

            var title = MakeTitle(_ => { });
            yield return null;
            yield return null;
            var root = title.transform;
            Assert.IsNotNull(ActiveButton(root, "ContinueButton"));

            FindButton(root, "SettingsButton").onClick.Invoke();
            Assert.IsTrue(title.IsSettingsOpen);
            var settings = root.Find("TitleCanvas/SafeArea/SettingsView");
            FindButton(settings, "DeleteSaveButton").onClick.Invoke();
            FindButton(settings.Find("ConfirmDialog"), "DialogConfirmButton").onClick.Invoke();

            Assert.IsFalse(manager.HasSave, "저장 파일 삭제");
            Assert.IsFalse(title.IsSettingsOpen);
            Assert.IsNull(ActiveButton(root, "ContinueButton"));
            Assert.AreEqual("시작하기", UiKit.LabelOf(FindButton(root, "NewGameButton")).text);

            // 지운 뒤 그냥 꺼도 빈 세이브를 만들지 않음 (시작하기 전에는 저장 안 함)
            manager.Save();
            Assert.IsFalse(manager.HasSave);

            Object.Destroy(title.gameObject);
            yield return null;
        }
    }
}
