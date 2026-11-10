using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Game
{
    // 후기 부탁 (#252 사용자: 동굴 보스를 물리친 뒤 게임이 어땠는지 후기를 남겨 달라고 하고 링크를 열어 줌).
    // 어느 지역인지는 FieldArea asksReviewAfterBoss. 세이브마다 한 번 (WorldState 이벤트, [나중에]를 골라도 다시 묻지 않음).
    // 후기에 보상을 붙이지 말 것 (스토어 규칙)
    public static class ReviewPrompt
    {
        public const string EventId = "review_asked";
        private const string AppStoreId = "6818805727";              // 공개된 앱스토어 주소의 번호
        private const string PackageName = "com.arttechsoo.wordrpg"; // 번들 ID — 바꾸지 말 것

        public static bool Due(FieldArea area, GameSession session) =>
            area != null && area.AsksReviewAfterBoss && !session.World.HasEvent(EventId);

        // 애플 = 후기 쓰는 화면 바로 · 구글 = 앱 페이지 (구글은 후기 칸으로 바로 가는 주소가 없음)
        public static string Url(RuntimePlatform platform) => platform == RuntimePlatform.Android
            ? "https://play.google.com/store/apps/details?id=" + PackageName
            : "https://apps.apple.com/app/id" + AppStoreId + "?action=write-review";
    }
}
