using System;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;
using WordRPG.UI;

namespace WordRPG.Tests
{
    // 게임이 이름으로 불러오는 소리·그림 파일이 실제로 있는지 (Tools/import_ninja_art.py 로 만든 것)
    public class AudioArtTests
    {
        [Test]
        public void EveryMusicTrackHasAClip()
        {
            foreach (Music track in Enum.GetValues(typeof(Music)))
            {
                if (track == Music.None) continue;
                string path = "Audio/Music/" + track.ToString().ToLowerInvariant();
                Assert.IsNotNull(Resources.Load<AudioClip>(path), $"Resources/{path} 없음");
            }
        }

        [Test]
        public void EverySoundEffectHasAClip()
        {
            foreach (Sfx effect in Enum.GetValues(typeof(Sfx)))
            {
                string path = "Audio/Sfx/" + effect.ToString().ToLowerInvariant();
                Assert.IsNotNull(Resources.Load<AudioClip>(path), $"Resources/{path} 없음");
            }
        }

        // 달리기 흙먼지 (#53): 밟고 지나간 바닥 색을 입힌다. 같은 테마 안에서도 길·풀숲·잔디가 서로 다른 색,
        // 먼지 그림은 회색조(색을 곱해 입힘)
        [Test]
        public void RunDustTakesTheColorOfTheGround()
        {
            foreach (FieldTheme theme in Enum.GetValues(typeof(FieldTheme)))
            {
                var floor = FieldArt.DustColor(theme, FieldTile.Floor);
                Assert.AreNotEqual(floor, FieldArt.DustColor(theme, FieldTile.Grass), $"{theme}: 길과 풀숲");
                Assert.AreNotEqual(floor, FieldArt.DustColor(theme, FieldTile.Lawn), $"{theme}: 길과 잔디");
                Assert.AreEqual(floor, FieldArt.DustColor(theme, FieldTile.Door), $"{theme}: 출입구는 길 색");
                foreach (FieldTile tile in Enum.GetValues(typeof(FieldTile)))
                    Assert.AreEqual(1f, FieldArt.DustColor(theme, tile).a, $"{theme} {tile}");
            }
            Assert.AreNotEqual(FieldArt.DustColor(FieldTheme.Meadow, FieldTile.Floor), FieldArt.DustColor(FieldTheme.Library, FieldTile.Floor),
                "초원 흙길과 서고 돌바닥");

            // 그림은 Tools/import_ninja_art.py가 회색조("gray")로 만든다
            Assert.IsNotNull(Resources.Load<Texture2D>("Art/NinjaAdventure/Fx/dust"));
        }

        // 유료 라이선스 소리는 깃에 없는 Audio/Licensed에 두고 먼저 찾는다 (#66) — 없으면 팩 소리
        [Test]
        public void LicensedSoundIsTriedBeforeThePackSound()
        {
            CollectionAssert.AreEqual(new[] { "Audio/Licensed/newword", "Audio/Sfx/newword" }, Sound.CandidatePaths("Sfx", "newword"));
        }

        [Test]
        public void EveryFieldTileHasArtForEveryTheme()
        {
            foreach (FieldTheme theme in Enum.GetValues(typeof(FieldTheme)))
            foreach (FieldTile tile in Enum.GetValues(typeof(FieldTile)))
            {
                if (!FieldThemes.Supports(theme, tile)) continue; // 정글 칸은 정글 그림만 (#80)
                string path = $"Art/NinjaAdventure/Tiles/{theme}_{tile}";
                Assert.IsNotNull(Resources.Load<Texture2D>(path), $"Resources/{path} 없음");
            }
        }

        [Test]
        public void EveryThemeHasLockedDoorArt()
        {
            foreach (FieldTheme theme in Enum.GetValues(typeof(FieldTheme)))
            {
                string path = $"Art/NinjaAdventure/Tiles/{theme}_Door_locked";
                Assert.IsNotNull(Resources.Load<Texture2D>(path), $"Resources/{path} 없음 (보스를 물리쳐야 열리는 출입구)");
                Assert.AreNotSame(FieldArt.ForDoor(theme, null, false), FieldArt.ForDoor(theme, null, true), $"{theme}: 잠긴 출입구는 다른 그림");
            }
            Assert.AreNotSame(FieldArt.ForDoor(FieldTheme.Meadow, FieldTheme.Library, false),
                FieldArt.ForDoor(FieldTheme.Meadow, FieldTheme.Forest, false), "초원 → 숲 출입구는 숲길 입구 그림");
        }

        // 주인공 모습마다 (#124): 걷기 시트(4방향 × 걷기·점프·자세 7행)와 고르는 화면의 얼굴
        [Test]
        public void EveryHeroLookHasAWalkSheetAndAFace()
        {
            var hero = UnityEditor.AssetDatabase.LoadAssetAtPath<WordRPG.Heroes.HeroData>("Assets/Data/Hero/hero.asset");
            Assert.AreEqual(8, hero.Looks.Count, "8명 중에서 고름 (사용자)");
            Assert.AreEqual(WordRPG.Heroes.HeroLook.Fallback, hero.DefaultLookId, "기본 = 1.0부터의 소년");
            foreach (var look in hero.Looks)
            {
                Assert.IsFalse(string.IsNullOrEmpty(look.DisplayName), look.LookId);
                var sheet = Resources.Load<Texture2D>(PlayerArt.SheetPathOf(look.LookId));
                Assert.IsNotNull(sheet, $"{look.LookId} 시트 없음");
                Assert.AreEqual(64, sheet.width, $"{look.LookId}: 4방향");
                Assert.AreEqual(112, sheet.height, $"{look.LookId}: 걷기 4 + 공격 + 점프 + 자세");
                Assert.AreEqual(FilterMode.Point, sheet.filterMode, "도트는 Point 필터로 또렷하게");
                Assert.IsNotNull(PlayerArt.Face(look.LookId), $"{look.LookId} 얼굴 없음");
            }
        }
    }
}
