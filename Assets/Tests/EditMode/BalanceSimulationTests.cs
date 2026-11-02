using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WordRPG.Battle;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.Words;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 밸런스 시뮬레이션 (#88, B-3): 실제 데이터(주인공·성유물·몬스터·아라)로 전투를 수백 번 돌려 이길 만한지 본다.
    // 플레이어 흉내: 단어 정답률 p, 대답 시간 1.5~6초(3초 안이면 크리티컬), HP가 낮으면 회복 기술, 적이 둘 이상이면 전체 공격.
    // 숫자는 [Explicit] 보고서(BalanceReport)로 출력하고, 일반 테스트는 '정글이 지나치게 쉽지도 어렵지도 않은지'만 확인한다
    public class BalanceSimulationTests
    {
        private const string Data = "Assets/Data/";

        private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Data + path);

        // 정답을 항상 0번에 두는 문제 (단어는 아무거나)
        private class SimQuiz : IQuizProvider
        {
            private static readonly WordEntry Word = new WordEntry("sim", "sim", "시뮬", "n");
            public QuizQuestion NextQuestion(QuizDirection direction) =>
                new QuizQuestion(Word, direction, false, "sim", new[] { "정답", "오답1", "오답2", "오답3" }, 0);
            public MasteryChange SubmitAnswer(QuizQuestion question, bool correct) =>
                new MasteryChange(question.Word.Id, MasteryLevel.Learning, MasteryLevel.Learning, correct);
        }

        public struct Outcome
        {
            public bool Won;
            public float HeroHpLeft; // 0~1
            public int Rounds;
        }

        public static Hero MakeHero(int level, params (string relicId, int upgrade)[] relics)
        {
            var hero = new Hero(Load<HeroData>("Hero/hero.asset"), level);
            foreach (var (id, upgrade) in relics)
            {
                var data = Load<RelicData>($"Relics/{id}.asset");
                var owned = hero.Owns(data) ? hero.Find(data) : hero.AddRelic(data, upgrade);
                if (!hero.IsEquipped(owned)) hero.Equip(owned);
            }
            return hero;
        }

        public static Outcome Fight(Hero hero, Companion ally, IEnumerable<(string speciesId, int level)> enemies, double accuracy, Random rng)
        {
            hero.RestoreFully();
            ally?.RestoreFully();
            var party = new List<ICombatant> { hero };
            if (ally != null) party.Add(ally);
            var foes = enemies.Select(e => new MonsterInstance(Load<MonsterSpecies>($"Monsters/{e.speciesId}.asset"), e.level)).ToList();
            var battle = new BattleEngine(party, foes, new SimQuiz(), new BattleConfig(), rng, 0, canFlee: false);
            int guard = 0;
            while (!battle.IsOver && guard++ < 400)
            {
                var actor = battle.CurrentActor;
                var (skill, target) = Choose(battle, actor);
                battle.SelectSkill(skill, target);
                bool correct = rng.NextDouble() < accuracy;
                float seconds = (float)(1.5 + rng.NextDouble() * 4.5);
                battle.SubmitAnswer(correct ? 0 : 1, seconds);
            }
            return new Outcome { Won = battle.Phase == BattlePhase.Victory, HeroHpLeft = (float)hero.CurrentHp / hero.Stats.MaxHp, Rounds = battle.Round };
        }

        private static (SkillData, BattleUnit) Choose(BattleEngine battle, BattleUnit actor)
        {
            var skills = actor.Skills;
            var aliveEnemies = battle.Enemies.Where(e => !e.IsDefeated).ToList();
            var allies = battle.Party.Where(p => !p.IsDefeated).ToList();
            var weakest = allies.OrderBy(a => (float)a.Hp / a.MaxHp).First();
            // 회복: 누군가 HP 45% 아래
            if ((float)weakest.Hp / weakest.MaxHp < 0.45f)
            {
                var heal = skills.Where(s => s.Kind == SkillKind.Heal).OrderByDescending(s => s.Power).FirstOrDefault();
                if (heal != null)
                {
                    if (heal.Target == SkillTarget.SingleAlly) return (heal, weakest);
                    if (heal.Target == SkillTarget.Self && (float)actor.Hp / actor.MaxHp < 0.45f) return (heal, actor);
                    if (heal.Target == SkillTarget.AllAllies) return (heal, null);
                }
            }
            var damage = skills.Where(s => s.Kind == SkillKind.Damage).ToList();
            if (aliveEnemies.Count >= 2)
            {
                var area = damage.Where(s => s.Target == SkillTarget.AllEnemies).OrderByDescending(s => s.Power).FirstOrDefault();
                if (area != null) return (area, null);
            }
            var single = damage.OrderByDescending(s => s.Power).First();
            var target = aliveEnemies.OrderBy(e => e.Hp).First();
            return (single, single.Target == SkillTarget.AllEnemies ? null : target);
        }

        public static (float winRate, float hpLeft, float rounds) Many(Func<Hero> hero, Func<Hero, Companion> ally,
            (string, int)[] enemies, double accuracy, int runs = 300, int seed = 1)
        {
            int wins = 0;
            float hp = 0f, rounds = 0f;
            var rng = new Random(seed);
            for (int i = 0; i < runs; i++)
            {
                var h = hero();
                var o = Fight(h, ally?.Invoke(h), enemies, accuracy, rng);
                if (o.Won) wins++;
                hp += o.Won ? o.HeroHpLeft : 0f;
                rounds += o.Rounds;
            }
            return ((float)wins / runs, wins > 0 ? hp / wins : 0f, rounds / runs);
        }

        // 숲 보스를 막 이긴 주인공(Lv13, 성유물 셋) + 같은 레벨 아라 — 정글 일반 전투는 대부분 이기고, 보스는 레벨을 좀 올리면 이길 만해야
        private static Hero JungleHero(int level) => MakeHero(level, ("relic_wand", 2), ("relic_whip", 1), ("relic_leaf", 0));
        private static Companion Ara(Hero hero) => new Companion(Load<CompanionData>("Companions/ara.asset"), hero.Level);

        [Test]
        public void JungleFieldBattlesAreWinnableRightAfterTheForest()
        {
            foreach (var group in new[] { new[] { ("tail_lizard", 13), ("tail_lizard", 13) }, new[] { ("batchim_kappa", 15) },
                         new[] { ("mimic_octopus", 14), ("mimic_octopus", 14) } })
            {
                var (win, hp, _) = Many(() => JungleHero(13), Ara, group, 0.75);
                Assert.Greater(win, 0.85f, $"정답률 75%로 {string.Join(",", group)} — 이긴 비율 {win:P0}");
            }
        }

        // 1.2 어두운 밀림 동굴 (#115): 정글 보스를 이긴 주인공(Lv17, 글자 구슬·지팡이·조롱박) + 아라 — 일반 전투는 대부분 이김
        private static Hero CaveHero(int level) => MakeHero(level, ("relic_letter_gem", 1), ("relic_wand", 2), ("relic_gourd", 1));

        [Test]
        public void CaveFieldBattlesAreWinnableRightAfterTheJungle()
        {
            foreach (var group in new[] { new[] { ("blink_ghost", 16), ("blink_ghost", 16) }, new[] { ("chew_larva", 17) },
                         new[] { ("tangle_spider", 17) }, new[] { ("tangle_spider", 16), ("blink_ghost", 15) }, new[] { ("ink_newt", 17), ("ink_newt", 17) } })
            {
                var (win, _, _) = Many(() => CaveHero(17), Ara, group, 0.75);
                Assert.Greater(win, 0.85f, $"정답률 75%로 {string.Join(",", group)} — 이긴 비율 {win:P0}");
            }
        }

        // 헷갈너구리 (#144 사용자: 서고 보스보다 너무 약함 — 비슷하거나 더 어렵게): 보스보다 같은 만큼 높은 레벨·같은 강화로 맞붙어 비교한다
        private static Hero LibraryBossHero(int level, int quill, int book, int lantern) =>
            MakeHero(level, ("relic_quill", quill), ("relic_book", book), ("relic_lantern", lantern));

        private static Hero ForestBossHero(int level, int wand, int whip, int book) =>
            MakeHero(level, ("relic_wand", wand), ("relic_whip", whip), ("relic_book", book));

        private static float BiggestHitShare(string speciesId, int bossLevel, Hero hero)
        {
            var species = Load<MonsterSpecies>($"Monsters/{speciesId}.asset");
            var boss = new MonsterInstance(species, bossLevel);
            return species.Skills.Where(s => s.Kind == SkillKind.Damage)
                .Max(s => BattleFormulas.ExpectedDamage(s.Power, boss.Stats.Attack, hero.Stats.Defense)) / (float)hero.Stats.MaxHp;
        }

        private static Hero JungleBossHero(int level, int wand, int whip, int leaf) =>
            MakeHero(level, ("relic_wand", wand), ("relic_whip", whip), ("relic_leaf", leaf));

        private static Hero CaveBossHero(int level, int gem, int wand, int gourd) =>
            MakeHero(level, ("relic_letter_gem", gem), ("relic_wand", wand), ("relic_gourd", gourd));

        [Test]
        public void ForestBossIsAtLeastAsHardAsTheLibraryBoss()
        {
            var king = new[] { ("boss_forget_king", 7) };
            var raccoon = new[] { ("boss_muddle_raccoon", 12) };
            foreach (var (gap, a, b, c) in new[] { (3, 2, 1, 1), (5, 3, 2, 2), (7, 5, 3, 3) })
            {
                var (kingWin, _, _) = Many(() => LibraryBossHero(7 + gap, a, b, c), null, king, 0.8);
                var (raccoonWin, _, _) = Many(() => ForestBossHero(12 + gap, a, b, c), null, raccoon, 0.8);
                Assert.LessOrEqual(raccoonWin, kingWin + 0.1f, $"보스보다 {gap}레벨 위: 너구리 {raccoonWin:P0} / 대왕 {kingWin:P0}");
                Assert.GreaterOrEqual(BiggestHitShare("boss_muddle_raccoon", 12, ForestBossHero(12 + gap, a, b, c)),
                    BiggestHitShare("boss_forget_king", 7, LibraryBossHero(7 + gap, a, b, c)) * 0.9f, $"가장 큰 한 방이 대왕만큼 ({gap}레벨 위)");
            }
            Assert.Greater(BiggestHitShare("boss_muddle_raccoon", 12, MakeHero(14, ("relic_quill", 5), ("relic_lantern", 5), ("relic_book", 5))), 0.4f,
                "+5 셋 Lv14(사용자가 쉽다고 한 상태)에도 큰 한 방은 HP의 40% 넘게");
        }

        private static int HitsToDefeat(string speciesId, int bossLevel, Hero hero)
        {
            var boss = new MonsterInstance(Load<MonsterSpecies>($"Monsters/{speciesId}.asset"), bossLevel);
            int best = hero.Skills.Where(s => s.Kind == SkillKind.Damage).Max(s => BattleFormulas.ExpectedDamage(s.Power, hero.Stats.Attack, boss.Stats.Defense));
            return (int)Math.Ceiling((double)boss.Stats.MaxHp / best);
        }

        // 보스는 뒤로 갈수록 어렵게 (#147 사용자): 보스보다 같은 만큼 높은 레벨·같은 강화로 맞붙으면 큰 한 방이 내 HP에서 차지하는 몫이 커지고,
        // 정글부터는 쓰러뜨리는 데 드는 대수도 많아짐. 그래도 HP가 가득하면 한 방에 쓰러지지는 않음.
        // 이기는 비율은 보지 않음 — 시뮬레이션은 강도 고르기·상처약을 안 써서 서고 보스부터 거의 0% (체감은 휴대폰에서)
        [Test]
        public void BossesGetHarderInStoryOrder()
        {
            foreach (var (gap, a, b, c) in new[] { (3, 2, 1, 1), (5, 3, 2, 2), (7, 5, 3, 3) })
            {
                var bosses = new (string id, int level, Hero hero)[]
                {
                    ("boss_forget_king", 7, LibraryBossHero(7 + gap, a, b, c)),
                    ("boss_muddle_raccoon", 12, ForestBossHero(12 + gap, a, b, c)),
                    ("boss_blank_frog", 17, JungleBossHero(17 + gap, a, b, c)),
                    ("boss_ellipsis_vine", 20, CaveBossHero(20 + gap, a, b, c)),
                };
                for (int i = 0; i < bosses.Length; i++)
                {
                    var (id, level, hero) = bosses[i];
                    float hit = BiggestHitShare(id, level, hero);
                    Assert.Less(hit, 1f, $"{id} ({gap}레벨 위): 가득 찬 HP를 한 방에 깎으면 안 됨");
                    if (i == 0) continue;
                    var before = bosses[i - 1];
                    Assert.Greater(hit, BiggestHitShare(before.id, before.level, before.hero), $"{id} ({gap}레벨 위): 큰 한 방이 앞 보스보다 세야");
                    if (i >= 2) Assert.Greater(HitsToDefeat(id, level, hero), HitsToDefeat(before.id, before.level, before.hero), $"{id} ({gap}레벨 위): 쓰러뜨리는 데 앞 보스보다 오래 걸려야");
                }
            }
        }

        // 레벨 차이 경험치 (#88): 몬스터보다 높을수록 한 레벨에 25%씩 줄고 5%에서 멈춤
        [Test]
        public void ExpFallsOffWhenOverLeveled()
        {
            var config = new BattleConfig();
            Assert.AreEqual(1f, config.ExpFactor(10, 12), 1e-4f, "몬스터가 더 높으면 그대로");
            Assert.AreEqual(1f, config.ExpFactor(12, 12), 1e-4f);
            Assert.AreEqual(0.75f, config.ExpFactor(13, 12), 1e-4f);
            Assert.AreEqual(0.25f, config.ExpFactor(15, 12), 1e-4f);
            Assert.AreEqual(0.05f, config.ExpFactor(30, 12), 1e-4f, "최소 5%");

            var lizard = new MonsterInstance(Load<MonsterSpecies>("Monsters/tail_lizard.asset"), 13);
            var same = BattleRewardCalculator.Calculate(new[] { lizard }, 0, config, new Random(1), 13);
            var above = BattleRewardCalculator.Calculate(new[] { lizard }, 0, config, new Random(1), 15);
            Assert.AreEqual(lizard.Species.ExpReward * 13, same.Exp);
            Assert.AreEqual((int)System.Math.Round(lizard.Species.ExpReward * 13 * 0.5f), above.Exp, "두 레벨 위 = 절반");
        }

        // 적 회복 (#88): HP가 절반 아래일 때만, 한 전투에 두 번까지
        [Test]
        public void EnemiesHealOnlyWhenHurtAndAtMostTwice()
        {
            var heal = TestData.Skill("snack", SkillKind.Heal, SkillTarget.Self, 5);
            var bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var healer = TestData.Species("healer", new MonsterStats(400, 1, 999), new MonsterStats(0, 0, 0), bite, heal);
            var hero = new Hero(TestData.Hero(new MonsterStats(500, 10, 999)).Set("basicSkill",
                TestData.Skill("tap", SkillKind.Damage, SkillTarget.SingleEnemy, 1)), 5);
            var battle = new BattleEngine(new ICombatant[] { hero }, new List<MonsterInstance> { new MonsterInstance(healer, 1) },
                new FakeQuizProvider(), new BattleConfig(), new Random(3));
            var enemy = battle.Enemies[0];
            int Heals(IReadOnlyList<BattleEvent> events) => events.Count(e => e.Type == BattleEventType.Heal && e.Actor == enemy);

            int fullHeals = 0;
            for (int round = 0; round < 10; round++)
            {
                battle.SelectSkill(battle.Party[0].Skills[0], enemy);
                fullHeals += Heals(battle.SubmitAnswer(0, 5f));
            }
            Assert.AreEqual(0, fullHeals, "HP가 절반 넘으면 회복하지 않음 (공격만)");

            int hurtHeals = 0;
            for (int round = 0; round < 16; round++)
            {
                if (enemy.Hp * 2 >= enemy.MaxHp) enemy.Monster.TakeDamage(enemy.Hp - enemy.MaxHp / 4); // 늘 절반 아래로
                battle.SelectSkill(battle.Party[0].Skills[0], enemy);
                hurtHeals += Heals(battle.SubmitAnswer(0, 5f));
            }
            Assert.AreEqual(2, hurtHeals, "다쳐도 한 전투에 두 번까지");
        }

        // 보고서: [Explicit] — 숫자 표를 출력 (테스트 결과 XML의 output)
        [Test, Explicit]
        public void BalanceReport()
        {
            void Row(string label, Func<Hero> hero, Func<Hero, Companion> ally, (string, int)[] enemies, double acc)
            {
                var (win, hp, rounds) = Many(hero, ally, enemies, acc);
                TestContext.Out.WriteLine($"{label,-44} 정답률 {acc:P0} → 이김 {win,5:P0} · 남은 HP {hp,5:P0} · 라운드 {rounds:0.0}");
            }
            // 보스 앞 주인공 비교 (#144): 적의 한 대가 내 HP의 몇 %인지 · 내 가장 센 기술로 몇 대에 쓰러지는지
            void Threat(string label, string speciesId, int level, Func<Hero> make)
            {
                var hero = make();
                var species = Load<MonsterSpecies>($"Monsters/{speciesId}.asset");
                var boss = new MonsterInstance(species, level);
                var hits = species.Skills.Where(s => s.Kind == SkillKind.Damage)
                    .Select(s => $"{s.DisplayName} {BattleFormulas.ExpectedDamage(s.Power, boss.Stats.Attack, hero.Stats.Defense) * 100 / hero.Stats.MaxHp}%");
                var best = hero.Skills.Where(s => s.Kind == SkillKind.Damage).Max(s => BattleFormulas.ExpectedDamage(s.Power, hero.Stats.Attack, boss.Stats.Defense));
                TestContext.Out.WriteLine($"{label,-40} 내 HP {hero.Stats.MaxHp} · 공 {hero.Stats.Attack} · 방 {hero.Stats.Defense} / 보스 HP {boss.Stats.MaxHp}: {string.Join(" · ", hits)} / 내 {best}씩 {Math.Ceiling((double)boss.Stats.MaxHp / best)}대");
            }
            var bossRows = new (string label, string id, int level, Func<Hero> hero)[]
            {
                ("까먹대왕 Lv7 vs Lv10 (+2·+1·+1)", "boss_forget_king", 7, () => LibraryBossHero(10, 2, 1, 1)),
                ("까먹대왕 Lv7 vs Lv12 (+3·+2·+2)", "boss_forget_king", 7, () => LibraryBossHero(12, 3, 2, 2)),
                ("까먹대왕 Lv7 vs Lv14 (+5·+3·+3)", "boss_forget_king", 7, () => LibraryBossHero(14, 5, 3, 3)),
                ("헷갈너구리 Lv12 vs Lv15 (+2·+1·+1)", "boss_muddle_raccoon", 12, () => ForestBossHero(15, 2, 1, 1)),
                ("헷갈너구리 Lv12 vs Lv17 (+3·+2·+2)", "boss_muddle_raccoon", 12, () => ForestBossHero(17, 3, 2, 2)),
                ("헷갈너구리 Lv12 vs Lv19 (+5·+3·+3)", "boss_muddle_raccoon", 12, () => ForestBossHero(19, 5, 3, 3)),
                ("헷갈너구리 Lv12 vs Lv14 (+5 셋 — 사용자)", "boss_muddle_raccoon", 12, () => MakeHero(14, ("relic_quill", 5), ("relic_lantern", 5), ("relic_book", 5))),
                ("빈칸두꺼비 Lv17 vs Lv20 (+2·+1·+1)", "boss_blank_frog", 17, () => JungleBossHero(20, 2, 1, 1)),
                ("빈칸두꺼비 Lv17 vs Lv22 (+3·+2·+2)", "boss_blank_frog", 17, () => JungleBossHero(22, 3, 2, 2)),
                ("빈칸두꺼비 Lv17 vs Lv24 (+5·+3·+3)", "boss_blank_frog", 17, () => JungleBossHero(24, 5, 3, 3)),
                ("말줄임덩굴 Lv20 vs Lv23 (+2·+1·+1)", "boss_ellipsis_vine", 20, () => CaveBossHero(23, 2, 1, 1)),
                ("말줄임덩굴 Lv20 vs Lv25 (+3·+2·+2)", "boss_ellipsis_vine", 20, () => CaveBossHero(25, 3, 2, 2)),
                ("말줄임덩굴 Lv20 vs Lv27 (+5·+3·+3)", "boss_ellipsis_vine", 20, () => CaveBossHero(27, 5, 3, 3)),
            };
            foreach (var (label, id, level, make) in bossRows)
            {
                Threat(label, id, level, make);
                foreach (double acc in new[] { 0.6, 0.8 }) Row("  " + label, make, null, new[] { (id, level) }, acc);
                if (id == "boss_blank_frog" || id == "boss_ellipsis_vine") Row("  " + label + " + 아라", make, Ara, new[] { (id, level) }, 0.8);
            }
            foreach (double acc in new[] { 0.6, 0.8 })
            {
                Row("서고 보스 까먹대왕 Lv7 vs Lv8 (깃펜+1·백과사전·등불)", () => MakeHero(8, ("relic_quill", 1), ("relic_book", 0), ("relic_lantern", 0)), null, new[] { ("boss_forget_king", 7) }, acc);
                Row("숲 보스 헷갈너구리 Lv12 vs Lv13 (지팡이+1·채찍·백과+1)", () => MakeHero(13, ("relic_wand", 1), ("relic_whip", 0), ("relic_book", 1)), null, new[] { ("boss_muddle_raccoon", 12) }, acc);
                foreach (int lv in new[] { 13, 16, 18 })
                {
                    Row($"정글 Lv{lv}+아라 vs 말꼬리 도마뱀 Lv13 ×2", () => JungleHero(lv), Ara, new[] { ("tail_lizard", 13), ("tail_lizard", 13) }, acc);
                    Row($"정글 Lv{lv}+아라 vs 받침갓파 Lv15", () => JungleHero(lv), Ara, new[] { ("batchim_kappa", 15) }, acc);
                    Row($"정글 Lv{lv}+아라 vs 흉내문어 Lv14 ×2", () => JungleHero(lv), Ara, new[] { ("mimic_octopus", 14), ("mimic_octopus", 14) }, acc);
                    Row($"정글 Lv{lv}+아라 vs 빈칸두꺼비 Lv17", () => JungleHero(lv), Ara, new[] { ("boss_blank_frog", 17) }, acc);
                    Row($"정글 Lv{lv} 혼자 vs 빈칸두꺼비 Lv17", () => JungleHero(lv), null, new[] { ("boss_blank_frog", 17) }, acc);
                }
                foreach (int lv in new[] { 17, 19, 21 })
                {
                    Row($"동굴 Lv{lv}+아라 vs 깜빡유령 Lv16 ×2", () => CaveHero(lv), Ara, new[] { ("blink_ghost", 16), ("blink_ghost", 16) }, acc);
                    Row($"동굴 Lv{lv}+아라 vs 곱씹애벌레 Lv17", () => CaveHero(lv), Ara, new[] { ("chew_larva", 17) }, acc);
                    Row($"동굴 Lv{lv}+아라 vs 엉킴거미 Lv17", () => CaveHero(lv), Ara, new[] { ("tangle_spider", 17) }, acc);
                    Row($"동굴 Lv{lv}+아라 vs 먹물도롱뇽 Lv17 ×2", () => CaveHero(lv), Ara, new[] { ("ink_newt", 17), ("ink_newt", 17) }, acc);
                    Row($"동굴 Lv{lv}+아라 vs 말줄임덩굴 Lv20", () => CaveHero(lv), Ara, new[] { ("boss_ellipsis_vine", 20) }, acc);
                    Row($"동굴 Lv{lv} 혼자 vs 말줄임덩굴 Lv20", () => CaveHero(lv), null, new[] { ("boss_ellipsis_vine", 20) }, acc);
                }
            }
            // 게임 전체 레벨 흐름: 지역마다 전투 N번(평균 적 레벨·기본 경험치·마릿수·정답 4개) → 보스 앞 주인공 레벨
            var areas = new (string name, int battles, int enemyLevel, int baseExp, double group, int boss)[]
            {
                ("초원", 15, 2, 7, 1.5, 0), ("잊혀진 서고", 20, 4, 9, 2.0, 7), ("숲", 25, 9, 11, 2.0, 12), ("메아리 정글", 25, 14, 15, 1.5, 17),
                ("어두운 밀림 동굴", 30, 17, 17, 1.5, 20),
            };
            foreach (var (falloff, floor) in new[] { (0f, 1f), (0.15f, 0.1f), (0.25f, 0.05f), (0.3f, 0.05f) })
            {
                var line = new System.Text.StringBuilder($"레벨 차이 경험치 {falloff:P0}씩 (최소 {floor:P0}): ");
                var walker = MakeHero(3);
                foreach (var a in areas)
                {
                    for (int i = 0; i < a.battles; i++)
                    {
                        int monsters = i % 2 == 0 ? (int)Math.Ceiling(a.group) : (int)Math.Floor(a.group);
                        int exp = 4 * 2;
                        for (int m = 0; m < monsters; m++)
                        {
                            int gap = walker.Level - a.enemyLevel;
                            float factor = gap <= 0 ? 1f : Math.Max(floor, 1f - falloff * gap);
                            exp += (int)Math.Round(a.baseExp * a.enemyLevel * factor);
                        }
                        walker.GainExp(exp);
                    }
                    line.Append($"{a.name} 끝 Lv{walker.Level}" + (a.boss > 0 ? $"(보스 {a.boss}) " : " "));
                }
                TestContext.Out.WriteLine(line.ToString());
            }
        }
    }
}
