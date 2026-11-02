using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Heroes;

namespace WordRPG.Game
{
    // 성유물 안내 규칙 (#142 사용자): 언제 안내하는지 · 가장 가까운 제단과 그 앞 칸 (연출은 FieldScreen.RelicGuides).
    // 가방 안내의 빨간 점은 MenuBadges.RelicNew·RelicSwap, 제단 안내의 기록은 TutorialProgress.Altar
    public static class RelicTutorials
    {
        public static OwnedRelic Newest(Hero hero) => hero.Relics.Count > 0 ? hero.Relics[hero.Relics.Count - 1] : null;

        // 칸이 가득 차서 못 끼운 성유물 중 가장 최근 것. 없으면 null
        public static OwnedRelic NewestUnequipped(Hero hero)
        {
            for (int i = hero.Relics.Count - 1; i >= 0; i--)
                if (!hero.IsEquipped(hero.Relics[i])) return hero.Relics[i];
            return null;
        }

        public static bool AnyUpgraded(Hero hero)
        {
            foreach (var relic in hero.Relics)
                if (relic.Level > 0) return true;
            return false;
        }

        // 지금 재료·골드로 강화할 수 있는 첫 성유물. 없으면 null
        public static OwnedRelic FirstUpgradable(GameSession session)
        {
            foreach (var relic in session.Hero.Relics)
                if (RelicUpgrade.Check(relic, session.Inventory) == UpgradeStatus.Ready) return relic;
            return null;
        }

        // 제단 안내를 할 때: 강화할 재료가 처음 모임. 이미 강화해 본 사람에게는 안 함
        public static bool AltarDue(GameSession session) =>
            !session.Tutorials.Has(TutorialProgress.Altar) && !AnyUpgraded(session.Hero) && FirstUpgradable(session) != null;

        // 지금 지역부터 열린 출입구를 따라 가장 가까운, 제단이 있는 지역. canEnter = 들어갈 수 있는 지역 (정식판). 없으면 null
        public static FieldArea NearestAltarArea(FieldArea from, GameSession session, Func<FieldArea, bool> canEnter)
        {
            if (from == null) return null;
            var seen = new HashSet<FieldArea> { from };
            var queue = new Queue<FieldArea>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var area = queue.Dequeue();
                if (area.Map.Find(FieldTile.Altar).HasValue) return area;
                foreach (var exit in area.Exits)
                {
                    if (exit.Target == null || !seen.Add(exit.Target) || !Objectives.IsOpen(session, exit)) continue;
                    if (canEnter == null || canEnter(exit.Target)) queue.Enqueue(exit.Target);
                }
            }
            return null;
        }

        // 제단 앞 칸: 제단을 올려다보는 아래쪽부터. facing = 제단 쪽. 둘레가 다 막혔으면 null
        public static Vector2Int? StandBefore(FieldMap map, Vector2Int altar, out Direction facing)
        {
            foreach (var direction in new[] { Direction.Up, Direction.Left, Direction.Right, Direction.Down })
            {
                var cell = altar - direction.ToOffset();
                if (!map.IsWalkable(cell) || map.Get(cell) == FieldTile.Door) continue;
                facing = direction;
                return cell;
            }
            facing = Direction.Down;
            return null;
        }
    }
}
