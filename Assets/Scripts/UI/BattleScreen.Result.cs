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
    // 전투 > 결산 화면: 승리·패배·수련 결과, MVP 기술·새 단어·틀린 단어·보상·도감 완성
    public partial class BattleScreen
    {
        private IEnumerator ShowResult()
        {
            var phase = engine.Phase;
            bool victory = phase == BattlePhase.Victory, fled = phase == BattlePhase.Fled;
            var hero = session.Hero;
            ResetFinisher();

            int levelBefore = hero.Level, levels = 0;
            BattleReward reward = null;
            companionGrowth = "";
            if (victory)
            {
                reward = engine.CalculateReward();
                levels = BattleRewardCalculator.Apply(reward, hero, session.Inventory);
                companionGrowth = GrowCompanion(reward);
            }
            var completions = session.ClaimDexRewards(new[] { words });
            // 결과 소리: 도감 완성 > 레벨 업 > 승리, 지면 패배 (도망치면 도망 소리를 이미 냈다)
            if (completions.Count > 0) Sound.Play(Sfx.DexComplete);
            else if (!fled) Sound.Play(levels > 0 ? Sfx.LevelUp : victory ? Sfx.Victory : Sfx.Defeat);
            if (fled) session.Record.RecordFled(engine.CorrectAnswers, engine.WrongAnswers);
            else session.Record.RecordBattle(victory, engine.CorrectAnswers, engine.WrongAnswers);
            saveProgress?.Invoke();

            bool goOn = victory || fled;
            if (loopBattles) SetResultButtons(goOn ? "다음 전투" : "회복 후 재도전", goOn ? "회복 후 전투" : null);
            else SetResultButtons(goOn ? "계속 탐험" : "시작 지점으로", null);
            UiKit.SetColor(resultPrimary, goOn ? Palette.Button : Palette.Neutral);

            // 버튼 위까지가 내용 칸. 넘치면 단어 줄을 하나씩 줄이고, 그래도 넘치면 통째로 줄인다
            relayoutResult = () => FitResult(rows => LayoutResult(phase, levelBefore, levels, reward, completions, rows));
            relayoutResult();

            resultChoice = -1;
            ShowPanel(resultPanel);
            while (resultChoice < 0) yield return null;
            relayoutResult = null;

            // 패배 후 재도전, 또는 '회복' 선택 시 완전 회복 (도망치면 줄어든 HP 그대로).
            // 이기거나 도망쳤으면 쓰러졌던 동료는 HP 1로 일어난다 (#77)
            if (phase == BattlePhase.Defeat || resultChoice == 1) session.RestoreHero();
            else session.AfterBattle();
            saveProgress?.Invoke();
        }

        private string companionGrowth = ""; // 결산 부제에 붙이는 동료 성장 한 줄 (#77)

        // 동료도 같은 경험치 (#77): 레벨이 오르면 "아라 레벨 업! Lv6", 새 기술을 배우면 "· '메아리 방패'를 배웠다"
        private string GrowCompanion(BattleReward reward)
        {
            var companion = session.PartyCompanion;
            if (companion == null) return "";
            int before = companion.Level;
            if (BattleRewardCalculator.ApplyTo(reward, companion) <= 0) return "";
            string line = $"{companion.DisplayName} 레벨 업! Lv{companion.Level}";
            foreach (var skill in companion.Data.SkillsLearnedBetween(before, companion.Level))
                line += $" · '{skill.DisplayName}'{UiKit.WithJosa(skill.DisplayName, "을", "를").Substring(skill.DisplayName.Length)} 배웠다";
            return line;
        }

        // 수련 결산 (Figma 'Battle — 수련 결산 (#44)'): 보상 없음. 준 피해 · 가장 활약한 성유물 · 기록 · 복습한 단어 · 틀린 단어
        private IEnumerator ShowTrainingResult()
        {
            ResetFinisher();
            Sound.Play(Sfx.Victory);
            saveProgress?.Invoke();
            SetResultButtons("돌아가기", null);
            UiKit.SetColor(resultPrimary, Palette.Button);
            relayoutResult = () => FitResult(LayoutTrainingResult);
            relayoutResult();

            resultChoice = -1;
            ShowPanel(resultPanel);
            while (resultChoice < 0) yield return null;
            relayoutResult = null;
        }

        // 버튼 위까지가 내용 칸. 넘치면 단어 줄을 하나씩 줄이고, 그래도 넘치면 통째로 줄인다
        private void FitResult(Func<int, float> layout)
        {
            resultLayoutHeight = ((RectTransform)resultPanel.transform).rect.height;
            float room = resultLayoutHeight - (ResultBottom + ResultButtonHeight + ResultGap);
            float height = 0f;
            for (int rows = MaxWordRows; rows >= 1; rows--)
            {
                ClearResultDynamic();
                height = layout(rows);
                if (height <= room) break;
            }
            resultCard.sizeDelta = new Vector2(0, height);
            resultCard.localScale = Vector3.one * (room > 0f && height > room ? room / height : 1f);
        }

        private float LayoutTrainingResult(int wordRows)
        {
            var summary = engine.Summary;
            float y = ResultTop;
            resultTitle.text = "수련 종료";
            resultTitle.color = Palette.Gold;
            PlaceResult(resultTitle.rectTransform, ref y, 140f, 0f);
            int answered = summary.Correct + summary.Wrong;
            resultSubtitle.text = $"허수아비에게 준 피해 {summary.TotalDamage}   ·   문제 {answered}개";
            PlaceResult(resultSubtitle.rectTransform, ref y, 52f, ResultGap);

            growthCard.gameObject.SetActive(false);
            rewardRow.gameObject.SetActive(false);
            dexBanners.gameObject.SetActive(false);
            PlaceMvpAndStats(ref y);

            FillWords(newWordsCard, summary.AnsweredWords, $"복습한 단어 {summary.AnsweredWords.Count}", Palette.Gold, wordRows, ref y);
            FillWords(wrongWordsCard, summary.WrongWords, $"틀린 단어 {summary.WrongWords.Count} — 오답 노트에 넣었어요, 곧 다시 나와요",
                Palette.Bad, wordRows, ref y);

            var (up, down) = MasteryUpDown();
            int wrongLeft = 0;
            foreach (var entry in session.Vocabulary.Entries)
                if (entry.InWrongNote) wrongLeft++;
            resultBody.text = $"단어 숙련도 ▲{up} ▼{down}   ·   오답 노트 남은 단어 {wrongLeft}";
            PlaceResult(resultBody.rectTransform, ref y, 48f, 0f);
            return y;
        }

        private (int up, int down) MasteryUpDown()
        {
            int up = 0, down = 0;
            foreach (var change in engine.MasteryChanges)
            {
                if (change.LeveledUp) up++;
                else if (change.LeveledDown) down++;
            }
            return (up, down);
        }

        // 가장 활약한 성유물 (피해를 가장 많이 준 기술의 출처) + 기록 알약 (정답 · 최대 콤보 · 크리티컬)
        private void PlaceMvpAndStats(ref float y)
        {
            var summary = engine.Summary;
            var top = summary.TopSkill;
            mvpCard.gameObject.SetActive(top != null);
            if (top != null)
            {
                FillMvp(top, summary.DamageOf(top), summary.TotalDamage);
                PlaceResult(mvpCard, ref y, 196f, ResultGap);
            }

            float px = 0f;
            int answered = summary.Correct + summary.Wrong;
            if (answered > 0) AddStat($"정답 {summary.Correct} / {answered}", ref px);
            if (summary.MaxCombo >= Combo.FirstStreak) AddStat($"최대 콤보 {summary.MaxCombo}", ref px);
            if (summary.Criticals > 0) AddStat($"크리티컬 {summary.Criticals}", ref px);
            statsRow.gameObject.SetActive(px > 0f);
            if (px > 0f) PlaceResult(statsRow, ref y, 62f, ResultGap);
        }

        private const int MaxWordRows = 4;

        private const float ResultTop = 140f;          // 오른쪽 위 [도감] 버튼 아래부터

        private const float ResultGap = 24f;

        private const float ResultButtonHeight = 118f, ResultBottom = 56f;

        // 결산 내용을 위에서부터 놓고 전체 높이를 돌려준다 (Figma 'Battle — 전투 결산 꽉 채움 (#42)')
        // 도망쳤으면 (#53, Figma 'Battle — 도망 결산 (#53)') 성장·보상 칸 없이 나머지는 같다
        private float LayoutResult(BattlePhase phase, int levelBefore, int levels, BattleReward reward,
            IReadOnlyList<DexCompletion> completions, int wordRows)
        {
            var summary = engine.Summary;
            var hero = session.Hero;
            bool victory = phase == BattlePhase.Victory, fled = phase == BattlePhase.Fled;
            float y = ResultTop;
            resultTitle.text = victory ? "승리!" : fled ? "도망쳤다" : "패배…";
            resultTitle.color = victory ? Palette.Gold : fled ? Palette.TextDim : Palette.Bad;
            PlaceResult(resultTitle.rectTransform, ref y, 140f, 0f);
            string fainted = UiKit.WithJosa(hero.DisplayName, "이", "가");
            resultSubtitle.text = victory ? (companionGrowth.Length > 0 ? $"{DefeatedLine()}\n{companionGrowth}" : DefeatedLine())
                : fled ? "무사히 빠져나왔다 — 이번 전투 보상은 없어요"
                : loopBattles ? $"{fainted} 쓰러졌다. 회복하고 다시 도전하자!" : $"{fainted} 쓰러졌다… 시작 지점으로 돌아간다.";
            PlaceResult(resultSubtitle.rectTransform, ref y, victory && companionGrowth.Length > 0 ? 100f : 52f, ResultGap); // 동료 성장 한 줄이 붙으면 두 줄

            growthCard.gameObject.SetActive(victory);
            rewardRow.gameObject.SetActive(victory);
            if (victory)
            {
                // 성장: 얼굴 · Lv(오르면 → 새 레벨 + '레벨 업!') · 경험치 막대
                growthFace.sprite = UiKit.HeroPortrait(hero.Data);
                growthFace.enabled = growthFace.sprite != null;
                growthLevel.text = levels > 0 ? $"{hero.DisplayName} Lv{levelBefore} → Lv{hero.Level}" : $"{hero.DisplayName} Lv{hero.Level}";
                levelUpPill.SetActive(levels > 0);
                int need = hero.IsMaxLevel ? 1 : LevelCurve.ExpToNextLevel(hero.Level);
                expFill.anchorMax = new Vector2(hero.IsMaxLevel ? 1f : Mathf.Clamp01(hero.Exp / (float)need), 1f);
                growthExp.text = hero.IsMaxLevel ? $"경험치 +{reward.Exp}   ·   최고 레벨" : $"경험치 +{reward.Exp}   ·   다음 레벨까지 {hero.ExpToNextLevel}";
                PlaceResult(growthCard, ref y, 200f, ResultGap);

                // 보상: 골드 + 재료
                float x = 0f;
                AddReward(UiKit.Icon("gold"), $"+{reward.Gold}G", ref x);
                foreach (var item in reward.Items) AddReward(UiKit.ItemIcon(item.Item), $"×{item.Count}", ref x);
                PlaceResult(rewardRow, ref y, 80f, ResultGap);
            }

            PlaceMvpAndStats(ref y);

            // 새로 만난 단어 · 틀린 단어 (틀린 단어는 정답 뜻과 함께 — 다시 보는 순간)
            FillWords(newWordsCard, summary.NewWords, $"새로 만난 단어 {summary.NewWords.Count}", Palette.Gold, wordRows, ref y);
            FillWords(wrongWordsCard, summary.WrongWords, $"틀린 단어 {summary.WrongWords.Count} — 오답 노트에 넣었어요, 곧 다시 나와요",
                Palette.Bad, wordRows, ref y);

            // 지역 도감을 다 채웠으면 징표 배너
            dexBanners.gameObject.SetActive(completions.Count > 0);
            if (completions.Count > 0)
            {
                float by = 0f;
                foreach (var completion in completions) AddDexBanner(completion, ref by);
                PlaceResult(dexBanners, ref y, by, ResultGap);
            }

            var (up, down) = MasteryUpDown();
            resultBody.text = $"발견한 단어 {LearnedCount()}/{words.Words.Count}   ·   단어 숙련도 ▲{up} ▼{down}";
            PlaceResult(resultBody.rectTransform, ref y, 48f, 0f);
            return y;
        }

        // "잉크 슬라임 · 낙서 박쥐를 물리쳤어요"
        private string DefeatedLine()
        {
            var names = new List<string>();
            foreach (var enemy in engine.Enemies)
                if (!names.Contains(enemy.DisplayName)) names.Add(enemy.DisplayName);
            if (names.Count == 0) return "";
            string last = UiKit.WithJosa(names[names.Count - 1], "을", "를");
            names[names.Count - 1] = last;
            return $"{string.Join(" · ", names)} 물리쳤어요";
        }

        // 피해를 가장 많이 준 기술의 출처: 성유물이면 성유물, 기술문서면 문서, 기본 기술이면 주인공
        private void FillMvp(SkillData skill, int damage, int total)
        {
            var hero = session.Hero;
            SkillSlot slot = null;
            foreach (var candidate in hero.SkillSlots)
                if (candidate.Skill == skill) slot = candidate;
            Sprite icon;
            if (slot?.Relic != null)
            {
                mvpLabel.text = "가장 활약한 성유물";
                mvpTitle.text = $"{slot.Relic.Data.DisplayName} +{slot.Relic.Level} — {skill.DisplayName}";
                icon = UiKit.RelicIcon(slot.Relic.Data);
            }
            else if (slot?.Document != null)
            {
                mvpLabel.text = "가장 활약한 기술";
                mvpTitle.text = $"{skill.DisplayName} (기술문서)";
                icon = UiKit.ItemIcon(slot.Document);
            }
            else
            {
                mvpLabel.text = "가장 활약한 기술";
                mvpTitle.text = $"{skill.DisplayName} (기본 기술)";
                icon = UiKit.HeroPortrait(hero.Data);
            }
            mvpIcon.sprite = icon;
            mvpIcon.enabled = icon != null;
            int percent = total > 0 ? Mathf.RoundToInt(100f * damage / total) : 100;
            mvpDetail.text = $"피해 {damage}   ·   이번 전투 피해의 {percent}%";
        }

        private void FillWords(RectTransform card, IReadOnlyList<WordEntry> list, string header, Color color, int maxRows, ref float y)
        {
            card.gameObject.SetActive(list.Count > 0);
            if (list.Count == 0) return;
            var title = card.Find("Header").GetComponent<Text>();
            title.text = header;
            title.color = color;
            float rowY = 82f;
            int shown = Mathf.Min(maxRows, list.Count);
            for (int i = 0; i < shown; i++)
            {
                var row = UiKit.Rect($"Word_{i}", card, 0, 1, 1, 1);
                PinTop(row, 28, rowY, 28, 56);
                var english = UiKit.Display(UiKit.OneLine(UiKit.Label("English", row, list[i].English, 46, Palette.Text, 0, 0, 0.38f, 1,
                    TextAnchor.MiddleLeft, FontStyle.Normal, true, 26)));
                english.rectTransform.offsetMax = new Vector2(-12, 0);
                UiKit.OneLine(UiKit.Label("Meaning", row, list[i].Meaning, 36, Palette.TextDim, 0.38f, 0, 1, 1, TextAnchor.MiddleLeft,
                    FontStyle.Normal, true, 22));
                resultDynamic.Add(row.gameObject);
                rowY += 60f;
            }
            if (list.Count > shown)
            {
                var more = UiKit.OneLine(UiKit.Label("More", card, $"외 {list.Count - shown}개 — 도감에서 볼 수 있어요", 30, Palette.TextDim,
                    0, 1, 1, 1, TextAnchor.MiddleLeft));
                PinTop(more.rectTransform, 28, rowY, 28, 44);
                resultDynamic.Add(more.gameObject);
                rowY += 48f;
            }
            PlaceResult(card, ref y, rowY + 18f, ResultGap);
        }

        private void ClearResultDynamic()
        {
            foreach (var go in resultDynamic)
            {
                if (go == null) continue;
                go.SetActive(false); // 레이아웃에서 즉시 빠지게
                Destroy(go);
            }
            resultDynamic.Clear();
        }

        // 보상 한 칸: [아이콘] 글자 (가로로 이어 붙임)
        private void AddReward(Sprite icon, string text, ref float x)
        {
            var cell = UiKit.Rect("Reward", rewardRow, 0, 0, 0, 1);
            cell.pivot = new Vector2(0, 0.5f);
            var image = UiKit.IconImage("Icon", cell, icon, 0, 0.5f, 0, 0.5f);
            image.rectTransform.pivot = new Vector2(0, 0.5f);
            image.rectTransform.sizeDelta = new Vector2(76, 76);
            var label = UiKit.Display(UiKit.OneLine(UiKit.Label("Text", cell, text, 44, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleLeft)));
            float iconWidth = icon != null ? 86 : 0;
            label.rectTransform.offsetMin = new Vector2(iconWidth, 0);
            float width = iconWidth + label.preferredWidth + 8;
            cell.sizeDelta = new Vector2(width, 0);
            cell.anchoredPosition = new Vector2(x, 0);
            x += width + 32f;
            resultDynamic.Add(cell.gameObject);
        }

        // 기록 알약 하나 (가로로 이어 붙임)
        private void AddStat(string text, ref float x)
        {
            var pill = UiKit.Pill(UiKit.Panel("Stat", statsRow, Palette.PanelLight, 0, 0, 0, 1));
            pill.raycastTarget = false;
            var rt = pill.rectTransform;
            rt.pivot = new Vector2(0, 0.5f);
            var label = UiKit.OneLine(UiKit.Label("Text", pill.transform, text, 34, Palette.Text, 0, 0, 1, 1, TextAnchor.MiddleCenter, FontStyle.Bold));
            float width = label.preferredWidth + 48;
            rt.sizeDelta = new Vector2(width, 0);
            rt.anchoredPosition = new Vector2(x, 0);
            x += width + 16f;
            resultDynamic.Add(pill.gameObject);
        }

        // 지역 도감을 다 채운 순간 결과 화면에 붙는 배너: 징표 아이콘 + "★ 초원 도감 완성!"
        private void AddDexBanner(DexCompletion completion, ref float y)
        {
            var row = UiKit.Rect("DexBanner", dexBanners, 0, 1, 1, 1);
            PinTop(row, 0, y, 0, 140);
            y += 152f;
            resultDynamic.Add(row.gameObject);
            UiKit.RoundPanel("Bg", row, Palette.PanelLight, UiKit.RadiusMd).raycastTarget = false;
            UiKit.Outline(UiKit.Panel("Border", row, Palette.Gold), UiKit.RadiusMd, 3).raycastTarget = false;
            var icon = UiKit.IconImage("Keepsake", row, UiKit.ItemIcon(completion.Keepsake), 0, 0.08f, 0, 0.92f);
            icon.rectTransform.pivot = new Vector2(0, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(104, 0);
            icon.rectTransform.anchoredPosition = new Vector2(16, 0);
            var headline = UiKit.Display(UiKit.Label("Headline", row, $"★ {completion.Region.RegionName} 도감 완성!", 44, Palette.Gold,
                0, 0.5f, 1, 0.94f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 24));
            headline.rectTransform.offsetMin = new Vector2(136, 0);
            headline.rectTransform.offsetMax = new Vector2(-16, 0);
            string keepsake = completion.Keepsake != null ? $"징표 '{completion.Keepsake.DisplayName}' + " : "";
            var detail = UiKit.Label("Detail", row, $"{keepsake}{completion.Gold} 골드 획득", 34, Palette.Text,
                0, 0.06f, 1, 0.5f, TextAnchor.MiddleLeft, FontStyle.Normal, true, 18);
            detail.rectTransform.offsetMin = new Vector2(136, 0);
            detail.rectTransform.offsetMax = new Vector2(-16, 0);
        }

        // ------------------------------------------------------------------ 도감 완성 · 결산 버튼

        private static string CompletionText(DexCompletion completion)
        {
            string keepsake = completion.Keepsake != null ? $"징표 '{completion.Keepsake.DisplayName}' + " : "";
            return $"★ {completion.Region.RegionName} 도감 완성!  {keepsake}{completion.Gold} 골드 획득";
        }

        private void OnDexClicked()
        {
            if (!dexButton.interactable) return;
            var completed = session.ClaimDexRewards(new[] { words });
            if (completed.Count > 0)
            {
                Log(CompletionText(completed[0]));
                saveProgress?.Invoke();
            }
            dexView.Show(words, session, DateTime.UtcNow);
        }

        private void SetResultButtons(string primary, string secondary)
        {
            UiKit.LabelOf(resultPrimary).text = primary;
            bool two = secondary != null;
            resultSecondary.gameObject.SetActive(two);
            if (two) UiKit.LabelOf(resultSecondary).text = secondary;
            // 맨 아래 (한 손으로 누르기 쉽게). 둘이면 반씩
            PinBottom((RectTransform)resultPrimary.transform, 0f, two ? 0.5f : 1f);
            PinBottom((RectTransform)resultSecondary.transform, 0.5f, 1f);
        }

        // 결산 판 안: 위에서 y만큼 내려와 높이 h (좌우 40 여백), 다음 y는 h + gap 아래
        private static void PlaceResult(RectTransform rect, ref float y, float h, float gap)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(40, -(y + h));
            rect.offsetMax = new Vector2(-40, -y);
            y += h + gap;
        }
    }
}
