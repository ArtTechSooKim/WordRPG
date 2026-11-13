using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Save;
using WordRPG.Words;

namespace WordRPG.Tests
{
    // 숲 상점부터 파는 물약 (#146): 살금살금 물약(몬스터 안 만남) · 호기심 물약(새 단어만) · 특제 상처약
    public class PotionEffectsTests
    {
        private static ItemData Potion(string id, ItemEffect effect, int amount) =>
            TestData.Item(id).Set("kind", ItemKind.Consumable).Set("effect", effect).Set("effectAmount", amount);

        private static GameSession Session() => GameSession.NewGame(TestData.Hero(new MonsterStats(100, 5, 5)), 1);

        [Test]
        public void DrinkingAddsTheEffectAndUsesOnePotion()
        {
            var session = Session();
            var quiet = Potion("quiet", ItemEffect.Quiet, 3);
            var curious = Potion("curious", ItemEffect.NewWords, 10);
            Assert.IsTrue(quiet.IsEffectItem);
            Assert.IsFalse(quiet.IsHealingItem, "전투 아이템 목록에는 안 나옴");
            Assert.IsFalse(session.UseEffectItem(quiet), "없으면 못 마심");

            session.Inventory.Add(quiet, 2);
            session.Inventory.Add(curious);
            Assert.IsTrue(session.UseEffectItem(quiet));
            Assert.AreEqual(1, session.Inventory.GetCount(quiet));
            Assert.AreEqual(3, session.Effects.QuietStepsLeft);
            Assert.IsTrue(session.UseEffectItem(quiet));
            Assert.AreEqual(6, session.Effects.QuietStepsLeft, "또 마시면 더해짐");
            Assert.IsTrue(session.UseEffectItem(curious));
            Assert.AreEqual(10, session.Effects.NewWordsLeft);
            Assert.IsFalse(session.UseEffectItem(TestData.Potion("plain", 40)), "상처약은 효과 물약이 아님");
        }

        [Test]
        public void QuietWearsOffStepByStep()
        {
            var effects = new PotionEffects(quietSteps: 2);
            Assert.IsTrue(effects.Quiet);
            Assert.IsFalse(effects.TakeStep());
            Assert.IsTrue(effects.Quiet);
            Assert.IsTrue(effects.TakeStep(), "이번 걸음으로 끝남 (안내용)");
            Assert.IsFalse(effects.Quiet);
            Assert.IsFalse(effects.TakeStep(), "효과가 없으면 아무 일 없음");
            Assert.AreEqual(0, effects.QuietStepsLeft);
        }

        // 세이브: 남은 효과가 저장되고, 예전 세이브에는 칸이 없어 효과 없이 시작 (형식 v2 그대로)
        [Test]
        public void EffectsSurviveSavingAndOldSavesStartWithout()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var hero = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            var session = GameSession.NewGame(hero, 10);
            session.Effects.Apply(db.FindItem("potion_quiet"));
            session.Effects.Apply(db.FindItem("potion_curious"));
            session.Effects.TakeStep();
            string json = JsonUtility.ToJson(session.ToSaveData(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc)));

