using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Items;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 철마다 여는 선물 퀴즈 (#274 사용자: 12월 1~31일에 켠 사람에게 크리스마스 단어 문제 3개 — 맞힌 문제마다 특제 상처약 1 + 살금살금 물약 1,
    // 하나도 못 맞혀도 기본 선물 · 받기 전엔 무엇인지 비밀 · 받은 뒤 개발자의 편지). 날짜는 기기의 현지 날짜. 받은 기록 = WorldState 이벤트 "{eventId}_{그해}" — 세이브마다 해마다 한 번 (세이브 칸 추가 없음, 내년에도 그대로 열림)
    [CreateAssetMenu(fileName = "NewGiftEvent", menuName = "WordRPG/Gift Event", order = 30)]
    public class GiftEvent : ScriptableObject
    {
        [Tooltip("세이브에 '{id}_{연도}'로 적힘 — 출시 뒤 바꾸지 말 것 (바꾸면 이미 받은 사람이 또 받음)")]
        [SerializeField] private string eventId;
        [SerializeField] private string displayName; // 꼬리표. 예: "크리스마스 선물"
        [SerializeField] private string title;       // 첫 창 제목. 예: "메리 크리스마스!"
        [SerializeField] private string quizName;    // 문제 창 꼬리표. 예: "크리스마스 선물 퀴즈"
        [SerializeField] private string wordsName;   // 첫 창 설명에 들어가는 단어 종류. 예: "크리스마스 영단어"
        [Header("여는 날 (현지 날짜, 끝 날 포함)")]
        [SerializeField] private int startMonth = 12;
        [SerializeField] private int startDay = 1;
        [SerializeField] private int endMonth = 12;
        [SerializeField] private int endDay = 31;
        [Header("퀴즈")]
        [SerializeField] private int questions = 3;
        [Tooltip("문제로 나오는 단어 (지역 도감·숙련도에는 들어가지 않음)")]
        [SerializeField] private WordDatabase words;
        [Tooltip("하나도 못 맞혀도 받는 기본 선물 (선물 상자 1개)")]
        [SerializeField] private List<ItemGift> baseGift = new List<ItemGift>();
        [Tooltip("한 문제를 맞힐 때마다 더 받는 것 (선물 상자 1개씩)")]
        [SerializeField] private List<ItemGift> rewardPerCorrect = new List<ItemGift>();
        [Tooltip("선물을 받은 뒤 읽는 편지 — 받으면 메일함에도 들어감 (#275). 비우면 편지 없음")]
        [SerializeField] private MailData letter;

        public string EventId => eventId;
        public string DisplayName => displayName;
        public string Title => title;
        public string QuizName => string.IsNullOrEmpty(quizName) ? displayName : quizName;
        public string WordsName => wordsName;
        public int Questions => Mathf.Max(1, questions);
        public WordDatabase Words => words;
        public IReadOnlyList<ItemGift> BaseGift => baseGift ?? (IReadOnlyList<ItemGift>)new List<ItemGift>();
        public IReadOnlyList<ItemGift> RewardPerCorrect => rewardPerCorrect;
        public MailData Letter => letter;
        public bool HasLetter => letter != null;
        public int StartMonth => startMonth;
        public int StartDay => startDay;
        public int EndMonth => endMonth;
        public int EndDay => endDay;

        // 끝 날 안내 글. 예: "12월 31일"
        public string EndLabel => $"{endMonth}월 {endDay}일";

        // 오늘(현지 날짜)이 여는 기간인지. 시작이 끝보다 늦으면(예: 12월 20일 ~ 1월 5일) 해를 넘기는 기간
        public bool IsOpen(DateTime localNow)
        {
            int today = localNow.Month * 100 + localNow.Day;
            int start = startMonth * 100 + startDay, end = endMonth * 100 + endDay;
            return start <= end ? today >= start && today <= end : today >= start || today <= end;
        }

        // 받은 기록 id: 해를 넘기는 기간이면 시작한 해로 (1월에 받아도 지난 12월 것과 같은 한 번)
        public string ClaimId(DateTime localNow)
        {
            int start = startMonth * 100 + startDay, end = endMonth * 100 + endDay;
            int today = localNow.Month * 100 + localNow.Day;
            int year = start > end && today <= end ? localNow.Year - 1 : localNow.Year;
            return $"{eventId}_{year}";
        }

#if UNITY_EDITOR
        // 샘플 데이터 생성기·테스트 전용
        public void Configure(string id, string name, string titleText, string quizLabel, string wordsLabel,
            int fromMonth, int fromDay, int toMonth, int toDay, int questionCount, WordDatabase wordBook,
            IEnumerable<ItemGift> basic, IEnumerable<ItemGift> perCorrect)
        {
            baseGift = new List<ItemGift>(basic ?? new ItemGift[0]);
            eventId = id;
            displayName = name;
            title = titleText;
            quizName = quizLabel;
            wordsName = wordsLabel;
            startMonth = fromMonth;
            startDay = fromDay;
            endMonth = toMonth;
            endDay = toDay;
            questions = questionCount;
            words = wordBook;
            rewardPerCorrect = new List<ItemGift>(perCorrect);
        }

        public void ConfigureLetter(MailData mail) => letter = mail;
#endif
    }

    // 선물 퀴즈 규칙 (#274)
    public static class GiftEvents
    {
        public static bool Claimed(GameSession session, GiftEvent gift, DateTime localNow) =>
            session != null && gift != null && session.World.HasEvent(gift.ClaimId(localNow));

        // 문제를 낼 수 있는지 (단어가 문제 수 + 보기 3개보다 적으면 안 엶)
        public static bool IsPlayable(GiftEvent gift) =>
            gift != null && !string.IsNullOrEmpty(gift.EventId) && gift.Words != null
            && gift.Words.Words.Count >= Math.Max(gift.Questions, QuizGenerator.DefaultChoiceCount);

        // 오늘 열려 있고 이 세이브가 올해 아직 안 받은 선물 퀴즈 (없으면 null)
        public static GiftEvent Offer(GameSession session, IEnumerable<GiftEvent> gifts, DateTime localNow)
        {
            if (session == null || gifts == null) return null;
            foreach (var gift in gifts)
                if (IsPlayable(gift) && gift.IsOpen(localNow) && !Claimed(session, gift, localNow)) return gift;
            return null;
        }

        // 선물 상자 수 = 기본 1개(기본 선물이 있으면) + 맞힌 문제마다 1개
        public static int Boxes(GiftEvent gift, int correct) =>
            gift == null ? 0 : (HasAny(gift.BaseGift) ? 1 : 0) + (HasAny(gift.RewardPerCorrect) ? Math.Max(0, correct) : 0);

        // 받을 것: 기본 선물 + 맞힌 문제마다 한 번 더 — 같은 아이템은 한 줄로 합침 (처음 나온 순서)
        public static List<ItemGift> Total(GiftEvent gift, int correct)
        {
            var order = new List<ItemData>();
            var counts = new Dictionary<ItemData, int>();
            void Add(IReadOnlyList<ItemGift> lines, int times)
            {
                if (lines == null || times <= 0) return;
                foreach (var line in lines)
                {
                    if (line == null || line.Item == null || line.Count <= 0) continue;
                    if (!counts.ContainsKey(line.Item))
                    {
                        counts[line.Item] = 0;
                        order.Add(line.Item);
                    }
                    counts[line.Item] += line.Count * times;
                }
            }
            if (gift != null)
            {
                Add(gift.BaseGift, 1);
                Add(gift.RewardPerCorrect, correct);
            }
            return order.ConvertAll(item => new ItemGift(item, counts[item]));
        }

        // 퀴즈를 마치면: 기본 선물 + 맞힌 수만큼을 소지품에 넣고 올해 받은 것으로 적음 + 편지는 메일함에 (#275).
        // 반환 = 받은 것 (이미 받았으면 빈 목록)
        public static List<ItemGift> Claim(GameSession session, GiftEvent gift, int correct, DateTime localNow)
        {
            if (session == null || gift == null || Claimed(session, gift, localNow)) return new List<ItemGift>();
            var given = Total(gift, correct);
            foreach (var line in given) session.Inventory.Add(line.Item, line.Count);
            session.World.MarkEvent(gift.ClaimId(localNow));
            Mails.Deliver(session, gift.Letter, localNow);
            return given;
        }

        private static bool HasAny(IReadOnlyList<ItemGift> lines)
        {
            if (lines == null) return false;
            foreach (var line in lines)
                if (line != null && line.Item != null && line.Count > 0) return true;
            return false;
        }
    }

    public enum GiftAnswer
    {
        Correct,
        Wrong,  // 그 문제의 선물은 없음 — 다시 고를 수 없음 (사용자: 맞힌 문제마다 선물)
        Ignored // 이미 답했거나 다 풀었음
    }

    // 선물 퀴즈 한 판: 단어장에서 겹치지 않게 고른 단어로 문제 N개 (영단어 → 뜻). 문제마다 한 번만 답함.
    // 숙련도·도감에는 기록하지 않음 (지역 단어가 아니라서)
    public class GiftQuiz
    {
        private readonly List<QuizQuestion> questions = new List<QuizQuestion>();
        private readonly List<bool> results = new List<bool>();

        public int Count => questions.Count;
        public int Index { get; private set; }          // 지금 문제 (0부터)
        public QuizQuestion Current => Index < Count ? questions[Index] : null;
        public bool Answered { get; private set; }      // 지금 문제에 답했고 Next()를 기다리는 중
        public int CorrectCount { get; private set; }
        public int LastPick { get; private set; } = -1;
        public bool IsComplete => Index >= Count;
        public IReadOnlyList<bool> Results => results;  // 답한 문제마다 맞혔는지

        public GiftQuiz(IReadOnlyList<WordEntry> pool, int count, System.Random rng)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var order = new List<WordEntry>(pool);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            for (int i = 0; i < Math.Min(count, order.Count); i++)
                questions.Add(QuizGenerator.Create(order[i], QuizDirection.EnglishToMeaning, pool, rng));
        }

        public GiftAnswer Answer(int choiceIndex)
        {
            if (IsComplete || Answered) return GiftAnswer.Ignored;
            bool correct = Current.IsCorrect(choiceIndex);
            Answered = true;
            LastPick = choiceIndex;
            results.Add(correct);
            if (correct) CorrectCount++;
            return correct ? GiftAnswer.Correct : GiftAnswer.Wrong;
        }

        // 답한 뒤 다음 문제로 (마지막이면 IsComplete)
        public void Next()
        {
            if (!Answered) return;
            Answered = false;
            LastPick = -1;
            Index++;
        }
    }
}
