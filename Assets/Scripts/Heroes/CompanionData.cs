using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Monsters;

namespace WordRPG.Heroes
{
    // 레벨이 되면 배우는 기술
    [Serializable]
    public class CompanionSkill
    {
        [SerializeField] private int level = 1;
        [SerializeField] private SkillData skill;

        public int Level => level;
        public SkillData Skill => skill;

        public CompanionSkill(int level, SkillData skill)
        {
            this.level = level;
            this.skill = skill;
        }
    }

    // 동료 (#77, 1.1 메아리 정글 — 사용자 결정): 주인공과 비슷한 '사람'. 파티 = 주인공 + 동료 1명.
    // 전투에서 자기 차례에 기술을 고르고 단어 문제를 풀고, 경험치를 같이 받아 레벨업하며 새 기술을 배운다. 성유물은 끼지 않는다
    [CreateAssetMenu(fileName = "NewCompanion", menuName = "WordRPG/Companion", order = 11)]
    public class CompanionData : ScriptableObject
    {
        [Tooltip("세이브에 저장되는 id — 출시 뒤에는 바꾸지 말 것")]
        [SerializeField] private string companionId;
        [SerializeField] private string displayName; // 예: "아라"
        [TextArea] [SerializeField] private string description;
        [Tooltip("한 줄 소개 (예: 정글 소녀 · 회복·보조)")]
        [SerializeField] private string role;
        [Tooltip("얼굴 그림. 비어 있으면 Art/NinjaAdventure/Companions/{companionId}.png")]
        [SerializeField] private Sprite portrait;
        [Header("능력치 = 기본 + 레벨당 성장 × (레벨-1)")]
        [SerializeField] private MonsterStats baseStats = new MonsterStats(50, 10, 10);
        [SerializeField] private MonsterStats growthPerLevel = new MonsterStats(7, 2, 2);
        [Tooltip("처음부터 쓰는 기본 기술")]
        [SerializeField] private SkillData basicSkill;
        [Tooltip("레벨이 되면 배우는 기술 (레벨 순)")]
        [SerializeField] private List<CompanionSkill> learnedSkills = new List<CompanionSkill>();

        public string CompanionId => companionId;
        public string DisplayName => displayName;
        public string Description => description;
        public string Role => role;
        public Sprite Portrait => portrait;
        public MonsterStats BaseStats => baseStats;
        public MonsterStats GrowthPerLevel => growthPerLevel;
        public SkillData BasicSkill => basicSkill;
        public IReadOnlyList<CompanionSkill> LearnedSkills => learnedSkills;

        public MonsterStats GetStats(int level) => baseStats + growthPerLevel * (Math.Max(1, level) - 1);

        // 그 레벨에서 쓸 수 있는 기술: 기본 기술 + 배운 기술 (배운 순)
        public List<SkillData> SkillsAt(int level)
        {
            var skills = new List<SkillData>();
            if (basicSkill != null) skills.Add(basicSkill);
            foreach (var learned in learnedSkills)
                if (learned != null && learned.Skill != null && learned.Level <= level && !skills.Contains(learned.Skill)) skills.Add(learned.Skill);
            return skills;
        }

        // fromLevel(포함 안 함) → toLevel(포함) 사이에 새로 배우는 기술 (레벨업 알림용)
        public List<SkillData> SkillsLearnedBetween(int fromLevel, int toLevel)
        {
            var skills = new List<SkillData>();
            foreach (var learned in learnedSkills)
                if (learned != null && learned.Skill != null && learned.Level > fromLevel && learned.Level <= toLevel) skills.Add(learned.Skill);
            return skills;
        }
    }
}
