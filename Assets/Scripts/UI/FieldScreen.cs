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
    public class FieldScreen : MonoBehaviour
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
        private BattleScreen battle;
        private bool bossBattle;

        private RectTransform hudRoot;
        private Canvas hudCanvas;
        private FieldNameTags nameTags;
        private Image confirmImage, confirmGlow, confirmRing;
        private Text confirmLabel;
        private Text areaLabel, dexLabel, toastText;
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
        // 보이는 몬스터 조우 (#73, 사막부터 — GDD 9-5): 규칙 = RoamingMonsters, 그림 = FieldRoamerViews, [선제공격] 문제 = PreemptView
        private RoamingMonsters roaming;
        private FieldRoamerViews roamerViews;
        private PreemptView preemptView;    // QTE (#74): 집중선·줄어드는 원·2×2 선택지
        private GameObject preemptPrompt;   // 둥근 [선제공격!] 버튼 묶음 (Figma 'Preempt Prompt')
        private Button preemptButton;
        private UiRing preemptRing;         // 버튼 둘레에서 줄어드는 금색 원 = 남은 기회 (QTE)
        private float preemptWindow;        // 버튼이 뜬 뒤 지난 시간 (기회 슬로모션)
        private TargetLock preemptLock;     // 선제공격 거리의 몬스터에 빨간 조준 괄호
        private RectTransform markLayer;    // 몬스터 위치에 붙는 HUD 표시 층 (PreemptView와 같은 좌표)
        private PauseTutorial pauseTutorial; // 처음 [선제공격] 기회에 화면을 멈추고 설명 (#79)
        private UiRing preemptRange;        // 튜토리얼 2단계: 몬스터 둘레 '선제공격 범위' 점선
        private bool preemptTutorialRunning;
        private Roamer battleRoamer; // 지금 싸우는 보이는 몬스터 (이기면 사라지고, 도망치면 잠깐 멈춤)
        private List<IReadOnlyList<WordEntry>> preemptBooks = new List<IReadOnlyList<WordEntry>>(); // 선제공격 문제 = 이미 본 단어만
        private const float PreemptSlow = 0.15f;        // QTE 문제를 푸는 동안 몬스터 속도
        private const float PreemptChanceSlow = 0.35f;  // 버튼이 막 떴을 때 (기회 슬로모션 — 누를 틈)
        private const float PreemptWindowSeconds = 1.5f;
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
                                   || (pauseTutorial != null && pauseTutorial.IsShowing);
        public PauseTutorial PauseTutorial => pauseTutorial;
        public bool IsPreemptRangeVisible => preemptRange != null && preemptRange.gameObject.activeSelf;
        public CompanionCutscene CompanionCutscene => companionCutscene;
        public FieldFollower Follower => follower;
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
            IStore purchaseStore = null, Entitlements owned = null)
        {
            area = fieldArea;
            showTutorials = tutorials;
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

            CreateWorldObjects();
            BuildHud();
            BuildBattle();

            // 저장된 위치가 이 지역의 걸을 수 있는 칸이면 거기서, 아니면 시작 위치에서
            var spawn = session.World.TryGetPosition(area.AreaId, out var saved) && map.IsWalkable(saved) ? saved : map.Start;
            EnterArea(area, spawn);

            if (!string.IsNullOrEmpty(statusMessage)) ShowToast(statusMessage, 3f);
            if (showTutorials && !session.Tutorials.Has(TutorialProgress.Field)) StartCoroutine(FieldGuide());
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
                playerRenderer.sprite = running ? PlayerArt.GetRun(walker.Facing, frame) : PlayerArt.Get(walker.Facing, frame);
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

        // 정식판이 필요한 지역(숲)으로 가는 출입구인데 아직 안 샀는지 (#40)
        private bool IsPaywalled(Vector2Int cell)
        {
            if (entitlements == null || entitlements.HasFullVersion) return false;
            var target = area.GetExit(cell)?.Target;
            return target != null && target.RequiresFullVersion;
        }

        // 정식판 안내 (Figma '필드 — 정식판 안내 (#40)'). 사면 바로 그 출입구로 들어갈 수 있다
        private void ShowPaywall(Vector2Int cell)
        {
            paywallArmed = false;
            var target = area.GetExit(cell).Target;
            paywall.Show(store, entitlements, OfferFor(target), () =>
                ShowToast($"정식판이 열렸어요! 이제 {UiKit.WithJosa(target.DisplayName, "으로", "로")} 들어갈 수 있어요.", 3f));
        }

        // 설정 > 정식판 (#63): 정식판이 필요한 지역(지금은 숲)의 안내를 어디서든 띄운다
        private void OpenFullVersionOffer(FieldArea target) =>
            paywall.Show(store, entitlements, OfferFor(target), () =>
                ShowToast($"정식판이 열렸어요! 이제 {UiKit.WithJosa(target.DisplayName, "으로", "로")} 들어갈 수 있어요.", 3f));

        // 정식판이 필요한 지역: 게임 데이터에서 (없으면 지금 지역의 출입구가 이어지는 곳에서)
        private FieldArea FullVersionArea()
        {
            if (database != null)
                foreach (var candidate in database.Areas)
                    if (candidate != null && candidate.RequiresFullVersion) return candidate;
            foreach (var exit in area.Exits)
                if (exit.Target != null && exit.Target.RequiresFullVersion) return exit.Target;
            return null;
        }

        // 안내 문구는 그 지역 데이터에서: 단어 수 · 몬스터·보스 · 성유물(상자·보스 보상)
        private static PaywallOffer OfferFor(FieldArea target)
        {
            var offer = new PaywallOffer { AreaName = target.DisplayName };
            if (target.Words != null && target.Words.Words.Count > 0)
                offer.Lines.Add($"{target.DisplayName} 지역 — 새 영단어 {target.Words.Words.Count}개");
            var species = new HashSet<MonsterSpecies>();
            if (target.Encounters != null)
                foreach (var entry in target.Encounters.Entries)
                    if (entry.Species != null) species.Add(entry.Species);
            var boss = target.Boss;
            offer.Lines.Add(boss != null ? $"새 몬스터 {species.Count}종과 보스 '{boss.Species.DisplayName}'" : $"새 몬스터 {species.Count}종");
            var relics = new List<RelicData>();
            foreach (var chest in target.ChestContents)
                if (chest.Relic != null) relics.Add(chest.Relic);
            if (boss?.RewardRelic != null) relics.Add(boss.RewardRelic);
            if (relics.Count > 0)
                offer.Lines.Add($"{target.DisplayName}의 성유물 {relics.Count}개 ({string.Join("·", relics.ConvertAll(r => r.DisplayName))})");
            offer.Lines.Add("앞으로 추가될 지역도 모두");
            foreach (var relic in relics) offer.Icons.Add(UiKit.RelicIcon(relic));
            return offer;
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
            if (transitioning || preemptView.IsOpen) return;
            if (quitDialog.IsOpen) quitDialog.Hide();
            else if (returnDialog.IsOpen) returnDialog.Hide(); // = 여기 있기
            else if (learnView.IsOpen) learnView.Cancel();
            else if (inventoryView.LearnView.IsOpen) inventoryView.LearnView.Cancel();
            else if (altarView.IsOpen)
            {
                if (!altarView.Cutscene.IsPlaying) altarView.Hide(); // 각성 연출 중에는 끝까지 보기
            }
            else if (inventoryView.IsOpen) inventoryView.Hide();
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
            }
        }

        // 지나갈 수 있게 된 칸 — [확인]할 것도 없다: 쓰러뜨린 보스 자리(사전은 쉼터 받침대로 옮겨짐, #45), 연 보스 방 문(#76)
        private bool IsClearedBossSpot(Vector2Int cell) => IsCleared(area, cell);

        private bool IsCleared(FieldArea fieldArea, Vector2Int cell)
        {
            switch (fieldArea.Map.Get(cell))
            {
                case FieldTile.Boss: return session.World.IsBossDefeated(fieldArea.BossId);
                case FieldTile.Gate: return BossGate.IsOpen(fieldArea, session);
                default: return false;
            }
        }

        // 보스 방 문 (#76, 사용자가 정한 말): 열쇠가 없으면 / 단어가 모자라면 주인공 혼잣말, 다 갖췄으면 문이 열린다
        private void KnockGate()
        {
            switch (BossGate.State(area, session))
            {
                case BossGateState.NeedKey:
                    bubble.Say("열쇠가 필요한 것 같다", 3f);
                    break;
                case BossGateState.NeedWords:
                    bubble.Say("보스를 상대하려면 새로운 단어로 힘을 모아야해", 4f,
                        $"이 지역에서 발견한 단어 {BossGate.WordsFound(area, session)} / {area.BossGateWords}");
                    break;
                case BossGateState.Ready:
                    if (!BossGate.TryOpen(area, session)) return;
                    foreach (var gate in BossGate.Cells(area))
                    {
                        tilemap.SetTile(new Vector3Int(gate.x, gate.y, 0), TileFor(FieldTile.Gate, true));
                        StartCoroutine(FieldFx.Play(transform, CellCenter(gate), Fx.Sparkle, 1.3f, animationScale, 12));
                    }
                    Sound.PlayJingle(Sfx.GateOpen);
                    ShowToast("보스 방 문이 열렸다!", 2.5f);
                    AfterFieldChange();
                    break;
            }
        }

        // 열쇠 받침대 'K' (#76): 처음 [확인]하면 열쇠를 얻고 빈 받침대가 된다
        private void TakeKey(Vector2Int cell)
        {
            if (!BossGate.TakeKey(area, session))
            {
                ShowToast("빈 받침대다.");
                return;
            }
            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.Key, true));
            Sound.Play(Sfx.Evolve);
            ShowToast($"{UiKit.WithJosa(area.BossKey.DisplayName, "을", "를")} 찾았다!\n보스 방 문을 열 수 있을 것 같다", 3f);
            AfterFieldChange();
        }

        // 문·열쇠처럼 맵 상태가 바뀐 뒤: 미니맵·이름표·목표 한 줄·메뉴 점을 다시 그리고 저장
        private void AfterFieldChange()
        {
            minimap.Redraw();
            RefreshNameTags();
            RefreshObjective();
            RefreshBadges();
            SaveNow();
            RefreshHud();
        }

        // 사전 받침대 (#45): 보스를 물리쳤으면 하루 한 번 읽고 이 지역 단어 하나를 발견.
        // 창 없이 주인공 머리 위 말풍선으로 (#46, 사용자 요청)
        private void ReadDictionary()
        {
            var now = LocalClock();
            var read = BossDictionary.Read(area, session, now, now.ToUniversalTime(), rng);
            switch (read.Outcome)
            {
                case DictionaryReadOutcome.Locked:
                    string boss = area.Boss != null ? UiKit.WithJosa(area.Boss.Species.DisplayName, "을", "를") : "보스를";
                    bubble.Say($"빈 받침대다.\n{boss} 물리치면 무언가 놓일 것 같다.", 4f);
                    break;
                case DictionaryReadOutcome.AlreadyReadToday:
                    bubble.Say("오늘 책은 충분히 읽은 것 같다.", 4f, "내일 다시 읽을 수 있어요 (밤 12시에 바뀜)");
                    break;
                case DictionaryReadOutcome.AllDiscovered:
                    bubble.Say($"{area.DictionaryName}의 단어를 모두 발견했다!", 3f);
                    break;
                case DictionaryReadOutcome.Discovered:
                    StartCoroutine(SpeakDictionaryWord(read.Word));
                    break;
                default:
                    bubble.Say("빈 받침대다.", 3f);
                    break;
            }
        }

        // '.' → '..' → '...' → '!' 풍선 (그동안은 움직일 수 없음) → 새 단어 말풍선 (움직이면 사라짐)
        private IEnumerator SpeakDictionaryWord(WordEntry word)
        {
            IsReadingDictionary = true;
            transitioning = true;
            SaveNow();
            foreach (var step in new[] { SpeechBubble.Icon.Dot1, SpeechBubble.Icon.Dot2, SpeechBubble.Icon.Dot3, SpeechBubble.Icon.Exclaim })
            {
                bubble.ShowIcon(step);
                Sound.Play(Sfx.Click);
                yield return WaitUnscaled(step == SpeechBubble.Icon.Exclaim ? 0.55f : 0.45f);
            }
            var progress = Dex.GetProgress(area.Words, session.Vocabulary);
            Sound.Play(Sfx.NewWord);
            bubble.ShowWord("새로운 단어를 발견했다!", word.English, word.Meaning,
                $"{area.Words.RegionName} 도감 {progress.Discovered} / {progress.Total}", 12f);
            transitioning = false;
            IsReadingDictionary = false;
            var completed = session.ClaimDexRewards(new[] { area.Words });
            if (completed.Count > 0)
            {
                SaveNow();
                ShowToast($"★ {area.Words.RegionName} 도감 완성! 보상을 받았다", 3f);
            }
            RefreshHud();
            interactCooldown = 0.5f;
        }

        private void ChallengeBoss()
        {
            var boss = area.Boss;
            if (boss == null)
            {
                ShowToast("아무도 없다.");
                return;
            }
            string name = boss.Species.DisplayName;
            if (session.World.IsBossDefeated(area.BossId))
            {
                ShowToast($"{UiKit.WithJosa(name, "이", "가")} 있던 자리다.");
                return;
            }
            var enemies = new List<MonsterInstance> { new MonsterInstance(boss.Species, boss.Level) };
            StartCoroutine(Encounter(enemies, $"보스 출현! {name} Lv{boss.Level} — 정답으로 맞서라!", true));
        }

        // 강화·구매·장착 직후 저장하고 HUD(주인공·성유물·골드) 갱신
        private void OnTownChanged()
        {
            SaveNow();
            RefreshHud();
        }

        // 한 칸 도착. 출입구·조우로 화면이 바뀌기 시작하면 false (계속 걷지 않음)
        private bool OnStepFinished()
        {
            session.World.SetPosition(area.AreaId, walker.Position);
            stepsTaken++;
            minimap.SetPlayer(walker.Position);
            RefreshNameTags();
            if (session.World.Reveal(area.AreaId, walker.Map.Width, walker.Map.Height, walker.Position) > 0) minimap.Redraw();
            MaybeSayGrassLine();
            var tile = walker.Map.Get(walker.Position);
            if (tile == FieldTile.Door)
            {
                var exit = area.GetExit(walker.Position);
                if (exit != null && exit.Target != null) StartCoroutine(UseDoor(exit));
                else ShowToast("문이 굳게 닫혀 있다.");
                return false;
            }
            if (!fieldTutorialRunning && encounterCounter.OnStep(tile == FieldTile.Grass && roaming == null, rng))
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

        private void OpenChest(Vector2Int cell)
        {
            var result = session.OpenChest(area, cell);
            if (result.WasEmpty)
            {
                ShowToast("빈 상자다.");
                return;
            }

            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.Chest, true));
            Sound.Play(result.Relic != null ? Sfx.Evolve : Sfx.Coin);
            minimap.Redraw();
            if (result.Relic != null)
            {
                ShowToast($"성유물 '{result.Relic.Data.DisplayName}'을(를) 손에 넣었다!\n" + RelicHint(result.Relic), 3.5f);
            }
            else
            {
                bool document = result.Item != null && result.Item.IsSkillDocument;
                string loot = result.Item != null ? (document ? result.Item.DisplayName : $"{result.Item.DisplayName} x{result.Count}") : "";
                if (result.Gold > 0) loot += (loot.Length > 0 ? " + " : "") + $"{result.Gold} 골드";
                ShowToast($"보물상자를 열었다!\n{loot} 획득", 2.5f);
            }
            SaveNow();
            RefreshHud();
            if (result.Item != null && result.Item.IsSkillDocument) OfferDocument(result.Item);
        }

        // 기술문서를 얻으면 바로 배울지 묻는다 (안 배워도 가방 > 아이템에서 언제든)
        private void OfferDocument(ItemData document)
        {
            if (document == null || !document.IsSkillDocument || session.Hero.Knows(document)) return;
            learnView.ShowDocument(session, document, learned =>
            {
                OnTownChanged();
                string name = document.TaughtSkill.DisplayName;
                string josa = UiKit.WithJosa(name, "을", "를").Substring(name.Length);
                ShowToast(learned ? $"새 기술 '{name}'{josa} 배웠다!\n전투에서 바로 쓸 수 있어요"
                    : "기술문서는 가방 > 아이템에서\n언제든 배울 수 있어요", 3f);
            });
        }

        private void UseFountain()
        {
            session.RestoreHero();
            SaveNow();
            RefreshHud();
            Sound.Play(Sfx.Fountain);
            ShowToast("회복의 샘 — HP가 모두 회복되었다!");
        }

        // 새 성유물을 얻었을 때 안내: 빈 칸이 있어 바로 끼웠는지, 가방에서 바꿔 끼워야 하는지
        private string RelicHint(OwnedRelic relic) =>
            session.Hero.IsEquipped(relic) ? $"빈 칸에 끼웠다 — 새 기술 '{relic.Skill.DisplayName}'" : "칸이 가득 — 가방 > 성유물에서 바꿔 끼울 수 있어요";

        private IEnumerator Encounter(List<MonsterInstance> enemies = null, string intro = null, bool boss = false)
        {
            transitioning = true;
            Sound.Play(Sfx.Encounter);
            Sound.PlayMusic(boss ? Music.Boss : Music.Battle);
            flash.gameObject.SetActive(true);
            float duration = 0.4f * animationScale;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                flash.color = new Color(1, 1, 1, Mathf.PingPong(k * 3f, 1f) * 0.85f);
                yield return null;
            }
            flash.gameObject.SetActive(false);

            enemies = enemies ?? area.Encounters.Roll(rng);
            transitioning = false;
            inBattle = true;
            bossBattle = boss;
            HideToast();
            battle.BeginBattle(enemies, area.Words, OnBattleFinished, intro, area.Theme, boss);
        }

        private Music AreaMusic
        {
            get
            {
                switch (area.Theme)
                {
                    case FieldTheme.Library: return Music.Library;
                    case FieldTheme.Forest: return Music.Forest;
                    default: return Music.Meadow;
                }
            }
        }

        // 이기면 보상(보스면 길 열림 등), 지면 시작 지점에서 다시 일어남, 도망치면 (#53) 그 자리 그대로
        private void OnBattleFinished(BattlePhase outcome)
        {
            bool won = outcome == BattlePhase.Victory;
            inBattle = false;
            var foughtRoamer = battleRoamer;
            battleRoamer = null;
            if (foughtRoamer != null && roaming != null)
            {
                if (won)
                {
                    roaming.Defeat(foughtRoamer);
                    roamerViews.Remove(foughtRoamer);
                }
                else roaming.Stun(foughtRoamer); // 도망쳤거나 져서 시작 지점으로 — 잠깐 멈춰 거리를 벌림
            }
            RefreshPreemptBooks();
            Sound.PlayMusic(AreaMusic);
            encounterCounter.Reset();
            bool wasBoss = bossBattle;
            bossBattle = false;

            if (won && wasBoss)
            {
                session.World.MarkBossDefeated(area.BossId);
                var bossCell = walker.Map.BossPosition.Value;
                tilemap.SetTile(new Vector3Int(bossCell.x, bossCell.y, 0), TileFor(FieldTile.Boss, true));
                var lectern = walker.Map.Find(FieldTile.Lectern);
                if (lectern.HasValue && area.HasDictionary)
                    tilemap.SetTile(new Vector3Int(lectern.Value.x, lectern.Value.y, 0), TileFor(FieldTile.Lectern, true));
                minimap.Redraw();
                RefreshNameTags();
                string name = area.Boss.Species.DisplayName;
                var relic = session.GrantRelic(area.Boss.RewardRelic);
                var rewardItem = area.Boss.RewardItem; // 기술문서 등
                if (rewardItem != null) session.Inventory.Add(rewardItem);
                SaveNow();
                string message = relic != null
                    ? $"★ {UiKit.WithJosa(name, "을", "를")} 물리쳤다!\n성유물 '{relic.Data.DisplayName}' 획득 — {RelicHint(relic)}"
                    : $"★ {UiKit.WithJosa(name, "을", "를")} 물리쳤다!\n{area.DisplayName}에 잊혀진 기억이 돌아왔다";
                if (rewardItem != null) message += $"\n{rewardItem.DisplayName} 획득!";
                if (area.HasDictionary) message += $"\n쉼터 받침대에 {UiKit.WithJosa(area.DictionaryName, "이", "가")} 놓였다";
                // 이 보스가 여는 출입구가 있으면 카메라가 가서 보여 준다 → 기술 배우기 → 시작 지점으로 돌아갈지 묻기
                var gates = database != null ? FieldArea.GatesOpenedBy(area, database.Areas) : new List<AreaGate>();
                StartCoroutine(AfterBossVictory(message, gates, rewardItem != null && rewardItem.IsSkillDocument ? rewardItem : null));
            }
            else if (outcome == BattlePhase.Defeat)
            {
                // 패배: 전투 화면이 이미 주인공을 회복시켰다. 이 지역의 시작 위치로 돌아간다
                walker.WarpTo(walker.Map.Start);
                SnapPlayer();
                RefreshNameTags();
                session.World.SetPosition(area.AreaId, walker.Position);
                SaveNow();
                StartCoroutine(Respawn($"{area.DisplayName} 시작 지점으로 돌아왔다. HP가 회복되었다!"));
            }
            RefreshHud();
        }

        // 보스를 이긴 뒤 (#60 사용자 요청): 승리 알림(또는 길 열림 연출) → 기술문서를 받았으면 배우기 →
        // "시작 지점으로 돌아갈까요?" — 보스 방은 대개 지역 맨 끝이라 걸어서 돌아가는 수고를 덜어 준다
        private IEnumerator AfterBossVictory(string message, List<AreaGate> gates, ItemData document)
        {
            var bossArea = area;
            if (gates.Count > 0) yield return RevealGates(gates, message);
            else
            {
                ShowToast(message, 4.5f);
                yield return WaitUnscaled(2.5f); // 받은 성유물·사전 소식을 먼저 읽게
            }
            if (document != null && !session.Hero.Knows(document))
            {
                OfferDocument(document);
                while (learnView.IsOpen) yield return null;
            }
            while (inBattle || transitioning || IsPanelOpen) yield return null;
            if (area != bossArea || walker.Position == walker.Map.Start) yield break; // 이미 다른 지역이나 시작 지점이면 묻지 않음
            returnDialog.Show(BossDefeatedTitle, $"{area.DisplayName} 시작 지점으로 돌아갈까요?", "돌아가기", false,
                () => StartCoroutine(ReturnToStart()), "star_full", "여기 있기");
        }

        private const string BossDefeatedTitle = "보스를 물리쳤다!";

        // 시작 지점으로: 어두워짐 + 문 소리 → 시작 지점에 정면으로 서서 밝아짐
        private IEnumerator ReturnToStart()
        {
            transitioning = true;
            ReturnsToStart++;
            Sound.Play(Sfx.Door);
            bubble.Hide();
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(0f, 1f, 0.35f * animationScale);
            walker.WarpTo(walker.Map.Start);
            walker.Face(Direction.Down);
            SnapPlayer();
            if (session.World.Reveal(area.AreaId, walker.Map.Width, walker.Map.Height, walker.Position) > 0) minimap.Redraw();
            RefreshNameTags();
            session.World.SetPosition(area.AreaId, walker.Position);
            SaveNow();
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.35f * animationScale);
            Destroy(cutscene.gameObject);
            transitioning = false;
            ShowToast($"{area.DisplayName} 시작 지점으로 돌아왔다");
        }

        // 지고 나서 시작 지점에서 다시 일어나는 연출: 어둠이 걷힘 → 쓰러진 주인공이 발밑 빛 고리 속에 나타남 + 소리 →
        // 반짝이와 함께 쓰러짐 → 웅크림 → 서기 (팩의 '쓰러짐' 그림을 거꾸로 — #51 사용자 요청). 빛 효과는 #43 그대로
        private IEnumerator Respawn(string message)
        {
            transitioning = true;
            RespawnsPlayed++;
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(1f, 1f, 0f);
            var home = player.position;
            walker.Face(Direction.Down);
            playerRenderer.sprite = PlayerArt.Pose(PlayerPose.Dead);
            playerRenderer.color = new Color(1f, 1f, 1f, 0f);
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.5f * animationScale);

            // 빛 고리 속에 쓰러진 채로 나타남
            Sound.Play(Sfx.Respawn);
            // 빛 고리는 주인공 뒤에 (쓰러진 모습이 빛에 가리지 않게), 반짝이는 앞에
            StartCoroutine(FieldFx.Play(transform, home, Fx.Heal, 2.6f, animationScale * 1.5f, 9));
            float appear = 0.45f * animationScale;
            for (float t = 0f; t < appear; t += Time.unscaledDeltaTime)
            {
                playerRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / Mathf.Max(0.0001f, appear)));
                yield return null;
            }
            playerRenderer.color = Color.white;
            StartCoroutine(FieldFx.Play(transform, home + Vector3.up * 0.35f, Fx.Sparkle, 1.9f, animationScale * 1.3f, 12));
            yield return WaitUnscaled(0.35f);

            // 거꾸로 돌린 쓰러짐: 웅크림 → 서기
            playerRenderer.sprite = PlayerArt.Pose(PlayerPose.Crouch);
            SawRespawnPose = true;
            yield return WaitUnscaled(0.25f);
            playerRenderer.sprite = PlayerArt.Get(Direction.Down, 0);
            player.position = home;
            yield return WaitUnscaled(0.35f);
            Destroy(cutscene.gameObject);
            transitioning = false;
            ShowToast(message);
        }

        // ------------------------------------------------------------------ 길 열림 연출

        // 승리 알림을 잠깐 보여 준 뒤, 열린 출입구마다: 어두워짐 → 그 지역의 출입구 앞으로 카메라 이동 →
        // 막힌 덤불이 흔들리며 사라지고 '열렸다!' → 어두워짐 → 원래 자리로
        private IEnumerator RevealGates(List<AreaGate> gates, string victoryMessage)
        {
            transitioning = true;
            revealingGate = true;
            ShowToast(victoryMessage, 30f);
            yield return WaitUnscaled(2.4f);
            HideToast();

            foreach (var gate in gates) yield return RevealGate(gate);

            revealingGate = false;
            transitioning = false;
            var last = gates[gates.Count - 1];
            string target = last.Exit.Target != null ? last.Exit.Target.DisplayName : "새 지역";
            ShowToast($"{last.Area.DisplayName}에서 {UiKit.WithJosa(target, "으로", "로")} 가는 길이 열렸다!", 4f);
        }

        private IEnumerator RevealGate(AreaGate gate)
        {
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(0f, 1f, 0.35f * animationScale);

            // 그 지역을 그려 두고 (이 출입구만 아직 잠긴 모습으로) 카메라를 출입구 조금 앞에서 출발
            hudCanvas.enabled = false;
            player.gameObject.SetActive(false);
            DrawTiles(gate.Area, cell => cell == gate.Cell || IsDoorLocked(gate.Area, cell));
            var gateCenter = CellCenter(gate.Cell);
            var approach = ApproachDirection(gate.Area.Map, gate.Cell);
            // 출입구는 대개 맵 끝에 있으므로 카메라는 출입구보다 몇 칸 안쪽을 비춘다 (맵 밖 빈 곳이 덜 보이게)
            var focus = gateCenter - approach * 3f;
            cameraFocus = focus - approach * 4f;
            cutscene.ShowLetterbox(gate.Area.DisplayName);
            yield return cutscene.Fade(1f, 0f, 0.35f * animationScale);

            // 카메라가 천천히 출입구로
            float pan = 1.2f * animationScale;
            var from = cameraFocus.Value;
            for (float t = 0; t < pan; t += Time.unscaledDeltaTime)
            {
                cameraFocus = Vector3.Lerp(from, focus, Mathf.SmoothStep(0f, 1f, t / pan));
                yield return null;
            }
            cameraFocus = focus;
            yield return WaitUnscaled(0.4f);

            // 덤불이 흔들리다가 연기와 함께 사라짐 → 열린 출입구
            Sound.PlayJingle(Sfx.GateOpen);
            float shake = 0.6f * animationScale;
            for (float t = 0; t < shake; t += Time.unscaledDeltaTime)
            {
                cameraFocus = focus + new Vector3(Mathf.Sin(t * 70f), Mathf.Cos(t * 55f), 0f) * 0.08f;
                yield return null;
            }
            cameraFocus = focus;
            UpdateCamera();
            StartCoroutine(cutscene.PlaySmoke(cam, gateCenter, animationScale));
            tilemap.SetTile(new Vector3Int(gate.Cell.x, gate.Cell.y, 0), DoorTile(gate.Area, gate.Cell, false));
            string target = gate.Exit.Target != null ? gate.Exit.Target.DisplayName : "새 지역";
            cutscene.ShowBanner($"{UiKit.WithJosa(target, "으로", "로")} 가는 길이 열렸다!");
            lastGateBanner = cutscene.BannerText;
            lastGateCamera = cam.transform.position;
            revealedGates.Add(gate);
            yield return WaitUnscaled(2.2f);

            // 원래 자리로
            yield return cutscene.Fade(0f, 1f, 0.35f * animationScale);
            cameraFocus = null;
            cutscene.HideLetterbox();
            DrawTiles(area, IsDoorLocked);
            nameTags.SetArea(area, IsDoorLocked);
            RefreshNameTags();
            player.gameObject.SetActive(true);
            hudCanvas.enabled = true;
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.35f * animationScale);
            Destroy(cutscene.gameObject);
        }

        // 출입구로 걸어 들어가는 방향 (출입구 옆 걸을 수 있는 칸 → 출입구). 카메라가 그 길을 따라 다가간다
        private static Vector3 ApproachDirection(FieldMap map, Vector2Int door)
        {
            foreach (var direction in new[] { Direction.Left, Direction.Down, Direction.Right, Direction.Up })
            {
                var from = door - direction.ToOffset();
                if (map.IsWalkable(from)) return new Vector3(direction.ToOffset().x, direction.ToOffset().y, 0f);
            }
            return Vector3.up;
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
        private void EnterArea(FieldArea newArea, Vector2Int position)
        {
            area = newArea;
            var map = area.Map;
            walker = new FieldWalker(map, position, cell => IsDoorLocked(cell) || IsPaywalled(cell), cell => IsCleared(area, cell));
            encounterCounter = new EncounterCounter(area.EncounterRate, area.MinStepsBetweenEncounters);
            session.World.SetPosition(area.AreaId, position);
            SetUpRoamers(position);

            DrawTiles(area, IsDoorLocked);
            if (!inBattle) Sound.PlayMusic(AreaMusic);
            session.World.Reveal(area.AreaId, map.Width, map.Height, position);
            minimap.SetArea(map, area.Theme, IsCellDone, cell => session.World.IsExplored(area.AreaId, cell));
            nameTags.SetArea(area, IsDoorLocked);
            RefreshNameTags();
            SnapPlayer();
            UpdateCamera();
            RefreshHud();
        }

        // 지역 타일 전체를 그린다 (연 상자·쓰러뜨린 보스·잠긴 출입구 반영). 길 열림 연출에서는 다른 지역도 그린다
        private void DrawTiles(FieldArea fieldArea, Func<Vector2Int, bool> lockedDoor)
        {
            var map = fieldArea.Map;
            tilemap.ClearAllTiles();
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var cell = new Vector2Int(x, y);
                var kind = map.Get(cell);
                bool done = kind == FieldTile.Chest && session.World.IsChestOpened(fieldArea.ChestId(cell))
                            || kind == FieldTile.Boss && session.World.IsBossDefeated(fieldArea.BossId)
                            || kind == FieldTile.Lectern && BossDictionary.IsUnlocked(fieldArea, session.World)
                            || kind == FieldTile.Gate && BossGate.IsOpen(fieldArea, session)
                            || kind == FieldTile.Key && BossGate.HasKey(fieldArea, session);
                Tile tile;
                if (kind == FieldTile.Door) tile = DoorTile(fieldArea, cell, lockedDoor(cell));
                else if (FieldAutotile.IsAutotiled(kind)) tile = AutoTile(fieldArea.Theme, kind, FieldAutotile.Mask(map, cell)) ?? TileFor(kind, done, fieldArea.Theme);
                else tile = TileFor(kind, done, fieldArea.Theme);
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
            cam.backgroundColor = PlaceholderArt.OutsideColor(fieldArea.Theme);
        }

        private Tile TileFor(FieldTile kind, bool done, FieldTheme? theme = null)
        {
            var t = theme ?? area.Theme;
            string key = $"{t}_{kind}_{done}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                tile = MakeTile(FieldArt.ForTile(kind, t, done));
                tileCache[key] = tile;
            }
            return tile;
        }

        // 길·물가 자동 테두리 (그림이 없는 테마면 null → 한 칸 그림)
        private Tile AutoTile(FieldTheme theme, FieldTile kind, int mask)
        {
            string key = $"{theme}_{kind}_auto_{mask}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                var sprite = FieldArt.AutoTile(kind, theme, mask);
                tile = sprite != null ? MakeTile(sprite) : null;
                tileCache[key] = tile;
            }
            return tile;
        }

        // 출입구 그림: 잠김 / 도착 지역 테마 전용(예: 초원의 숲길 입구) / 기본
        private Tile DoorTile(FieldArea fieldArea, Vector2Int cell, bool locked)
        {
            var exit = fieldArea.GetExit(cell);
            FieldTheme? target = exit != null && exit.Target != null ? exit.Target.Theme : (FieldTheme?)null;
            string key = $"{fieldArea.Theme}_Door_{target}_{locked}";
            if (!tileCache.TryGetValue(key, out var tile))
            {
                tile = MakeTile(FieldArt.ForDoor(fieldArea.Theme, target, locked));
                tileCache[key] = tile;
            }
            return tile;
        }

        private void CreateWorldObjects()
        {
            var gridGo = new GameObject("FieldGrid", typeof(Grid));
            gridGo.transform.SetParent(transform, false);
            var tilemapGo = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer));
            tilemapGo.transform.SetParent(gridGo.transform, false);
            tilemap = tilemapGo.GetComponent<Tilemap>();

            var playerGo = new GameObject("Player", typeof(SpriteRenderer));
            playerGo.transform.SetParent(transform, false);
            player = playerGo.transform;
            playerRenderer = playerGo.GetComponent<SpriteRenderer>();
            playerRenderer.sortingOrder = 10;
            follower = new FieldFollower(transform);
            roamerViews = new FieldRoamerViews(transform, CellCenter);

            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                camGo.transform.SetParent(transform, false); // 직접 만든 카메라는 필드와 함께 정리
                cam = camGo.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private static Tile MakeTile(Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            return tile;
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

        // ------------------------------------------------------------------ HUD

        private void BuildHud()
        {
            UiKit.EnsureEventSystem();
            var canvasGo = new GameObject("FieldHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            hudCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            hudRoot = UiKit.Stretch("SafeArea", canvasGo.transform);
            UiKit.ApplySafeArea(hudRoot);

            // 오브젝트 이름표는 맨 아래 층 (상단 바·패드·창이 그 위를 덮는다)
            nameTags = FieldNameTags.Create(hudRoot);
            bubble = SpeechBubble.Create(hudRoot); // 이름표 위, 상단 바·메뉴·창 아래

            // 상단: 지역 이름 · 도감 진행 · 골드
            var top = UiKit.Panel("TopBar", hudRoot, Palette.Scrim, 0, 0.94f, 1, 1);
            top.raycastTarget = false;
            areaLabel = UiKit.Display(UiKit.Label("Area", top.transform, "", 44, Palette.Gold, 0.03f, 0, 0.4f, 1,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 24));
            dexLabel = UiKit.Label("DexProgress", top.transform, "", 30, Palette.Text, 0.4f, 0, 0.97f, 1,
                TextAnchor.MiddleRight);

            // 왼쪽 위 미니맵 (누르면 큰 지도). 맵 데이터에서 그리므로 지역이 늘어나도 자동
            minimap = MinimapView.Create(hudRoot);
            minimap.Button.onClick.AddListener(OpenMap);

            // 오른쪽 세로 메뉴 (Figma 'Field — 수련 (#44)'): 도감 · 수련 · 가방 · 설정
            var entries = new (string name, string icon, string label, UnityEngine.Events.UnityAction open)[]
            {
                ("DexButton", "dex", "도감", OpenDex),
                ("TrainingButton", "train", "수련", OpenTraining),
                ("BagButton", "bag", "가방", OpenBag),
                ("SettingsButton", "settings", "설정", OpenSettings),
            };
            float menuHeight = entries.Length * 120 + (entries.Length - 1) * 16;
            var menu = UiKit.Rect("Menu", hudRoot, 1, 0.865f, 1, 0.865f);
            menuRect = menu;
            menu.pivot = new Vector2(1, 1);
            menu.sizeDelta = new Vector2(120, menuHeight);
            menu.anchoredPosition = new Vector2(-24, 0);
            var badgeOf = new Dictionary<string, string>
                { ["DexButton"] = MenuBadges.Dex, ["TrainingButton"] = MenuBadges.Training, ["BagButton"] = MenuBadges.Bag };
            for (int i = 0; i < entries.Length; i++)
            {
                float topY = 1f - i * (136f / menuHeight);
                var button = UiKit.IconButton(entries[i].name, menu, entries[i].icon, entries[i].label, Palette.Scrim,
                    0, topY - 120f / menuHeight, 1, topY);
                button.onClick.AddListener(entries[i].open);
                if (badgeOf.TryGetValue(entries[i].name, out var badgeId)) AddBadge(badgeId, (RectTransform)button.transform);
            }
            AddBadge(MenuBadges.Map, (RectTransform)minimap.Button.transform);

            // 목표 한 줄 (#61): 미니맵 바로 아래. 누르면 큰 지도 (목표 칸에 별)
            var goal = UiKit.Pill(UiKit.Panel("Objective", hudRoot, Palette.Scrim, 0, 0.865f, 0, 0.865f));
            objectivePill = goal.rectTransform;
            objectivePill.pivot = new Vector2(0, 1);
            UiKit.AddButton(goal).onClick.AddListener(OpenMap);
            var star = UiKit.Label("Star", objectivePill, "★", 32, Palette.Gold, 0, 0, 0, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            star.rectTransform.pivot = new Vector2(0, 0.5f);
            star.rectTransform.sizeDelta = new Vector2(40, 0);
            star.rectTransform.anchoredPosition = new Vector2(18, 0);
            objectiveText = UiKit.OneLine(UiKit.Label("Text", objectivePill, "", 28, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleLeft,
                FontStyle.Bold));
            objectiveText.rectTransform.offsetMin = new Vector2(62, 0);
            objectiveText.rectTransform.offsetMax = new Vector2(-22, 0);

            // 저장될 때 잠깐 (#61): 메뉴 아래 작은 알약
            var mark = UiKit.Pill(UiKit.Panel("SaveMark", hudRoot, Palette.Scrim, 1, 0.865f, 1, 0.865f));
            mark.raycastTarget = false;
            mark.rectTransform.pivot = new Vector2(1, 1);
            mark.rectTransform.sizeDelta = new Vector2(150, 56);
            mark.rectTransform.anchoredPosition = new Vector2(-24, -(menuHeight + 16));
            UiKit.Label("Text", mark.transform, "✓ 저장", 26, Palette.Good, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            saveMark = mark.gameObject;
            saveMark.SetActive(false);

            // 주인공 배지 + 끼운 성유물 3칸 (Figma 'Field — HUD (주인공·성유물)' / 'Hero Badge' / 'Relic Slot')
            var strip = UiKit.Rect("HeroStrip", hudRoot, 0, 0.875f, 1, 0.935f);
            var badge = UiKit.RoundPanel("HeroBadge", strip, Palette.Scrim, UiKit.RadiusMd, 0.012f, 0, 0.575f, 1);
            heroBadge = badge;
            UiKit.AddButton(badge).onClick.AddListener(() => OpenBag(BagTab.Hero)); // 누르면 가방 > 주인공
            var avatar = UiKit.Pill(UiKit.Panel("Avatar", badge.transform, Palette.PanelLight, 0, 0.5f, 0, 0.5f));
            avatar.raycastTarget = false;
            avatar.rectTransform.pivot = new Vector2(0, 0.5f);
            avatar.rectTransform.sizeDelta = new Vector2(76, 76);
            avatar.rectTransform.anchoredPosition = new Vector2(12, 0);
            UiKit.IconImage("Face", avatar.transform, UiKit.HeroPortrait(session.Hero.Data), 0.1f, 0.1f, 0.9f, 0.9f);
            heroName = UiKit.Label("Name", badge.transform, "", 30, Palette.Text, 0, 0.45f, 0.62f, 0.95f,
                TextAnchor.MiddleLeft, FontStyle.Bold, true, 18);
            heroName.rectTransform.offsetMin = new Vector2(104, 0);
            heroHp = UiKit.OneLine(UiKit.Label("Hp", badge.transform, "", 24, Palette.TextDim, 0.62f, 0.45f, 0.97f, 0.95f,
                TextAnchor.MiddleRight));
            var hpBack = UiKit.Pill(UiKit.Panel("HpBack", badge.transform, Palette.Track, 0, 0.16f, 0.97f, 0.36f));
            hpBack.raycastTarget = false;
            hpBack.rectTransform.offsetMin = new Vector2(104, 0);
            heroHpFill = UiKit.Pill(UiKit.Panel("HpFill", hpBack.transform, Palette.Good));
            heroHpFill.raycastTarget = false;
            BuildCompanionBadge(strip);
            for (int i = 0; i < HeroData.SlotCount; i++)
            {
                var back = UiKit.RoundPanel($"RelicSlot_{i}", strip, Palette.PanelLight, UiKit.RadiusMd, 0, 0.5f, 0, 0.5f);
                back.rectTransform.pivot = new Vector2(0, 0.5f);
                back.rectTransform.sizeDelta = new Vector2(104, 104);
                back.rectTransform.anchoredPosition = new Vector2(640 + i * 116, 0);
                int slot = i;
                UiKit.AddButton(back).onClick.AddListener(() => OpenBag(BagTab.Relics, slot)); // 누르면 가방 > 성유물
                var icon = UiKit.IconImage("Icon", back.transform, null, 0.12f, 0.12f, 0.88f, 0.88f);
                var levelBack = UiKit.Pill(UiKit.Panel("Level", back.transform, Palette.Gold, 1, 0, 1, 0));
                levelBack.raycastTarget = false;
                levelBack.rectTransform.pivot = new Vector2(1, 0);
                levelBack.rectTransform.sizeDelta = new Vector2(52, 30);
                levelBack.rectTransform.anchoredPosition = new Vector2(-4, 4);
                var level = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", levelBack.transform, "", 24, Palette.OnAccent, 0, 0, 1, 1)));
                var empty = UiKit.Label("Empty", back.transform, "빈 칸", 22, Palette.TextDim, 0, 0, 1, 1);
                relicSlots.Add((back, icon, level, empty));
            }

            // 알림
            var toast = UiKit.RoundPanel("Toast", hudRoot, Palette.Scrim, UiKit.RadiusMd, 0.06f, 0.29f, 0.94f, 0.37f);
            toast.raycastTarget = false;
            toastPanel = toast.gameObject;
            toastText = UiKit.Label("Text", toast.transform, "", 34, Palette.Text, 0, 0, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 20);
            UiKit.Pad(toastText.rectTransform, 20, 6, 20, 6);
            toastPanel.SetActive(false);

            // 가상 스틱 (Figma 'Virtual Stick' #34): 오른쪽 아래 아무 곳이나 엄지를 대면 그 자리에 생김, 끝까지 밀면 달리기
            stick = VirtualStick.Create(hudRoot, 0.4f, 0f, 1f, 0.27f);
            // [확인]은 왼쪽 아래로 따로 (스틱을 밀면서 다른 손가락으로 누를 수 있게)
            BuildConfirmButton(UiKit.Rect("ConfirmArea", hudRoot, 0.02f, 0.03f, 0.36f, 0.24f));
            // [선제공격] (#73 → #74 QTE): 쫓아오는 몬스터가 가까우면 몬스터에 빨간 조준 괄호, [확인] 바로 위에 둥근 버튼 (왼손 엄지로)
            markLayer = UiKit.Stretch("RoamerMarks", hudRoot);
            preemptLock = TargetLock.Create(markLayer, Palette.Bad, false);
            preemptLock.SetActive(false);
            preemptRange = UiRing.Create("PreemptRange", markLayer, Palette.Gold, 5f, 100f, 28);
            preemptRange.gameObject.SetActive(false);
            BuildPreemptPrompt(UiKit.Rect("PreemptPrompt", hudRoot, 0.19f, 0.3f, 0.19f, 0.3f));

            // 조우 연출용 번쩍임
            flash = UiKit.Panel("EncounterFlash", hudRoot, Color.clear);
            flash.raycastTarget = false;
            flash.gameObject.SetActive(false);

            dexView = DexView.Create(hudRoot);
            trainingView = TrainingView.Create(hudRoot, StartTraining);
            altarView = RelicAltarView.Create(hudRoot, animationScale);
            shopView = ShopView.Create(hudRoot);
            inventoryView = InventoryView.Create(hudRoot);
            settingsView = SettingsView.Create(hudRoot);
            mapView = MapView.Create(hudRoot);
            learnView = SkillLearnView.Create(hudRoot);
            quitDialog = ConfirmDialog.Create(hudRoot);
            returnDialog = ConfirmDialog.Create(hudRoot);
            paywall = PaywallView.Create(hudRoot);
            companionCutscene = CompanionCutscene.Create(hudRoot);
            pauseTutorial = PauseTutorial.Create(hudRoot);
            preemptView = PreemptView.Create(hudRoot);
            guide = GuideHints.Create(transform); // 자기 캔버스로 HUD·전투 위에 (눌림은 받지 않음)
        }

        // 왼쪽 아래 [확인] (Figma 'Action Button'): 옆에 쓸 것이 있으면 금색(Ready), 없으면 반투명(Idle)
        private void BuildConfirmButton(RectTransform area)
        {
            confirmGlow = UiKit.IconImage("ConfirmGlow", area, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            confirmGlow.rectTransform.sizeDelta = new Vector2(300, 300);
            confirmGlow.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.5f);
            confirmImage = UiKit.Pill(UiKit.Panel("Pad_Confirm", area, Color.white, 0.5f, 0.5f, 0.5f, 0.5f));
            confirmImage.rectTransform.sizeDelta = new Vector2(180, 180);
            UiKit.AddButton(confirmImage).onClick.AddListener(() => confirmRequested = true);
            confirmRing = UiKit.Outline(UiKit.Panel("Ring", confirmImage.transform, new Color(1f, 1f, 1f, 0.9f)), 32, 2);
            confirmRing.gameObject.AddComponent<AutoPill>();
            confirmRing.raycastTarget = false;
            confirmLabel = UiKit.Display(UiKit.Label("Label", confirmImage.transform, "확인", 52, Palette.Text, 0, 0, 1, 1));
            SetConfirmReady(false);
        }

        // 둥근 [선제공격!] (Figma 'Preempt Prompt' 264×284): 빨간 원 196 + 금테 + 칼 아이콘 + '선제공격!' 알약, 바깥 고리가 퍼지며 맥박
        private void BuildPreemptPrompt(RectTransform area)
        {
            area.sizeDelta = new Vector2(264, 284);
            preemptPrompt = area.gameObject;
            var glow = UiKit.IconImage("Glow", area, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(320, 320);
            glow.rectTransform.anchoredPosition = new Vector2(0, 18);
            glow.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.45f);
            preemptRing = UiRing.Create("WindowRing", area, Palette.Gold, 6f, 300f);
            preemptRing.rectTransform.anchoredPosition = new Vector2(0, 18);
            var core = UiKit.Pill(UiKit.Panel("PreemptButton", area, Palette.Attack, 0.5f, 0.5f, 0.5f, 0.5f));
            core.rectTransform.sizeDelta = new Vector2(196, 196);
            core.rectTransform.anchoredPosition = new Vector2(0, 18);
            preemptButton = UiKit.AddButton(core);
            preemptButton.onClick.AddListener(StartPreempt);
            var rim = UiRing.Create("Rim", core.transform, Palette.Gold, 6f, 196f);
            rim.rectTransform.anchorMin = Vector2.zero;
            rim.rectTransform.anchorMax = Vector2.one;
            rim.rectTransform.sizeDelta = Vector2.zero;
            var icon = UiKit.IconImage("Icon", core.transform, UiKit.Icon("skill_attack"), 0.5f, 0.5f, 0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(92, 92);
            icon.rectTransform.anchoredPosition = new Vector2(0, 16);

            var label = UiKit.Pill(UiKit.Panel("Label", area, Palette.Attack, 0.5f, 0.5f, 0.5f, 0.5f));
            label.raycastTarget = false;
            var text = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", label.transform, "선제공격!", 42, Palette.Text, 0, 0, 1, 1)));
            label.rectTransform.sizeDelta = new Vector2(text.preferredWidth + 56, 58);
            label.rectTransform.anchoredPosition = new Vector2(0, -94);
            var edge = UiKit.Outline(UiKit.Panel("Edge", label.transform, Palette.Gold), 32, 4);
            edge.gameObject.AddComponent<AutoPill>();
            edge.raycastTarget = false;
            preemptPrompt.SetActive(false);
        }

        // 동료 배지 (#77, Figma 'Companion Badge' 168×92): 주인공 배지(줄어듦) 오른쪽 · 성유물 칸 왼쪽. 누르면 가방 > 동료
        private void BuildCompanionBadge(RectTransform strip)
        {
            companionBadge = UiKit.RoundPanel("CompanionBadge", strip, Palette.Scrim, UiKit.RadiusMd, 0.41f, 0, 0.575f, 1);
            UiKit.AddButton(companionBadge).onClick.AddListener(() => OpenBag(BagTab.Companion));
            var avatar = UiKit.Pill(UiKit.Panel("Avatar", companionBadge.transform, Palette.PanelLight, 0, 0.5f, 0, 0.5f));
            avatar.raycastTarget = false;
            avatar.rectTransform.pivot = new Vector2(0, 0.5f);
            avatar.rectTransform.sizeDelta = new Vector2(64, 64);
            avatar.rectTransform.anchoredPosition = new Vector2(10, 0);
            companionFace = UiKit.IconImage("Face", avatar.transform, null, 0.08f, 0.08f, 0.92f, 0.92f);
            companionName = UiKit.Label("Name", companionBadge.transform, "", 24, Palette.Text, 0, 0.45f, 1, 0.95f,
                TextAnchor.MiddleLeft, FontStyle.Bold, true, 16);
            companionName.rectTransform.offsetMin = new Vector2(82, 0);
            companionName.rectTransform.offsetMax = new Vector2(-8, 0);
            var back = UiKit.Pill(UiKit.Panel("HpBack", companionBadge.transform, Palette.Track, 0, 0.2f, 1, 0.36f));
            back.raycastTarget = false;
            back.rectTransform.offsetMin = new Vector2(82, 0);
            back.rectTransform.offsetMax = new Vector2(-12, 0);
            companionHpFill = UiKit.Pill(UiKit.Panel("HpFill", back.transform, Palette.Good));
            companionHpFill.raycastTarget = false;
            companionBadge.gameObject.SetActive(false);
        }

        // 동료 그림은 보이기만 한다 — 순간이동 뒤 '주인공 뒤 한 칸'이 벽이면 주인공 자리에서 나오게 할 때만 칸을 본다
        private bool CanFollowerStand(Vector3 world) =>
            walker != null && walker.CanStand(new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y)));

        // 주인공 이동이 끝난 뒤 같은 프레임에 동료가 자취를 따라온다 (#78)
        private void LateUpdate()
        {
            if (follower == null || walker == null || session == null) return;
            follower.SetCompanion(session.PartyCompanion?.Data, player.position, walker.Facing, CanFollowerStand); // 바뀌었을 때만
            follower.Tick(player.position, walker.Facing, CanFollowerStand);
        }

        private void RefreshCompanionBadge()
        {
            var companion = session.PartyCompanion;
            if (walker != null) follower.SetCompanion(companion?.Data, player.position, walker.Facing, CanFollowerStand);
            companionBadge.gameObject.SetActive(companion != null);
            heroBadge.rectTransform.anchorMax = new Vector2(companion != null ? 0.4f : 0.575f, 1f);
            if (companion == null) return;
            companionFace.sprite = UiKit.CompanionPortrait(companion.Data);
            companionFace.enabled = companionFace.sprite != null;
            companionName.text = $"{companion.DisplayName} Lv{companion.Level}";
            float ratio = Mathf.Clamp01((float)companion.CurrentHp / Mathf.Max(1, companion.Stats.MaxHp));
            companionHpFill.enabled = ratio > 0f;
            companionHpFill.rectTransform.anchorMax = new Vector2(ratio, 1);
            companionHpFill.color = ratio > 0.5f ? Palette.Good : ratio > 0.25f ? Palette.Gold : Palette.Bad;
        }

        // 동료를 만난다 (#77): 조우 연출([함께 가기]까지) → 파티에 → 저장·HUD. 메아리 정글 유적 제단에서 부른다
        public IEnumerator MeetCompanion(CompanionData companion)
        {
            if (companion == null || session.HasCompanion(companion)) yield break;
            if (moving) SnapPlayer();
            yield return companionCutscene.Play(companion, Mathf.Max(0.05f, animationScale));
            session.AddCompanion(companion);
            SaveNow();
            RefreshHud();
            ShowToast($"{UiKit.WithJosa(companion.DisplayName, "이", "가")} 파티에 들어왔다!", 2.5f);
        }

        private void UpdateConfirmButton()
        {
            bool ready = FieldInteraction.FindTarget(walker.Map, walker.Position, walker.Facing, IsClearedBossSpot).HasValue;
            if (ready != confirmReady) SetConfirmReady(ready);

            // Ready면 숨 쉬듯 살짝, 부딪힌 직후에는 크게 깜빡
            float scale = 1f;
            if (confirmHint > 0f)
            {
                confirmHint -= Time.unscaledDeltaTime;
                scale = 1f + Mathf.Abs(Mathf.Sin(confirmHint * 12f)) * 0.14f;
            }
            else if (ready) scale = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
            confirmImage.rectTransform.localScale = Vector3.one * scale;
        }

        private void SetConfirmReady(bool ready)
        {
            confirmReady = ready;
            confirmImage.color = ready ? Palette.Gold : new Color(1f, 1f, 1f, 0.22f);
            confirmLabel.color = ready ? Palette.OnAccent : new Color(Palette.Text.r, Palette.Text.g, Palette.Text.b, 0.85f);
            confirmRing.enabled = ready;
            confirmGlow.enabled = ready;
        }

        // 가까운 오브젝트 이름표만 보이게 (쓰러뜨린 보스는 감춤)
        // 말풍선이 떠 있는 동안은 이름표를 숨긴다 (말풍선과 겹치지 않게)
        private void RefreshNameTags() =>
            nameTags.Refresh(walker.Position, cell => bubble.IsVisible || IsCleared(area, cell)
                || walker.Map.Get(cell) == FieldTile.Key && BossGate.HasKey(area, session));

        // 설정 > [튜토리얼 다시 보기]: 본 안내·빨간 점을 지우고 걷기 안내부터 다시 (전투·[확인] 안내도 그 상황에서 다시)
        private void ReplayTutorial()
        {
            session.Tutorials.Clear();
            SaveNow();
            RefreshHud();
            if (!fieldTutorialRunning) StartCoroutine(FieldGuide());
        }

        // 처음 필드에 들어오면 (#61, 사용자 피드백 '글이 너무 많다'): 스틱 받침 위에서 손끝이 끄는 모습 + 바로 위에 한 줄
        // (#65 기기 확인: 넓은 스틱 영역 기준이라 안내가 너무 멀었음 → 받침 기준) → 두 칸 걸어 보면 →
        // 끝까지 미는 모습 + 한 줄 → 달려 보면 끝. 화면을 막지 않고, 그동안만 몬스터가 나오지 않는다.
        // 메뉴·지도·저장 설명은 없앴다 — 빨간 점·목표 한 줄·'✓ 저장'이 그 자리에서 알려 준다
        private IEnumerator FieldGuide()
        {
            yield return null; // 화면 배치가 끝난 뒤
            fieldTutorialRunning = true;
            int from = stepsTaken;
            guide.Show(new GuideHints.Hint
            {
                Text = "여기를 끌어서 이동", Target = () => stick.Base, Gesture = GuideHints.Gesture.Drag, Drag = new Vector2(150, 0)
            });
            while (stepsTaken - from < 2) yield return null;
            guide.Show(new GuideHints.Hint
            {
                Text = "끝까지 밀면 달리기", Target = () => stick.Base, Gesture = GuideHints.Gesture.Drag, Drag = new Vector2(240, 0)
            });
            while (!(IsRunning || (stick.IsHeld && StickInput.IsRunning(stick.Value)))) yield return null;
            guide.Hide();
            session.Tutorials.Mark(TutorialProgress.Field);
            SaveNow();
            fieldTutorialRunning = false;
        }

        // 그 상황을 처음 만났을 때만 (#61): 물건 옆에 서면 [확인]을 톡톡 누르는 손끝 + 한 줄. 써 보면 끝, 멀어지면 다음에 다시
        private void UpdateGuide()
        {
            if (!showTutorials || guide.IsShowing || fieldTutorialRunning) return;
            if (confirmReady && !session.Tutorials.Has(TutorialProgress.Confirm))
            {
                int before = interactions;
                StartCoroutine(guide.Run(session, TutorialProgress.Confirm, new GuideHints.Hint
                {
                    Text = "[확인]을 눌러 보자", Target = () => confirmImage.rectTransform, Gesture = GuideHints.Gesture.Tap,
                    DoneWhen = () => interactions > before
                }, SaveNow, () => !confirmReady && interactions == before));
            }
        }

        // 풀숲 가까이 처음 오면 주인공 혼잣말 (#61) — 걸어도 잠깐 남아 있음
        private void MaybeSayGrassLine()
        {
            if (!showTutorials || fieldTutorialRunning || roaming != null || session.Tutorials.Has(TutorialProgress.Grass) || bubble.IsVisible) return;
            if (!GrassNearby(2)) return;
            session.Tutorials.Mark(TutorialProgress.Grass);
            bubble.Say("풀숲에서 뭔가 나올 것 같아…", 2.6f);
            bubbleKeepUntil = Time.unscaledTime + 2.6f;
            SaveNow();
        }

        private bool GrassNearby(int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                if (walker.Map.Get(walker.Position + new Vector2Int(dx, dy)) == FieldTile.Grass) return true;
            return false;
        }

        // ------------------------------------------------------------------ 빨간 점 · 목표 한 줄 · 저장 표시 (#61)

        private void AddBadge(string id, RectTransform host)
        {
            var dot = UiKit.Pill(UiKit.Panel("Badge", host, Palette.Bad, 1, 1, 1, 1));
            dot.raycastTarget = false;
            var rt = dot.rectTransform;
            rt.sizeDelta = new Vector2(30, 30);
            rt.anchoredPosition = new Vector2(-8, -8);
            var ring = UiKit.Outline(UiKit.Panel("Ring", rt, Color.white), 15, 3);
            ring.gameObject.AddComponent<AutoPill>();
            ring.raycastTarget = false;
            rt.gameObject.SetActive(false);
            badges[id] = rt;
        }

        private void RefreshBadges()
        {
            foreach (var pair in badges)
                pair.Value.gameObject.SetActive(showTutorials && MenuBadges.ShouldShow(pair.Key, session));
        }

        // 그 메뉴를 열면 점이 꺼진다 (점이 켜져 있었을 때만 — 아직 의미가 없을 때 열어 본 것은 세지 않음)
        private void ClearBadge(string id)
        {
            if (!IsBadgeVisible(id)) return;
            session.Tutorials.Mark(id);
            SaveNow();
            RefreshBadges();
        }

        // 2초마다 한 번 톡 커졌다 돌아온다
        private void PulseBadges()
        {
            float k = Time.unscaledTime % 2f;
            float scale = k < 0.3f ? 1f + 0.35f * Mathf.Sin(k / 0.3f * Mathf.PI) : 1f;
            foreach (var dot in badges.Values)
                if (dot.gameObject.activeSelf) dot.localScale = Vector3.one * scale;
        }

        private void RefreshObjective()
        {
            objective = Objectives.Current(session, area);
            objectiveText.text = ObjectiveLine(objective);
            objectivePill.sizeDelta = new Vector2(Mathf.Clamp(objectiveText.preferredWidth + 88f, 200f, 700f), 60f);
            objectivePill.anchoredPosition = new Vector2(24, -(minimap.Height + 12f));
            minimap.SetGoal(objective.Cell);
        }

        private string ObjectiveLine(Objective goal)
        {
            switch (goal.Kind)
            {
                case ObjectiveKind.FirstBattle:
                    return "풀숲에서 몬스터를 만나 보자";
                case ObjectiveKind.GoToArea:
                    string next = goal.Next.DisplayName, destination = goal.Destination.DisplayName;
                    return goal.Next == goal.Destination
                        ? $"{UiKit.WithJosa(next, "으로", "로")} 가자"
                        : $"{UiKit.WithJosa(next, "을", "를")} 지나 {UiKit.WithJosa(destination, "으로", "로")}";
                case ObjectiveKind.DefeatBoss:
                    return $"보스 {UiKit.WithJosa(goal.Destination.Boss.Species.DisplayName, "을", "를")} 물리치자";
                case ObjectiveKind.FindKey:
                    return "보스 방 열쇠를 찾자";
                case ObjectiveKind.GatherWords:
                    return $"새 단어로 힘을 모으자 {BossGate.WordsFound(area, session)}/{area.BossGateWords}";
                case ObjectiveKind.OpenGate:
                    return "보스 방 문을 열자";
                default:
                    var progress = Dex.GetProgress(area.Words, session.Vocabulary);
                    return $"도감 채우기 {progress.Discovered}/{progress.Total}";
            }
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

        private void OpenMap()
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Map);
            mapView.Show(area, minimap.Texture, walker.Position, session, objective.Cell);
        }

        // 미니맵에서 '끝난 곳' (연 상자, 쓰러뜨린 보스)
        private bool IsCellDone(Vector2Int cell)
        {
            switch (walker.Map.Get(cell))
            {
                case FieldTile.Chest: return session.World.IsChestOpened(area.ChestId(cell));
                case FieldTile.Boss: return session.World.IsBossDefeated(area.BossId);
                case FieldTile.Gate: return BossGate.IsOpen(area, session);
                case FieldTile.Key: return BossGate.HasKey(area, session);
                default: return false;
            }
        }

        private void OpenBag() => OpenBag(BagTab.Hero);

        // tab: 열 탭, slot: 성유물 탭에서 고를 칸 (그 칸에 끼운 성유물)
        private void OpenBag(BagTab tab, int slot = -1)
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Bag);
            var relic = session.Hero.SlotAt(slot);
            inventoryView.Show(session, database, tab, relic, OnTownChanged);
        }

        private void OpenSettings()
        {
            if (!CanOpenMenu) return;
            var full = FullVersionArea();
            settingsView.Show(settings, saveSettings, deleteSave, ReplayTutorial, new SettingsView.PurchaseHooks
            {
                Store = store, Owned = entitlements, OpenOffer = full != null ? () => OpenFullVersionOffer(full) : (Action)null
            });
        }

        private void OpenDex()
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Dex);
            var completed = session.ClaimDexRewards(new[] { area.Words });
            if (completed.Count > 0)
            {
                SaveNow();
                ShowToast($"★ {area.Words.RegionName} 도감 완성! 보상을 받았다", 3f);
            }
            dexView.Show(area.Words, session, DateTime.UtcNow);
        }

        // ------------------------------------------------------------------ 보이는 몬스터 · 선제공격 (#73)

        private void SetUpRoamers(Vector2Int position)
        {
            roamerViews.Clear();
            battleRoamer = null;
            var table = area.Encounters;
            roaming = area.EncounterStyle == EncounterStyle.Roaming && table != null
                ? new RoamingMonsters(walker.Map, r => table.Roll(r), rng)
                : null;
            if (roaming != null)
            {
                roaming.Populate(position);
                roamerViews.Sync(roaming);
            }
            RefreshPreemptBooks();
        }

        // 선제공격 문제를 낼 단어장 (이미 본 단어가 있는 곳만) — 지역에 들어올 때·전투가 끝날 때 다시 계산
        private void RefreshPreemptBooks() =>
            preemptBooks = roaming == null ? new List<IReadOnlyList<WordEntry>>()
                : Preemptive.QuizBooks(area.Words != null ? area.Words.Words : null,
                    TrainingBooks().ConvertAll(b => (IReadOnlyList<WordEntry>)b.Words), session.Vocabulary);

        // 전투·화면 전환·창이 열려 있으면 멈춤. [선제공격] 문제를 푸는 동안은 느리게 계속 (그래도 부딪히면 문제가 끝난 뒤 전투)
        private void UpdateRoamers(float dt)
        {
            preemptView.Tick(dt);
            if (roaming == null)
            {
                if (preemptPrompt.activeSelf) preemptPrompt.SetActive(false);
                preemptLock.SetActive(false);
                return;
            }
            roamerViews.Tick(dt);
            bool quiz = preemptView.IsOpen;
            bool paused = inBattle || transitioning || fieldTutorialRunning || revealingGate || (IsPanelOpen && !quiz);
            if (!paused)
            {
                bool chance = preemptPrompt.activeSelf && preemptWindow < PreemptWindowSeconds;
                roaming.SlowFactor = quiz ? PreemptSlow : chance ? PreemptChanceSlow : 1f;
                var events = roaming.Tick(dt, walker.Position);
                roamerViews.Apply(events, roaming.Rules, roaming.SlowFactor);
                foreach (var e in events)
                {
                    if (e.Kind == RoamerEventKind.Noticed) Sound.Play(Sfx.Click);
                    if (e.Kind == RoamerEventKind.Caught && !quiz)
                    {
                        StartRoamerBattle(e.Roamer, null);
                        break;
                    }
                }
            }
            UpdatePreemptButton(paused || quiz, dt);
        }

        private void UpdatePreemptButton(bool hidden, float dt)
        {
            if (pauseTutorial.IsShowing)
            {
                // 튜토리얼로 멈춘 동안 (#79): 버튼·괄호는 밝게 그대로, 기회 원은 멈춤
                if (!preemptPrompt.activeSelf) preemptPrompt.SetActive(true);
                UpdatePreemptLock(roaming.PreemptTarget(walker.Position));
                return;
            }
            var target = hidden || IsPanelOpen || preemptBooks.Count == 0 ? null : roaming.PreemptTarget(walker.Position);
            bool show = target != null;
            if (preemptPrompt.activeSelf != show) preemptPrompt.SetActive(show);
            UpdatePreemptLock(target);
            if (!show)
            {
                preemptWindow = 0f;
                return;
            }
            // 기회 슬로모션 (QTE): 버튼이 뜨면 1.5초 동안 몬스터가 느려지고, 버튼 둘레의 금색 원이 줄어들며 남은 기회를 보여 줌
            // (Figma 'Preempt Prompt' Window=Open → Closing). 원이 다 줄면 원래 속도 — 그래도 부딪히기 전까지는 누를 수 있다
            preemptWindow += dt;
            float window = Mathf.Clamp01(preemptWindow / PreemptWindowSeconds);
            preemptButton.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.04f);
            preemptRing.gameObject.SetActive(window < 1f);
            preemptRing.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(300f, 204f, window);
            preemptRing.color = window > 0.7f ? Palette.Bad : Palette.Gold;
            if (showTutorials && !preemptTutorialRunning && !session.Tutorials.Has(TutorialProgress.Preempt))
                StartCoroutine(RunPreemptTutorial());
        }

        // 정글에서 처음 [선제공격] 기회 (#79, 사용자 요청 — 사용자가 정한 말): 화면을 멈추고 세 단계. 마지막은 버튼을 직접 눌러 봐야 끝
        private static readonly string[] PreemptTutorialLines =
        {
            "정글에 오신걸 환영합니다. 이제 적이 당신을 직접 싸우러 옵니다.",
            "일정 거리 적과 가까워지면 선제공격을 할 수 있습니다.",
            "버튼을 눌러보세요!"
        };

        private IEnumerator RunPreemptTutorial()
        {
            preemptTutorialRunning = true;
            if (moving) SnapPlayer();
            guide.Hide();
            pauseTutorial.RaiseBelow(markLayer); // 조준 괄호·범위·[선제공격] 버튼은 막 위에서 밝게, 버튼만 눌림
            bool completed = false;
            yield return pauseTutorial.Run(PreemptTutorialLines, () => preemptView.IsOpen,
                () => inBattle || roaming == null, step => preemptRange.gameObject.SetActive(step == 1), ok => completed = ok);
            preemptTutorialRunning = false;
            if (!completed) yield break;
            session.Tutorials.Mark(TutorialProgress.Preempt);
            SaveNow();
        }

        // 선제공격 거리의 몬스터에 빨간 조준 괄호 (1.5칸, 천천히 숨쉬듯)
        private void UpdatePreemptLock(Roamer target)
        {
            var point = target != null && roamerViews.WorldPosition(target) is Vector3 world ? MarkPoint(world) : null;
            preemptLock.SetActive(point.HasValue);
            if (!point.HasValue) return;
            preemptLock.Root.anchoredPosition = point.Value;
            preemptLock.SetSize(TilePixels() * 1.5f);
            if (preemptRange.gameObject.activeSelf)
            {
                // '선제공격 범위' = 몬스터에게서 PreemptRadius칸 (그 칸까지 감싸게 반 칸 더)
                preemptRange.rectTransform.anchoredPosition = point.Value;
                preemptRange.rectTransform.sizeDelta = Vector2.one * (roaming.Rules.PreemptRadius * 2 + 1) * TilePixels();
            }
            preemptLock.Root.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f);
        }

        // 월드 위치 → 몬스터 표시 층의 좌표 (가운데 기준 — PreemptView 창과 같은 좌표)
        private Vector2? MarkPoint(Vector3 world)
        {
            var canvasCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(markLayer, cam.WorldToScreenPoint(world), canvasCamera, out var local)
                ? local : (Vector2?)null;
        }

        // 한 칸이 HUD에서 몇 단위인지 (조준 괄호·QTE 원 크기)
        private float TilePixels()
        {
            var origin = cam.transform.position;
            var a = MarkPoint(new Vector3(origin.x, origin.y, 0f));
            var b = MarkPoint(new Vector3(origin.x + 1f, origin.y, 0f));
            return a.HasValue && b.HasValue ? Mathf.Max(20f, Mathf.Abs(b.Value.x - a.Value.x)) : 90f;
        }

        // [선제공격]: 몬스터가 느려지고 이미 본 단어 한 문제 → 맞히면 맨 앞 몬스터가 다친 채, 틀리면 그냥 전투 (사용자 결정)
        private void StartPreempt()
        {
            if (roaming == null || preemptView.IsOpen || inBattle || transitioning || preemptBooks.Count == 0) return;
            var target = roaming.PreemptTarget(walker.Position);
            if (target == null) return;
            if (moving) SnapPlayer();
            var quiz = new TrainingQuizProvider(preemptBooks, session.Vocabulary, battle.MasteryRules, TrainingMode.All, rng);
            var question = quiz.NextQuestion(QuizDirection.EnglishToMeaning);
            preemptPrompt.SetActive(false);
            preemptLock.SetActive(false);
            Vector2? Focus() => roamerViews.WorldPosition(target) is Vector3 world ? MarkPoint(world) : null;
            preemptView.Show(question, Preemptive.AnswerSeconds, index =>
            {
                bool correct = question.IsCorrect(index);
                quiz.SubmitAnswer(question, correct);
                if (!roaming.Has(target)) return;
                string intro;
                if (correct)
                {
                    int dealt = Preemptive.Strike(target.Leader);
                    PreemptStrikes++;
                    intro = $"선제공격 성공! {target.Leader.DisplayName}에게 {dealt} 피해";
                }
                else intro = index < 0 ? "선제공격 실패 — 시간 초과! 정면 승부다" : "선제공격 실패 — 정면 승부다!";
                StartRoamerBattle(target, intro);
            }, Focus, TilePixels, Preemptive.Damage(target.Leader), settings != null && settings.Vibration);
        }

        private void StartRoamerBattle(Roamer roamer, string intro)
        {
            if (inBattle || transitioning) return;
            if (moving) SnapPlayer();
            session.World.SetPosition(area.AreaId, walker.Position);
            battleRoamer = roamer;
            preemptPrompt.SetActive(false);
            preemptLock.SetActive(false);
            StartCoroutine(Encounter(roamer.Group, intro));
        }

        // ------------------------------------------------------------------ 수련 (#44)

        // 수련할 단어장: 모든 지역의 단어장 (도감 속 단어). GameDatabase가 없으면(테스트) 지금 지역만
        private List<WordDatabase> TrainingBooks()
        {
            var books = new List<WordDatabase>();
            if (database != null)
                foreach (var candidate in database.Areas)
                    if (candidate != null && candidate.Words != null && !books.Contains(candidate.Words)) books.Add(candidate.Words);
            if (!books.Contains(area.Words)) books.Insert(0, area.Words);
            return books;
        }

        private MonsterSpecies TrainingDummy()
        {
            if (database == null) return null;
            foreach (var monster in database.Monsters)
                if (monster != null && monster.IsTrainingDummy) return monster;
            return null;
        }

        private void OpenTraining()
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Training);
            var books = TrainingBooks().ConvertAll(b => b.Words);
            var (discovered, wrong) = TrainingQuizProvider.Count(books, session.Vocabulary);
            trainingView.Show(discovered, wrong);
        }

        private void StartTraining(TrainingMode mode)
        {
            var dummy = TrainingDummy();
            if (dummy == null || inBattle || transitioning)
            {
                ShowToast("수련장을 준비하고 있어요");
                return;
            }
            inBattle = true;
            HideToast();
            Sound.PlayMusic(Music.Battle);
            battle.BeginTraining(dummy, TrainingBooks(), area.Words, mode, OnTrainingFinished, area.Theme);
        }

        private void OnTrainingFinished()
        {
            inBattle = false;
            Sound.PlayMusic(AreaMusic);
            SaveNow();
            RefreshHud();
        }

        private void RefreshHud()
        {
            areaLabel.text = area.DisplayName;
            var progress = Dex.GetProgress(area.Words, session.Vocabulary);
            dexLabel.text = $"발견 {progress.Discovered}/{progress.Total}   {session.Inventory.Gold}G";

            var hero = session.Hero;
            heroName.text = $"{hero.DisplayName} Lv{hero.Level}";
            heroHp.text = $"HP {hero.CurrentHp}/{hero.Stats.MaxHp}";
            float ratio = Mathf.Clamp01((float)hero.CurrentHp / Mathf.Max(1, hero.Stats.MaxHp));
            heroHpFill.enabled = ratio > 0f;
            heroHpFill.rectTransform.anchorMax = new Vector2(ratio, 1);
            heroHpFill.color = ratio > 0.5f ? Palette.Good : ratio > 0.25f ? Palette.Gold : Palette.Bad;
            RefreshCompanionBadge();
            for (int i = 0; i < relicSlots.Count; i++)
            {
                var relic = hero.SlotAt(i);
                var (back, icon, level, empty) = relicSlots[i];
                back.color = relic != null ? Palette.PanelLight : Palette.Track;
                icon.sprite = relic != null ? UiKit.RelicIcon(relic.Data) : null;
                icon.enabled = icon.sprite != null;
                level.transform.parent.gameObject.SetActive(relic != null);
                level.text = relic != null ? $"+{relic.Level}" : "";
                empty.enabled = relic == null;
            }
            RefreshBadges();
            RefreshObjective();
        }

        private void ShowToast(string message, float seconds = 2f)
        {
            toastText.text = message;
            toastPanel.SetActive(true);
            toastUntil = Time.unscaledTime + seconds;
        }

        private void HideToast() => toastPanel.SetActive(false);

        private void UpdateToast()
        {
            if (toastPanel.activeSelf && Time.unscaledTime >= toastUntil) toastPanel.SetActive(false);
        }
    }
}
