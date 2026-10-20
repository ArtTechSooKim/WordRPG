using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WordRPG.Field
{
    public enum FieldTile
    {
        Floor,    // 길
        Grass,    // 풀숲 — 걸으면 야생 몬스터 조우
        Wall,     // 나무·바위 (막힘)
        Water,    // 물 (막힘)
        Fountain, // 회복의 샘 (막힘, 옆에서 [확인] → HP 회복)
        Chest,    // 보물상자 (막힘, 옆에서 [확인] → 열기)
        Altar,    // 성유물 제단 (막힘, 옆에서 [확인] → 성유물 강화 화면)
        Shop,     // 상점 (막힘, 옆에서 [확인] → 상점 화면)
        Door,     // 출입구 — 걸어 들어가면 다른 지역으로 이동 (보스를 물리쳐야 열리는 것도 있음: AreaExit)
        Boss,     // 보스 (막힘, 옆에서 [확인] → 보스 전투)
        Lawn,     // 짧은 잔디·카펫·이끼 땅 — 걸을 수 있고 조우 없음 (탁 트인 곳)
        Lectern,  // 사전 받침대 (막힘, 옆에서 [확인] → 보스의 사전 읽기, 하루 한 번). 그 지역 보스를 물리치기 전엔 비어 있음 (#45)
        Gate,     // 보스 방 문 (#76): 그 지역 열쇠 + 단어 40개 발견 → [확인]으로 열림, 열리면 지나갈 수 있음 (BossGate)
        Key,      // 열쇠 받침대 (#76, 막힘): [확인] → 그 지역 보스 방 열쇠를 얻음
        // ---- 1.1 메아리 정글 (#80, GDD 9-6) — 지금은 정글 테마에서만 씀 (FieldArt.UsesTile)
        Stream,   // 얕은 강 '>' '<' '^' 'v': 걸을 수 있고 조우 없음, 물살 방향으로 걸으면 빨라짐 (방향 = StreamDirection)
        Dock,     // 나루터 'J': 걸을 수 있음. 여기서 깊은 강 쪽으로 가면 뗏목에 타고, 뗏목으로 나루터에 닿으면 내림
        Lever,    // 레버 'Y' (막힘): [확인] → 가장 가까운 덩굴 다리가 놓임
        Bridge,   // 덩굴 다리 'H': 레버를 당기기 전엔 막힘(희미한 윤곽만), 당기면 지나갈 수 있음
        RuinWall, // 유적 돌벽 'U' (막힘)
        RuinFloor,// 유적 바닥 ';': 걸을 수 있고 조우 없음 (보이는 몬스터도 들어오지 않음)
        Tablet,   // 단어 석판 'T' (막힘): [확인] → 빈칸 단어 문제
        Shrine,   // 유적 제단 'A' (막힘): 석판을 다 풀고 단어를 모으면 동료(아라)가 깨어남
        Cave,     // 동굴 입구 'O' (막힘): 1.2 어두운 밀림 동굴로 이어질 자리 — 지금은 무너진 바위로 막힘
        Decor     // 꾸밈 'Q' (#86, 막힘): 둘레를 보고 상자·통나무·그루터기·바위·이끼 기둥·석상 중 하나 (FieldDecor.Variant)
    }

    // 지역 분위기에 따라 같은 칸도 다르게 그린다 (초원: 나무·풀숲·물 / 서고: 책장·흩어진 책장·잉크 웅덩이 / 숲: 짙은 덤불·고사리·늪)
    // 에셋에 번호로 저장되므로 새 테마는 맨 뒤에 추가
    public enum FieldTheme
    {
        Meadow,
        Library,
        Forest,
        Jungle    // 1.1 메아리 정글 (#80)
    }

    public static class FieldThemes
    {
        // 그 테마의 맵에 쓸 수 있는 칸 — 정글 칸(얕은 강·나루터·레버·덩굴 다리·유적·석판·제단·동굴)은 정글에서만 (#80).
        // 그림(Tiles/{테마}_{종류}.png)도 이 표대로 있어야 함 (AudioArtTests). 다른 테마에서 쓰려면 여기에 더하고 그림을 만든다
        public static bool Supports(FieldTheme theme, FieldTile tile) => tile < FieldTile.Stream || theme == FieldTheme.Jungle;
    }

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class DirectionExtensions
    {
        public static Vector2Int ToOffset(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Vector2Int.up;
                case Direction.Down: return Vector2Int.down;
                case Direction.Left: return Vector2Int.left;
                default: return Vector2Int.right;
            }
        }
    }

    // 맵 텍스트 → 격자.
    //   '.' 길   ':' 잔디(조우 없음)   ',' 풀숲(조우)   '#' 나무(막힘)   '~' 물(막힘)   'P' 시작 위치(길)
    //   옆에서 [확인]으로 쓰는 칸: 'F' 회복의 샘   'C' 보물상자   'E' 성유물 제단   'S' 상점   'B' 보스   'L' 사전 받침대   'G' 보스 방 문   'K' 열쇠
    //   'D' 출입구 (밟으면 다른 지역으로)
    //   정글 (#80): '>' '<' '^' 'v' 얕은 강(물살 방향)   'J' 나루터   'Y' 레버   'H' 덩굴 다리   'U' 유적 돌벽   ';' 유적 바닥
    //               'T' 단어 석판   'A' 유적 제단   'O' 동굴 입구   'Q' 꾸밈(#86, 막힘)
    // 좌표: x는 오른쪽, y는 위쪽 (맨 아래 줄이 y=0) — Unity 월드 좌표와 같은 방향
    public class FieldMap
    {
        private readonly FieldTile[,] tiles;
        private readonly List<Vector2Int> chests; // 위→아래, 왼→오른 순 (보물상자 내용물 배정 순서)
        private readonly List<Vector2Int> doors;  // 같은 순서 (출입구 연결 배정 순서)
        private readonly Dictionary<Vector2Int, Direction> streams; // 얕은 강 칸의 물살 방향
        private HashSet<Vector2Int> raftWater;    // 나루터에서 뗏목으로 갈 수 있는 깊은 물 (처음 물을 때 계산)
        private List<List<Vector2Int>> bridges;   // 이어진 덩굴 다리 칸 덩어리들

        public int Width { get; }
        public int Height { get; }
        public Vector2Int Start { get; }
        public IReadOnlyList<Vector2Int> Chests => chests;
        public IReadOnlyList<Vector2Int> Doors => doors;
        public Vector2Int? BossPosition { get; }

        private FieldMap(FieldTile[,] tiles, Vector2Int start, List<Vector2Int> chests, List<Vector2Int> doors,
            Vector2Int? boss, Dictionary<Vector2Int, Direction> streams)
        {
            this.tiles = tiles;
            this.chests = chests;
            this.doors = doors;
            this.streams = streams;
            BossPosition = boss;
            Width = tiles.GetLength(0);
            Height = tiles.GetLength(1);
            Start = start;
        }

        public static FieldMap Parse(string text)
        {
            var rows = new List<string>();
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Length > 0) rows.Add(line);
            }
            if (rows.Count == 0) throw new FormatException("맵이 비어 있습니다");

            int width = rows[0].Length;
            int height = rows.Count;
            var tiles = new FieldTile[width, height];
            var chests = new List<Vector2Int>();
            var doors = new List<Vector2Int>();
            var streams = new Dictionary<Vector2Int, Direction>();
            Vector2Int? start = null;
            Vector2Int? boss = null;

            for (int r = 0; r < height; r++)
            {
                if (rows[r].Length != width)
                    throw new FormatException($"맵 {r + 1}번째 줄 길이가 {rows[r].Length}입니다 (첫 줄은 {width})");

                int y = height - 1 - r;
                for (int x = 0; x < width; x++)
                {
                    char c = rows[r][x];
                    switch (c)
                    {
                        case '.': tiles[x, y] = FieldTile.Floor; break;
                        case ':': tiles[x, y] = FieldTile.Lawn; break;
                        case ',': tiles[x, y] = FieldTile.Grass; break;
                        case '#': tiles[x, y] = FieldTile.Wall; break;
                        case '~': tiles[x, y] = FieldTile.Water; break;
                        case 'F': tiles[x, y] = FieldTile.Fountain; break;
                        case 'E': tiles[x, y] = FieldTile.Altar; break;
                        case 'S': tiles[x, y] = FieldTile.Shop; break;
                        case 'L': tiles[x, y] = FieldTile.Lectern; break;
                        case 'G': tiles[x, y] = FieldTile.Gate; break;
                        case 'K': tiles[x, y] = FieldTile.Key; break;
                        case '>': case '<': case '^': case 'v':
                            tiles[x, y] = FieldTile.Stream;
                            streams[new Vector2Int(x, y)] = c == '>' ? Direction.Right : c == '<' ? Direction.Left : c == '^' ? Direction.Up : Direction.Down;
                            break;
                        case 'J': tiles[x, y] = FieldTile.Dock; break;
                        case 'Y': tiles[x, y] = FieldTile.Lever; break;
                        case 'H': tiles[x, y] = FieldTile.Bridge; break;
                        case 'U': tiles[x, y] = FieldTile.RuinWall; break;
                        case ';': tiles[x, y] = FieldTile.RuinFloor; break;
                        case 'T': tiles[x, y] = FieldTile.Tablet; break;
                        case 'A': tiles[x, y] = FieldTile.Shrine; break;
                        case 'O': tiles[x, y] = FieldTile.Cave; break;
                        case 'Q': tiles[x, y] = FieldTile.Decor; break;
                        case 'D':
                            tiles[x, y] = FieldTile.Door;
                            doors.Add(new Vector2Int(x, y));
                            break;
                        case 'B':
                            if (boss.HasValue) throw new FormatException("보스 'B'는 지역마다 하나만 둘 수 있습니다");
                            tiles[x, y] = FieldTile.Boss;
                            boss = new Vector2Int(x, y);
                            break;
                        case 'C':
                            tiles[x, y] = FieldTile.Chest;
                            chests.Add(new Vector2Int(x, y));
                            break;
                        case 'P':
                            if (start.HasValue) throw new FormatException("시작 위치 'P'가 두 개 이상입니다");
                            tiles[x, y] = FieldTile.Floor;
                            start = new Vector2Int(x, y);
                            break;
                        default:
                            throw new FormatException($"맵 {r + 1}번째 줄 {x + 1}번째 칸: 알 수 없는 글자 '{c}'");
                    }
                }
            }

            if (!start.HasValue) throw new FormatException("시작 위치 'P'가 없습니다");
            return new FieldMap(tiles, start.Value, chests, doors, boss, streams);
        }

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;

        // 맵 밖은 벽으로 취급
        public FieldTile Get(Vector2Int p) => InBounds(p) ? tiles[p.x, p.y] : FieldTile.Wall;

        public static bool IsWalkable(FieldTile tile) =>
            tile == FieldTile.Floor || tile == FieldTile.Lawn || tile == FieldTile.Grass || tile == FieldTile.Door
            || tile == FieldTile.Stream || tile == FieldTile.Dock || tile == FieldTile.RuinFloor;

        // 걸을 수는 없지만 옆에서 [확인]을 누르면 무언가 일어나는 칸
        public static bool IsInteractive(FieldTile tile) =>
            tile == FieldTile.Chest || tile == FieldTile.Fountain || tile == FieldTile.Altar || tile == FieldTile.Shop
            || tile == FieldTile.Boss || tile == FieldTile.Lectern || tile == FieldTile.Gate || tile == FieldTile.Key
            || tile == FieldTile.Lever || tile == FieldTile.Tablet || tile == FieldTile.Shrine || tile == FieldTile.Cave;

        // 얕은 강의 물살 방향 (얕은 강이 아니면 null)
        public Direction? StreamDirection(Vector2Int p) => streams.TryGetValue(p, out var d) ? d : (Direction?)null;

        // 이 칸에서 그 방향으로 한 칸 가는 것이 물살을 따라가는 것인지 (얕은 강 위에서 물살 방향으로 → 빨라짐)
        public bool IsWithStream(Vector2Int from, Direction direction) =>
            StreamDirection(from) == direction && Get(from + direction.ToOffset()) == FieldTile.Stream;

        // 뗏목이 다닐 수 있는 깊은 물: 나루터 옆 물에서 이어진 물 칸 전부 (골짜기처럼 나루터가 없는 물은 아님)
        public bool IsRaftWater(Vector2Int p)
        {
            if (raftWater == null)
            {
                raftWater = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                foreach (var dock in FindAll(FieldTile.Dock))
                foreach (var step in Steps)
                {
                    var next = dock + step;
                    if (Get(next) == FieldTile.Water && raftWater.Add(next)) queue.Enqueue(next);
                }
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var step in Steps)
                    {
                        var next = cell + step;
                        if (Get(next) == FieldTile.Water && raftWater.Add(next)) queue.Enqueue(next);
                    }
                }
            }
            return raftWater.Contains(p);
        }

        // 뗏목이 다니는 물 칸 전부 (보이는 몬스터 중 물에 사는 것의 서식지)
        public List<Vector2Int> RaftWaterCells()
        {
            IsRaftWater(Start);
            return new List<Vector2Int>(raftWater);
        }

        // 레버가 놓는 덩굴 다리: 레버에서 가장 가까운 다리 덩어리 (맵에 다리가 없으면 빈 목록)
        public IReadOnlyList<Vector2Int> BridgeOf(Vector2Int lever)
        {
            List<Vector2Int> best = null;
            int bestDistance = int.MaxValue;
            foreach (var group in Bridges())
            foreach (var cell in group)
            {
                int d = Mathf.Abs(cell.x - lever.x) + Mathf.Abs(cell.y - lever.y);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = group;
                }
            }
            return best ?? (IReadOnlyList<Vector2Int>)Array.Empty<Vector2Int>();
        }

        // 이어진 나루터 칸 덩어리 (#121 2×2 부두) — 그 칸이 든 덩어리 (나루터가 아니면 빈 목록)
        public List<Vector2Int> DockGroup(Vector2Int cell)
        {
            var group = new List<Vector2Int>();
            if (Get(cell) != FieldTile.Dock) return group;
            var seen = new HashSet<Vector2Int> { cell };
            var queue = new Queue<Vector2Int>(seen);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                group.Add(c);
                foreach (var step in Steps)
                    if (Get(c + step) == FieldTile.Dock && seen.Add(c + step)) queue.Enqueue(c + step);
            }
            group.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x)); // 위→아래, 왼→오른
            return group;
        }

        // 나루터 덩어리마다 뗏목을 묶어 둘 물 칸 (#121): 부두가 뭍(섬)에 붙은 쪽의 반대 = 강 쪽 끝에 (부두 위에 얹혀 보이지 않게),
        // 그쪽에 뗏목 물이 없으면 앞이 가장 넓게 트인 칸. 묶인 뗏목은 보이기만 함 — 타는 것은 어느 나루터 칸에서든 물 쪽으로
        public List<(Vector2Int water, Vector2Int step)> MooringSpots()
        {
            var spots = new List<(Vector2Int, Vector2Int)>();
            var done = new HashSet<Vector2Int>();
            foreach (var dock in FindAll(FieldTile.Dock))
            {
                if (done.Contains(dock)) continue;
                var group = DockGroup(dock);
                done.UnionWith(group);
                Vector2Int landSide = Steps[0];
                int most = -1;
                foreach (var step in Steps)
                {
                    int contacts = group.Count(c => Get(c + step) != FieldTile.Dock && IsWalkable(c + step));
                    if (contacts > most)
                    {
                        most = contacts;
                        landSide = step;
                    }
                }
                Vector2Int? best = null, bestStep = null;
                int bestScore = -1;
                foreach (var cell in group)
                foreach (var step in Steps)
                {
                    var water = cell + step;
                    if (!IsRaftWater(water)) continue;
                    int open = 0;
                    while (open < 8 && IsRaftWater(water + step * (open + 1))) open++;
                    int score = (step == -landSide ? 100 : 0) + open;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = water;
                        bestStep = step;
                    }
                }
                if (best.HasValue) spots.Add((best.Value, bestStep.Value));
            }
            return spots;
        }

        // 덩굴 다리를 가로(왼↔오른)로 건너는지: 다리 줄의 왼쪽·오른쪽 끝 너머가 물이 아니면 가로 (그림 방향)
        public bool IsBridgeHorizontal(Vector2Int bridge)
        {
            foreach (var step in new[] { Vector2Int.left, Vector2Int.right })
            {
                var cell = bridge + step;
                while (Get(cell) == FieldTile.Bridge) cell += step;
                if (Get(cell) != FieldTile.Water && Get(cell) != FieldTile.Wall) return true;
            }
            return false;
        }

        // 그 다리 칸을 놓는 레버 (다리가 아니거나 맡은 레버가 없으면 null) — 레버마다 가장 가까운 다리를 맡는다
        public Vector2Int? LeverOf(Vector2Int bridge)
        {
            if (Get(bridge) != FieldTile.Bridge) return null;
            foreach (var lever in FindAll(FieldTile.Lever))
            {
                foreach (var cell in BridgeOf(lever))
                    if (cell == bridge) return lever;
            }
            return null;
        }

        private List<List<Vector2Int>> Bridges()
        {
            if (bridges != null) return bridges;
            bridges = new List<List<Vector2Int>>();
            var seen = new HashSet<Vector2Int>();
            foreach (var start in FindAll(FieldTile.Bridge))
            {
                if (!seen.Add(start)) continue;
                var group = new List<Vector2Int> { start };
                var queue = new Queue<Vector2Int>(group);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var step in Steps)
                    {
                        var next = cell + step;
                        if (Get(next) != FieldTile.Bridge || !seen.Add(next)) continue;
                        group.Add(next);
                        queue.Enqueue(next);
                    }
                }
                bridges.Add(group);
            }
            return bridges;
        }

        private static readonly Vector2Int[] Steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // 맵에서 그 종류의 칸 위치 (없으면 null) — 사전 받침대처럼 하나뿐인 칸
        public Vector2Int? Find(FieldTile tile)
        {
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (tiles[x, y] == tile) return new Vector2Int(x, y);
            return null;
        }

        public bool IsWalkable(Vector2Int p) => IsWalkable(Get(p));

        // 그 종류의 칸 전부 (보스 방 문처럼 여러 칸이 한 덩어리인 것)
        public List<Vector2Int> FindAll(FieldTile tile)
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (tiles[x, y] == tile) cells.Add(new Vector2Int(x, y));
            return cells;
        }

        public int ChestIndex(Vector2Int p) => chests.IndexOf(p);

        public int DoorIndex(Vector2Int p) => doors.IndexOf(p);
    }
}
