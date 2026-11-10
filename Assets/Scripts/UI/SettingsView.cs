using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 설정 (Figma '설정 — 후기 남기기 (#254)'): 소리(배경 음악·효과음), 전투 강도, 진동, 왼손 모드, [튜토리얼 다시 보기] [입장 영상 다시 보기](#138), [처음부터 다시 하기], 정식판, 정보.
    // 정식판 칸 (#63, 앱스토어 심사 대비): 언제든 [정식판 보기](정식판 안내 창)와 [구매 복원] — 숲 입구까지 가지 않아도 살 수 있고 되찾을 수 있다.
    // 값을 바꿀 때마다 onSettingsChanged로 저장. 처음부터는 실수로 지우지 않게 확인 창을 두 번 거쳐야 onDeleteSave
    // ('설정 — 처음부터 1차/2차 확인 (#37)')
    public class SettingsView
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public ConfirmDialog Dialog { get; private set; }

        private Slider music, sfx;
        private readonly System.Collections.Generic.List<Button> intensityOptions = new System.Collections.Generic.List<Button>();
        private Text intensitySub;
        private SwitchView vibration, leftHanded;
        private GameSettings settings;
        private Action onSettingsChanged;
        private bool dirty; // 슬라이더로 바꾸고 아직 저장하지 않음
        private Action onDeleteSave;
        private Action onReplayTutorial;
        private Action onReplayIntro;
        private Button replayButton, introReplayButton;
        private Action onOpenTester;
        private Button testerButton; // 테스트 빌드에만 (#229)

        // 정식판 칸 (#63)
        public class PurchaseHooks
        {
            public IStore Store;
            public Entitlements Owned;
            public Action OpenOffer; // [정식판 보기] — 없으면 버튼을 숨김 (타이틀 화면)
        }

        private PurchaseHooks purchase;
        private GameObject purchaseSection;
        private Text purchaseTitle, purchaseSub;
        private Button offerButton, restoreButton;
        public bool IsRestoring { get; private set; }
        public string PurchaseStatus => purchaseSub != null ? purchaseSub.text : "";
        public string PurchaseTitle => purchaseTitle != null ? purchaseTitle.text : "";

        public static SettingsView Create(Transform parent)
        {
            var view = new SettingsView();
            var root = UiKit.Stretch("SettingsView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background;

            UiKit.Display(UiKit.Label("Title", root, "설정", 56, Palette.Gold, 0, 0.93f, 1, 0.99f));

            // 소리
            var sound = Section(root, "소리", 0.79f, 0.915f);
            Row(sound, "music", "배경 음악", null, 0.47f, 0.71f);
            view.music = UiKit.MakeSlider("MusicSlider", sound, 0.44f, 0.47f, 0.965f, 0.71f);
            view.music.onValueChanged.AddListener(v => view.Change(s => s.MusicVolume = v, false));
            view.SaveOnRelease(view.music);
            Row(sound, "sound", "효과음", null, 0.15f, 0.39f);
            view.sfx = UiKit.MakeSlider("SfxSlider", sound, 0.44f, 0.15f, 0.965f, 0.39f);
            view.sfx.onValueChanged.AddListener(v => view.Change(s => s.SfxVolume = v, false));
            view.SaveOnRelease(view.sfx);

            view.BuildIntensity(Section(root, "전투 강도", 0.695f, 0.78f));

            // 게임: 진동 · 왼손 모드 + 튜토리얼 다시 보기 · 입장 영상 다시 보기 (두 칸·두 버튼 배치는 아래 정식판 칸과 같음)
            var game = Section(root, "게임", 0.545f, 0.685f);
            Row(game, null, "진동", "틀렸을 때 짧게 떨려요", 0.43f, 0.8f, 0.035f, 0.36f);
            view.vibration = SwitchView.Create("VibrationSwitch", game, 0.365f, 0.5f, 0.49f, 0.73f);
            Row(game, null, "왼손 모드", "스틱과 [확인]을 바꿔요", 0.43f, 0.8f, 0.51f, 0.835f);
            view.leftHanded = SwitchView.Create("LeftHandedSwitch", game, 0.84f, 0.5f, 0.965f, 0.73f);
            view.replayButton = UiKit.MakeButton("TutorialReplayButton", game, "튜토리얼 다시 보기", Palette.Neutral, 38,
                0.035f, 0.07f, 0.49f, 0.36f);
            view.replayButton.onClick.AddListener(view.ReplayTutorial);
            view.introReplayButton = UiKit.MakeButton("IntroReplayButton", game, "입장 영상 다시 보기", Palette.Neutral, 38,
                0.51f, 0.07f, 0.965f, 0.36f);
            view.introReplayButton.onClick.AddListener(view.ReplayIntro);

            // 저장: 자동 저장 안내 + 처음부터 다시 하기
            var save = Section(root, "저장", 0.4f, 0.535f);
            Row(save, null, "자동 저장", "답할 때마다, 전투가 끝날 때마다 저절로 저장돼요", 0.44f, 0.8f);
            var restart = UiKit.MakeButton("RestartButton", save, "처음부터 다시 하기", Palette.Confirm, 42, 0.035f, 0.08f, 0.965f, 0.38f);
            restart.onClick.AddListener(view.AskRestart);

            // 정식판: 상태 한 줄 + [정식판 보기] [구매 복원]
            var full = Section(root, "정식판", 0.255f, 0.39f);
            view.purchaseSection = full.gameObject;
            view.purchaseTitle = UiKit.Label("Label_정식판", full, "", 34, Palette.Text, 0.035f, 0.58f, 0.965f, 0.8f,
                TextAnchor.LowerLeft, FontStyle.Bold);
            view.purchaseSub = UiKit.Label("Sub_정식판", full, "", 24, Palette.TextDim, 0.035f, 0.43f, 0.965f, 0.58f,
                TextAnchor.UpperLeft, FontStyle.Normal, true, 16);
            view.offerButton = UiKit.MakeButton("FullVersionOfferButton", full, "정식판 보기", Palette.Gold, 38, 0.035f, 0.07f, 0.49f, 0.4f);
            UiKit.LabelOf(view.offerButton).color = Palette.OnAccent;
            view.offerButton.onClick.AddListener(view.OpenOffer);
            view.restoreButton = UiKit.MakeButton("SettingsRestoreButton", full, "구매 복원", Palette.Neutral, 38, 0.51f, 0.07f, 0.965f, 0.4f);
            view.restoreButton.onClick.AddListener(view.Restore);

            // 정보
            var info = Section(root, "정보", 0.11f, 0.245f);
            UiKit.Label("Version", info, $"버전 {Application.version}", 28, Palette.TextDim, 0.035f, 0.4f, 0.965f, 0.66f,
                TextAnchor.MiddleLeft);
            UiKit.Label("Fonts", info, "글꼴  Jua · Noto Sans KR — SIL Open Font License 1.1", 28, Palette.TextDim,
                0.035f, 0.1f, 0.965f, 0.38f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
#if WORDRPG_TESTER || UNITY_EDITOR
            // 테스터 메뉴 (#229): 테스트 빌드(TestFlight)에만 — 앱스토어 빌드에는 이 버튼이 없음
            view.testerButton = UiKit.MakeButton("TesterMenuButton", info, "테스터 메뉴", Palette.Neutral, 32, 0.23f, 0.38f, 0.585f, 0.72f);
            view.testerButton.onClick.AddListener(view.OpenTester);
#endif

            // 후기 (#254): 눌렀을 때만 스토어의 후기 쓰는 화면으로 (앱이 먼저 묻는 건 애플 기본 창만 — ReviewPrompt)
            var review = UiKit.MakeButton("ReviewButton", info, "후기 남기기", Palette.Neutral, 32, 0.6f, 0.38f, 0.965f, 0.72f);
            review.onClick.AddListener(ReviewPrompt.OpenStorePage);

            var close = UiKit.MakeButton("SettingsCloseButton", root, "닫기", Palette.Neutral, 44, 0.05f, 0.015f, 0.95f, 0.095f);
            close.onClick.AddListener(view.Hide);

            view.Dialog = ConfirmDialog.Create(root);
            view.Root.SetActive(false);
            return view;
        }

        // 전투 강도 (#147 사용자): [매번 고르기] 또는 ×1 ~ ×2 중 하나. 고르면 전투에서 강도를 묻지 않고 그 강도로 공격
        private void BuildIntensity(RectTransform section)
        {
            intensitySub = UiKit.OneLine(UiKit.Label("IntensitySub", section, "", 24, Palette.TextDim, 0.3f, 0.66f, 0.965f, 0.96f, TextAnchor.MiddleRight));
            var config = new BattleConfig();
            const float first = 0.285f, gap = 0.012f;
            float w = (0.93f - first - config.MaxIntensity * gap) / config.MaxIntensity;
            for (int i = 0; i <= config.MaxIntensity; i++)
            {
                float minX = i == 0 ? 0.035f : 0.035f + first + gap + (i - 1) * (w + gap);
                float maxX = i == 0 ? 0.035f + first : minX + w;
                string label = i == 0 ? "매번 고르기" : "×" + config.IntensityMultiplier(i).ToString("0.##");
                var button = UiKit.MakeButton($"IntensityOption_{i}", section, label, Palette.Neutral, 34, minX, 0.09f, maxX, 0.62f);
                int value = i;
                button.onClick.AddListener(() =>
                {
                    Change(s => s.BattleIntensity = value);
                    RefreshIntensity();
                });
                intensityOptions.Add(button);
            }
        }

        private void RefreshIntensity()
        {
            int chosen = Mathf.Clamp(settings.BattleIntensity, 0, intensityOptions.Count - 1);
            for (int i = 0; i < intensityOptions.Count; i++)
            {
                UiKit.SetColor(intensityOptions[i], i == chosen ? Palette.Gold : Palette.Neutral);
                UiKit.LabelOf(intensityOptions[i]).color = i == chosen ? Palette.OnAccent : Palette.Text;
            }
            intensitySub.text = chosen == 0 ? "공격할 때마다 골라요"
                : chosen == 1 ? "늘 이 강도로 — 단어 1개를 맞히면 발동"
                : $"늘 이 강도로 — 단어 {chosen}개를 모두 맞혀야 발동";
        }

        // 둥근 판 + 금색 소제목
        private static RectTransform Section(RectTransform root, string title, float minY, float maxY)
        {
            var panel = UiKit.RoundPanel($"Section_{title}", root, Palette.Panel, UiKit.RadiusLg, 0.022f, minY, 0.978f, maxY);
            float header = Mathf.Min(0.3f, 60f / ((maxY - minY) * 1920f));
            UiKit.Label("Header", panel.transform, title, 30, Palette.Gold, 0.035f, 1f - header - 0.04f, 0.6f, 0.98f,
                TextAnchor.MiddleLeft);
            return panel.rectTransform;
        }

        // [아이콘] 이름 (+ 작은 설명)
        private static void Row(RectTransform section, string icon, string label, string sub, float minY, float maxY,
            float minX = 0.035f, float maxX = 0.8f)
        {
            float x = minX;
            if (icon != null)
            {
                UiKit.IconImage($"Icon_{label}", section, UiKit.Icon(icon), x, minY + 0.02f, x + 0.05f, maxY - 0.02f);
                x += 0.065f;
            }
            if (sub == null)
            {
                UiKit.Label($"Label_{label}", section, label, 34, Palette.Text, x, minY, 0.42f, maxY, TextAnchor.MiddleLeft, FontStyle.Bold);
                return;
            }
            float mid = minY + (maxY - minY) * 0.45f;
            UiKit.Label($"Label_{label}", section, label, 34, Palette.Text, x, mid, maxX, maxY, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Label($"Sub_{label}", section, sub, 24, Palette.TextDim, x, minY, maxX, mid, TextAnchor.UpperLeft,
                FontStyle.Normal, true, 18);
        }

        // replayTutorial: [튜토리얼 다시 보기]를 누르면 (없으면 버튼을 숨김). purchaseHooks: 정식판 칸 (없으면 칸을 숨김).
        // replayIntro: [입장 영상 다시 보기]를 누르면 (없으면 버튼을 숨김). openTester: [테스터 메뉴] (테스트 빌드에만, 없으면 숨김 — #229)
        public void Show(GameSettings gameSettings, Action settingsChanged, Action deleteSave, Action replayTutorial = null,
            PurchaseHooks purchaseHooks = null, Action replayIntro = null, Action openTester = null)
        {
            if (purchase?.Owned != null) purchase.Owned.Changed -= RefreshPurchase;
            purchase = purchaseHooks;
            purchaseSection.SetActive(purchase != null);
            if (purchase?.Owned != null) purchase.Owned.Changed += RefreshPurchase;
            IsRestoring = false;
            RefreshPurchase();
            settings = gameSettings ?? new GameSettings();
            onSettingsChanged = settingsChanged;
            onDeleteSave = deleteSave;
            onReplayTutorial = replayTutorial;
            replayButton.gameObject.SetActive(replayTutorial != null);
            onReplayIntro = replayIntro;
            introReplayButton.gameObject.SetActive(replayIntro != null);
            onOpenTester = openTester;
            if (testerButton != null) testerButton.gameObject.SetActive(openTester != null);
            music.SetValueWithoutNotify(settings.MusicVolume);
            sfx.SetValueWithoutNotify(settings.SfxVolume);
            vibration.Bind(settings.Vibration, on => Change(s => s.Vibration = on));
            leftHanded.Bind(settings.LeftHanded, on => Change(s => s.LeftHanded = on));
            RefreshIntensity();
            Dialog.Hide();
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide()
        {
            if (dirty) Flush();
            if (purchase?.Owned != null) purchase.Owned.Changed -= RefreshPurchase;
            Dialog.Hide();
            Root.SetActive(false);
        }

        // 정식판 상태: 이미 샀으면 '정식판 이용 중'과 [구매 복원]만
        private void RefreshPurchase()
        {
            if (purchase == null) return;
            bool owned = purchase.Owned != null && purchase.Owned.HasFullVersion;
            purchaseTitle.text = owned ? "정식판 이용 중" : "무료판 이용 중";
            if (!IsRestoring)
                purchaseSub.text = owned ? "모든 지역이 열려 있어요. 고마워요!" : "숲과 앞으로 추가될 지역 · 한 번만 결제";
            offerButton.gameObject.SetActive(!owned && purchase.OpenOffer != null);
            restoreButton.interactable = !IsRestoring;
        }

        private void OpenOffer() => purchase?.OpenOffer?.Invoke();

        // [구매 복원]: 그 자리에서 애플 구매 기록을 확인한다 (기기를 바꿨거나 앱을 다시 깔았을 때)
        private void Restore()
        {
            if (purchase == null || IsRestoring) return;
            if (purchase.Store == null)
            {
                purchaseSub.text = PaywallView.MessageFor(StoreResult.Unavailable);
                return;
            }
            IsRestoring = true;
            restoreButton.interactable = false;
            purchaseSub.text = "구매 기록을 확인하는 중…";
            purchase.Store.Restore(result =>
            {
                IsRestoring = false;
                if (!IsOpen) return;
                RefreshPurchase();
                purchaseSub.text = PaywallView.MessageFor(result);
            });
        }

        // 소리는 바로 바꾸고, 저장은 saveNow일 때만. 슬라이더는 끄는 동안 값이 계속 바뀌므로 손을 뗄 때·창을 닫을 때 한 번 저장
        private void Change(Action<GameSettings> apply, bool saveNow = true)
        {
            if (settings == null) return;
            apply(settings);
            Sound.ApplyVolumes(settings);
            if (saveNow) Flush();
            else dirty = true;
        }

        private void Flush()
        {
            dirty = false;
            onSettingsChanged?.Invoke();
        }

        private void SaveOnRelease(Slider slider)
        {
            var trigger = slider.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ =>
            {
                if (dirty) Flush();
            });
            trigger.triggers.Add(entry);
        }

        // 처음부터: 두 번 묻는다 (실수로 누르지 않게 — 사용자 요청 2026-10-06)
        private void AskRestart()
        {
            Dialog.Show("처음부터 다시 할까요?",
                "발견한 단어, 성유물, 아이템이 모두 사라지고\n처음부터 다시 시작해요.",
                "처음부터", true, () => Dialog.Show("정말 지울까요?",
                    "실수로 누르지 않았는지 한 번 더 확인할게요.\n지운 기록은 되돌릴 수 없어요.",
                    "지우기", true, () =>
                    {
                        var delete = onDeleteSave;
                        Hide();
                        delete?.Invoke();
                    }));
        }

        private void ReplayTutorial()
        {
            var replay = onReplayTutorial;
            Hide();
            replay?.Invoke();
        }

        // 설정은 열어 둔 채 위에 테스터 메뉴를 띄움 (닫으면 설정으로 돌아옴)
        private void OpenTester()
        {
            if (dirty) Flush();
            onOpenTester?.Invoke();
        }

        private void ReplayIntro()
        {
            var replay = onReplayIntro;
            Hide();
            replay?.Invoke();
        }
    }
}
