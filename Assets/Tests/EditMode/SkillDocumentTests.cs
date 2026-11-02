using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;

namespace WordRPG.Tests
{
    // 기술 칸(기본 기술 + 3칸)과 기술문서: 4개까지, 가득이면 바꾸기, 문서는 남아서 다시 배우기
    public class SkillDocumentTests
    {
        private SkillData splash, shield, light, cram, sweep;
        private RelicData quill, book, lantern;
        private ItemData cramDoc, sweepDoc, potion;
        private HeroData heroData;

        [SetUp]
        public void SetUp()
        {
            splash = TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 14);
            shield = TestData.Skill("shield", SkillKind.Guard, SkillTarget.AllAllies, 4);
            light = TestData.Skill("nap", SkillKind.Heal, SkillTarget.AllAllies, 8);
            cram = TestData.Skill("cram", SkillKind.Damage, SkillTarget.SingleEnemy, 30);
            sweep = TestData.Skill("sweep", SkillKind.Damage, SkillTarget.AllEnemies, 20);
            quill = TestData.Relic("relic_quill", splash, new MonsterStats(0, 4, 0));
            book = TestData.Relic("relic_book", shield, new MonsterStats(10, 0, 4));
            lantern = TestData.Relic("relic_lantern", light, new MonsterStats(15, 0, 0));
            cramDoc = Document("skilldoc_cram", cram);
            sweepDoc = Document("skilldoc_sweep", sweep);
            potion = TestData.Potion("potion", 40);
            heroData = TestData.Hero(new MonsterStats(60, 14, 10), quill);
        }

        private static ItemData Document(string id, SkillData skill) =>
            TestData.Item(id).Set("kind", ItemKind.SkillDocument).Set("taughtSkill", skill);

        private GameSession FullSession()
        {
            var session = GameSession.NewGame(heroData, 5);
            session.GrantRelic(book);
            session.GrantRelic(lantern);
            return session;
        }

        // 휘두르기도 바꿔 넣기 (#147 사용자): 전투 기술 4칸 모두 바꿀 수 있음
        [Test]
        public void TheBasicSkillSeatCanBeSwappedAndPutBack()
        {
            var session = FullSession();
            var hero = session.Hero;
            session.Inventory.Add(cramDoc);

            Assert.IsTrue(hero.UsesBasicSkill);
            Assert.IsTrue(session.LearnSkill(cramDoc, Hero.BasicSeat));
            CollectionAssert.AreEqual(new[] { cram, splash, shield, light }, hero.Skills.ToArray(), "휘두르기 자리에 벼락치기");
            Assert.IsFalse(hero.UsesBasicSkill);
            Assert.IsTrue(hero.Knows(cramDoc));
            Assert.AreSame(cramDoc, hero.SourceOf(cram).Document);

            Assert.IsTrue(hero.PutBackBasicSkill(1), "책 방패를 빼고 휘두르기를 다시 넣음");
            CollectionAssert.AreEqual(new[] { heroData.BasicSkill, splash, cram, light }, hero.Skills.ToArray(), "벼락치기는 빠진 칸으로 옮겨 감");
            Assert.IsTrue(hero.UsesBasicSkill);
            Assert.IsFalse(hero.PutBackBasicSkill(0), "이미 있음");

            session.Inventory.Add(sweepDoc);
            session.LearnSkill(sweepDoc, Hero.BasicSeat);
            Assert.IsTrue(hero.PutBackBasicSkill(Hero.BasicSeat), "기본 기술 자리의 기술을 뺌");
            CollectionAssert.AreEqual(new[] { heroData.BasicSkill, splash, cram, light }, hero.Skills.ToArray());
        }

        // 뺀 성유물의 기술이 기본 기술 자리에 있었으면 그 자리는 휘두르기로 돌아감. 바꿔 끼우면 새 성유물 기술이 같은 자리에
        [Test]
        public void RemovingARelicInTheBasicSeatBringsTheBasicSkillBack()
        {
            var session = FullSession();
            var hero = session.Hero;
            session.Inventory.Add(cramDoc);
            session.LearnSkill(cramDoc, 0); // 잉크 뿌리기가 칸에서 빠짐
            var quillOwned = hero.Find(quill);
            Assert.IsTrue(hero.PlaceRelicSkill(quillOwned, Hero.BasicSeat));
            Assert.AreSame(splash, hero.Skills[0]);
            Assert.IsTrue(hero.HasSkillOf(quillOwned));

            Assert.IsTrue(hero.Unequip(quillOwned));
            Assert.IsTrue(hero.UsesBasicSkill);
            Assert.AreSame(heroData.BasicSkill, hero.Skills[0]);
        }

