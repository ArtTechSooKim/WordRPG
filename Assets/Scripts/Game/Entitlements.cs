using System;
using System.Collections.Generic;
using System.Linq;

namespace WordRPG.Game
{
    // 산 상품 (#40 수익화 — 무료 + 정식판 한 번 구매). 세이브가 아니라 기기(PlayerPrefs)에 둔다:
    // [처음부터 다시 하기]로 세이브를 지워도 산 것은 남는다. 진짜 기준은 애플 결제 기록이라
    // 앱을 켤 때마다 스토어에서 다시 확인하고(IStore), [구매 복원]으로 다른 기기에서도 되찾는다
    public class Entitlements
    {
        public const string FullVersion = "com.arttechsoo.wordrpg.full"; // 앱스토어 커넥트의 상품 id — 바꾸지 말 것

        private readonly HashSet<string> owned = new HashSet<string>();

        // 새로 생겼을 때만 (저장·화면 갱신용)
        public event Action Changed;

        public bool HasFullVersion => Owns(FullVersion);
        public bool Owns(string productId) => !string.IsNullOrEmpty(productId) && owned.Contains(productId);

        public bool Grant(string productId)
        {
            if (string.IsNullOrEmpty(productId) || !owned.Add(productId)) return false;
            Changed?.Invoke();
            return true;
        }

        // PlayerPrefs에 넣을 한 줄 (상품 id를 줄바꿈으로)
        public string ToStorage() => string.Join("\n", owned.OrderBy(id => id, StringComparer.Ordinal));

        public static Entitlements FromStorage(string text)
        {
            var result = new Entitlements();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (var line in text.Split('\n'))
            {
                var id = line.Trim();
                if (id.Length > 0) result.owned.Add(id);
            }
            return result;
        }
    }
}
