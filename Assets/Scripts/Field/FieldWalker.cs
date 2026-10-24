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
        public bool Boarded { get; } // 나루터에서 깊은 강으로 → 뗏목에 탐 (#80)
        public bool Landed { get; }  // 뗏목으로 나루터에 닿음 → 내림

        public StepOutcome(StepKind kind, Vector2Int target, FieldTile targetTile, bool boarded = false, bool landed = false)
        {
            Kind = kind;
            Target = target;
            TargetTile = targetTile;
            Boarded = boarded;
            Landed = landed;
        }
    }

    // 격자 위 한 칸씩 이동. 막힌 칸 쪽으로 누르면 방향만 바뀐다 (상자·제단 등은 그쪽을 보고 [확인]).
    // 뗏목 (#80, 사용자 결정 '직접 몰기'): 나루터에서 깊은 강 쪽으로 가면 타고, 그 강 위만 다니다가 나루터에 닿으면 내린다 (땅에는 못 올라감)
    public class FieldWalker
    {
        private readonly Func<Vector2Int, bool> isLockedDoor;
        private readonly Func<Vector2Int, bool> isCleared;

        public FieldMap Map { get; }
        public Vector2Int Position { get; private set; }
        public Direction Facing { get; private set; } = Direction.Down;
        public bool Rafting { get; private set; }

        // lockedDoor: 그 칸의 출입구가 지금 잠겨 있는지 (없으면 모든 출입구가 열림)
        // cleared: 원래 막힌 칸이지만 이제 지나갈 수 있는 칸 (쓰러뜨린 보스 자리 — 사전이 쉼터로 옮겨져 빈자리, #45)
        // rafting: 뗏목에 탄 채로 시작 (세이브가 강 위였을 때)
        public FieldWalker(FieldMap map, Vector2Int position, Func<Vector2Int, bool> lockedDoor = null, Func<Vector2Int, bool> cleared = null,
            bool rafting = false)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            isLockedDoor = lockedDoor;
            isCleared = cleared;
            WarpTo(position, rafting);
        }

        public bool CanStand(Vector2Int position) => CanStand(position, Rafting);

        // rafting: 뗏목에 탄 채로 설 수 있는지 (뗏목은 강 물 위, 걸어서는 걸을 수 있는 칸)
        public bool CanStand(Vector2Int position, bool rafting) =>
            rafting ? Map.IsRaftWater(position)
                    : Map.IsWalkable(position) || (isCleared != null && Map.InBounds(position) && isCleared(position));

        public StepOutcome TryStep(Direction direction)
        {
            Facing = direction;
            var target = Position + direction.ToOffset();
            var tile = Map.Get(target);

            if (Rafting)
            {
                if (Map.IsRaftWater(target))
                {
                    Position = target;
                    return new StepOutcome(StepKind.Moved, target, tile);
                }
                if (tile == FieldTile.Dock)
                {
                    Rafting = false;
                    Position = target;
                    return new StepOutcome(StepKind.Moved, target, tile, landed: true);
                }
                return new StepOutcome(FieldMap.IsInteractive(tile) ? StepKind.BlockedByObject : StepKind.Blocked, target, tile);
            }

            if (tile == FieldTile.Door && isLockedDoor != null && isLockedDoor(target))
                return new StepOutcome(StepKind.BlockedByGate, target, tile);

            if (Map.Get(Position) == FieldTile.Dock && Map.IsRaftWater(target))
            {
                Rafting = true;
                Position = target;
                return new StepOutcome(StepKind.Moved, target, tile, boarded: true);
            }

            if (FieldMap.IsWalkable(tile) || (isCleared != null && isCleared(target)))
            {
                Position = target;
                return new StepOutcome(StepKind.Moved, target, tile);
            }
            return new StepOutcome(FieldMap.IsInteractive(tile) ? StepKind.BlockedByObject : StepKind.Blocked, target, tile);
        }

        // [확인]으로 옆 칸을 쓸 때 그쪽을 바라보게
        public void Face(Direction direction) => Facing = direction;

        // rafting: 뗏목에 탄 채로 (세이브에서 강 위로 돌아올 때)
        public void WarpTo(Vector2Int position, bool rafting = false)
        {
            if (!CanStand(position, rafting))
                throw new ArgumentException($"{position}은(는) 걸을 수 없는 칸입니다", nameof(position));
            Position = position;
            Rafting = rafting;
        }
    }

    // 풀숲 조우 판정. 직전 조우 후 최소 걸음 수가 지나야 확률 판정을 시작해 연속 조우로 지치지 않게 한다
    public class EncounterCounter
    {
        // 처음 하는 사람 (#133 사용자: 첫 풀숲에서 몬스터를 못 만나면 뭘 하라는 건지 모름): 풀숲 두 번째 칸에서 반드시
        public const int FirstFightGrassSteps = 2;

        private readonly float rate;
        private readonly int minStepsBetween;
        private int guaranteedIn; // 반드시 만나기까지 남은 풀숲 걸음 (0 = 없음)

        public int StepsSinceLast { get; private set; }
        public bool IsGuaranteed => guaranteedIn > 0;

        public EncounterCounter(float rate, int minStepsBetween)
        {
            this.rate = rate;
            this.minStepsBetween = minStepsBetween;
        }

        // 지금부터 풀숲을 grassSteps칸 밟으면 확률·최소 걸음과 상관없이 조우 (한 번만)
        public void GuaranteeWithin(int grassSteps) => guaranteedIn = Math.Max(0, grassSteps);

        public bool OnStep(bool onGrass, System.Random rng)
        {
            if (!onGrass) return false;
            StepsSinceLast++;
            if (guaranteedIn > 0 && --guaranteedIn == 0)
            {
                StepsSinceLast = 0;
                return true;
            }
            if (StepsSinceLast <= minStepsBetween) return false;
            if (rng.NextDouble() >= rate) return false;
            StepsSinceLast = 0;
            guaranteedIn = 0;
            return true;
        }

        public void Reset() => StepsSinceLast = 0;
    }
}
