using UnityEngine;
using UnityEngine.UI;

namespace WordRPG.UI
{
    // 한 줄로 맞추는 글 (#287 사용자: 단어가 이상하게 두 줄로 꺾임 — 통일감 있게 한 줄로).
    // Unity의 bestFit은 줄을 바꿔서라도 큰 글자를 먼저 고름 → 'computed' / 'tomo/graphy', '~하기로 되어 있는,' / '~ 때문인'처럼 꺾임.
    // 이 칸은 너비에 한 줄로 들어가는 가장 큰 크기(최대 = 처음 크기, 최소 = minSize)로 한 줄. 가장 작게도 안 들어갈 때만:
    //   Wrap = 예전처럼 여러 줄(bestFit) · Ellipsis = 가장 작은 크기 한 줄 + 끝 '...' (Jua에 '…'가 없음)
    // 글이나 칸 크기가 바뀌면 다시 맞춤 (LateUpdate — 캔버스를 그리기 전). 한 줄짜리 글(단어·뜻·보기)에만 붙일 것
    [RequireComponent(typeof(Text))]
    public class OneLineFit : MonoBehaviour
    {
        public enum WhenTooLong { Wrap, Ellipsis }
        public const string Dots = "...";

        private Text label;
        private int maxSize, minSize;
        private WhenTooLong tooLong;
        private string source = "", shown;
        private Vector2 fittedFor = new Vector2(-1f, -1f);
        private float fittedScale = -1f;
        private bool force = true;

        public bool IsOneLine { get; private set; }

        // 보기 버튼: 한 줄로 줄이는 건 처음 크기의 70%까지 — 더 작아지면 옆 보기와 크기 차이가 너무 커서 차라리 두 줄 (#287 캡처 비교)
        public static int ReadableMin(int fontSize) => Mathf.RoundToInt(fontSize * 0.7f);

        // minSize < 0 이면 그 글의 bestFit 최소 크기 (bestFit이 아니면 처음 크기의 0.6배)
        public static Text Apply(Text label, int minSize = -1, WhenTooLong tooLong = WhenTooLong.Wrap)
        {
            if (!label.TryGetComponent(out OneLineFit fit)) fit = label.gameObject.AddComponent<OneLineFit>();
            fit.label = label;
            fit.maxSize = label.resizeTextForBestFit ? label.resizeTextMaxSize : label.fontSize;
            if (minSize < 0) minSize = label.resizeTextForBestFit ? label.resizeTextMinSize : Mathf.RoundToInt(fit.maxSize * 0.6f);
            fit.minSize = Mathf.Clamp(minSize, 1, fit.maxSize);
            fit.tooLong = tooLong;
            fit.shown = null;
            fit.force = true;
            fit.Refit();
            return label;
        }

        private void OnEnable()
        {
            force = true; // 꺼져 있는 동안 칸 크기가 바뀌었을 수 있음
            Refit();
        }

        private void LateUpdate() => Refit();

        public void Refit()
        {
            if (label == null) return;
            var box = label.rectTransform.rect.size;
            float scale = label.pixelsPerUnit;
            string now = label.text ?? "";
            bool changed = now != shown;
            if (!force && !changed && box == fittedFor && scale == fittedScale) return;
            force = false;
            if (changed) source = now; // 내가 자른 글('...')이 그대로면 원래 글을 그대로 씀
            fittedFor = box;
            fittedScale = scale;
            shown = now;
            if (box.x <= 1f || source.Length == 0) return;

            int size = LargestOneLine(label, source, box, maxSize, minSize);
            string text = source;
            IsOneLine = size > 0 || tooLong == WhenTooLong.Ellipsis;
            if (size > 0) Set(size, true);
            else if (tooLong == WhenTooLong.Wrap) Set(maxSize, false);
            else
            {
                Set(minSize, true);
                text = Cut(label, source, box.x, minSize);
            }
            shown = text;
            if (now != text) label.text = text;
        }

        private void Set(int fontSize, bool oneLine)
        {
            label.horizontalOverflow = oneLine ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = !oneLine;
            label.resizeTextMinSize = minSize;
            label.resizeTextMaxSize = maxSize;
            label.fontSize = fontSize;
        }

        // 너비(칸이 Truncate면 높이도)에 한 줄로 들어가는 가장 큰 크기, 없으면 0
        public static int LargestOneLine(Text label, string text, Vector2 box, int max, int min)
        {
            if (min > max) return 0;
            var settings = OneLineSettings(label);
            float ppu = Mathf.Max(0.0001f, label.pixelsPerUnit);
            var gen = label.cachedTextGeneratorForLayout;
            bool checkHeight = label.verticalOverflow == VerticalWrapMode.Truncate;
            float width = box.x - 1f;
            bool Fits(int s)
            {
                settings.fontSize = s;
                if (gen.GetPreferredWidth(text, settings) / ppu > width) return false;
                return !checkHeight || gen.GetPreferredHeight(text, settings) / ppu <= box.y + 1f;
            }
            settings.fontSize = max;
            float full = gen.GetPreferredWidth(text, settings) / ppu;
            int size = full <= width ? max : Mathf.Clamp(Mathf.FloorToInt(max * width / full), min, max);
            if (Fits(size))
            {
                while (size < max && Fits(size + 1)) size++;
                return size;
            }
            for (size--; size >= min; size--)
                if (Fits(size)) return size;
            return 0;
        }

        // 그 크기 한 줄로 쓴 너비 (칸 단위)
        public static float OneLineWidth(Text label, string text, int fontSize)
        {
            var settings = OneLineSettings(label);
            settings.fontSize = fontSize;
            return label.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / Mathf.Max(0.0001f, label.pixelsPerUnit);
        }

        private static TextGenerationSettings OneLineSettings(Text label)
        {
            var settings = label.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            return settings;
        }

        // 가장 작은 크기로도 넘치면 뒤를 잘라 '...' (글자 단위)
        public static string Cut(Text label, string text, float width, int fontSize)
        {
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (OneLineWidth(label, text.Substring(0, mid).TrimEnd() + Dots, fontSize) <= width - 1f) lo = mid;
                else hi = mid - 1;
            }
            return text.Substring(0, lo).TrimEnd() + Dots;
        }
    }
}
