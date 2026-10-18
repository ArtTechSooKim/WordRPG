using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;

namespace WordRPG.Tests
{
    public class ShopTests
    {
        private ItemData ink;

        [SetUp]
        public void SetUp() => ink = TestData.Item("shiny_ink");

        [Test]
        public void BuyingSpendsGoldAndAddsItem()
        {
            var inventory = new Inventory();
            inventory.AddGold(100);

            var result = Shop.TryBuy(inventory, new ShopEntry(ink, 60));

            Assert.AreEqual(PurchaseResult.Bought, result);
            Assert.AreEqual(40, inventory.Gold);
            Assert.AreEqual(1, inventory.GetCount(ink));
        }

        [Test]
        public void NotEnoughGoldChangesNothing()
        {
            var inventory = new Inventory();
            inventory.AddGold(59);

            Assert.AreEqual(PurchaseResult.NotEnoughGold, Shop.TryBuy(inventory, new ShopEntry(ink, 60)));
            Assert.AreEqual(59, inventory.Gold);
            Assert.AreEqual(0, inventory.GetCount(ink));
        }

        [Test]
        public void BrokenEntriesAreNotForSale()
        {
            var inventory = new Inventory();
            inventory.AddGold(1000);

            Assert.AreEqual(PurchaseResult.NotForSale, Shop.TryBuy(inventory, new ShopEntry(null, 10)));
            Assert.AreEqual(PurchaseResult.NotForSale, Shop.TryBuy(inventory, new ShopEntry(ink, 0)));
            Assert.AreEqual(PurchaseResult.NotForSale, Shop.TryBuy(inventory, null));
            Assert.AreEqual(1000, inventory.Gold);
        }
    }

    public class JosaTests
    {
        [TestCase("펜촉이", "이", "가", "펜촉이가")]
        [TestCase("책껍질", "이", "가", "책껍질이")]
        [TestCase("깃펜기사", "으로", "로", "깃펜기사로")]
        [TestCase("백과거북", "으로", "로", "백과거북으로")]
        [TestCase("지혜등불", "으로", "로", "지혜등불로")] // ㄹ받침은 '로'
        [TestCase("빛나는 잉크", "을", "를", "빛나는 잉크를")]
        [TestCase("단단한 표지", "을", "를", "단단한 표지를")]
        [TestCase("Slime", "이", "가", "Slime가")]
        public void PicksParticleByFinalConsonant(string word, string withBatchim, string without, string expected)
        {
            Assert.AreEqual(expected, UiKit.WithJosa(word, withBatchim, without));
        }
    }

    // 실제 프로젝트의 마을 데이터 점검
    public class TownDataTests
    {
        [Test]
        public void AreasWithShopTileHaveAValidShop()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var areas = AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(g))).ToList();

            foreach (var area in areas)
            {
                var map = area.Map;
                bool hasShopTile = false;
                for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    hasShopTile |= map.Get(new Vector2Int(x, y)) == FieldTile.Shop;
                if (!hasShopTile) continue;

                Assert.IsNotNull(area.Shop, $"{area.name}: 맵에 상점(S)이 있는데 상점 데이터가 연결되지 않음");
                Assert.Greater(area.Shop.Entries.Count, 0, $"{area.Shop.name}: 파는 물건이 없음");
                foreach (var entry in area.Shop.Entries)
                {
                    Assert.IsNotNull(entry.Item, $"{area.Shop.name}: 비어 있는 상품");
                    Assert.Greater(entry.Price, 0, $"{area.Shop.name}: {entry.Item.name} 가격이 0 이하");
                    Assert.AreSame(entry.Item, database.FindItem(entry.Item.ItemId), $"{entry.Item.name}이 GameDatabase에 없음");
                }
            }
        }

        // 사용자 결정: 상점은 상처약만 판다 (강화 재료를 사면 너무 쉬워서). 재료는 적 드롭·보물상자·보스로
        [Test]
        public void ShopsSellHealingItemsNotUpgradeMaterials()
        {
            var shops = AssetDatabase.FindAssets("t:ShopData", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ShopData>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.Greater(shops.Count, 0);
            foreach (var shop in shops)
            foreach (var entry in shop.Entries)
            {
                Assert.AreNotEqual(ItemKind.Material, entry.Item.Kind, $"{shop.name}: 강화 재료 {entry.Item.name}을(를) 팔면 안 됨");
                Assert.IsTrue(entry.Item.IsHealingItem, $"{shop.name}: {entry.Item.name}은(는) 회복 아이템이 아님");
            }
        }

        [Test]
        public void EveryRelicCanBeUpgradedWithObtainableMaterialsAndIsFindable()
        {
            var monsters = AssetDatabase.FindAssets("t:MonsterSpecies", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<MonsterSpecies>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var areas = AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var relics = AssetDatabase.FindAssets("t:RelicData", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");

            var obtainableItems = new HashSet<ItemData>();
            var findableRelics = new HashSet<RelicData>(hero.StartingRelics);
            foreach (var m in monsters)
            foreach (var drop in m.Drops)
                if (drop.Item != null && drop.Chance > 0) obtainableItems.Add(drop.Item);
            foreach (var area in areas)
            {
                foreach (var chest in area.ChestContents)
                {
                    if (chest.Item != null) obtainableItems.Add(chest.Item);
                    if (chest.Relic != null) findableRelics.Add(chest.Relic);
                }
                if (area.Boss != null && area.Boss.RewardRelic != null) findableRelics.Add(area.Boss.RewardRelic);
                // 글자 반딧돌을 모두 밝힌 보상 (#115) — 반딧돌이 있는 맵에서만
                if (area.GlowStoneRelic != null && area.Map.FindAll(FieldTile.GlowStone).Count > 0) findableRelics.Add(area.GlowStoneRelic);
            }

            foreach (var relic in relics)
            {
                Assert.IsTrue(findableRelics.Contains(relic), $"{relic.name}: 시작·보물상자·보스·반딧돌 어디에서도 얻을 수 없음");
                Assert.IsTrue(obtainableItems.Contains(relic.UpgradeItem),
                    $"{relic.name}의 강화 재료 {relic.UpgradeItem.name}을(를) 얻을 방법이 없음 (적 드롭·보물상자)");
            }
        }
    }
}
