using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;

namespace WordRPG.Tests
{
    // 실제 UI 버튼의 onClick을 눌러서 전투 한 판이 끝까지 진행되는지 확인하는 통합 테스트
    public class BattleScreenTests
    {
        private static T Set<T>(T obj, string field, object value) where T : class
        {
            var info = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            info.SetValue(obj, value);
            return obj;
        }

        private static EncounterTable Table(MonsterSpecies enemy)
        {
            var table = ScriptableObject.CreateInstance<EncounterTable>();
            Set(table, "entries", new List<EncounterTable.Entry> { new EncounterTable.Entry(enemy, 1, 1, 1) });
            Set(table, "minGroupSize", 1);
            Set(table, "maxGroupSize", 1);
            return table;
        }

        private static WordDatabase Words()
        {
            var db = ScriptableObject.CreateInstance<WordDatabase>();
            db.ReplaceWords(TestData.SampleWords());
            return db;
        }

        // 주인공: 기본 기술 = basic (성유물 없음)
        private static HeroData Hero(MonsterStats stats, SkillData basic) => TestData.Hero(stats).Set("basicSkill", basic);

        private BattleScreen CreateScreen(HeroData hero, MonsterSpecies enemy, GameDatabase database = null, GameSession session = null)
        {
            var go = new GameObject("BattleScreenUnderTest");
            var screen = go.AddComponent<BattleScreen>();
            screen.Configure(Table(enemy), Words(), session ?? GameSession.NewGame(hero, 1), animScale: 0.01f, gameDatabase: database);
            return screen;
        }

        [UnityTest]
        public IEnumerator WinningABattleThroughTheUi()
        {
            LogAssert.ignoreFailingMessages = false;
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var hero = Hero(new MonsterStats(100, 30, 10), strike);
            var enemy = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), strike)
                .Set("expReward", 20).Set("goldReward", 7);
            var screen = CreateScreen(hero, enemy);

            yield return null;
            yield return null;
            yield return PlayUntilResult(screen, answerCorrectly: true);

            Assert.IsTrue(screen.IsResultVisible, "결과 패널이 떠야 함");
            Assert.AreEqual("승리!", screen.ResultTitle);
            Assert.AreEqual(BattlePhase.Victory, screen.Engine.Phase);
            Assert.Greater(screen.Engine.CorrectAnswers, 0);
            Assert.Greater(screen.Hero.Exp + screen.Hero.Level, 1, "경험치가 지급돼야 함");
            Assert.Greater(screen.Vocabulary.Entries.Count, 0, "맞힌 단어가 학습 기록에 남아야 함");

            // 결산 (#41): 성장 · 가장 활약한 기술 · 기록 · 처음 본 단어
            var result = AllText(screen.transform.Find("BattleCanvas/SafeArea/ResultPanel"));
            StringAssert.Contains("물리쳤어요", result);
            StringAssert.Contains("경험치 +", result);
            StringAssert.Contains("가장 활약한 기술", result);
            StringAssert.Contains("(기본 기술)", result);
            StringAssert.Contains($"정답 {screen.Engine.Summary.Correct} / {screen.Engine.Summary.Correct + screen.Engine.Summary.Wrong}", result);
            StringAssert.Contains($"새로 만난 단어 {screen.Engine.Summary.NewWords.Count}", result);
            Assert.Greater(screen.Engine.Summary.NewWords.Count, 0, "첫 전투의 단어는 모두 새 단어");
            StringAssert.Contains(screen.Engine.Summary.NewWords[0].English, result);
            Assert.AreEqual(0, screen.FinishersPlayed, "보스전이 아니면 결정타 연출 없음");
            var card = (RectTransform)screen.transform.Find("BattleCanvas/SafeArea/ResultPanel/Card");
            var overlay = (RectTransform)card.parent;
            Assert.LessOrEqual(card.rect.height * card.localScale.y, overlay.rect.height, "결산 판이 화면 안에 들어가야 함");

            // 결과 버튼을 누르면 다음 전투가 시작된다
            var next = ActiveButton(screen.transform, "ResultButton_Primary");
            Assert.IsNotNull(next);
            var firstEngine = screen.Engine;
            next.onClick.Invoke();
            for (int i = 0; i < 30 && screen.Engine == firstEngine; i++) yield return null;
            Assert.AreNotSame(firstEngine, screen.Engine);
            Assert.IsFalse(screen.IsResultVisible);

