using UnityEngine;

namespace WordRPG.Field
{
    // 뗏목으로 빠른 물살 거스르기 (#235 사용자 ㉛: "강 쪽으로 뗏목을 틀어 봤을 것 같다"): 뗏목이 다니는 물에 흘러드는 얕은 강(> < ^ v)으로
    // 물살을 거슬러 MaxUpstream칸까지 들어갈 수 있다. 멈추거나 끝까지 가면 물살에 밀려 뗏목 물로 돌아온다 (FieldScreen.Jungle).
    // 정글 가운데를 가로지르는 '>' 강이 큰 강으로 흘러드는 어귀
    public static class RaftCurrent
    {
        public const int MaxUpstream = 3; // 사용자: 3칸쯤

        // 이 칸이 뗏목 물에서 물살을 거슬러 몇 번째 칸인지 (1~MaxUpstream). 얕은 강이 아니거나 너무 멀면 0
        public static int Depth(FieldMap map, Vector2Int cell)
        {
            var flow = map.StreamDirection(cell);
            if (!flow.HasValue) return 0;
            var down = flow.Value.ToOffset();
            for (int k = 1; k <= MaxUpstream; k++)
            {
                var next = cell + down * k;
                if (map.IsRaftWater(next)) return k;
                if (map.StreamDirection(next) != flow) return 0;
            }
            return 0;
        }

        // 뗏목을 탄 채로 from → to로 갈 수 있는지 (뗏목 물이나 어귀 안에서, 어귀 안으로)
        public static bool CanEnter(FieldMap map, Vector2Int from, Vector2Int to) =>
            (map.IsRaftWater(from) || Depth(map, from) > 0) && Depth(map, to) > 0;

        // 물살에 밀려 갈 칸 (어귀 안이 아니면 null)
        public static Vector2Int? Downstream(FieldMap map, Vector2Int cell)
        {
            if (Depth(map, cell) == 0) return null;
            return cell + map.StreamDirection(cell).Value.ToOffset();
        }
    }
}
