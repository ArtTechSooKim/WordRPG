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
    // 필드 > 보이는 몬스터 (#73~#75·#79): 어슬렁·쫓기 그림, [선제공격] 버튼·조준·QTE, 첫 기회 멈춤 설명
    public partial class FieldScreen
    {
        // 보이는 몬스터 조우 (#73, 사막부터 — GDD 9-5): 규칙 = RoamingMonsters, 그림 = FieldRoamerViews, [선제공격] 문제 = PreemptView
        private RoamingMonsters roaming;
        private FieldRoamerViews roamerViews;
        private PreemptView preemptView;    // QTE (#74): 집중선·줄어드는 원·2×2 선택지
        private GameObject preemptPrompt;   // 둥근 [선제공격!] 버튼 묶음 (Figma 'Preempt Prompt')
        private Button preemptButton;
        private UiRing preemptRing;         // 버튼 둘레에서 줄어드는 금색 원 = 남은 기회 (QTE)
        private float preemptWindow;        // 버튼이 뜬 뒤 지난 시간 (기회 슬로모션)
        private TargetLock preemptLock;     // 선제공격 거리의 몬스터에 빨간 조준 괄호
        private RectTransform markLayer;    // 몬스터 위치에 붙는 HUD 표시 층 (PreemptView와 같은 좌표)
        private PauseTutorial pauseTutorial; // 처음 [선제공격] 기회에 화면을 멈추고 설명 (#79)
        private UiRing preemptRange;        // 튜토리얼 2단계: 몬스터 둘레 '선제공격 범위' 점선
        private bool preemptTutorialRunning;
        private Roamer battleRoamer; // 지금 싸우는 보이는 몬스터 (이기면 사라지고, 도망치면 잠깐 멈춤)
        private List<IReadOnlyList<WordEntry>> preemptBooks = new List<IReadOnlyList<WordEntry>>(); // 선제공격 문제 = 이미 본 단어만
        private const float PreemptSlow = 0.15f;        // QTE 문제를 푸는 동안 몬스터 속도
        private const float PreemptChanceSlow = 0.35f;  // 버튼이 막 떴을 때 (기회 슬로모션 — 누를 틈)
        private const float PreemptWindowSeconds = 1.5f;

        // ------------------------------------------------------------------ 보이는 몬스터 · 선제공격 (#73)

        private void SetUpRoamers(Vector2Int position)
        {
            roamerViews.Clear();
            battleRoamer = null;
            var table = area.Encounters;
            var water = area.WaterEncounters; // 뗏목이 다니는 강의 몬스터 (#80 흉내문어)
            roaming = area.EncounterStyle == EncounterStyle.Roaming && table != null
                ? new RoamingMonsters(walker.Map, r => table.Roll(r), rng, null, water != null ? r => water.Roll(r) : (Func<System.Random, List<MonsterInstance>>)null)
                : null;
            if (roaming != null)
            {
                roaming.Populate(position);
                roamerViews.Sync(roaming);
            }
            RefreshPreemptBooks();
        }

        // 선제공격 문제를 낼 단어장 (이미 본 단어가 있는 곳만) — 지역에 들어올 때·전투가 끝날 때 다시 계산
        private void RefreshPreemptBooks() =>
            preemptBooks = roaming == null ? new List<IReadOnlyList<WordEntry>>()
                : Preemptive.QuizBooks(area.Words != null ? area.Words.Words : null,
                    TrainingBooks().ConvertAll(b => (IReadOnlyList<WordEntry>)b.Words), session.Vocabulary);

        // 전투·화면 전환·창이 열려 있으면 멈춤. [선제공격] 문제를 푸는 동안은 느리게 계속 (그래도 부딪히면 문제가 끝난 뒤 전투)
        private void UpdateRoamers(float dt)
        {
            preemptView.Tick(dt);
            if (roaming == null)
            {
                if (preemptPrompt.activeSelf) preemptPrompt.SetActive(false);
                preemptLock.SetActive(false);
                return;
            }
            roamerViews.Tick(dt);
            bool quiz = preemptView.IsOpen;
            bool paused = inBattle || transitioning || fieldTutorialRunning || revealingGate || (IsPanelOpen && !quiz);
            if (!paused)
            {
                bool chance = preemptPrompt.activeSelf && preemptWindow < PreemptWindowSeconds;
                roaming.SlowFactor = quiz ? PreemptSlow : chance ? PreemptChanceSlow : 1f;
                var events = roaming.Tick(dt, walker.Position);
                roamerViews.Apply(events, roaming.Rules, roaming.SlowFactor);
                foreach (var e in events)
                {
                    if (e.Kind == RoamerEventKind.Noticed) Sound.Play(Sfx.Click);
                    // 웅덩이 몬스터 (#113): 튀어나올 때·뛰어들 때 먹물이 튐
                    if (e.Roamer.Lurker && (e.Kind == RoamerEventKind.Dived || (e.Kind == RoamerEventKind.Moved && walker.Map.Get(e.From) == FieldTile.Water)))
                    {
                        StartCoroutine(FieldFx.Play(transform, CellCenter(e.Kind == RoamerEventKind.Dived ? e.Roamer.Home : e.From), Fx.Smoke, 1f,
                            animationScale, 13, tint: new Color(0.42f, 0.34f, 0.62f)));
                        Sound.Play(Sfx.Flee);
                    }
                    if (e.Kind == RoamerEventKind.Caught && !quiz)
                    {
                        StartRoamerBattle(e.Roamer, null);
                        break;
                    }
                }
            }
            UpdatePreemptButton(paused || quiz, dt);
        }

        private void UpdatePreemptButton(bool hidden, float dt)
        {
            if (pauseTutorial.IsShowing)
            {
                // 튜토리얼로 멈춘 동안 (#79): 버튼·괄호는 밝게 그대로, 기회 원은 멈춤
                if (!preemptPrompt.activeSelf) preemptPrompt.SetActive(true);
                UpdatePreemptLock(roaming.PreemptTarget(walker.Position));
                return;
            }
            var target = hidden || IsPanelOpen || preemptBooks.Count == 0 ? null : roaming.PreemptTarget(walker.Position);
            bool show = target != null;
            if (preemptPrompt.activeSelf != show) preemptPrompt.SetActive(show);
            UpdatePreemptLock(target);
            if (!show)
            {
                preemptWindow = 0f;
                return;
            }
            // 기회 슬로모션 (QTE): 버튼이 뜨면 1.5초 동안 몬스터가 느려지고, 버튼 둘레의 금색 원이 줄어들며 남은 기회를 보여 줌
            // (Figma 'Preempt Prompt' Window=Open → Closing). 원이 다 줄면 원래 속도 — 그래도 부딪히기 전까지는 누를 수 있다
            preemptWindow += dt;
            float window = Mathf.Clamp01(preemptWindow / PreemptWindowSeconds);
            preemptButton.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.04f);
            preemptRing.gameObject.SetActive(window < 1f);
            preemptRing.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(300f, 204f, window);
            preemptRing.color = window > 0.7f ? Palette.Bad : Palette.Gold;
            if (showTutorials && !preemptTutorialRunning && !session.Tutorials.Has(TutorialProgress.Preempt))
                StartCoroutine(RunPreemptTutorial());
        }

        // 정글에서 처음 [선제공격] 기회 (#79, 사용자 요청 — 사용자가 정한 말): 화면을 멈추고 세 단계. 마지막은 버튼을 직접 눌러 봐야 끝
        private static readonly string[] PreemptTutorialLines =
        {
            "정글에 오신걸 환영합니다.\n이제 적이 당신을 직접 싸우러 옵니다.", // 첫 문장 뒤 줄바꿈 (#83 사용자)
            "일정 거리 적과 가까워지면 선제공격을 할 수 있습니다.",
            "버튼을 눌러보세요!"
        };

        private IEnumerator RunPreemptTutorial()
        {
            preemptTutorialRunning = true;
            if (moving) SnapPlayer();
            guide.Hide();
            pauseTutorial.RaiseBelow(markLayer); // 조준 괄호·범위·[선제공격] 버튼은 막 위에서 밝게, 버튼만 눌림
            bool completed = false;
            yield return pauseTutorial.Run(PreemptTutorialLines, () => preemptView.IsOpen,
                () => inBattle || roaming == null, step => preemptRange.gameObject.SetActive(step == 1), ok => completed = ok);
            preemptTutorialRunning = false;
            if (!completed) yield break;
            session.Tutorials.Mark(TutorialProgress.Preempt);
            SaveNow();
        }

        // 선제공격 거리의 몬스터에 빨간 조준 괄호 (1.5칸, 천천히 숨쉬듯)
        private void UpdatePreemptLock(Roamer target)
        {
            var point = target != null && roamerViews.WorldPosition(target) is Vector3 world ? MarkPoint(world) : null;
            preemptLock.SetActive(point.HasValue);
            if (!point.HasValue) return;
            preemptLock.Root.anchoredPosition = point.Value;
            preemptLock.SetSize(TilePixels() * 1.5f);
            if (preemptRange.gameObject.activeSelf)
            {
                // '선제공격 범위' = 몬스터에게서 PreemptRadius칸 (그 칸까지 감싸게 반 칸 더)
                preemptRange.rectTransform.anchoredPosition = point.Value;
                preemptRange.rectTransform.sizeDelta = Vector2.one * (roaming.Rules.PreemptRadius * 2 + 1) * TilePixels();
            }
            preemptLock.Root.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f);
        }

        // 월드 위치 → 몬스터 표시 층의 좌표 (가운데 기준 — PreemptView 창과 같은 좌표)
        private Vector2? MarkPoint(Vector3 world)
        {
            var canvasCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(markLayer, cam.WorldToScreenPoint(world), canvasCamera, out var local)
                ? local : (Vector2?)null;
        }

        // 한 칸이 HUD에서 몇 단위인지 (조준 괄호·QTE 원 크기)
        private float TilePixels()
        {
            var origin = cam.transform.position;
            var a = MarkPoint(new Vector3(origin.x, origin.y, 0f));
            var b = MarkPoint(new Vector3(origin.x + 1f, origin.y, 0f));
            return a.HasValue && b.HasValue ? Mathf.Max(20f, Mathf.Abs(b.Value.x - a.Value.x)) : 90f;
        }

        // [선제공격]: 몬스터가 느려지고 이미 본 단어 한 문제 → 맞히면 맨 앞 몬스터가 다친 채, 틀리면 그냥 전투 (사용자 결정)
        private void StartPreempt()
        {
            if (roaming == null || preemptView.IsOpen || inBattle || transitioning || preemptBooks.Count == 0) return;
            var target = roaming.PreemptTarget(walker.Position);
            if (target == null) return;
            if (moving) SnapPlayer();
            var quiz = new TrainingQuizProvider(preemptBooks, session.Vocabulary, battle.MasteryRules, TrainingMode.All, rng);
            var question = quiz.NextQuestion(QuizDirection.EnglishToMeaning);
            preemptPrompt.SetActive(false);
            preemptLock.SetActive(false);
            Vector2? Focus() => roamerViews.WorldPosition(target) is Vector3 world ? MarkPoint(world) : null;
            preemptView.Show(question, Preemptive.AnswerSeconds, index =>
            {
                bool correct = question.IsCorrect(index);
                quiz.SubmitAnswer(question, correct);
                if (!roaming.Has(target)) return;
                string intro;
                if (correct)
                {
                    int dealt = Preemptive.Strike(target.Leader);
                    PreemptStrikes++;
                    intro = $"선제공격 성공! {target.Leader.DisplayName}에게 {dealt} 피해";
                }
                else intro = index < 0 ? "선제공격 실패 — 시간 초과! 정면 승부다" : "선제공격 실패 — 정면 승부다!";
                StartRoamerBattle(target, intro);
            }, Focus, TilePixels, Preemptive.Damage(target.Leader), settings != null && settings.Vibration);
        }

        private void StartRoamerBattle(Roamer roamer, string intro)
        {
            if (inBattle || transitioning) return;
            if (moving) SnapPlayer();
            session.World.SetPosition(area.AreaId, walker.Position);
            battleRoamer = roamer;
            preemptPrompt.SetActive(false);
            preemptLock.SetActive(false);
            StartCoroutine(Encounter(roamer.Group, intro));
        }
    }
}
