using System;
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
    // 1.1 메아리 정글 기믹 화면 (#81): 단어 석판 문제 · 유적 제단(아라) · 동굴 입구 · 레버와 덩굴 다리 · 얕은 강 물살 · 뗏목
    public class JungleScreenTests
    {
        private static FieldArea Area(string map, CompanionData shrineCompanion = null, int shrineWords = 3, params ChestContent[] chests)
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var slime = TestData.Species("slime", new MonsterStats(20, 1, 1), new MonsterStats(0, 0, 0), bite);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(slime, 1, 1, 1) });
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "jungle_screen").Set("displayName", "메아리 정글").Set("map", map).Set("theme", FieldTheme.Jungle)
                .Set("encounters", table).Set("words", words).Set("shrineCompanion", shrineCompanion).Set("shrineWords", shrineWords)
                .Set("chests", new List<ChestContent>(chests));
        }

        private static GameSession Session(FieldArea area, int discovered)
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)), 5);
            session.Record.RecordBattle(true, 1, 0);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass }) session.Tutorials.Mark(id);
            for (int i = 0; i < discovered; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, DateTime.UtcNow);
            return session;
        }

        private static FieldScreen Field(FieldArea area, GameSession session, bool tutorials = false)
        {
            var field = new GameObject("JungleFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: tutorials);
            return field;
        }

        //  y=6  #UUUUUUUU#
        //  y=5  #U;;A;;TU#   유적 제단 (4,5) · 단어 석판 (7,5)
        //  y=4  #U;;;;;;U#
        //  y=3  #UUU;;UUU#   유적 입구 (4,3)(5,3)
        //  y=2  #::::::O:#   동굴 입구 (7,2) — 출입구 (연결 없음, 그림만)
        //  y=1  #P:::::::#   시작 (1,1)
        private const string RuinsMap =
            "##########\n" +
            "#UUUUUUUU#\n" +
            "#U;;A;;TU#\n" +
            "#U;;;;;;U#\n" +
            "#UUU;;UUU#\n" +
            "#::::::O:#\n" +
            "#P:::::::#\n" +
            "##########";

        private static CompanionData Ara() =>
            ScriptableObject.CreateInstance<CompanionData>()
                .Set("companionId", "ara").Set("displayName", "아라")
                .Set("baseStats", new MonsterStats(50, 10, 10)).Set("growthPerLevel", new MonsterStats(6, 2, 2))
                .Set("basicSkill", TestData.Skill("ara_pebble", SkillKind.Damage, SkillTarget.SingleEnemy, 12));

        private static IEnumerator WalkToTabletRoom(FieldScreen field)
        {
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 4);
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 4);
        }

        [UnityTest]
        public IEnumerator TabletAsksThreeWordsAndTurnsGold()
        {
            var area = Area(RuinsMap, Ara());
            var session = Session(area, 6);
            var field = Field(area, session);
            yield return null;
            yield return WalkToTabletRoom(field);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 7);
            yield return FaceStick(field, Direction.Up);
            Assert.AreEqual(new Vector2Int(7, 4), field.PlayerCell);
            CollectionAssert.Contains(field.VisibleNameTags, "단어 석판");
            yield return PressConfirm(field);
            var view = field.TabletView;
            Assert.IsTrue(view.IsOpen, "석판 문제 창");
            Assert.IsTrue(field.IsPanelOpen, "푸는 동안은 움직이지 않음");
            Assert.AreEqual("석판 1 / 1", view.Counter);
            Assert.AreEqual("문제 1 / 3", view.Progress);
            var question = field.TabletQuestion;
            Assert.AreEqual(question.Prompt, view.Meaning, "새겨진 뜻");
            Assert.AreEqual(TabletQuizView.BlankFor(question.CorrectAnswer), view.Blank, "영단어 글자 수만큼 빈칸");

            int wrong = (question.CorrectIndex + 1) % question.Choices.Count;
            ActiveButton(field.transform, $"TabletChoice_{wrong}").onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.IsOpen, "틀려도 닫히지 않음");
            Assert.AreEqual("문제 1 / 3", view.Progress, "벌점 없이 같은 문제");
            Assert.IsFalse(FindButton(field.transform, $"TabletChoice_{wrong}").interactable, "틀린 선택지만 꺼짐");

            for (int i = 0; i < TabletQuiz.QuestionsPerTablet; i++)
            {
                question = field.TabletQuestion;
                ActiveButton(field.transform, $"TabletChoice_{question.CorrectIndex}").onClick.Invoke();
                // 맞히면 바로 보임 (테스트는 연출이 빨라 다음 프레임엔 다음 문제일 수 있음)
                Assert.AreEqual(question.CorrectAnswer, view.Blank, "빈칸에 금빛 영단어");
                Assert.IsTrue(view.StampVisible, "정답!");
                yield return null;
                yield return WaitFor(() => view.IsComplete || field.TabletQuestion != question, 3f);
            }
            Assert.IsTrue(view.IsComplete, "다 채움");
            Assert.IsTrue(Ruins.IsTabletSolved(area, session.World, new Vector2Int(7, 5)), "푼 석판은 세이브");
            StringAssert.Contains("석판을 모두 채웠다", view.Note);
            ActiveButton(field.transform, "TabletDone").onClick.Invoke();
            yield return null;
            Assert.IsFalse(view.IsOpen);

            yield return new WaitForSecondsRealtime(0.6f);
            yield return PressConfirm(field);
            Assert.IsFalse(view.IsOpen, "푼 석판은 다시 열리지 않음");
            StringAssert.Contains("이미 빈칸을 채운 석판", field.Bubble.Text);
            UnityEngine.Object.Destroy(field.gameObject);
        }

        [UnityTest]
        public IEnumerator TabletNeedsWordsYouHaveSeen()
        {
            var area = Area(RuinsMap, Ara());
            var session = Session(area, 0);
            var field = Field(area, session);
            yield return null;
            yield return WalkToTabletRoom(field);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 7);
            yield return FaceStick(field, Direction.Up);
            yield return PressConfirm(field);
            Assert.IsFalse(field.TabletView.IsOpen, "본 단어가 없으면 문제 대신 혼잣말");
            StringAssert.Contains("아직 읽을 수 있는 글자가 없다", field.Bubble.Text);

            // 한 번 열고 ✕로 그만두면 저장 안 함
            session.Vocabulary.Discover(area.Words.Words[0].Id, DateTime.UtcNow);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return PressConfirm(field);
            Assert.IsTrue(field.TabletView.IsOpen);
            ActiveButton(field.transform, "TabletClose").onClick.Invoke();
            yield return null;
            Assert.IsFalse(field.TabletView.IsOpen);
            Assert.AreEqual(0, Ruins.TabletsSolved(area, session.World), "그만두면 처음부터");
            UnityEngine.Object.Destroy(field.gameObject);
        }

        [UnityTest]
        public IEnumerator ShrineWakesTheCompanionWhenTabletsAndWordsAreReady()
        {
            var area = Area(RuinsMap, Ara(), shrineWords: 4);
            var session = Session(area, 2);
            var field = Field(area, session);
            yield return null;
            Assert.AreEqual(2, field.PropCount, "잠든 석상 + 동굴 입구 (칸보다 큰 그림 — 'O'는 1.2부터 출입구, #115)");

            yield return WalkToTabletRoom(field);
            yield return FaceStick(field, Direction.Up);
            Assert.AreEqual(new Vector2Int(4, 4), field.PlayerCell);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return PressConfirm(field);
            StringAssert.Contains("석판의 빈칸을 먼저 채워야 할 것 같다", field.Bubble.Text);
            StringAssert.Contains("0 / 1", field.Bubble.Text);

            Ruins.SolveTablet(area, session.World, new Vector2Int(7, 5));
            yield return new WaitForSecondsRealtime(0.4f);
            yield return PressConfirm(field);
            StringAssert.Contains("메아리가 아직 약하다", field.Bubble.Text);
            StringAssert.Contains("2 / 4", field.Bubble.Text);

            for (int i = 2; i < 4; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, DateTime.UtcNow);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return PressConfirm(field);
            yield return WaitFor(() => field.CompanionCutscene.ShowsCard, 5f);
            Assert.IsTrue(field.CompanionCutscene.ShowsCard, "동료를 얻었다!");
            ActiveButton(field.transform, "CompanionJoinButton").onClick.Invoke();
            yield return WaitFor(() => !field.CompanionCutscene.IsPlaying, 3f);
            yield return null;
            Assert.AreEqual("아라", session.PartyCompanion?.DisplayName, "아라가 파티에");
            Assert.AreEqual(ShrineState.Awakened, Ruins.State(area, session));
            Assert.AreEqual(1, field.PropCount, "석상이 사라지고 동굴만");
            UnityEngine.Object.Destroy(field.gameObject);
        }

        //  y=4  #::::::::#   건너편
        //  y=3  #~~~HH~~~#   골짜기 + 덩굴 다리 (4,3)(5,3)
        //  y=2  #::Y:::::#   레버 (3,2)
        //  y=1  #P:::::::#   시작 (1,1)
        private const string LeverMap =
            "##########\n" +
            "#::::::::#\n" +
            "#~~~HH~~~#\n" +
            "#::Y:::::#\n" +
            "#P:::::::#\n" +
            "##########";

        [UnityTest]
        public IEnumerator LeverGrowsTheVineBridge()
        {
            var area = Area(LeverMap);
            var session = Session(area, 0);
            var field = Field(area, session);
            yield return null;
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 4);
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 2);
            yield return HoldStick(field, Direction.Up, () => field.IsMoving, 0.3f);
            Assert.AreEqual(new Vector2Int(4, 2), field.PlayerCell, "다리를 놓기 전엔 못 건넘");

            yield return FaceStick(field, Direction.Left);
            CollectionAssert.Contains(field.VisibleNameTags, "레버");
            yield return PressConfirm(field);
            Assert.AreEqual(1, field.LeversPulled);
            yield return WaitFor(() => !field.IsInBattle && !field.IsCameraPanning, 5f);
            Assert.IsTrue(Levers.IsBridgeBuilt(area, session.World, new Vector2Int(4, 3)), "다리가 놓임 (세이브)");
            StringAssert.Contains("덩굴 다리가 놓였다", field.ToastMessage);

            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 4);
            Assert.AreEqual(new Vector2Int(4, 4), field.PlayerCell, "다리를 건너 건너편으로");

            yield return HoldStick(field, Direction.Down, () => field.PlayerCell.y <= 2);
            yield return FaceStick(field, Direction.Left);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return PressConfirm(field);
            Assert.AreEqual(1, field.LeversPulled, "한 번만");
            StringAssert.Contains("이미 당긴 레버", field.ToastMessage);
            UnityEngine.Object.Destroy(field.gameObject);
        }

        //  y=3  #:::::#~~C~#   강 가운데 모래톱 상자 (9,3)
        //  y=2  #>>>>>J~~~~#   얕은 강(→) · 나루터 (6,2)
        //  y=1  #P::::#~~~~#   시작 (1,1)
        private const string RiverMap =
            "############\n" +
            "#:::::#~~C~#\n" +
            "#>>>>>J~~~~#\n" +
            "#P::::#~~~~#\n" +
            "############";

        [UnityTest]
        public IEnumerator StreamSpeedsYouUpAndTheRaftSailsTheRiver()
        {
            var potion = TestData.Item("potion").Set("displayName", "상처약");
            var area = Area(RiverMap, null, 3, new ChestContent(potion, 2));
            var session = Session(area, 0);
            session.AddCompanion(Ara());
            var field = Field(area, session, tutorials: true);
            yield return null;
            Assert.AreEqual(1, field.MooredRaftsVisible, "나루터에 묶인 뗏목");

            // 얕은 강: 물살(→)을 따라가면 빨라짐, 거스르면 보통
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 2);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 2);
            Assert.AreEqual(0.05f * 0.55f, field.LastStepSeconds, 0.001f, "물살을 따라 → 빨라짐");
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 1);
            Assert.AreEqual(0.05f, field.LastStepSeconds, 0.001f, "거슬러 가면 보통 속도");
            yield return WaitFor(() => field.StreamRipples > 0, 1f);
            Assert.Greater(field.StreamRipples, 0, "물결이 흘러감");

            // 나루터 → 뗏목
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 6);
            Assert.AreEqual(new Vector2Int(6, 2), field.PlayerCell);
            StringAssert.Contains("물 쪽으로 가면 탈 수 있을 것 같다", field.Bubble.Text);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 8);
            Assert.IsTrue(field.IsRafting, "강으로 → 뗏목");
            Assert.IsTrue(field.IsRaftVisible);
            Assert.AreEqual(0, field.MooredRaftsVisible, "타는 동안 묶인 뗏목은 숨김");
            Assert.AreEqual(0.05f * 0.8f, field.LastStepSeconds, 0.001f, "뗏목은 조금 빠름");
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Raft));
            yield return null;
            Assert.Less((field.Follower.Position - new Vector3(8.5f, 2.5f, 0f)).magnitude, 1.6f, "아라도 같은 뗏목 위");

            // 모래톱 상자는 뗏목에서 [확인]
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3);
            yield return FaceStick(field, Direction.Right);
            yield return PressConfirm(field);
            Assert.AreEqual(2, session.Inventory.GetCount(potion), "뗏목에서 상자를 염");
            Assert.IsTrue(session.World.Rafting, "강 위 위치도 세이브");

            // 땅에는 못 올라감 → 나루터로 내림
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 7);
            yield return HoldStick(field, Direction.Left, () => field.IsMoving, 0.3f);
            Assert.AreEqual(new Vector2Int(7, 3), field.PlayerCell, "땅(벽)으론 못 감");
            Assert.IsTrue(field.IsRafting);
            yield return HoldStick(field, Direction.Down, () => field.PlayerCell.y <= 2);
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 6);
            Assert.AreEqual(new Vector2Int(6, 2), field.PlayerCell);
            Assert.IsFalse(field.IsRafting, "나루터에 닿으면 내림");
            Assert.AreEqual(1, field.MooredRaftsVisible);
            UnityEngine.Object.Destroy(field.gameObject);
        }
    }
}
