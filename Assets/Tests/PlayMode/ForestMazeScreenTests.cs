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

namespace WordRPG.Tests
{
    // 1.4 숲 이정표 미로 (#279): 안개에 부딪히면 맴돌다 제자리 → 이정표 [확인] → 뒤섞인 철자 고르기(틀려도 다시) → 바로잡으면 안개가 걷혀 지나감
    public class ForestMazeScreenTests
    {
        //  y=4  #########
        //  y=3  #..MM...#   헷갈 안개 (3,3)(4,3)
        //  y=2  ###W#X..#   맴도는 안개 (3,2) · 이정표 (5,2)
        //  y=1  #.....P.#   시작 (6,1)
        //  y=0  #########
        private const string Map = "#########\n#..MM...#\n###W#X..#\n#.....P.#\n#########";
        private static readonly Vector2Int Post = new Vector2Int(5, 2);

        private static FieldArea Area()
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("owl", new MonsterStats(60, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "물음표부엉이");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 3, 3, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "forest").Set("regionName", "숲");
            words.ReplaceWords(TestData.SampleWords());
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "forest").Set("displayName", "숲").Set("map", Map).Set("theme", FieldTheme.Forest)
                .Set("encounters", table).Set("encounterRate", 0f).Set("words", words);
        }

        private static GameSession Session(FieldArea area)
        {
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            for (int i = 0; i < 6; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            return session;
        }

        private static FieldScreen Field(FieldArea area, GameSession session)
        {
            var field = new GameObject("ForestField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: false);
            return field;
        }

        [UnityTest]
        public IEnumerator FixingTheSignpostClearsTheHaze()
        {
            var area = Area();
            var session = Session(area);
            var field = Field(area, session);
            yield return null;

            // 안개에 부딪힘 → 맴돌다 제자리
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3);
            CollectionAssert.Contains(field.VisibleNameTags, "이정표", "가까이 가면 이름표 (말풍선이 떠 있을 땐 숨음)");
            yield return HoldStick(field, Direction.Left, () => field.HazeLoops > 0, 2f);
            Assert.AreEqual(1, field.HazeLoops, "부딪히면 맴돌기 한 번 (밀고 있어도 연달아 나오지 않음)");
            Assert.AreEqual(new Vector2Int(5, 3), field.PlayerCell, "안개 속으로 못 들어가고 제자리");
            yield return WaitFor(() => !field.IsInBattle, 2f);
            StringAssert.Contains("맴돌다 제자리", field.Bubble.Text);

            // 이정표 [확인]
            yield return FaceStick(field, Direction.Down);
            yield return PressConfirm(field);
            var view = field.SignpostView;
            Assert.IsTrue(view.IsOpen, "이정표 문제 창");
            Assert.IsTrue(field.IsPanelOpen);
            var question = field.SignpostQuestion;
            Assert.AreEqual(SpellingQuiz.Spaced(question.Scrambled), view.Word, "뒤섞인 글자");
            Assert.AreEqual(question.Meaning, view.Meaning, "뜻");
            Assert.AreEqual("바로잡은 이정표 0 / 1", view.Counter);
            for (int i = 0; i < 4; i++) Assert.AreEqual(question.Choices[i], view.ChoiceText(i));

            int wrong = (question.CorrectIndex + 1) % question.Choices.Count;
            ActiveButton(field.transform, $"SignpostChoice_{wrong}").onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.IsOpen, "틀려도 닫히지 않음");
            Assert.IsFalse(view.ChoiceEnabled(wrong), "틀린 보기만 꺼짐");
            Assert.IsFalse(Signposts.IsFixed(area, session.World, Post));
            ActiveButton(field.transform, $"SignpostChoice_{question.CorrectIndex}").onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.IsFixed, "맞히면 바로잡힘");
            Assert.AreEqual(question.CorrectAnswer, view.Word, "바른 철자가 금빛으로");
            Assert.IsTrue(Signposts.IsFixed(area, session.World, Post), "바로잡은 이정표는 바로 세이브");
            StringAssert.Contains("모두 바로잡았다", view.Note, "이정표가 하나뿐");

            ActiveButton(field.transform, "SignpostDone").onClick.Invoke();
            yield return null;
            Assert.IsFalse(view.IsOpen);
            yield return WaitFor(() => field.SignpostsFixed == 1 && !field.IsInBattle, 3f);
            Assert.AreEqual(1, field.SignpostsFixed);

            // 걷힌 안개를 지나감
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 2, 3f);
            Assert.AreEqual(new Vector2Int(2, 3), field.PlayerCell, "안개가 걷혀 지나감");

            // 가짜 길(맴도는 안개)은 그대로
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 3, 2f);
            int loops = field.HazeLoops;
            yield return HoldStick(field, Direction.Down, () => field.HazeLoops > loops, 2f);
            Assert.AreEqual(new Vector2Int(3, 3), field.PlayerCell, "맴도는 안개는 바로잡아도 못 들어감");
            yield return WaitFor(() => !field.IsInBattle, 2f);
            StringAssert.Contains("이정표가 가리키는 쪽", field.Bubble.Text, "가짜 길이라는 말");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SignpostNeedsWordsYouHaveSeen()
        {
            var area = Area();
            var field = Field(area, GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5));
            yield return null;
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3);
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 5, 2f);
            yield return FaceStick(field, Direction.Down);
            yield return PressConfirm(field);
            Assert.IsFalse(field.SignpostView.IsOpen, "본 단어가 없으면 문제 없음");
            StringAssert.Contains("단어를 더 만나면", field.Bubble.Text);
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
