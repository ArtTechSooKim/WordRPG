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
        Roaming  // 보이는 몬스터가 쫓아옴 — 부딪히면 전투, 가까우면 [선제공격] (#73, 사막부터)
    }

    public enum RoamerState
    {
        Wandering, // 서식지 근처를 어슬렁
        Noticing,  // 주인공을 발견 — "!" 뜨고 잠깐 멈춤
        Chasing,   // 쫓아옴
        Returning, // 놓쳐서 집으로 돌아감
        Stunned,   // 도망친 직후 잠깐 멈춤
        // 웅덩이에 숨는 몬스터 (#113, 1.2 동굴의 먹물도롱뇽 — 보관했던 잉크 몬스터 아이디어)
        Lurking,   // 웅덩이 속에 숨어 있음 (보이지 않음)
        Surfacing  // 주인공이 가까이 옴 → 거품이 보글보글 (곧 튀어나옴)
    }

    public enum RoamerEventKind
    {
        Spawned,
        Moved,
        Noticed,
        GaveUp,
        Caught,   // 주인공에게 부딪힘 → 전투
        Surfaced, // 웅덩이에 거품이 올라옴 (#113)
        Dived     // 웅덩이로 돌아가 다시 숨음 (#113)
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
        public float RespawnSeconds = 25f;     // 쓰러뜨린 뒤 다른 곳에서 다시 나타나기까지
        public int SpawnMinDistance = 12;      // 화면 밖에서 나타남
        public int MinSpacing = 6;             // 몬스터끼리 떨어진 거리
        public int CellsPerRoamer = 120;       // 서식지 칸 수 ÷ 이 값 = 마릿수
        public int MaxRoamers = 30;
        public int WaterCellsPerRoamer = 140;  // 물에 사는 몬스터 (#80 흉내문어): 뗏목이 다니는 물 칸 수 ÷ 이 값
        public int MaxWaterRoamers = 8;
        public int ActiveRadius = 26;          // 이보다 먼 어슬렁 몬스터는 쉰다 (계산 아끼기)
        // 웅덩이에 숨는 몬스터 (#113): 나루터가 없는 맵의 물(먹물 웅덩이)이 숨는 곳. 가까이(2칸) 오면 거품 → 튀어나와 땅 위에서 쫓아옴
        public int LurkRadius = 2;
        public float SurfaceSeconds = 0.8f;    // 거품 경고 (GDD 9-7)
        public int LurkCellsPerRoamer = 30;    // 웅덩이(이어진 물) 칸 수 ÷ 이 값 = 그 웅덩이의 마릿수 (1~MaxLurkersPerPool)
        public int MaxLurkersPerPool = 2;
        public int MaxLurkers = 8;
        public int LurkSpawnMinDistance = 5;   // 바로 옆 웅덩이에서 생겨나 곧장 튀어나오지 않게
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
        public bool Lurker { get; }  // 웅덩이에 숨는 몬스터 — 집(Home)이 웅덩이 칸, 튀어나오면 땅 위를 걷는다 (#113)
        public bool Hidden => State == RoamerState.Lurking || State == RoamerState.Surfacing; // 웅덩이 속이라 몸이 안 보임
        internal float Timer;

        public Roamer(int id, Vector2Int home, List<MonsterInstance> group, bool aquatic = false, bool lurker = false)
        {
            if (group == null || group.Count == 0) throw new ArgumentException("무리가 비었습니다", nameof(group));
            Id = id;
            Home = home;
            Position = home;
            Group = group;
            Aquatic = aquatic && !lurker;
            Lurker = lurker;
            if (lurker) State = RoamerState.Lurking;
        }
    }

    // 보이는 몬스터 조우 규칙 (순수 C#, 시간은 Tick(dt), 랜덤은 주입 — GDD 9-5).
    // 서식지 = 마른 덤불(',', FieldTile.Grass) 칸. 몬스터는 길·모래·덤불·얕은 강만 걷고 출입구·물건 칸에는 서지 않는다.
    // 물에 사는 몬스터(#80, 정글의 흉내문어)는 뗏목이 다니는 깊은 강(FieldMap.IsRaftWater)이 서식지이자 다니는 곳.
    // 나루터가 없는 맵(1.2 동굴)이면 물의 몬스터는 웅덩이에 숨는 몬스터(#113, 먹물도롱뇽) — 이어진 물 덩어리마다 숨어 있다가 가까이 오면 튀어나옴
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
        private readonly List<List<Vector2Int>> pools = new List<List<Vector2Int>>(); // 웅덩이마다 숨을 수 있는 칸 (땅에 닿은 물, #113)
        private readonly List<int> poolSizes = new List<int>();
        private readonly List<float> lurkerRespawns = new List<float>();
        private readonly List<RoamerEvent> tickEvents = new List<RoamerEvent>();
        private int nextId;

        public RoamingRules Rules { get; }
        public IReadOnlyList<Roamer> Roamers => roamers;
        public IReadOnlyList<Vector2Int> Habitat => habitat;
        public IReadOnlyList<Vector2Int> WaterHabitat => waterHabitat;
        public int PendingRespawns => respawns.Count + lurkerRespawns.Count;
        public int PoolCount => pools.Count;

        // 1이면 보통, 작으면 느려짐 ([선제공격] 문제를 푸는 동안)
        public float SlowFactor { get; set; } = 1f;

        public int TargetCount => habitat.Count == 0 ? 0 : Mathf.Clamp(habitat.Count / Mathf.Max(1, Rules.CellsPerRoamer), 1, Rules.MaxRoamers);

        public int WaterTargetCount => waterHabitat.Count == 0 ? 0
            : Mathf.Clamp(waterHabitat.Count / Mathf.Max(1, Rules.WaterCellsPerRoamer), 1, Rules.MaxWaterRoamers);

        // 웅덩이에 숨는 몬스터 수 (#113): 웅덩이마다 크기에 따라 1~2마리, 모두 합쳐 MaxLurkers까지
        public int LurkerTargetCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < pools.Count; i++) total += PoolTarget(i);
                return Mathf.Min(total, Rules.MaxLurkers);
            }
        }

        private int PoolTarget(int pool) => Mathf.Clamp(poolSizes[pool] / Mathf.Max(1, Rules.LurkCellsPerRoamer), 1, Rules.MaxLurkersPerPool);

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
                if (waterHabitat.Count == 0) FindPools(); // 뗏목이 없는 물 = 숨는 웅덩이 (#113)
            }
        }

        // 이어진 물 덩어리(3칸 이상)마다, 땅(몬스터가 설 수 있는 칸)에 닿은 물 칸 = 숨을 자리
        private void FindPools()
        {
            var seen = new HashSet<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var start = new Vector2Int(x, y);
                if (map.Get(start) != FieldTile.Water || !seen.Add(start)) continue;
                var group = new List<Vector2Int> { start };
                var queue = new Queue<Vector2Int>(group);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var direction in Directions)
                    {
                        var next = cell + direction.ToOffset();
                        if (map.Get(next) != FieldTile.Water || !seen.Add(next)) continue;
                        group.Add(next);
                        queue.Enqueue(next);
                    }
                }
                var spots = group.FindAll(cell => Array.Exists(Directions, d => IsLand(cell + d.ToOffset())));
                if (group.Count < 3 || spots.Count == 0) continue;
                spots.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                pools.Add(spots);
                poolSizes.Add(group.Count);
            }
        }

        private bool IsLand(Vector2Int cell) => FieldTiles.Of(map.Get(cell)).RoamerWalkable; // 길·잔디·덤불·얕은 강

        // 지역에 들어올 때: 정해진 마릿수만큼 주인공에게서 먼 서식지에
        public List<RoamerEvent> Populate(Vector2Int player)
        {
            var events = new List<RoamerEvent>();
            foreach (bool aquatic in new[] { false, true })
            {
                int target = aquatic ? WaterTargetCount : TargetCount;
                while (roamers.FindAll(r => r.Aquatic == aquatic && !r.Lurker).Count < target)
                {
                    var roamer = TrySpawn(player, aquatic);
                    if (roamer == null) break;
                    events.Add(new RoamerEvent(RoamerEventKind.Spawned, roamer, roamer.Position));
                }
            }
            while (roamers.FindAll(r => r.Lurker).Count < LurkerTargetCount)
            {
                var lurker = TrySpawnLurker(player);
                if (lurker == null) break;
                events.Add(new RoamerEvent(RoamerEventKind.Spawned, lurker, lurker.Position));
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
            if (!roamers.Remove(roamer)) return;
            if (roamer.Lurker) lurkerRespawns.Add(Rules.RespawnSeconds);
            else respawns.Add((Rules.RespawnSeconds, roamer.Aquatic));
        }

        // 도망침 → 잠깐 멈춤 (그동안 거리를 벌림)
        public void Stun(Roamer roamer)
        {
            if (!roamers.Contains(roamer)) return;
            roamer.State = RoamerState.Stunned;
            roamer.Timer = Rules.StunSeconds;
        }

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
            for (int i = lurkerRespawns.Count - 1; i >= 0; i--)
            {
                lurkerRespawns[i] -= dt;
                if (lurkerRespawns[i] > 0f) continue;
                var lurker = TrySpawnLurker(player);
                if (lurker == null) continue;
                lurkerRespawns.RemoveAt(i);
                events.Add(new RoamerEvent(RoamerEventKind.Spawned, lurker, lurker.Position));
            }

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
                    case RoamerState.Lurking:
                        if (d2 <= Rules.LurkRadius * Rules.LurkRadius + 1) // 2칸 안 (대각선 한 칸 어긋남까지)
                        {
                            roamer.State = RoamerState.Surfacing;
                            roamer.Timer = Rules.SurfaceSeconds;
                            events.Add(new RoamerEvent(RoamerEventKind.Surfaced, roamer, roamer.Position));
                        }
                        break;
                    case RoamerState.Surfacing:
                        if (roamer.Timer <= 0f) Emerge(roamer, player, d2, events);
                        break;
                    case RoamerState.Stunned:
                        if (roamer.Timer <= 0f) StartReturning(roamer);
                        break;
                    case RoamerState.Wandering:
                    case RoamerState.Returning:
                        if (d2 <= Rules.DetectRadius * Rules.DetectRadius)
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

        // 거품 뒤 튀어나옴 (#113): 웅덩이 옆 땅 중 주인공에게 가장 가까운 칸으로 → "!" 뒤 쫓아옴.
        // 주인공이 바로 옆이면 부딪힘(전투), 그새 멀어졌으면 다시 숨음, 옆 땅이 다 막혔으면 조금 뒤 다시
        private void Emerge(Roamer roamer, Vector2Int player, int d2, List<RoamerEvent> events)
        {
            if (d2 > Rules.DetectRadius * Rules.DetectRadius)
            {
                roamer.State = RoamerState.Lurking;
                events.Add(new RoamerEvent(RoamerEventKind.Dived, roamer, roamer.Position));
                return;
            }
            Vector2Int? best = null;
            int bestDistance = int.MaxValue;
            foreach (var direction in Directions)
            {
                var cell = roamer.Position + direction.ToOffset();
                if (cell == player)
                {
                    roamer.Facing = direction;
                    events.Add(new RoamerEvent(RoamerEventKind.Caught, roamer, roamer.Position));
                    return;
                }
                if (!CanStand(cell, false)) continue;
                int d = Distance2(cell, player);
                if (d < bestDistance)
                {
                    best = cell;
                    bestDistance = d;
                }
            }
            if (!best.HasValue)
            {
                roamer.Timer = 0.2f;
                return;
            }
            roamer.Facing = Toward(roamer.Position, best.Value);
            Move(roamer, best.Value, events);
            roamer.State = RoamerState.Noticing;
            roamer.Timer = Rules.NoticeSeconds;
            roamer.Facing = Toward(roamer.Position, player);
            events.Add(new RoamerEvent(RoamerEventKind.Noticed, roamer, roamer.Position));
        }

        // 놓친 웅덩이 몬스터: 집(웅덩이 칸) 옆까지 걸어가 뛰어들어 다시 숨음. 길이 막혔으면 그 자리에서 바로 웅덩이로
        private void ReturnToPool(Roamer roamer, Vector2Int player, List<RoamerEvent> events)
        {
            roamer.Timer = Rules.WanderStepSeconds * 0.5f;
            var next = (roamer.Position - roamer.Home).sqrMagnitude <= 1 ? roamer.Home
                : NextStepToward(roamer.Position, roamer.Home, Rules.GiveUpRadius + Rules.LeashRadius + 10, false);
            if (next.HasValue && next.Value == player) return;
            if (!next.HasValue || next.Value == roamer.Home)
            {
                var from = roamer.Position;
                roamer.Position = roamer.Home;
                roamer.State = RoamerState.Lurking;
                events.Add(new RoamerEvent(RoamerEventKind.Moved, roamer, from));
                events.Add(new RoamerEvent(RoamerEventKind.Dived, roamer, from));
                return;
            }
            roamer.Facing = Toward(roamer.Position, next.Value);
            Move(roamer, next.Value, events);
        }

        private void ReturnHome(Roamer roamer, Vector2Int player, List<RoamerEvent> events)
        {
            if (roamer.Lurker)
            {
                ReturnToPool(roamer, player, events);
                return;
            }
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
            else if (!IsLand(cell))
            {
                return false;
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

        // 웅덩이에 숨는 몬스터 하나 (#113): 마릿수가 모자란 웅덩이의 숨을 자리 중 주인공에게서 조금 떨어진 곳
        private Roamer TrySpawnLurker(Vector2Int player)
        {
            if (rollWaterGroup == null) return null;
            int near = Rules.LurkSpawnMinDistance * Rules.LurkSpawnMinDistance;
            for (int i = 0; i < pools.Count; i++)
            {
                int inPool = roamers.FindAll(r => r.Lurker && pools[i].Contains(r.Home)).Count;
                if (inPool >= PoolTarget(i)) continue;
                var spots = pools[i];
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    var cell = spots[rng.Next(spots.Count)];
                    if (Distance2(cell, player) < near || At(cell) != null) continue;
                    if (roamers.Exists(r => r.Lurker && Distance2(r.Home, cell) < 9)) continue;
                    var lurker = new Roamer(nextId++, cell, rollWaterGroup(rng), false, true);
                    roamers.Add(lurker);
                    return lurker;
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
