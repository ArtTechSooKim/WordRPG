using System;
using System.Collections.Generic;
using System.Linq;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 누군가의 기록 한 줄: 단어 하나의 예문. 그 단어를 찾았으면 읽을 수 있다
    public class JournalLine
    {
        public WordEntry Word { get; }
        public bool Readable { get; }
        public string Heading => Word.StoryHeading;

        public JournalLine(WordEntry word, bool readable)
        {
            Word = word;
            Readable = readable;
        }
    }

    // 누군가의 기록 (#200): 단어장마다 다른 사람의 기록 하나. 상자에서 얻어 도감에서 읽는다.
    // 도감을 채우다 보면 저절로 읽히는 이스터에그 (사용자) — 목표·보상·안내가 없고 새 단어 순서도 바꾸지 않는다.
    // 세이브 칸을 더하지 않음: 얻었는지는 소지품, 읽을 수 있는 줄은 찾은 단어로 바로 계산
    public static class Journals
    {
        public static bool Has(WordDatabase book, Inventory inventory) =>
            book != null && book.Journal != null && inventory != null && inventory.GetCount(book.Journal) > 0;

        // 기록에 들어가는 줄을 기록 순서로. 예문만 있는 단어는 빠진다. '찾음' = 도감과 같은 기준
        public static List<JournalLine> Lines(WordDatabase book, VocabularyProgress vocabulary)
        {
            return book.Words.Where(word => word.Story > 0)
                .OrderBy(word => word.Story)
                .Select(word => new JournalLine(word, vocabulary.GetLevel(word.Id) > MasteryLevel.New))
                .ToList();
        }

        // 문장에서 그 단어가 처음 나오는 자리 (낱말 전체로, 대소문자 무시). 없으면 start = -1
        public static (int start, int length) WordSpan(string sentence, string word)
        {
            if (string.IsNullOrEmpty(sentence) || string.IsNullOrEmpty(word)) return (-1, 0);
            int from = 0;
            while (from <= sentence.Length - word.Length)
            {
                int at = sentence.IndexOf(word, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0) break;
                bool startsWord = at == 0 || !char.IsLetter(sentence[at - 1]);
                int end = at + word.Length;
                bool endsWord = end == sentence.Length || !char.IsLetter(sentence[end]);
                if (startsWord && endsWord) return (at, word.Length);
                from = at + 1;
            }
            return (-1, 0);
        }

        // 낱말로 정해지는 수 (FNV-1a): 못 읽는 줄의 필기 그림을 고르는 데 씀. 줄마다 늘 같은 모양
        public static uint StableHash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
