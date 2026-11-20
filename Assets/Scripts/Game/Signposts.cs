using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    // 뒤섞인 이정표 미로 (#279, 1.4 숲 넓히기 — GDD 9-15): 헷갈너구리가 이정표 글자를 뒤섞어 숲 오른쪽이 미로가 됨 (스토리 2장).
    // 이정표 'X'의 바른 철자를 고르면 바로잡히고, 가장 가까운 헷갈 안개 'M'이 걷혀 길이 열린다 (FieldMap.HazeOf — 레버 → 덩굴 다리와 같은 방식).
    // 걷히기 전 안개와 맴도는 안개 'W'(가짜 길)는 막힘 — 부딪히면 '안개 속을 맴돌다 제자리' (화면 FieldScreen.Forest).
    // 바로잡은 이정표는 세이브(WorldState.fixedSignposts, id = "지역:x,y" — 상자 id와 같은 방식)
    public static class Signposts
    {
        public static List<Vector2Int> All(FieldArea area) => area.Map.FindAll(FieldTile.Signpost);

        public static bool IsFixed(FieldArea area, WorldState world, Vector2Int signpost) =>
            area.Map.Get(signpost) == FieldTile.Signpost && world.IsSignpostFixed(area.SignpostId(signpost));

        // 바로잡는다. 반환값: 이번에 걷힌 안개 칸 (이미 바로잡았거나 이정표가 아니면 빈 목록)
        public static IReadOnlyList<Vector2Int> Fix(FieldArea area, WorldState world, Vector2Int signpost)
        {
            if (area.Map.Get(signpost) != FieldTile.Signpost || IsFixed(area, world, signpost)) return new Vector2Int[0];
            world.MarkSignpostFixed(area.SignpostId(signpost));
            return area.Map.HazeOf(signpost);
        }

        // 헷갈 안개 칸이 걷혔는지 (맡은 이정표를 바로잡았는지)
        public static bool IsHazeCleared(FieldArea area, WorldState world, Vector2Int haze)
        {
            var signpost = area.Map.SignpostOf(haze);
            return signpost.HasValue && IsFixed(area, world, signpost.Value);
        }

        // 부딪히면 맴도는 칸인지: 맴도는 안개, 또는 아직 안 걷힌 헷갈 안개
        public static bool Loops(FieldArea area, WorldState world, Vector2Int cell)
        {
            var tile = area.Map.Get(cell);
            return tile == FieldTile.LoopHaze || tile == FieldTile.Haze && !IsHazeCleared(area, world, cell);
        }

        public static int FixedCount(FieldArea area, WorldState world)
        {
            int count = 0;
            foreach (var signpost in All(area))
                if (IsFixed(area, world, signpost)) count++;
            return count;
        }

        // 지금 걸어서 갈 수 있는 칸 (걷힌 안개만, 보스 방 문은 연 경우만) — 목표 한 줄이 쓴다
        public static HashSet<Vector2Int> Reach(FieldArea area, GameSession session) =>
            FieldReach.Flood(area.Map, BossGate.IsOpen(area, session), true, null, cell => IsHazeCleared(area, session.World, cell));

        // 다음에 바로잡을 이정표 (목표 별): 지금 닿는 아직 안 바로잡은 이정표 중 맵에서 먼저 나오는 것. 없으면 null
        public static Vector2Int? Next(FieldArea area, GameSession session, HashSet<Vector2Int> reach = null)
        {
            var all = All(area);
            if (all.Count == 0) return null;
            reach = reach ?? Reach(area, session);
            foreach (var signpost in all)
                if (!IsFixed(area, session.World, signpost) && FieldReach.Touches(reach, signpost)) return signpost;
            return null;
        }
    }
}
