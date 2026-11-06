using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.EditorTools;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Save;

namespace WordRPG.Tests
{
    // 어르신이 샘으로 뛰어듦 (#223): 보스를 물리친 뒤 한 번만, 그 뒤엔 다시 안 나옴, 세이브에 남음
    public class SpringLeapTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);

        private static NpcData Npc(string id, FieldArea leapAfter)
        {
            var npc = ScriptableObject.CreateInstance<NpcData>();
            npc.Configure(id, "어르신", 0, new[] { new NpcStage("……", NpcMotionKind.Stand) });
            if (leapAfter != null) npc.SetSpringLeap(leapAfter, "얍!");
            return npc;
        }

        [Test]
        public void NeedsTheBossAndHappensOnce()
        {
            var library = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "lib");
            var elder = Npc("npc_test_elder", library);
            var world = new WorldState();
            Assert.IsFalse(SpringLeap.Ready(elder, world), "보스 전에는 말이 안 돌아옴");
            world.MarkBossDefeated(library.BossId);
            Assert.IsTrue(SpringLeap.Ready(elder, world));
            Assert.IsFalse(SpringLeap.Gone(elder, world));
            Assert.IsFalse(SpringLeap.AnyoneLeapt(world));

            world.MarkEvent(SpringLeap.EventId(elder));
            Assert.IsFalse(SpringLeap.Ready(elder, world), "한 번만");
            Assert.IsTrue(SpringLeap.Gone(elder, world), "뛰어든 뒤엔 안 나옴");
            Assert.IsTrue(SpringLeap.AnyoneLeapt(world));

            var other = Npc("npc_test_other", null);
            Assert.IsFalse(SpringLeap.Ready(other, world), "뛰어들지 않는 인물");
            Assert.IsFalse(SpringLeap.Gone(other, world));
        }

        [Test]
        public void TheEventIsSavedAndOldSavesStartEmpty()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>(SampleDataBuilder.HeroPath);
            var session = GameSession.NewGame(hero, 3);
            session.World.MarkEvent("spring_leap:npc_elder");
            string json = JsonUtility.ToJson(session.ToSaveData(Now));
            Assert.IsTrue(GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(json), db, hero).World.HasEvent("spring_leap:npc_elder"));

            string old = json.Replace(",\"events\":[\"spring_leap:npc_elder\"]", "");
            Assert.AreNotEqual(json, old, "1.2 세이브에는 이 칸이 없음");
            Assert.IsFalse(GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(old), db, hero).World.HasEvent("spring_leap:npc_elder"));
        }

        [Test]
        public void OnlyTheMeadowElderLeapsAndHisVillageHasASpring()
        {
            var areas = AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data/Areas" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            var leapers = areas.SelectMany(area => area.Npcs.Where(npc => npc != null && npc.LeapsIntoSpring).Select(npc => (area, npc))).ToList();
            Assert.AreEqual(1, leapers.Count);
            var (village, elder) = leapers[0];
            Assert.AreEqual("meadow", village.AreaId);
            Assert.AreEqual("npc_elder", elder.NpcId);
            Assert.AreEqual("library", elder.SpringLeapAfter.AreaId, "서고 보스를 물리쳐 말이 돌아온 뒤");
            Assert.IsNotNull(SpringWarp.SpringOf(village.Map), "뛰어들 샘이 있음");
            Assert.GreaterOrEqual(elder.SpringLeapLines.Count, 2);
            Assert.IsTrue(elder.SpringLeapLines.All(line => !string.IsNullOrWhiteSpace(line)));
        }
    }
}
