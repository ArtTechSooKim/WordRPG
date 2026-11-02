using System;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Game
{
    public readonly struct DexProgress
    {
        public int Discovered { get; }
        public int Total { get; }
        public int Mastered { get; }

        public bool IsComplete => Total > 0 && Discovered >= Total;
        public float Ratio => Total == 0 ? 0f : (float)Discovered / Total;

        public DexProgress(int discovered, int total, int mastered)
        {
            Discovered = discovered;
            Total = total;
            Mastered = mastered;
        }
    }

    // 도감 보상이 지급된 기록 (결과 화면 표시용)
    public class DexCompletion
    {
        public WordDatabase Region { get; }
        public DexReward Reward { get; }
        public ItemData Keepsake => Reward.Keepsake;
        public int Gold => Reward.Gold;

        public DexCompletion(WordDatabase region, DexReward reward)
        {
            Region = region;
            Reward = reward;
        }

        // 단어장을 다 채웠으면 도감 완성, 중간 보상이면 찾은 단어 수
        public string Headline
        {
            get
            {
                int needed = Reward.WordsNeeded(Region.Words.Count);
                return needed >= Region.Words.Count ? $"★ {Region.RegionName} 도감 완성!" : $"★ 단어 {needed}개 발견!";
            }
        }
    }

    // 단어 도감 규칙. '발견' = 한 번이라도 문제로 만난 단어 (맞히든 틀리든)
    public static class Dex
    {
        public static DexProgress GetProgress(WordDatabase region, VocabularyProgress vocabulary)
        {
            int discovered = 0, mastered = 0;
            foreach (var word in region.Words)
            {
                var level = vocabulary.GetLevel(word.Id);
                if (level > MasteryLevel.New) discovered++;
                if (level == MasteryLevel.Mastered) mastered++;
            }
            return new DexProgress(discovered, region.Words.Count, mastered);
        }

        public static bool IsEarned(DexReward reward, DexProgress progress) =>
            progress.Total > 0 && progress.Discovered >= reward.WordsNeeded(progress.Total);

        // 아직 안 받은 보상 중 가장 먼저 받을 것. 다 받았으면 null
        public static DexReward NextReward(WordDatabase region, PlayerRecord record)
        {
            foreach (var reward in region.Rewards)
                if (!record.HasClaimedRegion(reward.RegionId)) return reward;
            return null;
        }

        // 학습 ★☆☆☆ ... 완전 숙련 ★★★★. 미발견은 빈 문자열
        public static string Stars(MasteryLevel level)
        {
            int filled = (int)level;
            return level == MasteryLevel.New ? "" : new string('★', filled) + new string('☆', 4 - filled);
        }

        public static string DescribeNextReview(WordProgress progress, DateTime nowUtc)
        {
            if (progress == null || progress.Level == MasteryLevel.New) return "";
            if (progress.InWrongNote) return "오답 노트 — 다시 도전해 보세요";
            if (progress.IsDue(nowUtc)) return "복습할 때가 됐어요";

            var left = progress.NextReviewUtc - nowUtc;
            if (left.TotalDays >= 1) return $"{(int)left.TotalDays}일 후 복습";
            if (left.TotalHours >= 1) return $"{(int)left.TotalHours}시간 후 복습";
            return $"{Math.Max(1, (int)left.TotalMinutes)}분 후 복습";
        }
    }
}
