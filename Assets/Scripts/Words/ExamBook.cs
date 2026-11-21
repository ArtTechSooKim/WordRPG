using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Words
{
    // 확인문제 묶음 (#280): Assets/Data/Exams/{이름}.csv → 같은 이름의 .asset (ExamCsvImporter).
    // 헤더: id,chapter,chapter_title,number,question,answer — 의학 용어 = 의대생이 장마다 만든 문제 (Tools/medical_course.py)
    [CreateAssetMenu(fileName = "NewExamBook", menuName = "WordRPG/Exam Book", order = 2)]
    public class ExamBook : ScriptableObject
    {
        [SerializeField] private List<ExamQuestion> questions = new List<ExamQuestion>();

        public IReadOnlyList<ExamQuestion> Questions => questions;

        // 그 장들의 문제 (장 순서 → 번호 순서)
        public List<ExamQuestion> InChapters(IEnumerable<int> chapters)
        {
            var wanted = new HashSet<int>(chapters ?? new int[0]);
            var found = new List<ExamQuestion>();
            foreach (var question in questions)
                if (question != null && wanted.Contains(question.Chapter)) found.Add(question);
            return found;
        }

        // 임포터·테스트 전용
        public void ReplaceQuestions(IEnumerable<ExamQuestion> newQuestions)
        {
            questions = new List<ExamQuestion>(newQuestions);
        }
    }
}
