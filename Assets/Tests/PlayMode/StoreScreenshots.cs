#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
using WordRPG.Words;
using static WordRPG.Tests.UiDriver;
using Object = UnityEngine.Object;

namespace WordRPG.Tests
{
    // 앱스토어 스크린샷용 게임 화면 (아이폰 6.9인치 1320×2868) — 평소 테스트에서는 돌지 않음 ([Explicit]).
    // 그래픽이 있어야 하므로 -nographics 없이: -testFilter WordRPG.Tests.StoreScreenshots → Builds/store/raw/*.png
    // 그다음 python Tools/store_screenshots.py 가 위에 문구를 얹어 Builds/store/final/ 에 만든다 (작업메모 '앱스토어 스크린샷')
    [Explicit]
    public class StoreScreenshots
    {
        private const int W = 1320, H = 2868;
        private static string Out => Path.Combine(Application.dataPath, "..", "Builds", "store", "raw");

        private static IEnumerator Shot(string name)
        {
            yield return null;
            Directory.CreateDirectory(Out);
            var cam = Camera.main;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var oldTarget = cam.targetTexture;
            cam.targetTexture = rt;
            var canvases = new List<(Canvas c, RenderMode mode, int order)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || !c.isActiveAndEnabled) continue;
                canvases.Add((c, c.renderMode, c.sortingOrder));
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                c.sortingOrder += 1000; // 필드 스프라이트가 UI 위에 찍히지 않게 (캡처 때만)
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Out, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = oldTarget;
            foreach (var (c, mode, order) in canvases)
            {
                if (c == null) continue;
                c.renderMode = mode;
                c.sortingOrder = order;
            }
            Object.Destroy(rt);
        }

        private static T Private<T>(object owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

        private static Camera MakeCamera()
        {
            var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            return cam;
        }

        // 튜토리얼은 다 본 것으로, 성유물 두 개 더, 전투 몇 번 한 상태
        private static GameSession Session(HeroData heroData, GameDatabase db, int level)
        {
            var session = GameSession.NewGame(heroData, level);
            foreach (var id in new[] { TutorialProgress.Field, TutorialProgress.Confirm, TutorialProgress.Grass, TutorialProgress.Skill,
                         TutorialProgress.Intensity, TutorialProgress.NewWord, TutorialProgress.Quiz, TutorialProgress.Potion })
                session.Tutorials.Mark(id);
            session.GrantRelic(db.FindRelic("relic_book"));
            session.GrantRelic(db.FindRelic("relic_lantern"));
            for (int i = 0; i < 6; i++) session.Record.RecordBattle(true, 3, 0);
            session.Inventory.AddGold(240);
            return session;
        }

        private static FieldScreen Field(FieldArea area, GameSession session, GameDatabase db)
        {
            var field = new GameObject("CaptureField").AddComponent<FieldScreen>();
            field.Configure(area, session, () => { }, step: 0.2f, animScale: 1f, gameDatabase: db, tutorials: false);
            return field;
        }

        // 앞쪽 단어 count개를 여러 숙련도로 (도감 별)
        private static void Study(GameSession session, WordDatabase words, int count, DateTime now)
        {
            var rules = new MasteryRules();
            for (int i = 0; i < count && i < words.Words.Count; i++)
            {
                string id = words.Words[i].Id;
                int steps = 1 + (i * 7 % 5);
                var t = now.AddDays(-200);
                session.Vocabulary.Discover(id, t);
                for (int s = 0; s < steps; s++)
                {
                    session.Vocabulary.RecordAnswer(id, true, t, rules);
                    t = t.AddDays(35);
                }
            }
        }

        // 전투: 콤보 3 이상일 때의 4지선다
        [UnityTest]
        public IEnumerator Quiz()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            var meadow = db.FindArea("meadow");
            var cam = MakeCamera();
            var session = Session(heroData, db, 10);
            Study(session, meadow.Words, 150, DateTime.UtcNow);
            session.World.SetPosition("meadow", new Vector2Int(22, 25));
            var field = Field(meadow, session, db);
            yield return new WaitForSecondsRealtime(0.5f);

            var enemies = new List<MonsterInstance> { new MonsterInstance(db.FindMonster("ink_slime"), 13), new MonsterInstance(db.FindMonster("scribble_bat"), 13) };
            field.Battle.BeginBattle(enemies, meadow.Words, _ => { }, null, meadow.Theme);
            var root = field.Battle.transform;
            bool shot = false;
            for (float end = Time.realtimeSinceStartup + 90f; Time.realtimeSinceStartup < end && !shot && !field.Battle.IsResultVisible;)
            {
                var engine = field.Battle.Engine;
                var confirm = ActiveButton(root, "CardConfirmButton");
                if (confirm != null) confirm.onClick.Invoke();
                else if (engine != null && engine.Phase == BattlePhase.AnsweringQuiz && engine.CurrentQuestion != null)
                {
                    if (engine.Streak >= 3)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        yield return Shot("2_quiz");
                        shot = true;
                        break;
                    }
                    ActiveButton(root, $"Choice_{engine.CurrentQuestion.CorrectIndex}")?.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                else if (engine != null && engine.Phase == BattlePhase.ChoosingSkill)
                {
                    var intensity = ActiveButton(root, "Intensity_0");
                    var target = ActiveButton(root, "Enemy_0");
                    if (intensity != null) intensity.onClick.Invoke();
                    else if (target != null && ActiveButton(root, "CancelButton") != null) target.onClick.Invoke(); // 적이 둘이면 대상 고르기
                    else ActiveButton(root, "SkillButton_0")?.onClick.Invoke();
                }
                yield return null;
            }
            Assert.IsTrue(shot, "콤보 중 문제 화면을 못 찍음");
            Object.Destroy(field.gameObject);
            Object.Destroy(cam.gameObject);
        }

