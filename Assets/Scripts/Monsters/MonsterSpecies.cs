using System.Collections.Generic;
using UnityEngine;
using WordRPG.Items;

namespace WordRPG.Monsters
{
    public enum MonsterRole
    {
        Attacker, // 공격형
        Defender, // 방어형
        Supporter // 지원형
    }

    // 도망 등급 (#84, 2026-10-16 사용자 결정): 약함 = 항상 도망 / 보통 = 3/4은 바로, 1/4은 단어 문제 / 강함 = 늘 단어 문제(틀리면 차례를 놓침).
    // 에셋에 번호로 저장되므로 새 등급은 맨 뒤에. 칸이 없는 예전 에셋·테스트 몬스터는 약함 (진짜 몬스터는 SampleDataBuilder가 모두 정해 둠)
    public enum FleeTier
    {
        Weak,
        Normal,
        Strong
    }

    public static class MonsterRoleExtensions
    {
        public static string DisplayName(this MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Attacker: return "공격형";
                case MonsterRole.Defender: return "방어형";
                case MonsterRole.Supporter: return "지원형";
                default: return role.ToString();
            }
        }
    }

    // 몬스터 종(種) 데이터 — 필드에서 만나는 적. (아군 몬스터는 성유물로 바뀌었다: Heroes/RelicData)
    [CreateAssetMenu(fileName = "NewMonster", menuName = "WordRPG/Monster Species", order = 10)]
    public class MonsterSpecies : ScriptableObject
    {
        [Header("기본")]
        [SerializeField] private string speciesId;
        [SerializeField] private string displayName; // 예: "펜촉이"
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private MonsterRole role;
        [SerializeField] private Sprite sprite;
        [Tooltip("아트가 없을 때 쓰는 임시 색")]
        [SerializeField] private Color placeholderColor = Color.white;

        [Header("능력치")]
        [Tooltip("레벨 1 능력치")]
        [SerializeField] private MonsterStats baseStats = new MonsterStats(30, 10, 10);
        [Tooltip("레벨이 1 오를 때마다 더해지는 값")]
        [SerializeField] private MonsterStats growthPerLevel = new MonsterStats(4, 2, 2);

        [Header("스킬")]
        [SerializeField] private List<SkillData> skills = new List<SkillData>();

        [Header("적으로 등장할 때")]
        [Tooltip("레벨 1 기준. 실제 경험치 = 값 x 레벨")]
        [SerializeField] private int expReward = 5;
        [SerializeField] private int goldReward = 5;
        [SerializeField] private List<ItemDrop> drops = new List<ItemDrop>();

        [Header("수련 (#44)")]
        [Tooltip("수련용 허수아비: 맞아도 HP가 줄지 않는다 (피해 숫자는 그대로). 기술을 비워 두면 공격하지 않는다")]
        [SerializeField] private bool trainingDummy;

        [Header("도망 (#84)")]
        [Tooltip("약함 = 항상 도망 / 보통 = 1/4 확률로 단어 문제 / 강함 = 늘 단어 문제 (틀리면 차례를 놓침). 보스전은 원래 도망 불가")]
        [SerializeField] private FleeTier fleeTier = FleeTier.Weak;

        public FleeTier FleeTier => fleeTier;
        public string SpeciesId => speciesId;
        public string DisplayName => displayName;
        public string Description => description;
        public MonsterRole Role => role;
        public Sprite Sprite => sprite;
        public Color PlaceholderColor => placeholderColor;
        public MonsterStats BaseStats => baseStats;
        public MonsterStats GrowthPerLevel => growthPerLevel;
        public IReadOnlyList<SkillData> Skills => skills;
        public int ExpReward => expReward;
        public int GoldReward => goldReward;
        public IReadOnlyList<ItemDrop> Drops => drops;
        public bool IsTrainingDummy => trainingDummy;

        public MonsterStats GetStats(int level) => baseStats + growthPerLevel * (level - 1);
    }
}
