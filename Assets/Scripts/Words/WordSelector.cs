using System;
using System.Collections.Generic;

namespace WordRPG.Words
{
    // 다음에 출제할 단어를 고른다. 우선순위:
    //  1. 오답 노트  2. 복습 시기가 된 단어  3. 새 단어 (학습 중인 단어가 너무 많지 않을 때)
    //  4. 이미 본 단어 중 숙련도가 낮은 것  5. 아무 단어
    // 각 단계에서 최근에 낸 단어는 건너뛰어 같은 단어가 연달아 나오지 않게 한다
    public class WordSelector
    {
        private readonly int recentHistorySize;
        private readonly int maxWordsInLearning;
        private readonly LinkedList<string> recent = new LinkedList<string>();

        public WordSelector(int recentHistorySize = 4, int maxWordsInLearning = 12)
        {
            this.recentHistorySize = recentHistorySize;
            this.maxWordsInLearning = maxWordsInLearning;
        }

        public WordEntry SelectNext(IReadOnlyList<WordEntry> words, VocabularyProgress progress, DateTime nowUtc, Random rng)
        {
            if (words == null || words.Count == 0) throw new ArgumentException("단어장이 비어 있습니다", nameof(words));

            var wrongNote = new List<WordEntry>();
            var due = new List<WordEntry>();
            var fresh = new List<WordEntry>();
            var seen = new List<WordEntry>();
            int learningCount = 0;

            foreach (var word in words)
            {
                var p = progress.Find(word.Id);
                if (p != null && p.Level == MasteryLevel.Learning) learningCount++;
                if (recent.Contains(word.Id)) continue;

                if (p == null || p.Level == MasteryLevel.New) fresh.Add(word);
                else if (p.InWrongNote) wrongNote.Add(word);
                else if (p.IsDue(nowUtc)) due.Add(word);
                else seen.Add(word);
            }

            WordEntry picked;
            if (wrongNote.Count > 0) picked = PickRandom(wrongNote, rng);
            else if (due.Count > 0) picked = PickLowestLevel(due, progress, rng);
            else if (fresh.Count > 0 && learningCount < maxWordsInLearning) picked = PickRandom(fresh, rng);
            else if (seen.Count > 0) picked = PickLowestLevel(seen, progress, rng);
            else if (fresh.Count > 0) picked = PickRandom(fresh, rng);
            else picked = words[rng.Next(words.Count)]; // 단어 수가 최근 기록보다 적을 때

            Remember(picked.Id);
            return picked;
        }

        // 아직 안 만난 단어 하나 (호기심 물약 #146). 이 단어장에 더 없으면 null
        public WordEntry SelectNew(IReadOnlyList<WordEntry> words, VocabularyProgress progress, Random rng)
        {
            var fresh = new List<WordEntry>();
            foreach (var word in words)
            {
                var p = progress.Find(word.Id);
                if ((p == null || p.Level == MasteryLevel.New) && !recent.Contains(word.Id)) fresh.Add(word);
            }
            if (fresh.Count == 0) return null;
            var picked = PickRandom(fresh, rng);
            Remember(picked.Id);
            return picked;
        }

        private void Remember(string wordId)
        {
            recent.Remove(wordId);
            recent.AddLast(wordId);
            while (recent.Count > recentHistorySize) recent.RemoveFirst();
        }

        private static WordEntry PickRandom(List<WordEntry> candidates, Random rng)
        {
            return candidates[rng.Next(candidates.Count)];
        }

        private static WordEntry PickLowestLevel(List<WordEntry> candidates, VocabularyProgress progress, Random rng)
        {
            var lowest = MasteryLevel.Mastered;
            foreach (var word in candidates)
            {
                var level = progress.GetLevel(word.Id);
                if (level < lowest) lowest = level;
            }

            var pool = candidates.FindAll(w => progress.GetLevel(w.Id) == lowest);
            return PickRandom(pool, rng);
        }
    }
}
