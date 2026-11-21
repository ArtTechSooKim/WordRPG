using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;
using Object = UnityEngine.Object;

namespace WordRPG.Tests
{
    // 힐끔해골 (#281 — 사용자 친구 의견, Figma '힐끔해골 확인문제 (#281)'): 의학 용어 + 보스를 이긴 뒤 보석 옆에서 힐끔거리다
    // [확인]하면 그 지역 장의 확인문제 3개 — 틀려도 다시, 처음 바로 맞힌 새 문제마다 골드
    public class ExamScreenTests
    {
        private static List<ExamQuestion> Exams() => new List<ExamQuestion>
        {
            new ExamQuestion("t_02_02", 2, "제2장 심혈관 및 조혈계", 2, "심장박동이 정지해서 혈액을 방출할 수 없게 된 상태?", "cardiac arrest"),
            new ExamQuestion("t_02_15", 2, "제2장 심혈관 및 조혈계", 15, "심실사이의 사이막(중격)에 작은 구멍이 있는 선천성 심장질환은? (① TOF ② VSD ③ ASD ④ PDA ⑤ PFO)", "② VSD"),
            new ExamQuestion("t_02_01", 2, "제2장 심혈관 및 조혈계", 1, "정상심장에서 특수전도계의 자극전도 시작부위는?", "sinoatrial node"),
            new ExamQuestion("t_02_03", 2, "제2장 심혈관 및 조혈계", 3, "좌심방과 우심방 사이 중격의 결손은?", "atrial septal defect"),
            new ExamQuestion("t_02_06", 2, "제2장 심혈관 및 조혈계", 6, "심장의 내면을 싸고 있는 심내막의 염증?", "endocarditis"),
        };

        private static WordCourse Course()
        {
            var book = ScriptableObject.CreateInstance<ExamBook>();
            book.ReplaceQuestions(Exams());
            var course = ScriptableObject.CreateInstance<WordCourse>().Set("courseId", "medical"); // 단어장을 고른 세션 (고르는 화면이 다시 뜨지 않게)
            course.ConfigureExams(book, new[] { new CourseExamArea("library", 2) });
            return course;
        }

        [TearDown]
        public void TearDown() => WordCourses.Active = null;

        //  y=4  ##########
        //  y=3  #...B....#   이긴 보스 (4,3) → 해골은 (2,3)
        //  y=2  #........#
        //  y=1  #P.......#
        private const string Map = "##########\n#...B....#\n#........#\n#P.......#\n##########";

        private static (FieldArea area, GameSession session) Setup(bool bossDefeated, bool medical = true)
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "library").Set("regionName", "서고");
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var boss = TestData.Species("boss_exam", new MonsterStats(50, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "까먹대왕");
            var area = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "library").Set("displayName", "잊혀진 서고").Set("map", Map)
                .Set("theme", FieldTheme.Library).Set("words", words).Set("encounters", ScriptableObject.CreateInstance<EncounterTable>())
                .Set("boss", new BossEncounter(boss, 5));
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)), 5);
            session.Record.RecordBattle(true, 1, 0);
            if (bossDefeated) session.World.MarkBossDefeated(area.BossId);
            session.ChooseCourse(medical ? Course() : null); // 새 게임에서 고른 단어장 (테스트용 의학 단어장)
            return (area, session);
        }

        private static FieldScreen Field(FieldArea area, GameSession session)
        {
            var field = new GameObject("SkullFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: false);
            return field;
        }

        [UnityTest]
        public IEnumerator SkullBesideTheGemAsksThreeQuestions()
        {
            var (area, session) = Setup(bossDefeated: true);
            var field = Field(area, session);
            yield return null;
            Assert.IsTrue(field.SkullVisible, "의학 용어 + 이긴 보스 → 보석 옆에 해골");
            Assert.AreEqual(new Vector2Int(2, 3), field.SkullCell);

            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3);
            Assert.AreEqual(new Vector2Int(1, 3), field.PlayerCell);
            yield return HoldStick(field, Direction.Right, () => false, 0.4f);
            Assert.AreEqual(new Vector2Int(1, 3), field.PlayerCell, "해골은 지나갈 수 없음");
            Assert.That(field.SkullFacing, Is.EqualTo(Direction.Left).Or.EqualTo(Direction.Right), "주인공 쪽을 힐끔 보거나 등을 돌림");

            int gold = session.Inventory.Gold;
            yield return PressConfirm(field);
            var view = field.SkullView;
            Assert.IsTrue(view.IsOpen, "[확인] = 확인문제 창");
            Assert.IsTrue(field.IsPanelOpen);
            StringAssert.Contains("들켰다", view.Line);
            var quiz = field.SkullQuiz;
            Assert.AreEqual(3, quiz.Count);

            for (int i = 0; i < 3; i++)
            {
                var question = quiz.Current;
                Assert.AreEqual(question.Prompt, view.Question);
                Assert.AreEqual(question.Choices.Count, view.ChoiceCount, "보기 한 줄씩 (5지선다는 5개)");
                StringAssert.Contains("확인문제 · 제2장", view.Tag);
                if (i == 0)
                {
                    int wrong = (question.CorrectIndex + 1) % question.Choices.Count;
                    ActiveButton(field.transform, $"SkullChoice_{wrong}").onClick.Invoke();
                    yield return null;
                    Assert.IsFalse(view.ChoiceEnabled(wrong), "틀린 보기만 꺼짐");
                    StringAssert.Contains("아니야", view.Line);
                }
                ActiveButton(field.transform, $"SkullChoice_{question.CorrectIndex}").onClick.Invoke();
                // 맞힌 모습은 누른 그 순간에 (테스트는 연출 0.01배라 다음 문제가 곧바로 와서, 한 프레임 기다리면 지나감)
                Assert.AreEqual(question.CorrectAnswer, view.Answer, "정답이 금빛으로");
                Assert.AreEqual(i == 0 ? "정답!" : $"+{ExamSkull.GoldPerQuestion}G", view.Stamp, "골드는 처음 바로 맞힌 문제만");
                yield return WaitFor(() => view.IsComplete || quiz.Index > i, 3f);
            }
            Assert.IsTrue(view.IsComplete);
            Assert.AreEqual(gold + 2 * ExamSkull.GoldPerQuestion, session.Inventory.Gold);
            Assert.AreEqual(2, session.World.SolvedExamCount, "바로 맞힌 두 문제는 세이브");
            ActiveButton(field.transform, "SkullDone").onClick.Invoke();
            yield return null;
            Assert.IsFalse(view.IsOpen);
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoSkullBeforeTheBossOrWithoutTheMedicalCourse()
        {
            var (area, session) = Setup(bossDefeated: false);
            var field = Field(area, session);
            yield return null;
            Assert.IsFalse(field.SkullVisible, "보스를 이기기 전엔 없음");
            Assert.IsFalse(field.SkullCell.HasValue);
            Object.Destroy(field.gameObject);
            yield return null;

            (area, session) = Setup(bossDefeated: true, medical: false);
            field = Field(area, session);
            yield return null;
            Assert.IsFalse(field.SkullCell.HasValue, "수능·비즈니스는 확인문제가 없어 해골도 없음");
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
