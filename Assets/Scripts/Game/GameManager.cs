using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using WordRPG.Heroes;
using WordRPG.Save;

namespace WordRPG.Game
{
    // 게임 진행 상태(GameSession)와 저장을 맡는 오브젝트. 씬이 바뀌어도 유지된다.
    // 자동 저장: 문제에 답할 때마다, 전투 결과 후(화면 쪽에서 Save 호출), 앱이 백그라운드로 갈 때, 종료될 때.
    // 단, 타이틀에서 시작하기 전(필드·전투가 MarkPlaying 하기 전)에는 저장하지 않는다 — 켜고 바로 끄면 빈 세이브가 생기지 않게
    public class GameManager : MonoBehaviour
    {
        public const string TitleSceneName = "Title";
        public const string FieldSceneName = "Field";
        private const string SettingsKey = "WordRPG.Settings";
        private const string EntitlementsKey = "WordRPG.Entitlements"; // 산 상품 (세이브와 따로 — 처음부터 다시 해도 남음)

        // 테스트가 실제 세이브 파일을 건드리지 않도록 저장 폴더를 바꾸는 용도. 평소에는 null
        public static string SaveDirectoryOverride;

        // 테스트용 결제 창구. 평소에는 null → 실제 기기에서는 애플 인앱 결제(UnityIapStore)
        public static IStore StoreOverride;

        [SerializeField] private GameDatabase database;
        [SerializeField] private HeroData hero;

        private SaveSystem saveSystem;
        private bool playing;

        public static GameManager Instance { get; private set; }
        public GameSession Session { get; private set; }
        public GameSettings Settings { get; private set; } = new GameSettings();
        public Entitlements Entitlements { get; private set; } = new Entitlements(); // 산 상품 (정식판)
        public IStore Store { get; private set; } // 결제 창구 (배치모드 테스트에서는 없음)
        public bool LoadedFromSave { get; private set; }
        public bool HasSave => saveSystem != null && saveSystem.HasSave;
        // 더 새 버전 앱의 세이브가 있어 읽지도 덮어쓰지도 않는 상태 (#48). 타이틀에서 업데이트 안내, 게임 시작 막음
        public bool SaveBlocked { get; private set; }
        public string StatusMessage { get; private set; } = "";
        public string SaveDirectory => saveSystem?.Directory;
        public GameDatabase Database => database;
        public HeroData HeroData => hero;

        // 코드로 만들 때(테스트) 비활성 오브젝트에 붙이고 Configure → SetActive(true) 순서로 쓴다
        public void Configure(GameDatabase db, HeroData heroData)
        {
            database = db;
            hero = heroData;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60; // 모바일 기본 30fps는 걷기·연출이 끊겨 보인다

            if (database == null) Debug.LogError("[GameManager] GameDatabase가 비어 있어 세이브의 성유물·아이템을 불러올 수 없습니다");
            if (hero == null) Debug.LogError("[GameManager] 주인공 데이터(HeroData)가 비어 있습니다");
            saveSystem = new SaveSystem(SaveDirectoryOverride ?? Application.persistentDataPath);
            // 테스트(저장 폴더를 바꾼 경우)에서는 실제 기기 설정을 읽거나 덮어쓰지 않는다
            if (SaveDirectoryOverride == null) Settings = GameSettings.FromJson(PlayerPrefs.GetString(SettingsKey, ""));
            if (SaveDirectoryOverride == null) Entitlements = Entitlements.FromStorage(PlayerPrefs.GetString(EntitlementsKey, ""));
            Entitlements.Changed += SaveEntitlements;
            Store = StoreOverride ?? (Application.isBatchMode ? null : ConnectStore());
            LoadOrCreate();
        }

