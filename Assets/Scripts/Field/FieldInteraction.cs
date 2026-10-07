using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Field
{
    // 확인 버튼·이름표 규칙. 상자·샘·제단·상점·보스는 부딪히는 것이 아니라 옆에 서서 [확인]으로 쓴다
    public static class FieldInteraction
    {
        // 이 거리(가로·세로 모두 이 칸 수 안) 안에 들어오면 이름표가 보인다
        public const int NameTagRadius = 2;

        private static readonly Direction[] SideOrder = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        // 확인 버튼으로 쓸 방향: 바라보는 칸이 우선, 아니면 옆 칸(위·아래·왼·오른 순). 쓸 것이 없으면 null.
        // cleared: 이제는 쓸 것이 없는 칸 (쓰러뜨린 보스 자리) — 건너뛴다
        public static Direction? FindTarget(FieldMap map, Vector2Int position, Direction facing, Func<Vector2Int, bool> cleared = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            bool Usable(Vector2Int cell) => FieldMap.IsInteractive(map.Get(cell)) && (cleared == null || !cleared(cell));
            if (Usable(position + facing.ToOffset())) return facing;
            foreach (var direction in SideOrder)
            {
                if (Usable(position + direction.ToOffset())) return direction;
            }
            return null;
        }

        // 이름표를 다는 칸: 제단·상점·샘·보스·출입구. 보물상자는 모양만 봐도 알 수 있어 달지 않는다
        public static bool HasNameTag(FieldTile tile) =>
            tile == FieldTile.Altar || tile == FieldTile.Shop || tile == FieldTile.Fountain
            || tile == FieldTile.Boss || tile == FieldTile.Door || tile == FieldTile.Lectern;

        // 맵의 모든 이름표 칸 (지역에 들어갈 때 이름표를 미리 만들어 둔다)
        public static List<Vector2Int> Landmarks(FieldMap map)
        {
            var result = new List<Vector2Int>();
            for (int y = map.Height - 1; y >= 0; y--)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (HasNameTag(map.Get(cell))) result.Add(cell);
            }
            return result;
        }

        public static bool IsNear(Vector2Int position, Vector2Int cell, int radius = NameTagRadius) =>
            Mathf.Abs(cell.x - position.x) <= radius && Mathf.Abs(cell.y - position.y) <= radius;
    }
}
