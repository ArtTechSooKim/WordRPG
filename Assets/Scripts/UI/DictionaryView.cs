using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 보스의 사전 읽기 (#45, Figma 'Field — 사전 읽기 (#45)'): 뒤를 어둡게 덮고 사전 판.
    // '..' → '…' → '.....' → '!!' 한 줄씩 (화면을 누르면 건너뜀) → "새로운 단어를 발견했다!" + 단어 카드 + 발견! 도장 → [확인]
    public class DictionaryView
    {
        private const float PanelWidth = 960f;
        private static readonly string[] Dots = { "..", "…", ".....", "!!" };

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public bool IsRevealed => ok != null && ok.gameObject.activeSelf; // 단어가 보이는 중 ([확인]을 누를 수 있음)
        public string HeadText => head.text;
        public string WordText => english.text;

        private RectTransform panel, stamp;
        private Text title, dots, head, english, meaning, footer;
        private GameObject wordCard;
        private Button ok;
        private bool skip, closed;
        private Vector2 dotsMin, dotsMax, dotsOffsetMin, dotsOffsetMax; // 단어가 보일 때 점점점 자리 (제목 아래 한 줄)

        public static DictionaryView Create(Transform parent)
        {
            var view = new DictionaryView();
            var root = UiKit.Panel("DictionaryView", parent, Palette.Scrim); // 뒤쪽은 눌리지 않게
            view.Root = root.gameObject;
            var skipButton = root.gameObject.AddComponent<Button>(); // 점점점 동안 화면을 누르면 건너뜀
            skipButton.transition = Selectable.Transition.None;
            skipButton.onClick.AddListener(() => view.skip = true);

            var panelImage = UiKit.RoundPanel("Panel", root.transform, Palette.Panel, UiKit.RadiusLg, 0.5f, 0.5f, 0.5f, 0.5f);
            view.panel = panelImage.rectTransform;
            UiKit.Outline(UiKit.Panel("Border", view.panel, Palette.Gold), UiKit.RadiusLg, 4).raycastTarget = false;

            float y = 48f;
            view.title = UiKit.Display(UiKit.OneLine(UiKit.Label("Title", view.panel, "", 60, Palette.Gold, 0, 1, 1, 1)));
            Place(view.title.rectTransform, ref y, 80f, 6f);
            view.dots = UiKit.Display(UiKit.OneLine(UiKit.Label("Dots", view.panel, "", 64, Palette.TextDim, 0, 1, 1, 1)));
            Place(view.dots.rectTransform, ref y, 80f, 10f);
            var dotsRect = view.dots.rectTransform;
            view.dotsMin = dotsRect.anchorMin;
            view.dotsMax = dotsRect.anchorMax;
            view.dotsOffsetMin = dotsRect.offsetMin;
            view.dotsOffsetMax = dotsRect.offsetMax;
            view.head = UiKit.Display(UiKit.OneLine(UiKit.Label("Head", view.panel, "", 56, Palette.Gold, 0, 1, 1, 1)));
            Place(view.head.rectTransform, ref y, 72f, 18f);

            var card = UiKit.RoundPanel("Word", view.panel, Palette.PanelLight, UiKit.RadiusMd, 0, 1, 1, 1);
            card.raycastTarget = false;
            view.wordCard = card.gameObject;
            view.english = UiKit.Display(UiKit.OneLine(UiKit.Label("English", card.transform, "", 96, Palette.Text, 0, 0.42f, 1, 0.95f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 48)));
            view.meaning = UiKit.Label("Meaning", card.transform, "", 42, Palette.Text, 0, 0.06f, 1, 0.42f, TextAnchor.MiddleCenter,
                FontStyle.Normal, true, 24);
            UiKit.Pad(view.meaning.rectTransform, 24, 0, 24, 0);
            Place(card.rectTransform, ref y, 236f, 18f);

            // 발견! 도장 (카드 왼쪽 위, 살짝 기울임)
            var stampImage = UiKit.Pill(UiKit.Panel("Stamp", card.transform, Palette.Gold, 0, 1, 0, 1));
            stampImage.raycastTarget = false;
            view.stamp = stampImage.rectTransform;
            view.stamp.pivot = new Vector2(0.5f, 0.5f);
            view.stamp.sizeDelta = new Vector2(150, 64);
            view.stamp.anchoredPosition = new Vector2(40, -8);
            view.stamp.localEulerAngles = new Vector3(0, 0, 12);
            UiKit.Display(UiKit.Label("Text", stampImage.transform, "발견!", 40, Palette.OnAccent, 0, 0, 1, 1));

            view.footer = UiKit.OneLine(UiKit.Label("Footer", view.panel, "", 32, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleCenter,
                FontStyle.Normal, true, 20));
            Place(view.footer.rectTransform, ref y, 48f, 20f);
            view.ok = UiKit.MakeButton("DictionaryOk", view.panel, "확인", Palette.Button, 48, 0, 1, 1, 1);
            view.ok.onClick.AddListener(() => view.closed = true);
            Place((RectTransform)view.ok.transform, ref y, 110f, 48f);

            view.panel.sizeDelta = new Vector2(PanelWidth, y);
            view.Root.SetActive(false);
            return view;
        }

        // 사전을 펼쳐 읽는다: 점점점 → 발견한 단어. [확인]을 누르면 닫힘
        public IEnumerator Play(string bookName, WordEntry word, string footerText, float animScale)
        {
            title.text = bookName;
            dots.text = "";
            SetRevealed(false);
            skip = closed = false;
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);

            foreach (var line in Dots)
            {
                dots.text = line;
                if (!skip) Sound.Play(Sfx.Click);
                float end = Time.unscaledTime + 0.5f * animScale;
                while (!skip && Time.unscaledTime < end) yield return null;
            }

            head.text = "새로운 단어를 발견했다!";
            english.text = word.English;
            meaning.text = word.Meaning;
            footer.text = footerText;
            SetRevealed(true);
            Sound.PlayJingle(Sfx.NewWord);
            // 도장이 크게 찍히듯
            float pop = 0.25f * animScale;
            for (float t = 0f; t < pop; t += Time.unscaledDeltaTime)
            {
                stamp.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, t / Mathf.Max(0.0001f, pop));
                yield return null;
            }
            stamp.localScale = Vector3.one;

            while (!closed) yield return null;
            Root.SetActive(false);
        }

        // 뒤로가기: 점점점이면 건너뛰고, 단어가 보이면 닫는다
        public void Back()
        {
            if (IsRevealed) closed = true;
            else skip = true;
        }

        private void SetRevealed(bool revealed)
        {
            // 점점점 동안은 판 가운데에 크게, 단어가 보이면 제목 아래 한 줄로
            var rect = dots.rectTransform;
            if (revealed)
            {
                rect.anchorMin = dotsMin;
                rect.anchorMax = dotsMax;
                rect.offsetMin = dotsOffsetMin;
                rect.offsetMax = dotsOffsetMax;
                dots.fontSize = 64;
            }
            else
            {
                rect.anchorMin = new Vector2(0, 0.5f);
                rect.anchorMax = new Vector2(1, 0.5f);
                rect.offsetMin = new Vector2(48, -90);
                rect.offsetMax = new Vector2(-48, 70);
                dots.fontSize = 120;
            }
            head.gameObject.SetActive(revealed);
            wordCard.SetActive(revealed);
            footer.gameObject.SetActive(revealed);
            ok.gameObject.SetActive(revealed);
        }

        // 판 안: 위에서 y만큼 내려와 높이 h (좌우 48 여백)
        private static void Place(RectTransform rect, ref float y, float h, float gap)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(48, -(y + h));
            rect.offsetMax = new Vector2(-48, -y);
            y += h + gap;
        }
    }
}
