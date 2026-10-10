using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Save;
using WordRPG.Words;

namespace WordRPG.Tests
{
    public class SaveSystemTests
    {
        private string directory;
        private SaveSystem system;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "WordRPG_SaveTests_" + Guid.NewGuid().ToString("N"));
            system = new SaveSystem(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static SaveData Sample(int gold)
        {
            var inventory = new Inventory();
            inventory.AddGold(gold);
            return new SaveData(DateTime.UtcNow, new HeroSaveData(3, 5, 20),
                new List<RelicSaveData> { new RelicSaveData("relic_quill", 2, 0) },
                inventory, new VocabularyProgress(), new PlayerRecord());
        }

        [Test]
        public void NoSaveYet()
        {
            Assert.IsFalse(system.HasSave);
            var result = system.Load();
            Assert.IsNull(result.Data);
            Assert.IsNull(result.Error, "파일이 없는 건 오류가 아니다");
        }

        [Test]
        public void SaveThenLoad()
        {
            system.Save(Sample(42));

            var result = system.Load();

            Assert.IsTrue(system.HasSave);
            Assert.IsFalse(result.UsedBackup);
            Assert.AreEqual(42, result.Data.Inventory.Gold);
            Assert.AreEqual(3, result.Data.Hero.Level);
            Assert.AreEqual("relic_quill", result.Data.Relics[0].RelicId);
            Assert.AreEqual(SaveData.CurrentVersion, result.Data.Version);
        }

        [Test]
        public void CorruptMainFallsBackToPreviousSave()
        {
            system.Save(Sample(10));
            system.Save(Sample(20)); // 이전 저장(10)이 백업으로
            File.WriteAllText(system.MainPath, "{ 깨진 파일");

            var result = system.Load();

            Assert.IsTrue(result.UsedBackup);
            Assert.AreEqual(10, result.Data.Inventory.Gold);
        }

        [Test]
        public void BothCorruptReportsErrorAndCanBeQuarantined()
        {
            system.Save(Sample(10));
            system.Save(Sample(20));
            File.WriteAllText(system.MainPath, "garbage");
            File.WriteAllText(system.BackupPath, "");

            var result = system.Load();
            Assert.IsNull(result.Data);
            Assert.IsNotNull(result.Error);

            system.QuarantineCorrupt();
            Assert.IsFalse(system.HasSave);
            Assert.AreEqual(2, Directory.GetFiles(directory, "*.corrupt_*").Length, "깨진 파일은 지우지 않고 보관");
        }

        [Test]
        public void NewerVersionIsRejected()
        {
            system.Save(Sample(10));
            File.WriteAllText(system.MainPath, File.ReadAllText(system.MainPath).Replace($"\"version\": {SaveData.CurrentVersion}", "\"version\": 99"));
            File.Delete(system.BackupPath);

            var result = system.Load();

            Assert.IsNull(result.Data);
            StringAssert.Contains("새로운 버전", result.Error);
        }

        [Test]
        public void DeleteRemovesEverything()
        {
            system.Save(Sample(1));
            system.Save(Sample(2));
            system.Delete();
            Assert.IsFalse(system.HasSave);
        }
    }

    public class GameSessionTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        private RelicData quill, book, lantern;
        private HeroData heroData;
        private GameDatabase database;

