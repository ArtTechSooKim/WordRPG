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
            "업데이트 해주셔서 감사합니다", "동굴 지역이 열렸습니다!\n숲 지역에서 확인해보세요!");

        public static readonly IReadOnlyList<UpdateNotice> All = new[] { Cave };

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
