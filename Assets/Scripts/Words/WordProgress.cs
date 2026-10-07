using System;
using UnityEngine;

namespace WordRPG.Words
{
    // 단어 1개에 대한 플레이어 학습 기록. JsonUtility로 저장하기 위해 시간은 UTC Ticks(long)로 보관
    [Serializable]
    public class WordProgress
    {
        [SerializeField] private string wordId;
        [SerializeField] private MasteryLevel level;
        [SerializeField] private int correctCount;
        [SerializeField] private int wrongCount;
        [SerializeField] private long nextReviewTicks;
        [SerializeField] private bool inWrongNote;
        [SerializeField] private long discoveredTicks; // 처음 만난 시각 (단어 도감용). 0이면 기록 없음

        public string WordId => wordId;
        public MasteryLevel Level => level;
        public int CorrectCount => correctCount;
        public int WrongCount => wrongCount;
        public DateTime NextReviewUtc => new DateTime(nextReviewTicks, DateTimeKind.Utc);
        public bool InWrongNote => inWrongNote; // 오답 노트: 틀린 뒤 아직 다시 맞히지 못한 단어
        public DateTime? DiscoveredUtc => discoveredTicks > 0 ? new DateTime(discoveredTicks, DateTimeKind.Utc) : (DateTime?)null;

        private WordProgress() { } // Unity 직렬화용

        public WordProgress(string wordId, DateTime discoveredUtc)
        {
            this.wordId = wordId;
            level = MasteryLevel.New;
            discoveredTicks = discoveredUtc.Ticks;
        }

        public bool IsDue(DateTime nowUtc) => level != MasteryLevel.New && nowUtc >= NextReviewUtc;

        internal void MarkDiscovered(DateTime nowUtc)
        {
            level = MasteryLevel.Learning;
            nextReviewTicks = nowUtc.Ticks;
            if (discoveredTicks == 0) discoveredTicks = nowUtc.Ticks;
        }

        internal void Apply(MasteryLevel newLevel, DateTime nextReviewUtc, bool correct)
        {
            level = newLevel;
            nextReviewTicks = nextReviewUtc.Ticks;
            if (correct)
            {
                correctCount++;
                inWrongNote = false;
            }
            else
            {
                wrongCount++;
                inWrongNote = true;
            }
        }
    }
}
