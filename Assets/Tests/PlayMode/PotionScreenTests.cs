using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 숲 상점 물약 화면 (#146): 가방에서 마심 → 알약 → 걸음·문제에 효과
    public class PotionScreenTests
    {
        //   y=1 ............ 풀숲 (조우 100%)
        private const string Map = "##############\n#P,,,,,,,,,,,#\n##############";

        private ItemData quiet, curious;
        private FieldArea area;
        private GameDatabase database;

        [SetUp]
        public void SetUp()
        {
            quiet = TestData.Item("potion_quiet").Set("displayName", "살금살금 물약").Set("kind", ItemKind.Consumable)
                .Set("effect", ItemEffect.Quiet).Set("effectAmount", 6);
            curious = TestData.Item("potion_curious").Set("displayName", "호기심 물약").Set("kind", ItemKind.Consumable)
                .Set("effect", ItemEffect.NewWords).Set("effectAmount", 3);
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var enemy = TestData.Species("slime", new MonsterStats(400, 1, 50), new MonsterStats(0, 0, 0), bite);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "forest").Set("regionName", "숲");
            words.ReplaceWords(TestData.SampleWords());
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "forest").Set("displayName", "숲").Set("map", Map)
                .Set("encounters", table).Set("words", words)
                .Set("encounterRate", 1f).Set("minStepsBetweenEncounters", 0);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { enemy }, new[] { quiet, curious }, new[] { area }, new List<WordRPG.Heroes.RelicData>());
        }


        private static GameSession Session()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 5).Set("displayName", "휘두르기");
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(999, 5, 10)).Set("basicSkill", strike), 5);
            for (int i = 0; i < 3; i++) session.Record.RecordBattle(true, 1, 0); // 강도 고르기까지 열린 상태
            return session;
        }

        private FieldScreen CreateField(GameSession session)
        {
            var field = new GameObject("PotionFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, () => { }, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            return field;
        }

        private static Transform Hud(FieldScreen field) => field.transform.Find("FieldHud/SafeArea");

        private static IEnumerator DrinkFromBag(FieldScreen field, ItemData item, GameSession session)
        {
            var hud = Hud(field);
            FindButton(hud, "BagButton").onClick.Invoke();
            yield return null;
            FindButton(hud, "TabItems").onClick.Invoke();
            yield return null;
            StringAssert.Contains("쓰는 곳  필드", AllText(hud.Find("InventoryView")), "쓰는 곳 안내");
            int before = session.Inventory.GetCount(item);
            ActiveButton(hud, "UseItemButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(before - 1, session.Inventory.GetCount(item), "하나 씀");
            FindButton(hud, "InventoryCloseButton").onClick.Invoke();
            yield return null;
            yield return null;
        }

        // 살금살금 물약: 가방에서 마시면 알약이 뜨고, 풀숲을 걸어도 조우 없음 → 다 쓰면 알약이 사라지고 다시 조우
        [UnityTest]
        public IEnumerator QuietPotionStopsGrassEncountersUntilItWearsOff()
        {
            var session = Session();
            session.Inventory.Add(quiet);
            var field = CreateField(session);
            yield return null;
            yield return null;
            var pill = Hud(field).Find("QuietPill");
            Assert.IsFalse(pill.gameObject.activeSelf, "마시기 전엔 없음");

            yield return DrinkFromBag(field, quiet, session);
            Assert.AreEqual(6, session.Effects.QuietStepsLeft);
            Assert.IsTrue(pill.gameObject.activeSelf, "남은 동안 알약");
            StringAssert.Contains("6걸음", AllText(pill));

            // 시작 (1,1)에서 풀숲을 오른쪽으로: 6걸음(x=7)까지 조우 없음 → 7번째 걸음(x=8)에서 조우 (이 맵은 조우 100%)
            yield return HoldStick(field, Direction.Right, () => field.IsInBattle, 5f);
            Assert.IsTrue(field.IsInBattle, "효과가 끝나면 다시 조우");
            Assert.AreEqual(8, field.PlayerCell.x, "효과가 끝나는 걸음까지 보호, 그다음 걸음에서 조우");
            Assert.IsFalse(session.Effects.Quiet, "6걸음을 다 씀");
            Assert.IsFalse(pill.gameObject.activeSelf, "다 쓰면 알약도 사라짐");
            Object.Destroy(field.gameObject);
        }

        // 호기심 물약: 전투 문제가 새 단어 → 남은 수가 줄고, 다 쓰면 알약이 사라짐. 전투 아이템 목록에는 안 나옴
        [UnityTest]
        public IEnumerator CuriousPotionMakesBattleAskNewWords()
        {
            var session = Session();
            session.Inventory.Add(curious);
            var field = CreateField(session);
            yield return null;
            yield return null;
            yield return DrinkFromBag(field, curious, session);
            var pill = Hud(field).Find("NewWordPill");
            Assert.IsTrue(pill.gameObject.activeSelf);
            StringAssert.Contains("새 단어 3개", AllText(pill));

            var strong = TestData.Species("slime2", new MonsterStats(9999, 1, 50), new MonsterStats(0, 0, 0), TestData.Skill("bite2", SkillKind.Damage, SkillTarget.SingleEnemy, 1));
            field.Battle.BeginBattle(new List<MonsterInstance> { new MonsterInstance(strong, 1) }, area.Words, _ => { }, null, area.Theme);
            var root = field.Battle.transform;
            yield return WaitFor(() => ActiveButton(root, "SkillButton_0") != null, 5f);
            ActiveButton(root, "SkillButton_0").onClick.Invoke();
            yield return null;
            ActiveButton(root, "Intensity_0")?.onClick.Invoke();
            yield return WaitFor(() => field.Battle.Engine.CurrentQuestion != null, 5f);
            Assert.IsTrue(field.Battle.Engine.CurrentQuestion.IsNewWord, "새 단어가 나옴");
            Assert.AreEqual(2, session.Effects.NewWordsLeft, "새 단어 하나 씀");
            Object.Destroy(field.gameObject);
        }
    }
}
