using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
    // 성유물 안내 (#142): 가방 빨간 점 → 누르면 한 줄씩 · 강화 재료가 모이면 제단 앞으로 옮겨 설명
    public class RelicGuideScreenTests
    {
        //   y=4  #########
        //   y=3  #...E...#   ← 제단 (4,3)
        //   y=2  #.......#   ← 제단 앞 칸 (4,2)
        //   y=1  #P......#   ← 시작 (1,1)
        private const string CampMap = "#########\n#...E...#\n#.......#\n#P......#\n#########";

        private static readonly MonsterStats NoBonus = new MonsterStats(0, 0, 0);

        private static RelicData Relic(string id, ItemData material = null)
        {
            var skill = TestData.Skill(id + "_skill", SkillKind.Damage, SkillTarget.SingleEnemy, 5).Set("displayName", id + " 기술");
            return TestData.Relic(id, skill, NoBonus, material);
        }

        private static GameSession Session(params RelicData[] relics)
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(999, 5, 10), relics), 1);
            // 이 테스트와 상관없는 안내는 본 것으로
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.GrassFight, TutorialProgress.Goal, TutorialProgress.RelicSkills })
                session.Tutorials.Mark(id);
            session.Record.RecordBattle(true, 1, 0);
            return session;
        }

        private static FieldArea Area(string id, string map)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("enemy", new MonsterStats(400, 1, 50), NoBonus, bite);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", id).Set("regionName", id);
            words.ReplaceWords(TestData.SampleWords());
            return ScriptableObject.CreateInstance<FieldArea>().Set("areaId", id).Set("displayName", id).Set("map", map)
                .Set("encounters", table).Set("words", words);
        }

        private static FieldScreen CreateField(FieldArea area, GameSession session)
        {
            var field = new GameObject("RelicGuideFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, () => { }, step: 0.05f, animScale: 0.01f, tutorials: true);
            return field;
        }

        private static Transform Hud(FieldScreen field) => field.transform.Find("FieldHud/SafeArea");

        // 주인공 탭에도 같은 이름의 칸이 있어서 성유물 탭에서 찾음
        private static Transform Page(Transform hud)
        {
            foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "RelicPage") return rect;
            return null;
        }

        private static Button Cell(Transform hud, int index)
        {
            foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == $"RelicCell_{index}") return rect.Find("Back").GetComponent<Button>();
            return null;
        }

        // 끼우기와 기술 넣기는 다른 일 (#147 사용자): 새 성유물을 고른 뒤 처음 한 번 설명. 글을 눌러야 넘어감
        [UnityTest]
        public IEnumerator RelicGuideExplainsEquippingAndPlacingSkills()
        {
            var starter = Relic("starter");
            var session = Session(starter);
            session.Tutorials.Clear();
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.GrassFight, TutorialProgress.Goal })
                session.Tutorials.Mark(id);
            var found = Relic("found");
            session.GrantRelic(found);
            session.Tutorials.Mark(MenuBadges.Bag);
            var field = CreateField(Area("camp", CampMap), session);
            yield return null;
            yield return null;
            var hud = Hud(field);
            FindButton(hud, "BagButton").onClick.Invoke();
            yield return null;
            FindButton(hud.Find("InventoryView"), "TabRelics").onClick.Invoke();
            yield return null;
            Cell(hud, 1).onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(field.PauseTutorial.IsShowing, "설명이 멈춰서 기다림");
            StringAssert.Contains("끼우면 능력치", field.PauseTutorial.Message);
            field.PauseTutorial.PressNext();
            yield return null;
            yield return null;
            StringAssert.Contains("[기술 넣기]", field.PauseTutorial.Message);
            field.PauseTutorial.PressNext();
            yield return null;
            yield return null;
            field.PauseTutorial.PressNext();
            yield return null;
            yield return null;
            Assert.IsFalse(field.PauseTutorial.IsShowing);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.RelicSkills), "다시 나오지 않음");
            Object.Destroy(field.gameObject);
        }

        // 처음 더 얻은 성유물: 빨간 점 → 가방 → 성유물 탭 → 새 성유물 → 바꾸는 곳
        [UnityTest]
        public IEnumerator FirstNewRelicLightsTheBagAndGuidesToIt()
        {
            var starter = Relic("starter");
            var session = Session(starter);
            var found = Relic("found");
            session.GrantRelic(found);
            session.Tutorials.Mark(MenuBadges.Bag);
            var field = CreateField(Area("camp", CampMap), session);
            yield return null;
            yield return null;
            var hud = Hud(field);
            var guide = field.Guide;

            Assert.IsTrue(field.IsBadgeVisible(MenuBadges.RelicNew), "가방을 이미 열어 봤어도 새 성유물엔 빨간 점");
            Assert.IsFalse(guide.IsShowing, "누르기 전엔 안내 없음");

            FindButton(hud, "BagButton").onClick.Invoke();
            yield return null;
            Assert.IsFalse(field.IsBadgeVisible(MenuBadges.RelicNew), "열면 꺼짐");
            StringAssert.Contains("[성유물] 탭", guide.CurrentText);

            FindButton(hud, "TabRelics").onClick.Invoke();
            yield return null;
            yield return null;
            StringAssert.Contains("found", guide.CurrentText);

            Cell(hud, 1).onClick.Invoke();
            yield return null;
            yield return null;
            StringAssert.Contains("가득 차면", guide.CurrentText);

            FindButton(hud, "InventoryCloseButton").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsFalse(guide.IsShowing, "가방을 닫으면 안내도 끝");
            Object.Destroy(field.gameObject);
        }

        // 칸이 가득 차서 못 끼운 성유물: 빨간 점 → 직접 바꿔 끼워 보기 (취소하면 버튼부터 다시)
        [UnityTest]
        public IEnumerator FullSlotsTeachSwappingByDoingIt()
        {
            var session = Session(Relic("a"), Relic("b"), Relic("c"));
            var fourth = session.GrantRelic(Relic("d"));
            Assert.IsFalse(session.Hero.IsEquipped(fourth));
            var field = CreateField(Area("camp", CampMap), session);
            yield return null;
            yield return null;
            var hud = Hud(field);
            var guide = field.Guide;
            Assert.IsTrue(field.IsBadgeVisible(MenuBadges.RelicSwap));

            FindButton(hud, "BagButton").onClick.Invoke();
            yield return null;
            Assert.IsFalse(field.IsBadgeVisible(MenuBadges.RelicNew), "바꾸기를 배우면 앞 단계 안내는 필요 없음");
            Assert.IsFalse(field.IsBadgeVisible(MenuBadges.RelicSwap));
            FindButton(hud, "TabRelics").onClick.Invoke();
            yield return null;
            yield return null;
            StringAssert.Contains("d", guide.CurrentText);

            Cell(hud, 3).onClick.Invoke();
            yield return null;
            yield return null;
            StringAssert.Contains("[바꿔 끼우기]", guide.CurrentText);

            var action = FindButton(hud, "RelicActionButton");
            action.onClick.Invoke();
            yield return null;
            yield return null;
            StringAssert.Contains("바꿀 칸", guide.CurrentText);

            action.onClick.Invoke(); // 취소
            yield return null;
            yield return null;
            StringAssert.Contains("[바꿔 끼우기]", guide.CurrentText, "취소하면 버튼부터");

            action.onClick.Invoke();
            yield return null;
            yield return null;
            FindButton(Page(hud), "EquipSlot_0").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsTrue(session.Hero.IsEquipped(fourth), "바꿔 끼움");
            Assert.IsFalse(guide.IsShowing, "해 보면 끝");
            Object.Destroy(field.gameObject);
        }

        // 강화 재료가 모이면: 설명 → 제단 앞으로 → 설명 → [확인] → 강화
        [UnityTest]
        public IEnumerator EnoughMaterialsBringYouToTheAltarAndExplain()
        {
            var ink = TestData.Item("ink").Set("displayName", "잉크");
            var relic = Relic("quill", ink);
            var session = Session(relic);
            session.Inventory.Add(ink, 1);
            session.Inventory.AddGold(30);
            session.Tutorials.Mark(MenuBadges.Bag);
            var field = CreateField(Area("camp", CampMap), session);
            var hud = (Transform)null;
            yield return WaitFor(() => field.PauseTutorial != null && field.PauseTutorial.IsShowing, 3f);
            hud = Hud(field);
            Assert.AreEqual(1, field.AltarGuidesPlayed);
            StringAssert.Contains("강화 재료가 모였어요", field.PauseTutorial.Message);
            Assert.AreEqual(new Vector2Int(1, 1), field.PlayerCell, "설명을 읽기 전에는 그 자리");

            field.PauseTutorial.PressNext();
            yield return WaitFor(() => field.PlayerCell == new Vector2Int(4, 2) && field.PauseTutorial.IsShowing, 3f);
            Assert.AreEqual(new Vector2Int(4, 2), field.PlayerCell, "제단 앞으로");
            StringAssert.Contains("강화", field.PauseTutorial.Message);
            field.PauseTutorial.PressNext();
            yield return null;
            yield return null;
            StringAssert.Contains("각성", field.PauseTutorial.Message);
            field.PauseTutorial.PressNext();
            yield return WaitFor(() => field.Guide.IsShowing, 2f);
            StringAssert.Contains("[확인]", field.Guide.CurrentText);
            Assert.IsTrue(session.Tutorials.Has(TutorialProgress.Altar), "다시 켜도 또 옮기지 않음");

            yield return new WaitForSecondsRealtime(0.6f); // 창을 닫은 직후의 [확인]은 쉬는 시간
            yield return PressConfirm(field);
            var altar = hud.Find("RelicAltarView").gameObject;
            yield return WaitFor(() => altar.activeSelf, 2f);
            yield return null;
            StringAssert.Contains("[강화!]", field.Guide.CurrentText);

            var upgrade = FindButton(altar.transform, "UpgradeButton_0");
            upgrade.onClick.Invoke();
            upgrade.onClick.Invoke(); // 강화는 두 번 눌러야 함
            yield return null;
            yield return null;
            Assert.AreEqual(1, session.Hero.Find(relic).Level);
            Assert.IsFalse(field.Guide.IsShowing, "강화해 보면 끝");
            Assert.AreEqual(1, field.AltarGuidesPlayed);
            Object.Destroy(field.gameObject);
        }

        // 지금 지역에 제단이 없으면 가장 가까운 제단이 있는 지역으로
        [UnityTest]
        public IEnumerator NoAltarHereTakesYouToTheNearestOne()
        {
            var ink = TestData.Item("ink");
            var session = Session(Relic("quill", ink));
            session.Inventory.Add(ink, 1);
            session.Inventory.AddGold(30);
            session.Tutorials.Mark(MenuBadges.Bag);
            var dungeon = Area("dungeon", "#####\n#P..#\n##D##");
            var camp = Area("camp", CampMap.Replace("#P......#\n#########", "#P......#\n####D####"));
            dungeon.Set("exits", new List<AreaExit> { new AreaExit(camp, 0) });
            camp.Set("exits", new List<AreaExit> { new AreaExit(dungeon, 0) });
            var field = CreateField(dungeon, session);
            yield return WaitFor(() => field.PauseTutorial != null && field.PauseTutorial.IsShowing, 3f);

            field.PauseTutorial.PressNext();
            yield return WaitFor(() => field.CurrentArea == camp && field.PauseTutorial.IsShowing, 3f);
            Assert.AreSame(camp, field.CurrentArea);
            Assert.AreEqual(new Vector2Int(4, 2), field.PlayerCell);
            Object.Destroy(field.gameObject);
        }

        // 재료가 모자라면 옮기지 않음
        [UnityTest]
        public IEnumerator NoWarpWithoutMaterialsOrAfterUpgrading()
        {
            var ink = TestData.Item("ink");
            var session = Session(Relic("quill", ink));
            session.Inventory.AddGold(30);
            session.Tutorials.Mark(MenuBadges.Bag);
            var field = CreateField(Area("camp", CampMap), session);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(0, field.AltarGuidesPlayed, "재료 없음");
            Assert.AreEqual(new Vector2Int(1, 1), field.PlayerCell);
            Object.Destroy(field.gameObject);
        }
    }
}
