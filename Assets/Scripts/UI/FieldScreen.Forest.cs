using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 필드 > 1.4 숲 넓히기 — 뒤섞인 이정표 미로 (#279, GDD 9-15): 이정표 [확인] → 철자 문제 → 바로잡으면 헷갈 안개가 걷힘 ·
    // 걷히기 전 안개와 맴도는 안개(가짜 길)에 부딪히면 '안개 속을 맴돌다 제자리' (나무꾼이 말한 '같은 자리를 맴돌기')
    public partial class FieldScreen
    {
        private SignpostQuizView signpostView;  // Figma '이정표 문제 (#279)'
        private SignpostQuiz signpostQuiz;
        private Vector2Int signpostCell;
        private bool signpostFixedNow;          // 맞혀서 바로잡음 (창을 닫으면 안개가 걷힘)
        private float hazeLoopCooldown;         // 스틱을 밀고 있어도 맴돌기가 연달아 나오지 않게

        public const float HazeLoopSeconds = 0.75f;
        public SignpostQuizView SignpostView => signpostView;          // 테스트용
        public SpellingQuestion SignpostQuestion => signpostQuiz?.Current;
        public int HazeLoops { get; private set; }                      // 맴돌기 연출 횟수 (테스트용)
        public int SignpostsFixed { get; private set; }                 // 이번에 바로잡은 이정표 수 (테스트용)

        // 이정표 [확인]: 이미 본 단어 하나로 뒤섞인 철자 문제. 본 단어가 없으면 말만
        private void TouchSignpost(Vector2Int cell)
        {
            if (Signposts.IsFixed(area, session.World, cell))
            {
                bubble.Say("이정표가 바른 길을 가리키고 있다", 2.5f);
                return;
            }
            var books = Preemptive.QuizBooks(area.Words != null ? area.Words.Words : null,
                TrainingBooks().ConvertAll(b => (IReadOnlyList<WordEntry>)b.Words), session.Vocabulary);
            if (books.Count == 0)
            {
                bubble.Say("글자가 뒤죽박죽이다…\n단어를 더 만나면 읽을 수 있을 것 같다", 3.5f);
                return;
            }
            if (moving) SnapPlayer();
            var realWords = new HashSet<string>();
            foreach (var book in books)
            foreach (var word in book)
                realWords.Add(word.English.ToLowerInvariant());
            signpostQuiz = new SignpostQuiz(PuzzleWords(books), rng, realWords); // 이정표마다 다른 단어 (#290)
            signpostCell = cell;
            signpostFixedNow = false;
            signpostView.Show(signpostQuiz.Current, Signposts.FixedCount(area, session.World), Signposts.All(area).Count, PickSignpost, CloseSignpost);
        }

        private void PickSignpost(int index)
        {
            if (signpostQuiz == null || signpostFixedNow) return;
            var question = signpostQuiz.Current;
            switch (signpostQuiz.Answer(index))
            {
                case TabletAnswer.Wrong:
                    Sound.Play(Sfx.Wrong);
                    signpostView.MarkWrong(index);
                    break;
                case TabletAnswer.Correct:
                    Sound.Play(Sfx.Correct);
                    signpostFixedNow = true;
                    Signposts.Fix(area, session.World, signpostCell);
                    SaveNow(); // ×로 닫아도 바로잡은 것은 남게
                    signpostView.ShowFixed(question.CorrectAnswer, Signposts.FixedCount(area, session.World), Signposts.All(area).Count);
                    break;
            }
        }

        private void CloseSignpost()
        {
            bool fixedNow = signpostFixedNow;
            signpostQuiz = null;
            signpostFixedNow = false;
            signpostView.Hide();
            interactCooldown = 0.5f;
            if (fixedNow) StartCoroutine(ClearHaze(signpostCell));
        }

        // 바로잡은 순간: 이정표가 곧게 서고, 맡은 안개가 이정표 쪽부터 한 칸씩 걷힘 (덩굴 다리가 놓일 때처럼)
        private IEnumerator ClearHaze(Vector2Int signpost)
        {
            transitioning = true; // 걷히는 동안 [확인]이 옆 물건을 누르지 않게 (#251)
            if (moving) SnapPlayer();
            SignpostsFixed++;
            tilemap.SetTile(new Vector3Int(signpost.x, signpost.y, 0), TileFor(FieldTile.Signpost, true));
            StartCoroutine(FieldFx.Play(transform, CellCenter(signpost), Fx.Leaf, 1.2f, animationScale, 16, tint: new Color(0.75f, 1f, 0.6f)));
            var haze = new List<Vector2Int>(walker.Map.HazeOf(signpost));
            haze.Sort((a, b) => (a - signpost).sqrMagnitude.CompareTo((b - signpost).sqrMagnitude));
            yield return WaitUnscaled(0.25f);
            foreach (var cell in haze)
            {
                tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.Haze, true));
                StartCoroutine(FieldFx.Play(transform, CellCenter(cell), Fx.SmokeRing, 1.2f, animationScale, 12, tint: new Color(0.86f, 0.95f, 0.82f)));
                Sound.Play(Sfx.VineGrow);
                yield return WaitUnscaled(0.12f);
            }
            bool all = Signposts.FixedCount(area, session.World) >= Signposts.All(area).Count;
            if (all) Sound.PlayJingle(Sfx.DexComplete);
            else Sound.PlayJingle(Sfx.GateOpen); // 길이 열림 — 덩굴 다리와 같은 소리
            ShowToast(all ? "숲의 이정표를 모두 바로잡았다!" : "헷갈 안개가 걷혔다!", 2.5f);
            yield return WaitUnscaled(0.4f);
            if (all) yield return RevealSecretPath(); // 비밀의 숲 가는 길 (#292)
            transitioning = false;
            AfterFieldChange();
        }

        public int SecretPathsRevealed { get; private set; } // 테스트용
        public int BossHazeReveals { get; private set; }     // 테스트용

        // 서 있을 때 (UpdateNpcs): 숲 보스를 이긴 뒤 오른쪽 끝 보스 뒤 숨은 안개에 가까이 오면 한 번 걷힘 (#292 — 보스 직후 연출로 알려 주지 않고 찾는 재미)
        private void MaybeRevealBossHaze()
        {
            if (cutsceneRunning || springJumping) return;
            if (!Signposts.BossHazeDue(area, session.World, walker.Position)) return;
            StartCoroutine(RevealBossHaze());
        }

        private IEnumerator RevealBossHaze()
        {
            transitioning = true;
            if (moving) SnapPlayer(); // 달려오다가도 그 칸에 서서 봄
            session.World.MarkEvent(Signposts.BossHazeEventId(area));
            SaveNow();
            var cells = Signposts.BossHaze(area);
            var hero = walker.Position;
            cells.Sort((a, b) => (a - hero).sqrMagnitude.CompareTo((b - hero).sqrMagnitude)); // 가까운 쪽부터
            yield return WaitUnscaled(0.2f);
            foreach (var cell in cells)
            {
                tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.BossHaze, true));
                StartCoroutine(FieldFx.Play(transform, CellCenter(cell), Fx.SmokeRing, 1.3f, animationScale, 12, tint: new Color(1f, 0.9f, 0.55f)));
                Sound.Play(Sfx.VineGrow);
                yield return WaitUnscaled(0.14f);
            }
            Sound.PlayJingle(Sfx.GateOpen);
            BossHazeReveals++;
            bubble.Say("어? 안개가 스르르 걷혔다…\n저 너머에서 금빛이 반짝인다", 3.5f);
            yield return WaitUnscaled(0.4f);
            transitioning = false;
            AfterFieldChange();
        }

        // 마지막 이정표를 바로잡으면 (#292 사용자: 이정표로 밝아진 안개로 드러난 입구): 카메라가 숨은 안개로 가서 안개가 걷히는 모습 → 주인공에게 돌아옴
        private IEnumerator RevealSecretPath()
        {
            var secret = Signposts.SecretHaze(area);
            if (secret.Count == 0) yield break;
            yield return WaitUnscaled(1.2f); // '모두 바로잡았다'를 읽을 시간
            var center = Vector3.zero;
            foreach (var cell in secret) center += CellCenter(cell);
            center /= secret.Count;
            var key = area.Map.Find(FieldTile.Key);
            if (key.HasValue) center = (center + CellCenter(key.Value)) * 0.5f; // 열쇠 공터 (#292) — 안개와 열쇠 받침대가 같이 보이게
            yield return PanCamera(center, 0.9f);
            yield return WaitUnscaled(0.3f);
            secret.Sort((a, b) => b.y.CompareTo(a.y)); // 위(골짜기 쪽)부터
            foreach (var cell in secret)
            {
                tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), TileFor(FieldTile.SecretHaze, true));
                StartCoroutine(FieldFx.Play(transform, CellCenter(cell), Fx.SmokeRing, 1.3f, animationScale, 12, tint: new Color(1f, 0.93f, 0.7f)));
                Sound.Play(Sfx.VineGrow);
                yield return WaitUnscaled(0.14f);
            }
            Sound.PlayJingle(Sfx.GateOpen);
            ShowToast("이정표들이 한곳을 가리킨다…\n숨겨진 길이 드러났다!", 3f);
            SecretPathsRevealed++;
            minimap.Redraw();
            yield return WaitUnscaled(1.5f);
            yield return PanCamera(null, 0.7f);
        }

        // 안개에 부딪힘 (#279): 안개 속으로 한 발 들어갔다가 흐려지며 제자리로 — 같은 자리를 맴도는 느낌
        private void BumpHaze(Vector2Int cell, Direction direction)
        {
            if (hazeLoopCooldown > 0f || transitioning) return;
            hazeLoopCooldown = HazeLoopSeconds + 0.6f;
            HazeLoops++;
            StartCoroutine(HazeLoop(cell, direction));
        }

        private IEnumerator HazeLoop(Vector2Int cell, Direction direction)
        {
            transitioning = true;
            var home = CellCenter(walker.Position);
            var into = Vector3.Lerp(home, CellCenter(cell), 0.55f);
            StartCoroutine(FieldFx.Play(transform, CellCenter(cell), Fx.SmokeRing, 1.4f, animationScale, 12, tint: new Color(0.86f, 0.95f, 0.82f)));
            Sound.Play(Sfx.SneakPotion); // '스스슥' — 안개 속을 헤치는 소리
            float half = HazeLoopSeconds * 0.5f * animationScale;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                float k = t / Mathf.Max(0.001f, half);
                player.position = Vector3.Lerp(home, into, k);
                SetPlayerAlpha(1f - 0.75f * k);
                yield return null;
            }
            // 안개 속에서 한 바퀴 — 돌아 나오면 반대쪽을 봄
            walker.Face(Opposite(direction));
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                float k = t / Mathf.Max(0.001f, half);
                player.position = Vector3.Lerp(into, home, k);
                SetPlayerAlpha(0.25f + 0.75f * k);
                yield return null;
            }
            player.position = home;
            SetPlayerAlpha(1f);
            transitioning = false;
            var bumped = walker.Map.Get(cell);
            bool decoy = bumped == FieldTile.LoopHaze || bumped == FieldTile.SecretHaze || bumped == FieldTile.BossHaze; // 숨은 안개도 가짜 길처럼 (#292)
            bool nearFixed = false;
            foreach (var signpost in Signposts.All(area))
                if ((signpost - cell).sqrMagnitude <= 64 && Signposts.IsFixed(area, session.World, signpost)) nearFixed = true;
            bubble.Say(decoy && nearFixed
                ? "이 안개 속은 같은 자리를 맴돌 뿐이다…\n이정표가 가리키는 쪽으로 가자"
                : "안개 속을 맴돌다 제자리로 돌아왔다…\n이정표를 바로잡아야 할 것 같다", 3f);
        }

        private void UpdateHazeLoop(float dt)
        {
            if (hazeLoopCooldown > 0f) hazeLoopCooldown -= dt;
        }

        private void SetPlayerAlpha(float alpha)
        {
            var c = playerRenderer.color;
            c.a = alpha;
            playerRenderer.color = c;
        }

        private static Direction Opposite(Direction d) =>
            d == Direction.Up ? Direction.Down : d == Direction.Down ? Direction.Up : d == Direction.Left ? Direction.Right : Direction.Left;
    }
}
