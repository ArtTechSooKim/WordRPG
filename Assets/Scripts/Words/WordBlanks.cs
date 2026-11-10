using System;
using System.Collections.Generic;
using System.Text;

namespace WordRPG.Words
{
    // 빈칸두꺼비의 기믹 (#236 사용자 ㉝): 영어 단어 글자의 25%를 '_'로 — 남은 글자로 무슨 단어인지 떠올리게.
    // 예: security → s_cur_ty. 첫 글자는 남기고, 글자(알파벳)만 가림 (띄어쓰기·하이픈은 그대로), 적어도 한 글자는 가림 (두 글자 이상일 때)
    public static class WordBlanks
    {
        public const float Ratio = 0.25f;
        public const char Blank = '_';

        public static string Mask(string word, Random rng, float ratio = Ratio)
        {
            if (string.IsNullOrEmpty(word) || rng == null) return word ?? "";
            var letters = new List<int>();
            for (int i = 1; i < word.Length; i++) // 첫 글자는 남김
                if (char.IsLetter(word[i])) letters.Add(i);
            int letterCount = letters.Count + (char.IsLetter(word[0]) ? 1 : 0);
            if (letterCount < 2 || letters.Count == 0) return word;
            int blanks = Math.Max(1, (int)Math.Round(letterCount * ratio, MidpointRounding.AwayFromZero));
            blanks = Math.Min(blanks, letters.Count);
            // 섞어서 앞에서부터 고름
            for (int i = letters.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (letters[i], letters[j]) = (letters[j], letters[i]);
            }
            var masked = new StringBuilder(word);
            for (int i = 0; i < blanks; i++) masked[letters[i]] = Blank;
            return masked.ToString();
        }

        // 답한 뒤 채운 단어 (#245 사용자): 가렸던 글자에만 색을 입힘 (Unity 서식 글자). 가린 글자가 없으면 단어 그대로
        public static string Filled(string word, string masked, string colorHex)
        {
            if (string.IsNullOrEmpty(word) || masked == null || masked.Length != word.Length) return word ?? "";
            var filled = new StringBuilder();
            for (int i = 0; i < word.Length; i++)
            {
                if (masked[i] == Blank && word[i] != Blank) filled.Append($"<color=#{colorHex}>{word[i]}</color>");
                else filled.Append(word[i]);
            }
            return filled.ToString();
        }
    }
}
