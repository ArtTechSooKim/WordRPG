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
    // 힐끔해골 (#281 → #283 사용자 친구 의견, Figma '힐끔해골 — 말 걸기 (#283)' · '힐끔해골의 방 — 문제 (#283)'):
    // 의학 용어 + 보스를 이긴 뒤 보석 가까운 벽 뒤에서 얼굴 반만 → [확인] '○○ 문제인데… 한번 풀어 볼래?' → [풀어보기] = 번쩍, 해골의 방에서 3문제 →
    // 틀리면 깔봄 · 처음 바로 맞힌 새 문제마다 골드 · 많이 맞히면 슬금슬금 도망 → 다음 벽 뒤로
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
        //  y=3  #...B....#   이긴 보스 (4,3) → 해골은 왼쪽 벽 (0,3) 뒤에서 (1,3) 쪽으로 빼꼼
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
        public IEnumerator SkullPeeksInvitesAndRunsOffWhenBeaten()
        {
            var (area, session) = Setup(bossDefeated: true);
            var field = Field(area, session);
            yield return null;
            Assert.IsTrue(field.SkullVisible, "의학 용어 + 이긴 보스 → 벽 뒤에 해골");
            var peek = field.SkullPeek.Value;
            Assert.AreEqual(new Vector2Int(0, 3), peek.Wall, "보석 가까운 벽");
            Assert.AreEqual(Vector2Int.right, peek.Out);
            Assert.Less(field.SkullShownPixels, 16, "얼굴은 반만");

            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3);
            Assert.AreEqual(new Vector2Int(1, 3), field.PlayerCell, "벽 옆 바닥 (해골은 벽 안이라 길을 막지 않음)");
            yield return FaceStick(field, Direction.Left);

            // [확인] → 말 걸기 → [안 할래]
            yield return PressConfirm(field);
            var view = field.SkullView;
            Assert.IsTrue(view.IsInviting, "말 걸기 카드");
            StringAssert.Contains("심혈관 및 조혈계 문제인데", view.InviteLine);
            StringAssert.Contains("한번 풀어 볼래?", view.InviteLine);
            ActiveButton(field.transform, "SkullLeave").onClick.Invoke();
            yield return null;
            Assert.IsFalse(view.IsOpen, "안 하면 그냥 닫힘");
            Assert.AreEqual(new Vector2Int(0, 3), field.SkullPeek.Value.Wall, "그 자리 그대로");

            // 다시 [확인] → [풀어보기] → 해골의 방
            yield return new WaitForSecondsRealtime(0.5f);
            yield return PressConfirm(field);
            Assert.IsTrue(view.IsInviting);
            int gold = session.Inventory.Gold;
            ActiveButton(field.transform, "SkullPlay").onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.InRoom, "번쩍 → 해골의 방");
            Assert.IsTrue(field.IsPanelOpen);
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
                    Assert.IsFalse(view.ChoiceEnabled(wrong), "틀린 보기만 꺼짐");
                    Assert.IsTrue(view.IsSneering, "틀리면 해골이 깔봄");
                    yield return null;
                }
                ActiveButton(field.transform, $"SkullChoice_{question.CorrectIndex}").onClick.Invoke();
                // 맞힌 모습은 누른 그 순간에 (테스트는 연출 0.01배라 다음 문제가 곧바로 옴)
                Assert.AreEqual(question.CorrectAnswer, view.Answer, "정답이 금빛으로");
                Assert.AreEqual(i == 0 ? "정답!" : $"+{ExamSkull.GoldPerQuestion}G", view.Stamp, "골드는 처음 바로 맞힌 문제만");
                yield return WaitFor(() => view.IsComplete || quiz.Index > i, 3f);
            }
            Assert.IsTrue(view.IsComplete);
            Assert.IsTrue(view.IsFleeing, "바로 맞힌 문제가 둘 → 슬금슬금 도망");
            StringAssert.Contains("오늘은 이만", view.Line);
            Assert.AreEqual(gold + 2 * ExamSkull.GoldPerQuestion, session.Inventory.Gold);
            Assert.AreEqual(2, session.World.SolvedExamCount, "바로 맞힌 두 문제는 세이브");

            ActiveButton(field.transform, "SkullDone").onClick.Invoke();
            yield return null;
            Assert.IsFalse(view.IsOpen);
            Assert.AreNotEqual(new Vector2Int(0, 3), field.SkullPeek.Value.Wall, "도망쳐서 다른 벽 뒤로");
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
            Assert.IsFalse(field.SkullPeek.HasValue);
            Object.Destroy(field.gameObject);
            yield return null;

            (area, session) = Setup(bossDefeated: true, medical: false);
            field = Field(area, session);
            yield return null;
            Assert.IsFalse(field.SkullPeek.HasValue, "수능·비즈니스는 확인문제가 없어 해골도 없음");
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
