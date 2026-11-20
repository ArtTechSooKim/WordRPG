using System;
using System.Collections.Generic;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 이정표 문제 (#279 숲): 이미 본 단어 하나로 뒤섞인 철자 문제 (SpellingQuiz). 석판·반딧돌과 같은 규칙 —
    // 틀리면 그 보기만 꺼지고 벌점 없이 다시 고름 (사용자: 스트레스 받지 않게), 숙련도는 처음 고른 답만 기록한다
    public class SignpostQuiz
    {
        public const int WordTries = 12; // 철자 문제로 못 내는 단어(띄어쓰기·너무 짧음)가 나오면 다른 단어를 이만큼까지

        private readonly IQuizProvider provider;
        private readonly QuizQuestion source; // 숙련도 기록에 쓰는 원래 문제
        private bool missed;

        public SpellingQuestion Current { get; }
        public bool IsSolved { get; private set; }
        public int Mistakes { get; private set; }

        // realWords: 틀린 보기로 쓰면 안 되는 진짜 단어들 (이 지역 단어장)
        public SignpostQuiz(IQuizProvider provider, Random rng, ICollection<string> realWords = null)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            QuizQuestion first = null;
            for (int i = 0; i < WordTries; i++)
            {
                var question = provider.NextQuestion(QuizDirection.EnglishToMeaning);
                if (question == null) break;
                first = first ?? question;
                if (!SpellingQuiz.Fits(question.Word, realWords)) continue;
                source = question;
                break;
            }
            source = source ?? first ?? throw new InvalidOperationException("이정표 문제로 낼 단어가 없습니다");
            Current = SpellingQuiz.Make(source.Word, rng, realWords);
        }

        public TabletAnswer Answer(int choiceIndex)
        {
            if (IsSolved) return TabletAnswer.Ignored;
            bool correct = Current.IsCorrect(choiceIndex);
            if (!missed) provider.SubmitAnswer(source, correct);
            if (!correct)
            {
                missed = true;
                Mistakes++;
                return TabletAnswer.Wrong;
            }
            IsSolved = true;
            return TabletAnswer.Correct;
        }
    }
}
