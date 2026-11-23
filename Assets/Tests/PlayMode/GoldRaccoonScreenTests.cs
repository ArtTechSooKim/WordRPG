using System;
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
    // 비밀의 숲 황금 너구리 (#292 사용자): 들어오면 앞에서 춤추다 도망 → 옆에서 [잡기] = 동전 띠링 + 100 → 또 도망 … →
    // 마지막(1000)은 동전이 파바바박 터지고 춤추며 사라짐 (말은 안 함). 하루 한 번 — 다음 날 다시
    public class GoldRaccoonScreenTests
    {
        //  y=6  ###########
        //  y=5  #.3.......#   자리 ④ (3,5)
        //  y=4  #.2..0..1.#   자리 ① (5,4) · ② (8,4) · ③ (2,4)
        //  y=3  #.........#
        //  y=2  #.........#
        //  y=1  #....P....#   시작 (5,1)
        //  y=0  ###########
        private const string Map = "###########\n#.........#\n#.........#\n#.........#\n#.........#\n#....P....#\n###########";
        private static readonly Vector2Int[] Spots = { new Vector2Int(5, 4), new Vector2Int(8, 4), new Vector2Int(2, 4), new Vector2Int(3, 5) };
        private static readonly DateTime Day = new DateTime(2026, 12, 3, 10, 0, 0);

        private static FieldArea Area()
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 3);
            var enemy = TestData.Species("owl", new MonsterStats(60, 1, 5), new MonsterStats(0, 0, 0), bite);
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 3, 3, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "forest").Set("regionName", "숲");
            words.ReplaceWords(TestData.SampleWords());
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "secret_test").Set("displayName", "비밀의 숲").Set("map", Map).Set("theme", FieldTheme.Forest)
                .Set("encounters", table).Set("encounterRate", 0f).Set("words", words)
                .Set("goldRaccoonSpots", new List<Vector2Int>(Spots)).Set("goldRaccoonGold", new List<int> { 100, 150, 200, 1000 });
        }

        private static FieldScreen Field(FieldArea area, GameSession session, DateTime day)
        {
            var field = new GameObject("SecretField").AddComponent<FieldScreen>();
            field.LocalClock = () => day;
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, tutorials: false);
            return field;
        }

        private static IEnumerator Catch(FieldScreen field, int catches)
        {
            yield return new WaitForSecondsRealtime(0.1f); // 말이 끝난 뒤 [확인] 쉬는 시간이 지나게 (배치모드는 프레임이 아주 빨라 몇 프레임으론 모자람)
            yield return PressConfirm(field);
            yield return WaitFor(() => field.RaccoonCatches == catches && !field.IsCutsceneRunning && !field.RaccoonBusy, 5f);
        }

        [UnityTest]
        public IEnumerator TheGoldRaccoonDancesFleesThreeTimesAndSaysGoodbye()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            int gold = session.Inventory.Gold;
            var field = Field(area, session, Day);
            yield return null;

            // 들어오면 앞(가는 길 두 칸째)에 나타나 춤추다 첫 자리로 도망
            Assert.IsTrue(field.RaccoonVisible, "오늘 처음 — 황금 너구리가 있음");
            yield return WaitFor(() => field.RaccoonCell == Spots[0] && !field.RaccoonBusy && !field.IsCutsceneRunning, 5f);
            Assert.AreEqual(Spots[0], field.RaccoonCell, "춤추고 첫 자리로 도망");
            Assert.AreEqual(gold, session.Inventory.Gold, "아직 골드 없음");

            // ① 쫓아가 옆에서 [확인] → 100 · 춤추고 ②로
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3);
            Assert.AreEqual(new Vector2Int(5, 3), field.PlayerCell, "너구리는 길을 막음 — 바로 아래에 섬");
            yield return null;
            Assert.AreEqual("잡기", field.ConfirmText, "#292 사용자: 너구리 옆에선 [확인] 대신 [잡기]");
            yield return Catch(field, 1);
            Assert.AreEqual(gold + 100, session.Inventory.Gold, "첫 번째 = 100골드");
            Assert.AreEqual(Spots[1], field.RaccoonCell, "또 도망");

            // ② → 150 · ③으로
            yield return HoldStick(field, Direction.Right, () => field.PlayerCell.x >= 8, 3f);
            Assert.AreEqual(new Vector2Int(8, 3), field.PlayerCell);
            yield return Catch(field, 2);
            Assert.AreEqual(gold + 250, session.Inventory.Gold, "두 번째 = 150골드");
            Assert.AreEqual(Spots[2], field.RaccoonCell);

            // ③ → 200 · 마지막 자리로
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 2, 3f);
            yield return Catch(field, 3);
            Assert.AreEqual(gold + 450, session.Inventory.Gold, "세 번째 = 200골드");
            Assert.AreEqual(Spots[3], field.RaccoonCell, "세 번 도망 (#292 사용자)");

            // ④ 마지막 → 1000 · 동전 파바바박 · 춤추며 사라짐
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 5, 3f);
            Assert.AreEqual(new Vector2Int(2, 5), field.PlayerCell);
            yield return Catch(field, 4);
            Assert.AreEqual(gold + 1450, session.Inventory.Gold, "마지막 = 1000골드 (모두 1,450)");
            Assert.Greater(field.RaccoonCoinsShown, 20, "동전이 파바바박");
            Assert.IsFalse(field.RaccoonVisible, "춤추며 사라짐");
            Assert.IsNull(field.RaccoonCell);
            Assert.AreEqual(4, GoldRaccoon.CaughtToday(session.World, Day), "세이브에 오늘 다 잡은 것으로");
            Object.Destroy(field.gameObject);
            yield return null;

            // 같은 날 다시 들어오면 없음 · 다음 날엔 다시 처음부터
            var sameDay = Field(area, session, Day.AddHours(5));
            yield return null;
            Assert.IsFalse(sameDay.RaccoonVisible, "오늘은 이제 없음");
            Object.Destroy(sameDay.gameObject);
            yield return null;
            var nextDay = Field(area, session, Day.AddDays(1));
            yield return null;
            Assert.IsTrue(nextDay.RaccoonVisible, "하루가 지나면 다시 (#292 사용자: 하루에 한 번씩 리셋)");
            yield return WaitFor(() => nextDay.RaccoonCell == Spots[0] && !nextDay.RaccoonBusy && !nextDay.IsCutsceneRunning, 5f);
            Assert.AreEqual(Spots[0], nextDay.RaccoonCell, "첫 자리부터");
            Object.Destroy(nextDay.gameObject);
            yield return null;
        }

        // #292 사용자: 황금 너구리는 말하지 않음 — 춤추는 동안에도 말풍선 없음 (실제 빠르기로)
        [UnityTest]
        public IEnumerator TheGoldRaccoonDancesWithoutTalking()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            var field = new GameObject("SecretField").AddComponent<FieldScreen>();
            field.LocalClock = () => Day;
            field.Configure(area, session, step: 0.05f, animScale: 1f, tutorials: false);
            yield return WaitFor(() => field.IsCutsceneRunning, 5f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
            {
                Assert.IsFalse(field.Bubble.IsVisible, "말풍선 없음: " + field.Bubble.Text);
                yield return null;
            }
            Assert.IsTrue(field.RaccoonBusy, "그동안 춤추고 도망 중");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 중간에 나갔다 오면 그다음 자리부터 (잡은 골드는 그대로)
        [UnityTest]
        public IEnumerator LeavingHalfwayContinuesFromTheNextSpot()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(100, 10, 10)), 5);
            GoldRaccoon.Catch(area, session, Day); // 오늘 하나 잡음
            int gold = session.Inventory.Gold;
            var field = Field(area, session, Day);
            yield return null;
            yield return WaitFor(() => field.RaccoonCell == Spots[1] && !field.RaccoonBusy && !field.IsCutsceneRunning, 5f);
            Assert.AreEqual(Spots[1], field.RaccoonCell, "다시 들어와도 춤추고 두 번째 자리로");
            Assert.AreEqual(150, GoldRaccoon.NextGold(area, session.World, Day));
            Assert.AreEqual(gold, session.Inventory.Gold);
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
