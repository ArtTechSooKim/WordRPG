using System.Collections;
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
    // 뗏목으로 빠른 물살 거스르기 (#235 사용자 ㉛): 어귀로 3칸까지 들어가고, 멈추면 물살에 밀려 뗏목 물로
    public class RaftCurrentScreenTests
    {
        //   y=1  #PJ~~<<<<#   나루터 (2,1) · 뗏목 물 (3,1)(4,1) · 왼쪽으로 흐르는 얕은 강 (5..8,1)
        private const string Map = "##########\n#PJ~~<<<<#\n##########";

        [UnityTest]
        public IEnumerator TheRaftGoesThreeCellsUpstreamThenTheCurrentPushesItBack()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "river").Set("regionName", "강");
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "river").Set("displayName", "강")
                .Set("map", Map).Set("words", words).Set("theme", FieldTheme.Jungle);
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 3);
            var field = new GameObject("RiverField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f);
            yield return null;
            yield return null;

            // 나루터 → 뗏목 → 어귀로 계속 밀고 있기: 3칸째(7,1)까지 가고, 더는 못 가고 물살에 밀려 큰 강(4,1)으로
            int furthest = 0;
            var stick = field.Stick;
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pointerId = 1, position = stick.ScreenPointFor(Vector2.zero) };
            UnityEngine.EventSystems.ExecuteEvents.Execute(stick.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            data.position = stick.ScreenPointFor(Vector2.right * 0.6f);
            UnityEngine.EventSystems.ExecuteEvents.Execute(stick.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
            for (float end = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < end && field.DriftSteps == 0;)
            {
                furthest = Mathf.Max(furthest, field.PlayerCell.x);
                yield return null;
            }
            UnityEngine.EventSystems.ExecuteEvents.Execute(stick.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            Assert.AreEqual(7, furthest, "물살을 거슬러 3칸까지 (4칸째는 못 감)");
            Assert.Greater(field.DriftSteps, 0, "끝까지 가면 물살에 밀림");
            yield return WaitFor(() => !field.IsDrifting && !field.IsMoving, 3f);
            Assert.AreEqual(new Vector2Int(4, 1), field.PlayerCell, "뗏목 물로 밀려 돌아옴");
            Assert.AreEqual(3, field.DriftSteps);
            Assert.IsTrue(field.IsRafting, "뗏목은 그대로");

            // 한 칸만 들어갔다 손을 떼도 잠시 뒤 밀려남
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell == new Vector2Int(5, 1), 2f);
            yield return WaitFor(() => field.DriftSteps == 4 && !field.IsMoving, 3f);
            Assert.AreEqual(new Vector2Int(4, 1), field.PlayerCell);
            Assert.AreEqual(4, field.DriftSteps);
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
