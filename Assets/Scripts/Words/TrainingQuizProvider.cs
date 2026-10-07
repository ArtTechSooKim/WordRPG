using System;
using System.Collections.Generic;

namespace WordRPG.Words
{
    // 수련 방식 (#44, 사용자 아이디어): 도감에서 이미 발견한 단어만 낸다 — 새 단어는 나오지 않는다
    public enum TrainingMode
    {
        All,       // 전체적 암기: 발견한 단어를 골고루 (한 바퀴를 다 돌아야 같은 단어가 다시 나옴)
        WrongNote, // 오답 위주 암기: 오답 노트 단어를 먼저, 다 맞히면 숙련도가 낮은 단어부터
    }

    // 수련용 문제 내기. 출제 후보 = 여러 단어장(지역) 중 이미 본 단어. 보기(오답)는 그 단어의 단어장에서 고른다.
    // 숙련도·오답 노트는 실제 전투와 똑같이 기록한다
    public class TrainingQuizProvider : IQuizProvider
    {
        private readonly List<IReadOnlyList<WordEntry>> books = new List<IReadOnlyList<WordEntry>>();
        private readonly Dictionary<WordEntry, IReadOnlyList<WordEntry>> bookOf = new Dictionary<WordEntry, IReadOnlyList<WordEntry>>();
        private readonly VocabularyProgress progress;
        private readonly MasteryRules rules;
        private readonly Random rng;
        private readonly Func<DateTime> utcNow;
        private readonly List<WordEntry> deck = new List<WordEntry>(); // 전체적 암기: 남은 한 바퀴
        private WordEntry last;

        public TrainingMode Mode { get; }

        public TrainingQuizProvider(IEnumerable<IReadOnlyList<WordEntry>> wordBooks, VocabularyProgress progress, MasteryRules rules,
            TrainingMode mode, Random rng, Func<DateTime> utcNow = null)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.rules = rules;
            this.rng = rng ?? new Random();
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            Mode = mode;
            foreach (var book in wordBooks)
            {
                if (book == null || book.Count == 0 || books.Contains(book)) continue;
                books.Add(book);
                foreach (var word in book)
                    if (!bookOf.ContainsKey(word)) bookOf[word] = book;
            }
            if (Discovered().Count == 0) throw new InvalidOperationException("발견한 단어가 없어서 수련할 수 없습니다");
        }

        // 수련 창에 보여 줄 수: 발견한 단어 · 오답 노트 (같은 단어가 여러 단어장에 있어도 한 번)
        public static (int discovered, int wrongNote) Count(IEnumerable<IReadOnlyList<WordEntry>> wordBooks, VocabularyProgress progress)
        {
            var seen = new HashSet<string>();
            int discovered = 0, wrong = 0;
            foreach (var book in wordBooks)
            {
                if (book == null) continue;
                foreach (var word in book)
                {
                    if (!seen.Add(word.Id)) continue;
                    var p = progress.Find(word.Id);
                    if (p == null || p.Level == MasteryLevel.New) continue;
                    discovered++;
                    if (p.InWrongNote) wrong++;
                }
            }
            return (discovered, wrong);
        }

        public QuizQuestion NextQuestion(QuizDirection preferredDirection)
        {
            var word = Mode == TrainingMode.WrongNote ? PickWrongFirst() : PickFromDeck();
            last = word;
            return QuizGenerator.Create(word, preferredDirection, bookOf[word], rng, false);
        }

        public MasteryChange SubmitAnswer(QuizQuestion question, bool correct) =>
            progress.RecordAnswer(question.Word.Id, correct, utcNow(), rules);

        // 이미 본 단어 (단어장 순서, 같은 단어는 한 번)
        private List<WordEntry> Discovered()
        {
            var list = new List<WordEntry>();
            var ids = new HashSet<string>();
            foreach (var book in books)
            foreach (var word in book)
            {
                if (!ids.Add(word.Id)) continue;
                var p = progress.Find(word.Id);
                if (p != null && p.Level != MasteryLevel.New) list.Add(word);
            }
            return list;
        }

        private WordEntry PickFromDeck()
        {
            if (deck.Count == 0)
            {
                deck.AddRange(Discovered());
                Shuffle(deck);
                // 새 바퀴의 첫 단어가 방금 낸 단어면 뒤로 보낸다
                if (deck.Count > 1 && deck[0] == last)
                {
                    deck.RemoveAt(0);
                    deck.Add(last);
                }
            }
            var word = deck[0];
            deck.RemoveAt(0);
            return word;
        }

        private WordEntry PickWrongFirst()
        {
            var discovered = Discovered();
            var wrong = discovered.FindAll(w => progress.Find(w.Id).InWrongNote);
            var pool = wrong.Count > 0 ? wrong : Weakest(discovered);
            // 방금 낸 단어는 다른 후보가 있으면 피한다
            if (pool.Count > 1) pool.Remove(last);
            return pool[rng.Next(pool.Count)];
        }

        // 오답 노트가 비면: 숙련도가 가장 낮은 단어들
        private List<WordEntry> Weakest(List<WordEntry> words)
        {
            var lowest = MasteryLevel.Mastered;
            foreach (var word in words)
            {
                var level = progress.GetLevel(word.Id);
                if (level < lowest) lowest = level;
            }
            return words.FindAll(w => progress.GetLevel(w.Id) == lowest);
        }

        private void Shuffle(List<WordEntry> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
