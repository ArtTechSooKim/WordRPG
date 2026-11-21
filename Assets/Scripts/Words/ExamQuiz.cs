using System;
using System.Collections.Generic;

namespace WordRPG.Words
{
    // 확인문제 → 화면의 문제 (#280). 글자는 원본 그대로 보여 준다
    //  · 5지선다: 보기 순서 그대로 '① TOF' … (섞지 않음 — '가,나,다' 묶음 보기가 있어서)
    //  · 주관식: 정답 + 오답 3개를 섞음. 오답 = 그 문제의 [보기] 목록 → 같은 장의 다른 정답 → 같은 묶음의 다른 장 정답
    public static class ExamQuiz
    {
        public const int Choices = 4;

        // 긴 글을 읽을 시간: 글자 수에 비례 (2~10초). 전투의 제한 시간·크리티컬 시간에 더한다
        public const float SecondsPerCharacter = 0.06f, MinReadingSeconds = 2f, MaxReadingSeconds = 10f;

        public static float ReadingSeconds(string prompt, IReadOnlyList<string> choices)
        {
            int chars = (prompt ?? "").Length;
            if (choices != null) foreach (var choice in choices) chars += (choice ?? "").Length;
            float seconds = chars * SecondsPerCharacter;
            return seconds < MinReadingSeconds ? MinReadingSeconds : seconds > MaxReadingSeconds ? MaxReadingSeconds : seconds;
        }

        public static QuizQuestion Create(ExamQuestion exam, IReadOnlyList<ExamQuestion> pool, Random rng)
        {
            if (exam == null) throw new ArgumentNullException(nameof(exam));
            List<string> choices;
            int correct;
            if (exam.IsMultipleChoice)
            {
                choices = new List<string>();
                for (int i = 0; i < exam.Options.Count; i++) choices.Add($"{ExamQuestion.Circled(i)} {exam.Options[i]}");
                correct = exam.AnswerIndex;
            }
            else
            {
                var picked = Distractors(exam, pool, rng);
                choices = new List<string>(picked) { exam.Answer };
                Shuffle(choices, rng);
                correct = choices.IndexOf(exam.Answer);
            }
            // 전투가 쓰는 '단어' 자리: 영어 = 정답, 뜻 = 문제 (오답 카드·글자 연출용 — 숙련도엔 안 적힘)
            var word = new WordEntry(exam.Id, exam.Answer, exam.Stem);
            return new QuizQuestion(word, QuizDirection.MeaningToEnglish, false, exam.Stem, choices, correct, exam,
                ReadingSeconds(exam.Stem, choices));
        }

        private static List<string> Distractors(ExamQuestion exam, IReadOnlyList<ExamQuestion> pool, Random rng)
        {
            var taken = new HashSet<string> { ExamQuestion.Normalize(exam.Answer) };
            var result = new List<string>();
            void TakeFrom(List<string> candidates)
            {
                Shuffle(candidates, rng);
                foreach (var candidate in candidates)
                {
                    if (result.Count >= Choices - 1) return;
                    if (taken.Add(ExamQuestion.Normalize(candidate))) result.Add(candidate);
                }
            }
            TakeFrom(new List<string>(exam.Pool));
            var sameChapter = new List<string>();
            var otherChapters = new List<string>();
            if (pool != null)
                foreach (var other in pool)
                {
                    if (other == null || other == exam || other.IsMultipleChoice) continue;
                    (other.Chapter == exam.Chapter ? sameChapter : otherChapters).Add(other.Answer);
                }
            TakeFrom(sameChapter);
            TakeFrom(otherChapters);
            return result;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    // 확인문제를 차례로 내는 쪽 (#280 보스전·석판·반딧돌): 다 낼 때까지 겹치지 않고, 다 내면 다시 섞음.
    // 숙련도·도감에는 안 적는다 (단어가 아니라 문제)
    public class ExamQuizProvider : IQuizProvider
    {
        private readonly List<ExamQuestion> questions;
        private readonly Random rng;
        private readonly List<ExamQuestion> deck = new List<ExamQuestion>();

        public IReadOnlyList<ExamQuestion> Questions => questions;
        public int Asked { get; private set; }

        public ExamQuizProvider(IReadOnlyList<ExamQuestion> questions, Random rng)
        {
            if (questions == null || questions.Count == 0) throw new ArgumentException("확인문제가 없음", nameof(questions));
            this.questions = new List<ExamQuestion>(questions);
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public QuizQuestion NextQuestion(QuizDirection preferredDirection)
        {
            if (deck.Count == 0)
            {
                deck.AddRange(questions);
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (deck[i], deck[j]) = (deck[j], deck[i]);
                }
            }
            var exam = deck[deck.Count - 1];
            deck.RemoveAt(deck.Count - 1);
            Asked++;
            return ExamQuiz.Create(exam, questions, rng);
        }

        public MasteryChange SubmitAnswer(QuizQuestion question, bool correct)
        {
            string id = question?.Word != null ? question.Word.Id : "";
            return new MasteryChange(id, MasteryLevel.New, MasteryLevel.New, correct);
        }
    }
}
