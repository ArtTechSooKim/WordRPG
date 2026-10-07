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
        [SerializeField] private float stepDuration = 0.16f;
        [Tooltip("달릴 때 한 칸 시간 = 걷기 시간 × 이 값")]
        [SerializeField] private float runStepRatio = 0.55f;
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
        private TutorialOverlay tutorial;
        private PaywallView paywall;
        private IStore store;               // 결제 창구 (GameManager 것, 테스트는 가짜)
        private Entitlements entitlements; // 산 상품 — 없으면(테스트) 정식판 잠금 없음
        private bool paywallArmed = true;  // 정식판 안내를 닫은 뒤에는 스틱을 한 번 떼야 다시 뜬다
        private bool fieldTutorialRunning; // 첫 안내 중에는 몬스터가 나오지 않는다
        private int stepsTaken;
        private int runSteps;
        private DexView dexView;
        private TrainingView trainingView;
        private RelicAltarView altarView;
        private ShopView shopView;
        private InventoryView inventoryView;
        private SettingsView settingsView;
        private MinimapView minimap;
        private MapView mapView;
        private SkillLearnView learnView;
        private ConfirmDialog quitDialog; // 뒤로가기 → '게임을 끝낼까요?'
        private Text heroName, heroHp;
        private Image heroHpFill;
        private readonly List<(Image back, Image icon, Text level, Text empty)> relicSlots = new List<(Image, Image, Text, Text)>();

        public Vector2Int PlayerCell => walker.Position;
        public Direction Facing => walker.Facing;
        public bool IsMoving => moving;
        public bool IsRunning => moving && running; // 지금 칸을 달리는 중
        public VirtualStick Stick => stick;
        public TutorialOverlay Tutorial => tutorial;
        public PaywallView Paywall => paywall;
        public bool IsInBattle => inBattle || transitioning;
        public int RespawnsPlayed { get; private set; }   // 지고 나서 다시 일어나는 연출 횟수 (테스트용)
        public int DustPuffs { get; private set; }        // 달릴 때 발밑 먼지 (테스트용)
        public Sprite PlayerSprite => playerRenderer != null ? playerRenderer.sprite : null;
        public BattleScreen Battle => battle;
        public GameSession Session => session;
        public FieldArea CurrentArea => area;
        public bool IsPanelOpen => dexView.IsOpen || trainingView.IsOpen || altarView.IsOpen || shopView.IsOpen
                                   || inventoryView.IsOpen || settingsView.IsOpen || mapView.IsOpen || learnView.IsOpen
                                   || (quitDialog != null && quitDialog.IsOpen) || (paywall != null && paywall.IsOpen);
        public SkillLearnView LearnView => learnView;
        public TrainingView TrainingView => trainingView;
        public bool IsQuitDialogOpen => quitDialog.IsOpen;
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
            if (showTutorials && !session.Tutorials.Has(TutorialProgress.Field)) StartCoroutine(FieldTutorial());
            return true;
        }

        private void Update()
        {
            if (!initialized) return;
            if (BackPressed()) HandleBack();
            UpdateToast();
            UpdateCamera();
            nameTags.Tick(cam, hudCanvas, Time.unscaledDeltaTime);

            if (inBattle || transitioning || IsPanelOpen || tutorial.IsBlocking)
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
            var outcome = walker.TryStep(direction);
            if (outcome.Kind != StepKind.Moved) playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);

            switch (outcome.Kind)
            {
                case StepKind.Moved:
                    moving = true;
                    running = run;
                    // 달리면 두 걸음마다 떠난 자리에 먼지가 퍼진다
                    runSteps = run ? runSteps + 1 : 0;
                    if (run && runSteps % 2 == 1)
                    {
                        DustPuffs++;
                        StartCoroutine(FieldFx.Play(transform, player.position + Vector3.down * 0.3f, Fx.Dust, 1.25f, animationScale, 9));
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
            var direction = FieldInteraction.FindTarget(walker.Map, walker.Position, walker.Facing);
            if (!direction.HasValue) return false;
            walker.Face(direction.Value);
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            var cell = walker.Position + direction.Value.ToOffset();
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
            if (tutorial.IsShowing) return; // 안내 중에는 [건너뛰기]로
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
            if (transitioning) return;
            if (quitDialog.IsOpen) quitDialog.Hide();
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
            }
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
                ShowToast($"{UiKit.WithJosa(name, "이", "가")} 있던 자리에\n펼쳐진 책이 빛나고 있다.");
                return;
            }
            var enemies = new List<MonsterInstance> { new MonsterInstance(boss.Species, boss.Level) };
            StartCoroutine(Encounter(enemies, $"보스 출현! {name} Lv{boss.Level} — 정답으로 맞서라!", true));
        }

        // 강화·구매·장착 직후 저장하고 HUD(주인공·성유물·골드) 갱신
        private void OnTownChanged()
        {
            saveProgress?.Invoke();
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
            var tile = walker.Map.Get(walker.Position);
            if (tile == FieldTile.Door)
            {
                var exit = area.GetExit(walker.Position);
                if (exit != null && exit.Target != null) StartCoroutine(UseDoor(exit));
                else ShowToast("문이 굳게 닫혀 있다.");
                return false;
            }
            if (!fieldTutorialRunning && encounterCounter.OnStep(tile == FieldTile.Grass, rng))
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
            saveProgress?.Invoke();

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
            saveProgress?.Invoke();
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
            saveProgress?.Invoke();
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

        private void OnBattleFinished(bool won)
        {
            inBattle = false;
            Sound.PlayMusic(AreaMusic);
            encounterCounter.Reset();
            bool wasBoss = bossBattle;
            bossBattle = false;

            if (won && wasBoss)
            {
                session.World.MarkBossDefeated(area.BossId);
                var bossCell = walker.Map.BossPosition.Value;
                tilemap.SetTile(new Vector3Int(bossCell.x, bossCell.y, 0), TileFor(FieldTile.Boss, true));
                minimap.Redraw();
                RefreshNameTags();
                string name = area.Boss.Species.DisplayName;
                var relic = session.GrantRelic(area.Boss.RewardRelic);
                var rewardItem = area.Boss.RewardItem; // 기술문서 등
                if (rewardItem != null) session.Inventory.Add(rewardItem);
                saveProgress?.Invoke();
                string message = relic != null
                    ? $"★ {UiKit.WithJosa(name, "을", "를")} 물리쳤다!\n성유물 '{relic.Data.DisplayName}' 획득 — {RelicHint(relic)}"
                    : $"★ {UiKit.WithJosa(name, "을", "를")} 물리쳤다!\n{area.DisplayName}에 잊혀진 기억이 돌아왔다";
                if (rewardItem != null) message += $"\n{rewardItem.DisplayName} 획득!";
                Action offer = rewardItem != null && rewardItem.IsSkillDocument ? () => OfferDocument(rewardItem) : (Action)null;
                // 이 보스가 여는 출입구가 있으면 카메라가 가서 보여 준다 (그다음 기술 배우기)
                var gates = database != null ? FieldArea.GatesOpenedBy(area, database.Areas) : new List<AreaGate>();
                if (gates.Count > 0) StartCoroutine(RevealGates(gates, message, offer));
                else
                {
                    ShowToast(message, 4.5f);
                    offer?.Invoke();
                }
            }
            else if (!won)
            {
                // 패배: 전투 화면이 이미 주인공을 회복시켰다. 이 지역의 시작 위치로 돌아간다
                walker.WarpTo(walker.Map.Start);
                SnapPlayer();
                RefreshNameTags();
                session.World.SetPosition(area.AreaId, walker.Position);
                saveProgress?.Invoke();
                StartCoroutine(Respawn($"{area.DisplayName} 시작 지점으로 돌아왔다. HP가 회복되었다!"));
            }
            RefreshHud();
        }

        // 지고 나서 시작 지점에서 다시 일어나는 연출: 어둠이 걷힘 → 발밑에 빛 고리 + 반짝이 + 소리 →
        // 주인공이 빛 속에서 작게 → 원래 크기로, 투명 → 또렷하게 나타나며 살짝 떠올랐다 내려앉음
        private IEnumerator Respawn(string message)
        {
            transitioning = true;
            RespawnsPlayed++;
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(1f, 1f, 0f);
            var home = player.position;
            playerRenderer.color = new Color(1f, 1f, 1f, 0f);
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.5f * animationScale);

            Sound.Play(Sfx.Respawn);
            StartCoroutine(FieldFx.Play(transform, home, Fx.Heal, 2.6f, animationScale * 1.5f, 11));
            yield return WaitUnscaled(0.25f);
            StartCoroutine(FieldFx.Play(transform, home + Vector3.up * 0.35f, Fx.Sparkle, 1.9f, animationScale * 1.3f, 12));

            float appear = 0.7f * animationScale;
            for (float t = 0f; t < appear; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, appear));
                playerRenderer.color = new Color(1f, 1f, 1f, k);
                player.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, 1f - (1f - k) * (1f - k));
                player.position = home + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.3f);
                yield return null;
            }
            playerRenderer.color = Color.white;
            player.localScale = Vector3.one;
            player.position = home;
            yield return WaitUnscaled(0.3f);
            Destroy(cutscene.gameObject);
            transitioning = false;
            ShowToast(message);
        }

        // ------------------------------------------------------------------ 길 열림 연출

        // 승리 알림을 잠깐 보여 준 뒤, 열린 출입구마다: 어두워짐 → 그 지역의 출입구 앞으로 카메라 이동 →
        // 막힌 덤불이 흔들리며 사라지고 '열렸다!' → 어두워짐 → 원래 자리로
        private IEnumerator RevealGates(List<AreaGate> gates, string victoryMessage, Action after = null)
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
            after?.Invoke();
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
            walker = new FieldWalker(map, position, cell => IsDoorLocked(cell) || IsPaywalled(cell));
            encounterCounter = new EncounterCounter(area.EncounterRate, area.MinStepsBetweenEncounters);
            session.World.SetPosition(area.AreaId, position);

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
                            || kind == FieldTile.Boss && session.World.IsBossDefeated(fieldArea.BossId);
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
            for (int i = 0; i < entries.Length; i++)
            {
                float topY = 1f - i * (136f / menuHeight);
                var button = UiKit.IconButton(entries[i].name, menu, entries[i].icon, entries[i].label, Palette.Scrim,
                    0, topY - 120f / menuHeight, 1, topY);
                button.onClick.AddListener(entries[i].open);
            }

            // 주인공 배지 + 끼운 성유물 3칸 (Figma 'Field — HUD (주인공·성유물)' / 'Hero Badge' / 'Relic Slot')
            var strip = UiKit.Rect("HeroStrip", hudRoot, 0, 0.875f, 1, 0.935f);
            var badge = UiKit.RoundPanel("HeroBadge", strip, Palette.Scrim, UiKit.RadiusMd, 0.012f, 0, 0.575f, 1);
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
            paywall = PaywallView.Create(hudRoot);
            tutorial = TutorialOverlay.Create(transform); // 자기 캔버스로 HUD·전투 위에
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

        private void UpdateConfirmButton()
        {
            bool ready = FieldInteraction.FindTarget(walker.Map, walker.Position, walker.Facing).HasValue;
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
        private void RefreshNameTags() =>
            nameTags.Refresh(walker.Position, cell => walker.Map.Get(cell) == FieldTile.Boss && session.World.IsBossDefeated(area.BossId));

        // 설정 > [튜토리얼 다시 보기]: 본 안내를 지우고 필드 안내부터 다시 (전투 안내도 다음 전투에서 다시)
        private void ReplayTutorial()
        {
            session.Tutorials.Clear();
            saveProgress?.Invoke();
            StartCoroutine(FieldTutorial());
        }

        // 처음 필드에 들어오면 (#36, Figma '튜토리얼 — 필드 스틱'):
        // 환영 → 걷기(직접) → 달리기(직접) → [확인] → 메뉴 → 지도 → 자동 저장·설정 → 풀숲·전투
        private IEnumerator FieldTutorial()
        {
            yield return null; // 화면 배치가 끝난 뒤
            int? walkFrom = null;
            var steps = new List<TutorialOverlay.Step>
            {
                new TutorialOverlay.Step { Text = "영단어RPG에 온 것을 환영해요!\n처음이니 움직이는 법부터 몇 가지만 알려 드릴게요." },
                new TutorialOverlay.Step
                {
                    Text = "화면 오른쪽 아래를 엄지로 끌면 그 방향으로 걸어요.\n두 칸 걸어 보세요!", Target = () => stick.Zone,
                    DoneWhen = () =>
                    {
                        walkFrom ??= stepsTaken;
                        return stepsTaken - walkFrom.Value >= 2;
                    }
                },
                new TutorialOverlay.Step
                {
                    Text = "스틱을 끝까지 밀면 달릴 수 있어요.\n끝까지 밀어 보세요!", Target = () => stick.Zone,
                    DoneWhen = () => IsRunning || (stick.IsHeld && StickInput.IsRunning(stick.Value))
                },
                new TutorialOverlay.Step
                {
                    Text = "상자·샘·제단·상점 옆에 서면 [확인]이 금색으로 빛나요.\n그때 눌러서 사용해요.",
                    Target = () => confirmImage.rectTransform
                },
                new TutorialOverlay.Step { Text = "[도감]에는 만난 단어가 모여요. [수련]에서는 그 단어로 허수아비와 싸우며 외울 수 있고,\n[가방]에는 아이템과 성유물이 있어요.", Target = () => menuRect },
                new TutorialOverlay.Step
                {
                    Text = "왼쪽 위 작은 지도를 누르면 큰 지도로 볼 수 있어요.", Target = () => (RectTransform)minimap.Button.transform
                },
                new TutorialOverlay.Step
                {
                    Text = "게임은 자동으로 저장돼요. 문제를 풀 때마다, 전투가 끝날 때마다 저장되니 언제 꺼도 이어서 할 수 있어요.\n" +
                           "처음부터 다시 하기와 이 안내 다시 보기는 [설정]에 있어요.",
                    Target = () => (RectTransform)menuRect.Find("SettingsButton")
                },
                new TutorialOverlay.Step
                {
                    Text = "진한 풀숲을 걸으면 야생 몬스터가 나타나요.\n전투에서는 영단어를 맞혀야 기술을 쓸 수 있어요. 모험을 떠나 볼까요?",
                    Button = "시작하기"
                },
            };
            fieldTutorialRunning = true;
            yield return tutorial.Run(session, TutorialProgress.Field, steps, saveProgress);
            fieldTutorialRunning = false;
        }

        private void BuildBattle()
        {
            var battleGo = new GameObject("Battle");
            battleGo.transform.SetParent(transform, false);
            battle = battleGo.AddComponent<BattleScreen>();
            if (showTutorials) battle.SetTutorial(tutorial); // 첫 전투 안내 (기술·강도·새 단어·문제·오답·콤보·상처약)
            battle.Configure(area.Encounters, area.Words, session, animationScale, battleConfig, saveProgress, loop: false,
                gameDatabase: database);
        }

        // 메뉴는 걷는 중·전투 중·다른 창이 열려 있을 때는 열지 않는다
        private bool CanOpenMenu => !(inBattle || transitioning || moving || IsPanelOpen);

        private void OpenMap()
        {
            if (CanOpenMenu) mapView.Show(area, minimap.Texture, walker.Position, session);
        }

        // 미니맵에서 '끝난 곳' (연 상자, 쓰러뜨린 보스)
        private bool IsCellDone(Vector2Int cell)
        {
            switch (walker.Map.Get(cell))
            {
                case FieldTile.Chest: return session.World.IsChestOpened(area.ChestId(cell));
                case FieldTile.Boss: return session.World.IsBossDefeated(area.BossId);
                default: return false;
            }
        }

        private void OpenBag() => OpenBag(BagTab.Hero);

        // tab: 열 탭, slot: 성유물 탭에서 고를 칸 (그 칸에 끼운 성유물)
        private void OpenBag(BagTab tab, int slot = -1)
        {
            if (!CanOpenMenu) return;
            var relic = session.Hero.SlotAt(slot);
            inventoryView.Show(session, database, tab, relic, OnTownChanged);
        }

        private void OpenSettings()
        {
            if (CanOpenMenu) settingsView.Show(settings, saveSettings, deleteSave, ReplayTutorial);
        }

        private void OpenDex()
        {
            if (!CanOpenMenu) return;
            var completed = session.ClaimDexRewards(new[] { area.Words });
            if (completed.Count > 0)
            {
                saveProgress?.Invoke();
                ShowToast($"★ {area.Words.RegionName} 도감 완성! 보상을 받았다", 3f);
            }
            dexView.Show(area.Words, session, DateTime.UtcNow);
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
            saveProgress?.Invoke();
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
