using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace WordRPG.Game
{
    // Unity IAP 5로 애플 인앱 결제 (#40). 정식판 = 한 번 사면 계속 쓰는 상품(NonConsumable).
    //  - 켜자마자 스토어에 연결 → 상품(가격) 받아 오기 + 이미 산 것 확인(다른 기기에서 산 것·재설치 포함)
    //  - 결제는 두 단계: OnPurchasePending에서 Entitlements에 넣고(기기에 저장) → ConfirmPurchase. 확정 전에 꺼지면 다음에 다시 들어온다
    //  - [구매 복원]: RestoreTransactions → 산 것이 OnPurchasePending으로 다시 들어온다
    public class UnityIapStore : IStore
    {
        private readonly Entitlements entitlements;
        private StoreController store;
        private Product fullVersion;
        private Action<StoreResult> buying, restoring;

        public UnityIapStore(Entitlements entitlements) => this.entitlements = entitlements;

        public bool IsReady => store != null && fullVersion != null && fullVersion.availableToPurchase;
        public string FullVersionPrice => fullVersion?.metadata?.localizedPriceString;

        public async void Connect()
        {
            try
            {
                store = UnityIAPServices.StoreController();
                // 이벤트는 Connect 전에 모두 (지난번에 확정 못 한 결제가 바로 들어올 수 있음)
                store.OnStoreConnected += OnConnected;
                store.OnStoreDisconnected += failure => Debug.LogWarning($"[Store] 연결 끊김: {failure.Message}");
                store.OnProductsFetched += _ => fullVersion = store.GetProductById(Entitlements.FullVersion);
                store.OnProductsFetchFailed += failure => Debug.LogWarning($"[Store] 상품을 못 받아 옴: {failure.FailureReason}");
                store.OnPurchasesFetched += OnPurchasesFetched;
                store.OnPurchasesFetchFailed += failure => Debug.LogWarning($"[Store] 구매 기록을 못 받아 옴: {failure.Message}");
                store.OnPurchasePending += OnPurchasePending;
                store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                store.OnPurchaseFailed += OnPurchaseFailed;
                store.OnPurchaseDeferred += _ => Finish(ref buying, StoreResult.Deferred);
                await store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Store] 스토어 연결 실패: {e.Message}");
            }
        }

        private void OnConnected()
        {
            store.FetchProducts(new List<ProductDefinition> { new ProductDefinition(Entitlements.FullVersion, ProductType.NonConsumable) });
            store.FetchPurchases();
        }

        // 이미 확정된 구매 (재설치·다른 기기 포함) → 정식판
        private void OnPurchasesFetched(Orders orders)
        {
            if (orders.ConfirmedOrders.Any(order => Contains(order, Entitlements.FullVersion))) entitlements.Grant(Entitlements.FullVersion);
        }

        // 결제·복원·지난번 미확정 결제 → 먼저 기기에 넣고 확정
        private void OnPurchasePending(PendingOrder order)
        {
            bool full = Contains(order, Entitlements.FullVersion);
            if (full) entitlements.Grant(Entitlements.FullVersion);
            store.ConfirmPurchase(order);
            if (!full) return;
            Finish(ref buying, StoreResult.Purchased);
            Finish(ref restoring, StoreResult.Restored);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed) Debug.LogWarning($"[Store] 확정 실패: {failed.FailureReason} {failed.Details}");
        }

        private void OnPurchaseFailed(FailedOrder failed)
        {
            bool cancelled = failed.FailureReason == PurchaseFailureReason.UserCancelled;
            if (!cancelled) Debug.LogWarning($"[Store] 결제 실패: {failed.FailureReason} {failed.Details}");
            Finish(ref buying, cancelled ? StoreResult.Cancelled : StoreResult.Failed);
        }

        public void BuyFullVersion(Action<StoreResult> done)
        {
            if (entitlements.HasFullVersion)
            {
                done?.Invoke(StoreResult.Purchased);
                return;
            }
            if (!IsReady)
            {
                done?.Invoke(StoreResult.Unavailable);
                return;
            }
            buying = done;
            store.PurchaseProduct(fullVersion);
        }

        public void Restore(Action<StoreResult> done)
        {
            if (store == null)
            {
                done?.Invoke(StoreResult.Unavailable);
                return;
            }
            restoring = done;
            store.RestoreTransactions((success, error) =>
            {
                if (!success) Debug.LogWarning($"[Store] 복원 실패: {error}");
                // 산 것이 있으면 OnPurchasePending이 먼저 Restored로 끝냈을 수 있다. 남아 있으면 지금 상태로 마무리
                if (!success) Finish(ref restoring, StoreResult.Failed);
                else Finish(ref restoring, entitlements.HasFullVersion ? StoreResult.Restored : StoreResult.NothingToRestore);
            });
        }

        private static bool Contains(Order order, string productId) =>
            order?.CartOrdered?.Items()?.Any(item => item?.Product?.definition?.id == productId) == true;

        private static void Finish(ref Action<StoreResult> callback, StoreResult result)
        {
            var done = callback;
            callback = null;
            done?.Invoke(result);
        }
    }
}
