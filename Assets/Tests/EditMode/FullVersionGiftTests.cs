using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Game;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 정식판 특전 (#233 사용자 ⑨): 호기심 물약 3 + 특제 상처약 5 — 정식판이 있으면 세이브마다 한 번
    public class FullVersionGiftTests
    {
        [Test]
        public void TheGiftComesOncePerSaveAndOnlyWithTheFullVersion()
        {
            var curious = TestData.Item("potion_curious").Set("displayName", "호기심 물약");
            var great = TestData.Item("potion_great").Set("displayName", "특제 상처약");
            var gift = new List<ItemGift> { new ItemGift(curious, 3), new ItemGift(great, 5) };
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);

            Assert.IsEmpty(FullVersionGift.Claim(session, false, gift), "무료판은 없음");
            Assert.AreEqual(0, session.Inventory.GetCount(curious));
            Assert.IsFalse(FullVersionGift.Claimed(session));

            var given = FullVersionGift.Claim(session, true, gift);
            Assert.AreEqual(2, given.Count);
            Assert.AreEqual(3, session.Inventory.GetCount(curious));
            Assert.AreEqual(5, session.Inventory.GetCount(great));
            Assert.IsTrue(FullVersionGift.Claimed(session), "세이브에 남음 (WorldState 이벤트)");
            Assert.IsEmpty(FullVersionGift.Claim(session, true, gift), "두 번 받지 않음");
            Assert.AreEqual(3, session.Inventory.GetCount(curious));
            Assert.AreEqual("호기심 물약 3개 · 특제 상처약 5개", FullVersionGift.Describe(gift));
        }

        [Test]
        public void TheRealGiftIsThreeCuriousAndFiveGreatPotions()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            Assert.AreEqual(2, db.FullVersionGift.Count);
            Assert.AreEqual("potion_curious", db.FullVersionGift[0].Item.ItemId);
            Assert.AreEqual(3, db.FullVersionGift[0].Count);
            Assert.AreEqual("potion_great", db.FullVersionGift[1].Item.ItemId);
            Assert.AreEqual(5, db.FullVersionGift[1].Count);
        }
    }
}
