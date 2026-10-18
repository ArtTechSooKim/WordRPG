using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 1.2 어두운 밀림 동굴 (#112, GDD 9-7): 글자 반딧돌 'I' · 동굴 테마 · 빛(주인공 3.5칸 + 횃불 1칸 · 반딧돌 6칸 · 쉼터 4칸 · 지나간 곳 희미하게)
    public class CaveTests
    {
        //  y=4  ##########
        //  y=3  #I::,,:::#      반딧돌 (1,3)
        //  y=2  #::~~::F:#      먹물 웅덩이, 샘 (7,2)
        //  y=1  #P:::YHH:#      시작 (1,1), 레버 (5,1), 덩굴 다리
        //  y=0  ####D#####      출입구 (4,0)
        private const string Map =
            "##########\n" +
            "#I::,,:::#\n" +
            "#::~~::F:#\n" +
            "#P:::YHH:#\n" +
            "####D#####";

        private static FieldArea Area() =>
            ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "cave").Set("map", Map).Set("theme", FieldTheme.Cave);

        [Test]
        public void ParsesTheGlowStone()
        {
            var map = FieldMap.Parse(Map);
            Assert.AreEqual(FieldTile.GlowStone, map.Get(new Vector2Int(1, 3)));
            Assert.IsFalse(FieldMap.IsWalkable(FieldTile.GlowStone), "반딧돌은 막힘");
            Assert.IsTrue(FieldMap.IsInteractive(FieldTile.GlowStone), "옆에서 [확인]");
            Assert.IsTrue(FieldInteraction.HasNameTag(FieldTile.GlowStone));
            Assert.AreEqual("글자 반딧돌", Area().LandmarkName(new Vector2Int(1, 3)));
        }

        [Test]
        public void CaveThemeUsesGlowStonesLeversBridgesAndDecor()
        {
            foreach (var tile in new[] { FieldTile.GlowStone, FieldTile.Lever, FieldTile.Bridge, FieldTile.Decor, FieldTile.Chest, FieldTile.Water })
                Assert.IsTrue(FieldThemes.Supports(FieldTheme.Cave, tile), $"동굴: {tile}");
            foreach (var tile in new[] { FieldTile.Stream, FieldTile.Dock, FieldTile.Tablet, FieldTile.Shrine, FieldTile.RuinFloor })
                Assert.IsFalse(FieldThemes.Supports(FieldTheme.Cave, tile), $"동굴엔 정글 칸 {tile} 없음");
            Assert.IsFalse(FieldThemes.Supports(FieldTheme.Jungle, FieldTile.GlowStone), "반딧돌은 동굴에서만");
            Assert.IsFalse(FieldThemes.Supports(FieldTheme.Meadow, FieldTile.GlowStone));
            Assert.IsTrue(FieldDarkness.IsDark(FieldTheme.Cave));
            Assert.IsFalse(FieldDarkness.IsDark(FieldTheme.Jungle), "어두운 곳은 동굴만");
        }

        [Test]
        public void LitStoneIsSavedAndDrawnLit()
        {
            var area = Area();
            var session = GameSession.NewGame(TestData.Hero(new Monsters.MonsterStats(50, 10, 10)), 5);
            var stone = new Vector2Int(1, 3);
            Assert.IsFalse(GlowStones.IsLit(area, session.World, stone));
            Assert.IsFalse(CellState.IsDone(area, session, stone));
            Assert.IsTrue(GlowStones.Light(area, session.World, stone), "처음 밝힘");
            Assert.IsFalse(GlowStones.Light(area, session.World, stone), "두 번은 안 됨");
            Assert.IsFalse(GlowStones.Light(area, session.World, new Vector2Int(2, 3)), "반딧돌이 아닌 칸");
            Assert.IsTrue(CellState.IsDone(area, session, stone), "밝힌 모습으로 그림·미니맵");
            Assert.AreEqual(1, GlowStones.LitCount(area, session.World));

            // 세이브: JSON으로 갔다 와도 그대로, 예전 세이브(칸 없음)는 빈 목록
            var copy = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(session.World));
            Assert.IsTrue(copy.IsStoneLit(area.StoneId(stone)));
            var old = JsonUtility.FromJson<WorldState>("{\"areaId\":\"cave\"}");
            Assert.IsFalse(old.IsStoneLit(area.StoneId(stone)));
            old.MarkStoneLit("cave:1,3");
            Assert.IsTrue(old.IsStoneLit("cave:1,3"));
        }

        // 모두 밝힌 보상 (#115): 반딧돌을 다 밝혀야 성유물(반딧돌 등잔), 한 번만
        [Test]
        public void LightingEveryStoneGivesTheGlowStoneRelicOnce()
        {
            var lamp = TestData.Relic("relic_glow_lamp", TestData.Skill("lamp_flash", Monsters.SkillKind.Damage, Monsters.SkillTarget.AllEnemies, 26),
                new Monsters.MonsterStats(0, 9, 0));
            var twoStones = Map.Replace("#P:::YHH:#", "#P:::YHHI#");
            var area = ScriptableObject.CreateInstance<FieldArea>().Set("areaId", "cave").Set("map", twoStones)
                .Set("theme", FieldTheme.Cave).Set("glowStoneRelic", lamp);
            var session = GameSession.NewGame(TestData.Hero(new Monsters.MonsterStats(50, 10, 10)), 5);

            GlowStones.Light(area, session.World, new Vector2Int(1, 3));
            Assert.IsFalse(GlowStones.AllLit(area, session.World));
            Assert.IsNull(GlowStones.ClaimAllLitReward(area, session), "하나 남음 — 아직 보상 없음");
            Assert.IsFalse(session.Hero.Owns(lamp));

            GlowStones.Light(area, session.World, new Vector2Int(8, 1));
            Assert.IsTrue(GlowStones.AllLit(area, session.World));
            var relic = GlowStones.ClaimAllLitReward(area, session);
            Assert.AreSame(lamp, relic?.Data, "다 밝히면 반딧돌 등잔");
            Assert.IsTrue(session.Hero.Owns(lamp));
            Assert.IsNull(GlowStones.ClaimAllLitReward(area, session), "두 번은 안 줌");

            Assert.IsNull(GlowStones.ClaimAllLitReward(Area(), session), "보상이 없는 지역");
        }

        [Test]
        public void HeroLightIsThreeAndAHalfCellsAndTheTorchAddsOne()
        {
            Assert.AreEqual(3.5f, CaveLight.HeroLightRadius(false));
            Assert.AreEqual(4.5f, CaveLight.HeroLightRadius(true), "아라를 데려가면 횃불로 +1칸 (사용자)");
            var hero = CaveLight.Hero(new Vector2(10.5f, 10.5f), false);
            Assert.AreEqual(1f, hero.At(new Vector2(10.5f, 10.5f)));
            Assert.AreEqual(1f, hero.At(new Vector2(12f, 10.5f)), "가까운 곳은 환함");
            Assert.AreEqual(0f, hero.At(new Vector2(14.5f, 10.5f)), "반지름 밖은 캄캄");
            float edge = hero.At(new Vector2(13.3f, 10.5f));
            Assert.Greater(edge, 0f);
            Assert.Less(edge, 1f, "가장자리는 부드럽게");
            Assert.Greater(CaveLight.Hero(new Vector2(10.5f, 10.5f), true).At(new Vector2(14.5f, 10.5f)), 0f, "횃불이면 4칸 거리도 보임");
        }

        [Test]
        public void LitStonesCampAndDoorsGiveLastingLight()
        {
            var area = Area();
            var world = new WorldState();
            var map = area.Map;
            var none = CaveLight.StaticLights(map, cell => GlowStones.IsLit(area, world, cell));
            Assert.AreEqual(2, none.Count, "샘 + 출입구 (꺼진 반딧돌은 빛 없음)");
            GlowStones.Light(area, world, new Vector2Int(1, 3));
            var lights = CaveLight.StaticLights(map, cell => GlowStones.IsLit(area, world, cell));
            Assert.AreEqual(3, lights.Count);
            Assert.IsTrue(lights.Exists(l => l.Radius == CaveLight.StoneRadius && l.Center == new Vector2(1.5f, 3.5f)), "밝힌 반딧돌 6칸");
            Assert.IsTrue(lights.Exists(l => l.Radius == CaveLight.CampRadius), "쉼터 물건(샘) 4칸");
            Assert.IsTrue(lights.Exists(l => l.Radius == CaveLight.DoorRadius), "출입구 바깥 빛");
        }

        [Test]
        public void DarknessKeepsExploredCellsFaintAndUnexploredPitchDark()
        {
            var darkness = new DarknessMap(10, 5, 2);
            Assert.AreEqual(20, darkness.Width);
            Assert.AreEqual(10, darkness.Height);
            darkness.SetStatic(new[] { new LightSource(new Vector2(1.5f, 3.5f), CaveLight.StoneRadius) }, cell => cell.x >= 8);
            Assert.AreEqual(1f, darkness.StaticAt(new Vector2(1.5f, 3.5f)), "반딧돌 자리는 환함");
            Assert.AreEqual(CaveLight.Memory, darkness.StaticAt(new Vector2(9.5f, 0.5f)), 1e-4f, "지나간 곳은 희미하게");

            var hero = CaveLight.Hero(new Vector2(8.5f, 1.5f), false);
            var pixels = new Color32[darkness.Width * darkness.Height];
            darkness.Paint(hero, pixels, new Color32(6, 6, 12, 255), 0.97f);
            byte AlphaAt(float x, float y) => pixels[Mathf.FloorToInt(y * 2) * darkness.Width + Mathf.FloorToInt(x * 2)].a;
            Assert.AreEqual(0, AlphaAt(8.5f, 1.5f), "주인공 자리는 투명");
            Assert.AreEqual(0, AlphaAt(1.5f, 3.5f), "밝힌 반딧돌 자리도 투명");
            var faraway = new DarknessMap(30, 5, 2);
            faraway.SetStatic(null, null);
            var farPixels = new Color32[faraway.Width * faraway.Height];
            faraway.Paint(CaveLight.Hero(new Vector2(1.5f, 1.5f), false), farPixels, new Color32(6, 6, 12, 255), 0.97f);
            Assert.AreEqual(Mathf.RoundToInt(255 * 0.97f), farPixels[2 * faraway.Width + 50].a, "처음 가는 먼 곳은 거의 캄캄");
            var farHero = CaveLight.Hero(new Vector2(1.5f, 1.5f), false);
            Assert.Less(faraway.Brightness(new Vector2(20.5f, 1.5f), farHero), CaveLight.EyesBelow, "빛 밖 → 몬스터는 눈만");
            Assert.Greater(faraway.Brightness(new Vector2(2.5f, 1.5f), farHero), CaveLight.EyesBelow, "빛 안 몬스터는 몸이 보임");
        }

        [Test]
        public void CaveColorsAndDustFollowTheCaveGround()
        {
            Assert.AreNotEqual(MinimapArt.ColorOf(FieldTile.GlowStone, FieldTheme.Cave, false), MinimapArt.ColorOf(FieldTile.GlowStone, FieldTheme.Cave, true),
                "밝힌 반딧돌은 미니맵에서도 더 환하게");
            Assert.IsTrue(MinimapArt.HasDoneLook(FieldTile.GlowStone));
            Assert.AreNotEqual(MinimapArt.ColorOf(FieldTile.Wall, FieldTheme.Cave, false), MinimapArt.ColorOf(FieldTile.Lawn, FieldTheme.Cave, false), "벽과 땅");
            Assert.IsNotNull(Resources.Load<Texture2D>("Art/NinjaAdventure/Tiles/Cave_GlowStone_done"), "밝힌 반딧돌 그림");
            foreach (var atlas in new[] { "Cave_Wall_auto", "Cave_Floor_auto", "Cave_Water_auto" })
                Assert.IsNotNull(Resources.Load<Texture2D>("Art/NinjaAdventure/Tiles/" + atlas), atlas);
            Assert.AreNotSame(FieldArt.ForTile(FieldTile.GlowStone, FieldTheme.Cave, false), FieldArt.ForTile(FieldTile.GlowStone, FieldTheme.Cave, true));
        }
    }
}
