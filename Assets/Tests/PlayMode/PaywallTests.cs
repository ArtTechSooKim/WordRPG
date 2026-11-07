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

        // #233 사용자 ⑨: 정식판을 사면 특전 (호기심 물약 3 + 특제 상처약 5) — '열렸어요' 알림에 같이, 세이브마다 한 번. 이미 산 사람은 켤 때 한 번
        [UnityTest]
        public IEnumerator BuyingGivesTheGiftOnceAndOwnersGetItOnStart()
        {
            var curious = TestData.Item("potion_curious").Set("displayName", "호기심 물약");
            var great = TestData.Item("potion_great").Set("displayName", "특제 상처약");
            // 목록 순서와 상관없이 이야기 순서로 가장 앞 정식판 지역(숲)을 안내 (#233: 예전엔 동굴이 나왔음)
            var later = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "later").Set("displayName", "동굴")
                .Set("map", "#####\n#P..#\n#####").Set("requiresFullVersion", true).Set("storyOrder", 9);
            paid.Set("storyOrder", 3);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new[] { curious, great }, new[] { later, home, paid });
            database.Set("fullVersionGift", new List<ItemGift> { new ItemGift(curious, 3), new ItemGift(great, 5) });
            var owned = new Entitlements();
            var store = new FakeStore(owned);
            var session = NewSession();
            FieldScreen Make(GameSession s, Entitlements e)
            {
                var f = new GameObject("GiftField").AddComponent<FieldScreen>();
                f.Configure(home, s, step: 0.05f, animScale: 0.01f, gameDatabase: database, purchaseStore: store, owned: e);
                return f;
            }

            var field = Make(session, owned);
            yield return null;
            yield return null;
            Assert.AreEqual(0, field.FullVersionGiftsGiven, "무료판은 없음");
            var hud = field.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "SettingsButton").onClick.Invoke();
            FindButton(hud, "FullVersionOfferButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(field.Paywall.IsOpen);
            StringAssert.Contains("숲 너머의 모험", AllText(field.Paywall.Root.transform), "이야기 순서로 첫 정식판 지역");
            StringAssert.Contains("특전: 호기심 물약 3개 · 특제 상처약 5개", AllText(field.Paywall.Root.transform), "안내 창에 특전");
            store.NextBuy = StoreResult.Purchased;
            FindButton(field.Paywall.Root.transform, "BuyButton").onClick.Invoke();
            Assert.AreEqual(1, field.FullVersionGiftsGiven);
            Assert.AreEqual(3, session.Inventory.GetCount(curious));
            Assert.AreEqual(5, session.Inventory.GetCount(great));
            StringAssert.Contains("정식판이 열렸어요", field.ToastMessage);
            StringAssert.Contains("정식판 특전: 호기심 물약 3개 · 특제 상처약 5개를 받았어요", field.ToastMessage, "열렸어요 알림에 같이");
            Object.Destroy(field.gameObject);
            yield return null;

            var again = Make(session, owned);
            yield return null;
            yield return null;
            Assert.AreEqual(0, again.FullVersionGiftsGiven, "같은 세이브는 다시 안 받음");
            Assert.AreEqual(3, session.Inventory.GetCount(curious));
            Object.Destroy(again.gameObject);
            yield return null;

            // 이미 산 사람의 다른 세이브(또는 업데이트 전에 산 사람): 켤 때 한 번
            var other = NewSession();
            var owner = Make(other, owned);
            yield return null;
            yield return null;
            Assert.AreEqual(1, owner.FullVersionGiftsGiven);
            Assert.AreEqual(5, other.Inventory.GetCount(great));
            StringAssert.Contains("정식판 특전", owner.ToastMessage);
            Object.Destroy(owner.gameObject);
            yield return null;
        }

        // #233 사용자 ⑮: 정식판이면 가방 > 주인공 탭의 [모습 바꾸기]로 언제든 8명 중 고름. 무료판은 정식판 안내
        [UnityTest]
        public IEnumerator FullVersionLetsTheHeroChangeLooks()
        {
            var owned = new Entitlements();
            var store = new FakeStore(owned);
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)).Set("looks", new List<HeroLook>
            {
                new HeroLook("boy", "소년"), new HeroLook("hunter", "사냥꾼"), new HeroLook("knight", "기사")
            }), 1);
            session.Hero.SetLook("boy");
            var field = MakeField(session, store, owned);
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "BagButton").onClick.Invoke();
            yield return null;
            FindButton(hud.Find("InventoryView"), "ChangeLookButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(field.Paywall.IsOpen, "무료판은 정식판 안내");
            StringAssert.Contains("모습 바꾸기", AllText(field.Paywall.Root.transform), "안내 창 특전 줄에");
            Assert.IsFalse(field.CharacterSelect.IsOpen);
            FindButton(field.Paywall.Root.transform, "LaterButton").onClick.Invoke();

            owned.Grant(Entitlements.FullVersion);
            yield return null;
            FindButton(hud.Find("InventoryView"), "ChangeLookButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(field.CharacterSelect.IsOpen, "정식판은 고르기 창");
            Assert.IsTrue(field.CharacterSelect.CanCancel);
            StringAssert.Contains("이 모습으로 바꾸기", AllText(field.CharacterSelect.Root.transform));
            field.HandleBack();
            Assert.IsFalse(field.CharacterSelect.IsOpen, "뒤로가기 = 닫기");
            Assert.AreEqual("boy", session.Hero.LookOrDefault, "닫으면 그대로");

            FindButton(hud.Find("InventoryView"), "ChangeLookButton").onClick.Invoke();
            yield return null;
            field.CharacterSelect.Select("knight");
            field.CharacterSelect.Confirm();
            yield return null;
            Assert.AreEqual("knight", session.Hero.LookOrDefault, "바꾼 모습");
            Assert.IsFalse(field.CharacterSelect.IsOpen);
            StringAssert.Contains("모습을 바꿨어요", field.ToastMessage);
            Object.Destroy(field.gameObject);
            yield return null;
        }

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

        // 설정 > 정식판 (#63 앱스토어 심사 대비): 숲 입구까지 가지 않아도 [정식판 보기]로 살 수 있고, [구매 복원]은 그 자리에서
        [UnityTest]
        public IEnumerator SettingsOffersFullVersionAndRestoreAnywhere()
        {
            var owned = new Entitlements();
            var store = new FakeStore(owned) { NextRestore = StoreResult.NothingToRestore };
            var field = MakeField(NewSession(), store, owned);
            yield return null;
            yield return null;
            var hud = field.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "SettingsButton").onClick.Invoke();
            var settingsRoot = hud.Find("SettingsView");
            Assert.IsTrue(settingsRoot.gameObject.activeSelf);
            StringAssert.Contains("정식판", AllText(settingsRoot.Find("Section_정식판")));

            // [구매 복원] — 기록이 없으면 그 자리에 안내
            ActiveButton(settingsRoot, "SettingsRestoreButton").onClick.Invoke();
            StringAssert.Contains("복원할 구매 기록이 없어요", AllText(settingsRoot.Find("Section_정식판")));
            Assert.IsFalse(owned.HasFullVersion);

            // [정식판 보기] → 정식판 안내 창 (설정 위에) → 사면 닫히고 설정은 '정식판 이용 중'
            ActiveButton(settingsRoot, "FullVersionOfferButton").onClick.Invoke();
            Assert.IsTrue(field.Paywall.IsOpen, "어디서든 정식판 안내");
            StringAssert.Contains("숲", AllText(field.Paywall.Root.transform));
            FindButton(field.Paywall.Root.transform, "BuyButton").onClick.Invoke();
            Assert.IsFalse(field.Paywall.IsOpen);
            Assert.IsTrue(owned.HasFullVersion);
            StringAssert.Contains("정식판 이용 중", AllText(settingsRoot.Find("Section_정식판")));
            Assert.IsNull(ActiveButton(settingsRoot, "FullVersionOfferButton"), "산 뒤에는 [정식판 보기] 없음");
            Assert.IsNotNull(ActiveButton(settingsRoot, "SettingsRestoreButton"), "[구매 복원]은 늘 있음");

            // 다른 기기: 복원하면 그 자리에서 되찾음
            var other = new Entitlements();
            var otherStore = new FakeStore(other) { NextRestore = StoreResult.Restored };
            Object.Destroy(field.gameObject);
            yield return null;
            var second = MakeField(NewSession(), otherStore, other);
            yield return null;
            yield return null;
            hud = second.transform.Find("FieldHud/SafeArea");
            FindButton(hud, "SettingsButton").onClick.Invoke();
            settingsRoot = hud.Find("SettingsView");
            ActiveButton(settingsRoot, "SettingsRestoreButton").onClick.Invoke();
            Assert.IsTrue(other.HasFullVersion);
            StringAssert.Contains("되찾았어요", AllText(settingsRoot.Find("Section_정식판")));
            Object.Destroy(second.gameObject);
            yield return null;
        }
    }
}
