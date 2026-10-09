using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 보스의 사전 (#45): 쉼터 받침대에서 [확인] → (잠김) / 점점점 → 새 단어 / 오늘은 그만 / 다음 날 또. 쓰러뜨린 보스 자리는 지나갈 수 있음
    public class DictionaryScreenTests
    {
        //  y=2  #B...##   보스 (1,2)
        //  y=1  #..L.P#   받침대 (3,1), 시작 (5,1)
        private const string Map = "#######\n#B...##\n#..L.P#\n#######";

        private static FieldArea Area()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var slime = TestData.Species("slime", new MonsterStats(20, 1, 1), new MonsterStats(0, 0, 0), bite);
            var king = TestData.Species("king", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), bite).Set("displayName", "헷갈너구리");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(slime, 1, 1, 1) });
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "camp_test").Set("displayName", "숲").Set("map", Map).Set("theme", FieldTheme.Forest)
                .Set("encounters", table).Set("words", words).Set("boss", new BossEncounter(king, 3)).Set("dictionaryName", "숲의 사전");
        }

        private static HeroData Player()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            return TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike);
        }

        [UnityTest]
        public IEnumerator ReadTheBossDictionaryOnceADay()
        {
            var area = Area();
            var session = GameSession.NewGame(Player(), 3);
            var go = new GameObject("DictionaryFieldUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f);
            var today = new DateTime(2026, 10, 7, 15, 0, 0, DateTimeKind.Local);
            field.LocalClock = () => today;
            yield return null;
            yield return null;

            CollectionAssert.Contains(field.VisibleNameTags, "숲의 사전", "받침대 이름표");

            // 왼쪽 = 받침대. 보스를 아직 안 이겼으면 비어 있음
            yield return HoldStick(field, Direction.Left, () => field.IsMoving);
            yield return FaceStick(field, Direction.Left);
            Assert.AreEqual(new Vector2Int(4, 1), field.PlayerCell);
            yield return PressConfirm(field);
            Assert.IsTrue(field.Bubble.ShowsText, "창이 아니라 주인공 머리 위 말풍선");
            StringAssert.Contains("빈 받침대다", field.Bubble.Text);
            StringAssert.Contains("헷갈너구리를 물리치면", field.Bubble.Text);
            Assert.IsFalse(field.IsPanelOpen);

            // 보스를 이겼다 → '.' '..' '...' '!' 풍선 → 새 단어 말풍선
            session.World.MarkBossDefeated(area.BossId);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PressConfirm(field);
            Assert.IsTrue(field.IsReadingDictionary, "점점점 동안은 움직일 수 없음");
            var icons = new List<SpeechBubble.Icon>();
            yield return WaitFor(() =>
            {
                var shown = field.Bubble.ShownIcon;
                if (shown.HasValue && (icons.Count == 0 || icons[icons.Count - 1] != shown.Value)) icons.Add(shown.Value);
                return field.Bubble.ShowsText;
            });
            // 확인 버튼 도우미가 두 프레임을 기다리는 동안 첫 '.'은 지나갈 수 있음 → 순서대로 '...' 다음 '!'로 끝나는지
            Assert.GreaterOrEqual(icons.Count, 3);
            for (int i = 1; i < icons.Count; i++) Assert.Less(icons[i - 1], icons[i], "점이 늘어나다가 느낌표");
            Assert.AreEqual(SpeechBubble.Icon.Exclaim, icons[icons.Count - 1]);
            CollectionAssert.Contains(icons, SpeechBubble.Icon.Dot3);
            Assert.IsFalse(field.IsPanelOpen, "창은 열리지 않음");
            Assert.AreEqual("새로운 단어를 발견했다!", field.Bubble.HeadText);
            var first = field.Bubble.MainText;
            var word = System.Linq.Enumerable.FirstOrDefault(area.Words.Words, w => w.English == first);
            Assert.IsNotNull(word, "그 맵 단어장의 단어");
            Assert.AreNotEqual(MasteryLevel.New, session.Vocabulary.GetLevel(word.Id), "도감에 등록");
            Assert.IsFalse(field.IsReadingDictionary);

            // 같은 날 또 → 혼잣말만
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PressConfirm(field);
            StringAssert.Contains("오늘 책은 충분히 읽은 것 같다", field.Bubble.Text);
            Assert.AreEqual(1, session.Vocabulary.DiscoveredCount);

            // 다음 날 → 또 한 단어. 뒤로가기로 말풍선 닫기
            today = today.AddDays(1);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PressConfirm(field);
            yield return WaitFor(() => field.Bubble.ShowsText && field.Bubble.HeadText.Length > 0);
            Assert.AreNotEqual(first, field.Bubble.MainText, "이미 발견한 단어는 다시 안 나옴");
            Assert.AreEqual(2, session.Vocabulary.DiscoveredCount);
            field.HandleBack();
            Assert.IsFalse(field.Bubble.IsVisible);

            // 다시 읽으면 혼잣말 → 걸으면 말풍선이 사라짐
            yield return new WaitForSecondsRealtime(0.6f);
            yield return PressConfirm(field);
            Assert.IsTrue(field.Bubble.IsVisible);
            // 쓰러뜨린 보스 자리는 빈자리 — 지나갈 수 있다
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            Assert.IsFalse(field.Bubble.IsVisible, "걷기 시작하면 말풍선은 사라짐");
            yield return HoldStick(field, Direction.Left, () => field.PlayerCell.x <= 1);
            Assert.AreEqual(new Vector2Int(1, 2), field.PlayerCell, "보스가 있던 칸에 설 수 있음");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }
    }
}
