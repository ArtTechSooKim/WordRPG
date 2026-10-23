using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 처음 시작 (#124 사용자 — 테스터 "갑자기 초원에 떨어져 당황"): 모습 고르기(8명, 모습만 다름) → 환영 → 혼잣말 → 깃펜으로 떠오른 이름 →
    // 걷기·달리기 → "풀숲으로 가서 몬스터와 싸워 보세요" → 첫 승리 뒤 목표(보스) 한 번. 1.0 세이브는 업데이트 뒤 모습·이름만 한 번
    public class HeroSetupScreenTests
    {
        //   y=3  #,,,,,,B#    ← 풀숲 (조우 100%), 보스 (7,3)
        //   y=2  #.......#
        //   y=1  #P......#    ← 시작 (1,1)
        private const string Map = "#########\n#,,,,,,B#\n#.......#\n#P......#\n#########";

        private int saves;

        private FieldScreen CreateField(GameSession session)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var enemy = TestData.Species("slime", new MonsterStats(8, 1, 1), new MonsterStats(0, 0, 0), bite).Set("displayName", "잉크 슬라임");
            var king = TestData.Species("king", new MonsterStats(500, 10, 10), new MonsterStats(0, 0, 0), bite).Set("displayName", "까먹대왕");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "test").Set("regionName", "테스트");
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "library").Set("displayName", "잊혀진 서고").Set("map", Map)
                .Set("encounters", table).Set("words", words).Set("boss", new BossEncounter(king, 3))
                .Set("encounterRate", 1f).Set("minStepsBetweenEncounters", 0);
            var field = new GameObject("HeroSetupFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, () => saves++, step: 0.05f, animScale: 0.01f, tutorials: true, setup: true);
            return field;
        }

        private static GameSession NewSession()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40).Set("displayName", "휘두르기");
            var hero = TestData.Hero(new MonsterStats(999, 40, 10)).Set("basicSkill", strike).Set("looks", new List<HeroLook>
            {
                new HeroLook("boy", "소년"), new HeroLook("hunter", "사냥꾼"), new HeroLook("knight", "기사")
            });
            return GameSession.NewGame(hero, 1);
        }

        [UnityTest]
        public IEnumerator NewGameChoosesLookThenWelcomeThenQuillName()
        {
            var session = NewSession();
            var field = CreateField(session);
            yield return WaitFor(() => field.CharacterSelect != null && field.CharacterSelect.IsOpen, 2f);
            Assert.IsTrue(field.CharacterSelect.IsOpen, "새 게임은 모습 고르기부터");
            Assert.IsTrue(field.IsPanelOpen, "고르는 동안 필드는 멈춤");
            Assert.AreEqual("boy", field.CharacterSelect.Selected, "처음엔 기본 모습이 골라져 있음");
            Assert.IsNotNull(FindButton(field.transform, "LookCard_knight"));

            FindButton(field.transform, "LookCard_hunter").onClick.Invoke();
            Assert.AreEqual("hunter", field.CharacterSelect.Selected);
            StringAssert.Contains("사냥꾼", AllText(field.CharacterSelect.Root.transform));
            FindButton(field.transform, "CharacterStartButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual("hunter", session.Hero.LookId);
            Assert.AreEqual("hunter", PlayerArt.LookId, "필드 주인공 그림이 고른 모습으로");
            StringAssert.Contains("hunter", field.PlayerSprite.texture.name);

            // 환영 (존댓말, 사용자) → 혼잣말 두 줄 → 깃펜으로 떠오른 이름
            yield return WaitFor(() => field.PauseTutorial.IsShowing, 2f);
            StringAssert.Contains("영단어RPG에 오신 걸 환영합니다", field.PauseTutorial.Message);
            Assert.AreEqual("확인", UiKit.LabelOf(ActiveButton(field.transform, "TutorialNextButton")).text);
            ActiveButton(field.transform, "TutorialNextButton").onClick.Invoke();
            yield return WaitFor(() => field.NameEntry.IsOpen, 3f);
            Assert.IsTrue(field.NameEntry.IsOpen);
            StringAssert.Contains("깃펜으로 떠오른", AllText(field.NameEntry.Root.transform));
            Assert.IsFalse(field.NameEntry.OkButton.interactable, "이름이 비면 [확인] 안 됨");
            field.NameEntry.Input.text = " 아린😀 ";
            Assert.IsTrue(field.NameEntry.OkButton.interactable);
            field.NameEntry.OkButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual("아린", session.Hero.DisplayName, "글꼴에 없는 글자·빈칸은 빼고");
            Assert.IsFalse(session.NeedsHeroSetup);
            Assert.Greater(saves, 0, "정하면 저장");
            yield return null;
            StringAssert.Contains("아린 Lv1", AllText(field.transform), "HUD의 '주인공' 대신 이름");

            // 그다음 평소 걷기 안내
            yield return WaitFor(() => field.Guide.IsShowing, 3f);
            StringAssert.Contains("끌어서 이동", field.Guide.CurrentText);
            Assert.IsFalse(field.IsHeroSetupRunning);
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OldSaveAsksLookAndNameOnceWithoutPrologue()
        {
            var session = NewSession();
            session.Record.RecordBattle(true, 3, 0);
            session.Tutorials.Mark(TutorialProgress.Field);
            Assert.IsTrue(session.NeedsHeroSetup, "1.0 세이브 = 모습·이름 칸 없음");
            var field = CreateField(session);
            yield return WaitFor(() => field.CharacterSelect != null && field.CharacterSelect.IsOpen, 2f);
            Assert.AreEqual("boy", field.CharacterSelect.Selected, "지금 모습이 골라져 있음");
            StringAssert.Contains("업데이트", AllText(field.CharacterSelect.Root.transform));
            FindButton(field.transform, "CharacterStartButton").onClick.Invoke();

            yield return WaitFor(() => field.NameEntry.IsOpen, 2f);
            Assert.IsFalse(field.PauseTutorial.IsShowing, "하던 사람에게는 환영·프롤로그 없음");
            Assert.AreEqual("주인공", field.NameEntry.Input.text, "지금 이름이 들어 있음 — 그대로 [확인]해도 됨");
            StringAssert.Contains("이름을 정해 주세요", AllText(field.NameEntry.Root.transform));
            field.NameEntry.OkButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual("boy", session.Hero.LookId);
            Assert.IsFalse(session.NeedsHeroSetup);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.GrassFight), "풀숲 안내는 건너뜀");
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Goal), "목표 안내도 건너뜀");
            yield return null;
            Assert.IsFalse(field.IsPanelOpen);
            Object.Destroy(field.gameObject);
            yield return null;

            var again = CreateField(session);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(again.CharacterSelect.IsOpen, "한 번 정하면 다시 안 물음");
            Object.Destroy(again.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AfterRunningGoFightInGrassThenGoalReminderAfterFirstWin()
        {
            var session = NewSession();
            session.Hero.SetLook("boy");
            session.Hero.Rename("아린");
            var field = CreateField(session);
            yield return null;
            yield return WaitFor(() => field.Guide.IsShowing, 2f);
            yield return HoldStick(field, Direction.Right, () => !field.Guide.CurrentText.Contains("끌어서"), 5f);
            yield return HoldStick(field, Direction.Left, () => field.Guide.CurrentText.Contains("풀숲"), 5f, run: true);
            Assert.AreEqual("풀숲으로 가서 몬스터와 싸워 보세요", field.Guide.CurrentText);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Field));

            yield return HoldStick(field, Direction.Up, () => field.IsInBattle, 5f);
            Assert.IsTrue(field.IsInBattle, "풀숲에서 첫 전투");
            yield return WaitFor(() => field.Battle.IsRunning, 3f);
            yield return null;
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.GrassFight), "전투가 시작되면 풀숲 안내 끝");
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => field.PauseTutorial.IsShowing, 3f);
            StringAssert.Contains("목표는 잊혀진 서고의 까먹대왕을 물리치는 것입니다", field.PauseTutorial.Message);
            ActiveButton(field.transform, "TutorialNextButton").onClick.Invoke();
            yield return null;
            Assert.IsFalse(field.PauseTutorial.IsShowing);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Goal), "한 번만");
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
