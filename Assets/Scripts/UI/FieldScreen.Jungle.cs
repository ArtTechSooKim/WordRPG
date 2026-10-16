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
    // 필드 > 1.1 메아리 정글 기믹 (#80·#81): 레버 → 덩굴 다리, 단어 석판, 유적 제단(동료), 뗏목·나루터
    public partial class FieldScreen
    {
        // 1.1 메아리 정글 기믹 (#81)
        private TabletQuizView tabletView;      // 단어 석판 문제 (Figma '석판 문제 (#81)')
        private TabletQuiz tabletQuiz;          // 지금 푸는 석판
        private Vector2Int tabletCell;
        private readonly List<string> tabletFilled = new List<string>();
        private bool tabletBusy;                // 맞힌 뒤 다음 문제로 넘어가는 중
        private FieldStreams streams;           // 얕은 강 물결
        private FieldScatter scatter;           // 흩뿌림 꾸밈 — 풀·꽃·연잎·돌 부스러기 (#86)
        private FieldFog fog;                   // 서고 바닥 안개 띠 (#102)
        private SpriteRenderer raftRenderer;    // 타고 있는 뗏목
        private Sprite raftSprite;
        private readonly List<GameObject> mooredRafts = new List<GameObject>(); // 나루터마다 묶인 뗏목 (타는 동안은 숨김)
        private const float StreamStepRatio = 0.55f; // 물살을 따라 걸으면 한 칸 시간 × (사용자: 방향대로 이속 버프)
        private const float RaftStepRatio = 0.8f;    // 뗏목 한 칸 시간 × (걷기보다 조금 빠르게)

        // ------------------------------------------------------------------ 1.1 메아리 정글 기믹 (#81)

        // 레버 (사용자: 피곤하지 않게, 무엇이 생기는지 잘 보이게): 당기면 카메라가 덩굴 다리로 가서 판자가 하나씩 놓이는 걸 보여 주고 돌아온다
        private void PullLever(Vector2Int lever)
        {
            if (Levers.IsPulled(area, session.World, lever))
            {
                ShowToast("이미 당긴 레버다. 덩굴 다리가 놓여 있다.");
                return;
            }
            if (moving) SnapPlayer();
            StartCoroutine(BuildBridge(lever));
        }

        private IEnumerator BuildBridge(Vector2Int lever)
        {
            transitioning = true;
            var bridge = new List<Vector2Int>(Levers.Pull(area, session.World, lever));
            LeversPulled++;
            tilemap.SetTile(new Vector3Int(lever.x, lever.y, 0), TileFor(FieldTile.Lever, true));
            Sound.Play(Sfx.Click);
            SaveNow();
            if (bridge.Count > 0)
            {
                bridge.Sort((a, b) => (a - lever).sqrMagnitude.CompareTo((b - lever).sqrMagnitude)); // 레버 쪽부터 자람
                var center = Vector3.zero;
                foreach (var cell in bridge) center += CellCenter(cell);
                center /= bridge.Count;
                yield return PanCamera(center, 0.6f);
                yield return WaitUnscaled(0.25f);
                bool horizontal = walker.Map.IsBridgeHorizontal(bridge[0]);
                foreach (var cell in bridge)
                {
                    tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), BridgeTile(area.Theme, true, horizontal));
                    StartCoroutine(FieldFx.Play(transform, CellCenter(cell), Fx.Sparkle, 1.3f, animationScale, 12, tint: new Color(0.6f, 1f, 0.5f)));
                    Sound.Play(Sfx.EvolveLight);
                    yield return WaitUnscaled(0.22f);
                }
                ShowToast("덩굴 다리가 놓였다!", 2.5f);
                yield return WaitUnscaled(0.7f);
                yield return PanCamera(null, 0.5f);
            }
            transitioning = false;
            AfterFieldChange();
        }

        // 카메라를 to(없으면 주인공)로 부드럽게
        private IEnumerator PanCamera(Vector3? to, float seconds)
        {
            var visibleHeight = tilesAcross / Mathf.Max(0.1f, cam.aspect);
            var start = cameraFocus ?? player.position + Vector3.down * (visibleHeight * 0.10f);
            var end = to ?? player.position + Vector3.down * (visibleHeight * 0.10f);
            float duration = seconds * animationScale;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                cameraFocus = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            cameraFocus = to;
        }

        // 단어 석판: 이미 본 단어로 3문제 (선제공격과 같은 단어장). 다 채우면 금빛 석판(세이브), ✕로 그만두면 그 석판은 처음부터
        private void ReadTablet(Vector2Int cell)
        {
            if (Ruins.IsTabletSolved(area, session.World, cell))
            {
                bubble.Say("이미 빈칸을 채운 석판이다.", 2.5f);
                return;
            }
            var books = Preemptive.QuizBooks(area.Words != null ? area.Words.Words : null,
                TrainingBooks().ConvertAll(b => (IReadOnlyList<WordEntry>)b.Words), session.Vocabulary);
            if (books.Count == 0)
            {
                bubble.Say("아직 읽을 수 있는 글자가 없다…\n정글에서 단어를 더 만나 보자", 3.5f);
                return;
            }
            if (moving) SnapPlayer();
            tabletQuiz = new TabletQuiz(new TrainingQuizProvider(books, session.Vocabulary, battle.MasteryRules, TrainingMode.All, rng));
            tabletCell = cell;
            tabletFilled.Clear();
            tabletBusy = false;
            tabletView.Show(Ruins.TabletsSolved(area, session.World) + 1, Ruins.Tablets(area).Count, PickTablet, CloseTablet);
            tabletView.ShowQuestion(tabletQuiz.Current, 0, "틀려도 괜찮아요 — 다시 고르면 돼요");
        }

        private void PickTablet(int index)
        {
            if (tabletQuiz == null || tabletBusy) return;
            var question = tabletQuiz.Current;
            switch (tabletQuiz.Answer(index))
            {
                case TabletAnswer.Wrong:
                    Sound.Play(Sfx.Wrong);
                    tabletView.MarkWrong(index);
                    break;
                case TabletAnswer.Correct:
                    Sound.Play(Sfx.Correct);
                    tabletFilled.Add(question.CorrectAnswer);
                    tabletView.MarkCorrect(index, question.CorrectAnswer, tabletQuiz.Solved);
                    StartCoroutine(NextTabletQuestion());
                    break;
            }
        }

        private IEnumerator NextTabletQuestion()
        {
            tabletBusy = true;
            yield return WaitUnscaled(0.8f);
            if (tabletQuiz == null) yield break;
            tabletQuiz.Next();
            if (tabletQuiz.IsComplete)
            {
                Ruins.SolveTablet(area, session.World, tabletCell);
                SaveNow();
                tilemap.SetTile(new Vector3Int(tabletCell.x, tabletCell.y, 0), TileFor(FieldTile.Tablet, true));
                StartCoroutine(FieldFx.Play(transform, CellCenter(tabletCell), Fx.Sparkle, 1.5f, animationScale, 12));
                Sound.Play(Sfx.Evolve);
                int remaining = Ruins.Tablets(area).Count - Ruins.TabletsSolved(area, session.World);
                string region = area.Words != null ? area.Words.RegionName : area.DisplayName;
                string words = $"{region} 단어 {BossGate.WordsFound(area, session)} / {area.ShrineWords}";
                tabletView.ShowComplete(tabletFilled, remaining > 0 ? $"남은 석판 {remaining}개 · {words}" : $"석판을 모두 채웠다! · {words}");
            }
            else tabletView.ShowQuestion(tabletQuiz.Current, tabletQuiz.Solved, "틀려도 괜찮아요 — 다시 고르면 돼요");
            tabletBusy = false;
        }

        private void CloseTablet()
        {
            bool solved = tabletQuiz != null && tabletQuiz.IsComplete;
            tabletQuiz = null;
            tabletView.Hide();
            interactCooldown = 0.5f;
            if (!solved) return;
            AfterFieldChange();
            if (Ruins.HasShrine(area) && Ruins.State(area, session) != ShrineState.NeedTablets) return;
            if (Ruins.TabletsSolved(area, session.World) >= Ruins.Tablets(area).Count)
                bubble.Say("석판을 모두 채웠다!\n유적 안쪽 제단으로 가 보자", 3.5f);
        }

        // 유적 제단 (GDD 9-6 '처음 깨어날 때', 사용자 '이대로가자'): 조건이 덜 차면 혼잣말, 다 갖추면 동료 조우 연출 → 열쇠 힌트
        private void TouchShrine(Vector2Int cell)
        {
            int solved = Ruins.TabletsSolved(area, session.World), total = Ruins.Tablets(area).Count;
            string region = area.Words != null ? area.Words.RegionName : area.DisplayName;
            switch (Ruins.State(area, session))
            {
                case ShrineState.NeedTablets:
                    bubble.Say("석판의 빈칸을 먼저 채워야 할 것 같다", 3.5f, $"채운 석판 {solved} / {total}");
                    break;
                case ShrineState.NeedWords:
                    bubble.Say("메아리가 아직 약하다…\n정글 단어를 더 모으자", 4f, $"{region}에서 발견한 단어 {BossGate.WordsFound(area, session)} / {area.ShrineWords}");
                    break;
                case ShrineState.Ready:
                    StartCoroutine(AwakenShrine(cell));
                    break;
                case ShrineState.Awakened:
                    bubble.Say("잠들어 있던 석상이 사라진 빈 제단이다.", 2.5f);
                    break;
                default:
                    bubble.Say("오래된 제단이다.", 2f);
                    break;
            }
        }

        private IEnumerator AwakenShrine(Vector2Int cell)
        {
            var companion = area.ShrineCompanion;
            transitioning = true;
            StartCoroutine(FieldFx.Play(transform, CellCenter(cell) + Vector3.up * 0.5f, Fx.Sparkle, 2.4f, animationScale, 12));
            Sound.PlayJingle(Sfx.GateOpen);
            yield return WaitUnscaled(0.9f);
            transitioning = false;
            yield return MeetCompanion(companion);
            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.Shrine, true));
            DrawProps(area);
            AfterFieldChange();
            // 깨어난 동료의 첫마디 = 넓은 정글에서 보스 방 열쇠 찾기 힌트 (GDD 9-6)
            if (companion != null && BossGate.HasGate(area) && !BossGate.HasKey(area, session))
                bubble.Say("빈칸두꺼비가 석판 글자를 삼켰어…\n보스 방 열쇠는 강 건너 동쪽 기슭에 있을 거야", 6f, companion.DisplayName);
        }

        // 뗏목에 타고 내림 (사용자: 직접 몰기): 폴짝 소리. 처음 타면 안내를 본 것으로
        private void Board(bool boarding)
        {
            Sound.Play(Sfx.Flee);
            if (boarding) session.Tutorials.Mark(TutorialProgress.Raft);
            UpdateRaft();
        }

        // 타고 있는 뗏목 그림: 주인공은 앞쪽, 뒤쪽엔 동료가 앉음. 바라보는 방향으로 놓이고 살짝 출렁임
        private void UpdateRaft()
        {
            bool on = walker != null && walker.Rafting;
            if (raftRenderer.gameObject.activeSelf != on)
            {
                raftRenderer.gameObject.SetActive(on);
                foreach (var moored in mooredRafts) if (moored != null) moored.SetActive(!on);
            }
            if (!on) return;
            var facing = walker.Facing;
            var offset = (Vector3)(Vector2)facing.ToOffset();
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 0.04f;
            raftRenderer.transform.position = player.position - offset * 0.5f + new Vector3(0f, -0.28f + bob, 0f);
            bool vertical = facing == Direction.Up || facing == Direction.Down;
            raftRenderer.transform.localEulerAngles = new Vector3(0f, 0f, vertical ? 90f : 0f);
            raftRenderer.flipX = facing == Direction.Left || facing == Direction.Down;
        }

        // 나루터마다 묶인 뗏목 (어느 나루터에서든 탈 수 있음 — 타는 동안은 숨김)
        private void RefreshMooredRafts()
        {
            foreach (var moored in mooredRafts) if (moored != null) Destroy(moored);
            mooredRafts.Clear();
            if (raftSprite == null) return;
            var map = walker.Map;
            foreach (var dock in map.FindAll(FieldTile.Dock))
            {
                foreach (var step in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    if (!map.IsRaftWater(dock + step)) continue;
                    var go = new GameObject("MooredRaft", typeof(SpriteRenderer));
                    go.transform.SetParent(transform, false);
                    go.transform.position = CellCenter(dock + step) + new Vector3(0f, -0.2f, 0f);
                    go.transform.localEulerAngles = new Vector3(0f, 0f, step.x == 0 ? 90f : 0f);
                    var renderer = go.GetComponent<SpriteRenderer>();
                    renderer.sprite = raftSprite;
                    renderer.sortingOrder = 2;
                    go.SetActive(!walker.Rafting);
                    mooredRafts.Add(go);
                    break;
                }
            }
        }
    }
}
