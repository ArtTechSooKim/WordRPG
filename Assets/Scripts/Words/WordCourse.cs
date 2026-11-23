using System;
using System.Collections.Generic;
using UnityEngine;

namespace WordRPG.Words
{
    // 단어장 하나가 기본 단어장(지역에 붙은 수능·내신 단어장) 하나를 대신한다 (#273)
    [Serializable]
    public class CourseBook
    {
        [Tooltip("지역에 붙어 있는 기본 단어장 (tier1_common·tier2_advanced)")]
        [SerializeField] private WordDatabase replaces;
        [Tooltip("이 단어장을 고르면 그 대신 나오는 단어 (Assets/Data/Words/Courses/*.csv)")]
        [SerializeField] private WordDatabase book;

        public WordDatabase Replaces => replaces;
        public WordDatabase Book => book;

        public CourseBook(WordDatabase replaces, WordDatabase book)
        {
            this.replaces = replaces;
            this.book = book;
        }
    }

    // 확인문제를 내는 지역 (#280): 그 지역의 보스전·석판·반딧돌이 이 장들의 문제를 낸다
    [Serializable]
    public class CourseExamArea
    {
        [Tooltip("FieldArea의 areaId (library · forest · jungle · cave)")]
        [SerializeField] private string areaId;
        [Tooltip("ExamBook 문제의 장 번호")]
        [SerializeField] private List<int> chapters = new List<int>();

        public string AreaId => areaId;
        public IReadOnlyList<int> Chapters => chapters;

        public CourseExamArea(string areaId, params int[] chapters)
        {
            this.areaId = areaId;
            this.chapters = new List<int>(chapters);
        }
    }

    // 처음에 고르는 외울 단어장 (#273 사용자, GDD 9-12): 수능·내신 / 비즈니스 / 의학 용어.
    // 지역·도감 보상·누군가의 기록은 그대로, 지역의 단어만 이 단어장의 것으로 바뀐다 (WordDatabase.Words).
    // books가 비어 있으면 기본 단어장 그대로 (= 수능·내신)
    [CreateAssetMenu(fileName = "NewWordCourse", menuName = "WordRPG/Word Course", order = 1)]
    public class WordCourse : ScriptableObject
    {
        [Tooltip("세이브에 적히는 id (suneung · business · medical). 출시 뒤 바꾸지 말 것")]
        [SerializeField] private string courseId;
        [SerializeField] private string displayName;
        [SerializeField] private string description;
        [Tooltip("고르는 화면의 순서 (작은 수가 위)")]
        [SerializeField] private int order;
        [Tooltip("고르는 화면에 보여 줄 예시 단어 id 3개 (이 단어장의 단어)")]
        [SerializeField] private List<string> sampleWordIds = new List<string>();
        [SerializeField] private List<CourseBook> books = new List<CourseBook>();
        [Tooltip("확인문제 (#280 의학 용어 = 의대생이 장마다 만든 문제). 비면 없음")]
        [SerializeField] private ExamBook exams;
        [SerializeField] private List<CourseExamArea> examAreas = new List<CourseExamArea>();
        [Tooltip("단어 문제 시간에 더하는 초 — 전투 문제·선제공격 (#289 사용자: 의학 용어는 어려워서 +5초)")]
        [SerializeField] private float extraAnswerSeconds;

        public string CourseId => courseId;
        public string DisplayName => displayName;
        public string Description => description;
        public int Order => order;
        public IReadOnlyList<string> SampleWordIds => sampleWordIds;
        public IReadOnlyList<CourseBook> Books => books;
        public ExamBook Exams => exams;
        public IReadOnlyList<CourseExamArea> ExamAreas => examAreas;
        public float ExtraAnswerSeconds => extraAnswerSeconds;

        // 그 지역의 확인문제 (없으면 빈 목록)
        public List<ExamQuestion> ExamFor(string areaId)
        {
            if (exams == null || string.IsNullOrEmpty(areaId)) return new List<ExamQuestion>();
            foreach (var area in examAreas)
                if (area != null && area.AreaId == areaId) return exams.InChapters(area.Chapters);
            return new List<ExamQuestion>();
        }

        // 기본 단어장 대신 쓰는 단어장 (없으면 null = 기본 그대로)
        public WordDatabase BookFor(WordDatabase baseBook)
        {
            if (baseBook == null) return null;
            foreach (var entry in books)
                if (entry != null && entry.Replaces == baseBook && entry.Book != null) return entry.Book;
            return null;
        }

        // 이 단어장의 모든 단어 수 (고르는 화면의 '단어 N개'). 기본 단어장이면 baseBooks로 셈
        public int WordCount(IEnumerable<WordDatabase> baseBooks)
        {
            int count = 0;
            foreach (var baseBook in baseBooks)
            {
                if (baseBook == null) continue;
                var book = BookFor(baseBook);
                count += book != null ? book.OwnWords.Count : baseBook.OwnWords.Count;
            }
            return count;
        }

        public WordEntry FindWord(string wordId, IEnumerable<WordDatabase> baseBooks)
        {
            foreach (var baseBook in baseBooks)
            {
                if (baseBook == null) continue;
                var book = BookFor(baseBook) ?? baseBook;
                foreach (var word in book.OwnWords) if (word.Id == wordId) return word;
            }
            return null;
        }

        // 샘플 데이터 생성 전용 (#280)
        public void ConfigureExams(ExamBook book, IEnumerable<CourseExamArea> areas)
        {
            exams = book;
            examAreas = areas != null ? new List<CourseExamArea>(areas) : new List<CourseExamArea>();
        }

        // 샘플 데이터 생성 전용 (#289)
        public void ConfigureAnswerTime(float extraSeconds) => extraAnswerSeconds = extraSeconds;

        // 샘플 데이터 생성 전용. 런타임에서는 호출하지 않는다
        public void Configure(string id, string name, string summary, int sortOrder, IEnumerable<string> samples, IEnumerable<CourseBook> courseBooks)
        {
            courseId = id;
            displayName = name;
            description = summary;
            order = sortOrder;
            sampleWordIds = new List<string>(samples);
            books = new List<CourseBook>(courseBooks);
        }
    }

    // 지금 하는 게임의 단어장. 세션이 만들어지거나 불러와질 때·단어장을 고를 때 GameSession이 정한다 (한 번에 세션 하나).
    // null = 기본 단어장(수능·내신). 테스트에서 바꾸면 TearDown에서 null로 되돌릴 것
    public static class WordCourses
    {
        public const string DefaultId = "suneung";

        public static WordCourse Active { get; set; }

        // 지금 단어장으로 그 지역에서 낼 확인문제 (#280 — 수능·비즈니스는 없음)
        public static List<ExamQuestion> ExamFor(string areaId) => Active != null ? Active.ExamFor(areaId) : new List<ExamQuestion>();

        // 지금 단어장이 단어 문제 시간에 더하는 초 (#289 — 의학 용어 +5). BattleConfig.AnswerTimeLimitSeconds·Preemptive.AnswerSeconds가 더해 씀
        public static float ExtraAnswerSeconds => Active != null ? Math.Max(0f, Active.ExtraAnswerSeconds) : 0f;
    }
}
