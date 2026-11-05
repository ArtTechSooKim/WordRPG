using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Items
{
    [Serializable]
    public class ShopEntry
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int price = 50;

        public ItemData Item => item;
        public int Price => price;

        private ShopEntry() { } // Unity 직렬화용

        public ShopEntry(ItemData item, int price)
        {
            this.item = item;
            this.price = price;
        }
    }

    public enum PurchaseResult
    {
        Bought,
        NotEnoughGold,
        NotForSale // 아이템이 비었거나 가격이 0 이하 (데이터 오류)
    }

    // 상점 하나의 판매 목록. 지역(FieldArea)에 연결한다
    [CreateAssetMenu(fileName = "NewShop", menuName = "WordRPG/Shop", order = 21)]
    public class ShopData : ScriptableObject
    {
        [SerializeField] private string displayName; // 예: "초원 마을 잡화점"
        [SerializeField] private List<ShopEntry> entries = new List<ShopEntry>();
        [Tooltip("가판대 안에 서는 상인 (#210). 그림·움직임만 씀")]
        [SerializeField] private WordRPG.Field.NpcData merchant;
        [Tooltip("근처를 지나갈 때 하는 호객 말")]
        [SerializeField] private List<string> pitches = new List<string>();

        public string DisplayName => displayName;
        public IReadOnlyList<ShopEntry> Entries => entries;
        public WordRPG.Field.NpcData Merchant => merchant;
        public IReadOnlyList<string> Pitches => pitches;
    }

    // 호객 (#210, 사용자): 상점 가까이 들어온 순간에만, 한 번 하면 잠시 쉼. 같은 말을 연달아 하지 않음
    public class ShopPitch
    {
        public const int Range = 4;
        public const float CooldownSeconds = 25f;

        private bool wasNear;
        private float nextAt;
        private int last = -1;

        public string Update(IReadOnlyList<string> lines, int distance, float now, System.Random rng)
        {
            bool near = distance <= Range;
            bool entered = near && !wasNear;
            wasNear = near;
            if (!entered || now < nextAt || lines == null || lines.Count == 0) return null;
            nextAt = now + CooldownSeconds;
            int index = rng.Next(lines.Count);
            if (index == last && lines.Count > 1) index = (index + 1) % lines.Count;
            last = index;
            return lines[index];
        }
    }

    public static class Shop
    {
        public static PurchaseResult TryBuy(Inventory inventory, ShopEntry entry)
        {
            if (entry == null || entry.Item == null || entry.Price <= 0) return PurchaseResult.NotForSale;
            if (!inventory.TrySpendGold(entry.Price)) return PurchaseResult.NotEnoughGold;
            inventory.Add(entry.Item);
            return PurchaseResult.Bought;
        }
    }
}
