using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Items;

namespace WordRPG.Game
{
    // 선물 한 줄: 아이템 + 개수
    [Serializable]
    public class ItemGift
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int count = 1;

        public ItemData Item => item;
        public int Count => count;

        private ItemGift() { } // Unity 직렬화용

        public ItemGift(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }

    // 정식판 특전 (#233 사용자 ⑨): 정식판을 사면 호기심 물약 3 + 특제 상처약 5 — 세이브마다 한 번 (WorldState.events, 세이브 칸 추가 없음).
    // 이미 산 사람도 다음에 켤 때 받음. 무엇을 주는지는 데이터(GameDatabase.FullVersionGift)
    public static class FullVersionGift
    {
        public const string EventId = "gift_full_version";

        public static bool Claimed(GameSession session) => session != null && session.World.HasEvent(EventId);

        // 정식판이 있고 아직 안 받았으면 소지품에 넣고 받은 것을 돌려줌 (없으면 빈 목록)
        public static List<ItemGift> Claim(GameSession session, bool ownsFullVersion, IReadOnlyList<ItemGift> gift)
        {
            var given = new List<ItemGift>();
            if (session == null || !ownsFullVersion || gift == null || gift.Count == 0 || Claimed(session)) return given;
            foreach (var line in gift)
            {
                if (line == null || line.Item == null || line.Count <= 0) continue;
                session.Inventory.Add(line.Item, line.Count);
                given.Add(line);
            }
            session.World.MarkEvent(EventId);
            return given;
        }

        // 안내 창·알림에 쓰는 한 줄: "호기심 물약 3개 · 특제 상처약 5개"
        public static string Describe(IReadOnlyList<ItemGift> gift)
        {
            var parts = new List<string>();
            if (gift != null)
                foreach (var line in gift)
                    if (line != null && line.Item != null && line.Count > 0) parts.Add($"{line.Item.DisplayName} {line.Count}개");
            return string.Join(" · ", parts);
        }
    }
}
