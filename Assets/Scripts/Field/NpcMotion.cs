using System;
using UnityEngine;
using Random = System.Random;

namespace WordRPG.Field
{
    public enum NpcPose { None, Crouch, Item }

    // 지금 조건에 맞는 말 고르기 (#209)
    public static class NpcLines
    {
        public const string NameToken = "{이름}";

        public static NpcStage Current(NpcData npc, Func<FieldArea, bool> bossDefeated, int discoveredWords)
        {
            NpcStage current = null;
            foreach (var stage in npc.Stages)
            {
                bool bossDone = stage.BossOf == null || (bossDefeated != null && bossDefeated(stage.BossOf));
                if (bossDone && discoveredWords >= stage.WordsNeeded) current = stage;
            }
            return current;
        }

        public static string Render(string line, string heroName) => (line ?? "").Replace(NameToken, heroName);
    }

    // 인물 하나의 움직임 (순수 C#, 랜덤은 주입). 칸 단위로 움직이고, 화면은 Cell·Previous·StepProgress로 그린다
    public class NpcMotion
    {
        public const int PaceRadius = 2;
        public const int WanderRadius = 3;
        private const float PaceStep = 0.45f, StrollStep = 1.1f, WanderStep = 0.3f;
        private const float HopSeconds = 0.35f;

        private readonly Random rng;
        private float timer;
        private float stepSeconds = PaceStep;
        private Direction paceDirection;
        private int busyBeats;
        private float hopT = 1f;

        public Vector2Int Home { get; }
        public Vector2Int Cell { get; private set; }
        public Vector2Int Previous { get; private set; }
        public NpcMotionKind Kind { get; private set; }
        public float Pace { get; private set; } = 1f; // 성격에 맞는 빠르기 (#211)
        public Direction Facing { get; private set; } = Direction.Down;
        public NpcPose Pose { get; private set; }
        public bool Walking { get; private set; }
        public float StepProgress { get; private set; } = 1f;
        public int StepsTaken { get; private set; }
        public bool Held { get; private set; }
        public float HopHeight => hopT < 1f ? Mathf.Sin(hopT * Mathf.PI) * 0.25f : 0f;

        public NpcMotion(Vector2Int home, NpcMotionKind kind, Random random, Vector2Int? startCell = null, float pace = 1f)
        {
            Home = home;
            Cell = Previous = startCell ?? home;
            rng = random;
            paceDirection = rng.Next(2) == 0 ? Direction.Left : Direction.Right;
            SetKind(kind, pace);
        }

        public void SetKind(NpcMotionKind kind, float pace = 1f)
        {
            if (kind == Kind && Mathf.Approximately(pace, Pace) && timer > 0f) return;
            Kind = kind;
            Pace = pace > 0f ? pace : 1f;
            Pose = NpcPose.None;
            busyBeats = 0;
            timer = Range(0.2f, 1.2f);
        }

        // 지금 차지한 칸: 걷는 동안에는 떠나는 칸도 비워 두지 않는다
        public bool Occupies(Vector2Int cell) => cell == Cell || (Walking && cell == Previous);

        // 주인공이 옆에 오거나 말을 거는 동안: 멈추고 그쪽을 본다
        public void Hold(Direction face)
        {
            Held = true;
            Facing = face;
            Pose = NpcPose.None;
        }

        public void Release() => Held = false;

        // 컷씬이 직접 옮긴 뒤 그 칸에 선 것으로 (#234 — 달려왔다 돌아가는 나무꾼)
        public void Teleport(Vector2Int cell, Direction face)
        {
            Cell = Previous = cell;
            Walking = false;
            StepProgress = 1f;
            Facing = face;
            Pose = NpcPose.None;
            timer = Range(0.5f, 1.5f);
        }

