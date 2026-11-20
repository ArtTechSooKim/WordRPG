using System;
using System.Collections.Generic;
using System.Text;

namespace WordRPG.Words
{
    // 뒤섞인 철자 문제 하나 (#279 숲 이정표): 뜻 + 뒤섞인 글자를 보고, 같은 글자로 쓴 철자 4개 중 바른 것을 고른다
    public class SpellingQuestion
    {
        public WordEntry Word { get; }
        public string Scrambled { get; }               // 이정표에 뒤섞여 붙은 글자 (바른 철자·보기와 다름)
        public IReadOnlyList<string> Choices { get; }
        public int CorrectIndex { get; }

        public string Meaning => Word.Meaning;
        public string CorrectAnswer => Choices[CorrectIndex];

        public SpellingQuestion(WordEntry word, string scrambled, IReadOnlyList<string> choices, int correctIndex)
        {
            Word = word;
            Scrambled = scrambled;
            Choices = choices;
            CorrectIndex = correctIndex;
        }

        public bool IsCorrect(int index) => index == CorrectIndex;
    }

    // 철자 문제 만들기 (#279, 사용자: '뒤섞인 철자 맞히기' — 2.0 새 문제 방식을 이정표에서만 먼저).
    // 틀린 보기 = 바른 철자에서 글자 두 개만 자리를 바꾼 것 (첫 글자는 그대로 — 그럴듯하게): 이웃 글자 바꾸기 · 모음끼리 바꾸기 · 한 칸 건너 바꾸기.
    // 보기가 모두 같은 글자라 글자 수로는 못 고르고 철자를 알아야 함. 다른 진짜 단어가 되는 보기는 뺌 (silent ↔ listen 같은 것)
    public static class SpellingQuiz
    {
        public const int MinLength = 4;
        public const int MaxLength = 12;
        public const int ChoiceCount = 4;
        private const string Vowels = "aeiou";

        // 철자 문제로 낼 수 있는 단어: 영어 글자로만 된 한 단어(띄어쓰기·하이픈 없음), 길이 4~12, 틀린 보기를 3개 만들 수 있음
        public static bool Fits(WordEntry word, ICollection<string> realWords = null)
        {
            if (word == null) return false;
            string text = word.English;
            if (string.IsNullOrEmpty(text) || text.Length < MinLength || text.Length > MaxLength) return false;
            foreach (char c in text)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')) return false;
            return Misspellings(text, realWords).Count >= ChoiceCount - 1;
        }

        // 바른 철자에서 두 글자 자리만 바꾼 틀린 철자 후보 — 종류 순서(이웃 · 모음 · 한 칸 건너), 겹침·진짜 단어 없음
        public static List<string> Misspellings(string word, ICollection<string> realWords = null)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(word)) return result;
            void Add(int i, int j)
            {
                if (word[i] == word[j]) return;
                var chars = word.ToCharArray();
                (chars[i], chars[j]) = (chars[j], chars[i]);
                string s = new string(chars);
                if (s == word || result.Contains(s) || IsRealWord(s, realWords)) return;
                result.Add(s);
            }
            for (int i = 1; i + 1 < word.Length; i++) Add(i, i + 1);
            for (int i = 1; i < word.Length; i++)
            for (int j = i + 2; j < word.Length; j++)
                if (IsVowel(word[i]) && IsVowel(word[j])) Add(i, j);
            for (int i = 1; i + 2 < word.Length; i++) Add(i, i + 2);
            return result;
        }

        // 문제 하나: 틀린 보기는 종류가 섞이게 3개 (이웃 바꾸기 하나는 꼭 — 가장 헷갈리는 것), 보기 순서와 뒤섞인 글자는 rng
        public static SpellingQuestion Make(WordEntry word, Random rng, ICollection<string> realWords = null)
        {
            if (word == null) throw new ArgumentNullException(nameof(word));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            string answer = word.English;
            var pool = Misspellings(answer, realWords);
            var wrong = new List<string>();
            // 이웃 바꾸기 하나 + 나머지는 섞어서
            var adjacent = pool.FindAll(s => IsAdjacentSwap(answer, s));
            if (adjacent.Count > 0)
            {
                wrong.Add(adjacent[rng.Next(adjacent.Count)]);
                pool.Remove(wrong[0]);
            }
            Shuffle(pool, rng);
            foreach (var s in pool)
            {
                if (wrong.Count >= ChoiceCount - 1) break;
                wrong.Add(s);
            }
            // 글자가 너무 단순해 모자라면(맞는 단어를 못 찾았을 때만) 글자를 하나 겹치거나 빼서 채움
            for (int i = 1; wrong.Count < ChoiceCount - 1 && i < answer.Length + 2; i++)
            {
                string extra = i < answer.Length ? answer.Insert(i, answer[i].ToString()) : answer.Remove(i - answer.Length + 1, 1);
                if (extra != answer && !wrong.Contains(extra)) wrong.Add(extra);
            }

            var choices = new List<string>(wrong) { answer };
            Shuffle(choices, rng);
            return new SpellingQuestion(word, Scramble(answer, rng, choices), choices, choices.IndexOf(answer));
        }

        // 이정표에 붙일 뒤섞인 글자: 바른 철자·보기와 다르고, 되도록 첫 글자도 자리를 옮김
        public static string Scramble(string word, Random rng, ICollection<string> avoid = null)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 2) return word ?? "";
            string best = null;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var chars = word.ToCharArray();
                for (int i = chars.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (chars[i], chars[j]) = (chars[j], chars[i]);
                }
                string s = new string(chars);
                if (s == word || avoid != null && avoid.Contains(s)) continue;
                best = s;
                if (s[0] != word[0]) return s;
            }
            if (best != null) return best;
            var reversed = word.ToCharArray();
            Array.Reverse(reversed);
            return new string(reversed);
        }

        // 화면용: 뒤섞인 글자를 대문자로 띄워서 (T S R O F E)
        public static string Spaced(string scrambled)
        {
            var sb = new StringBuilder();
            foreach (char c in scrambled ?? "")
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        private static bool IsVowel(char c) => Vowels.IndexOf(char.ToLowerInvariant(c)) >= 0;

        // 바로 붙은 두 글자만 자리가 바뀐 철자인지
        private static bool IsAdjacentSwap(string a, string b)
        {
            if (a.Length != b.Length) return false;
            int first = -1, count = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i]) continue;
                if (first < 0) first = i;
                count++;
            }
            return count == 2 && first + 1 < a.Length && a[first] == b[first + 1] && a[first + 1] == b[first];
        }

        private static bool IsRealWord(string s, ICollection<string> realWords)
        {
            if (realWords == null) return false;
            foreach (var w in realWords)
                if (string.Equals(w, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
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
