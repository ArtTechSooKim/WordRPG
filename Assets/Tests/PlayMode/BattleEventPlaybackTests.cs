using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WordRPG.Battle;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 전투 사건 연출 (#119): BattleScreen.PlayEvents가 사건마다 하는 연출(로그·소리·효과·떠오르는 숫자·HP 갱신·기다림)의 순서와 빠짐을 못 박는다.
    // 연출을 나누거나 고쳐도 이 순서가 그대로여야 함. 기록 = BattleScreen.CueRecorder + Sound.Recorder (평소엔 null)
    public class BattleEventPlaybackTests
    {
        private static readonly SkillData Strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 10);
        private static readonly SkillData Bite = TestData.Skill("bite", SkillKind.Damage, SkillTarget.SingleEnemy, 10);
        private static readonly SkillData Guard = TestData.Skill("guard", SkillKind.Guard, SkillTarget.Self, 10);
        private static readonly SkillData Mend = TestData.Skill("mend", SkillKind.Heal, SkillTarget.Self, 10);

        private readonly List<string> cues = new List<string>();
        private BattleScreen screen;
        private WordDatabase words;
        private BattleUnit hero, enemy;

        [TearDown]
        public void TearDown()
        {
            Sound.Recorder = null;
            if (screen != null)
            {
                screen.CueRecorder = null;
                Object.Destroy(screen.gameObject);
            }
        }

        private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
        private static string Bad => Hex(Palette.Bad);
        private static string Good => Hex(Palette.Good);
        private static string Info => Hex(Palette.Info);
        private static string Gold => Hex(Palette.Gold);
        private static string Dim => Hex(Palette.TextDim);

        private static MasteryChange Mastery(MasteryLevel before, MasteryLevel after, bool correct) => new MasteryChange("w", before, after, correct);

        private IEnumerator Begin(string enemyName = "슬라임", bool boss = false)
        {
            var heroData = TestData.Hero(new MonsterStats(100, 10, 10)).Set("basicSkill", Strike);
            var species = TestData.Species("enemy", new MonsterStats(40, 5, 5), new MonsterStats(0, 0, 0), Bite).Set("displayName", enemyName);
            words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            screen = new GameObject("BattleScreenUnderTest").AddComponent<BattleScreen>();
            screen.Configure(null, words, GameSession.NewGame(heroData, 1), animScale: 0.01f, loop: false);
            screen.BeginBattle(new List<MonsterInstance> { new MonsterInstance(species, 1) }, words, _ => { }, boss: boss);
            yield return WaitFor(() => screen.Engine != null && screen.Engine.Phase == BattlePhase.ChoosingSkill);
            hero = screen.Engine.Party[0];
            enemy = screen.Engine.Enemies[0];
        }

        private IEnumerator BeginTraining()
        {
            var heroData = TestData.Hero(new MonsterStats(100, 10, 10)).Set("basicSkill", Strike);
            var dummy = TestData.Species("dummy", new MonsterStats(80, 1, 5), new MonsterStats(0, 0, 0))
                .Set("displayName", "허수아비").Set("trainingDummy", true);
            words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            screen = new GameObject("BattleScreenUnderTest").AddComponent<BattleScreen>();
            var session = GameSession.NewGame(heroData, 1);
            // 수련은 이미 발견한 단어만 낸다 (TrainingScreenTests와 같이) — 하나 발견해 둠
            session.Vocabulary.RecordAnswer(words.Words[0].Id, true, System.DateTime.UtcNow, new MasteryRules());
            screen.Configure(null, words, session, animScale: 0.01f, loop: false);
            screen.BeginTraining(dummy, new[] { words }, words, TrainingMode.All, () => { });
            yield return WaitFor(() => screen.Engine != null && screen.Engine.Phase == BattlePhase.ChoosingSkill);
            hero = screen.Engine.Party[0];
            enemy = screen.Engine.Enemies[0];
        }

        private void StartRecording()
        {
            cues.Clear();
            screen.CueRecorder = cues.Add;
            Sound.Recorder = (sfx, pitch) =>
                cues.Add(pitch == 1f ? $"소리 {sfx}" : $"소리 {sfx} {pitch.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        private IEnumerator Play(params BattleEvent[] events)
        {
            StartRecording();
            // 연출 도중 예외가 나면 코루틴이 멈춰 끝을 기다리던 테스트가 영영 멈춤 (#119 버그 심기에서 겪음) → 시간 제한을 두고 실패로
            bool finished = false;
            screen.StartCoroutine(RunThenMark(screen.PlayEventsForTest(events), () => finished = true));
            yield return WaitFor(() => finished, 10f);
            screen.CueRecorder = null;
            Sound.Recorder = null;
            Assert.IsTrue(finished, $"연출이 끝나지 않음 (예외?)\n--- 그때까지 연출 ---\n{string.Join("\n", cues)}");
        }

        private static IEnumerator RunThenMark(IEnumerator routine, System.Action done)
        {
            yield return routine;
            done();
        }

        // 마지막엔 늘 실제 HP로 맞춤 (적 → 아군 순, [라운드 글자])
        private string End(string round = "라운드 1") =>
            $"HP 전체 {enemy.DisplayName} {enemy.Hp}/{enemy.Shield}, {hero.DisplayName} {hero.Hp}/{hero.Shield} [{round}]";

        private void AssertCues(params string[] expected)
        {
            for (int i = 0; i < Mathf.Max(expected.Length, cues.Count); i++)
            {
                string want = i < expected.Length ? expected[i] : "(없음)";
                string got = i < cues.Count ? cues[i] : "(없음)";
                Assert.AreEqual(want, got, $"{i}번째 연출이 다름\n--- 실제 전체 ---\n{string.Join("\n", cues)}");
            }
        }

        // 공격 한 차례: 정답 → 콤보 → 강도 ×1.5 기술 → 크리티컬 · 막음 · 보호막 흡수 → 쓰러짐 → 승리(연출 없음)
        [UnityTest]
        public IEnumerator AttackTurnPlaysEveryStepInOrder()
        {
            yield return Begin();
            yield return Play(
                BattleEvent.QuizAnswered(hero, true, Mastery(MasteryLevel.New, MasteryLevel.Learning, true)),
                BattleEvent.Combo(hero, 3),
                BattleEvent.SkillUsed(hero, Strike, 1.5f),
                BattleEvent.Damage(hero, enemy, Strike, 12, 0, true),
                BattleEvent.Damage(hero, enemy, Strike, 0, 4, false),
                BattleEvent.Damage(hero, enemy, Strike, 7, 3, false),
                BattleEvent.Defeated(enemy),
                BattleEvent.Victory());

            // #137: 기술을 쓰면 카드가 상대에게 달려감(몸짓) · 크리티컬도 기술 효과 + 폭발 · 보호막이 받아 내면 물결
            AssertCues(
                "로그 정답! [처음 만남 → 학습 ▲]", "기다림 0.5",
                "콤보 글자 3", "소리 Combo 1.08", "기다림 0.45",
                "로그 주인공의 strike ×1.5!", "몸짓 Lunge 주인공", $"숫자 주인공 ×1.5! {Gold} 56", "소리 Critical", "기다림 0.7",
                // 콤보 2단계(Good!)라 아군 공격 숫자는 +10 크게
                "HP 슬라임 28/0", "소리 Critical", "효과 Slash 슬라임", "효과 Explosion 슬라임", $"숫자 슬라임 크리티컬! -12 {Bad} 68", "로그 슬라임 피해 12 — 크리티컬!", "기다림 0.6",
                "HP 슬라임 28/0", "소리 Shield", "효과 Shield 슬라임", "보호막 물결 슬라임", $"숫자 슬라임 막음! {Info} 58", "로그 슬라임 피해 0 (보호막이 4 흡수)", "기다림 0.6",
                "HP 슬라임 21/0", "소리 Hit", "효과 Slash 슬라임", "보호막 물결 슬라임", $"숫자 슬라임 -7 {Bad} 58", "로그 슬라임 피해 7 (보호막이 3 흡수)", "기다림 0.6",
                "로그 슬라임 쓰러졌다!", "소리 Faint", "효과 Smoke 슬라임", "기다림 0.5",
                End());
            Assert.AreEqual("Good!", screen.LastComboText);
            Assert.AreEqual(0, screen.FinishersPlayed);
        }

        // 오답 → 기술 실패 → 적의 공격(아군은 할퀴기 효과, 콤보 크기 없음) → 새 라운드
        [UnityTest]
        public IEnumerator WrongAnswerAndEnemyTurnPlayInOrder()
        {
            yield return Begin();
            yield return Play(
                BattleEvent.QuizAnswered(hero, false, Mastery(MasteryLevel.Learning, MasteryLevel.New, false)),
                BattleEvent.SkillFailed(hero, Strike),
                BattleEvent.SkillUsed(enemy, Bite),
                BattleEvent.Damage(enemy, hero, Bite, 9, 0, false),
                BattleEvent.RoundStarted(2));

            AssertCues(
                "로그 오답… [학습 → 처음 만남 ▼]", "기다림 0.5",
                "로그 주인공의 strike 실패…", $"숫자 주인공 실패 {Dim} 44", "소리 Fail", "효과 Smoke 주인공", "기다림 0.7",
                "로그 슬라임의 bite!", "몸짓 Lunge 슬라임", "기다림 0.5",
                "HP 주인공 91/0", "소리 Hit", "효과 Claw 주인공", $"숫자 주인공 -9 {Bad} 48", "로그 주인공 피해 9", "기다림 0.6",
                End("라운드 2"), "기다림 0.2",
                End("라운드 2"));
        }

        // 보호막 → 회복(크리티컬) → 상처약: 화면 숫자는 사건마다 쌓임, 끝나면 실제 HP로
        [UnityTest]
        public IEnumerator ShieldHealAndItemPlayInOrder()
        {
            yield return Begin();
            var potion = TestData.Potion("상처약", 40);
            yield return Play(
                BattleEvent.QuizAnswered(hero, true, Mastery(MasteryLevel.Reviewing, MasteryLevel.Reviewing, true)),
                BattleEvent.SkillUsed(hero, Guard),
                BattleEvent.Shield(hero, hero, Guard, 15, false),
                BattleEvent.Heal(hero, hero, Mend, 20, true),
                BattleEvent.ItemUsed(hero, potion, 40));

            // #137: 보호막·회복 기술은 제자리에서 마법진(몸짓 Cast) · 보호막은 막이 튕기며 덮음 ·
            // 회복은 크게 찍히는 숫자 + 발밑 마법진·빛기둥·알갱이(회복 빛) — 상처약도 같은 빛
            AssertCues(
                "로그 정답! [복습]", "기다림 0.5",
                "로그 주인공의 guard!", "몸짓 Cast 주인공", "효과 Circle 주인공 느리게1 크기1.3", "기다림 0.5",
                "보호막 막 주인공", "HP 주인공 100/15", $"숫자 주인공 보호막 +15 {Info} 40", "소리 Shield", "효과 Shield 주인공", "로그 주인공 보호막 +15", "기다림 0.6",
                "HP 주인공 120/15", $"숫자 주인공 +20 {Good} 60", "소리 Heal", "회복 빛 주인공", "효과 Circle 주인공 느리게1 크기1.5", "효과 Heal 주인공",
                "로그 주인공 HP +20 — 크리티컬!", "기다림 0.6",
                "HP 주인공 160/15", $"숫자 주인공 +40 {Good} 60", "소리 Heal", "회복 빛 주인공", "효과 Circle 주인공 느리게1 크기1.5", "효과 Heal 주인공",
                "로그 상처약을 썼다! HP +40", "기다림 0.6",
                End());
        }

        // 도망: 성공 · 도망 문제(막은 적 있음/없음) · 실패. 도망 불가·승리·패배는 연출 없음 (도망 버튼·결과 화면이 맡음)
        [UnityTest]
        public IEnumerator FleeEventsPlayAndSilentEventsShowNothing()
        {
            yield return Begin();
            yield return Play(
                BattleEvent.Fled(hero),
                BattleEvent.FleeQuiz(hero, enemy),
                BattleEvent.FleeQuiz(hero, null),
                BattleEvent.FleeFailed(hero),
                BattleEvent.FleeBlocked(hero),
                BattleEvent.Defeat(),
                BattleEvent.Victory());

            AssertCues(
                "로그 주인공은 무사히 도망쳤다!", "소리 Flee", "기다림 0.6",
                "로그 슬라임이 길을 막았다! 도망치려면 단어를 맞혀라", "소리 Encounter", "기다림 0.7",
                "로그 도망치려면 단어를 맞혀라!", "소리 Encounter", "기다림 0.7",
                "로그 도망 실패! 차례를 놓쳤다", "소리 Fail", "기다림 0.6",
                End());
        }

        // 힘 모으기 (#120): "○○이 힘을 모으고 있다" — 금색 글자 · 반짝임 · 조금 길게 기다림 (기다림은 한 번)
        [UnityTest]
        public IEnumerator ChargingShowsTheWindUp()
        {
            yield return Begin("까먹대왕", boss: true);
            var amnesia = TestData.Skill("amnesia", SkillKind.Damage, SkillTarget.SingleEnemy, 230).Set("displayName", "기억상실").Set("chargeTurns", 1);
            yield return Play(BattleEvent.Charging(enemy, amnesia));

            AssertCues(
                "로그 까먹대왕이 힘을 모으고 있다", $"숫자 까먹대왕 힘을 모은다! {Gold} 48", "소리 EvolveLight", "효과 Sparkle 까먹대왕 느리게1.5 크기1.3", "기다림 0.9",
                End());
            Assert.AreEqual(1, Yields(BattleEvent.Charging(enemy, amnesia)));
        }

        // 보스전에서 아군이 보스를 쓰러뜨리는 마지막 한 방만 결정타 (앞의 타격은 보통), 보스는 천천히 쓰러짐
        [UnityTest]
        public IEnumerator BossFinisherIsOnlyTheLastHitThatWinsTheFight()
        {
            yield return Begin("까먹대왕", boss: true);
            yield return Play(
                BattleEvent.SkillUsed(hero, Strike),
                BattleEvent.Damage(hero, enemy, Strike, 5, 0, false),
                BattleEvent.Damage(hero, enemy, Strike, 30, 0, false),
                BattleEvent.Defeated(enemy),
                BattleEvent.Victory());

            AssertCues(
                "로그 주인공의 strike!", "몸짓 Lunge 주인공", "기다림 0.5",
                "HP 까먹대왕 35/0", "소리 Hit", "효과 Slash 까먹대왕", $"숫자 까먹대왕 -5 {Bad} 48", "로그 까먹대왕 피해 5", "기다림 0.6",
                "결정타 까먹대왕", "로그 결정타! 까먹대왕 피해 30", "소리 Critical 0.55", "소리 Hit 0.6",
                "효과 Explosion 까먹대왕 느리게3 크기1.8", $"숫자 까먹대왕 -30 {Bad} 96", "어둡게 0.75 0.25", "HP 까먹대왕 5/0",
                "로그 까먹대왕 쓰러졌다!", "소리 Faint 0.7", "효과 Smoke 까먹대왕 느리게2.5 크기1.4", "어둡게 0 0.6", "기다림 0.4",
                End());
            Assert.AreEqual(1, screen.FinishersPlayed);
        }

        // 보스전이라도: 적이 때린 것 · 아군이 쓰러진 것 · 승리가 아닌 한 방은 보통 연출
        [UnityTest]
        public IEnumerator BossBattleWithoutVictoryUsesNormalPlayback()
        {
            yield return Begin("까먹대왕", boss: true);
            yield return Play(
                BattleEvent.Damage(enemy, hero, Bite, 50, 0, false),
                BattleEvent.Defeated(hero),
                BattleEvent.Damage(hero, enemy, Strike, 40, 0, false),
                BattleEvent.Defeated(enemy),
                BattleEvent.Defeat());

            AssertCues(
                "HP 주인공 50/0", "소리 Hit", "효과 Claw 주인공", $"숫자 주인공 -50 {Bad} 48", "로그 주인공 피해 50", "기다림 0.6",
                "로그 주인공 쓰러졌다!", "소리 Faint", "효과 Smoke 주인공", "기다림 0.5",
                "HP 까먹대왕 0/0", "소리 Hit", "효과 Slash 까먹대왕", $"숫자 까먹대왕 -40 {Bad} 48", "로그 까먹대왕 피해 40", "기다림 0.6",
                "로그 까먹대왕 쓰러졌다!", "소리 Faint", "효과 Smoke 까먹대왕", "기다림 0.5",
                End());
            Assert.AreEqual(0, screen.FinishersPlayed);
        }

        // 수련 허수아비: HP는 그대로, '누적 피해'가 쌓이고 라운드 글자는 '수련'
        [UnityTest]
        public IEnumerator TrainingDummyKeepsItsHpAndCountsDamage()
        {
            yield return BeginTraining();
            yield return Play(
                BattleEvent.QuizAnswered(hero, true, Mastery(MasteryLevel.Learning, MasteryLevel.Reviewing, true)),
                BattleEvent.SkillUsed(hero, Strike),
                BattleEvent.Damage(hero, enemy, Strike, 25, 0, true),
                BattleEvent.Damage(hero, enemy, Strike, 10, 0, false),
                BattleEvent.RoundStarted(2));

            AssertCues(
                "로그 정답! [학습 → 복습 ▲]", "기다림 0.5",
                "로그 주인공의 strike!", "몸짓 Lunge 주인공", "기다림 0.5",
                "HP 허수아비 80/0", "소리 Critical", "효과 Slash 허수아비", "효과 Explosion 허수아비", $"숫자 허수아비 크리티컬! -25 {Bad} 58", "로그 허수아비 피해 25 — 크리티컬!", "기다림 0.6",
                "HP 허수아비 80/0", "소리 Hit", "효과 Slash 허수아비", $"숫자 허수아비 -10 {Bad} 48", "로그 허수아비 피해 10", "기다림 0.6",
                End("수련"), "기다림 0.2",
                End("수련"));
            StringAssert.Contains("누적 피해 35", AllText(screen));
        }

        // #137 기술마다 다른 연출: 돌팔매는 몸짓 없이 돌이 날아감 · 번개는 마법진 + 번쩍 · 글자 소나기 · 형광펜(기술 한 번에 한 줄) ·
        // 적의 울음은 소리 고리. 기다리는 시간은 그대로 (몸짓·효과는 겹쳐 돌아감)
        [UnityTest]
        public IEnumerator EachSkillPlaysItsOwnLook()
        {
            yield return Begin();
            var pebble = TestData.Skill("pebble", SkillKind.Damage, SkillTarget.SingleEnemy, 10).Set("fx", SkillFx.Rock);
            var bolt = TestData.Skill("bolt", SkillKind.Damage, SkillTarget.SingleEnemy, 10).Set("fx", SkillFx.Thunder);
            var shower = TestData.Skill("shower", SkillKind.Damage, SkillTarget.AllEnemies, 10).Set("fx", SkillFx.Letters);
            var marker = TestData.Skill("marker", SkillKind.Damage, SkillTarget.AllEnemies, 10).Set("fx", SkillFx.Highlight);
            var croak = TestData.Skill("croak", SkillKind.Damage, SkillTarget.AllEnemies, 10).Set("fx", SkillFx.Sound);
            yield return Play(
                BattleEvent.SkillUsed(hero, pebble), BattleEvent.Damage(hero, enemy, pebble, 3, 0, false),
                BattleEvent.SkillUsed(hero, bolt), BattleEvent.Damage(hero, enemy, bolt, 3, 0, false),
                BattleEvent.SkillUsed(hero, shower), BattleEvent.Damage(hero, enemy, shower, 3, 0, false),
                BattleEvent.SkillUsed(hero, marker), BattleEvent.Damage(hero, enemy, marker, 3, 0, false), BattleEvent.Damage(hero, enemy, marker, 3, 0, false),
                BattleEvent.SkillUsed(enemy, croak), BattleEvent.Damage(enemy, hero, croak, 3, 0, false));

            Assert.AreEqual("날아감 RockFly 주인공→슬라임", cues[cues.IndexOf("로그 주인공의 pebble!") + 1], "돌팔매는 몸짓 없이 돌만");
            AssertInOrder("날아감 RockFly 주인공→슬라임", "효과 Rock 슬라임 느리게1 크기1.1",
                "로그 주인공의 bolt!", "몸짓 Cast 주인공", "효과 Thunder 슬라임 느리게1 크기1.3", "번쩍",
                "로그 주인공의 shower!", "글자 소나기 슬라임",
                "로그 주인공의 marker!", "형광펜",
                "로그 슬라임의 croak!", "몸짓 Cast 슬라임", "효과 Ring 주인공", "소리 고리 주인공");
            Assert.AreEqual(1, cues.FindAll(c => c == "형광펜").Count, "형광펜은 기술 한 번에 한 줄");
            Assert.AreEqual(5 * 2 + 1, cues.FindAll(c => c.StartsWith("기다림")).Count, "사건마다 기다림 한 번 (기술 5 + 피해 6 = 11)");
        }

        // 보호막: 받아 내면 물결, 다 깎이면 깨짐(조각) + 로그 '보호막이 깨졌다!'. 남아 있는 동안만 카드 둘레 테두리
        [UnityTest]
        public IEnumerator ShieldRipplesThenBreaksWhenItRunsOut()
        {
            yield return Begin();
            yield return Play(
                BattleEvent.Shield(hero, hero, Guard, 5, false),
                BattleEvent.Damage(enemy, hero, Bite, 2, 3, false),
                BattleEvent.Damage(enemy, hero, Bite, 4, 2, false));

            AssertInOrder("보호막 막 주인공", "HP 주인공 100/5",
                "HP 주인공 98/2", "보호막 물결 주인공", "로그 주인공 피해 2 (보호막이 3 흡수)",
                "HP 주인공 94/0", "보호막 깨짐 주인공", "로그 주인공 피해 4 (보호막이 2 흡수 — 보호막이 깨졌다!)");
            Assert.AreEqual(1, cues.FindAll(c => c.StartsWith("보호막 깨짐")).Count);

            var view = UnitView.Create(screen.transform, "AuraCard", 0f, 0f, 1f, 1f);
            view.Bind(hero);
            view.Sync(50, 5);
            Assert.IsTrue(ShieldAura(view).activeSelf, "보호막이 있으면 테두리");
            view.Sync(50, 0);
            Assert.IsFalse(ShieldAura(view).activeSelf, "다 깎이면 테두리도 꺼짐");
        }

        private static GameObject ShieldAura(UnitView view)
        {
            foreach (var t in view.Root.GetComponentsInChildren<Transform>(true))
                if (t.name == "ShieldAura") return t.gameObject;
            return null;
        }

        // 연출이 기다리는 횟수 (= 프레임을 넘기는 자리): 사건마다 한 번, 결정타 한 번, 보스 쓰러짐은 어둡게 + 기다림 두 번, 연출 없는 사건은 0.
        // 나누기 전후로 같아야 화면 속도가 그대로 (기다림 안쪽은 따로 돌리지 않고 횟수만 셈)
        [UnityTest]
        public IEnumerator EachEventWaitsTheSameNumberOfTimes()
        {
            yield return Begin("까먹대왕", boss: true);
            Assert.AreEqual(0, Yields(BattleEvent.FleeBlocked(hero), BattleEvent.Victory(), BattleEvent.Defeat()), "연출 없는 사건만이면 기다리지 않고 끝남");
            Assert.AreEqual(9, Yields(
                BattleEvent.QuizAnswered(hero, true, Mastery(MasteryLevel.New, MasteryLevel.Learning, true)),
                BattleEvent.Combo(hero, 2),
                BattleEvent.SkillUsed(hero, Strike),
                BattleEvent.Damage(hero, enemy, Strike, 5, 0, false),
                BattleEvent.Shield(hero, hero, Guard, 5, false),
                BattleEvent.Heal(hero, hero, Mend, 5, false),
                BattleEvent.Fled(hero),
                BattleEvent.FleeFailed(hero),
                BattleEvent.RoundStarted(2)));
            Assert.AreEqual(3, Yields(
                BattleEvent.Damage(hero, enemy, Strike, 40, 0, false),
                BattleEvent.Defeated(enemy),
                BattleEvent.Victory()), "결정타 1 + 보스 쓰러짐 2");
        }

        private int Yields(params BattleEvent[] events)
        {
            var routine = screen.PlayEventsForTest(events);
            int count = 0;
            while (routine.MoveNext())
            {
                Assert.IsInstanceOf<IEnumerator>(routine.Current, "연출은 기다림·결정타·어둡게만 넘김");
                count++;
            }
            return count;
        }

        // 실제 전투 한 판 (버튼을 눌러서): 사건 순서가 로직 → 연출로 그대로 이어지는지. 전체 기록은 로그에 남김 (나누기 전후 비교용)
        [UnityTest]
        public IEnumerator RealBattleWinPlaysAnswerSkillHitThenDefeat()
        {
            var cfg = NoVariance();
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var heroData = TestData.Hero(new MonsterStats(100, 30, 10)).Set("basicSkill", strike);
            var species = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), strike).Set("displayName", "슬라임");
            yield return RealBattle(heroData, species, cfg, answerCorrectly: true);

            Assert.AreEqual(BattlePhase.Victory, screen.Engine.Phase);
            AssertInOrder("로그 정답!", "로그 주인공의 strike!", "숫자 슬라임 크리티컬!", "로그 슬라임 피해", "로그 슬라임 쓰러졌다!", "소리 Faint", "효과 Smoke 슬라임", "HP 전체");
        }

        [UnityTest]
        public IEnumerator RealBattleLossPlaysMissEnemyHitThenFall()
        {
            var cfg = NoVariance();
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var heroData = TestData.Hero(new MonsterStats(30, 5, 1)).Set("basicSkill", strike);
            var species = TestData.Species("enemy", new MonsterStats(999, 30, 50), new MonsterStats(0, 0, 0), strike).Set("displayName", "슬라임");
            yield return RealBattle(heroData, species, cfg, answerCorrectly: false);

            Assert.AreEqual(BattlePhase.Defeat, screen.Engine.Phase);
            AssertInOrder("로그 오답…", "로그 주인공의 strike 실패…", "소리 Fail", "로그 슬라임의 strike!", "효과 Claw 주인공", "로그 주인공 피해",
                "로그 주인공 쓰러졌다!", "소리 Faint", "HP 전체");
        }

        // 기본값 그대로, 피해 흔들림만 0 (기록이 매번 같게)
        private static BattleConfig NoVariance() =>
            new BattleConfig(answerTimeLimitSeconds: 10f, criticalTimeSeconds: 3f, criticalMultiplier: 1.5f, variance: 0f, expPerCorrectAnswer: 2);

        private IEnumerator RealBattle(HeroData heroData, MonsterSpecies species, BattleConfig cfg, bool answerCorrectly)
        {
            words = ScriptableObject.CreateInstance<WordDatabase>();
            words.ReplaceWords(TestData.SampleWords());
            screen = new GameObject("BattleScreenUnderTest").AddComponent<BattleScreen>();
            screen.Configure(null, words, GameSession.NewGame(heroData, 1), animScale: 0.01f, config: cfg, loop: false);
            StartRecording();
            screen.BeginBattle(new List<MonsterInstance> { new MonsterInstance(species, 1) }, words, _ => { });
            yield return PlayUntilResult(screen, answerCorrectly);
            screen.CueRecorder = null;
            Sound.Recorder = null;
            Debug.Log($"[CUE {TestContext.CurrentContext.Test.Name}]\n" + string.Join("\n", cues));
        }

        // 기록에서 이 글들로 시작하는 연출이 이 순서로 나오는지 (사이에 다른 연출은 있어도 됨)
        private void AssertInOrder(params string[] prefixes)
        {
            int at = 0;
            foreach (var prefix in prefixes)
            {
                while (at < cues.Count && !cues[at].StartsWith(prefix)) at++;
                Assert.Less(at, cues.Count, $"'{prefix}' 연출이 순서대로 나오지 않음\n--- 실제 전체 ---\n{string.Join("\n", cues)}");
                at++;
            }
        }
    }
}
