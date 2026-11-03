using System;
using UnityEngine;

namespace WordRPG.Game
{
    public enum StoreResult
    {
        Purchased,        // 샀다 (Entitlements에 이미 들어간 뒤에 알린다)
        Restored,         // 예전에 산 것을 되찾았다
        NothingToRestore, // 복원할 구매가 없다
        Cancelled,        // 결제 창에서 취소
        Deferred,         // 보호자 승인 대기 (Ask to Buy) — 승인되면 나중에 들어온다
        Failed,           // 결제 실패
        Unavailable       // 스토어에 연결되지 않았거나 상품을 못 받아 옴
    }

    // 결제 안내에 나오는 계정 이름 (#149): 안드로이드 = Google, 그 밖 = Apple
    public static class StoreLabels
    {
        public static string Account(RuntimePlatform platform) => platform == RuntimePlatform.Android ? "Google 계정" : "Apple 계정";
        public static string Account() => Account(Application.platform);
    }

    // 결제 창구 (#40). 실제 기기는 UnityIapStore(앱스토어·구글플레이 인앱 결제), 테스트는 가짜.
    // 사거나 되찾으면 Entitlements에 넣은 뒤 결과를 알린다
    public interface IStore
    {
        bool IsReady { get; }           // 상품 정보(가격)를 받아 와서 살 수 있는지
        string FullVersionPrice { get; } // 그 나라 통화로 쓴 가격 (예: "₩5,000"). 모르면 null
        void BuyFullVersion(Action<StoreResult> done);
        void Restore(Action<StoreResult> done);
    }
}
