using System.Collections.Generic;

namespace WordRPG.Game
{
    // 업데이트 안내 한 개: 타이틀에서 한 번 뜨는 창의 제목·설명
    public class UpdateNotice
    {
        public string Id { get; }
        public string Title { get; }
        public string Message { get; }

        public UpdateNotice(string id, string title, string message)
        {
            Id = id;
            Title = title;
            Message = message;
        }
    }

    // 업데이트 안내 (#115, 사용자 결정): 앱을 업데이트한 사람만 처음 켤 때 타이틀에서 한 번 보는 창 (Figma '타이틀 — 업데이트 안내 (#115)').
    // 본 안내 id는 세이브에 (튜토리얼과 따로 — [튜토리얼 다시 보기]로 다시 뜨지 않음). 예전 세이브엔 없음 → 아직 안 본 것.
    // 새 게임은 지금 있는 안내를 모두 본 것으로 시작 → 새로 설치한 사람에겐 안 뜸. 새 업데이트 안내는 All 맨 뒤에 더한다
    public class UpdateNotices
    {
        // 1.2: 정글 강 가운데 섬의 동굴 입구가 열림 (문구 = 사용자가 정한 그대로)
        public static readonly UpdateNotice Cave = new UpdateNotice("update_1_2_cave",
            "업데이트 해주셔서 감사합니다", "새로운 지역 동굴이 열렸습니다!\n숲 지역에 있는 동굴을 찾아보세요!");

        // 1.3: 말을 잃어 가는 사람들 (#213, 사용자: 짧게). 누군가의 기록은 이스터에그라 쓰지 않음, AI 곡은 권리 답에 따라 빠질 수 있어 쓰지 않음
        public static readonly UpdateNotice People = new UpdateNotice("update_1_3_people",
            "업데이트 해주셔서 감사합니다", "말을 잃어 가는 사람들이 나타났습니다!\n만나면 말을 걸어 보세요!");

        public static readonly IReadOnlyList<UpdateNotice> All = new[] { Cave, People };

        private readonly HashSet<string> seen = new HashSet<string>();

        public UpdateNotices() { }

        public UpdateNotices(IEnumerable<string> seenIds)
        {
            if (seenIds == null) return;
            foreach (var id in seenIds) if (!string.IsNullOrEmpty(id)) seen.Add(id);
        }

        public IEnumerable<string> Seen => seen;
        public bool Has(string id) => seen.Contains(id);
        public void Mark(string id) => seen.Add(id);

        public void MarkAll()
        {
            foreach (var notice in All) seen.Add(notice.Id);
        }

        // 아직 안 본 안내 중 가장 오래된 것 (없으면 null)
        public UpdateNotice NextUnseen()
        {
            foreach (var notice in All)
                if (!seen.Contains(notice.Id)) return notice;
            return null;
        }
    }
}
