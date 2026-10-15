using UnityEngine;

namespace WordRPG.Field
{
    // 자동 테두리 (길·물가): 이웃 8칸이 같은 종류로 이어지는지로 마스크를 만들면, 그림 쪽(FieldArt.AutoTile)이
    // 그 모양에 맞는 조각(흙길 가장자리 풀, 물가 등)을 고른다. 맵은 글자 그대로 두고 모양만 이웃에서 계산한다.
    // 비트: 위 1, 오른위 2, 오른 4, 오른아래 8, 아래 16, 왼아래 32, 왼 64, 왼위 128 (화면 위 = y+1).
    // 모서리는 맞닿은 두 변이 모두 이어질 때만 센다 → 모양은 47가지 (Tools/import_ninja_art.py의 auto_normalize와 같은 규칙)
    public static class FieldAutotile
    {
        public const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

        private static readonly Vector2Int[] Offsets =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
            new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1)
        };

        // 테두리를 자동으로 그리는 칸: 길, 물
        public static bool IsAutotiled(FieldTile tile) => tile == FieldTile.Floor || tile == FieldTile.Water;

        // 이어지는 이웃: 길은 길·출입구(길이 문까지 이어지게), 물은 물
        public static bool Connects(FieldTile tile, FieldTile neighbor)
        {
            switch (tile)
            {
                case FieldTile.Floor: return neighbor == FieldTile.Floor || neighbor == FieldTile.Door || neighbor == FieldTile.Gate; // 보스 방 문은 길이 이어지는 자리
                case FieldTile.Water: return neighbor == FieldTile.Water;
                default: return false;
            }
        }

        // 맵 밖: 물은 이어진 것으로 (강이 맵 끝까지 흘러가게), 길은 막힌 것으로
        public static int Mask(FieldMap map, Vector2Int cell)
        {
            var tile = map.Get(cell);
            int mask = 0;
            for (int i = 0; i < Offsets.Length; i++)
            {
                var next = cell + Offsets[i];
                bool joined = map.InBounds(next) ? Connects(tile, map.Get(next)) : tile == FieldTile.Water;
                if (joined) mask |= 1 << i;
            }
            return Normalize(mask);
        }

        public static int Normalize(int mask)
        {
            if ((mask & N) == 0 || (mask & E) == 0) mask &= ~NE;
            if ((mask & S) == 0 || (mask & E) == 0) mask &= ~SE;
            if ((mask & S) == 0 || (mask & W) == 0) mask &= ~SW;
            if ((mask & N) == 0 || (mask & W) == 0) mask &= ~NW;
            return mask;
        }
    }
}
