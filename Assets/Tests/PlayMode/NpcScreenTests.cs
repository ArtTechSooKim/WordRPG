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
    // 말을 잃어 가는 사람들 화면 (#209): 막힘 · 말 걸기 · 머리 위 말풍선 · 말이 되돌아옴 · 예전 세이브 자리 겹침
    public class NpcScreenTests
    {
        private const string Map = "#######\n#PN...#\n#.....#\n#######";

        private FieldArea area;
        private GameDatabase database;
        private WordDatabase words;

        [SetUp]
        public void SetUp()
        {
            words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "common").Set("regionName", "초원");
            words.ReplaceWords(TestData.SampleWords());
            var npc = ScriptableObject.CreateInstance<NpcData>();
            npc.Configure("npc_chief", "촌장", 0, new[]
            {
                new NpcStage("자네 이름이 뭐였더라?", NpcMotionKind.Stand),
                new NpcStage("{이름}! 그래, 그 이름이야!", NpcMotionKind.Stand, null, 1),
            });
            area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "meadow").Set("displayName", "초원").Set("map", Map)
                .Set("encounters", ScriptableObject.CreateInstance<EncounterTable>()).Set("words", words)
                .Set("npcs", new List<NpcData> { npc });
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], new[] { area });
        }

        private static GameSession Session() => GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10)), 5);

        private FieldScreen CreateField(GameSession session)
        {
            var field = new GameObject("NpcFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, () => { }, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            return field;
        }

        [UnityTest]
        public IEnumerator TheNpcBlocksTheWayAndTalksInABubbleAboveItsHead()
        {
            var session = Session();
            var field = CreateField(session);
            yield return null;
            yield return null;
            var view = field.Npcs.Views.Single();
            Assert.AreEqual(new Vector2Int(2, 2), view.Motion.Cell);

            // 부딪혀도 못 지나감
            yield return HoldStick(field, Direction.Right, () => false, 0.15f);
            Assert.AreEqual(new Vector2Int(1, 2), field.PlayerCell, "인물 칸으로 못 감");

            // [확인] → 이름과 말, 말풍선은 인물 머리 위, 인물은 주인공을 봄
            yield return PressConfirm(field);
            Assert.IsTrue(field.Bubble.ShowsText);
            Assert.AreEqual("촌장", field.Bubble.HeadText);
            StringAssert.Contains("자네 이름이 뭐였더라?", field.Bubble.Text);
            yield return null;
            Assert.AreEqual(Direction.Left, view.Motion.Facing);

            // 단어를 찾으면 말이 되돌아옴, 플레이어 이름으로
            field.Bubble.Hide();
            session.Vocabulary.Discover(words.Words[0].Id, System.DateTime.UtcNow);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return PressConfirm(field);
            StringAssert.Contains($"{session.Hero.DisplayName}! 그래, 그 이름이야!", field.Bubble.Text);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnOldSaveStandingOnTheSpotPushesTheNpcAside()
        {
            var session = Session();
            session.World.SetPosition("meadow", new Vector2Int(2, 2), false);
            var field = CreateField(session);
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector2Int(2, 2), field.PlayerCell, "저장된 자리 그대로");
            Assert.AreNotEqual(new Vector2Int(2, 2), field.Npcs.Views.Single().Motion.Cell, "인물이 비켜 섬");
            yield return HoldStick(field, Direction.Down, () => field.IsMoving);
            Assert.AreNotEqual(new Vector2Int(2, 2), field.PlayerCell, "못 움직이는 일이 없음");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 상점 가판대 (#210): 상인이 안에 서고, 가까이 가면 부르고, [확인]은 그대로 상점
        [UnityTest]
        public IEnumerator TheMerchantCallsAsYouPassAndTheStallStillOpensTheShop()
        {
            var merchant = ScriptableObject.CreateInstance<NpcData>();
            merchant.Configure("npc_merchant_meadow", "잡화점 아주머니", 0, new[] { new NpcStage("", NpcMotionKind.Stand) });
            var shop = ScriptableObject.CreateInstance<ShopData>().Set("displayName", "초원 마을 잡화점").Set("merchant", merchant)
                .Set("pitches", new List<string> { "상처약 있어요!" });
            var stallArea = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "market").Set("displayName", "장터").Set("map", "###########\n#SSS......#\n#SSS......#\n#........P#\n###########")
                .Set("encounters", ScriptableObject.CreateInstance<EncounterTable>()).Set("words", words).Set("shop", shop);
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], new[] { stallArea });
            var session = Session();
            var field = new GameObject("StallFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(stallArea, session, () => { }, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            yield return null;
            yield return null;

            Assert.IsNotNull(field.Npcs.Merchant, "상인이 가판대 안에");
            Assert.AreEqual(new Vector2Int(2, 3), field.Npcs.Merchant.Motion.Cell);
            Assert.IsFalse(field.Bubble.IsVisible, "멀면 안 부름");

            // 왼쪽으로 걸어 가까이 → 상인 머리 위에 호객
            yield return HoldStick(field, Direction.Left, () => field.Bubble.IsVisible, 3f);
            Assert.IsTrue(field.Bubble.ShowsText, "가까이 가면 부름");
            Assert.AreEqual("잡화점 아주머니", field.Bubble.HeadText);
            StringAssert.Contains("상처약 있어요!", field.Bubble.Text);
            Assert.AreSame(field.Npcs.Merchant, field.TalkingNpc, "호객은 상인 머리 위");

            // 호객이 떠 있는 동안 주인공이 말하면 (사전 읽기 등) 주인공 머리 위로 (#227 사용자 — 상인에게 붙었음)
            field.Bubble.ShowWord("새로운 단어를 발견했다!", "overcome", "극복하다", "", 2f);
            Assert.IsFalse(field.Bubble.FromNpc);
            yield return null;
            Assert.IsNull(field.TalkingNpc, "주인공 말풍선은 주인공 머리 위");

            // 가판대 앞까지 가서 [확인] → 상점
            field.Bubble.Hide();
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 4, 3f);
            yield return WaitFor(() => !field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 2, 1f);
            yield return WaitFor(() => !field.IsMoving);
            yield return FaceStick(field, Direction.Left);
            yield return PressConfirm(field);
            Assert.IsTrue(field.IsPanelOpen, "가판대 어느 칸이든 [확인]이면 상점이 열림");

            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
