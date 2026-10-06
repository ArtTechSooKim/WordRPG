using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Save;

namespace WordRPG.Tests
{
    // 튜토리얼(안내)을 본 기록 (#36): 본 것은 다시 안 뜨고, 세이브에 남고, [건너뛰기]는 전부 본 것으로
    public class TutorialProgressTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void MarkAndMarkAll()
        {
            var progress = new TutorialProgress();
            Assert.IsFalse(progress.Has(TutorialProgress.Field), "처음엔 아무것도 안 봄");
            progress.Mark(TutorialProgress.Quiz);
            Assert.IsTrue(progress.Has(TutorialProgress.Quiz));
            Assert.IsFalse(progress.Has(TutorialProgress.Wrong));

            progress.MarkAll();
            foreach (var id in TutorialProgress.All) Assert.IsTrue(progress.Has(id), id);
            progress.Clear();
            Assert.IsFalse(progress.Seen.Any(), "다시 보기");
        }

        [Test]
        public void SeenTutorialsSurviveSaveAndOldSavesStartEmpty()
        {
            var quill = TestData.Relic("relic_quill", TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 14),
                new MonsterStats(0, 4, 0));
            var heroData = TestData.Hero(new MonsterStats(60, 14, 10), quill);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], null, new[] { quill });

            var session = GameSession.NewGame(heroData);
            session.Tutorials.Mark(TutorialProgress.Field);
            session.Tutorials.Mark(TutorialProgress.Intensity);
            var json = session.ToSaveData(T0).ToJson();
            var loaded = GameSession.FromSaveData(SaveData.FromJson(json), database, heroData);
            Assert.IsTrue(loaded.Tutorials.Has(TutorialProgress.Field));
            Assert.IsTrue(loaded.Tutorials.Has(TutorialProgress.Intensity));
            Assert.IsFalse(loaded.Tutorials.Has(TutorialProgress.Wrong));

            // 튜토리얼이 생기기 전 세이브(필드 없음)는 아무것도 안 본 것으로 → 한 번씩 보여 준다
            string oldJson = json.Replace("\"tutorials\"", "\"oldUnknownField\"");
            var old = GameSession.FromSaveData(SaveData.FromJson(oldJson), database, heroData);
            Assert.IsFalse(old.Tutorials.Seen.Any());
        }
    }
}
