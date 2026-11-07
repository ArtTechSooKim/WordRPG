using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Items;

namespace WordRPG.UI
{
    // 마을 상점: 골드로 아이템(상처약) 구매 — Figma '상점 — 상처약' 화면 / 'Shop Row'
    public class ShopView
    {
        private const int MaxRows = 6;

        private class Row
        {
            public GameObject Root;
            public Image Icon;
            public Text Info;
            public Button Buy;
            public Text BuyLabel;
        }

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        private readonly List<Row> rows = new List<Row>();
        private Text title, goldText, messageText;
        private ShopData shop;
        private GameSession session;
        private Action onChanged;

        public static ShopView Create(Transform parent)
        {
            var view = new ShopView();
            var root = UiKit.Stretch("ShopView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background;

            view.title = UiKit.Display(UiKit.Label("Title", root, "상점", 56, Palette.Gold, 0, 0.92f, 1, 0.99f));
            view.goldText = UiKit.IconTitle("Gold", root, UiKit.Icon("gold"), "", 44, Palette.Text, 0, 0.87f, 1, 0.915f);

            for (int i = 0; i < MaxRows; i++)
            {
                float top = 0.855f - i * 0.105f;
                var panel = UiKit.RoundPanel($"ShopRow_{i}", root, Palette.Panel, UiKit.RadiusMd, 0.03f, top - 0.095f, 0.97f, top);
                var row = new Row { Root = panel.gameObject };
                row.Icon = UiKit.IconImage("Icon", panel.transform, null, 0.02f, 0.12f, 0.13f, 0.88f);
                row.Info = UiKit.Label("Info", panel.transform, "", 34, Palette.Text, 0.15f, 0.05f, 0.7f, 0.95f,
                    TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
                row.Buy = UiKit.MakeButton($"BuyButton_{i}", panel.transform, "", Palette.Button, 44, 0.72f, 0.16f, 0.97f, 0.84f);
                var colors = row.Buy.colors;
                colors.disabledColor = Color.white; // 잠김 색은 직접 지정
                row.Buy.colors = colors;
                UiKit.IconImage("Gold", row.Buy.transform, UiKit.Icon("gold"), 0.08f, 0.2f, 0.34f, 0.8f);
                row.BuyLabel = UiKit.LabelOf(row.Buy);
                row.BuyLabel.rectTransform.anchorMin = new Vector2(0.3f, 0);
                row.BuyLabel.horizontalOverflow = HorizontalWrapMode.Overflow; // 값은 줄을 바꾸지 않음 (#232)
                int index = i;
                row.Buy.onClick.AddListener(() => view.OnBuy(index));
                view.rows.Add(row);
            }

            var messageBox = UiKit.RoundPanel("MessageBox", root, Palette.PanelLight, UiKit.RadiusMd, 0.03f, 0.115f, 0.97f, 0.215f);
            view.messageText = UiKit.Label("Message", messageBox.transform, "", 34, Palette.Text, 0, 0, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 20);
            UiKit.Pad(view.messageText.rectTransform, 20, 6, 20, 6);

            var close = UiKit.MakeButton("ShopCloseButton", root, "닫기", Palette.Neutral, 44, 0.05f, 0.015f, 0.95f, 0.095f);
            close.onClick.AddListener(view.Hide);

            view.Root.SetActive(false);
            return view;
        }

        public void Show(ShopData shopData, GameSession gameSession, Action changed)
        {
            shop = shopData;
            session = gameSession;
            onChanged = changed;
            title.text = string.IsNullOrEmpty(shop.DisplayName) ? "상점" : shop.DisplayName;
            messageText.text = "어서 오세요! 모험에는 상처약이 꼭 필요하답니다";
            Root.SetActive(true);
            Refresh();
        }

        public void Hide() => Root.SetActive(false);

        private void Refresh()
        {
            goldText.text = $"보유 골드  {session.Inventory.Gold}G";
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool has = i < shop.Entries.Count && shop.Entries[i].Item != null;
                row.Root.SetActive(has);
                if (!has) continue;

                var entry = shop.Entries[i];
                int owned = session.Inventory.GetCount(entry.Item);
                var icon = UiKit.ItemIcon(entry.Item);
                row.Icon.sprite = icon;
                row.Icon.enabled = icon != null;
                row.Info.text = $"<b>{entry.Item.DisplayName}</b>   <color=#A6B3D1>보유 {owned}</color>\n<size=26><color=#A6B3D1>{entry.Item.Description}</color></size>";
                row.BuyLabel.text = $"{entry.Price}G";
                row.BuyLabel.fontSize = PriceFontSize(row.BuyLabel.text);
                bool affordable = session.Inventory.Gold >= entry.Price;
                row.Buy.interactable = affordable;
                UiKit.SetColor(row.Buy, affordable ? Palette.Button : Palette.Disabled);
                row.BuyLabel.color = affordable ? Palette.Text : Palette.TextDim;
            }
        }

        // 값이 길면 글씨를 줄임 (#232 사용자: 2000G가 버튼에 다 안 들어가 줄이 바뀜 — 폰 화면 비율에선 버튼이 조금 좁음).
        // Jua 글꼴 너비 기준: 버튼 안 글자 칸 약 125 (1080 기준)에 들어가는 크기
        public static int PriceFontSize(string price) => price.Length <= 4 ? 44 : price.Length == 5 ? 34 : 30;

        private void OnBuy(int index)
        {
            var entry = shop.Entries[index];
            switch (Shop.TryBuy(session.Inventory, entry))
            {
                case PurchaseResult.Bought:
                    Sound.Play(Sfx.Coin);
                    messageText.text = $"{UiKit.WithJosa(entry.Item.DisplayName, "을", "를")} 샀다! (보유 {session.Inventory.GetCount(entry.Item)}개)";
                    onChanged?.Invoke();
                    break;
                case PurchaseResult.NotEnoughGold:
                    messageText.text = "골드가 부족해요. 전투와 보물상자로 모아 보세요";
                    break;
                default:
                    messageText.text = "지금은 팔지 않는 물건이에요";
                    break;
            }
            Refresh();
        }
    }
}
