using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;

namespace WordRPG.UI
{
    // 끼운 성유물 3칸 줄 (Figma 'Relic Slot') — 소지품의 주인공·성유물 탭 맨 위
    public class RelicSlotsRow
    {
        private class Cell
        {
            public Image Back, Icon, LevelBack, Border;
            public Text Level, Empty;
        }

        private readonly List<Cell> cells = new List<Cell>();
        private Text subtitle;

        public static RelicSlotsRow Create(RectTransform parent, float minX, float minY, float maxX, float maxY, Action<int> onClick)
        {
            var row = new RelicSlotsRow();
            var panel = UiKit.RoundPanel("EquippedRelics", parent, Palette.Panel, UiKit.RadiusLg, minX, minY, maxX, maxY);
            panel.raycastTarget = false;
            UiKit.Display(UiKit.Label("Title", panel.transform, "장착한 성유물", 40, Palette.Gold, 0.03f, 0.5f, 0.6f, 0.9f, TextAnchor.MiddleLeft));
            row.subtitle = UiKit.OneLine(UiKit.Label("Sub", panel.transform, "", 24, Palette.TextDim, 0.03f, 0.12f, 0.6f, 0.5f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 16));
            for (int i = 0; i < HeroData.SlotCount; i++)
            {
                var back = UiKit.RoundPanel($"EquipSlot_{i}", panel.transform, Palette.PanelLight, UiKit.RadiusMd, 1, 0.5f, 1, 0.5f);
                back.rectTransform.pivot = new Vector2(1, 0.5f);
                back.rectTransform.sizeDelta = new Vector2(116, 116);
                back.rectTransform.anchoredPosition = new Vector2(-24 - (HeroData.SlotCount - 1 - i) * 132, 0);
                int slot = i;
                UiKit.AddButton(back).onClick.AddListener(() => onClick?.Invoke(slot));
                var cell = new Cell { Back = back };
                cell.Icon = UiKit.IconImage("Icon", back.transform, null, 0.12f, 0.12f, 0.88f, 0.88f);
                cell.LevelBack = UiKit.Pill(UiKit.Panel("Level", back.transform, Palette.Gold, 1, 0, 1, 0));
                cell.LevelBack.raycastTarget = false;
                cell.LevelBack.rectTransform.pivot = new Vector2(1, 0);
                cell.LevelBack.rectTransform.sizeDelta = new Vector2(52, 30);
                cell.LevelBack.rectTransform.anchoredPosition = new Vector2(-4, 4);
                cell.Level = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", cell.LevelBack.transform, "", 24, Palette.OnAccent, 0, 0, 1, 1)));
                cell.Empty = UiKit.Label("Empty", back.transform, "빈 칸", 22, Palette.TextDim, 0, 0, 1, 1);
                cell.Border = UiKit.Outline(UiKit.Panel("Border", back.transform, Palette.Gold), UiKit.RadiusMd, 4);
                cell.Border.raycastTarget = false;
                row.cells.Add(cell);
            }
            return row;
        }

        // selected: 금테를 두를 칸 (-1 = 없음). choosing: 바꿀 칸을 고르는 중 (#91 — 모든 칸에 금테 + 안내)
        public void Show(Hero hero, int selected = -1, bool choosing = false, string note = null)
        {
            subtitle.text = note ?? (choosing ? "바꿀 칸을 고르세요 — 누른 칸의 성유물과 바꿔요"
                : $"{hero.EquippedCount} / {hero.SlotCount}칸 · 칸을 누르면 성유물 탭에서 보기");
            subtitle.color = choosing ? Palette.Gold : Palette.TextDim;
            for (int i = 0; i < cells.Count; i++)
            {
                var relic = hero.SlotAt(i);
                var cell = cells[i];
                cell.Back.color = relic != null ? Palette.PanelLight : Palette.Track;
                cell.Icon.sprite = relic != null ? UiKit.RelicIcon(relic.Data) : null;
                cell.Icon.enabled = cell.Icon.sprite != null;
                cell.LevelBack.gameObject.SetActive(relic != null);
                cell.Level.text = relic != null ? $"+{relic.Level}" : "";
                cell.Empty.enabled = relic == null;
                cell.Border.enabled = choosing || (i == selected && relic != null);
            }
        }
    }