            var loaded = GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(json), db, hero);
            Assert.AreEqual(99, loaded.Effects.QuietStepsLeft);
            Assert.AreEqual(10, loaded.Effects.NewWordsLeft);

            foreach (var key in new[] { "newWordsLeft", "quietStepsLeft" })
            {
                StringAssert.Contains($"\"{key}\"", json);
                json = Regex.Replace(json, $",\"{key}\":-?\\d+", "");
            }
            var old = GameSession.FromSaveData(JsonUtility.FromJson<SaveData>(json), db, hero);
            Assert.AreEqual(0, old.Effects.QuietStepsLeft);
            Assert.AreEqual(0, old.Effects.NewWordsLeft);
        }

        // 호기심 물약: 남은 만큼 새 단어만. 새 단어가 실제로 나올 때만 줄고, 이 단어장에 더 없으면 평소대로 (낭비 없음)
        [Test]
        public void CuriousPotionAsksOnlyNewWordsAndIsNotWastedWhenNoneAreLeft()
        {
            var words = new List<WordEntry>
            {
                new WordEntry("", "abandon", "버리다", "v"), new WordEntry("", "acquire", "얻다", "v"), new WordEntry("", "obtain", "손에 넣다", "v"),
                new WordEntry("", "budget", "예산", "n"), new WordEntry("", "reason", "이유", "n"), new WordEntry("", "offer", "제안", "n"),
            };
            var progress = new VocabularyProgress();
            var now = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
            // 이미 본 단어 둘은 틀려서 오답 노트 — 평소라면 이 단어가 먼저 나옴
            foreach (var seen in words.Take(2))
            {
                progress.Discover(seen.Id, now);
                progress.RecordAnswer(seen.Id, false, now, new MasteryRules());
            }
            var effects = new PotionEffects(newWords: 3);
            var quiz = new WordQuizService(words, progress, new MasteryRules(), new System.Random(1), utcNow: () => now,
                wantNewWord: () => effects.NewWordsLeft > 0, newWordTaken: effects.SpendNewWord);

            for (int i = 0; i < 3; i++)
            {
                var question = quiz.NextQuestion(QuizDirection.EnglishToMeaning);
                Assert.IsTrue(question.IsNewWord, $"{i + 1}번째도 새 단어 (오답 노트보다 먼저)");
                progress.Discover(question.Word.Id, now);
                quiz.SubmitAnswer(question, true);
            }
            Assert.AreEqual(0, effects.NewWordsLeft);
            Assert.IsFalse(quiz.NextQuestion(QuizDirection.EnglishToMeaning).IsNewWord, "효과가 끝나면 평소대로 (오답 노트)");

            // 단어장에 새 단어가 없으면 효과가 그대로 남음 → 다른 지역에서 쓸 수 있음
            var allSeen = new VocabularyProgress();
            foreach (var w in words) allSeen.Discover(w.Id, now);
            foreach (var w in words) allSeen.RecordAnswer(w.Id, true, now, new MasteryRules());
            var saved = new PotionEffects(newWords: 5);
            var other = new WordQuizService(words, allSeen, new MasteryRules(), new System.Random(1), utcNow: () => now,
                wantNewWord: () => saved.NewWordsLeft > 0, newWordTaken: saved.SpendNewWord);
            Assert.IsFalse(other.NextQuestion(QuizDirection.EnglishToMeaning).IsNewWord);
            Assert.AreEqual(5, saved.NewWordsLeft, "새 단어가 없으면 안 줄어듦");
        }

        // #265 사용자: 새 단어는 뜻 카드를 보여 준 뒤 묻기 때문에 보스전에서 물약이 들으면 답을 다 알고 싸움 → 보스전에서는 쉼
        [Test]
        public void CuriousPotionRestsDuringBossBattles()
        {
            var words = new List<WordEntry>
            {
                new WordEntry("", "abandon", "버리다", "v"), new WordEntry("", "acquire", "얻다", "v"), new WordEntry("", "obtain", "손에 넣다", "v"),
                new WordEntry("", "budget", "예산", "n"), new WordEntry("", "reason", "이유", "n"), new WordEntry("", "offer", "제안", "n"),
            };
            var progress = new VocabularyProgress();
            var now = new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
            foreach (var seen in words.Take(2)) // 오답 노트 둘 — 물약이 쉬면 평소대로 이 단어가 먼저
            {
                progress.Discover(seen.Id, now);
                progress.RecordAnswer(seen.Id, false, now, new MasteryRules());
            }
            var effects = new PotionEffects(newWords: 3);
            Assert.IsTrue(effects.WantsNewWord(bossBattle: false));
            Assert.IsFalse(effects.WantsNewWord(bossBattle: true));
            Assert.IsFalse(new PotionEffects().WantsNewWord(bossBattle: false), "물약을 안 마셨으면 평소대로");

            bool boss = true;
            var quiz = new WordQuizService(words, progress, new MasteryRules(), new System.Random(1), utcNow: () => now,
                wantNewWord: () => effects.WantsNewWord(boss), newWordTaken: effects.SpendNewWord);
            var question = quiz.NextQuestion(QuizDirection.EnglishToMeaning);
            Assert.IsFalse(question.IsNewWord, "보스전: 오답 노트 단어가 평소대로 먼저");
            quiz.SubmitAnswer(question, true);
            Assert.AreEqual(3, effects.NewWordsLeft, "보스전에서는 남은 수가 그대로");

            boss = false; // 다음 보통 전투에서 다시 들음
            Assert.IsTrue(quiz.NextQuestion(QuizDirection.EnglishToMeaning).IsNewWord);
            Assert.AreEqual(2, effects.NewWordsLeft);
        }

        private static (RoamingMonsters roaming, Roamer roamer) OneMonsterNear()
        {
            var map = FieldMap.Parse(new string('#', 30) + "\n#P" + new string(':', 18) + "," + new string(':', 8) + "#\n" + new string('#', 30)); // 몬스터는 시작에서 먼 서식지에
            var species = TestData.Species("slime", new MonsterStats(50, 6, 5), new MonsterStats(5, 1, 1));
            var roaming = new RoamingMonsters(map, rng => new List<MonsterInstance> { new MonsterInstance(species, 5) }, new System.Random(1));
            roaming.Populate(new Vector2Int(1, 1));
            Assert.AreEqual(1, roaming.Roamers.Count);
            return (roaming, roaming.Roamers[0]);
        }

        // 살금살금 물약: 가까이 가도 알아채지 않고, 쫓던 몬스터도 돌아감
        [Test]
        public void QuietPotionKeepsVisibleMonstersFromNoticing()
        {
            var (roaming, roamer) = OneMonsterNear();
            var near = roamer.Position + Vector2Int.left * 2;

            roaming.Quiet = true;
            for (int i = 0; i < 100; i++) Assert.IsEmpty(roaming.Tick(0.02f, near).Where(e => e.Kind == RoamerEventKind.Noticed || e.Kind == RoamerEventKind.Caught));
            Assert.IsNull(roaming.PreemptTarget(near), "[선제공격]도 안 뜸");
            Assert.IsFalse(roaming.HuntingPlayer);

            roaming.Quiet = false;
            for (int i = 0; i < 200 && !roaming.HuntingPlayer; i++) roaming.Tick(0.02f, near);
            Assert.IsTrue(roaming.HuntingPlayer, "효과가 끝나면 다시 알아챔");

            roaming.Quiet = true;
            roaming.Tick(0.02f, near);
            Assert.IsFalse(roaming.HuntingPlayer, "쫓던 몬스터도 돌아감");
        }

        // 실제 데이터: 초원 상점은 그대로, 숲은 특제 상처약까지, 효과 물약 두 가지는 정글부터 (#232 사용자 — 값도 밸런스로 2000·10000).
        // 동굴엔 상점이 없음 (#232 사용자: 동굴 속 가게는 이상함). 값은 사용자가 정한 관계대로
        [Test]
        public void ForestAndLaterShopsSellTheNewPotions()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            ShopEntry Entry(ShopData shop, string id) => shop.Entries.FirstOrDefault(e => e.Item.ItemId == id);

            var meadow = db.FindArea("meadow").Shop;
            CollectionAssert.AreEquivalent(new[] { "potion", "potion_large" }, meadow.Entries.Select(e => e.Item.ItemId), "초원은 그대로");
            var forest = db.FindArea("forest").Shop;
            CollectionAssert.AreEquivalent(new[] { "potion", "potion_large", "potion_great" }, forest.Entries.Select(e => e.Item.ItemId), "숲은 특제 상처약까지");
            Assert.IsNull(db.FindArea("cave").Shop, "동굴엔 상점 없음");
            Assert.AreEqual(0, db.FindArea("cave").Map.ShopBlocks.Count, "맵에도 가판대 없음");

            foreach (var areaId in new[] { "jungle" })
            {
                var shop = db.FindArea(areaId).Shop;
                var large = Entry(shop, "potion_large");
                var great = Entry(shop, "potion_great");
                var quiet = Entry(shop, "potion_quiet");
                var curious = Entry(shop, "potion_curious");
                Assert.IsNotNull(great, areaId);
                Assert.IsNotNull(quiet, areaId);
                Assert.IsNotNull(curious, areaId);
                Assert.Greater(great.Item.HealAmount, large.Item.HealAmount, "한 단계 더 좋음");
                Assert.AreEqual((float)large.Price / large.Item.HealAmount, (float)great.Price / great.Item.HealAmount, 0.001f, "회복량에 비례한 값");
                Assert.Greater(quiet.Price, large.Price, "몬스터 안 만나는 약은 조금 비싸게");
                Assert.Greater(curious.Price, great.Price, "새 단어 약은 비싸게");
                Assert.Greater(curious.Price, quiet.Price);
                Assert.AreEqual(ItemEffect.Quiet, quiet.Item.Effect);
                Assert.AreEqual(ItemEffect.NewWords, curious.Item.Effect);
                Assert.AreEqual(2000, quiet.Price, "살금살금 물약 (#232 사용자)");
                Assert.AreEqual(10000, curious.Price, "호기심 물약 (#232 사용자)");
            }
            foreach (var id in new[] { "potion_great", "potion_quiet", "potion_curious" })
            {
                Assert.IsNotNull(db.FindItem(id), $"{id}이 GameDatabase에 있어야 세이브에서 불러옴");
                Assert.IsNotNull(Resources.Load<Texture2D>($"Art/NinjaAdventure/Items/{id}"), $"{id} 그림");
            }
        }
    }
}
