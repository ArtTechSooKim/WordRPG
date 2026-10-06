using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 세로 화면 전투 UI. 모든 규칙은 BattleEngine이 처리하고, 여기서는 입력을 넘기고 돌려받은 BattleEvent를 연출만 한다.
    // UI는 전부 코드로 만든다 (아트 전 플레이스홀더). 주인공·단어 기록은 GameManager의 GameSession을 쓰고,
    // 답할 때마다와 전투가 끝날 때마다 GameManager.Save()로 자동 저장한다
    public class BattleScreen : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private EncounterTable encounter;
        [SerializeField] private WordDatabase words;

        [Header("밸런스")]
        [SerializeField] private BattleConfig battleConfig = new BattleConfig();
        [SerializeField] private MasteryRules masteryRules = new MasteryRules();

        [Tooltip("켜면 전투가 끝나도 다음 전투가 계속 이어짐 (전투 연습 씬). 필드에서는 끄고 BeginBattle로 한 판씩")]
        [SerializeField] private bool loopBattles = true;

        [Header("연출")]
        [Tooltip("연출 대기 시간 배율. 테스트에서는 아주 작게")]
        [SerializeField] private float animationScale = 1f;

        // --- 런타임 상태 ---
        private System.Random rng;
        private GameSession session;
        private GameDatabase database; // 가방의 상처약 찾기용 (id → 에셋)
        private GameSettings settings; // 진동 여부. GameManager가 없으면(테스트) null
        private Action saveProgress;
        private string pendingStatusMessage;
        private WordQuizService quizService;
        private WordDatabase quizWords;
        private BattleEngine engine;
        private bool initialized;
        private bool running; // 필드 모드에서 전투 중인지

        private Canvas canvas;
        private RectTransform root;
        private RectTransform enemyArea;
        private Image backdropFloor, backdropWall, backdropTint;
        private UnitView[] partyViews;
        private readonly List<UnitView> enemyViews = new List<UnitView>();
        private readonly Dictionary<BattleUnit, UnitView> viewOf = new Dictionary<BattleUnit, UnitView>();
        private readonly Dictionary<BattleUnit, (int hp, int shield)> shown = new Dictionary<BattleUnit, (int, int)>();
        private readonly List<string> logLines = new List<string>();

        private Text roundLabel, vocabLabel, logLabel;
        private Button dexButton;
        private DexView dexView;
        private GameObject skillPanel, quizPanel, cardPanel, resultPanel;

        private Text skillTitle;
        private readonly List<Button> skillButtons = new List<Button>();
        private readonly List<Text> skillDetails = new List<Text>();
        private Button cancelButton;
        private Button itemButton;
        private bool itemMode; // 아이템(상처약) 고르는 중
        // 공격 기술 강도 (Figma 'Intensity Button'): ×1 단어 1개 / ×1.2 연속 2개 / ×1.5 연속 3개 / ×2 연속 4개
        private readonly List<Button> intensityButtons = new List<Button>();
        private readonly List<(Text multiplier, Text title, Text detail)> intensityTexts = new List<(Text, Text, Text)>();
        private bool intensityMode;
        private static readonly Color[] IntensityColors = { Palette.Button, Palette.Guard, Palette.Evolve, Palette.Confirm };

        private Text quizPrompt, quizHint;
        private RectTransform timerFill;
        private Image timerFillImage;
        private readonly List<Button> choiceButtons = new List<Button>();
        private GameObject chainBadge;   // 연속 문제 진행 (Figma 'Chain Badge'): ×1.5 ●●○ 2/3
        private Text chainLabel, chainCount;
        private readonly List<Image> chainPips = new List<Image>();

        private Text cardTitle, cardWord, cardMeaning, cardExtra, cardConfirmLabel;
        private Image cardBorder;
        private Button cardConfirm;
        private Image cardGlow, cardStamp; // 새 단어 '발견!' 연출 (Figma '전투 — 새 단어 발견')

        private Text resultTitle, resultBody;
        private RectTransform resultLines;
        private Image resultBorder;
        private Button resultPrimary, resultSecondary;

        // --- 입력 대기용 ---
        private SkillData pickedSkill;
        private ItemData pickedItem;
        private BattleUnit pickedTarget;
        private int pickedIntensity = 1;
        private SkillData targetingSkill;
        private int pickedChoice = int.MinValue;
        private bool cardConfirmed;
        private int resultChoice = -1;

        public BattleEngine Engine => engine;
        public bool IsResultVisible => resultPanel != null && resultPanel.activeSelf;
        public string ResultTitle => resultTitle != null ? resultTitle.text : "";
        public GameSession Session => session;
        public bool IsRunning => running;

        private TutorialOverlay tutorial; // 필드에서 넘겨줌 (전투 연습 씬에는 없음)

        // 첫 전투 안내 (#36): 각 상황을 처음 만날 때 한 번씩
        public void SetTutorial(TutorialOverlay overlay) => tutorial = overlay;

        private bool Tip(string id) => tutorial != null && session != null && !session.Tutorials.Has(id) && !tutorial.IsShowing;

        private IEnumerator RunTip(string id, string text, Func<RectTransform> target, Func<bool> doneWhen = null) =>
            tutorial.Run(session, id, new TutorialOverlay.Step { Text = text, Target = target, DoneWhen = doneWhen }, saveProgress);
        public Hero Hero => session.Hero;
        public VocabularyProgress Vocabulary => session.Vocabulary;

        // 코드로 만든 BattleScreen(테스트 등)이 Start 전에 데이터를 넣는 용도.
        // session을 안 주면 Start에서 GameManager.Instance의 세션을 쓴다
        // gameDatabase: 상처약 등 아이템 찾기용. 안 주면 GameManager 것
        public void Configure(EncounterTable table, WordDatabase wordBook, GameSession gameSession = null,
            float animScale = 1f, BattleConfig config = null, Action onProgress = null, bool loop = true,
            GameDatabase gameDatabase = null)
        {
            encounter = table;
            words = wordBook;
            database = gameDatabase;
            session = gameSession;
            animationScale = animScale;
            if (config != null) battleConfig = config;
            saveProgress = onProgress;
            loopBattles = loop;
        }

        private void Start()
        {
            if (!EnsureInitialized()) return;
            if (loopBattles) StartCoroutine(MainLoop());
        }

        private bool EnsureInitialized()
        {
            if (initialized) return true;

            if (session == null && GameManager.Instance != null)
            {
                var manager = GameManager.Instance;
                session = manager.Session;
                saveProgress = manager.Save;
                settings = manager.Settings;
                manager.MarkPlaying();
                if (loopBattles) pendingStatusMessage = manager.StatusMessage; // 필드에서는 필드가 보여줌
            }

            if (database == null && GameManager.Instance != null) database = GameManager.Instance.Database;

            if (session == null || (loopBattles && (encounter == null || words == null)))
            {
                Debug.LogError("[BattleScreen] GameManager(또는 Configure의 session) / encounter / words 가 비어 있습니다");
                return false;
            }

            initialized = true;
            rng = new System.Random();
            BuildUi();
            if (!loopBattles) canvas.gameObject.SetActive(false);
            return true;
        }

        // 필드에서 조우했을 때 한 판. 결과 화면의 버튼을 누르면 화면을 닫고 onFinished(승리 여부)
        // intro: 전투 로그 첫 줄 (보스전 등). 비우면 "야생 몬스터 출현!"
        public void BeginBattle(List<MonsterInstance> enemies, WordDatabase wordBook, Action<bool> onFinished,
            string intro = null, FieldTheme theme = FieldTheme.Meadow, bool boss = false)
        {
            if (running) throw new InvalidOperationException("이미 전투 중입니다");
            if (!EnsureInitialized()) return;
            SetBackdrop(theme, boss);
            words = wordBook;
            running = true;
            canvas.gameObject.SetActive(true);
            StartCoroutine(SingleBattle(enemies, onFinished, intro));
        }

        // ------------------------------------------------------------------ 흐름

        private IEnumerator MainLoop()
        {
            while (true)
            {
                Sound.PlayMusic(Music.Battle);
                StartNewBattle(encounter.Roll(rng));
                yield return PlayBattle();
                session.EndBattleCombo(DateTime.UtcNow);
                yield return ShowResult();
            }
        }

        private IEnumerator SingleBattle(List<MonsterInstance> enemies, Action<bool> onFinished, string intro)
        {
            StartNewBattle(enemies, intro);
            yield return PlayBattle();
            session.EndBattleCombo(DateTime.UtcNow); // 콤보 3분은 전투가 끝난 뒤부터
            yield return ShowResult();
            bool victory = engine.Phase == BattlePhase.Victory;
            HideAllPanels();
            if (dexView.IsOpen) dexView.Hide();
            canvas.gameObject.SetActive(false);
            running = false;
            onFinished?.Invoke(victory);
        }

        private void StartNewBattle(List<MonsterInstance> enemies, string intro = null)
        {
            if (!session.CanFight) session.RestoreHero();
            if (quizService == null || quizWords != words)
            {
                quizWords = words;
                quizService = new WordQuizService(words.Words, session.Vocabulary, masteryRules, rng);
            }
            // 연속 정답 콤보는 전투가 바뀌어도 이어진다
            engine = new BattleEngine(new ICombatant[] { session.Hero }, enemies, quizService, battleConfig, rng,
                session.StartBattleCombo(DateTime.UtcNow));

            foreach (var view in enemyViews) Destroy(view.Root.gameObject);
            enemyViews.Clear();
            viewOf.Clear();

            int n = engine.Enemies.Count;
            float start = (1f - n / 3f) / 2f;
            for (int i = 0; i < n; i++)
            {
                var view = UnitView.Create(enemyArea, $"Enemy_{i}", start + i / 3f, 0, start + (i + 1) / 3f, 1);
                view.Bind(engine.Enemies[i]);
                enemyViews.Add(view);
                viewOf[engine.Enemies[i]] = view;
            }
            for (int i = 0; i < partyViews.Length; i++)
            {
                if (i < engine.Party.Count)
                {
                    partyViews[i].Root.gameObject.SetActive(true);
                    partyViews[i].Bind(engine.Party[i]);
                    viewOf[engine.Party[i]] = partyViews[i];
                }
                else
                {
                    partyViews[i].Root.gameObject.SetActive(false);
                }
            }

            logLines.Clear();
            if (!string.IsNullOrEmpty(pendingStatusMessage))
            {
                Log(pendingStatusMessage);
                pendingStatusMessage = null;
            }
            var names = new StringBuilder();
            foreach (var enemy in engine.Enemies)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append($"{enemy.DisplayName} Lv{enemy.Level}");
            }
            Log(intro ?? $"야생 몬스터 출현! {names}");
            roundLabel.text = "라운드 1";
            RefreshVocabLabel();
            Snapshot();
            SyncAll();
        }

        private IEnumerator PlayBattle()
        {
            while (!engine.IsOver)
            {
                // 1. 기술(과 대상) 선택 — 또는 상처약
                yield return ChooseSkill();
                if (pickedItem != null)
                {
                    Snapshot();
                    var itemEvents = engine.UseItem(pickedItem, session.Inventory);
                    saveProgress?.Invoke();
                    HideAllPanels();
                    yield return PlayEvents(itemEvents);
                    continue;
                }
                var question = engine.SelectSkill(pickedSkill, pickedTarget, pickedIntensity);
                IReadOnlyList<BattleEvent> events;
                while (true)
                {
                    // 2. 처음 보는 단어면 뜻부터 보여준다
                    if (question.IsNewWord)
                        yield return ShowWordCard($"새 단어 · 도감 등록 {LearnedCount() + 1}/{words.Words.Count}",
                            question.Word, "확인", Palette.Gold);

                    // 3. 문제 (강도를 올렸으면 단어 여러 개를 연속으로)
                    float secondsTaken = 0f;
                    yield return AskQuestion(question, seconds => secondsTaken = seconds);
                    int choice = pickedChoice;
                    int chainLength = engine.Intensity;

                    Snapshot();
                    events = engine.SubmitAnswer(choice, secondsTaken);
                    session.ComboStreak = engine.Streak;
                    saveProgress?.Invoke(); // 단어 학습 기록은 답할 때마다 저장
                    bool correct = choice >= 0 && question.IsCorrect(choice) && secondsTaken <= battleConfig.AnswerTimeLimitSeconds;
                    bool more = correct && engine.Phase == BattlePhase.AnsweringQuiz; // 아직 맞혀야 할 단어가 남음

                    // 4. 정답/오답 표시. 틀리면 정답을 꼭 보여줘서 학습 순간으로 만든다
                    MarkChoices(question, choice);
                    Sound.Play(correct ? Sfx.Correct : Sfx.Wrong);
                    if (!correct && settings != null && settings.Vibration) Haptics.Vibrate();
                    if (more)
                    {
                        RefreshChainBadge();
                        Log($"정답! 다음 단어 ({engine.ChainCorrect}/{engine.Intensity})");
                    }
                    yield return Wait(more ? 0.6f : 0.8f);
                    if (!correct)
                    {
                        if (Tip(TutorialProgress.Wrong))
                            yield return RunTip(TutorialProgress.Wrong,
                                "아쉬워요! 틀리거나 시간이 지나면 이번 기술은 실패하고, 연속 정답 콤보도 0이 돼요.\n" +
                                "초록색이 정답이에요. 틀린 단어는 오답 노트에 남아 곧 다시 나와요.",
                                () => (RectTransform)quizPanel.transform);
                        string title = choice < 0 ? "시간 초과! 오답 노트에 추가" : "오답! 오답 노트에 추가";
                        if (chainLength > 1) title = (choice < 0 ? "시간 초과" : "오답") + $" — ×{Multiplier(chainLength)} 공격 실패! 오답 노트에 추가";
                        yield return ShowWordCard(title, question.Word, "다음", Palette.Bad);
                    }
                    if (!more) break;
                    question = engine.CurrentQuestion;
                }

                // 5. 결과 연출
                HideAllPanels();
                yield return PlayEvents(events);
                if (engine.Streak >= Combo.FirstStreak && Tip(TutorialProgress.Combo))
                    yield return RunTip(TutorialProgress.Combo,
                        "연속으로 맞혔어요! 콤보가 이어질수록 공격 피해가 단계마다 5%씩 (최대 30%) 늘어요.\n" +
                        "틀리거나, 전투가 끝나고 3분이 지나면 0부터 다시 세요.", null);
            }
        }

        private IEnumerator ChooseSkill()
        {
            var actor = engine.CurrentActor;
            pickedSkill = null;
            pickedItem = null;
            pickedTarget = null;
            pickedIntensity = 1;
            targetingSkill = null;
            itemMode = false;
            UpdateFrames();
            ShowSkillMenu(actor);

            if (Tip(TutorialProgress.Potion) && actor.Hp * 2 < actor.MaxHp && itemButton.gameObject.activeInHierarchy && itemButton.interactable)
                yield return RunTip(TutorialProgress.Potion,
                    "HP가 절반 아래로 줄었어요.\n[가방 — 상처약 쓰기]로 문제 없이 HP를 회복할 수 있어요. (한 차례를 써요)",
                    () => (RectTransform)itemButton.transform);
            if (Tip(TutorialProgress.Skill))
                yield return RunTip(TutorialProgress.Skill,
                    "몬스터가 나타났어요! 쓸 기술을 하나 고르세요.\n기술을 쓰려면 영단어 문제를 맞혀야 해요.",
                    () => (RectTransform)skillPanel.transform,
                    () => pickedSkill != null || pickedItem != null || itemMode || intensityMode || targetingSkill != null);

            while (pickedSkill == null && pickedItem == null)
            {
                if (intensityMode && Tip(TutorialProgress.Intensity))
                    yield return RunTip(TutorialProgress.Intensity,
                        "공격 강도를 고르세요!\n×1은 단어 1개, ×1.2·×1.5·×2는 단어 2·3·4개를 연속으로 맞혀야 해요.\n하나라도 틀리면 이번 차례 공격은 실패해요.",
                        () => (RectTransform)skillPanel.transform,
                        () => pickedSkill != null || !intensityMode);
                yield return null;
            }
            ClearSelectable();
        }

        private IEnumerator AskQuestion(QuizQuestion question, Action<float> onDone)
        {
            ShowPanel(quizPanel);
            quizPrompt.text = question.Prompt;
            string hint = question.Direction == QuizDirection.EnglishToMeaning ? "이 단어의 뜻은?" : "알맞은 영단어는?";
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                bool used = i < question.Choices.Count;
                choiceButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var choiceLabel = UiKit.LabelOf(choiceButtons[i]);
                choiceLabel.text = question.Choices[i];
                choiceLabel.color = Palette.Text;
                UiKit.SetColor(choiceButtons[i], Palette.Button);
                choiceButtons[i].interactable = true;
            }

            RefreshChainBadge();
            timerFill.anchorMax = new Vector2(1, 1);
            if (Tip(TutorialProgress.Quiz))
                yield return RunTip(TutorialProgress.Quiz,
                    $"알맞은 답을 고르세요. 시간은 {battleConfig.AnswerTimeLimitSeconds:0}초!\n" +
                    $"{battleConfig.CriticalTimeSeconds:0}초 안에 맞히면 크리티컬로 더 세게 공격해요.",
                    () => (RectTransform)quizPanel.transform);
            pickedChoice = int.MinValue;
            float startTime = Time.unscaledTime;
            float limit = battleConfig.AnswerTimeLimitSeconds;
            float elapsed = 0f;

            while (pickedChoice == int.MinValue)
            {
                elapsed = Time.unscaledTime - startTime;
                if (elapsed >= limit)
                {
                    pickedChoice = -1;
                    elapsed = limit;
                    break;
                }

                bool critical = elapsed <= battleConfig.CriticalTimeSeconds && engine.CanStillCrit;
                quizHint.text = critical ? $"{hint}   ★ 크리티컬 찬스!" : hint;
                timerFill.anchorMax = new Vector2(Mathf.Clamp01(1f - elapsed / limit), 1);
                timerFillImage.color = critical ? Palette.Gold : (limit - elapsed < 3f ? Palette.Bad : Palette.Info);
                yield return null;
            }

            foreach (var button in choiceButtons) button.interactable = false;
            onDone(elapsed);
        }

        // 강도 2 이상일 때만: 배율 · 맞힌 칸(초록) · 맞힌 수/필요한 수
        private void RefreshChainBadge()
        {
            int length = engine.Intensity;
            chainBadge.SetActive(length > 1 && engine.Phase == BattlePhase.AnsweringQuiz);
            if (length <= 1) return;
            chainLabel.text = $"×{Multiplier(length)}";
            for (int i = 0; i < chainPips.Count; i++)
            {
                chainPips[i].gameObject.SetActive(i < length);
                chainPips[i].color = i < engine.ChainCorrect ? Palette.Good : Palette.Track;
            }
            chainCount.text = $"{engine.ChainCorrect}/{length}";
        }

        private void MarkChoices(QuizQuestion question, int chosen)
        {
            var font = UiFonts.Bold;
            bool marks = font.HasCharacter('✓') && font.HasCharacter('✕');
            for (int i = 0; i < question.Choices.Count; i++)
            {
                var label = UiKit.LabelOf(choiceButtons[i]);
                if (i == question.CorrectIndex)
                {
                    UiKit.SetColor(choiceButtons[i], Palette.Good);
                    if (marks) label.text = "✓  " + label.text;
                }
                else if (i == chosen)
                {
                    UiKit.SetColor(choiceButtons[i], Palette.Bad);
                    if (marks) label.text = "✕  " + label.text;
                }
                else
                {
                    UiKit.SetColor(choiceButtons[i], Palette.Disabled);
                    label.color = Palette.TextDim;
                }
            }
        }

        private IEnumerator ShowWordCard(string title, WordEntry word, string buttonText, Color titleColor)
        {
            cardTitle.text = title;
            cardTitle.color = titleColor;
            cardWord.text = word.English;
            cardMeaning.text = word.Meaning;
            var extra = new StringBuilder();
            if (word.PartOfSpeech.Length > 0) extra.Append($"({word.PartOfSpeech})");
            if (word.Example.Length > 0)
            {
                if (extra.Length > 0) extra.Append("  ");
                extra.Append(word.Example);
                if (word.ExampleMeaning.Length > 0) extra.Append($"\n{word.ExampleMeaning}");
            }
            cardExtra.text = extra.ToString();
            cardConfirmLabel.text = buttonText;
            bool discovery = titleColor == Palette.Gold; // 처음 보는 단어 = 발견!
            cardBorder.color = discovery ? Palette.Gold : Palette.PanelLight;
            cardConfirmed = false;
            ShowPanel(cardPanel);
            cardGlow.gameObject.SetActive(discovery);
            cardStamp.gameObject.SetActive(discovery);
            if (discovery) Sound.PlayJingle(Sfx.NewWord); // 짧은 '발견' 멜로디 (배경 음악은 잠깐 멈춤)
            if (discovery && Tip(TutorialProgress.NewWord))
                StartCoroutine(RunTip(TutorialProgress.NewWord,
                    "처음 만난 단어예요! 도감에 등록돼요.\n뜻을 잘 보고 [확인]을 누르면 문제가 나와요.",
                    () => (RectTransform)cardPanel.transform, () => cardConfirmed));

            // '발견!' 도장이 크게 찍히듯 줄어들고, 단어 뒤 빛이 숨 쉬듯 반짝인다
            float t = 0f;
            while (!cardConfirmed)
            {
                if (discovery)
                {
                    float pop = Mathf.Clamp01(t / (0.3f * Mathf.Max(0.01f, animationScale)));
                    float overshoot = 1f + 0.25f * Mathf.Sin(pop * Mathf.PI);
                    cardStamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, pop) * (pop < 1f ? overshoot : 1f);
                    cardStamp.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, pop);
                    float glow = 0.28f + 0.14f * Mathf.Sin(t * 3f);
                    cardGlow.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, glow);
                    cardGlow.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 3f));
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            cardStamp.rectTransform.localScale = Vector3.one;
        }

        private IEnumerator PlayEvents(IReadOnlyList<BattleEvent> events)
        {
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case BattleEventType.Combo:
                        comboStep = Combo.Step(e.Amount);
                        StartCoroutine(ComboRoutine(e.Amount));
                        yield return Wait(0.45f);
                        break;

                    case BattleEventType.QuizAnswered:
                        comboStep = 0;
                        Log((e.Correct ? "정답! " : "오답… ") + MasteryText(e.Mastery));
                        yield return Wait(0.5f);
                        break;

                    case BattleEventType.SkillUsed:
                        bool charged = e.Multiplier > 1.001f;
                        Log($"{e.Actor.DisplayName}의 {e.Skill.DisplayName}" + (charged ? $" ×{FormatMultiplier(e.Multiplier)}!" : "!"));
                        if (charged)
                        {
                            Float(e.Actor, $"×{FormatMultiplier(e.Multiplier)}!", Palette.Gold, 56);
                            Sound.Play(Sfx.Critical);
                        }
                        yield return Wait(charged ? 0.7f : 0.5f);
                        break;

                    case BattleEventType.SkillFailed:
                        Log($"{e.Actor.DisplayName}의 {e.Skill.DisplayName} 실패…");
                        Float(e.Actor, "실패", Palette.TextDim, 44);
                        Sound.Play(Sfx.Fail);
                        PlayFx(e.Actor, Fx.Smoke);
                        yield return Wait(0.7f);
                        break;

                    case BattleEventType.Damage:
                        var d = shown[e.Target];
                        shown[e.Target] = (Mathf.Max(0, d.hp - e.Amount), Mathf.Max(0, d.shield - e.Absorbed));
                        Sync(e.Target);
                        string dmgText = e.Amount > 0 ? $"-{e.Amount}" : "막음!";
                        Sound.Play(e.Amount <= 0 ? Sfx.Shield : e.IsCritical ? Sfx.Critical : Sfx.Hit);
                        PlayFx(e.Target, e.Amount <= 0 ? Fx.Shield : e.IsCritical ? Fx.Explosion : e.Target.IsPlayerSide ? Fx.Claw : Fx.Slash);
                        // 콤보 중인 아군 공격은 숫자가 단계만큼 더 크게
                        int boost = e.Actor != null && e.Actor.IsPlayerSide ? comboStep * 5 : 0;
                        Float(e.Target, e.IsCritical ? $"크리티컬! {dmgText}" : dmgText,
                            e.Amount > 0 ? Palette.Bad : Palette.Info, (e.IsCritical ? 58 : 48) + boost);
                        Log(e.Absorbed > 0
                            ? $"{e.Target.DisplayName} 피해 {e.Amount} (보호막이 {e.Absorbed} 흡수)"
                            : $"{e.Target.DisplayName} 피해 {e.Amount}" + (e.IsCritical ? " — 크리티컬!" : ""));
                        yield return Wait(0.6f);
                        break;

                    case BattleEventType.Heal:
                        var h = shown[e.Target];
                        shown[e.Target] = (h.hp + e.Amount, h.shield);
                        Sync(e.Target);
                        Float(e.Target, $"+{e.Amount}", Palette.Good, 48);
                        Sound.Play(Sfx.Heal);
                        PlayFx(e.Target, Fx.Heal);
                        Log($"{e.Target.DisplayName} HP +{e.Amount}" + (e.IsCritical ? " — 크리티컬!" : ""));
                        yield return Wait(0.6f);
                        break;

                    case BattleEventType.Shield:
                        var s = shown[e.Target];
                        shown[e.Target] = (s.hp, s.shield + e.Amount);
                        Sync(e.Target);
                        Float(e.Target, $"보호막 +{e.Amount}", Palette.Info, 40);
                        Sound.Play(Sfx.Shield);
                        PlayFx(e.Target, Fx.Shield);
                        Log($"{e.Target.DisplayName} 보호막 +{e.Amount}");
                        yield return Wait(0.6f);
                        break;

                    case BattleEventType.ItemUsed:
                        var it = shown[e.Target];
                        shown[e.Target] = (it.hp + e.Amount, it.shield);
                        Sync(e.Target);
                        Float(e.Target, $"+{e.Amount}", Palette.Good, 48);
                        Sound.Play(Sfx.Heal);
                        PlayFx(e.Target, Fx.Heal);
                        Log($"{UiKit.WithJosa(e.Item.DisplayName, "을", "를")} 썼다! HP +{e.Amount}");
                        yield return Wait(0.6f);
                        break;

                    case BattleEventType.Defeated:
                        Log($"{e.Target.DisplayName} 쓰러졌다!");
                        Sound.Play(Sfx.Faint);
                        PlayFx(e.Target, Fx.Smoke);
                        yield return Wait(0.5f);
                        break;

                    case BattleEventType.RoundStarted:
                        roundLabel.text = $"라운드 {e.Round}";
                        foreach (var unit in engine.Party) shown[unit] = (shown[unit].hp, 0);
                        SyncAll();
                        yield return Wait(0.2f);
                        break;
                }
            }

            RefreshVocabLabel();
            SyncAll();
        }

        private IEnumerator ShowResult()
        {
            bool victory = engine.Phase == BattlePhase.Victory;
            var body = new StringBuilder();
            ClearResultLines();
            Sound.PlayMusic(Music.None); // 결과 음악(징글)이 잘 들리게 전투 음악을 멈춘다
            bool leveledUp = false;

            if (victory)
            {
                var reward = engine.CalculateReward();
                int levels = BattleRewardCalculator.Apply(reward, session.Hero, session.Inventory);
                resultTitle.text = "승리!";
                resultTitle.color = Palette.Gold;
                AddResultLine(null, $"경험치 +{reward.Exp}", Palette.Text);
                AddResultLine(UiKit.Icon("gold"), $"+{reward.Gold}   (보유 {session.Inventory.Gold}G)", Palette.Text);
                foreach (var item in reward.Items) AddResultLine(UiKit.ItemIcon(item.Item), $"{item.Item.DisplayName} x{item.Count} 획득", Palette.Text);
                if (levels > 0) AddResultLine(UiKit.Icon("star_full"), $"레벨 업!  {session.Hero.DisplayName} Lv{session.Hero.Level}", Palette.Gold);
                leveledUp = levels > 0;
            }
            else
            {
                resultTitle.text = "패배…";
                resultTitle.color = Palette.Bad;
                string fainted = UiKit.WithJosa(session.Hero.DisplayName, "이", "가");
                AddResultLine(null, loopBattles ? $"{fainted} 쓰러졌다. 회복하고 다시 도전하자!" : $"{fainted} 쓰러졌다… 시작 지점으로 돌아간다.", Palette.Text);
            }
            resultBorder.color = victory ? Palette.Gold : Palette.PanelLight;
            UiKit.SetColor(resultPrimary, victory ? Palette.Button : Palette.Neutral);

            var completions = session.ClaimDexRewards(new[] { words });
            foreach (var completion in completions) AddDexBanner(completion);
            // 결과 소리: 도감 완성 > 레벨 업 > 승리, 지면 패배
            Sound.Play(completions.Count > 0 ? Sfx.DexComplete : leveledUp ? Sfx.LevelUp : victory ? Sfx.Victory : Sfx.Defeat);

            int up = 0, down = 0;
            foreach (var change in engine.MasteryChanges)
            {
                if (change.LeveledUp) up++;
                else if (change.LeveledDown) down++;
            }
            body.AppendLine($"정답 {engine.CorrectAnswers}   오답 {engine.WrongAnswers}   단어 숙련도 ▲{up} ▼{down}");
            body.Append($"발견한 단어 {LearnedCount()}/{words.Words.Count}");
            resultBody.text = body.ToString();

            session.Record.RecordBattle(victory, engine.CorrectAnswers, engine.WrongAnswers);
            saveProgress?.Invoke();

            if (loopBattles) SetResultButtons(victory ? "다음 전투" : "회복 후 재도전", victory ? "회복 후 전투" : null);
            else SetResultButtons(victory ? "계속 탐험" : "시작 지점으로", null);
            resultChoice = -1;
            ShowPanel(resultPanel);
            while (resultChoice < 0) yield return null;

            // 패배 후 재도전, 또는 '회복' 선택 시 완전 회복
            if (!victory || resultChoice == 1)
            {
                session.RestoreHero();
                saveProgress?.Invoke();
            }
        }

        private void ClearResultLines()
        {
            foreach (Transform child in resultLines)
            {
                child.gameObject.SetActive(false); // 레이아웃에서 즉시 빠지게
                Destroy(child.gameObject);
            }
        }

        // 결과 한 줄: [아이콘] 글자 (Figma 'Result Panel')
        private void AddResultLine(Sprite icon, string text, Color color)
        {
            var row = UiKit.Rect("Line", resultLines, 0, 0, 1, 1);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 46;
            if (icon != null)
            {
                var image = UiKit.IconImage("Icon", row, icon, 0, 0, 0, 1);
                image.rectTransform.pivot = new Vector2(0, 0.5f);
                image.rectTransform.sizeDelta = new Vector2(46, 0);
            }
            var label = UiKit.Label("Text", row, text, 32, color, 0, 0, 1, 1, TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
            label.rectTransform.offsetMin = new Vector2(icon != null ? 60 : 0, 0);
        }

        // 지역 도감을 다 채운 순간 결과 화면에 붙는 배너: 징표 아이콘 + "★ 초원 도감 완성!"
        private void AddDexBanner(DexCompletion completion)
        {
            var row = UiKit.Rect("DexBanner", resultLines, 0, 0, 1, 1);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 120;
            UiKit.RoundPanel("Bg", row, Palette.PanelLight, UiKit.RadiusMd).raycastTarget = false;
            UiKit.Outline(UiKit.Panel("Border", row, Palette.Gold), UiKit.RadiusMd, 3).raycastTarget = false;
            var icon = UiKit.IconImage("Keepsake", row, UiKit.ItemIcon(completion.Keepsake), 0, 0.08f, 0, 0.92f);
            icon.rectTransform.pivot = new Vector2(0, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(88, 0);
            icon.rectTransform.anchoredPosition = new Vector2(12, 0);
            var headline = UiKit.Display(UiKit.Label("Headline", row, $"★ {completion.Region.RegionName} 도감 완성!", 36, Palette.Gold,
                0, 0.48f, 1, 0.94f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 20));
            headline.rectTransform.offsetMin = new Vector2(116, 0);
            headline.rectTransform.offsetMax = new Vector2(-12, 0);
            string keepsake = completion.Keepsake != null ? $"징표 '{completion.Keepsake.DisplayName}' + " : "";
            var detail = UiKit.Label("Detail", row, $"{keepsake}{completion.Gold} 골드 획득", 28, Palette.Text,
                0, 0.08f, 1, 0.48f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 16);
            detail.rectTransform.offsetMin = new Vector2(116, 0);
            detail.rectTransform.offsetMax = new Vector2(-12, 0);
        }

        // ------------------------------------------------------------------ 스킬 메뉴 / 대상 선택

        private void ShowSkillMenu(BattleUnit actor)
        {
            itemMode = false;
            intensityMode = false;
            foreach (var button in intensityButtons) button.gameObject.SetActive(false);
            ShowPanel(skillPanel);
            skillTitle.text = $"{actor.DisplayName}의 차례 — 기술을 고르세요";
            cancelButton.gameObject.SetActive(false);

            for (int i = 0; i < skillButtons.Count; i++)
            {
                bool used = i < actor.Skills.Count;
                skillButtons[i].gameObject.SetActive(used);
                if (!used) continue;

                var skill = actor.Skills[i];
                UiKit.LabelOf(skillButtons[i]).text = skill.DisplayName;
                skillDetails[i].text = SourcePrefix(actor, skill) + SkillDetail(skill);
                UiKit.SetColor(skillButtons[i], SkillColor(skill));
                skillButtons[i].interactable = true;
                skillButtons[i].onClick.RemoveAllListeners();
                skillButtons[i].onClick.AddListener(() => OnSkillClicked(skill));
            }

            // 상처약: 가진 개수, HP가 가득이면 잠금
            bool isHero = actor.Hero != null;
            itemButton.gameObject.SetActive(isHero);
            if (isHero)
            {
                int potions = 0;
                foreach (var item in HealingItems()) potions += session.Inventory.GetCount(item);
                bool full = actor.Hp >= actor.MaxHp;
                itemButton.interactable = potions > 0 && !full;
                UiKit.LabelOf(itemButton).text = potions == 0 ? "가방 — 상처약이 없어요" : full ? "가방 — HP가 가득해요" : $"가방 — 상처약 쓰기 ({potions}개)";
            }
        }

        // "깃펜 · " (성유물 기술) / "기본 · " (주인공 기본 기술). 적이면 빈 글자
        private static string SourcePrefix(BattleUnit actor, SkillData skill)
        {
            if (actor.Hero == null) return "";
            var source = actor.Hero.SourceOf(skill);
            return source != null ? $"{source.SourceName} · " : "기본 · ";
        }

        // 가방에 있는 회복 아이템 (상처약 등). 소지품은 id만 있어서 GameDatabase로 찾는다
        private List<ItemData> HealingItems()
        {
            var list = new List<ItemData>();
            if (database == null) return list;
            foreach (var stack in session.Inventory.Stacks)
            {
                var item = database.FindItem(stack.ItemId);
                if (item != null && item.IsHealingItem && stack.Count > 0) list.Add(item);
            }
            return list;
        }

        private void ShowItemMenu()
        {
            itemMode = true;
            skillTitle.text = "어떤 아이템을 쓸까요? (한 턴을 쓰고, 문제는 없어요)";
            itemButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(true);
            var items = HealingItems();
            for (int i = 0; i < skillButtons.Count; i++)
            {
                bool used = i < items.Count;
                skillButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var item = items[i];
                UiKit.LabelOf(skillButtons[i]).text = $"{item.DisplayName}  × {session.Inventory.GetCount(item)}";
                skillDetails[i].text = $"HP {item.HealAmount} 회복";
                UiKit.SetColor(skillButtons[i], Palette.Heal);
                skillButtons[i].interactable = true;
                skillButtons[i].onClick.RemoveAllListeners();
                skillButtons[i].onClick.AddListener(() => pickedItem = item);
            }
        }

        private void OnSkillClicked(SkillData skill)
        {
            if (!skill.NeedsTargetChoice)
            {
                Commit(skill, null);
                return;
            }

            var candidates = skill.Target == SkillTarget.SingleEnemy ? engine.Enemies : engine.Party;
            var alive = new List<BattleUnit>();
            foreach (var unit in candidates)
            {
                if (!unit.IsDefeated) alive.Add(unit);
            }

            // 적이 한 마리뿐이면 대상 선택을 건너뛴다
            if (skill.Target == SkillTarget.SingleEnemy && alive.Count == 1)
            {
                Commit(skill, alive[0]);
                return;
            }

            targetingSkill = skill;
            skillTitle.text = $"{skill.DisplayName} — 대상을 선택하세요";
            foreach (var button in skillButtons) button.gameObject.SetActive(false);
            itemButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(true);

            foreach (var unit in alive)
            {
                var target = unit;
                var view = viewOf[unit];
                view.SetSelectable(true, () => Commit(skill, target));
                view.SetFrame(Palette.Info);
            }
        }

        // 기술(과 대상)이 정해짐: 주인공의 공격 기술이면 강도를 고르고, 아니면 바로 문제로
        private void Commit(SkillData skill, BattleUnit target)
        {
            if (engine.CurrentActor.IsPlayerSide && BattleEngine.CanChooseIntensity(skill) && battleConfig.MaxIntensity > 1)
            {
                ShowIntensityMenu(skill, target);
                return;
            }
            pickedTarget = target;
            pickedSkill = skill;
        }

        // 강도 고르기 (Figma 'Battle — 강도 고르기'): 단어를 더 많이 연속으로 맞힐수록 피해가 크지만, 하나라도 틀리면 공격 실패
        private void ShowIntensityMenu(SkillData skill, BattleUnit target)
        {
            intensityMode = true;
            targetingSkill = null;
            ClearSelectable();
            skillTitle.text = $"{skill.DisplayName} — 강도를 고르세요\n<size=26><color=#A6B3D1>하나라도 틀리면 이번 턴 공격 실패 · 맞힌 단어는 모두 콤보</color></size>";
            foreach (var button in skillButtons) button.gameObject.SetActive(false);
            itemButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(true);

            // 예상 피해는 고른 대상(없으면 살아 있는 첫 적) 기준
            var estimateTarget = target;
            if (estimateTarget == null)
            {
                foreach (var enemy in engine.Enemies)
                {
                    if (!enemy.IsDefeated) { estimateTarget = enemy; break; }
                }
            }
            for (int i = 0; i < intensityButtons.Count; i++)
            {
                int words = i + 1;
                bool used = words <= battleConfig.MaxIntensity;
                intensityButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var (multiplier, title, detail) = intensityTexts[i];
                multiplier.text = $"×{Multiplier(words)}";
                title.text = words == 1 ? "단어 1개" : $"단어 {words}개 연속";
                int damage = engine.EstimateDamage(skill, estimateTarget, words);
                string each = skill.Target == SkillTarget.AllEnemies ? "적마다 " : "";
                detail.text = (words == 1 ? "맞히면 발동" : $"{words}개 모두 맞혀야 발동") + $" · 예상 피해 {each}약 {damage}";
                intensityButtons[i].onClick.RemoveAllListeners();
                intensityButtons[i].onClick.AddListener(() =>
                {
                    pickedIntensity = words;
                    pickedTarget = target;
                    pickedSkill = skill;
                });
            }
        }

        private string Multiplier(int words) => FormatMultiplier(battleConfig.IntensityMultiplier(words));

        private static string FormatMultiplier(float value) => value.ToString("0.##");

        // 뒤로가기: 도감이 열려 있으면 닫고, 대상·강도·아이템을 고르는 중이면 [취소]와 같음. 전투에서 도망치기는 없다
        public void HandleBack()
        {
            if (dexView != null && dexView.IsOpen) dexView.Hide();
            else if (cancelButton != null && cancelButton.gameObject.activeInHierarchy) OnCancelTargeting();
        }

        // 전투 연습 씬(단독)에서만 직접 받는다 — 필드에서는 FieldScreen이 넘겨 준다
        private void Update()
        {
            if (loopBattles && FieldScreen.BackPressed()) HandleBack();
        }

        private void OnCancelTargeting()
        {
            if (itemMode || intensityMode)
            {
                ShowSkillMenu(engine.CurrentActor);
                return;
            }
            if (targetingSkill == null) return;
            targetingSkill = null;
            ClearSelectable();
            UpdateFrames();
            ShowSkillMenu(engine.CurrentActor);
        }

        private void ClearSelectable()
        {
            foreach (var view in viewOf.Values) view.SetSelectable(false);
            UpdateFrames();
        }

        private void UpdateFrames()
        {
            foreach (var pair in viewOf)
            {
                bool isActor = engine != null && !engine.IsOver && pair.Key == engine.CurrentActor;
                pair.Value.SetFrame(isActor ? (Color?)Palette.Gold : null);
            }
        }

        internal static string SkillDetail(SkillData skill) => $"{SkillEffect(skill)}  |  문제 {QuizText(skill)}";

        // "공격 20 · 적 1체"
        internal static string SkillEffect(SkillData skill)
        {
            string kind = skill.Kind == SkillKind.Damage ? "공격" : skill.Kind == SkillKind.Heal ? "회복" : "보호막";
            string target;
            switch (skill.Target)
            {
                case SkillTarget.SingleEnemy: target = "적 1체"; break;
                case SkillTarget.AllEnemies: target = "적 전체"; break;
                case SkillTarget.SingleAlly: target = "아군 1체"; break;
                case SkillTarget.AllAllies: target = "아군 전체"; break;
                default: target = "자신"; break;
            }
            return $"{kind} {skill.Power} · {target}";
        }

        internal static bool IsHardQuiz(SkillData skill) => skill.QuizDirection != QuizDirection.EnglishToMeaning;

        internal static string QuizText(SkillData skill) => IsHardQuiz(skill) ? "한→영 · 어려움" : "영→한 · 쉬움";

        internal static Color SkillColor(SkillData skill)
        {
            switch (skill.Kind)
            {
                case SkillKind.Damage: return Palette.Attack;
                case SkillKind.Heal: return Palette.Heal;
                default: return Palette.Guard;
            }
        }

        internal static string SkillIconName(SkillData skill)
        {
            switch (skill.Kind)
            {
                case SkillKind.Damage: return skill.Target == SkillTarget.AllEnemies ? "skill_attack_all" : "skill_attack";
                case SkillKind.Heal: return "skill_heal";
                default: return "skill_guard";
            }
        }

        // ------------------------------------------------------------------ 화면 갱신 도우미

        private void Snapshot()
        {
            shown.Clear();
            foreach (var unit in engine.Party) shown[unit] = (unit.Hp, unit.Shield);
            foreach (var unit in engine.Enemies) shown[unit] = (unit.Hp, unit.Shield);
        }

        private void Sync(BattleUnit unit) => viewOf[unit].Sync(shown[unit].hp, shown[unit].shield);

        private void SyncAll()
        {
            foreach (var unit in engine.Party) shown[unit] = (unit.Hp, unit.Shield);
            foreach (var unit in engine.Enemies) shown[unit] = (unit.Hp, unit.Shield);
            foreach (var pair in viewOf) pair.Value.Sync(shown[pair.Key].hp, shown[pair.Key].shield);
            UpdateFrames();
        }

        private void RefreshVocabLabel() => vocabLabel.text = $"발견한 단어 {LearnedCount()}/{words.Words.Count}";

        // 이 지역 단어장 중 발견한 단어 수
        private int LearnedCount()
        {
            int count = 0;
            foreach (var word in words.Words)
            {
                if (session.Vocabulary.GetLevel(word.Id) > MasteryLevel.New) count++;
            }
            return count;
        }

        private void Log(string line)
        {
            logLines.Add(line);
            if (logLines.Count > 3) logLines.RemoveAt(0);
            logLabel.text = string.Join("\n", logLines);
        }

        private static string MasteryText(MasteryChange change)
        {
            if (change.After > change.Before) return $"[{change.Before.DisplayName()} → {change.After.DisplayName()} ▲]";
            if (change.After < change.Before) return $"[{change.Before.DisplayName()} → {change.After.DisplayName()} ▼]";
            return $"[{change.After.DisplayName()}]";
        }

        private IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds * animationScale);
        }

        // ------------------------------------------------------------------ 연속 정답 콤보 (Figma 'Combo Text')

        // 단계별 색: Combo! 흰색 → Good! 초록 → Very Good! 하늘 → Excellent! 금 → Outstanding! 주황 → Exceptional! 분홍
        private static readonly Color[] ComboColors =
        {
            Color.white, new Color32(111, 227, 138, 255), new Color32(91, 200, 255, 255),
            new Color32(255, 209, 64, 255), new Color32(255, 138, 61, 255), new Color32(255, 95, 210, 255)
        };

        private int comboStep; // 지금 연출 중인 공격의 콤보 단계 (피해 숫자 크기)
        public string LastComboText { get; private set; } // 마지막으로 띄운 콤보 글자 (테스트용)

        // 글자가 크게 찍히듯 줄어들며 나타났다가 잠깐 머문 뒤 위로 떠오르며 사라진다. 단계가 오를수록 크고 효과음이 높다
        private IEnumerator ComboRoutine(int streak)
        {
            int step = Combo.Step(streak);
            if (step <= 0) yield break;
            var color = ComboColors[step - 1];
            LastComboText = Combo.Label(streak);

            var group = UiKit.Rect("Combo", root, 0.5f, 0.25f, 0.5f, 0.25f); // 주인공 카드 아래 (연출 중엔 기술 패널이 숨어 비어 있음)
            group.sizeDelta = new Vector2(1000, 300);
            group.localEulerAngles = new Vector3(0, 0, 6f);
            var glow = UiKit.IconImage("Glow", group, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            glow.preserveAspect = false;
            glow.rectTransform.sizeDelta = new Vector2(900, 330);
            var label = UiKit.Display(UiKit.OneLine(UiKit.Label("Label", group, Combo.Label(streak), 84 + (step - 1) * 7, color,
                0, 0.35f, 1, 1)));
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(0, -8);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(26, 20, 48, 255);
            outline.effectDistance = new Vector2(5, -5);
            int bonus = Mathf.RoundToInt(Combo.DamageBonus(streak, battleConfig.ComboBonusPerStep) * 100f);
            var detail = UiKit.OneLine(UiKit.Label("Detail", group, $"{streak} 연속 정답 · 피해 +{bonus}%", 32, Color.white,
                0, 0, 1, 0.35f, TextAnchor.MiddleCenter, FontStyle.Bold));
            var detailOutline = detail.gameObject.AddComponent<Outline>();
            detailOutline.effectColor = new Color32(26, 20, 48, 255);
            detailOutline.effectDistance = new Vector2(3, -3);
            var group2 = group.gameObject.AddComponent<CanvasGroup>();
            group2.blocksRaycasts = false;
            Sound.Play(Sfx.Combo, 1f + 0.08f * (step - 1));

            float pop = 0.18f * animationScale, hold = 0.6f * animationScale, fade = 0.35f * animationScale;
            for (float t = 0; t < pop; t += Time.unscaledDeltaTime)
            {
                float k = t / pop;
                float overshoot = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
                group.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k) * overshoot;
                group2.alpha = k;
                glow.color = new Color(color.r, color.g, color.b, 0.35f * k);
                yield return null;
            }
            group.localScale = Vector3.one;
            group2.alpha = 1f;
            for (float t = 0; t < hold; t += Time.unscaledDeltaTime)
            {
                glow.color = new Color(color.r, color.g, color.b, 0.3f + 0.08f * Mathf.Sin(t * 12f));
                yield return null;
            }
            var start = group.anchoredPosition;
            for (float t = 0; t < fade; t += Time.unscaledDeltaTime)
            {
                float k = t / fade;
                group.anchoredPosition = start + Vector2.up * (70f * k);
                group2.alpha = 1f - k;
                yield return null;
            }
            Destroy(group.gameObject);
        }

        private void Float(BattleUnit unit, string text, Color color, int size)
        {
            if (!viewOf.TryGetValue(unit, out var view)) return;
            StartCoroutine(FloatRoutine(view.Root, text, color, size));
        }

        private IEnumerator FloatRoutine(RectTransform target, string text, Color color, int size)
        {
            var label = UiKit.Display(UiKit.Label("Float", root, text, size + 8, color, 0.5f, 0.5f, 0.5f, 0.5f,
                TextAnchor.MiddleCenter));
            var rt = label.rectTransform;
            rt.sizeDelta = new Vector2(560, 100);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(3, -3);

            Vector3 start = target.TransformPoint(target.rect.center);
            float duration = 0.9f * animationScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                rt.position = start + Vector3.up * (140f * k * canvas.scaleFactor);
                var c = label.color;
                c.a = 1f - k * k;
                label.color = c;
                outline.effectColor = new Color(0, 0, 0, 0.85f * c.a);
                yield return null;
            }
            Destroy(label.gameObject);
        }

        // ------------------------------------------------------------------ 패널 전환

        private static string CompletionText(DexCompletion completion)
        {
            string keepsake = completion.Keepsake != null ? $"징표 '{completion.Keepsake.DisplayName}' + " : "";
            return $"★ {completion.Region.RegionName} 도감 완성!  {keepsake}{completion.Gold} 골드 획득";
        }

        private void OnDexClicked()
        {
            if (!dexButton.interactable) return;
            var completed = session.ClaimDexRewards(new[] { words });
            if (completed.Count > 0)
            {
                Log(CompletionText(completed[0]));
                saveProgress?.Invoke();
            }
            dexView.Show(words, session, DateTime.UtcNow);
        }

        private void ShowPanel(GameObject panel)
        {
            // 도감은 전투 연출·문제 풀이 중에는 열 수 없다 (스킬 선택 또는 결과 화면일 때만)
            dexButton.interactable = panel == skillPanel || panel == resultPanel;
            skillPanel.SetActive(panel == skillPanel);
            quizPanel.SetActive(panel == quizPanel);
            cardPanel.SetActive(panel == cardPanel);
            resultPanel.SetActive(panel == resultPanel);
        }

        private void HideAllPanels() => ShowPanel(null);

        private void SetResultButtons(string primary, string secondary)
        {
            UiKit.LabelOf(resultPrimary).text = primary;
            bool two = secondary != null;
            resultSecondary.gameObject.SetActive(two);
            if (two) UiKit.LabelOf(resultSecondary).text = secondary;
            var rt = (RectTransform)resultPrimary.transform;
            rt.anchorMin = new Vector2(two ? 0.03f : 0.05f, 0.025f);
            rt.anchorMax = new Vector2(two ? 0.49f : 0.95f, 0.185f);
        }

        // ------------------------------------------------------------------ 전투 배경 (지역 타일)

        // 적이 서는 위쪽에 지역 바닥(초원 = 풀숲, 서고 = 돌바닥)을 깔고 맨 위에 벽 한 줄(덤불·책장), 아래로 갈수록 어둡게
        private void BuildBackdrop(Transform parent)
        {
            var area = UiKit.Rect("Backdrop", parent, 0, 0.6f, 1, 1);
            backdropFloor = UiKit.Panel("Floor", area, Color.white);
            backdropWall = UiKit.Panel("Wall", area, Color.white, 0, 0.86f, 1, 1);
            foreach (var image in new[] { backdropFloor, backdropWall })
            {
                image.type = Image.Type.Tiled;
                image.pixelsPerUnitMultiplier = 100f / 120f; // 한 칸 = 120px
                image.raycastTarget = false;
            }
            backdropTint = UiKit.Panel("Tint", area, new Color(0, 0, 0, 0.72f)); // Linear 색공간이라 알파를 높게 잡아야 눈에 보이는 만큼 어두워진다
            backdropTint.raycastTarget = false;
            var topScrim = UiKit.Panel("TopScrim", area, new Color(0, 0, 0, 0.75f), 0, 0.86f, 1, 1); // 위쪽 글자·도감 버튼이 잘 보이게
            topScrim.raycastTarget = false;
            var fade = UiKit.Panel("Fade", area, Palette.Background, 0, 0, 1, 0.55f);
            fade.sprite = UiKit.FadeSprite();
            fade.raycastTarget = false;
            SetBackdrop(FieldTheme.Meadow, false);
        }

        // 보스전은 보랏빛으로 더 어둡게
        public void SetBackdrop(FieldTheme theme, bool boss)
        {
            if (backdropFloor == null) return;
            backdropFloor.sprite = FieldArt.ForTile(theme == FieldTheme.Library ? FieldTile.Floor : FieldTile.Grass, theme, false); // 숲은 고사리
            backdropWall.sprite = FieldArt.ForTile(FieldTile.Wall, theme, false);
            backdropTint.color = boss ? new Color(0.2f, 0.04f, 0.3f, 0.8f) : new Color(0, 0, 0, 0.72f);
        }

        // 카드 위에 효과 애니메이션 (기다리지 않고 바로 다음 연출로)
        private void PlayFx(BattleUnit unit, Fx fx)
        {
            if (unit == null || !viewOf.TryGetValue(unit, out var view)) return;
            float size = unit.IsPlayerSide ? 200f : 300f;
            StartCoroutine(BattleFx.Play(view.Root, fx, size, animationScale));
        }

        // ------------------------------------------------------------------ UI 생성

        private void BuildUi()
        {
            UiKit.EnsureEventSystem();

            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.sortingOrder = 10; // 필드 HUD 위에 덮는다
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            UiKit.Panel("Background", canvasGo.transform, Palette.Background);
            BuildBackdrop(canvasGo.transform);

            root = UiKit.Stretch("SafeArea", canvasGo.transform);
            UiKit.ApplySafeArea(root);

            // 상단 바
            roundLabel = UiKit.Display(UiKit.Label("Round", root, "라운드 1", 44, Palette.Gold, 0.03f, 0.945f, 0.3f, 1f,
                TextAnchor.MiddleLeft));
            vocabLabel = UiKit.Label("Vocab", root, "", 30, Palette.TextDim, 0.3f, 0.945f, 0.78f, 1f,
                TextAnchor.MiddleRight);
            dexButton = UiKit.MakeButton("DexButton", root, "도감", Palette.Button, 34, 0.8f, 0.948f, 0.97f, 0.998f);
            dexButton.onClick.AddListener(OnDexClicked);

            enemyArea = UiKit.Rect("EnemyArea", root, 0, 0.62f, 1, 0.94f);

            // 전투 로그
            var logPanel = UiKit.RoundPanel("LogPanel", root, Palette.Panel, UiKit.RadiusMd, 0.03f, 0.512f, 0.97f, 0.612f);
            logLabel = UiKit.Label("Log", logPanel.transform, "", 31, Palette.Text, 0, 0, 1, 1,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 22);
            UiKit.Pad(logLabel.rectTransform, 24, 8, 24, 8);

            // 주인공 카드 (가운데 한 장)
            var partyArea = UiKit.Rect("PartyArea", root, 0, 0.355f, 1, 0.51f);
            partyViews = new[] { UnitView.Create(partyArea, "Party_0", 1f / 3f, 0, 2f / 3f, 1) };

            // 하단 패널들
            var bottom = UiKit.Rect("Bottom", root, 0, 0, 1, 0.35f);
            UiKit.Pad(bottom, 24, 24, 24, 12);
            BuildSkillPanel(bottom);
            BuildQuizPanel(bottom);
            BuildCardPanel(bottom);
            BuildResultPanel(bottom);
            HideAllPanels();

            dexView = DexView.Create(root); // 맨 마지막에 만들어 모든 화면 위에 덮는다
        }

        private void BuildSkillPanel(Transform parent)
        {
            skillPanel = UiKit.Stretch("SkillPanel", parent).gameObject;
            skillTitle = UiKit.Label("Title", skillPanel.transform, "", 40, Palette.Text, 0, 0.84f, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 24);

            // 기술 4칸 (기본 기술 + 성유물 3개) + 맨 아래 가방(상처약) 버튼 — Figma 'Battle — 주인공'
            for (int i = 0; i < 4; i++)
            {
                float top = 0.835f - i * 0.165f;
                var button = UiKit.MakeButton($"SkillButton_{i}", skillPanel.transform, "", Palette.Button, 44,
                    0, top - 0.155f, 1, top, bestFit: true);
                var label = UiKit.LabelOf(button);
                label.rectTransform.anchorMin = new Vector2(0, 0.4f);
                label.rectTransform.offsetMin = new Vector2(16, 0);
                var detail = UiKit.Label("Detail", button.transform, "", 26, new Color(1, 1, 1, 0.85f), 0, 0.06f, 1, 0.44f,
                    TextAnchor.MiddleCenter, FontStyle.Normal, true, 16);
                UiKit.Pad(detail.rectTransform, 16, 0, 16, 0);
                skillButtons.Add(button);
                skillDetails.Add(detail);
            }

            itemButton = UiKit.MakeButton("ItemButton", skillPanel.transform, "가방 — 상처약 쓰기", Palette.Button, 40,
                0, 0, 1, 0.155f, bestFit: true);
            var itemColors = itemButton.colors;
            itemColors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            itemButton.colors = itemColors;
            itemButton.onClick.AddListener(ShowItemMenu);

            cancelButton = UiKit.MakeButton("CancelButton", skillPanel.transform, "취소", Palette.Neutral,
                44, 0.25f, 0, 0.75f, 0.155f);
            cancelButton.onClick.AddListener(OnCancelTargeting);

            // 강도 버튼: 왼쪽 큰 배율 + 오른쪽 '단어 n개 연속' · 예상 피해 (평소엔 숨김)
            for (int i = 0; i < 4; i++)
            {
                float top = 0.835f - i * 0.165f;
                var button = UiKit.MakeButton($"Intensity_{i}", skillPanel.transform, "", IntensityColors[i], 40,
                    0, top - 0.155f, 1, top);
                var label = UiKit.LabelOf(button);
                label.text = "";
                var multiplier = UiKit.Display(UiKit.OneLine(UiKit.Label("Multiplier", button.transform, "", 64,
                    i < 2 ? Palette.Gold : Palette.Text, 0, 0, 0.2f, 1)));
                var title = UiKit.Display(UiKit.OneLine(UiKit.Label("Title", button.transform, "", 42, Palette.Text,
                    0.22f, 0.45f, 0.98f, 0.95f, TextAnchor.MiddleLeft)));
                var detail = UiKit.Label("Detail", button.transform, "", 26, new Color(1, 1, 1, 0.85f), 0.22f, 0.06f, 0.98f, 0.46f,
                    TextAnchor.MiddleLeft, FontStyle.Normal, true, 16);
                button.gameObject.SetActive(false);
                intensityButtons.Add(button);
                intensityTexts.Add((multiplier, title, detail));
            }
        }

        private void BuildQuizPanel(Transform parent)
        {
            quizPanel = UiKit.Stretch("QuizPanel", parent).gameObject;
            quizPrompt = UiKit.Display(UiKit.Label("Prompt", quizPanel.transform, "", 84, Palette.Text, 0, 0.8f, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 30));
            quizHint = UiKit.OneLine(UiKit.Label("Hint", quizPanel.transform, "", 30, Palette.TextDim, 0, 0.735f, 1, 0.8f)); // 줄 높이 때문에 잘리지 않게

            var timerBack = UiKit.Pill(UiKit.Panel("TimerBack", quizPanel.transform, Palette.Track,
                0.02f, 0.695f, 0.98f, 0.72f));
            timerFillImage = UiKit.Pill(UiKit.Panel("TimerFill", timerBack.transform, Palette.Info));
            timerFill = timerFillImage.rectTransform;

            var badge = UiKit.Pill(UiKit.Panel("ChainBadge", quizPanel.transform, Palette.Evolve, 1, 1, 1, 1));
            badge.raycastTarget = false;
            badge.rectTransform.pivot = new Vector2(1, 0);
            badge.rectTransform.anchoredPosition = new Vector2(-8, 10);
            var row = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 6, 6);
            row.spacing = 12;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fit = badge.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            chainLabel = UiKit.Display(UiKit.OneLine(UiKit.Label("Multiplier", badge.transform, "", 36, Palette.Text, 0, 0, 1, 1)));
            for (int i = 0; i < 4; i++)
            {
                var pip = UiKit.Pill(UiKit.Panel($"Pip_{i}", badge.transform, Palette.Track));
                pip.raycastTarget = false;
                var size = pip.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = 22;
                chainPips.Add(pip);
            }
            chainCount = UiKit.OneLine(UiKit.Label("Count", badge.transform, "", 30, Palette.Text, 0, 0, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            chainBadge = badge.gameObject;
            chainBadge.SetActive(false);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float top = 0.68f - i * 0.1725f;
                var button = UiKit.MakeButton($"Choice_{i}", quizPanel.transform, "", Palette.Button, 40,
                    0, top - 0.16f, 1, top, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold; // 보기는 읽기 쉬운 본문 글꼴 (Figma Body/Large Bold)
                var choiceColors = button.colors;
                choiceColors.disabledColor = Color.white; // 정답/오답 색이 흐려지지 않게
                button.colors = choiceColors;
                button.onClick.AddListener(() =>
                {
                    if (pickedChoice == int.MinValue) pickedChoice = index;
                });
                choiceButtons.Add(button);
            }
        }

        private void BuildCardPanel(Transform parent)
        {
            cardPanel = UiKit.Stretch("CardPanel", parent).gameObject;
            UiKit.RoundPanel("Bg", cardPanel.transform, Palette.Panel, UiKit.RadiusLg);
            // 새 단어일 때만: 단어 뒤 금빛 + 왼쪽 위 '발견!' 도장
            cardGlow = UiKit.IconImage("DiscoveryGlow", cardPanel.transform, UiKit.GlowSprite(), 0.5f, 0.6f, 0.5f, 0.6f);
            cardGlow.rectTransform.sizeDelta = new Vector2(900, 420);
            cardGlow.preserveAspect = false;
            cardGlow.raycastTarget = false;
            cardBorder = UiKit.Outline(UiKit.Panel("Border", cardPanel.transform, Palette.Gold), UiKit.RadiusLg, 4);
            cardBorder.raycastTarget = false;
            cardTitle = UiKit.Display(UiKit.Label("Title", cardPanel.transform, "", 44, Palette.Gold, 0, 0.8f, 1, 0.97f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 24));
            cardWord = UiKit.Display(UiKit.Label("Word", cardPanel.transform, "", 92, Palette.Text, 0.03f, 0.56f, 0.97f, 0.8f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 36));
            cardMeaning = UiKit.Display(UiKit.Label("Meaning", cardPanel.transform, "", 56, Palette.Gold, 0.03f, 0.38f, 0.97f, 0.57f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 26));
            cardExtra = UiKit.Label("Extra", cardPanel.transform, "", 30, Palette.TextDim, 0.05f, 0.2f, 0.95f, 0.38f,
                TextAnchor.UpperCenter, FontStyle.Normal, true, 20);
            cardConfirm = UiKit.MakeButton("CardConfirmButton", cardPanel.transform, "확인", Palette.Gold, 44,
                0.05f, 0.04f, 0.95f, 0.2f);
            cardConfirmLabel = UiKit.LabelOf(cardConfirm);
            cardConfirmLabel.color = Palette.OnAccent;
            cardConfirm.onClick.AddListener(() => cardConfirmed = true);

            cardStamp = UiKit.Pill(UiKit.Panel("DiscoveryStamp", cardPanel.transform, Palette.Gold, 0, 1, 0, 1));
            cardStamp.raycastTarget = false;
            cardStamp.rectTransform.sizeDelta = new Vector2(210, 84);
            cardStamp.rectTransform.anchoredPosition = new Vector2(120, -6);
            cardStamp.rectTransform.localEulerAngles = new Vector3(0, 0, 10f);
            UiKit.Display(UiKit.OneLine(UiKit.Label("Text", cardStamp.transform, "발견!", 50, Palette.OnAccent, 0, 0, 1, 1)));
            cardGlow.gameObject.SetActive(false);
            cardStamp.gameObject.SetActive(false);
        }

        private void BuildResultPanel(Transform parent)
        {
            resultPanel = UiKit.Stretch("ResultPanel", parent).gameObject;
            UiKit.RoundPanel("Bg", resultPanel.transform, Palette.Panel, UiKit.RadiusLg);
            resultBorder = UiKit.Outline(UiKit.Panel("Border", resultPanel.transform, Palette.Gold), UiKit.RadiusLg, 4);
            resultBorder.raycastTarget = false;
            resultTitle = UiKit.Display(UiKit.Label("Title", resultPanel.transform, "", 60, Palette.Gold, 0, 0.845f, 1, 0.975f,
                TextAnchor.MiddleCenter));
            resultLines = UiKit.Rect("Lines", resultPanel.transform, 0.05f, 0.37f, 0.95f, 0.85f);
            var lines = resultLines.gameObject.AddComponent<VerticalLayoutGroup>();
            lines.spacing = 6;
            lines.childAlignment = TextAnchor.UpperLeft;
            lines.childControlWidth = true;
            lines.childControlHeight = true;
            lines.childForceExpandWidth = true;
            lines.childForceExpandHeight = false;
            resultBody = UiKit.Label("Body", resultPanel.transform, "", 28, Palette.TextDim, 0.05f, 0.2f, 0.95f, 0.36f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 18);
            resultPrimary = UiKit.MakeButton("ResultButton_Primary", resultPanel.transform, "", Palette.Button, 38,
                0.03f, 0.02f, 0.49f, 0.2f, bestFit: true);
            resultPrimary.onClick.AddListener(() => resultChoice = 0);
            resultSecondary = UiKit.MakeButton("ResultButton_Secondary", resultPanel.transform, "", Palette.Heal,
                38, 0.51f, 0.02f, 0.97f, 0.2f, bestFit: true);
            resultSecondary.onClick.AddListener(() => resultChoice = 1);
        }

    }
}
