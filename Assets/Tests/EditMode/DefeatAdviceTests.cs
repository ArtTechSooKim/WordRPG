using NUnit.Framework;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 졌을 때 원인에 맞는 한 줄 (#233 사용자 ㉔): 보스 기믹 → 레벨 → 성유물 → 그 밖 순서
    public class DefeatAdviceTests
    {
        [Test]
        public void TheMostUsefulCauseComesFirst()
        {
            Assert.AreEqual(DefeatCause.Gimmick, DefeatAdvice.Cause(3, 10, gimmickHit: true, canUpgradeRelic: true), "기믹에 당했으면 기믹 조언");
            Assert.AreEqual(DefeatCause.LowLevel, DefeatAdvice.Cause(3, 10, false, true), "적보다 레벨이 낮으면 레벨");
            Assert.AreEqual(DefeatCause.WeakRelics, DefeatAdvice.Cause(10, 10, false, true), "레벨이 같으면 강화할 수 있는 성유물");
            Assert.AreEqual(DefeatCause.General, DefeatAdvice.Cause(12, 10, false, false));
        }

        [Test]
        public void EveryCauseHasALine()
        {
            StringAssert.Contains("레벨", DefeatAdvice.Line(DefeatCause.LowLevel));
            StringAssert.Contains("성유물", DefeatAdvice.Line(DefeatCause.WeakRelics));
            Assert.IsNotEmpty(DefeatAdvice.Line(DefeatCause.General));
            Assert.AreEqual("헷갈너구리는 답의 자리를 바꾸니 신중하게 고르자", DefeatAdvice.Line(DefeatCause.Gimmick, "헷갈너구리는 답의 자리를 바꾸니 신중하게 고르자"));
            Assert.AreEqual(DefeatAdvice.Line(DefeatCause.General), DefeatAdvice.Line(DefeatCause.Gimmick), "기믹 조언이 없는 보스면 보통 조언");
        }

        [Test]
        public void ARelicThatCanBeUpgradedNowCounts()
        {
            var ink = TestData.Item("ink");
            var relic = TestData.Relic("quill", TestData.Skill("poke", SkillKind.Damage, SkillTarget.SingleEnemy, 5), new MonsterStats(0, 0, 0), ink);
            var owned = new OwnedRelic(relic); // +0 → +1 = 재료 1개 · 30골드
            var bag = new Inventory();
            Assert.IsFalse(DefeatAdvice.CanUpgradeAny(new[] { owned, null }, bag), "재료가 없음");
            bag.Add(ink, 1);
            bag.AddGold(30);
            Assert.IsTrue(DefeatAdvice.CanUpgradeAny(new[] { null, owned }, bag), "재료·골드가 있으면 지금 강화할 수 있음");
            Assert.IsFalse(DefeatAdvice.CanUpgradeAny(null, bag));
        }
    }
}
