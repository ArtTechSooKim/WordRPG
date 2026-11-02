using System.Collections.Generic;
using System.Linq;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 징표 진열장 한 칸: 도감 보상 하나의 징표 (#147 — 단어장 하나에 여러 칸)
    public class KeepsakeEntry
    {
        public WordDatabase Region { get; }
        public DexReward Reward { get; }
        public ItemData Keepsake => Reward.Keepsake;
        public bool Owned { get; }
        public DexProgress Progress { get; }

        public string RegionName => Reward.RegionName;
        public int WordsNeeded => Reward.WordsNeeded(Progress.Total);

        public KeepsakeEntry(WordDatabase region, DexReward reward, bool owned, DexProgress progress)
        {
            Region = region;
            Reward = reward;
            Owned = owned;
            Progress = progress;
        }
    }

    public static class Keepsakes
    {
        // 단어장 목록 → 진열장 칸. 같은 보상(regionId)은 한 번만, 징표가 없는 보상은 뺀다.
        // 받은 기록이 있거나 소지품에 있으면 '획득'
        public static List<KeepsakeEntry> Collect(IEnumerable<WordDatabase> regions, GameSession session)
        {
            var entries = new List<KeepsakeEntry>();
            var seen = new HashSet<string>();
            foreach (var region in regions)
            {
                if (region == null) continue;
                foreach (var reward in region.Rewards)
                {
                    if (reward.Keepsake == null || string.IsNullOrEmpty(reward.RegionId)) continue;
                    if (!seen.Add(reward.RegionId)) continue;
                    bool owned = session.Record.HasClaimedRegion(reward.RegionId)
                                 || session.Inventory.GetCount(reward.Keepsake) > 0;
                    entries.Add(new KeepsakeEntry(region, reward, owned, Dex.GetProgress(region, session.Vocabulary)));
                }
            }
            return entries;
        }

        // 게임 데이터의 모든 지역에서 단어장을 모은다 (같은 단어장을 쓰는 지역은 Collect가 하나로 합침).
        // 진열장은 이야기 순서(DexReward.storyOrder: 초원 → 서고 → 숲 → 정글 → 동굴) — 번호가 없으면 맨 뒤 (#97)
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
            return Collect(regions, session).OrderBy(e => e.Reward.StoryOrder > 0 ? e.Reward.StoryOrder : int.MaxValue).ToList();
        }
    }
}
