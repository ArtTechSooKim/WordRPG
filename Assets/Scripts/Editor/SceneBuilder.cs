using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using WordRPG.Words;

namespace WordRPG.EditorTools
{
    // 씬 생성 (타이틀 = 빌드 첫 씬, 필드 = 본 게임, 전투 연습). 샘플 데이터가 없으면 먼저 만든다.
    // 배치모드: -executeMethod WordRPG.EditorTools.SceneBuilder.CreateAllScenes
    public static class SceneBuilder
    {
        public const string BattleScenePath = "Assets/Scenes/Battle.unity";

        public const string FieldScenePath = "Assets/Scenes/Field.unity";

        public const string TitleScenePath = "Assets/Scenes/Title.unity";

        // 타이틀: 이어하기 / 처음부터 → 필드 씬 (앱을 켜면 처음 나오는 씬)
        [MenuItem("WordRPG/Scenes/Create Title Scene")]
        public static void CreateTitleScene()
        {
            PrepareData();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(Palette.Background);
            AddGameManager();
            new GameObject("TitleScreen", typeof(TitleScreen));

            EditorSceneManager.SaveScene(scene, TitleScenePath);
            UpdateBuildSettings();
            Debug.Log($"[WordRPG] 타이틀 씬 생성: {TitleScenePath}");
        }

        // 본 게임: 초원 필드. Play로 바로 열어도 동작한다 (GameManager 포함)
        [MenuItem("WordRPG/Scenes/Create Field Scene")]
        public static void CreateFieldScene()
        {
            PrepareData();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(ThemeLooks.Of(FieldTheme.Meadow).Outside);
            AddGameManager();

            var fieldGo = new GameObject("FieldScreen", typeof(FieldScreen));
            var so = new SerializedObject(fieldGo.GetComponent<FieldScreen>());
            so.FindProperty("area").objectReferenceValue = Load<FieldArea>("Assets/Data/Areas/meadow.asset");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, FieldScenePath);
            UpdateBuildSettings();
            Debug.Log($"[WordRPG] 필드 씬 생성: {FieldScenePath}");
        }

        // 전투만 연속으로 해보는 연습 씬
        [MenuItem("WordRPG/Scenes/Create Battle Scene")]
        public static void CreateBattleScene()
        {
            PrepareData();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(Palette.Background);
            AddGameManager();

            var screenGo = new GameObject("BattleScreen", typeof(BattleScreen));
            var so = new SerializedObject(screenGo.GetComponent<BattleScreen>());
            so.FindProperty("encounter").objectReferenceValue = Load<EncounterTable>("Assets/Data/Encounters/meadow_field.asset");
            so.FindProperty("words").objectReferenceValue = Load<WordDatabase>("Assets/Data/Words/tier1_meadow.asset");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BattleScenePath);
            UpdateBuildSettings();
            Debug.Log($"[WordRPG] 전투 씬 생성: {BattleScenePath}");
        }

        [MenuItem("WordRPG/Scenes/Create All Scenes")]
        public static void CreateAllScenes()
        {
            CreateBattleScene();
            CreateFieldScene();
            CreateTitleScene();
        }

        private static void PrepareData()
        {
            SampleDataBuilder.Build();
            WordCsvImporter.ImportAll();
            MonsterArtLinker.LinkAll();
        }

        private static void AddCamera(Color background)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            cameraGo.transform.position = new Vector3(0, 0, -10);
        }

        // 게임 상태·저장 담당 (씬이 바뀌어도 유지됨)
        private static void AddGameManager()
        {
            var managerGo = new GameObject("GameManager", typeof(GameManager));
            var manager = new SerializedObject(managerGo.GetComponent<GameManager>());
            manager.FindProperty("database").objectReferenceValue = Load<GameDatabase>(GameDatabaseBuilder.DatabasePath);
            manager.FindProperty("hero").objectReferenceValue = Load<HeroData>(SampleDataBuilder.HeroPath);
            manager.ApplyModifiedPropertiesWithoutUndo();
        }

        // 타이틀이 첫 씬 (앱 실행 시 시작), 필드, 전투 연습 순서
        private static void UpdateBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in new[] { TitleScenePath, FieldScenePath, BattleScenePath })
            {
                if (System.IO.File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T Load<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new System.IO.FileNotFoundException(path);
        }
    }
}
