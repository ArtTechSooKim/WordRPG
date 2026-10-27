using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Battle;
using WordRPG.Monsters;

namespace WordRPG.UI
{
    // 전투 > 사건 연출 (#119): BattleEngine이 돌려준 사건 목록을 순서대로 화면에 보여 준다.
    // 사건 종류마다 Show… 함수 하나 — 로그·소리·효과·떠오르는 숫자·HP 갱신을 하고 기다릴 시간(초)을 돌려준다.
    // 기다림은 PlayEvents가 사건마다 한 번 (보스 결정타·보스 쓰러짐만 따로). 승리·패배·도망 불가는 연출 없음 (결과 화면·도망 버튼이 맡음).
    // 1.1 고침(#120)은 가지 release-1.1의 PlayEvents에 먼저 들어가고 여기로 옮김 — 두 곳의 연출이 같아야 함
    // 연출 순서·빠짐·기다리는 횟수는 PlayMode BattleEventPlaybackTests가 못 박음 — 연출을 바꾸면 그 기대 순서도 같이 고칠 것
    public partial class BattleScreen
    {
        private IEnumerator PlayEvents(IReadOnlyList<BattleEvent> events)
        {
            playingEvents = events; // 기술 연출이 대상을 찾을 때 (#137)
            highlightedUse = null;
            foreach (var e in events)
            {
                if (e.Type == BattleEventType.Damage && IsBossFinisher(events, e))
                {
                    yield return BossFinisher(e);
                }
                else if (e.Type == BattleEventType.Defeated && IsBossDown(events, e))
                {
                    ShowBossDown(e);
                    yield return FadeShade(0f, 0.6f); // 보스는 느린 화면 그대로 천천히 쓰러진다
                    yield return Wait(0.4f);
                }
                else
                {
                    float seconds = ShowEvent(e);
                    if (seconds > 0f) yield return Wait(seconds);
                }
            }

            RefreshVocabLabel();
            SyncAll();
        }

