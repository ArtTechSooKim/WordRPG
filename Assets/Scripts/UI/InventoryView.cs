using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;

namespace WordRPG.UI
{
    public enum BagTab
    {
        Hero,     // 주인공 능력치·기술 (HeroInfoPage)
        Relics,   // 모은 성유물 · 장착 (RelicPage)
        Items,    // 강화 재료 · 상처약
        Keepsakes, // 징표 진열장
        Companion  // 동료 (#77) — 동료를 얻은 뒤에만 탭이 보임 (주인공 다음 자리)
    }

    // 소지품 (Figma '소지품 — 주인공' / '— 성유물' / '— 재료(아이템)' / '— 징표 진열장'):
    // 주인공 탭 = 끼운 성유물 + 능력치·기술, 성유물 탭 = 모은 성유물 · 장착/빼기, 아이템 탭 = 4칸 격자 + 고른 아이템 상세(쓰는 곳 ·
    // 상처약은 [사용하기]), 징표 탭 = 지역 도감 완성 징표 진열장. 아이템은 세이브에 id만 있어서 GameDatabase로 찾는다
    public class InventoryView
    {
        public const int SlotCount = 8;
        private const int ShelfCount = 3;

        private class Slot
        {
            public GameObject Root;
            public Image Icon;
            public Text Count;
            public Image Border;
        }

        private class Shelf
        {
            public GameObject Root;
            public Image Display;
            public Image Glow;
            public Image Icon;
            public Text Region;
            public Text Name;
            public Image Border;
        }

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;
        public BagTab CurrentTab { get; private set; }
        public bool ShowingKeepsakes => CurrentTab == BagTab.Keepsakes;
        public OwnedRelic SelectedRelic => relicPage.Selected;

        private Text goldText;
        private Button heroTab, relicsTab, itemsTab, keepsakesTab, companionTab;
        private Button[] tabButtons;
        private CompanionPage companionPage;
        private GameObject materialsPage, keepsakesPage, detailBox;
        private HeroInfoPage heroPage;
        private RelicPage relicPage;
        private SkillLearnView learnView; // 기술문서 [배우기] · 성유물 [기술 넣기]
        private Button useButton;
        private Action onChanged;
        private int selectedMaterial;
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Shelf> shelves = new List<Shelf>();
        private Image detailIcon, usageIcon;
        private Text detailName, detailMeta, detailDesc, usageLine1, usageLine2;
        private GameObject usageBox;

        private GameSession session;
        private GameDatabase database;
        private readonly List<ItemData> materials = new List<ItemData>();
        private List<KeepsakeEntry> keepsakes = new List<KeepsakeEntry>();
        private int keepsakePage;                    // 진열장 쪽 (3칸씩, #97 — 지역이 3개를 넘음)
        private GameObject keepsakePager;
        private Button keepsakePrev, keepsakeNext;
        private Text keepsakePageLabel;
        public int KeepsakePageCount => Mathf.Max(1, (keepsakes.Count + ShelfCount - 1) / ShelfCount);

        public static InventoryView Create(Transform parent)
        {
            var view = new InventoryView();
            var root = UiKit.Stretch("InventoryView", parent);
            view.Root = root.gameObject;
            view.Root.AddComponent<Image>().color = Palette.Background;

            UiKit.Display(UiKit.Label("Title", root, "소지품", 56, Palette.Gold, 0, 0.93f, 1, 0.99f));
            view.goldText = UiKit.IconTitle("Gold", root, UiKit.Icon("gold"), "", 44, Palette.Text, 0, 0.885f, 1, 0.925f);

            var tabs = UiKit.RoundPanel("Tabs", root, Palette.Track, UiKit.RadiusMd, 0.022f, 0.815f, 0.978f, 0.872f);
            var names = new[] { ("TabHero", "주인공", BagTab.Hero), ("TabCompanion", "동료", BagTab.Companion), ("TabRelics", "성유물", BagTab.Relics),
                ("TabItems", "아이템", BagTab.Items), ("TabKeepsakes", "징표", BagTab.Keepsakes) };
            var buttons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                buttons[i] = UiKit.MakeButton(names[i].Item1, tabs.transform, names[i].Item2, Color.clear, 44, 0, 0.1f, 0, 0.9f);
                var tab = names[i].Item3;
                buttons[i].onClick.AddListener(() => view.SelectTab(tab));
            }
            view.tabButtons = buttons;
            view.heroTab = buttons[0];
            view.companionTab = buttons[1];
            view.relicsTab = buttons[2];
            view.itemsTab = buttons[3];
            view.keepsakesTab = buttons[4];
            view.LayoutTabs(false);

