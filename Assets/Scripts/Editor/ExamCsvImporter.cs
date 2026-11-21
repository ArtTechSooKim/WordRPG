using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using WordRPG.Core;
using WordRPG.Words;

namespace WordRPG.EditorTools
{
    // Assets/Data/Exams/*.csv → 같은 이름의 ExamBook(.asset) (#280). CSV를 저장하면 자동으로 다시 임포트된다.
    // 헤더: id,chapter,chapter_title,number,question,answer — 의학 용어 확인문제는 Tools/medical_course.py가 원본에서 만듦
    public static class ExamCsvImporter
    {
        public const string ExamsFolder = "Assets/Data/Exams";

        public static bool IsExamCsv(string assetPath)
        {
            return assetPath.StartsWith(ExamsFolder + "/", StringComparison.Ordinal)
                   && assetPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        }

        public static ExamBook Import(string csvPath)
        {
            var rows = CsvParser.Parse(File.ReadAllText(csvPath, Encoding.UTF8));
            if (rows.Count == 0) return null;
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++) columns[rows[0][i].Trim()] = i;
            var questions = new List<ExamQuestion>();
            var ids = new HashSet<string>();
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Cell(row, columns, "id"), question = Cell(row, columns, "question"), answer = Cell(row, columns, "answer");
                if (id.Length == 0 || question.Length == 0 || answer.Length == 0 || !ids.Add(id))
                {
                    Debug.LogWarning($"[WordRPG] {csvPath} {r + 1}행: id·문제·정답이 비었거나 id가 겹쳐 건너뜀");
                    continue;
                }
                int.TryParse(Cell(row, columns, "chapter"), out int chapter);
                int.TryParse(Cell(row, columns, "number"), out int number);
                questions.Add(new ExamQuestion(id, chapter, Cell(row, columns, "chapter_title"), number, question, answer));
            }

            var assetPath = Path.ChangeExtension(csvPath, ".asset");
            var book = AssetDatabase.LoadAssetAtPath<ExamBook>(assetPath);
            if (book == null)
            {
                book = ScriptableObject.CreateInstance<ExamBook>();
                AssetDatabase.CreateAsset(book, assetPath);
            }
            book.ReplaceQuestions(questions);
            EditorUtility.SetDirty(book);
            Debug.Log($"[WordRPG] {assetPath}: 확인문제 {questions.Count}개");
            return book;
        }

        private static string Cell(List<string> row, Dictionary<string, int> columns, string name)
        {
            if (!columns.TryGetValue(name, out int index) || index >= row.Count) return "";
            return row[index].Trim();
        }
    }

    internal class ExamCsvPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool changed = false;
            foreach (var path in imported)
            {
                if (!ExamCsvImporter.IsExamCsv(path)) continue;
                ExamCsvImporter.Import(path);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
        }
    }
}
