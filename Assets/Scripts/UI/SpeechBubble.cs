using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 주인공 머리 위 말풍선 (#46, Figma 'Field — 사전 말풍선 (#46)'). 그림 = 팩 Ui/Dialog/DialogInfo(흰 풍선 4칸: 빈 · . · .. · ...)
    // + Emote 22(느낌표, 흰색으로). 글자 말풍선은 빈 풍선의 몸통을 늘리고(모서리는 도트 그대로) 꼬리를 아래 가운데에 붙인다
    public class SpeechBubble
    {
        public enum Icon { Dot1 = 1, Dot2 = 2, Dot3 = 3, Exclaim = 4 }

        private const string StripPath = "Art/NinjaAdventure/Ui/bubble";
        private const string ExclaimPath = "Art/NinjaAdventure/Ui/bubble_exclaim";
        private const float PixelSize = 100f / 16f; // UI 한 칸(도트 1px) = 6.25 — 필드 타일과 같은 비율
        private const float MaxWidth = 920f;
        private static readonly Color HeadColor = new Color32(200, 100, 30, 255);
        private static readonly Color MainColor = new Color32(28, 31, 46, 255);
        private static readonly Color SubColor = new Color32(58, 63, 85, 255);
        private static readonly Color FootColor = new Color32(122, 127, 149, 255);

        public RectTransform Root { get; private set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;
        public bool ShowsText => IsVisible && textRoot.gameObject.activeSelf;
        public Icon? ShownIcon => IsVisible && icon.gameObject.activeSelf ? shownIcon : (Icon?)null;
        public string Text { get; private set; } = "";
        public string HeadText => ShowsText ? head.text : "";
        public string MainText => ShowsText ? main.text : "";

        private Image icon;
        private RectTransform textRoot, body;
        private Text head, main, sub, foot;
        private Icon shownIcon;
        private float hideAt = -1f;
        private Sprite[] iconSprites;

        public static SpeechBubble Create(RectTransform layer)
        {
            var bubble = new SpeechBubble();
            var root = UiKit.Rect("SpeechBubble", layer, 0.5f, 0.5f, 0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = Vector2.zero;
            bubble.Root = root;

            var strip = Resources.Load<Texture2D>(StripPath);
            var exclaim = Resources.Load<Texture2D>(ExclaimPath);
            bubble.iconSprites = new Sprite[5];
            if (strip != null)
                for (int i = 0; i < 4; i++)
                    bubble.iconSprites[i] = Sprite.Create(strip, new Rect(i * 20, 0, 20, 16), new Vector2(0.5f, 0f), 16, 0, SpriteMeshType.FullRect);
            if (exclaim != null)
                bubble.iconSprites[4] = Sprite.Create(exclaim, new Rect(0, 0, exclaim.width, exclaim.height), new Vector2(0.5f, 0f), 16, 0,
                    SpriteMeshType.FullRect);

            // 점점점·느낌표: 풍선 한 장
            bubble.icon = UiKit.IconImage("Icon", root, null, 0.5f, 0f, 0.5f, 0f);
            bubble.icon.rectTransform.pivot = new Vector2(0.5f, 0f);
            bubble.icon.preserveAspect = true;

            // 글자 말풍선: 꼬리(아래 가운데) + 늘어나는 몸통
            bubble.textRoot = UiKit.Rect("TextBubble", root, 0.5f, 0f, 0.5f, 0f);
            bubble.textRoot.pivot = new Vector2(0.5f, 0f);
            var tailImage = UiKit.IconImage("Tail", bubble.textRoot, null, 0.5f, 0f, 0.5f, 0f);
            tailImage.rectTransform.pivot = new Vector2(0.5f, 0f);
            tailImage.rectTransform.sizeDelta = new Vector2(6 * PixelSize, 3 * PixelSize);
            var bodyImage = UiKit.Panel("Body", bubble.textRoot, Color.white, 0, 0, 1, 1);
            bodyImage.raycastTarget = false;
            bubble.body = bodyImage.rectTransform;
            bubble.body.offsetMin = new Vector2(0, 3 * PixelSize - 1f);
            if (strip != null)
            {
                // 빈 풍선(첫 칸): 몸통 = 위 1~12줄, 꼬리 = 13~15줄 가운데 6칸 (텍스처는 아래가 y=0)
                tailImage.sprite = Sprite.Create(strip, new Rect(6, 0, 6, 3), new Vector2(0.5f, 0f), 16, 0, SpriteMeshType.FullRect);
                tailImage.enabled = true;
                bodyImage.sprite = Sprite.Create(strip, new Rect(0, 3, 19, 12), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect,
                    new Vector4(5, 4, 5, 4));
                bodyImage.type = Image.Type.Sliced;
            }
            bubble.head = UiKit.Display(UiKit.OneLine(UiKit.Label("Head", bubble.body, "", 38, HeadColor, 0, 1, 1, 1)));
            bubble.main = UiKit.Display(UiKit.OneLine(UiKit.Label("Main", bubble.body, "", 72, MainColor, 0, 1, 1, 1, TextAnchor.MiddleCenter,
                FontStyle.Normal, true, 36)));
            bubble.sub = UiKit.OneLine(UiKit.Label("Sub", bubble.body, "", 36, SubColor, 0, 1, 1, 1)); // 줄 높이가 커서 잘리지 않게
            bubble.foot = UiKit.OneLine(UiKit.Label("Foot", bubble.body, "", 26, FootColor, 0, 1, 1, 1));

            root.gameObject.SetActive(false);
            return bubble;
        }

        // 점점점·느낌표 풍선
        public void ShowIcon(Icon which)
        {
            shownIcon = which;
            var sprite = iconSprites[(int)which];
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            float w = sprite != null ? sprite.rect.width : 20, h = sprite != null ? sprite.rect.height : 16;
            icon.rectTransform.sizeDelta = new Vector2(w, h) * PixelSize;
            icon.gameObject.SetActive(true);
            textRoot.gameObject.SetActive(false);
            Text = which == Icon.Exclaim ? "!" : new string('.', (int)which);
            hideAt = -1f;
            Root.gameObject.SetActive(true);
        }

        // 혼잣말 한 줄 (잠긴 받침대·오늘은 그만 등). seconds 뒤에 사라짐
        public void Say(string message, float seconds, string footnote = null) => ShowText(null, null, message, footnote, seconds);

        // 새 단어: 머리말 · 영어 · 뜻 · 아래 작은 글씨. seconds ≤ 0 이면 움직일 때까지
        public void ShowWord(string headline, string english, string meaning, string footnote, float seconds) =>
            ShowText(headline, english, meaning, footnote, seconds);

        private void ShowText(string headline, string mainText, string subText, string footnote, float seconds)
        {
            const float padX = 44f, padTop = 30f, padBottom = 30f, gap = 2f;
            var lines = new (Text label, string text, float height)[]
            {
                (head, headline, 50f), (main, mainText, 86f), (sub, subText, 0f), (foot, footnote, 36f),
            };
            float width = 0f;
            foreach (var (label, text, _) in lines)
            {
                label.gameObject.SetActive(!string.IsNullOrEmpty(text));
                label.text = text ?? "";
                if (!string.IsNullOrEmpty(text)) width = Mathf.Max(width, label.preferredWidth);
            }
            width = Mathf.Clamp(width + padX * 2f, 260f, MaxWidth);

            float y = padTop;
            foreach (var (label, text, fixedHeight) in lines)
            {
                if (string.IsNullOrEmpty(text)) continue;
                float h = fixedHeight;
                if (h <= 0f) // 여러 줄일 수 있는 혼잣말·뜻: 줄 수만큼
                {
                    int count = text.Split('\n').Length;
                    h = 52f * count;
                }
                var rt = label.rectTransform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(padX - 8f, -(y + h));
                rt.offsetMax = new Vector2(-(padX - 8f), -y);
                y += h + gap;
            }
            float height = y - gap + padBottom;
            textRoot.sizeDelta = new Vector2(width, height + 3 * PixelSize);

            Text = string.Join("\n", System.Array.FindAll(new[] { headline, mainText, subText, footnote }, s => !string.IsNullOrEmpty(s)));
            icon.gameObject.SetActive(false);
            textRoot.gameObject.SetActive(true);
            hideAt = seconds > 0f ? Time.unscaledTime + seconds : -1f;
            Root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            Text = "";
            Root.gameObject.SetActive(false);
        }

        // 매 프레임: 월드 위치(주인공 머리 위)를 화면 위치로. 시간이 다 되면 숨김
        public void Tick(Camera cam, Canvas canvas, Vector3 world)
        {
            if (!IsVisible || cam == null) return;
            if (hideAt > 0f && Time.unscaledTime >= hideAt)
            {
                Hide();
                return;
            }
            var layer = (RectTransform)Root.parent;
            var canvasCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, cam.WorldToScreenPoint(world), canvasCamera, out var local))
                Root.anchoredPosition = local;
        }
    }
}
