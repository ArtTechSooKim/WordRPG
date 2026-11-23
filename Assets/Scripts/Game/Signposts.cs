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

        // 헷갈 안개 칸이 걷혔는지 (맡은 이정표를 바로잡았는지). 숨은 안개 'V'(#292)는 이정표를 모두 바로잡았는지,
        // 보스 뒤 숨은 안개 'Z'(#292)는 그 지역 보스를 이기고 가까이 가서 걷히는 모습을 봤는지 (WorldState 이벤트)
        public static bool IsHazeCleared(FieldArea area, WorldState world, Vector2Int haze)
        {
            var tile = area.Map.Get(haze);
            if (tile == FieldTile.SecretHaze) return All(area).Count > 0 && AllFixed(area, world);
            if (tile == FieldTile.BossHaze) return BossHazeOpen(area, world) && world.HasEvent(BossHazeEventId(area));
            var signpost = area.Map.SignpostOf(haze);
            return signpost.HasValue && IsFixed(area, world, signpost.Value);
        }

        // 부딪히면 맴도는 칸인지: 맴도는 안개, 또는 아직 안 걷힌 헷갈 안개·숨은 안개
        public static bool Loops(FieldArea area, WorldState world, Vector2Int cell)
        {
            var tile = area.Map.Get(cell);
            return tile == FieldTile.LoopHaze || (tile == FieldTile.Haze || tile == FieldTile.SecretHaze || tile == FieldTile.BossHaze) && !IsHazeCleared(area, world, cell);
        }

        // 이정표를 모두 바로잡았는지 (이정표가 없는 지역은 참) — 보스 방 문(#291) · 숨은 안개(#292)
        public static bool AllFixed(FieldArea area, WorldState world) => FixedCount(area, world) >= All(area).Count;

        // 숨은 안개 칸 (#292) — 마지막 이정표를 바로잡으면 걷히는 모습을 보여 줌
        public static List<Vector2Int> SecretHaze(FieldArea area) => area.Map.FindAll(FieldTile.SecretHaze);

        // 보스 뒤 숨은 안개 (#292 사용자: 너구리 공터는 숲 보스를 이긴 뒤 — 콘텐츠를 한 번에 소비하지 않게).
        // 보스를 이기면 걷힐 수 있고, 가까이 가면 걷히는 모습을 한 번 보여 준 뒤(이벤트) 지나갈 수 있음
        public static List<Vector2Int> BossHaze(FieldArea area) => area.Map.FindAll(FieldTile.BossHaze);
        public static bool BossHazeOpen(FieldArea area, WorldState world) => area.Boss != null && world.IsBossDefeated(area.BossId);
        public static string BossHazeEventId(FieldArea area) => "boss_haze:" + area.AreaId;
        public const int BossHazeNoticeRange = 4; // 이만큼 가까이 오면 걷힘

        // 지금 걷힐 차례인지: 보스를 이겼고 아직 안 걷혔고 주인공이 가까이 옴
        public static bool BossHazeDue(FieldArea area, WorldState world, Vector2Int hero)
        {
            if (!BossHazeOpen(area, world) || world.HasEvent(BossHazeEventId(area))) return false;
            foreach (var cell in BossHaze(area))
                if (Mathf.Abs(cell.x - hero.x) + Mathf.Abs(cell.y - hero.y) <= BossHazeNoticeRange) return true;
            return false;
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
