using System.Collections.Generic;
using System.Linq;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 징표 진열장 한 칸: 지역 하나의 도감 완성 징표
    public class KeepsakeEntry
    {
        public WordDatabase Region { get; }
        public ItemData Keepsake { get; }
        public bool Owned { get; }
        public DexProgress Progress { get; }

        public string RegionName => string.IsNullOrEmpty(Region.RegionName) ? Region.RegionId : Region.RegionName;

        public KeepsakeEntry(WordDatabase region, bool owned, DexProgress progress)
        {
            Region = region;
            Keepsake = region.CompletionKeepsake;
            Owned = owned;
            Progress = progress;
        }
    }

    public static class Keepsakes
    {
        // 지역(단어장) 목록 → 진열장 칸. 같은 지역(regionId)은 한 번만, 징표가 없는 지역은 뺀다.
        // 받은 기록이 있거나 소지품에 있으면 '획득'
        public static List<KeepsakeEntry> Collect(IEnumerable<WordDatabase> regions, GameSession session)
        {
            var entries = new List<KeepsakeEntry>();
            var seen = new HashSet<string>();
            foreach (var region in regions)
            {
                if (region == null || region.CompletionKeepsake == null || string.IsNullOrEmpty(region.RegionId)) continue;
                if (!seen.Add(region.RegionId)) continue;
                bool owned = session.Record.HasClaimedRegion(region.RegionId)
                             || session.Inventory.GetCount(region.CompletionKeepsake) > 0;
                entries.Add(new KeepsakeEntry(region, owned, Dex.GetProgress(region, session.Vocabulary)));
            }
            return entries;
        }

        // 게임 데이터의 모든 지역에서 단어장을 모은다 (던전처럼 같은 단어장을 쓰는 지역은 Collect가 하나로 합침).
        // 진열장은 이야기 순서(WordDatabase.storyOrder: 초원 → 서고 → 숲 → 정글) — 번호가 없으면 맨 뒤 (#97)
        public static List<KeepsakeEntry> Collect(GameDatabase database, GameSession session)
        {
            var regions = new List<WordDatabase>();
            if (database != null)
            {
                foreach (var area in database.Areas)
                {
                    if (area != null && area.Words != null) regions.Add(area.Words);
                }
            }
            return Collect(regions.OrderBy(r => r.StoryOrder > 0 ? r.StoryOrder : int.MaxValue), session);
        }
    }
}
