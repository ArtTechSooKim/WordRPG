using System.Linq;
using NUnit.Framework;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 주인공: 능력치 = 레벨 + 끼운 성유물 보너스, 기술 = 기본 기술 + 끼운 성유물마다 1개, 성유물은 모아서 3칸에 끼운다
    public class HeroTests
    {
        private SkillData splash, storm, shieldSkill, light;
        private RelicData quill, book, lantern, wand;
        private HeroData data;

        [SetUp]
        public void SetUp()
        {
            splash = TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 14, QuizDirection.MeaningToEnglish);
            storm = TestData.Skill("storm", SkillKind.Damage, SkillTarget.AllEnemies, 22, QuizDirection.MeaningToEnglish);
            shieldSkill = TestData.Skill("shield", SkillKind.Guard, SkillTarget.AllAllies, 4);
            light = TestData.Skill("light", SkillKind.Heal, SkillTarget.AllAllies, 8);
            quill = TestData.Relic("quill", splash, new MonsterStats(0, 4, 0), awakened: storm)
                .Set("bonusPerLevel", new MonsterStats(0, 2, 0));
            book = TestData.Relic("book", shieldSkill, new MonsterStats(10, 0, 4));
            lantern = TestData.Relic("lantern", light, new MonsterStats(15, 0, 0));
            wand = TestData.Relic("wand", TestData.Skill("bolt", SkillKind.Damage, SkillTarget.SingleEnemy, 26), new MonsterStats(0, 6, 0));
            data = TestData.Hero(new MonsterStats(60, 14, 10), quill).Set("growthPerLevel", new MonsterStats(8, 3, 2));
        }

        [Test]
        public void StatsAreLevelPlusEquippedRelicBonus()
        {
            var hero = new Hero(data, 3);
            Assert.AreEqual(new MonsterStats(76, 20, 14).ToString(), hero.LevelStats.ToString(), "60+8x2 / 14+3x2 / 10+2x2");
            hero.AddRelic(quill);
            hero.AddRelic(book);
            Assert.AreEqual(86, hero.Stats.MaxHp);
            Assert.AreEqual(24, hero.Stats.Attack);
            Assert.AreEqual(18, hero.Stats.Defense);
            Assert.AreEqual(new MonsterStats(10, 4, 4).ToString(), hero.RelicBonus.ToString());
        }

        [Test]
        public void SkillsAreBasicThenEquippedRelics()
        {
            var hero = new Hero(data, 1);
            Assert.AreEqual(1, hero.Skills.Count, "성유물이 없으면 기본 기술만");
            hero.AddRelic(quill);
            hero.AddRelic(book);
            CollectionAssert.AreEqual(new[] { data.BasicSkill, splash, shieldSkill }, hero.Skills);
            Assert.IsNull(hero.SourceOf(data.BasicSkill));
            Assert.AreSame(quill, hero.SourceOf(splash).Relic.Data);
        }

        [Test]
        public void NewRelicGoesIntoEmptySlotUntilFull()
        {
            var hero = new Hero(data, 1);
            Assert.IsNotNull(hero.AddRelic(quill));
            Assert.IsNull(hero.AddRelic(quill), "같은 성유물은 한 번만");
            hero.AddRelic(book);
            hero.AddRelic(lantern);
            var extra = hero.AddRelic(wand);

            Assert.AreEqual(4, hero.Relics.Count, "모으는 건 제한 없음");
            Assert.AreEqual(3, hero.EquippedCount, "끼우는 건 3칸");
            Assert.IsFalse(hero.IsEquipped(extra), "칸이 가득하면 가방에만");
            Assert.IsFalse(hero.Equip(extra));

            Assert.IsTrue(hero.Unequip(hero.Find(book)));
            Assert.IsTrue(hero.Equip(extra), "빈 칸이 생기면 끼울 수 있다");
            Assert.AreEqual(1, hero.SlotOf(extra), "빠진 칸(1번)에 들어감");
        }

        [Test]
        public void UnequippingHpRelicClampsCurrentHp()
        {
            var hero = new Hero(data, 1);
            var relic = hero.AddRelic(lantern);
            hero.RestoreFully();
            Assert.AreEqual(75, hero.CurrentHp);
            hero.Unequip(relic);
            Assert.AreEqual(60, hero.CurrentHp, "최대 HP가 줄면 현재 HP도");
        }

        [Test]
        public void LevelUpRaisesCurrentHpByGrowth()
        {
            var hero = new Hero(data, 1);
            hero.TakeDamage(10);
            Assert.AreEqual(1, hero.GainExp(LevelCurve.ExpToNextLevel(1)));
            Assert.AreEqual(2, hero.Level);
            Assert.AreEqual(58, hero.CurrentHp, "50 + 늘어난 최대 HP 8");
            Assert.AreEqual(LevelCurve.ExpToNextLevel(2), hero.ExpToNextLevel);
        }
    }

    // 성유물 강화: 재료 + 골드로 +1 (최대 +5), +3에서 각성 → 기술이 바뀐다
    public class RelicUpgradeTests
    {
        private ItemData ink;
        private SkillData splash, storm;
        private RelicData quill;
        private Inventory inventory;
        private OwnedRelic relic;

        [SetUp]
        public void SetUp()
        {
            ink = TestData.Item("shiny_ink");
            splash = TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 14);
            storm = TestData.Skill("storm", SkillKind.Damage, SkillTarget.AllEnemies, 22);
            quill = TestData.Relic("quill", splash, new MonsterStats(0, 4, 0), ink, storm).Set("bonusPerLevel", new MonsterStats(0, 2, 0));
            inventory = new Inventory();
            relic = new OwnedRelic(quill);
        }

        [Test]
        public void NeedsMaterialsThenGold()
        {
            Assert.AreEqual(UpgradeStatus.NotEnoughItems, RelicUpgrade.Check(relic, inventory));
            inventory.Add(ink, 1);
            Assert.AreEqual(UpgradeStatus.NotEnoughGold, RelicUpgrade.Check(relic, inventory));
            Assert.IsFalse(RelicUpgrade.TryUpgrade(relic, inventory));
            Assert.AreEqual(1, inventory.GetCount(ink), "실패하면 아무것도 쓰지 않는다");

            inventory.AddGold(30);
            Assert.AreEqual(UpgradeStatus.Ready, RelicUpgrade.Check(relic, inventory));
            Assert.IsTrue(RelicUpgrade.TryUpgrade(relic, inventory));
            Assert.AreEqual(1, relic.Level);
            Assert.AreEqual(0, inventory.GetCount(ink));
            Assert.AreEqual(0, inventory.Gold);
            Assert.AreEqual(6, relic.Bonus.Attack, "4 + 2 x 1");
        }

        [Test]
        public void AwakensAtPlusThreeAndStopsAtPlusFive()
        {
            inventory.Add(ink, 99);
            inventory.AddGold(9999);
            Assert.IsFalse(quill.AwakensWhenUpgradedFrom(1));
            RelicUpgrade.TryUpgrade(relic, inventory);
            RelicUpgrade.TryUpgrade(relic, inventory);
            Assert.AreSame(splash, relic.Skill);
            Assert.IsTrue(quill.AwakensWhenUpgradedFrom(2));

            RelicUpgrade.TryUpgrade(relic, inventory);
            Assert.AreEqual(3, relic.Level);
            Assert.IsTrue(relic.IsAwakened);
            Assert.AreSame(storm, relic.Skill, "+3 각성 → 더 강한 기술");

            RelicUpgrade.TryUpgrade(relic, inventory);
            RelicUpgrade.TryUpgrade(relic, inventory);
            Assert.AreEqual(RelicData.MaxLevel, relic.Level);
            Assert.AreEqual(UpgradeStatus.MaxLevel, RelicUpgrade.Check(relic, inventory));
            Assert.IsFalse(RelicUpgrade.TryUpgrade(relic, inventory));
            Assert.AreEqual(99 - (1 + 2 + 2 + 3 + 3), inventory.GetCount(ink));
            Assert.AreEqual(9999 - (30 + 60 + 90 + 120 + 150), inventory.Gold);
        }

        [Test]
        public void UpgradingEquippedRelicRaisesHeroStats()
        {
            var hero = new Hero(TestData.Hero(new MonsterStats(60, 14, 10)), 1);
            var owned = hero.AddRelic(quill);
            inventory.Add(ink, 1);
            inventory.AddGold(30);
            int before = hero.Stats.Attack;
            RelicUpgrade.TryUpgrade(owned, inventory);
            Assert.AreEqual(before + 2, hero.Stats.Attack);
        }
    }

    // 성유물 바로 바꿔 끼우기 (#91 사용자 요청): 그 칸의 성유물과 바꾸고, 기술 칸의 순서는 그대로
    public class RelicSwapTests
    {
        private static RelicData Relic(string id, int power) =>
            TestData.Relic(id, TestData.Skill(id + "_skill", SkillKind.Damage, SkillTarget.SingleEnemy, power), new MonsterStats(0, power, 0), null);

        [Test]
        public void SwapKeepsTheSkillInTheSamePlace()
        {
            var a = Relic("a", 1); var b = Relic("b", 2); var c = Relic("c", 3); var d = Relic("d", 4);
            var hero = new Hero(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            var ra = hero.AddRelic(a); var rb = hero.AddRelic(b); var rc = hero.AddRelic(c); var rd = hero.AddRelic(d); // 얻으면 빈 칸에 바로 끼워짐
            Assert.IsTrue(hero.IsEquipped(ra) && hero.IsEquipped(rb) && hero.IsEquipped(rc));
            Assert.IsFalse(hero.IsEquipped(rd), "네 번째는 칸이 없어 가방에만");
            Assert.IsFalse(hero.HasEmptySlot);
            Assert.IsFalse(hero.Equip(rd), "칸이 가득 차서 그냥 장착은 안 됨");

            Assert.IsTrue(hero.SwapInto(rd, 1), "가운데 칸(b)과 바꿈");
            Assert.AreSame(rd, hero.SlotAt(1));
            Assert.IsFalse(hero.IsEquipped(rb), "b는 빠짐");
            CollectionAssert.AreEqual(new[] { "relic:a", "relic:d", "relic:c" }, hero.SkillSlots.Select(s => s.Key).ToArray(), "기술 칸 순서 그대로, b 자리에 d");
            Assert.AreEqual(10 + 1 + 4 + 3, hero.Stats.Attack, "능력치 보너스도 바뀜");

            Assert.IsFalse(hero.SwapInto(ra, 2), "이미 끼운 성유물은 못 바꿈");
            Assert.IsFalse(hero.SwapInto(rb, 5), "없는 칸");
        }

        [Test]
        public void SwapIntoAnEmptySlotJustEquips()
        {
            var a = Relic("a", 1); var b = Relic("b", 2);
            var hero = new Hero(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            var ra = hero.AddRelic(a); var rb = hero.AddRelic(b);
            hero.Unequip(rb);
            Assert.IsTrue(hero.SwapInto(rb, 2), "빈 3번 칸에");
            Assert.AreSame(rb, hero.SlotAt(2));
            Assert.IsTrue(hero.HasSkillOf(rb), "빈 기술 칸이 있으면 기술도");
        }
    }
}
