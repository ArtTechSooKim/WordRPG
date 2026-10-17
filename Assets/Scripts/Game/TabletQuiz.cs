using System;
using WordRPG.Words;

namespace WordRPG.Game
{
    public enum TabletAnswer
    {
        Correct, // 빈칸이 채워짐 → 잠깐 보여 준 뒤 Next()
        Wrong,   // 그 선택지만 틀림 — 벌점 없이 다시 고름 (사용자: 스트레스 받지 않게)
        Ignored  // 이미 다 풀었음
    }

    // 단어 석판 문제 (#81, 1.1 메아리 정글 유적 — Figma '석판 문제 (#81)'): 석판마다 3문제, 새겨진 뜻을 보고 빈칸에 들어갈 영단어를 고른다.
    // 문제는 이미 본 단어만 (선제공격과 같은 단어장 고르기 — Preemptive.QuizBooks). 숙련도는 문제마다 처음 고른 답만 기록한다.
    // 1.2 동굴의 글자 반딧돌(#113)도 같은 규칙 — 한 문제, 영단어를 보고 뜻 고르기
    public class TabletQuiz
    {
        public const int QuestionsPerTablet = 3;

        private readonly IQuizProvider provider;
        private readonly int questions;
        private readonly QuizDirection direction;
        private bool missed;   // 지금 문제를 한 번 틀렸는지 (다시 골라 맞혀도 숙련도엔 '틀림' 하나만)
        private bool answered; // 지금 문제를 맞혔고 Next()를 기다리는 중

        public QuizQuestion Current { get; private set; }
        public int Solved { get; private set; }
        public int Mistakes { get; private set; }
        public bool IsComplete => Solved >= questions;

        public TabletQuiz(IQuizProvider provider) : this(provider, QuestionsPerTablet, QuizDirection.MeaningToEnglish) { }

        public TabletQuiz(IQuizProvider provider, int questions, QuizDirection direction)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.questions = Math.Max(1, questions);
            this.direction = direction;
            Current = provider.NextQuestion(direction);
        }

        public TabletAnswer Answer(int choiceIndex)
        {
            if (IsComplete || answered) return TabletAnswer.Ignored;
            bool correct = Current.IsCorrect(choiceIndex);
            if (!missed) provider.SubmitAnswer(Current, correct);
            if (!correct)
            {
                missed = true;
                Mistakes++;
                return TabletAnswer.Wrong;
            }
            Solved++;
            answered = true;
            return TabletAnswer.Correct;
        }

        // 맞힌 뒤 다음 문제로 (다 풀었으면 그대로)
        public void Next()
        {
            if (!answered) return;
            answered = false;
            missed = false;
            if (!IsComplete) Current = provider.NextQuestion(direction);
        }
    }
}