        // 공격 기술은 하나 남음: 회복·보호막만 들고 갈 수 없음. 뺀 성유물 때문에 없어지면 휘두르기가 돌아옴
        [Test]
        public void AnAttackSkillAlwaysStays()
        {
            var healA = Document("doc_heal_a", TestData.Skill("heal_a", SkillKind.Heal, SkillTarget.AllAllies, 5));
            var session = FullSession();
            var hero = session.Hero;
            session.Inventory.Add(healA);
            // 지금: 휘두르기(공격) · 잉크 뿌리기(공격) · 방패 · 빛
            Assert.IsTrue(session.LearnSkill(healA, Hero.BasicSeat), "잉크 뿌리기가 남으니 휘두르기를 회복으로 바꿀 수 있음");
            CollectionAssert.AreEqual(new[] { healA.TaughtSkill, splash, shield, light }, hero.Skills.ToArray());
            Assert.IsFalse(hero.CanReplace(0, shield), "마지막 공격 기술은 회복·보호막으로 못 바꿈");
            Assert.IsTrue(hero.CanReplace(0, cram), "공격 기술로는 바꿀 수 있음");

            Assert.IsTrue(hero.Unequip(hero.Find(quill)), "공격 기술을 가진 성유물을 뺌");
            Assert.IsTrue(hero.UsesBasicSkill, "공격 기술이 없어지면 휘두르기가 돌아옴");
            Assert.AreSame(heroData.BasicSkill, hero.Skills[0]);
        }

        [Test]
        public void TheBasicSeatSurvivesSavingAndOldSavesKeepTheBasicSkill()
        {
            var session = FullSession();
            session.Inventory.Add(cramDoc);
            session.LearnSkill(cramDoc, Hero.BasicSeat);
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new[] { cramDoc, sweepDoc, potion }, new FieldArea[0], new[] { quill, book, lantern });

