using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Field
{
    // 시작 칸에서 갈 수 있는 칸 (걷기 + 뗏목, #80). 데이터 검사(모든 상자·출입구에 갈 수 있는지, 보스 방 문을 열어야만 보스에게 가는지)가 쓴다.
    // gatesOpen: 보스 방 문(G)을 연 것으로 / bridgesOpen: 덩굴 다리(H)를 놓은 것으로 본다. 뗏목은 나루터(J)에서 강으로 들어가고 나루터로 내린다
    public static class FieldReach
    {
        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public static HashSet<Vector2Int> Flood(FieldMap map, bool gatesOpen = true, bool bridgesOpen = true, Vector2Int? from = null)
        {
            var start = from ?? map.Start;
            var seen = new HashSet<(Vector2Int cell, bool raft)> { (start, map.IsRaftWater(start)) };
            var queue = new Queue<(Vector2Int cell, bool raft)>(seen);
            while (queue.Count > 0)
            {
                var (cell, raft) = queue.Dequeue();
                foreach (var step in Steps)
                {
                    var next = cell + step;
                    var tile = map.Get(next);
                    (Vector2Int, bool)? reached = null;
                    if (raft)
                    {
                        if (map.IsRaftWater(next)) reached = (next, true);
                        else if (tile == FieldTile.Dock) reached = (next, false);
                    }
                    else
                    {
                        bool passable = map.IsWalkable(next) || gatesOpen && tile == FieldTile.Gate || bridgesOpen && tile == FieldTile.Bridge;
                        if (passable) reached = (next, false);
                        else if (map.Get(cell) == FieldTile.Dock && map.IsRaftWater(next)) reached = (next, true);
                    }
                    if (reached.HasValue && seen.Add(reached.Value)) queue.Enqueue(reached.Value);
                }
            }
            var cells = new HashSet<Vector2Int>();
            foreach (var (cell, _) in seen) cells.Add(cell);
            return cells;
        }

        // from에서 걸어서 가장 가까운 목표 칸까지의 길 (#133 처음 하는 사람에게 풀숲 가는 길 보여 주기).
        // from은 빼고 다음 칸부터 목표 칸까지, 갈 수 없으면 빈 목록. 목표에 닿은 뒤 같은 방향으로 이어진 목표 칸을 extra칸까지 더 간다 (풀숲 안으로)
        public static List<Vector2Int> PathToNearest(FieldMap map, Vector2Int from, Func<Vector2Int, bool> canStand,
            Func<FieldTile, bool> isGoal, int extra = 0)
        {
            var path = new List<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            Vector2Int? goal = null;
            while (queue.Count > 0 && goal == null)
            {
                var cell = queue.Dequeue();
                foreach (var step in Steps)
                {
                    var next = cell + step;
                    if (cameFrom.ContainsKey(next) || !map.InBounds(next) || !canStand(next)) continue;
                    cameFrom[next] = cell;
                    if (isGoal(map.Get(next)))
                    {
                        goal = next;
                        break;
                    }
                    queue.Enqueue(next);
                }
            }
            if (goal == null) return path;

            for (var cell = goal.Value; cell != from; cell = cameFrom[cell]) path.Add(cell);
            path.Reverse();
            var direction = path[path.Count - 1] - (path.Count > 1 ? path[path.Count - 2] : from);
            for (int i = 0; i < extra; i++)
            {
                var next = path[path.Count - 1] + direction;
                if (!map.InBounds(next) || !canStand(next) || !isGoal(map.Get(next))) break;
                path.Add(next);
            }
            return path;
        }

        // 그 칸 바로 옆(위·아래·왼·오른)에 갈 수 있는지 — 옆에서 [확인]으로 쓰는 물건용
        public static bool Touches(HashSet<Vector2Int> reachable, Vector2Int cell)
        {
            foreach (var step in Steps)
                if (reachable.Contains(cell + step)) return true;
            return false;
        }
    }
}
