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
    // 전투 > 고르기 메뉴: 기술·대상·상처약·강도(단어 n개)·도망, 기술 설명 글(다른 화면도 씀)
    public partial class BattleScreen
    {
        // ------------------------------------------------------------------ 스킬 메뉴 / 대상 선택

        private void ShowSkillMenu(BattleUnit actor)
        {
            itemMode = false;
            intensityMode = false;
            foreach (var button in intensityButtons) button.gameObject.SetActive(false);
            ShowPanel(skillPanel);
            skillTitle.text = TurnTitle(actor);
            skillTitle.color = Palette.Text;
            cancelButton.gameObject.SetActive(false);

            for (int i = 0; i < skillButtons.Count; i++)
            {
                bool used = i < actor.Skills.Count;
                skillButtons[i].gameObject.SetActive(used);
                if (!used) continue;

                var skill = actor.Skills[i];
                UiKit.LabelOf(skillButtons[i]).text = skill.DisplayName;
                skillDetails[i].text = SourcePrefix(actor, skill) + SkillDetail(skill);
                UiKit.SetColor(skillButtons[i], SkillColor(skill));
                skillButtons[i].interactable = true;
                skillButtons[i].onClick.RemoveAllListeners();
                skillButtons[i].onClick.AddListener(() => OnSkillClicked(skill));
            }

            // 상처약: 가진 개수, HP가 가득이면 잠금 (동료 차례에도 — 그 차례인 아군에게, #77)
            bool isPlayer = actor.IsPlayerSide;
            itemButton.gameObject.SetActive(isPlayer && !training);
            fleeButton.gameObject.SetActive(isPlayer && !training);
            trainingEndButton.gameObject.SetActive(training);
            if (isPlayer && !training)
            {
                int potions = 0;
                foreach (var item in HealingItems()) potions += session.Inventory.GetCount(item);
                bool full = actor.Hp >= actor.MaxHp;
                itemButton.interactable = potions > 0 && !full;
                UiKit.LabelOf(itemButton).text = potions == 0 ? "가방 — 상처약이 없어요" : full ? "가방 — HP가 가득해요" : $"가방 — 상처약 쓰기 ({potions}개)";
            }
        }

        private static string TurnTitle(BattleUnit actor) => $"{actor.DisplayName}의 차례 — 기술을 고르세요";

        // [도망가기]: 보통은 바로 전투가 끝나고(ChooseSkill이 끝남 → 도망 결산), 보스전이면 제목이 잠깐 빨간 '도망갈 수 없다!' + 버튼이 흔들림
        private void OnFleeClicked()
        {
            if (engine == null || engine.Phase != BattlePhase.ChoosingSkill || itemMode || intensityMode || targetingSkill != null) return;
            var events = engine.Flee();
            if (engine.Phase == BattlePhase.Fled)
            {
                fleeEvents = events;
                return;
            }
            if (engine.IsFleeQuiz) // 강한·보통(1/4) 몬스터: 단어 문제로 (ChooseSkill이 끝나고 PlayBattle이 문제를 냄, #84)
            {
                fleeQuizEvents = events;
                FleeQuizzes++;
                return;
            }
            FleeBlockedCount++;
            Sound.Play(Sfx.Fail);
            Log("보스에게서는 도망갈 수 없다!");
            if (cantFleeRoutine != null) StopCoroutine(cantFleeRoutine);
            cantFleeRoutine = StartCoroutine(CantFlee(engine.CurrentActor));
        }

        private IEnumerator CantFlee(BattleUnit actor)
        {
            var rt = (RectTransform)fleeButton.transform;
            skillTitle.text = CantFleeText;
            skillTitle.color = Palette.Bad;
            float shake = 0.35f, hold = Mathf.Max(1.5f * animationScale, 0.05f);
            for (float t = 0f; t < hold && skillTitle.text == CantFleeText; t += Time.unscaledDeltaTime)
            {
                float k = t < shake ? 1f - t / shake : 0f;
                rt.anchoredPosition = fleeButtonHome + new Vector2(Mathf.Sin(t * 70f) * 14f * k, 0f);
                yield return null;
            }
            rt.anchoredPosition = fleeButtonHome;
            skillTitle.color = Palette.Text;
            if (skillTitle.text == CantFleeText) skillTitle.text = TurnTitle(actor);
            cantFleeRoutine = null;
        }

        // "깃펜 · " (성유물 기술) / "기본 · " (기본 기술) / "아라 · " (동료가 배운 기술, #77). 적이면 빈 글자
        private static string SourcePrefix(BattleUnit actor, SkillData skill)
        {
            if (actor.Companion != null) return actor.Companion.Data.BasicSkill == skill ? "기본 · " : $"{actor.Companion.DisplayName} · ";
            if (actor.Hero == null) return "";
            var source = actor.Hero.SourceOf(skill);
            return source != null ? $"{source.SourceName} · " : "기본 · ";
        }

        // 가방에 있는 회복 아이템 (상처약 등). 소지품은 id만 있어서 GameDatabase로 찾는다
        private List<ItemData> HealingItems()
        {
            var list = new List<ItemData>();
            if (database == null) return list;
            foreach (var stack in session.Inventory.Stacks)
            {
                var item = database.FindItem(stack.ItemId);
                if (item != null && item.IsHealingItem && stack.Count > 0) list.Add(item);
            }
            return list;
        }

        private void ShowItemMenu()
        {
            itemMode = true;
            skillTitle.text = "어떤 아이템을 쓸까요? (한 턴을 쓰고, 문제는 없어요)";
            itemButton.gameObject.SetActive(false);
            fleeButton.gameObject.SetActive(false);
            trainingEndButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(true);
            var items = HealingItems();
            for (int i = 0; i < skillButtons.Count; i++)
            {
                bool used = i < items.Count;
                skillButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var item = items[i];
                UiKit.LabelOf(skillButtons[i]).text = $"{item.DisplayName}  × {session.Inventory.GetCount(item)}";
                skillDetails[i].text = $"HP {item.HealAmount} 회복";
                UiKit.SetColor(skillButtons[i], Palette.Heal);
                skillButtons[i].interactable = true;
                skillButtons[i].onClick.RemoveAllListeners();
                skillButtons[i].onClick.AddListener(() => pickedItem = item);
            }
        }

        private void OnSkillClicked(SkillData skill)
        {
            if (!skill.NeedsTargetChoice)
            {
                Commit(skill, null);
                return;
            }

            var candidates = skill.Target == SkillTarget.SingleEnemy ? engine.Enemies : engine.Party;
            var alive = new List<BattleUnit>();
            foreach (var unit in candidates)
            {
                if (!unit.IsDefeated) alive.Add(unit);
            }

            // 적이 한 마리뿐이면 대상 선택을 건너뛴다
            if (skill.Target == SkillTarget.SingleEnemy && alive.Count == 1)
            {
                Commit(skill, alive[0]);
                return;
            }

            targetingSkill = skill;
            skillTitle.text = $"{skill.DisplayName} — 대상을 선택하세요";
            foreach (var button in skillButtons) button.gameObject.SetActive(false);
            itemButton.gameObject.SetActive(false);
            fleeButton.gameObject.SetActive(false);
            trainingEndButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(true);

            foreach (var unit in alive)
            {
                var target = unit;
                var view = viewOf[unit];
                view.SetSelectable(true, () => Commit(skill, target));
                view.SetFrame(Palette.Info);
            }
        }

        // 기술(과 대상)이 정해짐: 주인공의 공격 기술이면 강도를 고르고, 아니면 바로 문제로
        private void Commit(SkillData skill, BattleUnit target)
        {
            if (engine.CurrentActor.IsPlayerSide && BattleEngine.CanChooseIntensity(skill) && battleConfig.MaxIntensity > 1 && IntensityUnlocked)
            {
                ShowIntensityMenu(skill, target);
                return;
            }
            pickedTarget = target;
            pickedSkill = skill;
        }

        // 강도 고르기 (Figma 'Battle — 강도 고르기'): 단어를 더 많이 연속으로 맞힐수록 피해가 크지만, 하나라도 틀리면 공격 실패
        private void ShowIntensityMenu(SkillData skill, BattleUnit target)
        {
            intensityMode = true;
            targetingSkill = null;
            ClearSelectable();
            skillTitle.text = $"{skill.DisplayName} — 강도를 고르세요\n<size=26><color=#A6B3D1>하나라도 틀리면 이번 턴 공격 실패 · 맞힌 단어는 모두 콤보</color></size>";
            foreach (var button in skillButtons) button.gameObject.SetActive(false);
            itemButton.gameObject.SetActive(false);
            fleeButton.gameObject.SetActive(false);
            trainingEndButton.gameObject.SetActive(false);
            cancelButton.gameObject.SetActive(true);

            // 예상 피해는 고른 대상(없으면 살아 있는 첫 적) 기준
            var estimateTarget = target;
            if (estimateTarget == null)
            {
                foreach (var enemy in engine.Enemies)
                {
                    if (!enemy.IsDefeated) { estimateTarget = enemy; break; }
                }
            }
            for (int i = 0; i < intensityButtons.Count; i++)
            {
                int words = i + 1;
                bool used = words <= battleConfig.MaxIntensity;
                intensityButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var (multiplier, title, detail) = intensityTexts[i];
                multiplier.text = $"×{Multiplier(words)}";
                title.text = words == 1 ? "단어 1개" : $"단어 {words}개 연속";
                int damage = engine.EstimateDamage(skill, estimateTarget, words);
                string each = skill.Target == SkillTarget.AllEnemies ? "적마다 " : "";
                detail.text = (words == 1 ? "맞히면 발동" : $"{words}개 모두 맞혀야 발동") + $" · 예상 피해 {each}약 {damage}";
                intensityButtons[i].onClick.RemoveAllListeners();
                intensityButtons[i].onClick.AddListener(() =>
                {
                    pickedIntensity = words;
                    pickedTarget = target;
                    pickedSkill = skill;
                });
            }
        }

        private string Multiplier(int words) => FormatMultiplier(battleConfig.IntensityMultiplier(words));

        private static string FormatMultiplier(float value) => value.ToString("0.##");

        // 뒤로가기: 도감이 열려 있으면 닫고, 대상·강도·아이템을 고르는 중이면 [취소]와 같음. 전투에서 도망치기는 없다
        public void HandleBack()
        {
            if (dexView != null && dexView.IsOpen) dexView.Hide();
            else if (cancelButton != null && cancelButton.gameObject.activeInHierarchy) OnCancelTargeting();
        }

        // 전투 연습 씬(단독)에서만 직접 받는다 — 필드에서는 FieldScreen이 넘겨 준다
        private void Update()
        {
            PulseShieldAuras(); // 보호막 테두리가 숨 쉬듯 (#137)
            if (loopBattles && FieldScreen.BackPressed()) HandleBack();
            if (relayoutResult != null && IsResultVisible
                && !Mathf.Approximately(((RectTransform)resultPanel.transform).rect.height, resultLayoutHeight))
                relayoutResult();
        }

        private void OnCancelTargeting()
        {
            if (itemMode || intensityMode)
            {
                ShowSkillMenu(engine.CurrentActor);
                return;
            }
            if (targetingSkill == null) return;
            targetingSkill = null;
            ClearSelectable();
            UpdateFrames();
            ShowSkillMenu(engine.CurrentActor);
        }

        private void ClearSelectable()
        {
            foreach (var view in viewOf.Values) view.SetSelectable(false);
            UpdateFrames();
        }

        private void UpdateFrames()
        {
            foreach (var pair in viewOf)
            {
                bool isActor = engine != null && !engine.IsOver && pair.Key == engine.CurrentActor;
                pair.Value.SetFrame(isActor ? (Color?)Palette.Gold : null);
            }
        }

        internal static string SkillDetail(SkillData skill) => $"{SkillEffect(skill)}  |  문제 {QuizText(skill)}";

        // "공격 20 · 적 1체"
        internal static string SkillEffect(SkillData skill)
        {
            string kind = skill.Kind == SkillKind.Damage ? "공격" : skill.Kind == SkillKind.Heal ? "회복" : "보호막";
            string target;
            switch (skill.Target)
            {
                case SkillTarget.SingleEnemy: target = "적 1체"; break;
                case SkillTarget.AllEnemies: target = "적 전체"; break;
                case SkillTarget.SingleAlly: target = "아군 1체"; break;
                case SkillTarget.AllAllies: target = "아군 전체"; break;
                default: target = "자신"; break;
            }
            return $"{kind} {skill.Power} · {target}";
        }

        internal static bool IsHardQuiz(SkillData skill) => skill.QuizDirection != QuizDirection.EnglishToMeaning;

        internal static string QuizText(SkillData skill) => IsHardQuiz(skill) ? "한→영 · 어려움" : "영→한 · 쉬움";

        internal static Color SkillColor(SkillData skill)
        {
            switch (skill.Kind)
            {
                case SkillKind.Damage: return Palette.Attack;
                case SkillKind.Heal: return Palette.Heal;
                default: return Palette.Guard;
            }
        }

        internal static string SkillIconName(SkillData skill)
        {
            switch (skill.Kind)
            {
                case SkillKind.Damage: return skill.Target == SkillTarget.AllEnemies ? "skill_attack_all" : "skill_attack";
                case SkillKind.Heal: return "skill_heal";
                default: return "skill_guard";
            }
        }
    }
}
