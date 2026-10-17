using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
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
    // 필드 > HUD: 버튼·배지·[확인]·동료 배지, 튜토리얼 안내, 메뉴 빨간 점, 목표 한 줄, 창 열기(지도·가방·설정·도감), 토스트
    public partial class FieldScreen
    {
        // ------------------------------------------------------------------ HUD

        private void BuildHud()
        {
            UiKit.EnsureEventSystem();
            var canvasGo = new GameObject("FieldHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            hudCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            hudRoot = UiKit.Stretch("SafeArea", canvasGo.transform);
            UiKit.ApplySafeArea(hudRoot);

            // 오브젝트 이름표는 맨 아래 층 (상단 바·패드·창이 그 위를 덮는다)
            nameTags = FieldNameTags.Create(hudRoot);
            bubble = SpeechBubble.Create(hudRoot); // 이름표 위, 상단 바·메뉴·창 아래

            // 상단: 지역 이름 · 도감 진행 · 골드
            var top = UiKit.Panel("TopBar", hudRoot, Palette.Scrim, 0, 0.94f, 1, 1);
            top.raycastTarget = false;
            areaLabel = UiKit.Display(UiKit.Label("Area", top.transform, "", 44, Palette.Gold, 0.03f, 0, 0.4f, 1,
                TextAnchor.MiddleLeft, FontStyle.Normal, true, 24));
            dexLabel = UiKit.Label("DexProgress", top.transform, "", 30, Palette.Text, 0.4f, 0, 0.97f, 1,
                TextAnchor.MiddleRight);

            // 왼쪽 위 미니맵 (누르면 큰 지도). 맵 데이터에서 그리므로 지역이 늘어나도 자동
            minimap = MinimapView.Create(hudRoot);
            minimap.Button.onClick.AddListener(OpenMap);

            // 오른쪽 세로 메뉴 (Figma 'Field — 수련 (#44)'): 도감 · 수련 · 가방 · 설정
            var entries = new (string name, string icon, string label, UnityEngine.Events.UnityAction open)[]
            {
                ("DexButton", "dex", "도감", OpenDex),
                ("TrainingButton", "train", "수련", OpenTraining),
                ("BagButton", "bag", "가방", OpenBag),
                ("SettingsButton", "settings", "설정", OpenSettings),
            };
            float menuHeight = entries.Length * 120 + (entries.Length - 1) * 16;
            var menu = UiKit.Rect("Menu", hudRoot, 1, 0.865f, 1, 0.865f);
            menuRect = menu;
            menu.pivot = new Vector2(1, 1);
            menu.sizeDelta = new Vector2(120, menuHeight);
            menu.anchoredPosition = new Vector2(-24, 0);
            var badgeOf = new Dictionary<string, string>
                { ["DexButton"] = MenuBadges.Dex, ["TrainingButton"] = MenuBadges.Training, ["BagButton"] = MenuBadges.Bag };
            for (int i = 0; i < entries.Length; i++)
            {
                float topY = 1f - i * (136f / menuHeight);
                var button = UiKit.IconButton(entries[i].name, menu, entries[i].icon, entries[i].label, Palette.Scrim,
                    0, topY - 120f / menuHeight, 1, topY);
                button.onClick.AddListener(entries[i].open);
                if (badgeOf.TryGetValue(entries[i].name, out var badgeId)) AddBadge(badgeId, (RectTransform)button.transform);
                if (entries[i].name == "BagButton") AddBadge(MenuBadges.Companion, (RectTransform)button.transform); // 같은 자리의 점 (#81)
            }
            AddBadge(MenuBadges.Map, (RectTransform)minimap.Button.transform);

            // 목표 한 줄 (#61): 미니맵 바로 아래. 누르면 큰 지도 (목표 칸에 별)
            var goal = UiKit.Pill(UiKit.Panel("Objective", hudRoot, Palette.Scrim, 0, 0.865f, 0, 0.865f));
            objectivePill = goal.rectTransform;
            objectivePill.pivot = new Vector2(0, 1);
            UiKit.AddButton(goal).onClick.AddListener(OpenMap);
            var star = UiKit.Label("Star", objectivePill, "★", 32, Palette.Gold, 0, 0, 0, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            star.rectTransform.pivot = new Vector2(0, 0.5f);
            star.rectTransform.sizeDelta = new Vector2(40, 0);
            star.rectTransform.anchoredPosition = new Vector2(18, 0);
            objectiveText = UiKit.OneLine(UiKit.Label("Text", objectivePill, "", 28, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleLeft,
                FontStyle.Bold));
            objectiveText.rectTransform.offsetMin = new Vector2(62, 0);
            objectiveText.rectTransform.offsetMax = new Vector2(-22, 0);

            // 저장될 때 잠깐 (#61): 메뉴 아래 작은 알약
            var mark = UiKit.Pill(UiKit.Panel("SaveMark", hudRoot, Palette.Scrim, 1, 0.865f, 1, 0.865f));
            mark.raycastTarget = false;
            mark.rectTransform.pivot = new Vector2(1, 1);
            mark.rectTransform.sizeDelta = new Vector2(150, 56);
            mark.rectTransform.anchoredPosition = new Vector2(-24, -(menuHeight + 16));
            UiKit.Label("Text", mark.transform, "✓ 저장", 26, Palette.Good, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold);
            saveMark = mark.gameObject;
            saveMark.SetActive(false);

            // 주인공 배지 + 끼운 성유물 3칸 (Figma 'Field — HUD (주인공·성유물)' / 'Hero Badge' / 'Relic Slot')
            var strip = UiKit.Rect("HeroStrip", hudRoot, 0, 0.875f, 1, 0.935f);
            var badge = UiKit.RoundPanel("HeroBadge", strip, Palette.Scrim, UiKit.RadiusMd, 0.012f, 0, 0.575f, 1);
            heroBadge = badge;
            UiKit.AddButton(badge).onClick.AddListener(() => OpenBag(BagTab.Hero)); // 누르면 가방 > 주인공
            var avatar = UiKit.Pill(UiKit.Panel("Avatar", badge.transform, Palette.PanelLight, 0, 0.5f, 0, 0.5f));
            avatar.raycastTarget = false;
            avatar.rectTransform.pivot = new Vector2(0, 0.5f);
            avatar.rectTransform.sizeDelta = new Vector2(76, 76);
            avatar.rectTransform.anchoredPosition = new Vector2(12, 0);
            UiKit.IconImage("Face", avatar.transform, UiKit.HeroPortrait(session.Hero.Data), 0.1f, 0.1f, 0.9f, 0.9f);
            heroName = UiKit.Label("Name", badge.transform, "", 30, Palette.Text, 0, 0.45f, 0.62f, 0.95f,
                TextAnchor.MiddleLeft, FontStyle.Bold, true, 18);
            heroName.rectTransform.offsetMin = new Vector2(104, 0);
            heroHp = UiKit.OneLine(UiKit.Label("Hp", badge.transform, "", 24, Palette.TextDim, 0.62f, 0.45f, 0.97f, 0.95f,
                TextAnchor.MiddleRight));
            var hpBack = UiKit.Pill(UiKit.Panel("HpBack", badge.transform, Palette.Track, 0, 0.16f, 0.97f, 0.36f));
            hpBack.raycastTarget = false;
            hpBack.rectTransform.offsetMin = new Vector2(104, 0);
            heroHpFill = UiKit.Pill(UiKit.Panel("HpFill", hpBack.transform, Palette.Good));
            heroHpFill.raycastTarget = false;
            BuildCompanionBadge(strip);
            for (int i = 0; i < HeroData.SlotCount; i++)
            {
                var back = UiKit.RoundPanel($"RelicSlot_{i}", strip, Palette.PanelLight, UiKit.RadiusMd, 0, 0.5f, 0, 0.5f);
                back.rectTransform.pivot = new Vector2(0, 0.5f);
                back.rectTransform.sizeDelta = new Vector2(104, 104);
                back.rectTransform.anchoredPosition = new Vector2(640 + i * 116, 0);
                int slot = i;
                UiKit.AddButton(back).onClick.AddListener(() => OpenBag(BagTab.Relics, slot)); // 누르면 가방 > 성유물
                var icon = UiKit.IconImage("Icon", back.transform, null, 0.12f, 0.12f, 0.88f, 0.88f);
                var levelBack = UiKit.Pill(UiKit.Panel("Level", back.transform, Palette.Gold, 1, 0, 1, 0));
                levelBack.raycastTarget = false;
                levelBack.rectTransform.pivot = new Vector2(1, 0);
                levelBack.rectTransform.sizeDelta = new Vector2(52, 30);
                levelBack.rectTransform.anchoredPosition = new Vector2(-4, 4);
                var level = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", levelBack.transform, "", 24, Palette.OnAccent, 0, 0, 1, 1)));
                var empty = UiKit.Label("Empty", back.transform, "빈 칸", 22, Palette.TextDim, 0, 0, 1, 1);
                relicSlots.Add((back, icon, level, empty));
            }

            // 알림
            var toast = UiKit.RoundPanel("Toast", hudRoot, Palette.Scrim, UiKit.RadiusMd, 0.06f, 0.29f, 0.94f, 0.37f);
            toast.raycastTarget = false;
            toastPanel = toast.gameObject;
            toastText = UiKit.Label("Text", toast.transform, "", 34, Palette.Text, 0, 0, 1, 1,
                TextAnchor.MiddleCenter, FontStyle.Normal, true, 20);
            UiKit.Pad(toastText.rectTransform, 20, 6, 20, 6);
            toastPanel.SetActive(false);

            // 가상 스틱 (Figma 'Virtual Stick' #34): 오른쪽 아래 아무 곳이나 엄지를 대면 그 자리에 생김, 끝까지 밀면 달리기
            stick = VirtualStick.Create(hudRoot, 0.4f, 0f, 1f, 0.27f);
            // [확인]은 왼쪽 아래로 따로 (스틱을 밀면서 다른 손가락으로 누를 수 있게)
            BuildConfirmButton(UiKit.Rect("ConfirmArea", hudRoot, 0.02f, 0.03f, 0.36f, 0.24f));
            // [선제공격] (#73 → #74 QTE): 쫓아오는 몬스터가 가까우면 몬스터에 빨간 조준 괄호, [확인] 바로 위에 둥근 버튼 (왼손 엄지로)
            markLayer = UiKit.Stretch("RoamerMarks", hudRoot);
            preemptLock = TargetLock.Create(markLayer, Palette.Bad, false);
            preemptLock.SetActive(false);
            preemptRange = UiRing.Create("PreemptRange", markLayer, Palette.Gold, 5f, 100f, 28);
            preemptRange.gameObject.SetActive(false);
            BuildPreemptPrompt(UiKit.Rect("PreemptPrompt", hudRoot, 0.19f, 0.3f, 0.19f, 0.3f));

            // 조우 연출용 번쩍임
            flash = UiKit.Panel("EncounterFlash", hudRoot, Color.clear);
            flash.raycastTarget = false;
            flash.gameObject.SetActive(false);

            dexView = DexView.Create(hudRoot);
            trainingView = TrainingView.Create(hudRoot, StartTraining);
            altarView = RelicAltarView.Create(hudRoot, animationScale);
            shopView = ShopView.Create(hudRoot);
            inventoryView = InventoryView.Create(hudRoot);
            settingsView = SettingsView.Create(hudRoot);
            mapView = MapView.Create(hudRoot);
            learnView = SkillLearnView.Create(hudRoot);
            quitDialog = ConfirmDialog.Create(hudRoot);
            returnDialog = ConfirmDialog.Create(hudRoot);
            paywall = PaywallView.Create(hudRoot);
            companionCutscene = CompanionCutscene.Create(hudRoot);
            tabletView = TabletQuizView.Create(hudRoot);
            stoneView = GlowStoneQuizView.Create(hudRoot); // 글자 반딧돌 문제 (#113)
            pauseTutorial = PauseTutorial.Create(hudRoot);
            preemptView = PreemptView.Create(hudRoot);
            guide = GuideHints.Create(transform); // 자기 캔버스로 HUD·전투 위에 (눌림은 받지 않음)
        }

        // 왼쪽 아래 [확인] (Figma 'Action Button'): 옆에 쓸 것이 있으면 금색(Ready), 없으면 반투명(Idle)
        private void BuildConfirmButton(RectTransform area)
        {
            confirmGlow = UiKit.IconImage("ConfirmGlow", area, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            confirmGlow.rectTransform.sizeDelta = new Vector2(300, 300);
            confirmGlow.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.5f);
            confirmImage = UiKit.Pill(UiKit.Panel("Pad_Confirm", area, Color.white, 0.5f, 0.5f, 0.5f, 0.5f));
            confirmImage.rectTransform.sizeDelta = new Vector2(180, 180);
            UiKit.AddButton(confirmImage).onClick.AddListener(() => confirmRequested = true);
            confirmRing = UiKit.Outline(UiKit.Panel("Ring", confirmImage.transform, new Color(1f, 1f, 1f, 0.9f)), 32, 2);
            confirmRing.gameObject.AddComponent<AutoPill>();
            confirmRing.raycastTarget = false;
            confirmLabel = UiKit.Display(UiKit.Label("Label", confirmImage.transform, "확인", 52, Palette.Text, 0, 0, 1, 1));
            SetConfirmReady(false);
        }

        // 둥근 [선제공격!] (Figma 'Preempt Prompt' 264×284): 빨간 원 196 + 금테 + 칼 아이콘 + '선제공격!' 알약, 바깥 고리가 퍼지며 맥박
        private void BuildPreemptPrompt(RectTransform area)
        {
            area.sizeDelta = new Vector2(264, 284);
            preemptPrompt = area.gameObject;
            var glow = UiKit.IconImage("Glow", area, UiKit.GlowSprite(), 0.5f, 0.5f, 0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(320, 320);
            glow.rectTransform.anchoredPosition = new Vector2(0, 18);
            glow.color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.45f);
            preemptRing = UiRing.Create("WindowRing", area, Palette.Gold, 6f, 300f);
            preemptRing.rectTransform.anchoredPosition = new Vector2(0, 18);
            var core = UiKit.Pill(UiKit.Panel("PreemptButton", area, Palette.Attack, 0.5f, 0.5f, 0.5f, 0.5f));
            core.rectTransform.sizeDelta = new Vector2(196, 196);
            core.rectTransform.anchoredPosition = new Vector2(0, 18);
            preemptButton = UiKit.AddButton(core);
            preemptButton.onClick.AddListener(StartPreempt);
            var rim = UiRing.Create("Rim", core.transform, Palette.Gold, 6f, 196f);
            rim.rectTransform.anchorMin = Vector2.zero;
            rim.rectTransform.anchorMax = Vector2.one;
            rim.rectTransform.sizeDelta = Vector2.zero;
            var icon = UiKit.IconImage("Icon", core.transform, UiKit.Icon("skill_attack"), 0.5f, 0.5f, 0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(92, 92);
            icon.rectTransform.anchoredPosition = new Vector2(0, 16);

            var label = UiKit.Pill(UiKit.Panel("Label", area, Palette.Attack, 0.5f, 0.5f, 0.5f, 0.5f));
            label.raycastTarget = false;
            var text = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", label.transform, "선제공격!", 42, Palette.Text, 0, 0, 1, 1)));
            label.rectTransform.sizeDelta = new Vector2(text.preferredWidth + 56, 58);
            label.rectTransform.anchoredPosition = new Vector2(0, -94);
            var edge = UiKit.Outline(UiKit.Panel("Edge", label.transform, Palette.Gold), 32, 4);
            edge.gameObject.AddComponent<AutoPill>();
            edge.raycastTarget = false;
            preemptPrompt.SetActive(false);
        }

        // 동료 배지 (#77, Figma 'Companion Badge' 168×92): 주인공 배지(줄어듦) 오른쪽 · 성유물 칸 왼쪽. 누르면 가방 > 동료
        private void BuildCompanionBadge(RectTransform strip)
        {
            companionBadge = UiKit.RoundPanel("CompanionBadge", strip, Palette.Scrim, UiKit.RadiusMd, 0.41f, 0, 0.575f, 1);
            UiKit.AddButton(companionBadge).onClick.AddListener(() => OpenBag(BagTab.Companion));
            var avatar = UiKit.Pill(UiKit.Panel("Avatar", companionBadge.transform, Palette.PanelLight, 0, 0.5f, 0, 0.5f));
            avatar.raycastTarget = false;
            avatar.rectTransform.pivot = new Vector2(0, 0.5f);
            avatar.rectTransform.sizeDelta = new Vector2(64, 64);
            avatar.rectTransform.anchoredPosition = new Vector2(10, 0);
            companionFace = UiKit.IconImage("Face", avatar.transform, null, 0.08f, 0.08f, 0.92f, 0.92f);
            companionName = UiKit.Label("Name", companionBadge.transform, "", 24, Palette.Text, 0, 0.45f, 1, 0.95f,
                TextAnchor.MiddleLeft, FontStyle.Bold, true, 16);
            companionName.rectTransform.offsetMin = new Vector2(82, 0);
            companionName.rectTransform.offsetMax = new Vector2(-8, 0);
            var back = UiKit.Pill(UiKit.Panel("HpBack", companionBadge.transform, Palette.Track, 0, 0.2f, 1, 0.36f));
            back.raycastTarget = false;
            back.rectTransform.offsetMin = new Vector2(82, 0);
            back.rectTransform.offsetMax = new Vector2(-12, 0);
            companionHpFill = UiKit.Pill(UiKit.Panel("HpFill", back.transform, Palette.Good));
            companionHpFill.raycastTarget = false;
            companionBadge.gameObject.SetActive(false);
        }

        private void RefreshCompanionBadge()
        {
            var companion = session.PartyCompanion;
            if (walker != null) follower.SetCompanion(companion?.Data, player.position, walker.Facing, CanFollowerStand);
            companionBadge.gameObject.SetActive(companion != null);
            heroBadge.rectTransform.anchorMax = new Vector2(companion != null ? 0.4f : 0.575f, 1f);
            if (companion == null) return;
            companionFace.sprite = UiKit.CompanionPortrait(companion.Data);
            companionFace.enabled = companionFace.sprite != null;
            companionName.text = $"{companion.DisplayName} Lv{companion.Level}";
            float ratio = Mathf.Clamp01((float)companion.CurrentHp / Mathf.Max(1, companion.Stats.MaxHp));
            companionHpFill.enabled = ratio > 0f;
            companionHpFill.rectTransform.anchorMax = new Vector2(ratio, 1);
            companionHpFill.color = ratio > 0.5f ? Palette.Good : ratio > 0.25f ? Palette.Gold : Palette.Bad;
        }

        // 동료를 만난다 (#77): 조우 연출([함께 가기]까지) → 파티에 → 저장·HUD. 메아리 정글 유적 제단에서 부른다
        public IEnumerator MeetCompanion(CompanionData companion)
        {
            if (companion == null || session.HasCompanion(companion)) yield break;
            if (moving) SnapPlayer();
            yield return companionCutscene.Play(companion, Mathf.Max(0.05f, animationScale));
            session.AddCompanion(companion);
            SaveNow();
            RefreshHud();
            ShowToast($"{UiKit.WithJosa(companion.DisplayName, "이", "가")} 파티에 들어왔다!", 2.5f);
        }

        private void UpdateConfirmButton()
        {
            bool ready = FieldInteraction.FindTarget(walker.Map, walker.Position, walker.Facing, IsClearedBossSpot).HasValue;
            if (ready != confirmReady) SetConfirmReady(ready);

            // Ready면 숨 쉬듯 살짝, 부딪힌 직후에는 크게 깜빡
            float scale = 1f;
            if (confirmHint > 0f)
            {
                confirmHint -= Time.unscaledDeltaTime;
                scale = 1f + Mathf.Abs(Mathf.Sin(confirmHint * 12f)) * 0.14f;
            }
            else if (ready) scale = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
            confirmImage.rectTransform.localScale = Vector3.one * scale;
        }

        private void SetConfirmReady(bool ready)
        {
            confirmReady = ready;
            confirmImage.color = ready ? Palette.Gold : new Color(1f, 1f, 1f, 0.22f);
            confirmLabel.color = ready ? Palette.OnAccent : new Color(Palette.Text.r, Palette.Text.g, Palette.Text.b, 0.85f);
            confirmRing.enabled = ready;
            confirmGlow.enabled = ready;
        }

        // 가까운 오브젝트 이름표만 보이게 (쓰러뜨린 보스는 감춤)
        // 말풍선이 떠 있는 동안은 이름표를 숨긴다 (말풍선과 겹치지 않게)
        private void RefreshNameTags() =>
            nameTags.Refresh(walker.Position, cell => bubble.IsVisible || IsCleared(area, cell)
                || walker.Map.Get(cell) == FieldTile.Key && BossGate.HasKey(area, session));

        // 설정 > [튜토리얼 다시 보기]: 본 안내·빨간 점을 지우고 걷기 안내부터 다시 (전투·[확인] 안내도 그 상황에서 다시)
        private void ReplayTutorial()
        {
            session.Tutorials.Clear();
            SaveNow();
            RefreshHud();
            if (!fieldTutorialRunning) StartCoroutine(FieldGuide());
        }

        // 처음 필드에 들어오면 (#61, 사용자 피드백 '글이 너무 많다'): 스틱 받침 위에서 손끝이 끄는 모습 + 바로 위에 한 줄
        // (#65 기기 확인: 넓은 스틱 영역 기준이라 안내가 너무 멀었음 → 받침 기준) → 두 칸 걸어 보면 →
        // 끝까지 미는 모습 + 한 줄 → 달려 보면 끝. 화면을 막지 않고, 그동안만 몬스터가 나오지 않는다.
        // 메뉴·지도·저장 설명은 없앴다 — 빨간 점·목표 한 줄·'✓ 저장'이 그 자리에서 알려 준다
        private IEnumerator FieldGuide()
        {
            yield return null; // 화면 배치가 끝난 뒤
            fieldTutorialRunning = true;
            int from = stepsTaken;
            guide.Show(new GuideHints.Hint
            {
                Text = "여기를 끌어서 이동", Target = () => stick.Base, Gesture = GuideHints.Gesture.Drag, Drag = new Vector2(150, 0)
            });
            while (stepsTaken - from < 2) yield return null;
            guide.Show(new GuideHints.Hint
            {
                Text = "끝까지 밀면 달리기", Target = () => stick.Base, Gesture = GuideHints.Gesture.Drag, Drag = new Vector2(240, 0)
            });
            while (!(IsRunning || (stick.IsHeld && StickInput.IsRunning(stick.Value)))) yield return null;
            guide.Hide();
            session.Tutorials.Mark(TutorialProgress.Field);
            SaveNow();
            fieldTutorialRunning = false;
        }

        // 그 상황을 처음 만났을 때만 (#61): 물건 옆에 서면 [확인]을 톡톡 누르는 손끝 + 한 줄. 써 보면 끝, 멀어지면 다음에 다시
        private void UpdateGuide()
        {
            if (!showTutorials || guide.IsShowing || fieldTutorialRunning) return;
            if (confirmReady && !session.Tutorials.Has(TutorialProgress.Confirm))
            {
                int before = interactions;
                StartCoroutine(guide.Run(session, TutorialProgress.Confirm, new GuideHints.Hint
                {
                    Text = "[확인]을 눌러 보자", Target = () => confirmImage.rectTransform, Gesture = GuideHints.Gesture.Tap,
                    DoneWhen = () => interactions > before
                }, SaveNow, () => !confirmReady && interactions == before));
            }
        }

        // 풀숲 가까이 처음 오면 주인공 혼잣말 (#61) — 걸어도 잠깐 남아 있음
        private void MaybeSayGrassLine()
        {
            if (!showTutorials || fieldTutorialRunning || roaming != null || session.Tutorials.Has(TutorialProgress.Grass) || bubble.IsVisible) return;
            if (!GrassNearby(2)) return;
            session.Tutorials.Mark(TutorialProgress.Grass);
            bubble.Say("풀숲에서 뭔가 나올 것 같아…", 2.6f);
            bubbleKeepUntil = Time.unscaledTime + 2.6f;
            SaveNow();
        }

        private bool GrassNearby(int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                if (walker.Map.Get(walker.Position + new Vector2Int(dx, dy)) == FieldTile.Grass) return true;
            return false;
        }

        // ------------------------------------------------------------------ 빨간 점 · 목표 한 줄 · 저장 표시 (#61)

        private void AddBadge(string id, RectTransform host)
        {
            var dot = UiKit.Pill(UiKit.Panel("Badge", host, Palette.Bad, 1, 1, 1, 1));
            dot.raycastTarget = false;
            var rt = dot.rectTransform;
            rt.sizeDelta = new Vector2(30, 30);
            rt.anchoredPosition = new Vector2(-8, -8);
            var ring = UiKit.Outline(UiKit.Panel("Ring", rt, Color.white), 15, 3);
            ring.gameObject.AddComponent<AutoPill>();
            ring.raycastTarget = false;
            rt.gameObject.SetActive(false);
            badges[id] = rt;
        }

        private void RefreshBadges()
        {
            foreach (var pair in badges)
                pair.Value.gameObject.SetActive(showTutorials && MenuBadges.ShouldShow(pair.Key, session));
        }

        // 그 메뉴를 열면 점이 꺼진다 (점이 켜져 있었을 때만 — 아직 의미가 없을 때 열어 본 것은 세지 않음)
        private void ClearBadge(string id)
        {
            if (!IsBadgeVisible(id)) return;
            session.Tutorials.Mark(id);
            SaveNow();
            RefreshBadges();
        }

        // 2초마다 한 번 톡 커졌다 돌아온다
        private void PulseBadges()
        {
            float k = Time.unscaledTime % 2f;
            float scale = k < 0.3f ? 1f + 0.35f * Mathf.Sin(k / 0.3f * Mathf.PI) : 1f;
            foreach (var dot in badges.Values)
                if (dot.gameObject.activeSelf) dot.localScale = Vector3.one * scale;
        }

        private void RefreshObjective()
        {
            objective = Objectives.Current(session, area);
            objectiveText.text = ObjectiveLine(objective);
            objectivePill.sizeDelta = new Vector2(Mathf.Clamp(objectiveText.preferredWidth + 88f, 200f, 700f), 60f);
            objectivePill.anchoredPosition = new Vector2(24, -(minimap.Height + 12f));
            minimap.SetGoal(objective.Cell);
        }

        private string ObjectiveLine(Objective goal)
        {
            switch (goal.Kind)
            {
                case ObjectiveKind.FirstBattle:
                    return "풀숲에서 몬스터를 만나 보자";
                case ObjectiveKind.GoToArea:
                    string next = goal.Next.DisplayName, destination = goal.Destination.DisplayName;
                    return goal.Next == goal.Destination
                        ? $"{UiKit.WithJosa(next, "으로", "로")} 가자"
                        : $"{UiKit.WithJosa(next, "을", "를")} 지나 {UiKit.WithJosa(destination, "으로", "로")}";
                case ObjectiveKind.DefeatBoss:
                    return $"보스 {UiKit.WithJosa(goal.Destination.Boss.Species.DisplayName, "을", "를")} 물리치자";
                case ObjectiveKind.FindKey:
                    return "보스 방 열쇠를 찾자";
                case ObjectiveKind.GatherWords:
                    return $"새 단어로 힘을 모으자 {BossGate.WordsFound(area, session)}/{area.BossGateWords}";
                case ObjectiveKind.OpenGate:
                    return "보스 방 문을 열자";
                case ObjectiveKind.SolveTablets:
                    return $"유적의 석판을 채우자 {Ruins.TabletsSolved(area, session.World)}/{Ruins.Tablets(area).Count}";
                case ObjectiveKind.GatherEcho:
                    return $"단어를 모아 유적의 메아리를 깨우자 {BossGate.WordsFound(area, session)}/{area.ShrineWords}";
                case ObjectiveKind.WakeCompanion:
                    return "유적 제단에서 동료를 깨우자";
                case ObjectiveKind.LightStones:
                    return $"반딧돌을 찾아 불을 밝혀 보자 {GlowStones.LitCount(area, session.World)}/{GlowStones.Stones(area).Count}";
                default:
                    var progress = Dex.GetProgress(area.Words, session.Vocabulary);
                    return $"도감 채우기 {progress.Discovered}/{progress.Total}";
            }
        }

        private void OpenMap()
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Map);
            mapView.Show(area, minimap.Texture, walker.Position, session, objective.Cell);
        }

        private void OpenBag() => OpenBag(BagTab.Hero);

        // tab: 열 탭, slot: 성유물 탭에서 고를 칸 (그 칸에 끼운 성유물)
        private void OpenBag(BagTab tab, int slot = -1)
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Bag);
            ClearBadge(MenuBadges.Companion);
            var relic = session.Hero.SlotAt(slot);
            inventoryView.Show(session, database, tab, relic, OnTownChanged);
        }

        private void OpenSettings()
        {
            if (!CanOpenMenu) return;
            var full = FullVersionArea();
            settingsView.Show(settings, saveSettings, deleteSave, ReplayTutorial, new SettingsView.PurchaseHooks
            {
                Store = store, Owned = entitlements, OpenOffer = full != null ? () => OpenFullVersionOffer(full) : (Action)null
            });
        }

        private void OpenDex()
        {
            if (!CanOpenMenu) return;
            ClearBadge(MenuBadges.Dex);
            var completed = session.ClaimDexRewards(new[] { area.Words });
            if (completed.Count > 0)
            {
                SaveNow();
                ShowToast($"★ {area.Words.RegionName} 도감 완성! 보상을 받았다", 3f);
            }
            dexView.Show(area.Words, session, DateTime.UtcNow);
        }

        private void RefreshHud()
        {
            areaLabel.text = area.DisplayName;
            var progress = Dex.GetProgress(area.Words, session.Vocabulary);
            dexLabel.text = $"발견 {progress.Discovered}/{progress.Total}   {session.Inventory.Gold}G";

            var hero = session.Hero;
            heroName.text = $"{hero.DisplayName} Lv{hero.Level}";
            heroHp.text = $"HP {hero.CurrentHp}/{hero.Stats.MaxHp}";
            float ratio = Mathf.Clamp01((float)hero.CurrentHp / Mathf.Max(1, hero.Stats.MaxHp));
            heroHpFill.enabled = ratio > 0f;
            heroHpFill.rectTransform.anchorMax = new Vector2(ratio, 1);
            heroHpFill.color = ratio > 0.5f ? Palette.Good : ratio > 0.25f ? Palette.Gold : Palette.Bad;
            RefreshCompanionBadge();
            for (int i = 0; i < relicSlots.Count; i++)
            {
                var relic = hero.SlotAt(i);
                var (back, icon, level, empty) = relicSlots[i];
                back.color = relic != null ? Palette.PanelLight : Palette.Track;
                icon.sprite = relic != null ? UiKit.RelicIcon(relic.Data) : null;
                icon.enabled = icon.sprite != null;
                level.transform.parent.gameObject.SetActive(relic != null);
                level.text = relic != null ? $"+{relic.Level}" : "";
                empty.enabled = relic == null;
            }
            RefreshBadges();
            RefreshObjective();
        }

        private void ShowToast(string message, float seconds = 2f)
        {
            toastText.text = message;
            toastPanel.SetActive(true);
            toastUntil = Time.unscaledTime + seconds;
        }

        private void HideToast() => toastPanel.SetActive(false);

        private void UpdateToast()
        {
            if (toastPanel.activeSelf && Time.unscaledTime >= toastUntil) toastPanel.SetActive(false);
        }
    }
}
