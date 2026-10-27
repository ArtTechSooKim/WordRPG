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
  적 6종 + 보스 2 (+ 1.1 메아리 정글 완성 — 출시 대기, 가지 release-1.1: 숲 보스 공터 위 출입구, 적 3종 + 빈칸두꺼비, 동료 아라)
  (+ 1.2 어두운 밀림 동굴 완성 — main, 1.1 출시 뒤 TestFlight: 정글 강 가운데 섬의 'O' 출입구(입장 조건 없음), 적 4종 + 말줄임덩굴, 설계 GDD 9-7). 단어 = 2022 개정 교육과정 영어과 기본 어휘(교육부 고시, 저작권 없음 — GDD 11-2)에서 **지역마다 150개**(#76: 초원·서고 = 중·고 공통,
  숲 = 고급에서 자주 쓰는 순 — `Tools/trim_wordbooks.py`). 남은 단어는 `Docs/단어_예비_중고공통.csv`(900)·`Docs/단어_예비_고급.csv`(850) — 새 지역은 위에서부터 150개.
  뜻은 Claude 초안 + 사용자 검수. 맵은 2026-10-03에 넓이 4배. 보스 방은 열쇠 + 그 지역 단어 40개 발견으로 열림(#76)

## 기술 규칙 (반드시 준수)

1. **게임 데이터는 ScriptableObject**: 주인공(HeroData), 성유물(RelicData), 적 몬스터, 스킬, 아이템, 출현표, 단어장. 하드코딩 금지
   - 단어는 `Assets/Data/Words/*.csv`를 고치면 같은 이름의 `WordDatabase` .asset이 자동 갱신됨.
     지역마다 자기 단어장, 한 단어는 한 단어장에만 (RegionDataTests). 단어를 늘릴 땐 남의 사이트·사전을 긁어 오지 않는다 (GDD 11-2)
2. **로직과 UI 분리**: `Words/ Monsters/ Heroes/ Items/ Battle/`은 순수 C# (MonoBehaviour 없음). UI는 `BattleEngine`이 돌려주는 `BattleEvent` 목록을 연출만 한다
3. **새 로직에는 EditMode 테스트**: `Assets/Tests/EditMode/`. SO는 `TestData` 도우미로 생성
4. **시간·랜덤은 주입**: 로직에서 `DateTime.UtcNow`, `UnityEngine.Random` 직접 사용 금지 → `DateTime nowUtc` / `System.Random` 파라미터로 받기 (테스트 결정성)
5. **저장 데이터는 id 문자열로 참조**: wordId, itemId, relicId, areaId. 에셋 이름이 바뀌어도 세이브가 깨지지 않게 (세이브 v2: 주인공 + 성유물, v1 몬스터 파티는 불러올 때 변환)
   - 아이템·성유물·몬스터·지역을 새로 만들면 `WordRPG > Data > Refresh Game Database` 실행 (안 하면 세이브에서 불러올 수 없음, DataIntegrityTests가 잡음)
   - 성유물은 시작·보물상자(ChestContent.relic)·보스(BossEncounter.rewardRelic)·반딧돌 보상(FieldArea.glowStoneRelic) 중 하나에서 얻을 수 있어야 하고, 강화 재료는 적 드롭·상자에서 얻을 수 있어야 함.
     상점은 회복 아이템(상처약)만 판다 — 사용자 결정 (TownDataTests가 검사)
   - 기술문서(ItemKind.SkillDocument)는 taughtSkill이 있어야 하고 상자·보스(BossEncounter.rewardItem)에서 얻을 수 있어야 함 (SkillDocumentDataTests).
     전투 기술 = 기본 기술(고정) + 기술 칸 3개(성유물 기술·기술문서 기술), 세이브에는 칸 순서를 키 문자열로 저장
   - 이미 출시된 id는 바꾸지 말 것. SaveData에 필드 추가는 자유(예전 세이브는 기본값), 기존 필드 의미를 바꿀 때만 version 올리고 변환.
     version을 올리면 예전 앱은 그 세이브를 '너무 새로움'으로 보고 건드리지 않음 (GameManager.SaveBlocked, 깨진 세이브와 구분)
   - 게임 진행 상태는 `GameManager.Instance.Session`에서 얻고, 바뀌면 `GameManager.Save()` 호출
   - 동료 (#77, 1.1): 데이터 = `CompanionData` SO(companionId — 출시 뒤 바꾸지 말 것, 얼굴 = Art/NinjaAdventure/Companions/{id}.png). 세이브 칸 companions·partyCompanionId.
     전투는 `GameSession.PartyMembers`(주인공 → 동료 차례), 경험치는 둘 다(`BattleRewardCalculator.ApplyTo`), 이긴 뒤 쓰러진 동료는 HP 1(`AfterBattle`),
     샘·패배 회복(`RestoreHero`)은 동료도. 얻기 = `FieldScreen.MeetCompanion`(조우 연출) — 동료를 새로 만들면 Refresh Game Database (CompanionDataTests)
   - 세션을 쓰기 시작하는 화면(필드·전투)은 `MarkPlaying()`을 불러야 저장된다 (타이틀에서 시작 전엔 저장 안 함)
   - 주인공 이름·모습 (#124, 1.1): 모습 = HeroData `looks`(`HeroLook` lookId — 출시 뒤 바꾸지 말 것, 그림 Art/NinjaAdventure/Player/{id}_sheet·{id}_face,
     import_ninja_art.py HERO_LOOKS) — 모습만 다르고 능력은 같음. 세이브 칸 heroName·heroLook (빈 값 = 아직 안 정함 → 필드에서 한 번 고르는 화면
     `FieldScreen.HeroSetup`, 1.0 세이브도). `Hero.Rename`(규칙 `HeroNames`: 한글·영어·숫자 8글자)·`SetLook`, 그림은 `PlayerArt.SetLook`.
     이름 뒤 조사는 꼭 `UiKit.WithJosa` (플레이어가 지은 이름이라 받침이 정해져 있지 않음)
   - 업데이트 안내 (#115): `UpdateNotices`(세이브 칸 notices) — 예전 세이브로 켠 사람(= 업데이트한 사람)만 타이틀에서 한 번 (`TitleScreen.ShowUpdateNotice`,
     `ConfirmDialog.ShowNotice` [확인] 하나). 새 게임은 모두 본 것으로 시작, 튜토리얼과 따로. 다음 업데이트 안내는 `UpdateNotices.All` 맨 뒤에 새 id로
   - 설정(`GameSettings`: 음량·진동)은 세이브가 아니라 PlayerPrefs. `GameManager.Settings` / `SaveSettings()`
   - **수익화** (GDD 9-3): 산 상품(`Entitlements`, 정식판 id `com.arttechsoo.wordrpg.full` — 바꾸지 말 것)도 세이브가 아니라 PlayerPrefs.
     결제는 `IStore`(실제 `UnityIapStore` = Unity IAP 5, 테스트는 가짜 — `GameManager.StoreOverride`/`FieldScreen.Configure`).
     정식판이 필요한 지역은 FieldArea `requiresFullVersion` (숲·정글·동굴) → 입구에서 `PaywallView`, 어디서든 설정 > 정식판(보기·구매 복원).
     앱 버전(PlayerSettings.bundleVersion)은 앱스토어 버전과 같아야 함 (지금 main = **1.2 개발 중**(동굴, #110). **1.1 출시·고침은 가지 `release-1.1`**에서 빌드하고
     고친 것은 main에도 합침 — main에서 1.1을 빌드하지 말 것. 1.0 심사 코드는 태그 v1.0-build3, 1.0을 고쳐 다시 내야 하면 그 태그에서 가지를 땀). 지원·개인정보 웹 페이지 = `gh-pages` 브랜치 (작업메모)
   - 새 지역(단어장)은 regionId·regionName·징표 아이템(종류 Keepsake)·골드·storyOrder(이야기 순서 = 징표 진열장 순서, 다음 번호)를 지정해야 함 (RegionDataTests가 검사)
   - 수련용 허수아비는 MonsterSpecies `trainingDummy`(맞아도 HP 그대로, 기술 없음) — 게임에 하나, 출현표에 넣지 말 것 (TrainingTests)
   - 필드 맵은 FieldArea의 글자 맵 (GDD 7·9장). 상자 수 = 내용물 수, 출입구 수 = 연결 수(서로 왕복), B ↔ 보스 지정,
     모든 상자·샘·출입구 도달 가능 (AreaDataTests·DungeonDataTests가 검사). 지역을 새로 만들면 Refresh Game Database
   - 맵 글자 'L' = 사전 받침대: 보스가 있는 맵마다 쉼터에 하나, FieldArea `dictionaryName` 필수 (BossDictionaryTests). 하루 한 번 읽기 = `BossDictionary`
   - 맵 글자 'G' = 보스 방 문, 'K' = 열쇠 받침대 (#76): 보스가 있는 맵마다 G 한 덩어리(가운데 칸에 이름표)·K 하나 + FieldArea `bossKey`(종류 Key)·`bossGateWords`(40).
     열쇠 + 그 지역 단어 40개 발견 → [확인]으로 열림 (규칙 `BossGate`, 세이브 `WorldState.openedGates`). 열쇠는 문 밖, 보스는 문 안에서만 닿아야 함 (BossGateDataTests)
   - 맵 글자 ':' = 잔디(걸을 수 있고 조우 없음). 출입구 연결(AreaExit)의 openedByBossOf = 그 지역 보스를 물리쳐야 열림 →
     보스를 이기면 FieldScreen이 카메라로 그 출입구를 보여 주는 연출(GateCutscene). 잠긴 길은 진짜 보스가 열어야 함 (GateDataTests)
   - 맵 원본은 SampleDataBuilder의 MeadowMap·LibraryMap·ForestMap·JungleMap·CaveMap 상수. 이미 있는 에셋은 Create Sample Data가 덮어쓰지 않으므로
     맵을 바꿀 땐 에셋의 map 칸도 같이 고칠 것 (상자 id는 위치 기준이라 상자를 옮기면 새 상자)
   - 조우 방식은 FieldArea `encounterStyle`: Grass(풀숲 확률 조우) / Roaming(보이는 몬스터 — 마른 덤불 ','이 서식지, 발견하면 쫓아옴·부딪히면 전투,
     가까우면 [선제공격] = QTE(기회 슬로모션 → 줄어드는 원 + 이미 본 단어 한 문제), 맞히면 HP 20%. 규칙 `RoamingMonsters`·`Preemptive`, 그림 `FieldRoamerViews`·`PreemptView`, #73~75. 정글부터 — 1.1).
     Roaming 지역도 풀숲 ','을 밟으면 확률 조우 (#123) — 단 보이는 몬스터가 나를 발견·쫓는 중(`RoamingMonsters.HuntingPlayer`, 동굴은 거품 중도)엔 없음,
     전투가 끝나면 2초 동안 새로 알아채지 않음 (`RoamingMonsters.Calm`, `RoamingRules.CalmSeconds`).
     FieldArea `waterEncounters`가 있으면 뗏목이 다니는 강에도 물에 사는 몬스터(`Roamer.Aquatic`, 정글의 흉내문어 #81). 첫 선제공격 기회엔 `PauseTutorial` 설명(#79).
     나루터가 없는 맵(동굴)이면 waterEncounters = 웅덩이에 숨는 몬스터(`Roamer.Lurker`, 먹물도롱뇽 #113): 이어진 물 덩어리마다 1~2마리 숨어 있다가(안 보임)
     2칸 안에 오면 거품(Surfacing 0.8초) → 옆 땅으로 튀어나와 쫓음 → 놓치면 웅덩이로 돌아가 다시 숨음 (RoamingRules Lurk*)
   - 정글 맵 글자 (#80, 1.1): '>' '<' '^' 'v' 얕은 강(걸을 수 있음, 물살 방향으로 걸으면 빨라짐 — `FieldMap.StreamDirection/IsWithStream`) ·
     'J' 나루터(여기서 깊은 강 쪽으로 가면 뗏목 — `FieldWalker.Rafting`, 뗏목이 다니는 물 = 나루터에 이어진 물 `FieldMap.IsRaftWater`, 세이브 `WorldState.Rafting`.
     #121부터 2×2 나무 부두(뭍 한 줄 + 물 쪽 한 줄): 그림 Jungle_Dock_auto(물 쪽만 말뚝), 이름표·묶인 뗏목은 부두마다 하나 `FieldMap.DockGroup`·`MooringSpots`) ·
     'Y' 레버 → 가장 가까운 'H' 덩굴 다리가 놓임(`FieldMap.BridgeOf`, 규칙 `Levers`, 세이브 pulledLevers) · 'U' 유적 돌벽 · ';' 유적 바닥 ·
     'T' 단어 석판(세이브 solvedTablets) · 'A' 유적 제단(FieldArea `shrineCompanion` + `shrineWords` 60, 규칙 `Ruins`) ·
     'O' 동굴 입구 = 출입구(#115 — 'D'와 같이 Door·출입구 번호에 들어가고 그림만 3×3 동굴 `FieldMap.IsCaveMouth` · `FieldArt.CaveMouthProp`).
     정글 칸은 정글 테마에서만 (`FieldTiles` 표의 테마 칸 = `FieldThemes.Supports` — 다른 테마에 쓰려면 거기에 더하고 그림을 만듦). 칸 상태(끝난 모습·지나갈 수 있음)는 `CellState`,
     도달 검사(모든 상자·출입구)는 걷기 + 뗏목 `FieldReach.Flood`. 칸보다 큰 그림(석상·동굴)은 `FieldArt.Prop`(Tiles/{테마}_{종류}_prop.png).
     'Q' 꾸밈(#86, 막힘): 종류는 둘레로 정함 `FieldDecor.Variant` (그림 Tiles/{테마}_Decor_{종류}.png, 1×2는 _prop), 막지 않는 흩뿌림은 `FieldScatter`(Tiles/{테마}_Scatter_{바닥}_n.png).
     정글 맵 원본 = Tools/jungle_map.py (마지막에 widen_maps.widen을 나무만 걷게 해서 한 칸 통로를 넓힘) → Builds/maps/JungleMap.txt를 상수·에셋에 옮김
     → #121 `Tools/widen_river.py`로 큰 강을 넓히고(72 → 79칸) 나루터를 2×2 부두로 (한 번만 — 맵을 처음부터 다시 만들면 이 단계도 다시)
   - 동굴 (#112, 1.2 — GDD 9-7): 테마 `FieldTheme.Cave` = 어두운 지역 (`FieldDarkness.IsDark` = 테마 표 `ThemeLooks.Dark`). 맵 글자 'I' = 글자 반딧돌(규칙 `GlowStones`, 세이브 `WorldState.litStones`),
     동굴 칸 = 기본 칸 + 레버·덩굴 다리·꾸밈·반딧돌 (`FieldTiles` 표). 빛 규칙 `CaveLight`·`DarknessMap`(순수): 주인공 3.5칸 + 동료 횃불 1칸 · 밝힌 반딧돌 6칸 ·
     쉼터 물건(샘·가게·제단·사전) 4칸 · 출입구 2.5칸 · 지나간 곳 Memory. 그리기 `FieldDarkness`(칸마다 2×2픽셀 한 장, 주인공 빛이 1/4칸 움직일 때만 다시 칠함, 알파 1 —
     Linear라 0.97만 돼도 바닥이 비침). 빛 밖 몬스터는 빨간 눈만 (`FieldRoamerViews.UpdateEyes` — 사용자: 적의 눈은 모두 빨간색), 꺼진 반딧돌은 희미하게 깜빡(`FieldScreen.Cave`).
     반딧돌 [확인] = `GlowStoneQuizView`(Figma #111) + `TabletQuiz(provider, 1, EnglishToMeaning)` — 석판과 같은 규칙(틀려도 벌점 없음), 밝히면 빛이 퍼지고(`FieldDarkness.Grow`)
     모두 밝히면 동굴 전체가 은은하게(`CaveLight.AllLitGlow`) + FieldArea `glowStoneRelic`(동굴 = 반딧돌 등잔, `GlowStones.ClaimAllLitReward`).
     목표 한 줄은 열쇠를 찾기 전까지 `ObjectiveKind.LightStones` (별 없음). 동굴 지역 = cave.asset (#115, 정글 출입구 0번 ↔ 동굴 출입구 0번)
     그림 = import_ninja_art.py `build_cave` (Cave_Wall_auto = 바위 윗면 + 밝은 테 + 절벽면), 시안 = Tools/cave_mockup.py
   - 통로는 두 칸 너비 (사용자 요청 #67): 새 맵은 `python Tools/widen_maps.py --only {상수} [--apply]`로 한 칸 통로를 넓힘 (상수·에셋 같이)
6. **네이밍**: PascalCase 클래스/메서드/프로퍼티, camelCase 필드. SO 필드는 `[SerializeField] private` + 읽기 전용 프로퍼티. 클래스명은 영문, 화면 표시명은 한국어 필드(`displayName = "펜촉이"`). 주석은 한국어
7. **Git LFS 사용 중**: 이미지·오디오·영상(mp4, #129)·폰트·네이티브 플러그인. 새 바이너리 타입 추가 시 `.gitattributes` 확인 (새로 클론하면 `git lfs install` 먼저)
8. **입력은 Input System 전용**: EventSystem에 `InputSystemUIInputModule` 사용 (`StandaloneInputModule` 금지).
   뒤로가기 = Escape 키 (`FieldScreen.BackPressed`, 안드로이드 뒤로 버튼·PC Esc) → 새 창(패널)을 만들면 각 화면의 `HandleBack`에 닫기 순서를 추가.
   아이폰은 앱이 스스로 꺼지면 안 됨 → 종료는 `GameManager.CanQuit`일 때만.
   새 기능에 처음 하는 사람용 안내가 필요하면 `TutorialProgress`에 id를 더하고 그 상황에서 `GuideHints.Run` — 글은 한 줄,
   화면을 막지 않고, 손끝(Drag·Tap)·화살표로 보여 주고, 해 보면(DoneWhen) 끝 (GDD 7-1, #61).
   화면을 멈추는 설명(`PauseTutorial`)은 사용자가 원한 곳만 (지금: 정글 첫 [선제공격] #79 · 처음 시작 환영과 첫 승리 뒤 목표 #124 — `PauseTutorial.Tell`).
   처음 시작 흐름 (#124, `FieldScreen.Prologue`): (입장 영상을 아직 못 봤으면 — 새 게임·하던 세이브 모두 한 번 #131, 본 기록 `TutorialProgress.Intro`는
   튜토리얼 다시 보기로 안 지워짐 · 영상 #129 `IntroCinematicView` — Resources/Video/intro.mp4 + 자막표 intro_captions.json,
   `python Tools/build_intro_video.py`가 Flow 컷들을 이어 만듦 — 자막은 스크립트의 CAPTIONS 표(#133 10줄), [건너뛰기]·뒤로가기) → 모습 고르기 → 환영 → 혼잣말 → 깃펜으로 떠오른 이름 → 걷기·달리기 →
   풀숲 가는 길(#133 `ShowWayToGrass`: 카메라가 가장 가까운 풀숲(`FieldReach.PathToNearest`)으로 따라가며 반투명 분신 `FieldGhost`가 잔상을 남기고 걸어 들어감 → 주인공에게 돌아옴) →
   '풀숲으로 가서 몬스터와 싸워 보세요'(알약은 스틱 위 — 주인공을 가리지 않게, 화살표는 목표 한 줄) → 첫 승리 뒤 목표.
   한 번도 싸우지 않은 사람은 풀숲 두 번째 칸에서 반드시 조우 (`FirstFightPending` → `EncounterCounter.GuaranteeWithin`, #133).
   음악: 입장 영상 `Music.Intro` · 캐릭터 고르기 `Music.HeroSelect`(고르면 지역 음악) — #133, 사용자가 찾은 곡은 Licensed 폴더에.
   테스트는 `Configure(..., setup: true, intro: true)`로 켬 (기본 끔 — 실제 씬은 켜짐). 메뉴 빨간 점은 `MenuBadges`, 목표 한 줄은 `Objectives`
9. **한국어 조사**: 이름 뒤 조사는 `UiKit.WithJosa(name, "이", "가")`로 (펜촉이가 / 책껍질이, 깃펜기사로 / 백과거북으로)
10. **세로 화면 기준 UI**: 1080×1920 레퍼런스, 한 손 조작, 4지선다 버튼은 화면 하단
11. **UI는 Figma UI 키트를 따른다** (https://www.figma.com/design/UUDRmdKgisU6B59saw5gJr): 프리팹 없이 코드로 만들고 `UiKit` 도우미를 쓴다
    - 색은 `Palette`(Figma 변수와 같은 값), 모서리 `UiKit.RadiusSm/Md/Lg`(8/16/24): `RoundPanel`, `Pill`(양끝 완전 둥글게), `Outline`(테두리)
    - 위아래로 미는 목록(ScrollRect)은 오른쪽에 스크롤 막대 `UiKit.VerticalScrollbar`(Figma 'Scrollbar', 넘칠 때만 보임 — #122 사용자:
      밀 수 있다는 걸 알 수 없음). 목록 칸 오른쪽을 0.945까지로 비우고 막대는 0.955~0.97 (성유물 제단·단어 도감)
    - 글꼴: 제목·숫자·버튼 = Jua(`UiKit.Display`, `MakeButton` 기본), 본문 = Noto Sans KR(`Label`, Bold 지원). `Assets/Resources/UI/Fonts/` (OFL)
    - 아이콘: `UiKit.Icon("gold")`, 아이템은 `UiKit.ItemIcon(item)`. PNG를 `Assets/Resources/UI/Icons/`에 넣으면 `UiAssetImporter`가 스프라이트로 설정
    - 새 화면은 Figma에 먼저 그리고 같은 컴포넌트로 조립. 기호(✓ ▲ ◀ 등)는 Jua에 없을 수 있으니 Noto 글꼴로 (✕는 Noto에도 없음 → ×)
    - 본문 글꼴 Noto는 한자·가나를 뺀 부분 집합 (#93 앱 크기 — 원본 `Tools/FontSource/`, `python Tools/subset_fonts.py`).
      한글 11,172자는 전부 있음. 단어 뜻·코드 문자열에 새 한자·기호를 쓰면 EditMode `FontCoverageTests`가 잡음 → 스크립트 다시 실행 또는 다른 기호로.
      (`font.HasCharacter`는 에디터에서 OS 글꼴까지 찾아 주므로 글꼴 검사에 못 씀 — 테스트는 cmap 표를 직접 읽음)
12. **도트 그림·소리는 Ninja Adventure 팩(CC0)**: `python Tools/import_ninja_art.py` 가 쓸 파일만 골라 복사·가공한다 (원본 팩 경로는 스크립트 안)
    - 적 몬스터: `Assets/Resources/Art/NinjaAdventure/Monsters/{speciesId}.png` → `WordRPG > Data > Link Monster Art`(Create All Scenes에도 포함)가 Sprite 칸에 연결
    - 성유물: `Art/NinjaAdventure/Relics/{relicId}.png` (`UiKit.RelicIcon`, RelicData.icon이 비면 사용), 상처약 등 도트 아이템: `Art/NinjaAdventure/Items/{itemId}.png`
    - 주인공 얼굴: HeroData.portrait가 비면 필드 주인공 정면 (`UiKit.HeroPortrait`)
    - 필드 타일 `FieldArt`(Tiles/{테마}_{종류}[_done].png — 같은 종류 변형 _v1·_v2…가 있으면 칸 위치로 섞음 `FieldDecor.TileVariant` #98, 예: 서고 책장·바랜 종이), 주인공 `PlayerArt`(4방향×걷기 4프레임 시트를 코드로 자름, 달리기 = 걷기 + 점프 행 `GetRun`). 없으면 `PlaceholderArt`
    - `Assets/Resources/Art/` 그림은 가져올 때 16px = 1칸, Point 필터 (`UiAssetImporter`)
    - 소리: `Sound.PlayMusic(Music.X)` / `Sound.Play(Sfx.X)`, 파일 = `Resources/Audio/Music|Sfx/{열거형 소문자}`. 음량은 설정을 따름.
      `UiKit.AddButton`/`MakeButton`은 누르면 딸깍 소리 (`clickSound: false`로 끔). 새 소리를 쓰면 열거형 + 스크립트 표에 같이 추가 (AudioArtTests가 검사)
    - **유료 라이선스 소리(Envato Elements)**: `Resources/Audio/Licensed/{같은 이름}.wav`에 두면 팩 소리보다 먼저 쓰임. 이 폴더·받은 원본(`*-utc/`)·
      라이선스 증명서는 .gitignore — 공개 저장소에 올리면 재배포라 안 됨. 저장소엔 팩 소리가 대신 있음 (지금: newword, 작업메모 #66). 음악도 같은 방식 (#133)
    - 전투: 효과 `BattleFx`(Fx 열거형 = Art/NinjaAdventure/Fx/{소문자}.png 정사각 프레임 시트), 배경은 지역 타일(`BattleScreen.SetBackdrop`).
      **기술마다 연출** (#137, 1.2): `SkillData.fx`(`SkillFx` — 에셋에 번호로 저장, 새 종류는 맨 뒤) → UI 표 `SkillLooks`(몸짓 Lunge/Cast · 효과 그림·색·크기 ·
      날아가는 그림 · 화면 번쩍 · 흔들림 · 특수 Letters/Highlight/Rings · 빛 색 · 회복 알갱이) → 그리기 `BattleScreen.SkillFx`(겹쳐 돌려서 기다리는 시간은 그대로).
      회복 = 발밑 마법진·빛기둥·알갱이·카드 빛·HP 막대가 차오름, 보호막 = 막이 튕김 + 남아 있는 동안 카드 테두리(`UnitView.PulseShield`) + 물결·깨짐.
      새 기술은 `SampleDataBuilder.SkillFxById`와 에셋 fx를 같이 (SkillLooksTests: Auto 금지·회복/보호막/공격 종류와 맞는지·표·그림 빠짐)
      필드에서 같은 효과를 월드에 그리려면 `FieldFx.Play`(1 = 한 칸 크기, tint로 색 입히기) — 다시 일어남(Heal·Sparkle),
      달리기 먼지(Dust = 회색조 그림 × 바닥 색 `FieldArt.DustColor` — 새 바닥 타일을 만들면 색도 정할 것).
      지역 사이 길의 출입구(초원↔숲↔정글, #120) = 옆 길과 이어진 바닥(`FieldAutotile.DoorFloorMask`, 맵 밖으로도 이어짐) + 맵 끝에서 비치는 빛
      (`FieldArt.ExitGlow` 색 — 새 지역 길을 잇으면 거기에 더함). 서고 입구·동굴처럼 원래 그림을 쓰는 출입구는 null
    - Linear 색공간이라 반투명 검정은 알파를 높게(0.7~0.8) 잡아야 눈에 보이는 만큼 어두워진다
13. **맵은 글자 데이터로만**: 미니맵·지도(`MapViews.cs`)가 FieldMap에서 자동으로 그려진다. 탐험 안개는 `WorldState.Reveal/IsExplored`
    (지역별 비트 기록 `ExploredArea`, 세이브에 포함). **맵 글자 표 `FieldTiles`** (#116): 칸 종류마다 한 줄 — 글자·걷기·몬스터가 걷는 땅·[확인]·이름표(기본 글자)·
    끝난 모습·끝나면 열림·쉼터 물건·쓰는 테마. 게임 규칙은 이 표만 보고, 화면 모습은 UI 쪽 표가 칸 종류로 찾는다 (미니맵 색 = `MinimapArt` 테마별 표, 먼지 색 = `FieldArt.DustColor` 표).
    새 칸 종류 = `FieldTile` 맨 뒤 + 표 한 줄 + 그림 Tiles/{테마}_{종류}.png + 미니맵 색 + `PlaceholderArt` 한 줄
    (+ [확인]이면 `FieldScreen` Interact, 끝난 모습이면 `CellState.IsDone`). 빠진 것은 FieldTilesTests·AudioArtTests가 잡음.
    **테마 표 `ThemeLooks`** (#118, UI): 테마마다 화면 모습 한 줄 — 음악·맵 밖 색·전투 배경 바닥·바닥 안개(`FieldFog`)·어둠(`FieldDarkness.IsDark`). 값만 담고 그리는 일은 각 클래스.
    새 테마 = `FieldTheme` 맨 뒤(에셋에 번호로 저장) + ThemeLooks 한 줄 + `MinimapArt`·`FieldArt.DustColor` 표 한 줄씩 + 그림·음악(`Music` 열거형).
    빠지면 ThemeTableTests가 잡음 (예전엔 초원 모습으로 조용히 보였음). 새 칸·테마를 넣고 EditMode 테스트를 돌리면 실패 글이 채울 곳을 알려 줌 (#118 실험: 사막 테마 + 모래 칸 → 7개)
    길·물은 이웃 모양으로 테두리 조각을 고른다 (FieldAutotile → FieldArt.AutoTile, 그림 = Tiles/{테마}_{Floor|Water}_auto.png 256칸).
    벽·풀숲도 그림이 있는 테마만: 초원 덤불 Meadow_Wall_auto(짙은 덤불 + 그늘 바탕, #100) · 초원 풀숲 Meadow_Grass_auto(풀숲이 아닌 쪽은 풀잎 모양 테두리, #102) ·
    서고 바닥 안개 = 몬스터 칸은 돌 + 위에 이어진 안개 한 장 `FieldFog`(그림은 규칙 `FogMap`이 맵에서 바로 — 구름 무늬, 벽엔 안 번짐, 쓰는 테마·색 = 테마 표 `ThemeLooks.Fog` #103·#118).
    구름 무늬는 천천히 흘러감(초당 1px = 1초에 한 번만 다시 칠함 — FogMap.Prepare 자리 고정 + Paint 무늬, #104·#105).
    필드 그림을 바꿀 땐 벽과 땅이 한눈에 구분되는지 실제 맵 시안으로 먼저 확인 (사용자 #100)

## AI가 코드를 고칠 때 확인 방법 (#117 분석 → #118 사용자와 정함)

테스트 통과가 곧 안전은 아니다. 고치는 곳에 따라 확인을 더 한다 (#117: 규칙 코드에 버그 12개를 심어 보니 테스트가 다 잡음 — 화면·세이브·결제는 그 밖)
- **게임 규칙** (Battle·Words·Heroes·Items·Monsters, Field 규칙·FieldTiles, Game의 규칙 클래스): 고치고 EditMode 테스트면 충분
- **동작은 그대로 두는 리팩터링**: 바꾸기 전 코드를 임시 테스트에 그대로 옮겨 새 코드와 모든 입력(모든 칸·테마·실제 맵)에서 비교 → 같으면 그 임시 테스트는 지움 (#117·#118).
  테스트가 정말 잡는지 의심되면 버그를 몇 개 일부러 심어 보고 되돌림 (못 잡으면 테스트를 더함)
- **세이브·id·버전**: 테스트 통과로 끝내지 않음 — 출시된 id(wordId·itemId·relicId·areaId·companionId·상품 id)가 그대로인지 git diff로 보고,
  SaveData 필드 의미를 바꾸면 version + 변환 + 예전 세이브 불러오기 테스트 (#89 방식)
- **결제**: 가짜 상점 테스트만으로 안전하다고 보지 않음 → 결제 코드를 바꾸면 보고서 [사용자가 직접 해야 할 일]에 TestFlight 결제·구매 복원 확인을 넣음
- **화면·연출**: 테스트는 결과만 봄 → 바꾸기 전·후 캡처를 `_캡처/`에 남겨 비교 (사용자 확인). 전투 사건 연출은 PlayMode `BattleEventPlaybackTests`가 순서·빠짐·기다리는 횟수를 못 박음 (#119 —
  연출을 일부러 바꾸면 그 기대 순서도 같이 고치고 무엇이 왜 바뀌는지 보고. 기록 = `BattleScreen.CueRecorder`·`Sound.Recorder`, 테스트용·평소엔 null)
- **빌드·출시 설정** (번들 ID·앱 버전·release-1.1 가지·ProjectSettings·워크플로): 사용자 확인 후

## 폴더 구조

```
Assets/
  Scripts/              WordRPG.asmdef (런타임)
    Core/               CSV 파서 등 공용
    Words/              단어, 숙련도(VocabularyProgress), 출제(WordSelector), 4지선다(QuizGenerator), 수련 출제(TrainingQuizProvider — 이미 본 단어만)
    Monsters/           적 몬스터 MonsterSpecies·SkillData SO(+ 전투 연출 종류 SkillFx #137), MonsterInstance, ICombatant(싸우는 것 공통), LevelCurve
    Heroes/             HeroData SO(주인공), Hero(레벨·HP·성유물 3칸·기술 칸 3개 SkillSlot), RelicData SO(성유물: 기술·각성·보너스·강화 비용), RelicUpgrade(강화 규칙),
                        CompanionData SO + Companion(동료 #77: 레벨·경험치·HP, 기본 기술 + 레벨에 배우는 기술, 성유물 없음 — 파티 = 주인공 + 동료 1명)
    Items/              ItemData SO(재료·상처약·징표·기술문서), Inventory, ShopData SO + Shop(구매 규칙)
    Battle/             BattleEngine(기술·강도(단어 n개 연속)·상처약·도망(Flee, 보스전 canFlee=false — 몬스터 등급 MonsterSpecies.fleeTier: 약함 바로·보통 1/4·강함 늘 단어 문제 #84)·연속 정답 수,
                        적 회복은 HP 절반 아래·한 전투 두 번까지 #88, 아군 보호막은 다 깎일 때까지 전투 내내 #120,
                        힘 모으기 = SkillData.chargeTurns → 고른 차례엔 BattleEvent.Charging만, 다음 차례에 발동 #120 — 까먹대왕 기억상실), 경험치는 레벨 차이 비율 BattleConfig.ExpFactor(25%씩·최소 5% #88) —
                        숫자를 바꾸면 EditMode BalanceSimulationTests(정글 전투·보스) 확인, 표는 [Explicit] BalanceReport, Combo(콤보 단계·글자·추가 피해), BattleFormulas, BattleReward,
                        BattleSummary(한 판 결산: 새 단어·틀린 단어·기술별 피해·최대 콤보 — 결산 화면용), Preemptive(선제공격: HP 20%·이미 본 단어만)
    Field/              FieldMap(맵 글자→격자), FieldTiles(맵 글자 표 — 칸 종류마다 글자·성질·이름표·테마, #116), FieldWalker(이동), StickInput(스틱 값→4방향·달리기), EncounterCounter(조우), FieldInteraction([확인] 대상·이름표 규칙), FieldAutotile(길·물가 테두리 모양),
                        FieldArea(지역 SO: 테마·출입구 연결·보스·상자·상점·조우 방식·물의 몬스터·유적 동료), EncounterTable, RoamingMonsters(보이는 몬스터: 어슬렁·발견·쫓기·포기·부딪힘),
                        FieldReach(시작 칸에서 갈 수 있는 칸 — 걷기 + 뗏목, 문·다리를 연/닫은 기준, #80), CaveLight(동굴 빛 규칙 + DarknessMap — #112)
    Game/               GameSession(진행 상태 전체), GameManager(씬 간 유지 + 자동 저장 + 설정 + 결제 창구), GameDatabase(id→에셋), PlayerRecord,
                        Entitlements(산 상품) · IStore + UnityIapStore(애플 인앱 결제),
                        Dex(도감 규칙), Keepsakes(징표 진열장), GameSettings(음량·진동), TutorialProgress(본 튜토리얼 id, 세이브에 저장),
                        Objectives(목표 한 줄) · MenuBadges(메뉴 빨간 점) · BattleUnlocks(강도 고르기는 전투 2번 뒤),
                        BossDictionary(보스의 사전: 받침대 L, 하루 한 번 새 단어 — 읽은 날은 WorldState),
                        BossGate(보스 방 문: 열쇠 K + 단어 40개 → G가 열림 — #76),
                        Ruins(유적: 석판 T 4개 + 정글 단어 60개 → 제단 A에서 동료) · Levers(레버 Y → 덩굴 다리 H) · CellState(칸의 끝난 모습·지나갈 수 있음) — #80,
                        TabletQuiz(석판 3문제: 틀리면 벌점 없이 다시, 숙련도는 처음 답만 — #81), GlowStones(글자 반딧돌: 밝힘·밝힌 수·모두 밝힌 보상 — #112·#115),
                        UpdateNotices(업데이트 안내: 업데이트한 사람만 타이틀에서 한 번 — #115)
    Save/               SaveData(JSON 형식), SaveSystem(임시파일+백업으로 안전 저장)
    UI/                 FieldScreen(필드·지역 이동·HUD·가상 스틱 + 왼쪽 [확인]), BattleScreen(필드 위에 덮이는 전투 + 보스 결정타 연출 + 전투 결산, 단독 연습 모드도 있음)
                        — 둘 다 partial로 나눔 (#93): FieldScreen.cs(상태·이동·입력) + .Places([확인] 장소)·.Jungle(정글 기믹)·.Battles(조우·전투 뒤·수련)·
                        .Roamers(보이는 몬스터·선제공격)·.World(타일 그리기)·.Hud(HUD·안내·메뉴)·.Cave(동굴 어둠·반딧돌 #112) / BattleScreen.cs(전투 흐름) + .Menus·.Result·.Fx·.Layout·.Events(사건 연출: 사건 종류마다 Show… 함수 — #119)·.SkillFx(기술 연출: 몸짓·회복·보호막·특수 — #137).
                        새 기능은 맞는 파일에, 기능 묶음이 커지면 새 partial 파일로,
                        DexView(도감), RelicAltarView(성유물 제단·강화) + AwakeningCutscene(각성 연출), ShopView(상점), SkillLearnView(기술 배우기·바꾸기),
                        InventoryView(소지품: 주인공·성유물·아이템·징표) + HeroViews(HeroInfoPage·RelicPage·RelicSlotsRow·SkillRowView),
                        HeroSetupViews(처음 시작: CharacterSelectView 모습 8명 · NameEntryView 이름 — #124) · IntroCinematicView(입장 영상 + 자막 + [건너뛰기] — #129),
                        SettingsView(설정) + ConfirmDialog(확인 창 · ShowNotice = [확인] 하나), SwitchView, GateCutscene(보스가 연 길을 보여 주는 연출),
                        TrainingView(수련 창: 전체적 암기·오답 위주 암기 → BattleScreen.BeginTraining, 허수아비),
                        SpeechBubble(주인공 머리 위 말풍선 — 팩 DialogInfo·Emote: 점점점·느낌표 풍선, 글자 풍선. 사전 읽기·혼잣말),
                        FieldNameTags(오브젝트 이름표),
                        TitleScreen(타이틀), Haptics(진동), UnitView, VirtualStick(가상 스틱 — 누른 자리에 생김, 끝까지 밀면 달리기),
                        GuideHints(튜토리얼: 화면을 막지 않는 한 줄 알약 + 손끝 끌기·톡톡·화살표, 해 보면 끝 — #61),
                        PauseTutorial(예외로 화면을 멈추는 단계 설명 — 정글 첫 선제공격 기회 #79, 강조할 버튼만 막 위로),
                        TabletQuizView(단어 석판 문제 창 — #81) · FieldStreams(얕은 강 물결선 — #81) · FieldDarkness(동굴 어둠 한 장 — #112) ·
                        GlowStoneQuizView(글자 반딧돌 문제 창 — #113),
                        PaywallView(정식판 안내: 구매·구매 복원·나중에), CompanionViews(가방 > 동료 탭 CompanionPage · 동료 조우 연출 CompanionCutscene — #77),
                        FieldFollower(필드에서 주인공 자취를 한 칸 뒤 따라오는 동료 그림 — 보이기만 함, 칸 영향 없음 #78) · CharacterSheet(주인공과 같은 배치의 캐릭터 시트 자르기), FieldRoamerViews(필드 위 보이는 몬스터 그림·'!') · PreemptView([선제공격] QTE: 집중선·줄어드는 원·2×2 선택지·결과 도장 + TargetLock 조준 괄호),
                        UiRing(두께가 일정한 원 테두리 — 코드로 만들 땐 CanvasRenderer를 먼저 붙임),
                        UiMotion(숫자 굴러 올라가기·톡 커지기·떠오르는 글자 — 결산 능력치·골드, HUD 골드 #127),
                        ThemeLooks(테마 표: 테마마다 음악·맵 밖 색·전투 바닥·안개·어둠 — #118), SkillLooks(기술 연출 표: SkillFx마다 몸짓·효과·색 — #137),
                        FieldArt(필드 타일·잠긴/지역별 출입구) · PlayerArt(주인공) · PlaceholderArt(그림이 없을 때 임시 도트), Sound(음악·효과음·징글),
                        BattleFx(전투 효과) + FieldFx(필드 위 효과) + IdleBob, MapViews(미니맵·지도),
                        UiKit(Palette·글꼴·둥근 패널·아이콘 + WithJosa 한국어 조사), AutoPill — 세로 1080x1920
    Editor/             WordRPG.Editor.asmdef — CSV 임포터, 샘플 데이터 생성기, UiAssetImporter(아이콘·도트·소리 가져오기 설정),
                        MonsterArtLinker(몬스터 그림 연결), MobileBuild(모바일 설정·아이폰 Xcode/안드로이드 빌드)
  Resources/UI/         Fonts(Jua, Noto Sans KR + OFL 라이선스), Icons(UI 아이콘 128px) — 아이템 그림은 모두 도트 Art/NinjaAdventure/Items (#96: 매끈한 256px 4개를 도트로 통일.
                        Icons/Items/{itemId}.png를 두면 그게 먼저 쓰이지만 그림체가 섞이니 쓰지 말 것)
  Resources/Art/NinjaAdventure/  Monsters(speciesId.png), Relics(relicId.png), Items(itemId.png), Player(Boy 시트), Tiles(합성 타일 + _auto 테두리 아틀라스 + _prop 큰 그림),
                                 Field(raft.png 뗏목 · ripple.png 물살 물결 — 필드 위 그림, #80),
                                 Title(title_scene.png 타이틀 배경 풍경), LICENSE.txt(CC0)
  Resources/Audio/      Music(6곡 ogg), Sfx(21개 wav) — Ninja Adventure. 징글(새 단어 발견·길 열림)은 Sound.PlayJingle(음악 잠깐 멈춤)
  Branding/             AppIcon.png (앱 아이콘 1024px, Figma 'App Icon (#28)')
  Tests/EditMode/       WordRPG.Tests.EditMode.asmdef (로직)
  Tests/PlayMode/       WordRPG.Tests.PlayMode.asmdef (UI 버튼을 눌러 전투 한 판 진행)
  Data/                 Words, Hero(hero.asset), Relics, Monsters(적), Skills, Items, Encounters, Areas, Shops (SO 에셋), GameDatabase.asset
  Scenes/               Title.unity (빌드 첫 씬) → Field.unity (본 게임), Battle.unity (전투만 반복하는 연습 씬)
Docs/                   PRD.txt, GDD.md, 아트에셋목록.md, 아이폰출시방법.txt(TestFlight 올리는 순서), 작업메모.md
.github/workflows/      ios-testflight.yml (GitHub Mac 서버에서 서명·TestFlight 업로드)
Tools/                  import_ninja_art.py (에셋 팩 → 프로젝트, Pillow 필요), zip_ios_for_mac.py (Xcode 프로젝트 → Mac용 zip, 실행 권한 유지),
                        widen_maps.py (맵의 한 칸 통로 → 두 칸), subset_fonts.py (본문 글꼴에서 한자·가나 빼기 — 앱 크기, fontTools 필요),
                        pixellab_client.py (PixelLab AI 도트: Ninja 그림을 예시로 16px 64장·4방향 — 키는 환경변수, 저장소에 적지 말 것, 결과는 _캡처/PixelLab #94), trim_wordbooks.py (지역 단어장 150개로 + 예비 단어장, wordfreq 필요), desert_map.py (3장 사막 144×176 맵 생성 + 미리보기), jungle_map.py (1.1 메아리 정글 72×88 맵 초안 + 미리보기), cave_map.py (1.2 어두운 밀림 동굴 50×62 맵 초안 + 미리보기·어둠 미리보기, GDD 9-7), cave_mockup.py (동굴 그림 시안: 팩 타일 임시 합성 + 어둠 → _캡처/111_동굴시안, #111), store_screenshots.py (앱스토어 스크린샷 = PlayMode StoreScreenshots[Explicit] 화면 + 문구),
                        build_intro_video.py (입장 영상: Flow 컷 이어 붙이기·Veo 표시 잘라냄·자막표 → Resources/Video, imageio-ffmpeg 필요, #129 —
                        컷 원본은 저장소 밖 Builds/cinematic_pixellab/Videos, release-1.1 폴더에선 그 경로를 인자로),
                        cinematic_storyboard.py (입장 시네마틱 7컷 = 영화 구도 스케치 → Builds/cinematic_v2, Figma 'Cinematic (#128)' — Flow img to img로 영상, #128)
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
# Mac 없이 TestFlight로: zip을 릴리스로 올리면 .github/workflows/ios-testflight.yml이 서명·업로드 (gh = C:\Program Files\GitHub CLI, PATH에 있음)
gh release create ios-build-0.1-1 Builds/WordRPG-iOS-0.1-build1.zip --prerelease --title "iOS 빌드 0.1 (1)"
"$UNITY" -batchmode -quit -projectPath . -buildTarget Android -executeMethod WordRPG.EditorTools.MobileBuild.BuildAndroidApk -logFile build-android.log
# 샘플 데이터 + 씬 생성
"$UNITY" -batchmode -quit -nographics -projectPath . -executeMethod WordRPG.EditorTools.SceneBuilder.CreateAllScenes -logFile build.log
```

## 환경 메모

- F: 드라이브는 소유권을 기록하지 않는 파일시스템이라 Git이 `safe.directory` 등록을 요구함 (등록 완료)
- 상위 폴더 `F:\GameProject`는 별개 저장소(Spiritual-Warfare)이므로 거기서 git 명령 실행 주의
- **저장소가 공개**: 애플 키(.p8)·인증서는 .gitignore로 막혀 있음. 키 번호·발급자 ID 같은 값도 저장소 문서에 적지 말 것 (GitHub 비밀값으로만)
