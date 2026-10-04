using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 마을: 상점 앞에서 [확인] → 상처약 구매 → 성유물 제단 앞에서 [확인] → 깃펜 +2 → +3 강화 (각성 연출)
    public class TownScreenTests
    {
        //   y=3  #####
        //   y=2  #ES.#   ← 성유물 제단 (1,2), 상점 (2,2)
        //   y=1  #.P.#   ← 시작 (2,1)
        //   y=0  #####
        private const string TownMap = "#####\n#ES.#\n#.P.#\n#####";

        [UnityTest]
        public IEnumerator BuyPotionThenUpgradeRelic()
        {
            var ink = TestData.Item("shiny_ink").Set("displayName", "빛나는 잉크");
            var potion = TestData.Potion("potion", 40).Set("displayName", "상처약");
            var splash = TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 14).Set("displayName", "잉크 뿌리기");
            var storm = TestData.Skill("storm", SkillKind.Damage, SkillTarget.AllEnemies, 22).Set("displayName", "잉크 폭풍");
            var quill = TestData.Relic("relic_quill", splash, new MonsterStats(0, 4, 0), ink, storm).Set("displayName", "깃펜")
                .Set("bonusPerLevel", new MonsterStats(0, 2, 0));
            var enemy = TestData.Species("slime", new MonsterStats(20, 5, 5), new MonsterStats(0, 0, 0), splash);
            var shop = ScriptableObject.CreateInstance<ShopData>()
                .Set("displayName", "테스트 상점")
                .Set("entries", new List<ShopEntry> { new ShopEntry(potion, 30) });
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) });
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "town").Set("displayName", "마을").Set("map", TownMap)
                .Set("encounters", table).Set("words", words).Set("shop", shop);

            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 5);
            session.Hero.RestoreRelic(quill, 2, 0); // 깃펜 +2 (다음 강화에서 각성)
            session.Inventory.Add(ink, 2);
            session.Inventory.AddGold(120);
            int saves = 0;

            var go = new GameObject("TownUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, onSave: () => saves++, step: 0.05f, animScale: 0.01f);
            yield return null;
            yield return null;
            var shopView = go.transform.Find("FieldHud/SafeArea/ShopView").gameObject;
            var altarView = go.transform.Find("FieldHud/SafeArea/RelicAltarView").gameObject;

            // 가까이 있는 제단·상점 위에 이름표 (상점은 상점 이름)
            CollectionAssert.AreEquivalent(new[] { "성유물 제단", "테스트 상점" }, field.VisibleNameTags.ToList());

            // 1) 아래(벽)를 보고 있어도 [확인]은 옆 칸의 상점을 찾아 연다
            Assert.AreEqual(Direction.Down, field.Facing);
            Assert.IsTrue(field.ConfirmReady);
            yield return PressConfirm(field);
            yield return WaitFor(() => shopView.activeSelf, 2f);
            Assert.IsTrue(shopView.activeSelf, "상점 앞에서 [확인] → 상점 화면");
            StringAssert.Contains("테스트 상점", AllText(shopView.transform));

            // 2) 상처약 구매: 120G → 90G
            FindButton(shopView.transform, "BuyButton_0").onClick.Invoke();
            Assert.AreEqual(90, session.Inventory.Gold);
            Assert.AreEqual(1, session.Inventory.GetCount(potion));
            Assert.Greater(saves, 0, "구매하면 저장");
            FindButton(shopView.transform, "ShopCloseButton").onClick.Invoke();

            // 3) 왼쪽으로 한 칸 → 위 = 성유물 제단. 부딪히기만 하면 안 열리고 [확인]으로 열림
            yield return WaitFor(() => !field.IsPanelOpen);
            yield return new WaitForSecondsRealtime(0.6f); // 창을 닫은 직후 대기 시간
            yield return HoldStick(field, Direction.Left, () => field.IsMoving);
            Assert.AreEqual(new Vector2Int(1, 1), field.PlayerCell);
            yield return FaceStick(field, Direction.Up);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsFalse(altarView.activeSelf, "부딪히기만 해서는 안 열림");
            yield return PressConfirm(field);
            yield return WaitFor(() => altarView.activeSelf, 2f);
            Assert.IsTrue(altarView.activeSelf, "제단 앞에서 [확인] → 성유물 제단");
            StringAssert.Contains("각성! 잉크 폭풍", AllText(altarView.transform));

            // 4) 강화는 두 번 눌러야 함: +2 → +3 (잉크 2개 + 90G) → 각성
            var upgrade = FindButton(altarView.transform, "UpgradeButton_0");
            Assert.IsTrue(upgrade.interactable);
            upgrade.onClick.Invoke();
            Assert.AreEqual(2, session.Hero.Find(quill).Level, "첫 번째 누름은 확인만");
            StringAssert.Contains("+3으로 강화할까요", AllText(altarView.transform));
            upgrade.onClick.Invoke();

            var relic = session.Hero.Find(quill);
            Assert.AreEqual(3, relic.Level);
            Assert.AreSame(storm, relic.Skill, "각성 → 잉크 폭풍");
            Assert.AreEqual(0, session.Inventory.GetCount(ink));
            Assert.AreEqual(0, session.Inventory.Gold);
            CollectionAssert.Contains(session.Hero.Skills, storm);

            // 각성 연출: 단어가 반짝 → 글자 고리 → 각성 성공! (보너스 변화 + 강해진 기술) → [좋아요!]로 닫힘
            var cutscene = altarView.transform.Find("AwakeningCutscene").gameObject;
            Assert.IsTrue(cutscene.activeSelf, "각성하면 연출 시작");
            yield return WaitFor(() => ActiveButton(cutscene.transform, "AwakeningOkButton") != null, 5f);
            var scene = AllText(cutscene.transform);
            StringAssert.Contains("각성 성공", scene);
            StringAssert.Contains("+3으로 각성했다", scene);
            StringAssert.DoesNotContain("어라", scene, "다른 게임 대사를 쓰지 않음");
            StringAssert.Contains("잉크 폭풍", scene);
            FindButton(cutscene.transform, "AwakeningOkButton").onClick.Invoke();
            Assert.IsFalse(cutscene.activeSelf);
            Assert.IsTrue(altarView.activeSelf, "연출이 끝나면 제단 화면으로");
            Assert.IsFalse(upgrade.interactable, "재료가 없어 다음 강화는 잠김");
            StringAssert.Contains("재료가 부족해요", AllText(altarView.transform));

            FindButton(altarView.transform, "RelicAltarCloseButton").onClick.Invoke();
            yield return null;
            StringAssert.Contains("+3", AllText(go.transform.Find("FieldHud/SafeArea/HeroStrip")), "HUD 성유물 칸에 강화 단계");

            Object.Destroy(go);
            yield return null;
        }
    }
}
