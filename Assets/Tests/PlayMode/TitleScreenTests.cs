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
        public IEnumerator NoSaveTapStartsNewGame()
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
            Assert.IsNull(root.Find("TitleCanvas/SafeArea/ContinueButton"), "이어하기·처음부터 버튼 없음 (2026-10-06)");
            Assert.IsNull(root.Find("TitleCanvas/SafeArea/NewGameButton"));
            Assert.AreEqual("화면을 터치하세요", root.Find("TitleCanvas/SafeArea/TapLabel").GetComponent<UnityEngine.UI.Text>().text);
            Assert.IsFalse(root.Find("TitleCanvas/SafeArea/SaveCard").gameObject.activeSelf, "저장이 없으면 요약 없음");

            FindButton(root, "TapToStart").onClick.Invoke();
            Assert.AreEqual(true, started, "저장이 없으면 화면을 누르면 바로 새 게임");
            Assert.IsTrue(manager.HasSave, "시작하면 바로 저장 → 다음에 켜면 이어하기");

            Object.Destroy(title.gameObject);
            yield return null;
        }

        // 더 새 버전 앱의 세이브 (예: TestFlight에서 예전 빌드를 설치): 치우거나 덮어쓰지 않고, 업데이트를 안내하며 시작을 막는다 (#48)
        [UnityTest]
        public IEnumerator SaveFromNewerAppIsKeptAndTitleAsksToUpdate()
        {
            var system = new WordRPG.Save.SaveSystem(dir);
            System.IO.Directory.CreateDirectory(dir);
            const string newer = "{\"version\": 99, \"note\": \"from a newer app\"}";
            System.IO.File.WriteAllText(system.MainPath, newer);

            var manager = StartManager();
            Assert.IsTrue(manager.SaveBlocked);
            StringAssert.Contains("업데이트", manager.StatusMessage);
            manager.MarkPlaying();
            manager.Save();
            Assert.AreEqual(newer, System.IO.File.ReadAllText(system.MainPath), "덮어쓰지 않음");
            Assert.AreEqual(1, System.IO.Directory.GetFiles(dir).Length, "깨진 파일처럼 치우지 않음");

            bool? started = null;
            var title = MakeTitle(newGame => started = newGame);
            yield return null;
            yield return null;
            var root = title.transform;
            Assert.AreEqual("앱을 업데이트해 주세요", root.Find("TitleCanvas/SafeArea/TapLabel").GetComponent<UnityEngine.UI.Text>().text);
            StringAssert.Contains("더 새 버전", AllText(root.Find("TitleCanvas/SafeArea/SaveCard")));
            FindButton(root, "TapToStart").onClick.Invoke();
            Assert.IsNull(started, "업데이트 전에는 시작하지 않음");

            Object.Destroy(title.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedGameTapContinues()
        {
            var manager = StartManager();
            manager.MarkPlaying();
            manager.Session.Inventory.AddGold(77);
            manager.Save();

            bool? started = null;
            var title = MakeTitle(newGame => started = newGame);
            yield return null;
            yield return null;
            var root = title.transform;
            var summary = AllText(root.Find("TitleCanvas/SafeArea/SaveCard"));
            StringAssert.Contains("주인공 Lv2  ·  성유물 1개", summary);
            StringAssert.Contains("발견한 단어 0", summary);

            // 설정을 연 동안에는 화면을 눌러도 시작하지 않음
            FindButton(root, "SettingsButton").onClick.Invoke();
            FindButton(root, "TapToStart").onClick.Invoke();
            Assert.IsNull(started);
            title.HandleBack();

            FindButton(root, "TapToStart").onClick.Invoke();
            Assert.AreEqual(false, started, "기본은 이어하기");
            Assert.AreEqual(77, manager.Session.Inventory.Gold, "기록 그대로");
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

        // 처음부터 다시 하기는 설정에서, 실수로 지우지 않게 두 번 묻는다
        [UnityTest]
        public IEnumerator RestartInSettingsAsksTwiceThenStartsFresh()
        {
            var manager = StartManager();
            manager.MarkPlaying();
            manager.Session.Inventory.AddGold(77);
            manager.Save();

            bool? started = null;
            var title = MakeTitle(newGame => started = newGame);
            yield return null;
            yield return null;
            var root = title.transform;
            FindButton(root, "SettingsButton").onClick.Invoke();
            var settings = root.Find("TitleCanvas/SafeArea/SettingsView");
            var dialog = settings.Find("ConfirmDialog");

            // 1차 [처음부터] → 2차에서 [취소]: 그대로
            FindButton(settings, "RestartButton").onClick.Invoke();
            StringAssert.Contains("처음부터 다시 할까요?", AllText(dialog));
            FindButton(dialog, "DialogConfirmButton").onClick.Invoke();
            Assert.IsTrue(dialog.gameObject.activeSelf, "한 번 더 묻는다");
            StringAssert.Contains("정말 지울까요?", AllText(dialog));
            Assert.IsTrue(manager.HasSave, "아직 안 지움");
            FindButton(dialog, "DialogCancelButton").onClick.Invoke();
            Assert.IsTrue(manager.HasSave);
            Assert.AreEqual(77, manager.Session.Inventory.Gold);

            // 두 번 다 확인하면 지운다 → 요약이 사라지고, 화면을 누르면 새 게임
            FindButton(settings, "RestartButton").onClick.Invoke();
            FindButton(dialog, "DialogConfirmButton").onClick.Invoke();
            FindButton(dialog, "DialogConfirmButton").onClick.Invoke();
            Assert.IsFalse(manager.HasSave, "저장 파일 삭제");
            Assert.IsFalse(title.IsSettingsOpen);
            Assert.IsFalse(root.Find("TitleCanvas/SafeArea/SaveCard").gameObject.activeSelf);
            Assert.IsNull(started, "지우기만 하고 바로 시작하지는 않음");

            // 지운 뒤 그냥 꺼도 빈 세이브를 만들지 않음 (시작하기 전에는 저장 안 함)
            manager.Save();
            Assert.IsFalse(manager.HasSave);

            FindButton(root, "TapToStart").onClick.Invoke();
            Assert.AreEqual(true, started);
            Assert.AreEqual(0, manager.Session.Inventory.Gold, "새 게임");

            Object.Destroy(title.gameObject);
            yield return null;
        }

        // 튜토리얼 다시 보기 (타이틀 설정): 본 안내를 지우고 바로 시작
        [UnityTest]
        public IEnumerator ReplayTutorialFromTitleClearsAndStarts()
        {
            var manager = StartManager();
            manager.MarkPlaying();
            manager.Session.Tutorials.MarkAll();
            manager.Save();

            bool? started = null;
            var title = MakeTitle(newGame => started = newGame);
            yield return null;
            yield return null;
            FindButton(title.transform, "SettingsButton").onClick.Invoke();
            FindButton(title.transform.Find("TitleCanvas/SafeArea/SettingsView"), "TutorialReplayButton").onClick.Invoke();
            Assert.AreEqual(false, started, "이어서 시작");
            Assert.IsFalse(manager.Session.Tutorials.Has(TutorialProgress.Field), "안내를 다시 볼 수 있게");

            Object.Destroy(title.gameObject);
            yield return null;
        }
    }
}
