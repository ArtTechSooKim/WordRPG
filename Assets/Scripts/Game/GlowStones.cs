using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Heroes;

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

        public static bool AllLit(FieldArea area, WorldState world)
        {
            int total = Stones(area).Count;
            return total > 0 && LitCount(area, world) >= total;
        }

        // 모두 밝힌 보상 (#115): 그 지역 반딧돌을 다 밝혔으면 그 지역의 반딧돌 성유물(동굴 = 반딧돌 등잔)을 준다.
        // 이번에 새로 얻었으면 그 성유물, 아직 덜 밝혔거나 보상이 없거나 이미 가졌으면 null
        public static OwnedRelic ClaimAllLitReward(FieldArea area, GameSession session) =>
            area.GlowStoneRelic != null && AllLit(area, session.World) ? session.GrantRelic(area.GlowStoneRelic) : null;
    }
}
