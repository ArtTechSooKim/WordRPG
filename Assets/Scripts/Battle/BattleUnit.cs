using System.Collections.Generic;
using WordRPG.Heroes;
using WordRPG.Monsters;

namespace WordRPG.Battle
{
    // 전투 중인 한 명 (주인공 또는 적 몬스터). HP는 Combatant에 직접 반영되어 전투 후에도 유지되고, 보호막만 전투 전용
    public class BattleUnit
    {
        public ICombatant Combatant { get; }
        public MonsterInstance Monster => Combatant as MonsterInstance; // 적이면 몬스터, 주인공이면 null
        public Hero Hero => Combatant as Hero;
        public Companion Companion => Combatant as Companion; // 동료 (#77)
        public bool IsPlayerSide { get; }
        public int Index { get; } // 같은 편 안에서의 위치 (UI 배치용)
        public int Shield { get; private set; }

        public string DisplayName => Combatant.DisplayName;
        public int Level => Combatant.Level;
        public int Hp => Combatant.CurrentHp;
        public int MaxHp => Combatant.Stats.MaxHp;
        public int Attack => Combatant.Stats.Attack;
        public int Defense => Combatant.Stats.Defense;
        public IReadOnlyList<SkillData> Skills => Combatant.Skills;
        public bool IsDefeated => Combatant.IsFainted;

        public BattleUnit(ICombatant combatant, bool isPlayerSide, int index)
        {
            Combatant = combatant;
            IsPlayerSide = isPlayerSide;
            Index = index;
        }

        // 보호막이 먼저 흡수하고 남은 만큼 HP가 깎인다
        internal void ReceiveDamage(int amount, out int hpDamage, out int absorbed)
        {
            absorbed = amount < Shield ? amount : Shield;
            Shield -= absorbed;
            hpDamage = Combatant.TakeDamage(amount - absorbed);
        }

        internal int ReceiveHeal(int amount) => Combatant.Heal(amount);

        internal void AddShield(int amount) => Shield += amount;

        internal void ClearShield() => Shield = 0;
    }
}
