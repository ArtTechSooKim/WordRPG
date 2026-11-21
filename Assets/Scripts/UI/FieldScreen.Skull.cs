using System.Collections;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 필드 > 힐끔해골 (#281 — 사용자 친구 의견, 규칙 ExamSkull): 의학 용어로 하는 사람이 그 지역 보스를 이기면 보석 옆에 나타남.
    // 부끄러워 등을 돌리고 있다가 가끔 주인공 쪽을 힐끔 보고 '…키킥'. [확인] = 그 지역 장의 확인문제 3개 (창 SkullQuizView)
    public partial class FieldScreen
    {
        private SkullQuizView skullView;
        private ExamSkullQuiz skullQuiz;
        private SpriteRenderer skullRenderer;
        private CharacterSheet skullSheet;
        private Vector2Int? skullCell;
        private Direction skullFacing = Direction.Down;
        private float skullGlanceUntil, skullNextGlance, skullNextGiggle;
        private bool skullBusy;

        public const float SkullHearDistance = 5f; // 이만큼 가까우면 힐끔·키킥
        private static readonly string[] SkullGiggles = { "…키킥", "(힐끔)", "키, 키킥…", "…(힐끔힐끔)" };

        public Vector2Int? SkullCell => skullCell;                                   // 테스트용
        public SkullQuizView SkullView => skullView;                                 // 테스트용
        public ExamSkullQuiz SkullQuiz => skullQuiz;                                 // 테스트용
        public Direction SkullFacing => skullFacing;                                 // 테스트용
        public bool SkullVisible => skullRenderer != null && skullRenderer.gameObject.activeSelf; // 테스트용

        private bool IsSkullAt(Vector2Int cell) => skullCell.HasValue && skullCell.Value == cell;

        // 지역에 들어올 때 · 보스를 이긴 뒤: 있어야 하면 보석 옆에 세움
        private void SetUpSkull()
        {
            skullCell = ExamSkull.IsHere(area, session) ? ExamSkull.Spot(area, null, walker != null ? walker.Position : (Vector2Int?)null) : null;
            if (!skullCell.HasValue)
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
                skullSheet = new CharacterSheet("Art/NinjaAdventure/Npcs/" + ExamSkull.ArtId);
            }
            skullRenderer.gameObject.SetActive(true);
            skullRenderer.transform.position = CellCenter(skullCell.Value);
            skullFacing = Direction.Down;
            skullNextGlance = Time.unscaledTime + 1f;
            skullNextGiggle = Time.unscaledTime + 1.5f;
            DrawSkull();
        }

        // 부끄러워 주인공 반대쪽을 보다가 1.5~3초마다 힐끔 (0.6초). 가까우면 가끔 '…키킥'
        private void UpdateSkull()
        {
            if (!skullCell.HasValue || skullRenderer == null) return;
            var offset = walker.Position - skullCell.Value;
            float distance = Mathf.Abs(offset.x) + Mathf.Abs(offset.y);
            var toward = Toward(offset);
            bool near = distance <= SkullHearDistance;
            float now = Time.unscaledTime;
            if (skullBusy) skullFacing = toward; // 문제를 내는 동안은 마주 봄
            else if (near && now >= skullNextGlance)
            {
                skullGlanceUntil = now + 0.6f;
                skullNextGlance = now + Random.Range(1.5f, 3f);
            }
            if (!skullBusy) skullFacing = near && now < skullGlanceUntil ? toward : Away(toward);
            if (near && !skullBusy && now >= skullNextGiggle && !bubble.IsVisible && !inBattle && !transitioning && !IsPanelOpen)
            {
                bubble.Speak(skullRenderer.transform, ExamSkull.DisplayName, SkullGiggles[Random.Range(0, SkullGiggles.Length)], 1.6f);
                skullNextGiggle = now + Random.Range(7f, 11f);
            }
            DrawSkull();
        }

        private void DrawSkull()
        {
            if (skullSheet == null || !skullSheet.IsLoaded) return;
            skullRenderer.sprite = skullSheet.Get(skullFacing, 0);
            // 소름 끼치게 숨죽여 웃는 듯 작게 들썩 (힐끔 보는 동안 조금 더)
            float shake = Time.unscaledTime < skullGlanceUntil ? 0.035f : 0.015f;
            skullRenderer.transform.position = CellCenter(skullCell.Value) + Vector3.up * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) * shake;
        }

        private static Direction Toward(Vector2Int offset)
        {
            if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)) return offset.x >= 0 ? Direction.Right : Direction.Left;
            return offset.y >= 0 ? Direction.Up : Direction.Down;
        }

        private static Direction Away(Direction d) =>
            d == Direction.Right ? Direction.Left : d == Direction.Left ? Direction.Right : d == Direction.Up ? Direction.Down : Direction.Up;

        // 바라보는 칸(없으면 옆 칸)에 해골이 있으면 문제 창
        private bool TryTalkToSkull(bool facingOnly)
        {
            if (!skullCell.HasValue || walker.Rafting) return false;
            var offset = skullCell.Value - walker.Position;
            if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) != 1) return false;
            Direction? facing = null;
            foreach (var direction in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
                if (direction.ToOffset() == offset) facing = direction;
            if (!facing.HasValue || (facingOnly && walker.Facing != facing.Value)) return false;
            walker.Face(facing.Value);
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            interactions++;
            OpenSkullQuiz();
            return true;
        }

        private void OpenSkullQuiz()
        {
            var pool = ExamSkull.Pool(area);
            if (pool.Count == 0) return;
            if (moving) SnapPlayer();
            bubble.Hide();
            skullBusy = true;
            skullQuiz = new ExamSkullQuiz(pool, session.World, rng);
            skullView.Show(PickSkull, CloseSkull);
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
                    skullView.MarkWrong(index, "히히… 아니야… 다시 골라 봐…");
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
                        gold > 0 ? "키킥… 맞혔어… 이거, 가져…" : "키킥… 맞혔어…");
                    StartCoroutine(NextSkullQuestion());
                    break;
            }
        }

        private IEnumerator NextSkullQuestion()
        {
            yield return new WaitForSecondsRealtime(1.1f * animationScale);
            if (skullQuiz == null || !skullView.IsOpen) yield break;
            skullQuiz.Next();
            if (skullQuiz.IsComplete)
                skullView.ShowComplete(skullQuiz.FirstTryCorrect, skullQuiz.Count, skullQuiz.GoldEarned, "오, 오늘은… 여기까지… 키킥. 또… 와…");
            else
                skullView.ShowQuestion(skullQuiz.Current, skullQuiz.Index, skullQuiz.Count, skullQuiz.GoldEarned, "…키킥. 다음… 이거는?");
        }

        private void CloseSkull()
        {
            skullView.Hide();
            skullQuiz = null;
            skullBusy = false;
            skullNextGiggle = Time.unscaledTime + 4f;
            interactCooldown = 0.4f;
            saveProgress?.Invoke();
            RefreshHud();
        }
    }
}
