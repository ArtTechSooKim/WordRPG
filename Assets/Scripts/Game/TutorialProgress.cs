using System.Collections.Generic;

namespace WordRPG.Game
{
    // 튜토리얼(안내) 중 이미 본 것 (#36 → #61). 세이브에 들어가서 다시 켜도 같은 안내는 다시 뜨지 않는다.
    // #61부터 안내는 화면을 막지 않는 한 줄(GuideHints): 필드는 걷기·달리기, 물건 옆 [확인], 풀숲 혼잣말을 그 상황에서 처음 한 번,
    // 전투는 기술 고르기·새 단어·첫 문제·강도(전투를 몇 번 해 본 뒤 열림)·상처약을 처음 만날 때 한 번. 메뉴 빨간 점(MenuBadges)도 여기 기록
    public class TutorialProgress
    {
        public const string Field = "field_basics";     // 걷기 → 달리기
        public const string Confirm = "field_confirm";  // 물건 옆에서 [확인]
        public const string Grass = "field_grass";      // 풀숲 근처 혼잣말
        public const string Skill = "battle_skill";
        public const string Intensity = "battle_intensity";
        public const string NewWord = "battle_new_word";
        public const string Quiz = "battle_quiz";
        public const string Potion = "battle_potion";
        public const string Preempt = "field_preempt"; // 보이는 몬스터가 가까이 오면 [선제공격] 버튼 (#73)

        public static readonly IReadOnlyList<string> All = new[]
        {
            Field, Confirm, Grass, Skill, Intensity, NewWord, Quiz, Potion, Preempt,
            MenuBadges.Dex, MenuBadges.Training, MenuBadges.Bag, MenuBadges.Map
        };

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