    // 기술 한 줄 (Figma 'Skill Info Row' + 'Quiz Tag'): 종류 색 아이콘 · 이름 + 문제 유형 · 효과 · 설명
    public class SkillRowView
    {
        public GameObject Root { get; private set; }
        private Image iconBack, icon, quizBack;
        private Text name, quiz, effect, description;

        public static SkillRowView Create(Transform parent, string objectName)
        {
            var view = new SkillRowView();
            var panel = UiKit.RoundPanel(objectName, parent, Palette.Panel, UiKit.RadiusMd);
            panel.raycastTarget = false;
            view.Root = panel.gameObject;
            view.iconBack = UiKit.RoundPanel("IconBack", panel.transform, Palette.Attack, UiKit.RadiusMd, 0f, 0.5f, 0f, 0.5f);
            view.iconBack.raycastTarget = false;
            view.iconBack.rectTransform.pivot = new Vector2(0f, 0.5f);
            view.iconBack.rectTransform.sizeDelta = new Vector2(84, 84);
            view.iconBack.rectTransform.anchoredPosition = new Vector2(20, 0);
            view.icon = UiKit.IconImage("Icon", view.iconBack.transform, null, 0.17f, 0.17f, 0.83f, 0.83f);

            // 이름 + 문제 유형 태그를 한 줄로 (이름 길이에 따라 태그가 붙어 따라온다)
            var head = UiKit.Rect("Head", panel.transform, 0, 0.6f, 1, 0.94f);
            head.offsetMin = new Vector2(124, 0);
            head.offsetMax = new Vector2(-20, 0);
            var layout = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            view.name = UiKit.Display(UiKit.OneLine(UiKit.Label("Name", head, "", 38, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleLeft)));
            view.name.horizontalOverflow = HorizontalWrapMode.Overflow;
            view.quizBack = UiKit.Pill(UiKit.Panel("QuizTag", head, Palette.Guard));
            view.quizBack.raycastTarget = false;
            var tagLayout = view.quizBack.gameObject.AddComponent<HorizontalLayoutGroup>();
            tagLayout.padding = new RectOffset(16, 16, 4, 4);
            tagLayout.childControlWidth = tagLayout.childControlHeight = true;
            tagLayout.childForceExpandWidth = tagLayout.childForceExpandHeight = false;
            view.quiz = UiKit.OneLine(UiKit.Label("Text", view.quizBack.transform, "", 24, Palette.Text, 0, 0, 1, 1));
            view.quiz.horizontalOverflow = HorizontalWrapMode.Overflow;

            view.effect = UiKit.Label("Effect", panel.transform, "", 27, Palette.Gold, 0, 0.32f, 1, 0.6f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 16);
            view.description = UiKit.Label("Description", panel.transform, "", 27, Palette.TextDim, 0, 0.05f, 1, 0.32f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 16);
            foreach (var label in new[] { view.effect, view.description })
            {
                label.rectTransform.offsetMin = new Vector2(124, 0);
                label.rectTransform.offsetMax = new Vector2(-20, 0);
            }
            return view;
        }

        // source: "깃펜 +2 · " 같은 앞말, note: 설명 대신 보여줄 글 (null이면 기술 설명). compact: 설명 줄 없이 두 줄로
        public void Show(SkillData skill, string source, string note = null, bool compact = false)
        {
            iconBack.color = BattleScreen.SkillColor(skill);
            var sprite = UiKit.Icon(BattleScreen.SkillIconName(skill));
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            name.text = skill.DisplayName;
            quiz.text = BattleScreen.QuizText(skill);
            quizBack.color = BattleScreen.IsHardQuiz(skill) ? Palette.Evolve : Palette.Guard;
            effect.text = source + BattleScreen.SkillEffect(skill);
            description.text = note ?? skill.Description;
            description.gameObject.SetActive(!compact);
            var head = (RectTransform)name.transform.parent;
            head.anchorMin = new Vector2(0, compact ? 0.5f : 0.6f);
            effect.rectTransform.anchorMin = new Vector2(0, compact ? 0.06f : 0.32f);
            effect.rectTransform.anchorMax = new Vector2(1, compact ? 0.5f : 0.6f);
        }
    }

    // 소지품 > 주인공 탭 (Figma '소지품 — 주인공'): 끼운 성유물 3칸 + 주인공 그림·레벨·경험치·능력치(성유물 보너스 포함)·기술·성유물 효과
    public class HeroInfoPage
    {
        private const int MaxSkillRows = 1 + HeroData.SlotCount;

        public GameObject Root { get; private set; }