        // 사건 하나를 보여 주고 기다릴 시간(초)을 돌려준다. 0 = 연출 없음 (기다리지도 않음)
        private float ShowEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.Combo: return ShowCombo(e);
                case BattleEventType.Fled: return ShowFled(e);
                case BattleEventType.FleeQuiz: return ShowFleeQuiz(e);
                case BattleEventType.FleeFailed: return ShowFleeFailed();
                case BattleEventType.QuizAnswered: return ShowQuizAnswered(e);
                case BattleEventType.SkillUsed: return ShowSkillUsed(e);
                case BattleEventType.SkillFailed: return ShowSkillFailed(e);
                case BattleEventType.Damage: return ShowDamage(e);
                case BattleEventType.Heal: return ShowHeal(e);
                case BattleEventType.Shield: return ShowShield(e);
                case BattleEventType.ItemUsed: return ShowItemUsed(e);
                case BattleEventType.Defeated: return ShowDefeated(e);
                case BattleEventType.RoundStarted: return ShowRoundStarted(e);
                case BattleEventType.Charging: return ShowCharging(e);
                default: return 0f; // Victory · Defeat · FleeBlocked
            }
        }

        private float ShowCombo(BattleEvent e)
        {
            comboStep = Combo.Step(e.Amount);
            StartCoroutine(ComboRoutine(e.Amount));
            return 0.45f;
        }

        private float ShowFled(BattleEvent e)
        {
            Log($"{UiKit.WithJosa(e.Actor.DisplayName, "은", "는")} 무사히 도망쳤다!");
            Sound.Play(Sfx.Flee);
            return 0.6f;
        }

        // 강한 몬스터 (또는 보통 몬스터의 1/4) — 도망치려면 단어를 맞혀야 함 (#84)
        private float ShowFleeQuiz(BattleEvent e)
        {
            Log(e.Target != null
                ? $"{UiKit.WithJosa(e.Target.DisplayName, "이", "가")} 길을 막았다! 도망치려면 단어를 맞혀라"
                : "도망치려면 단어를 맞혀라!");
            Sound.Play(Sfx.Encounter);
            return 0.7f;
        }

        private float ShowFleeFailed()
        {
            Log("도망 실패! 차례를 놓쳤다");
            Sound.Play(Sfx.Fail);
            return 0.6f;
        }

        private float ShowQuizAnswered(BattleEvent e)
        {
            comboStep = 0;
            Log((e.Correct ? "정답! " : "오답… ") + MasteryText(e.Mastery));
            return 0.5f;
        }

        // 강도를 올려 쓴 기술(×1.2 등)은 배율 글자 + 크리티컬 소리, 조금 더 기다림. 쓰는 카드의 몸짓·날아가는 그림 (#137)
        private float ShowSkillUsed(BattleEvent e)
        {
            bool charged = e.Multiplier > 1.001f;
            Log($"{e.Actor.DisplayName}의 {e.Skill.DisplayName}" + (charged ? $" ×{FormatMultiplier(e.Multiplier)}!" : "!"));
            PlaySkillStart(e);
            if (!charged) return 0.5f;
            Float(e.Actor, $"×{FormatMultiplier(e.Multiplier)}!", Palette.Gold, 56);
            Sound.Play(Sfx.Critical);
            return 0.7f;
        }

        private float ShowSkillFailed(BattleEvent e)
        {
            Log($"{e.Actor.DisplayName}의 {e.Skill.DisplayName} 실패…");
            Float(e.Actor, "실패", Palette.TextDim, 44);
            Sound.Play(Sfx.Fail);
            PlayFx(e.Actor, Fx.Smoke);
            return 0.7f;
        }

        // 결정타가 아닌 피해. 수련 허수아비는 HP가 줄지 않고 '누적 피해'가 쌓임, 0이면 '막음!'.
        // 효과는 기술마다 (#137 SkillLooks) — 보호막이 다 깎이면 깨지는 연출 + 로그에 '보호막이 깨졌다!'
        private float ShowDamage(BattleEvent e)
        {
            var d = shown[e.Target];
            bool dummyHit = e.Target.Monster != null && e.Target.Monster.Species.IsTrainingDummy;
            shown[e.Target] = (dummyHit ? d.hp : Mathf.Max(0, d.hp - e.Amount), Mathf.Max(0, d.shield - e.Absorbed));
            bool shieldBroke = e.Absorbed > 0 && d.shield > 0 && shown[e.Target].shield == 0;
            if (dummyHit)
            {
                trainingDamage += e.Amount;
                viewOf[e.Target].HpCaption = $"누적 피해 {trainingDamage}";
            }
            Sync(e.Target);
            string dmgText = e.Amount > 0 ? $"-{e.Amount}" : "막음!";
            Sound.Play(e.Amount <= 0 ? Sfx.Shield : e.IsCritical ? Sfx.Critical : Sfx.Hit);
            PlayHit(e, shieldBroke);
            // 콤보 중인 아군 공격은 숫자가 단계만큼 더 크게
            int boost = e.Actor != null && e.Actor.IsPlayerSide ? comboStep * 5 : 0;
            Float(e.Target, e.IsCritical ? $"크리티컬! {dmgText}" : dmgText,
                e.Amount > 0 ? Palette.Bad : Palette.Info, (e.IsCritical ? 58 : 48) + boost);
            Log(e.Absorbed > 0
                ? $"{e.Target.DisplayName} 피해 {e.Amount} (보호막이 {e.Absorbed} 흡수" + (shieldBroke ? " — 보호막이 깨졌다!)" : ")")
                : $"{e.Target.DisplayName} 피해 {e.Amount}" + (e.IsCritical ? " — 크리티컬!" : ""));
            return 0.6f;
        }

        private float ShowHeal(BattleEvent e)
        {
            ShowRecovery(e.Target, e.Amount, SkillLooks.Of(e.Skill, e.Actor == null || e.Actor.IsPlayerSide));
            Log($"{e.Target.DisplayName} HP +{e.Amount}" + (e.IsCritical ? " — 크리티컬!" : ""));
            return 0.6f;
        }

        private float ShowItemUsed(BattleEvent e)
        {
            ShowRecovery(e.Target, e.Amount, SkillLooks.Of(SkillFx.HealLight));
            Log($"{UiKit.WithJosa(e.Item.DisplayName, "을", "를")} 썼다! HP +{e.Amount}");
            return 0.6f;
        }

        // 회복 기술·상처약 공통: 화면 HP를 올리고 크게 찍히는 초록 숫자 + 회복 소리 + 회복 연출(#137 — 마법진·빛기둥·알갱이·HP 막대가 차오름). 로그는 각자
        private void ShowRecovery(BattleUnit target, int amount, SkillLook look)
        {
            var h = shown[target];
            shown[target] = (h.hp + amount, h.shield);
            Sync(target);
            Float(target, $"+{amount}", Palette.Good, 60, pop: true);
            Sound.Play(Sfx.Heal);
            PlayRecovery(target, h.hp, look);
        }

        // 보호막: 막이 튕기며 덮고(#137) 기술마다 다른 효과 그림, 남아 있는 동안 카드 테두리
        private float ShowShield(BattleEvent e)
        {
            var s = shown[e.Target];
            shown[e.Target] = (s.hp, s.shield + e.Amount);
            var look = SkillLooks.Of(e.Skill, e.Actor == null || e.Actor.IsPlayerSide);
            PlayShield(e.Target, look); // 테두리 색을 먼저 정하고 Sync가 테두리를 켬
            Sync(e.Target);
            Float(e.Target, $"보호막 +{e.Amount}", Palette.Info, 40);
            Sound.Play(Sfx.Shield);
            PlayFx(e.Target, look.Hit, 1f, 1f, look.Tint);
            Log($"{e.Target.DisplayName} 보호막 +{e.Amount}");
            return 0.6f;
        }

        private float ShowDefeated(BattleEvent e)
        {
            Log($"{e.Target.DisplayName} 쓰러졌다!");
            Sound.Play(Sfx.Faint);
            PlayFx(e.Target, Fx.Smoke);
            return 0.5f;
        }

        // 보스전에서 이긴 순간 쓰러지는 보스 — 결정타의 느린 화면 그대로 (뒤이어 PlayEvents가 화면을 다시 밝히고 기다림)
        private bool IsBossDown(IReadOnlyList<BattleEvent> events, BattleEvent e) =>
            bossBattle && !e.Target.IsPlayerSide && Has(events, BattleEventType.Victory);

        private void ShowBossDown(BattleEvent e)
        {
            Log($"{e.Target.DisplayName} 쓰러졌다!");
            Sound.Play(Sfx.Faint, 0.7f);
            PlayFx(e.Target, Fx.Smoke, 2.5f, 1.4f);
        }

        // 아군 보호막은 전투 내내 남음 (#120) — 화면도 실제 값 그대로. 수련은 라운드 대신 '수련'
        private float ShowRoundStarted(BattleEvent e)
        {
            roundLabel.text = training ? "수련" : $"라운드 {e.Round}";
            SyncAll();
            return 0.2f;
        }

        // 힘을 모으는 기술 (#120): 이번 차례는 힘만 모으고 다음 차례에 큰 한 방 — 보호막으로 대비할 틈
        private float ShowCharging(BattleEvent e)
        {
            Log($"{UiKit.WithJosa(e.Actor.DisplayName, "이", "가")} 힘을 모으고 있다");
            Float(e.Actor, "힘을 모은다!", Palette.Gold, 48);
            Sound.Play(Sfx.EvolveLight);
            PlayFx(e.Actor, Fx.Sparkle, 1.5f, 1.3f);
            return 0.9f;
        }
    }
}
