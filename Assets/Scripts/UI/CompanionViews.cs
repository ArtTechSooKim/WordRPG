using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Monsters;

namespace WordRPG.UI
{
    // 소지품 > 동료 탭 (#77, Figma '가방 — 동료 (#77)'): 함께 가는 동료 한 줄 + 고른 동료의 얼굴·레벨·경험치·능력치·기술
    // (아직 못 배운 기술은 'Lv8에 배움') + [파티에서 쉬게 하기]/[함께 가기]. 동료가 여럿이 되면 위 줄에서 고른다
    public class CompanionPage
    {
        private const int MaxSkillRows = 4;
        private const int MaxPicks = 4;

        public GameObject Root { get; private set; }
        public Companion Selected { get; private set; }

        private Text partyLine, nameText, levelText, nextText, description;
        private Image face, expFill;
        private readonly Text[] statValues = new Text[3];
        private readonly List<SkillRowView> skillRows = new List<SkillRowView>();
        private readonly List<Button> picks = new List<Button>();
        private Button partyButton;
        private GameSession session;
        private Action onChanged;

        public static CompanionPage Create(RectTransform parent, float minX, float minY, float maxX, float maxY)
        {
            var page = new CompanionPage();
            var root = UiKit.Rect("CompanionPage", parent, minX, minY, maxX, maxY);
            page.Root = root.gameObject;

            var top = UiKit.RoundPanel("Party", root, Palette.Panel, UiKit.RadiusLg, 0, 0.885f, 1, 1);
            top.raycastTarget = false;
            UiKit.Label("Title", top.transform, "함께 가는 동료", 30, Palette.Gold, 0.03f, 0.5f, 0.6f, 0.92f, TextAnchor.MiddleLeft, FontStyle.Bold);
            page.partyLine = UiKit.Label("Line", top.transform, "", 24, Palette.TextDim, 0.03f, 0.1f, 0.6f, 0.5f, TextAnchor.MiddleLeft);
            for (int i = 0; i < MaxPicks; i++)
            {
                float x1 = 0.97f - i * 0.1f;
                var pick = UiKit.Pill(UiKit.Panel($"Pick_{i}", top.transform, Palette.PanelLight, x1 - 0.085f, 0.12f, x1, 0.88f));
                var button = UiKit.AddButton(pick);
                UiKit.IconImage("Face", pick.transform, null, 0.1f, 0.1f, 0.9f, 0.9f);
                int index = i;
                button.onClick.AddListener(() => page.Pick(index));
                page.picks.Add(button);
            }

            var box = UiKit.RoundPanel("CompanionDetail", root, Palette.PanelLight, UiKit.RadiusLg, 0, 0.1f, 1, 0.87f);
            box.raycastTarget = false;
            var art = UiKit.RoundPanel("Art", box.transform, Palette.Panel, UiKit.RadiusLg, 0, 1, 0, 1);
            art.raycastTarget = false;
            art.rectTransform.pivot = new Vector2(0, 1);
            art.rectTransform.sizeDelta = new Vector2(176, 176);
            art.rectTransform.anchoredPosition = new Vector2(28, -24);
            page.face = UiKit.IconImage("Face", art.transform, null, 0.08f, 0.08f, 0.92f, 0.92f);
            page.nameText = UiKit.Display(UiKit.Label("Name", box.transform, "", 56, Palette.Text, 0.23f, 0.89f, 0.97f, 0.97f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 30));
            page.levelText = UiKit.OneLine(UiKit.Label("Level", box.transform, "", 30, Palette.TextDim, 0.23f, 0.85f, 0.97f, 0.89f, TextAnchor.MiddleLeft));
            UiKit.OneLine(UiKit.Label("ExpLabel", box.transform, "경험치", 24, Palette.TextDim, 0.23f, 0.818f, 0.33f, 0.848f, TextAnchor.MiddleLeft));
            var track = UiKit.Pill(UiKit.Panel("ExpBack", box.transform, Palette.Track, 0.34f, 0.826f, 0.97f, 0.84f));
            track.raycastTarget = false;
            page.expFill = UiKit.Pill(UiKit.Panel("ExpFill", track.transform, Palette.Gold));
            page.expFill.raycastTarget = false;
            page.nextText = UiKit.OneLine(UiKit.Label("Next", box.transform, "", 24, Palette.TextDim, 0.23f, 0.785f, 0.97f, 0.815f, TextAnchor.MiddleLeft));
            page.description = UiKit.Label("Description", box.transform, "", 30, Palette.TextDim, 0.027f, 0.71f, 0.973f, 0.775f,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 20);

            string[] labels = { "HP", "공격", "방어" };
            const float gap = 24f / 1032f;
            float w = (0.946f - 2 * gap) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float x = 0.027f + i * (w + gap);
                var tile = UiKit.RoundPanel($"Stat_{labels[i]}", box.transform, Palette.Panel, UiKit.RadiusMd, x, 0.595f, x + w, 0.7f);
                tile.raycastTarget = false;
                UiKit.OneLine(UiKit.Label("Label", tile.transform, labels[i], 28, Palette.TextDim, 0, 0.6f, 1, 0.94f));
                page.statValues[i] = UiKit.Display(UiKit.Label("Value", tile.transform, "", 52, Palette.Text, 0, 0.04f, 1, 0.66f,
                    TextAnchor.MiddleCenter, FontStyle.Normal, true, 26));
            }
            UiKit.Display(UiKit.Label("SkillsHeader", box.transform, "기술", 44, Palette.Gold, 0.027f, 0.545f, 0.5f, 0.587f, TextAnchor.MiddleLeft));
            var skillArea = UiKit.Rect("Skills", box.transform, 0.027f, 0.03f, 0.973f, 0.54f);
            for (int i = 0; i < MaxSkillRows; i++) page.skillRows.Add(SkillRowView.Create(skillArea, $"SkillRow_{i}"));

