using System;
using System.Collections.Generic;

namespace WordRPG.Words
{
    // 전투가 단어 시스템을 몰라도 되도록 분리한 인터페이스. 테스트에서는 가짜 구현으로 대체한다
    public interface IQuizProvider
    {
        QuizQuestion NextQuestion(QuizDirection preferredDirection);
        MasteryChange SubmitAnswer(QuizQuestion question, bool correct);
    }

    // 단어 선택(WordSelector) + 문제 생성(QuizGenerator) + 숙련도 기록(VocabularyProgress)을 묶는다
    public class WordQuizService : IQuizProvider
    {
        private readonly IReadOnlyList<WordEntry> words;
        private readonly VocabularyProgress progress;
        private readonly MasteryRules rules;
        private readonly WordSelector selector;
        private readonly Random rng;
        private readonly Func<DateTime> utcNow;
        private readonly Func<bool> wantNewWord;  // 호기심 물약 (#146): 참이면 새 단어부터. 이 단어장에 더 없으면 평소대로
        private readonly Action newWordTaken;

        public WordQuizService(IReadOnlyList<WordEntry> words, VocabularyProgress progress, MasteryRules rules,
            Random rng, WordSelector selector = null, Func<DateTime> utcNow = null, Func<bool> wantNewWord = null, Action newWordTaken = null)
        {
            this.wantNewWord = wantNewWord;
            this.newWordTaken = newWordTaken;
            this.words = words;
            this.progress = progress;
            this.rules = rules;
            this.rng = rng;
            this.selector = selector ?? new WordSelector();
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public QuizQuestion NextQuestion(QuizDirection preferredDirection)
        {
            WordEntry word = null;
            if (wantNewWord != null && wantNewWord())
            {
                word = selector.SelectNew(words, progress, rng);
                if (word != null) newWordTaken?.Invoke();
            }
            word ??= selector.SelectNext(words, progress, utcNow(), rng);
            bool isNew = progress.GetLevel(word.Id) == MasteryLevel.New;
            // 처음 보는 단어는 카드로 뜻을 보여준 직후라 '영→한'으로만 묻는다
            var direction = isNew ? QuizDirection.EnglishToMeaning : preferredDirection;
            return QuizGenerator.Create(word, direction, words, rng, isNew);
        }

        public MasteryChange SubmitAnswer(QuizQuestion question, bool correct)
        {
            return progress.RecordAnswer(question.Word.Id, correct, utcNow(), rules);
        }
    }
}
