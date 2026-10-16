using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WordRPG.Battle;
using WordRPG.Field;
using WordRPG.Game;
using WordRPG.Heroes;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.UI
{
    // 전투 > 화면 만들기: 패널(기술·문제·단어 카드·결산)과 위치 도우미
    public partial class BattleScreen
    {
        private static void PinBottom(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(from, 0);
            rect.anchorMax = new Vector2(to, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.offsetMin = new Vector2(from > 0 ? 10 : 40, ResultBottom);
            rect.offsetMax = new Vector2(to < 1 ? -10 : -40, ResultBottom + ResultButtonHeight);
        }

        // 부모 위쪽에서 top만큼 내려와 높이 height (좌우 left·right 여백)
        private static void PinTop(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        // ------------------------------------------------------------------ UI 생성

        private void BuildUi()
        {
            UiKit.EnsureEventSystem();

            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.sortingOrder = 10; // 필드 HUD 위에 덮는다
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            UiKit.Panel("Background", canvasGo.transform, Palette.Background);
            BuildBackdrop(canvasGo.transform);

            root = UiKit.Stretch("SafeArea", canvasGo.transform);
            UiKit.ApplySafeArea(root);

            // 상단 바
            roundLabel = UiKit.Display(UiKit.Label("Round", root, "라운드 1", 44, Palette.Gold, 0.03f, 0.945f, 0.3f, 1f,
                TextAnchor.MiddleLeft));
            vocabLabel = UiKit.Label("Vocab", root, "", 30, Palette.TextDim, 0.3f, 0.945f, 0.78f, 1f,
                TextAnchor.MiddleRight);
            dexButton = UiKit.MakeButton("DexButton", root, "도감", Palette.Button, 34, 0.8f, 0.948f, 0.97f, 0.998f);
            dexButton.onClick.AddListener(OnDexClicked);

            enemyArea = UiKit.Rect("EnemyArea", root, 0, 0.62f, 1, 0.94f);

            // 전투 로그
            var logPanel = UiKit.RoundPanel("LogPanel", root, Palette.Panel, UiKit.RadiusMd, 0.03f, 0.512f, 0.97f, 0.612f);
            logLabel = UiKit.Label("Log", logPanel.transform, "", 31, Palette.Text, 0, 0, 1, 1,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 22);
            UiKit.Pad(logLabel.rectTransform, 24, 8, 24, 8);

            // 아군 카드: 주인공 (+ 동료, #77 — 자리는 BeginBattle에서 인원에 맞춤)
            var partyArea = UiKit.Rect("PartyArea", root, 0, 0.355f, 1, 0.51f);
            partyViews = new[] { UnitView.Create(partyArea, "Party_0", 1f / 3f, 0, 2f / 3f, 1),
                                 UnitView.Create(partyArea, "Party_1", 2f / 3f, 0, 1f, 1) };

            // 하단 패널들
            var bottom = UiKit.Rect("Bottom", root, 0, 0, 1, 0.35f);
            UiKit.Pad(bottom, 24, 24, 24, 12);
            BuildSkillPanel(bottom);
            BuildQuizPanel(bottom);
            BuildCardPanel(bottom);

            // 보스 결정타 연출 층: 어둡게 · 하얀 번쩍임 · '결정타!' (Figma 'Battle — 보스 결정타 (#41)')
            finisherShade = UiKit.Panel("FinisherShade", root, new Color(0f, 0f, 0f, 0f));
            finisherShade.raycastTarget = false;
            finisherShade.gameObject.SetActive(false);
            finisherFlash = UiKit.Panel("FinisherFlash", root, new Color(1f, 1f, 1f, 0f));
            finisherFlash.raycastTarget = false;
            finisherFlash.gameObject.SetActive(false);
            finisherStamp = UiKit.Display(UiKit.Label("FinisherStamp", root, "결정타!", 150, Palette.Gold, 0, 0.66f, 1, 0.8f));
            var stampOutline = finisherStamp.gameObject.AddComponent<Outline>();
            stampOutline.effectColor = new Color(0.1f, 0.05f, 0.02f, 1f);
            stampOutline.effectDistance = new Vector2(5, -5);
            finisherStamp.rectTransform.localEulerAngles = new Vector3(0, 0, 4);
            finisherStamp.gameObject.SetActive(false);

            // 결산은 화면을 다 덮지만 [도감]은 그 위에. 결정타 층은 결산이 뜨기 전에 걷히므로 맨 위에 둬도 된다
            BuildResultPanel(root);
            dexButton.transform.SetAsLastSibling();
            finisherShade.transform.SetAsLastSibling();
            finisherFlash.transform.SetAsLastSibling();
            finisherStamp.transform.SetAsLastSibling();
            HideAllPanels();

            dexView = DexView.Create(root); // 맨 마지막에 만들어 모든 화면 위에 덮는다
        }

        private void BuildSkillPanel(Transform parent)
        {
            skillPanel = UiKit.Stretch("SkillPanel", parent).gameObject;
            skillTitle = UiKit.Label("Title", skillPanel.transform, "", 40, Palette.Text, 0, 0.84f, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Bold, true, 24);

            // 기술 4칸 (기본 기술 + 성유물 3개) + 맨 아래 가방(상처약) 버튼 — Figma 'Battle — 주인공'
            for (int i = 0; i < 4; i++)
            {
                float top = 0.835f - i * 0.165f;
                var button = UiKit.MakeButton($"SkillButton_{i}", skillPanel.transform, "", Palette.Button, 44,
                    0, top - 0.155f, 1, top, bestFit: true);
                var label = UiKit.LabelOf(button);
                label.rectTransform.anchorMin = new Vector2(0, 0.4f);
                label.rectTransform.offsetMin = new Vector2(16, 0);
                var detail = UiKit.Label("Detail", button.transform, "", 26, new Color(1, 1, 1, 0.85f), 0, 0.06f, 1, 0.44f,
                    TextAnchor.MiddleCenter, FontStyle.Normal, true, 16);
                UiKit.Pad(detail.rectTransform, 16, 0, 16, 0);
                skillButtons.Add(button);
                skillDetails.Add(detail);
            }

            // 맨 아래 줄: [가방 — 상처약 쓰기] [도망가기] (Figma 'Battle — 도망가기 (#53)': 696 : 320, 사이 16)
            itemButton = UiKit.MakeButton("ItemButton", skillPanel.transform, "가방 — 상처약 쓰기", Palette.Button, 40,
                0, 0, 0.674f, 0.155f, bestFit: true);
            var itemColors = itemButton.colors;
            itemColors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            itemButton.colors = itemColors;
            itemButton.onClick.AddListener(ShowItemMenu);

            fleeButton = UiKit.MakeButton("FleeButton", skillPanel.transform, "도망가기", Palette.Neutral, 44,
                0.69f, 0, 1, 0.155f, bestFit: true);
            fleeButton.onClick.AddListener(OnFleeClicked);
            fleeButtonHome = ((RectTransform)fleeButton.transform).anchoredPosition;

            // 수련 중에는 상처약 대신 [수련 종료] (Figma 'Battle — 수련 (#44)')
            trainingEndButton = UiKit.MakeButton("TrainingEndButton", skillPanel.transform, "수련 종료", Palette.Neutral, 44,
                0, 0, 1, 0.155f, bestFit: true);
            trainingEndButton.onClick.AddListener(() => endTrainingRequested = true);
            trainingEndButton.gameObject.SetActive(false);

            cancelButton = UiKit.MakeButton("CancelButton", skillPanel.transform, "취소", Palette.Neutral,
                44, 0.25f, 0, 0.75f, 0.155f);
            cancelButton.onClick.AddListener(OnCancelTargeting);

            // 강도 버튼: 왼쪽 큰 배율 + 오른쪽 '단어 n개 연속' · 예상 피해 (평소엔 숨김)
            for (int i = 0; i < 4; i++)
            {
                float top = 0.835f - i * 0.165f;
                var button = UiKit.MakeButton($"Intensity_{i}", skillPanel.transform, "", IntensityColors[i], 40,
                    0, top - 0.155f, 1, top);
                var label = UiKit.LabelOf(button);
                label.text = "";
                var multiplier = UiKit.Display(UiKit.OneLine(UiKit.Label("Multiplier", button.transform, "", 64,
                    i < 2 ? Palette.Gold : Palette.Text, 0, 0, 0.2f, 1)));
                var title = UiKit.Display(UiKit.OneLine(UiKit.Label("Title", button.transform, "", 42, Palette.Text,
                    0.22f, 0.45f, 0.98f, 0.95f, TextAnchor.MiddleLeft)));
                var detail = UiKit.Label("Detail", button.transform, "", 26, new Color(1, 1, 1, 0.85f), 0.22f, 0.06f, 0.98f, 0.46f,
                    TextAnchor.MiddleLeft, FontStyle.Normal, true, 16);
                button.gameObject.SetActive(false);
                intensityButtons.Add(button);
                intensityTexts.Add((multiplier, title, detail));
            }
        }

        private void BuildQuizPanel(Transform parent)
        {
            quizPanel = UiKit.Stretch("QuizPanel", parent).gameObject;
            quizPrompt = UiKit.Display(UiKit.Label("Prompt", quizPanel.transform, "", 84, Palette.Text, 0, 0.8f, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 30));
            quizHint = UiKit.OneLine(UiKit.Label("Hint", quizPanel.transform, "", 30, Palette.TextDim, 0, 0.735f, 1, 0.8f)); // 줄 높이 때문에 잘리지 않게

            var timerBack = UiKit.Pill(UiKit.Panel("TimerBack", quizPanel.transform, Palette.Track,
                0.02f, 0.695f, 0.98f, 0.72f));
            timerFillImage = UiKit.Pill(UiKit.Panel("TimerFill", timerBack.transform, Palette.Info));
            timerFill = timerFillImage.rectTransform;

            var badge = UiKit.Pill(UiKit.Panel("ChainBadge", quizPanel.transform, Palette.Evolve, 1, 1, 1, 1));
            badge.raycastTarget = false;
            badge.rectTransform.pivot = new Vector2(1, 0);
            badge.rectTransform.anchoredPosition = new Vector2(-8, 10);
            var row = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 6, 6);
            row.spacing = 12;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fit = badge.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            chainLabel = UiKit.Display(UiKit.OneLine(UiKit.Label("Multiplier", badge.transform, "", 36, Palette.Text, 0, 0, 1, 1)));
            for (int i = 0; i < 4; i++)
            {
                var pip = UiKit.Pill(UiKit.Panel($"Pip_{i}", badge.transform, Palette.Track));
                pip.raycastTarget = false;
                var size = pip.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = size.preferredHeight = 22;
                chainPips.Add(pip);
            }
            chainCount = UiKit.OneLine(UiKit.Label("Count", badge.transform, "", 30, Palette.Text, 0, 0, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Bold));
            chainBadge = badge.gameObject;
            chainBadge.SetActive(false);

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                float top = 0.68f - i * 0.1725f;
                var button = UiKit.MakeButton($"Choice_{i}", quizPanel.transform, "", Palette.Button, 40,
                    0, top - 0.16f, 1, top, bestFit: true);
                UiKit.LabelOf(button).font = UiFonts.Bold; // 보기는 읽기 쉬운 본문 글꼴 (Figma Body/Large Bold)
                var choiceColors = button.colors;
                choiceColors.disabledColor = Color.white; // 정답/오답 색이 흐려지지 않게
                button.colors = choiceColors;
                button.onClick.AddListener(() =>
                {
                    if (pickedChoice == int.MinValue) pickedChoice = index;
                });
                choiceButtons.Add(button);
            }
        }

        private void BuildCardPanel(Transform parent)
        {
            cardPanel = UiKit.Stretch("CardPanel", parent).gameObject;
            UiKit.RoundPanel("Bg", cardPanel.transform, Palette.Panel, UiKit.RadiusLg);
            // 새 단어일 때만: 단어 뒤 금빛 + 왼쪽 위 '발견!' 도장
            cardGlow = UiKit.IconImage("DiscoveryGlow", cardPanel.transform, UiKit.GlowSprite(), 0.5f, 0.6f, 0.5f, 0.6f);
            cardGlow.rectTransform.sizeDelta = new Vector2(900, 420);
            cardGlow.preserveAspect = false;
            cardGlow.raycastTarget = false;
            cardBorder = UiKit.Outline(UiKit.Panel("Border", cardPanel.transform, Palette.Gold), UiKit.RadiusLg, 4);
            cardBorder.raycastTarget = false;
            cardTitle = UiKit.Display(UiKit.Label("Title", cardPanel.transform, "", 44, Palette.Gold, 0, 0.8f, 1, 0.97f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 24));
            cardWord = UiKit.Display(UiKit.Label("Word", cardPanel.transform, "", 92, Palette.Text, 0.03f, 0.56f, 0.97f, 0.8f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 36));
            cardMeaning = UiKit.Display(UiKit.Label("Meaning", cardPanel.transform, "", 56, Palette.Gold, 0.03f, 0.38f, 0.97f, 0.57f,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 26));
            cardExtra = UiKit.Label("Extra", cardPanel.transform, "", 30, Palette.TextDim, 0.05f, 0.2f, 0.95f, 0.38f,
                TextAnchor.UpperCenter, FontStyle.Normal, true, 20);
            cardConfirm = UiKit.MakeButton("CardConfirmButton", cardPanel.transform, "확인", Palette.Gold, 44,
                0.05f, 0.04f, 0.95f, 0.2f);
            cardConfirmLabel = UiKit.LabelOf(cardConfirm);
            cardConfirmLabel.color = Palette.OnAccent;
            cardConfirm.onClick.AddListener(() => cardConfirmed = true);

            cardStamp = UiKit.Pill(UiKit.Panel("DiscoveryStamp", cardPanel.transform, Palette.Gold, 0, 1, 0, 1));
            cardStamp.raycastTarget = false;
            cardStamp.rectTransform.sizeDelta = new Vector2(210, 84);
            cardStamp.rectTransform.anchoredPosition = new Vector2(120, -6);
            cardStamp.rectTransform.localEulerAngles = new Vector3(0, 0, 10f);
            UiKit.Display(UiKit.OneLine(UiKit.Label("Text", cardStamp.transform, "발견!", 50, Palette.OnAccent, 0, 0, 1, 1)));
            cardGlow.gameObject.SetActive(false);
            cardStamp.gameObject.SetActive(false);
        }

        // 전투 결산 (Figma 'Battle — 전투 결산 꽉 채움 (#42)'): 화면 전체를 덮는 판. 내용은 LayoutResult가 위에서부터 놓고,
        // 버튼은 맨 아래. 오른쪽 위 [도감] 버튼은 이 판 위에 그대로 보인다
        private void BuildResultPanel(Transform parent)
        {
            var panel = UiKit.Panel("ResultPanel", parent, Palette.Background);
            resultPanel = panel.gameObject;
            resultCard = UiKit.Rect("Card", panel.transform, 0, 1, 1, 1);
            resultCard.pivot = new Vector2(0.5f, 1);
            resultTitle = UiKit.Display(UiKit.OneLine(UiKit.Label("Title", resultCard, "", 120, Palette.Gold, 0, 1, 1, 1)));
            resultSubtitle = UiKit.OneLine(UiKit.Label("Subtitle", resultCard, "", 36, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleCenter,
                FontStyle.Normal, true, 22));

            // 성장 카드: 얼굴 · Lv · 레벨 업 · 경험치 막대
            growthCard = Card("Growth");
            growthFace = UiKit.IconImage("Face", growthCard, null, 0, 0.5f, 0, 0.5f);
            growthFace.rectTransform.pivot = new Vector2(0, 0.5f);
            growthFace.rectTransform.sizeDelta = new Vector2(140, 140);
            growthFace.rectTransform.anchoredPosition = new Vector2(28, 0);
            growthLevel = UiKit.Display(UiKit.OneLine(UiKit.Label("Level", growthCard, "", 52, Palette.Text, 0, 1, 1, 1, TextAnchor.MiddleLeft,
                FontStyle.Normal, true, 32)));
            PinTop(growthLevel.rectTransform, 192, 24, 214, 66);
            var pill = UiKit.Pill(UiKit.Panel("LevelUp", growthCard, Palette.Gold, 1, 1, 1, 1));
            pill.raycastTarget = false;
            pill.rectTransform.pivot = new Vector2(1, 1);
            pill.rectTransform.sizeDelta = new Vector2(174, 58);
            pill.rectTransform.anchoredPosition = new Vector2(-28, -28);
            UiKit.Display(UiKit.Label("Text", pill.transform, "레벨 업!", 34, Palette.OnAccent, 0, 0, 1, 1));
            levelUpPill = pill.gameObject;
            var track = UiKit.Pill(UiKit.Panel("ExpBar", growthCard, Palette.Track, 0, 1, 1, 1));
            track.raycastTarget = false;
            PinTop(track.rectTransform, 192, 104, 28, 28);
            var fill = UiKit.Pill(UiKit.Panel("Fill", track.transform, Palette.Gold, 0, 0, 0.5f, 1));
            fill.raycastTarget = false;
            expFill = fill.rectTransform;
            growthExp = UiKit.OneLine(UiKit.Label("Exp", growthCard, "", 34, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleLeft,
                FontStyle.Normal, true, 22));
            PinTop(growthExp.rectTransform, 192, 142, 28, 44);

            rewardRow = UiKit.Rect("Rewards", resultCard, 0, 1, 1, 1);

            // 가장 활약한 성유물
            mvpCard = Card("MVP");
            mvpIcon = UiKit.IconImage("Icon", mvpCard, null, 0, 0.5f, 0, 0.5f);
            mvpIcon.rectTransform.pivot = new Vector2(0, 0.5f);
            mvpIcon.rectTransform.sizeDelta = new Vector2(132, 132);
            mvpIcon.rectTransform.anchoredPosition = new Vector2(28, 0);
            mvpLabel = UiKit.OneLine(UiKit.Label("Label", mvpCard, "", 32, Palette.Gold, 0, 1, 1, 1, TextAnchor.MiddleLeft, FontStyle.Bold));
            PinTop(mvpLabel.rectTransform, 184, 22, 28, 42);
            mvpTitle = UiKit.Display(UiKit.OneLine(UiKit.Label("Title", mvpCard, "", 48, Palette.Text, 0, 1, 1, 1, TextAnchor.MiddleLeft,
                FontStyle.Normal, true, 28)));
            PinTop(mvpTitle.rectTransform, 184, 68, 28, 62);
            mvpDetail = UiKit.OneLine(UiKit.Label("Detail", mvpCard, "", 34, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleLeft,
                FontStyle.Normal, true, 22));
            PinTop(mvpDetail.rectTransform, 184, 134, 28, 44);

            statsRow = UiKit.Rect("Stats", resultCard, 0, 1, 1, 1);
            newWordsCard = WordCard("NewWords");
            wrongWordsCard = WordCard("WrongWords");
            dexBanners = UiKit.Rect("DexBanners", resultCard, 0, 1, 1, 1);

            resultBody = UiKit.OneLine(UiKit.Label("Body", resultCard, "", 34, Palette.TextDim, 0, 1, 1, 1, TextAnchor.MiddleCenter,
                FontStyle.Normal, true, 20));
            resultPrimary = UiKit.MakeButton("ResultButton_Primary", panel.transform, "", Palette.Button, 54, 0, 0, 1, 0, bestFit: true);
            resultPrimary.onClick.AddListener(() => resultChoice = 0);
            resultSecondary = UiKit.MakeButton("ResultButton_Secondary", panel.transform, "", Palette.Heal, 54, 0, 0, 1, 0, bestFit: true);
            resultSecondary.onClick.AddListener(() => resultChoice = 1);
            PinBottom((RectTransform)resultPrimary.transform, 0f, 1f);
            PinBottom((RectTransform)resultSecondary.transform, 0.5f, 1f);
        }

        private RectTransform Card(string name)
        {
            var card = UiKit.RoundPanel(name, resultCard, Palette.PanelLight, UiKit.RadiusMd, 0, 1, 1, 1);
            card.raycastTarget = false;
            return card.rectTransform;
        }

        private RectTransform WordCard(string name)
        {
            var card = Card(name);
            var header = UiKit.OneLine(UiKit.Label("Header", card, "", 36, Palette.Gold, 0, 1, 1, 1, TextAnchor.MiddleLeft, FontStyle.Bold,
                true, 22));
            PinTop(header.rectTransform, 28, 20, 28, 52);
            return card;
        }
    }
}