            Object.Destroy(screen.gameObject);
        }

        [UnityTest]
        public IEnumerator LosingABattleThroughTheUi()
        {
            var weak = TestData.Skill("weak", SkillKind.Damage, SkillTarget.SingleEnemy, 1);
            var smash = TestData.Skill("smash", SkillKind.Damage, SkillTarget.SingleEnemy, 500);
            var hero = Hero(new MonsterStats(10, 5, 1), weak);
            var enemy = TestData.Species("enemy", new MonsterStats(500, 50, 50), new MonsterStats(0, 0, 0), smash);
            var screen = CreateScreen(hero, enemy);

            yield return null;
            yield return null;
            yield return PlayUntilResult(screen, answerCorrectly: false);

            Assert.IsTrue(screen.IsResultVisible);
            Assert.AreEqual("패배…", screen.ResultTitle);
            Assert.Greater(screen.Engine.WrongAnswers, 0);
            var wrongWord = screen.Vocabulary.Entries[0];
            Assert.IsTrue(wrongWord.InWrongNote, "틀린 단어는 오답 노트에 있어야 함");
            var defeatText = AllText(screen.transform.Find("BattleCanvas/SafeArea/ResultPanel"));
            StringAssert.Contains($"틀린 단어 {screen.Engine.Summary.WrongWords.Count} — 오답 노트에 넣었어요", defeatText);
            StringAssert.Contains(screen.Engine.Summary.WrongWords[0].English, defeatText);

            StringAssert.Contains("주인공이 쓰러졌다", AllText(screen.transform.Find("BattleCanvas/SafeArea/ResultPanel")));

            // 재도전하면 주인공이 회복된다
            ActiveButton(screen.transform, "ResultButton_Primary").onClick.Invoke();
            for (int i = 0; i < 30 && screen.IsResultVisible; i++) yield return null;
            Assert.AreEqual(screen.Hero.Stats.MaxHp, screen.Hero.CurrentHp);

            Object.Destroy(screen.gameObject);
        }

        // 실제 흐름: GameManager가 있는 상태로 전투 → 자동 저장 → 앱 재시작(GameManager 새로 생성) → 이어하기
        [UnityTest]
        public IEnumerator ProgressIsSavedAndRestoredAfterRestart()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WordRPG_PlaySave_" + System.Guid.NewGuid().ToString("N"));
            GameManager.SaveDirectoryOverride = dir;
            try
            {
                var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
                var hero = Hero(new MonsterStats(100, 30, 10), strike).Set("growthPerLevel", new MonsterStats(5, 1, 1));
                var enemy = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), strike)
                    .Set("expReward", 20).Set("goldReward", 7);
                var database = ScriptableObject.CreateInstance<GameDatabase>();
                database.ReplaceContents(new[] { enemy }, new ItemData[0]);

                GameManager StartManager()
                {
                    var managerGo = new GameObject("GameManager");
                    managerGo.SetActive(false);
                    managerGo.AddComponent<GameManager>().Configure(database, hero);
                    managerGo.SetActive(true); // 여기서 Awake → 세이브 읽기
                    return GameManager.Instance;
                }

                // 1회차: 새 게임 → 전투 승리
                var manager = StartManager();
                Assert.IsFalse(manager.LoadedFromSave);
                var screenGo = new GameObject("BattleScreen");
                var screen = screenGo.AddComponent<BattleScreen>();
                screen.Configure(Table(enemy), Words(), animScale: 0.01f); // 세션은 GameManager 것을 사용
                yield return null;
                yield return null;
                Assert.AreSame(manager.Session, screen.Session);

                yield return PlayUntilResult(screen, answerCorrectly: true);
                Assert.AreEqual("승리!", screen.ResultTitle);
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "save.json")), "자동 저장 파일");

                int discovered = manager.Session.Vocabulary.DiscoveredCount;
                int exp = manager.Session.Hero.Exp;
                int level = manager.Session.Hero.Level;
                int gold = manager.Session.Inventory.Gold;
                Assert.Greater(discovered, 0);
                Assert.Greater(gold, 0);

                // 앱 종료
                Object.Destroy(screenGo);
                Object.Destroy(manager.gameObject);
                yield return null;
                Assert.IsNull(GameManager.Instance);

                // 2회차: 다시 켜면 이어하기
                var restarted = StartManager();
                Assert.IsTrue(restarted.LoadedFromSave);
                StringAssert.Contains("이어하기", restarted.StatusMessage);
                Assert.AreEqual(discovered, restarted.Session.Vocabulary.DiscoveredCount);
                Assert.AreEqual(level, restarted.Session.Hero.Level);
                Assert.AreEqual(exp, restarted.Session.Hero.Exp);
                Assert.AreEqual(gold, restarted.Session.Inventory.Gold);
                Assert.AreEqual(1, restarted.Session.Record.BattlesWon);

                Object.Destroy(restarted.gameObject);
                yield return null;
            }
            finally
            {
                GameManager.SaveDirectoryOverride = null;
                if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            }
        }

        // 도감 화면: 미발견은 ???, 목록 개수, 닫기. 스킬 선택 중에만 열린다
        [UnityTest]
        public IEnumerator DexShowsUndiscoveredWordsAsHidden()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var hero = Hero(new MonsterStats(100, 30, 10), strike);
            var enemy = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), strike);
            var screen = CreateScreen(hero, enemy);
            yield return null;
            yield return null;

            var open = FindButton(screen.transform, "DexButton");
            Assert.IsTrue(open.interactable, "스킬 선택 중에는 도감을 열 수 있다");
            open.onClick.Invoke();
            yield return null;

            int rowCount = 0;
            foreach (var button in screen.GetComponentsInChildren<Button>(true))
            {
                if (button.name.StartsWith("DexRow_")) rowCount++;
            }
            Assert.AreEqual(TestData.SampleWords().Count, rowCount);

            var firstRow = FindButton(screen.transform, "DexRow_0");
            StringAssert.Contains("???", AllText(firstRow));
            firstRow.onClick.Invoke();
            StringAssert.Contains("아직 발견하지 못한", AllText(screen.transform.Find("BattleCanvas/SafeArea/DexView")));

            FindButton(screen.transform, "DexCloseButton").onClick.Invoke();
            Assert.IsFalse(screen.transform.Find("BattleCanvas/SafeArea/DexView").gameObject.activeSelf);

            // 문제를 푸는 중에는 도감 버튼이 잠긴다 (공격 기술 → 강도 ×1 → 문제)
            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            yield return null;
            ActiveButton(screen.transform, "Intensity_0").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsFalse(FindButton(screen.transform, "DexButton").interactable);

            Object.Destroy(screen.gameObject);
        }

        // 단어장을 전부 발견하면 징표와 골드가 지급되고 결과 화면에 표시, 도감에서도 완료 표시
        [UnityTest]
        public IEnumerator CompletingDexGivesKeepsakeAndGold()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var hero = Hero(new MonsterStats(100, 30, 10), strike);
            var enemy = TestData.Species("enemy", new MonsterStats(40, 1, 50), new MonsterStats(0, 0, 0), strike)
                .Set("goldReward", 0);
            var keepsake = TestData.Item("keepsake_test").Set("displayName", "시험 징표");
            var oneWord = ScriptableObject.CreateInstance<WordDatabase>()
                .Set("regionId", "test").Set("regionName", "시험 지역")
                .Set("completionKeepsake", keepsake).Set("completionGold", 777);
            oneWord.ReplaceWords(new List<WordEntry> { new WordEntry("", "abandon", "버리다, 포기하다", "v") });

            var session = GameSession.NewGame(hero, 1);
            var go = new GameObject("DexCompletionTest");
            var screen = go.AddComponent<BattleScreen>();
            screen.Configure(Table(enemy), oneWord, session, animScale: 0.01f);
            yield return null;
            yield return null;
            yield return PlayUntilResult(screen, answerCorrectly: true);

            Assert.IsTrue(screen.IsResultVisible);
            Assert.AreEqual(777, session.Inventory.Gold);
            Assert.AreEqual(1, session.Inventory.GetCount(keepsake));
            var resultText = AllText(screen.transform.Find("BattleCanvas/SafeArea/ResultPanel"));
            StringAssert.Contains("도감 완성", resultText);
            StringAssert.Contains("시험 징표", resultText);

            // 결과 화면에서 도감을 열면 '획득 완료'
            FindButton(screen.transform, "DexButton").onClick.Invoke();
            yield return null;
            StringAssert.Contains("획득 완료", AllText(screen.transform.Find("BattleCanvas/SafeArea/DexView")));
            Assert.AreEqual(777, session.Inventory.Gold, "도감을 열어도 중복 지급 없음");

            Object.Destroy(go);
        }

        // 기술 버튼 = 기본 기술 + 끼운 성유물 기술 (어느 성유물인지 표시), 맨 아래 [가방]으로 상처약 → 문제 없이 회복 + 한 턴
        [UnityTest]
        public IEnumerator RelicSkillsAndPotionThroughTheUi()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 10);
            var splash = TestData.Skill("splash", SkillKind.Damage, SkillTarget.AllEnemies, 10).Set("displayName", "잉크 뿌리기");
            var quill = TestData.Relic("relic_quill", splash, new MonsterStats(0, 2, 0)).Set("displayName", "깃펜");
            var hero = Hero(new MonsterStats(100, 10, 10), strike);
            var enemy = TestData.Species("enemy", new MonsterStats(500, 5, 50), new MonsterStats(0, 0, 0), strike);
            var potion = TestData.Potion("potion", 40).Set("displayName", "상처약");
            var database = ScriptableObject.CreateInstance<GameDatabase>();
            database.ReplaceContents(new[] { enemy }, new[] { potion }, null, new[] { quill });
            var session = GameSession.NewGame(hero, 1);
            session.GrantRelic(quill);
            session.Inventory.Add(potion, 2);
            var screen = CreateScreen(hero, enemy, database, session);
            yield return null;
            yield return null;
            yield return WaitFor(() => ActiveButton(screen.transform, "SkillButton_0") != null);

            var skillText = AllText(screen.transform.Find("BattleCanvas/SafeArea/Bottom/SkillPanel"));
            StringAssert.Contains("기본 · ", skillText);
            StringAssert.Contains("깃펜 · ", skillText, "성유물 기술은 어느 성유물인지");
            StringAssert.Contains("잉크 뿌리기", skillText);
            Assert.IsFalse(FindButton(screen.transform, "ItemButton").interactable, "HP가 가득하면 상처약 잠금");

            // HP를 깎고 한 턴 진행 → 다음 차례에 [가방]이 열린다 → 상처약
            session.Hero.TakeDamage(60);
            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            yield return PlayOneTurn(screen);
            yield return WaitFor(() => ActiveButton(screen.transform, "ItemButton") != null, 5f);
            Assert.IsNotNull(ActiveButton(screen.transform, "ItemButton"), "다친 상태면 [가방]을 누를 수 있다");
            int answered = screen.Engine.CorrectAnswers + screen.Engine.WrongAnswers;
            int round = screen.Engine.Round;
            ActiveButton(screen.transform, "ItemButton").onClick.Invoke();
            yield return null;
            StringAssert.Contains("상처약  × 2", AllText(screen.transform.Find("BattleCanvas/SafeArea/Bottom/SkillPanel")));
            int hpBefore = session.Hero.CurrentHp;
            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            yield return WaitFor(() => screen.Engine.Round > round, 5f);

            Assert.AreEqual(1, session.Inventory.GetCount(potion));
            Assert.Greater(session.Hero.CurrentHp, hpBefore, "상처약 40 회복 (적 공격 몇 점보다 큼)");
            Assert.AreEqual(answered, screen.Engine.CorrectAnswers + screen.Engine.WrongAnswers, "상처약은 문제 없이");

            Object.Destroy(screen.gameObject);
        }

        [UnityTest]
        public IEnumerator ComboTextShowsAndStreakCarriesOverUntilAMiss()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40);
            var hero = Hero(new MonsterStats(500, 30, 10), strike);
            var tank = TestData.Species("tank", new MonsterStats(9999, 1, 50), new MonsterStats(0, 0, 0), strike);
            var session = GameSession.NewGame(hero, 1);
            session.ComboStreak = 4; // 지난 전투에서 4연속 정답
            var screen = CreateScreen(hero, tank, session: session);
            yield return null;
            yield return null;

            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            yield return PlayOneTurn(screen);
            Assert.AreEqual(5, session.ComboStreak, "이어서 5연속");
            Assert.AreEqual("Excellent!", screen.LastComboText);

            // 이번엔 틀림 → 콤보 끊김
            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            // 강도 ×1 → 새 단어 카드가 먼저 뜨면 닫고, 보기 버튼이 나타날 때까지
            for (int frame = 0; frame < 600 && ActiveButton(screen.transform, "Choice_0") == null; frame++)
            {
                ActiveButton(screen.transform, "Intensity_0")?.onClick.Invoke();
                ActiveButton(screen.transform, "CardConfirmButton")?.onClick.Invoke();
                yield return null;
            }
            int wrong = (screen.Engine.CurrentQuestion.CorrectIndex + 1) % screen.Engine.CurrentQuestion.Choices.Count;
            ActiveButton(screen.transform, $"Choice_{wrong}").onClick.Invoke();
            yield return WaitFor(() => session.ComboStreak == 0, 5f);
            Assert.AreEqual(0, session.ComboStreak, "틀리면 처음부터");

            Object.Destroy(screen.gameObject);
        }

        [UnityTest]
        public IEnumerator ChargedAttackAsksTwoWordsAndShowsProgress()
        {
            var strike = TestData.Skill("strike", SkillKind.Damage, SkillTarget.SingleEnemy, 40).Set("displayName", "찌르기");
            var hero = Hero(new MonsterStats(500, 30, 10), strike);
            var tank = TestData.Species("tank", new MonsterStats(9999, 1, 50), new MonsterStats(0, 0, 0), strike);
            var session = GameSession.NewGame(hero, 1);
            var screen = CreateScreen(hero, tank, session: session);
            yield return null;
            yield return null;

            // 공격 기술을 누르면 강도 고르기 (×1 ~ ×2, 예상 피해)
            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            yield return null;
            var panelText = AllText(screen.transform.Find("BattleCanvas/SafeArea/Bottom/SkillPanel"));
            StringAssert.Contains("강도를 고르세요", panelText);
            StringAssert.Contains("단어 2개 연속", panelText);
            StringAssert.Contains("×2", panelText);
            StringAssert.Contains("예상 피해", panelText);
            Assert.IsNull(ActiveButton(screen.transform, "SkillButton_0"), "강도 고르는 동안 기술 버튼은 숨김");

            // 취소하면 기술 목록으로
            ActiveButton(screen.transform, "CancelButton").onClick.Invoke();
            yield return null;
            Assert.IsNotNull(ActiveButton(screen.transform, "SkillButton_0"));

            // 안드로이드 뒤로가기도 [취소]와 같음 (기술 목록에서는 아무 일도 없음 — 전투에서 도망치기는 없다)
            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            yield return null;
            screen.HandleBack();
            yield return null;
            Assert.IsNotNull(ActiveButton(screen.transform, "SkillButton_0"), "뒤로가기 → 기술 목록");
            screen.HandleBack();
            Assert.IsNotNull(ActiveButton(screen.transform, "SkillButton_0"));

            // ×1.2 = 단어 2개 연속
            ActiveButton(screen.transform, "SkillButton_0").onClick.Invoke();
            yield return null;
            ActiveButton(screen.transform, "Intensity_1").onClick.Invoke();
            int enemyHp = screen.Engine.Enemies[0].Hp;
            for (int word = 0; word < 2; word++)
            {
                for (int frame = 0; frame < 600 && ActiveButton(screen.transform, "Choice_0") == null; frame++)
                {
                    ActiveButton(screen.transform, "CardConfirmButton")?.onClick.Invoke();
                    yield return null;
                }
                var quizText = AllText(screen.transform.Find("BattleCanvas/SafeArea/Bottom/QuizPanel"));
                StringAssert.Contains("×1.2", quizText, "진행 배지");
                StringAssert.Contains($"{word}/2", quizText);
                Assert.AreEqual(enemyHp, screen.Engine.Enemies[0].Hp, "다 맞히기 전엔 공격 안 함");
                ActiveButton(screen.transform, $"Choice_{screen.Engine.CurrentQuestion.CorrectIndex}").onClick.Invoke();
                yield return WaitFor(() => ActiveButton(screen.transform, "Choice_0") == null, 5f);
            }
            yield return WaitFor(() => ActiveButton(screen.transform, "SkillButton_0") != null, 10f);

            Assert.AreEqual(2, screen.Engine.CorrectAnswers, "단어 2개");
            Assert.AreEqual(2, session.ComboStreak, "맞힌 단어마다 콤보");
            Assert.Less(screen.Engine.Enemies[0].Hp, enemyHp, "다 맞히면 공격");
            StringAssert.Contains("×1.2", AllText(screen), "기록에 강도");

            Object.Destroy(screen.gameObject);
        }

        // 기술을 고른 뒤: (강도 ×1 →) 새 단어 카드 확인 → 정답 → 연출이 끝나 다시 기술 고르기가 될 때까지
        private static IEnumerator PlayOneTurn(BattleScreen screen)
        {
            for (int frame = 0; frame < 600; frame++)
            {
                ActiveButton(screen.transform, "Intensity_0")?.onClick.Invoke();
                var confirm = ActiveButton(screen.transform, "CardConfirmButton");
                if (confirm != null) confirm.onClick.Invoke();
                var engine = screen.Engine;
                if (engine.Phase == BattlePhase.AnsweringQuiz && engine.CurrentQuestion != null)
                    ActiveButton(screen.transform, $"Choice_{engine.CurrentQuestion.CorrectIndex}")?.onClick.Invoke();
                else if (engine.Phase == BattlePhase.ChoosingSkill && frame > 2 && ActiveButton(screen.transform, "SkillButton_0") != null)
                    yield break;
                yield return null;
            }
        }
    }
}
