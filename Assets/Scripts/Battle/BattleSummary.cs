using System.Collections.Generic;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Battle
{
    // 한 판의 결산 (#41, Figma 'Battle — 전투 결산'): 새로 만난 단어 · 틀린 단어 · 정답/오답 · 기술별로 적에게 준 피해 ·
    // 최대 콤보 · 크리티컬. 전투 엔진이 답과 사건을 넣고, 결산 화면이 읽는다
    public class BattleSummary
    {
        private readonly List<WordEntry> newWords = new List<WordEntry>();
        private readonly List<WordEntry> wrongWords = new List<WordEntry>();
        private readonly Dictionary<SkillData, int> damageBySkill = new Dictionary<SkillData, int>();
        private readonly List<SkillData> skillOrder = new List<SkillData>(); // 처음 쓴 순서 (같은 피해면 먼저 쓴 기술)

        public IReadOnlyList<WordEntry> NewWords => newWords;
        public IReadOnlyList<WordEntry> WrongWords => wrongWords;
        public int Correct { get; private set; }
        public int Wrong { get; private set; }
        public int Criticals { get; private set; }
        public int MaxCombo { get; private set; }
        public int TotalDamage { get; private set; } // 아군이 적에게 준 HP 피해

        // 문제에 답할 때마다 (시간 초과도 오답). 같은 단어는 한 번만 적는다
        public void RecordAnswer(WordEntry word, bool isNewWord, bool correct)
        {
            if (correct) Correct++;
            else Wrong++;
            if (word == null) return;
            if (isNewWord && !newWords.Contains(word)) newWords.Add(word);
            if (!correct && !wrongWords.Contains(word)) wrongWords.Add(word);
        }

        public void Record(IEnumerable<BattleEvent> events)
        {
            foreach (var e in events)
            {
                if (e.Type == BattleEventType.Combo && e.Amount > MaxCombo) MaxCombo = e.Amount;
                if (e.Type != BattleEventType.Damage || e.Actor == null || !e.Actor.IsPlayerSide || e.Target == null || e.Target.IsPlayerSide) continue;
                if (e.IsCritical) Criticals++;
                TotalDamage += e.Amount;
                if (e.Skill == null) continue;
                if (!damageBySkill.ContainsKey(e.Skill))
                {
                    damageBySkill[e.Skill] = 0;
                    skillOrder.Add(e.Skill);
                }
                damageBySkill[e.Skill] += e.Amount;
            }
        }

        public int DamageOf(SkillData skill) => skill != null && damageBySkill.TryGetValue(skill, out var amount) ? amount : 0;

        // 피해를 가장 많이 준 기술 (없으면 null)
        public SkillData TopSkill
        {
            get
            {
                SkillData best = null;
                foreach (var skill in skillOrder)
                    if (best == null || damageBySkill[skill] > damageBySkill[best]) best = skill;
                return best != null && damageBySkill[best] > 0 ? best : null;
            }
        }
    }
}
