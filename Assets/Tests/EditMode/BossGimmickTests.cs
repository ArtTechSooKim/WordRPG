using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Field;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 이긴 보스와 다시 싸울 때 (#236 사용자 ㉓): 경험치·골드만
    public class RematchRewardTests
    {
        [Test]
        public void ARematchKeepsExpAndGoldButDropsItems()
        {
            var ink = TestData.Item("ink");
            var reward = new Battle.BattleReward(120, 30, new[] { new Battle.RewardItem(ink, 2) });
            var rematch = reward.WithoutItems();
            Assert.AreEqual(120, rematch.Exp);
            Assert.AreEqual(30, rematch.Gold);
            Assert.IsEmpty(rematch.Items);
            Assert.AreEqual(1, reward.Items.Count, "원래 보상은 그대로");
        }
    }

    // 보스마다 다른 기믹 (#236 사용자 ㉑: 기믹이 똑같아서 식상함) — 실제 데이터
    public class BossGimmickDataTests
    {
        private static MonsterSpecies Species(string id) => AssetDatabase.LoadAssetAtPath<MonsterSpecies>($"Assets/Data/Monsters/{id}.asset");

        [Test]
        public void TheForgetKingMakesYouForgetAndOnlyChargesUp()
        {
            var king = Species("boss_forget_king");
            Assert.AreEqual(BossGimmick.Forget, king.Gimmick, "크리티컬 시간이 지나면 정답을 까먹음");
            StringAssert.Contains("까먹", king.GimmickHint, "졌을 때 조언 한 줄");
            Assert.IsFalse(king.Skills.Any(s => s.SkillId == "boss_forget_bonk"), "보호막을 깨던 큰 방망이질은 뺌 (사용자)");
            Assert.IsTrue(king.Skills.Any(s => s.ChargeTurns > 0), "힘 모으기(기억상실)는 남김");
            Assert.AreEqual(BossGimmick.None, Species("forget_goblin").Gimmick, "보통 까먹깨비는 기믹 없음");
        }

        // #238 사용자 아이디어 1: 헷갈너구리는 보기가 2초마다 자리를 섞음
        [Test]
        public void TheMuddleRaccoonShufflesTheChoices()
        {
            var raccoon = Species("boss_muddle_raccoon");
            Assert.AreEqual(BossGimmick.Shuffle, raccoon.Gimmick);
            StringAssert.Contains("2초", raccoon.GimmickHint);
            var gimmicks = new[] { "boss_forget_king", "boss_muddle_raccoon", "boss_blank_frog" }.Select(id => Species(id).Gimmick).ToList();
            CollectionAssert.AllItemsAreUnique(gimmicks, "보스마다 기믹이 다름 (사용자: 똑같아서 식상함)");
        }

        // 서고 사서가 까먹대왕을 조심하라며 상처약 하나 (보스를 물리치기 전에만)
        [Test]
        public void TheLibrarianWarnsAboutTheKingAndGivesAPotion()
        {
            var library = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/library.asset");
            var librarian = library.Npcs.Single(npc => npc.NpcId == "npc_librarian");
            Assert.AreEqual("potion", librarian.GiftItem.ItemId);
            Assert.AreEqual(1, librarian.GiftCount);
            Assert.AreSame(library, librarian.GiftBeforeBossOf);
            Assert.IsTrue(librarian.GiftLines.Any(line => line.Contains("까먹대왕")), "까먹대왕을 조심하라는 말");
        }
    }
}
