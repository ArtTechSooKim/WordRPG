using System.Collections.Generic;
using WordRPG.Heroes;
using WordRPG.Items;

namespace WordRPG.Game
{
    // 졌을 때 원인에 맞는 한 줄 (#233 사용자 ㉔): 보스 기믹에 당함 → 레벨이 적보다 낮음 → 성유물을 지금 더 강화할 수 있음 → 그 밖.
    // 결산 화면 '쓰러졌다' 아래에 한 줄 (BattleScreen.Result)
    public enum DefeatCause
    {
        Gimmick,    // 보스 기믹에 당함 (그 보스의 조언 한 줄 — BossEncounter.DefeatHint)
        LowLevel,   // 주인공 레벨이 가장 강한 적보다 낮음
        WeakRelics, // 끼운 성유물 중 지금 강화할 수 있는 것이 있음 (재료·골드 충분)
        General
    }

    public static class DefeatAdvice
    {
        public static DefeatCause Cause(int heroLevel, int strongestEnemyLevel, bool gimmickHit, bool canUpgradeRelic)
        {
            if (gimmickHit) return DefeatCause.Gimmick;
            if (heroLevel < strongestEnemyLevel) return DefeatCause.LowLevel;
            if (canUpgradeRelic) return DefeatCause.WeakRelics;
            return DefeatCause.General;
        }

        public static string Line(DefeatCause cause, string gimmickHint = null)
        {
            switch (cause)
            {
                case DefeatCause.Gimmick when !string.IsNullOrEmpty(gimmickHint): return gimmickHint;
                case DefeatCause.LowLevel: return "레벨을 키워 능력치를 늘린 뒤에 다시 도전해 보자";
                case DefeatCause.WeakRelics: return "제단에서 성유물을 강화하면 더 세질 수 있어";
                default: return "빨리 맞히면 크리티컬! 수련에서 틀린 단어를 복습해 보자";
            }
        }

        // 끼운 성유물 중 지금 강화할 수 있는 것이 있는지
        public static bool CanUpgradeAny(IEnumerable<OwnedRelic> equipped, Inventory inventory)
        {
            if (equipped == null || inventory == null) return false;
            foreach (var relic in equipped)
                if (relic != null && RelicUpgrade.Check(relic, inventory) == UpgradeStatus.Ready) return true;
            return false;
        }
    }
}