        private RelicSlotsRow slotsRow;
        private Image art, expFill;
        private Text nameText, levelText, nextText, description, effectLine1, effectLine2;
        private readonly Text[] statValues = new Text[3];
        private readonly List<SkillRowView> skillRows = new List<SkillRowView>();
        private Action<int> onSlotClicked;

        public static HeroInfoPage Create(RectTransform parent, float minX, float minY, float maxX, float maxY, Action<int> slotClicked)
        {
            var page = new HeroInfoPage { onSlotClicked = slotClicked };
            var root = UiKit.Rect("HeroPage", parent, minX, minY, maxX, maxY);
            page.Root = root.gameObject;
            page.slotsRow = RelicSlotsRow.Create(root, 0, 0.885f, 1, 1, slot => page.onSlotClicked?.Invoke(slot));
            page.BuildDetail(UiKit.RoundPanel("HeroDetail", root, Palette.PanelLight, UiKit.RadiusLg, 0, 0, 1, 0.87f));
            return page;
        }

        private void BuildDetail(Image panel)
        {
            panel.raycastTarget = false;
            var box = panel.rectTransform;

            art = UiKit.RoundPanel("Art", box, Palette.Panel, UiKit.RadiusLg, 0, 1, 0, 1);
            art.raycastTarget = false;
            art.rectTransform.pivot = new Vector2(0, 1);
            art.rectTransform.sizeDelta = new Vector2(176, 176);
            art.rectTransform.anchoredPosition = new Vector2(28, -24);
            UiKit.IconImage("Face", art.transform, null, 0.08f, 0.08f, 0.92f, 0.92f);

            nameText = UiKit.Display(UiKit.Label("Name", box, "", 56, Palette.Text, 0.23f, 0.9f, 0.97f, 0.975f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 30));
            levelText = UiKit.OneLine(UiKit.Label("Level", box, "", 30, Palette.TextDim, 0.23f, 0.862f, 0.97f, 0.9f, TextAnchor.MiddleLeft));
            UiKit.OneLine(UiKit.Label("ExpLabel", box, "경험치", 24, Palette.TextDim, 0.23f, 0.832f, 0.33f, 0.862f, TextAnchor.MiddleLeft));
            var track = UiKit.Pill(UiKit.Panel("ExpBack", box, Palette.Track, 0.34f, 0.84f, 0.97f, 0.854f));
            track.raycastTarget = false;
            expFill = UiKit.Pill(UiKit.Panel("ExpFill", track.transform, Palette.Gold));
            expFill.raycastTarget = false;
            nextText = UiKit.OneLine(UiKit.Label("Next", box, "", 24, Palette.TextDim, 0.23f, 0.8f, 0.97f, 0.83f, TextAnchor.MiddleLeft));

            description = UiKit.Label("Description", box, "", 30, Palette.TextDim, 0.027f, 0.735f, 0.973f, 0.79f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 20);

            // 능력치 3칸 (Figma 'Stat Tile'). 성유물 보너스는 괄호로
            string[] labels = { "HP", "공격", "방어" };
            const float gap = 24f / 1032f;
            float w = (0.946f - 2 * gap) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float x = 0.027f + i * (w + gap);
                var tile = UiKit.RoundPanel($"Stat_{labels[i]}", box, Palette.Panel, UiKit.RadiusMd, x, 0.62f, x + w, 0.725f);
                tile.raycastTarget = false;
                UiKit.OneLine(UiKit.Label("Label", tile.transform, labels[i], 28, Palette.TextDim, 0, 0.6f, 1, 0.94f));
                statValues[i] = UiKit.Display(UiKit.Label("Value", tile.transform, "", 52, Palette.Text, 0, 0.04f, 1, 0.66f,
                    TextAnchor.MiddleCenter, FontStyle.Normal, true, 26));
            }

            UiKit.Display(UiKit.Label("SkillsHeader", box, "기술", 44, Palette.Gold, 0.027f, 0.57f, 0.5f, 0.612f, TextAnchor.MiddleLeft));
            var skillArea = UiKit.Rect("Skills", box, 0.027f, 0.165f, 0.973f, 0.565f);
            for (int i = 0; i < MaxSkillRows; i++) skillRows.Add(SkillRowView.Create(skillArea, $"SkillRow_{i}"));

