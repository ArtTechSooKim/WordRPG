using System;
using System.Collections.Generic;
using UnityEngine;
using WordRPG.Monsters;

namespace WordRPG.Field
{
    // 지역의 조우 방식 (에셋에 번호로 저장 — 새 방식은 맨 뒤에)
    public enum EncounterStyle
    {
        Grass,   // 풀숲을 밟으면 확률로 조우 (초원·서고·숲)
        Roaming  // 보이는 몬스터가 쫓아옴 — 부딪히면 전투, 가까우면 [선제공격] (#73, 사막부터).
                 // 풀숲도 확률 조우 — 단, 보이는 몬스터가 나를 쫓는 동안은 없음 (#123, HuntingPlayer)
    }

    public enum RoamerState
    {
        Wandering, // 서식지 근처를 어슬렁
        Noticing,  // 주인공을 발견 — "!" 뜨고 잠깐 멈춤
        Chasing,   // 쫓아옴
        Returning, // 놓쳐서 집으로 돌아감
        Stunned    // 도망친 직후 잠깐 멈춤
    }

    public enum RoamerEventKind
    {
        Spawned,
        Moved,
        Noticed,
        GaveUp,
        Caught // 주인공에게 부딪힘 → 전투
    }

    public readonly struct RoamerEvent
    {
        public RoamerEventKind Kind { get; }
        public Roamer Roamer { get; }
        public Vector2Int From { get; }

        public RoamerEvent(RoamerEventKind kind, Roamer roamer, Vector2Int from)
        {
            Kind = kind;
            Roamer = roamer;
            From = from;
        }
    }

    // 숫자는 추정치 — 해 보고 조정 (주인공: 걷기 한 칸 0.16초, 달리기 0.16 × 0.55 ≈ 0.09초)
    public class RoamingRules
    {
        public int DetectRadius = 5;           // 이 거리 안에 들어오면 발견
        public int GiveUpRadius = 10;          // 쫓다가 이보다 멀어지면 포기
        public int PreemptRadius = 3;          // [선제공격] 버튼이 뜨는 거리
        public int LeashRadius = 6;            // 집(태어난 서식지 칸)에서 어슬렁거리는 범위
        public float WanderStepSeconds = 0.6f; // 어슬렁 한 걸음 (가끔 쉼)
        public double WanderPauseChance = 0.45;
        public float ChaseStepSeconds = 0.12f; // 걷기보다 빠르고 달리기보다 느림 → 달리면 따돌림
        public float NoticeSeconds = 0.6f;     // "!" 뒤 출발까지
        public float StunSeconds = 3f;         // 도망친 뒤 멈춤
        public float CalmSeconds = 2f;         // 전투가 끝난 뒤 이 시간 동안은 새로 알아채지 않음 (#123)
        public float RespawnSeconds = 25f;     // 쓰러뜨린 뒤 다른 곳에서 다시 나타나기까지
        public int SpawnMinDistance = 12;      // 화면 밖에서 나타남
        public int MinSpacing = 6;             // 몬스터끼리 떨어진 거리
        public int CellsPerRoamer = 120;       // 서식지 칸 수 ÷ 이 값 = 마릿수
        public int MaxRoamers = 30;
        public int WaterCellsPerRoamer = 140;  // 물에 사는 몬스터 (#80 흉내문어): 뗏목이 다니는 물 칸 수 ÷ 이 값
        public int MaxWaterRoamers = 8;
        public int ActiveRadius = 26;          // 이보다 먼 어슬렁 몬스터는 쉰다 (계산 아끼기)
    }

    // 필드에 보이는 몬스터 하나 = 적 한 무리 (그림은 맨 앞 몬스터)
    public class Roamer
    {
        public int Id { get; }
        public Vector2Int Home { get; }
        public Vector2Int Position { get; internal set; }
        public Direction Facing { get; internal set; } = Direction.Down;
        public RoamerState State { get; internal set; } = RoamerState.Wandering;
        public List<MonsterInstance> Group { get; }
        public MonsterInstance Leader => Group[0];
        public bool Aquatic { get; } // 물에 사는 몬스터 — 뗏목이 다니는 깊은 강만 다닌다 (#80)
        internal float Timer;

        public Roamer(int id, Vector2Int home, List<MonsterInstance> group, bool aquatic = false)
        {
            if (group == null || group.Count == 0) throw new ArgumentException("무리가 비었습니다", nameof(group));
            Id = id;
            Home = home;
            Position = home;
            Group = group;
            Aquatic = aquatic;
        }
    }

