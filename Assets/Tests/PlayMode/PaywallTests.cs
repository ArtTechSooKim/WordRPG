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
using Object = UnityEngine.Object;

namespace WordRPG.Tests
{
    // 정식판 (#40, Figma '필드 — 정식판 안내'): 정식판이 필요한 지역 입구에서 안내 → 사거나 복원하면 바로 들어간다
    public class PaywallTests
    {
        // 결제 창구 가짜: 다음 결과를 정해 두고, 사거나 복원되면 실제처럼 Entitlements에 먼저 넣는다
        private class FakeStore : IStore
        {
            private readonly Entitlements owned;
            public bool IsReady { get; set; } = true;
            public string FullVersionPrice { get; set; } = "₩5,000";
            public StoreResult NextBuy = StoreResult.Purchased;
            public StoreResult NextRestore = StoreResult.NothingToRestore;

            public FakeStore(Entitlements entitlements) => owned = entitlements;

            public void BuyFullVersion(Action<StoreResult> done)
            {
                if (NextBuy == StoreResult.Purchased) owned.Grant(Entitlements.FullVersion);
                done(NextBuy);
            }

            public void Restore(Action<StoreResult> done)
            {
                if (NextRestore == StoreResult.Restored) owned.Grant(Entitlements.FullVersion);
                done(NextRestore);
            }
        }

        //   집:  #P..D#   → 오른쪽 끝 D = 정식판 지역으로
        //   정식판 지역: #D..P# (왼쪽 D = 집으로)
        private FieldArea home, paid;

        [SetUp]
        public void SetUp()
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var owl = TestData.Species("owl", new MonsterStats(30, 5, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "물음표부엉이");
            var raccoon = TestData.Species("raccoon", new MonsterStats(90, 9, 9), new MonsterStats(0, 0, 0), bite).Set("displayName", "헷갈너구리");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(owl, 1, 1, 1) });
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "test").Set("regionName", "테스트");
            words.ReplaceWords(TestData.SampleWords());
            var whip = TestData.Relic("relic_whip", bite, new MonsterStats(0, 1, 0)).Set("displayName", "덩굴 채찍");
            var leaf = TestData.Relic("relic_leaf", bite, new MonsterStats(1, 0, 0)).Set("displayName", "세계수 잎");
            home = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "home").Set("displayName", "초원").Set("map", "######\n#P..D#\n######")
                .Set("encounters", table).Set("words", words);
            paid = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "paid").Set("displayName", "숲").Set("map", "######\n#D.CP#\n######")
                .Set("encounters", table).Set("words", words).Set("requiresFullVersion", true)
                .Set("chests", new List<ChestContent> { new ChestContent(null, 0, 60, whip) })
                .Set("boss", new BossEncounter(raccoon, 5, leaf));
            home.Set("exits", new List<AreaExit> { new AreaExit(paid, 0) });
            paid.Set("exits", new List<AreaExit> { new AreaExit(home, 0) });
        }

        private FieldScreen MakeField(GameSession session, IStore store, Entitlements owned)
        {
            var field = new GameObject("PaywallFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(home, session, step: 0.05f, animScale: 0.01f, purchaseStore: store, owned: owned);
            return field;
        }

        private static GameSession NewSession() =>
            GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)).Set("basicSkill",
                TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 5)), 1);

        [UnityTest]
        public IEnumerator EntranceAsksToBuyThenOpens()
        {
            var owned = new Entitlements();
            var store = new FakeStore(owned);
            var field = MakeField(NewSession(), store, owned);
            yield return null;
            yield return null;
            var paywall = field.Paywall;

            // 입구 쪽으로 밀고 있으면 바로 앞에서 멈추고 안내
            yield return HoldStick(field, Direction.Right, () => paywall.IsOpen, 5f);
            Assert.IsTrue(paywall.IsOpen);
            Assert.AreEqual(new Vector2Int(3, 1), field.PlayerCell, "들어가지 않고 앞에 섬");
            Assert.AreSame(home, field.CurrentArea);
            var text = AllText(paywall.Root.transform);
            StringAssert.Contains("숲 너머의 모험을 이어가요", text);
            StringAssert.Contains("₩5,000에 정식판 열기", text, "스토어 가격 그대로");
            StringAssert.Contains("새 영단어", text);
            StringAssert.Contains("보스 '헷갈너구리'", text);
            StringAssert.Contains("성유물 2개 (덩굴 채찍·세계수 잎)", text, "상자·보스 보상 성유물");
            Assert.IsTrue(field.IsPanelOpen, "안내 중엔 걷지 않음");

            // [나중에] → 닫힘. 스틱을 떼었다 다시 밀면 또 뜬다
            FindButton(paywall.Root.transform, "LaterButton").onClick.Invoke();
            Assert.IsFalse(paywall.IsOpen);
            yield return HoldStick(field, Direction.Right, () => paywall.IsOpen, 3f);
            Assert.IsTrue(paywall.IsOpen);

            // 실패 · 연결 안 됨 · 복원할 것 없음 → 안내 그대로, 알맞은 문구
            store.NextBuy = StoreResult.Failed;
            FindButton(paywall.Root.transform, "BuyButton").onClick.Invoke();
            StringAssert.Contains("완료하지 못했어요", paywall.StatusText);
            store.IsReady = false;
            FindButton(paywall.Root.transform, "BuyButton").onClick.Invoke();
            StringAssert.Contains("지금은 결제할 수 없어요", paywall.StatusText);
            store.IsReady = true;
            FindButton(paywall.Root.transform, "RestoreButton").onClick.Invoke();
            StringAssert.Contains("복원할 구매 기록이 없어요", paywall.StatusText);
            Assert.IsTrue(paywall.IsOpen);
            Assert.IsFalse(owned.HasFullVersion);

            // 사면 닫히고 알림 → 바로 들어갈 수 있다
            store.NextBuy = StoreResult.Purchased;
            FindButton(paywall.Root.transform, "BuyButton").onClick.Invoke();
            Assert.IsFalse(paywall.IsOpen);
            Assert.IsTrue(owned.HasFullVersion);
            StringAssert.Contains("정식판이 열렸어요", field.ToastMessage);
            yield return HoldStick(field, Direction.Right, () => field.CurrentArea == paid, 5f);
            yield return WaitFor(() => !field.IsInBattle && field.CurrentArea == paid, 3f);
            Assert.AreSame(paid, field.CurrentArea, "정식판 지역으로 들어감");

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestoreOpensAndOwnersWalkRightIn()
        {
            var owned = new Entitlements();
            var store = new FakeStore(owned) { NextRestore = StoreResult.Restored };
            var field = MakeField(NewSession(), store, owned);
            yield return null;
            yield return null;
            yield return HoldStick(field, Direction.Right, () => field.Paywall.IsOpen, 5f);
            FindButton(field.Paywall.Root.transform, "RestoreButton").onClick.Invoke();
            Assert.IsFalse(field.Paywall.IsOpen, "복원하면 바로 열림");
            Assert.IsTrue(owned.HasFullVersion);
            Object.Destroy(field.gameObject);
            yield return null;

            // 이미 산 사람은 안내 없이 바로 들어간다
            var again = MakeField(NewSession(), store, owned);
            yield return null;
            yield return null;
            yield return HoldStick(again, Direction.Right, () => again.CurrentArea == paid || again.Paywall.IsOpen, 5f);
            Assert.IsFalse(again.Paywall.IsOpen);
            yield return WaitFor(() => again.CurrentArea == paid, 3f);
            Assert.AreSame(paid, again.CurrentArea);
            Object.Destroy(again.gameObject);
            yield return null;
        }
    }
}
