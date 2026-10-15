using System;
using System.Collections.Generic;
using WordRPG.Monsters;

namespace WordRPG.Heroes
{
    // 얻은 동료 한 명 (#77): 레벨·경험치·HP. 전투에 나서는 쪽(ICombatant)이라 주인공처럼 자기 차례에 기술 + 단어 문제
    public class Companion : ICombatant
    {
        public CompanionData Data { get; }
        public int Level { get; private set; }
        public int Exp { get; private set; } // 현재 레벨에서 쌓은 경험치
        public int CurrentHp { get; private set; }

        public string DisplayName => Data.DisplayName;
        public bool IsFainted => CurrentHp <= 0;
        public bool IsMaxLevel => Level >= LevelCurve.MaxLevel;
        public int ExpToNextLevel => IsMaxLevel ? 0 : LevelCurve.ExpToNextLevel(Level) - Exp;
        public MonsterStats Stats => Data.GetStats(Level);
        public IReadOnlyList<SkillData> Skills => Data.SkillsAt(Level);

        public Companion(CompanionData data, int level, int exp = 0)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Level = Math.Max(1, Math.Min(level, LevelCurve.MaxLevel));
            Exp = IsMaxLevel ? 0 : Math.Max(0, exp);
            CurrentHp = Stats.MaxHp;
        }

        // 반환값: 오른 레벨 수. 레벨업으로 늘어난 최대 HP만큼 현재 HP도 늘어난다 (주인공과 같은 규칙)
        public int GainExp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsMaxLevel) return 0;
            int levelsGained = 0;
            Exp += amount;
            while (!IsMaxLevel && Exp >= LevelCurve.ExpToNextLevel(Level))
            {
                int oldMaxHp = Stats.MaxHp;
                Exp -= LevelCurve.ExpToNextLevel(Level);
                Level++;
                levelsGained++;
                if (!IsFainted) CurrentHp += Stats.MaxHp - oldMaxHp;
            }
            if (IsMaxLevel) Exp = 0;
            return levelsGained;
        }

        public int TakeDamage(int amount)
        {
            int dealt = Math.Min(Math.Max(0, amount), CurrentHp);
            CurrentHp -= dealt;
            return dealt;
        }

        public int Heal(int amount)
        {
            if (IsFainted) return 0;
            int healed = Math.Min(Math.Max(0, amount), Stats.MaxHp - CurrentHp);
            CurrentHp += healed;
            return healed;
        }

        public void RestoreFully() => CurrentHp = Stats.MaxHp;

        // 저장된 HP로 맞출 때 (0 ~ 최대 HP)
        public void SetHp(int hp) => CurrentHp = Math.Max(0, Math.Min(hp, Stats.MaxHp));

        // 전투에서 쓰러졌다가 이긴 뒤: HP 1로 일어난다 (기본값 — 샘·상처약으로 회복)
        public void ReviveIfFainted()
        {
            if (IsFainted) CurrentHp = 1;
        }
    }
}
