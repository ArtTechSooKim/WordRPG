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
        public const string Raft = "field_raft";       // 처음 나루터에 서면 '물 쪽으로 가면 탈 수 있을 것 같다' (#81)
        public const string CompanionTurn = "battle_companion"; // 처음 동료 차례: '아라 차례 — 아라도 단어를 맞혀야 기술이 나가요' (#81, GDD 9-6)
        // #124 새 튜토리얼 (사용자): 환영 → (프롤로그·이름) → 걷기·달리기 → 풀숲으로 가서 싸우기 → 첫 승리 뒤 목표 까먹대왕 상기
        public const string GrassFight = "field_grass_fight"; // '풀숲으로 가서 몬스터와 싸워 보세요' (첫 전투까지)
        public const string Goal = "field_goal";              // 첫 승리 뒤 '목표는 잊혀진 서고의 까먹대왕을 물리치는 것입니다'
        // #131 입장 영상(#129)을 본 기록 (사용자: 이미 하던 사람도 영상을 볼 수 있게) — 새 게임이든 하던 세이브든 못 본 사람은 필드에서 한 번.
        // 튜토리얼 다시 보기(Clear)로는 지우지 않음 — 다시 켤 때마다 영상이 나오지 않게 (그래서 All에도 없음)
        public const string Intro = "intro_video";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Field, Confirm, Grass, Skill, Intensity, NewWord, Quiz, Potion, Preempt, Raft, CompanionTurn, GrassFight, Goal,
            MenuBadges.Dex, MenuBadges.Training, MenuBadges.Bag, MenuBadges.Map, MenuBadges.Companion
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
        // 튜토리얼 다시 보기: 본 안내·빨간 점을 지움 (입장 영상을 본 기록은 남김)
        public void Clear() => seen.RemoveWhere(id => id != Intro);

        public void MarkAll()
        {
            foreach (var id in All) seen.Add(id);
        }
    }
}
