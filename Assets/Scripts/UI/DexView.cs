using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 지역 단어 도감 화면: 진행률 + 완성 보상 + 스크롤 목록 + 단어 상세.
    // 미발견 단어는 "???"로 가려서 '채워 나가는' 재미를 준다
    public class DexView
    {
        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        private Text title, progressText, rewardText, detailText;
        private Image rewardIcon;
        private RectTransform progressFill, content;
        private ScrollRect scroll;
        private readonly List<GameObject> rows = new List<GameObject>();

        private WordDatabase region;
        private GameSession session;
        private DateTime nowUtc;

        public static DexView Create(Transform parent)
        {
            var view = new DexView();
            var rootRect = UiKit.Stretch("DexView", parent);
            view.Root = rootRect.gameObject;
            var bg = view.Root.AddComponent<Image>(); // 뒤쪽 화면으로 터치가 새지 않게 막는다
            bg.color = Palette.Background;

            view.title = UiKit.IconTitle("Title", rootRect, UiKit.Icon("dex"), "단어 도감", 52, Palette.Gold, 0, 0.928f, 1, 0.995f);
            view.progressText = UiKit.Label("Progress", rootRect, "", 34, Palette.Text, 0.03f, 0.89f, 0.97f, 0.93f,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var barBack = UiKit.Pill(UiKit.Panel("BarBack", rootRect, Palette.Track, 0.03f, 0.869f, 0.97f, 0.884f));
            var fill = UiKit.Pill(UiKit.Panel("BarFill", barBack.transform, Palette.Gold));
            view.progressFill = fill.rectTransform;

            var rewardBox = UiKit.RoundPanel("RewardBox", rootRect, Palette.Panel, UiKit.RadiusMd, 0.03f, 0.775f, 0.97f, 0.858f);
            view.rewardIcon = UiKit.IconImage("Keepsake", rewardBox.transform, null, 0.02f, 0.1f, 0.12f, 0.9f);
            view.rewardText = UiKit.Label("Reward", rewardBox.transform, "", 32, Palette.Text, 0.14f, 0, 1, 1,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 20);
            UiKit.Pad(view.rewardText.rectTransform, 8, 4, 16, 4);

            view.BuildList(rootRect);

            var detailBox = UiKit.RoundPanel("DetailBox", rootRect, Palette.PanelLight, UiKit.RadiusMd, 0.03f, 0.105f, 0.97f, 0.29f);
            view.detailText = UiKit.Label("Detail", detailBox.transform, "", 34, Palette.Text, 0, 0, 1, 1,
                TextAnchor.UpperLeft, FontStyle.Normal, true, 20);
            UiKit.Pad(view.detailText.rectTransform, 22, 10, 22, 10);

            var close = UiKit.MakeButton("DexCloseButton", rootRect, "닫기", Palette.Neutral, 44,
                0.05f, 0.012f, 0.95f, 0.092f);
            close.onClick.AddListener(view.Hide);

            view.Root.SetActive(false);
            return view;
        }

        private void BuildList(RectTransform parent)
        {
            var viewport = UiKit.Panel("Viewport", parent, Color.clear, 0.03f, 0.30f, 0.97f, 0.765f); // 투명하지만 스크롤 터치는 받음
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
            layout.spacing = 8;
            layout.padding = new RectOffset(10, 10, 10, 10);
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
        }

        public void Show(WordDatabase wordBook, GameSession gameSession, DateTime now)
        {
            region = wordBook;
            session = gameSession;
            nowUtc = now;
            Root.SetActive(true);
            Refresh();
            content.anchoredPosition = Vector2.zero;
            detailText.text = "단어를 눌러 자세히 보세요";
        }

        public void Hide() => Root.SetActive(false);

        private void Refresh()
        {
            var progress = Dex.GetProgress(region, session.Vocabulary);
            string regionName = string.IsNullOrEmpty(region.RegionName) ? "" : $" · {region.RegionName}";
            title.text = $"단어 도감{regionName}";
            int wrongNote = 0;
            foreach (var word in region.Words)
            {
                var entry = session.Vocabulary.Find(word.Id);
                if (entry != null && entry.InWrongNote) wrongNote++;
            }
            progressText.text = $"{progress.Discovered} / {progress.Total} 발견      완전 숙련 {progress.Mastered}개" +
                                (wrongNote > 0 ? $"      오답 노트 {wrongNote}" : "");
            progressFill.anchorMax = new Vector2(progress.Ratio, 1);
            rewardText.text = RewardText(progress);
            var keepsakeIcon = UiKit.ItemIcon(region.CompletionKeepsake);
            rewardIcon.sprite = keepsakeIcon;
            rewardIcon.enabled = keepsakeIcon != null;

            foreach (var row in rows) UnityEngine.Object.Destroy(row);
            rows.Clear();
            for (int i = 0; i < region.Words.Count; i++) rows.Add(CreateRow(i, region.Words[i]));
        }

        private string RewardText(DexProgress progress)
        {
            var keepsake = region.CompletionKeepsake;
            if (keepsake == null && region.CompletionGold <= 0) return "이 지역은 완성 보상이 없어요";

            var reward = new StringBuilder("완성 보상: ");
            if (keepsake != null) reward.Append($"징표 '{keepsake.DisplayName}'");
            if (keepsake != null && region.CompletionGold > 0) reward.Append(" + ");
            if (region.CompletionGold > 0) reward.Append($"{region.CompletionGold} 골드");

            bool claimed = !string.IsNullOrEmpty(region.RegionId) && session.Record.HasClaimedRegion(region.RegionId);
            reward.Append(claimed ? "\n★ 획득 완료!" : $"\n앞으로 {progress.Total - progress.Discovered}개 더 발견하면 받아요");
            return reward.ToString();
        }

        private GameObject CreateRow(int index, WordEntry word)
        {
            var level = session.Vocabulary.GetLevel(word.Id);
            bool discovered = level > MasteryLevel.New;
            var entryProgress = session.Vocabulary.Find(word.Id);
            bool wrong = discovered && entryProgress != null && entryProgress.InWrongNote;

            var image = UiKit.RoundPanel($"DexRow_{index}", content, RowColor(level), UiKit.RadiusMd);
            image.gameObject.AddComponent<LayoutElement>().preferredHeight = 92;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            int captured = index;
            button.onClick.AddListener(() => ShowDetail(captured));

            UiKit.Label("Number", image.transform, $"{index + 1:000}", 28, Palette.TextDim, 0.03f, 0, 0.12f, 1, TextAnchor.MiddleLeft);
            var name = UiKit.Label("Name", image.transform, discovered ? word.English : "???", 40,
                discovered ? Palette.Text : Palette.TextDim, 0.13f, 0, wrong ? 0.5f : 0.66f, 1, TextAnchor.MiddleLeft,
                discovered ? FontStyle.Bold : FontStyle.Normal, true, 22);
            if (!discovered) UiKit.Display(name);
            // 오답 노트에 있는 단어: 빨간 '오답' 알약 (맞히면 사라짐, #48)
            if (wrong)
            {
                var tag = UiKit.Pill(UiKit.Panel("WrongTag", image.transform, Palette.Bad, 0.51f, 0.26f, 0.63f, 0.74f));
                tag.raycastTarget = false;
                UiKit.OneLine(UiKit.Label("Text", tag.transform, "오답", 24, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            }
            // 숙련도 별 4개 (Figma Icon/Star Full · Star Empty)
            if (discovered)
            {
                int filled = (int)level;
                for (int s = 0; s < 4; s++)
                {
                    float x = 0.68f + s * 0.075f;
                    UiKit.IconImage($"Star_{s}", image.transform, UiKit.Icon(s < filled ? "star_full" : "star_empty"), x, 0.24f, x + 0.065f, 0.76f);
                }
            }
            return image.gameObject;
        }

        private static Color RowColor(MasteryLevel level)
        {
            switch (level)
            {
                case MasteryLevel.Learning: return Palette.MasteryLearning;
                case MasteryLevel.Reviewing: return Palette.MasteryReviewing;
                case MasteryLevel.Proficient: return Palette.MasteryProficient;
                case MasteryLevel.Mastered: return Palette.MasteryMastered;
                default: return Palette.Panel;
            }
        }

        private void ShowDetail(int index)
        {
            var word = region.Words[index];
            var progress = session.Vocabulary.Find(word.Id);
            if (progress == null || progress.Level == MasteryLevel.New)
            {
                detailText.text = $"{index + 1:000}  ???\n아직 발견하지 못한 단어예요.\n전투에서 만나면 도감에 등록돼요!";
                return;
            }

            var text = new StringBuilder();
            text.Append($"{word.English}");
            if (word.PartOfSpeech.Length > 0) text.Append($"  ({word.PartOfSpeech})");
            text.Append($"\n{word.Meaning}");
            if (word.Example.Length > 0)
            {
                text.Append($"\n{word.Example}");
                if (word.ExampleMeaning.Length > 0) text.Append($"  {word.ExampleMeaning}");
            }
            text.Append($"\n{Dex.Stars(progress.Level)} {progress.Level.DisplayName()}   맞힘 {progress.CorrectCount} · 틀림 {progress.WrongCount}");
            text.Append($"\n{Dex.DescribeNextReview(progress, nowUtc)}");
            if (progress.InWrongNote) text.Append("\n오답 노트에 있어요 — 전투나 수련에서 맞히면 빠져요");
            var found = progress.DiscoveredUtc;
            if (found.HasValue) text.Append($"   (발견 {found.Value.ToLocalTime():yyyy-MM-dd})");
            detailText.text = text.ToString();
        }
    }
}
