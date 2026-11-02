using System.Collections;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;

namespace WordRPG.UI
{
    // 성유물 안내 (#142 사용자):
    // ① 가방: 성유물을 처음 더 얻으면 가방에 빨간 점 → 가방을 누르면 성유물 탭 · 새 성유물 · 바꾸는 곳을 한 줄씩.
    //    칸이 가득 차서 못 끼운 성유물이 처음 생기면 다시 빨간 점 → 이번엔 직접 바꿔 끼워 보게 (그때가 진짜 바꾸는 순간)
    // ② 제단: 강화할 재료가 처음 모이면 제단 앞으로 옮겨서 설명(PauseTutorial — 사용자가 원함)하고 강화까지 해 보게
    public partial class FieldScreen
    {
        private const float AltarGuideIdleSeconds = 1.5f; // 얻었다는 알림을 읽을 틈

        private float altarGuideIdle;
        private bool afterBossRunning; // 보스를 이긴 뒤 알림·길 열림·돌아가기 묻기가 끝날 때까지 제단 안내를 미룸

        public int AltarGuidesPlayed { get; private set; } // 테스트용

        // 가방을 연 뒤: 빨간 점이 켜져 있었으면 안내. swap = 직접 바꿔 끼워 보기
        private IEnumerator RelicBagGuide(bool swap)
        {
            var hero = session.Hero;
            var relic = swap ? RelicTutorials.NewestUnequipped(hero) : RelicTutorials.Newest(hero);
            var page = inventoryView.Relics;
            bool OnRelics() => inventoryView.IsOpen && inventoryView.CurrentTab == BagTab.Relics;

            if (!OnRelics())
            {
                guide.Show(new GuideHints.Hint { Text = "[성유물] 탭을 눌러 보세요", Target = () => inventoryView.RelicsTabRect, Gesture = GuideHints.Gesture.Tap });
                while (inventoryView.IsOpen && !OnRelics()) yield return null;
            }
            if (OnRelics() && page.Selected != relic)
            {
                string named = UiKit.WithJosa(relic.Data.DisplayName, "을", "를");
                guide.Show(new GuideHints.Hint { Text = $"새로 얻은 {named} 눌러 보세요", Target = () => page.CellRect(relic), Gesture = GuideHints.Gesture.Tap });
                while (OnRelics() && page.Selected != relic) yield return null;
            }

            if (!swap)
            {
                if (OnRelics())
                {
                    guide.Show(new GuideHints.Hint { Text = "칸이 가득 차면 여기서 바꿔 끼워요", Target = () => page.SlotsRect });
                    float end = Time.unscaledTime + 4f;
                    while (OnRelics() && Time.unscaledTime < end) yield return null;
                }
                guide.Hide();
                yield break;
            }

            // 직접 바꿔 끼우기: [바꿔 끼우기] → 칸 고르기 (취소하면 다시 버튼부터)
            bool? shown = null;
            while (OnRelics() && !hero.IsEquipped(relic))
            {
                if (page.Selected != relic) break; // 다른 성유물을 봄 → 안내는 여기까지
                if (shown != page.ChoosingSlot)
                {
                    shown = page.ChoosingSlot;
                    guide.Show(page.ChoosingSlot
                        ? new GuideHints.Hint { Text = "바꿀 칸을 눌러 보세요", Target = () => page.SlotsRect }
                        : new GuideHints.Hint { Text = "[바꿔 끼우기]를 눌러 보세요", Target = () => page.ActionRect, Gesture = GuideHints.Gesture.Tap });
                }
                yield return null;
            }
            guide.Hide();
        }

        // UpdateGuide에서 (서 있을 때만 불림): 강화할 재료가 처음 모였고 잠깐 한가하면 제단 안내
        private bool MaybeStartAltarGuide()
        {
            if (afterBossRunning || walker.Rafting || !RelicTutorials.AltarDue(session)) return false;
            altarGuideIdle += Time.unscaledDeltaTime;
            if (altarGuideIdle < AltarGuideIdleSeconds * animationScale) return false;
            var target = RelicTutorials.NearestAltarArea(area, session,
                candidate => entitlements == null || entitlements.HasFullVersion || !candidate.RequiresFullVersion);
            if (target == null) return false;
            var cell = RelicTutorials.StandBefore(target.Map, target.Map.Find(FieldTile.Altar).Value, out var facing);
            if (!cell.HasValue) return false;
            StartCoroutine(AltarGuide(target, cell.Value, facing));
            return true;
        }

        private IEnumerator AltarGuide(FieldArea target, Vector2Int cell, Direction facing)
        {
            fieldTutorialRunning = true; // 그동안 몬스터가 나오지 않음
            AltarGuidesPlayed++;
            bubble.Hide();
            yield return pauseTutorial.Tell(new[] { "강화 재료가 모였어요!\n성유물 제단으로 모셔다 드릴게요" });

            // 시작 지점으로 돌아갈 때와 같은 어두워짐 + 문 소리
            transitioning = true;
            Sound.Play(Sfx.Door);
            var cutscene = GateCutscene.Create(transform);
            yield return cutscene.Fade(0f, 1f, 0.35f * animationScale);
            if (target == area)
            {
                walker.WarpTo(cell);
                if (session.World.Reveal(area.AreaId, walker.Map.Width, walker.Map.Height, cell) > 0)
                {
                    minimap.Redraw();
                    RefreshDarkness();
                }
                session.World.SetPosition(area.AreaId, cell);
                RefreshNameTags();
            }
            else EnterArea(target, cell);
            walker.Face(facing);
            SnapPlayer();
            UpdateCamera();
            yield return cutscene.Fade(1f, 0f, 0.35f * animationScale);
            Destroy(cutscene.gameObject);
            transitioning = false;

            yield return pauseTutorial.Tell(new[]
            {
                "성유물 제단에서는 모은 재료와 골드로\n성유물을 강화할 수 있어요",
                "강화할수록 능력치가 오르고,\n+3이 되면 각성해서 기술이 더 강해져요"
            });
            session.Tutorials.Mark(TutorialProgress.Altar); // 여기서 꺼도 다시 옮기지 않음
            SaveNow();

            // 직접 해 보기: [확인] → 강화. 걸어서 떠나면 안내는 여기까지
            guide.Show(new GuideHints.Hint { Text = "[확인]을 눌러 제단을 열어 보세요", Target = () => confirmImage.rectTransform, Gesture = GuideHints.Gesture.Tap });
            while (!altarView.IsOpen && !inBattle && walker.Position == cell) yield return null;
            if (altarView.IsOpen)
            {
                guide.Show(new GuideHints.Hint { Text = "[강화!]를 눌러 보세요", Target = () => altarView.FirstReadyButton, Gesture = GuideHints.Gesture.Tap });
                while (altarView.IsOpen && !RelicTutorials.AnyUpgraded(session.Hero)) yield return null;
            }
            guide.Hide();
            fieldTutorialRunning = false;
        }

        // 보스를 이긴 뒤의 흐름이 끝나기 전에는 제단으로 옮기지 않음
        private IEnumerator WhileAfterBoss(IEnumerator routine)
        {
            afterBossRunning = true;
            yield return routine;
            afterBossRunning = false;
        }
    }
}
