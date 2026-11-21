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
    // 힐끔해골 (#281 — 사용자 친구 의견: 확인문제는 보스가 아니라 보스를 이긴 뒤 옆에서 힐끔거리는 해골이 보너스로 ·
    // #283: 책장 사이에서 얼굴 반만 · 말 걸면 '○○ 문제인데 한번 풀어 볼래?' · 많이 맞히면 슬금슬금 도망)
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

        // #283 친구 의견 '책장 사이에서 반 칸만 얼굴': 보석 가까운 벽 칸 뒤에서 옆 바닥 쪽으로
        [Test]
        public void SkullPeeksFromBehindAWallNearTheGem()
        {
            var area = Area();
            var spots = ExamSkull.PeekSpots(area);
            Assert.Greater(spots.Count, 1, "도망치면 옮겨 갈 자리도");
            Assert.AreEqual(new Vector2Int(0, 3), spots[0].Wall, "보석과 같은 줄 왼쪽 벽 (옆으로 내미는 곳 먼저)");
            Assert.AreEqual(new Vector2Int(1, 3), spots[0].Floor);
            Assert.AreEqual(Vector2Int.right, spots[0].Out, "오른쪽으로 얼굴을 내밂");
            var reach = FieldReach.Flood(area.Map);
            var walls = new HashSet<Vector2Int>();
            foreach (var spot in spots)
            {
                Assert.AreEqual(FieldTile.Wall, area.Map.Get(spot.Wall), "벽 칸 안 — 길을 막지 않음");
                Assert.IsTrue(reach.Contains(spot.Floor), "얼굴이 보이는 쪽은 갈 수 있는 바닥");
                Assert.AreNotEqual(area.Map.Start, spot.Floor);
                Assert.IsTrue(walls.Add(spot.Wall), "벽 하나에 한 자리");
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
            Assert.AreEqual("심혈관 및 조혈계", quiz.Topic, "말 걸 때 '○○ 문제인데…' — '제n장 '을 뺀 장 이름");

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
            Assert.IsTrue(quiz.Flees, "바로 맞힌 문제가 2개 이상이면 슬금슬금 도망 (#283)");

            // 이미 맞힌 문제는 다시 바로 맞혀도 골드 없음
            var again = new ExamSkullQuiz(exams.Where(q => world.IsExamSolved(q.Id)).ToList(), world, new System.Random(3));
            Assert.AreEqual(0, again.Answer(again.Current.CorrectIndex).gold);

            // 다 틀린 뒤 맞히면 도망 안 감 (깔보며 그대로)
            var hard = new ExamSkullQuiz(exams, new WorldState(), new System.Random(4));
            while (!hard.IsComplete)
            {
                var q = hard.Current;
                hard.Answer((q.CorrectIndex + 1) % q.Choices.Count);
                hard.Answer(q.CorrectIndex);
                hard.Next();
            }
            Assert.AreEqual(0, hard.FirstTryCorrect);
            Assert.IsFalse(hard.Flees);
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

        // 실제 맵: 보스가 있는 네 지역 모두 보석 가까이 숨을 벽이 둘 이상 (도망치면 옮겨 감)
        [Test]
        public void EveryRealBossAreaHasWallsToHideBehind()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            int areas = 0;
            foreach (var area in database.Areas.Where(a => a != null && a.Boss != null))
            {
                var spots = ExamSkull.PeekSpots(area);
                Assert.GreaterOrEqual(spots.Count, 2, $"{area.AreaId}: 숨을 벽");
                var boss = area.Map.FindAll(FieldTile.Boss)[0];
                var reach = FieldReach.Flood(area.Map);
                foreach (var spot in spots)
                {
                    int distance = Mathf.Abs(spot.Wall.x - boss.x) + Mathf.Abs(spot.Wall.y - boss.y);
                    Assert.That(distance, Is.InRange(ExamSkull.MinDistance, ExamSkull.MaxDistance), $"{area.AreaId}: 보석 가까이");
                    Assert.IsFalse(area.Map.IsWalkable(spot.Wall), $"{area.AreaId}: 벽 안");
                    Assert.IsTrue(reach.Contains(spot.Floor), $"{area.AreaId}: 얼굴 쪽 바닥은 갈 수 있음");
                }
                areas++;
            }
            Assert.AreEqual(4, areas, "서고 · 숲 · 정글 · 동굴");
        }
    }
}
