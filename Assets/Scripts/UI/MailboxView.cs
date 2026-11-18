using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 메일함 (#275, Figma '메일함 · 메일함 — 편지 읽기 · 메일함 — 지우기 확인 (#275)', 한 줄 = 컴포넌트 'Mail Row' State=Unread/Read).
    // 목록: 봉투 · 제목 · 보낸 사람·받은 날 · (받을 선물) · '새 편지' · [지우기]. 줄을 누르면 종이 편지(LetterPaper) + (붙은 선물 [선물 받기]) + [지우기]/[닫기].
    // 읽으면 '새 편지'가 꺼지고 흐려짐. 지우기는 확인 창 — 안 받은 선물은 가방에 넣고 지움 (규칙 Mails)
    public class MailboxView
    {
        private const float ScreenW = 1080f, ScreenH = 1920f;
        private static readonly Color Red = new Color32(205, 62, 72, 255);

        private class Row
        {
            public MailData Mail;
            public GameObject Root;
            public Button Open, Delete;
        }

        private RectTransform content;
        private Text empty, status;
        private readonly List<Row> rows = new List<Row>();
        private GameObject reader, attachmentBar;
        private LetterPaper paper;
        private Text attachmentText;
        private Button claimButton;
        private readonly List<Image> attachmentIcons = new List<Image>();
        private GameSession session;
        private Func<string, MailData> find;
        private Action onChanged;
        private MailData reading;

        public GameObject Root { get; private set; }
        public ConfirmDialog Dialog { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public bool IsReading => reader != null && reader.activeSelf;
        // 테스트용
        public int RowCount => rows.Count;
        public string Status => status.text;
        public string EmptyText => empty.gameObject.activeSelf ? empty.text : "";
        public string LetterTitle => paper.TitleText;
        public string LetterText => paper.AllText;
        public bool ShowsAttachment => attachmentBar.activeSelf;
        public string RowText(int index) => UiDriverText(rows[index].Root);
        public bool RowIsUnread(int index) => rows[index].Root.transform.Find("New").gameObject.activeSelf;

        public static MailboxView Create(Transform parent)
        {
            var view = new MailboxView();
            var root = UiKit.Stretch("MailboxView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background;

            UiKit.Display(UiKit.Label("Title", root, "메일함", 56, Palette.Gold, 0, 0.92f, 1, 0.99f));
            UiKit.Label("Sub", root, "개발자가 보낸 편지와 선물이 와요", 30, Palette.TextDim, 0, 0.88f, 1, 0.915f);

            // 목록 (위아래로 밀기 + 스크롤 막대 — #122)
            var viewport = UiKit.Panel("Viewport", root, Color.clear, 0.022f, 0.16f, 0.945f, 0.875f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            view.content = (RectTransform)contentGo.transform;
            view.content.SetParent(viewport.transform, false);
            view.content.anchorMin = new Vector2(0, 1);
            view.content.anchorMax = new Vector2(1, 1);
            view.content.pivot = new Vector2(0.5f, 1);
            view.content.offsetMin = view.content.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = view.content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
            UiKit.VerticalScrollbar(scroll, 0.955f, 0.16f, 0.97f, 0.875f);

            view.empty = UiKit.Label("Empty", root, "아직 받은 메일이 없어요", 32, Palette.TextDim, 0, 0.6f, 1, 0.68f);
            view.status = UiKit.Label("Status", root, "", 30, Palette.Gold, 0.03f, 0.105f, 0.97f, 0.15f,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 20);
            var close = UiKit.MakeButton("MailboxCloseButton", root, "닫기", Palette.Neutral, 44, 0.022f, 0.015f, 0.978f, 0.068f);
            close.onClick.AddListener(view.Hide);

            view.BuildReader(root);
            view.Dialog = ConfirmDialog.Create(root);
            view.Root.SetActive(false);
            return view;
        }

        // 편지 읽기: 어두운 막 + 종이 + (붙은 선물) + [지우기]/[닫기]
        private void BuildReader(RectTransform root)
        {
            var group = UiKit.Stretch("MailReader", root);
            reader = group.gameObject;
            UiKit.Panel("Dim", group, Palette.Scrim).raycastTarget = true;
            paper = LetterPaper.Create(group, 120, 1420);

            var bar = UiKit.RoundPanel("Attachment", group, Palette.PanelLight, UiKit.RadiusMd,
                60 / ScreenW, 1f - 1718 / ScreenH, 1020 / ScreenW, 1f - 1568 / ScreenH);
            attachmentBar = bar.gameObject;
            for (int i = 0; i < 2; i++)
            {
                var icon = UiKit.IconImage($"Icon_{i}", bar.transform, null, (40 + i * 88) / 960f, 0.5f, (40 + i * 88) / 960f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(72, 72);
                icon.rectTransform.anchoredPosition = new Vector2(36, 0);
                attachmentIcons.Add(icon);
            }
            attachmentText = UiKit.Label("Text", bar.transform, "", 28, Palette.Text, 0.23f, 0.08f, 0.68f, 0.92f,
                TextAnchor.MiddleLeft, FontStyle.Bold, true, 20);
            claimButton = UiKit.MakeButton("MailClaimButton", bar.transform, "선물 받기", Palette.Gold, 38, 0.7f, 0.18f, 0.97f, 0.82f);
            UiKit.LabelOf(claimButton).color = Palette.OnAccent;
            var colors = claimButton.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 1f);
            claimButton.colors = colors;
            claimButton.onClick.AddListener(Claim);

            var delete = UiKit.MakeButton("MailDeleteButton", group, "지우기", Palette.Neutral, 44,
                60 / ScreenW, 1f - 1862 / ScreenH, 516 / ScreenW, 1f - 1760 / ScreenH);
            delete.onClick.AddListener(() => AskDelete(reading));
            var close = UiKit.MakeButton("MailReaderCloseButton", group, "닫기", Palette.Gold, 44,
                564 / ScreenW, 1f - 1862 / ScreenH, 1020 / ScreenW, 1f - 1760 / ScreenH);
            UiKit.LabelOf(close).color = Palette.OnAccent;
            close.onClick.AddListener(CloseReader);
            reader.SetActive(false);
        }

        // changed: 읽음·선물·지우기마다 (저장 + 메일함 버튼의 점)
        public void Show(GameSession gameSession, Func<string, MailData> findMail, Action changed)
        {
            session = gameSession;
            find = findMail;
            onChanged = changed;
            status.text = "";
            Dialog.Hide();
            reader.SetActive(false);
            Rebuild();
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Hide()
        {
            Dialog.Hide();
            reader.SetActive(false);
            Root.SetActive(false);
        }

        // 뒤로가기: 확인 창 → 편지 → 메일함 순서로 닫음
        public void HandleBack()
        {
            if (Dialog.IsOpen) Dialog.Hide();
            else if (IsReading) CloseReader();
            else Hide();
        }

        // 테스트용 (줄을 누른 것과 같음)
        public void OpenMail(int index) => Read(rows[index].Mail);

        private void Rebuild()
        {
            foreach (var row in rows) UnityEngine.Object.Destroy(row.Root);
            rows.Clear();
            var visible = Mails.Visible(session, find);
            foreach (var (mail, state) in visible) rows.Add(BuildRow(mail, state));
            empty.gameObject.SetActive(visible.Count == 0);
        }

        // 한 줄 (Figma 'Mail Row' 1032×150)
        private Row BuildRow(MailData mail, MailState state)
        {
            const float w = 1032f, h = 150f;
            var panel = UiKit.RoundPanel($"Mail_{mail.MailId}", content, Palette.Panel, UiKit.RadiusMd);
            panel.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            var row = new Row { Mail = mail, Root = panel.gameObject, Open = UiKit.AddButton(panel) };
            row.Open.onClick.AddListener(() => Read(mail));

            bool read = state.Read;
            var envelope = UiKit.IconImage("Envelope", panel.transform, UiKit.ArtSprite("Event/letter"), 76 / w, 0.5f, 76 / w, 0.5f);
            envelope.rectTransform.sizeDelta = new Vector2(96, 72);
            envelope.color = new Color(1f, 1f, 1f, read ? 0.45f : 1f);
            UiKit.OneLine(UiKit.Label("Subject", panel.transform, mail.Subject, 34, read ? Palette.TextDim : Palette.Text,
                148 / w, 0.5f, 700 / w, 0.86f, TextAnchor.MiddleLeft, FontStyle.Bold, true, 22));
            UiKit.OneLine(UiKit.Label("Meta", panel.transform, $"{mail.Sender} · {Mails.DateLabel(state)}", 26, Palette.TextDim,
                148 / w, 0.14f, 700 / w, 0.5f, TextAnchor.MiddleLeft));

            if (Mails.IsUnclaimed(mail, state))
            {
                var gift = UiKit.IconImage("Gift", panel.transform, UiKit.ArtSprite("Event/gift_box"), 742 / w, 0.5f, 742 / w, 0.5f);
                gift.rectTransform.sizeDelta = new Vector2(56, 56);
            }
            var pill = UiKit.Pill(UiKit.Panel("New", panel.transform, Red, 778 / w, 0.37f, 904 / w, 0.63f));
            pill.raycastTarget = false;
            UiKit.OneLine(UiKit.Display(UiKit.Label("Text", pill.transform, "새 편지", 28, Color.white, 0, 0, 1, 1)));
            pill.gameObject.SetActive(!read);

            var trash = UiKit.RoundPanel("DeleteButton", panel.transform, Palette.Neutral, UiKit.RadiusMd, 1f, 0.5f, 1f, 0.5f);
            trash.rectTransform.sizeDelta = new Vector2(88, 88);
            trash.rectTransform.anchoredPosition = new Vector2(-24 - 44, 0);
            row.Delete = UiKit.AddButton(trash);
            row.Delete.name = $"MailDelete_{mail.MailId}";
            UiKit.IconImage("Icon", trash.transform, UiKit.Icon("trash"), 0.22f, 0.22f, 0.78f, 0.78f);
            row.Delete.onClick.AddListener(() => AskDelete(mail));
            row.Open.name = $"MailRow_{mail.MailId}";
            return row;
        }

        private void Read(MailData mail)
        {
            if (mail == null) return;
            reading = mail;
            paper.Fill(mail);
            Mails.MarkRead(session, mail);
            onChanged?.Invoke();
            RefreshAttachment();
            reader.transform.SetAsLastSibling();
            Dialog.Root.transform.SetAsLastSibling();
            reader.SetActive(true);
            Rebuild(); // 목록의 '새 편지'를 끔
        }

        private void RefreshAttachment()
        {
            var state = reading != null ? session.Mailbox.Find(reading.MailId) : null;
            bool has = reading != null && reading.HasAttachments;
            attachmentBar.SetActive(has);
            if (!has) return;
            var lines = new List<ItemGift>();
            foreach (var line in reading.Attachments)
                if (line != null && line.Item != null && line.Count > 0) lines.Add(line);
            for (int i = 0; i < attachmentIcons.Count; i++)
            {
                var sprite = i < lines.Count ? UiKit.ItemIcon(lines[i].Item) : null;
                attachmentIcons[i].sprite = sprite;
                attachmentIcons[i].enabled = sprite != null;
            }
            attachmentText.text = FullVersionGift.Describe(lines);
            bool waiting = Mails.IsUnclaimed(reading, state);
            claimButton.interactable = waiting;
            UiKit.LabelOf(claimButton).text = waiting ? "선물 받기" : "받았어요";
        }

        private void Claim()
        {
            var given = Mails.Claim(session, reading);
            if (given.Count == 0) return;
            Sound.Play(Sfx.Coin);
            onChanged?.Invoke();
            status.text = $"{FullVersionGift.Describe(given)}를 가방에 넣었어요";
            RefreshAttachment();
            Rebuild();
        }

        private void CloseReader()
        {
            reading = null;
            reader.SetActive(false);
        }

        private void AskDelete(MailData mail)
        {
            if (mail == null) return;
            var state = session.Mailbox.Find(mail.MailId);
            string message = "지운 메일은 다시 볼 수 없어요."
                             + (Mails.IsUnclaimed(mail, state) ? "\n받지 않은 선물은 가방에 넣고 지워요." : "");
            Dialog.Show("이 메일을 지울까요?", message, "지우기", true, () => Delete(mail), "trash");
        }

        private void Delete(MailData mail)
        {
            var given = Mails.Delete(session, mail);
            onChanged?.Invoke();
            status.text = given.Count > 0 ? $"메일을 지웠어요 · {FullVersionGift.Describe(given)}는 가방에" : "메일을 지웠어요";
            if (reading == mail) CloseReader();
            Rebuild();
        }

        private static string UiDriverText(GameObject root)
        {
            var parts = new List<string>();
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text)) parts.Add(text.text);
            return string.Join(" ", parts);
        }
    }
}
