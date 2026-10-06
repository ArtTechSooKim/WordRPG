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
    // 튜토리얼 (#36): 검은 막 위 안내를 따라 하기, [다음]·[건너뛰기], 첫 전투에서 상황마다 한 번씩
    public class TutorialTests
    {
        //   y=3  #,,,,,,#    ← 풀숲 (조우 100%)
        //   y=2  #......#
        //   y=1  #P.....#    ← 시작 (1,1)
        private const string Map = "########\n#,,,,,,#\n#......#\n#P.....#\n########";

        private static FieldScreen CreateField(GameSession session)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("enemy", new MonsterStats(400, 1, 50), new MonsterStats(0, 0, 0), bite);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "test").Set("regionName", "테스트");
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "test").Set("displayName", "테스트 들판").Set("map", Map)
                .Set("encounters", table).Set("words", words)
                .Set("encounterRate", 1f).Set("minStepsBetweenEncounters", 0);
            var field = new GameObject("TutorialFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: true);
            return field;
        }

        private static GameSession NewSession()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 5).Set("displayName", "휘두르기");
            var hero = TestData.Hero(new MonsterStats(999, 5, 10)).Set("basicSkill", strike);
            return GameSession.NewGame(hero, 1);
        }

        [UnityTest]
        public IEnumerator FieldTutorialWalksThroughControls()
        {
            var session = NewSession();
            var field = CreateField(session);
            yield return null; // 첫 프레임에 화면을 만든다
            var tutorial = field.Tutorial;
            yield return WaitFor(() => tutorial.IsShowing, 2f);

            // 1/7 환영: [다음] 단계 — 그동안은 움직이지 않는다
            Assert.AreEqual("1 / 8", tutorial.CurrentCounter);
            StringAssert.Contains("환영", tutorial.CurrentText);
            Assert.IsTrue(tutorial.IsBlocking);
            var start = field.PlayerCell;
            yield return HoldStick(field, Direction.Right, () => false, 0.3f);
            Assert.AreEqual(start, field.PlayerCell, "읽는 동안은 걷지 않음");
            tutorial.PressNext();
            yield return null;

            // 2/7 걷기: 따라 하기 — 두 칸 걸으면 넘어감
            Assert.AreEqual("2 / 8", tutorial.CurrentCounter);
            Assert.IsFalse(tutorial.IsBlocking, "따라 하기 단계는 스틱이 움직임");
            var ring = tutorial.transform.Find("Overlay/Ring").GetComponent<UnityEngine.UI.Image>();
            Assert.IsTrue(ring.enabled, "스틱 자리에 금색 테두리");
            yield return HoldStick(field, Direction.Right, () => tutorial.CurrentCounter != "2 / 8", 5f);
            Assert.GreaterOrEqual(field.PlayerCell.x, start.x + 2);

            // 3/7 달리기: 끝까지 밀면 넘어감
            Assert.AreEqual("3 / 8", tutorial.CurrentCounter);
            yield return HoldStick(field, Direction.Left, () => tutorial.CurrentCounter != "3 / 8", 5f, run: true);
            Assert.AreEqual("4 / 8", tutorial.CurrentCounter, "[확인] 버튼 안내");
            for (int i = 4; i < 7; i++)
            {
                tutorial.PressNext();
                yield return null;
            }
            Assert.AreEqual("7 / 8", tutorial.CurrentCounter);
            StringAssert.Contains("자동으로 저장", tutorial.CurrentText, "자동 저장·설정 안내");
            tutorial.PressNext();
            yield return null;
            Assert.AreEqual("8 / 8", tutorial.CurrentCounter);
            Assert.AreEqual("시작하기", UiKit.LabelOf(FindButton(tutorial.transform, "TutorialNextButton")).text);
            tutorial.PressNext();
            yield return null;

            Assert.IsFalse(tutorial.IsShowing);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Field), "본 것으로 저장");
            Assert.IsFalse(session.Tutorials.Has(TutorialProgress.Skill), "전투 안내는 전투에서");
            Object.Destroy(field.gameObject);
            yield return null;

            // 다시 들어와도 안 뜬다
            var again = CreateField(session);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(again.Tutorial.IsShowing);
            Object.Destroy(again.gameObject);
            yield return null;
        }

        // 설정 > [튜토리얼 다시 보기]: 필드 안내가 처음부터 다시
        [UnityTest]
        public IEnumerator ReplayFromSettingsShowsFieldTutorialAgain()
        {
            var session = NewSession();
            session.Tutorials.MarkAll();
            var field = CreateField(session);
            yield return null;
            yield return null;
            Assert.IsFalse(field.Tutorial.IsShowing);
            var hud = field.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "SettingsButton").onClick.Invoke();
            FindButton(hud.Find("SettingsView"), "TutorialReplayButton").onClick.Invoke();
            yield return WaitFor(() => field.Tutorial.IsShowing, 2f);
            Assert.AreEqual("1 / 8", field.Tutorial.CurrentCounter);
            Assert.IsFalse(session.Tutorials.Has(TutorialProgress.Skill), "전투 안내도 다시");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkipMarksEveryTutorialSeen()
        {
            var session = NewSession();
            var field = CreateField(session);
            yield return null;
            yield return WaitFor(() => field.Tutorial.IsShowing, 2f);
            FindButton(field.Tutorial.transform, "TutorialSkipButton").onClick.Invoke();
            yield return null;
            Assert.IsFalse(field.Tutorial.IsShowing);
            foreach (var id in TutorialProgress.All) Assert.IsTrue(session.Tutorials.Has(id), id);
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 첫 전투: 기술 고르기(따라 하기) → 강도(따라 하기) → 새 단어 카드(따라 하기) → 문제 설명([다음], 그동안 타이머 멈춤) → 오답 설명
        [UnityTest]
        public IEnumerator FirstBattleExplainsSkillIntensityWordQuizAndPenalty()
        {
            var session = NewSession();
            session.Tutorials.Mark(TutorialProgress.Field);
            var field = CreateField(session);
            yield return null;
            yield return null;
            var tutorial = field.Tutorial;
            Assert.IsFalse(tutorial.IsShowing, "필드 안내는 이미 봄");

            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return WaitFor(() => field.Battle.IsRunning, 5f);
            yield return WaitFor(() => tutorial.IsShowing, 5f);
            StringAssert.Contains("기술을 하나 고르세요", tutorial.CurrentText);
            Assert.IsFalse(tutorial.IsBlocking, "기술 버튼은 직접 누름");

            ActiveButton(field.Battle.transform, "SkillButton_0").onClick.Invoke();
            yield return WaitFor(() => tutorial.CurrentText.Contains("강도"), 3f);
            StringAssert.Contains("하나라도 틀리면", tutorial.CurrentText);
            ActiveButton(field.Battle.transform, "Intensity_0").onClick.Invoke();

            yield return WaitFor(() => tutorial.CurrentText.Contains("처음 만난 단어"), 3f);
            ActiveButton(field.Battle.transform, "CardConfirmButton").onClick.Invoke();

            yield return WaitFor(() => tutorial.CurrentText.Contains("알맞은 답"), 3f);
            Assert.IsTrue(tutorial.IsBlocking, "문제 설명은 [다음]으로");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(int.MinValue, PickedChoiceOf(field.Battle), "설명 중에는 시간이 가지 않음 (아직 시간 초과 아님)");
            tutorial.PressNext();
            yield return WaitFor(() => ActiveButton(field.Battle.transform, "Choice_0") != null, 3f);

            int wrong = (field.Battle.Engine.CurrentQuestion.CorrectIndex + 1) % 4;
            ActiveButton(field.Battle.transform, $"Choice_{wrong}").onClick.Invoke();
            yield return WaitFor(() => tutorial.CurrentText.Contains("틀리거나"), 5f);
            StringAssert.Contains("오답 노트", tutorial.CurrentText);
            tutorial.PressNext();
            yield return null;

            foreach (var id in new[] { TutorialProgress.Skill, TutorialProgress.Intensity, TutorialProgress.NewWord, TutorialProgress.Quiz, TutorialProgress.Wrong })
                Assert.IsTrue(session.Tutorials.Has(id), id);
            Object.Destroy(field.gameObject);
            yield return null;
        }

        private static int PickedChoiceOf(BattleScreen battle) =>
            (int)typeof(BattleScreen).GetField("pickedChoice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(battle);
    }
}
