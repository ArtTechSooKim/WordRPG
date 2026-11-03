using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;

namespace WordRPG.EditorTools
{
    // 모바일 출시 준비: 플레이어 설정 한 번에 맞추기 + 빌드.
    //  - 아이폰(앱스토어 출시 — 2026-10-03 사용자 결정): Xcode 프로젝트를 만들어 Mac의 Xcode에서 서명·업로드. iOS Build Support 모듈 필요
    //  - 안드로이드(구글플레이 — 2026-11-03 사용자 결정, 1.2부터 앱스토어와 같이): 폰 테스트용 APK / 스토어용 AAB. Android Build Support 모듈 필요.
    //    AAB는 올림 열쇠로 서명 — 열쇠·비밀번호는 저장소 밖, 환경변수 WORDRPG_ANDROID_KEYS = 설정 파일 경로 (Docs/안드로이드출시방법.txt)
    // 배치모드: -executeMethod WordRPG.EditorTools.MobileBuild.BuildIos (또는 BuildAndroidApk, BuildAndroidAab)
    public static class MobileBuild
    {
        // 앱 고유 이름(번들 ID·패키지 이름)은 스토어에 한 번 올리면 바꿀 수 없고, 바꾸면 폰의 세이브 위치도 바뀐다 (2026-10-03 사용자 확정).
        // 회사 이름(companyName)은 건드리지 않는다: PC(에디터) 세이브·설정 위치가 회사·앱 이름으로 정해져서 바꾸면 지금 세이브가 안 보임
        public const string ProductName = "영단어RPG";
        public const string PackageName = "com.arttechsoo.wordrpg";
        public const string IconPath = "Assets/Branding/AppIcon.png";
        public const string IosFolder = "Builds/iOS";
        public const string AndroidFolder = "Builds/Android";

        [MenuItem("WordRPG/Build/Apply Mobile Settings", priority = 1)]
        public static void ApplyMobileSettings()
        {
            PlayerSettings.productName = ProductName; // 폰 홈 화면에 보이는 앱 이름
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, PackageName);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);

            // 세로 고정, 상태 표시줄(시계·배터리) 숨김 — 위쪽 HUD가 가려지지 않게
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.statusBarHidden = true;

            // 아이폰: 아이폰 전용(아이패드에서는 아이폰 화면으로 실행 → 아이패드 스크린샷이 필요 없음),
            // 세로만 쓰는 앱은 '전체 화면 필요'를 켜야 앱스토어 업로드 검사를 통과한다. 서명은 Xcode가 자동으로
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            // 앱스토어에 올릴 때마다 빌드 번호가 전보다 커야 한다 (처음은 1, 다시 올릴 땐 Player Settings > iOS > Build에서 +1)
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
                PlayerSettings.iOS.buildNumber = "1";

            // 안드로이드: 64비트(ARM64) → IL2CPP. 안드로이드 7.1(API 25) 이상.
            // 대상 API 36 = 2026-08-31부터 구글플레이 조건 — 해마다 오르니 '자동' 대신 분명히 적어 둠 (Tools/check_android_bundle.py가 확인)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            // 게임으로 표시: 안드로이드 16의 '큰 화면에서 세로 고정 무시'에서 빠짐 (태블릿·폴더블에서도 세로 고정)
            PlayerSettings.Android.appCategory = "game";
            // 뒤로 가기 키를 게임이 그대로 받음 (Escape → 각 화면 HandleBack). 켜면 API 36에서 뒤로 가기 키가 앱으로 오지 않음
            PlayerSettings.Android.predictiveBackSupport = false;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning($"[WordRPG] 앱 아이콘이 없음: {IconPath}");

