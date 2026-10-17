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

        private static FieldArea Area()
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("spider", new MonsterStats(60, 1, 5), new MonsterStats(0, 0, 0), bite).Set("displayName", "엉킴거미");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 3, 3, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "cave").Set("regionName", "동굴");
            words.ReplaceWords(TestData.SampleWords());
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "cave").Set("displayName", "어두운 밀림 동굴").Set("map", Map())
                .Set("theme", FieldTheme.Cave).Set("encounters", table).Set("words", words)
                .Set("encounterStyle", EncounterStyle.Roaming);
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
            Assert.Less(field.BrightnessAt(new Vector2Int(10, 7)), 0.3f, "6칸 밖은 어둠");
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