            view.heroPage = HeroInfoPage.Create(root, 0.022f, 0.105f, 0.978f, 0.8f, slot => view.SelectTab(BagTab.Relics, view.session.Hero.SlotAt(slot)));
            view.relicPage = RelicPage.Create(root, 0.022f, 0.105f, 0.978f, 0.8f);
            view.companionPage = CompanionPage.Create(root, 0.022f, 0.105f, 0.978f, 0.8f);
            view.BuildMaterials(root);
            view.BuildKeepsakes(root);
            view.BuildDetail(root);

            var close = UiKit.MakeButton("InventoryCloseButton", root, "닫기", Palette.Neutral, 44, 0.05f, 0.015f, 0.95f, 0.095f);
            close.onClick.AddListener(view.Hide);

            view.learnView = SkillLearnView.Create(root); // 가방 위에 덮는다
            view.relicPage.PlaceSkill = relic => view.learnView.ShowRelicSkill(view.session, relic, learned =>
            {
                if (learned) view.onChanged?.Invoke();
                view.relicPage.Refresh();
            });

            view.Root.SetActive(false);
            return view;
        }

        // 4칸 × 2줄 (Figma 'Item Slot' 240×240)
        private void BuildMaterials(RectTransform root)
        {
            var page = UiKit.Rect("MaterialsPage", root, 0.022f, 0.53f, 0.978f, 0.8f);
            materialsPage = page.gameObject;
            const float gapX = 24f / 1032f, gapY = 24f / 518f;
            float w = (1f - 3 * gapX) / 4f, h = (1f - gapY) / 2f;
            for (int i = 0; i < SlotCount; i++)
            {
                int col = i % 4, row = i / 4;
                float x = col * (w + gapX), top = 1f - row * (h + gapY);
                var panel = UiKit.RoundPanel($"ItemSlot_{i}", page, Palette.Panel, UiKit.RadiusMd, x, top - h, x + w, top);
                var button = UiKit.AddButton(panel);
                var colors = button.colors;
                colors.disabledColor = Color.white; // 빈 칸 색은 직접 지정 (반투명 판)
                button.colors = colors;
                int index = i;
                button.onClick.AddListener(() => ShowMaterial(index));
                var slot = new Slot { Root = panel.gameObject };
                slot.Icon = UiKit.IconImage("Icon", panel.transform, null, 0.18f, 0.32f, 0.82f, 0.92f);
                slot.Count = UiKit.Display(UiKit.Label("Count", panel.transform, "", 44, Palette.Text, 0, 0.04f, 1, 0.32f));
                slot.Border = UiKit.Outline(UiKit.Panel("Border", panel.transform, Palette.Gold), UiKit.RadiusMd, 6);
                slot.Border.raycastTarget = false;
                slots.Add(slot);
            }
        }

