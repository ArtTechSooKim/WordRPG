# CLAUDE.md — WordRPG (영단어 RPG)

> Claude Code가 세션 시작 시 자동으로 읽는 프로젝트 컨텍스트.
> 결정 사항·게임 규칙·진행 현황은 `Docs/GDD.md`, 원본 기획은 `Docs/PRD.txt`. 큰 기능 작업 전에 GDD를 확인할 것.

## 개발현황보고서 (사용자 요청 — 반드시 지킬 것)

- 세션을 시작하면 루트의 `개발현황보고서.txt`를 먼저 읽고 어디까지 했는지 파악한다
- **작업을 할 때마다** 같은 파일을 갱신한다 (작업을 마치고 사용자에게 보고하기 전에):
  1. 맨 위 `[현재 상태]`: 마지막 업데이트 날짜, 마지막 커밋, 지금 해볼 수 있는 것, PRD STEP 진행도, 아직 안 되는 것
  2. `[다음 작업]`, `[사용자가 직접 해야 할 일]`: 끝난 항목은 지우거나 [x] 처리, 새 항목 추가
  3. `[작업 로그]` 맨 위에 새 항목: `[날짜] #번호 제목 (커밋 해시)` + 무엇을 했는지, 검증 결과, 남은 문제
- 사용자가 나중에 읽고 이어서 시작할 수 있게 쉬운 한국어로, 코드 용어는 최소화. 형식은 일반 텍스트(.txt)

## 사용자와 정한 작업 방식

- 작업이 끝나면 테스트 → 보고서 갱신 → **커밋·푸시까지** 한다 (사용자가 권한을 줌). 사용자에게는 한국어로 짧게 보고
- 화면(UI)은 **Figma에 먼저 그리고** 게임에 옮긴다. 에셋 팩에 없는 UI도 Figma로 그려서 맞춘다
- 맵·시스템은 **나중 기능(미니맵 등)까지 고려**해서 만든다 — 맵은 글자 데이터 유지, 저장이 필요한 기능은 일찍 결정 받기
- 그림·소리는 Ninja Adventure 팩. 몬스터 이름·컨셉은 유지하고 비슷한 그림을 씌우며, 맞는 그림이 없으면 임시 도형으로 둔다
- 대화 기록은 이어지지 않으므로 Figma 노드 id·캡처 방법 같은 작업 메모는 `Docs/작업메모.md`에 남긴다

## 프로젝트 개요

- **장르**: 주인공 + 성유물 수집·강화 턴제 RPG + 영단어 학습. "공부하려고 켜는 게임"이 아니라 "게임하다 보니 단어를 외우고 있는 게임"
  (2026-10-02 사용자 결정으로 몬스터 3마리 육성 → 주인공 혼자 + 성유물. 몬스터는 적으로만)
- **엔진**: Unity 6000.3.11f1 (2D, Built-in RP) / **플랫폼**: 모바일, **세로 고정**, **아이폰 앱스토어 출시** (2026-10-03 사용자 결정, 안드로이드는 시험용 빌드만) / **개발**: 1인
- **핵심 루프**: 탐험 → 랜덤 조우 → 전투(기술마다 단어 문제) → 보상(경험치·재료·성유물) → 레벨업·성유물 강화(+3 각성) → 새 지역·어려운 단어
- **현재 단계**: MVP 이후 확장 — 초원(마을)·잊혀진 서고(던전)·숲(2번째 지역, 서고 보스를 물리치면 열림), 주인공 + 성유물 8개(3칸 장착),
  적 6종 + 보스 2, 단어 1단계 100개 + 2단계 100개. 맵은 2026-10-03에 넓이 4배

## 기술 규칙 (반드시 준수)

1. **게임 데이터는 ScriptableObject**: 주인공(HeroData), 성유물(RelicData), 적 몬스터, 스킬, 아이템, 출현표, 단어장. 하드코딩 금지
   - 단어는 `Assets/Data/Words/*.csv`를 고치면 같은 이름의 `WordDatabase` .asset이 자동 갱신됨
