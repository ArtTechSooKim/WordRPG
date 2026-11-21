using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    // 의학 용어 확인문제 (#280 사용자: 의대생이 장마다 만든 문제를 보스전·석판(아라)·반딧돌(수정구)에).
    // Figma '보스전 확인문제 (#280)'·'5지선다'·'석판 확인문제'·'반딧돌 확인문제'
    public class ExamScreenTests
    {
        private const string ShortQuestion = "심장박동이 정지해서 혈액을 방출할 수 없게 된 상태?";
        private const string ChoiceQuestion = "심실사이의 사이막(중격)에 작은 구멍이 있는 선천성 심장질환은? (① TOF ② VSD ③ ASD ④ PDA ⑤ PFO)";

        private static List<ExamQuestion> Exams() => new List<ExamQuestion>
        {
            new ExamQuestion("t_02_02", 2, "제2장 심혈관 및 조혈계", 2, ShortQuestion, "cardiac arrest"),
            new ExamQuestion("t_02_15", 2, "제2장 심혈관 및 조혈계", 15, ChoiceQuestion, "② VSD"),
            new ExamQuestion("t_02_01", 2, "제2장 심혈관 및 조혈계", 1, "정상심장에서 특수전도계의 자극전도 시작부위는?", "sinoatrial node"),
            new ExamQuestion("t_02_03", 2, "제2장 심혈관 및 조혈계", 3, "좌심방과 우심방 사이 중격의 결손은?", "atrial septal defect"),
            new ExamQuestion("t_02_06", 2, "제2장 심혈관 및 조혈계", 6, "심장의 내면을 싸고 있는 심내막의 염증?", "endocarditis"),
        };

        private static WordCourse Course(string areaId)
        {
            var book = ScriptableObject.CreateInstance<ExamBook>();
            book.ReplaceQuestions(Exams());
            var course = ScriptableObject.CreateInstance<WordCourse>();
            course.ConfigureExams(book, new[] { new CourseExamArea(areaId, 2) });
            return course;
        }

        [TearDown]
        public void TearDown() => WordCourses.Active = null;

        // 보스전: 문제 카드(로그·아군 칸 자리) + 보기 한 줄씩 — 주관식 4개 · 5지선다 5개, 틀리면 정답 카드, 숙련도엔 안 적힘
        [UnityTest]
        public IEnumerator BossBattleAsksTheChapterQuestions()
        {
            const string map = "##########\n#P:::::::#\n##########";
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "library").Set("regionName", "서고");
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var boss = TestData.Species("boss_exam", new MonsterStats(99999, 1, 50), new MonsterStats(0, 0, 0), bite).Set("displayName", "까먹대왕");
            var area = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "library").Set("displayName", "잊혀진 서고").Set("map", map)
                .Set("words", words).Set("encounters", ScriptableObject.CreateInstance<EncounterTable>());
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 5).Set("displayName", "휘두르기");
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(999, 5, 10)).Set("basicSkill", strike), 5);
            for (int i = 0; i < 3; i++) session.Record.RecordBattle(true, 1, 0);
            var field = new GameObject("ExamFieldUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, () => { }, step: 0.05f, animScale: 0.01f);
            yield return null;
            yield return null;

            var exams = Exams();
            var battle = field.Battle;
            battle.BeginBattle(new List<MonsterInstance> { new MonsterInstance(boss, 1) }, area.Words, _ => { }, null, area.Theme, boss: true, exam: exams);
            var root = battle.transform;
            var seen = new HashSet<string>();
            bool sawFive = false, sawFour = false;
            for (int round = 0; round < exams.Count; round++)
            {
                yield return WaitFor(() => ActiveButton(root, "SkillButton_0") != null, 8f);
                Assert.IsFalse(battle.ExamCardVisible, "기술을 고를 땐 문제 카드 없음 (로그·아군 칸이 보임)");
                ActiveButton(root, "SkillButton_0").onClick.Invoke();
                yield return null;
                ActiveButton(root, "Intensity_0")?.onClick.Invoke();
                yield return WaitFor(() => battle.Engine.CurrentQuestion != null && battle.ExamCardVisible, 5f);
                var question = battle.Engine.CurrentQuestion;
                Assert.IsTrue(question.IsExam, "보스전 문제 = 확인문제");
                Assert.IsTrue(seen.Add(question.Exam.Id), "다 내기 전엔 같은 문제가 또 나오지 않음");
                Assert.AreEqual(question.Prompt, battle.ExamCardText, "문제 글은 카드에");
                Assert.AreEqual(question.Choices.Count, battle.ChoiceCount);
                Assert.Greater(question.ReadingSeconds, 0f, "읽을 시간을 더 줌");
                if (question.Exam.IsMultipleChoice)
                {
                    sawFive = true;
                    Assert.AreEqual(5, battle.ChoiceCount, "5지선다는 보기 다섯");
                    Assert.AreEqual("② VSD", question.CorrectAnswer);
                    StringAssert.DoesNotContain("①", battle.ExamCardText, "보기는 문제 글에서 빼고 버튼으로");
                }
                else
                {
                    sawFour = true;
                    Assert.AreEqual(4, battle.ChoiceCount);
                }

                if (round == 0)
                {
                    int wrong = (question.CorrectIndex + 1) % question.Choices.Count;
                    ActiveButton(root, $"Choice_{wrong}").onClick.Invoke();
                    yield return WaitFor(() => battle.CardVisible, 5f);
                    Assert.AreEqual(question.CorrectAnswer, battle.CardWordText, "틀리면 정답을 보여 줌");
                    Assert.IsTrue(battle.ExamCardVisible, "정답 카드를 보는 동안 문제도 그대로");
                    Assert.AreEqual(MasteryLevel.New, session.Vocabulary.GetLevel(question.Exam.Id), "확인문제는 숙련도에 안 적힘");
                    ActiveButton(root, "CardConfirmButton").onClick.Invoke();
                }
                else
                {
                    ActiveButton(root, $"Choice_{question.CorrectIndex}").onClick.Invoke();
                }
                yield return null;
            }
            Assert.IsTrue(sawFive && sawFour, "주관식·5지선다 모두 나옴");
            Assert.IsEmpty(battle.Engine.Summary.WrongWords, "결산의 틀린 단어에는 안 들어감");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        //  y=4  #UUUUUUUU#
        //  y=3  #U;;;;;TU#   단어 석판 (7,3)
        //  y=2  #UUU;;UUU#
        //  y=1  #P:::::::#
        private const string RuinsMap =
            "##########\n" +
            "#UUUUUUUU#\n" +
            "#U;;;;;TU#\n" +
            "#UUU;;UUU#\n" +
            "#P:::::::#\n" +
            "##########";

        // 석판 (아라의 유적): 의학 용어면 본 단어가 없어도 이 지역 확인문제 3개 — 문제 글 + 보기 한 줄씩
        [UnityTest]
        public IEnumerator TabletAsksExamQuestionsInTheMedicalCourse()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "jungle").Set("displayName", "메아리 정글").Set("map", RuinsMap).Set("theme", FieldTheme.Jungle)
                .Set("encounters", ScriptableObject.CreateInstance<EncounterTable>()).Set("words", words);
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)), 5);
            session.Record.RecordBattle(true, 1, 0);
            WordCourses.Active = Course("jungle");
            var field = new GameObject("ExamTabletUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: false);
            yield return null;
            WordCourses.Active = Course("jungle"); // Configure가 세션의 단어장으로 정하므로 다시
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 4);
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 6, 3f);
            yield return FaceStick(field, Direction.Right);
            yield return PressConfirm(field);
            var view = field.TabletView;
            Assert.IsTrue(view.IsOpen, "본 단어가 없어도 확인문제로 열림");
            Assert.IsTrue(view.IsExam);
            for (int i = 0; i < TabletQuiz.QuestionsPerTablet; i++)
            {
                var question = field.TabletQuestion;
                Assert.IsTrue(question.IsExam);
                Assert.AreEqual(question.Prompt, view.Meaning, "새겨진 칸 = 문제 글");
                Assert.AreEqual(question.Choices.Count, view.ChoiceCount);
                StringAssert.Contains("확인문제 · 제2장", AllText(field.transform.Find("FieldHud")), "어느 장 문제인지");
                ActiveButton(field.transform, $"TabletChoice_{question.CorrectIndex}").onClick.Invoke();
                yield return WaitFor(() => view.IsComplete || field.TabletQuestion != question, 3f);
            }
            Assert.IsTrue(view.IsComplete, "세 문제를 풀면 석판이 채워짐");
            Assert.IsTrue(Ruins.IsTabletSolved(area, session.World, new Vector2Int(7, 3)));
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 반딧돌 (수정구): 의학 용어면 확인문제 한 문제 — 밝게 읽히는 문제 글 + 보기 한 줄씩, 맞히면 밝아짐
        [UnityTest]
        public IEnumerator GlowStoneAsksAnExamQuestionInTheMedicalCourse()
        {
            const string map = "#######\n#::I::#\n#:::::#\n#P::::#\n#######";
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "cave").Set("displayName", "어두운 밀림 동굴").Set("map", map).Set("theme", FieldTheme.Cave)
                .Set("encounters", ScriptableObject.CreateInstance<EncounterTable>()).Set("words", words)
                .Set("encounterStyle", EncounterStyle.Grass);
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)), 5);
            session.Record.RecordBattle(true, 1, 0);
            var field = new GameObject("ExamStoneUnderTest").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: false);
            yield return null;
            WordCourses.Active = Course("cave");
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 2);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 3, 3f);
            yield return FaceStick(field, Direction.Up);
            yield return PressConfirm(field);
            var view = field.StoneView;
            Assert.IsTrue(view.IsOpen);
            Assert.IsTrue(view.IsExam);
            var question = field.StoneQuestion;
            Assert.IsTrue(question.IsExam);
            Assert.AreEqual(question.Prompt, view.Word, "떠오른 글 = 문제 글");
            Assert.AreEqual(question.Choices.Count, view.ChoiceCount);
            ActiveButton(field.transform, $"StoneChoice_{question.CorrectIndex}").onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.IsLit);
            Assert.AreEqual(question.CorrectAnswer, view.Meaning, "정답이 금빛으로");
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
