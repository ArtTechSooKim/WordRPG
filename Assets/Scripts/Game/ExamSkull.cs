using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 힐끔해골 (#281 — 사용자 친구(의대생) 의견: "단어만 외우는 사람은 확인문제를 못 푼다 · 보스 깨면 옆에서 부끄럼 타는 해골이
    // 힐끔힐끔 보며 소름 끼치게 웃고 있다가, 말을 걸면 문제를 던지는 것"). 의학 용어로 하는 사람만, 그 지역 보스를 이긴 뒤 보스 자리 옆에.
    // 한 번에 3문제 (아직 못 맞힌 문제부터), 처음 바로 맞힌 문제마다 골드 — 틀려도 벌점 없이 다시 고를 수 있지만 그 문제는 골드 없음
    public static class ExamSkull
    {
        public const string DisplayName = "힐끔해골";
        public const string ArtId = "exam_skull"; // 그림 Art/NinjaAdventure/Npcs/exam_skull.png (팩 Skeleton, import_ninja_art.py)
        public const int QuestionsPerVisit = 3;
        public const int GoldPerQuestion = 20;
        public const int MinDistance = 2, MaxDistance = 4; // 보스 몸에서 이만큼 떨어진 자리 (보석 옆 [확인] 자리는 비워 둠)

        // 그 지역의 확인문제 (지금 단어장 기준 — 수능·비즈니스는 없음)
        public static List<ExamQuestion> Pool(FieldArea area) => WordCourses.ExamFor(area != null ? area.AreaId : null);

        public static bool IsHere(FieldArea area, GameSession session) =>
            area != null && area.Boss != null && session != null && session.World.IsBossDefeated(area.BossId) && Pool(area).Count > 0;

        // 서는 자리: 보스 몸에서 2~4칸 떨어진 빈 바닥(바닥·잔디·유적 바닥, 인물 자리 아님) 중 가장 가까운 곳 —
        // 같은 줄(옆)을 먼저, 그 칸이 막혀도 갈 수 있던 곳에 다 갈 수 있는 자리만. avoid = 주인공이 서 있는 칸
        public static Vector2Int? Spot(FieldArea area, IReadOnlyCollection<Vector2Int> bossBody = null, Vector2Int? avoid = null)
        {
            if (area == null || area.Boss == null) return null;
            var map = area.Map;
            var bosses = map.FindAll(FieldTile.Boss);
            if (bosses.Count == 0) return null;
            var body = new HashSet<Vector2Int>(bossBody ?? (IReadOnlyCollection<Vector2Int>)bosses);
            body.Add(bosses[0]);
            var reach = FieldReach.Flood(map);
            var npcSpots = new HashSet<Vector2Int>(map.NpcSpots);

            var candidates = new List<(Vector2Int cell, int distance)>();
            foreach (var cell in reach)
            {
                if (body.Contains(cell) || npcSpots.Contains(cell) || (avoid.HasValue && avoid.Value == cell)) continue;
                var tile = map.Get(cell);
                if (tile != FieldTile.Floor && tile != FieldTile.Lawn && tile != FieldTile.RuinFloor) continue;
                if (map.Start == cell) continue;
                int distance = int.MaxValue;
                foreach (var part in body) distance = Math.Min(distance, Math.Abs(part.x - cell.x) + Math.Abs(part.y - cell.y));
                if (distance < MinDistance || distance > MaxDistance) continue;
                candidates.Add((cell, distance));
            }
            var boss = bosses[0];
            candidates.Sort((a, b) =>
            {
                int c = a.distance.CompareTo(b.distance);
                if (c != 0) return c;
                c = Math.Abs(a.cell.y - boss.y).CompareTo(Math.Abs(b.cell.y - boss.y)); // 같은 줄 = 보스 '옆'
                if (c != 0) return c;
                c = a.cell.x.CompareTo(b.cell.x);
                return c != 0 ? c : b.cell.y.CompareTo(a.cell.y);
            });
            foreach (var (cell, _) in candidates)
                if (KeepsEverythingReachable(reach, cell, map.Start)) return cell;
            return null;
        }

        // 그 칸을 막아도 갈 수 있던 칸에 모두 갈 수 있는지
        private static bool KeepsEverythingReachable(HashSet<Vector2Int> reach, Vector2Int blocked, Vector2Int start)
        {
            if (!reach.Contains(start)) return true;
            var seen = new HashSet<Vector2Int> { start, blocked };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var next = cell + step;
                    if (reach.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return seen.Count == reach.Count; // seen = 막은 칸 + 그 칸 없이 닿은 칸 (막은 칸은 reach 안의 칸)
        }

        // 이번에 낼 문제: 아직 못 맞힌 문제를 먼저(섞어서), 모자라면 맞힌 문제에서
        public static List<ExamQuestion> Pick(IReadOnlyList<ExamQuestion> pool, WorldState world, System.Random rng, int count = QuestionsPerVisit)
        {
            var fresh = new List<ExamQuestion>();
            var solved = new List<ExamQuestion>();
            foreach (var question in pool) (world.IsExamSolved(question.Id) ? solved : fresh).Add(question);
            Shuffle(fresh, rng);
            Shuffle(solved, rng);
            fresh.AddRange(solved);
            return fresh.GetRange(0, Math.Min(count, fresh.Count));
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    // 힐끔해골이 내는 한 번(3문제)의 진행 (#281). 틀리면 그 보기만 꺼지고 다시 (벌점 없음),
    // 처음 고른 답이 맞고 전에 못 맞힌 문제면 골드 + 맞힌 문제로 기록(세이브 solvedExams)
    public class ExamSkullQuiz
    {
        private readonly List<ExamQuestion> questions;
        private readonly IReadOnlyList<ExamQuestion> pool;
        private readonly WorldState world;
        private readonly System.Random rng;
        private bool missed, answered;

        public QuizQuestion Current { get; private set; }
        public int Index { get; private set; }
        public int Count => questions.Count;
        public int FirstTryCorrect { get; private set; }
        public int GoldEarned { get; private set; }
        public bool IsComplete => Index >= questions.Count;

        public ExamSkullQuiz(IReadOnlyList<ExamQuestion> pool, WorldState world, System.Random rng, int count = ExamSkull.QuestionsPerVisit)
        {
            if (pool == null || pool.Count == 0) throw new ArgumentException("확인문제가 없음", nameof(pool));
            this.pool = pool;
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            questions = ExamSkull.Pick(pool, world, rng, count);
            Current = ExamQuiz.Create(questions[0], pool, rng);
        }

        // 골드: 이번 답으로 받은 골드 (없으면 0)
        public (TabletAnswer result, int gold) Answer(int choiceIndex)
        {
            if (IsComplete || answered) return (TabletAnswer.Ignored, 0);
            if (!Current.IsCorrect(choiceIndex))
            {
                missed = true;
                return (TabletAnswer.Wrong, 0);
            }
            answered = true;
            int gold = 0;
            if (!missed)
            {
                FirstTryCorrect++;
                string id = Current.Exam.Id;
                if (!world.IsExamSolved(id))
                {
                    world.MarkExamSolved(id);
                    gold = ExamSkull.GoldPerQuestion;
                    GoldEarned += gold;
                }
            }
            return (TabletAnswer.Correct, gold);
        }

        public void Next()
        {
            if (!answered) return;
            answered = false;
            missed = false;
            Index++;
            if (!IsComplete) Current = ExamQuiz.Create(questions[Index], pool, rng);
        }
    }
}