2. **로직과 UI 분리**: `Words/ Monsters/ Heroes/ Items/ Battle/`은 순수 C# (MonoBehaviour 없음). UI는 `BattleEngine`이 돌려주는 `BattleEvent` 목록을 연출만 한다
3. **새 로직에는 EditMode 테스트**: `Assets/Tests/EditMode/`. SO는 `TestData` 도우미로 생성
4. **시간·랜덤은 주입**: 로직에서 `DateTime.UtcNow`, `UnityEngine.Random` 직접 사용 금지 → `DateTime nowUtc` / `System.Random` 파라미터로 받기 (테스트 결정성)
5. **저장 데이터는 id 문자열로 참조**: wordId, itemId, relicId, areaId. 에셋 이름이 바뀌어도 세이브가 깨지지 않게 (세이브 v2: 주인공 + 성유물, v1 몬스터 파티는 불러올 때 변환)
   - 아이템·성유물·몬스터·지역을 새로 만들면 `WordRPG > Data > Refresh Game Database` 실행 (안 하면 세이브에서 불러올 수 없음, DataIntegrityTests가 잡음)
   - 성유물은 시작·보물상자(ChestContent.relic)·보스(BossEncounter.rewardRelic) 중 하나에서 얻을 수 있어야 하고, 강화 재료는 적 드롭·상자에서 얻을 수 있어야 함.
     상점은 회복 아이템(상처약)만 판다 — 사용자 결정 (TownDataTests가 검사)
   - 기술문서(ItemKind.SkillDocument)는 taughtSkill이 있어야 하고 상자·보스(BossEncounter.rewardItem)에서 얻을 수 있어야 함 (SkillDocumentDataTests).
     전투 기술 = 기본 기술(고정) + 기술 칸 3개(성유물 기술·기술문서 기술), 세이브에는 칸 순서를 키 문자열로 저장
   - 이미 출시된 id는 바꾸지 말 것. SaveData에 필드 추가는 자유(예전 세이브는 기본값), 기존 필드 의미를 바꿀 때만 version 올리고 변환
   - 게임 진행 상태는 `GameManager.Instance.Session`에서 얻고, 바뀌면 `GameManager.Save()` 호출
   - 세션을 쓰기 시작하는 화면(필드·전투)은 `MarkPlaying()`을 불러야 저장된다 (타이틀에서 시작 전엔 저장 안 함)
   - 설정(`GameSettings`: 음량·진동)은 세이브가 아니라 PlayerPrefs. `GameManager.Settings` / `SaveSettings()`
   - **수익화** (GDD 9-3): 산 상품(`Entitlements`, 정식판 id `com.arttechsoo.wordrpg.full` — 바꾸지 말 것)도 세이브가 아니라 PlayerPrefs.
     결제는 `IStore`(실제 `UnityIapStore` = Unity IAP 5, 테스트는 가짜 — `GameManager.StoreOverride`/`FieldScreen.Configure`).
     정식판이 필요한 지역은 FieldArea `requiresFullVersion` (지금은 숲) → 입구에서 `PaywallView`
   - 새 지역(단어장)은 regionId·regionName·징표 아이템(종류 Keepsake)·골드를 지정해야 함 (RegionDataTests가 검사)
   - 수련용 허수아비는 MonsterSpecies `trainingDummy`(맞아도 HP 그대로, 기술 없음) — 게임에 하나, 출현표에 넣지 말 것 (TrainingTests)
   - 필드 맵은 FieldArea의 글자 맵 (GDD 7·9장). 상자 수 = 내용물 수, 출입구 수 = 연결 수(서로 왕복), B ↔ 보스 지정,
     모든 상자·샘·출입구 도달 가능 (AreaDataTests·DungeonDataTests가 검사). 지역을 새로 만들면 Refresh Game Database
   - 맵 글자 ':' = 잔디(걸을 수 있고 조우 없음). 출입구 연결(AreaExit)의 openedByBossOf = 그 지역 보스를 물리쳐야 열림 →
     보스를 이기면 FieldScreen이 카메라로 그 출입구를 보여 주는 연출(GateCutscene). 잠긴 길은 진짜 보스가 열어야 함 (GateDataTests)
   - 맵 원본은 SampleDataBuilder의 MeadowMap·LibraryMap·ForestMap 상수. 이미 있는 에셋은 Create Sample Data가 덮어쓰지 않으므로
     맵을 바꿀 땐 에셋의 map 칸도 같이 고칠 것 (상자 id는 위치 기준이라 상자를 옮기면 새 상자)
