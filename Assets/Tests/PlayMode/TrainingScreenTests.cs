using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 수련 (#44): 필드 메뉴 [수련] → 방식 고르기 → 허수아비와 전투(이미 본 단어만) → [수련 종료] → 수련 결산 → 필드
    public class TrainingScreenTests
    {
        private const string Map = "#####\n#.P.#\n#####";

        private static FieldArea Area(MonsterSpecies enemy)
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) });
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "training_test").Set("displayName", "수련 마을").Set("map", Map)
                .Set("encounters", table).Set("words", words);
        }

        private static HeroData Player()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            return TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike);
        }

        private static MonsterSpecies Scarecrow() =>
            TestData.Species("training_scarecrow", new MonsterStats(30, 10, 10), new MonsterStats(4, 2, 2))
                .Set("displayName", "허수아비").Set("trainingDummy", true);

        private static FieldScreen CreateField(FieldArea area, GameSession session, MonsterSpecies scarecrow)
        {
            var enemy = area.Encounters.Entries[0].Species;
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { enemy, scarecrow }, new ItemData[0], new[] { area });
            var go = new GameObject("TrainingFieldUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            return field;
        }

        [UnityTest]
        public IEnumerator TrainingAgainstTheScarecrowUsesOnlyKnownWords()
        {
            var enemy = TestData.Species("slime", new MonsterStats(20, 5, 5), new MonsterStats(0, 0, 0),
                TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 10));
            var area = Area(enemy);
            var session = GameSession.NewGame(Player(), 3);
            var words = area.Words.Words;
            var rules = new MasteryRules();
            foreach (int i in new[] { 0, 1, 2 }) session.Vocabulary.RecordAnswer(words[i].Id, true, DateTime.UtcNow, rules);
            session.Vocabulary.RecordAnswer(words[3].Id, false, DateTime.UtcNow, rules); // 오답 노트
            var known = new HashSet<string> { words[0].Id, words[1].Id, words[2].Id, words[3].Id };
            var scarecrow = Scarecrow();
            var field = CreateField(area, session, scarecrow);
            yield return null;
            yield return null;
            var cell = field.PlayerCell;

            // 메뉴 [수련] → 창: 발견한 단어 · 오답 노트 수
            FindButton(field.transform, "TrainingButton").onClick.Invoke();
            Assert.IsTrue(field.TrainingView.IsOpen);
            StringAssert.Contains("발견한 단어 4개", field.TrainingView.InfoText);
            StringAssert.Contains("오답 노트 1개", field.TrainingView.InfoText);

            // [오답 위주 암기] → 허수아비 (주인공과 같은 레벨)
            ActiveButton(field.transform, "TrainingWrong").onClick.Invoke();
            Assert.IsFalse(field.TrainingView.IsOpen);
            yield return WaitFor(() => field.Battle.IsRunning);
            Assert.IsTrue(field.Battle.IsTraining);
            var dummy = field.Battle.Engine.Enemies[0];
            Assert.AreSame(scarecrow, dummy.Monster.Species);
            Assert.AreEqual(session.Hero.Level, dummy.Level);

            // 문제 5개를 풀고 (두 번째는 틀림) [수련 종료]
            var root = field.Battle.transform;
            var asked = new List<WordEntry>();
            QuizQuestion lastQuestion = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline && !field.Battle.IsResultVisible)
            {
                var engine = field.Battle.Engine;
                var card = ActiveButton(root, "CardConfirmButton"); // 틀렸을 때 정답 카드
                if (card != null) card.onClick.Invoke();
                else if (engine.Phase == BattlePhase.AnsweringQuiz && engine.CurrentQuestion != null && engine.CurrentQuestion != lastQuestion)
                {
                    var question = engine.CurrentQuestion;
                    var choice = ActiveButton(root, $"Choice_{question.CorrectIndex}");
                    if (choice != null)
                    {
                        Assert.IsFalse(question.IsNewWord, "수련에는 새 단어가 나오지 않음");
                        asked.Add(question.Word);
                        int index = asked.Count == 2 ? (question.CorrectIndex + 1) % question.Choices.Count : question.CorrectIndex;
                        lastQuestion = question;
                        ActiveButton(root, $"Choice_{index}").onClick.Invoke();
                    }
                }
                else if (engine.Phase == BattlePhase.ChoosingSkill)
                {
                    Assert.IsNull(ActiveButton(root, "ItemButton"), "수련 중에는 상처약 대신 [수련 종료]");
                    if (asked.Count >= 5) ActiveButton(root, "TrainingEndButton")?.onClick.Invoke();
                    else (ActiveButton(root, "Intensity_0") ?? ActiveButton(root, "SkillButton_0"))?.onClick.Invoke();
                }
                yield return null;
            }

            Assert.IsTrue(field.Battle.IsResultVisible, "[수련 종료]를 누르면 결산");
            Assert.AreEqual("수련 종료", field.Battle.ResultTitle);
            Assert.AreEqual(words[3], asked[0], "오답 노트 단어부터");
            foreach (var word in asked) Assert.IsTrue(known.Contains(word.Id), $"{word.English}는 발견하지 않은 단어");
            Assert.AreEqual(dummy.MaxHp, dummy.Hp, "허수아비는 HP가 줄지 않음");
            Assert.Greater(field.Battle.Engine.Summary.TotalDamage, 0, "피해는 그대로 들어감");
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp, "허수아비는 공격하지 않음");
            StringAssert.Contains("누적 피해", AllText(root.Find("BattleCanvas/SafeArea/EnemyArea")));

            var result = AllText(root.Find("BattleCanvas/SafeArea/ResultPanel"));
            StringAssert.Contains("허수아비에게 준 피해", result);
            StringAssert.Contains("복습한 단어", result);
            StringAssert.Contains("틀린 단어 1", result);
            StringAssert.DoesNotContain("새로 만난 단어", result);

            ActiveButton(root, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);
            Assert.IsFalse(field.Battle.IsTraining);
            Assert.AreEqual(cell, field.PlayerCell, "수련이 끝나면 그 자리");
            Assert.AreEqual(0, session.Record.BattlesWon + session.Record.BattlesLost, "수련은 전투 기록에 넣지 않음");

            UnityEngine.Object.Destroy(field.gameObject);
            yield return null;
        }

        // 실기기 점검표 (#48): 오답 노트에 들어간 단어를 도감에서 볼 수 있다
        [UnityTest]
        public IEnumerator DexMarksWordsInTheWrongNote()
        {
            var enemy = TestData.Species("slime", new MonsterStats(20, 5, 5), new MonsterStats(0, 0, 0),
                TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 10));
            var area = Area(enemy);
            var session = GameSession.NewGame(Player(), 1);
            var words = area.Words.Words;
            var rules = new MasteryRules();
            session.Vocabulary.RecordAnswer(words[0].Id, true, DateTime.UtcNow, rules);
            session.Vocabulary.RecordAnswer(words[1].Id, false, DateTime.UtcNow, rules); // 오답 노트
            var field = CreateField(area, session, Scarecrow());
            yield return null;
            yield return null;

            FindButton(field.transform, "DexButton").onClick.Invoke();
            Transform dex = null;
            foreach (var t in field.GetComponentsInChildren<Transform>(true))
                if (t.name == "DexView" && t.gameObject.activeInHierarchy) dex = t; // 전투 화면에도 도감이 있어 열린 것을
            Assert.IsNotNull(dex);
            yield return null;
            StringAssert.Contains("오답 노트 1", AllText(dex));
            Transform row0 = null, row1 = null;
            foreach (var t in dex.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "DexRow_0") row0 = t;
                if (t.name == "DexRow_1") row1 = t;
            }
            Assert.IsNull(row0.Find("WrongTag"), "맞힌 단어는 표시 없음");
            Assert.IsNotNull(row1.Find("WrongTag"), "오답 노트 단어에 빨간 '오답'");
            row1.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            StringAssert.Contains("오답 노트에 있어요", AllText(dex));

            UnityEngine.Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrainingNeedsDiscoveredWords()
        {
            var enemy = TestData.Species("slime", new MonsterStats(20, 5, 5), new MonsterStats(0, 0, 0),
                TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 10));
            var area = Area(enemy);
            var session = GameSession.NewGame(Player(), 1);
            var field = CreateField(area, session, Scarecrow());
            yield return null;
            yield return null;

            FindButton(field.transform, "TrainingButton").onClick.Invoke();
            Assert.IsTrue(field.TrainingView.IsOpen);
            StringAssert.Contains("발견한 단어 0개", field.TrainingView.InfoText);
            Assert.IsNull(ActiveButton(field.transform, "TrainingAll"), "발견한 단어가 없으면 시작할 수 없음");
            Assert.IsNull(ActiveButton(field.transform, "TrainingWrong"));

            // 뒤로가기(닫기)
            field.HandleBack();
            Assert.IsFalse(field.TrainingView.IsOpen);

            UnityEngine.Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
