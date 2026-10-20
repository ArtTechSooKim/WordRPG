using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WordRPG.Battle;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using Random = System.Random;

namespace WordRPG.Tests
{
    // 까먹대왕 밸런스 (#120 사용자: 두 방에 쓰러짐 → HP 2배 · 한 방 40 이상 · "힘을 모으고 있다" 뒤 기억상실 100).
    // 실제 데이터로 수백 번 싸움. 플레이어 흉내: 보스가 힘을 모으면 보호막, HP 45% 아래면 회복 기술, 40% 아래면 상처약(작은 것 2 · 큰 것 1),
    // 공격은 강도(단어 n개 연속). '빠른 플레이어' = 사용자처럼 1~3초 안에 답하고(늘 크리티컬) 콤보가 이어져 옴
    public class LibraryBossBalanceTests
    {
        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>("Assets/Data/" + path);

        private struct Result
        {
            public float WinRate, HpLeft, Rounds, Bonk, Amnesia;
        }

        // 서고 보스 앞 주인공 (사용자 기기: Lv12 · 깃펜 +5 · 백과사전 +5 · 마법 지팡이 +2)
        private static Hero UserLike() => BalanceSimulationTests.MakeHero(12, ("relic_quill", 5), ("relic_book", 5), ("relic_wand", 2));

        private static Result Fight(System.Func<Hero> makeHero, double accuracy, int intensity, bool fast, int runs = 300)
        {
            var boss = Load<MonsterSpecies>("Monsters/boss_forget_king.asset");
            var small = Load<ItemData>("Items/potion.asset");
            var large = Load<ItemData>("Items/potion_large.asset");
            var rng = new Random(7);
            int wins = 0;
            float rounds = 0, hpLeft = 0;
            var bonks = new List<int>();
            var amnesias = new List<int>();
            for (int i = 0; i < runs; i++)
            {
                var hero = makeHero();
                hero.RestoreFully();
                var bag = new Inventory();
                bag.Add(small, 2);
                bag.Add(large, 1);
                var battle = new BattleEngine(new List<ICombatant> { hero }, new List<MonsterInstance> { new MonsterInstance(boss, 7) },
                    new FakeQuizProvider(), new BattleConfig(), rng, fast ? 138 : 0, canFlee: false);
                int guard = 0;
                while (!battle.IsOver && guard++ < 400)
                {
                    var actor = battle.CurrentActor;
                    var events = new List<BattleEvent>();
                    var potion = actor.MaxHp - actor.Hp >= 120 && bag.GetCount(large) > 0 ? large
                        : bag.GetCount(small) > 0 ? small : bag.GetCount(large) > 0 ? large : null;
                    if ((float)actor.Hp / actor.MaxHp < 0.4f && potion != null)
                    {
                        events.AddRange(battle.UseItem(potion, bag));
                    }
                    else
                    {
                        var (skill, target) = Choose(battle, actor);
                        battle.SelectSkill(skill, target, skill.Kind == SkillKind.Damage ? intensity : 1);
                        while (battle.Phase == BattlePhase.AnsweringQuiz)
                        {
                            bool correct = rng.NextDouble() < accuracy;
                            float seconds = fast ? (float)(1.0 + rng.NextDouble() * 1.8) : (float)(1.5 + rng.NextDouble() * 4.5);
                            events.AddRange(battle.SubmitAnswer(correct ? 0 : 1, seconds));
                        }
                    }
                    foreach (var e in events)
                        if (e.Type == BattleEventType.Damage && e.Target.IsPlayerSide)
                            (e.Skill.SkillId == "boss_amnesia" ? amnesias : bonks).Add(e.Amount + e.Absorbed);
                }
                if (battle.Phase == BattlePhase.Victory)
                {
                    wins++;
                    hpLeft += (float)hero.CurrentHp / hero.Stats.MaxHp;
                }
                rounds += battle.Round;
            }
            return new Result
            {
                WinRate = (float)wins / runs, HpLeft = wins > 0 ? hpLeft / wins : 0f, Rounds = rounds / runs,
                Bonk = bonks.Count > 0 ? (float)bonks.Average() : 0f, Amnesia = amnesias.Count > 0 ? (float)amnesias.Average() : 0f
            };
        }

