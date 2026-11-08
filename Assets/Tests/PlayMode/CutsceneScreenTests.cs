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
    // 인물 컷씬 (#234 사용자 ⑲ — 숲 나무꾼): 처음 들어오면 주인공이 다섯 칸 걸어 들어가고 인물이 달려와 알려 줌 /
    // 보스를 이기면 인물이 달려와 고맙다 → 보스가 연 길 옆으로 → 주인공도 옆으로 → 이야기 → 제자리로 → 그다음 돌아가기 묻기
    public class CutsceneScreenTests
    {
        private static NpcData Woodcutter()
        {
            var npc = ScriptableObject.CreateInstance<NpcData>();
            npc.Configure("npc_woodcutter", "나무꾼", 0, new[] { new NpcStage("길을 내가 잃었다…", NpcMotionKind.Stand) });
            npc.SetCutscenes(new[] { "이보게, 거기!", "헷갈너구리가 이정표를 뒤섞었네." },
                new[] { "이정표가 돌아왔군!" }, new[] { "이 너머로 갈 생각인가?" }, new[] { "행운을 빌겠네!" });
            return npc;
        }

        private static WordDatabase Words()
        {
            var words = ScriptableObject.CreateInstance<WordDatabase>().Set("regionId", "cut").Set("regionName", "숲");
            words.ReplaceWords(TestData.SampleWords());
            return words;
        }

        private static GameSession Session()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 60);
            return GameSession.NewGame(TestData.Hero(new MonsterStats(300, 60, 10)).Set("basicSkill", strike), 5);
        }

        //   y=1  #P......N#   시작 (1,1), 나무꾼 (8,1)
        [UnityTest]
        public IEnumerator OnFirstEntryTheWoodcutterRunsOverToBrief()
        {
            var npc = Woodcutter();
            var area = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "woods").Set("displayName", "숲").Set("map", "##########\n#P......N#\n##########")
                .Set("words", Words()).Set("npcs", new List<NpcData> { npc });
            var session = Session();
            var field = new GameObject("CutsceneField").AddComponent<FieldScreen>();
            field.Configure(area, session, step: 0.05f, animScale: 0.01f);
            yield return WaitFor(() => field.IsCutsceneRunning, 2f);
            Assert.IsTrue(field.IsCutsceneRunning, "들어오자마자 컷씬");
            Assert.IsTrue(field.IsPanelOpen || field.IsCutsceneRunning, "입력은 막힘");
            bool spoke = false;
            for (float t = 0; t < 5f && field.IsCutsceneRunning; t += Time.unscaledDeltaTime)
            {
                if (field.Bubble.IsVisible && field.Bubble.FromNpc) spoke = true;
                yield return null;
            }
            Assert.IsFalse(field.IsCutsceneRunning, "끝남");
            Assert.IsTrue(spoke, "나무꾼이 말함");
            Assert.AreEqual(new Vector2Int(6, 1), field.PlayerCell, "주인공은 다섯 칸 걸어 들어감");
            Assert.AreEqual(new Vector2Int(8, 1), field.Npcs.Views[0].Motion.Cell, "나무꾼은 제자리로");
            Assert.IsTrue(session.World.HasEvent(FieldScreen.EntryEventId(npc)), "한 번만");
            Object.Destroy(field.gameObject);
            yield return null;

            var again = new GameObject("CutsceneField2").AddComponent<FieldScreen>();
            again.Configure(area, session, step: 0.05f, animScale: 0.01f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0, again.CutscenesPlayed, "다시 들어와도 안 나옴");
            Object.Destroy(again.gameObject);
            yield return null;
        }

        // #236 사용자 ㉑: 서고 사서가 가까이(2칸) 오면 까먹대왕을 조심하라며 상처약 하나 — 한 번만, 보스를 물리친 뒤엔 없음
        //   y=1  #P.N#   시작 (1,1), 사서 (3,1)
        [UnityTest]
        public IEnumerator TheLibrarianHandsOverAPotionOnce()
        {
            var potion = TestData.Item("potion").Set("displayName", "상처약");
            var npc = ScriptableObject.CreateInstance<NpcData>();
            npc.Configure("npc_librarian", "사서", 0, new[] { new NpcStage("쉿.", NpcMotionKind.Stand) });
            var library = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "lib_t").Set("displayName", "서고").Set("map", "#####\n#P.N#\n#####")
                .Set("words", Words()).Set("npcs", new List<NpcData> { npc });
            npc.SetGift(potion, 1, library, "까먹대왕을 물리치러 가요?", "이거 가져가요.");
            var session = Session();
            var field = new GameObject("GiftField").AddComponent<FieldScreen>();
            field.Configure(library, session, step: 0.05f, animScale: 0.01f);
            yield return WaitFor(() => session.Inventory.GetCount(potion) > 0, 3f);
            Assert.AreEqual(1, session.Inventory.GetCount(potion), "상처약 하나");
            Assert.IsTrue(session.World.HasEvent(FieldScreen.GiftEventId(npc)));
            StringAssert.Contains("상처약을 받았다", field.ToastMessage);
            yield return WaitFor(() => !field.IsCutsceneRunning, 2f);
            Object.Destroy(field.gameObject);
            yield return null;

            var again = new GameObject("GiftField2").AddComponent<FieldScreen>();
            again.Configure(library, session, step: 0.05f, animScale: 0.01f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1, session.Inventory.GetCount(potion), "한 번만");
            Object.Destroy(again.gameObject);
            yield return null;
        }

        //   y=5  #####D####   정글 쪽 길 (5,5) — 보스가 염
        //   y=4  #........#
        //   y=3  #....B...#   보스 (5,3)
        //   y=2  #....P...#   시작 (5,2)
        //   y=1  #.......N#   나무꾼 (8,1)
        [UnityTest]
        public IEnumerator AfterTheBossTheWoodcutterSeesTheHeroOffAtTheNewPath()
        {
            var npc = Woodcutter();
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var raccoon = TestData.Species("raccoon", new MonsterStats(20, 1, 1), new MonsterStats(0, 0, 0), bite).Set("displayName", "헷갈너구리");
            var jungle = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "jungle_t").Set("displayName", "정글")
                .Set("map", "#####\n#.D.#\n#.P.#\n#####").Set("words", Words());
            var woods = ScriptableObject.CreateInstance<FieldArea>()
                .Set("areaId", "woods_b").Set("displayName", "숲").Set("words", Words())
                .Set("map", "##########\n#####D####\n#........#\n#....B...#\n#....P...#\n#.......N#\n##########")
                .Set("boss", new BossEncounter(raccoon, 3)).Set("npcs", new List<NpcData> { npc });
            woods.Set("exits", new List<AreaExit> { new AreaExit(jungle, 0, woods) });
            jungle.Set("exits", new List<AreaExit> { new AreaExit(woods, 0) });
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { raccoon }, new Items.ItemData[0], new[] { woods, jungle });
            var session = Session();
            session.World.MarkEvent(FieldScreen.EntryEventId(npc)); // 들어올 때 컷씬은 이미 봄
            var field = new GameObject("BossCutsceneField").AddComponent<FieldScreen>();
            field.Configure(woods, session, step: 0.05f, animScale: 0.01f, gameDatabase: database);
            yield return null;
            yield return null;

            yield return FaceStick(field, Direction.Up);
            yield return PressConfirm(field);
            yield return WaitFor(() => field.Battle.IsRunning, 3f);
            yield return PlayUntilResult(field.Battle, answerCorrectly: true);
            FindButton(field.Battle.transform, "ResultButton_Primary").onClick.Invoke();
            yield return WaitFor(() => field.IsCutsceneRunning, 8f);
            Assert.IsTrue(field.IsCutsceneRunning, "보스를 이기면 나무꾼 컷씬");
            Assert.IsFalse(field.IsReturnDialogOpen, "돌아가기 묻기는 컷씬 뒤");
            yield return WaitFor(() => !field.IsCutsceneRunning, 10f);
            Assert.AreEqual(new Vector2Int(6, 4), field.PlayerCell, "주인공은 길 옆으로");
            Assert.AreEqual(new Vector2Int(8, 1), field.Npcs.Views[0].Motion.Cell, "나무꾼은 제자리로");
            Assert.IsTrue(session.World.HasEvent(FieldScreen.BossEventId(npc)));
            yield return WaitFor(() => field.IsReturnDialogOpen, 5f);
            Assert.IsTrue(field.IsReturnDialogOpen, "그다음 돌아가기 묻기");
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
