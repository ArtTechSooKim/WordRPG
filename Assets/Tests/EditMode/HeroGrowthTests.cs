using NUnit.Framework;
using WordRPG.Heroes;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 결산의 레벨 업 능력치 칩 (#127): 오른 것만 HP → 공격 → 방어 순서로
    public class HeroGrowthTests
    {
        [Test]
        public void RisesListOnlyStatsThatWentUpInOrder()
        {
            var rises = HeroGrowth.Rises(new MonsterStats(60, 14, 10), new MonsterStats(68, 17, 12));
            Assert.AreEqual(3, rises.Count);
            Assert.AreEqual(("HP", 60, 68, 8), (rises[0].Label, rises[0].Before, rises[0].After, rises[0].Amount));
            Assert.AreEqual(("공격", 14, 17, 3), (rises[1].Label, rises[1].Before, rises[1].After, rises[1].Amount));
            Assert.AreEqual(("방어", 10, 12, 2), (rises[2].Label, rises[2].Before, rises[2].After, rises[2].Amount));

            Assert.IsEmpty(HeroGrowth.Rises(new MonsterStats(60, 14, 10), new MonsterStats(60, 14, 10)), "그대로면 없음");
            var hpOnly = HeroGrowth.Rises(new MonsterStats(60, 14, 10), new MonsterStats(70, 14, 9));
            Assert.AreEqual(1, hpOnly.Count, "내려간 건 보여 주지 않음");
            Assert.AreEqual("HP", hpOnly[0].Label);
        }

        [Test]
        public void LevelUpRaisesEveryStatForTheRealHero()
        {
            var hero = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            var rises = HeroGrowth.Rises(hero.GetStats(5), hero.GetStats(6));
            Assert.AreEqual(3, rises.Count, "실제 주인공은 레벨마다 HP·공격·방어가 모두 오름 — 결산에 칩 세 개");
        }
    }
}
