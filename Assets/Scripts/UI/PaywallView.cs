using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;

namespace WordRPG.UI
{
    // 정식판 안내 (#40, Figma '필드 — 정식판 안내 (#40)'): 정식판이 필요한 지역(숲) 입구에서 뜬다.
    // [가격에 정식판 열기] · [구매 복원] · [나중에]. 사거나 되찾으면 닫히고 onUnlocked.
    // 문구(지역 이름·단어 수·몬스터·성유물)는 PaywallOffer로 받는다 — 지역 데이터에서 만들므로 지역이 늘어도 그대로
    public class PaywallOffer
    {
        public string AreaName;
        public List<string> Lines = new List<string>();
        public List<Sprite> Icons = new List<Sprite>();

        // 정식판이 필요한 지역 중 이야기 순서로 가장 앞 (숲) — #233: 예전엔 목록 순서라 동굴이 나왔음
        public static FieldArea FirstFullVersionArea(IEnumerable<FieldArea> areas)
        {
            FieldArea first = null;
            if (areas != null)
                foreach (var candidate in areas)
                    if (candidate != null && candidate.RequiresFullVersion && (first == null || candidate.StoryOrder < first.StoryOrder)) first = candidate;
            return first;
        }

        // 안내 문구는 그 지역 데이터에서: 단어 수 · 몬스터·보스 · 성유물(상자·보스 보상) · 앞으로의 지역 · 특전 (필드·타이틀이 같이 씀)
        public static PaywallOffer For(FieldArea target, GameDatabase gameDatabase)
        {
            var offer = new PaywallOffer { AreaName = target.DisplayName };
            if (target.Words != null && target.Words.Words.Count > 0)
                offer.Lines.Add($"{target.DisplayName} 지역 — 새 영단어 {target.Words.Words.Count}개");
            var species = new HashSet<MonsterSpecies>();
            if (target.Encounters != null)
                foreach (var entry in target.Encounters.Entries)
                    if (entry.Species != null) species.Add(entry.Species);
            var boss = target.Boss;
            offer.Lines.Add(boss != null ? $"새 몬스터 {species.Count}종과 보스 '{boss.Species.DisplayName}'" : $"새 몬스터 {species.Count}종");
            var relics = new List<RelicData>();
            foreach (var chest in target.ChestContents)
                if (chest.Relic != null) relics.Add(chest.Relic);
            if (boss?.RewardRelic != null) relics.Add(boss.RewardRelic);
            if (relics.Count > 0)
                offer.Lines.Add($"{target.DisplayName}의 성유물 {relics.Count}개 ({string.Join("·", relics.ConvertAll(r => r.DisplayName))})");
            offer.Lines.Add("앞으로 추가될 지역도 모두");
            string gift = gameDatabase != null ? FullVersionGift.Describe(gameDatabase.FullVersionGift) : "";
            offer.Lines.Add(gift.Length > 0 ? $"특전: {gift} · 모습 바꾸기" : "특전: 모습 바꾸기 (8가지 중 언제든)"); // #233 사용자 ⑨⑮
            foreach (var relic in relics) offer.Icons.Add(UiKit.RelicIcon(relic));
            return offer;
        }
    }

    public class PaywallView
    {
        private const float PanelWidth = 960f;
        private static string Footer => $"결제는 {StoreLabels.Account()}으로 진행돼요";

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public bool IsBusy { get; private set; } // 결제 창이 떠 있는 동안 (닫기·뒤로가기 막음)
        public string StatusText => status.text;

        private RectTransform panel;
        private Text title, note, status, buyLabel;
        private readonly List<Text> lines = new List<Text>();
        private readonly List<Image> icons = new List<Image>();
        private Button buy, restore, later;
        private IStore store;
        private Entitlements entitlements;
        private Action onUnlocked;