            var data = SaveTestsHelper.RoundTrip(session.ToSaveData(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)));
            StringAssert.Contains("doc:skilldoc_cram", data.ToJson());
            var loaded = GameSession.FromSaveData(data, database, heroData);
            CollectionAssert.AreEqual(new[] { cram, splash, shield, light }, loaded.Hero.Skills.ToArray(), "자리 그대로");
            Assert.AreEqual(0, loaded.LoadWarnings.Count);

            var old = GameSession.FromSaveData(Save.SaveData.FromJson(data.ToJson().Replace("\"basicSeat\": \"doc:skilldoc_cram\"", "\"basicSeat\": \"\"")), database, heroData);
            Assert.IsTrue(old.Hero.UsesBasicSkill, "칸이 비어 있는 예전 세이브 = 기본 기술");
        }

        [Test]
        public void RelicSkillsFillTheThreeSkillSlotsLikeBefore()
        {
            var session = FullSession();
            var hero = session.Hero;
            CollectionAssert.AreEqual(new[] { heroData.BasicSkill, splash, shield, light }, hero.Skills.ToArray(), "기본 기술 + 성유물 기술 3개 = 4개");
            Assert.IsFalse(hero.HasSkillRoom);
            Assert.AreSame(hero.Find(book), hero.SourceOf(shield).Relic);
            Assert.IsNull(hero.SourceOf(heroData.BasicSkill), "기본 기술은 칸이 아님 (항상 남음)");
        }

        [Test]
        public void DocumentFillsEmptySlotRightAway()
        {
            var session = GameSession.NewGame(heroData, 5); // 깃펜만 → 기술 칸 1/3
            session.Inventory.Add(cramDoc);
            Assert.IsTrue(session.Hero.HasSkillRoom);
            Assert.IsTrue(session.LearnSkill(cramDoc));
            CollectionAssert.Contains(session.Hero.Skills.ToArray(), cram);
            Assert.AreEqual("기술문서", session.Hero.SourceOf(cram).SourceName);
            Assert.AreEqual(1, session.Inventory.GetCount(cramDoc), "배워도 문서는 남는다");
            Assert.IsFalse(session.LearnSkill(cramDoc), "이미 배운 기술");
        }

        [Test]
        public void WhenFullYouMustPickASkillToReplace()
        {
            var session = FullSession();
            var hero = session.Hero;
            session.Inventory.Add(cramDoc);
            Assert.IsFalse(session.LearnSkill(cramDoc), "가득이면 바꿀 칸을 골라야 함");
            Assert.IsFalse(session.LearnSkill(cramDoc, 7), "없는 칸");

            Assert.IsTrue(session.LearnSkill(cramDoc, 1)); // 책 방패 → 벼락치기
            CollectionAssert.AreEqual(new[] { heroData.BasicSkill, splash, cram, light }, hero.Skills.ToArray());
            Assert.IsTrue(hero.IsEquipped(hero.Find(book)), "성유물은 그대로 끼운 채 (능력치 보너스 유지)");
            Assert.IsFalse(hero.HasSkillOf(hero.Find(book)));
            Assert.AreEqual(4, hero.Skills.Count, "전투 기술은 최대 4개");
        }

        [Test]
        public void ReplacedSkillsCanComeBackFromRelicOrDocument()
        {
            var session = FullSession();
            var hero = session.Hero;
            session.Inventory.Add(cramDoc);
            session.LearnSkill(cramDoc, 1);

            // 빠진 책 방패를 다시 넣기 (벼락치기와 바꿈)
            Assert.IsTrue(hero.PlaceRelicSkill(hero.Find(book), 1));
            CollectionAssert.DoesNotContain(hero.Skills.ToArray(), cram);
            Assert.IsFalse(hero.Knows(cramDoc));
            // 문서가 남아 있어 벼락치기를 다시 배울 수 있다
            Assert.IsTrue(session.LearnSkill(cramDoc, 2));
            CollectionAssert.Contains(hero.Skills.ToArray(), cram);
            CollectionAssert.DoesNotContain(hero.Skills.ToArray(), light);
        }

        [Test]
        public void UnequippingRelicFreesItsSkillSlot()
        {
            var session = FullSession();
            var hero = session.Hero;
            hero.Unequip(hero.Find(lantern));
            CollectionAssert.DoesNotContain(hero.Skills.ToArray(), light);
            Assert.IsTrue(hero.HasSkillRoom);
            session.Inventory.Add(sweepDoc);
            Assert.IsTrue(session.LearnSkill(sweepDoc), "빈 칸에 바로");
            hero.Equip(hero.Find(lantern));
            CollectionAssert.DoesNotContain(hero.Skills.ToArray(), light, "기술 칸이 가득이면 끼워도 기술은 자동으로 안 들어감");
            Assert.IsFalse(hero.HasSkillOf(hero.Find(lantern)));
            Assert.IsFalse(hero.PlaceRelicSkill(hero.Find(lantern)), "넣으려면 바꿀 칸을 골라야 함");
        }

        [Test]
        public void OnlyDocumentsInTheBagTeach()
        {
            var session = GameSession.NewGame(heroData, 5);
            Assert.IsFalse(session.LearnSkill(cramDoc), "가방에 없는 문서");
            session.Inventory.Add(potion);
            Assert.IsFalse(session.LearnSkill(potion), "기술문서가 아님");
            Assert.Throws<ArgumentException>(() => new SkillSlot(potion));
        }

        [Test]
        public void RelicSkillSlotFollowsAwakening()
        {
            var awakened = TestData.Skill("storm", SkillKind.Damage, SkillTarget.AllEnemies, 22);
            var ink = TestData.Item("ink");
            var relic = TestData.Relic("relic_awake", splash, new MonsterStats(0, 1, 0), ink, awakened);
            var session = GameSession.NewGame(TestData.Hero(new MonsterStats(60, 14, 10), relic), 5);
            session.Inventory.Add(ink, 10);
            session.Inventory.AddGold(1000);
            var owned = session.Hero.Find(relic);
            for (int i = 0; i < 3; i++) RelicUpgrade.TryUpgrade(owned, session.Inventory);
            CollectionAssert.Contains(session.Hero.Skills.ToArray(), awakened, "각성하면 칸의 기술도 강한 기술로");
        }

        [Test]
        public void SkillSlotsSurviveSaveAndOldSavesUseEquippedRelics()
        {
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new[] { cramDoc, sweepDoc }, null, new[] { quill, book, lantern });
            var session = FullSession();
            session.Inventory.Add(cramDoc);
            session.LearnSkill(cramDoc, 0); // 잉크 뿌리기 → 벼락치기

            var data = SaveTestsHelper.RoundTrip(session.ToSaveData(new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)));
            var loaded = GameSession.FromSaveData(data, database, heroData);
            CollectionAssert.AreEqual(new[] { heroData.BasicSkill, cram, shield, light }, loaded.Hero.Skills.ToArray(), "칸 순서 그대로");
            Assert.IsTrue(loaded.Hero.IsEquipped(loaded.Hero.Find(quill)), "깃펜은 끼운 채 (기술만 빠짐)");
            Assert.AreEqual(0, loaded.LoadWarnings.Count);

            // 기술 칸 정보가 없는 예전 세이브 → 끼운 성유물 기술로
            var json = data.ToJson().Replace("\"hasSkillSlots\": true", "\"hasSkillSlots\": false");
            var old = GameSession.FromSaveData(Save.SaveData.FromJson(json), database, heroData);
            CollectionAssert.AreEqual(new[] { heroData.BasicSkill, splash, shield, light }, old.Hero.Skills.ToArray());
        }

        [Test]
        public void MissingDocumentInSaveIsDroppedWithWarning()
        {
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], null, new[] { quill });
            var session = GameSession.NewGame(heroData, 5);
            session.Inventory.Add(cramDoc);
            session.LearnSkill(cramDoc);
            var loaded = GameSession.FromSaveData(session.ToSaveData(DateTime.UtcNow), database, heroData);
            CollectionAssert.DoesNotContain(loaded.Hero.Skills.ToArray(), cram);
            CollectionAssert.Contains(loaded.Hero.Skills.ToArray(), splash);
            Assert.IsTrue(loaded.LoadWarnings.Any(w => w.Contains("doc:skilldoc_cram")));
        }
    }

    internal static class SaveTestsHelper
    {
        public static Save.SaveData RoundTrip(Save.SaveData data) => Save.SaveData.FromJson(data.ToJson());
    }

    // 실제 데이터: 기술문서마다 기술이 있고, 보스나 보물상자에서 얻을 수 있어야 한다
    public class SkillDocumentDataTests
    {
        [Test]
        public void EveryDocumentTeachesASkillAndCanBeFound()
        {
            var items = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var areas = AssetDatabase.FindAssets("t:FieldArea", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<FieldArea>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var findable = new HashSet<ItemData>();
            foreach (var area in areas)
            {
                foreach (var chest in area.ChestContents) if (chest.Item != null) findable.Add(chest.Item);
                if (area.Boss?.RewardItem != null) findable.Add(area.Boss.RewardItem);
            }

            var documents = items.Where(i => i.Kind == ItemKind.SkillDocument).ToList();
            Assert.GreaterOrEqual(documents.Count, 3);
            foreach (var document in documents)
            {
                Assert.IsNotNull(document.TaughtSkill, $"{document.name}: 배우는 기술이 비어 있음");
                Assert.IsTrue(findable.Contains(document), $"{document.name}: 보물상자·보스 어디에서도 얻을 수 없음");
            }
            var bossDocuments = areas.Where(a => a.Boss?.RewardItem != null).Select(a => a.Boss.RewardItem).ToList();
            Assert.GreaterOrEqual(bossDocuments.Count, 2, "서고·숲 보스가 기술문서를 준다");
        }

        [Test]
        public void HealingLightIsNowANap()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Data/Skills/lumi_healing_light.asset");
            Assert.AreEqual("쪽잠자기", skill.DisplayName, "공부 테마 (2026-10-03 사용자 결정)");
        }
    }
}