6. **네이밍**: PascalCase 클래스/메서드/프로퍼티, camelCase 필드. SO 필드는 `[SerializeField] private` + 읽기 전용 프로퍼티. 클래스명은 영문, 화면 표시명은 한국어 필드(`displayName = "펜촉이"`). 주석은 한국어
7. **Git LFS 사용 중**: 이미지·오디오·폰트·네이티브 플러그인. 새 바이너리 타입 추가 시 `.gitattributes` 확인 (새로 클론하면 `git lfs install` 먼저)
8. **입력은 Input System 전용**: EventSystem에 `InputSystemUIInputModule` 사용 (`StandaloneInputModule` 금지).
   뒤로가기 = Escape 키 (`FieldScreen.BackPressed`, 안드로이드 뒤로 버튼·PC Esc) → 새 창(패널)을 만들면 각 화면의 `HandleBack`에 닫기 순서를 추가.
   아이폰은 앱이 스스로 꺼지면 안 됨 → 종료는 `GameManager.CanQuit`일 때만.
   새 기능에 처음 하는 사람용 안내가 필요하면 `TutorialProgress`에 id를 더하고 그 상황에서 `TutorialOverlay.Run` (GDD 7-1)
9. **한국어 조사**: 이름 뒤 조사는 `UiKit.WithJosa(name, "이", "가")`로 (펜촉이가 / 책껍질이, 깃펜기사로 / 백과거북으로)
10. **세로 화면 기준 UI**: 1080×1920 레퍼런스, 한 손 조작, 4지선다 버튼은 화면 하단
11. **UI는 Figma UI 키트를 따른다** (https://www.figma.com/design/UUDRmdKgisU6B59saw5gJr): 프리팹 없이 코드로 만들고 `UiKit` 도우미를 쓴다
    - 색은 `Palette`(Figma 변수와 같은 값), 모서리 `UiKit.RadiusSm/Md/Lg`(8/16/24): `RoundPanel`, `Pill`(양끝 완전 둥글게), `Outline`(테두리)
    - 글꼴: 제목·숫자·버튼 = Jua(`UiKit.Display`, `MakeButton` 기본), 본문 = Noto Sans KR(`Label`, Bold 지원). `Assets/Resources/UI/Fonts/` (OFL)
    - 아이콘: `UiKit.Icon("gold")`, 아이템은 `UiKit.ItemIcon(item)`. PNG를 `Assets/Resources/UI/Icons/`에 넣으면 `UiAssetImporter`가 스프라이트로 설정
    - 새 화면은 Figma에 먼저 그리고 같은 컴포넌트로 조립. 기호(✓ ✕ ▲ ◀ 등)는 Jua에 없을 수 있으니 Noto 글꼴로 쓰고 `font.HasCharacter`로 확인
12. **도트 그림·소리는 Ninja Adventure 팩(CC0)**: `python Tools/import_ninja_art.py` 가 쓸 파일만 골라 복사·가공한다 (원본 팩 경로는 스크립트 안)
    - 적 몬스터: `Assets/Resources/Art/NinjaAdventure/Monsters/{speciesId}.png` → `WordRPG > Data > Link Monster Art`(Create All Scenes에도 포함)가 Sprite 칸에 연결
    - 성유물: `Art/NinjaAdventure/Relics/{relicId}.png` (`UiKit.RelicIcon`, RelicData.icon이 비면 사용), 상처약 등 도트 아이템: `Art/NinjaAdventure/Items/{itemId}.png`
    - 주인공 얼굴: HeroData.portrait가 비면 필드 주인공 정면 (`UiKit.HeroPortrait`)
    - 필드 타일 `FieldArt`(Tiles/{테마}_{종류}[_done].png), 주인공 `PlayerArt`(4방향×걷기 4프레임 시트를 코드로 자름, 달리기 = 걷기 + 점프 행 `GetRun`). 없으면 `PlaceholderArt`
    - `Assets/Resources/Art/` 그림은 가져올 때 16px = 1칸, Point 필터 (`UiAssetImporter`)
    - 소리: `Sound.PlayMusic(Music.X)` / `Sound.Play(Sfx.X)`, 파일 = `Resources/Audio/Music|Sfx/{열거형 소문자}`. 음량은 설정을 따름.
      `UiKit.AddButton`/`MakeButton`은 누르면 딸깍 소리 (`clickSound: false`로 끔). 새 소리를 쓰면 열거형 + 스크립트 표에 같이 추가 (AudioArtTests가 검사)
    - 전투: 효과 `BattleFx`(Fx 열거형 = Art/NinjaAdventure/Fx/{소문자}.png 정사각 프레임 시트), 배경은 지역 타일(`BattleScreen.SetBackdrop`).
      필드에서 같은 효과를 월드에 그리려면 `FieldFx.Play`(1 = 한 칸 크기) — 다시 일어남(Heal·Sparkle), 달리기 먼지(Dust)
    - Linear 색공간이라 반투명 검정은 알파를 높게(0.7~0.8) 잡아야 눈에 보이는 만큼 어두워진다
13. **맵은 글자 데이터로만**: 미니맵·지도(`MapViews.cs`)가 FieldMap에서 자동으로 그려진다. 탐험 안개는 `WorldState.Reveal/IsExplored`
    (지역별 비트 기록 `ExploredArea`, 세이브에 포함). 새 맵 글자(타일 종류)를 추가하면
    `MinimapArt.ColorOf`·`FieldArt`·`PlaceholderArt`에도 추가 (MinimapTests·AudioArtTests가 빠진 것을 잡음).
    길·물은 이웃 모양으로 테두리 조각을 고른다 (FieldAutotile → FieldArt.AutoTile, 그림 = Tiles/{테마}_{Floor|Water}_auto.png 256칸)

## 폴더 구조

```
Assets/
  Scripts/              WordRPG.asmdef (런타임)
    Core/               CSV 파서 등 공용
    Words/              단어, 숙련도(VocabularyProgress), 출제(WordSelector), 4지선다(QuizGenerator), 수련 출제(TrainingQuizProvider — 이미 본 단어만)
    Monsters/           적 몬스터 MonsterSpecies·SkillData SO, MonsterInstance, ICombatant(싸우는 것 공통), LevelCurve
    Heroes/             HeroData SO(주인공), Hero(레벨·HP·성유물 3칸·기술 칸 3개 SkillSlot), RelicData SO(성유물: 기술·각성·보너스·강화 비용), RelicUpgrade(강화 규칙)
    Items/              ItemData SO(재료·상처약·징표·기술문서), Inventory, ShopData SO + Shop(구매 규칙)
    Battle/             BattleEngine(기술·강도(단어 n개 연속)·상처약·연속 정답 수), Combo(콤보 단계·글자·추가 피해), BattleFormulas, BattleReward,
                        BattleSummary(한 판 결산: 새 단어·틀린 단어·기술별 피해·최대 콤보 — 결산 화면용)
    Field/              FieldMap(맵 글자→격자), FieldWalker(이동), StickInput(스틱 값→4방향·달리기), EncounterCounter(조우), FieldInteraction([확인] 대상·이름표 규칙), FieldAutotile(길·물가 테두리 모양),
                        FieldArea(지역 SO: 테마·출입구 연결·보스·상자·상점), EncounterTable
    Game/               GameSession(진행 상태 전체), GameManager(씬 간 유지 + 자동 저장 + 설정 + 결제 창구), GameDatabase(id→에셋), PlayerRecord,
                        Entitlements(산 상품) · IStore + UnityIapStore(애플 인앱 결제),
                        Dex(도감 규칙), Keepsakes(징표 진열장), GameSettings(음량·진동), TutorialProgress(본 튜토리얼 id, 세이브에 저장)
    Save/               SaveData(JSON 형식), SaveSystem(임시파일+백업으로 안전 저장)
    UI/                 FieldScreen(필드·지역 이동·HUD·가상 스틱 + 왼쪽 [확인]), BattleScreen(필드 위에 덮이는 전투 + 보스 결정타 연출 + 전투 결산, 단독 연습 모드도 있음),
                        DexView(도감), RelicAltarView(성유물 제단·강화) + AwakeningCutscene(각성 연출), ShopView(상점), SkillLearnView(기술 배우기·바꾸기),
                        InventoryView(소지품: 주인공·성유물·아이템·징표) + HeroViews(HeroInfoPage·RelicPage·RelicSlotsRow·SkillRowView),
                        SettingsView(설정) + ConfirmDialog(확인 창), SwitchView, GateCutscene(보스가 연 길을 보여 주는 연출),
                        TrainingView(수련 창: 전체적 암기·오답 위주 암기 → BattleScreen.BeginTraining, 허수아비),
                        FieldNameTags(오브젝트 이름표),
                        TitleScreen(타이틀), Haptics(진동), UnitView, VirtualStick(가상 스틱 — 누른 자리에 생김, 끝까지 밀면 달리기),
                        TutorialOverlay(튜토리얼: 검은 막 + 뚫린 곳 + 안내 상자, [다음]/직접 해 보기/[건너뛰기]),
                        PaywallView(정식판 안내: 구매·구매 복원·나중에),
                        FieldArt(필드 타일·잠긴/지역별 출입구) · PlayerArt(주인공) · PlaceholderArt(그림이 없을 때 임시 도트), Sound(음악·효과음·징글),
                        BattleFx(전투 효과) + FieldFx(필드 위 효과) + IdleBob, MapViews(미니맵·지도),
                        UiKit(Palette·글꼴·둥근 패널·아이콘 + WithJosa 한국어 조사), AutoPill — 세로 1080x1920
    Editor/             WordRPG.Editor.asmdef — CSV 임포터, 샘플 데이터 생성기, UiAssetImporter(아이콘·도트·소리 가져오기 설정),
                        MonsterArtLinker(몬스터 그림 연결), MobileBuild(모바일 설정·아이폰 Xcode/안드로이드 빌드)
  Resources/UI/         Fonts(Jua, Noto Sans KR + OFL 라이선스), Icons(UI 아이콘 128px), Icons/Items(아이템 256px, 파일명 = itemId)
  Resources/Art/NinjaAdventure/  Monsters(speciesId.png), Relics(relicId.png), Items(itemId.png), Player(Boy 시트), Tiles(합성 타일 + _auto 테두리 아틀라스),
                                 Title(title_scene.png 타이틀 배경 풍경), LICENSE.txt(CC0)
  Resources/Audio/      Music(6곡 ogg), Sfx(21개 wav) — Ninja Adventure. 징글(새 단어 발견·길 열림)은 Sound.PlayJingle(음악 잠깐 멈춤)
  Branding/             AppIcon.png (앱 아이콘 1024px, Figma 'App Icon (#28)')
  Tests/EditMode/       WordRPG.Tests.EditMode.asmdef (로직)
  Tests/PlayMode/       WordRPG.Tests.PlayMode.asmdef (UI 버튼을 눌러 전투 한 판 진행)
  Data/                 Words, Hero(hero.asset), Relics, Monsters(적), Skills, Items, Encounters, Areas, Shops (SO 에셋), GameDatabase.asset
  Scenes/               Title.unity (빌드 첫 씬) → Field.unity (본 게임), Battle.unity (전투만 반복하는 연습 씬)
Docs/                   PRD.txt, GDD.md, 아트에셋목록.md, 아이폰출시방법.txt(TestFlight 올리는 순서), 작업메모.md
.github/workflows/      ios-testflight.yml (GitHub Mac 서버에서 서명·TestFlight 업로드)
Tools/                  import_ninja_art.py (에셋 팩 → 프로젝트, Pillow 필요), zip_ios_for_mac.py (Xcode 프로젝트 → Mac용 zip, 실행 권한 유지)
```

## 명령어

에디터 메뉴: `WordRPG > Data > Import Word CSVs`, `WordRPG > Data > Create Sample Data` (없는 에셋만 생성), `WordRPG > Data > Refresh Game Database`, `WordRPG > Scenes > Create All Scenes` (타이틀·필드·전투 씬 다시 생성 + 빌드 순서), `WordRPG > Save > Delete Save Data / Open Save Folder`,
`WordRPG > Build > Apply Mobile Settings / iOS Xcode 프로젝트 (앱스토어용, Mac에서 열기) / Android APK (폰 테스트용) / Android AAB`
(각 플랫폼 Build Support 모듈 필요, 결과는 `Builds/iOS/`·`Builds/Android/`. 번들 ID `com.arttechsoo.wordrpg` 확정 — 바꾸지 말 것, companyName도 그대로)

게임 실행: `Assets/Scenes/Title.unity`를 열고 Play (Game 뷰를 세로 비율로, 예: 1080x1920). 필드부터 바로: `Field.unity`, 전투만 연습: `Battle.unity`

배치모드 (Unity 에디터가 이 프로젝트를 열고 있으면 실행 불가):

```bash
UNITY="C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe"
# 테스트 (-testPlatform EditMode 또는 PlayMode)
"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml -logFile tests.log
# 아이폰 Xcode 프로젝트 (iOS 모듈 필요) → Mac으로 옮길 zip (Mac 단계는 Docs/아이폰출시방법.txt) / 안드로이드 APK
#   사용자 프로젝트의 플랫폼을 바꾸지 않으려면 Library 없는 복사본에서 빌드 (Docs/작업메모.md)
"$UNITY" -batchmode -quit -projectPath . -buildTarget iOS -executeMethod WordRPG.EditorTools.MobileBuild.BuildIos -logFile build-ios.log
python Tools/zip_ios_for_mac.py [Xcode 프로젝트 폴더, 기본 Builds/iOS]
# Mac 없이 TestFlight로: zip을 릴리스로 올리면 .github/workflows/ios-testflight.yml이 서명·업로드 (gh = %LOCALAPPDATA%/gh-cli/bin/gh.exe)
"$LOCALAPPDATA/gh-cli/bin/gh.exe" release create ios-build-0.1-1 Builds/WordRPG-iOS-0.1-build1.zip --prerelease --title "iOS 빌드 0.1 (1)"
"$UNITY" -batchmode -quit -projectPath . -buildTarget Android -executeMethod WordRPG.EditorTools.MobileBuild.BuildAndroidApk -logFile build-android.log
# 샘플 데이터 + 씬 생성
"$UNITY" -batchmode -quit -nographics -projectPath . -executeMethod WordRPG.EditorTools.SceneBuilder.CreateAllScenes -logFile build.log
```

## 환경 메모

- F: 드라이브는 소유권을 기록하지 않는 파일시스템이라 Git이 `safe.directory` 등록을 요구함 (등록 완료)
- 상위 폴더 `F:\GameProject`는 별개 저장소(Spiritual-Warfare)이므로 거기서 git 명령 실행 주의
- **저장소가 공개**: 애플 키(.p8)·인증서는 .gitignore로 막혀 있음. 키 번호·발급자 ID 같은 값도 저장소 문서에 적지 말 것 (GitHub 비밀값으로만)
