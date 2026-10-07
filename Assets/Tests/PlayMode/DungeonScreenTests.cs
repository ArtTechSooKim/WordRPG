using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
    // 지역 이동(출입구)과 보스전 통합 테스트 — 실제 가상 패드로 걷는다
    public class DungeonScreenTests
    {
        //  A (바깥)   y=3 #D#   ← 출입구 (1,3)
        //             y=2 #.#
        //             y=1 #P#
        //             y=0 ###
        private const string OutsideMap = "#D#\n#.#\n#P#\n###";

        //  B (안쪽)   y=3 ###
        //             y=2 #P#
        //             y=1 #.#
        //             y=0 #D#   ← 출입구 (1,0)
        private const string InsideMap = "###\n#P#\n#.#\n#D#";

        private static WordDatabase Words()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            return words;
        }

        // 지역의 적으로 쓰는 몬스터 (조우 확률 0이라 실제로는 안 나옴)
        private static MonsterSpecies Hero()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            return TestData.Species("hero", new MonsterStats(100, 30, 10), new MonsterStats(0, 0, 0), strike);
        }

        // 주인공 (기본 기술 공격 40)
        private static HeroData Player()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            return TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike);
        }

        private static FieldArea Area(string id, string name, string map, MonsterSpecies enemy)
        {
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) });
            return ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", id).Set("displayName", name).Set("map", map)
                .Set("encounters", table).Set("words", Words());
        }

        private static (FieldArea outside, FieldArea inside) LinkedAreas(MonsterSpecies enemy)
        {
            var outside = Area("outside", "바깥 들판", OutsideMap, enemy);
            var inside = Area("inside", "안쪽 서고", InsideMap, enemy).Set("theme", FieldTheme.Library);
            outside.Set("exits", new List<AreaExit> { new AreaExit(inside, 0) });
            inside.Set("exits", new List<AreaExit> { new AreaExit(outside, 0) });
            return (outside, inside);
        }

        private static FieldScreen CreateField(FieldArea area, GameSession session, GameDatabase database = null)
        {
            var go = new GameObject("DungeonFieldUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            return field;
        }

        [UnityTest]
        public IEnumerator DoorsMoveBetweenAreasAndBack()
        {
            var hero = Hero();
            var (outside, inside) = LinkedAreas(hero);
            var session = GameSession.NewGame(Player(), 1);
            var field = CreateField(outside, session);
            yield return null;
            yield return null;

            CollectionAssert.Contains(field.VisibleNameTags, "안쪽 서고", "출입구 위에 도착 지역 이름표");

            // 위, 위 → 출입구를 밟으면 안쪽 서고의 출입구 칸에 나타남
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return WaitFor(() => field.CurrentArea == inside && !field.IsInBattle);

            Assert.AreSame(inside, field.CurrentArea);
            Assert.AreEqual(new Vector2Int(1, 0), field.PlayerCell);
            Assert.AreEqual("inside", session.World.AreaId, "현재 지역이 세이브 상태에 기록됨");
            StringAssert.Contains("안쪽 서고", AllText(field.transform.Find("FieldHud/SafeArea/TopBar")));
            StringAssert.Contains("안쪽 서고", field.ToastMessage);

            // 도착하자마자 되돌아가지 않음
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreSame(inside, field.CurrentArea);

            // 위로 한 칸 갔다가 다시 아래 출입구 → 바깥 들판의 출입구로
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return HoldStick(field, Direction.Down, () => field.IsMoving);
            yield return WaitFor(() => field.CurrentArea == outside && !field.IsInBattle);

            Assert.AreSame(outside, field.CurrentArea);
            Assert.AreEqual(new Vector2Int(1, 3), field.PlayerCell);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossIsFoughtOnceAndRemembered()
        {
            var hero = Hero();
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var king = TestData.Species("king", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), bite)
                .Set("displayName", "까먹대왕").Set("expReward", 10);
            var crown = TestData.Relic("relic_grail", TestData.Skill("recall", SkillKind.Heal, SkillTarget.Self, 16),
                new MonsterStats(20, 0, 2)).Set("displayName", "기억의 성배");
            var area = Area("lair", "보스 방", "#####\n#.B.#\n#.P.#\n#####", hero)
                .Set("boss", new BossEncounter(king, 3, crown));
            var session = GameSession.NewGame(Player(), 1);
            var field = CreateField(area, session);
            yield return null;
            yield return null;

            CollectionAssert.Contains(field.VisibleNameTags, "까먹대왕", "보스 위에 보스 이름표");

            // 위 = 보스 → [확인]으로 도전 → 보스전
            yield return FaceStick(field, Direction.Up);
            Assert.IsFalse(field.IsInBattle, "부딪히기만 해서는 싸우지 않음");
            yield return PressConfirm(field);
            yield return WaitFor(() => field.Battle.IsRunning);
            Assert.IsTrue(field.Battle.IsRunning);
            Assert.AreSame(king, field.Battle.Engine.Enemies[0].Monster.Species);
            Assert.AreEqual(3, field.Battle.Engine.Enemies[0].Monster.Level);
            StringAssert.Contains("보스 출현", AllText(field.Battle));

            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            // 보스를 쓰러뜨린 한 방은 결정타 연출 (#41), 결산에 물리친 보스 이름
            Assert.AreEqual(1, field.Battle.FinishersPlayed);
            StringAssert.Contains("까먹대왕을 물리쳤어요", AllText(field.Battle.transform.Find("BattleCanvas/SafeArea/ResultPanel")));
            Assert.IsFalse(field.Battle.transform.Find("BattleCanvas/SafeArea/FinisherShade").gameObject.activeSelf, "결산 전에 어두운 막은 걷힘");
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);

            Assert.IsTrue(session.World.IsBossDefeated("lair:boss"));
            StringAssert.Contains("까먹대왕을 물리쳤다", field.ToastMessage);
            StringAssert.Contains("기억의 성배", field.ToastMessage, "보스 보상 성유물");
            Assert.IsTrue(session.Hero.Owns(crown));
            Assert.IsTrue(session.Hero.IsEquipped(session.Hero.Find(crown)), "빈 칸이 있으면 바로 끼움");

            CollectionAssert.DoesNotContain(field.VisibleNameTags, "까먹대왕", "쓰러뜨린 보스는 이름표도 사라짐");

            // 다시 [확인]을 눌러도 전투 없음
            yield return new WaitForSecondsRealtime(0.7f);
            yield return PressConfirm(field);
            yield return WaitFor(() => field.ToastMessage.Contains("있던 자리"), 2f);
            Assert.IsFalse(field.IsInBattle);
            StringAssert.Contains("까먹대왕이 있던 자리", field.ToastMessage);
            Assert.AreEqual(1, session.Record.BattlesWon);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossOpensLockedGateAndCameraShowsIt()
        {
            var hero = Hero();
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var king = TestData.Species("king", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), bite)
                .Set("displayName", "까먹대왕");
            //  마을  y=3 ##D##  ← 숲 (보스 방의 보스를 물리쳐야 열림)      보스 방  y=3 #####
            //        y=2 #...#                                           y=2 #.B.#
            //        y=1 #.P.#                                           y=1 #.P.#
            //        y=0 ##D##  ← 보스 방                                y=0 ##D##
            var town = Area("town", "마을", "##D##\n#...#\n#.P.#\n##D##", hero);
            var forest = Area("forest", "숲", "###\n#P#\n#D#", hero).Set("theme", FieldTheme.Forest);
            var lair = Area("lair", "보스 방", "#####\n#.B.#\n#.P.#\n##D##", hero).Set("theme", FieldTheme.Library)
                .Set("boss", new BossEncounter(king, 3));
            town.Set("exits", new List<AreaExit> { new AreaExit(forest, 0, lair), new AreaExit(lair, 0) });
            forest.Set("exits", new List<AreaExit> { new AreaExit(town, 0) });
            lair.Set("exits", new List<AreaExit> { new AreaExit(town, 1) });
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { hero, king }, new ItemData[0], new[] { town, forest, lair });
            var session = GameSession.NewGame(Player(), 1);
            var field = CreateField(town, session, database);
            yield return null;
            yield return null;

            CollectionAssert.Contains(field.VisibleNameTags, "숲 · 잠김", "잠긴 출입구 이름표");

            // 위로 한 칸 → 숲 출입구는 덤불로 막혀 있음
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return WaitFor(() => !field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.ToastMessage.Contains("막혀"));
            Assert.AreEqual(new Vector2Int(2, 2), field.PlayerCell, "잠긴 출입구는 못 지나감");
            StringAssert.Contains("보스 방의 까먹대왕을 물리치면", field.ToastMessage);
            Assert.AreSame(town, field.CurrentArea);

            // 아래로 → 보스 방 → 보스를 물리침
            yield return HoldStick(field, Direction.Down, () => field.IsMoving);
            yield return HoldStick(field, Direction.Down, () => field.IsMoving);
            yield return WaitFor(() => field.CurrentArea == lair && !field.IsInBattle);
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return WaitFor(() => !field.IsMoving);
            yield return FaceStick(field, Direction.Up);
            yield return PressConfirm(field);
            yield return WaitFor(() => field.Battle.IsRunning);
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();

            // 카메라가 마을의 숲 출입구로 가서 길이 열리는 것을 보여 주고 돌아옴
            yield return WaitFor(() => field.IsRevealingGate);
            Assert.IsTrue(field.IsInBattle, "연출 중에는 움직이거나 메뉴를 열 수 없음");
            yield return WaitFor(() => !field.IsInBattle && !field.IsRevealingGate);
            Assert.AreEqual(1, field.RevealedGates.Count);
            Assert.AreSame(town, field.RevealedGates[0].Area);
            Assert.AreEqual(new Vector2Int(2, 3), field.RevealedGates[0].Cell);
            Assert.LessOrEqual(Vector2.Distance(field.LastGateCamera, new Vector2(2.5f, 3.5f)), 3.2f, "길이 열릴 때 카메라는 출입구 근처를 비춤");
            StringAssert.Contains("숲으로 가는 길이 열렸다", field.LastGateBanner);
            Assert.AreSame(lair, field.CurrentArea, "연출이 끝나면 보스 방으로 돌아옴");
            Assert.AreEqual(new Vector2Int(2, 1), field.PlayerCell);
            Assert.Less(Vector2.Distance(field.CameraPosition, new Vector2(2.5f, 1.5f)), 3f, "카메라도 주인공에게 돌아옴");
            StringAssert.Contains("숲으로 가는 길이 열렸다", field.ToastMessage);
            Assert.IsTrue(session.World.IsExitOpen(town.Exits[0]));

            // 마을로 돌아가 열린 길로 숲에 들어감
            yield return HoldStick(field, Direction.Down, () => field.IsMoving);
            yield return WaitFor(() => field.CurrentArea == town && !field.IsInBattle);
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            CollectionAssert.Contains(field.VisibleNameTags, "숲", "열린 뒤에는 '잠김' 없이");
            for (int i = 0; i < 2; i++) yield return HoldStick(field, Direction.Up, () => field.IsMoving || field.CurrentArea == forest);
            yield return WaitFor(() => field.CurrentArea == forest && !field.IsInBattle);
            Assert.AreSame(forest, field.CurrentArea);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartsInTheAreaWhereTheGameWasSaved()
        {
            var hero = Hero();
            var (outside, inside) = LinkedAreas(hero);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { hero }, new ItemData[0], new[] { outside, inside });
            var session = GameSession.NewGame(Player(), 1);
            session.World.SetPosition("inside", new Vector2Int(1, 1));

            var field = CreateField(outside, session, database);
            yield return null;
            yield return null;

            Assert.AreSame(inside, field.CurrentArea, "세이브의 마지막 지역에서 시작");
            Assert.AreEqual(new Vector2Int(1, 1), field.PlayerCell);

            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
