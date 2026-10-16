using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 본문 글꼴은 한자·가나를 뺀 부분 집합 (#93 앱 크기, Tools/subset_fonts.py) — 게임에 나오는 글자가 다 들어 있는지.
    // 실패하면: 단어 뜻·이름에 새 한자를 넣었으면 python Tools/subset_fonts.py 다시 실행, 기호면 글꼴에 있는 것으로 바꾸기 (예: ✕ → ×)
    // Font.HasCharacter는 에디터에서 OS 글꼴까지 찾아 주므로 쓰지 않고, 글꼴 파일의 cmap 표를 직접 읽는다
    public class FontCoverageTests
    {
        private static readonly string[] BodyFonts = { "NotoSansKR-Regular.otf", "NotoSansKR-Bold.otf" };

        private static HashSet<int> Cmap(string file)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Application.dataPath, "Resources/UI/Fonts", file));
            int U16(int at) => (bytes[at] << 8) | bytes[at + 1];
            int U32(int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
            int tables = U16(4), cmap = -1;
            for (int i = 0; i < tables; i++)
                if (System.Text.Encoding.ASCII.GetString(bytes, 12 + 16 * i, 4) == "cmap") cmap = U32(12 + 16 * i + 8);
            Assert.GreaterOrEqual(cmap, 0, "cmap 표");
            var chars = new HashSet<int>();
            for (int i = 0, n = U16(cmap + 2); i < n; i++)
            {
                int sub = cmap + U32(cmap + 4 + 8 * i + 4);
                int format = U16(sub);
                if (format == 12) // 그룹(시작, 끝, 글리프)
                {
                    for (int g = 0, groups = U32(sub + 12); g < groups; g++)
                        for (int c = U32(sub + 16 + 12 * g); c <= U32(sub + 16 + 12 * g + 4); c++) chars.Add(c);
                }
                else if (format == 4) // 구간(끝·시작·delta·rangeOffset) — 글리프 0이면 없음
                {
                    int segs = U16(sub + 6) / 2, ends = sub + 14, starts = ends + 2 * segs + 2, deltas = starts + 2 * segs, offsets = deltas + 2 * segs;
                    for (int s = 0; s < segs; s++)
                    {
                        int start = U16(starts + 2 * s), end = U16(ends + 2 * s), delta = U16(deltas + 2 * s), ro = U16(offsets + 2 * s);
                        for (int c = start; c <= end && c != 0xFFFF; c++)
                        {
                            int glyph = ro == 0 ? (c + delta) & 0xFFFF : U16(offsets + 2 * s + ro + 2 * (c - start));
                            if (glyph != 0) chars.Add(c);
                        }
                    }
                }
            }
            return chars;
        }

        private static IEnumerable<string> GameTexts()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:WordDatabase"))
            {
                var book = AssetDatabase.LoadAssetAtPath<WordDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                yield return book.RegionName;
                foreach (var w in book.Words)
                {
                    yield return w.English;
                    yield return w.Meaning;
                    yield return w.Example;
                    yield return w.ExampleMeaning;
                }
            }
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            foreach (var m in db.Monsters) yield return m.DisplayName;
            foreach (var i in db.Items) { yield return i.DisplayName; yield return i.Description; }
            foreach (var r in db.Relics) { yield return r.DisplayName; yield return r.Description; }
            foreach (var c in db.Companions) yield return c.DisplayName;
            foreach (var a in db.Areas) yield return a.DisplayName;
        }

        private static string Missing(IEnumerable<string> texts, HashSet<int> font)
        {
            var missing = new SortedSet<char>();
            foreach (var text in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;
                foreach (char c in text)
                    if (!char.IsWhiteSpace(c) && !char.IsControl(c) && !char.IsSurrogate(c) && c != '﻿' && !font.Contains(c)) missing.Add(c);
            }
            return string.Join(" ", missing.Select(c => $"{c}(U+{(int)c:X4})"));
        }

        [Test]
        public void BodyFontHasEveryCharacterInGameData()
        {
            var texts = GameTexts().ToList();
            Assert.Greater(texts.Count, 600, "단어장·이름을 읽었는지");
            foreach (var file in BodyFonts)
                Assert.IsEmpty(Missing(texts, Cmap(file)), $"{file}에 없는 글자");
        }

        [Test]
        public void BodyFontHasEverySymbolInUiCode()
        {
            // 코드의 문자열("…") 속 글자. 주석 줄은 빼고 (줄 끝 주석 안 따옴표는 같이 읽혀도 괜찮음)
            var literal = new Regex("\"(?:[^\"\\\\]|\\\\.)*\"");
            var texts = new List<string>();
            foreach (var path in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').Contains("/Editor/")) continue;
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.TrimStart().StartsWith("//")) continue;
                    foreach (Match m in literal.Matches(line)) texts.Add(m.Value);
                }
            }
            Assert.Greater(texts.Count, 500);
            foreach (var file in BodyFonts)
                Assert.IsEmpty(Missing(texts, Cmap(file)), $"{file}에 없는 글자 (기호면 글꼴에 있는 것으로)");
        }

        [Test]
        public void HangulIsCompleteSoNewMeaningsAlwaysShow()
        {
            foreach (var file in BodyFonts)
            {
                var font = Cmap(file);
                for (int c = 0xAC00; c <= 0xD7A3; c++) Assert.IsTrue(font.Contains(c), $"{file}: 한글 음절 {(char)c}");
                foreach (char c in "ㄱㅎㅏㅣAz09·…→×✓★") Assert.IsTrue(font.Contains(c), $"{file}: {c}");
                // 검사가 진짜로 작동하는지: 원래 없는 ✕, 뺀 한자 漢은 없어야 함
                Assert.IsFalse(font.Contains('✕'), "✕는 Noto에 없음");
                Assert.IsFalse(font.Contains('漢'), "쓰지 않는 한자는 뺐음");
            }
        }
    }
}
