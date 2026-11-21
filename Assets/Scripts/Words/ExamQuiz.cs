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
            // 단어 자리: 영어 = 정답, 뜻 = 문제 (숙련도엔 안 적힘)
            var word = new WordEntry(exam.Id, exam.Answer, exam.Stem);
            return new QuizQuestion(word, QuizDirection.MeaningToEnglish, false, exam.Stem, choices, correct, exam);
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
}