        public static PaywallView Create(Transform parent)
        {
            var view = new PaywallView();
            var root = UiKit.Stretch("PaywallView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f); // 뒤는 눌리지 않게

            var panelImage = UiKit.RoundPanel("Panel", root, Palette.Panel, UiKit.RadiusLg, 0.5f, 0.5f, 0.5f, 0.5f);
            view.panel = panelImage.rectTransform;
            UiKit.Outline(UiKit.Panel("Border", view.panel, Palette.Gold), UiKit.RadiusLg, 4).raycastTarget = false;

            float y = 56f;
            var tag = UiKit.Pill(UiKit.Panel("Tag", view.panel, Palette.Gold, 0.5f, 1, 0.5f, 1));
            tag.raycastTarget = false;
            tag.rectTransform.pivot = new Vector2(0.5f, 1);
            tag.rectTransform.sizeDelta = new Vector2(150, 56);
            tag.rectTransform.anchoredPosition = new Vector2(0, -y);
            UiKit.Display(UiKit.Label("Text", tag.transform, "정식판", 34, Palette.OnAccent, 0, 0, 1, 1));
            y += 56f + 24f;

            view.title = UiKit.Display(UiKit.OneLine(UiKit.Label("Title", view.panel, "", 58, Palette.Gold, 0, 1, 1, 1)));
            Place(view.title.rectTransform, ref y, 80f, 24f);

            var iconRow = UiKit.Rect("Icons", view.panel, 0, 1, 1, 1);
            Place(iconRow, ref y, 160f, 24f);
            for (int i = 0; i < 3; i++)
            {
                var icon = UiKit.IconImage($"Icon_{i}", iconRow, null, 0.5f, 0.5f, 0.5f, 0.5f);
                icon.rectTransform.sizeDelta = new Vector2(150, 150);
                view.icons.Add(icon);
            }

            var check = UiFonts.Bold.HasCharacter('✓') ? "✓" : "•";
            for (int i = 0; i < 5; i++) // 지역 단어·몬스터·성유물·앞으로의 지역·특전 (#233)
            {
                var line = UiKit.Label($"Benefit_{i}", view.panel, "", 32, Palette.Text, 0, 1, 1, 1, TextAnchor.MiddleLeft,
                    FontStyle.Normal, true, 20);
                Place(line.rectTransform, ref y, 52f, 8f);
                line.rectTransform.offsetMin = new Vector2(104, line.rectTransform.offsetMin.y);
                var mark = UiKit.Label("Check", line.transform, check, 34, Palette.Gold, 0, 0, 0, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
                mark.rectTransform.pivot = new Vector2(1, 0.5f);
                mark.rectTransform.sizeDelta = new Vector2(44, 0);
                mark.rectTransform.anchoredPosition = new Vector2(-12, 0);
                view.lines.Add(line);
            }
            y += 16f;

            view.note = UiKit.OneLine(UiKit.Label("Note", view.panel, "한 번만 결제하면 계속 즐길 수 있어요", 30, Palette.TextDim, 0, 1, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            Place(view.note.rectTransform, ref y, 44f, 24f);

            view.buy = UiKit.MakeButton("BuyButton", view.panel, "", Palette.Gold, 44, 0, 1, 1, 1);
            view.buyLabel = UiKit.LabelOf(view.buy);
            view.buyLabel.color = Palette.OnAccent;
            Place((RectTransform)view.buy.transform, ref y, 120f, 24f);
            view.buy.onClick.AddListener(view.OnBuy);

            float row = y;
            view.restore = UiKit.MakeButton("RestoreButton", view.panel, "구매 복원", Palette.Neutral, 38, 0, 1, 0.5f, 1);
            Place((RectTransform)view.restore.transform, ref row, 108f, 0f);
            SetHalf((RectTransform)view.restore.transform, 0f, 0.5f);
            view.restore.onClick.AddListener(view.OnRestore);
            view.later = UiKit.MakeButton("LaterButton", view.panel, "나중에", Palette.Neutral, 38, 0.5f, 1, 1, 1);
            Place((RectTransform)view.later.transform, ref y, 108f, 20f);
            SetHalf((RectTransform)view.later.transform, 0.5f, 1f);
            view.later.onClick.AddListener(view.Close);

            view.status = UiKit.Label("Status", view.panel, Footer, 24, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleCenter,
                FontStyle.Normal, true, 18);
            Place(view.status.rectTransform, ref y, 64f, 40f);
            view.panel.sizeDelta = new Vector2(PanelWidth, y);

            view.Root.SetActive(false);
            return view;
        }

        public void Show(IStore purchaseStore, Entitlements owned, PaywallOffer offer, Action unlocked)
        {
            store = purchaseStore;
            entitlements = owned;
            onUnlocked = unlocked;
            title.text = $"{offer.AreaName} 너머의 모험을 이어가요";
            for (int i = 0; i < lines.Count; i++)
            {
                bool used = i < offer.Lines.Count;
                lines[i].gameObject.SetActive(used);
                if (used) lines[i].text = offer.Lines[i];
            }
            int count = Mathf.Min(icons.Count, offer.Icons.Count);
            for (int i = 0; i < icons.Count; i++)
            {
                bool used = i < count;
                icons[i].enabled = used && offer.Icons[i] != null;
                if (!used) continue;
                icons[i].sprite = offer.Icons[i];
                icons[i].rectTransform.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * 190f, 0);
            }
            string price = store?.FullVersionPrice;
            buyLabel.text = $"{(string.IsNullOrEmpty(price) ? "5,000원" : price)}에 정식판 열기";
            status.text = Footer;
            SetBusy(false);
            if (entitlements != null) entitlements.Changed += OnEntitlementsChanged;
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        public void Close()
        {
            if (IsBusy) return;
            if (entitlements != null) entitlements.Changed -= OnEntitlementsChanged;
            Root.SetActive(false);
        }

        private void OnBuy()
        {
            if (IsBusy) return;
            if (store == null || !store.IsReady)
            {
                status.text = "지금은 결제할 수 없어요. 인터넷 연결을 확인하고 잠시 뒤에 다시 해 주세요.";
                return;
            }
            SetBusy(true);
            status.text = "결제 창을 여는 중…";
            store.BuyFullVersion(OnResult);
        }

        private void OnRestore()
        {
            if (IsBusy) return;
            if (store == null)
            {
                status.text = "지금은 복원할 수 없어요. 인터넷 연결을 확인하고 잠시 뒤에 다시 해 주세요.";
                return;
            }
            SetBusy(true);
            status.text = "구매 기록을 확인하는 중…";
            store.Restore(OnResult);
        }

        private void OnResult(StoreResult result)
        {
            SetBusy(false);
            if (!IsOpen) return;
            if (result == StoreResult.Purchased || result == StoreResult.Restored)
            {
                Unlock();
                return;
            }
            status.text = result == StoreResult.Cancelled ? Footer : MessageFor(result);
        }

        // 결제·복원 결과 안내 (정식판 안내 창과 설정의 [구매 복원]이 같이 씀)
        public static string MessageFor(StoreResult result)
        {
            switch (result)
            {
                case StoreResult.Purchased: return "정식판이 열렸어요!";
                case StoreResult.Restored: return "정식판을 되찾았어요!";
                case StoreResult.Cancelled: return "결제를 취소했어요.";
                case StoreResult.Deferred: return "보호자 승인을 기다리고 있어요. 승인되면 자동으로 열려요.";
                case StoreResult.NothingToRestore: return $"복원할 구매 기록이 없어요. 결제한 {StoreLabels.Account()}으로 로그인했는지 확인해 주세요.";
                case StoreResult.Unavailable: return "지금은 결제할 수 없어요. 인터넷 연결을 확인하고 잠시 뒤에 다시 해 주세요.";
                default: return "결제를 완료하지 못했어요. 잠시 뒤에 다시 해 주세요.";
            }
        }

        // 다른 경로(복원·승인·지난번 미확정 결제)로 정식판이 들어와도 바로 열린다
        private void OnEntitlementsChanged()
        {
            if (IsOpen && entitlements.HasFullVersion) Unlock();
        }

        private void Unlock()
        {
            IsBusy = false;
            if (entitlements != null) entitlements.Changed -= OnEntitlementsChanged;
            Root.SetActive(false);
            var done = onUnlocked;
            onUnlocked = null;
            done?.Invoke();
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            buy.interactable = !busy;
            restore.interactable = !busy;
            later.interactable = !busy;
        }

        // 위에서 y만큼 내려와 높이 h (좌우 56 여백)
        private static void Place(RectTransform rect, ref float y, float h, float gap)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(56, -(y + h));
            rect.offsetMax = new Vector2(-56, -y);
            y += h + gap;
        }

        private static void SetHalf(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(from, rect.anchorMin.y);
            rect.anchorMax = new Vector2(to, rect.anchorMax.y);
            rect.offsetMin = new Vector2(from > 0 ? 12 : 56, rect.offsetMin.y);
            rect.offsetMax = new Vector2(to < 1 ? -12 : -56, rect.offsetMax.y);
        }
    }
}
