#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;
using WordRPG.UI;
using static WordRPG.Tests.UiDriver;
using Object = UnityEngine.Object;

namespace WordRPG.Tests
{
    // 광고 영상용 기술 연출 찍기 (#155) — 평소 테스트에서는 돌지 않음 ([Explicit]).
    // 그래픽이 있어야 하므로 -nographics 없이: -testFilter WordRPG.Tests.AdCapture → Builds/ad/skills/{번호}_{기술 id}/f0000.jpg + sfx.txt
    // 실제 BattleScreen의 사건 연출을 30fps로 한 장씩 찍는다 (Time.captureDeltaTime — 연출 시간은 unscaledDeltaTime이라 같이 고정됨)
    // 영상으로 묶기 = python Tools/ad_motion/pack_skill_clips.py
    [Explicit]
    public class AdCapture
    {
        private const int W = 1080, H = 2346, Fps = 30, MaxFrames = 96;
        private static string Out => Path.Combine(Application.dataPath, "..", "Builds", "ad", "skills");

        private static readonly (string area, FieldTheme theme, string[] enemies, string[] skills)[] Groups =
        {
            ("meadow", FieldTheme.Meadow, new[] { "ink_slime", "scribble_bat" }, new[] { "hero_swing", "doc_finish_sentence", "ara_pebble" }),
            ("library", FieldTheme.Library, new[] { "forget_goblin", "question_owl" }, new[] { "wand_thunder_spell", "quill_ink_storm", "gem_letter_shower", "doc_highlight_sweep" }),
            ("forest", FieldTheme.Forest, new[] { "tangle_spider", "chew_larva" }, new[] { "whip_thorn_storm", "lamp_firefly_swarm", "leaf_world_breath", "grail_blessing" }),
            ("jungle", FieldTheme.Jungle, new[] { "tail_lizard", "batchim_kappa" }, new[] { "gourd_spring", "flute_echo_wall" }),
            ("cave", FieldTheme.Cave, new[] { "ink_newt", "blink_ghost" }, new[] { "tortoise_encyclo_wall", "hourglass_rewind", "seed_period_grove" }),
        };

        // 기술 소리 미리보기 (#164): 연출 종류마다 기술 하나 → Builds/ad/skill_sounds, 묶기 = python Tools/ad_motion/skill_sound_preview.py
        private static string SoundOut => Path.Combine(Application.dataPath, "..", "Builds", "ad", "skill_sounds");
        private static readonly (string area, FieldTheme theme, string[] enemies, string[] skills)[] SoundGroups =
        {
            ("forest", FieldTheme.Forest, new[] { "tangle_spider", "chew_larva" }, new[] {
                "hero_swing", "bat_scratch", "raccoon_tail_slam", "wand_thunder_spell", "quill_ink_storm", "kappa_plate_splash", "whip_thorn_storm",
                "frog_tongue_lash", "ara_pebble", "lamp_firefly_swarm", "gem_letter_shower", "doc_highlight_sweep", "frog_blank_croak", "spider_tangle_web", "raccoon_muddle_whirl" }),
            ("jungle", FieldTheme.Jungle, new[] { "tail_lizard", "batchim_kappa" }, new[] {
                "grail_blessing", "leaf_world_breath", "gourd_spring", "boss_memory_drain", "flute_echo_wall", "tortoise_encyclo_wall", "hourglass_rewind", "seed_period_grove" }),
        };

        private static SkillData Skill(string id) => AssetDatabase.LoadAssetAtPath<SkillData>($"Assets/Data/Skills/{id}.asset");

        // ---- 모험 장면 (#158): 정글에서 아라와 걷기 · 뗏목 · 동굴 · 선제공격 → Builds/ad/adventure/{장면}/f0000.jpg + marks.txt
        // 필드 이동은 Time.deltaTime이라 captureDeltaTime으로 정확히 30fps. 선제공격 창은 unscaledDeltaTime이라 약간 느리게 찍힘
        private static string AdventureOut => Path.Combine(Application.dataPath, "..", "Builds", "ad", "adventure");
        private Camera advCam;
        private RenderTexture advRt;
        private Texture2D advTex;
        private StringBuilder marks;

