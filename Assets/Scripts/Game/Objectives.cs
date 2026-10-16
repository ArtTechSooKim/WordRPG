using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    public enum ObjectiveKind
    {
        FirstBattle, // 아직 한 번도 안 싸움 → 풀숲에서 몬스터 만나기
        GoToArea,    // 아직 안 쓰러뜨린 보스가 다른 지역에 있음 → 다음 출입구로
        DefeatBoss,  // 이 지역의 보스
        FillDex,     // 갈 수 있는 보스를 다 쓰러뜨림 → 도감 채우기
        FindKey,     // 이 지역 보스 방 문이 잠겼고 열쇠가 없음 (#76) — 열쇠 자리는 별로 알려 주지 않는다 (찾는 재미)
        GatherWords, // 열쇠는 있지만 이 지역 단어를 덜 발견함 (#76)
        OpenGate,    // 문을 열 수 있음 (#76) — 문에 별
        // 유적 (#87, 정글 — 동료는 선택이라 열쇠를 찾기 전까지만 보여 줌. 열쇠를 찾으면 위의 보스 목표로)
        SolveTablets,  // 유적의 석판을 채우자 n/4 — 아직 안 푼 석판에 별
        GatherEcho,    // 석판은 다 풀었고 이 지역 단어가 모자람 → 단어를 모아 메아리를 깨우자 n/60
        WakeCompanion  // 다 갖춤 → 유적 제단에서 동료를 깨우자 — 제단에 별
    }

    // 지금 할 일 한 줄 (#61 '목표 한 줄'): 화면에 늘 떠 있고, 미니맵·지도에 그 칸을 별로 표시한다
    public readonly struct Objective
    {
        public ObjectiveKind Kind { get; }
        public FieldArea Next { get; }         // GoToArea: 바로 다음에 들어갈 지역
        public FieldArea Destination { get; }  // GoToArea·DefeatBoss: 보스가 있는 지역
        public Vector2Int? Cell { get; }       // 지금 지역에서 가리킬 칸 (출입구·보스). 없으면 null

        public Objective(ObjectiveKind kind, FieldArea next = null, FieldArea destination = null, Vector2Int? cell = null)
        {
            Kind = kind;
            Next = next;
            Destination = destination;
            Cell = cell;
        }
    }

    // 목표 정하기: 처음엔 첫 전투 → 지금 갈 수 있는 곳 중 가장 가까운(지역을 덜 건너는) 아직 안 쓰러뜨린 보스 →
    // 다 쓰러뜨렸으면 도감 채우기. 지역 사이 길은 출입구 연결(AreaExit)을 따라가고, 보스가 열어야 하는 길은 열렸을 때만
    public static class Objectives
    {
        public static Objective Current(GameSession session, FieldArea current)
        {
            if (session == null || current == null) return new Objective(ObjectiveKind.FillDex);
            if (session.Record.BattlesFought == 0) return new Objective(ObjectiveKind.FirstBattle);

            // 지역 단위 너비 우선 탐색: 처음 들어선 출입구(지금 지역의 몇 번째 출입구)를 함께 기억한다
            var firstExit = new Dictionary<FieldArea, int> { [current] = -1 };
            var queue = new Queue<FieldArea>();
            queue.Enqueue(current);
            while (queue.Count > 0)
            {
                var area = queue.Dequeue();
                if (HasUndefeatedBoss(session, area))
                {
                    if (area == current) return BossObjective(session, area);
                    int index = firstExit[area];
                    var doors = current.Map.Doors;
                    Vector2Int? cell = index >= 0 && index < doors.Count ? doors[index] : (Vector2Int?)null;
                    return new Objective(ObjectiveKind.GoToArea, current.Exits[index].Target, area, cell);
                }
                for (int i = 0; i < area.Exits.Count; i++)
                {
                    var exit = area.Exits[i];
                    if (exit.Target == null || firstExit.ContainsKey(exit.Target) || !IsOpen(session, exit)) continue;
                    firstExit[exit.Target] = area == current ? i : firstExit[area];
                    queue.Enqueue(exit.Target);
                }
            }
            return new Objective(ObjectiveKind.FillDex);
        }

        // 지금 지역 보스: 보스 방 문이 잠겼으면 문부터 (#76). 유적이 있는 지역은 열쇠를 찾기 전까지 유적 먼저 (#87 —
        // 깨어난 동료가 열쇠 자리를 알려 주는 흐름, GDD 9-6). 동료는 선택이라 열쇠를 먼저 찾으면 바로 보스 목표로
        private static Objective BossObjective(GameSession session, FieldArea area)
        {
            var gate = BossGate.State(area, session);
            if (gate == BossGateState.NeedKey)
            {
                switch (Ruins.State(area, session))
                {
                    case ShrineState.NeedTablets:
                        foreach (var tablet in Ruins.Tablets(area))
                            if (!Ruins.IsTabletSolved(area, session.World, tablet))
                                return new Objective(ObjectiveKind.SolveTablets, null, area, tablet);
                        break;
                    case ShrineState.NeedWords: return new Objective(ObjectiveKind.GatherEcho, null, area);
                    case ShrineState.Ready: return new Objective(ObjectiveKind.WakeCompanion, null, area, area.Map.Find(FieldTile.Shrine));
                }
            }
            switch (gate)
            {
                case BossGateState.NeedKey: return new Objective(ObjectiveKind.FindKey, null, area);
                case BossGateState.NeedWords: return new Objective(ObjectiveKind.GatherWords, null, area);
                case BossGateState.Ready:
                    var cells = BossGate.Cells(area);
                    return new Objective(ObjectiveKind.OpenGate, null, area, cells[cells.Count / 2]);
                default: return new Objective(ObjectiveKind.DefeatBoss, null, area, area.Map.BossPosition);
            }
        }

        private static bool HasUndefeatedBoss(GameSession session, FieldArea area) =>
            area.Boss != null && !session.World.IsBossDefeated(area.BossId);

        private static bool IsOpen(GameSession session, AreaExit exit) =>
            exit.OpenedByBossOf == null || session.World.IsBossDefeated(exit.OpenedByBossOf.BossId);
    }

    // 메뉴 버튼의 빨간 점 (#61 '처음 쓸 때만 반짝'): 처음 의미가 생겼을 때 켜지고, 그 메뉴를 한 번 열면 꺼진다.
    // 본 것은 튜토리얼과 같은 기록(TutorialProgress)에 남아 [튜토리얼 다시 보기]로 다시 켜진다
    public static class MenuBadges
    {
        public const string Dex = "badge_dex";
        public const string Training = "badge_training";
        public const string Bag = "badge_bag";
        public const string Map = "badge_map";
        public const string Companion = "badge_companion"; // 가방 버튼: 동료를 처음 얻으면 ('동료' 탭이 생김, #81)
        public const int TrainingWords = 5; // 수련은 도감 단어가 이만큼 모이면

        public static readonly IReadOnlyList<string> All = new[] { Dex, Training, Bag, Map, Companion };

        public static bool IsUnlocked(string id, GameSession session)
        {
            switch (id)
            {
                case Dex: return session.Vocabulary.DiscoveredCount >= 1;
                case Training: return session.Vocabulary.DiscoveredCount >= TrainingWords;
                case Bag:
                    if (session.Hero.Relics.Count >= 2) return true;
                    foreach (var stack in session.Inventory.Stacks)
                        if (stack.Count > 0) return true;
                    return false;
                case Map: return session.World.ExploredAreaCount >= 2;
                case Companion: return session.Companions.Count > 0;
                default: return false;
            }
        }

        public static bool ShouldShow(string id, GameSession session) =>
            session != null && !session.Tutorials.Has(id) && IsUnlocked(id, session);
    }

    // 전투 기능을 하나씩 열기 (#61): 처음 하는 사람은 전투를 몇 번 해 본 뒤에 공격 강도 고르기가 열린다
    public static class BattleUnlocks
    {
        public const int IntensityAfterBattles = 2;

        public static bool IntensityUnlocked(PlayerRecord record) => record != null && record.BattlesFought >= IntensityAfterBattles;
    }
}
