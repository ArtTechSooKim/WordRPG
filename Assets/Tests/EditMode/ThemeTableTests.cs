using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 테마 표 (#118): 테마마다 화면 모습 한 줄 + 칸 색 표(미니맵·먼지)에도 줄이 있어야 한다.
    // 없으면 게임은 멈추지 않고 초원 모습으로 조용히 보이므로 여기서 잡는다
    public class ThemeTableTests
    {
        private static FieldTheme[] AllThemes => (FieldTheme[])Enum.GetValues(typeof(FieldTheme));

        [Test]
        public void EveryThemeHasOneLookRow()
        {
            var seen = new HashSet<FieldTheme>();
            foreach (var look in ThemeLooks.All)
                Assert.IsTrue(seen.Add(look.Theme), $"{look.Theme}이(가) 테마 표에 두 번 있음");
            foreach (var theme in AllThemes)
            {
                Assert.IsTrue(ThemeLooks.Has(theme), $"ThemeLooks 표에 {theme} 줄이 없음 — 음악·맵 밖 색·전투 바닥·안개·어둠");
                Assert.AreEqual(theme, ThemeLooks.Of(theme).Theme);
            }
        }

        // 지역마다 자기 전투곡 (#164·#166 사용자: 지역 컨셉에 맞게) — 다른 지역과 겹치지 않고, 파일이 없을 때 대신 틀 곡이 있음
        [Test]
        public void EveryThemeHasItsOwnBattleMusic()
        {
            var used = new HashSet<Music>();
            foreach (var look in ThemeLooks.All)
            {
                Assert.Greater(look.BattleMusic.Length, 0, $"{look.Theme}: 전투곡");
                foreach (var track in look.BattleMusic)
                {
                    Assert.IsTrue(used.Add(track), $"{track}을(를) 두 지역이 같이 씀");
                    Assert.AreEqual(Music.Battle, Sound.FallbackOf(track), $"{look.Theme}: {track} — 이름이 Battle로 시작해야 대신 틀 곡이 있음");
                }
            }
        }

        // 보스가 있는 지역은 보스곡도 따로 (#164 사용자: 보스들은 특별히 다른 곡)
        [Test]
        public void EveryAreaWithABossHasItsOwnBossMusic()
        {
            int bosses = 0;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" }))
            {
                var area = UnityEditor.AssetDatabase.LoadAssetAtPath<FieldArea>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (area.Boss == null) continue;
                bosses++;
                var track = ThemeLooks.Of(area.Theme).BossMusic;
                Assert.AreNotEqual(Music.Boss, track, $"{area.name}: 공용 보스곡을 씀");
                Assert.AreEqual(Music.Boss, Sound.FallbackOf(track), $"{area.name}: {track} — 이름이 Boss로 시작해야 대신 틀 곡이 있음");
            }
            Assert.GreaterOrEqual(bosses, 4);
        }

        [Test]
        public void BattleTracksTakeTurnsAndBossUsesItsOwn()
        {
            var look = new ThemeLook(FieldTheme.Meadow, Music.Meadow, new[] { Music.BattleMeadow, Music.BattleCave }, Music.BossCave, Color.black, FieldTile.Grass);
            Assert.AreEqual(Music.BattleMeadow, look.BattleTrack(false, 0));
            Assert.AreEqual(Music.BattleCave, look.BattleTrack(false, 1));
            Assert.AreEqual(Music.BattleMeadow, look.BattleTrack(false, 2), "번갈아");
            Assert.AreEqual(Music.BossCave, look.BattleTrack(true, 1));
        }

        [Test]
        public void EveryThemeHasMinimapAndDustRows()
        {
            foreach (var theme in AllThemes)
            {
                Assert.IsTrue(MinimapArt.HasTheme(theme), $"MinimapArt 표에 {theme} 줄이 없음 (초원 색으로 보임)");
                Assert.IsTrue(FieldArt.HasDustColors(theme), $"FieldArt.DustColor 표에 {theme} 줄이 없음 (초원 흙길 색으로 보임)");
            }
        }

        [Test]
        public void LookValuesPointAtRealThings()
        {
            foreach (var look in ThemeLooks.All)
            {
                Assert.AreNotEqual(Music.None, look.Music, $"{look.Theme}: 음악");
                Assert.IsNotNull(Resources.Load<AudioClip>("Audio/Music/" + look.Music.ToString().ToLowerInvariant()), $"{look.Theme}: 음악 파일");
                Assert.IsTrue(FieldThemes.Supports(look.Theme, look.BattleFloor), $"{look.Theme}: 전투 바닥 칸 {look.BattleFloor}");
                Assert.AreEqual(1f, look.Outside.a, $"{look.Theme}: 맵 밖 색은 불투명");
                if (look.Fog.HasValue) Assert.That(look.Fog.Value.Alpha, Is.InRange(0.05f, 0.95f), $"{look.Theme}: 안개 짙기");
            }
        }

        // 지금 정해진 모습 (사용자 결정): 안개는 서고만 (#101·#103), 어둠은 동굴만 (#112), 서고 전투 바닥은 돌바닥
        [Test]
        public void CurrentThemeChoicesStay()
        {
            foreach (var theme in AllThemes)
            {
                Assert.AreEqual(theme == FieldTheme.Library, ThemeLooks.Of(theme).Fog.HasValue, $"{theme}: 안개");
                Assert.AreEqual(theme == FieldTheme.Cave, ThemeLooks.Of(theme).Dark, $"{theme}: 어둠");
            }
            Assert.AreEqual(FieldTile.Floor, ThemeLooks.Of(FieldTheme.Library).BattleFloor);
            Assert.AreEqual(FieldTile.Grass, ThemeLooks.Of(FieldTheme.Forest).BattleFloor, "숲 전투 바닥은 고사리");
        }
    }
}