        private static (SkillData, BattleUnit) Choose(BattleEngine battle, BattleUnit actor)
        {
            var enemy = battle.Enemies.First(e => !e.IsDefeated);
            var guard = actor.Skills.FirstOrDefault(s => s.Kind == SkillKind.Guard);
            if (guard != null && battle.IsCharging(enemy) && actor.Shield < 40) return (guard, null); // 큰 한 방 전에 보호막
            if ((float)actor.Hp / actor.MaxHp < 0.45f)
            {
                var heal = actor.Skills.Where(s => s.Kind == SkillKind.Heal).OrderByDescending(s => s.Power).FirstOrDefault();
                if (heal != null) return (heal, heal.NeedsTargetChoice ? actor : null);
            }
            var hit = actor.Skills.Where(s => s.Kind == SkillKind.Damage).OrderByDescending(s => s.Power).First();
            return (hit, hit.Target == SkillTarget.AllEnemies ? null : enemy);
        }

        [Test]
        public void KingHitsHardAndChargesABigBlow()
        {
            var r = Fight(UserLike, 0.95, 4, fast: true);
            Assert.GreaterOrEqual(r.Bonk, 38f, $"Lv12(방어 41)에게도 한 방 40 안팎 — 평균 {r.Bonk:0.0}");
            Assert.That(r.Amnesia, Is.InRange(85f, 115f), $"기억상실은 100 안팎 — 평균 {r.Amnesia:0.0}");
        }

        [Test]
        public void KingIsTenseButBeatableForAQuickPlayer()
        {
            var r = Fight(UserLike, 0.95, 4, fast: true);
            Assert.Greater(r.WinRate, 0.85f, $"사용자처럼 빠르게·단어 4개로 싸우면 대부분 이김 — {r.WinRate:P0}");
            Assert.Less(r.HpLeft, 0.75f, $"그래도 많이 다침 (쫄깃) — 남은 HP 평균 {r.HpLeft:P0}");
        }

        // 숫자 표 — 레벨·정답률·강도별 (테스트 로그의 [BOSS] 줄)
        [Test, Explicit]
        public void Report()
        {
            var heroes = new (string name, System.Func<Hero> make)[]
            {
                ("Lv9  깃펜2 백과2 지팡이1", () => BalanceSimulationTests.MakeHero(9, ("relic_quill", 2), ("relic_book", 2), ("relic_wand", 1))),
                ("Lv10 깃펜3 백과3 지팡이2", () => BalanceSimulationTests.MakeHero(10, ("relic_quill", 3), ("relic_book", 3), ("relic_wand", 2))),
                ("Lv12 깃펜5 백과5 지팡이2", UserLike),
                ("Lv10 깃펜3 등불3 지팡이2", () => BalanceSimulationTests.MakeHero(10, ("relic_quill", 3), ("relic_lantern", 3), ("relic_wand", 2))),
            };
            var lines = new List<string>();
            foreach (var (name, make) in heroes)
            foreach (var (acc, intensity, fast) in new[] { (0.8, 1, false), (0.9, 3, false), (0.85, 4, true), (0.95, 4, true) })
            {
                var r = Fight(make, acc, intensity, fast);
                lines.Add($"{name,-20} | 정답률 {acc:P0} 강도{intensity}{(fast ? " 빠름" : "")} | 승률 {r.WinRate:P0} 남은HP {r.HpLeft:P0} " +
                          $"라운드 {r.Rounds:0.0} 방망이 {r.Bonk:0.0} 기억상실 {r.Amnesia:0.0}");
            }
            Debug.Log("[BOSS]\n" + string.Join("\n", lines));
        }
    }
}
