using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;

namespace WordRPG.UI
{
    // 편지 종이 (#274 개발자의 편지 → #275 메일함과 같이 씀, Figma '크리스마스 선물 — 개발자의 편지' · '메일함 — 편지 읽기'):
    // 종이 판 + 빨간 테두리 · 꼬리표 · 머리 그림(Art/Event) · 제목 · 밀어 읽는 본문(문단마다) · 맺음말 · 보낸 사람
    public class LetterPaper
    {
        private const float ScreenW = 1080f, ScreenH = 1920f, PaperW = 960f;
        private static readonly Color Red = new Color32(205, 62, 72, 255);

        private readonly float height;
        private RectTransform viewport;
        private Text tag, title, closing, sign;
        private Image tagPill, art;
        private RectTransform content;
        private ScrollRect scroll;
        private RectTransform bar;
        private readonly List<Text> paragraphs = new List<Text>();

        public RectTransform Root { get; private set; }
        public string TitleText => title.text;
        public string AllText
        {
            get
            {
                var lines = new List<string>();
                foreach (var p in paragraphs) if (p.gameObject.activeSelf) lines.Add(p.text);
                if (closing.gameObject.activeSelf) lines.Add(closing.text);
                if (sign.gameObject.activeSelf) lines.Add(sign.text);
                return string.Join("\n", lines);
            }
        }

        private LetterPaper(float paperHeight) => height = paperHeight;

        // y·paperHeight = Figma 화면 기준 (종이는 가로 60~1020)
        public static LetterPaper Create(RectTransform parent, float y, float paperHeight)
        {
            var paper = new LetterPaper(paperHeight);
            var panel = UiKit.RoundPanel("Letter", parent, Palette.Paper, UiKit.RadiusLg,
                60 / ScreenW, 1f - (y + paperHeight) / ScreenH, (60 + PaperW) / ScreenW, 1f - y / ScreenH);
            panel.raycastTarget = true;
            paper.Root = panel.rectTransform;
            var edge = UiKit.Outline(UiKit.Panel("Edge", panel.transform, Red), UiKit.RadiusLg, 4);
            edge.raycastTarget = false;

            paper.tagPill = UiKit.Pill(UiKit.Panel("Tag", panel.transform, Red, 340 / PaperW, 1f - 112 / paperHeight, 620 / PaperW, 1f - 56 / paperHeight));
            paper.tagPill.raycastTarget = false;
            paper.tag = UiKit.OneLine(UiKit.Display(UiKit.Label("Text", paper.tagPill.transform, "", 32, Color.white, 0, 0, 1, 1)));
            paper.art = UiKit.IconImage("Art", panel.transform, null, 0.5f, 1f - 188 / paperHeight, 0.5f, 1f - 188 / paperHeight);
            paper.art.rectTransform.sizeDelta = new Vector2(112, 112);
            paper.title = UiKit.Display(UiKit.Label("Title", panel.transform, "", 44, Palette.PaperHeading, 0, 0, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 28));

            paper.viewport = UiKit.Panel("Viewport", panel.transform, Color.clear).rectTransform;
            paper.viewport.gameObject.AddComponent<RectMask2D>();
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            paper.content = (RectTransform)contentGo.transform;
            paper.content.SetParent(paper.viewport, false);
            paper.content.anchorMin = new Vector2(0, 1);
            paper.content.anchorMax = new Vector2(1, 1);
            paper.content.pivot = new Vector2(0.5f, 1);
            paper.content.offsetMin = paper.content.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 8, 0, 16);
            layout.spacing = 18;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            paper.scroll = paper.viewport.gameObject.AddComponent<ScrollRect>();
            paper.scroll.viewport = paper.viewport;
            paper.scroll.content = paper.content;
            paper.scroll.horizontal = false;
            paper.scroll.vertical = true;
            paper.scroll.movementType = ScrollRect.MovementType.Elastic;
            paper.scroll.scrollSensitivity = 60f;
            paper.bar = (RectTransform)UiKit.VerticalScrollbar(paper.scroll, 896 / PaperW, 56 / paperHeight, 910 / PaperW, 1f - 344 / paperHeight).transform;
            paper.closing = paper.Line("Closing", 40, Palette.PaperWord, TextAnchor.MiddleCenter, true);
            paper.sign = paper.Line("Sign", 30, Palette.PaperDim, TextAnchor.MiddleRight, false);
            return paper;
        }

        public void Fill(MailData mail)
        {
            tag.text = mail != null ? mail.Tag : "";
            tagPill.gameObject.SetActive(!string.IsNullOrEmpty(tag.text));
            var picture = mail != null && !string.IsNullOrEmpty(mail.Art) ? UiKit.ArtSprite("Event/" + mail.Art) : null;
            art.sprite = picture;
            art.enabled = picture != null;
            title.text = mail != null ? mail.Title : "";

            // 그림이 없으면 제목·본문을 위로
            float titleY = picture != null ? 260f : 140f;
            Place(title.rectTransform, 56, titleY, 848, 64);
            Place(viewport, 64, titleY + 84, 820, height - 56 - (titleY + 84));
            Place(bar, 896, titleY + 84, 14, height - 56 - (titleY + 84));

            var lines = (mail != null ? mail.Body ?? "" : "").Replace("\r", "").Split('\n');
            int used = 0;
            foreach (var paragraph in lines)
            {
                if (string.IsNullOrWhiteSpace(paragraph)) continue;
                if (used >= paragraphs.Count)
                {
                    var line = Line($"Paragraph_{used}", 30, Palette.PaperText, TextAnchor.UpperLeft, false);
                    line.lineSpacing = 1.25f;
                    paragraphs.Add(line);
                }
                var text = paragraphs[used];
                text.gameObject.SetActive(true);
                text.text = paragraph.Trim();
                text.transform.SetSiblingIndex(used);
                used++;
            }
            for (int i = used; i < paragraphs.Count; i++) paragraphs[i].gameObject.SetActive(false);
            closing.text = mail != null ? mail.Closing ?? "" : "";
            closing.gameObject.SetActive(!string.IsNullOrEmpty(closing.text));
            sign.text = mail != null ? mail.Sign ?? "" : "";
            sign.gameObject.SetActive(!string.IsNullOrEmpty(sign.text));
            closing.transform.SetAsLastSibling();
            sign.transform.SetAsLastSibling();
            ScrollToTop();
        }

        public void ScrollToTop() => scroll.verticalNormalizedPosition = 1f;

        private Text Line(string name, int size, Color color, TextAnchor anchor, bool display)
        {
            var line = UiKit.Label(name, content, "", size, color, 0, 0, 1, 1, anchor);
            line.verticalOverflow = VerticalWrapMode.Overflow;
            line.lineSpacing = 1.2f;
            if (display) UiKit.Display(line);
            return line;
        }

        private void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(x / PaperW, 1f - (y + h) / height);
            rt.anchorMax = new Vector2((x + w) / PaperW, 1f - y / height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
