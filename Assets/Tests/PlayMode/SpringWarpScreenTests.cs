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
            yield return WaitFor(() => field.CurrentArea == far && !field.IsMoving && field.ToastMessage == "먼 곳", 3f);
            Assert.AreSame(far, field.CurrentArea);
            Assert.AreEqual(new Vector2Int(2, 2), field.PlayerCell, "샘 바로 아래");
            Assert.AreEqual(Direction.Up, field.Facing, "샘을 봄");
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
