using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    public enum ShrineState
    {
        None,        // 이 지역엔 유적 제단이 없음
        NeedTablets, // 석판을 덜 풀었음 → "석판의 빈칸을 먼저 채워야 할 것 같다 (2/4)"
        NeedWords,   // 석판은 다 풀었지만 이 지역 단어가 모자람 → "메아리가 아직 약하다… 정글 단어를 더 모으자 (41/60)"
        Ready,       // 다 갖춤 → [확인]하면 동료가 깨어남 (조우 연출)
        Awakened     // 이미 동료를 얻음 → 빈 받침
    }

    // 고대 유적 (#80, 1.1 메아리 정글 — GDD 9-6 사용자 결정): 단어 석판(T) 4개를 모두 풀고 + 정글 단어 60개를 발견하면
    // 유적 제단(A)에서 동료가 깨어난다. 푼 석판은 세이브(WorldState.solvedTablets), 깨어난 동료는 세이브의 동료 목록
    public static class Ruins
    {
        public static bool HasShrine(FieldArea area) => area != null && area.Map.Find(FieldTile.Shrine).HasValue;

        public static List<Vector2Int> Tablets(FieldArea area) => area.Map.FindAll(FieldTile.Tablet);

        public static bool IsTabletSolved(FieldArea area, WorldState world, Vector2Int tablet) => world.IsTabletSolved(area.TabletId(tablet));

        public static int TabletsSolved(FieldArea area, WorldState world)
        {
            int solved = 0;
            foreach (var tablet in Tablets(area))
                if (IsTabletSolved(area, world, tablet)) solved++;
            return solved;
        }

        public static void SolveTablet(FieldArea area, WorldState world, Vector2Int tablet) => world.MarkTabletSolved(area.TabletId(tablet));

        public static bool IsAwakened(FieldArea area, GameSession session)
        {
            if (area.ShrineCompanion == null) return false;
            foreach (var companion in session.Companions)
                if (companion.Data == area.ShrineCompanion) return true;
            return false;
        }

        public static ShrineState State(FieldArea area, GameSession session)
        {
            if (!HasShrine(area) || area.ShrineCompanion == null) return ShrineState.None;
            if (IsAwakened(area, session)) return ShrineState.Awakened;
            if (TabletsSolved(area, session.World) < Tablets(area).Count) return ShrineState.NeedTablets;
            if (BossGate.WordsFound(area, session) < area.ShrineWords) return ShrineState.NeedWords;
            return ShrineState.Ready;
        }
    }

    // 레버와 덩굴 다리 (#80): 레버를 당기면 가장 가까운 덩굴 다리가 놓인다 (FieldMap.BridgeOf). 당긴 레버는 세이브(WorldState.pulledLevers)
    public static class Levers
    {
        public static bool IsPulled(FieldArea area, WorldState world, Vector2Int lever) => world.IsLeverPulled(area.LeverId(lever));

        // 다리 칸이 놓였는지 (맡은 레버를 당겼는지)
        public static bool IsBridgeBuilt(FieldArea area, WorldState world, Vector2Int bridge)
        {
            var lever = area.Map.LeverOf(bridge);
            return lever.HasValue && IsPulled(area, world, lever.Value);
        }

        // 당긴다. 반환값: 이번에 놓인 다리 칸 (이미 당겼으면 빈 목록)
        public static IReadOnlyList<Vector2Int> Pull(FieldArea area, WorldState world, Vector2Int lever)
        {
            if (area.Map.Get(lever) != FieldTile.Lever || IsPulled(area, world, lever)) return new Vector2Int[0];
            world.MarkLeverPulled(area.LeverId(lever));
            return area.Map.BridgeOf(lever);
        }
    }

    // 칸의 지금 상태 — 필드 그림·미니맵(끝난 모습)과 걷기(지나갈 수 있게 된 칸)가 같은 규칙을 쓰게
    public static class CellState
    {
        // 끝난 모습으로 그릴 칸: 연 상자 · 쓰러뜨린 보스 자리 · 사전이 놓인 받침대 · 연 보스 방 문 · 가져간 열쇠 ·
        // 당긴 레버 · 놓인 덩굴 다리 · 푼 석판 · 동료가 깨어난 제단 · 밝힌 글자 반딧돌 (#112)
        public static bool IsDone(FieldArea area, GameSession session, Vector2Int cell)
        {
            var world = session.World;
            switch (area.Map.Get(cell))
            {
                case FieldTile.Chest: return world.IsChestOpened(area.ChestId(cell));
                case FieldTile.Boss: return world.IsBossDefeated(area.BossId);
                case FieldTile.Lectern: return BossDictionary.IsUnlocked(area, world);
                case FieldTile.Gate: return BossGate.IsOpen(area, session);
                case FieldTile.Key: return BossGate.HasKey(area, session);
                case FieldTile.Lever: return Levers.IsPulled(area, world, cell);
                case FieldTile.Bridge: return Levers.IsBridgeBuilt(area, world, cell);
                case FieldTile.Tablet: return Ruins.IsTabletSolved(area, world, cell);
                case FieldTile.Shrine: return Ruins.IsAwakened(area, session);
                case FieldTile.GlowStone: return GlowStones.IsLit(area, world, cell);
                default: return false;
            }
        }

        // 원래 막힌 칸이지만 이제 지나갈 수 있는 칸: 쓰러뜨린 보스 자리(사전은 쉼터로 옮겨짐, #45) · 연 보스 방 문(#76) · 놓인 덩굴 다리(#80)
        public static bool IsPassable(FieldArea area, GameSession session, Vector2Int cell)
        {
            switch (area.Map.Get(cell))
            {
                case FieldTile.Boss: return session.World.IsBossDefeated(area.BossId);
                case FieldTile.Gate: return BossGate.IsOpen(area, session);
                case FieldTile.Bridge: return Levers.IsBridgeBuilt(area, session.World, cell);
                default: return false;
            }
        }
    }
}
