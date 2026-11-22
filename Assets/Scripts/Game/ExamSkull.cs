using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;
using WordRPG.Words;

namespace WordRPG.Game
{
    // 힐끔해골 (#281 — 사용자 친구(의대생) 의견: "단어만 외우는 사람은 확인문제를 못 푼다 · 보스 깨면 옆에서 부끄럼 타는 해골이
    // 힐끔힐끔 보며 소름 끼치게 웃고 있다가, 말을 걸면 문제를 던지는 것"). 의학 용어로 하는 사람만, 그 지역 보스를 이긴 뒤 보석 가까운 벽 뒤에서 반만 (#283).
    // 말을 걸면 '○○ 문제인데… 한번 풀어 볼래?' → [풀어보기] = 해골의 방에서 3문제 (아직 못 맞힌 문제부터), 처음 바로 맞힌 문제마다 골드 —
    // 틀려도 벌점 없이 다시 고를 수 있지만 그 문제는 골드 없음. 바로 맞힌 수가 FleeAt 이상이면 슬금슬금 도망 (다음 벽 뒤로)
    public static class ExamSkull
    {
        // 화면에 띄우는 이름 = '???' (#284 사용자·친구 의견: 이름이 붙어 있으면 이상함 — 정체 모를 해골). '힐끔해골'은 개발 문서에서만 부르는 이름
        public const string DisplayName = "???";
        public const string ArtId = "exam_skull"; // 그림 Art/NinjaAdventure/Npcs/exam_skull.png (팩 Skeleton, import_ninja_art.py)
        public const int QuestionsPerVisit = 3;
        public const int GoldPerQuestion = 20;
        public const int MinDistance = 1, MaxDistance = 9; // 숨는 벽: 보석에서 이만큼 떨어진 벽 칸 (동굴 보스 방이 넓어 9 — 가까운 벽부터)
        public const int FleeAt = 2; // 3문제 중 바로 맞힌 수가 이만큼이면 슬금슬금 도망 (다른 벽 뒤로)

        // 그 지역의 확인문제 (지금 단어장 기준 — 수능·비즈니스는 없음)
        public static List<ExamQuestion> Pool(FieldArea area) => WordCourses.ExamFor(area != null ? area.AreaId : null);

        public static bool IsHere(FieldArea area, GameSession session) =>
            area != null && area.Boss != null && session != null && session.World.IsBossDefeated(area.BossId) && Pool(area).Count > 0;

        // 숨는 곳 (#283 — 친구 의견 '책장 사이에서 반 칸만 얼굴'): 보석 가까운 벽 칸(서고 = 책장, 숲·정글 = 나무, 동굴 = 바위) 뒤에서
        // 옆 바닥 쪽으로 얼굴 반쪽을 내밂. 벽 칸 안이라 길을 막지 않음. 가까운 순(옆으로 내미는 곳 먼저) · 벽 하나에 한 자리
        public readonly struct Peek
        {
            public Vector2Int Wall { get; }
            public Vector2Int Floor { get; }    // 얼굴이 보이는 쪽 바닥 (여기 서서 [확인])
            public Vector2Int Out => Floor - Wall; // 내미는 방향 (왼쪽·오른쪽·위)

            public Peek(Vector2Int wall, Vector2Int floor)
            {
                Wall = wall;
                Floor = floor;
            }
        }

        public static List<Peek> PeekSpots(FieldArea area)
        {
            var spots = new List<Peek>();
            if (area == null || area.Boss == null) return spots;
            var map = area.Map;
            var bosses = map.FindAll(FieldTile.Boss);
            if (bosses.Count == 0) return spots;
            var boss = bosses[0];
            var reach = FieldReach.Flood(map);
            var npcSpots = new HashSet<Vector2Int>(map.NpcSpots);
            var found = new List<(Peek peek, int distance, bool side)>();
            for (int x = boss.x - MaxDistance; x <= boss.x + MaxDistance; x++)
            for (int y = boss.y - MaxDistance; y <= boss.y + MaxDistance; y++)
            {
                var wall = new Vector2Int(x, y);
                int distance = Math.Abs(x - boss.x) + Math.Abs(y - boss.y);
                if (distance < MinDistance || distance > MaxDistance || !map.InBounds(wall)) continue;
                var tile = map.Get(wall);
                if (tile != FieldTile.Wall && tile != FieldTile.RuinWall) continue;
                foreach (var dir in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up })
                {
                    var floor = wall + dir;
                    if (!reach.Contains(floor) || npcSpots.Contains(floor) || floor == map.Start) continue;
                    var floorTile = map.Get(floor);
                    if (floorTile != FieldTile.Floor && floorTile != FieldTile.Lawn && floorTile != FieldTile.RuinFloor) continue;
                    found.Add((new Peek(wall, floor), distance, dir.y == 0));
                    break; // 벽 하나에 한 자리 (오른쪽 → 왼쪽 → 위 순)
                }
            }
            found.Sort((a, b) =>
            {
                // 옆으로 내미는 곳이 보기 좋아서 먼저 — 위로 내미는 곳은 두 칸 더 먼 셈
                int c = (a.distance + (a.side ? 0 : 2)).CompareTo(b.distance + (b.side ? 0 : 2));
                if (c != 0) return c;
                c = b.side.CompareTo(a.side);
                if (c != 0) return c;
                c = a.peek.Wall.x.CompareTo(b.peek.Wall.x);
                return c != 0 ? c : b.peek.Wall.y.CompareTo(a.peek.Wall.y);
            });
            foreach (var f in found) spots.Add(f.peek);
            return spots;
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
        public bool Flees => IsComplete && FirstTryCorrect >= ExamSkull.FleeAt; // 많이 맞히면 슬금슬금 도망 (#283)

        // 말 걸 때 '○○ 문제인데…'의 ○○ = 첫 문제의 장 이름에서 '제n장 '을 뺀 것
        public string Topic
        {
            get
            {
                string title = questions.Count > 0 ? questions[0].ChapterTitle : "";
                int space = title.StartsWith("제", StringComparison.Ordinal) ? title.IndexOf(' ') : -1;
                return space > 0 ? title.Substring(space + 1) : title;
            }
        }

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