            AssetDatabase.SaveAssets();
            Debug.Log($"[WordRPG] 모바일 설정 적용: {ProductName} ({PackageName}), 세로 고정, 아이폰 전용·전체 화면, 안드로이드 ARM64·최소 API 25");
        }

        // 결과 = Builds/iOS 폴더의 Xcode 프로젝트 → Mac으로 옮겨 Unity-iPhone.xcodeproj 열기 → 팀 선택 → 실행 또는 Archive → 앱스토어 업로드
        [MenuItem("WordRPG/Build/iOS Xcode 프로젝트 (앱스토어용, Mac에서 열기)", priority = 20)]
        public static void BuildIos()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Fail("iOS Build Support 모듈이 없어요. Unity Hub > 설치 > 6000.3.11f1 > 모듈 추가에서 " +
                     "iOS Build Support를 설치한 뒤 다시 해 주세요. (만든 Xcode 프로젝트는 Mac에서 열어야 해요)");
                return;
            }
            Build(BuildTarget.iOS, BuildTargetGroup.iOS, IosFolder);
        }

        [MenuItem("WordRPG/Build/Android APK (폰 테스트용)", priority = 40)]
        public static void BuildAndroidApk() => BuildAndroid(false);

        [MenuItem("WordRPG/Build/Android AAB (구글 플레이용)", priority = 41)]
        public static void BuildAndroidAab() => BuildAndroid(true);

        private static void BuildAndroid(bool appBundle)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Fail("Android Build Support 모듈이 없어요. Unity Hub > 설치 > 6000.3.11f1 > 모듈 추가에서 " +
                     "Android Build Support(OpenJDK, Android SDK & NDK Tools 포함)를 설치한 뒤 다시 해 주세요.");
                return;
            }
            if (!ApplyAndroidSigning(appBundle)) return;
            EditorUserBuildSettings.buildAppBundle = appBundle;
            Build(BuildTarget.Android, BuildTargetGroup.Android,
                $"{AndroidFolder}/WordRPG-{PlayerSettings.bundleVersion}{(appBundle ? ".aab" : ".apk")}");
        }

        // 올림 열쇠로 서명. 설정 파일(storeFile=, storePassword=, keyAlias=, keyPassword=)은 저장소 밖에 두고 경로만 환경변수로 받는다.
        // 비밀번호는 PlayerSettings 메모리에만 (ProjectSettings 파일에 저장되지 않음). AAB는 꼭 필요, APK는 없으면 임시 디버그 서명
        private static bool ApplyAndroidSigning(bool required)
        {
            string path = Environment.GetEnvironmentVariable("WORDRPG_ANDROID_KEYS");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                if (!required) return true;
                Fail("구글플레이용 AAB에는 올림 열쇠가 필요해요. 환경변수 WORDRPG_ANDROID_KEYS에 설정 파일 경로를 넣어 주세요 (Docs/안드로이드출시방법.txt).");
                return false;
            }
            var values = File.ReadAllLines(path).Select(line => line.Split(new[] { '=' }, 2)).Where(pair => pair.Length == 2)
                .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim());
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = values["storeFile"];
            PlayerSettings.Android.keystorePass = values["storePassword"];
            PlayerSettings.Android.keyaliasName = values["keyAlias"];
            PlayerSettings.Android.keyaliasPass = values["keyPassword"];
            return true;
        }

        private static void Build(BuildTarget target, BuildTargetGroup group, string location)
        {
            ApplyMobileSettings();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].EndsWith("Title.unity"))
            {
                Fail("빌드 씬 순서가 맞지 않아요. WordRPG > Scenes > Create All Scenes를 먼저 실행해 주세요 (첫 씬 = 타이틀).");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(location) ?? ".");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail($"빌드 실패 ({report.summary.result}, 오류 {report.summary.totalErrors}개) — Console 창을 확인해 주세요.");
                return;
            }
            Debug.Log($"[WordRPG] 빌드 완료: {location} ({report.summary.totalSize / (1024f * 1024f):0.0} MB)");
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(location);
        }

        // 아이폰 빌드 뒤: Info.plist에 '암호화 안 씀'을 적어 둔다 → 앱스토어에 올릴 때마다 수출 규정 질문을 받지 않음
        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plist = Path.Combine(path, "Info.plist");
            if (File.Exists(plist)) File.WriteAllText(plist, WithNoEncryption(File.ReadAllText(plist)));
        }

        // 이 게임은 통신·암호화를 쓰지 않는다. 이미 있으면 그대로
        public static string WithNoEncryption(string plistXml)
        {
            const string key = "<key>ITSAppUsesNonExemptEncryption</key>";
            if (plistXml.Contains(key)) return plistXml;
            int end = plistXml.LastIndexOf("</dict>", StringComparison.Ordinal);
            if (end < 0) return plistXml;
            // 맨 바깥 </dict> 줄 바로 위에, 그 줄보다 두 칸 더 들여 써서 넣는다
            int lineStart = plistXml.LastIndexOf('\n', end) + 1;
            string indent = plistXml.Substring(lineStart, end - lineStart);
            if (indent.Trim().Length > 0)
            {
                lineStart = end;
                indent = "";
            }
            string child = indent + "  ";
            return plistXml.Insert(lineStart, $"{child}{key}\n{child}<false/>\n");
        }

        // 메뉴에서는 알림 창, 배치모드에서는 예외 (종료 코드 1)
        private static void Fail(string message)
        {
            Debug.LogError("[WordRPG] " + message);
            if (Application.isBatchMode) throw new BuildFailedException(message);
            EditorUtility.DisplayDialog("빌드할 수 없어요", message, "확인");
        }
    }
}
