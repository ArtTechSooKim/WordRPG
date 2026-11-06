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
    // 샘으로 다른 지역 가기 (#216): [확인] → 회복 + 물음 → [취소]는 그대로 / [확인]은 그 지역 샘 앞으로
    public class SpringWarpScreenTests
    {
        //   y=1  #FP.#
        private const string VillageMap = "#####\n#FP.#\n#####";
        //   y=3  #.F.#   ← 샘 (2,3), 바로 아래 칸에 내림
        //   y=1  #P..#
        private const string FarMap = "#####\n#.F.#\n#...#\n#P..#\n#####";
        private const float AfterPanelSeconds = 0.6f; // 창을 닫은 뒤 0.5초는 [확인]이 막힘 (FieldScreen — 실수로 다시 눌리지 않게)

        private static FieldArea Area(string id, string name, string map, int order)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 5);
            var enemy = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), bite);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "test_" + id).Set("regionName", name);
            words.ReplaceWords(TestData.SampleWords());
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", id).Set("displayName", name).Set("map", map).Set("storyOrder", order)
                .Set("encounters", table).Set("words", words).Set("encounterRate", 0f).Set("minStepsBetweenEncounters", 0);
        }

        private static GameSession NewSession()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            return GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike), 1);
        }

        private static CompanionData Ara()
        {
            var pebble = TestData.Skill("ara_pebble", SkillKind.Damage, SkillTarget.SingleEnemy, 12);
            return ScriptableObject.CreateInstance<CompanionData>()
                .Set("companionId", "ara").Set("displayName", "아라").Set("baseStats", new MonsterStats(50, 10, 10)).Set("basicSkill", pebble)
                .Set("learnedSkills", new List<CompanionSkill>());
        }

        private static void See(GameSession session, FieldArea area)
        {
            var map = area.Map;
            session.World.Reveal(area.AreaId, map.Width, map.Height, SpringWarp.SpringOf(map).Value);
        }

        private static FieldScreen MakeField(FieldArea start, GameSession session, System.Action saved, params FieldArea[] areas)
        {
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], areas);
            var field = new GameObject("SpringUnderTest").AddComponent<FieldScreen>();
            field.Configure(start, session, saved, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            return field;
        }

        [UnityTest]
        public IEnumerator SpringHealsAsksAndTakesYouToTheOtherSpring()
        {
            var village = Area("village", "마을", VillageMap, 1);
            var far = Area("far", "먼 곳", FarMap, 2);
            var session = NewSession();
            See(session, far);
            session.AddCompanion(Ara());
            int saves = 0;
            var field = MakeField(village, session, () => saves++, village, far);
            yield return null;
            yield return null;

            session.Hero.TakeDamage(50);
            yield return FaceStick(field, Direction.Left);
            yield return PressConfirm(field);
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp, "묻는 동안 이미 회복됨");
            Assert.IsTrue(field.SpringDialog.IsOpen);
            Assert.AreEqual("회복의 샘", field.SpringDialog.TitleText);
            StringAssert.Contains("HP가 모두 회복되었습니다.", field.SpringDialog.MessageText);
            StringAssert.DoesNotContain("옛날부터", field.SpringDialog.MessageText, "이야기는 창이 아니라 튜토리얼로");
            StringAssert.Contains("먼 곳의 샘으로 이동하시겠습니까?", field.SpringDialog.MessageText);
            Assert.IsTrue(field.IsPanelOpen, "창이 떠 있는 동안 필드가 멈춤");

            // 뒤로가기·[취소] = 그 자리에 그대로
            field.HandleBack();
            Assert.IsFalse(field.SpringDialog.IsOpen);
            Assert.AreSame(village, field.CurrentArea);

            yield return new WaitForSecondsRealtime(AfterPanelSeconds);
            yield return PressConfirm(field);
            Assert.IsTrue(field.SpringDialog.IsOpen);
            FindButton(field.SpringDialog.Root.transform, "DialogCancelButton").onClick.Invoke();
            Assert.IsFalse(field.SpringDialog.IsOpen);
            Assert.AreSame(village, field.CurrentArea, "[취소]는 그대로");

            // [확인] = 그 지역 샘 앞으로, 샘을 보고 섬
            yield return new WaitForSecondsRealtime(AfterPanelSeconds);
            yield return PressConfirm(field);
            int before = saves;
            FindButton(field.SpringDialog.Root.transform, "DialogConfirmButton").onClick.Invoke();
            // 쏙 들어갔다가 쏙 나옴 (#217): 들어간 뒤에는 잠깐 안 보이고, 동료도 함께
            bool sawDive = false, sawGoneHere = false, sawGoneThere = false;
            yield return WaitFor(() =>
            {
                sawDive |= field.ToastMessage == "샘으로 뛰어들었다!";
                bool gone = field.PlayerScale.x < 0.01f;
                if (field.CurrentArea == village) sawGoneHere |= gone;
                else sawGoneThere |= gone;
                return field.CurrentArea == far && field.ToastMessage == "먼 곳";
            }, 3f);
            Assert.IsTrue(sawDive, "샘으로 뛰어들었다!");
            Assert.IsTrue(sawGoneHere, "떠나는 샘으로 쏙 들어감");
            Assert.IsTrue(sawGoneThere, "도착한 샘에서 쏙 나오기 전");
            Assert.AreSame(far, field.CurrentArea);
            Assert.AreEqual(new Vector2Int(2, 2), field.PlayerCell, "샘 바로 아래");
            Assert.AreEqual(Direction.Down, field.Facing, "샘에서 튀어나온 쪽을 봄");
            Assert.AreEqual(1f, field.PlayerScale.x, 0.001f, "크기는 원래대로");
            Assert.AreEqual(new Vector3(2.5f, 2.5f, 0f), field.PlayerWorld, "칸 가운데에 섬");
            Assert.AreEqual(1f, field.Follower.Root.localScale.x, 0.001f, "동료 크기도 원래대로");
            Assert.AreEqual(new Vector3(1.5f, 2.5f, 0f), field.Follower.Position, "동료는 주인공 옆 (뒤는 샘)");
            Assert.Greater(saves, before, "도착하면 저장");
            Assert.IsTrue(session.World.TryGetPosition("far", out var position));
            Assert.AreEqual(new Vector2Int(2, 2), position);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeveralSpringsOpenAPickerInStoryOrder()
        {
            var village = Area("village", "마을", VillageMap, 1);
            var far = Area("far", "먼 곳", FarMap, 3);
            var near = Area("near", "가까운 곳", FarMap, 2);
            var unseen = Area("unseen", "못 본 곳", FarMap, 4);
            var session = NewSession();
            See(session, far);
            See(session, near);
            var field = MakeField(village, session, () => { }, far, near, unseen, village);
            yield return null;
            yield return null;

            yield return FaceStick(field, Direction.Left);
            yield return PressConfirm(field);
            StringAssert.Contains("다른 지역으로 이동하시겠습니까?", field.SpringDialog.MessageText);
            FindButton(field.SpringDialog.Root.transform, "DialogConfirmButton").onClick.Invoke();
            Assert.IsFalse(field.SpringDialog.IsOpen);
            Assert.IsTrue(field.SpringWarpPicker.IsOpen, "갈 곳이 여럿이면 고르는 창");
            Assert.IsTrue(field.IsPanelOpen);

            var picker = field.SpringWarpPicker.Root.transform;
            var nearRow = FindButton(picker, "SpringWarp_near");
            var farRow = FindButton(picker, "SpringWarp_far");
            Assert.Less(nearRow.transform.GetSiblingIndex(), farRow.transform.GetSiblingIndex(), "이야기 순서");
            Assert.Greater(((RectTransform)nearRow.transform).anchorMin.y, ((RectTransform)farRow.transform).anchorMin.y, "앞 순서가 위에");
            Assert.IsNull(FindButton(picker, "SpringWarp_unseen"), "못 본 샘은 안 나옴");
            Assert.IsNull(FindButton(picker, "SpringWarp_village"), "지금 있는 지역은 안 나옴");

            field.HandleBack();
            Assert.IsFalse(field.SpringWarpPicker.IsOpen, "뒤로가기로 닫힘");
            Assert.AreSame(village, field.CurrentArea);

            yield return new WaitForSecondsRealtime(AfterPanelSeconds);
            yield return PressConfirm(field);
            FindButton(field.SpringDialog.Root.transform, "DialogConfirmButton").onClick.Invoke();
            FindButton(field.SpringWarpPicker.Root.transform, "SpringWarp_far").onClick.Invoke();
            yield return WaitFor(() => field.CurrentArea == far && field.ToastMessage == "먼 곳", 3f);
            Assert.AreSame(far, field.CurrentArea);
            Assert.IsFalse(field.SpringWarpPicker.IsOpen);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 튜토리얼 (#217): 다른 샘을 본 뒤 처음 샘 옆에 서면 한 번. 그전엔 안 나오고, 멀어지면 다음에 다시, [확인]을 누르면 끝
        [UnityTest]
        public IEnumerator GuideAppearsOnceAnotherSpringIsKnown()
        {
            var village = Area("village", "마을", VillageMap, 1);
            var far = Area("far", "먼 곳", FarMap, 2);
            var session = NewSession();
            foreach (var id in TutorialProgress.All)
                if (id != TutorialProgress.Spring) session.Tutorials.Mark(id);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], new[] { village, far });
            var field = new GameObject("SpringGuideUnderTest").AddComponent<FieldScreen>();
            field.Configure(village, session, () => { }, step: 0.05f, animScale: 0.01f, gameDatabase: database, tutorials: true);
            yield return null;
            yield return null;

            // 새로 하는 사람: 첫 샘(마을)에서는 갈 곳이 없어 안 나옴
            yield return FaceStick(field, Direction.Left);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(field.Guide.IsShowing, "다른 샘을 보기 전엔 안 나옴");

            // 다른 샘을 봄 (서고 샘 / 이미 하던 세이브) → 샘 옆에서 안내
            See(session, far);
            yield return WaitFor(() => field.Guide.IsShowing, 2f);
            Assert.AreEqual("[확인]을 눌러 샘에 뛰어들어 보세요", field.Guide.CurrentText);
            StringAssert.Contains("먼 곳 샘이랑 같은 냄새", field.Bubble.Text, "주인공이 스스로 알아챔 (#223)");
            Assert.AreEqual(GuideHints.Gesture.Tap, field.Guide.CurrentGesture);
            Assert.IsFalse(session.Tutorials.Has(TutorialProgress.Spring));
            yield return null;
            var corners = new Vector3[4];
            field.Guide.PillRect.GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].x, 0f, "글이 화면 왼쪽으로 잘리지 않음");
            Assert.LessOrEqual(corners[2].x, Screen.width, "오른쪽으로도");

            // 멀어지면 사라지고 본 것으로 치지 않음 → 돌아오면 다시
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 3);
            yield return WaitFor(() => !field.Guide.IsShowing, 2f);
            Assert.IsFalse(field.Guide.IsShowing, "멀어지면 사라짐");
            Assert.IsFalse(session.Tutorials.Has(TutorialProgress.Spring), "아직 안 해 봄");
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 2);
            yield return FaceStick(field, Direction.Left);
            yield return WaitFor(() => field.Guide.IsShowing, 2f);
            Assert.IsTrue(field.Guide.IsShowing, "돌아오면 다시");

            // [확인]을 누르면 끝 — 창은 뜨고 안내는 다시 안 나옴
            yield return PressConfirm(field);
            Assert.IsTrue(field.SpringDialog.IsOpen);
            yield return WaitFor(() => session.Tutorials.Has(TutorialProgress.Spring), 2f);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Spring));
            Assert.IsFalse(field.Guide.IsShowing);
            FindButton(field.SpringDialog.Root.transform, "DialogCancelButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(AfterPanelSeconds);
            Assert.IsFalse(field.Guide.IsShowing, "한 번 본 안내는 다시 안 나옴");

            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 어르신 컷씬 (#223): 말이 돌아온(보스를 물리친) 어르신 가까이 가면 한 번 — 말하고 샘으로 쓩, 다시 안 나옴
        [UnityTest]
        public IEnumerator ElderLeapsIntoTheSpringOnce()
        {
            //   y=2  #..F..#
            //   y=1  #.N.P.#   ← 어르신 (2,1) · 주인공 (4,1)
            var village = Area("village", "마을", "#######\n#..F..#\n#.N.P.#\n#######", 1);
            var far = Area("far", "먼 곳", FarMap, 2);
            var elder = ScriptableObject.CreateInstance<NpcData>();
            elder.Configure("npc_elder", "어르신", 0, new[] { new NpcStage("……", NpcMotionKind.Stand) });
            elder.SetSpringLeap(far, "샘물은 땅 밑으로 다 이어져 있지.", "얍!");
            village.Set("npcs", new List<NpcData> { elder });
            var session = NewSession();
            See(session, far);

            var field = MakeField(village, session, () => { }, village, far);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1, field.Npcs.Views.Count);
            Assert.IsFalse(session.World.HasEvent(SpringLeap.EventId(elder)), "말이 돌아오기 전(보스 전)에는 안 함");
            Object.Destroy(field.gameObject);
            yield return null;

            session.World.MarkBossDefeated(far.BossId);
            field = MakeField(village, session, () => { }, village, far);
            yield return null;
            yield return null;
            bool sawElderLine = false, sawHeroLine = false;
            yield return WaitFor(() =>
            {
                sawElderLine |= field.Bubble.IsVisible && field.Bubble.Text.Contains("땅 밑으로");
                sawHeroLine |= field.Bubble.IsVisible && field.Bubble.Text.Contains("어르신?!");
                return sawHeroLine;
            }, 5f);
            Assert.IsTrue(sawElderLine, "어르신이 샘 이야기를 함");
            Assert.IsTrue(sawHeroLine, "주인공이 놀람");
            Assert.IsTrue(session.World.HasEvent(SpringLeap.EventId(elder)), "한 번 본 것으로 저장");
            Assert.AreEqual(0, field.Npcs.Views.Count, "샘으로 들어가 사라짐");
            Assert.AreSame(village, field.CurrentArea, "주인공은 그대로");
            Object.Destroy(field.gameObject);
            yield return null;

            // 다시 와도 어르신은 없음
            var again = MakeField(village, session, () => { }, village, far);
            yield return null;
            yield return null;
            Assert.AreEqual(0, again.Npcs.Views.Count, "다시 안 나옴");
            Object.Destroy(again.gameObject);
            yield return null;
        }

        // 갈 곳이 없으면 묻지 않고 예전처럼 회복만
        [UnityTest]
        public IEnumerator NoOtherSpringJustHeals()
        {
            var village = Area("village", "마을", VillageMap, 1);
            var far = Area("far", "먼 곳", FarMap, 2);
            var session = NewSession();
            var field = MakeField(village, session, () => { }, village, far);
            yield return null;
            yield return null;

            session.Hero.TakeDamage(50);
            yield return FaceStick(field, Direction.Left);
            yield return PressConfirm(field);
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp);
            Assert.IsFalse(field.SpringDialog.IsOpen);
            StringAssert.Contains("회복의 샘", field.ToastMessage);

            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
