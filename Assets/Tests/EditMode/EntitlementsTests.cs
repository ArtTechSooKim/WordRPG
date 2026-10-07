using NUnit.Framework;
using UnityEditor;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.Tests
{
    // 정식판 (#40): 산 상품 기록, 숲부터 정식판이 필요한지 (실제 데이터)
    public class EntitlementsTests
    {
        [Test]
        public void GrantOwnsAndStorageRoundTrip()
        {
            var owned = new Entitlements();
            int changed = 0;
            owned.Changed += () => changed++;
            Assert.IsFalse(owned.HasFullVersion);
            Assert.IsTrue(owned.Grant(Entitlements.FullVersion));
            Assert.IsFalse(owned.Grant(Entitlements.FullVersion), "두 번 넣어도 한 번");
            Assert.AreEqual(1, changed, "새로 생겼을 때만 알림 (저장)");
            Assert.IsTrue(owned.HasFullVersion);

            var loaded = Entitlements.FromStorage(owned.ToStorage());
            Assert.IsTrue(loaded.HasFullVersion, "기기에 저장했다가 다시 읽어도 그대로");
            Assert.IsFalse(Entitlements.FromStorage("").HasFullVersion);
            Assert.IsFalse(Entitlements.FromStorage(null).HasFullVersion);
        }

        [Test]
        public void OnlyForestAndLaterNeedFullVersion()
        {
            var meadow = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/meadow.asset");
            var library = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/library.asset");
            var forest = AssetDatabase.LoadAssetAtPath<FieldArea>("Assets/Data/Areas/forest.asset");
            Assert.IsFalse(meadow.RequiresFullVersion, "초원은 무료");
            Assert.IsFalse(library.RequiresFullVersion, "서고(첫 던전)는 무료");
            Assert.IsTrue(forest.RequiresFullVersion, "숲 입구에서 정식판 (사용자 결정 2026-10-07)");
        }
    }
}
