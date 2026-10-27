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

    // 전투에서 보여 줄 모습 (#137 — 기술마다 다른 효과). 규칙에는 영향 없음 — 그림은 UI 쪽 표 SkillLooks.
    // 에셋에 번호로 저장되므로 새 종류는 맨 뒤에 (SkillLooksTests가 표에 빠진 종류를 잡음)
    public enum SkillFx
    {
        Auto,        // 종류대로: 공격 = 베기(적은 할퀴기) · 회복 = 빛 · 보호막 = 물방울 막
        Slash,       // 휘두르기 — 달려가 베기
        Claw,        // 할퀴기·깨물기·쪼기
        Heavy,       // 방망이·후려치기 — 크게 덮치고 흔들림
        Thunder,     // 번개·벼락 — 화면이 번쩍
        Ink,         // 잉크·먹물
        Water,       // 물 튀기기
        Plant,       // 덩굴
        Whip,        // 혀·꼬리 — 휘감는 줄
        Rock,        // 돌팔매 — 돌이 날아감
        Light,       // 반딧불·빛 — 반짝이
        Letters,     // 글자 소나기 — 방금 맞힌 단어의 글자가 쏟아짐
        Highlight,   // 형광펜 — 형광 한 줄이 적 전체를
        Sound,       // 울음 — 소리 고리가 퍼짐
        Spore,       // 포자·안개·실
        Swirl,       // 소용돌이·뒤죽박죽·꼬부랑
        HealLight,   // 빛으로 회복 (지혜의 빛·기억·쪽잠·상처약)
        HealLeaf,    // 잎으로 회복 (새싹·세계수·도토리)
        HealWater,   // 물로 회복 (조롱박·샘)
        Drain,       // 빼앗아 회복 (적 — 기억 흡수·꿀꺽·문장 삼키기)
        GuardBubble, // 물방울 막
        GuardEcho,   // 메아리 — 소리 고리
        GuardBook,   // 책·필통·백과 — 금빛 방패
        GuardTime,   // 모래시계
        GuardSeed    // 마침표 씨앗
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

        [Tooltip("전투 연출 (#137): 이 기술의 효과 그림·몸짓. Auto = 종류대로")]
        [SerializeField] private SkillFx fx;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string Description => description;
        public SkillKind Kind => kind;
        public SkillTarget Target => target;
        public int Power => power;
        public int ChargeTurns => chargeTurns;
        public QuizDirection QuizDirection => quizDirection;
        public SkillFx Fx => fx;

        public bool NeedsTargetChoice => target == SkillTarget.SingleEnemy || target == SkillTarget.SingleAlly;
    }
}
