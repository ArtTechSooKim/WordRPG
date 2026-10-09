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
    // 필드 통합 테스트: 실제 가상 스틱을 끌어 걷고, [확인]으로 상자·샘 사용, 풀숲 조우·전투 복귀까지 확인
    public class FieldScreenTests
    {
        //   y=4  #####
        //   y=3  #,,,#     ← 풀숲 (조우 확률 100%)
        //   y=2  #.C.#     ← 보물상자 (2,2)
        //   y=1  #FP.#     ← 회복의 샘 (1,1), 시작 (2,1)
        //   y=0  #####
        private const string TestMap = "#####\n#,,,#\n#.C.#\n#FP.#\n#####";

        private ItemData ink;

        private FieldScreen CreateField(GameSession session, MonsterSpecies enemy, string map = TestMap)
        {
            ink = TestData.Item("shiny_ink").Set("displayName", "빛나는 잉크");
            var table = ScriptableObject.CreateInstance<EncounterTable>()
                .Set("entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) })
                .Set("minGroupSize", 1).Set("maxGroupSize", 1);
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "test").Set("regionName", "테스트");
            words.ReplaceWords(TestData.SampleWords());
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "test").Set("displayName", "테스트 들판").Set("map", map)
                .Set("encounters", table).Set("words", words)
                .Set("encounterRate", 1f).Set("minStepsBetweenEncounters", 0)
                .Set("chests", new List<ChestContent> { new ChestContent(ink, 2) });

            var go = new GameObject("FieldUnderTest");
            var field = go.AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f);
            return field;
        }

        private static HeroData Hero(int hp, int atk)
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            return TestData.Hero(new MonsterStats(hp, atk, 10)).Set("basicSkill", strike);
        }

        private static MonsterSpecies Enemy(int hp, int atk, int power)
        {
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, power);
            return TestData.Species("enemy", new MonsterStats(hp, atk, 50), new MonsterStats(0, 0, 0), bite);
        }

        [UnityTest]
        public IEnumerator ChestFountainEncounterAndReturnToField()
        {
            var session = GameSession.NewGame(Hero(100, 30), 1);
            var field = CreateField(session, Enemy(40, 1, 5));
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector2Int(2, 1), field.PlayerCell);

            // 1) 위 = 보물상자. 부딪히기만 해서는 열리지 않고 [확인]을 눌러야 열림 → 빛나는 잉크 2개, 자리는 그대로
            yield return FaceStick(field, Direction.Up);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(0, session.Inventory.GetCount(ink), "부딪히기만 하면 안 열림");
            Assert.IsTrue(field.ConfirmReady, "옆에 상자가 있으면 확인 버튼이 금색");
            yield return PressConfirm(field);
            yield return WaitFor(() => session.Inventory.GetCount(ink) > 0, 2f);
            Assert.AreEqual(2, session.Inventory.GetCount(ink));
            StringAssert.Contains("빛나는 잉크", field.ToastMessage);
            Assert.AreEqual(new Vector2Int(2, 1), field.PlayerCell);
            CollectionAssert.Contains(field.VisibleNameTags, "회복의 샘", "가까이 있는 샘에 이름표");

            // 2) 왼쪽 = 회복의 샘 → 그쪽을 보고 [확인] → HP 회복
            session.Hero.TakeDamage(50);
            yield return new WaitForSecondsRealtime(0.35f); // 연속 사용 대기 시간
            yield return FaceStick(field, Direction.Left);
            yield return PressConfirm(field);
            yield return WaitFor(() => session.Hero.CurrentHp == session.Hero.Stats.MaxHp, 2f);
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp);
            StringAssert.Contains("회복의 샘", field.ToastMessage);

            // 3) 오른쪽 → 위 → 위(풀숲) = 조우
            yield return HoldStick(field, Direction.Right, () => field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            Assert.AreEqual(new Vector2Int(3, 2), field.PlayerCell);
            Assert.IsFalse(field.IsInBattle, "길에서는 조우하지 않음");
            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return WaitFor(() => field.Battle.IsRunning);
            Assert.IsTrue(field.Battle.IsRunning, "풀숲에 들어서면 전투");

            // 4) 전투 승리 → '계속 탐험' → 같은 자리로 복귀
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            Assert.AreEqual("승리!", field.Battle.ResultTitle);
            StringAssert.Contains("계속 탐험", AllText(FindButton(field.Battle.transform, "ResultButton_Primary")));
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);

            Assert.IsFalse(field.IsInBattle);
            Assert.IsFalse(field.Battle.IsRunning);
            Assert.AreEqual(new Vector2Int(3, 3), field.PlayerCell);
            Assert.AreEqual(1, session.Record.BattlesWon);
            Assert.IsTrue(session.World.TryGetPosition("test", out var saved));
            Assert.AreEqual(new Vector2Int(3, 3), saved, "위치가 세이브 상태에 기록됨");
            Assert.IsTrue(session.World.IsChestOpened("test:2,2"));

            // 5) 다시 움직일 수 있다
            yield return HoldStick(field, Direction.Left, () => field.IsMoving);
            Assert.AreEqual(new Vector2Int(2, 3), field.PlayerCell);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        // 스틱 (#34): 계속 밀면 서지 않고 여러 칸, 끝까지 밀면 달리기(금색 '달리기'), 누른 자리로 받침이 옮겨 옴,
        // [확인]은 왼쪽 아래·스틱은 오른쪽 아래
        [UnityTest]
        public IEnumerator StickWalksRunsAndConfirmIsOnTheLeft()
        {
            //   y=1  #P.....C#   ← 긴 길 (끝에 보물상자)
            const string corridor = "#########\n#P.....C#\n#########";
            var session = GameSession.NewGame(Hero(100, 30), 1);
            var field = CreateField(session, Enemy(40, 1, 5), corridor);
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector2Int(1, 1), field.PlayerCell);

            // 자리 배치: [확인]은 화면 왼쪽, 스틱 영역은 오른쪽
            var confirm = FindButton(field.transform, "Pad_Confirm");
            Assert.Less(RectTransformUtility.WorldToScreenPoint(null, confirm.transform.position).x, Screen.width * 0.4f);
            Assert.GreaterOrEqual(RectTransformUtility.WorldToScreenPoint(null, field.Stick.Zone.position).x, Screen.width * 0.4f - 1f,
                "스틱 영역은 오른쪽 (영역 왼쪽 아래 모서리가 화면 40% 지점부터)");

            // 살짝 밀고 있으면: 걷기로 계속 이동 (한 번 밀어서 3칸)
            bool sawRun = false, sawRunPose = false;
            var runPose = PlayerArt.GetRun(Direction.Right, 1); // 점프 자세 (달릴 때만)
            yield return HoldStick(field, Direction.Right, () =>
            {
                sawRun |= field.IsRunning;
                sawRunPose |= field.PlayerSprite == runPose;
                return field.PlayerCell.x >= 4;
            });
            Assert.GreaterOrEqual(field.PlayerCell.x, 4, "손을 떼기 전까지 칸마다 서지 않고 계속 감");
            Assert.IsFalse(sawRun, "살짝 밀면 걷기");
            Assert.IsFalse(sawRunPose, "걸을 때는 걷기 그림만");
            Assert.AreEqual(0, field.DustPuffs, "걸을 때는 먼지 없음");

            // 끝까지 밀면: 달리기 + 스틱에 '달리기'
            bool sawLabel = false;
            sawRun = false;
            runPose = PlayerArt.GetRun(Direction.Left, 1);
            yield return HoldStick(field, Direction.Left, () =>
            {
                sawRun |= field.IsRunning;
                sawRunPose |= field.PlayerSprite == runPose;
                sawLabel |= field.Stick.transform.Find("Stick/RunLabel").gameObject.activeSelf;
                return field.PlayerCell.x <= 1;
            }, run: true);
            Assert.AreEqual(new Vector2Int(1, 1), field.PlayerCell);
            Assert.IsTrue(sawRun, "끝까지 밀면 달리기");
            Assert.IsTrue(sawRunPose, "달릴 때는 달리기 그림(점프 자세가 섞임)");
            Assert.Greater(field.DustPuffs, 0, "달리면 발밑 먼지");
            Assert.IsTrue(sawLabel, "달리는 동안 스틱에 '달리기'");
            Assert.IsFalse(field.Stick.transform.Find("Stick/RunLabel").gameObject.activeSelf, "손을 떼면 사라짐");

            // 영역 안 다른 곳을 누르면 받침이 그 자리로 옮겨 오고, 떼면 제자리로
            var zone = field.Stick.Zone;
            var stickRoot = (RectTransform)field.Stick.transform.Find("Stick");
            var home = stickRoot.anchoredPosition;
            var touch = new Vector2(zone.rect.width * 0.72f, zone.rect.height * 0.5f);
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                pointerId = 2,
                position = RectTransformUtility.WorldToScreenPoint(null, zone.TransformPoint(touch))
            };
            UnityEngine.EventSystems.ExecuteEvents.Execute(field.Stick.gameObject, data,
                UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            Assert.AreEqual(touch.x, stickRoot.anchoredPosition.x, 1f, "누른 자리에 스틱");
            Assert.AreEqual(Vector2.zero, field.Stick.Value, "누르기만 하면 움직이지 않음");
            UnityEngine.EventSystems.ExecuteEvents.Execute(field.Stick.gameObject, data,
                UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            Assert.AreEqual(home, stickRoot.anchoredPosition, "떼면 제자리");
            yield return null;
            Assert.AreEqual(new Vector2Int(1, 1), field.PlayerCell);

            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnsAtSavedPositionAndDefeatReturnsToFountain()
        {
            var session = GameSession.NewGame(Hero(10, 1), 1);
            session.World.SetPosition("test", new Vector2Int(3, 2));
            var field = CreateField(session, Enemy(500, 50, 500));
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector2Int(3, 2), field.PlayerCell, "저장된 위치에서 시작");

            yield return HoldStick(field, Direction.Up, () => field.IsMoving);
            yield return WaitFor(() => field.Battle.IsRunning);
            yield return PlayUntilResult(field.Battle, answerCorrectly: false);
            Assert.AreEqual("패배…", field.Battle.ResultTitle);

            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => !field.IsInBattle);

            Assert.AreEqual(new Vector2Int(2, 1), field.PlayerCell, "시작 위치(회복의 샘 앞)로 돌아감");
            Assert.AreEqual(1, field.RespawnsPlayed, "빛과 함께 다시 일어나는 연출");
            Assert.IsTrue(field.SawRespawnPose, "쓰러짐 → 웅크림 → 서기 (#51)");
            Assert.AreEqual(PlayerArt.Get(Direction.Down, 0), field.PlayerSprite, "다 일어나면 정면으로 서 있음");
            var playerRenderer = field.transform.Find("Player").GetComponent<SpriteRenderer>();
            Assert.AreEqual(1f, playerRenderer.color.a, "연출이 끝나면 주인공이 또렷하게");
            Assert.AreEqual(Vector3.one, playerRenderer.transform.localScale);
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp, "HP 회복");
            StringAssert.Contains("돌아왔다", field.ToastMessage);
            Assert.AreEqual(1, session.Record.BattlesLost);

            // 다시 풀숲으로 → 다음 전투는 HP가 가득인 채로 정상 시작 (사용자 기기 버그 제보 2026-10-09: 죽은 뒤 다음 전투가 바로 패배)
            yield return HoldStick(field, Direction.Right, () => field.IsMoving); // 위는 보물상자라 오른쪽으로 돌아서
            yield return WaitFor(() => !field.IsMoving);
            yield return HoldStick(field, Direction.Up, () => field.PlayerCell.y >= 3 || field.Battle.IsRunning);
            yield return WaitFor(() => field.Battle.IsRunning);
            Assert.IsTrue(field.Battle.IsRunning, "다시 조우");
            yield return WaitFor(() => field.Battle.Engine.Phase == WordRPG.Battle.BattlePhase.ChoosingSkill && !field.Battle.IsResultVisible, 3f);
            var heroUnit = field.Battle.Engine.Party[0];
            Assert.AreEqual(heroUnit.MaxHp, heroUnit.Hp, "새 전투는 HP 가득");
            Assert.IsFalse(field.Battle.IsResultVisible, "바로 결과(패배)가 뜨면 안 됨");
            Assert.AreEqual(WordRPG.Battle.BattlePhase.ChoosingSkill, field.Battle.Engine.Phase);

            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