            var effect = UiKit.RoundPanel("RelicEffect", box, Palette.Panel, UiKit.RadiusMd, 0.027f, 0.022f, 0.973f, 0.15f);
            effect.raycastTarget = false;
            var star = UiKit.IconImage("Icon", effect.transform, UiKit.Icon("star_full"), 0f, 0.5f, 0f, 0.5f);
            star.rectTransform.pivot = new Vector2(0f, 0.5f);
            star.rectTransform.sizeDelta = new Vector2(44, 44);
            star.rectTransform.anchoredPosition = new Vector2(24, 0);
            effectLine1 = UiKit.Label("Line1", effect.transform, "", 28, Palette.Text, 0.085f, 0.5f, 0.98f, 0.92f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
            effectLine2 = UiKit.Label("Line2", effect.transform, "기술 = 기본 기술 1개 + 끼운 성유물마다 1개", 26, Palette.TextDim, 0.085f, 0.08f, 0.98f, 0.5f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 16);
        }

        public void Show(Hero hero)
        {
            Root.SetActive(true);
            slotsRow.Show(hero);
            var face = art.transform.GetChild(0).GetComponent<Image>();
            face.sprite = UiKit.HeroPortrait(hero.Data);
            face.enabled = face.sprite != null;
            nameText.text = hero.DisplayName;
            levelText.text = $"Lv{hero.Level} · 성유물 {hero.EquippedCount}개 장착";
            if (hero.IsMaxLevel)
            {
                expFill.enabled = true;
                expFill.rectTransform.anchorMax = Vector2.one;
                nextText.text = "최고 레벨이에요";
            }
            else
            {
                float ratio = Mathf.Clamp01((float)hero.Exp / LevelCurve.ExpToNextLevel(hero.Level));
                expFill.enabled = ratio > 0f;
                expFill.rectTransform.anchorMax = new Vector2(ratio, 1);
                nextText.text = $"다음 레벨까지 {hero.ExpToNextLevel}";
            }
            description.text = hero.Data.Description;

            var stats = hero.Stats;
            var bonus = hero.RelicBonus;
            statValues[0].text = $"{hero.CurrentHp} / {stats.MaxHp}";
            statValues[1].text = stats.Attack + (bonus.Attack > 0 ? $" (+{bonus.Attack})" : "");
            statValues[2].text = stats.Defense + (bonus.Defense > 0 ? $" (+{bonus.Defense})" : "");

            ShowSkills(hero);
            effectLine1.text = EffectSummary(hero);
        }

        public void Hide() => Root.SetActive(false);

        // 기술 줄은 개수에 맞춰 영역을 나눈다. 4개면 설명 줄 없이 두 줄로
        private void ShowSkills(Hero hero)
        {
            var skills = hero.Skills;
            int count = Mathf.Min(skills.Count, skillRows.Count);
            bool compact = count > 3;
            const float gap = 12f / 464f;
            float h = count > 0 ? (1f - (count - 1) * gap) / count : 1f;
            for (int i = 0; i < skillRows.Count; i++)
            {
                var row = skillRows[i];
                bool has = i < count;
                row.Root.SetActive(has);
                if (!has) continue;
                var rt = (RectTransform)row.Root.transform;
                float top = 1f - i * (h + gap);
                rt.anchorMin = new Vector2(0, top - h);
                rt.anchorMax = new Vector2(1, top);
                var source = hero.SourceOf(skills[i]);
                string from = source == null ? "기본 기술 · " : source.Relic != null ? $"{source.SourceName} +{source.Relic.Level} · " : "기술문서 · ";
                row.Show(skills[i], from, null, compact);
            }
        }

        // "공격 +6 (깃펜 +2) · 방어 +5 · HP +12 (백과사전 +1)"
        internal static string EffectSummary(Hero hero)
        {
            var parts = new List<string>();
            for (int i = 0; i < hero.SlotCount; i++)
            {
                var relic = hero.SlotAt(i);
                if (relic == null) continue;
                parts.Add($"{relic.Data.DisplayName} +{relic.Level}: {BonusText(relic.Bonus)}");
            }
            return parts.Count > 0 ? "성유물 효과  " + string.Join("  ·  ", parts) : "끼운 성유물이 없어요 — 성유물 탭에서 끼워 보세요";
        }

        internal static string BonusText(MonsterStats bonus)
        {
            var parts = new List<string>();
            if (bonus.MaxHp != 0) parts.Add($"HP +{bonus.MaxHp}");
            if (bonus.Attack != 0) parts.Add($"공격 +{bonus.Attack}");
            if (bonus.Defense != 0) parts.Add($"방어 +{bonus.Defense}");
            return parts.Count > 0 ? string.Join(" ", parts) : "보너스 없음";
        }
    }

