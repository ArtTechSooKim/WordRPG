using NUnit.Framework;
using UnityEngine;
using WordRPG.Field;

namespace WordRPG.Tests
{
    // 가상 스틱 → 4방향·달리기 규칙
    public class StickInputTests
    {
        [Test]
        public void SmallPushDoesNothing()
        {
            Assert.IsNull(StickInput.ToDirection(Vector2.zero, null));
            Assert.IsNull(StickInput.ToDirection(new Vector2(0.2f, 0.1f), Direction.Up), "살짝 닿기만 하면 멈춤");
        }

        [Test]
        public void StrongerAxisWins()
        {
            Assert.AreEqual(Direction.Right, StickInput.ToDirection(new Vector2(0.6f, 0.2f), null));
            Assert.AreEqual(Direction.Left, StickInput.ToDirection(new Vector2(-0.6f, -0.2f), null));
            Assert.AreEqual(Direction.Up, StickInput.ToDirection(new Vector2(0.1f, 0.7f), null));
            Assert.AreEqual(Direction.Down, StickInput.ToDirection(new Vector2(-0.2f, -0.5f), null));
        }

        [Test]
        public void NearDiagonalKeepsCurrentDirection()
        {
            var diagonal = new Vector2(0.5f, 0.55f); // 위가 조금 더 크지만 대각선 근처
            Assert.AreEqual(Direction.Right, StickInput.ToDirection(diagonal, Direction.Right), "오른쪽으로 가던 중이면 오른쪽 유지");
            Assert.AreEqual(Direction.Up, StickInput.ToDirection(diagonal, Direction.Up));
            Assert.AreEqual(Direction.Up, StickInput.ToDirection(new Vector2(0.3f, 0.8f), Direction.Right), "확실히 위로 밀면 바꿈");
            Assert.AreEqual(Direction.Left, StickInput.ToDirection(new Vector2(-0.6f, 0.1f), Direction.Right), "같은 축의 반대쪽은 바로 바꿈");
        }

        [Test]
        public void FullPushRuns()
        {
            Assert.IsFalse(StickInput.IsRunning(new Vector2(0.6f, 0f)), "살짝 밀면 걷기");
            Assert.IsTrue(StickInput.IsRunning(new Vector2(0.9f, 0f)), "끝까지 밀면 달리기");
            Assert.IsTrue(StickInput.IsRunning(new Vector2(0.7f, 0.7f)), "대각선도 길이로 판단");
        }
    }
}
