using UnityEngine;

namespace WordRPG.Field
{
    // 가상 스틱 값(-1~1, 손잡이가 받침 가장자리까지 가면 길이 1) → 4방향 + 달리기 (Figma 'Virtual Stick' #34)
    //  - 조금만 밀면(DeadZone 미만) 멈춤, 살짝 밀면 걷기, 끝까지(RunThreshold 이상) 밀면 달리기
    //  - 대각선 근처에서 방향이 왔다 갔다 하지 않게, 지금 방향의 축을 조금 더 오래 유지한다 (다른 축이 Stickiness배 커야 바꿈)
    public static class StickInput
    {
        public const float DeadZone = 0.3f;
        public const float RunThreshold = 0.85f;
        public const float Stickiness = 1.3f;

        public static Direction? ToDirection(Vector2 value, Direction? current)
        {
            if (value.magnitude < DeadZone) return null;
            float ax = Mathf.Abs(value.x), ay = Mathf.Abs(value.y);
            bool horizontal;
            if (current == Direction.Left || current == Direction.Right) horizontal = ay <= ax * Stickiness;
            else if (current == Direction.Up || current == Direction.Down) horizontal = ax > ay * Stickiness;
            else horizontal = ax > ay;
            if (horizontal) return value.x > 0 ? Direction.Right : Direction.Left;
            return value.y > 0 ? Direction.Up : Direction.Down;
        }

        public static bool IsRunning(Vector2 value) => value.magnitude >= RunThreshold;
    }
}
