using System;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    // 후기 (#252·#254 사용자): 동굴 보스를 물리친 뒤 애플 기본 별점 창 + 설정의 [후기 남기기] = 스토어 링크.
    // 애플 규정 5.6.1: 앱이 직접 만든 후기 요청 창은 쓰지 말 것 — 창은 애플 것만, 링크는 사람이 직접 누를 때만.
    // 어느 지역인지는 FieldArea asksReviewAfterBoss. 세이브마다 한 번 (WorldState 이벤트). 후기에 보상을 붙이지 말 것 (스토어 규칙)
    public static class ReviewPrompt
    {
        public const string EventId = "review_asked";
        private const string AppStoreId = "6818805727";              // 공개된 앱스토어 주소의 번호
        private const string PackageName = "com.arttechsoo.wordrpg"; // 번들 ID — 바꾸지 말 것

        public static Action NativeRequest = RequestNative;         // 테스트에서 바꿈
        public static Action<string> OpenUrl = Application.OpenURL; // 테스트에서 바꿈

        public static bool Due(FieldArea area, GameSession session) =>
            area != null && area.AsksReviewAfterBoss && !session.World.HasEvent(EventId);

        // 애플 = 후기 쓰는 화면 바로 · 구글 = 앱 페이지 (구글은 후기 칸으로 바로 가는 주소가 없음)
        public static string Url(RuntimePlatform platform) => platform == RuntimePlatform.Android
            ? "https://play.google.com/store/apps/details?id=" + PackageName
            : "https://apps.apple.com/app/id" + AppStoreId + "?action=write-review";

        public static void OpenStorePage() => OpenUrl(Url(Application.platform));

        // 애플 기본 창: 1년에 3번까지이고 뜰지는 애플이 정함, TestFlight에서는 안 뜸 (뜬 뒤 알려 주는 콜백 없음).
        // 구글은 플러그인(In-App Review)이 있어야 해서 아직 없음 — 구글플레이 출시 때. 그때까지는 설정의 링크만
        private static void RequestNative()
        {
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview();
#endif
        }
    }
}
