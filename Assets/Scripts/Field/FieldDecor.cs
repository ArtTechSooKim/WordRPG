using UnityEngine;

namespace WordRPG.Field
{
    // 꾸밈 (#86, 정글 맵 다듬기 — 텅 빈 야영지·유적·들판에 볼거리):
    //  - 꾸밈 칸 'Q' (FieldTile.Decor, 막힘): 둘레를 보고 종류를 고른다 — 유적 옆 = 이끼 기둥·부서진 석상·돌무더기·이끼 바위,
    //    야영지(샘·가게·제단·사전 가까이) = 나무 상자·통·통나무, 그 밖의 들판 = 그루터기·통나무·바위. 같은 칸은 늘 같은 종류(좌표 해시)
    //  - 흩뿌림 (막지 않음, 그림만): 이끼 땅의 풀·꽃, 뗏목이 다니는 강의 연잎, 유적 바닥의 돌 부스러기
    // 맵 글자만으로 정해지므로 저장할 것이 없고, 미니맵·도달 검사에도 영향이 없다 (꾸밈 칸 = 막힌 칸)
    public static class FieldDecor
    {
        public static readonly string[] Ruins = { "Pillar", "Statue", "Rubble", "MossRock" };
        public static readonly string[] Camp = { "Crate", "Barrel", "Log" };
        public static readonly string[] Wild = { "Stump", "Log", "Rock" };
        public const int CampRadius = 8; // 쉼터 물건(샘·가게·제단·사전)에서 이 칸 안이면 야영지

        // 칸보다 큰 그림을 쓰는 종류 (1×2 — 아래 칸에 맞춰 그 위로)
        public static bool IsTall(string variant) => variant == "Pillar" || variant == "Statue";

        public static string Variant(FieldMap map, Vector2Int cell)
        {
            var set = NearRuins(map, cell) ? Ruins : NearCamp(map, cell) ? Camp : Wild;
            return set[Hash(cell, 7) % set.Length];
        }

        // 흩뿌림: 그 칸에 그릴 그림 번호 (없으면 -1). count = 그 바닥의 그림 수
        // 칸 그림 변형 (#98): 같은 종류 칸(책장·나무·풀숲)이 한 그림만 되풀이되지 않게 0(기본) ~ variants 중 하나를 칸 위치로 고른다
        public static int TileVariant(Vector2Int cell, int variants) => variants <= 0 ? 0 : Hash(cell, 97) % (variants + 1);

        public static int ScatterAt(FieldMap map, Vector2Int cell, int count)
        {
            if (count <= 0) return -1;
            var tile = map.Get(cell);
            int percent = tile == FieldTile.Lawn ? 8 : tile == FieldTile.RuinFloor ? 6 : tile == FieldTile.Water && map.IsRaftWater(cell) ? 5 : 0;
            if (percent == 0 || Hash(cell, 31) % 100 >= percent) return -1;
            return Hash(cell, 53) % count;
        }

        private static bool NearRuins(FieldMap map, Vector2Int cell)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var t = map.Get(cell + new Vector2Int(dx, dy));
                if (t == FieldTile.RuinFloor || t == FieldTile.RuinWall) return true;
            }
            return false;
        }

        private static bool NearCamp(FieldMap map, Vector2Int cell)
        {
            foreach (var kind in new[] { FieldTile.Fountain, FieldTile.Shop, FieldTile.Altar, FieldTile.Lectern })
            foreach (var spot in map.FindAll(kind))
                if (Mathf.Abs(spot.x - cell.x) <= CampRadius && Mathf.Abs(spot.y - cell.y) <= CampRadius) return true;
            return false;
        }

        // 좌표 기반 고정 해시 (매번 같은 결과)
        private static int Hash(Vector2Int cell, int seed)
        {
            unchecked
            {
                int h = cell.x * 73856093 ^ cell.y * 19349663 ^ seed * 83492791;
                h ^= h >> 13;
                h *= 1274126177;
                return (h ^ (h >> 16)) & 0x7FFFFFFF;
            }
        }
    }
}
