using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 보스 방 문 (#76): 문 앞 [확인] → "열쇠가 필요한 것 같다" → 받침대에서 열쇠 → "보스를 상대하려면…" → 단어를 모으면 문이 열리고 지나감
    public class BossGateScreenTests
    {
        //  y=3  #..B..#   보스 (3,3)
        //  y=2  ##GGG##   보스 방 문
        //  y=1  #K...P#   열쇠 받침대 (1,1), 시작 (5,1)
        private const string Map = "#######\n#..B..#\n##GGG##\n#K...P#\n#######";

        private static FieldArea Area()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var slime = TestData.Species("slime", new MonsterStats(20, 1, 1), new MonsterStats(0, 0, 0), bite);
            var king = TestData.Species("king", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), bite).Set("displayName", "까먹대왕");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new System.Collections.Generic.List<EncounterTable.Entry> { new EncounterTable.Entry(slime, 1, 1, 1) });
            var key = TestData.Item("gate_key").Set("displayName", "서고 열쇠").Set("kind", ItemKind.Key);
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "gate_screen").Set("displayName", "잊혀진 서고").Set("map", Map).Set("theme", FieldTheme.Library)
                .Set("encounters", table).Set("words", words).Set("boss", new BossEncounter(king, 3))
                .Set("bossKey", key).Set("bossGateWords", 3);
        }

        [UnityTest]
        public IEnumerator KeyAndWordsOpenTheBossGate()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 30, 10)), 3);
            session.Record.RecordBattle(true, 1, 0);
            var go = new GameObject("GateFieldUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f);
            yield return null;
            yield return null;
            Assert.AreEqual("보스 방 열쇠를 찾자", field.ObjectiveText);

            // 문 앞 (3,1)에서 위로 [확인] → 열쇠가 없음
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 3);
            yield return FaceStick(field, Direction.Up);
            Assert.AreEqual(new Vector2Int(3, 1), field.PlayerCell);
            CollectionAssert.Contains(field.VisibleNameTags, "보스 방 문", "문 가운데 칸에 이름표");
            yield return PressConfirm(field);
            Assert.IsTrue(field.Bubble.ShowsText);
            StringAssert.Contains("열쇠가 필요한 것 같다", field.Bubble.Text);

            // 왼쪽 끝 받침대에서 열쇠
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 2);
            yield return FaceStick(field, Direction.Left);
            yield return new WaitForSecondsRealtime(0.4f); // [확인] 사이 0.3초 쉬는 시간
            yield return PressConfirm(field);
            StringAssert.Contains("서고 열쇠를 찾았다", field.ToastMessage);
            Assert.AreEqual(1, session.Inventory.GetCount(area.BossKey));
            StringAssert.StartsWith("새 단어로 힘을 모으자 0/3", field.ObjectiveText);

            // 다시 문 앞 → 단어가 모자람 (발견 수 안내)
            yield return new WaitForSecondsRealtime(0.4f);
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 3);
            yield return FaceStick(field, Direction.Up);
            yield return PressConfirm(field);
            StringAssert.Contains("보스를 상대하려면 새로운 단어로 힘을 모아야해", field.Bubble.Text);
            StringAssert.Contains("0 / 3", field.Bubble.Text);
            yield return HoldStick(field, Direction.Up, () => field.IsMoving, 0.3f);
            Assert.AreEqual(new Vector2Int(3, 1), field.PlayerCell, "닫힌 문은 못 지나감");

            // 단어 3개를 발견하면 문이 열림 → 지나가서 보스 앞으로
            for (int i = 0; i < 3; i++) session.Vocabulary.Discover(area.Words.Words[i].Id, DateTime.UtcNow);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return FaceStick(field, Direction.Up);
            yield return PressConfirm(field);
            Assert.AreEqual("보스 방 문이 열렸다!", field.ToastMessage);
            Assert.IsTrue(session.World.IsGateOpened(area.AreaId));
            Assert.AreEqual("보스 까먹대왕을 물리치자", field.ObjectiveText);
            CollectionAssert.DoesNotContain(field.VisibleNameTags, "보스 방 문", "연 문은 이름표를 감춤");
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 2);
            Assert.AreEqual(new Vector2Int(3, 2), field.PlayerCell, "연 문 칸에 설 수 있음");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }
    }
}
