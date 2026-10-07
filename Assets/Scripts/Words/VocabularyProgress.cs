using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Words
{
    public readonly struct MasteryChange
    {
        public string WordId { get; }
        public MasteryLevel Before { get; }
        public MasteryLevel After { get; }
        public bool Correct { get; }

        public bool LeveledUp => After > Before;
        public bool LeveledDown => After < Before;

        public MasteryChange(string wordId, MasteryLevel before, MasteryLevel after, bool correct)
        {
            WordId = wordId;
            Before = before;
            After = after;
            Correct = correct;
        }
    }

    // 플레이어의 전체 단어 학습 기록. 세이브 파일에 그대로 들어간다
    [Serializable]
    public class VocabularyProgress
    {
        [SerializeField] private List<WordProgress> entries = new List<WordProgress>();

        [NonSerialized] private Dictionary<string, WordProgress> lookup;

        public IReadOnlyList<WordProgress> Entries => entries;

        // 한 번도 답한 적 없는 단어면 null
        public WordProgress Find(string wordId)
        {
            EnsureLookup();
            lookup.TryGetValue(wordId, out var progress);
            return progress;
        }

        // 한 번이라도 만난 단어 수 (단어 도감 발견 수)
        public int DiscoveredCount
        {
            get
            {
                int count = 0;
                foreach (var entry in entries)
                {
                    if (entry.Level > MasteryLevel.New) count++;
                }
                return count;
            }
        }

        public MasteryLevel GetLevel(string wordId)
        {
            var progress = Find(wordId);
            return progress?.Level ?? MasteryLevel.New;
        }

        // 숙련도 규칙:
        //  정답 + 처음 만남      → 학습
        //  정답 + 오답 노트에 있음 → 단계 유지, 오답 노트에서 빠짐 (틀린 대가로 한 단계를 다시 밟게 함)
        //  정답 + 복습 시기 도래  → 한 단계 상승
        //  정답 + 복습 시기 전    → 변화 없음 (몰아서 풀어도 숙련도가 오르지 않게)
        //  오답                  → 한 단계 하락(최저 '학습'), 오답 노트에 들어가고 즉시 다시 출제 대상
        public MasteryChange RecordAnswer(string wordId, bool correct, DateTime nowUtc, MasteryRules rules)
        {
            var progress = Find(wordId);
            if (progress == null)
            {
                progress = new WordProgress(wordId, nowUtc);
                entries.Add(progress);
                lookup[wordId] = progress;
            }

            var before = progress.Level;
            MasteryLevel after;
            DateTime nextReview;

            if (!correct)
            {
                after = before <= MasteryLevel.Learning ? MasteryLevel.Learning : before - 1;
                nextReview = nowUtc;
            }
            else if (before == MasteryLevel.New)
            {
                after = MasteryLevel.Learning;
                nextReview = nowUtc + rules.GetReviewInterval(after);
            }
            else if (progress.InWrongNote)
            {
                after = before;
                nextReview = nowUtc + rules.GetReviewInterval(after);
            }
            else if (progress.IsDue(nowUtc))
            {
                after = before == MasteryLevel.Mastered ? MasteryLevel.Mastered : before + 1;
                nextReview = nowUtc + rules.GetReviewInterval(after);
            }
            else
            {
                after = before;
                nextReview = progress.NextReviewUtc;
            }

            progress.Apply(after, nextReview, correct);
            return new MasteryChange(wordId, before, after, correct);
        }

        // 문제 없이 단어를 발견한다 (보스의 사전, #45): 아직 New면 '학습 중'으로, 바로 복습 시기가 되어 곧 문제로 나온다.
        // 반환값: 새로 발견했는지
        public bool Discover(string wordId, DateTime nowUtc)
        {
            var progress = Find(wordId);
            if (progress == null)
            {
                progress = new WordProgress(wordId, nowUtc);
                entries.Add(progress);
                lookup[wordId] = progress;
            }
            if (progress.Level != MasteryLevel.New) return false;
            progress.MarkDiscovered(nowUtc);
            return true;
        }

        private void EnsureLookup()
        {
            if (lookup != null) return;
            lookup = new Dictionary<string, WordProgress>();
            foreach (var entry in entries) lookup[entry.WordId] = entry;
        }
    }
}
