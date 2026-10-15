using System;
using System.Collections.Generic;
using WordRPG.Monsters;
using WordRPG.Words;

namespace WordRPG.Battle
{
    // [선제공격] (#73, GDD 9-5): 쫓아오는 몬스터가 가까이 오면 누를 수 있다 → 몬스터가 느려지고 "○○의 뜻은?" 한 문제.
    // 맞히면 맨 앞 몬스터가 최대 HP의 20%를 잃은 채 전투 시작, 틀리면 보너스 없이 그냥 전투 (사용자 결정).
    // 문제는 이미 본 단어만 — 새 단어 카드·도감 발견과 섞여 혼선이 없게 (사용자 요청)
    public static class Preemptive
    {
        public const float DamageRatio = 0.2f;
        public const float AnswerSeconds = 6f;

        // 반환값: 실제로 깎인 HP
        public static int Strike(MonsterInstance target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            int amount = Math.Max(1, (int)Math.Round(target.Stats.MaxHp * DamageRatio));
            return target.TakeDamage(amount);
        }

        // 문제를 낼 단어장: 이 지역에서 본 단어가 있으면 이 지역만, 없으면 지금까지 본 모든 지역.
        // 본 단어가 하나도 없으면 빈 목록 → [선제공격] 버튼을 띄우지 않음
        public static List<IReadOnlyList<WordEntry>> QuizBooks(IReadOnlyList<WordEntry> region,
            IEnumerable<IReadOnlyList<WordEntry>> all, VocabularyProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (region != null && TrainingQuizProvider.Count(new[] { region }, progress).discovered > 0)
                return new List<IReadOnlyList<WordEntry>> { region };
            var books = new List<IReadOnlyList<WordEntry>>();
            if (all != null)
                foreach (var book in all)
                    if (book != null && TrainingQuizProvider.Count(new[] { book }, progress).discovered > 0) books.Add(book);
            return books;
        }
    }
}
