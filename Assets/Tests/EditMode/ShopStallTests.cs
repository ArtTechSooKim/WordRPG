using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Items;
using WordRPG.UI;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 상점 가판대 + 상인 + 호객 (#210)
    public class ShopStallTests
    {
        private static readonly string[] Lines = { "하나", "둘", "셋" };

        [Test]
        public void ConnectedShopCellsAreOneStall()
        {
            var map = FieldMap.Parse("#######\n#SSS..#\n#SSS.P#\n#######");
            var block = map.ShopBlocks.Single();
            Assert.AreEqual(new Vector2Int(1, 1), block.Min);
            Assert.AreEqual(new Vector2Int(3, 2), block.Max);
            Assert.AreEqual(new Vector2Int(2, 2), block.MerchantCell, "윗줄 가운데");
            Assert.AreEqual(2.5f, block.CenterX);
        }

        [Test]
        public void TheMerchantCallsOnlyWhenYouComeNearAndThenRests()
        {
            var pitch = new ShopPitch();
            var rng = new Random(1);
            Assert.IsNull(pitch.Update(Lines, ShopPitch.Range + 1, 0f, rng), "멀면 안 부름");
            Assert.IsNotNull(pitch.Update(Lines, ShopPitch.Range, 1f, rng), "들어오는 순간");
            Assert.IsNull(pitch.Update(Lines, 1, 2f, rng), "머무는 동안은 또 안 부름");
            Assert.IsNull(pitch.Update(Lines, 10, 3f, rng));
            Assert.IsNull(pitch.Update(Lines, 2, 4f, rng), "금방 돌아오면 쉼");
            Assert.IsNull(pitch.Update(Lines, 10, 30f, rng));
            Assert.IsNotNull(pitch.Update(Lines, 2, 31f, rng), "쉬는 시간이 지나면 다시");
        }

        [Test]
        public void TheSameLineIsNotRepeatedInARow()
        {
            var pitch = new ShopPitch();
            var rng = new Random(5);
            string last = null;
            float now = 0f;
            for (int i = 0; i < 30; i++)
            {
                pitch.Update(Lines, 10, now, rng);
                now += ShopPitch.CooldownSeconds + 1f;
                var line = pitch.Update(Lines, 1, now, rng);
                Assert.IsNotNull(line);
                Assert.AreNotEqual(last, line);
                last = line;
            }
        }

        [Test]
        public void EveryRealShopHasAStallAMerchantAndPitches()
        {
            var areas = AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data/Areas" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(area => area.Map.ShopBlocks.Count > 0).ToList();
            Assert.AreEqual(4, areas.Count, "초원·숲·정글·동굴");
            foreach (var area in areas)
            {
                var block = area.Map.ShopBlocks.Single();
                Assert.AreEqual(3, block.Max.x - block.Min.x + 1, $"{area.name}: 3칸 너비");
                Assert.AreEqual(2, block.Max.y - block.Min.y + 1, $"{area.name}: 2칸 높이");
                var merchant = area.Shop.Merchant;
                Assert.IsNotNull(merchant, area.name);
                Assert.IsNotNull(Resources.Load<Texture2D>("Art/NinjaAdventure/Npcs/" + merchant.NpcId), merchant.NpcId);
                Assert.GreaterOrEqual(area.Shop.Pitches.Count, 2, area.name);
                foreach (var part in new[] { "back", "front", "roof" })
                    Assert.IsNotNull(FieldArt.Stall(area.Theme, part), $"{area.Theme} {part}");
                Assert.AreEqual(area.Shop.DisplayName, area.LandmarkName(block.MerchantCell), "이름표는 가운데 한 곳");
                Assert.IsNull(area.LandmarkName(block.Min), "다른 칸에는 이름표 없음");
            }
        }
    }
}
