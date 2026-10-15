using System;
using System.Collections.Generic;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;

namespace WordRPG.Battle
{
    public class RewardItem
    {
        public ItemData Item { get; }
        public int Count { get; internal set; }

        public RewardItem(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    public class BattleReward
    {
        public int Exp { get; }
        public int Gold { get; }
        public IReadOnlyList<RewardItem> Items { get; }

        public BattleReward(int exp, int gold, IReadOnlyList<RewardItem> items)
        {
            Exp = exp;
            Gold = gold;
            Items = items;
        }
    }

    public static class BattleRewardCalculator
    {
        // 경험치 = Σ(적 기본 경험치 x 레벨) + 정답 수 x 정답 보너스
        public static BattleReward Calculate(IReadOnlyList<MonsterInstance> defeatedEnemies, int correctAnswers,
            BattleConfig config, Random rng)
        {
            int exp = correctAnswers * config.ExpPerCorrectAnswer;
            int gold = 0;
            var items = new List<RewardItem>();

            foreach (var enemy in defeatedEnemies)
            {
                exp += enemy.Species.ExpReward * enemy.Level;
                gold += enemy.Species.GoldReward * enemy.Level;

                foreach (var drop in enemy.Species.Drops)
                {
                    if (drop.Item == null || rng.NextDouble() >= drop.Chance) continue;
                    var existing = items.Find(i => i.Item == drop.Item);
                    if (existing != null) existing.Count += drop.Count;
                    else items.Add(new RewardItem(drop.Item, drop.Count));
                }
            }

            return new BattleReward(exp, gold, items);
        }

        // 동료도 같은 경험치를 받는다 (#77 — 사용자 결정: 둘 다 받아 각자 레벨업). 반환: 오른 레벨 수
        public static int ApplyTo(BattleReward reward, Companion companion) =>
            companion != null ? companion.GainExp(reward.Exp) : 0;

        // 주인공이 경험치를 받고, 골드·떨어뜨린 재료는 소지품으로. 반환: 오른 레벨 수
        public static int Apply(BattleReward reward, Hero hero, Inventory inventory)
        {
            int levelsGained = hero.GainExp(reward.Exp);
            if (reward.Gold > 0) inventory.AddGold(reward.Gold);
            foreach (var item in reward.Items) inventory.Add(item.Item, item.Count);
            return levelsGained;
        }
    }
}
