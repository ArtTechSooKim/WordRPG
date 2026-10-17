using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 1.2 동굴 어둠 (#112): 동굴에 들어가면 어둠 한 장 — 주인공 둘레·밝힌 반딧돌은 환하고 먼 곳은 캄캄, 빛 밖 몬스터는 빨간 눈만.
    // 가만히 있으면 다시 칠하지 않음 (폰 부담)
    public class CaveScreenTests
    {
        // 30×14. 시작 P (3,1), 샘 (8,1). 반딧돌 I (2,6) — 시작에서 5칸 넘게. 위쪽 덤불 띠(y 9~12, 112칸 + 아래 줄) = 몬스터 서식지 (시작에서 8칸 넘게)
        private static string Map()
        {
            var rows = new List<string>();
            for (int r = 0; r < 14; r++)
            {
                var row = new char[30];
                for (int x = 0; x < 30; x++)
                    row[x] = r == 0 || r == 13 || x == 0 || x == 29 ? '#' : r >= 1 && r <= 5 ? ',' : ':';
                rows.Add(new string(row));
            }
            var stoneRow = rows[7].ToCharArray(); // y = 6
            stoneRow[2] = 'I';
            rows[7] = new string(stoneRow);
            var startRow = rows[12].ToCharArray(); // y = 1
            startRow[3] = 'P';
            startRow[8] = 'F';
            rows[12] = new string(startRow);
            return string.Join("\n", rows);
        }

        private static FieldArea Area(bool roaming = true, string map = null, EncounterTable water = null)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("spider", new MonsterStats(60, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "엉킴거미");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 3, 3, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "cave").Set("regionName", "동굴");
            words.ReplaceWords(TestData.SampleWords());
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "cave").Set("displayName", "어두운 밀림 동굴").Set("map", map ?? Map())
                .Set("theme", FieldTheme.Cave).Set("encounters", table).Set("words", words).Set("waterEncounters", water)
                .Set("encounterStyle", roaming ? EncounterStyle.Roaming : EncounterStyle.Grass);
        }

        private static EncounterTable InkTable()
        {
            var spit = TestData.Skill("ink_spit", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var salamander = TestData.Species("ink_salamander", new MonsterStats(60, 1, 5), new MonsterStats(0, 0, 0), spit).Set("displayName", "먹물도롱뇽");
            return ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(salamander, 3, 3, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
        }

        // 20×8 바위 바닥. 시작 P (3,1), 먹물 웅덩이 (9~11, 4) — 시작에서 5칸 넘게 떨어져 있어 거기 숨어 있음
        private static string PoolMap()
        {
            var rows = new List<string>();
            for (int r = 0; r < 8; r++)
            {
                var row = new char[20];
                for (int x = 0; x < 20; x++) row[x] = r == 0 || r == 7 || x == 0 || x == 19 ? '#' : (7 - r == 4 && x >= 9 && x <= 11) ? '~' : ':';
                rows.Add(new string(row));
            }
            var start = rows[6].ToCharArray(); // y = 1
            start[3] = 'P';
            rows[6] = new string(start);
            return string.Join("\n", rows);
        }

        private static GameSession Session() => GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);

        private static FieldScreen Field(FieldArea area, GameSession session)
        {
            var field = new GameObject("CaveField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: false);
            return field;
        }

        [UnityTest]
        public IEnumerator CaveIsDarkExceptAroundTheHero()
        {
            var field = Field(Area(), Session());
            yield return null;
            yield return null;
            Assert.IsTrue(field.HasDarkness, "동굴엔 어둠");
            Assert.Greater(field.BrightnessAt(new Vector2Int(3, 1)), 0.99f, "주인공 자리는 환함");
            Assert.Less(field.BrightnessAt(new Vector2Int(25, 6)), 0.05f, "멀리 처음 가는 곳은 캄캄");
            Assert.Less(field.BrightnessAt(new Vector2Int(2, 6)), 0.3f, "꺼진 반딧돌은 빛을 내지 않음");
            Assert.Greater(field.BrightnessAt(new Vector2Int(10, 1)), 0.99f, "쉼터의 샘 둘레는 모닥불처럼 밝음");
            Assert.AreEqual(1, field.StoneGlimmers, "꺼진 반딧돌은 어둠 속에서 희미하게 깜빡 (찾아갈 수 있게)");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LitStoneLightsItsSurroundings()
        {
            var area = Area();
            var session = Session();
            GlowStones.Light(area, session.World, new Vector2Int(2, 6));
            var field = Field(area, session);
            yield return null;
            Assert.Greater(field.BrightnessAt(new Vector2Int(2, 6)), 0.99f, "밝힌 반딧돌");
            Assert.Greater(field.BrightnessAt(new Vector2Int(5, 7)), 0.99f, "둘레 몇 칸도 환함");
            Assert.AreEqual(CaveLight.AllLitGlow, field.BrightnessAt(new Vector2Int(10, 7)), 0.01f, "6칸 밖은 은은하게만 (반딧돌이 하나라 모두 밝힌 셈)");
            Assert.AreEqual(0, field.StoneGlimmers, "밝힌 반딧돌은 깜빡이는 빛이 필요 없음");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MonstersInTheDarkShowOnlyRedEyes()
        {
            var field = Field(Area(), Session());
            yield return null;
            int shown = 0;
            for (int i = 0; i < 10 && shown == 0; i++)
            {
                yield return null;
                shown = field.RoamerEyesShown;
            }
            Assert.Greater(field.RoamerViewCount, 0, "서식지에 몬스터");
            Assert.Greater(shown, 0, "빛 밖 몬스터는 눈만 보임");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StandingStillDoesNotRepaintTheDarkness()
        {
            var field = Field(Area(), Session());
            yield return null;
            yield return null;
            int before = field.DarknessRepaints;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.LessOrEqual(field.DarknessRepaints - before, 1, "가만히 있으면 다시 칠하지 않음");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 글자 반딧돌 (#113): 옆에서 [확인] → 떠오른 영단어의 뜻 고르기 (틀려도 벌점 없이 다시) → 맞히면 밝아짐 → [확인]으로 닫으면 빛이 퍼짐
        [UnityTest]
        public IEnumerator GlowStoneQuizLightsTheStone()
        {
            var area = Area(roaming: false);
            var session = Session();
            for (int i = 0; i < 5; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, System.DateTime.UtcNow.AddDays(-1));
            var field = Field(area, session);
            yield return null;
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 6);
            yield return FaceStick(field, Direction.Left);
            Assert.AreEqual(new Vector2Int(3, 6), field.PlayerCell);
            CollectionAssert.Contains(field.VisibleNameTags, "글자 반딧돌");
            yield return PressConfirm(field);
            var view = field.StoneView;
            Assert.IsTrue(view.IsOpen, "반딧돌 문제 창");
            Assert.IsTrue(field.IsPanelOpen);
            var question = field.StoneQuestion;
            Assert.AreEqual(QuizDirection.EnglishToMeaning, question.Direction, "영단어를 보고 뜻 고르기");
            Assert.AreEqual(question.Prompt, view.Word, "떠오른 글자");
            Assert.AreEqual("밝힌 반딧돌 0 / 1", view.Counter);

            int wrong = (question.CorrectIndex + 1) % question.Choices.Count;
            ActiveButton(field.transform, $"StoneChoice_{wrong}").onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.IsOpen, "틀려도 닫히지 않음");
            Assert.IsFalse(view.IsLit);
            ActiveButton(field.transform, $"StoneChoice_{question.CorrectIndex}").onClick.Invoke();
            yield return null;
            Assert.IsTrue(view.IsLit, "맞히면 밝아짐");
            Assert.AreEqual(question.CorrectAnswer, view.Meaning, "뜻이 금빛으로");
            Assert.IsTrue(GlowStones.IsLit(area, session.World, new Vector2Int(2, 6)), "밝힌 반딧돌은 바로 세이브");
            StringAssert.Contains("모두 밝혔다", view.Note, "반딧돌이 하나뿐이라 다 밝힘");

            ActiveButton(field.transform, "StoneDone").onClick.Invoke();
            yield return null;
            Assert.IsFalse(view.IsOpen);
            Assert.AreEqual(1, field.StonesLit);
            Assert.AreEqual(0, field.StoneGlimmers, "깜빡이던 빛 대신 환한 빛");
            yield return WaitFor(() => !field.IsStoneLightGrowing, 3f);
            Assert.Greater(field.BrightnessAt(new Vector2Int(6, 7)), 0.9f, "빛이 둘레까지 퍼짐");
            Assert.Greater(field.BrightnessAt(new Vector2Int(25, 10)), 0.3f, "모두 밝혀 동굴 전체가 은은하게");

            yield return new WaitForSecondsRealtime(0.6f);
            yield return PressConfirm(field);
            Assert.IsFalse(view.IsOpen, "밝힌 반딧돌은 다시 묻지 않음");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlowStoneNeedsWordsYouHaveSeen()
        {
            var field = Field(Area(roaming: false), Session());
            yield return null;
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 6);
            yield return FaceStick(field, Direction.Left);
            yield return PressConfirm(field);
            Assert.IsFalse(field.StoneView.IsOpen, "본 단어가 없으면 문제 없음");
            StringAssert.Contains("단어를 더 만나면", field.Bubble.Text);
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 먹물도롱뇽 (#113): 웅덩이에 숨어 안 보이다가, 2칸 안으로 다가가면 거품 → 튀어나와 몸이 보임
        [UnityTest]
        public IEnumerator InkSalamanderBubblesThenJumpsOut()
        {
            var field = Field(Area(map: PoolMap(), water: InkTable()), Session());
            yield return null;
            Assert.AreEqual(1, field.RoamerViewCount, "웅덩이에 한 마리");
            Assert.AreEqual(1, field.HiddenRoamers, "숨어 있음");
            Assert.AreEqual(0, field.RoamerBubblesShown);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 10);
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 2 || field.RoamerBubblesShown > 0, 2f);
            yield return WaitFor(() => field.RoamerBubblesShown > 0 || field.HiddenRoamers == 0, 2f);
            Assert.IsTrue(field.RoamerBubblesShown > 0 || field.HiddenRoamers == 0, "거품이 오름");
            yield return WaitFor(() => field.HiddenRoamers == 0, 3f);
            Assert.AreEqual(0, field.HiddenRoamers, "튀어나와 몸이 보임");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OtherAreasHaveNoDarkness()
        {
            var area = Area().Set("theme", FieldTheme.Jungle);
            var field = Field(area, Session());
            yield return null;
            Assert.IsFalse(field.HasDarkness);
            Assert.AreEqual(0, field.RoamerEyesShown);
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