        [UnityTest]
        public IEnumerator Screens()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
            var heroData = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/Data/Hero/hero.asset");
            var meadow = db.FindArea("meadow");
            var library = db.FindArea("library");
            var cam = MakeCamera();
            var now = DateTime.UtcNow;

            // 1) 초원 탐험: 호수 곶 앞, 지나온 길은 안개가 걷힘
            var session = Session(heroData, db, 6);
            Study(session, meadow.Words, 60, now);
            session.Inventory.Add("potion", 3);
            foreach (var p in new[] { new Vector2Int(12, 5), new Vector2Int(12, 12), new Vector2Int(8, 16), new Vector2Int(8, 22),
                         new Vector2Int(14, 25), new Vector2Int(22, 25), new Vector2Int(31, 26), new Vector2Int(31, 31) })
                session.World.Reveal("meadow", meadow.Map.Width, meadow.Map.Height, p);
            session.World.SetPosition("meadow", new Vector2Int(22, 25));
            var field = Field(meadow, session, db);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("1_field");
            Object.Destroy(field.gameObject);
            yield return new WaitForSecondsRealtime(0.3f);

            // 3) 새 단어 '발견!' 카드 (서고 단어, 처음 만남)
            session = Session(heroData, db, 8);
            session.World.SetPosition("library", library.Map.Start);
            field = Field(library, session, db);
            yield return new WaitForSecondsRealtime(0.4f);
            field.Battle.BeginBattle(new List<MonsterInstance> { new MonsterInstance(db.FindMonster("question_owl"), 8) }, library.Words, _ => { }, null, library.Theme);
            var root = field.Battle.transform;
            yield return WaitFor(() => field.Battle.Engine != null && field.Battle.Engine.Phase == BattlePhase.ChoosingSkill, 8f);
            ActiveButton(root, "SkillButton_0")?.onClick.Invoke();
            yield return WaitFor(() => ActiveButton(root, "Intensity_0") != null || ActiveButton(root, "CardConfirmButton") != null, 5f);
            ActiveButton(root, "Intensity_0")?.onClick.Invoke();
            yield return WaitFor(() => ActiveButton(root, "CardConfirmButton") != null, 8f);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot("3_new_word");
            Object.Destroy(field.gameObject);
            yield return new WaitForSecondsRealtime(0.3f);

