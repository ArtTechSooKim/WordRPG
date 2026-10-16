using System;
using UnityEngine;

namespace WordRPG.Battle
{
    // 전투 밸런스 값. 플레이테스트로 조정
    [Serializable]
    public class BattleConfig
    {
        [Tooltip("이 시간 안에 못 고르면 오답 처리")]
        [SerializeField] private float answerTimeLimitSeconds = 10f;
        [Tooltip("이 시간 안에 맞히면 크리티컬")]
        [SerializeField] private float criticalTimeSeconds = 3f;
        [SerializeField] private float criticalMultiplier = 1.5f;
        [Tooltip("데미지/회복량 ±랜덤 폭 (0.1 = ±10%)")]
        [SerializeField] private float variance = 0.1f;
        [Tooltip("정답 1개당 주인공이 추가로 받는 경험치 — 공부가 곧 성장")]
        [SerializeField] private int expPerCorrectAnswer = 2;
        [Tooltip("연속 정답 콤보 한 단계마다 아군 기술 피해 증가 (0.05 = +5%, 6단계면 +30%)")]
        [SerializeField] private float comboBonusPerStep = 0.05f;
        [Tooltip("공격 기술 강도: n번째 값 = 단어 n개를 연속으로 맞혀야 할 때의 피해 배율 (하나라도 틀리면 이번 턴 공격 실패)")]
        [SerializeField] private float[] intensityMultipliers = { 1f, 1.2f, 1.5f, 2f };
        [Tooltip("보통 등급 몬스터에게서 도망칠 때 단어 문제가 나올 확률 (#84 사용자: 1/4 — 나머지 3/4은 바로 도망)")]
        [Range(0f, 1f)]
        [SerializeField] private float normalFleeQuizChance = 0.25f;
        [Tooltip("주인공이 몬스터보다 레벨이 높을 때, 한 레벨마다 그 몬스터 경험치가 줄어드는 비율 (#88 시뮬레이션: 0.25 = 25%씩 → 보스 앞 주인공이 보스보다 3레벨쯤 위)")]
        [SerializeField] private float expFalloffPerLevel = 0.25f;
        [Tooltip("레벨 차이가 아무리 커도 남는 경험치 비율 (#88)")]
        [SerializeField] private float minExpFactor = 0.05f;
        [Tooltip("적이 회복 기술을 쓰는 조건: HP가 이 비율 아래일 때만 (#88 — 가득 찬데 회복하지 않게)")]
        [SerializeField] private float enemyHealBelow = 0.5f;
        [Tooltip("적 한 마리가 한 전투에서 회복할 수 있는 횟수 (#88 — 보스전이 끝없이 길어지지 않게)")]
        [SerializeField] private int enemyHealLimit = 2;

        private static readonly float[] DefaultIntensity = { 1f, 1.2f, 1.5f, 2f };

        public float AnswerTimeLimitSeconds => answerTimeLimitSeconds;
        public float CriticalTimeSeconds => criticalTimeSeconds;
        public float CriticalMultiplier => criticalMultiplier;
        public float Variance => variance;
        public int ExpPerCorrectAnswer => expPerCorrectAnswer;
        public float ComboBonusPerStep => comboBonusPerStep;
        public float NormalFleeQuizChance => normalFleeQuizChance;
        public float EnemyHealBelow => enemyHealBelow;
        public int EnemyHealLimit => enemyHealLimit;

        // 레벨 차이 경험치 (#88): 몬스터 레벨 이상이면 그대로, 주인공이 높을수록 한 레벨마다 expFalloffPerLevel씩 줄고 minExpFactor에서 멈춤
        public float ExpFactor(int heroLevel, int enemyLevel)
        {
            int gap = heroLevel - enemyLevel;
            if (gap <= 0) return 1f;
            return Math.Max(minExpFactor, 1f - expFalloffPerLevel * gap);
        }

        // 고를 수 있는 가장 높은 강도 (= 연속으로 맞혀야 할 단어 수의 최대)
        public int MaxIntensity => Multipliers.Length;

        // 강도(단어 수 1~MaxIntensity)의 피해 배율
        public float IntensityMultiplier(int intensity) =>
            Multipliers[Math.Max(0, Math.Min(intensity, Multipliers.Length) - 1)];

        private float[] Multipliers => intensityMultipliers != null && intensityMultipliers.Length > 0 ? intensityMultipliers : DefaultIntensity;

        public BattleConfig() { }

        public BattleConfig(float answerTimeLimitSeconds, float criticalTimeSeconds, float criticalMultiplier,
            float variance, int expPerCorrectAnswer, float comboBonusPerStep = 0.05f, float[] intensityMultipliers = null,
            float normalFleeQuizChance = 0.25f)
        {
            this.normalFleeQuizChance = normalFleeQuizChance;
            if (intensityMultipliers != null) this.intensityMultipliers = intensityMultipliers;
            this.answerTimeLimitSeconds = answerTimeLimitSeconds;
            this.criticalTimeSeconds = criticalTimeSeconds;
            this.criticalMultiplier = criticalMultiplier;
            this.variance = variance;
            this.expPerCorrectAnswer = expPerCorrectAnswer;
            this.comboBonusPerStep = comboBonusPerStep;
        }
    }
}
