using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 설정 (Figma '설정 (#37)'): 소리(배경 음악·효과음), 진동, [튜토리얼 다시 보기], [처음부터 다시 하기], 정보.
    // 값을 바꿀 때마다 onSettingsChanged로 저장. 처음부터는 실수로 지우지 않게 확인 창을 두 번 거쳐야 onDeleteSave
    // ('설정 — 처음부터 1차/2차 확인 (#37)')
    public class SettingsView
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public ConfirmDialog Dialog { get; private set; }

        private Slider music, sfx;
        private SwitchView vibration;
        private GameSettings settings;
        private Action onSettingsChanged;
        private bool dirty; // 슬라이더로 바꾸고 아직 저장하지 않음
        private Action onDeleteSave;
        private Action onReplayTutorial;
        private Button replayButton;

        public static SettingsView Create(Transform parent)
        {
            var view = new SettingsView();
            var root = UiKit.Stretch("SettingsView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background;

            UiKit.Display(UiKit.Label("Title", root, "설정", 56, Palette.Gold, 0, 0.93f, 1, 0.99f));

            // 소리
            var sound = Section(root, "소리", 0.745f, 0.915f);
            Row(sound, "music", "배경 음악", null, 0.47f, 0.71f);
            view.music = UiKit.MakeSlider("MusicSlider", sound, 0.44f, 0.47f, 0.965f, 0.71f);
            view.music.onValueChanged.AddListener(v => view.Change(s => s.MusicVolume = v, false));
            view.SaveOnRelease(view.music);
            Row(sound, "sound", "효과음", null, 0.15f, 0.39f);
            view.sfx = UiKit.MakeSlider("SfxSlider", sound, 0.44f, 0.15f, 0.965f, 0.39f);
            view.sfx.onValueChanged.AddListener(v => view.Change(s => s.SfxVolume = v, false));
            view.SaveOnRelease(view.sfx);

            // 게임: 진동 + 튜토리얼 다시 보기
            var game = Section(root, "게임", 0.535f, 0.73f);
            Row(game, null, "진동", "틀렸을 때 짧게 떨려요", 0.43f, 0.8f);
            view.vibration = SwitchView.Create("VibrationSwitch", game, 0.84f, 0.5f, 0.965f, 0.73f);
            view.replayButton = UiKit.MakeButton("TutorialReplayButton", game, "튜토리얼 다시 보기", Palette.Neutral, 40,
                0.035f, 0.07f, 0.965f, 0.36f);
            view.replayButton.onClick.AddListener(view.ReplayTutorial);

            // 저장: 자동 저장 안내 + 처음부터 다시 하기
            var save = Section(root, "저장", 0.32f, 0.515f);
            Row(save, null, "자동 저장", "답할 때마다, 전투가 끝날 때마다 저절로 저장돼요", 0.44f, 0.8f);
            var restart = UiKit.MakeButton("RestartButton", save, "처음부터 다시 하기", Palette.Confirm, 42, 0.035f, 0.08f, 0.965f, 0.38f);
            restart.onClick.AddListener(view.AskRestart);

            // 정보
            var info = Section(root, "정보", 0.185f, 0.305f);
            UiKit.Label("Version", info, $"버전 {Application.version} (MVP)", 28, Palette.TextDim, 0.035f, 0.4f, 0.965f, 0.66f,
                TextAnchor.MiddleLeft);
            UiKit.Label("Fonts", info, "글꼴  Jua · Noto Sans KR — SIL Open Font License 1.1", 28, Palette.TextDim,
                0.035f, 0.1f, 0.965f, 0.38f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);

            var close = UiKit.MakeButton("SettingsCloseButton", root, "닫기", Palette.Neutral, 44, 0.05f, 0.015f, 0.95f, 0.095f);
            close.onClick.AddListener(view.Hide);

            view.Dialog = ConfirmDialog.Create(root);
            view.Root.SetActive(false);
            return view;
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
        private static void Row(RectTransform section, string icon, string label, string sub, float minY, float maxY)
        {
            float x = 0.035f;
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
            UiKit.Label($"Label_{label}", section, label, 34, Palette.Text, x, mid, 0.8f, maxY, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.Label($"Sub_{label}", section, sub, 24, Palette.TextDim, x, minY, 0.8f, mid, TextAnchor.UpperLeft,
                FontStyle.Normal, true, 18);
        }

        // replayTutorial: [튜토리얼 다시 보기]를 누르면 (없으면 버튼을 숨김)
        public void Show(GameSettings gameSettings, Action settingsChanged, Action deleteSave, Action replayTutorial = null)
        {
            settings = gameSettings ?? new GameSettings();
            onSettingsChanged = settingsChanged;
            onDeleteSave = deleteSave;
            onReplayTutorial = replayTutorial;
            replayButton.gameObject.SetActive(replayTutorial != null);
            music.SetValueWithoutNotify(settings.MusicVolume);
            sfx.SetValueWithoutNotify(settings.SfxVolume);
            vibration.Bind(settings.Vibration, on => Change(s => s.Vibration = on));
            Dialog.Hide();
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide()
        {
            if (dirty) Flush();
            Dialog.Hide();
            Root.SetActive(false);
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
    }
}