    // 소지품 > 성유물 탭 (Figma '소지품 — 성유물'): 끼운 3칸 + 모은 성유물(못 찾은 것은 ???) + 고른 성유물 상세 · [장착하기]/[빼기]
    public class RelicPage
    {
        public const int Columns = 5;   // 한 줄 기본 칸 수 (10개까지 두 줄)
        private const int MaxCells = 16; // 성유물이 늘어도 두 줄 안에 (한 줄 최대 8칸) — #120 1.1에서 11개가 되자 칸이 모자랐음

        private class Cell
        {
            public GameObject Root;
            public Image Back, Icon, LevelBack, Border;
            public Text Level, Name;
        }

        public GameObject Root { get; private set; }
        public OwnedRelic Selected { get; private set; }
        public bool ChoosingSlot { get; private set; } // 바꿔 끼울 칸을 고르는 중 (#91)

        private RelicSlotsRow slotsRow;
        private string swapNote; // 방금 바꾼 결과 ("등불 ↔ 깃펜 바꿔 끼웠어요")
        private Text countText;
        private RectTransform grid;
        private readonly List<Cell> cells = new List<Cell>();
        private Image art, artIcon;
        private Text nameText, roleText, bonusText, description, upgradeText, unknownText;
        private SkillRowView skillRow;
        private Button actionButton;
        private Text actionLabel;
        private Button skillButton; // 끼웠는데 기술 칸이 가득해서 기술이 빠져 있을 때 [기술 넣기]
        private GameObject detailGroup;

        // [기술 넣기]를 눌렀을 때 (가방이 기술 배우기 창을 연다)
        public Action<OwnedRelic> PlaceSkill { get; set; }

        private GameSession session;
        private GameDatabase database;
        private Action onChanged;
        private readonly List<RelicData> catalog = new List<RelicData>(); // 칸 순서: 가진 것 먼저, 그다음 못 찾은 것

        public static RelicPage Create(RectTransform parent, float minX, float minY, float maxX, float maxY)
        {
            var page = new RelicPage();
            var root = UiKit.Rect("RelicPage", parent, minX, minY, maxX, maxY);
            page.Root = root.gameObject;
            page.slotsRow = RelicSlotsRow.Create(root, 0, 0.885f, 1, 1, page.OnSlotClicked);
            page.countText = UiKit.Display(UiKit.Label("Count", root, "", 40, Palette.Gold, 0.01f, 0.835f, 0.99f, 0.875f, TextAnchor.MiddleLeft));
            page.grid = UiKit.Rect("Grid", root, 0, 0.655f, 1, 0.83f);
            for (int i = 0; i < MaxCells; i++) page.cells.Add(page.BuildCell(i));
            page.BuildDetail(UiKit.RoundPanel("RelicDetail", root, Palette.PanelLight, UiKit.RadiusLg, 0, 0, 1, 0.64f));
            return page;
        }

        private Cell BuildCell(int index)
        {
            var cellRoot = UiKit.Rect($"RelicCell_{index}", grid, 0, 0, 0, 0);
            var back = UiKit.RoundPanel("Back", cellRoot, Palette.PanelLight, UiKit.RadiusMd, 0.5f, 1, 0.5f, 1);
            back.rectTransform.pivot = new Vector2(0.5f, 1);
            back.rectTransform.sizeDelta = new Vector2(112, 112);
            UiKit.AddButton(back).onClick.AddListener(() => SelectCell(index));
            var cell = new Cell { Root = cellRoot.gameObject, Back = back };
            cell.Icon = UiKit.IconImage("Icon", back.transform, null, 0.12f, 0.12f, 0.88f, 0.88f);
            cell.LevelBack = UiKit.Pill(UiKit.Panel("Level", back.transform, Palette.Gold, 1, 0, 1, 0));
            cell.LevelBack.raycastTarget = false;
            cell.LevelBack.rectTransform.pivot = new Vector2(1, 0);
            cell.LevelBack.rectTransform.sizeDelta = new Vector2(52, 30);
            cell.LevelBack.rectTransform.anchoredPosition = new Vector2(-4, 4);
            cell.Level = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", cell.LevelBack.transform, "", 24, Palette.OnAccent, 0, 0, 1, 1)));
            cell.Border = UiKit.Outline(UiKit.Panel("Border", back.transform, Palette.Gold), UiKit.RadiusMd, 5);
            cell.Border.raycastTarget = false;
            // 이름은 그림 바로 아래 (그림 크기는 LayoutCells에서 정함)
            cell.Name = UiKit.OneLine(UiKit.Label("Name", cellRoot, "", 24, Palette.TextDim, 0, 1, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 16));
            cell.Name.rectTransform.pivot = new Vector2(0.5f, 1);
            cell.Name.rectTransform.sizeDelta = new Vector2(0, 34);
            return cell;
        }

