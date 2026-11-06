using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 — 회복의 샘: 회복 + 다른 지역의 샘으로 이동 (#216 사용자, GDD 9-11).
    // 옛날부터 사람들은 샘으로 오갔다 — 샘으로 쏙 뛰어들었다가 그 지역 샘에서 쏙 튀어나온다 (#217 사용자). 동료도 함께
    public partial class FieldScreen
    {
        private const float SpringJumpSeconds = 0.45f;
        private const float SpringJumpHeight = 0.9f;
        private const float FollowerJumpDelay = 0.12f; // 동료는 조금 뒤에 따라 뛰어듦
        private const float DiveShrinkFrom = 0.6f;     // 뛰어들 때 이 지점부터 작아지며 쏙
        private const float SurfaceGrowUntil = 0.4f;   // 튀어나올 때 이 지점까지 커짐
        private const float SurfaceOvershoot = 1.15f;  // 튀어나오며 살짝 크게 → 원래 크기
        private const float ElderLeapHeight = 1.8f;    // 어르신은 높이 쓩 (#223)
        private const float ElderShrinkFrom = 0.85f;   // 높이 뛰므로 물에 닿을 때쯤 작아지고 물이 튐
        private const float SpringMonologueSeconds = 3.2f;

        private ConfirmDialog springDialog;
        private SpringWarpView springWarpView;
        private bool springJumping;

        public ConfirmDialog SpringDialog => springDialog;       // 테스트용
        public SpringWarpView SpringWarpPicker => springWarpView; // 테스트용
        public Vector3 PlayerScale => player.localScale;         // 테스트용
        public Vector3 PlayerWorld => player.position;           // 테스트용

        // 먼저 회복하고, 갈 수 있는 다른 샘이 있으면 묻는다 ([취소]해도 회복은 그대로). 갈 곳이 하나면 바로, 여럿이면 고르는 창
        private void UseFountain(Vector2Int spring)
        {
            session.RestoreHero();
            SaveNow();
            RefreshHud();
            Sound.Play(Sfx.Fountain);

            var destinations = SpringDestinations();
            if (destinations.Count == 0)
            {
                ShowToast("회복의 샘 — HP가 모두 회복되었다!");
                return;
            }
            string where = destinations.Count == 1 ? $"{destinations[0].DisplayName}의 샘으로" : "다른 지역으로";
            springDialog.Show("회복의 샘", $"HP가 모두 회복되었습니다.\n{where} 이동하시겠습니까?", "확인", false, () =>
            {
                if (destinations.Count == 1) StartCoroutine(WarpToSpring(spring, destinations[0]));
                else springWarpView.Show(destinations, target => StartCoroutine(WarpToSpring(spring, target)));
            }, "skill_heal");
        }

        private List<FieldArea> SpringDestinations() => SpringWarp.Destinations(database != null ? database.Areas : null, area, session.World,
            entitlements == null || entitlements.HasFullVersion);

        // 주인공 혼잣말 (#223): 어르신이 뛰어드는 걸 봤으면 따라 해 볼까, 아니면 물 냄새로 스스로 알아챔
        private string SpringMonologue()
        {
            if (SpringLeap.AnyoneLeapt(session.World)) return "어르신처럼… 나도 뛰어들어 볼까?";
            var destinations = SpringDestinations();
            return destinations.Count > 0 ? $"이 물… {destinations[0].DisplayName} 샘이랑 같은 냄새가 나. 이어져 있나?" : "이 샘물… 어디로 이어져 있을까?";
        }

        // 어르신 컷씬 (#223 사용자): 말이 돌아온 어르신에게 가까이 가면 한 번 — 샘 이야기를 하고 샘으로 쓩 뛰어들어 사라짐
        private void MaybeStartSpringLeap()
        {
            if (springJumping || transitioning || bubble.IsVisible || fieldTutorialRunning || NeedsPrologue || guide.IsShowing) return;
            foreach (var view in npcs.Views)
            {
                if (!SpringLeap.Ready(view.Data, session.World)) continue;
                var offset = view.Motion.Cell - walker.Position;
                if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) > SpringLeap.NoticeRange) continue;
                if (SpringWarp.SpringOf(area.Map) == null || SpringDestinations().Count == 0) continue;
                StartCoroutine(SpringLeapCutscene(view));
                return;
            }
        }

        private IEnumerator SpringLeapCutscene(NpcView elder)
        {
            var spring = SpringWarp.SpringOf(area.Map).Value;
            transitioning = true;
            springJumping = true; // 동료도 제자리
            SnapPlayer();
            cameraFocus = CameraTarget(player.position);
            walker.Face(FacingToward(elder.Motion.Cell - walker.Position));
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            elder.Motion.Hold(FacingToward(walker.Position - elder.Motion.Cell));
            npcs.Redraw(elder);

            talkingNpc = elder;
            foreach (var line in elder.Data.SpringLeapLines)
            {
                float seconds = Mathf.Clamp(0.8f + line.Length * 0.07f, 1f, 3f);
                bubble.Speak(elder.Data.DisplayName, NpcLines.Render(line, session.Hero.DisplayName), seconds);
                yield return WaitUnscaled(seconds);
            }
            bubble.Hide();
            talkingNpc = null;

            // 쓩
            elder.Motion.Hold(FacingToward(spring - elder.Motion.Cell));
            npcs.Redraw(elder);
            Sound.Play(Sfx.Flee);
            yield return Dive(elder.Body, elder.Position, CellCenter(spring), elder.Body.localScale, 0f, true, ElderLeapHeight, ElderShrinkFrom);
            npcs.Remove(elder);
            session.World.MarkEvent(SpringLeap.EventId(elder.Data));
            SaveNow();

            yield return WaitUnscaled(0.5f);
            bubble.Say("어, 어르신?!", 1.8f);
            bubbleKeepUntil = Time.unscaledTime + 1.8f;
            cameraFocus = null;
            springJumping = false;
            transitioning = false;
        }

        private static Direction FacingToward(Vector2Int offset)
        {
            if (Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)) return offset.x < 0 ? Direction.Left : Direction.Right;
            return offset.y < 0 ? Direction.Down : Direction.Up;
        }

        // 안내 (#217 사용자): 다른 샘으로 갈 수 있게 된 뒤 처음 샘 옆에 서면 — 새로 하는 사람은 서고 샘, 이미 하던 사람은 어느 샘이든
        private bool SpringGuideDue()
        {
            if (session.Tutorials.Has(TutorialProgress.Spring)) return false;
            var direction = FieldInteraction.FindTarget(walker.Map, walker.Position, walker.Facing, IsClearedBossSpot);
            return direction.HasValue && walker.Map.Get(walker.Position + direction.Value.ToOffset()) == FieldTile.Fountain
                                      && SpringDestinations().Count > 0;
        }

        // 샘으로 쏙 → 어두워짐 → 그 지역 샘에서 쏙 튀어나와 샘 앞에 섬
        private IEnumerator WarpToSpring(Vector2Int fromSpring, FieldArea target)
        {
            var targetSpring = SpringWarp.SpringOf(target.Map);
            if (targetSpring == null) yield break;

            transitioning = true;
            springJumping = true;
            bubble.Hide();
            cameraFocus = CameraTarget(player.position); // 카메라는 그대로 — 뛰어오르는 모습이 보이게
            ShowToast("샘으로 뛰어들었다!", 1.2f);
            var heroSize = player.localScale;
            var friendSize = follower.Root.localScale;
            bool withFriend = follower.Visible;

            var into = CellCenter(fromSpring);
            playerRenderer.sprite = PlayerArt.GetRun(SpringWarp.DirectionBetween(walker.Position, fromSpring), 1);
            var heroDive = StartCoroutine(Dive(player, player.position, into, heroSize, 0f, true));
            var friendDive = withFriend ? StartCoroutine(Dive(follower.Root, follower.Position, into, friendSize, FollowerJumpDelay, false)) : null;
            yield return heroDive;
            if (friendDive != null) yield return friendDive;

            yield return FadeFlash(0f, 1f);

            var spring = targetSpring.Value;
            var arrival = SpringWarp.ArrivalCell(target.Map, spring);
            var arrivalWorld = CellCenter(arrival);
            cameraFocus = CameraTarget(arrivalWorld);
            EnterArea(target, arrival);
            var facing = SpringWarp.DirectionBetween(spring, arrival);
            walker.Face(facing);
            playerRenderer.sprite = PlayerArt.GetRun(facing, 1);
            var outOf = CellCenter(spring);
            player.position = outOf;
            player.localScale = Vector3.zero;
            var friendLands = CellCenter(SpringWarp.CompanionCell(target.Map, arrival, spring));
            if (withFriend)
            {
                follower.Root.position = outOf;
                follower.Root.localScale = Vector3.zero;
            }
            SaveNow();

            yield return FadeFlash(1f, 0f);
            flash.gameObject.SetActive(false);

            var heroSurface = StartCoroutine(Surface(player, outOf, arrivalWorld, heroSize, 0f, true));
            var friendSurface = withFriend ? StartCoroutine(Surface(follower.Root, outOf, friendLands, friendSize, FollowerJumpDelay, false)) : null;
            yield return heroSurface;
            if (friendSurface != null) yield return friendSurface;

            playerRenderer.sprite = PlayerArt.Get(facing, 0);
            if (withFriend) follower.StandAt(friendLands, arrivalWorld, facing);
            cameraFocus = null;
            springJumping = false;
            transitioning = false;
            ShowToast(area.DisplayName, 1.5f);

            // 처음 건너간 뒤 한 번 (#223)
            if (showTutorials && !session.Tutorials.Has(TutorialProgress.SpringArrived))
            {
                session.Tutorials.Mark(TutorialProgress.SpringArrived);
                SaveNow();
                talkingNpc = null;
                bubble.Say("정말 이어져 있었어!", 2.2f);
                bubbleKeepUntil = Time.unscaledTime + 2.2f;
            }
        }

        // 포물선으로 샘까지 → 뒤쪽에서 작아지며 쏙
        private IEnumerator Dive(Transform body, Vector3 from, Vector3 spring, Vector3 size, float delay, bool splash, float height = SpringJumpHeight,
            float shrinkFrom = DiveShrinkFrom)
        {
            if (delay > 0f) yield return WaitUnscaled(delay);
            float seconds = SpringJumpSeconds * animationScale;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float p = t / seconds;
                body.position = Arc(from, spring, p, height);
                body.localScale = size * (p < shrinkFrom ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (p - shrinkFrom) / (1f - shrinkFrom)));
                if (splash && p >= shrinkFrom)
                {
                    splash = false;
                    Splash(spring);
                }
                yield return null;
            }
            if (splash) Splash(spring);
            body.position = spring;
            body.localScale = Vector3.zero;
        }

        // 샘에서 쏙 커지며 포물선으로 도착 칸까지 (살짝 크게 튀어나왔다가 원래 크기로)
        private IEnumerator Surface(Transform body, Vector3 spring, Vector3 to, Vector3 size, float delay, bool splash)
        {
            if (delay > 0f) yield return WaitUnscaled(delay);
            if (splash) Splash(spring);
            float seconds = SpringJumpSeconds * animationScale;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float p = t / seconds;
                body.position = Arc(spring, to, p);
                body.localScale = size * (p < SurfaceGrowUntil
                    ? SurfaceOvershoot * Mathf.SmoothStep(0f, 1f, p / SurfaceGrowUntil)
                    : Mathf.Lerp(SurfaceOvershoot, 1f, (p - SurfaceGrowUntil) / (1f - SurfaceGrowUntil)));
                yield return null;
            }
            body.position = to;
            body.localScale = size;
        }

        private static Vector3 Arc(Vector3 from, Vector3 to, float p, float height = SpringJumpHeight) =>
            Vector3.Lerp(from, to, p) + Vector3.up * (Mathf.Sin(p * Mathf.PI) * height);

        private void Splash(Vector3 world)
        {
            Sound.Play(Sfx.SkillWater);
            StartCoroutine(FieldFx.Play(transform, world, Fx.Water, 1.1f, animationScale, 12, tint: new Color(0.8f, 0.9f, 1f, 0.8f))); // 은은하게
        }

        private IEnumerator FadeFlash(float from, float to)
        {
            flash.gameObject.SetActive(true);
            float seconds = 0.25f * animationScale;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            flash.color = new Color(0, 0, 0, to);
        }
    }
}
