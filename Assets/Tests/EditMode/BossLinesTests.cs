using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Field;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 보스 대사 (#207): 스토리 문서의 대사가 게임에 들어가 있는지
    public class BossLinesTests
    {
        private static BossEncounter Boss() =>
            new BossEncounter(TestData.Species("king", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0),
                TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1)).Set("displayName", "까먹대왕"), 7);

        [Test]
        public void ABossWithoutLinesKeepsTheOldIntro()
        {
            StringAssert.Contains("보스 출현! 까먹대왕 Lv7", BossLines.Intro(Boss(), "주인공"));
        }

        [Test]
        public void TheIntroIsTheBossLineThenTheHeroLineWithThePlayersName()
        {
            var boss = Boss().Set("bossLine", "잊어버리면… 아플 일도 없어!").Set("heroLine", "기억을 돌려줘!");
            Assert.AreEqual("까먹대왕: \"잊어버리면… 아플 일도 없어!\"\n수연: \"기억을 돌려줘!\"", BossLines.Intro(boss, "수연"));
        }

        [Test]
        public void EveryRealBossSpeaksAndLeavesALine()
        {
            var areas = AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data/Areas" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(area => area.Boss != null && area.Boss.Species != null).ToList();
            Assert.GreaterOrEqual(areas.Count, 4);
            foreach (var area in areas)
            {
                Assert.IsNotEmpty(area.Boss.BossLine, $"{area.name}: 보스 대사");
                Assert.IsNotEmpty(area.Boss.HeroLine, $"{area.name}: 주인공 대사");
                Assert.IsNotEmpty(area.Boss.AfterLine, $"{area.name}: 승리 뒤 글");
            }
        }
    }
}
