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

        private static IEnumerator Then(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }

        // 기술의 대상대로 사건을 만듦. 숫자는 위력에 맞춘 표시용 (엔진을 돌리지 않음)
        private static BattleEvent[] Events(SkillData skill, BattleUnit hero, BattleUnit ara, BattleUnit a, BattleUnit b)
        {
            var actor = skill.SkillId.StartsWith("ara_") ? ara : hero;
            var list = new List<BattleEvent> { BattleEvent.SkillUsed(actor, skill) };
            int amount = Mathf.RoundToInt(skill.Power * 1.5f);
            // 크리티컬 폭발이 기술 고유의 연출을 가리므로, '크리티컬' 장면에 쓰는 두 기술만
            bool crit = skill.SkillId == "hero_swing" || skill.SkillId == "doc_finish_sentence";
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
        public IEnumerator SkillReel()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            if (Directory.Exists(Out)) Directory.Delete(Out, true);
            Directory.CreateDirectory(Out);

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
            int frameNow = 0, clip = 0;
            string clipName = "";
            Sound.Recorder = (sfx, _) => sfxLog.AppendLine($"{clipName}\t{frameNow}\t{sfx.ToString().ToLowerInvariant()}");

            foreach (var (areaId, theme, enemies, skills) in Groups)
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
                    string dir = Path.Combine(Out, clipName);
                    Directory.CreateDirectory(dir);
                    clip++;
                    frameNow = 0;
                    Time.captureDeltaTime = 1f / Fps;
                    bool done = false;
                    battle.StartCoroutine(Then(battle.PlayEventsForTest(Events(skill, hero, ara, a, b)), () => done = true));
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
            File.WriteAllText(Path.Combine(Out, "sfx.txt"), sfxLog.ToString());
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(tex);
            Object.Destroy(cam.gameObject);
        }
    }
}
#endif
