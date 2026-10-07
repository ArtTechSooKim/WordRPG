using System;
using UnityEngine;

namespace WordRPG.Field
{
    public enum StepKind
    {
        Moved,
        Blocked,    // 나무·물·맵 끝 — 방향만 바뀜
        BlockedByObject, // 상자·샘·제단·상점·보스에 막힘 — 사용은 [확인] 버튼으로 (FieldInteraction)
        BlockedByGate    // 잠긴 출입구 — 보스를 물리쳐야 열림 (AreaExit.OpenedByBossOf)
    }

    public readonly struct StepOutcome
    {
        public StepKind Kind { get; }
        public Vector2Int Target { get; }
        public FieldTile TargetTile { get; }

        public bool EnteredGrass => Kind == StepKind.Moved && TargetTile == FieldTile.Grass;
        public bool EnteredDoor => Kind == StepKind.Moved && TargetTile == FieldTile.Door;

        public StepOutcome(StepKind kind, Vector2Int target, FieldTile targetTile)
        {
            Kind = kind;
            Target = target;
            TargetTile = targetTile;
        }
    }

    // 격자 위 한 칸씩 이동. 막힌 칸 쪽으로 누르면 방향만 바뀐다 (상자·제단 등은 그쪽을 보고 [확인])
    public class FieldWalker
    {
        private readonly Func<Vector2Int, bool> isLockedDoor;
        private readonly Func<Vector2Int, bool> isCleared;

        public FieldMap Map { get; }
        public Vector2Int Position { get; private set; }
        public Direction Facing { get; private set; } = Direction.Down;

        // lockedDoor: 그 칸의 출입구가 지금 잠겨 있는지 (없으면 모든 출입구가 열림)
        // cleared: 원래 막힌 칸이지만 이제 지나갈 수 있는 칸 (쓰러뜨린 보스 자리 — 사전이 쉼터로 옮겨져 빈자리, #45)
        public FieldWalker(FieldMap map, Vector2Int position, Func<Vector2Int, bool> lockedDoor = null, Func<Vector2Int, bool> cleared = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            isLockedDoor = lockedDoor;
            isCleared = cleared;
            WarpTo(position);
        }

        public bool CanStand(Vector2Int position) => Map.IsWalkable(position) || (isCleared != null && Map.InBounds(position) && isCleared(position));

        public StepOutcome TryStep(Direction direction)
        {
            Facing = direction;
            var target = Position + direction.ToOffset();
            var tile = Map.Get(target);

            if (tile == FieldTile.Door && isLockedDoor != null && isLockedDoor(target))
                return new StepOutcome(StepKind.BlockedByGate, target, tile);

            switch (tile)
            {
                case FieldTile.Floor:
                case FieldTile.Lawn:
                case FieldTile.Grass:
                case FieldTile.Door:
                    Position = target;
                    return new StepOutcome(StepKind.Moved, target, tile);
                default:
                    if (isCleared != null && isCleared(target))
                    {
                        Position = target;
                        return new StepOutcome(StepKind.Moved, target, tile);
                    }
                    return new StepOutcome(FieldMap.IsInteractive(tile) ? StepKind.BlockedByObject : StepKind.Blocked, target, tile);
            }
        }

        // [확인]으로 옆 칸을 쓸 때 그쪽을 바라보게
        public void Face(Direction direction) => Facing = direction;

        public void WarpTo(Vector2Int position)
        {
            if (!CanStand(position))
                throw new ArgumentException($"{position}은(는) 걸을 수 없는 칸입니다", nameof(position));
            Position = position;
        }
    }

    // 풀숲 조우 판정. 직전 조우 후 최소 걸음 수가 지나야 확률 판정을 시작해 연속 조우로 지치지 않게 한다
    public class EncounterCounter
    {
        private readonly float rate;
        private readonly int minStepsBetween;

        public int StepsSinceLast { get; private set; }

        public EncounterCounter(float rate, int minStepsBetween)
        {
            this.rate = rate;
            this.minStepsBetween = minStepsBetween;
        }

        public bool OnStep(bool onGrass, System.Random rng)
        {
            if (!onGrass) return false;
            StepsSinceLast++;
            if (StepsSinceLast <= minStepsBetween) return false;
            if (rng.NextDouble() >= rate) return false;
            StepsSinceLast = 0;
            return true;
        }

        public void Reset() => StepsSinceLast = 0;
    }
}
