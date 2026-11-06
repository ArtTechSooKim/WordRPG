using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 머리 위 말풍선 (#46, Figma 'Field — 사전 말풍선 (#46)'). 그림 = 팩 Ui/Dialog/DialogInfo(흰 풍선 4칸: 빈 · . · .. · ...)
    // + Emote 22(느낌표, 흰색으로). 글자 말풍선은 빈 풍선의 몸통을 늘리고(모서리는 도트 그대로) 꼬리를 붙인다.
    // #231 사용자: 말하는 캐릭터(Speaker)를 말풍선을 띄울 때 함께 정하고, 꼬리 끝은 늘 그 캐릭터 머리에 붙인다 —
    // 몸통만 화면 안으로 밀린다 (예전엔 꼬리가 몸통 가운데에 붙어 있어서, 긴 말이 화면 안으로 밀리면 꼬리가 엉뚱한 사람을 가리켰음).
    // 위에 자리가 없으면 발 아래로 뒤집혀 꼬리가 위를 가리킨다. 긴 말은 줄을 바꿔 너비를 줄인다
    public class SpeechBubble
    {
        public enum Icon { Dot1 = 1, Dot2 = 2, Dot3 = 3, Exclaim = 4 }

        private const string StripPath = "Art/NinjaAdventure/Ui/bubble";
        private const string ExclaimPath = "Art/NinjaAdventure/Ui/bubble_exclaim";
        private const float PixelSize = 100f / 16f; // UI 한 칸(도트 1px) = 6.25 — 필드 타일과 같은 비율
        public const float MaxWidth = 700f;         // 넘으면 줄을 바꿈 (#231 — 예전 920은 화면 너비만 한 띠가 되었음)
        private const float TailHeight = 3 * PixelSize;
        private const float TailHalfWidth = 3 * PixelSize;
        private const float CornerInset = 5 * PixelSize; // 몸통의 둥근 모서리 — 꼬리는 그 안쪽 곧은 변에만
        private const float ScreenMargin = 8f;
        private const float HeadGap = 0.05f;             // 그림 맨 위(아래)에서 이만큼 안쪽 = 머리(발) — 예전 '+0.45칸'과 같음
        private static readonly Color HeadColor = new Color32(200, 100, 30, 255);
        private static readonly Color MainColor = new Color32(28, 31, 46, 255);
        private static readonly Color SubColor = new Color32(58, 63, 85, 255);
        private static readonly Color FootColor = new Color32(122, 127, 149, 255);

        public RectTransform Root { get; private set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;
        public bool ShowsText => IsVisible && textRoot.gameObject.activeSelf;
        public Icon? ShownIcon => IsVisible && icon.gameObject.activeSelf ? shownIcon : (Icon?)null;
        public string Text { get; private set; } = "";
        public Transform Speaker { get; private set; }      // 말하는 캐릭터 — 꼬리 끝이 이 머리에
        public bool FromNpc => Speaker != hero;              // 인물의 말(Speak)인지 — 아니면 주인공 (#227)
        public bool Flipped { get; private set; }            // 위에 자리가 없어 발 아래로 뒤집힘
        public string HeadText => ShowsText ? head.text : "";
        public string MainText => ShowsText ? main.text : "";

        private Transform hero;
        private Image icon;
        private RectTransform textRoot, body, tail;
        private Text head, main, sub, foot;
        private Icon shownIcon;
        private float hideAt = -1f;
        private Sprite[] iconSprites;

        // layer = 말풍선을 놓는 판 (HUD), defaultSpeaker = 주인공 (혼잣말·사전 읽기)
        public static SpeechBubble Create(RectTransform layer, Transform defaultSpeaker)
        {
            var bubble = new SpeechBubble { hero = defaultSpeaker, Speaker = defaultSpeaker };
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

            // 글자 말풍선: 꼬리(끝 = 말하는 캐릭터 머리) + 늘어나는 몸통. 몸통이 화면 안으로 밀려도 꼬리는 제자리
            bubble.textRoot = UiKit.Rect("TextBubble", root, 0.5f, 0f, 0.5f, 0f);
            bubble.textRoot.pivot = new Vector2(0.5f, 0f);
            var tailImage = UiKit.IconImage("Tail", bubble.textRoot, null, 0.5f, 0f, 0.5f, 0f);
            bubble.tail = tailImage.rectTransform;
            bubble.tail.pivot = new Vector2(0.5f, 0f);
            bubble.tail.sizeDelta = new Vector2(TailHalfWidth * 2f, TailHeight);
            var bodyImage = UiKit.Panel("Body", bubble.textRoot, Color.white, 0, 0, 1, 1);
            bodyImage.raycastTarget = false;
            bubble.body = bodyImage.rectTransform;
            bubble.body.offsetMin = new Vector2(0, TailHeight - 1f);
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

        // 점점점·느낌표 풍선 (주인공)
        public void ShowIcon(Icon which)
        {
            Speaker = hero;
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

        // 주인공 혼잣말 한 줄 (잠긴 받침대·오늘은 그만 등). seconds 뒤에 사라짐
        public void Say(string message, float seconds, string footnote = null)
        {
            Speaker = hero;
            ShowText(null, null, message, footnote, seconds);
        }

        // 인물의 말 (#209): 이름 + 한두 줄. speaker = 그 인물의 그림 — 꼬리가 그 머리에 붙는다 (#231)
        public void Speak(Transform speaker, string name, string line, float seconds)
        {
            Speaker = speaker != null ? speaker : hero;
            ShowText(name, null, line, null, seconds);
        }

        // 새 단어 (주인공): 머리말 · 영어 · 뜻 · 아래 작은 글씨. seconds ≤ 0 이면 움직일 때까지
        public void ShowWord(string headline, string english, string meaning, string footnote, float seconds)
        {
            Speaker = hero;
            ShowText(headline, english, meaning, footnote, seconds);
        }

        private void ShowText(string headline, string mainText, string subText, string footnote, float seconds)
        {
            const float padX = 44f, padTop = 30f, padBottom = 30f, gap = 2f, inset = padX - 8f;
            var lines = new (Text label, string text, float height)[]
            {
                (head, headline, 50f), (main, mainText, 86f), (sub, subText, 0f), (foot, footnote, 0f),
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
                // 혼잣말·인물의 말·뜻·작은 글씨: 몸통 너비에서 줄을 바꾼 높이만큼 (#231)
                float h = fixedHeight > 0f ? fixedHeight : WrappedHeight(label, text, width - inset * 2f);
                var rt = label.rectTransform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = new Vector2(inset, -(y + h));
                rt.offsetMax = new Vector2(-inset, -y);
                y += h + gap;
            }
            float height = y - gap + padBottom;
            textRoot.sizeDelta = new Vector2(width, height + TailHeight);

            Text = string.Join("\n", System.Array.FindAll(new[] { headline, mainText, subText, footnote }, s => !string.IsNullOrEmpty(s)));
            icon.gameObject.SetActive(false);
            textRoot.gameObject.SetActive(true);
            hideAt = seconds > 0f ? Time.unscaledTime + seconds : -1f;
            Root.gameObject.SetActive(true);
        }

        // 그 너비에서 줄을 바꿔 쓴 높이 (글자 크기·줄 간격은 그 글자 칸 그대로)
        private static float WrappedHeight(Text label, string text, float width)
        {
            var settings = label.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
            float scale = Mathf.Max(0.0001f, label.pixelsPerUnit);
            float lineHeight = label.fontSize * 1.3f;
            return Mathf.Max(lineHeight, label.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / scale);
        }

        public void Hide()
        {
            Text = "";
            Root.gameObject.SetActive(false);
        }

        // 캐릭터의 머리(맨 위) · 발(맨 아래) — 그림 크기를 따른다 (그림이 없으면 한 칸 그림으로 침)
        public static Vector3 HeadOf(Transform speaker) => EdgeOf(speaker, true);
        public static Vector3 FeetOf(Transform speaker) => EdgeOf(speaker, false);

        private static Vector3 EdgeOf(Transform speaker, bool top)
        {
            var position = speaker.position;
            var renderer = speaker.GetComponent<SpriteRenderer>();
            float y = renderer != null && renderer.sprite != null
                ? (top ? renderer.bounds.max.y - HeadGap : renderer.bounds.min.y + HeadGap)
                : position.y + (top ? 0.5f - HeadGap : -(0.5f - HeadGap));
            return new Vector3(position.x, y, position.z);
        }

        // 매 프레임 (카메라·캐릭터가 다 움직인 뒤): 꼬리 끝을 말하는 캐릭터 머리에, 몸통은 화면 안에. 시간이 다 되면 숨김
        public void Tick(Camera cam, Canvas canvas)
        {
            if (!IsVisible || cam == null) return;
            if ((hideAt > 0f && Time.unscaledTime >= hideAt) || Speaker == null) // 말하던 인물이 사라졌으면 같이
            {
                Hide();
                return;
            }
            var layer = (RectTransform)Root.parent;
            var canvasCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            // 인물이 화면 밖으로 나가면 닫힘 (#231 사용자: 호객이 오래 남는 건 좋지만 상인이 안 보이는데 말풍선만 떠 있었음).
            // 주인공 말은 그대로 — 연출 중 카메라가 다른 곳을 비출 수 있음
            if (FromNpc && !(ToLayer(layer, cam, canvasCamera, Speaker.position, out var center) && layer.rect.Contains(center)))
            {
                Hide();
                return;
            }
            if (!ToLayer(layer, cam, canvasCamera, HeadOf(Speaker), out var tip)) return;
            if (!textRoot.gameObject.activeSelf)
            {
                Root.anchoredPosition = tip; // 점점점·느낌표는 머리 위에 그대로
                return;
            }

            var bounds = layer.rect;
            float width = textRoot.sizeDelta.x, height = textRoot.sizeDelta.y;
            // 위에 자리가 없으면 발 아래로 (아래도 없으면 위 그대로)
            bool flip = false;
            if (tip.y + height > bounds.yMax - ScreenMargin && ToLayer(layer, cam, canvasCamera, FeetOf(Speaker), out var feet)
                                                            && feet.y - height >= bounds.yMin + ScreenMargin)
            {
                flip = true;
                tip = feet;
            }
            SetFlipped(flip);
            Root.anchoredPosition = tip;

            // 몸통은 화면 안으로 밀고, 꼬리는 머리 자리에 (몸통의 곧은 변 안에서)
            float half = width * 0.5f;
            float minX = bounds.xMin + half + ScreenMargin, maxX = Mathf.Max(minX, bounds.xMax - half - ScreenMargin);
            float shift = Mathf.Clamp(tip.x, minX, maxX) - tip.x;
            textRoot.anchoredPosition = new Vector2(shift, 0f);
            float reach = Mathf.Max(0f, half - CornerInset - TailHalfWidth);
            tail.anchoredPosition = new Vector2(Mathf.Clamp(-shift, -reach, reach), 0f);
        }

        private static bool ToLayer(RectTransform layer, Camera cam, Camera canvasCamera, Vector3 world, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, cam.WorldToScreenPoint(world), canvasCamera, out local);

        // 뒤집기: 몸통이 꼬리 아래로, 꼬리는 위를 가리킴 (꼬리 그림을 위아래로 뒤집음)
        private void SetFlipped(bool flip)
        {
            if (Flipped == flip) return;
            Flipped = flip;
            textRoot.pivot = new Vector2(0.5f, flip ? 1f : 0f);
            tail.anchorMin = tail.anchorMax = new Vector2(0.5f, flip ? 1f : 0f);
            tail.localScale = new Vector3(1f, flip ? -1f : 1f, 1f);
            body.offsetMin = new Vector2(0f, flip ? 0f : TailHeight - 1f);
            body.offsetMax = new Vector2(0f, flip ? -(TailHeight - 1f) : 0f);
        }

        // ---- 테스트용 (#231): 실제로 놓인 자리를 말풍선 판(layer) 좌표로

        // 꼬리 끝 (뒤집혔으면 위 끝)
        public Vector2 TailTip()
        {
            var corners = LayerCorners(tail);
            float x = (corners[0].x + corners[2].x) * 0.5f;
            float y = Flipped ? Mathf.Max(corners[0].y, corners[1].y) : Mathf.Min(corners[0].y, corners[1].y);
            return new Vector2(x, y);
        }

        public Rect BodyRect()
        {
            var corners = LayerCorners(body);
            float minX = Mathf.Min(corners[0].x, corners[2].x), maxX = Mathf.Max(corners[0].x, corners[2].x);
            float minY = Mathf.Min(corners[0].y, corners[2].y), maxY = Mathf.Max(corners[0].y, corners[2].y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public Rect LayerRect => ((RectTransform)Root.parent).rect;

        private Vector3[] LayerCorners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var layer = (RectTransform)Root.parent;
            for (int i = 0; i < 4; i++) corners[i] = layer.InverseTransformPoint(corners[i]);
            return corners;
        }
    }
}
