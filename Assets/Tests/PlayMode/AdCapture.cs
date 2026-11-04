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
