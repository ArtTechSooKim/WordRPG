using System.Collections.Generic;

namespace WordRPG.Game
{
    // 튜토리얼(안내) 중 이미 본 것 (#36). 세이브에 들어가서 다시 켜도 같은 안내는 다시 뜨지 않는다.
    // 필드 첫 안내는 여러 단계 묶음, 전투 안내는 그 상황(기술 고르기·강도·새 단어·문제·오답…)을 처음 만날 때 하나씩.
    // [건너뛰기]를 누르면 남은 안내를 모두 본 것으로 한다
    public class TutorialProgress
    {
        public const string Field = "field_basics";
        public const string Skill = "battle_skill";
        public const string Intensity = "battle_intensity";
        public const string NewWord = "battle_new_word";
        public const string Quiz = "battle_quiz";
        public const string Wrong = "battle_wrong";
        public const string Combo = "battle_combo";
        public const string Potion = "battle_potion";

        public static readonly IReadOnlyList<string> All = new[] { Field, Skill, Intensity, NewWord, Quiz, Wrong, Combo, Potion };

        private readonly HashSet<string> seen = new HashSet<string>();

        public TutorialProgress() { }

        public TutorialProgress(IEnumerable<string> seenIds)
        {
            if (seenIds == null) return;
            foreach (var id in seenIds) if (!string.IsNullOrEmpty(id)) seen.Add(id);
        }

        public IEnumerable<string> Seen => seen;
        public bool Has(string id) => seen.Contains(id);
        public void Mark(string id) => seen.Add(id);
        public void Clear() => seen.Clear();

        public void MarkAll()
        {
            foreach (var id in All) seen.Add(id);
        }
    }
}