        private static GameSession AdventureSession(GameDatabase db, HeroData heroData, string areaId, Vector2Int cell)
        {
            var session = GameSession.NewGame(heroData, 30);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Skill,
                         TutorialProgress.Intensity, TutorialProgress.NewWord, TutorialProgress.Quiz, TutorialProgress.Potion })
                session.Tutorials.Mark(id);
            for (int i = 0; i < 6; i++) session.Record.RecordBattle(true, 3, 0);
            session.SetPartyCompanion(session.AddCompanion(AssetDatabase.LoadAssetAtPath<CompanionData>("Assets/Data/Companions/ara.asset")));
            var words = db.FindArea("jungle").Words;
            for (int i = 0; i < 80 && i < words.Words.Count; i++) session.Vocabulary.Discover(words.Words[i].Id, DateTime.UtcNow.AddDays(-3));
            session.World.SetPosition(areaId, cell);
            return session;
        }

        // 맵 글자 줄 번호(위에서부터) → 칸 (y는 아래에서부터)
        private static Vector2Int CellAt(FieldArea area, int row, int col) => new Vector2Int(col, area.Map.Height - 1 - row);

        private static FieldScreen OpenField(FieldArea area, GameSession session, GameDatabase db)
        {
            var field = new GameObject("CaptureField").AddComponent<FieldScreen>();
            field.Configure(area, session, () => { }, animScale: 1f, gameDatabase: db, tutorials: false);
            return field;
        }

        private static void CanvasesToCamera(Camera cam)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode == RenderMode.ScreenSpaceCamera) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                c.sortingOrder += 1000;
            }
        }

        private void SaveFrame(string dir, int n)
        {
            CanvasesToCamera(advCam); // 전투·연출이 새로 만든 캔버스도
            Canvas.ForceUpdateCanvases();
            advCam.Render();
            RenderTexture.active = advRt;
            advTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            advTex.Apply(false);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, $"f{n:0000}.jpg"), advTex.EncodeToJPG(93));
        }

        private static PointerEventData PressStick(FieldScreen field, Direction direction)
        {
            var stick = field.Stick;
            var data = new PointerEventData(EventSystem.current) { pointerId = 1, position = stick.ScreenPointFor(Vector2.zero) };
            ExecuteEvents.Execute(stick.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.position = stick.ScreenPointFor((Vector2)direction.ToOffset() * 0.6f);
            ExecuteEvents.Execute(stick.gameObject, data, ExecuteEvents.dragHandler);
            return data;
        }

        // 스틱을 민 채로 찍는다: 미리 걷기(startWhen 전까지 안 찍음) → 찍기 → 손 뗌. 보이는 몬스터는 알아채지 않게
        private IEnumerator WalkClip(string name, FieldArea area, GameSession session, GameDatabase db, Direction direction,
            Func<FieldScreen, bool> startWhen, Func<FieldScreen, bool> stopWhen, int maxFrames)
        {
            string dir = Path.Combine(AdventureOut, name);
            Directory.CreateDirectory(dir);
            var field = OpenField(area, session, db);
            yield return new WaitForSecondsRealtime(0.5f);
            CanvasesToCamera(advCam);
            yield return null;
            var data = PressStick(field, direction);
            for (float t = 0; !startWhen(field) && t < 8f; t += Time.unscaledDeltaTime)
            {
                field.Roaming?.Calm();
                yield return null;
            }
            Time.captureDeltaTime = 1f / Fps;
            int n = 0;
            for (; n < maxFrames && !stopWhen(field) && !field.IsInBattle; n++)
            {
                field.Roaming?.Calm();
                yield return null;
                SaveFrame(dir, n);
            }
            Time.captureDeltaTime = 0f;
            ExecuteEvents.Execute(field.Stick.gameObject, data, ExecuteEvents.pointerUpHandler);
            marks.AppendLine($"{name}\tframes={n}\trafting={field.IsRafting}\tcell={field.PlayerCell}\tbattle={field.IsInBattle}");
            Object.Destroy(field.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator Adventure()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            if (Directory.Exists(AdventureOut)) Directory.Delete(AdventureOut, true);
            Directory.CreateDirectory(AdventureOut);
            advCam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            advCam.clearFlags = CameraClearFlags.SolidColor;
            advCam.backgroundColor = Color.black;
            advRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            advCam.targetTexture = advRt;
            advTex = new Texture2D(W, H, TextureFormat.RGB24, false);
            marks = new StringBuilder();
            var jungle = db.FindArea("jungle");
            var cave = db.FindArea("cave");
            Assert.AreEqual(CellAt(jungle, 78, 6), jungle.Map.Start, "맵 줄 번호와 칸이 맞아야 함");

            // 1) 아라와 카메라 쪽으로 걸어오며 얕은 강을 건넘
            yield return WalkClip("walk", jungle, AdventureSession(db, heroData, "jungle", CellAt(jungle, 36, 22)), db, Direction.Down,
                f => f.PlayerCell.y <= CellAt(jungle, 39, 22).y, f => f.PlayerCell.y <= CellAt(jungle, 57, 22).y, 110);
            // 2) 나루터에서 뗏목을 타고 큰 강으로
            yield return WalkClip("raft", jungle, AdventureSession(db, heroData, "jungle", CellAt(jungle, 70, 42)), db, Direction.Right,
                f => f.PlayerCell.x >= 45, f => f.PlayerCell.x >= 65, 110);
            // 3) 어두운 동굴 (주인공 빛·아라 횃불)
            yield return WalkClip("cave", cave, AdventureSession(db, heroData, "cave", cave.Map.Start), db, Direction.Up,
                f => f.PlayerCell.y >= cave.Map.Start.y + 1, f => f.PlayerCell.y >= cave.Map.Start.y + 9, 60);

            // 4) 몬스터가 알아채고 → [선제공격] → 단어 한 문제 → 성공 도장 → 전투
            {
                string dir = Path.Combine(AdventureOut, "preempt");
                Directory.CreateDirectory(dir);
                var start = CellAt(jungle, 55, 24);
                var session = AdventureSession(db, heroData, "jungle", start);
                var field = OpenField(jungle, session, db);
                yield return new WaitForSecondsRealtime(0.5f);
                CanvasesToCamera(advCam);
                Assert.IsNotEmpty(field.Roaming.Roamers, "정글에 보이는 몬스터");
                var roamer = field.Roaming.Roamers[0];
                typeof(Roamer).GetProperty("Position").GetSetMethod(true).Invoke(roamer, new object[] { start + new Vector2Int(9, 0) });
                var data = PressStick(field, Direction.Right);
                Time.captureDeltaTime = 1f / Fps;
                int n = 0, available = -1, clicked = -1, opened = -1, answered = -1, stamped = -1, battleAt = -1;
                string stamp = "";
                for (; n < 330; n++)
                {
                    yield return null;
                    SaveFrame(dir, n);
                    if (available < 0 && field.IsPreemptAvailable)
                    {
                        available = n;
                        ExecuteEvents.Execute(field.Stick.gameObject, data, ExecuteEvents.pointerUpHandler);
                    }
                    if (clicked < 0 && available >= 0 && n >= available + 4)
                    {
                        clicked = n;
                        FindButton(field.transform, "PreemptButton").onClick.Invoke();
                    }
                    if (opened < 0 && field.Preempt.IsOpen) opened = n;
                    if (answered < 0 && opened >= 0 && n >= opened + 22 && field.Preempt.Question != null)
                    {
                        answered = n;
                        ActiveButton(field.transform, $"PreemptChoice_{field.Preempt.Question.CorrectIndex}").onClick.Invoke();
                    }
                    if (stamped < 0 && !string.IsNullOrEmpty(field.Preempt.StampText)) { stamped = n; stamp = field.Preempt.StampText; }
                    if (battleAt < 0 && field.Battle.Engine != null && field.Battle.Engine.Phase == BattlePhase.ChoosingSkill) battleAt = n;
                    if (battleAt >= 0 && n >= battleAt + 15) break;
                }
                Time.captureDeltaTime = 0f;
                marks.AppendLine($"preempt\tframes={n}\tavailable={available}\tclicked={clicked}\topened={opened}\tanswered={answered}\tstamped={stamped}({stamp})\tbattle={battleAt}");
                Object.Destroy(field.gameObject);
            }
            File.WriteAllText(Path.Combine(AdventureOut, "marks.txt"), marks.ToString());
            advCam.targetTexture = null;
            Object.Destroy(advRt);
            Object.Destroy(advTex);
            Object.Destroy(advCam.gameObject);
        }

        // ---- 실제 시간 그대로 찍기 (#159): 픽셀만 메모리에 받아 두고 장면이 끝난 뒤 저장 → 게임이 거의 제 속도로 돎 (문제 줄이 타는 속도 그대로)
        // Builds/ad/real/{장면}/f0000.jpg + times.txt(프레임마다 시각) + ../marks.txt — Tools/ad_motion/pack_real.py가 시각대로 30fps 영상으로
        private static string RealOut => Path.Combine(Application.dataPath, "..", "Builds", "ad", "real");
        private readonly List<byte[]> rawFrames = new List<byte[]>();
        private readonly List<float> rawTimes = new List<float>();
        private float clipStart;

        private void BeginClip()
        {
            rawFrames.Clear();
            rawTimes.Clear();
            clipStart = Time.realtimeSinceStartup;
        }

        private float ClipTime => Time.realtimeSinceStartup - clipStart;

        private void Grab()
        {
            CanvasesToCamera(advCam);
            Canvas.ForceUpdateCanvases();
            advCam.Render();
            RenderTexture.active = advRt;
            advTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = null;
            rawFrames.Add(advTex.GetRawTextureData());
            rawTimes.Add(ClipTime);
        }

        private void FlushClip(string name, string root = null)
        {
            string dir = Path.Combine(root ?? RealOut, name);
            Directory.CreateDirectory(dir);
            var times = new StringBuilder();
            for (int i = 0; i < rawFrames.Count; i++)
            {
                advTex.LoadRawTextureData(rawFrames[i]);
                advTex.Apply(false);
                File.WriteAllBytes(Path.Combine(dir, $"f{i:0000}.jpg"), advTex.EncodeToJPG(93));
                times.AppendLine(rawTimes[i].ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            }
            File.WriteAllText(Path.Combine(dir, "times.txt"), times.ToString());
            float length = rawTimes.Count > 0 ? rawTimes[rawTimes.Count - 1] : 0f;
            marks.AppendLine($"{name}\tframes={rawFrames.Count}\tseconds={length:F2}\tfps={(length > 0 ? rawFrames.Count / length : 0):F1}");
            rawFrames.Clear();
            rawTimes.Clear();
            GC.Collect();
        }

        private void Mark(string name, string what) => marks.AppendLine($"{name}\t{what}\tt={ClipTime:F3}");

        // 기술을 고르고 문제가 나올 때까지 (찍지 않음). 정답 수를 쌓을 때도 씀
        private static void ChooseSkill(BattleScreen battle)
        {
            var root = battle.transform;
            var intensity = ActiveButton(root, "Intensity_0");
            var target = ActiveButton(root, "Enemy_0");
            if (intensity != null) intensity.onClick.Invoke();
            else if (target != null && ActiveButton(root, "CancelButton") != null) target.onClick.Invoke();
            else ActiveButton(root, "SkillButton_0")?.onClick.Invoke();
        }

        private static void StudyAll(GameSession session, Words.WordDatabase words)
        {
            var rules = new Words.MasteryRules();
            var when = DateTime.UtcNow.AddDays(-200);
            for (int i = 0; i < words.Words.Count; i++)
            {
                session.Vocabulary.Discover(words.Words[i].Id, when);
                session.Vocabulary.RecordAnswer(words.Words[i].Id, true, when, rules);
            }
        }

        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator RealClips()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            if (Directory.Exists(RealOut)) Directory.Delete(RealOut, true);
            Directory.CreateDirectory(RealOut);
            advCam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            advCam.clearFlags = CameraClearFlags.SolidColor;
            advCam.backgroundColor = Color.black;
            advRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            advCam.targetTexture = advRt;
            advTex = new Texture2D(W, H, TextureFormat.RGB24, false);
            marks = new StringBuilder();
            var sfx = new StringBuilder();
            string clip = "";
            Sound.Recorder = (s, _) => sfx.AppendLine($"{clip}\t{ClipTime:F3}\t{s.ToString().ToLowerInvariant()}");

            // 1) 숲의 풀숲을 걸어 상자 열기 (이 장면에서만 풀숲 조우를 끔 — 복사본 지역)
            {
                clip = "chest";
                var forest = Object.Instantiate(db.FindArea("forest")).Set("encounterRate", 0f);
                Assert.AreEqual(CellAt(forest, 38, 5), forest.Map.Start, "맵 줄 번호와 칸이 맞아야 함");
                var session = AdventureSession(db, heroData, "forest", CellAt(forest, 37, 30));
                var field = OpenField(forest, session, db);
                yield return new WaitForSecondsRealtime(0.5f);
                CanvasesToCamera(advCam);
                yield return null;
                var data = PressStick(field, Direction.Up);
                BeginClip();
                int topY = CellAt(forest, 31, 30).y;
                for (float end = Time.realtimeSinceStartup + 6f; field.PlayerCell.y < topY && Time.realtimeSinceStartup < end;)
                {
                    yield return null;
                    Grab();
                }
                data.position = field.Stick.ScreenPointFor((Vector2)Direction.Right.ToOffset() * 0.6f);
                ExecuteEvents.Execute(field.Stick.gameObject, data, ExecuteEvents.dragHandler);
                for (float end = Time.realtimeSinceStartup + 4f; field.PlayerCell.x < 32 && Time.realtimeSinceStartup < end;)
                {
                    yield return null;
                    Grab();
                }
                ExecuteEvents.Execute(field.Stick.gameObject, data, ExecuteEvents.pointerUpHandler);
                for (float end = Time.realtimeSinceStartup + 1f; field.IsMoving && Time.realtimeSinceStartup < end;)
                {
                    yield return null;
                    Grab();
                }
                Mark(clip, $"arrived cell={field.PlayerCell} facing={field.Facing}");
                for (int i = 0; i < 4; i++) { yield return null; Grab(); }
                FindButton(field.transform, "Pad_Confirm").onClick.Invoke();
                Mark(clip, "confirm");
                for (float end = Time.realtimeSinceStartup + 2.6f; Time.realtimeSinceStartup < end;)
                {
                    yield return null;
                    Grab();
                }
                FlushClip(clip);
                Object.Destroy(field.gameObject);
                yield return null;
                yield return null;
            }

            // 2) 첫 문제: 줄이 타는 동안 기다렸다가 정답 → 휘두르기 (초원, 주인공 혼자 — 앱스토어 그림과 같은 배치)
            {
                clip = "quiz";
                var meadow = db.FindArea("meadow");
                var session = GameSession.NewGame(heroData, 10);
                foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Skill,
                             TutorialProgress.Intensity, TutorialProgress.NewWord, TutorialProgress.Quiz, TutorialProgress.Potion })
                    session.Tutorials.Mark(id);
                for (int i = 0; i < 6; i++) session.Record.RecordBattle(true, 3, 0);
                StudyAll(session, meadow.Words);
                session.World.SetPosition("meadow", new Vector2Int(22, 25));
                var field = OpenField(meadow, session, db);
                yield return new WaitForSecondsRealtime(0.5f);
                field.Battle.BeginBattle(new List<MonsterInstance> { new MonsterInstance(db.FindMonster("ink_slime"), 13), new MonsterInstance(db.FindMonster("scribble_bat"), 13) },
                    meadow.Words, _ => { }, null, meadow.Theme);
                var battle = field.Battle;
                yield return WaitFor(() => battle.Engine != null && battle.Engine.Phase == BattlePhase.ChoosingSkill, 20f);
                yield return new WaitForSecondsRealtime(0.4f);
                for (float end = Time.realtimeSinceStartup + 10f; battle.Engine.Phase != BattlePhase.AnsweringQuiz && Time.realtimeSinceStartup < end;)
                {
                    ChooseSkill(battle);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                Assert.AreEqual(BattlePhase.AnsweringQuiz, battle.Engine.Phase, "문제가 나와야 함");
                // 적 고르기에 내려간 적 카드가 제자리로 돌아온 뒤에 카메라 캔버스로 (적 카드 이동은 화면 좌표라 도중에 바꾸면 사라짐)
                yield return new WaitForSecondsRealtime(0.35f);
                CanvasesToCamera(advCam);
                yield return null;
                BeginClip();
                var question = battle.Engine.CurrentQuestion;
                Mark(clip, $"question word={question.Word.English} meaning={question.Word.Meaning} correct={question.CorrectIndex}");
                for (float end = Time.realtimeSinceStartup + 2.2f; Time.realtimeSinceStartup < end;) // 크리티컬은 문제가 나오고 3초 안 (찍기 전 0.4초 포함)
                {
                    yield return null;
                    Grab();
                }
                ActiveButton(battle.transform, $"Choice_{question.CorrectIndex}").onClick.Invoke();
                Mark(clip, "answered");
                for (float end = Time.realtimeSinceStartup + 2.6f; Time.realtimeSinceStartup < end;)
                {
                    yield return null;
                    Grab();
                }
                FlushClip(clip);
                Object.Destroy(field.gameObject);
                yield return null;
                yield return null;
            }

            // 3) 보스 결정타: 정답을 쌓아 콤보를 올린 뒤 보스 HP를 깎아 마지막 한 방 (콤보는 실제 값)
            {
                clip = "finisher";
                var library = db.FindArea("library");
                var session = GameSession.NewGame(heroData, 16);
                foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Skill,
                             TutorialProgress.Intensity, TutorialProgress.NewWord, TutorialProgress.Quiz, TutorialProgress.Potion })
                    session.Tutorials.Mark(id);
                for (int i = 0; i < 6; i++) session.Record.RecordBattle(true, 3, 0);
                StudyAll(session, library.Words);
                session.World.SetPosition("library", library.Map.Start);
                var field = OpenField(library, session, db);
                yield return new WaitForSecondsRealtime(0.5f);
                var boss = library.Boss;
                field.Battle.BeginBattle(new List<MonsterInstance> { new MonsterInstance(boss.Species, boss.Level) }, library.Words, _ => { }, "보스 출현!", library.Theme, boss: true);
                var battle = field.Battle;
                const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
                bool capturing = false;
                float stopAt = float.MaxValue;
                for (float end = Time.realtimeSinceStartup + 150f; Time.realtimeSinceStartup < end;)
                {
                    var engine = battle.Engine;
                    if (session.Hero.CurrentHp * 2 < session.Hero.Stats.MaxHp) session.Hero.RestoreFully();
                    if (!capturing && engine != null && engine.Enemies[0].Hp * 2 < engine.Enemies[0].MaxHp) engine.Enemies[0].Combatant.Heal(engine.Enemies[0].MaxHp); // 콤보가 쌓이기 전에 쓰러지지 않게
                    if (engine != null && engine.Phase == BattlePhase.AnsweringQuiz && engine.CurrentQuestion != null)
                    {
                        if (engine.Streak >= 6 && !capturing)
                        {
                            var foe = engine.Enemies[0];
                            foe.Monster.TakeDamage(Mathf.Max(0, foe.Hp - 12)); // 다음 한 방이면 쓰러지게 (찍기 전용)
                            typeof(BattleScreen).GetMethod("SyncAll", any)?.Invoke(battle, null); // HP 줄도 그 값으로
                            CanvasesToCamera(advCam);
                            capturing = true;
                            BeginClip();
                            Mark(clip, $"streak={engine.Streak} word={engine.CurrentQuestion.Word.English}");
                            for (float wait = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < wait;)
                            {
                                yield return null;
                                Grab();
                            }
                            Mark(clip, "answered");
                        }
                        ActiveButton(battle.transform, $"Choice_{engine.CurrentQuestion.CorrectIndex}")?.onClick.Invoke();
                    }
                    else if (engine != null && engine.Phase == BattlePhase.ChoosingSkill && !capturing) ChooseSkill(battle);
                    if (capturing)
                    {
                        if (battle.FinishersPlayed > 0 && stopAt == float.MaxValue)
                        {
                            stopAt = Time.realtimeSinceStartup + 2.6f;
                            Mark(clip, "finisher");
                        }
                        if (Time.realtimeSinceStartup > stopAt || battle.IsResultVisible || ClipTime > 9f) break;
                    }
                    yield return null;
                    if (capturing) Grab();
                }
                Assert.IsTrue(capturing, "콤보를 쌓지 못함");
                FlushClip(clip);
                Object.Destroy(field.gameObject);
            }

            Sound.Recorder = null;
            File.WriteAllText(Path.Combine(RealOut, "marks.txt"), marks.ToString());
            File.WriteAllText(Path.Combine(RealOut, "sfx.txt"), sfx.ToString());
            advCam.targetTexture = null;
            Object.Destroy(advRt);
            Object.Destroy(advTex);
            Object.Destroy(advCam.gameObject);
        }

        // ---- 보스 기믹 장면 (#259, 광고 시안 9 — 1.3): 실제 보스전을 실제 시간 그대로 (RealClips와 같은 방식)
        // 까먹대왕 정답 사라짐 · 헷갈너구리 보기 섞기 · 빈칸두꺼비 빈칸 · 말줄임덩굴 덩굴
        // Builds/ad/gimmick/{장면}/f0000.jpg + times.txt + marks.txt·sfx.txt → python Tools/ad_motion/pack_real.py Builds/ad/gimmick
        private static string GimmickOut => Path.Combine(Application.dataPath, "..", "Builds", "ad", "gimmick");

        private static GameSession BossSession(GameDatabase db, HeroData heroData, string areaId, int level, bool withAra)
        {
            var session = GameSession.NewGame(heroData, level);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Skill,
                         TutorialProgress.Intensity, TutorialProgress.NewWord, TutorialProgress.Quiz, TutorialProgress.Potion })
                session.Tutorials.Mark(id);
            for (int i = 0; i < 6; i++) session.Record.RecordBattle(true, 3, 0);
            if (withAra) session.SetPartyCompanion(session.AddCompanion(AssetDatabase.LoadAssetAtPath<CompanionData>("Assets/Data/Companions/ara.asset")));
            var area = db.FindArea(areaId);
            StudyAll(session, area.Words);
            session.World.SetPosition(areaId, area.Map.Start);
            return session;
        }

        // 보스전을 열고 카메라 캔버스로 (적이 하나라 적 고르기 이동이 없음)
        private IEnumerator OpenBossBattle(GameDatabase db, string areaId, GameSession session, Action<FieldScreen> opened)
        {
            var area = db.FindArea(areaId);
            var field = OpenField(area, session, db);
            yield return new WaitForSecondsRealtime(0.5f);
            var boss = area.Boss;
            field.Battle.BeginBattle(new List<MonsterInstance> { new MonsterInstance(boss.Species, boss.Level) }, area.Words, _ => { }, "보스 출현!", area.Theme, boss: true);
            yield return WaitFor(() => field.Battle.Engine != null && field.Battle.Engine.Phase == BattlePhase.ChoosingSkill, 20f);
            yield return new WaitForSecondsRealtime(0.4f);
            CanvasesToCamera(advCam);
            yield return null;
            opened(field);
        }

        // 원하는 문제가 나올 때까지 정답으로 넘김 (찍지 않음). 주인공·보스 HP는 절반 아래로 안 내려가게
        private static IEnumerator UntilQuestion(BattleScreen battle, GameSession session, Func<Words.QuizQuestion, bool> want)
        {
            for (float end = Time.realtimeSinceStartup + 120f; Time.realtimeSinceStartup < end;)
            {
                var engine = battle.Engine;
                if (session.Hero.CurrentHp * 2 < session.Hero.Stats.MaxHp) session.Hero.RestoreFully();
                if (engine != null && engine.Enemies[0].Hp * 2 < engine.Enemies[0].MaxHp) engine.Enemies[0].Combatant.Heal(engine.Enemies[0].MaxHp);
                if (engine != null && engine.Phase == BattlePhase.AnsweringQuiz && engine.CurrentQuestion != null)
                {
                    if (!engine.IsFleeQuiz && want(engine.CurrentQuestion)) yield break;
                    ActiveButton(battle.transform, $"Choice_{engine.CurrentQuestion.CorrectIndex}")?.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                else if (engine != null && engine.Phase == BattlePhase.ChoosingSkill) ChooseSkill(battle);
                yield return null;
            }
            Assert.Fail("원하는 문제가 나오지 않음");
        }

        private IEnumerator GrabFor(float seconds)
        {
            for (float end = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < end;)
            {
                yield return null;
                Grab();
            }
        }

        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator BossGimmicks()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            if (Directory.Exists(GimmickOut)) Directory.Delete(GimmickOut, true);
            Directory.CreateDirectory(GimmickOut);
            advCam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            advCam.clearFlags = CameraClearFlags.SolidColor;
            advCam.backgroundColor = Color.black;
            advRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            advCam.targetTexture = advRt;
            advTex = new Texture2D(W, H, TextureFormat.RGB24, false);
            marks = new StringBuilder();
            var sfx = new StringBuilder();
            string clip = "";
            bool vineGrew = false;
            Sound.Recorder = (s, _) =>
            {
                sfx.AppendLine($"{clip}\t{ClipTime:F3}\t{s.ToString().ToLowerInvariant()}");
                if (s == Sfx.VineGrow) vineGrew = true;
            };
            FieldScreen field = null;

            // 1) 까먹대왕: 크리티컬 시간이 지나면 정답 버튼이 사라짐 — 답하지 않고 기다림
            {
                clip = "forget";
                var session = BossSession(db, heroData, "library", 16, false);
                yield return OpenBossBattle(db, "library", session, f => field = f);
                var battle = field.Battle;
                yield return UntilQuestion(battle, session, q => true);
                BeginClip();
                var question = battle.Engine.CurrentQuestion;
                var correct = FindButton(battle.transform, $"Choice_{question.CorrectIndex}");
                Mark(clip, $"question word={question.Word.English} correct={question.CorrectIndex}");
                float stopAt = float.MaxValue;
                for (float end = Time.realtimeSinceStartup + 8f; Time.realtimeSinceStartup < end && Time.realtimeSinceStartup < stopAt;)
                {
                    yield return null;
                    Grab();
                    if (stopAt == float.MaxValue && !correct.gameObject.activeSelf)
                    {
                        Mark(clip, "vanished");
                        stopAt = Time.realtimeSinceStartup + 1.6f;
                    }
                }
                Assert.AreNotEqual(float.MaxValue, stopAt, "정답 버튼이 사라져야 함");
                FlushClip(clip, GimmickOut);
                Object.Destroy(field.gameObject);
                yield return null;
                yield return null;
            }

            // 2) 헷갈너구리: 2초마다 보기 자리가 섞임 (직전에 흔들림)
            {
                clip = "shuffle";
                var session = BossSession(db, heroData, "forest", 20, false);
                yield return OpenBossBattle(db, "forest", session, f => field = f);
                var battle = field.Battle;
                yield return UntilQuestion(battle, session, q => true);
                BeginClip();
                Mark(clip, $"question word={battle.Engine.CurrentQuestion.Word.English}");
                int before = battle.ChoiceShuffles;
                float stopAt = float.MaxValue;
                for (float end = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < end && Time.realtimeSinceStartup < stopAt;)
                {
                    yield return null;
                    Grab();
                    if (stopAt == float.MaxValue && battle.ChoiceShuffles > before)
                    {
                        Mark(clip, "shuffled");
                        stopAt = Time.realtimeSinceStartup + 1.0f;
                    }
                }
                Assert.AreNotEqual(float.MaxValue, stopAt, "보기가 섞여야 함");
                FlushClip(clip, GimmickOut);
                Object.Destroy(field.gameObject);
                yield return null;
                yield return null;
            }

            // 3) 빈칸두꺼비: 영어 단어에 빈칸 → 맞히면 채워진 글자가 금색으로 (긴 단어가 나올 때까지 넘김)
            {
                clip = "blank";
                var session = BossSession(db, heroData, "jungle", 25, true);
                yield return OpenBossBattle(db, "jungle", session, f => field = f);
                var battle = field.Battle;
                yield return UntilQuestion(battle, session, q => q.Direction == Words.QuizDirection.EnglishToMeaning && q.Prompt.Length >= 7);
                BeginClip();
                var question = battle.Engine.CurrentQuestion;
                Mark(clip, $"question word={question.Word.English} meaning={question.Word.Meaning} correct={question.CorrectIndex}");
                yield return GrabFor(1.3f);
                ActiveButton(battle.transform, $"Choice_{question.CorrectIndex}").onClick.Invoke();
                Mark(clip, "answered");
                yield return GrabFor(1.8f);
                FlushClip(clip, GimmickOut);
                Object.Destroy(field.gameObject);
                yield return null;
                yield return null;
            }

            // 4) 말줄임덩굴: 첫 문제를 맞힌 뒤 보스 차례에 양옆으로 덩굴이 자람 (주인공 혼자 — 바로 보스 차례)
            {
                clip = "vine";
                var session = BossSession(db, heroData, "cave", 28, false);
                yield return OpenBossBattle(db, "cave", session, f => field = f);
                var battle = field.Battle;
                yield return UntilQuestion(battle, session, q => true);
                vineGrew = false;
                BeginClip();
                Mark(clip, $"question word={battle.Engine.CurrentQuestion.Word.English}");
                yield return GrabFor(0.5f);
                ActiveButton(battle.transform, $"Choice_{battle.Engine.CurrentQuestion.CorrectIndex}").onClick.Invoke();
                Mark(clip, "answered");
                float stopAt = float.MaxValue;
                for (float end = Time.realtimeSinceStartup + 9f; Time.realtimeSinceStartup < end && Time.realtimeSinceStartup < stopAt;)
                {
                    yield return null;
                    Grab();
                    if (stopAt == float.MaxValue && vineGrew)
                    {
                        Mark(clip, "grew");
                        stopAt = Time.realtimeSinceStartup + 1.8f;
                    }
                }
                Assert.AreNotEqual(float.MaxValue, stopAt, "덩굴이 자라야 함");
                FlushClip(clip, GimmickOut);
                Object.Destroy(field.gameObject);
            }

            Sound.Recorder = null;
            File.WriteAllText(Path.Combine(GimmickOut, "marks.txt"), marks.ToString());
            File.WriteAllText(Path.Combine(GimmickOut, "sfx.txt"), sfx.ToString());
            advCam.targetTexture = null;
            Object.Destroy(advRt);
            Object.Destroy(advTex);
            Object.Destroy(advCam.gameObject);
        }

        private static IEnumerator Then(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }

        // 기술의 대상대로 사건을 만듦. 숫자는 위력에 맞춘 표시용 (엔진을 돌리지 않음)
        private static BattleEvent[] Events(SkillData skill, BattleUnit hero, BattleUnit ara, BattleUnit a, BattleUnit b, bool allowCrit = true)
        {
            var actor = skill.SkillId.StartsWith("ara_") ? ara : hero;
            var list = new List<BattleEvent> { BattleEvent.SkillUsed(actor, skill) };
            int amount = Mathf.RoundToInt(skill.Power * 1.5f);
            // 크리티컬 폭발이 기술 고유의 연출을 가리므로, '크리티컬' 장면에 쓰는 두 기술만
            bool crit = allowCrit && (skill.SkillId == "hero_swing" || skill.SkillId == "doc_finish_sentence");
            switch (skill.Kind)
            {
                case SkillKind.Heal:
                    list.Add(BattleEvent.Heal(actor, hero, skill, amount, false));
                    if (skill.Target != SkillTarget.Self) list.Add(BattleEvent.Heal(actor, ara, skill, amount, false));
                    break;
                case SkillKind.Guard:
                    list.Add(BattleEvent.Shield(actor, hero, skill, amount, false));
                    if (skill.Target != SkillTarget.Self) list.Add(BattleEvent.Shield(actor, ara, skill, amount, false));
                    break;
                default:
                    list.Add(BattleEvent.Damage(actor, a, skill, amount, 0, crit));
                    if (skill.Target == SkillTarget.AllEnemies) list.Add(BattleEvent.Damage(actor, b, skill, amount, 0, crit));
                    break;
            }
            return list.ToArray();
        }

        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator SkillReel() => CaptureSkills(Out, Groups, true);

        [UnityTest]
        [Timeout(1200000)]
        public IEnumerator SkillSoundReel() => CaptureSkills(SoundOut, SoundGroups, false);

        private IEnumerator CaptureSkills(string outDir, (string area, FieldTheme theme, string[] enemies, string[] skills)[] groups, bool allowCrit)
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            var session = GameSession.NewGame(heroData, 30);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Skill,
                         TutorialProgress.Intensity, TutorialProgress.NewWord, TutorialProgress.Quiz, TutorialProgress.Potion })
                session.Tutorials.Mark(id);
            for (int i = 0; i < 6; i++) session.Record.RecordBattle(true, 3, 0);
            session.SetPartyCompanion(session.AddCompanion(AssetDatabase.LoadAssetAtPath<CompanionData>("Assets/Data/Companions/ara.asset")));

            const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var sfxLog = new StringBuilder();
            var nameLog = new StringBuilder(); // 묶을 때 붙일 이름표: 기술 이름 · 연출 종류
            int frameNow = 0, clip = 0;
            string clipName = "";
            Sound.Recorder = (sfx, _) => sfxLog.AppendLine($"{clipName}\t{frameNow}\t{sfx.ToString().ToLowerInvariant()}");

            foreach (var (areaId, theme, enemies, skills) in groups)
            {
                var area = db.FindArea(areaId);
                session.RestoreHero(); // 지난 전투의 표시용 피해가 쌓여 쓰러지지 않게
                session.World.SetPosition(areaId, area.Map.Start);
                var field = new GameObject("CaptureField").AddComponent<FieldScreen>();
                field.Configure(area, session, () => { }, step: 0.2f, animScale: 1f, gameDatabase: db, tutorials: false);
                yield return new WaitForSecondsRealtime(0.4f);

                var battle = field.Battle;
                var foes = new List<MonsterInstance>();
                foreach (var e in enemies) foes.Add(new MonsterInstance(db.FindMonster(e), 30));
                battle.BeginBattle(foes, area.Words, _ => { }, null, theme);
                yield return WaitFor(() => battle.Engine != null && battle.Engine.Phase == BattlePhase.ChoosingSkill, 20f);
                yield return new WaitForSecondsRealtime(0.5f);
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!c.isRootCanvas) continue;
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 1f;
                    c.sortingOrder += 1000;
                }
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return new WaitForSecondsRealtime(0.3f);
                typeof(BattleScreen).GetMethod("HideAllPanels", any)?.Invoke(battle, null);
                typeof(BattleScreen).GetField("lastWord", any)?.SetValue(battle, "memory");

                var engine = battle.Engine;
                BattleUnit hero = engine.Party[0], ara = engine.Party[1], a = engine.Enemies[0], b = engine.Enemies[1];
                hero.Combatant.TakeDamage(hero.MaxHp / 3); // HP가 가득이면 회복이 차오르는 게 안 보임
                ara.Combatant.TakeDamage(ara.MaxHp / 3);
                typeof(BattleScreen).GetMethod("Snapshot", any)?.Invoke(battle, null);

                foreach (var id in skills)
                {
                    var skill = Skill(id);
                    Assert.IsNotNull(skill, id);
                    clipName = $"{clip:00}_{id}";
                    string dir = Path.Combine(outDir, clipName);
                    Directory.CreateDirectory(dir);
                    nameLog.AppendLine($"{clipName}\t{skill.DisplayName}\t{skill.Fx}");
                    clip++;
                    frameNow = 0;
                    Time.captureDeltaTime = 1f / Fps;
                    bool done = false;
                    battle.StartCoroutine(Then(battle.PlayEventsForTest(Events(skill, hero, ara, a, b, allowCrit)), () => done = true));
                    int tail = 0;
                    float deltaSeen = 0f;
                    for (int n = 0; n < MaxFrames && tail < 8; n++)
                    {
                        frameNow = n;
                        yield return null;
                        deltaSeen = Time.unscaledDeltaTime;
                        Canvas.ForceUpdateCanvases();
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                        tex.Apply(false);
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(dir, $"f{n:0000}.jpg"), tex.EncodeToJPG(93));
                        if (done) tail++;
                    }
                    Debug.Log($"[AdCapture] {clipName} done={done} unscaledDelta={deltaSeen:F4}");
                    Time.captureDeltaTime = 0f;
                    while (!done) yield return null;
                    // 다음 기술 전에 보호막·HP 표시를 처음 상태로
                    typeof(BattleScreen).GetMethod("Snapshot", any)?.Invoke(battle, null);
                    yield return new WaitForSecondsRealtime(0.25f);
                }
                Object.Destroy(field.gameObject);
                yield return null;
                yield return null;
            }
            Sound.Recorder = null;
            File.WriteAllText(Path.Combine(outDir, "sfx.txt"), sfxLog.ToString());
            File.WriteAllText(Path.Combine(outDir, "names.txt"), nameLog.ToString());
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(tex);
            Object.Destroy(cam.gameObject);
        }
    }
}
#endif
