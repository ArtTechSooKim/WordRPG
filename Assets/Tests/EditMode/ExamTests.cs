using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 의학 용어 = 의대생 자료 (#280 사용자: "그대로 넣으면 됨" · 장별 확인문제를 보스전·석판·반딧돌에)
    public class ExamTests
    {
        [TearDown]
        public void TearDown() => WordCourses.Active = null;

        private static ExamQuestion Q(string question, string answer, int chapter = 2, int number = 1, string id = null) =>
            new ExamQuestion(id ?? $"q{chapter}_{number}", chapter, $"제{chapter}장", number, question, answer);

        // --- 문제 글 읽기 (원본 모양 그대로 받아서 화면용으로만 나눔) ---

        [Test]
        public void MultipleChoiceInParenthesesBecomesFiveOptions()
        {
            var q = Q("심실사이의 사이막(중격)에 작은 구멍이 있는 선천성 심장질환은? (① TOF ② VSD ③ ASD ④ PDA ⑤ PFO)", "② VSD");
            Assert.IsTrue(q.IsMultipleChoice);
            CollectionAssert.AreEqual(new[] { "TOF", "VSD", "ASD", "PDA", "PFO" }, q.Options.ToArray());
            Assert.AreEqual(1, q.AnswerIndex);
            Assert.AreEqual("심실사이의 사이막(중격)에 작은 구멍이 있는 선천성 심장질환은?", q.Stem, "문제 안 괄호는 그대로");
        }

        [Test]
        public void InlineOptionsAndLeadingNumberAreReadToo()
        {
            var q = Q("18. 자궁 … 절제하는 수술은? ① LEEP ② TAH ③ TVH ④ panhysterectomy ⑤ RH", "⑤ RH (radical hysterectomy)");
            CollectionAssert.AreEqual(new[] { "LEEP", "TAH", "TVH", "panhysterectomy", "RH" }, q.Options.ToArray());
            Assert.AreEqual(4, q.AnswerIndex);
            Assert.AreEqual("자궁 … 절제하는 수술은?", q.Stem, "앞 번호(원본과 어긋남)는 화면에서 뺌");
        }

        [Test]
        public void CombinationOptionsKeepTheListInTheQuestion()
        {
            var q = Q("심장질환의 진단을 위한 검사로 묶인 것은? (가. heart scan 나. echocardiography 다. cardiocentesis 라. encephalography) (① 가,나,다 ② 가,다 ③ 나,라 ④ 라 ⑤ 가,나,다,라)", "① 가,나,다");
            CollectionAssert.AreEqual(new[] { "가,나,다", "가,다", "나,라", "라", "가,나,다,라" }, q.Options.ToArray());
            Assert.AreEqual(0, q.AnswerIndex);
            StringAssert.Contains("가. heart scan", q.Stem, "가나다라 목록은 문제에 남음");
        }

        [Test]
        public void ChoiceListsBecomeThePoolOfWrongChoices()
        {
            var bracket = Q("5. 음경이 생리적으로 팽대 강직하게 되는 것은?  [보기] placenta previa / erection / phimosis / BPH", "erection");
            Assert.IsFalse(bracket.IsMultipleChoice);
            CollectionAssert.AreEqual(new[] { "placenta previa", "erection", "phimosis", "BPH" }, bracket.Pool.ToArray());
            Assert.AreEqual("음경이 생리적으로 팽대 강직하게 되는 것은?", bracket.Stem);

            var paren = Q("(보기: incoherence, amnesia, colpitis(=vaginitis), delusion) 기억을 생각해내지 못하는 것은?", "amnesia (기억상실)");
            CollectionAssert.AreEqual(new[] { "incoherence", "amnesia", "colpitis(=vaginitis)", "delusion" }, paren.Pool.ToArray(), "안쪽 괄호까지 짝을 맞춤");
            Assert.AreEqual("기억을 생각해내지 못하는 것은?", paren.Stem);
        }

        [Test]
        public void SameAnswerIgnoresSpacesCaseAndNotes()
        {
            Assert.AreEqual(ExamQuestion.Normalize("colpitis(=vaginitis)"), ExamQuestion.Normalize("colpitis (= vaginitis)"));
            Assert.AreEqual(ExamQuestion.Normalize("amnesia"), ExamQuestion.Normalize("Amnesia (기억상실)"));
            Assert.AreNotEqual(ExamQuestion.Normalize("amnesia"), ExamQuestion.Normalize("dementia"));
        }

        // --- 문제 만들기 ---

        [Test]
        public void ShortAnswerGetsThreeOtherAnswersFromTheSameChapterFirst()
        {
            var pool = new List<ExamQuestion>
            {
                Q("a?", "sinoatrial node", 2, 1), Q("b?", "cardiac arrest", 2, 2), Q("c?", "endocarditis", 2, 3),
                Q("d?", "valvuloplasty", 2, 4), Q("e?", "trachea", 3, 1), Q("f? (① x ② y)", "① x", 2, 5),
            };
            for (int seed = 0; seed < 30; seed++)
            {
                var question = ExamQuiz.Create(pool[0], pool, new System.Random(seed));
                Assert.AreEqual(4, question.Choices.Count);
                Assert.AreEqual("sinoatrial node", question.CorrectAnswer);
                Assert.AreEqual(4, question.Choices.Distinct().Count(), "같은 보기 없음");
                Assert.IsFalse(question.Choices.Contains("trachea"), "같은 장 정답이 셋 있으면 다른 장은 안 씀");
                Assert.IsFalse(question.Choices.Contains("① x"), "5지선다 정답은 오답 재료로 안 씀");
                Assert.IsTrue(question.IsExam);
                Assert.AreEqual("a?", question.Prompt);
                Assert.AreEqual(QuizDirection.MeaningToEnglish, question.Direction);
                Assert.IsFalse(question.IsNewWord);
            }
        }

        [Test]
        public void MultipleChoiceKeepsTheOriginalOrderWithCircledNumbers()
        {
            var q = Q("호흡기 소리가 아닌 것은? (① pleural rub ② rales ③ murmur ④ wheeze ⑤ stridor)", "③ murmur");
            var question = ExamQuiz.Create(q, new[] { q }, new System.Random(1));
            CollectionAssert.AreEqual(new[] { "① pleural rub", "② rales", "③ murmur", "④ wheeze", "⑤ stridor" }, question.Choices.ToArray());
            Assert.AreEqual(2, question.CorrectIndex);
        }

        [Test]
        public void LongQuestionsGetMoreTimeToRead()
        {
            float shortTime = ExamQuiz.ReadingSeconds("짧은 문제?", new[] { "a", "b", "c", "d" });
            float longTime = ExamQuiz.ReadingSeconds(new string('가', 120), new[] { "aaaaaaaaaa", "bbbbbbbbbb", "cccccccccc", "dddddddddd" });
            Assert.AreEqual(ExamQuiz.MinReadingSeconds, shortTime);
            Assert.Greater(longTime, shortTime);
            Assert.LessOrEqual(ExamQuiz.ReadingSeconds(new string('가', 1000), null), ExamQuiz.MaxReadingSeconds);
        }

        [Test]
        public void ProviderAsksEveryQuestionOnceBeforeRepeatingAndRecordsNoMastery()
        {
            var pool = Enumerable.Range(1, 5).Select(i => Q($"q{i}?", $"answer{i}", 2, i)).ToList();
            var provider = new ExamQuizProvider(pool, new System.Random(3));
            var asked = new HashSet<string>();
            for (int i = 0; i < 5; i++) Assert.IsTrue(asked.Add(provider.NextQuestion(QuizDirection.EnglishToMeaning).Exam.Id), "다 내기 전엔 겹치지 않음");
            Assert.AreEqual(5, asked.Count);
            var next = provider.NextQuestion(QuizDirection.EnglishToMeaning);
            var change = provider.SubmitAnswer(next, true);
            Assert.IsFalse(change.LeveledUp || change.LeveledDown, "숙련도에 안 적힘");
        }

        [Test]
        public void CourseGivesEachAreaItsChapters()
        {
            var book = ScriptableObject.CreateInstance<ExamBook>();
            book.ReplaceQuestions(new[] { Q("a?", "x", 2, 1), Q("b?", "y", 3, 1), Q("c?", "z", 5, 1) });
            var course = ScriptableObject.CreateInstance<WordCourse>();
            course.ConfigureExams(book, new[] { new CourseExamArea("library", 2, 3), new CourseExamArea("forest", 5) });
            CollectionAssert.AreEqual(new[] { "x", "y" }, course.ExamFor("library").Select(q => q.Answer).ToArray());
            CollectionAssert.AreEqual(new[] { "z" }, course.ExamFor("forest").Select(q => q.Answer).ToArray());
            Assert.IsEmpty(course.ExamFor("meadow"), "보스가 없는 초원은 없음");
            Assert.IsEmpty(WordCourses.ExamFor("library"), "단어장을 안 골랐으면(수능·내신) 없음");
            WordCourses.Active = course;
            Assert.AreEqual(2, WordCourses.ExamFor("library").Count);
        }

        // --- 실제 데이터 ---

        private static GameDatabase RealDatabase() => AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");

        [Test]
        public void EveryRealExamQuestionMakesAGoodQuiz()
        {
            var book = AssetDatabase.LoadAssetAtPath<ExamBook>("Assets/Data/Exams/medical_exam.asset");
            Assert.IsNotNull(book, "Assets/Data/Exams/medical_exam.csv → .asset (python Tools/medical_course.py)");
            Assert.AreEqual(221, book.Questions.Count, "의대생 확인문제 221개");
            Assert.AreEqual(book.Questions.Count, book.Questions.Select(q => q.Id).Distinct().Count());
            int multiple = 0;
            foreach (var q in book.Questions)
            {
                Assert.IsNotEmpty(q.Stem, q.Id);
                Assert.IsNotEmpty(q.ChapterTitle, q.Id);
                var question = ExamQuiz.Create(q, book.Questions, new System.Random(q.Id.GetHashCode()));
                if (q.IsMultipleChoice)
                {
                    multiple++;
                    Assert.GreaterOrEqual(q.Options.Count, 4, $"{q.Id}: 보기 수");
                    StringAssert.StartsWith(ExamQuestion.Circled(q.AnswerIndex), q.Answer, $"{q.Id}: 정답 번호");
                    StringAssert.Contains(q.Options[q.AnswerIndex].Split(' ')[0], q.Answer, $"{q.Id}: 정답 보기 글이 정답에 있음");
                }
                else
                {
                    Assert.AreEqual(ExamQuiz.Choices, question.Choices.Count, $"{q.Id}: 보기 4개");
                    Assert.AreEqual(q.Answer, question.CorrectAnswer, q.Id);
                }
                Assert.IsFalse(q.Stem.Contains("①"), $"{q.Id}: 보기가 문제 글에 남음");
                Assert.IsFalse(q.Stem.Contains("[보기]") || q.Stem.Contains("(보기:"), $"{q.Id}: 보기 목록이 문제 글에 남음");
                Assert.LessOrEqual(question.ReadingSeconds, ExamQuiz.MaxReadingSeconds);
            }
            Assert.AreEqual(35, multiple, "5지선다 35개");
        }

        [Test]
        public void MedicalCourseGivesEveryBossAreaThreeChaptersOfQuestions()
        {
            var database = RealDatabase();
            var medical = database.FindCourse("medical");
            Assert.IsNotNull(medical.Exams, "의학 용어 확인문제 (WordRPG > Data > Build Word Courses)");
            var chapters = medical.ExamAreas.SelectMany(a => a.Chapters).ToList();
            CollectionAssert.AreEquivalent(Enumerable.Range(2, 12).ToArray(), chapters, "제2~13장을 한 번씩");
            foreach (var area in database.Areas.Where(a => a != null && a.Boss != null))
            {
                var exams = medical.ExamFor(area.AreaId);
                Assert.GreaterOrEqual(exams.Count, 40, $"{area.AreaId}: 보스전 확인문제 (세 장)");
            }
            var jungle = database.Areas.First(a => a.AreaId == "jungle");
            Assert.GreaterOrEqual(medical.ExamFor("jungle").Count, Ruins.Tablets(jungle).Count * TabletQuiz.QuestionsPerTablet, "석판마다 3문제");
            var cave = database.Areas.First(a => a.AreaId == "cave");
            Assert.GreaterOrEqual(medical.ExamFor("cave").Count, GlowStones.Stones(cave).Count, "반딧돌마다 1문제");
            Assert.IsEmpty(database.FindCourse("business").ExamFor("library"), "비즈니스는 확인문제 없음");
        }

        // 단어장 = 원본 장 순서 그대로, 같은 용어가 또 나오면 처음 것만 — 앞 450개 (초원·서고 150 + 숲~동굴 300)
        [Test]
        public void MedicalWordsAreTheStudentListInOrder()
        {
            var medical = RealDatabase().FindCourse("medical");
            var books = medical.Books.Select(b => b.Book).ToList();
            Assert.AreEqual(150, books[0].OwnWords.Count);
            Assert.AreEqual(300, books[1].OwnWords.Count);
            var first = books[0].OwnWords[0];
            Assert.AreEqual("osteitis", first.English);
            Assert.AreEqual("골염", first.Meaning);
            Assert.AreEqual("oste(bone·뼈) + itis(inflammation·염증)", first.Note, "설명도 그대로");
            var english = books.SelectMany(b => b.OwnWords).Select(w => w.English.ToLowerInvariant()).ToList();
            Assert.AreEqual(english.Count, english.Distinct().Count(), "같은 용어 두 번 없음");

            string source = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Docs", "의학용어_원본.html"));
            int last = -1;
            foreach (var word in books.SelectMany(b => b.OwnWords).Take(60))
            {
                int at = source.IndexOf($"<b>{System.Net.WebUtility.HtmlEncode(word.English)}</b> — {word.Meaning}", StringComparison.Ordinal);
                Assert.Greater(at, last, $"{word.English}: 원본에 그 글자 그대로, 순서대로");
                last = at;
            }
        }
    }
}
