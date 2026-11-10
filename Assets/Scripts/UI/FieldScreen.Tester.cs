using System;
using System.Collections;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 필드 — 테스터 메뉴 (#229 사용자): 설정 > 정보 칸의 [테스터 메뉴]. 테스트 빌드(WORDRPG_TESTER, TestFlight용)와
    // 에디터(테스트에서 TesterBuild.Enabled를 켤 때)에만. 앱스토어 빌드에는 아래 #else의 빈 자리만 남는다
    public partial class FieldScreen
    {
#if WORDRPG_TESTER || UNITY_EDITOR
        private TesterMenuView testerView;
        public TesterMenuView TesterMenu => testerView; // 테스트용

        private bool TesterMenuOpen => testerView != null && testerView.IsOpen;
        private Action TesterMenuOpener() => TesterBuild.Enabled ? OpenTesterMenu : (Action)null;
        private void CloseTesterMenu() => testerView.Hide();

        private void OpenTesterMenu()
        {
            if (testerView == null) testerView = TesterMenuView.Create(hudRoot);
            testerView.Show(session, database, DateTime.UtcNow, () =>
            {
                SaveNow();
                RefreshHud();
                npcs.Refresh(area, session); // 단어 수·보스로 바뀌는 인물 말 (#209)
            }, target =>
            {
                settingsView.Hide();
                StartCoroutine(TesterTravel(target));
            }, entitlements);
        }

        // 건너뛴 지역의 시작 지점으로: 어두워짐 → 그 지역을 새로 그림(이긴 보스·열린 길) → 밝아짐
        private IEnumerator TesterTravel(FieldArea target)
        {
            transitioning = true;
            Sound.Play(Sfx.Door);
            flash.gameObject.SetActive(true);
            float half = 0.25f * animationScale;
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(0, 0, 0, t / half);
                yield return null;
            }
            EnterArea(target, target.Map.Start);
            SaveNow();
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(0, 0, 0, 1f - t / half);
                yield return null;
            }
            flash.gameObject.SetActive(false);
            transitioning = false;
            ShowToast($"[테스터] {target.DisplayName}까지 건너뜀", 2f);
        }
#else
        private bool TesterMenuOpen => false;
        private Action TesterMenuOpener() => null;
        private void CloseTesterMenu() { }
#endif
    }
}
