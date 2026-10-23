using System.Collections.Generic;
using UnityEngine;
using WordRPG.Monsters;

namespace WordRPG.Heroes
{
    // 주인공 데이터: 레벨별 능력치, 늘 쓸 수 있는 기본 기술, 시작 성유물
    [CreateAssetMenu(fileName = "Hero", menuName = "WordRPG/Hero", order = 9)]
    public class HeroData : ScriptableObject
    {
        public const int SlotCount = 3; // 성유물을 끼우는 칸 수

        [SerializeField] private string displayName = "주인공";
        [TextArea]
        [SerializeField] private string description;
        [Tooltip("비워 두면 필드 주인공 그림(정면)을 쓴다")]
        [SerializeField] private Sprite portrait;

        [Header("능력치")]
        [Tooltip("레벨 1 능력치")]
        [SerializeField] private MonsterStats baseStats = new MonsterStats(60, 14, 10);
        [Tooltip("레벨이 1 오를 때마다 더해지는 값")]
        [SerializeField] private MonsterStats growthPerLevel = new MonsterStats(8, 3, 2);
        [SerializeField] private int startLevel = 3;

        [Header("기술·성유물")]
        [Tooltip("성유물과 상관없이 늘 쓸 수 있는 기술 (쉬운 문제)")]
        [SerializeField] private SkillData basicSkill;
        [Tooltip("새 게임을 시작할 때 가지고 있는 성유물 (앞에서부터 칸에 끼움)")]
        [SerializeField] private List<RelicData> startingRelics = new List<RelicData>();

        [Header("모습 (#124)")]
        [Tooltip("처음에 고르는 모습 — 첫 번째가 기본 (1.0부터의 소년). lookId는 세이브에 저장되니 바꾸지 말 것")]
        [SerializeField] private List<HeroLook> looks = new List<HeroLook>();

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Portrait => portrait;
        public MonsterStats BaseStats => baseStats;
        public MonsterStats GrowthPerLevel => growthPerLevel;
        public int StartLevel => startLevel;
        public SkillData BasicSkill => basicSkill;
        public IReadOnlyList<RelicData> StartingRelics => startingRelics;
        public IReadOnlyList<HeroLook> Looks => looks;
        public string DefaultLookId => looks.Count > 0 ? looks[0].LookId : HeroLook.Fallback;
        public bool HasLook(string lookId) => looks.Exists(look => look.LookId == lookId);

        public MonsterStats GetStats(int level) => baseStats + growthPerLevel * (level - 1);
    }
}