        private void LoadOrCreate()
        {
            var result = saveSystem.Load();
            if (result.Data != null)
            {
                Session = GameSession.FromSaveData(result.Data, database, hero);
                LoadedFromSave = true;
                playing = true;
                StatusMessage = $"이어하기 — 발견한 단어 {Session.Vocabulary.DiscoveredCount}개, " +
                                $"전투 {Session.Record.BattlesWon}승 {Session.Record.BattlesLost}패";
                if (result.UsedBackup)
                {
                    StatusMessage += " (백업에서 복구)";
                    Debug.LogWarning($"[GameManager] 세이브가 손상되어 백업에서 불러왔습니다: {result.Error}");
                }
                foreach (var warning in Session.LoadWarnings) Debug.LogWarning($"[GameManager] {warning}");
                return;
            }

            if (result.TooNew)
            {
                // 더 새 버전 앱의 세이브: 깨진 것이 아니므로 그대로 두고, 덮어쓰지 않게 저장을 막는다. 타이틀이 업데이트를 안내
                Debug.LogWarning($"[GameManager] 더 새 버전 앱의 세이브라 읽지 않고 그대로 둡니다: {result.Error}");
                SaveBlocked = true;
                Session = GameSession.NewGame(hero);
                LoadedFromSave = false;
                StatusMessage = "더 새 버전 앱에서 저장한 기록이 있어요. 앱을 업데이트해 주세요";
                return;
            }
            if (result.Error != null)
            {
                Debug.LogError($"[GameManager] 세이브를 읽을 수 없어 새 게임으로 시작합니다. 원본은 보관합니다: {result.Error}");
                saveSystem.QuarantineCorrupt();
            }
            Session = GameSession.NewGame(hero);
            LoadedFromSave = false;
            StatusMessage = "새 게임 시작!";
        }

        // 필드·전투 화면이 이 세션으로 게임을 시작했다. 이때부터 자동 저장
        public void MarkPlaying() => playing = true;

        public void Save()
        {
            if (Session == null || !playing || SaveBlocked) return;
            try
            {
                saveSystem.Save(Session.ToSaveData(DateTime.UtcNow));
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] 저장 실패: {e.Message}");
            }
        }

        // 애플 인앱 결제에 연결: 가격을 받아 오고, 이미 산 것(재설치·다른 기기)을 확인해 Entitlements에 넣는다
        private IStore ConnectStore()
        {
            var store = new UnityIapStore(Entitlements);
            store.Connect();
            return store;
        }

        public void SaveEntitlements()
        {
            if (SaveDirectoryOverride != null) return;
            PlayerPrefs.SetString(EntitlementsKey, Entitlements.ToStorage());
            PlayerPrefs.Save();
        }

        public void SaveSettings()
        {
            if (SaveDirectoryOverride != null) return;
            PlayerPrefs.SetString(SettingsKey, Settings.ToJson());
            PlayerPrefs.Save();
        }

        // 저장 파일을 지우고 새 게임 상태로 돌린다. 다시 시작하기 전까지는 저장하지 않는다 (설정은 그대로)
        public void DeleteSave()
        {
            saveSystem.Delete();
            SaveBlocked = false; // 직접 지웠으면 (두 번 확인) 다시 저장할 수 있음
            Session = GameSession.NewGame(hero);
            LoadedFromSave = false;
            playing = false;
            StatusMessage = "새 게임 시작!";
        }

        // 세이브를 지우고 바로 처음부터
        public void StartNewGame()
        {
            DeleteSave();
            playing = true;
            Save();
        }

        // 아이폰은 앱이 스스로 꺼지면 안 된다 (애플 지침 — 홈으로 나가서 닫음) → 끝내기 확인 창을 띄우지 않는다
        public static bool CanQuit => Application.platform != RuntimePlatform.IPhonePlayer;

        // 뒤로가기 → '게임을 끝낼까요?' → 끝내기: 저장하고 앱 종료 (에디터에서는 아무 일도 없음)
        public static void QuitGame()
        {
            if (Instance != null) Instance.Save();
            Application.Quit();
        }

        // 저장 데이터를 지운 뒤: 타이틀 씬이 빌드에 있으면 타이틀로, 없으면 지금 씬을 처음부터
        public static void ReturnToTitle()
        {
            if (Application.CanStreamedLevelBeLoaded(TitleSceneName))
            {
                SceneManager.LoadScene(TitleSceneName);
                return;
            }
            int current = SceneManager.GetActiveScene().buildIndex;
            if (current >= 0) SceneManager.LoadScene(current);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            Save();
            SaveSettings(); // 설정 창을 연 채 앱을 내려도 바꾼 음량이 남게 (#48)
        }

        private void OnApplicationQuit() => Save();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