        private void BuildDetail(Image panel)
        {
            panel.raycastTarget = false;
            var box = panel.rectTransform;
            detailGroup = UiKit.Stretch("Content", box).gameObject;
            var content = (RectTransform)detailGroup.transform;

            art = UiKit.RoundPanel("Art", content, Palette.Panel, UiKit.RadiusLg, 0, 1, 0, 1);
            art.raycastTarget = false;
            art.rectTransform.pivot = new Vector2(0, 1);
            art.rectTransform.sizeDelta = new Vector2(160, 160);
            art.rectTransform.anchoredPosition = new Vector2(28, -24);
            artIcon = UiKit.IconImage("Icon", art.transform, null, 0.1f, 0.1f, 0.9f, 0.9f);

            nameText = UiKit.Display(UiKit.Label("Name", content, "", 56, Palette.Text, 0.22f, 0.865f, 0.97f, 0.965f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 30));
            roleText = UiKit.OneLine(UiKit.Label("Role", content, "", 30, Palette.TextDim, 0.22f, 0.815f, 0.97f, 0.865f, TextAnchor.MiddleLeft));
            bonusText = UiKit.OneLine(UiKit.Label("Bonus", content, "", 30, Palette.Gold, 0.22f, 0.765f, 0.97f, 0.815f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 18));
            description = UiKit.Label("Description", content, "", 30, Palette.TextDim, 0.027f, 0.6f, 0.973f, 0.72f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 20);
            var skillArea = UiKit.Rect("SkillArea", content, 0.027f, 0.33f, 0.973f, 0.585f);
            skillRow = SkillRowView.Create(skillArea, "SkillRow");
            upgradeText = UiKit.Label("Upgrade", content, "", 28, Palette.TextDim, 0.027f, 0.2f, 0.973f, 0.32f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
            actionButton = UiKit.MakeButton("RelicActionButton", content, "", Palette.Gold, 44, 0.027f, 0.03f, 0.973f, 0.18f, bestFit: true);
            var colors = actionButton.colors;
            colors.disabledColor = Color.white;
            actionButton.colors = colors;
            actionLabel = UiKit.LabelOf(actionButton);
            actionButton.onClick.AddListener(OnAction);
            skillButton = UiKit.MakeButton("RelicSkillButton", content, "기술 넣기", Palette.Info, 44, 0.51f, 0.03f, 0.973f, 0.18f, bestFit: true);
            skillButton.onClick.AddListener(() =>
            {
                if (Selected != null) PlaceSkill?.Invoke(Selected);
            });
            skillButton.gameObject.SetActive(false);

            unknownText = UiKit.Label("Unknown", box, "", 34, Palette.TextDim, 0.05f, 0.3f, 0.95f, 0.7f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 20);
        }

        // selected: 처음 고를 성유물 (null이면 첫 번째)
        public void Show(GameSession gameSession, GameDatabase gameDatabase, OwnedRelic selected, Action changed)
        {
            session = gameSession;
            database = gameDatabase;
            onChanged = changed;
            Root.SetActive(true);
            BuildCatalog();
            Select(selected ?? (session.Hero.Relics.Count > 0 ? session.Hero.Relics[0] : null), selected == null ? 0 : -1);
        }

        public void Hide() => Root.SetActive(false);

        // 기술 칸이 바뀐 뒤 다시 그리기
        public void Refresh()
        {
            if (Root.activeSelf && Selected != null) Select(Selected, -1);
        }

        private void BuildCatalog()
        {
            catalog.Clear();
            foreach (var relic in session.Hero.Relics) catalog.Add(relic.Data);
            if (database != null)
                foreach (var relic in database.Relics)
                    if (relic != null && !catalog.Contains(relic)) catalog.Add(relic);
        }

        // 칸을 누름: 바꿀 칸을 고르는 중이면 그 칸과 바꿔 끼우고, 아니면 그 칸의 성유물을 본다
        private void OnSlotClicked(int slot)
        {
            var hero = session.Hero;
            if (ChoosingSlot && Selected != null)
            {
                var outgoing = hero.SlotAt(slot);
                ChoosingSlot = false;
                if (hero.SwapInto(Selected, slot))
                {
                    swapNote = outgoing != null ? $"{Selected.Data.DisplayName} ↔ {outgoing.Data.DisplayName} 바꿔 끼웠어요" : $"{Selected.Data.DisplayName}을(를) 끼웠어요";
                    onChanged?.Invoke();
                }
                Select(Selected, -1);
                return;
            }
            var relic = hero.SlotAt(slot);
            if (relic != null) Select(relic, -1);
        }

        private void SelectCell(int index)
        {
            if (index >= catalog.Count) return;
            ChoosingSlot = false;
            var owned = session.Hero.Find(catalog[index]);
            Select(owned, owned == null ? index : -1);
        }

        // owned가 null이면 못 찾은 성유물 칸(unknownIndex)을 고른 것
        private void Select(OwnedRelic owned, int unknownIndex)
        {
            if (owned != Selected) swapNote = null;
            Selected = owned;
            var hero = session.Hero;
            slotsRow.Show(hero, owned != null ? hero.SlotOf(owned) : -1, ChoosingSlot, ChoosingSlot ? null : swapNote);
            countText.text = $"모은 성유물  {hero.Relics.Count} / {Mathf.Max(hero.Relics.Count, catalog.Count)}";
            LayoutCells();

            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                bool has = i < catalog.Count;
                cell.Root.SetActive(has);
                if (!has) continue;
                var mine = hero.Find(catalog[i]);
                bool selected = mine != null ? mine == owned : i == unknownIndex;
                cell.Back.color = mine != null ? (selected ? Palette.PanelLight : Palette.Panel) : Palette.Track;
                cell.Icon.sprite = mine != null ? UiKit.RelicIcon(mine.Data) : UiKit.Icon("lock");
                cell.Icon.enabled = cell.Icon.sprite != null;
                cell.Icon.color = mine != null ? Color.white : new Color(1, 1, 1, 0.4f);
                cell.LevelBack.gameObject.SetActive(mine != null);
                cell.Level.text = mine != null ? $"+{mine.Level}" : "";
                cell.Border.enabled = selected;
                bool equipped = mine != null && hero.IsEquipped(mine);
                cell.Name.text = mine == null ? "???" : equipped ? $"{mine.Data.DisplayName} ✓" : mine.Data.DisplayName;
                cell.Name.color = equipped ? Palette.Good : Palette.TextDim;
            }

            detailGroup.SetActive(owned != null);
            unknownText.gameObject.SetActive(owned == null);
            if (owned == null)
            {
                unknownText.text = catalog.Count == 0 ? "아직 성유물이 없어요" : "???\n아직 찾지 못한 성유물이에요\n보물상자나 보스에게서 얻을 수 있어요";
                return;
            }
            ShowDetail(owned);
        }

