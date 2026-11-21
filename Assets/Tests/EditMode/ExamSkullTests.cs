using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 힐끔해골 (#281 — 사용자 친구 의견: 확인문제는 보스가 아니라 보스를 이긴 뒤 옆에서 힐끔거리는 해골이 보너스로)
    public class ExamSkullTests
    {
        [TearDown]
        public void TearDown() => WordCourses.Active = null;

        private static List<ExamQuestion> Exams(int count = 5) =>
            Enumerable.Range(1, count).Select(i => new ExamQuestion($"t_02_{i:00}", 2, "제2장 심혈관 및 조혈계", i, $"문제 {i}?", $"answer{i}")).ToList();

        private static WordCourse Course(string areaId, List<ExamQuestion> exams)
        {
            var book = ScriptableObject.CreateInstance<ExamBook>();
            book.ReplaceQuestions(exams);
            var course = ScriptableObject.CreateInstance<WordCourse>();
            course.ConfigureExams(book, new[] { new CourseExamArea(areaId, 2) });
            return course;
        }

        //  y=4  ##########
        //  y=3  #...B....#   보스 (4,3)
        //  y=2  #........#
        //  y=1  #P.......#
        private const string Map = "##########\n#...B....#\n#........#\n#P.......#\n##########";

        private static FieldArea Area(string map = Map)
        {
            var species = TestData.Species("boss_t", new MonsterStats(10, 1, 1), new MonsterStats(0, 0, 0),
                TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1));
            return ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "library").Set("map", map)
                .Set("boss", new BossEncounter(species, 5));
        }

        [Test]
        public void SkullComesOnlyForTheMedicalCourseAfterTheBoss()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            WordCourses.Active = Course("library", Exams());
            Assert.IsFalse(ExamSkull.IsHere(area, session), "보스를 이기기 전엔 없음");
            session.World.MarkBossDefeated(area.BossId);
            Assert.IsTrue(ExamSkull.IsHere(area, session));
            WordCourses.Active = null;
            Assert.IsFalse(ExamSkull.IsHere(area, session), "수능·비즈니스(확인문제 없음)는 없음");
            WordCourses.Active = Course("forest", Exams());
            Assert.IsFalse(ExamSkull.IsHere(area, session), "이 지역 장이 없으면 없음");
        }

        [Test]
        public void SkullStandsBesideTheGemNotNextToIt()
        {
            var area = Area();
            Assert.AreEqual(new Vector2Int(2, 3), ExamSkull.Spot(area), "보스와 같은 줄, 두 칸 떨어진 곳 (보석 옆 [확인] 자리는 비워 둠)");
            Assert.AreEqual(new Vector2Int(6, 3), ExamSkull.Spot(area, null, new Vector2Int(2, 3)), "주인공이 서 있으면 다음 자리");
        }

        [Test]
        public void SkullNeverCutsOffAPath()
        {
            //  y=2  ######B###   보스 (6,2)
            //  y=1  #P.:..:..#   한 칸 통로 — 막으면 길이 끊기는 칸에는 서지 않음 (막다른 끝 (8,1)만 됨)
            const string corridor = "##########\n######B###\n#P.:..:..#\n##########";
            var area = Area(corridor);
            var spot = ExamSkull.Spot(area);
            Assert.AreEqual(new Vector2Int(8, 1), spot, "길을 끊지 않는 막다른 끝");
            {
                var reach = FieldReach.Flood(area.Map);
                reach.Remove(spot.Value);
                var seen = new HashSet<Vector2Int> { area.Map.Start };
                var queue = new Queue<Vector2Int>(seen);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        if (reach.Contains(c + d) && seen.Add(c + d)) queue.Enqueue(c + d);
                }
                Assert.AreEqual(reach.Count, seen.Count, "해골이 막아도 다 갈 수 있음");
            }
        }

        [Test]
        public void QuestionsYouHaveNotSolvedComeFirst()
        {
            var exams = Exams(5);
            var world = new WorldState();
            foreach (var q in exams.Take(3)) world.MarkExamSolved(q.Id);
            var picked = ExamSkull.Pick(exams, world, new System.Random(1));
            Assert.AreEqual(3, picked.Count);
            CollectionAssert.IsSubsetOf(new[] { "t_02_04", "t_02_05" }, picked.Select(q => q.Id).ToArray(), "못 맞힌 두 문제가 먼저");
            Assert.AreEqual(2, picked.Take(2).Count(q => !world.IsExamSolved(q.Id)));
        }

        [Test]
        public void GoldOnlyForAFirstTryAnswerToANewQuestion()
        {
            var exams = Exams(5);
            var world = new WorldState();
            var quiz = new ExamSkullQuiz(exams, world, new System.Random(2));
            Assert.AreEqual(3, quiz.Count);

            // 1번: 틀리고 다시 → 맞아도 골드 없음, 맞힌 문제로 안 적힘
            var first = quiz.Current;
            int wrong = (first.CorrectIndex + 1) % first.Choices.Count;
            Assert.AreEqual(TabletAnswer.Wrong, quiz.Answer(wrong).result);
            var (result, gold) = quiz.Answer(first.CorrectIndex);
            Assert.AreEqual(TabletAnswer.Correct, result);
            Assert.AreEqual(0, gold, "틀린 뒤에 맞히면 골드 없음 (하나씩 눌러 보기로 받지 않게)");
            Assert.IsFalse(world.IsExamSolved(first.Exam.Id));
            Assert.AreEqual(TabletAnswer.Ignored, quiz.Answer(first.CorrectIndex).result, "다음으로 넘기기 전엔 무시");
            quiz.Next();

            // 2·3번: 바로 맞힘 → 골드
            for (int i = 0; i < 2; i++)
            {
                var q = quiz.Current;
                var answer = quiz.Answer(q.CorrectIndex);
                Assert.AreEqual(ExamSkull.GoldPerQuestion, answer.gold);
                Assert.IsTrue(world.IsExamSolved(q.Exam.Id), "바로 맞힌 문제는 세이브에");
                quiz.Next();
            }
            Assert.IsTrue(quiz.IsComplete);
            Assert.AreEqual(2, quiz.FirstTryCorrect);
            Assert.AreEqual(2 * ExamSkull.GoldPerQuestion, quiz.GoldEarned);

            // 이미 맞힌 문제는 다시 바로 맞혀도 골드 없음
            var again = new ExamSkullQuiz(exams.Where(q => world.IsExamSolved(q.Id)).ToList(), world, new System.Random(3));
            Assert.AreEqual(0, again.Answer(again.Current.CorrectIndex).gold);
        }

        [Test]
        public void SolvedExamsSurviveASaveRoundTrip()
        {
            var world = new WorldState();
            world.MarkExamSolved("medexam_02_01");
            var copy = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(world));
            Assert.IsTrue(copy.IsExamSolved("medexam_02_01"));
            var old = JsonUtility.FromJson<WorldState>("{}");
            Assert.AreEqual(0, old.SolvedExamCount, "예전 세이브는 빈 목록");
        }

        // 실제 맵: 보스가 있는 네 지역 모두 보석 옆에 설 자리가 있음
        [Test]
        public void EveryRealBossAreaHasASpotForTheSkull()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            int areas = 0;
            foreach (var area in database.Areas.Where(a => a != null && a.Boss != null))
            {
                var spot = ExamSkull.Spot(area);
                Assert.IsTrue(spot.HasValue, $"{area.AreaId}: 해골 자리");
                var boss = area.Map.FindAll(FieldTile.Boss)[0];
                int distance = Mathf.Abs(spot.Value.x - boss.x) + Mathf.Abs(spot.Value.y - boss.y);
                Assert.That(distance, Is.InRange(ExamSkull.MinDistance, ExamSkull.MaxDistance), $"{area.AreaId}: 보석 가까이");
                Assert.IsTrue(FieldReach.Flood(area.Map).Contains(spot.Value), $"{area.AreaId}: 갈 수 있는 칸");
                areas++;
            }
            Assert.AreEqual(4, areas, "서고 · 숲 · 정글 · 동굴");
        }
    }
}
