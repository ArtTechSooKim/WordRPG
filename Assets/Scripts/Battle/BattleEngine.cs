using System;
using System.Collections.Generic;
using WordRPG.Items;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Battle
{
    public enum BattlePhase
    {
        ChoosingSkill, // CurrentActor의 스킬/대상 선택 대기
        AnsweringQuiz, // CurrentQuestion 답 대기
        Victory,
        Defeat,
        Fled           // 도망침 (#53) — 보상 없음
    }

    // 턴제 전투 진행. 한 라운드 = 아군(지금은 주인공 혼자)이 [기술 선택 → 단어 문제 → 결과] 후 적 전원 행동.
    //  정답: 기술 발동 (빨리 맞히면 크리티컬)
    //  오답/시간 초과: 기술 실패, 단어는 오답 노트로
    //  기술 대신 상처약을 쓰면 문제 없이 회복하고 차례를 넘긴다
    //  연속으로 맞히면 콤보 (Combo): 단계마다 아군 기술 피해가 조금씩 늘고, 틀리면 0으로
    //  기술 대신 도망칠 수 있다 (#53): 바로 끝나고 보상은 없음. 보스전(canFlee = false)에서는 막힘
    //  공격 기술은 강도를 고를 수 있다 (사용자 아이디어): 단어 n개를 연속으로 맞혀야 발동하고 피해 ×1.2·×1.5·×2.
    //    하나라도 틀리면 이번 턴 공격 실패. 맞힌 단어는 하나하나 콤보에 더해지고, 크리티컬은 모두 빨리 맞혔을 때만
    // MonoBehaviour·UI 의존 없음. UI는 SelectSkill/SubmitAnswer를 호출하고 돌려받은 BattleEvent 목록을 연출한다
    public class BattleEngine
    {
        private readonly List<BattleUnit> party = new List<BattleUnit>();
        private readonly List<BattleUnit> enemies = new List<BattleUnit>();
        private readonly List<MasteryChange> masteryChanges = new List<MasteryChange>();
        private readonly IQuizProvider quiz;
        private readonly BattleConfig config;
        private readonly Random rng;

        private SkillData pendingSkill;
        private BattleUnit pendingTarget;
        private bool chainAllFast;

        public IReadOnlyList<BattleUnit> Party => party;
        public IReadOnlyList<BattleUnit> Enemies => enemies;
        public BattlePhase Phase { get; private set; }
        public int Round { get; private set; } = 1;
        public BattleUnit CurrentActor { get; private set; }
        public QuizQuestion CurrentQuestion { get; private set; }
        public SkillData PendingSkill => pendingSkill;
        public BattleConfig Config => config;
        public int CorrectAnswers { get; private set; }
        public int WrongAnswers { get; private set; }
        public int Streak { get; private set; } // 지금까지 연속으로 맞힌 수 (이전 전투에서 이어받을 수 있음)
        public int Intensity { get; private set; } = 1; // 이번 기술에서 연속으로 맞혀야 할 단어 수
        public int ChainCorrect { get; private set; }    // 그중 지금까지 맞힌 수
        public bool CanStillCrit => chainAllFast;        // 지금까지 모두 빨리 맞혀서 크리티컬이 아직 가능한지
        public IReadOnlyList<MasteryChange> MasteryChanges => masteryChanges;
        public BattleSummary Summary { get; } = new BattleSummary(); // 결산 화면용 (#41)
        public bool IsOver => Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat || Phase == BattlePhase.Fled;
        public bool CanFlee { get; }

        // startStreak: 이전 전투에서 이어지는 연속 정답 수, canFlee: 도망칠 수 있는 전투인지 (보스전은 false)
        public BattleEngine(IReadOnlyList<ICombatant> partyMembers, IReadOnlyList<MonsterInstance> enemyMonsters,
            IQuizProvider quiz, BattleConfig config, Random rng, int startStreak = 0, bool canFlee = true)
        {
            this.quiz = quiz ?? throw new ArgumentNullException(nameof(quiz));
            this.config = config ?? new BattleConfig();
            this.rng = rng ?? new Random();
            Streak = Math.Max(0, startStreak);
            CanFlee = canFlee;

            for (int i = 0; i < partyMembers.Count; i++) party.Add(new BattleUnit(partyMembers[i], true, i));
            for (int i = 0; i < enemyMonsters.Count; i++) enemies.Add(new BattleUnit(enemyMonsters[i], false, i));

            if (FirstAlive(party) == null) throw new ArgumentException("싸울 수 있는 아군이 없습니다", nameof(partyMembers));
            if (FirstAlive(enemies) == null) throw new ArgumentException("적이 없습니다", nameof(enemyMonsters));

            CurrentActor = FirstAlive(party);
            Phase = BattlePhase.ChoosingSkill;
        }

        // 강도를 고를 수 있는 기술인지: 아군의 공격 기술만
        public static bool CanChooseIntensity(SkillData skill) => skill != null && skill.Kind == SkillKind.Damage;

        // 단일 대상 스킬이면 target 필수. intensity = 연속으로 맞혀야 할 단어 수 (공격 기술만 2 이상).
        // 반환된 문제를 UI에 보여준다
        public QuizQuestion SelectSkill(SkillData skill, BattleUnit target = null, int intensity = 1)
        {
            RequirePhase(BattlePhase.ChoosingSkill);
            if (skill == null || !Contains(CurrentActor.Skills, skill))
                throw new ArgumentException($"{CurrentActor.DisplayName}의 스킬이 아닙니다", nameof(skill));
            if (skill.NeedsTargetChoice) ValidateTarget(skill, target);
            if (intensity < 1 || intensity > config.MaxIntensity)
                throw new ArgumentOutOfRangeException(nameof(intensity), $"강도는 1~{config.MaxIntensity}");
            if (intensity > 1 && !CanChooseIntensity(skill))
                throw new ArgumentException("강도는 공격 기술만 고를 수 있습니다", nameof(intensity));

            pendingSkill = skill;
            pendingTarget = target;
            Intensity = intensity;
            ChainCorrect = 0;
            chainAllFast = true;
            CurrentQuestion = quiz.NextQuestion(skill.QuizDirection);
            Phase = BattlePhase.AnsweringQuiz;
            return CurrentQuestion;
        }

        // 강도 고르기 화면의 예상 피해 (랜덤·크리티컬 빼고, 다 맞혔을 때 쌓일 콤보까지 넣어서)
        public int EstimateDamage(SkillData skill, BattleUnit target, int intensity)
        {
            if (skill == null || target == null) return 0;
            float bonus = Combo.DamageBonus(Streak + intensity, config.ComboBonusPerStep);
            return BattleFormulas.ExpectedDamage(skill.Power, CurrentActor.Attack, target.Defense, bonus,
                config.IntensityMultiplier(intensity));
        }

        // choiceIndex < 0 이면 시간 초과로 처리
        public IReadOnlyList<BattleEvent> SubmitAnswer(int choiceIndex, float secondsTaken)
        {
            RequirePhase(BattlePhase.AnsweringQuiz);
            var events = new List<BattleEvent>();

            bool correct = choiceIndex >= 0
                           && secondsTaken <= config.AnswerTimeLimitSeconds
                           && CurrentQuestion.IsCorrect(choiceIndex);

            var mastery = quiz.SubmitAnswer(CurrentQuestion, correct);
            masteryChanges.Add(mastery);
            Summary.RecordAnswer(CurrentQuestion.Word, CurrentQuestion.IsNewWord, correct);
            if (correct)
            {
                CorrectAnswers++;
                Streak++;
                ChainCorrect++;
                chainAllFast &= secondsTaken <= config.CriticalTimeSeconds;
            }
            else
            {
                WrongAnswers++;
                Streak = 0;
            }
            events.Add(BattleEvent.QuizAnswered(CurrentActor, correct, mastery));

            // 강도를 올렸으면 남은 단어를 계속 낸다 (차례는 그대로)
            if (correct && ChainCorrect < Intensity)
            {
                CurrentQuestion = quiz.NextQuestion(pendingSkill.QuizDirection);
                Summary.Record(events);
                return events;
            }

            if (correct)
            {
                if (Combo.Step(Streak) > 0) events.Add(BattleEvent.Combo(CurrentActor, Streak));
                ExecuteSkill(CurrentActor, pendingSkill, pendingTarget, chainAllFast, events,
                    Combo.DamageBonus(Streak, config.ComboBonusPerStep), config.IntensityMultiplier(Intensity));
            }
            else
            {
                events.Add(BattleEvent.SkillFailed(CurrentActor, pendingSkill));
            }

            pendingSkill = null;
            pendingTarget = null;
            CurrentQuestion = null;
            Intensity = 1;
            ChainCorrect = 0;

            if (!CheckBattleEnd(events)) AdvanceTurn(events);
            Summary.Record(events);
            return events;
        }

        // 기술 대신 회복 아이템(상처약)을 쓴다: 문제 없이 지금 차례인 아군을 회복하고 차례를 넘긴다
        public IReadOnlyList<BattleEvent> UseItem(ItemData item, Inventory inventory)
        {
            RequirePhase(BattlePhase.ChoosingSkill);
            if (item == null || !item.IsHealingItem) throw new ArgumentException("전투에서 쓸 수 있는 회복 아이템이 아닙니다", nameof(item));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!inventory.TryRemove(item)) throw new InvalidOperationException($"{item.DisplayName}이(가) 없습니다");

            var events = new List<BattleEvent>();
            int healed = CurrentActor.ReceiveHeal(item.HealAmount);
            events.Add(BattleEvent.ItemUsed(CurrentActor, item, healed));
            if (!CheckBattleEnd(events)) AdvanceTurn(events);
            Summary.Record(events);
            return events;
        }

        // 기술 대신 도망친다 (#53): 바로 전투가 끝나고(Fled) 보상은 없다. 그때까지 쓴 상처약·받은 피해·푼 단어 기록은 그대로.
        // 도망칠 수 없는 전투(보스)면 FleeBlocked만 돌려주고 차례는 그대로
        public IReadOnlyList<BattleEvent> Flee()
        {
            RequirePhase(BattlePhase.ChoosingSkill);
            var events = new List<BattleEvent>();
            if (!CanFlee)
            {
                events.Add(BattleEvent.FleeBlocked(CurrentActor));
                return events;
            }
            Phase = BattlePhase.Fled;
            events.Add(BattleEvent.Fled(CurrentActor));
            Summary.Record(events);
            return events;
        }

        public BattleReward CalculateReward()
        {
            if (Phase != BattlePhase.Victory) throw new InvalidOperationException("승리했을 때만 보상이 있습니다");
            var defeated = new List<MonsterInstance>();
            foreach (var enemy in enemies) defeated.Add(enemy.Monster);
            return BattleRewardCalculator.Calculate(defeated, CorrectAnswers, config, rng);
        }

        private void AdvanceTurn(List<BattleEvent> events)
        {
            var next = NextAlivePartyMember(CurrentActor.Index);
            if (next != null)
            {
                CurrentActor = next;
                Phase = BattlePhase.ChoosingSkill;
                return;
            }

            RunEnemyPhase(events);
            if (CheckBattleEnd(events)) return;

            // 보호막은 건 쪽의 다음 행동 차례가 오면 사라진다
            Round++;
            foreach (var unit in party) unit.ClearShield();
            events.Add(BattleEvent.RoundStarted(Round));
            CurrentActor = FirstAlive(party);
            Phase = BattlePhase.ChoosingSkill;
        }

        private void RunEnemyPhase(List<BattleEvent> events)
        {
            foreach (var enemy in enemies) enemy.ClearShield();

            foreach (var enemy in enemies)
            {
                if (enemy.IsDefeated || enemy.Skills.Count == 0) continue;
                if (FirstAlive(party) == null) return;

                var skill = enemy.Skills[rng.Next(enemy.Skills.Count)];
                ExecuteSkill(enemy, skill, ChooseEnemyTarget(enemy, skill), false, events);
            }
        }

        private BattleUnit ChooseEnemyTarget(BattleUnit enemy, SkillData skill)
        {
            switch (skill.Target)
            {
                case SkillTarget.SingleEnemy:
                    var alive = AliveUnits(party);
                    return alive[rng.Next(alive.Count)];
                case SkillTarget.SingleAlly:
                    // 체력 비율이 가장 낮은 아군(적 편)을 돌본다
                    BattleUnit weakest = null;
                    foreach (var unit in AliveUnits(enemies))
                    {
                        if (weakest == null || (double)unit.Hp / unit.MaxHp < (double)weakest.Hp / weakest.MaxHp)
                            weakest = unit;
                    }
                    return weakest;
                default:
                    return null;
            }
        }

        // comboBonus: 연속 정답 콤보 추가 피해, intensity: 강도 배율 (아군이 문제를 맞혔을 때만, 공격 기술에만)
        private void ExecuteSkill(BattleUnit user, SkillData skill, BattleUnit chosenTarget, bool critical,
            List<BattleEvent> events, float comboBonus = 0f, float intensity = 1f)
        {
            events.Add(BattleEvent.SkillUsed(user, skill, intensity));

            foreach (var target in ResolveTargets(user, skill, chosenTarget))
            {
                switch (skill.Kind)
                {
                    case SkillKind.Damage:
                        int damage = BattleFormulas.Damage(skill.Power, user.Attack, target.Defense, critical, config, rng, comboBonus, intensity);
                        target.ReceiveDamage(damage, out int hpDamage, out int absorbed);
                        events.Add(BattleEvent.Damage(user, target, skill, hpDamage, absorbed, critical));
                        if (target.IsDefeated) events.Add(BattleEvent.Defeated(target));
                        break;

                    case SkillKind.Heal:
                        int heal = BattleFormulas.Heal(skill.Power, user.Attack, critical, config, rng);
                        events.Add(BattleEvent.Heal(user, target, skill, target.ReceiveHeal(heal), critical));
                        break;

                    case SkillKind.Guard:
                        int shield = BattleFormulas.Shield(skill.Power, user.Defense, critical, config, rng);
                        target.AddShield(shield);
                        events.Add(BattleEvent.Shield(user, target, skill, shield, critical));
                        break;
                }
            }
        }

        private List<BattleUnit> ResolveTargets(BattleUnit user, SkillData skill, BattleUnit chosenTarget)
        {
            var opponents = user.IsPlayerSide ? enemies : party;
            var allies = user.IsPlayerSide ? party : enemies;

            switch (skill.Target)
            {
                case SkillTarget.SingleEnemy:
                    return SingleOrFallback(chosenTarget, opponents);
                case SkillTarget.SingleAlly:
                    return SingleOrFallback(chosenTarget, allies);
                case SkillTarget.AllEnemies:
                    return AliveUnits(opponents);
                case SkillTarget.AllAllies:
                    return AliveUnits(allies);
                case SkillTarget.Self:
                    return new List<BattleUnit> { user };
                default:
                    return new List<BattleUnit>();
            }
        }

        private static List<BattleUnit> SingleOrFallback(BattleUnit chosen, List<BattleUnit> side)
        {
            if (chosen != null && !chosen.IsDefeated) return new List<BattleUnit> { chosen };
            var first = FirstAlive(side);
            return first != null ? new List<BattleUnit> { first } : new List<BattleUnit>();
        }

        private bool CheckBattleEnd(List<BattleEvent> events)
        {
            if (FirstAlive(enemies) == null)
            {
                Phase = BattlePhase.Victory;
                events.Add(BattleEvent.Victory());
                return true;
            }
            if (FirstAlive(party) == null)
            {
                Phase = BattlePhase.Defeat;
                events.Add(BattleEvent.Defeat());
                return true;
            }
            return false;
        }

        private void ValidateTarget(SkillData skill, BattleUnit target)
        {
            var side = skill.Target == SkillTarget.SingleEnemy ? enemies : party;
            if (target == null || !side.Contains(target) || target.IsDefeated)
                throw new ArgumentException($"{skill.DisplayName}의 대상으로 고를 수 없습니다", nameof(target));
        }

        private void RequirePhase(BattlePhase expected)
        {
            if (Phase != expected) throw new InvalidOperationException($"지금은 {Phase} 단계입니다 ({expected} 필요)");
        }

        private BattleUnit NextAlivePartyMember(int afterIndex)
        {
            for (int i = afterIndex + 1; i < party.Count; i++)
            {
                if (!party[i].IsDefeated) return party[i];
            }
            return null;
        }

        private static BattleUnit FirstAlive(List<BattleUnit> units)
        {
            foreach (var unit in units)
            {
                if (!unit.IsDefeated) return unit;
            }
            return null;
        }

        private static List<BattleUnit> AliveUnits(List<BattleUnit> units)
        {
            return units.FindAll(u => !u.IsDefeated);
        }

        private static bool Contains(IReadOnlyList<SkillData> skills, SkillData skill)
        {
            foreach (var s in skills)
            {
                if (s == skill) return true;
            }
            return false;
        }
    }
}
