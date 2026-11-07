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
    // #233 사용자 ⑯: 아직 [확인]하지 않은 상자·열쇠에 은은한 반짝임 — 열면 그 상자는 더 반짝이지 않음
    public class SparkleScreenTests
    {
        //   y=1  #PC..#   상자 (2,1)
        private const string Map = "######\n#PC..#\n######";

        [UnityTest]
        public IEnumerator UnopenedChestsSparkleAndOpenedOnesStop()
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var enemy = TestData.Species("slime", new MonsterStats(10, 1, 1), new MonsterStats(0, 0, 0), bite);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) });
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "sparkle").Set("regionName", "반짝");
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "sparkle").Set("displayName", "반짝 들판").Set("map", Map).Set("encounters", table).Set("words", words)
                .Set("chests", new List<ChestContent> { new ChestContent(null, 0, 10) });
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);
            var field = new GameObject("SparkleField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f);
            yield return WaitFor(() => field.SparklesPlayed > 0, 2f);
            Assert.Greater(field.SparklesPlayed, 0, "열지 않은 상자가 반짝임");
            Assert.AreEqual(new Vector2Int(2, 1), field.LastSparkleCell);

            yield return FaceStick(field, Direction.Right);
            yield return PressConfirm(field);
            yield return new WaitForSecondsRealtime(0.5f);
            int after = field.SparklesPlayed;
            yield return new WaitForSecondsRealtime(3.5f);
            Assert.AreEqual(after, field.SparklesPlayed, "연 상자는 더 반짝이지 않음");
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
