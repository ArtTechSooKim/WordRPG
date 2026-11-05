using System.Collections;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 — 회복의 샘: 회복 + 다른 지역의 샘으로 이동 (#216 사용자, GDD 9-11)
    public partial class FieldScreen
    {
        private ConfirmDialog springDialog;
        private SpringWarpView springWarpView;

        public ConfirmDialog SpringDialog => springDialog;       // 테스트용
        public SpringWarpView SpringWarpPicker => springWarpView; // 테스트용

        // 먼저 회복하고, 갈 수 있는 다른 샘이 있으면 묻는다 ([취소]해도 회복은 그대로). 갈 곳이 하나면 바로, 여럿이면 고르는 창
        private void UseFountain()
        {
            session.RestoreHero();
            SaveNow();
            RefreshHud();
            Sound.Play(Sfx.Fountain);

            var destinations = SpringWarp.Destinations(database != null ? database.Areas : null, area, session.World,
                entitlements == null || entitlements.HasFullVersion);
            if (destinations.Count == 0)
            {
                ShowToast("회복의 샘 — HP가 모두 회복되었다!");
                return;
            }
            string where = destinations.Count == 1 ? $"{destinations[0].DisplayName}의 샘으로" : "다른 지역으로";
            springDialog.Show("회복의 샘", $"HP가 모두 회복되었습니다.\n{where} 이동하시겠습니까?", "확인", false, () =>
            {
                if (destinations.Count == 1) StartCoroutine(WarpToSpring(destinations[0]));
                else springWarpView.Show(destinations, target => StartCoroutine(WarpToSpring(target)));
            }, "skill_heal");
        }

        // 어두워짐 → 그 지역 샘 앞에서 샘을 보고 섬 → 밝아짐 (출입구와 같은 흐름)
        private IEnumerator WarpToSpring(FieldArea target)
        {
            var spring = SpringWarp.SpringOf(target.Map);
            if (spring == null) yield break;

            transitioning = true;
            bubble.Hide();
            Sound.Play(Sfx.Fountain);
            flash.gameObject.SetActive(true);
            float half = 0.25f * animationScale;
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(0, 0, 0, t / half);
                yield return null;
            }

            var arrival = SpringWarp.ArrivalCell(target.Map, spring.Value);
            EnterArea(target, arrival);
            walker.Face(SpringWarp.FacingSpring(arrival, spring.Value));
            playerRenderer.sprite = PlayerArt.Get(walker.Facing, 0);
            SaveNow();

            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(0, 0, 0, 1f - t / half);
                yield return null;
            }
            flash.gameObject.SetActive(false);
            transitioning = false;
            ShowToast(area.DisplayName, 1.5f);
        }
    }
}
