using System;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    // 황금 너구리 (#292 사용자, GDD 9-17 — 비밀의 숲): 들어가면 앞에서 춤추다 도망 → 쫓아가 [확인]하면 동전이 '띠링' + 골드,
    // 또 춤추다 다음 자리로 도망. 마지막 자리에서 잡으면 동전이 파바바박 터지고 춤추며 사라짐 (말은 안 함 — 사용자).
    // 하루 한 번 (현지 날짜 — 사전과 같음), 자리·골드는 지역 데이터(FieldArea goldRaccoonSpots·goldRaccoonGold = 100 · 150 · 200 · 1000).
    // 세이브 = WorldState 그날 · 잡은 횟수 — 중간에 나가도 다음에 들어오면 그다음 자리부터
    public static class GoldRaccoon
    {
        // 이 지역에 황금 너구리가 사는지 (자리와 골드 수가 맞아야 함 — GoldRaccoonTests)
        public static bool LivesIn(FieldArea area) =>
            area != null && area.GoldRaccoonSpots.Count > 0 && area.GoldRaccoonSpots.Count == area.GoldRaccoonGold.Count;

        // 오늘 잡은 횟수 (다른 날 기록이면 0 — 하루가 지나면 처음부터)
        public static int CaughtToday(WorldState world, DateTime localNow) =>
            world.GoldRaccoonDay == BossDictionary.DayKey(localNow) ? Math.Max(0, world.GoldRaccoonCaught) : 0;

        // 오늘 아직 남아 있는지 (마지막 자리까지 잡으면 내일까지 없음)
        public static bool IsHere(FieldArea area, WorldState world, DateTime localNow) =>
            LivesIn(area) && CaughtToday(world, localNow) < area.GoldRaccoonSpots.Count;

        // 지금 도망가 서 있을 자리 (없으면 null)
        public static Vector2Int? Spot(FieldArea area, WorldState world, DateTime localNow) =>
            IsHere(area, world, localNow) ? area.GoldRaccoonSpots[CaughtToday(world, localNow)] : (Vector2Int?)null;

        // 지금 자리에서 잡으면 받을 골드 (없으면 0)
        public static int NextGold(FieldArea area, WorldState world, DateTime localNow) =>
            IsHere(area, world, localNow) ? area.GoldRaccoonGold[CaughtToday(world, localNow)] : 0;

        // 지금 자리가 마지막인지 (잡으면 파바바박 + 사라짐)
        public static bool IsLastSpot(FieldArea area, WorldState world, DateTime localNow) =>
            IsHere(area, world, localNow) && CaughtToday(world, localNow) == area.GoldRaccoonSpots.Count - 1;

        // 잡는다: 골드를 주고 그날 잡은 횟수를 하나 올림. 반환값 = 받은 골드 (오늘 다 잡았으면 0)
        public static int Catch(FieldArea area, GameSession session, DateTime localNow)
        {
            var world = session.World;
            if (!IsHere(area, world, localNow)) return 0;
            int caught = CaughtToday(world, localNow);
            int gold = Math.Max(0, area.GoldRaccoonGold[caught]);
            session.Inventory.AddGold(gold);
            world.SetGoldRaccoon(BossDictionary.DayKey(localNow), caught + 1);
            return gold;
        }
    }
}
