using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
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
    // 탑다운 필드: 한 칸씩 이동, 풀숲 조우 → 전투(BattleScreen을 위에 덮음) → 원래 자리로 복귀.
    // 출입구(D)를 밟으면 다른 지역으로 (같은 씬에서 맵만 바꿔 그림). 보스를 물리쳐야 열리는 출입구는 그 전까지 막혀 있고,
    // 보스를 물리치면 카메라가 그 출입구로 가서 길이 열리는 모습을 보여 준 뒤 돌아온다 (RevealGates).
    // 보물상자·회복의 샘·성유물 제단·상점·보스는 옆에 서서 왼쪽 아래 [확인](키보드 Space·Enter·Z)으로 사용.
    // 가까이 가면 오브젝트 위에 이름표가 뜬다. 입력은 오른쪽 아래 가상 스틱(끝까지 밀면 달리기) + 키보드(방향키/WASD, Shift = 달리기).
    // 이동·조우 규칙은 FieldWalker / EncounterCounter(순수 C#)가 하고 여기서는 화면과 입력만 다룬다
    // 파일 나눔 (partial, #93): 여기 = 상태·초기화·이동·입력·지역 들어가기 / .Places [확인]으로 쓰는 장소 / .Jungle 정글 기믹 /
    //   .Battles 조우·전투 결과·보스 뒤·수련 / .Roamers 보이는 몬스터·선제공격 / .World 맵 타일 그리기 / .Hud HUD·안내·메뉴·토스트
    public partial class FieldScreen : MonoBehaviour
    {
        [SerializeField] private FieldArea area;
        [SerializeField] private BattleConfig battleConfig = new BattleConfig();
        [Tooltip("한 칸 이동에 걸리는 시간(초)")]
        // 한 칸 걷는 시간 · 달리면 그 몇 배 (보이는 몬스터는 이 사이 속도로 쫓아옴 — RoamingRules.ChaseStepSeconds, RoamingTests)
        public const float WalkStepSeconds = 0.16f;
        public const float RunStepRatio = 0.55f;

        [SerializeField] private float stepDuration = WalkStepSeconds;
        [Tooltip("달릴 때 한 칸 시간 = 걷기 시간 × 이 값")]
        [SerializeField] private float runStepRatio = RunStepRatio;
        [Tooltip("화면 가로에 보이는 타일 수")]
        [SerializeField] private float tilesAcross = 11f;
        [Tooltip("연출 시간 배율. 테스트에서는 아주 작게")]
        [SerializeField] private float animationScale = 1f;
        [Tooltip("처음 하는 사람에게 튜토리얼 안내를 보여 줄지 (테스트에서는 Configure가 꺼 둔다)")]
        [SerializeField] private bool showTutorials = true;
        [SerializeField] private bool heroSetup = true; // 모습·이름을 아직 안 정했으면 고르는 화면 (#124) — 테스트는 Configure로 끔

        private GameSession session;
        private GameDatabase database;
        private GameSettings settings;
        private Action saveProgress;
        private Action saveSettings;
        private Action deleteSave;
        private string statusMessage;
        private bool loadedFromSave;

        private System.Random rng;
        private FieldWalker walker;
        private EncounterCounter encounterCounter;
        private bool initialized;
        private bool moving;
        private bool inBattle;
        private bool transitioning;
        private float moveT;
        private float walkTime; // 걷기 애니메이션 시계 (멈추면 서 있는 모습)
        private float currentStep; // 지금 칸을 가는 데 걸리는 시간 (걷기/달리기)
        private bool running;
        private Direction? stickDirection; // 스틱으로 가던 방향 (대각선 근처에서 떨리지 않게)
        private Vector3 moveFrom, moveTo;
        private float interactCooldown;
        private bool confirmRequested;  // [확인]을 눌렀다 (걷는 중이면 걸음이 끝난 뒤 처리)
        private float confirmHint;      // 오브젝트에 부딪히면 [확인] 버튼을 잠깐 크게 깜빡여 알려 준다
        private bool confirmReady;

        private Camera cam;
        private Vector3? cameraFocus; // 연출 중 카메라가 볼 곳 (없으면 주인공)
        private bool revealingGate;
        private readonly List<AreaGate> revealedGates = new List<AreaGate>();
        private string lastGateBanner = "";
        private Vector3 lastGateCamera;
        private Transform player;
        private SpriteRenderer playerRenderer;
        private Tilemap tilemap;
        private readonly Dictionary<string, Tile> tileCache = new Dictionary<string, Tile>();
        private readonly List<GameObject> props = new List<GameObject>(); // 칸보다 큰 그림 (석상·동굴, #80)
        private BattleScreen battle;
        private bool bossBattle;

        private RectTransform hudRoot;
        private Canvas hudCanvas;
        private FieldNameTags nameTags;
        private Image confirmImage, confirmGlow, confirmRing;
        private Text confirmLabel;
        private Text areaLabel, dexLabel, toastText;
        private RectTransform goldSpot;
        private DexProgress hudDex;
        private int shownGold = -1;      // HUD에 보이는 골드 (늘면 굴러 올라감 — #127)
        private Coroutine goldMotion;
        private GameObject toastPanel;
        private float toastUntil;
        private Image flash;
        private VirtualStick stick;
        private RectTransform menuRect;
        private GuideHints guide;          // 처음 하는 사람용 한 줄 안내 (#61, 화면을 막지 않음)
        private int interactions;          // [확인]으로 쓴 횟수 ('해 봤는지' 확인용)
        private float bubbleKeepUntil;     // 혼잣말 말풍선은 이때까지 걸어도 남아 있음
        // 메뉴 빨간 점 (#61): id → 점
        private readonly Dictionary<string, RectTransform> badges = new Dictionary<string, RectTransform>();
        private RectTransform objectivePill; // 목표 한 줄 (#61): 미니맵 아래
        private Text objectiveText;
        private Objective objective;
        private GameObject saveMark;       // 저장될 때 잠깐 '✓ 저장' (#61, 자동 저장 안내 대신)
        private float saveMarkUntil, lastSaveMark = -999f;
        private const float SaveMarkGap = 20f; // 자주 저장돼도 이 간격보다 자주 띄우지 않음
        private PaywallView paywall;
        private IStore store;               // 결제 창구 (GameManager 것, 테스트는 가짜)
        private Entitlements entitlements; // 산 상품 — 없으면(테스트) 정식판 잠금 없음
        private bool paywallArmed = true;  // 정식판 안내를 닫은 뒤에는 스틱을 한 번 떼야 다시 뜬다
        private bool fieldTutorialRunning; // 첫 안내 중에는 몬스터가 나오지 않는다
        private int stepsTaken;
        private int runSteps;
        private DexView dexView;
        private TrainingView trainingView;
        private SpeechBubble bubble; // 주인공 머리 위 말풍선 (사전 읽기 등, #46)
        private bool bubbleShown;
        private RelicAltarView altarView;
        private ShopView shopView;
        private InventoryView inventoryView;
        private SettingsView settingsView;
        private MinimapView minimap;
        private MapView mapView;
        private SkillLearnView learnView;
        private ConfirmDialog quitDialog; // 뒤로가기 → '게임을 끝낼까요?'
        private ConfirmDialog returnDialog; // 보스를 이긴 뒤 '시작 지점으로 돌아갈까요?' (#60)
        private Text heroName, heroHp;
        private Image heroBadge;
        private Image companionBadge, companionFace, companionHpFill; // 동료 배지 (#77, Figma 'Companion Badge')
        private Text companionName;
        private CompanionCutscene companionCutscene;                  // 동료 조우 연출 (#77)
        private FieldFollower follower;                               // 주인공을 따라다니는 동료 그림 (#78)
        private Image heroHpFill;
        private readonly List<(Image back, Image icon, Text level, Text empty)> relicSlots = new List<(Image, Image, Text, Text)>();

        public Vector2Int PlayerCell => walker.Position;
        public Direction Facing => walker.Facing;
        public bool IsMoving => moving;
        public bool IsRunning => moving && running; // 지금 칸을 달리는 중
        public VirtualStick Stick => stick;
        public GuideHints Guide => guide;
        public string ObjectiveText => objectivePill != null && objectivePill.gameObject.activeSelf ? objectiveText.text : "";
        public Objective CurrentObjective => objective;
        public bool IsBadgeVisible(string id) => badges.TryGetValue(id, out var dot) && dot.gameObject.activeSelf;
        public int SaveMarksShown { get; private set; } // '✓ 저장' 표시 횟수 (테스트용)
        public PaywallView Paywall => paywall;
        public bool IsInBattle => inBattle || transitioning;
        public int ShownGold => shownGold; // HUD 골드 (테스트용)
        public int RespawnsPlayed { get; private set; }   // 지고 나서 다시 일어나는 연출 횟수 (테스트용)
        public bool SawRespawnPose { get; private set; }  // 다시 일어날 때 웅크린 자세를 거쳤는지 (테스트용)
        public int DustPuffs { get; private set; }        // 달릴 때 발밑 먼지 (테스트용)
        public Color LastDustColor { get; private set; }  // 마지막 흙먼지 색 = 떠난 칸 바닥 색 (테스트용)
        public Sprite PlayerSprite => playerRenderer != null ? playerRenderer.sprite : null;
        public BattleScreen Battle => battle;
        public GameSession Session => session;
        public FieldArea CurrentArea => area;
        public bool IsPanelOpen => dexView.IsOpen || trainingView.IsOpen || altarView.IsOpen || shopView.IsOpen
                                   || inventoryView.IsOpen || settingsView.IsOpen || mapView.IsOpen || learnView.IsOpen
                                   || (quitDialog != null && quitDialog.IsOpen) || (returnDialog != null && returnDialog.IsOpen)
                                   || (paywall != null && paywall.IsOpen) || (preemptView != null && preemptView.IsOpen)
                                   || (companionCutscene != null && companionCutscene.IsPlaying)
                                   || (pauseTutorial != null && pauseTutorial.IsShowing)
                                   || (tabletView != null && tabletView.IsOpen)
                                   || (stoneView != null && stoneView.IsOpen) || heroSetupRunning;
        public PauseTutorial PauseTutorial => pauseTutorial;
        public bool IsPreemptRangeVisible => preemptRange != null && preemptRange.gameObject.activeSelf;
        public CompanionCutscene CompanionCutscene => companionCutscene;
        public FieldFollower Follower => follower;
        public TabletQuizView TabletView => tabletView;
        public QuizQuestion TabletQuestion => tabletQuiz?.Current; // 테스트용
        public bool IsRafting => walker != null && walker.Rafting;
        public bool IsRaftVisible => raftRenderer != null && raftRenderer.gameObject.activeSelf;
        public int MooredRaftsVisible => mooredRafts.FindAll(r => r != null && r.activeSelf).Count;
        public int StreamRipples => streams != null ? streams.ActiveCount : 0;
        public int ScatterCount => scatter != null ? scatter.Count : 0;
        public bool HasFog => fog != null && fog.IsShowing; // 바닥 안개 (#103, 테스트용)
        public int FogRepaints => fog != null ? fog.Repaints : 0; // 안개 무늬가 흘러가 다시 칠한 횟수 (#104, 테스트용)
        public float LastStepSeconds => currentStep; // 마지막 한 칸에 걸린 시간 (물살·뗏목 확인용)
        public int LeversPulled { get; private set; }
        public bool IsCameraPanning => cameraFocus.HasValue;
        public bool IsCompanionBadgeVisible => companionBadge != null && companionBadge.gameObject.activeSelf;
        public SkillLearnView LearnView => learnView;
        public RoamingMonsters Roaming => roaming;
        public bool IsPreemptAvailable => preemptPrompt != null && preemptPrompt.activeSelf;
        public bool IsPreemptLockVisible => preemptLock != null && preemptLock.IsActive;
        public PreemptView Preempt => preemptView;
        public int PreemptStrikes { get; private set; } // 선제공격 성공 횟수 (테스트용)
        public int RoamerViewCount => roamerViews != null ? roamerViews.Count : 0;
        public TrainingView TrainingView => trainingView;
        public SpeechBubble Bubble => bubble;
        public bool IsReadingDictionary { get; private set; } // 점점점 말풍선이 나오는 동안 (움직일 수 없음)
        public Func<DateTime> LocalClock { get; set; } = () => DateTime.Now; // 사전 '하루 한 번'의 기준 (테스트에서 바꿀 수 있음)
        public bool IsQuitDialogOpen => quitDialog.IsOpen;
        public bool IsReturnDialogOpen => returnDialog.IsOpen;
        public int ReturnsToStart { get; private set; } // 보스를 이긴 뒤 시작 지점으로 돌아간 횟수 (테스트용)
        public MinimapView Minimap => minimap;
        public string ToastMessage => toastPanel != null && toastPanel.activeSelf ? toastText.text : "";
        public IEnumerable<string> VisibleNameTags => nameTags.VisibleNames;
        public bool ConfirmReady => confirmReady; // 지금 [확인]으로 쓸 것이 옆에 있는지
        public bool IsRevealingGate => revealingGate; // 보스를 물리쳐 열린 길을 보여 주는 중
        public IReadOnlyList<AreaGate> RevealedGates => revealedGates; // 지금까지 연출로 보여 준 출입구
        public string LastGateBanner => lastGateBanner;
        public Vector3 LastGateCamera => lastGateCamera; // 길이 열리는 순간 카메라 위치
        public Vector3 CameraPosition => cam.transform.position;

        // 코드로 만들 때(테스트) Start 전에 호출. session을 안 주면 GameManager 것을 쓴다
        // database: 세이브의 마지막 지역이 다른 곳이면 거기서 시작하기 위해 지역을 찾는 데 쓴다 (소지품 화면의 아이템 찾기에도)
        // gameSettings / onSettingsChanged / onDeleteSave: 설정 화면용. 안 주면 GameManager 것
        public void Configure(FieldArea fieldArea, GameSession gameSession = null, Action onSave = null,
            float step = 0.16f, float animScale = 1f, BattleConfig config = null, GameDatabase gameDatabase = null,
            GameSettings gameSettings = null, Action onSettingsChanged = null, Action onDeleteSave = null, bool tutorials = false,
            IStore purchaseStore = null, Entitlements owned = null, bool setup = false)
        {
            area = fieldArea;
            showTutorials = tutorials;
            heroSetup = setup;
            store = purchaseStore;
            entitlements = owned;
            session = gameSession;
            database = gameDatabase;
            saveProgress = onSave;
            stepDuration = step;
            animationScale = animScale;
            if (config != null) battleConfig = config;
            settings = gameSettings;
            saveSettings = onSettingsChanged;
            deleteSave = onDeleteSave;
        }

        private void Start() => EnsureInitialized();

        private bool EnsureInitialized()
        {
            if (initialized) return true;

            if (session == null && GameManager.Instance != null)
            {
                var manager = GameManager.Instance;
                session = manager.Session;
                saveProgress = manager.Save;
                statusMessage = manager.StatusMessage;
                loadedFromSave = manager.LoadedFromSave;
                if (database == null) database = manager.Database;
                if (store == null) store = manager.Store;
                if (entitlements == null) entitlements = manager.Entitlements;
                if (settings == null) settings = manager.Settings;
                if (saveSettings == null) saveSettings = manager.SaveSettings;
                // 설정에서 저장 데이터를 지우면 타이틀로 (타이틀 씬이 없으면 이 씬을 처음부터)
                if (deleteSave == null) deleteSave = () =>
                {
                    manager.DeleteSave();
                    GameManager.ReturnToTitle();
                };
                manager.MarkPlaying();
            }
            if (settings == null) settings = new GameSettings();
            if (session == null || area == null)
            {
                Debug.LogError("[FieldScreen] GameManager(또는 Configure의 session) / area 가 비어 있습니다");
                return false;
            }

            // 세이브의 마지막 위치가 다른 지역(예: 던전)이면 그 지역에서 시작
            var startArea = area;
            string savedAreaId = session.World.AreaId;
            if (database != null && savedAreaId != null && savedAreaId != area.AreaId)
                startArea = database.FindArea(savedAreaId) ?? area;

            FieldMap map;
            try
            {
                map = startArea.Map;
            }
            catch (FormatException e)
            {
                Debug.LogError($"[FieldScreen] {startArea.name} 맵 오류: {e.Message}");
                return false;
            }

            initialized = true;
            rng = new System.Random();
            area = startArea;
            PlayerArt.SetLook(session.Hero.LookOrDefault); // 고른 모습 (#124)

            CreateWorldObjects();
            BuildHud();
            BuildBattle();

            // 저장된 위치가 이 지역의 걸을 수 있는 칸(뗏목이었으면 강 위)이면 거기서, 아니면 시작 위치에서
            bool onRaft = session.World.Rafting;
            bool savedHere = session.World.TryGetPosition(area.AreaId, out var saved)
                             && (onRaft ? map.IsRaftWater(saved) : map.IsWalkable(saved));
            EnterArea(area, savedHere ? saved : map.Start, savedHere && onRaft);

            if (!string.IsNullOrEmpty(statusMessage)) ShowToast(statusMessage, 3f);
            if (heroSetup && session.NeedsHeroSetup) StartCoroutine(HeroSetup()); // 모습·이름 → (새 게임이면 프롤로그) → 걷기 안내
            else if (showTutorials && !session.Tutorials.Has(TutorialProgress.Field)) StartCoroutine(FieldGuide());
            return true;
        }

        private void Update()
        {
            if (!initialized) return;
            if (BackPressed()) HandleBack();
            UpdateToast();
            UpdateCamera();
            nameTags.Tick(cam, hudCanvas, Time.unscaledDeltaTime);
            bubble.Tick(cam, hudCanvas, player.position + Vector3.up * 0.45f);
            if (bubble.IsVisible != bubbleShown)
            {
                bubbleShown = bubble.IsVisible;
                RefreshNameTags();
            }

            if (saveMark.activeSelf && Time.unscaledTime > saveMarkUntil) saveMark.SetActive(false);
            PulseBadges();
            UpdateRoamers(Time.unscaledDeltaTime);
            characterSelect.Tick(Time.unscaledDeltaTime);
            streams.Tick(Time.deltaTime, walker.Position);
            fog.Tick(Time.deltaTime);
            UpdateDarkness();
            tabletView.Tick(Time.unscaledDeltaTime);
            stoneView.Tick(Time.unscaledDeltaTime);
            UpdateRaft();

            if (inBattle || transitioning || IsPanelOpen)
            {
                // 창을 닫은 직후 같은 키(Enter 등)로 바로 다시 열리지 않게
                if (IsPanelOpen) interactCooldown = 0.5f;
                confirmRequested = false;
                if (!ReadMove().direction.HasValue) paywallArmed = true; // 정식판 안내가 떠 있는 동안 손을 뗐으면 다시 뜰 수 있게
                return;
            }

            if (moving)
            {
                moveT += Time.deltaTime / Mathf.Max(0.001f, currentStep);
                player.position = Vector3.Lerp(moveFrom, moveTo, Mathf.Clamp01(moveT));
                walkTime += Time.deltaTime * (running ? 1.8f : 1f); // 달리면 발도 빠르게
                int frame = Mathf.FloorToInt(walkTime * PlayerArt.FramesPerSecond);
                playerRenderer.sprite = walker.Rafting ? PlayerArt.Get(walker.Facing, 0)
                    : running ? PlayerArt.GetRun(walker.Facing, frame) : PlayerArt.Get(walker.Facing, frame);
                if (moveT >= 1f)
                {
                    float leftover = (moveT - 1f) * currentStep;
                    moving = false;
                    player.position = moveTo;
                    if (!OnStepFinished()) return;
                    // 계속 밀고 있으면 서지 않고 바로 다음 칸으로 (칸마다 한 프레임씩 멈칫하던 끊김 없애기)
                    var (next, run) = ReadMove();
                    if (next.HasValue && !IsPanelOpen)
                    {
                        TryStep(next.Value, run);
                        if (moving)
                        {
                            moveT = leftover / Mathf.Max(0.001f, currentStep);
                            player.position = Vector3.Lerp(moveFrom, moveTo, Mathf.Clamp01(moveT));
                        }
                    }
                }
                return;
            }

            interactCooldown -= Time.deltaTime;
            UpdateConfirmButton();
            UpdateGuide();
            if (confirmRequested || ConfirmKeyPressed())
            {
                confirmRequested = false;
                if (TryInteract()) return;
            }

            var (direction, runInput) = ReadMove();
            if (!direction.HasValue) paywallArmed = true;
            if (direction.HasValue) TryStep(direction.Value, runInput);
            else if (walkTime > 0f)
            {
                walkTime = 0f;
                playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            }
        }

        // ------------------------------------------------------------------ 이동·상호작용

        private void TryStep(Direction direction, bool run = false)
        {
            var bumped = roaming?.At(walker.Position + direction.ToOffset());
            if (bumped != null)
            {
                walker.Face(direction);
                playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
                StartRoamerBattle(bumped, null);
                return;
            }
            var from = walker.Position;
            bool wasRafting = walker.Rafting;
            var outcome = walker.TryStep(direction);
            if (outcome.Kind != StepKind.Moved) playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);

            switch (outcome.Kind)
            {
                case StepKind.Moved:
                    moving = true;
                    running = run;
                    if (bubble.IsVisible && Time.unscaledTime > bubbleKeepUntil) bubble.Hide();
                    // 달리면 걸음마다 발뒤꿈치 뒤로 흙먼지가 튀어 흩어진다 (#51 사용자 레퍼런스: 달리는 발 뒤로 날리는 먼지).
                    // 그림 = 폭발 시트의 마지막 3칸(회색조). 떠난 칸의 바닥 색을 입힌다 — 흙길은 흙빛, 풀숲은 풀빛, 서고는 돌가루 (#53).
                    // #53 기기 확인: 작아서 잘 안 보임 → 1.5칸, 0.5초 동안 남고 덜 옅어짐. 달리는 반대 방향으로 조금 밀려난다
                    if (outcome.Boarded || outcome.Landed) Board(outcome.Boarded);
                    if (wasRafting || walker.Rafting) run = false; // 뗏목은 달리기 없음 (먼지도 없음)
                    runSteps = run ? runSteps + 1 : 0;
                    if (run)
                    {
                        DustPuffs++;
                        var back = -(Vector3)(Vector2)direction.ToOffset();
                        float side = runSteps % 2 == 0 ? 0.12f : -0.12f; // 왼발·오른발
                        var across = new Vector3(back.y, -back.x, 0f) * side;
                        var at = player.position + Vector3.down * 0.3f + back * 0.5f + across;
                        LastDustColor = FieldArt.DustColor(area.Theme, walker.Map.Get(outcome.Target - direction.ToOffset()));
                        StartCoroutine(FieldFx.Play(transform, at, Fx.Dust, 1.5f, animationScale, 9, 6f,
                            back * 1.0f + Vector3.up * 0.2f, 0.45f, direction == Direction.Left, LastDustColor));
                    }
                    currentStep = stepDuration * (run ? runStepRatio : 1f);
                    if (wasRafting && walker.Rafting) currentStep = stepDuration * RaftStepRatio;
                    else if (walker.Map.IsWithStream(from, direction)) currentStep *= StreamStepRatio; // 물살을 따라가면 빨라짐 (#81)
                    moveT = 0f;
                    moveFrom = player.position;
                    moveTo = CellCenter(outcome.Target);
                    break;
                case StepKind.BlockedByObject:
                    confirmHint = 0.8f; // 부딪히면 쓰지 않고 [확인] 버튼만 깜빡여 알려 준다
                    break;
                case StepKind.BlockedByGate:
                    if (!IsDoorLocked(outcome.Target) && IsPaywalled(outcome.Target))
                    {
                        if (paywallArmed) ShowPaywall(outcome.Target);
                    }
                    else ShowGateHint(outcome.Target);
                    break;
            }
        }

        // 잠긴 출입구: 어느 보스를 물리치면 열리는지 알려 준다
        private void ShowGateHint(Vector2Int cell)
        {
            var exit = area.GetExit(cell);
            var bossArea = exit?.OpenedByBossOf;
            if (bossArea == null) return;
            string target = exit.Target != null ? exit.Target.DisplayName : "저쪽";
            string boss = bossArea.Boss.Species.DisplayName;
            string message = $"{UiKit.WithJosa(target, "으로", "로")} 가는 길이 덤불로 막혀 있다…\n" +
                             $"{bossArea.DisplayName}의 {UiKit.WithJosa(boss, "을", "를")} 물리치면 열릴 것 같다";
            if (ToastMessage != message) ShowToast(message, 3f);
        }

        // 그 칸의 출입구가 지금 잠겨 있는지 (지금 지역 기준)
        private bool IsDoorLocked(Vector2Int cell) => IsDoorLocked(area, cell);

        private bool IsDoorLocked(FieldArea fieldArea, Vector2Int cell) => !session.World.IsExitOpen(fieldArea.GetExit(cell));

        // [확인]: 바라보는 칸(없으면 옆 칸)의 상자·샘·제단·상점·보스를 쓴다. 쓴 것이 있으면 true
        private bool TryInteract()
        {
            if (interactCooldown > 0f) return false;
            var direction = FieldInteraction.FindTarget(walker.Map, walker.Position, walker.Facing, IsClearedBossSpot);
            if (!direction.HasValue) return false;
            walker.Face(direction.Value);
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            var cell = walker.Position + direction.Value.ToOffset();
            interactions++;
            Interact(walker.Map.Get(cell), cell);
            return true;
        }

        // 안드로이드 뒤로가기 버튼 = Input System의 Escape 키
        internal static bool BackPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        // 뒤로가기: 맨 위 창부터 닫고, 아무 창도 없으면 '게임을 끝낼까요?'. 전투 중이면 전투 화면에 맡기고, 연출 중에는 무시
        public void HandleBack()
        {
            if (paywall.IsOpen)
            {
                paywall.Close(); // 결제 창이 떠 있는 동안은 닫히지 않음
                return;
            }
            if (inBattle)
            {
                battle.HandleBack();
                return;
            }
            if (transitioning || preemptView.IsOpen || heroSetupRunning) return; // 모습·이름 고르기는 끝까지 (#124)
            if (tabletView.IsOpen)
            {
                if (!tabletBusy) CloseTablet();
                return;
            }
            if (stoneView.IsOpen)
            {
                CloseStone();
                return;
            }
            if (quitDialog.IsOpen) quitDialog.Hide();
            else if (returnDialog.IsOpen) returnDialog.Hide(); // = 여기 있기
            else if (learnView.IsOpen) learnView.Cancel();
            else if (inventoryView.LearnView.IsOpen) inventoryView.LearnView.Cancel();
            else if (altarView.IsOpen)
            {
                if (!altarView.Cutscene.IsPlaying) altarView.Hide(); // 각성 연출 중에는 끝까지 보기
            }
            else if (inventoryView.IsOpen)
            {
                if (!inventoryView.CancelRelicChoosing()) inventoryView.Hide(); // 바꿀 칸 고르기 중이면 그것만 취소 (#91)
            }
            else if (shopView.IsOpen) shopView.Hide();
            else if (dexView.IsOpen) dexView.Hide();
            else if (trainingView.IsOpen) trainingView.Hide();
            else if (settingsView.IsOpen) settingsView.Hide();
            else if (mapView.IsOpen) mapView.Hide();
            else if (bubble.IsVisible) bubble.Hide();
            else if (GameManager.CanQuit)
                quitDialog.Show("게임을 끝낼까요?", "지금까지의 기록은 자동으로 저장돼요.", "끝내기", false, GameManager.QuitGame);
        }

        private static bool ConfirmKeyPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                                        || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame);
        }

        private void Interact(FieldTile tile, Vector2Int cell)
        {
            interactCooldown = 0.3f;
            switch (tile)
            {
                case FieldTile.Chest: OpenChest(cell); break;
                case FieldTile.Fountain: UseFountain(); break;
                case FieldTile.Altar: altarView.Show(session, OnTownChanged); break;
                case FieldTile.Shop:
                    if (area.Shop == null) ShowToast("상점 문이 닫혀 있다.");
                    else shopView.Show(area.Shop, session, OnTownChanged);
                    break;
                case FieldTile.Boss: ChallengeBoss(); break;
                case FieldTile.Lectern: ReadDictionary(); break;
                case FieldTile.Gate: KnockGate(); break;
                case FieldTile.Key: TakeKey(cell); break;
                case FieldTile.Lever: PullLever(cell); break;
                case FieldTile.Tablet: ReadTablet(cell); break;
                case FieldTile.Shrine: TouchShrine(cell); break;
                case FieldTile.GlowStone: TouchGlowStone(cell); break;
            }
        }

        // 지나갈 수 있게 된 칸 — [확인]할 것도 없다: 쓰러뜨린 보스 자리(사전은 쉼터 받침대로 옮겨짐, #45), 연 보스 방 문(#76)
        private bool IsClearedBossSpot(Vector2Int cell) => IsCleared(area, cell);

        private bool IsCleared(FieldArea fieldArea, Vector2Int cell) => CellState.IsPassable(fieldArea, session, cell);

        // 한 칸 도착. 출입구·조우로 화면이 바뀌기 시작하면 false (계속 걷지 않음)
        private bool OnStepFinished()
        {
            session.World.SetPosition(area.AreaId, walker.Position, walker.Rafting);
            stepsTaken++;
            minimap.SetPlayer(walker.Position);
            RefreshNameTags();
            if (session.World.Reveal(area.AreaId, walker.Map.Width, walker.Map.Height, walker.Position) > 0)
            {
                minimap.Redraw();
                RefreshDarkness();
            }
            MaybeSayGrassLine();
            var tile = walker.Map.Get(walker.Position);
            if (tile == FieldTile.Dock && showTutorials && !session.Tutorials.Has(TutorialProgress.Raft) && !bubble.IsVisible)
            {
                bubble.Say("뗏목이 묶여 있다.\n물 쪽으로 가면 탈 수 있을 것 같다", 3.5f);
                bubbleKeepUntil = Time.unscaledTime + 1.5f;
            }
            if (tile == FieldTile.Door)
            {
                var exit = area.GetExit(walker.Position);
                if (exit != null && exit.Target != null) StartCoroutine(UseDoor(exit));
                else ShowToast("문이 굳게 닫혀 있다.");
                return false;
            }
            // 풀숲 조우 — 보이는 몬스터 지역도 (#123). 단, 보이는 몬스터가 나를 쫓는 동안은 없음 (걸음 수도 안 셈)
            bool grass = tile == FieldTile.Grass && (roaming == null || !roaming.HuntingPlayer);
            if (!fieldTutorialRunning && encounterCounter.OnStep(grass, rng))
            {
                StartCoroutine(Encounter());
                return false;
            }
            return true;
        }

        // 출입구: 화면을 어둡게 → 도착 지역의 해당 출입구 칸에 나타남 → 밝게.
        // 도착은 '걸음'이 아니라서 바로 되돌아가지 않는다 (한 칸 벗어났다 다시 밟아야 이동)
        private IEnumerator UseDoor(AreaExit exit)
        {
            var target = exit.Target;
            var doors = target.Map.Doors;
            if (exit.TargetDoorIndex < 0 || exit.TargetDoorIndex >= doors.Count)
            {
                Debug.LogError($"[FieldScreen] {area.name}의 출입구가 {target.name}의 {exit.TargetDoorIndex}번 D를 가리키지만 없음");
                ShowToast("문이 굳게 닫혀 있다.");
                yield break;
            }

            transitioning = true;
            Sound.Play(Sfx.Door);
            flash.gameObject.SetActive(true);
            float half = 0.25f * animationScale;
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(0, 0, 0, t / half);
                yield return null;
            }

            EnterArea(target, doors[exit.TargetDoorIndex]);
            SaveNow();

            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(0, 0, 0, 1f - t / half);
                yield return null;
            }
            flash.gameObject.SetActive(false);
            transitioning = false;
            ShowToast(area.DisplayName, 1.5f);
        }

        private IEnumerator WaitUnscaled(float seconds)
        {
            float end = Time.unscaledTime + seconds * animationScale;
            while (Time.unscaledTime < end) yield return null;
        }

        // 가는 방향 + 달리기: 스틱(끝까지 밀면 달리기)이 먼저, 없으면 키보드(방향키/WASD, Shift = 달리기)
        private (Direction? direction, bool run) ReadMove()
        {
            if (stick.IsHeld)
            {
                stickDirection = StickInput.ToDirection(stick.Value, stickDirection);
                return (stickDirection, StickInput.IsRunning(stick.Value));
            }
            stickDirection = null;

            var keyboard = Keyboard.current;
            if (keyboard == null) return (null, false);
            bool run = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) return (Direction.Up, run);
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) return (Direction.Down, run);
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) return (Direction.Left, run);
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) return (Direction.Right, run);
            return (null, false);
        }

        // ------------------------------------------------------------------ 월드(타일맵·플레이어·카메라)

        private static Vector3 CellCenter(Vector2Int cell) => new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        private void SnapPlayer()
        {
            moving = false;
            player.position = CellCenter(walker.Position);
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            minimap?.SetPlayer(walker.Position);
        }

        // 지역을 바꿔 그린다 (처음 시작할 때, 출입구를 지날 때)
        // rafting: 뗏목에 탄 채로 (세이브가 강 위였을 때, #80)
        private void EnterArea(FieldArea newArea, Vector2Int position, bool rafting = false)
        {
            area = newArea;
            var map = area.Map;
            walker = new FieldWalker(map, position, cell => IsDoorLocked(cell) || IsPaywalled(cell), cell => IsCleared(area, cell), rafting);
            encounterCounter = new EncounterCounter(area.EncounterRate, area.MinStepsBetweenEncounters);
            session.World.SetPosition(area.AreaId, position, rafting);
            SetUpRoamers(position);

            DrawTiles(area, IsDoorLocked);
            streams.SetMap(map);
            scatter.Build(map, area.Theme);
            fog.Build(map, area.Theme);
            RefreshMooredRafts();
            if (!inBattle) Sound.PlayMusic(AreaMusic);
            session.World.Reveal(area.AreaId, map.Width, map.Height, position);
            BuildDarkness();
            minimap.SetArea(map, area.Theme, IsCellDone, cell => session.World.IsExplored(area.AreaId, cell));
            nameTags.SetArea(area, IsDoorLocked);
            RefreshNameTags();
            SnapPlayer();
            UpdateCamera();
            RefreshHud();
        }

        private void UpdateCamera()
        {
            if (cam == null) return;
            float visibleHeight = tilesAcross / Mathf.Max(0.1f, cam.aspect);
            cam.orthographicSize = visibleHeight / 2f;
            // 아래쪽은 가상 패드가 가리므로 플레이어가 화면 높이 60% 지점에 오게 카메라를 내린다 (연출 중에는 그곳이 가운데)
            var target = cameraFocus ?? player.position + Vector3.down * (visibleHeight * 0.10f);
            cam.transform.position = new Vector3(target.x, target.y, -10f);
        }

        // 동료 그림은 보이기만 한다 — 순간이동 뒤 '주인공 뒤 한 칸'이 벽이면 주인공 자리에서 나오게 할 때만 칸을 본다
        private bool CanFollowerStand(Vector3 world) =>
            walker != null && walker.CanStand(new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y)));

        // 주인공 이동이 끝난 뒤 같은 프레임에 동료가 자취를 따라온다 (#78)
        private void LateUpdate()
        {
            if (follower == null || walker == null || session == null) return;
            follower.SetCompanion(session.PartyCompanion?.Data, player.position, walker.Facing, CanFollowerStand); // 바뀌었을 때만
            if (walker.Rafting)
            {
                UpdateRaft();
                var offset = (Vector3)(Vector2)walker.Facing.ToOffset();
                follower.Ride(raftRenderer.transform.position - offset * 0.5f + new Vector3(0f, 0.28f, 0f), player.position, walker.Facing);
            }
            else follower.Tick(player.position, walker.Facing, CanFollowerStand);
        }

        // 자동 저장 + (필드에서) 잠깐 '✓ 저장'. 자주 저장되어도 SaveMarkGap보다 자주 띄우지 않는다
        private void SaveNow()
        {
            saveProgress?.Invoke();
            if (saveProgress == null || inBattle || saveMark == null || Time.unscaledTime - lastSaveMark < SaveMarkGap) return;
            lastSaveMark = Time.unscaledTime;
            saveMarkUntil = Time.unscaledTime + 1.2f;
            saveMark.SetActive(true);
            SaveMarksShown++;
        }

        private void BuildBattle()
        {
            var battleGo = new GameObject("Battle");
            battleGo.transform.SetParent(transform, false);
            battle = battleGo.AddComponent<BattleScreen>();
            if (showTutorials) battle.SetGuide(guide); // 첫 전투 안내 (기술·새 단어·첫 문제·강도·상처약 — 한 줄씩)
            battle.Configure(area.Encounters, area.Words, session, animationScale, battleConfig, saveProgress, loop: false,
                gameDatabase: database);
        }

        // 메뉴는 걷는 중·전투 중·다른 창이 열려 있을 때는 열지 않는다
        private bool CanOpenMenu => !(inBattle || transitioning || moving || IsPanelOpen);

        // 미니맵에서 '끝난 곳' (연 상자, 쓰러뜨린 보스)
        private bool IsCellDone(Vector2Int cell) => CellState.IsDone(area, session, cell);
    }
}
