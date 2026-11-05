using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 누군가의 기록 화면 (#200, Figma '누군가의 일기 (#200)'): 도감에서 연다.
    // 찾은 단어의 줄 = 영어 + 해석, 못 찾은 줄 = 일그러진 필기 그림 (글자를 그리지 않으므로 절대 읽을 수 없음)
    public class JournalView
    {
        // Tools/make_journal_smudge.py와 같은 규격
        private const float SheetSize = 1024f;
        private const int EnglishRows = 8, EnglishHeight = 64;
        private const int KoreanRows = 8, KoreanHeight = 52;
        private const float EnglishCharWidth = 18f, KoreanCharWidth = 27f;

        private const int EnglishSize = 36, KoreanSize = 28, HeadingSize = 42;
        private const int LinePadding = 14;

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public int ReadableLines { get; private set; }
        public int LostLines { get; private set; }

        private Text title;
        private RectTransform content;
        private ScrollRect scroll;
        private Texture2D sheet;
        private float rowWidth;

        public static JournalView Create(Transform parent)
        {
            var view = new JournalView();
            var root = UiKit.Stretch("JournalView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background; // 뒤쪽 도감으로 터치가 새지 않게
            view.sheet = Resources.Load<Texture2D>("UI/Journal/smudge");

            view.title = UiKit.Display(UiKit.Label("Title", root, "", 52, Palette.Gold, 0, 0.94f, 1, 0.995f));
            UiKit.RoundPanel("Paper", root, Palette.Paper, UiKit.RadiusLg, 0.03f, 0.105f, 0.945f, 0.94f);
            view.BuildList(root);

            var close = UiKit.MakeButton("JournalCloseButton", root, "닫기", Palette.Neutral, 44, 0.05f, 0.012f, 0.95f, 0.092f);
            close.onClick.AddListener(view.Hide);

            view.Root.SetActive(false);
            return view;
        }

        private void BuildList(RectTransform parent)
        {
            var viewport = UiKit.Panel("Viewport", parent, Color.clear, 0.03f, 0.11f, 0.945f, 0.935f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content = (RectTransform)contentGo.transform;
            content.SetParent(viewport.transform, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 8, 24);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 60f;
            UiKit.VerticalScrollbar(scroll, 0.955f, 0.105f, 0.97f, 0.94f);
        }

        public void Show(WordDatabase book, GameSession session)
        {
            Root.SetActive(true);
            title.text = book.Journal != null ? book.Journal.DisplayName : "";
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var old = content.GetChild(i).gameObject;
                old.SetActive(false);
                Object.Destroy(old);
            }

            Canvas.ForceUpdateCanvases();
            rowWidth = Mathf.Max(100f, scroll.viewport.rect.width - 72f);
            ReadableLines = 0;
            LostLines = 0;
            foreach (var line in Journals.Lines(book, session.Vocabulary))
            {
                if (line.Heading.Length > 0) AddHeading(line.Heading);
                if (line.Readable) AddReadable(line.Word);
                else AddLost(line.Word);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        public void Hide() => Root.SetActive(false);

        private void AddHeading(string heading)
        {
            var label = UiKit.Display(UiKit.Label("Heading", content, heading, HeadingSize, Palette.PaperHeading, 0, 0, 1, 1, TextAnchor.LowerLeft));
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.gameObject.AddComponent<LayoutElement>().minHeight = 86;
        }

        private RectTransform AddRow(string name)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var rect = (RectTransform)row.transform;
            rect.SetParent(content, false);
            var layout = row.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, LinePadding, LinePadding);
            layout.spacing = 2;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rule = UiKit.Panel("Rule", rect, Palette.PaperLine, 0, 0, 1, 0);
            rule.rectTransform.sizeDelta = new Vector2(0, 2);
            rule.rectTransform.pivot = new Vector2(0.5f, 0);
            rule.raycastTarget = false;
            rule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return rect;
        }

        // 찾은 단어의 줄: 그 단어는 굵고 다른 색으로
        private void AddReadable(WordEntry word)
        {
            var row = AddRow("Line");
            var text = new StringBuilder();
            var (start, length) = Journals.WordSpan(word.Example, word.English);
            if (start >= 0)
            {
                text.Append(word.Example, 0, start);
                text.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(Palette.PaperWord)).Append('>');
                text.Append(word.Example, start, length);
                text.Append("</color></b>");
                text.Append(word.Example, start + length, word.Example.Length - start - length);
            }
            else text.Append(word.Example);

            AddText(row, "English", text.ToString(), EnglishSize, Palette.PaperText);
            AddText(row, "Korean", word.ExampleMeaning, KoreanSize, Palette.PaperDim);
            ReadableLines++;
        }

        private static void AddText(RectTransform row, string name, string text, int size, Color color)
        {
            var label = UiKit.Label(name, row, text, size, color, 0, 0, 1, 1, TextAnchor.UpperLeft);
            label.supportRichText = true;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // 못 찾은 줄: 큰 그림에서 낱말로 정해지는 곳을 잘라 붙인다 (Figma 시안의 흐린 글자 대신, #200 사용자: 못 알아볼 정도로)
        private void AddLost(WordEntry word)
        {
            var row = AddRow("LostLine");
            uint hash = Journals.StableHash(word.Id);
            // 그림 줄은 기록 순서로 돌아가며 써서 이웃한 줄이 같은 필기로 보이지 않게
            AddSmudge(row, "EnglishSmudge", word.Story % EnglishRows, hash, 0, EnglishHeight, word.Example.Length * EnglishCharWidth);
            AddSmudge(row, "KoreanSmudge", word.Story * 3 % KoreanRows, hash >> 8, EnglishRows * EnglishHeight, KoreanHeight, word.ExampleMeaning.Length * KoreanCharWidth);
            LostLines++;
        }

        private void AddSmudge(RectTransform row, string name, int rowIndex, uint hash, int top, int height, float width)
        {
            width = Mathf.Min(width, rowWidth, SheetSize);
            float maxStart = SheetSize - width;
            float start = maxStart <= 0 ? 0 : (hash >> 4) % (uint)maxStart;

            var holder = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            var rect = (RectTransform)holder.transform;
            rect.SetParent(row, false);
            holder.GetComponent<LayoutElement>().preferredHeight = height;

            var image = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(rect, false);
            image.texture = sheet;
            image.color = Palette.PaperSmudge;
            image.raycastTarget = false;
            image.uvRect = new Rect(start / SheetSize, 1f - (top + (rowIndex + 1) * height) / SheetSize, width / SheetSize, height / SheetSize);
            var imageRect = image.rectTransform;
            imageRect.anchorMin = new Vector2(0, 0);
            imageRect.anchorMax = new Vector2(0, 1);
            imageRect.pivot = new Vector2(0, 0.5f);
            imageRect.sizeDelta = new Vector2(width, 0);
            imageRect.anchoredPosition = Vector2.zero;
        }
    }
}
