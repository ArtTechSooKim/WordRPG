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
        public const string FullVersion = "com.arttechsoo.wordrpg.full"; // 앱스토어 커넥트·구글플레이 콘솔의 상품 id — 바꾸지 말 것

        private readonly HashSet<string> owned = new HashSet<string>();

        // 새로 생겼을 때만 (저장·화면 갱신용)
        public event Action Changed;

        public bool HasFullVersion => Owns(FullVersion);
        public bool Owns(string productId) => !string.IsNullOrEmpty(productId) && owned.Contains(productId);

        public bool Grant(string productId)
        {
#if WORDRPG_TESTER || UNITY_EDITOR
            if (TesterFree) return false; // 무료 상태를 보는 중 — 켤 때마다 스토어 기록이 정식판을 다시 넣지 못하게
#endif
            if (string.IsNullOrEmpty(productId) || !owned.Add(productId)) return false;
            Changed?.Invoke();
            return true;
        }

#if WORDRPG_TESTER || UNITY_EDITOR
        // 테스터 메뉴 [무료 상태로] (#251 사용자: 이미 산 계정이라 무료 상태를 볼 수 없음). 표시를 상품 id와 같이 저장해서 앱을 다시 켜도 그대로.
        // 끝내면 (테스터 메뉴·TesterFreeStore로 사면) 정식판이 돌아옴. 앱스토어 빌드에는 이 코드가 없고, 남은 표시는 그냥 무시됨
        private const string TesterFreeMark = "tester_free";
        public bool TesterFree => owned.Contains(TesterFreeMark);

        public void SetTesterFree(bool free)
        {
            if (free == TesterFree) return;
            if (free)
            {
                owned.Remove(FullVersion);
                owned.Add(TesterFreeMark);
            }
            else
            {
                owned.Remove(TesterFreeMark);
                owned.Add(FullVersion);
            }
            Changed?.Invoke();
        }
#endif

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
