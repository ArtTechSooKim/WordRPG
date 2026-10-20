using UnityEngine;
using WordRPG.Words;

namespace WordRPG.Monsters
{
    public enum SkillKind
    {
        Damage, // 공격
        Heal,   // 회복
        Guard   // 보호막 (적 공격을 흡수 — 다 깎일 때까지 그 전투 내내 남음, #120)
    }

    // 사용자 기준 대상. 적이 쓰면 'Enemy'는 주인공 쪽을 가리킨다
    public enum SkillTarget
    {
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies,
        Self
    }

    [CreateAssetMenu(fileName = "NewSkill", menuName = "WordRPG/Skill", order = 11)]
    public class SkillData : ScriptableObject
    {
        [SerializeField] private string skillId;
        [SerializeField] private string displayName; // 예: "찌르기"
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private SkillKind kind;
        [SerializeField] private SkillTarget target;
        [SerializeField] private int power = 10;

        [Tooltip("힘 모으기 (#120, 적 기술): 0이면 바로 씀. 1 이상이면 고른 차례엔 '힘을 모으고 있다'만 하고 그만큼 차례를 넘긴 뒤 발동 (보스의 큰 한 방)")]
        [SerializeField] private int chargeTurns;

        [Tooltip("이 스킬을 쓸 때 나오는 문제 유형. 강한 스킬일수록 어려운 '한→영'")]
        [SerializeField] private QuizDirection quizDirection = QuizDirection.EnglishToMeaning;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string Description => description;
        public SkillKind Kind => kind;
        public SkillTarget Target => target;
        public int Power => power;
        public int ChargeTurns => chargeTurns;
        public QuizDirection QuizDirection => quizDirection;

        public bool NeedsTargetChoice => target == SkillTarget.SingleEnemy || target == SkillTarget.SingleAlly;
    }
}