            // 4·5) 서고 보스 결정타 → 결산 (하나는 틀려서 오답 노트)
            session = Session(heroData, db, 10);
            session.GrantRelic(db.FindRelic("relic_quill"));
            Study(session, library.Words, 200, now);
            session.World.SetPosition("library", library.Map.Start);
            field = Field(library, session, db);
            yield return new WaitForSecondsRealtime(0.4f);
            var boss = library.Boss;
            field.Battle.BeginBattle(new List<MonsterInstance> { new MonsterInstance(boss.Species, boss.Level) }, library.Words, _ => { },
                "보스 출현!", library.Theme, boss: true);
            root = field.Battle.transform;
            int answers = 0, turn = 0;
            bool shotFinisher = false;
            float finisherAt = 0f;
            for (float end = Time.realtimeSinceStartup + 180f; Time.realtimeSinceStartup < end && !field.Battle.IsResultVisible;)
            {
                var engine = field.Battle.Engine;
                if (field.Battle.FinishersPlayed > 0)
                {
                    if (finisherAt == 0f) finisherAt = Time.realtimeSinceStartup;
                    if (!shotFinisher && Time.realtimeSinceStartup - finisherAt > 0.9f) { yield return Shot("4_finisher"); shotFinisher = true; }
                }
                var confirm = ActiveButton(root, "CardConfirmButton");
                if (confirm != null) confirm.onClick.Invoke();
                else if (engine != null && engine.Phase == BattlePhase.AnsweringQuiz && engine.CurrentQuestion != null)
                {
                    int index = engine.CurrentQuestion.CorrectIndex;
                    if (answers == 2) index = (index + 1) % engine.CurrentQuestion.Choices.Count;
                    answers++;
                    ActiveButton(root, $"Choice_{index}")?.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(0.05f);
                }
                else if (engine != null && engine.Phase == BattlePhase.ChoosingSkill)
                {
                    var intensity = ActiveButton(root, turn % 2 == 0 ? "Intensity_1" : "Intensity_0") ?? ActiveButton(root, "Intensity_0");
                    if (intensity != null) intensity.onClick.Invoke();
                    else
                    {
                        var skill = ActiveButton(root, $"SkillButton_{(turn % 3 == 0 ? 1 : 0)}") ?? ActiveButton(root, "SkillButton_0");
                        turn++;
                        skill?.onClick.Invoke();
                    }
                }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("5_summary");
            Assert.IsTrue(shotFinisher && field.Battle.IsResultVisible, "결정타·결산 화면을 못 찍음");
            Object.Destroy(field.gameObject);
            yield return new WaitForSecondsRealtime(0.3f);

            // 6) 성유물 제단: 8개 다 모음, 몇 개는 강화(+3 각성)
            session = Session(heroData, db, 12);
            session.Hero.AddRelic(db.FindRelic("relic_wand"), 3);
            session.Hero.AddRelic(db.FindRelic("relic_grail"), 2);
            session.Hero.AddRelic(db.FindRelic("relic_whip"), 1);
            foreach (var relic in db.Relics) session.GrantRelic(relic);
            session.Inventory.Add("shiny_ink", 4);
            session.Inventory.Add("hard_cover", 3);
            session.Inventory.Add("sparkle_dust", 5);
            session.World.SetPosition("meadow", meadow.Map.Start);
            field = Field(meadow, session, db);
            yield return new WaitForSecondsRealtime(0.4f);
            Private<RelicAltarView>(field, "altarView").Show(session, null);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("6_altar");
            Private<RelicAltarView>(field, "altarView").Hide();

            // 7) 단어 도감 + 단어 하나 자세히
            Study(session, meadow.Words, 180, now);
            Private<DexView>(field, "dexView").Show(meadow.Words, session, now);
            yield return new WaitForSecondsRealtime(0.3f);
            FindButton(field.transform, "DexRow_3").onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("7_dex");
            Object.Destroy(field.gameObject);
            Object.Destroy(cam.gameObject);
        }
    }
}
#endif