            page.partyButton = UiKit.MakeButton("CompanionPartyButton", root, "", Palette.Button, 40, 0, 0, 1, 0.085f);
            page.partyButton.onClick.AddListener(page.ToggleParty);
            return page;
        }

        public void Show(GameSession gameSession, Action changed)
        {
            session = gameSession;
            onChanged = changed;
            Root.SetActive(true);
            if (Selected == null || !Contains(session.Companions, Selected))
                Selected = session.PartyCompanion ?? (session.Companions.Count > 0 ? session.Companions[0] : null);
            Refresh();
        }

        public void Hide() => Root.SetActive(false);

        private static bool Contains(IReadOnlyList<Companion> list, Companion companion)
        {
            foreach (var item in list) if (item == companion) return true;
            return false;
        }

        private void Pick(int index)
        {
            if (session == null || index >= session.Companions.Count) return;
            Selected = session.Companions[index];
            Refresh();
        }

        // 고른 동료를 데려가거나 쉬게 한다 (파티는 주인공 + 1명이라 다른 동료를 고르면 바뀜)
        private void ToggleParty()
        {
            if (session == null || Selected == null) return;
            session.SetPartyCompanion(session.PartyCompanion == Selected ? null : Selected);
            onChanged?.Invoke();
            Refresh();
        }

        public void Refresh()
        {
            var companions = session.Companions;
            for (int i = 0; i < picks.Count; i++)
            {
                bool has = i < companions.Count && companions.Count > 1;
                picks[i].gameObject.SetActive(has);
                if (!has) continue;
                var image = picks[i].transform.GetChild(0).GetComponent<Image>();
                image.sprite = UiKit.CompanionPortrait(companions[i].Data);
                image.enabled = image.sprite != null;
                UiKit.SetColor(picks[i], companions[i] == Selected ? Palette.Gold : Palette.PanelLight);
            }
            var party = session.PartyCompanion;
            partyLine.text = party != null ? $"파티 2 / 2명 · {session.Hero.DisplayName} + {party.DisplayName}" : "지금은 혼자 다녀요 · 동료를 골라 함께 가기";
            var companion = Selected;
            if (companion == null) return;
            face.sprite = UiKit.CompanionPortrait(companion.Data);
            face.enabled = face.sprite != null;
            nameText.text = companion.DisplayName;
            levelText.text = string.IsNullOrEmpty(companion.Data.Role) ? $"Lv{companion.Level}" : $"Lv{companion.Level} · {companion.Data.Role}";
            float ratio = companion.IsMaxLevel ? 1f : Mathf.Clamp01((float)companion.Exp / LevelCurve.ExpToNextLevel(companion.Level));
            expFill.enabled = ratio > 0f;
            expFill.rectTransform.anchorMax = new Vector2(ratio, 1);
            nextText.text = companion.IsMaxLevel ? "최고 레벨이에요" : $"다음 레벨까지 {companion.ExpToNextLevel}";
            description.text = companion.Data.Description;
            var stats = companion.Stats;
            statValues[0].text = $"{companion.CurrentHp} / {stats.MaxHp}";
            statValues[1].text = stats.Attack.ToString();
            statValues[2].text = stats.Defense.ToString();
            ShowSkills(companion);
            bool inParty = party == companion;
            UiKit.LabelOf(partyButton).text = inParty ? "파티에서 쉬게 하기" : "함께 가기";
            UiKit.SetColor(partyButton, inParty ? Palette.Button : Palette.Gold);
            UiKit.LabelOf(partyButton).color = inParty ? Palette.Text : Palette.OnAccent;
        }

