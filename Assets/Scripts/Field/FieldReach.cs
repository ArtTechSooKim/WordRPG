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

        // 그 칸 바로 옆(위·아래·왼·오른)에 갈 수 있는지 — 옆에서 [확인]으로 쓰는 물건용
        public static bool Touches(HashSet<Vector2Int> reachable, Vector2Int cell)
        {
            foreach (var step in Steps)
                if (reachable.Contains(cell + step)) return true;
            return false;
        }
    }
}
