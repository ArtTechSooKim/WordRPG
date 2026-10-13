using System;
using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 확인 창 (Figma 'Dialog'): 뒤를 어둡게 덮고 아이콘·제목·설명 + [취소] [확인].
    // 되돌릴 수 없는 일(저장 데이터 지우기, 처음부터)이나 물어볼 일(보스를 이긴 뒤 시작 지점으로 돌아갈지)에 쓴다
    public class ConfirmDialog
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        private Image icon;
        private Text title, message, confirmLabel, cancelLabel;
        private Button confirm;
        private Action onConfirm;

        public static ConfirmDialog Create(Transform parent)
        {
            var dialog = new ConfirmDialog();
            var root = UiKit.Panel("ConfirmDialog", parent, Palette.Scrim); // 뒤쪽 버튼을 막는다
            dialog.Root = root.gameObject;

            var panel = UiKit.RoundPanel("Panel", root.transform, Palette.PanelLight, UiKit.RadiusLg, 0.067f, 0.355f, 0.933f, 0.645f);
            dialog.icon = UiKit.IconImage("Icon", panel.transform, null, 0.42f, 0.76f, 0.58f, 0.94f);
            dialog.title = UiKit.Display(UiKit.Label("Title", panel.transform, "", 52, Palette.Bad, 0.04f, 0.58f, 0.96f, 0.75f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 28));
            dialog.message = UiKit.Label("Message", panel.transform, "", 34, Palette.Text, 0.06f, 0.29f, 0.94f, 0.58f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 22);

            var cancel = UiKit.MakeButton("DialogCancelButton", panel.transform, "취소", Palette.Neutral, 42, 0.05f, 0.07f, 0.485f, 0.25f);
            cancel.onClick.AddListener(dialog.Hide);
            dialog.cancelLabel = UiKit.LabelOf(cancel);
            dialog.confirm = UiKit.MakeButton("DialogConfirmButton", panel.transform, "", Palette.Confirm, 42, 0.515f, 0.07f, 0.95f, 0.25f);
            dialog.confirmLabel = UiKit.LabelOf(dialog.confirm);
            dialog.confirm.onClick.AddListener(dialog.OnConfirm);

            dialog.Root.SetActive(false);
            return dialog;
        }

        // danger = 빨간 제목·버튼 (되돌릴 수 없음)
        public void Show(string titleText, string messageText, string confirmText, bool danger, Action confirmed,
            string iconName = "trash", string cancelText = "취소")
        {
            title.text = titleText;
            cancelLabel.text = cancelText;
            title.color = danger ? Palette.Bad : Palette.Gold;
            message.text = messageText;
            confirmLabel.text = confirmText;
            UiKit.SetColor(confirm, danger ? Palette.Confirm : Palette.Button);
            var sprite = UiKit.Icon(iconName);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            onConfirm = confirmed;
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide()
        {
            onConfirm = null;
            Root.SetActive(false);
        }

        private void OnConfirm()
        {
            var action = onConfirm;
            Hide();
            action?.Invoke();
        }
    }
}
