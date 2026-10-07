using System;
using System.Collections.Generic;
using System.Globalization;
using WordRPG.Field;
using WordRPG.Words;

namespace WordRPG.Game
{
    public enum DictionaryReadOutcome
    {
        NoDictionary,     // 이 지역에는 사전이 없음
        Locked,           // 아직 보스를 물리치지 않아 받침대가 비어 있음
        AlreadyReadToday, // 오늘 이미 읽음 ("오늘 책은 충분히 읽은 것 같다")
        AllDiscovered,    // 이 단어장의 단어를 모두 발견함 (그날 읽은 것으로 치지 않음)
        Discovered,       // 새 단어 하나를 발견함
    }

    public readonly struct DictionaryRead
    {
        public DictionaryReadOutcome Outcome { get; }
        public WordEntry Word { get; }

        public DictionaryRead(DictionaryReadOutcome outcome, WordEntry word = null)
        {
            Outcome = outcome;
            Word = word;
        }
    }

    // 보스의 사전 (#45, 사용자 아이디어): 보스를 물리치면 그 맵 쉼터의 받침대(맵 글자 L)에 사전이 놓인다.
    // 하루에 한 번 읽을 수 있고, 읽으면 그 맵 단어장에서 아직 발견하지 못한 단어 하나를 발견한다 (도감을 빨리 채우게, 탐험 유도).
    // 하루 = 기기의 날짜 (자정에 바뀜). 읽은 날은 지역별로 세이브(WorldState)에 남는다
    public static class BossDictionary
    {
        public static string DayKey(DateTime localNow) => localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static bool IsUnlocked(FieldArea area, WorldState world) =>
            area != null && area.HasDictionary && area.Boss != null && world.IsBossDefeated(area.BossId);

        public static bool HasReadToday(FieldArea area, WorldState world, DateTime localNow) =>
            area != null && world.DictionaryReadDay(area.AreaId) == DayKey(localNow);

        // 이 단어장에서 아직 발견하지 못한 단어
        public static List<WordEntry> Undiscovered(FieldArea area, VocabularyProgress vocabulary)
        {
            var list = new List<WordEntry>();
            if (area == null || area.Words == null) return list;
            foreach (var word in area.Words.Words)
                if (vocabulary.GetLevel(word.Id) == MasteryLevel.New && !list.Contains(word)) list.Add(word);
            return list;
        }

        // 받침대에서 [확인]: 읽을 수 있으면 단어 하나를 발견하고 오늘 읽은 것으로 기록
        public static DictionaryRead Read(FieldArea area, GameSession session, DateTime localNow, DateTime utcNow, Random rng)
        {
            if (area == null || !area.HasDictionary) return new DictionaryRead(DictionaryReadOutcome.NoDictionary);
            if (!IsUnlocked(area, session.World)) return new DictionaryRead(DictionaryReadOutcome.Locked);
            if (HasReadToday(area, session.World, localNow)) return new DictionaryRead(DictionaryReadOutcome.AlreadyReadToday);

            var candidates = Undiscovered(area, session.Vocabulary);
            if (candidates.Count == 0) return new DictionaryRead(DictionaryReadOutcome.AllDiscovered);

            var word = candidates[rng.Next(candidates.Count)];
            session.Vocabulary.Discover(word.Id, utcNow);
            session.World.MarkDictionaryRead(area.AreaId, DayKey(localNow));
            return new DictionaryRead(DictionaryReadOutcome.Discovered, word);
        }
    }
}
