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
    // 파일 나눔 (partial, #93): 여기 = 상태·시작·전투 흐름(기술 → 문제 → 이벤트 연출) / .Menus 기술·대상·상처약·강도·도망 /
    //   .Result 결산 화면 / .Fx 콤보·숫자·보스 결정타 연출 / .Layout 패널 만들기
    public partial class BattleScreen : MonoBehaviour
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
        // 도망가기 (#53, Figma 'Battle — 도망가기 (#53)'): 상처약 옆. 보스전이면 '도망갈 수 없다!'만 뜨고 차례는 그대로
        private Button fleeButton;
        private Vector2 fleeButtonHome;
        private Coroutine cantFleeRoutine;
        private IReadOnlyList<BattleEvent> fleeEvents;
        private IReadOnlyList<BattleEvent> fleeQuizEvents; // 도망 문제가 나왔을 때 (#84 강한·보통 몬스터)
        private string quizHintPrefix;                      // 문제 안내 앞에 붙는 말 (도망 문제: '도망치려면 맞혀라! — ')
        private const string CantFleeText = "도망갈 수 없다!";
        public int FleeBlockedCount { get; private set; } // 보스전에서 [도망가기]를 누른 횟수 (테스트용)
        public int FleeQuizzes { get; private set; }      // 도망 문제가 나온 횟수 (테스트용, #84)
        public string SkillTitle => skillTitle != null ? skillTitle.text : "";
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

        // 전투 결산 (#42, Figma 'Battle — 전투 결산 꽉 채움'): 화면 전체를 덮고 위에서부터 차례로 놓는다. 버튼은 맨 아래
        private RectTransform resultCard;
        private Text resultTitle, resultSubtitle, resultBody;
        private Button resultPrimary, resultSecondary;
        private Action relayoutResult;      // 지금 결산을 다시 놓기 (화면 높이가 바뀌면 — 안전 영역이 늦게 잡히는 기기 등)
        private float resultLayoutHeight;
        private RectTransform growthCard, expFill, rewardRow, mvpCard, statsRow, newWordsCard, wrongWordsCard, dexBanners;
        private Image growthFace, mvpIcon;
        private Text growthLevel, growthExp, mvpLabel, mvpTitle, mvpDetail;
        private GameObject levelUpPill;
        private readonly List<GameObject> resultDynamic = new List<GameObject>(); // 판마다 새로 만드는 줄들

        // 수련 (#44, Figma 'Battle — 수련'): 허수아비와 싸우며 도감 속 단어만 복습. [수련 종료]를 누르면 수련 결산
        private bool training, endTrainingRequested;
        private int trainingDamage;
        private Button trainingEndButton;
        public bool IsTraining => training;
        // [수련 종료]는 수련 중에만 뜻이 있다. 끝나면 꼭 내려야 다음 일반 전투가 바로 끝나지 않는다
        // (기기 버그 2026-10-09: 수련을 끝낸 뒤 모든 전투가 시작하자마자 '패배…')
        private bool TrainingEnding => training && endTrainingRequested;

        // 보스 결정타 (#41, Figma 'Battle — 보스 결정타'): 하얀 번쩍임 · 느린 화면 동안 가장자리 어둡게 · '결정타!' 도장
        private bool bossBattle;
        private Image finisherFlash, finisherShade;
        private Text finisherStamp;
        private int enemyAreaIndex = -1; // 결정타 동안 적 칸을 어두운 막 위로 올렸다가 되돌릴 자리
        public int FinishersPlayed { get; private set; } // 보스 결정타 연출 횟수 (테스트용)

        // --- 입력 대기용 ---
        private SkillData pickedSkill;
        private ItemData pickedItem;
        private BattleUnit pickedTarget;
        private int pickedIntensity = 1;
        private SkillData targetingSkill;
        private int pickedChoice = int.MinValue;

        // 앱이 가려진 동안(전화·알림·홈 화면)은 문제 시간이 흐르지 않는다 (#48 실기기 점검표).
        // 한 프레임에 흐르는 시간도 MaxTimerStep까지만 — 앱이 멈췄다 돌아온 첫 프레임에 시간이 한꺼번에 지나가지 않게
        private bool appSuspended;
        private const float MaxTimerStep = 0.1f;
        private void OnApplicationPause(bool paused) => appSuspended = paused;
        private void OnApplicationFocus(bool focus) => appSuspended = !focus;
        private bool cardConfirmed;
        private int resultChoice = -1;

        public BattleEngine Engine => engine;
        public MasteryRules MasteryRules => masteryRules; // 필드의 [선제공격] 문제도 같은 숙련도 규칙으로 (#73)
        public bool IsResultVisible => resultPanel != null && resultPanel.activeSelf;
        public string ResultTitle => resultTitle != null ? resultTitle.text : "";
        public GameSession Session => session;
        public bool IsRunning => running;

        private GuideHints guide; // 필드에서 넘겨줌 (전투 연습 씬에는 없음)

        // 첫 전투 안내 (#61): 각 상황을 처음 만날 때 한 번씩, 화면을 막지 않는 한 줄 + 화살표·손끝 (GuideHints)
        public void SetGuide(GuideHints hints) => guide = hints;

        private bool Tip(string id) => !training && guide != null && session != null && !session.Tutorials.Has(id);

        private void ShowTip(string id, string text, Func<RectTransform> target, Func<bool> doneWhen,
            GuideHints.Gesture gesture = GuideHints.Gesture.Point, Func<RectTransform> pillNear = null) =>
            StartCoroutine(guide.Run(session, id,
                new GuideHints.Hint { Text = text, Target = target, DoneWhen = doneWhen, Gesture = gesture, PillNear = pillNear }, saveProgress));

        // 처음 하는 사람(안내가 켜진 필드)은 전투를 몇 번 해 본 뒤에 강도 고르기가 열린다 (#61 기능 하나씩 열기)
        private bool IntensityUnlocked => guide == null || BattleUnlocks.IntensityUnlocked(session.Record);
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

        // 필드에서 조우했을 때 한 판. 결과 화면의 버튼을 누르면 화면을 닫고 onFinished(Victory · Defeat · Fled)
        // intro: 전투 로그 첫 줄 (보스전 등). 비우면 "야생 몬스터 출현!". boss면 도망칠 수 없다
        public void BeginBattle(List<MonsterInstance> enemies, WordDatabase wordBook, Action<BattlePhase> onFinished,
            string intro = null, FieldTheme theme = FieldTheme.Meadow, bool boss = false)
        {
            if (running) throw new InvalidOperationException("이미 전투 중입니다");
            if (!EnsureInitialized()) return;
            SetBackdrop(theme, boss);
            bossBattle = boss;
            ResetFinisher();
            words = wordBook;
            running = true;
            canvas.gameObject.SetActive(true);
            StartCoroutine(SingleBattle(enemies, onFinished, intro));
        }

        // 수련 (#44): 허수아비(주인공과 같은 레벨)와 싸운다. 문제는 wordBooks에서 이미 발견한 단어만.
        // areaBook = 위쪽 '발견한 단어' 표시용 (지금 지역). [수련 종료]를 누르면 결산 → 닫고 onFinished
        public void BeginTraining(MonsterSpecies dummy, IReadOnlyList<WordDatabase> wordBooks, WordDatabase areaBook, TrainingMode mode,
            Action onFinished, FieldTheme theme = FieldTheme.Meadow)
        {
            if (running) throw new InvalidOperationException("이미 전투 중입니다");
            if (dummy == null) throw new ArgumentNullException(nameof(dummy));
            if (!EnsureInitialized()) return;
            var books = new List<IReadOnlyList<WordEntry>>();
            foreach (var book in wordBooks)
                if (book != null) books.Add(book.Words);
            var provider = new TrainingQuizProvider(books, session.Vocabulary, masteryRules, mode, rng);
            SetBackdrop(theme, false);
            bossBattle = false;
            ResetFinisher();
            words = areaBook;
            running = true;
            canvas.gameObject.SetActive(true);
            StartCoroutine(TrainingRoutine(dummy, provider, onFinished));
        }

        private IEnumerator TrainingRoutine(MonsterSpecies dummy, TrainingQuizProvider provider, Action onFinished)
        {
            training = true;
            endTrainingRequested = false;
            trainingDamage = 0;
            string intro = provider.Mode == TrainingMode.WrongNote
                ? "오답 위주 수련 시작! 허수아비는 쓰러지지 않아요 — 마음껏 연습하세요"
                : "수련 시작! 허수아비는 쓰러지지 않아요 — 마음껏 연습하세요";
            session.ResetCombo(); // 수련 콤보는 0부터 (#83)
            StartNewBattle(new List<MonsterInstance> { new MonsterInstance(dummy, Mathf.Max(1, session.Hero.Level)) }, intro, provider);
            roundLabel.text = "수련";
            var scarecrow = engine.Enemies[0];
            viewOf[scarecrow].HpCaption = "누적 피해 0";
            Sync(scarecrow);
            yield return PlayBattle();
            session.ResetCombo(); // 수련에서 쌓은 콤보는 실제 전투로 이어지지 않음 (#83)
            yield return ShowTrainingResult();
            training = false;
            endTrainingRequested = false;
            HideAllPanels();
            if (dexView.IsOpen) dexView.Hide();
            canvas.gameObject.SetActive(false);
            running = false;
            onFinished?.Invoke();
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

        private IEnumerator SingleBattle(List<MonsterInstance> enemies, Action<BattlePhase> onFinished, string intro)
        {
            StartNewBattle(enemies, intro);
            yield return PlayBattle();
            session.EndBattleCombo(DateTime.UtcNow); // 콤보 3분은 전투가 끝난 뒤부터
            yield return ShowResult();
            var outcome = engine.Phase;
            HideAllPanels();
            if (dexView.IsOpen) dexView.Hide();
            canvas.gameObject.SetActive(false);
            running = false;
            onFinished?.Invoke(outcome);
        }

        // provider: 문제 내는 쪽 (비우면 이 지역 단어장 — 수련은 TrainingQuizProvider)
        private void StartNewBattle(List<MonsterInstance> enemies, string intro = null, IQuizProvider provider = null)
        {
            if (!session.CanFight) session.RestoreHero();
            if (provider == null)
            {
                if (quizService == null || quizWords != words)
                {
                    quizWords = words;
                    quizService = new WordQuizService(words.Words, session.Vocabulary, masteryRules, rng);
                }
                provider = quizService;
            }
            // 연속 정답 콤보는 전투가 바뀌어도 이어진다. 보스전은 도망칠 수 없다
            engine = new BattleEngine(session.PartyMembers, enemies, provider, battleConfig, rng,
                session.StartBattleCombo(DateTime.UtcNow), canFlee: !bossBattle);
            fleeEvents = null;
            fleeQuizEvents = null;

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
            // 아군 카드: 혼자면 가운데, 동료와 둘이면 나란히 (#77)
            int members = Mathf.Min(engine.Party.Count, partyViews.Length);
            float partyStart = (1f - members / 3f) / 2f;
            for (int i = 0; i < partyViews.Length; i++)
            {
                if (i < engine.Party.Count)
                {
                    partyViews[i].Root.anchorMin = new Vector2(partyStart + i / 3f, 0f);
                    partyViews[i].Root.anchorMax = new Vector2(partyStart + (i + 1) / 3f, 1f);
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
            while (!engine.IsOver && !TrainingEnding)
            {
                // 1. 기술(과 대상) 선택 — 또는 상처약 (수련이면 [수련 종료]로 끝)
                yield return ChooseSkill();
                if (TrainingEnding) break;
                if (engine.Phase == BattlePhase.Fled)
                {
                    HideAllPanels();
                    yield return PlayEvents(fleeEvents);
                    break;
                }
                if (pickedItem != null)
                {
                    Snapshot();
                    var itemEvents = engine.UseItem(pickedItem, session.Inventory);
                    saveProgress?.Invoke();
                    HideAllPanels();
                    yield return PlayEvents(itemEvents);
                    continue;
                }
                // 도망 문제 (#84): 전투 문제 칸 그대로, 안내만 '도망치려면 맞혀라!' — 틀리면 차례를 놓침
                bool fleeQuiz = engine.IsFleeQuiz;
                if (fleeQuiz)
                {
                    HideAllPanels();
                    yield return PlayEvents(fleeQuizEvents);
                }
                quizHintPrefix = fleeQuiz ? "도망치려면 맞혀라! — " : null;
                var question = fleeQuiz ? engine.CurrentQuestion : engine.SelectSkill(pickedSkill, pickedTarget, pickedIntensity);
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
                        // 틀리면: 정답이 초록·내 답이 빨강으로 표시되고, 오답 카드가 '오답 노트에 추가'를 알려 준다 (따로 설명 없음)
                        string title = choice < 0 ? "시간 초과! 오답 노트에 추가" : "오답! 오답 노트에 추가";
                        if (chainLength > 1) title = (choice < 0 ? "시간 초과" : "오답") + $" — ×{Multiplier(chainLength)} 공격 실패! 오답 노트에 추가";
                        if (fleeQuiz) title = (choice < 0 ? "시간 초과" : "오답") + " — 도망 실패! 오답 노트에 추가";
                        yield return ShowWordCard(title, question.Word, "다음", Palette.Bad);
                    }
                    if (!more) break;
                    question = engine.CurrentQuestion;
                }

                // 5. 결과 연출
                quizHintPrefix = null;
                HideAllPanels();
                yield return PlayEvents(events); // 콤보는 글자 아래 'n 연속 정답 · 피해 +n%'가 곧 설명
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

            Func<bool> chosen = () => pickedSkill != null || pickedItem != null || itemMode || intensityMode || targetingSkill != null || engine.IsOver;
            if (Tip(TutorialProgress.Potion) && actor.Hp * 2 < actor.MaxHp && itemButton.gameObject.activeInHierarchy && itemButton.interactable)
                ShowTip(TutorialProgress.Potion, "HP가 낮아요 — 상처약으로 회복", () => (RectTransform)itemButton.transform, chosen);
            if (Tip(TutorialProgress.Skill) && skillButtons[0].gameObject.activeInHierarchy)
                ShowTip(TutorialProgress.Skill, "기술을 고르면 단어 문제!", () => (RectTransform)skillButtons[0].transform, chosen,
                    GuideHints.Gesture.Tap);
            else if (actor.Companion != null && Tip(TutorialProgress.CompanionTurn) && skillButtons[0].gameObject.activeInHierarchy)
                ShowTip(TutorialProgress.CompanionTurn, $"{actor.DisplayName} 차례 — {actor.DisplayName}도 단어를 맞혀야 기술이 나가요",
                    () => (RectTransform)skillButtons[0].transform, chosen, GuideHints.Gesture.Tap); // 처음 함께 싸울 때 (#81, GDD 9-6)

            while (pickedSkill == null && pickedItem == null && !TrainingEnding && !engine.IsOver && engine.Phase == BattlePhase.ChoosingSkill)
            {
                if (intensityMode && Tip(TutorialProgress.Intensity))
                    ShowTip(TutorialProgress.Intensity, "새로 열림: 단어를 더 맞히면 더 세게!",
                        () => (RectTransform)intensityButtons[Mathf.Min(1, intensityButtons.Count - 1)].transform,
                        () => pickedSkill != null || !intensityMode);
                yield return null;
            }
            ClearSelectable();
        }

        private IEnumerator AskQuestion(QuizQuestion question, Action<float> onDone)
        {
            ShowPanel(quizPanel);
            quizPrompt.text = question.Prompt;
            string hint = (quizHintPrefix ?? "") + (question.Direction == QuizDirection.EnglishToMeaning ? "이 단어의 뜻은?" : "알맞은 영단어는?");
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
            pickedChoice = int.MinValue;
            // 첫 문제: 막지 않고 한 줄만 (시간은 그대로 흐름). 크리티컬은 시간 막대가 금색인 동안 '★ 크리티컬 찬스!'가 곧 설명
            if (Tip(TutorialProgress.Quiz))
                ShowTip(TutorialProgress.Quiz, $"{battleConfig.AnswerTimeLimitSeconds:0}초 안에! 빨리 맞히면 크리티컬",
                    () => quizPrompt.rectTransform, () => pickedChoice != int.MinValue, GuideHints.Gesture.None);
            float limit = battleConfig.AnswerTimeLimitSeconds;
            float elapsed = 0f;
            string criticalHint = $"{hint}   ★ 크리티컬 찬스!";
            bool? shownCritical = null;

            while (pickedChoice == int.MinValue)
            {
                if (!appSuspended) elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxTimerStep);
                if (elapsed >= limit)
                {
                    pickedChoice = -1;
                    elapsed = limit;
                    break;
                }

                bool critical = elapsed <= battleConfig.CriticalTimeSeconds && engine.CanStillCrit;
                if (shownCritical != critical) // 글자·색은 크리티컬이 바뀔 때만 (매 프레임 새 글자를 만들지 않게)
                {
                    shownCritical = critical;
                    quizHint.text = critical ? criticalHint : hint;
                }
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
            bool marks = font.HasCharacter('✓') && font.HasCharacter('×'); // ✕는 글꼴에 없어 ×로 (#48)
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
                    if (marks) label.text = "×  " + label.text;
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
            if (discovery) Sound.Play(Sfx.NewWord); // 짧은 '발견' 소리 (#66 사용자: 가장 자주 듣는 소리라 가볍게 — 음악은 멈추지 않음)
            if (discovery && Tip(TutorialProgress.NewWord))
                ShowTip(TutorialProgress.NewWord, "뜻을 기억하고 [확인]", () => (RectTransform)cardConfirm.transform, () => cardConfirmed,
                    GuideHints.Gesture.Tap, () => (RectTransform)cardPanel.transform); // 알약은 카드 위 (뜻을 가리지 않게)

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

                    case BattleEventType.Fled:
                        Log($"{UiKit.WithJosa(e.Actor.DisplayName, "은", "는")} 무사히 도망쳤다!");
                        Sound.Play(Sfx.Flee);
                        yield return Wait(0.6f);
                        break;

                    case BattleEventType.FleeQuiz: // 강한 몬스터 (또는 보통 몬스터의 1/4) — 도망치려면 단어를 맞혀야 함 (#84)
                        Log(e.Target != null
                            ? $"{UiKit.WithJosa(e.Target.DisplayName, "이", "가")} 길을 막았다! 도망치려면 단어를 맞혀라"
                            : "도망치려면 단어를 맞혀라!");
                        Sound.Play(Sfx.Encounter);
                        yield return Wait(0.7f);
                        break;

                    case BattleEventType.FleeFailed:
                        Log("도망 실패! 차례를 놓쳤다");
                        Sound.Play(Sfx.Fail);
                        yield return Wait(0.6f);
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
                        if (IsBossFinisher(events, e))
                        {
                            yield return BossFinisher(e);
                            break;
                        }
                        var d = shown[e.Target];
                        bool dummyHit = e.Target.Monster != null && e.Target.Monster.Species.IsTrainingDummy;
                        shown[e.Target] = (dummyHit ? d.hp : Mathf.Max(0, d.hp - e.Amount), Mathf.Max(0, d.shield - e.Absorbed));
                        if (dummyHit)
                        {
                            trainingDamage += e.Amount;
                            viewOf[e.Target].HpCaption = $"누적 피해 {trainingDamage}";
                        }
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
                        if (bossBattle && !e.Target.IsPlayerSide && Has(events, BattleEventType.Victory))
                        {
                            // 보스는 느린 화면 그대로 천천히 쓰러진다
                            Sound.Play(Sfx.Faint, 0.7f);
                            PlayFx(e.Target, Fx.Smoke, 2.5f, 1.4f);
                            yield return FadeShade(0f, 0.6f);
                            yield return Wait(0.4f);
                            break;
                        }
                        Sound.Play(Sfx.Faint);
                        PlayFx(e.Target, Fx.Smoke);
                        yield return Wait(0.5f);
                        break;

                    case BattleEventType.Charging: // 힘을 모으는 기술 (#120): 이번 차례는 힘만 모으고 다음 차례에 큰 한 방 — 보호막으로 대비할 틈
                        Log($"{UiKit.WithJosa(e.Actor.DisplayName, "이", "가")} 힘을 모으고 있다");
                        Float(e.Actor, "힘을 모은다!", Palette.Gold, 48);
                        Sound.Play(Sfx.EvolveLight);
                        PlayFx(e.Actor, Fx.Sparkle, 1.5f, 1.3f);
                        yield return Wait(0.9f);
                        break;

                    case BattleEventType.RoundStarted: // 아군 보호막은 전투 내내 남음 (#120) — 화면도 실제 값 그대로
                        roundLabel.text = training ? "수련" : $"라운드 {e.Round}";
                        SyncAll();
                        yield return Wait(0.2f);
                        break;
                }
            }

            RefreshVocabLabel();
            SyncAll();
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

        // ------------------------------------------------------------------ 패널 전환

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
    }
}
