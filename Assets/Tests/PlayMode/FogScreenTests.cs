#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.UI;
using Object = UnityEngine.Object;

namespace WordRPG.Tests
{
    // 서고 바닥 안개 (#103·#104): 실제 서고에 들어가면 안개 한 장이 깔리고, 구름 무늬가 천천히 흘러 다시 칠해진다. 초원엔 안개 없음
    public class FogScreenTests
    {
        private static FieldScreen Field(string areaId)
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            var session = GameSession.NewGame(hero, 8);
            var field = new GameObject("FogField").AddComponent<FieldScreen>();
            field.Configure(db.FindArea(areaId), session, () => { }, step: 0.05f, animScale: 0.01f, gameDatabase: db);
            return field;
        }

        [UnityTest]
        public IEnumerator LibraryFogDriftsSlowly()
        {
            var field = Field("library");
            yield return null;
            Assert.IsTrue(field.HasFog, "서고 몬스터 칸에 안개");
            int before = field.FogRepaints;
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(field.FogRepaints, before, "구름 무늬가 흘러감 (1초에 2~3번 다시 칠함)");
            Assert.LessOrEqual(field.FogRepaints - before, 6, "아주 살짝 — 매 프레임 다시 칠하지 않음");
            Object.Destroy(field.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MeadowHasNoFog()
        {
            var field = Field("meadow");
            yield return null;
            Assert.IsFalse(field.HasFog);
            Object.Destroy(field.gameObject);
            yield return null;
        }
    }
}
#endif
