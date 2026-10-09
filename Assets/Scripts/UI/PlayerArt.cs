using System.Collections.Generic;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.UI
{
    // 시트 맨 아래 줄의 자세 (SeparateAnim/Dead·Item·Special1·Special2와 같은 그림)
    public enum PlayerPose { Dead = 0, Item = 1, Crouch = 2, Wave = 3 }

    // 필드 주인공 그림 (Ninja Adventure 'Boy' 시트, 16×16 칸).
    // 시트: 열 = 방향(아래·위·왼쪽·오른쪽), 행 0~3 = 걷기 4프레임(첫 행 = 서 있는 모습), 4 = 공격, 5 = 점프. 시트가 없으면 임시 도트
    // 팩에 달리기 그림이 없어서, 달리기는 걷기의 내딛는 두 칸 사이에 점프 행(앞으로 숙이고 다리를 벌린 모습)을 끼워 만든다
    public static class PlayerArt
    {
        public const string SheetPath = "Art/NinjaAdventure/Player/Boy";
        public const float FramesPerSecond = 8f;
        private const int Cell = 16;
        private const int WalkFrames = 4;
        private static readonly int[] RunRows = { 1, 5, 3, 5 };

        private static Texture2D sheet;
        private static bool loaded;
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        public static Sprite Get(Direction facing, int step) => Frame(facing, Wrap(step, WalkFrames));

        // 한 장짜리 자세: 쓰러짐(다시 일어나는 연출에서 거꾸로 — 쓰러짐 → 웅크림 → 서기) 등
        public static Sprite Pose(PlayerPose pose) => CellAt((int)pose, 6, $"player_pose_{pose}");

        // 달리는 모습 (걷기 → 점프 자세 → 반대 발 → 점프 자세)
        public static Sprite GetRun(Direction facing, int step) => Frame(facing, RunRows[Wrap(step, RunRows.Length)]);

        private static int Wrap(int step, int count) => ((step % count) + count) % count;

        private static Sprite Frame(Direction facing, int row)
        {
            if (!loaded)
            {
                loaded = true;
                sheet = Resources.Load<Texture2D>(SheetPath);
            }
            if (sheet == null || (row + 1) * Cell > sheet.height) return PlaceholderArt.Player(facing);
            return CellAt(Column(facing), row, $"player_{facing}_{row}");
        }

        private static Sprite CellAt(int column, int row, string name)
        {
            if (!loaded)
            {
                loaded = true;
                sheet = Resources.Load<Texture2D>(SheetPath);
            }
            if (sheet == null || (row + 1) * Cell > sheet.height) return PlaceholderArt.Player(Direction.Down);
            int key = column * 16 + row;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var rect = new Rect(column * Cell, sheet.height - (row + 1) * Cell, Cell, Cell);
            var sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), Cell, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            Cache[key] = sprite;
            return sprite;
        }

        private static int Column(Direction facing)
        {
            switch (facing)
            {
                case Direction.Down: return 0;
                case Direction.Up: return 1;
                case Direction.Left: return 2;
                default: return 3;
            }
        }
    }
}
