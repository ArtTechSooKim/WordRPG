using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Battle
{
    public enum BattleEventType
    {
        QuizAnswered, // Correct, Mastery
        SkillUsed,    // Actor, Skill, Multiplier(공격 강도 배율, 기본 1)
        SkillFailed,  // Actor, Skill (오답이라 기술이 빗나감)
        Damage,       // Actor, Target, Amount(HP 피해), Absorbed(보호막 흡수), IsCritical
        Heal,         // Actor, Target, Amount, IsCritical
        Shield,       // Actor, Target, Amount, IsCritical
        ItemUsed,     // Actor, Item, Amount(회복한 HP)
        Defeated,     // Target
        RoundStarted, // Round
        Victory,
        Defeat,
        Combo,        // Actor, Amount(연속 정답 수) — 정답 직후, 기술 발동 전 (Combo.Label로 글자)
        Fled,         // Actor — 도망쳐서 전투가 끝남 (#53)
        FleeBlocked   // Actor — 보스전이라 도망칠 수 없음 (차례는 그대로)
    }

    // 전투 로직이 만들어내는 사건 기록. UI는 이 목록을 순서대로 연출만 한다
    public class BattleEvent
    {
        public BattleEventType Type { get; private set; }
        public BattleUnit Actor { get; private set; }
        public BattleUnit Target { get; private set; }
        public SkillData Skill { get; private set; }
        public ItemData Item { get; private set; }
        public int Amount { get; private set; }
        public int Absorbed { get; private set; }
        public bool IsCritical { get; private set; }
        public bool Correct { get; private set; }
        public MasteryChange Mastery { get; private set; }
        public int Round { get; private set; }
        public float Multiplier { get; private set; } = 1f;

        private BattleEvent(BattleEventType type) { Type = type; }

        public static BattleEvent QuizAnswered(BattleUnit actor, bool correct, MasteryChange mastery) =>
            new BattleEvent(BattleEventType.QuizAnswered) { Actor = actor, Correct = correct, Mastery = mastery };

        public static BattleEvent SkillUsed(BattleUnit actor, SkillData skill, float multiplier = 1f) =>
            new BattleEvent(BattleEventType.SkillUsed) { Actor = actor, Skill = skill, Multiplier = multiplier };

        public static BattleEvent SkillFailed(BattleUnit actor, SkillData skill) =>
            new BattleEvent(BattleEventType.SkillFailed) { Actor = actor, Skill = skill };

        public static BattleEvent Damage(BattleUnit actor, BattleUnit target, SkillData skill, int amount, int absorbed, bool critical) =>
            new BattleEvent(BattleEventType.Damage) { Actor = actor, Target = target, Skill = skill, Amount = amount, Absorbed = absorbed, IsCritical = critical };

        public static BattleEvent Heal(BattleUnit actor, BattleUnit target, SkillData skill, int amount, bool critical) =>
            new BattleEvent(BattleEventType.Heal) { Actor = actor, Target = target, Skill = skill, Amount = amount, IsCritical = critical };

        public static BattleEvent Shield(BattleUnit actor, BattleUnit target, SkillData skill, int amount, bool critical) =>
            new BattleEvent(BattleEventType.Shield) { Actor = actor, Target = target, Skill = skill, Amount = amount, IsCritical = critical };

        public static BattleEvent ItemUsed(BattleUnit actor, ItemData item, int healed) =>
            new BattleEvent(BattleEventType.ItemUsed) { Actor = actor, Target = actor, Item = item, Amount = healed };

        public static BattleEvent Defeated(BattleUnit target) =>
            new BattleEvent(BattleEventType.Defeated) { Target = target };

        public static BattleEvent RoundStarted(int round) =>
            new BattleEvent(BattleEventType.RoundStarted) { Round = round };

        public static BattleEvent Combo(BattleUnit actor, int streak) =>
            new BattleEvent(BattleEventType.Combo) { Actor = actor, Amount = streak };

        public static BattleEvent Fled(BattleUnit actor) => new BattleEvent(BattleEventType.Fled) { Actor = actor };

        public static BattleEvent FleeBlocked(BattleUnit actor) => new BattleEvent(BattleEventType.FleeBlocked) { Actor = actor };

        public static BattleEvent Victory() => new BattleEvent(BattleEventType.Victory);

        public static BattleEvent Defeat() => new BattleEvent(BattleEventType.Defeat);
    }
}