        // 진열장 선반 3칸 + 나무 선반 (Figma 'Keepsake Slot')
        private void BuildKeepsakes(RectTransform root)
        {
            var page = UiKit.Rect("KeepsakesPage", root, 0.022f, 0.53f, 0.978f, 0.8f);
            keepsakesPage = page.gameObject;
            UiKit.Label("Intro", page, "지역 도감을 모두 채우면 징표를 받아요", 30, Palette.TextDim, 0.01f, 0.9f, 0.72f, 1, TextAnchor.MiddleLeft);
            BuildKeepsakePager(page);
            UiKit.Pill(UiKit.Panel("Plank", page, new Color(0.42f, 0.31f, 0.22f), 0, 0.02f, 1, 0.06f)).raycastTarget = false;
            const float gap = 36f / 1032f;
            float w = (1f - 2 * gap) / ShelfCount;
            for (int i = 0; i < ShelfCount; i++)
            {
                float x = i * (w + gap);
                var panel = UiKit.RoundPanel($"KeepsakeSlot_{i}", page, Palette.Panel, UiKit.RadiusLg, x, 0.07f, x + w, 0.88f);
                var button = UiKit.AddButton(panel);
                int index = i;
                button.onClick.AddListener(() => ShowKeepsake(index));
                var shelf = new Shelf { Root = panel.gameObject };
                shelf.Glow = UiKit.IconImage("Glow", panel.transform, UiKit.GlowSprite(), 0.5f, 0.66f, 0.5f, 0.66f);
                shelf.Glow.rectTransform.sizeDelta = new Vector2(280, 280);
                shelf.Glow.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.45f);
                shelf.Display = UiKit.Pill(UiKit.Panel("Display", panel.transform, Palette.PanelLight, 0.5f, 0.66f, 0.5f, 0.66f));
                shelf.Display.raycastTarget = false;
                shelf.Display.rectTransform.sizeDelta = new Vector2(190, 190);
                shelf.Icon = UiKit.IconImage("Icon", shelf.Display.transform, null, 0.14f, 0.14f, 0.86f, 0.86f);
                shelf.Region = UiKit.Label("Region", panel.transform, "", 28, Palette.Gold, 0.04f, 0.2f, 0.96f, 0.33f);
                shelf.Name = UiKit.Label("Name", panel.transform, "", 32, Palette.Text, 0.04f, 0.04f, 0.96f, 0.2f,
                    TextAnchor.MiddleCenter, FontStyle.Bold, true, 20);
                shelf.Border = UiKit.Outline(UiKit.Panel("Border", panel.transform, Palette.Gold), UiKit.RadiusLg, 4);
                shelf.Border.raycastTarget = false;
                shelves.Add(shelf);
            }
        }

        // 진열장 쪽 넘기기: 안내 줄 오른쪽 ◀ 1 / 2 ▶ (한 쪽뿐이면 숨김). 끝 쪽의 화살표는 흐리게·누를 수 없음
        private void BuildKeepsakePager(RectTransform page)
        {
            var pager = UiKit.Rect("KeepsakePager", page, 1, 0.885f, 1, 1.015f);
            pager.pivot = new Vector2(1, 0.5f);
            pager.sizeDelta = new Vector2(250, 0);
            keepsakePager = pager.gameObject;
            Button Arrow(string name, string glyph, float side)
            {
                var image = UiKit.Pill(UiKit.Panel(name, pager, Palette.Button, side, 0.5f, side, 0.5f));
                image.rectTransform.pivot = new Vector2(side, 0.5f);
                image.rectTransform.sizeDelta = new Vector2(60, 60);
                var label = UiKit.Label("Glyph", image.transform, glyph, 26, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.font = UiFonts.Bold; // ◀ ▶ 는 Jua에 없을 수 있음
                return UiKit.AddButton(image);
            }
            keepsakePrev = Arrow("KeepsakePrev", "◀", 0);
            keepsakeNext = Arrow("KeepsakeNext", "▶", 1);
            keepsakePageLabel = UiKit.Label("KeepsakePage", pager, "1 / 1", 34, Palette.Gold, 0.25f, 0, 0.75f, 1, TextAnchor.MiddleCenter);
            keepsakePageLabel.font = UiFonts.Display;
            keepsakePrev.onClick.AddListener(() => TurnKeepsakePage(-1));
            keepsakeNext.onClick.AddListener(() => TurnKeepsakePage(1));
        }

        private void TurnKeepsakePage(int delta)
        {
            int next = Mathf.Clamp(keepsakePage + delta, 0, KeepsakePageCount - 1);
            if (next == keepsakePage) return;
            keepsakePage = next;
            ShowKeepsake(0);
        }

        // 고른 아이템 상세: 그림 · 이름 · 보유 수 · 설명 · 쓰는 곳
        private void BuildDetail(RectTransform root)
        {
            var box = UiKit.RoundPanel("DetailBox", root, Palette.PanelLight, UiKit.RadiusLg, 0.022f, 0.28f, 0.978f, 0.515f);
            detailBox = box.gameObject;
            detailIcon = UiKit.IconImage("Icon", box.transform, null, 0.03f, 0.6f, 0.155f, 0.92f);
            detailName = UiKit.Display(UiKit.Label("Name", box.transform, "", 44, Palette.Text, 0.18f, 0.76f, 0.97f, 0.93f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 24));
            detailMeta = UiKit.Label("Meta", box.transform, "", 30, Palette.TextDim, 0.18f, 0.6f, 0.97f, 0.76f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
            detailDesc = UiKit.Label("Description", box.transform, "", 34, Palette.Text, 0.031f, 0.37f, 0.97f, 0.59f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 20);
            var usage = UiKit.RoundPanel("Usage", box.transform, Palette.Panel, UiKit.RadiusMd, 0.031f, 0.06f, 0.969f, 0.34f);
            usageBox = usage.gameObject;
            usageIcon = UiKit.IconImage("Icon", usage.transform, null, 0.02f, 0.2f, 0.085f, 0.8f);
            usageLine1 = UiKit.Label("Line1", usage.transform, "", 30, Palette.Text, 0.11f, 0.5f, 0.98f, 0.92f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
            usageLine2 = UiKit.Label("Line2", usage.transform, "", 30, Palette.TextDim, 0.11f, 0.08f, 0.98f, 0.5f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
            // 상처약: 필드에서 바로 쓰기 (쓰는 곳 상자 오른쪽)
            useButton = UiKit.MakeButton("UseItemButton", box.transform, "사용하기", Palette.Heal, 40, 0.7f, 0.06f, 0.969f, 0.34f);
            var colors = useButton.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            useButton.colors = colors;
            useButton.onClick.AddListener(UseSelectedItem);
        }

        // tab: 처음 보여줄 탭, relic: 성유물 탭에서 고를 성유물, changed: 장착·아이템 사용 뒤 (저장·HUD 갱신)
        public void Show(GameSession gameSession, GameDatabase gameDatabase, BagTab tab = BagTab.Hero,
            OwnedRelic relic = null, Action changed = null)
        {
            session = gameSession;
            database = gameDatabase;
            onChanged = changed;
            CollectItems();
            keepsakes = Keepsakes.Collect(database, session);

            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
            bool hasCompanion = session.Companions.Count > 0;
            LayoutTabs(hasCompanion);
            SelectTab(tab == BagTab.Companion && !hasCompanion ? BagTab.Hero : tab, relic);
        }

        // 탭 4개(주인공·성유물·아이템·징표), 동료가 있으면 '동료'를 주인공 다음에 넣어 5개 (#77)
        private void LayoutTabs(bool withCompanion)
        {
            companionTab.gameObject.SetActive(withCompanion);
            int count = withCompanion ? 5 : 4, slot = 0;
            float width = (0.988f - 0.006f * (count - 1)) / count;
            foreach (var button in tabButtons)
            {
                if (!button.gameObject.activeSelf) continue;
                var rt = (RectTransform)button.transform;
                float x0 = 0.006f + slot * (width + 0.006f);
                rt.anchorMin = new Vector2(x0, 0.1f);
                rt.anchorMax = new Vector2(x0 + width, 0.9f);
                slot++;
            }
        }

        public CompanionPage CompanionPage => companionPage;

        private void CollectItems()
        {
            goldText.text = $"보유 골드  {session.Inventory.Gold}G";
            materials.Clear();
            foreach (var stack in session.Inventory.Stacks)
            {
                var item = database != null ? database.FindItem(stack.ItemId) : null;
                if (item != null && item.Kind != ItemKind.Keepsake) materials.Add(item);
            }
        }

        public void Hide() => Root.SetActive(false);

        // 뒤로가기 (#91): 성유물 바꿀 칸을 고르는 중이면 그것만 취소하고 true
        public bool CancelRelicChoosing() => relicPage.Root.activeSelf && relicPage.CancelChoosing();

        public SkillLearnView LearnView => learnView;

        private void SelectTab(BagTab tab) => SelectTab(tab, null);

        private void SelectTab(BagTab tab, OwnedRelic relic)
        {
            CurrentTab = tab;
            materialsPage.SetActive(tab == BagTab.Items);
            keepsakesPage.SetActive(tab == BagTab.Keepsakes);
            detailBox.SetActive(tab == BagTab.Items || tab == BagTab.Keepsakes);
            StyleTab(heroTab, tab == BagTab.Hero);
            StyleTab(relicsTab, tab == BagTab.Relics);
            StyleTab(itemsTab, tab == BagTab.Items);
            StyleTab(keepsakesTab, tab == BagTab.Keepsakes);
            StyleTab(companionTab, tab == BagTab.Companion);
            if (tab == BagTab.Hero) heroPage.Show(session.Hero);
            else heroPage.Hide();
            if (tab == BagTab.Companion) companionPage.Show(session, onChanged);
            else companionPage.Hide();
            if (tab == BagTab.Relics) relicPage.Show(session, database, relic, onChanged);
            else relicPage.Hide();
            if (tab == BagTab.Items) ShowMaterial(0);
            if (tab == BagTab.Keepsakes)
            {
                keepsakePage = 0;
                ShowKeepsake(0);
            }
        }

        private static void StyleTab(Button tab, bool selected)
        {
            UiKit.SetColor(tab, selected ? Palette.Button : Color.clear);
            UiKit.LabelOf(tab).color = selected ? Palette.Text : Palette.TextDim;
        }

        // ------------------------------------------------------------------ 재료

        private void ShowMaterial(int selected)
        {
            selectedMaterial = selected;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool has = i < materials.Count;
                slot.Root.GetComponent<Image>().color = has ? (i == selected ? Palette.PanelLight : Palette.Panel)
                    : new Color(Palette.Panel.r, Palette.Panel.g, Palette.Panel.b, 0.5f);
                slot.Root.GetComponent<Button>().interactable = has;
                slot.Border.enabled = has && i == selected;
                slot.Icon.sprite = has ? UiKit.ItemIcon(materials[i]) : null;
                slot.Icon.enabled = slot.Icon.sprite != null;
                slot.Count.text = has ? $"× {session.Inventory.GetCount(materials[i])}" : "";
            }

            useButton.gameObject.SetActive(false);
            if (materials.Count == 0)
            {
                SetDetail(UiKit.Icon("bag"), "아직 아이템이 없어요", "", "전투 · 보물상자에서 강화 재료를, 상점에서 상처약을 얻어요");
                usageBox.SetActive(false);
                return;
            }

            var item = materials[Mathf.Clamp(selected, 0, materials.Count - 1)];
            SetDetail(UiKit.ItemIcon(item), item.DisplayName, $"보유 {session.Inventory.GetCount(item)}개", item.Description);
            if (item.IsHealingItem) ShowPotion(item);
            else if (item.IsSkillDocument) ShowDocument(item);
            else ShowUsage(item);
        }

        // 기술문서: 배우는 기술 + [배우기] (이미 배웠으면 잠금). 배워도 문서는 남는다
        private void ShowDocument(ItemData item)
        {
            var hero = session.Hero;
            var skill = item.TaughtSkill;
            bool known = hero.Knows(item);
            string line2 = known ? "이미 배운 기술이에요 — 전투 기술 칸에 있어요"
                : hero.HasSkillRoom ? $"기술 칸 {hero.SkillSlots.Count} / {Hero.SkillSlotCount} — 바로 배울 수 있어요"
                : "기술 칸이 가득 — 배우면 지금 기술 하나와 바꿔요";
            SetUsage(UiKit.Icon(BattleScreen.SkillIconName(skill)), $"배우는 기술  {skill.DisplayName} · {BattleScreen.SkillEffect(skill)}",
                line2, known ? Palette.Good : Palette.TextDim);
            UiKit.LabelOf(useButton).text = known ? "배움" : "배우기";
            UiKit.SetColor(useButton, Palette.Info);
            useButton.gameObject.SetActive(true);
            useButton.interactable = !known;
            ((RectTransform)usageBox.transform).anchorMax = new Vector2(0.68f, 0.34f);
        }

        // 상처약: 쓰는 곳 + [사용하기] (HP가 가득이면 잠금)
        private void ShowPotion(ItemData item)
        {
            var hero = session.Hero;
            bool full = hero.CurrentHp >= hero.Stats.MaxHp;
            SetUsage(UiKit.Icon("skill_heal"), $"쓰는 곳  필드·전투 — HP {item.HealAmount} 회복",
                full ? "HP가 가득해서 지금은 쓸 필요가 없어요" : $"지금 HP {hero.CurrentHp} / {hero.Stats.MaxHp}", Palette.TextDim);
            UiKit.LabelOf(useButton).text = "사용하기";
            UiKit.SetColor(useButton, Palette.Heal);
            useButton.gameObject.SetActive(true);
            useButton.interactable = !full && session.Inventory.GetCount(item) > 0;
            ((RectTransform)usageBox.transform).anchorMax = new Vector2(0.68f, 0.34f);
        }

        private void UseSelectedItem()
        {
            if (materials.Count == 0) return;
            var item = materials[Mathf.Clamp(selectedMaterial, 0, materials.Count - 1)];
            if (item.IsSkillDocument)
            {
                int index = selectedMaterial;
                learnView.ShowDocument(session, item, learned =>
                {
                    if (learned) onChanged?.Invoke();
                    ShowMaterial(index);
                    if (learned)
                    {
                        detailMeta.text = $"새 기술 '{item.TaughtSkill.DisplayName}'을(를) 배웠어요!";
                        detailMeta.color = Palette.Good;
                    }
                });
                return;
            }
            int healed = session.UseHealingItem(item);
            if (healed <= 0) return;
            Sound.Play(Sfx.Heal);
            onChanged?.Invoke();
            CollectItems();
            ShowMaterial(Mathf.Min(selectedMaterial, Mathf.Max(0, materials.Count - 1)));
            detailMeta.text = $"HP +{healed} 회복했어요   (남은 {session.Inventory.GetCount(item)}개)";
            detailMeta.color = Palette.Good;
        }

        // 강화 재료: 이 재료로 강화하는 모은 성유물과 다음 단계에 필요한 개수
        private void ShowUsage(ItemData item)
        {
            OwnedRelic user = null;
            foreach (var relic in session.Hero.Relics)
            {
                if (relic.Data.UpgradeItem == item && !relic.IsMaxLevel) { user = relic; break; }
            }
            if (user == null)
            {
                SetUsage(UiKit.Icon("star_full"), "쓰는 곳  성유물 제단", "지금 모은 성유물 중에는 이 재료로 강화하는 것이 없어요", Palette.TextDim);
                return;
            }

            var cost = user.Data.CostFrom(user.Level);
            int owned = session.Inventory.GetCount(item);
            string where = $"쓰는 곳  성유물 제단 · {user.Data.DisplayName} +{user.Level} → +{user.Level + 1}";
            var status = RelicUpgrade.Check(user, session.Inventory);
            string line2 = status == UpgradeStatus.Ready ? $"재료 {owned} / {cost.ItemCount} ✓   {cost.Gold}G ✓   지금 강화할 수 있어요!"
                : status == UpgradeStatus.NotEnoughGold ? $"재료 {owned} / {cost.ItemCount} ✓   골드 {cost.Gold}G가 필요해요"
                : $"재료 {owned} / {cost.ItemCount} — {cost.ItemCount - owned}개 더 모으면 돼요";
            SetUsage(UiKit.RelicIcon(user.Data), where, line2, status == UpgradeStatus.Ready ? Palette.Good : Palette.TextDim);
        }

        // 쓰는 곳 상자: 두 줄, 둘째 줄이 없으면 첫 줄을 가운데로
        private void SetUsage(Sprite icon, string line1, string line2, Color line2Color)
        {
            usageBox.SetActive(true);
            ((RectTransform)usageBox.transform).anchorMax = new Vector2(0.969f, 0.34f);
            usageIcon.sprite = icon;
            usageIcon.enabled = icon != null;
            usageLine1.text = line1;
            usageLine2.text = line2 ?? "";
            usageLine2.color = line2Color;
            usageLine1.rectTransform.anchorMin = new Vector2(0.11f, string.IsNullOrEmpty(line2) ? 0.08f : 0.5f);
        }

        internal static string RoleIconName(MonsterRole role)
        {
            switch (role)
            {
                case MonsterRole.Attacker: return "role_attacker";
                case MonsterRole.Defender: return "role_defender";
                default: return "role_supporter";
            }
        }

        // ------------------------------------------------------------------ 징표 진열장

        // slot = 이 쪽의 몇 번째 칸 (진열장 전체에서는 keepsakePage * 3 + slot)
        private void ShowKeepsake(int slot)
        {
            keepsakePage = Mathf.Clamp(keepsakePage, 0, KeepsakePageCount - 1);
            int first = keepsakePage * ShelfCount;
            int selected = slot;
            int pages = KeepsakePageCount;
            keepsakePager.SetActive(pages > 1);
            keepsakePageLabel.text = $"{keepsakePage + 1} / {pages}";
            keepsakePrev.interactable = keepsakePage > 0;
            keepsakeNext.interactable = keepsakePage < pages - 1;
            UiKit.SetColor(keepsakePrev, keepsakePrev.interactable ? Palette.Button : Palette.Track);
            UiKit.SetColor(keepsakeNext, keepsakeNext.interactable ? Palette.Button : Palette.Track);
            for (int i = 0; i < shelves.Count; i++)
            {
                var shelf = shelves[i];
                var entry = first + i < keepsakes.Count ? keepsakes[first + i] : null;
                bool owned = entry != null && entry.Owned;
                shelf.Root.GetComponent<Image>().color = i == selected ? Palette.PanelLight : Palette.Panel;
                shelf.Border.enabled = owned || i == selected;
                shelf.Border.color = owned ? new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, i == selected ? 1f : 0.6f) : Palette.TextDim;
                shelf.Glow.enabled = owned;
                shelf.Display.color = owned ? Palette.PanelLight : Palette.Track;
                shelf.Icon.sprite = owned ? UiKit.ItemIcon(entry.Keepsake) : UiKit.Icon("lock");
                shelf.Icon.enabled = shelf.Icon.sprite != null;
                shelf.Icon.color = owned ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                shelf.Region.text = entry != null ? entry.RegionName : "다음 지역";
                shelf.Region.color = owned ? Palette.Gold : Palette.TextDim;
                shelf.Name.text = owned ? entry.Keepsake.DisplayName : "???";
                shelf.Name.color = owned ? Palette.Text : Palette.TextDim;
            }

            usageBox.SetActive(false);
            useButton.gameObject.SetActive(false);
            var chosen = first + selected < keepsakes.Count ? keepsakes[first + selected] : null;
            if (chosen == null)
            {
                SetDetail(UiKit.Icon("lock"), "???", "다음 지역", "새 지역이 열리면 그 지역의 징표 자리가 생겨요");
                return;
            }

            var progress = chosen.Progress;
            if (!chosen.Owned)
            {
                SetDetail(UiKit.Icon("lock"), "???", $"{chosen.RegionName} 도감 {progress.Discovered} / {progress.Total}",
                    $"{chosen.RegionName} 단어 도감을 모두 채우면 받을 수 있어요");
                return;
            }

            SetDetail(UiKit.ItemIcon(chosen.Keepsake), chosen.Keepsake.DisplayName,
                $"{chosen.RegionName} 도감 {progress.Total} / {progress.Total} 완성", chosen.Keepsake.Description);
            detailMeta.color = Palette.Gold;
            if (chosen.Region.CompletionGold > 0)
                SetUsage(UiKit.Icon("gold"), $"완성 보상으로 {chosen.Region.CompletionGold} 골드도 함께 받았어요", null, Palette.TextDim);
        }

        private void SetDetail(Sprite icon, string name, string meta, string description)
        {
            detailIcon.sprite = icon;
            detailIcon.enabled = icon != null;
            detailName.text = name;
            detailMeta.text = meta;
            detailMeta.color = Palette.TextDim;
            detailDesc.text = description;
        }
    }
}
