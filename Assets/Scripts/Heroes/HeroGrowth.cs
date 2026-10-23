using System.Collections.Generic;
using WordRPG.Monsters;

namespace WordRPG.Heroes
{
    // 레벨이 올라 오른 능력치 (#127 사용자: "레벨 오르는 게 무슨 의미가 있는지 의문" → 결산에서 HP·공격·방어가 오르는 게 보이게).
    // 오른 것만 HP → 공격 → 방어 순서로
    public static class HeroGrowth
    {
        public readonly struct Rise
        {
            public string Label { get; }
            public int Before { get; }
            public int After { get; }
            public int Amount => After - Before;

            public Rise(string label, int before, int after)
            {
                Label = label;
                Before = before;
                After = after;
            }
        }

        public static List<Rise> Rises(MonsterStats before, MonsterStats after)
        {
            var rises = new List<Rise>();
            if (after.MaxHp > before.MaxHp) rises.Add(new Rise("HP", before.MaxHp, after.MaxHp));
            if (after.Attack > before.Attack) rises.Add(new Rise("공격", before.Attack, after.Attack));
            if (after.Defense > before.Defense) rises.Add(new Rise("방어", before.Defense, after.Defense));
            return rises;
        }
    }
}
