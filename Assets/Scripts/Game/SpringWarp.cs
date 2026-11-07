using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    // 샘으로 다른 지역 가기 (#216 사용자, GDD 9-11): 지역마다 회복의 샘 하나가 다른 지역으로 가는 길이다. 옛날부터 사람들은 샘으로 오갔다 (#217 사용자).
    // 옆에서 [확인] → 회복 + '다른 지역으로 이동하시겠습니까?'. 갈 수 있는 곳 = 이미 본 샘 — 탐험 안개 기록을 보므로 세이브 칸이 필요 없고
    // 예전 세이브도 바로 된다. 정식판이 필요한 지역은 샀을 때만. 게임 안에서는 '샘'이라고만 부른다 ('이정표'는 사막 같은 큰 지역 안의 빠른 이동, 따로)
    public static class SpringWarp
    {
        // 도착하면 샘 아래에 서는 것이 먼저 (샘이 주인공 위로 보임)
        private static readonly Direction[] ArrivalOrder = { Direction.Down, Direction.Left, Direction.Right, Direction.Up };

        // 그 지역의 이동용 샘: 맵에서 처음 찾은 샘 (지금은 지역마다 하나)
        public static Vector2Int? SpringOf(FieldMap map)
        {
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (map.Get(cell) == FieldTile.Fountain) return cell;
            }
            return null;
        }

        // 샘 옆 걸을 수 있는 칸 (출입구는 밟으면 다시 옮겨지므로 뺌). 없으면 지역 시작 칸
        public static Vector2Int ArrivalCell(FieldMap map, Vector2Int spring)
        {
            foreach (var direction in ArrivalOrder)
            {
                var cell = spring + direction.ToOffset();
                if (map.IsWalkable(cell) && map.Get(cell) != FieldTile.Door) return cell;
            }
            return map.Start;
        }

        // 함께 튀어나온 동료가 내릴 칸: 주인공 옆 (주인공 뒤는 샘이라 못 섬). 없으면 주인공 칸
        public static Vector2Int CompanionCell(FieldMap map, Vector2Int arrival, Vector2Int spring)
        {
            foreach (var direction in new[] { Direction.Left, Direction.Right, Direction.Down, Direction.Up })
            {
                var cell = arrival + direction.ToOffset();
                if (cell != spring && map.IsWalkable(cell) && map.Get(cell) != FieldTile.Door) return cell;
            }
            return arrival;
        }

        // 옆 칸으로 가는 방향 (뛰어들 때 = 샘 쪽, 튀어나올 때 = 샘에서 도착 칸 쪽)
        public static Direction DirectionBetween(Vector2Int from, Vector2Int to)
        {
            foreach (var direction in ArrivalOrder)
                if (from + direction.ToOffset() == to) return direction;
            return Direction.Down;
        }

        // 지금 갈 수 있는 지역 (이야기 순서). 첫 보스를 물리치기 전엔 없음
        public static List<FieldArea> Destinations(IEnumerable<FieldArea> areas, FieldArea here, WorldState world, bool ownsFullVersion)
        {
            if (areas == null) return new List<FieldArea>();
            var list = areas.Where(area => area != null).ToList();
            if (!IsOpen(list, world)) return new List<FieldArea>();
            return list.Where(area => area != here && (ownsFullVersion || !area.RequiresFullVersion) && HasSeenSpring(area, world))
                .OrderBy(area => area.StoryOrder).ToList();
        }

        // 샘 길은 첫 보스(이야기 순서로 가장 앞 지역의 보스 = 서고의 까먹대왕)를 물리친 뒤에 열림 (#232 사용자: 그 전엔 서고와 초원을
        // 오가다 샘 냄새로 먼저 알아 버려서, 보스 뒤 어르신이 샘 길을 알려 주는 이야기가 어색했음)
        public static bool IsOpen(IEnumerable<FieldArea> areas, WorldState world)
        {
            var first = FirstBossArea(areas);
            return first == null || world.IsBossDefeated(first.BossId);
        }

        // 이야기 순서로 가장 앞 보스가 있는 지역 (서고)
        public static FieldArea FirstBossArea(IEnumerable<FieldArea> areas) =>
            areas?.Where(area => area != null && area.Boss != null).OrderBy(area => area.StoryOrder).FirstOrDefault();

        private static bool HasSeenSpring(FieldArea area, WorldState world)
        {
            var spring = SpringOf(area.Map);
            return spring != null && world.IsExplored(area.AreaId, spring.Value);
        }
    }
}
