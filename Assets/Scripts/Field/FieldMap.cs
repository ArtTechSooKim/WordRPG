using System;
using System.Collections.Generic;
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
        Lectern   // 사전 받침대 (막힘, 옆에서 [확인] → 보스의 사전 읽기, 하루 한 번). 그 지역 보스를 물리치기 전엔 비어 있음 (#45)
    }

    // 지역 분위기에 따라 같은 칸도 다르게 그린다 (초원: 나무·풀숲·물 / 서고: 책장·흩어진 책장·잉크 웅덩이 / 숲: 짙은 덤불·고사리·늪)
    // 에셋에 번호로 저장되므로 새 테마는 맨 뒤에 추가
    public enum FieldTheme
    {
        Meadow,
        Library,
        Forest
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
    //   옆에서 [확인]으로 쓰는 칸: 'F' 회복의 샘   'C' 보물상자   'E' 성유물 제단   'S' 상점   'B' 보스
    //   'D' 출입구 (밟으면 다른 지역으로)
    // 좌표: x는 오른쪽, y는 위쪽 (맨 아래 줄이 y=0) — Unity 월드 좌표와 같은 방향
    public class FieldMap
    {
        private readonly FieldTile[,] tiles;
        private readonly List<Vector2Int> chests; // 위→아래, 왼→오른 순 (보물상자 내용물 배정 순서)
        private readonly List<Vector2Int> doors;  // 같은 순서 (출입구 연결 배정 순서)

        public int Width { get; }
        public int Height { get; }
        public Vector2Int Start { get; }
        public IReadOnlyList<Vector2Int> Chests => chests;
        public IReadOnlyList<Vector2Int> Doors => doors;
        public Vector2Int? BossPosition { get; }

        private FieldMap(FieldTile[,] tiles, Vector2Int start, List<Vector2Int> chests, List<Vector2Int> doors,
            Vector2Int? boss)
        {
            this.tiles = tiles;
            this.chests = chests;
            this.doors = doors;
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
            return new FieldMap(tiles, start.Value, chests, doors, boss);
        }

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;

        // 맵 밖은 벽으로 취급
        public FieldTile Get(Vector2Int p) => InBounds(p) ? tiles[p.x, p.y] : FieldTile.Wall;

        public static bool IsWalkable(FieldTile tile) =>
            tile == FieldTile.Floor || tile == FieldTile.Lawn || tile == FieldTile.Grass || tile == FieldTile.Door;

        // 걸을 수는 없지만 옆에서 [확인]을 누르면 무언가 일어나는 칸
        public static bool IsInteractive(FieldTile tile) =>
            tile == FieldTile.Chest || tile == FieldTile.Fountain || tile == FieldTile.Altar || tile == FieldTile.Shop
            || tile == FieldTile.Boss || tile == FieldTile.Lectern;

        // 맵에서 그 종류의 칸 위치 (없으면 null) — 사전 받침대처럼 하나뿐인 칸
        public Vector2Int? Find(FieldTile tile)
        {
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (tiles[x, y] == tile) return new Vector2Int(x, y);
            return null;
        }

        public bool IsWalkable(Vector2Int p) => IsWalkable(Get(p));

        public int ChestIndex(Vector2Int p) => chests.IndexOf(p);

        public int DoorIndex(Vector2Int p) => doors.IndexOf(p);
    }
}