        // 칸은 늘 두 줄 안에: 10개까지 5칸씩, 더 많으면 한 줄 칸 수를 늘림 (11~12개 → 6칸, 13~14개 → 7칸).
        // #120: 1.1에서 성유물이 11개가 되자 세 줄로 나뉘며 줄 높이가 작아져 이름이 아랫줄 자물쇠에 가려졌고, 11번째 칸은 아예 없었음
        public static int ColumnsFor(int count) => Mathf.Max(Columns, Mathf.CeilToInt(count / 2f));

        private void LayoutCells()
        {
            int columns = ColumnsFor(catalog.Count);
            int rows = Mathf.Max(1, Mathf.CeilToInt(catalog.Count / (float)columns));
            float h = 1f / rows;
            // 그림 크기는 한 줄 높이에 맞춤: 그림 + 6 + 이름 34 + 여유 4가 한 줄 안에 (화면 비율이 달라도 이름이 아랫줄에 가려지지 않게)
            float size = Mathf.Min(rows > 1 ? 80f : 112f, grid.rect.height / rows - 6f - 34f - 4f);
            size = Mathf.Max(24f, size);
            for (int i = 0; i < cells.Count; i++)
            {
                int col = i % columns, row = i / columns;
                var rt = (RectTransform)cells[i].Root.transform;
                rt.anchorMin = new Vector2(col / (float)columns, 1f - (row + 1) * h);
                rt.anchorMax = new Vector2((col + 1) / (float)columns, 1f - row * h);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                cells[i].Back.rectTransform.sizeDelta = new Vector2(size, size);
                cells[i].Name.rectTransform.anchoredPosition = new Vector2(0, -size - 6);
            }
        }

