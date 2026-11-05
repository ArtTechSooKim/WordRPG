using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using WordRPG.Core;
using WordRPG.Words;

namespace WordRPG.EditorTools
{
    // Assets/Data/Words/*.csv → 같은 이름의 WordDatabase(.asset).
    // CSV를 저장하면 자동으로 다시 임포트된다. 헤더: id,english,meaning,pos,example,example_meaning,story,story_heading
    public static class WordCsvImporter
    {
        public const string WordsFolder = "Assets/Data/Words";

        private static readonly Regex TierInFileName = new Regex(@"tier(\d+)", RegexOptions.IgnoreCase);

        [MenuItem("WordRPG/Data/Import Word CSVs")]
        public static void ImportAll()
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { WordsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsWordCsv(path)) continue;
                Import(path);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[WordRPG] 단어 CSV {count}개 임포트 완료");
        }

        public static bool IsWordCsv(string assetPath)
        {
            return assetPath.StartsWith(WordsFolder + "/", StringComparison.Ordinal)
                   && assetPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        }

        public static WordDatabase Import(string csvPath)
        {
            var rows = CsvParser.Parse(File.ReadAllText(csvPath, Encoding.UTF8));
            if (rows.Count == 0)
            {
                Debug.LogWarning($"[WordRPG] 빈 CSV: {csvPath}");
                return null;
            }

            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++) columns[rows[0][i].Trim()] = i;
            if (!columns.ContainsKey("english") || !columns.ContainsKey("meaning"))
            {
                Debug.LogError($"[WordRPG] {csvPath}: 헤더에 english, meaning 열이 필요합니다");
                return null;
            }

            var entries = new List<WordEntry>();
            var ids = new HashSet<string>();
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string english = Cell(row, columns, "english");
                string meaning = Cell(row, columns, "meaning");
                if (english.Length == 0 || meaning.Length == 0)
                {
                    Debug.LogWarning($"[WordRPG] {csvPath} {r + 1}행: english/meaning이 비어 있어 건너뜀");
                    continue;
                }

                int.TryParse(Cell(row, columns, "story"), out int story);
                var entry = new WordEntry(Cell(row, columns, "id"), english, meaning, Cell(row, columns, "pos"),
                    Cell(row, columns, "example"), Cell(row, columns, "example_meaning"), story, Cell(row, columns, "story_heading"));
                if (!ids.Add(entry.Id))
                {
                    Debug.LogWarning($"[WordRPG] {csvPath} {r + 1}행: id '{entry.Id}' 중복이라 건너뜀");
                    continue;
                }
                entries.Add(entry);
            }

            var assetPath = Path.ChangeExtension(csvPath, ".asset");
            var database = AssetDatabase.LoadAssetAtPath<WordDatabase>(assetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<WordDatabase>();
                AssetDatabase.CreateAsset(database, assetPath);
                var match = TierInFileName.Match(Path.GetFileNameWithoutExtension(csvPath));
                if (match.Success)
                {
                    var so = new SerializedObject(database);
                    so.FindProperty("tier").intValue = int.Parse(match.Groups[1].Value);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            database.ReplaceWords(entries);
            EditorUtility.SetDirty(database);
            Debug.Log($"[WordRPG] {assetPath}: 단어 {entries.Count}개");
            return database;
        }

        private static string Cell(List<string> row, Dictionary<string, int> columns, string name)
        {
            if (!columns.TryGetValue(name, out int index) || index >= row.Count) return "";
            return row[index].Trim();
        }
    }

    // CSV를 엑셀 등에서 고치고 저장하면 Unity가 다시 임포트할 때 자동으로 SO 갱신
    internal class WordCsvPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool changed = false;
            foreach (var path in imported)
            {
                if (!WordCsvImporter.IsWordCsv(path)) continue;
                WordCsvImporter.Import(path);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
        }
    }
}