        // 기본 기술 + 배우는 기술 전부 (아직이면 'Lv8에 배움')
        private void ShowSkills(Companion companion)
        {
            var rows = new List<(SkillData skill, string source, string note)>();
            if (companion.Data.BasicSkill != null) rows.Add((companion.Data.BasicSkill, "기본 기술 · ", null));
            foreach (var learned in companion.Data.LearnedSkills)
            {
                if (learned?.Skill == null) continue;
                bool known = learned.Level <= companion.Level;
                rows.Add((learned.Skill, $"Lv{learned.Level} · ", known ? null : $"Lv{learned.Level}에 배움 — {learned.Skill.Description}"));
            }
            int count = Mathf.Min(rows.Count, skillRows.Count);
            const float gap = 12f / 464f;
            float h = count > 0 ? (1f - (count - 1) * gap) / count : 1f;
            for (int i = 0; i < skillRows.Count; i++)
            {
                var row = skillRows[i];
                row.Root.SetActive(i < count);
                if (i >= count) continue;
                var rt = (RectTransform)row.Root.transform;
                float top = 1f - i * (h + gap);
                rt.anchorMin = new Vector2(0, top - h);
                rt.anchorMax = new Vector2(1, top);
                row.Show(rows[i].skill, rows[i].source, rows[i].note, count > 3);
            }
        }
    }

    // 동료 조우 연출 (#77, Figma '동료 조우 1·2·3 (#77)'): ① 화면이 어두워지고 제단이 빛남 + "석판의 빈칸이 모두 채워졌다…" →
    // ② 동료 얼굴이 화면 가운데로 크게 다가옴(확대) + 빛줄기 + 한마디 → ③ '동료를 얻었다!' 띠 + 동료 카드 + [함께 가기]
    public class CompanionCutscene
    {
        private GameObject root;
        private CanvasGroup group;
        private Image dim, glow, rays, bigFace, cardFace;
        private Text line1, line2, quote, cardName, cardRole, cardDesc;
        private RectTransform banner, card;
        private Button goButton;
        private bool confirmed;

        public bool IsPlaying => root != null && root.activeSelf;
        public bool ShowsCard => IsPlaying && card.gameObject.activeSelf;

        public static CompanionCutscene Create(RectTransform parent)
        {
            var view = new CompanionCutscene();
            var rect = UiKit.Stretch("CompanionCutscene", parent);
            view.root = rect.gameObject;
            view.group = view.root.AddComponent<CanvasGroup>();
            view.dim = UiKit.Panel("Dim", rect, new Color(0.03f, 0.04f, 0.05f, 0.92f));
            view.dim.raycastTarget = true;
            view.rays = UiKit.IconImage("Rays", rect, UiKit.RaysSprite(), 0.5f, 0.55f, 0.5f, 0.55f);
            view.rays.rectTransform.sizeDelta = new Vector2(2000, 2000);
            view.rays.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.35f);
            view.glow = UiKit.IconImage("Glow", rect, UiKit.GlowSprite(), 0.5f, 0.6f, 0.5f, 0.6f);
            view.glow.rectTransform.sizeDelta = new Vector2(420, 420);
            view.glow.color = Palette.Gold;
            view.bigFace = UiKit.IconImage("BigFace", rect, null, 0.5f, 0.57f, 0.5f, 0.57f);
            view.bigFace.rectTransform.sizeDelta = new Vector2(660, 660);
            view.line1 = UiKit.Display(UiKit.OneLine(UiKit.Label("Line1", rect, "", 56, Palette.Text, 0.04f, 0.33f, 0.96f, 0.38f)));
            view.line2 = UiKit.OneLine(UiKit.Label("Line2", rect, "", 32, Palette.Gold, 0.04f, 0.29f, 0.96f, 0.33f, TextAnchor.MiddleCenter, FontStyle.Bold));
            view.quote = UiKit.OneLine(UiKit.Label("Quote", rect, "", 36, Palette.Gold, 0.04f, 0.24f, 0.96f, 0.28f, TextAnchor.MiddleCenter, FontStyle.Bold));

            // ③ 띠 + 카드 + [함께 가기]
            view.banner = UiKit.Rect("Banner", rect, 0.5f, 0.875f, 0.5f, 0.875f);
            view.banner.sizeDelta = new Vector2(1240f, 150f);
            view.banner.localEulerAngles = new Vector3(0f, 0f, 4f);
            UiKit.Panel("Band", view.banner, Palette.Attack).raycastTarget = false;
            UiKit.Panel("EdgeTop", view.banner, Palette.Gold, 0, 1, 1, 1).rectTransform.sizeDelta = new Vector2(0f, 6f);
            UiKit.Panel("EdgeBottom", view.banner, Palette.Gold, 0, 0, 1, 0).rectTransform.sizeDelta = new Vector2(0f, 6f);
            UiKit.Display(UiKit.OneLine(UiKit.Label("Title", view.banner, "동료를 얻었다!", 86, Palette.Text, 0, 0, 1, 1)));
            var cardImage = UiKit.RoundPanel("Card", rect, Palette.Panel, UiKit.RadiusLg, 0.067f, 0.2f, 0.933f, 0.78f);
            view.card = cardImage.rectTransform;
            var edge = UiKit.Outline(UiKit.Panel("Edge", view.card, Palette.Gold), UiKit.RadiusLg, 4);
            edge.raycastTarget = false;
            view.cardFace = UiKit.IconImage("Face", view.card, null, 0.36f, 0.62f, 0.64f, 0.95f);
            view.cardName = UiKit.Display(UiKit.OneLine(UiKit.Label("Name", view.card, "", 72, Palette.Text, 0, 0.5f, 1, 0.61f)));
            view.cardRole = UiKit.OneLine(UiKit.Label("Role", view.card, "", 32, Palette.Gold, 0, 0.44f, 1, 0.5f, TextAnchor.MiddleCenter, FontStyle.Bold));
            view.cardDesc = UiKit.Label("Desc", view.card, "", 30, Palette.TextDim, 0.06f, 0.06f, 0.94f, 0.43f, TextAnchor.UpperCenter, FontStyle.Normal, true, 20);
            view.goButton = UiKit.MakeButton("CompanionJoinButton", rect, "함께 가기", Palette.Gold, 46, 0.067f, 0.07f, 0.933f, 0.135f);
            UiKit.LabelOf(view.goButton).color = Palette.OnAccent;
            view.goButton.onClick.AddListener(() => view.confirmed = true);
            view.root.SetActive(false);
            return view;
        }

        // speed: 연출 속도 배율 (테스트는 작게). [함께 가기]를 누르면 끝
        public IEnumerator Play(CompanionData companion, float speed = 1f)
        {
            confirmed = false;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            var portrait = UiKit.CompanionPortrait(companion);
            bigFace.sprite = cardFace.sprite = portrait;
            bigFace.enabled = cardFace.enabled = portrait != null;
            ShowStage(1);
            line1.text = "석판의 빈칸이 모두 채워졌다…";
            line2.text = "유적 깊은 곳에서 메아리가 울린다";
            Sound.Play(Sfx.EvolveLight);
            yield return Animate(1.3f * speed, t =>
            {
                group.alpha = Mathf.Clamp01(t * 4f);
                glow.rectTransform.localScale = Vector3.one * (0.6f + 0.5f * Mathf.Sin(t * Mathf.PI));
                rays.rectTransform.localEulerAngles = new Vector3(0, 0, t * 25f);
            });

            ShowStage(2);
            line1.text = "고대 유적에 잠들어 있던 소녀가 깨어났다";
            line2.text = "";
            quote.text = "\"…글자를 되찾아 줬구나. 고마워!\"";
            Sound.Play(Sfx.Evolve);
            yield return Animate(1.6f * speed, t =>
            {
                float k = 1f - (1f - t) * (1f - t);
                bigFace.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, k); // 카메라가 다가가듯 확대
                rays.rectTransform.localEulerAngles = new Vector3(0, 0, 25f + t * 40f);
            });

            ShowStage(3);
            cardName.text = companion.DisplayName;
            cardRole.text = companion.Role;
            cardDesc.text = companion.Description + "\n\n파티는 주인공 + 동료 1명 · 가방 > 동료에서 바꿀 수 있어요";
            Sound.PlayJingle(Sfx.NewWord);
            while (!confirmed) yield return null;
            root.SetActive(false);
        }

        private void ShowStage(int stage)
        {
            glow.gameObject.SetActive(stage == 1);
            bigFace.gameObject.SetActive(stage == 2);
            line1.gameObject.SetActive(stage < 3);
            line2.gameObject.SetActive(stage == 1);
            quote.gameObject.SetActive(stage == 2);
            banner.gameObject.SetActive(stage == 3);
            card.gameObject.SetActive(stage == 3);
            goButton.gameObject.SetActive(stage == 3);
            rays.gameObject.SetActive(true);
        }

        private static IEnumerator Animate(float seconds, Action<float> step)
        {
            seconds = Mathf.Max(0.01f, seconds);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                step(t / seconds);
                yield return null;
            }
            step(1f);
        }
    }
}