        public void Tick(float dt, Func<Vector2Int, bool> canStand)
        {
            if (hopT < 1f) hopT = Mathf.Min(1f, hopT + dt / HopSeconds);
            if (Walking)
            {
                StepProgress += dt / stepSeconds;
                if (StepProgress < 1f) return;
                StepProgress = 1f;
                Walking = false;
                Previous = Cell;
                StepsTaken++;
            }
            if (Held) return;
            timer -= dt;
            if (timer > 0f) return;

            switch (Kind)
            {
                case NpcMotionKind.Stand: LookAround(Scaled(Range(2f, 5f))); break;
                case NpcMotionKind.Pace: Pacing(PaceStep / Pace, Scaled(Range(0.8f, 2.4f)), canStand); break;
                case NpcMotionKind.Stroll: Pacing(StrollStep / Pace, Scaled(Range(1.5f, 3.5f)), canStand); break;
                case NpcMotionKind.Wander: Wandering(canStand); break;
                case NpcMotionKind.Sit: Sitting(); break;
                case NpcMotionKind.Busy: BeingBusy(); break;
                case NpcMotionKind.Hop: Hopping(); break;
            }
        }

        private void LookAround(float next)
        {
            Facing = (Direction)rng.Next(4);
            timer = next;
        }

        // 한 줄을 오가다 끝에 닿으면 돌아섬. 가끔 쉼
        private void Pacing(float seconds, float pause, Func<Vector2Int, bool> canStand)
        {
            var target = Cell + paceDirection.ToOffset();
            bool inRange = Mathf.Abs(target.x - Home.x) + Mathf.Abs(target.y - Home.y) <= PaceRadius;
            if (!inRange || !canStand(target))
            {
                paceDirection = Opposite(paceDirection);
                Facing = paceDirection;
                timer = pause;
                return;
            }
            StepTo(target, paceDirection, seconds);
            timer = rng.NextDouble() < 0.4 ? pause : 0f;
        }

        // 방향을 정하지 못하고 갈팡질팡
        private void Wandering(Func<Vector2Int, bool> canStand)
        {
            var direction = (Direction)rng.Next(4);
            Facing = direction;
            var target = Cell + direction.ToOffset();
            bool inRange = Mathf.Abs(target.x - Home.x) + Mathf.Abs(target.y - Home.y) <= WanderRadius;
            if (inRange && canStand(target) && rng.NextDouble() < 0.75) StepTo(target, direction, WanderStep / Pace);
            timer = Scaled(Range(0.05f, 0.7f));
        }

        private void Sitting()
        {
            if (Pose == NpcPose.Crouch)
            {
                Pose = NpcPose.None;
                Facing = (Direction)rng.Next(4);
                timer = Scaled(Range(0.8f, 1.6f));
            }
            else
            {
                Pose = NpcPose.Crouch;
                timer = Scaled(Range(2.5f, 5f));
            }
        }

        // 잠깐 쉬다가 두 자세를 빠르게 번갈아
        private void BeingBusy()
        {
            if (busyBeats <= 0)
            {
                if (Pose != NpcPose.None)
                {
                    Pose = NpcPose.None;
                    timer = Scaled(Range(1.2f, 3f));
                    return;
                }
                busyBeats = 4 + rng.Next(5);
            }
            Pose = Pose == NpcPose.Item ? NpcPose.Crouch : NpcPose.Item;
            busyBeats--;
            timer = Scaled(0.32f);
        }

        private void Hopping()
        {
            hopT = 0f;
            if (rng.Next(2) == 0) Facing = rng.Next(2) == 0 ? Direction.Left : Direction.Right;
            timer = Scaled(Range(0.9f, 3f));
        }

        private void StepTo(Vector2Int target, Direction direction, float seconds)
        {
            Previous = Cell;
            Cell = target;
            Facing = direction;
            Pose = NpcPose.None;
            Walking = true;
            StepProgress = 0f;
            stepSeconds = seconds;
        }

        private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        private float Scaled(float seconds) => seconds / Pace;

        private static Direction Opposite(Direction direction)
        {
            switch (direction)
            {
                case Direction.Left: return Direction.Right;
                case Direction.Right: return Direction.Left;
                case Direction.Up: return Direction.Down;
                default: return Direction.Up;
            }
        }
    }
}