        [SetUp]
        public void SetUp()
        {
            var splash = TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 14);
            quill = TestData.Relic("relic_quill", splash, new MonsterStats(0, 4, 0));
            book = TestData.Relic("relic_book", TestData.Skill("shield", SkillKind.Guard, SkillTarget.AllAllies, 4), new MonsterStats(10, 0, 4));
            lantern = TestData.Relic("relic_lantern", TestData.Skill("light", SkillKind.Heal, SkillTarget.AllAllies, 8), new MonsterStats(15, 0, 0));
            heroData = TestData.Hero(new MonsterStats(60, 14, 10), quill).Set("growthPerLevel", new MonsterStats(8, 3, 2)).Set("startLevel", 3);
            database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new MonsterSpecies[0], new ItemData[0], null, new[] { quill, book, lantern });
        }

        // 저장 → JSON 문자열 → 다시 읽기 (실제 파일 저장과 같은 경로)
        private GameSession RoundTrip(GameSession session)
        {
            var json = session.ToSaveData(T0).ToJson();
            return GameSession.FromSaveData(SaveData.FromJson(json), database, heroData);
        }

        [Test]
        public void NewGameStartsWithHeroAndStartingRelic()
        {
            var session = GameSession.NewGame(heroData);

            Assert.AreEqual(3, session.Hero.Level, "HeroData의 시작 레벨");
            Assert.AreEqual(1, session.Hero.Relics.Count);
            Assert.IsTrue(session.Hero.IsEquipped(session.Hero.Find(quill)), "시작 성유물은 끼운 채로");
            Assert.AreEqual(session.Hero.Stats.MaxHp, session.Hero.CurrentHp);
            Assert.AreEqual(0, session.Vocabulary.DiscoveredCount);
        }

        [Test]
        public void EverythingSurvivesSaveAndLoad()
        {
            var session = GameSession.NewGame(heroData, 1);
            session.Hero.GainExp(25);          // Lv2, exp 5
            session.GrantRelic(book);
            var lanternOwned = session.GrantRelic(lantern);
            session.Hero.Unequip(session.Hero.Find(quill)); // 0번 칸 비움 → 칸 배치가 그대로 저장되는지
            session.Inventory.AddGold(30 + 60);         // 백과사전 +2 (재료 없는 테스트 성유물 — 골드만)
            RelicUpgrade.TryUpgrade(session.Hero.Find(book), session.Inventory);
            RelicUpgrade.TryUpgrade(session.Hero.Find(book), session.Inventory);
            session.Hero.TakeDamage(7);
            session.Inventory.AddGold(123);
            session.Inventory.Add("shiny_ink", 2);
            var rules = new MasteryRules();
            session.Vocabulary.RecordAnswer("abandon", true, T0, rules);
            session.Vocabulary.RecordAnswer("budget", false, T0, rules);
            session.Record.RecordBattle(true, 5, 1);
            session.Record.RecordFled(2, 1);

            var loaded = RoundTrip(session);
            var hero = loaded.Hero;

            Assert.AreEqual(2, hero.Level);
            Assert.AreEqual(5, hero.Exp);
            Assert.AreEqual(session.Hero.CurrentHp, hero.CurrentHp);
            Assert.AreEqual(3, hero.Relics.Count);
            Assert.IsNull(hero.SlotAt(0), "빈 칸 유지");
            Assert.AreSame(book, hero.SlotAt(1).Data);
            Assert.AreSame(lantern, hero.SlotAt(2).Data);
            Assert.IsFalse(hero.IsEquipped(hero.Find(quill)));
            Assert.AreEqual(2, hero.Find(book).Level, "강화 단계 유지");
            Assert.AreEqual(lanternOwned.Level, hero.Find(lantern).Level);
            Assert.AreEqual(123, loaded.Inventory.Gold);
            Assert.AreEqual(2, loaded.Inventory.GetCount("shiny_ink"));

            Assert.AreEqual(2, loaded.Vocabulary.DiscoveredCount);
            var abandon = loaded.Vocabulary.Find("abandon");
            Assert.AreEqual(MasteryLevel.Learning, abandon.Level);
            Assert.AreEqual(T0 + rules.GetReviewInterval(MasteryLevel.Learning), abandon.NextReviewUtc, "복습 시각 유지");
            Assert.AreEqual(T0, abandon.DiscoveredUtc, "발견 시각 유지 (도감용)");
            Assert.IsTrue(loaded.Vocabulary.Find("budget").InWrongNote, "오답 노트 유지");

            Assert.AreEqual(1, loaded.Record.BattlesWon);
            Assert.AreEqual(0, loaded.Record.BattlesLost, "도망은 진 것이 아님");
            Assert.AreEqual(1, loaded.Record.BattlesFled);
            Assert.AreEqual(2, loaded.Record.BattlesFought);
            Assert.AreEqual(7, loaded.Record.CorrectAnswers, "도망친 전투에서 푼 문제도 셈");
            Assert.AreEqual(0, loaded.LoadWarnings.Count);
        }

        [Test]
        public void LoadedVocabularyKeepsWorkingWithRules()
        {
            var session = GameSession.NewGame(heroData, 1);
            var rules = new MasteryRules();
            session.Vocabulary.RecordAnswer("abandon", true, T0, rules);

            var loaded = RoundTrip(session);
            var due = loaded.Vocabulary.Find("abandon").NextReviewUtc;
            var change = loaded.Vocabulary.RecordAnswer("abandon", true, due, rules);

            Assert.AreEqual(MasteryLevel.Reviewing, change.After);
            Assert.AreEqual(1, loaded.Vocabulary.Entries.Count, "불러온 뒤에도 같은 단어를 중복 기록하지 않음");
        }

        [Test]
        public void UnknownRelicIsDroppedWithWarning()
        {
            var save = new SaveData(T0, new HeroSaveData(4, 0, 30),
                new List<RelicSaveData> { new RelicSaveData("deleted_relic", 3, 0), new RelicSaveData("relic_book", 1, 1) },
                new Inventory(), new VocabularyProgress(), new PlayerRecord());

            var loaded = GameSession.FromSaveData(save, database, heroData);

            Assert.AreEqual(1, loaded.Hero.Relics.Count);
            Assert.AreSame(book, loaded.Hero.SlotAt(1).Data);
            Assert.AreEqual(4, loaded.Hero.Level);
            Assert.AreEqual(1, loaded.LoadWarnings.Count);
        }

        [Test]
        public void NoRelicsFallsBackToStartingRelicButKeepsWords()
        {
            var vocabulary = new VocabularyProgress();
            vocabulary.RecordAnswer("abandon", true, T0, new MasteryRules());
            var save = new SaveData(T0, new HeroSaveData(5, 0, 30), new List<RelicSaveData> { new RelicSaveData("gone", 1, 0) },
                new Inventory(), vocabulary, new PlayerRecord());

            var loaded = GameSession.FromSaveData(save, database, heroData);

            Assert.AreSame(quill, loaded.Hero.SlotAt(0).Data, "시작 성유물로 채움");
            Assert.AreEqual(5, loaded.Hero.Level);
            Assert.AreEqual(1, loaded.Vocabulary.DiscoveredCount);
        }

        [Test]
        public void FaintedHeroIsRestoredOnLoad()
        {
            var session = GameSession.NewGame(heroData, 1);
            session.Hero.TakeDamage(999);
            Assert.IsFalse(session.CanFight);

            var loaded = RoundTrip(session);

            Assert.AreEqual(loaded.Hero.Stats.MaxHp, loaded.Hero.CurrentHp);
        }

        // v1 세이브(몬스터 파티 시절): 파티에서 가장 높은 레벨이 주인공 레벨, 시작 성유물, 단어 기록·소지품은 그대로
        [Test]
        public void VersionOneMonsterPartySaveBecomesHero()
        {
            var vocabulary = new VocabularyProgress();
            vocabulary.RecordAnswer("abandon", true, T0, new MasteryRules());
            var inventory = new Inventory();
            inventory.AddGold(77);
            var v1 = SaveData.LegacyV1(T0,
                new List<MonsterSaveData> { new MonsterSaveData("nib", 4, 0, 10), new MonsterSaveData("bookshell", 6, 0, 10) },
                inventory, vocabulary, new PlayerRecord());

            var loaded = GameSession.FromSaveData(SaveData.FromJson(v1.ToJson()), database, heroData);

            Assert.AreEqual(6, loaded.Hero.Level);
            Assert.AreSame(quill, loaded.Hero.SlotAt(0).Data);
            Assert.AreEqual(loaded.Hero.Stats.MaxHp, loaded.Hero.CurrentHp);
            Assert.AreEqual(77, loaded.Inventory.Gold);
            Assert.AreEqual(1, loaded.Vocabulary.DiscoveredCount);
            Assert.AreEqual(SaveData.CurrentVersion, loaded.ToSaveData(T0).Version, "다시 저장하면 새 형식");
        }

        [Test]
        public void OldSaveMissingNewFieldsStillLoads()
        {
            // 필드가 늘어나도 예전 세이브(필드 없음)를 읽을 수 있어야 함
            var data = SaveData.FromJson("{\"version\":1,\"party\":[{\"speciesId\":\"nib\",\"level\":2,\"exp\":0,\"currentHp\":10}]}");
            var loaded = GameSession.FromSaveData(data, database, heroData);

            Assert.AreEqual(3, loaded.Hero.Level, "시작 레벨(3)보다 낮으면 시작 레벨");
            Assert.AreEqual(0, loaded.Inventory.Gold);
            Assert.AreEqual(0, loaded.Vocabulary.DiscoveredCount);
        }

        [Test]
        public void FieldPotionHealsOnlyWhenHurt()
        {
            var potion = TestData.Potion("potion", 40);
            var session = GameSession.NewGame(heroData, 1);
            session.Inventory.Add(potion, 2);

            Assert.AreEqual(0, session.UseHealingItem(potion), "HP가 가득하면 쓰지 않음");
            Assert.AreEqual(2, session.Inventory.GetCount(potion));

            session.Hero.TakeDamage(50);
            Assert.AreEqual(40, session.UseHealingItem(potion));
            Assert.AreEqual(1, session.Inventory.GetCount(potion));
        }
    }

    // 실제 프로젝트 데이터 점검: 새 몬스터를 만들고 GameDatabase 갱신을 잊으면 세이브에서 사라지므로 여기서 잡는다
    public class DataIntegrityTests
    {
        private static List<T> All<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        }

        [Test]
        public void GameDatabaseContainsEveryMonsterItemAndRelicWithUniqueIds()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            Assert.IsNotNull(database, "Assets/Data/GameDatabase.asset 이 없습니다 (WordRPG > Data > Refresh Game Database)");

            foreach (var species in All<MonsterSpecies>())
            {
                Assert.IsFalse(string.IsNullOrEmpty(species.SpeciesId), $"{species.name}: speciesId 비어 있음");
                Assert.AreSame(species, database.FindMonster(species.SpeciesId),
                    $"{species.name}이 GameDatabase에 없거나 id가 중복됩니다 (WordRPG > Data > Refresh Game Database)");
            }
            foreach (var item in All<ItemData>())
            {
                Assert.IsFalse(string.IsNullOrEmpty(item.ItemId), $"{item.name}: itemId 비어 있음");
                Assert.AreSame(item, database.FindItem(item.ItemId), $"{item.name}이 GameDatabase에 없거나 id가 중복됩니다");
            }
            var relics = All<RelicData>();
            Assert.Greater(relics.Count, 0, "성유물이 하나도 없습니다 (WordRPG > Data > Create Sample Data)");
            foreach (var relic in relics)
            {
                Assert.IsFalse(string.IsNullOrEmpty(relic.RelicId), $"{relic.name}: relicId 비어 있음");
                Assert.AreSame(relic, database.FindRelic(relic.RelicId), $"{relic.name}이 GameDatabase에 없거나 id가 중복됩니다");
                Assert.IsNotNull(relic.Skill, $"{relic.name}: 기술이 없습니다");
                Assert.IsNotNull(relic.UpgradeItem, $"{relic.name}: 강화 재료가 없습니다");
                Assert.AreEqual(RelicData.MaxLevel, relic.UpgradeCosts.Count, $"{relic.name}: 강화 비용은 +1~+5 다섯 개");
                Assert.IsNotNull(UI.UiKit.RelicIcon(relic), $"{relic.name}: 그림이 없습니다 (Tools/import_ninja_art.py)");
            }
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            Assert.IsNotNull(hero, "Assets/Data/Hero/hero.asset 이 없습니다");
            Assert.IsNotNull(hero.BasicSkill, "주인공 기본 기술이 없습니다");
            Assert.Greater(hero.StartingRelics.Count, 0, "시작 성유물이 없습니다");
        }

        // Art/NinjaAdventure/Monsters/{speciesId}.png 를 넣고 연결을 잊으면 임시 도형으로 나오므로 여기서 잡는다
        [Test]
        public void MonsterArtFilesAreLinkedToTheirSpecies()
        {
            const string folder = "Assets/Resources/Art/NinjaAdventure/Monsters";
            var species = All<MonsterSpecies>();
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string id = System.IO.Path.GetFileNameWithoutExtension(path);
                var owner = species.Find(s => s.SpeciesId == id);
                Assert.IsNotNull(owner, $"{path}: '{id}' 몬스터가 없습니다 (파일 이름 = speciesId)");
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(path), owner.Sprite,
                    $"{id} 그림이 연결되지 않았습니다 (WordRPG > Data > Link Monster Art)");
                count++;
            }
            Assert.Greater(count, 0, "몬스터 그림이 하나도 없습니다 (Tools/import_ninja_art.py)");
        }

        [Test]
        public void WordIdsAreUniqueAcrossAllWordBooks()
        {
            var seen = new Dictionary<string, string>();
            foreach (var book in All<WordDatabase>())
            {
                foreach (var word in book.Words)
                {
                    Assert.IsFalse(seen.ContainsKey(word.Id), $"단어 id '{word.Id}'가 여러 단어장({book.name} 등)에 중복");
                    seen[word.Id] = book.name;
                }
            }
            Assert.Greater(seen.Count, 0);
        }
    }
}
