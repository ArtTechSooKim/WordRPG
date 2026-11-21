using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 필드 > 힐끔해골 (#281 → #283 사용자 친구 의견, 규칙 ExamSkull): 의학 용어로 하는 사람이 그 지역 보스를 이기면
    // 보석 가까운 벽(책장·나무·바위) 뒤에서 얼굴 반쪽만 내밀고 숨어 있음 — 가까이 오면 힐끔 더 내밀었다 숨고 '…키킥'.
    // [확인] = '○○ 문제인데… 한번 풀어 볼래?' → [풀어보기] = 번쩍 → 해골의 방에서 3문제 (SkullQuizView).
    // 많이 맞히면 슬금슬금 도망 → 다음 벽 뒤로. 벽 칸 안에 숨으니 길을 막지 않음
    public partial class FieldScreen
    {
        private SkullQuizView skullView;
        private ExamSkullQuiz skullQuiz;
        private SpriteRenderer skullRenderer;
        private Texture2D skullSheet;
        private readonly Dictionary<(Vector2Int, int), Sprite> skullSlices = new Dictionary<(Vector2Int, int), Sprite>();
        private List<ExamSkull.Peek> skullSpots = new List<ExamSkull.Peek>();
        private int skullSpot = -1;
        private float skullShow, skullGlanceUntil, skullNextGlance, skullNextGiggle;
        private bool skullBusy;

        public const float SkullHearDistance = 5f; // 이만큼 가까우면 힐끔·키킥
        private const int SkullHidden = 4, SkullPeeking = 7, SkullGlance = 11; // 벽에서 내민 얼굴 폭 (16px 중)
        private static readonly string[] SkullGiggles = { "…키킥", "(힐끔)", "키, 키킥…", "…(힐끔힐끔)" };
        private static readonly string[] SkullSneers = { "키킥… 그것도 몰라…?", "히히… 다시 공부하고 와…", "키킥… 그건 아니지…" };

        public ExamSkull.Peek? SkullPeek => skullSpot >= 0 && skullSpot < skullSpots.Count ? skullSpots[skullSpot] : (ExamSkull.Peek?)null; // 테스트용
        public SkullQuizView SkullView => skullView;                                 // 테스트용
        public ExamSkullQuiz SkullQuiz => skullQuiz;                                 // 테스트용
        public bool SkullVisible => skullRenderer != null && skullRenderer.gameObject.activeSelf; // 테스트용
        public int SkullShownPixels => Mathf.RoundToInt(skullShow);                  // 테스트용 (얼굴을 얼마나 내밀었는지)

        // 지역에 들어올 때 · 보스를 이긴 뒤: 있어야 하면 첫 벽 뒤에 숨김
        private void SetUpSkull()
        {
            skullSpots = ExamSkull.IsHere(area, session) ? ExamSkull.PeekSpots(area) : new List<ExamSkull.Peek>();
            skullSpot = skullSpots.Count > 0 ? 0 : -1;
            if (skullSpot < 0)
            {
                if (skullRenderer != null) skullRenderer.gameObject.SetActive(false);
                return;
            }
            if (skullRenderer == null)
            {
                var go = new GameObject("ExamSkull", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                skullRenderer = go.GetComponent<SpriteRenderer>();
                skullRenderer.sortingOrder = 9; // 주인공(10) 바로 아래, 동굴에서는 어둠 아래라 빛 안에서만 보임 (인물과 같음)
                skullSheet = Resources.Load<Texture2D>("Art/NinjaAdventure/Npcs/" + ExamSkull.ArtId);
            }
            skullRenderer.gameObject.SetActive(true);
            skullShow = SkullPeeking;
            skullNextGlance = Time.unscaledTime + 1f;
            skullNextGiggle = Time.unscaledTime + 1.5f;
            DrawSkull();
        }

        // 가까이 오면 1.5~3초마다 0.7초씩 얼굴을 더 내밀었다(힐끔) 숨고, 가끔 '…키킥'. 멀면 거의 숨음
        private void UpdateSkull()
        {
            var peek = SkullPeek;
            if (!peek.HasValue || skullRenderer == null) return;
            var offset = walker.Position - peek.Value.Floor;
            float distance = Mathf.Abs(offset.x) + Mathf.Abs(offset.y);
            bool near = distance <= SkullHearDistance;
            float now = Time.unscaledTime;
            if (near && now >= skullNextGlance)
            {
                skullGlanceUntil = now + 0.7f;
                skullNextGlance = now + Random.Range(1.5f, 3f);
            }
            float target = skullBusy ? SkullGlance : !near ? SkullHidden : now < skullGlanceUntil ? SkullGlance : SkullPeeking;
            skullShow = Mathf.MoveTowards(skullShow, target, Time.unscaledDeltaTime * 30f);
            if (near && !skullBusy && now >= skullNextGiggle && !bubble.IsVisible && !inBattle && !transitioning && !IsPanelOpen)
            {
                bubble.Speak(skullRenderer.transform, ExamSkull.DisplayName, SkullGiggles[Random.Range(0, SkullGiggles.Length)], 1.6f);
                skullNextGiggle = now + Random.Range(7f, 11f);
            }
            DrawSkull();
        }

        // 벽 칸 가장자리에서 바닥 쪽으로 얼굴 몇 픽셀만: 오른쪽·왼쪽으로 내밀면 정면 그림의 그쪽 반, 위로 내밀면 머리 윗부분
        private void DrawSkull()
        {
            var peek = SkullPeek;
            if (!peek.HasValue || skullSheet == null) return;
            int px = Mathf.Clamp(Mathf.RoundToInt(skullShow), 1, 16);
            var dir = peek.Value.Out;
            skullRenderer.sprite = SkullSlice(dir, px);
            var wall = peek.Value.Wall;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) * 0.02f; // 숨죽여 웃듯 작게 들썩
            skullRenderer.transform.position = dir == Vector2Int.right ? new Vector3(wall.x + 1f, wall.y + 0.5f + bob, 0f)
                : dir == Vector2Int.left ? new Vector3(wall.x, wall.y + 0.5f + bob, 0f)
                : new Vector3(wall.x + 0.5f, wall.y + 1f + bob, 0f);
        }

        private Sprite SkullSlice(Vector2Int dir, int px)
        {
            if (skullSlices.TryGetValue((dir, px), out var cached) && cached != null) return cached;
            int top = skullSheet.height - 16; // 정면(아래를 봄) 첫 칸
            Rect rect;
            Vector2 pivot;
            if (dir == Vector2Int.right) { rect = new Rect(16 - px, top, px, 16); pivot = new Vector2(0f, 0.5f); }
            else if (dir == Vector2Int.left) { rect = new Rect(0, top, px, 16); pivot = new Vector2(1f, 0.5f); }
            else { rect = new Rect(0, top + 16 - px, 16, px); pivot = new Vector2(0.5f, 0f); }
            var sprite = Sprite.Create(skullSheet, rect, pivot, 16, 0, SpriteMeshType.FullRect);
            sprite.name = $"exam_skull_peek_{dir}_{px}";
            skullSlices[(dir, px)] = sprite;
            return sprite;
        }

        // 해골이 숨은 벽 옆에서 그 벽을 보고 [확인] (바라보지 않아도 옆이면 — facingOnly=false)
        private bool TryTalkToSkull(bool facingOnly)
        {
            var peek = SkullPeek;
            if (!peek.HasValue || walker.Rafting) return false;
            var offset = peek.Value.Wall - walker.Position;
            if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) != 1) return false;
            Direction? facing = null;
            foreach (var direction in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
                if (direction.ToOffset() == offset) facing = direction;
            if (!facing.HasValue || (facingOnly && walker.Facing != facing.Value)) return false;
            walker.Face(facing.Value);
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            interactions++;
            OpenSkullInvite();
            return true;
        }

        private bool IsNextToSkull() =>
            SkullPeek.HasValue && !walker.Rafting
            && Mathf.Abs(SkullPeek.Value.Wall.x - walker.Position.x) + Mathf.Abs(SkullPeek.Value.Wall.y - walker.Position.y) == 1;

        private void OpenSkullInvite()
        {
            var pool = ExamSkull.Pool(area);
            if (pool.Count == 0) return;
            if (moving) SnapPlayer();
            bubble.Hide();
            skullBusy = true;
            skullQuiz = new ExamSkullQuiz(pool, session.World, rng);
            skullView.ShowInvite(skullQuiz.Topic, EnterSkullRoom, CloseSkull);
        }

        private void EnterSkullRoom()
        {
            Sound.Play(Sfx.Encounter); // 번쩍 — 해골의 방으로 끌려감
            skullView.EnterRoom(PickSkull, CloseSkull);
            skullView.ShowQuestion(skullQuiz.Current, 0, skullQuiz.Count, 0, "히, 히익…! 들켰다… 키킥. 이, 이거… 알아?");
        }

        private void PickSkull(int index)
        {
            if (skullQuiz == null || skullQuiz.IsComplete) return;
            var question = skullQuiz.Current;
            var (result, gold) = skullQuiz.Answer(index);
            switch (result)
            {
                case TabletAnswer.Wrong:
                    Sound.Play(Sfx.Wrong);
                    skullView.MarkWrong(index, SkullSneers[Random.Range(0, SkullSneers.Length)]);
                    break;
                case TabletAnswer.Correct:
                    Sound.Play(Sfx.Correct);
                    if (gold > 0)
                    {
                        session.Inventory.AddGold(gold);
                        Sound.Play(Sfx.Coin);
                    }
                    saveProgress?.Invoke(); // 맞힌 문제·골드는 바로 세이브
                    skullView.MarkCorrect(index, question.CorrectAnswer, gold, skullQuiz.GoldEarned,
                        gold > 0 ? "키, 키킥… 맞혔어… 이거, 가져…" : "키킥… 맞혔어…");
                    StartCoroutine(NextSkullQuestion());
                    break;
            }
        }

        private IEnumerator NextSkullQuestion()
        {
            yield return new WaitForSecondsRealtime(1.1f * animationScale);
            if (skullQuiz == null || !skullView.InRoom) yield break;
            skullQuiz.Next();
            if (!skullQuiz.IsComplete)
            {
                skullView.ShowQuestion(skullQuiz.Current, skullQuiz.Index, skullQuiz.Count, skullQuiz.GoldEarned, "…키킥. 다음… 이거는?");
                yield break;
            }
            bool flees = skullQuiz.Flees;
            if (flees) Sound.Play(Sfx.Flee); // 휙 — 슬금슬금 도망
            skullView.ShowEnd(skullQuiz.FirstTryCorrect, skullQuiz.Count, skullQuiz.GoldEarned, flees,
                flees ? "히, 히익…! 오, 오늘은 이만…!" : "키킥… 공부 더 하고 와… 또 기다릴게…");
        }

        // [안 할래]·×·[확인]·뒤로가기: 방에서 나오면 번쩍. 도망쳤으면 다음 벽 뒤로 옮겨 숨음
        private void CloseSkull()
        {
            bool fled = skullQuiz != null && skullQuiz.Flees;
            bool fromRoom = skullView.InRoom;
            skullView.Hide();
            skullQuiz = null;
            skullBusy = false;
            if (fromRoom) StartCoroutine(SkullFlashBack());
            if (fled && skullSpots.Count > 1)
            {
                skullSpot = (skullSpot + 1) % skullSpots.Count;
                skullShow = SkullHidden;
                DrawSkull();
            }
            skullNextGiggle = Time.unscaledTime + 4f;
            interactCooldown = 0.4f;
            saveProgress?.Invoke();
            RefreshHud();
        }

        // 필드로 돌아올 때 화면이 하얗게 번쩍 (방에 들어갈 때와 짝)
        private IEnumerator SkullFlashBack()
        {
            flash.gameObject.SetActive(true);
            float duration = SkullQuizView.FlashSeconds * animationScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(1f, 1f, 1f, 1f - t / Mathf.Max(0.001f, duration));
                yield return null;
            }
            flash.gameObject.SetActive(false);
        }
    }
}
