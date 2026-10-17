using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    // 글자 반딧돌 'I' (#112, 1.2 동굴 — GDD 9-7): 단어 한 문제로 밝히면 둘레가 영영 밝아진다 (빛은 CaveLight).
    // 밝힌 반딧돌은 세이브(WorldState.litStones, id = "지역:x,y" — 상자 id와 같은 방식)
    public static class GlowStones
    {
        public static List<Vector2Int> Stones(FieldArea area) => area.Map.FindAll(FieldTile.GlowStone);

        public static bool IsLit(FieldArea area, WorldState world, Vector2Int stone) =>
            area.Map.Get(stone) == FieldTile.GlowStone && world.IsStoneLit(area.StoneId(stone));

        // 밝힌다. 이번에 새로 밝혔으면 true
        public static bool Light(FieldArea area, WorldState world, Vector2Int stone)
        {
            if (area.Map.Get(stone) != FieldTile.GlowStone || IsLit(area, world, stone)) return false;
            world.MarkStoneLit(area.StoneId(stone));
            return true;
        }

        public static int LitCount(FieldArea area, WorldState world)
        {
            int lit = 0;
            foreach (var stone in Stones(area))
                if (IsLit(area, world, stone)) lit++;
            return lit;
        }
    }
}
