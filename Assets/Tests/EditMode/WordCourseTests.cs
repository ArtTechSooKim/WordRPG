using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Save;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 처음에 고르는 외울 단어장 (#273 사용자, GDD 9-12): 지역의 단어만 고른 단어장의 것으로 바뀌고, 세이브에 남고, 예전 세이브는 수능·내신
    public class WordCourseTests
    {
        private static readonly System.DateTime Now = new System.DateTime(2026, 11, 17, 0, 0, 0, System.DateTimeKind.Utc);

        [TearDown]
        public void ResetCourse() => WordCourses.Active = null;

        private static WordDatabase Book(params string[] ids)
        {
            var book = ScriptableObject.CreateInstance<WordDatabase>();
            book.ReplaceWords(ids.Select(id => new WordEntry(id, id, id + " 뜻", "n")));
            return book;
        }

        private static WordCourse Course(string id, params CourseBook[] books)
        {
            var course = ScriptableObject.CreateInstance<WordCourse>();
            course.Configure(id, id, "", 1, new string[0], books);
            return course;
        }

        private static HeroData Hero() => AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
        private static GameDatabase RealDatabase() => AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");

        // 단어장 데이터에서 고른 단어장이 대신하는 단어장들 (다른 테스트가 '지역 단어장' 검사에서 뺄 때 씀)
        public static HashSet<WordDatabase> CourseBooks() =>
            new HashSet<WordDatabase>(AssetDatabase.FindAssets("t:WordCourse", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WordCourse>(AssetDatabase.GUIDToAssetPath(g)))
                .SelectMany(c => c.Books).Select(b => b.Book).Where(b => b != null));

        [Test]
        public void TheChosenCourseReplacesOnlyTheBooksItNames()
        {
            var common = Book("a1", "a2");
            var advanced = Book("b1");
            var biz = Book("biz_1", "biz_2", "biz_3");
            var course = Course("business", new CourseBook(common, biz));

            Assert.AreEqual("a1", common.Words[0].Id, "고르기 전엔 기본 단어");
            WordCourses.Active = course;
            CollectionAssert.AreEqual(new[] { "biz_1", "biz_2", "biz_3" }, common.Words.Select(w => w.Id).ToArray(), "지역의 단어 = 고른 단어장");
            Assert.AreEqual("b1", advanced.Words[0].Id, "대신하지 않는 단어장은 그대로");
            Assert.AreEqual(2, common.OwnWords.Count, "에셋 자체는 그대로");
            Assert.AreEqual(3, biz.Words.Count, "대신 쓰는 단어장 자신은 그대로");
            Assert.AreEqual(4, course.WordCount(new[] { common, advanced }), "고르는 화면의 단어 수");
            Assert.AreEqual("biz_2", course.FindWord("biz_2", new[] { common, advanced }).Id);
        }

        [Test]
        public void ANewGameAsksOnceAndTheChoiceIsSavedAndLoaded()
        {
            var database = RealDatabase();
            var session = GameSession.NewGame(Hero(), 1);
            Assert.IsTrue(session.NeedsCourseChoice, "새 게임은 고르는 화면");
            Assert.IsNull(WordCourses.Active);

            var business = database.FindCourse("business");
            Assert.IsNotNull(business, "GameDatabase에 비즈니스 단어장 (WordRPG > Data > Refresh Game Database)");
            session.ChooseCourse(business);
            Assert.IsFalse(session.NeedsCourseChoice);
            Assert.AreSame(business, WordCourses.Active);

            var data = session.ToSaveData(Now);
            Assert.AreEqual("business", data.Course);
            WordCourses.Active = null;
            var loaded = GameSession.FromSaveData(SaveRoundTrip(data), database, Hero());
            Assert.AreEqual("business", loaded.CourseId);
            Assert.AreSame(business, loaded.Course);
            Assert.AreSame(business, WordCourses.Active, "불러오면 그 단어장으로");
            Assert.IsTrue(database.FindArea("meadow").Words.Words.All(w => w.Id.StartsWith("biz_")), "초원의 단어 = 비즈니스");
            Assert.IsTrue(database.FindArea("cave").Words.Words.All(w => w.Id.StartsWith("biz_")), "동굴의 단어 = 비즈니스");
        }

        [Test]
        public void OldSavesKeepTheBasicWordsWithoutAsking()
        {
            var database = RealDatabase();
            // 1.3까지의 세이브: course 칸이 없음 + 이미 단어를 만남
            var old = GameSession.NewGame(Hero(), 1);
            old.Vocabulary.RecordAnswer("access", true, Now, new MasteryRules());
            var data = old.ToSaveData(Now);
            Assert.AreEqual("", data.Course);

            var loaded = GameSession.FromSaveData(SaveRoundTrip(data), database, Hero());
            Assert.IsFalse(loaded.NeedsCourseChoice, "하던 사람에게는 묻지 않음");
            Assert.AreEqual(WordCourses.DefaultId, loaded.CourseId);
            Assert.AreEqual("access", database.FindArea("meadow").Words.Words.First(w => w.Id == "access").Id, "수능·내신 단어 그대로");
            Assert.AreEqual(0, loaded.LoadWarnings.Count);
        }

        [Test]
        public void ASaveMadeBeforeChoosingAsksAgain()
        {
            var data = GameSession.NewGame(Hero(), 1).ToSaveData(Now); // 고르는 화면에서 앱을 끔
            var loaded = GameSession.FromSaveData(SaveRoundTrip(data), RealDatabase(), Hero());
            Assert.IsTrue(loaded.NeedsCourseChoice);
        }

        [Test]
        public void ANewSessionStartsFromTheBasicWords()
        {
            WordCourses.Active = RealDatabase().FindCourse("medical");
            GameSession.NewGame(Hero(), 1);
            Assert.IsNull(WordCourses.Active, "게임은 한 번에 하나 — 전에 고른 단어장이 남지 않음");
        }

        [Test]
        public void AnUnknownCourseIdIsKeptAndPlaysWithBasicWords()
        {
            var data = GameSession.NewGame(Hero(), 1).ToSaveData(Now).WithCourse("future_course");
            var loaded = GameSession.FromSaveData(SaveRoundTrip(data), RealDatabase(), Hero());
            Assert.AreEqual("future_course", loaded.CourseId, "id는 지킴 (다시 저장해도 사라지지 않게)");
            Assert.IsNull(WordCourses.Active);
            Assert.AreEqual(1, loaded.LoadWarnings.Count);
            Assert.AreEqual("future_course", loaded.ToSaveData(Now).Course);
        }

        // 누군가의 기록은 수능·내신 예문으로 쓴 이야기 — 다른 단어장은 찾은 단어 수만큼 앞에서부터 읽힘
        [Test]
        public void JournalLinesFollowTheNumberOfFoundWordsInAnotherCourse()
        {
            var story = ScriptableObject.CreateInstance<WordDatabase>();
            story.ReplaceWords(new[]
            {
                new WordEntry("s1", "apple", "사과", "n", "An apple.", "사과.", 1),
                new WordEntry("s2", "bread", "빵", "n", "Some bread.", "빵.", 2),
                new WordEntry("s3", "cat", "고양이", "n", "A cat.", "고양이.", 3),
            });
            var biz = Book("biz_1", "biz_2", "biz_3");
            WordCourses.Active = Course("business", new CourseBook(story, biz));
            var vocabulary = new VocabularyProgress();
            vocabulary.RecordAnswer("s3", true, Now, new MasteryRules()); // 수능 단어를 찾아도 다른 단어장에선 상관없음
            Assert.IsFalse(Journals.Lines(story, vocabulary).Any(l => l.Readable));

            vocabulary.RecordAnswer("biz_2", false, Now, new MasteryRules());
            vocabulary.RecordAnswer("biz_3", true, Now, new MasteryRules());
            var lines = Journals.Lines(story, vocabulary);
            CollectionAssert.AreEqual(new[] { "s1", "s2", "s3" }, lines.Select(l => l.Word.Id).ToArray(), "문장은 그대로");
            CollectionAssert.AreEqual(new[] { true, true, false }, lines.Select(l => l.Readable).ToArray(), "찾은 2개만큼 앞에서부터");
        }

        // 실제 데이터: 세 단어장 · 순서 · 기본 단어장 둘을 모두 대신 · 크기가 같아서 보스 문·도감 보상 목표가 그대로 맞음
        [Test]
        public void RealCoursesCoverBothWordBooksWithTheSameSizes()
        {
            var database = RealDatabase();
            CollectionAssert.AreEqual(new[] { "suneung", "business", "medical" }, database.Courses.Select(c => c.CourseId).ToArray(),
                "고르는 화면 순서 (출시 뒤 id는 바꾸지 말 것)");
            var baseBooks = database.BaseWordBooks();
            Assert.AreEqual(2, baseBooks.Count, "초원·서고 단어장 + 숲·정글·동굴 단어장");
            Assert.AreEqual(0, database.FindCourse("suneung").Books.Count, "수능·내신 = 기본 단어장 그대로");

            foreach (var course in database.Courses)
            {
                Assert.IsFalse(string.IsNullOrEmpty(course.DisplayName), course.CourseId);
                Assert.IsFalse(string.IsNullOrEmpty(course.Description), course.CourseId);
                Assert.AreEqual(3, course.SampleWordIds.Count, $"{course.CourseId}: 예시 단어 3개");
                foreach (var sample in course.SampleWordIds)
                    Assert.IsNotNull(course.FindWord(sample, baseBooks), $"{course.CourseId}: 예시 단어 '{sample}'가 그 단어장에 없음");
                if (course.CourseId == WordCourses.DefaultId) continue;

                string prefix = course.CourseId == "business" ? "biz_" : "med_";
                foreach (var baseBook in baseBooks)
                {
                    var book = course.BookFor(baseBook);
                    Assert.IsNotNull(book, $"{course.CourseId}: {baseBook.name}을 대신할 단어장이 없음");
                    Assert.AreEqual(baseBook.OwnWords.Count, book.OwnWords.Count, $"{course.CourseId}: {book.name} 단어 수가 기본 단어장과 다름");
                    Assert.AreEqual(baseBook.Tier, book.Tier, $"{book.name}: 난이도 단계");
                    foreach (var word in book.OwnWords)
                    {
                        StringAssert.StartsWith(prefix, word.Id, $"{book.name}: 단어 id는 단어장 앞글자로 (세이브 기록이 섞이지 않게)");
                        Assert.IsFalse(string.IsNullOrWhiteSpace(word.Meaning), $"{word.Id} 뜻이 비어 있음");
                        Assert.IsTrue(word.PartOfSpeech == "n" || word.PartOfSpeech == "v" || word.PartOfSpeech == "adj", $"{word.Id} 품사");
                    }
                }
                var all = baseBooks.SelectMany(b => course.BookFor(b).OwnWords).ToList();
                Assert.AreEqual(all.Count, all.Select(w => w.Id).Distinct().Count(), $"{course.CourseId}: 같은 단어가 두 번");
            }
        }

        // 고른 단어장으로 해도 지역마다 정한 단어 수(보스 문·유적 제단)를 채울 수 있음
        [Test]
        public void EveryWordGoalFitsEveryCourse()
        {
            var database = RealDatabase();
            foreach (var course in database.Courses)
            {
                WordCourses.Active = course;
                foreach (var area in database.Areas)
                {
                    if (area == null || area.Words == null) continue;
                    int words = area.Words.Words.Count;
                    Assert.LessOrEqual(area.BossGateWords, words, $"{course.CourseId}·{area.AreaId}: 보스 문 단어 수");
                    Assert.LessOrEqual(area.ShrineWords, words, $"{course.CourseId}·{area.AreaId}: 유적 제단 단어 수");
                }
            }
        }

        private static SaveData SaveRoundTrip(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
    }
}