    // 보이는 몬스터 조우 규칙 (순수 C#, 시간은 Tick(dt), 랜덤은 주입 — GDD 9-5).
    // 서식지 = 마른 덤불(',', FieldTile.Grass) 칸. 몬스터는 길·모래·덤불·얕은 강만 걷고 출입구·물건 칸에는 서지 않는다.
    // 물에 사는 몬스터(#80, 정글의 흉내문어)는 뗏목이 다니는 깊은 강(FieldMap.IsRaftWater)이 서식지이자 다니는 곳
    public class RoamingMonsters
    {
        private static readonly Direction[] Directions = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

        private readonly FieldMap map;
        private readonly Func<System.Random, List<MonsterInstance>> rollGroup;
        private readonly Func<System.Random, List<MonsterInstance>> rollWaterGroup;
        private readonly System.Random rng;
        private readonly List<Vector2Int> habitat = new List<Vector2Int>();
        private readonly List<Vector2Int> waterHabitat = new List<Vector2Int>();
        private readonly List<Roamer> roamers = new List<Roamer>();
        private readonly List<(float seconds, bool aquatic)> respawns = new List<(float, bool)>();
        private readonly List<RoamerEvent> tickEvents = new List<RoamerEvent>();
        private int nextId;
        private float calm; // 남은 '알아채지 않는' 시간 (#123)

        public RoamingRules Rules { get; }
        public IReadOnlyList<Roamer> Roamers => roamers;
        public IReadOnlyList<Vector2Int> Habitat => habitat;
        public IReadOnlyList<Vector2Int> WaterHabitat => waterHabitat;
        public int PendingRespawns => respawns.Count;

        // 1이면 보통, 작으면 느려짐 ([선제공격] 문제를 푸는 동안)
        public float SlowFactor { get; set; } = 1f;
        public float CalmLeft => calm;

        public int TargetCount => habitat.Count == 0 ? 0 : Mathf.Clamp(habitat.Count / Mathf.Max(1, Rules.CellsPerRoamer), 1, Rules.MaxRoamers);

        public int WaterTargetCount => waterHabitat.Count == 0 ? 0
            : Mathf.Clamp(waterHabitat.Count / Mathf.Max(1, Rules.WaterCellsPerRoamer), 1, Rules.MaxWaterRoamers);