        private void ShowDetail(OwnedRelic relic)
        {
            var hero = session.Hero;
            var data = relic.Data;
            art.color = Color.Lerp(Palette.Panel, data.PlaceholderColor, 0.18f);
            artIcon.sprite = UiKit.RelicIcon(data);
            artIcon.enabled = artIcon.sprite != null;
            nameText.text = $"{data.DisplayName}  +{relic.Level}";
            bool equipped = hero.IsEquipped(relic);
            bool skillOut = equipped && !hero.HasSkillOf(relic); // 끼웠지만 기술 칸이 가득해서 기술은 빠져 있음
            roleText.text = $"{data.Role.DisplayName()} · " + (equipped ? $"{hero.SlotOf(relic) + 1}번 칸에 장착 중" : "장착하지 않음")
                            + (skillOut ? " · 기술은 기술 칸에 없음" : "");
            var perLevel = HeroInfoPage.BonusText(data.BonusPerLevel);
            bonusText.text = $"{HeroInfoPage.BonusText(relic.Bonus)}   (강화할 때마다 {perLevel})";
            description.text = data.Description;

            string awaken = data.HasAwakening
                ? relic.IsAwakened ? $"각성 완료 (+{data.AwakenLevel}) — {data.Skill.DisplayName}에서 강해진 기술"
                                   : $"+{data.AwakenLevel} 각성 → {data.AwakenedSkill.DisplayName} ({BattleScreen.SkillEffect(data.AwakenedSkill)})"
                : data.Skill != null ? data.Skill.Description : "";
            if (relic.Skill != null) skillRow.Show(relic.Skill, "", awaken);
            skillRow.Root.SetActive(relic.Skill != null);

            var cost = data.CostFrom(relic.Level);
            upgradeText.text = cost == null
                ? "최대 단계(+5)까지 강화했어요"
                : $"강화: 마을의 성유물 제단 · +{relic.Level + 1}에 "
                  + (data.UpgradeItem != null ? $"{data.UpgradeItem.DisplayName} {cost.ItemCount}개 (보유 {session.Inventory.GetCount(data.UpgradeItem)}) · " : "")
                  + $"{cost.Gold}G";

            skillButton.gameObject.SetActive(skillOut);
            ((RectTransform)actionButton.transform).anchorMax = new Vector2(skillOut ? 0.49f : 0.973f, 0.18f);
            if (equipped)
            {
                SetAction("빼기", Palette.Neutral, true);
            }
            else if (hero.HasEmptySlot)
            {
                SetAction("장착하기 (빈 칸에)", Palette.Gold, true);
            }
            else if (ChoosingSlot)
            {
                SetAction("취소", Palette.Neutral, true);
            }
            else
            {
                SetAction("바꿔 끼우기 — 바꿀 칸 고르기", Palette.Gold, true); // #91: 칸이 가득 차도 바로 바꿈 (Figma '가방 — 성유물 바꿔 끼우기')
            }
        }

        private void SetAction(string text, Color color, bool enabled)
        {
            actionLabel.text = text;
            actionLabel.color = color == Palette.Gold ? Palette.OnAccent : enabled ? Palette.Text : Palette.TextDim;
            UiKit.SetColor(actionButton, color);
            actionButton.interactable = enabled;
        }

        private void OnAction()
        {
            if (Selected == null) return;
            var hero = session.Hero;
            swapNote = null;
            if (!hero.IsEquipped(Selected) && !hero.HasEmptySlot)
            {
                ChoosingSlot = !ChoosingSlot; // 바꿔 끼우기: 위의 칸을 고르게 (다시 누르면 취소)
                Select(Selected, -1);
                return;
            }
            ChoosingSlot = false;
            if (hero.IsEquipped(Selected)) hero.Unequip(Selected);
            else hero.Equip(Selected);
            onChanged?.Invoke();
            Select(Selected, -1);
        }

        // 뒤로가기: 칸 고르기 중이면 그것만 취소 (true)
        public bool CancelChoosing()
        {
            if (!ChoosingSlot) return false;
            ChoosingSlot = false;
            if (Selected != null) Select(Selected, -1);
            return true;
        }
    }
}
