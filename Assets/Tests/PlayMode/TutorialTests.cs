using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 튜토리얼 (#61 — '글이 너무 많다' 피드백으로 다시 만듦): 화면을 막지 않는 한 줄 안내.
    // 걷기·달리기는 손끝이 끄는 모습, 물건 옆에서는 [확인]을 톡톡, 풀숲 근처 혼잣말, 메뉴 빨간 점, 목표 한 줄, '✓ 저장',
    // 첫 전투는 기술 고르기·새 단어·첫 문제를 한 줄씩. 강도 고르기는 전투를 몇 번 해 본 뒤에 열린다
    public class TutorialTests
    {
        //   y=3  #,,,,,,#    ← 풀숲 (조우 100%)
        //   y=2  #......#
        //   y=1  #P....C#    ← 시작 (1,1), 보물상자 (6,1)
        private const string Map = "########\n#,,,,,,#\n#......#\n#P....C#\n########";

        private int saves;

        private FieldScreen CreateField(GameSession session, int enemyHp = 400)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("enemy", new MonsterStats(enemyHp, 1, 50), new MonsterStats(0, 0, 0), bite);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "test").Set("regionName", "테스트");
            words.ReplaceWords(TestData.SampleWords());
            var ink = TestData.Item("shiny_ink").Set("displayName", "빛나는 잉크");
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "test").Set("displayName", "테스트 들판").Set("map", Map)
                .Set("encounters", table).Set("words", words)
                .Set("encounterRate", 1f).Set("minStepsBetweenEncounters", 0)
                .Set("chests", new List<ChestContent> { new ChestContent(ink, 1) });
            var field = new GameObject("TutorialFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, () => saves++, step: 0.05f, animScale: 0.01f, tutorials: true);
            return field;
        }

        private static GameSession NewSession()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 5).Set("displayName", "휘두르기");
            var hero = TestData.Hero(new MonsterStats(999, 5, 10)).Set("basicSkill", strike);
            return GameSession.NewGame(hero, 1);
        }

        // 걷기·달리기: 손끝이 끄는 모습 + 한 줄. 막지 않아서 바로 걸을 수 있고, 해 보면 넘어간다
        [UnityTest]
        public IEnumerator FieldGuideShowsWalkAndRunWithoutBlocking()
        {
            var session = NewSession();
            var field = CreateField(session);
            yield return null;
            var guide = field.Guide;
            yield return WaitFor(() => guide.IsShowing, 2f);

            StringAssert.Contains("끌어서 이동", guide.CurrentText);
            Assert.LessOrEqual(guide.CurrentText.Length, 16, "글은 한 줄");
            Assert.AreEqual(GuideHints.Gesture.Drag, guide.CurrentGesture);
            Assert.IsTrue(guide.GhostVisible, "손끝이 끄는 모습");
            // 안내는 스틱 받침 바로 위에 (#65 기기 확인: 넓은 스틱 영역 기준이라 너무 멀었음)
            yield return null;
            var baseCorners = new Vector3[4];
            var pillCorners = new Vector3[4];
            field.Stick.Base.GetWorldCorners(baseCorners);
            guide.PillRect.GetWorldCorners(pillCorners);
            float scale = guide.PillRect.lossyScale.y;
            float gap = (pillCorners[0].y - baseCorners[1].y) / scale;
            Assert.That(gap, Is.InRange(0f, 120f), $"안내와 스틱 받침 사이 {gap}");
            float pillCenterX = (pillCorners[0].x + pillCorners[2].x) * 0.5f, baseCenterX = (baseCorners[0].x + baseCorners[2].x) * 0.5f;
            Assert.Less(Mathf.Abs(pillCenterX - baseCenterX) / scale, 260f, "안내가 받침 위쪽 가운데 근처");
            var ghostWorld = guide.GhostRect.position;
            Assert.Less(Vector2.Distance(ghostWorld, (baseCorners[0] + baseCorners[2]) * 0.5f) / scale, 260f, "손끝은 받침에서 출발");
            Assert.IsNull(guide.GetComponent<UnityEngine.UI.GraphicRaycaster>(), "안내는 눌림을 막지 않음");

            var start = field.PlayerCell;
            yield return HoldStick(field, Direction.Right, () => !guide.CurrentText.Contains("끌어서"), 5f);
            Assert.GreaterOrEqual(field.PlayerCell.x, start.x + 2, "안내가 떠 있어도 바로 걸음");
            StringAssert.Contains("끝까지 밀면 달리기", guide.CurrentText);

            yield return HoldStick(field, Direction.Left, () => !guide.IsShowing, 5f, run: true);
            Assert.IsFalse(guide.IsShowing);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Field), "본 것으로 저장");
            Assert.Greater(saves, 0);
            Object.Destroy(field.gameObject);
            yield return null;

            var again = CreateField(session);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(again.Guide.IsShowing, "다시 들어와도 안 뜸");
            Object.Destroy(again.gameObject);
            yield return null;
        }

        // 물건 옆에 처음 서면 [확인]을 톡톡 누르는 손끝 + 한 줄 → 써 보면 끝
        [UnityTest]
        public IEnumerator ConfirmHintShowsNextToTheFirstChest()
        {
            var session = NewSession();
            session.Tutorials.Mark(TutorialProgress.Field);
            var field = CreateField(session);
            yield return null;
            yield return null;
            Assert.IsFalse(field.Guide.IsShowing, "걷기 안내는 이미 봄");

            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 5, 5f);
            Assert.AreEqual(new Vector2Int(5, 1), field.PlayerCell);
            yield return WaitFor(() => field.Guide.IsShowing, 2f);
            StringAssert.Contains("[확인]", field.Guide.CurrentText);
            Assert.AreEqual(GuideHints.Gesture.Tap, field.Guide.CurrentGesture, "[확인]을 톡톡");

            yield return PressConfirm(field);
            yield return WaitFor(() => !field.Guide.IsShowing, 2f);
            Assert.IsTrue(session.World.IsChestOpened("test:6,1"));
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Confirm));
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 풀숲 혼잣말 · 목표 한 줄 · 첫 전투 뒤 [도감] 빨간 점 → 열면 꺼짐 · '✓ 저장'
        [UnityTest]
        public IEnumerator GrassLineObjectiveBadgeAndSaveMark()
        {
            var session = NewSession();
            session.Tutorials.Mark(TutorialProgress.Field);
            session.Tutorials.Mark(TutorialProgress.Skill);
            session.Tutorials.Mark(TutorialProgress.NewWord);
            session.Tutorials.Mark(TutorialProgress.Quiz);
            var field = CreateField(session, enemyHp: 5);
            yield return null;
            yield return null;

            Assert.AreEqual("풀숲에서 몬스터를 만나 보자", field.ObjectiveText, "처음 목표");
            foreach (var id in MenuBadges.All) Assert.IsFalse(field.IsBadgeVisible(id), $"처음엔 {id} 점 없음");

            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return WaitFor(() => field.Bubble.IsVisible, 2f);
            StringAssert.Contains("풀숲", field.Bubble.Text, "풀숲 근처에서 혼잣말");
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Grass));

            yield return HoldStick(field, Direction.Up, () => field.Battle.IsRunning, 3f);
            yield return WaitFor(() => field.Battle.IsRunning, 3f);
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle, 3f);

            Assert.IsTrue(field.IsBadgeVisible(MenuBadges.Dex), "단어를 처음 만나면 [도감]에 빨간 점");
            StringAssert.StartsWith("도감 채우기", field.ObjectiveText, "보스가 없는 지역이면 도감 채우기");
            Assert.Greater(field.SaveMarksShown, 0, "저장될 때 '✓ 저장'");

            FindButton(field.transform, "DexButton").onClick.Invoke();
            Assert.IsFalse(field.IsBadgeVisible(MenuBadges.Dex), "한 번 열면 꺼짐");
            Assert.IsTrue(session.Tutorials.Has(MenuBadges.Dex));
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 설정 > [튜토리얼 다시 보기]: 걷기 안내부터 다시
        [UnityTest]
        public IEnumerator ReplayFromSettingsShowsTheWalkGuideAgain()
        {
            var session = NewSession();
            session.Tutorials.MarkAll();
            var field = CreateField(session);
            yield return null;
            yield return null;
            Assert.IsFalse(field.Guide.IsShowing);
            var hud = field.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "SettingsButton").onClick.Invoke();
            FindButton(hud.Find("SettingsView"), "TutorialReplayButton").onClick.Invoke();
            yield return WaitFor(() => field.Guide.IsShowing, 2f);
            StringAssert.Contains("끌어서 이동", field.Guide.CurrentText);
            Assert.IsFalse(session.Tutorials.Has(TutorialProgress.Skill), "전투 안내도 다시");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 첫 전투: 기술 버튼을 톡톡(한 줄) → 처음엔 강도 고르기 없이 바로 단어 → 새 단어 [확인] 한 줄 → 첫 문제 한 줄(시간은 흐름).
        // 틀려도 설명 창 없이 오답 카드만
        [UnityTest]
        public IEnumerator FirstBattleUsesOneLinersAndSkipsIntensity()
        {
            var session = NewSession();
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass }) session.Tutorials.Mark(id);
            var field = CreateField(session);
            yield return null;
            yield return null;
            var guide = field.Guide;

            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.Battle.IsRunning, 3f);
            yield return WaitFor(() => field.Battle.IsRunning, 5f);
            yield return WaitFor(() => guide.IsShowing, 5f);
            StringAssert.Contains("기술을 고르면", guide.CurrentText);
            Assert.AreEqual(GuideHints.Gesture.Tap, guide.CurrentGesture);

            ActiveButton(field.Battle.transform, "SkillButton_0").onClick.Invoke();
            yield return WaitFor(() => guide.CurrentText.Contains("[확인]"), 3f);
            Assert.IsNull(ActiveButton(field.Battle.transform, "Intensity_0"), "처음엔 강도 고르기 없이 바로 단어");
            StringAssert.Contains("뜻을 기억하고", guide.CurrentText);
            ActiveButton(field.Battle.transform, "CardConfirmButton").onClick.Invoke();

            yield return WaitFor(() => guide.CurrentText.Contains("크리티컬"), 3f);
            StringAssert.Contains("빨리 맞히면 크리티컬", guide.CurrentText);
            yield return WaitFor(() => ActiveButton(field.Battle.transform, "Choice_0") != null, 3f);
            int wrong = (field.Battle.Engine.CurrentQuestion.CorrectIndex + 1) % 4;
            ActiveButton(field.Battle.transform, $"Choice_{wrong}").onClick.Invoke();
            yield return WaitFor(() => ActiveButton(field.Battle.transform, "CardConfirmButton") != null, 3f);
            Assert.IsFalse(guide.IsShowing && guide.CurrentText.Contains("틀리"), "틀려도 설명 창 없음 — 오답 카드가 알려 줌");

            foreach (var id in new[] { TutorialProgress.Skill, TutorialProgress.NewWord, TutorialProgress.Quiz })
                Assert.IsTrue(session.Tutorials.Has(id), id);
            Assert.IsFalse(session.Tutorials.Has(TutorialProgress.Intensity), "강도는 아직");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 전투를 두 번 해 보면 강도 고르기가 열리고 그때 한 줄 안내
        [UnityTest]
        public IEnumerator IntensityOpensAfterTwoBattles()
        {
            var session = NewSession();
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Skill,
                         TutorialProgress.NewWord, TutorialProgress.Quiz })
                session.Tutorials.Mark(id);
            session.Record.RecordBattle(true, 1, 0);
            session.Record.RecordBattle(true, 1, 0);
            var field = CreateField(session);
            yield return null;
            yield return null;

            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.Battle.IsRunning, 3f);
            yield return WaitFor(() => ActiveButton(field.Battle.transform, "SkillButton_0") != null, 5f);
            ActiveButton(field.Battle.transform, "SkillButton_0").onClick.Invoke();
            yield return WaitFor(() => ActiveButton(field.Battle.transform, "Intensity_0") != null, 3f);
            Assert.IsNotNull(ActiveButton(field.Battle.transform, "Intensity_0"), "강도 고르기 열림");
            yield return WaitFor(() => field.Guide.IsShowing, 2f);
            StringAssert.Contains("새로 열림", field.Guide.CurrentText);
            ActiveButton(field.Battle.transform, "Intensity_1").onClick.Invoke();
            yield return WaitFor(() => session.Tutorials.Has(TutorialProgress.Intensity), 2f);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Intensity));
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