        // rollWaterGroup: 물에 사는 몬스터 무리 (없으면 물엔 몬스터 없음 — FieldArea.WaterEncounters)
        public RoamingMonsters(FieldMap map, Func<System.Random, List<MonsterInstance>> rollGroup, System.Random rng, RoamingRules rules = null,
            Func<System.Random, List<MonsterInstance>> rollWaterGroup = null)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.rollGroup = rollGroup ?? throw new ArgumentNullException(nameof(rollGroup));
            this.rollWaterGroup = rollWaterGroup;
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Rules = rules ?? new RoamingRules();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.Get(new Vector2Int(x, y)) == FieldTile.Grass) habitat.Add(new Vector2Int(x, y));
            if (rollWaterGroup != null)
            {
                waterHabitat.AddRange(map.RaftWaterCells());
                waterHabitat.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x)); // 랜덤 결과가 매번 같게
            }
        }

        // 지역에 들어올 때: 정해진 마릿수만큼 주인공에게서 먼 서식지에
        public List<RoamerEvent> Populate(Vector2Int player)
        {
            var events = new List<RoamerEvent>();
            foreach (bool aquatic in new[] { false, true })
            {
                int target = aquatic ? WaterTargetCount : TargetCount;
                while (roamers.FindAll(r => r.Aquatic == aquatic).Count < target)
                {
                    var roamer = TrySpawn(player, aquatic);
                    if (roamer == null) break;
                    events.Add(new RoamerEvent(RoamerEventKind.Spawned, roamer, roamer.Position));
                }
            }
            return events;
        }

        public Roamer At(Vector2Int cell) => roamers.Find(r => r.Position == cell);

        public bool Has(Roamer roamer) => roamers.Contains(roamer);

        // [선제공격] 대상: 나를 발견하고 가까이 온 몬스터 중 가장 가까운 것 (없으면 null)
        public Roamer PreemptTarget(Vector2Int player)
        {
            Roamer best = null;
            int bestDistance = int.MaxValue;
            foreach (var roamer in roamers)
            {
                if (roamer.State != RoamerState.Chasing && roamer.State != RoamerState.Noticing) continue;
                int d = Distance2(roamer.Position, player);
                if (d <= Rules.PreemptRadius * Rules.PreemptRadius && d < bestDistance)
                {
                    best = roamer;
                    bestDistance = d;
                }
            }
            return best;
        }

        // 전투에서 이김 → 사라지고 얼마 뒤 다른 곳에서 새로 나타남
        public void Defeat(Roamer roamer)
        {
            if (roamers.Remove(roamer)) respawns.Add((Rules.RespawnSeconds, roamer.Aquatic));
        }

        // 도망침 → 잠깐 멈춤 (그동안 거리를 벌림)
        public void Stun(Roamer roamer)
        {
            if (!roamers.Contains(roamer)) return;
            roamer.State = RoamerState.Stunned;
            roamer.Timer = Rules.StunSeconds;
        }

        // 나를 발견했거나("!") 쫓아오는 몬스터가 있는지 (#123): 그동안은 풀숲 조우를 하지 않는다 —
        // 그 싸움은 쫓아오는 몬스터와 (연달아 두 번 싸우지 않고, [선제공격] 기회를 풀숲이 빼앗지 않게)
        public bool HuntingPlayer
        {
            get
            {
                foreach (var roamer in roamers)
                    if (roamer.State == RoamerState.Noticing || roamer.State == RoamerState.Chasing) return true;
                return false;
            }
        }

        // 전투가 끝남 (#123): 잠깐 새로 알아채지 않음 — 풀숲 전투에서 나오자마자 옆에서 어슬렁거리던 몬스터가 덮치지 않게.
        // 이미 쫓던 몬스터는 그대로 쫓는다
        public void Calm() => calm = Rules.CalmSeconds;

        // 돌려주는 목록은 다음 Tick에서 다시 쓴다 (매 프레임 새 목록을 만들지 않게 — #93). 바로 읽고 버릴 것
        public IReadOnlyList<RoamerEvent> Tick(float dt, Vector2Int player)
        {
            var events = tickEvents;
            events.Clear();
            for (int i = respawns.Count - 1; i >= 0; i--)
            {
                respawns[i] = (respawns[i].seconds - dt, respawns[i].aquatic);
                if (respawns[i].seconds > 0f) continue;
                var roamer = TrySpawn(player, respawns[i].aquatic);
                if (roamer == null) continue; // 자리가 없으면 다음 Tick에 다시
                respawns.RemoveAt(i);
                events.Add(new RoamerEvent(RoamerEventKind.Spawned, roamer, roamer.Position));
            }

            if (calm > 0f) calm -= dt;
            float step = dt * SlowFactor;
            foreach (var roamer in roamers.ToArray())
            {
                int d2 = Distance2(roamer.Position, player);
                // 멀리서 어슬렁거리는 몬스터만 쉰다 (쫓기·돌아가기·멈춤은 끝까지)
                bool active = roamer.State != RoamerState.Wandering || d2 <= Rules.ActiveRadius * Rules.ActiveRadius;
                if (!active) continue;
                roamer.Timer -= step;
                switch (roamer.State)
                {
                    case RoamerState.Stunned:
                        if (roamer.Timer <= 0f) StartReturning(roamer);
                        break;
                    case RoamerState.Wandering:
                    case RoamerState.Returning:
                        if (calm <= 0f && d2 <= Rules.DetectRadius * Rules.DetectRadius)
                        {
                            roamer.State = RoamerState.Noticing;
                            roamer.Timer = Rules.NoticeSeconds;
                            roamer.Facing = Toward(roamer.Position, player);
                            events.Add(new RoamerEvent(RoamerEventKind.Noticed, roamer, roamer.Position));
                        }
                        else if (roamer.Timer <= 0f)
                        {
                            if (roamer.State == RoamerState.Wandering) Wander(roamer, player, events);
                            else ReturnHome(roamer, player, events);
                        }
                        break;
                    case RoamerState.Noticing:
                        if (roamer.Timer <= 0f)
                        {
                            roamer.State = RoamerState.Chasing;
                            roamer.Timer = 0f;
                        }
                        break;
                    case RoamerState.Chasing:
                        if (d2 > Rules.GiveUpRadius * Rules.GiveUpRadius)
                        {
                            StartReturning(roamer);
                            events.Add(new RoamerEvent(RoamerEventKind.GaveUp, roamer, roamer.Position));
                            break;
                        }
                        // 한 Tick이 길어도 쫓는 속도를 넘지 않게 걸음 단위로
                        while (roamer.Timer <= 0f && roamer.State == RoamerState.Chasing)
                        {
                            roamer.Timer += Rules.ChaseStepSeconds;
                            if (Chase(roamer, player, events)) break;
                        }
                        break;
                }
            }
            return events;
        }

        // 반환값: 주인공에게 부딪혔는지
        private bool Chase(Roamer roamer, Vector2Int player, List<RoamerEvent> events)
        {
            var next = NextStepToward(roamer.Position, player, Rules.GiveUpRadius + 6, roamer.Aquatic);
            if (!next.HasValue) return false;
            roamer.Facing = Toward(roamer.Position, next.Value);
            if (next.Value == player)
            {
                events.Add(new RoamerEvent(RoamerEventKind.Caught, roamer, roamer.Position));
                return true;
            }
            Move(roamer, next.Value, events);
            return false;
        }

        private void Wander(Roamer roamer, Vector2Int player, List<RoamerEvent> events)
        {
            roamer.Timer = Rules.WanderStepSeconds;
            if (rng.NextDouble() < Rules.WanderPauseChance) return;
            var direction = Directions[rng.Next(Directions.Length)];
            var target = roamer.Position + direction.ToOffset();
            roamer.Facing = direction;
            if (target == player || !CanStand(target, roamer.Aquatic) || Distance2(target, roamer.Home) > Rules.LeashRadius * Rules.LeashRadius) return;
            Move(roamer, target, events);
        }

        private void ReturnHome(Roamer roamer, Vector2Int player, List<RoamerEvent> events)
        {
            roamer.Timer = Rules.WanderStepSeconds * 0.5f;
            if (Distance2(roamer.Position, roamer.Home) <= (Rules.LeashRadius / 2) * (Rules.LeashRadius / 2))
            {
                roamer.State = RoamerState.Wandering;
                return;
            }
            var next = NextStepToward(roamer.Position, roamer.Home, Rules.GiveUpRadius + Rules.LeashRadius + 10, roamer.Aquatic);
            if (!next.HasValue || next.Value == player || !CanStand(next.Value, roamer.Aquatic))
            {
                roamer.State = RoamerState.Wandering; // 길이 막혔으면 그 자리에서 다시 어슬렁
                return;
            }
            roamer.Facing = Toward(roamer.Position, next.Value);
            Move(roamer, next.Value, events);
        }

        private void StartReturning(Roamer roamer)
        {
            roamer.State = RoamerState.Returning;
            roamer.Timer = Rules.WanderStepSeconds;
        }

        private void Move(Roamer roamer, Vector2Int target, List<RoamerEvent> events)
        {
            var from = roamer.Position;
            roamer.Position = target;
            events.Add(new RoamerEvent(RoamerEventKind.Moved, roamer, from));
        }

        // 몬스터가 설 수 있는 칸: 길·모래·덤불·얕은 강, 다른 몬스터가 없는 곳 (출입구·물건·나루터·유적 칸은 아님).
        // 물에 사는 몬스터는 뗏목이 다니는 깊은 강만
        private bool CanStand(Vector2Int cell, bool aquatic)
        {
            if (aquatic)
            {
                if (!map.IsRaftWater(cell)) return false;
            }
            else
            {
                var tile = map.Get(cell);
                if (tile != FieldTile.Floor && tile != FieldTile.Lawn && tile != FieldTile.Grass && tile != FieldTile.Stream) return false;
            }
            return At(cell) == null;
        }

        // 너비 우선 탐색으로 목표까지의 첫 걸음 (목표 칸 자체는 막혀 있어도 됨 — 주인공 자리). 길이 없으면 null
        private Vector2Int? NextStepToward(Vector2Int from, Vector2Int goal, int searchRadius, bool aquatic)
        {
            if (from == goal) return null;
            var parent = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            int limit = searchRadius * searchRadius;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction.ToOffset();
                    if (parent.ContainsKey(next) || Distance2(next, from) > limit) continue;
                    if (next != goal && !CanStand(next, aquatic)) continue;
                    parent[next] = cell;
                    if (next == goal)
                    {
                        var step = next;
                        while (parent[step] != from) step = parent[step];
                        return step;
                    }
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        private Roamer TrySpawn(Vector2Int player, bool aquatic)
        {
            var cells = aquatic ? waterHabitat : habitat;
            if (cells.Count == 0) return null;
            int far = Rules.SpawnMinDistance * Rules.SpawnMinDistance;
            int spacing = Rules.MinSpacing * Rules.MinSpacing;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                var cell = cells[rng.Next(cells.Count)];
                if (Distance2(cell, player) < far || !CanStand(cell, aquatic)) continue;
                if (roamers.Exists(r => Distance2(r.Position, cell) < spacing)) continue;
                var roamer = new Roamer(nextId++, cell, aquatic ? rollWaterGroup(rng) : rollGroup(rng), aquatic);
                roamer.Timer = (float)rng.NextDouble() * Rules.WanderStepSeconds; // 모두 같은 박자로 걷지 않게
                roamers.Add(roamer);
                return roamer;
            }
            return null;
        }

        private static int Distance2(Vector2Int a, Vector2Int b) => (a - b).sqrMagnitude;

        private static Direction Toward(Vector2Int from, Vector2Int to)
        {
            var d = to - from;
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) return d.x >= 0 ? Direction.Right : Direction.Left;
            return d.y >= 0 ? Direction.Up : Direction.Down;
        }
    }
}
