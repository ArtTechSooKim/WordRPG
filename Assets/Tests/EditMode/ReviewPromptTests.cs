using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Heroes;

namespace WordRPG.Tests
{
    // 후기 (#252·#254 사용자: 동굴 보스를 물리친 뒤 애플 기본 창 + 설정의 링크)
    public class ReviewPromptTests
    {
        private static GameDatabase Database() => AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");

        [Test]
        public void OnlyTheCaveAsksForAReview()
        {
            foreach (var area in Database().Areas)
                Assert.AreEqual(area.AreaId == "cave", area.AsksReviewAfterBoss, area.AreaId);
        }

        [Test]
        public void AsksOncePerSave()
        {
            var cave = Database().Areas.First(a => a.AreaId == "cave");
            var session = GameSession.NewGame(AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset"), 1);
            Assert.IsTrue(ReviewPrompt.Due(cave, session));
            session.World.MarkEvent(ReviewPrompt.EventId);
            Assert.IsFalse(ReviewPrompt.Due(cave, session), "한 번만");
        }

        [Test]
        public void LinkGoesToTheStoreOfThePhone()
        {
            Assert.AreEqual("https://apps.apple.com/app/id6818805727?action=write-review", ReviewPrompt.Url(RuntimePlatform.IPhonePlayer));
            Assert.AreEqual("https://play.google.com/store/apps/details?id=com.arttechsoo.wordrpg", ReviewPrompt.Url(RuntimePlatform.Android));
        }
    }
}
