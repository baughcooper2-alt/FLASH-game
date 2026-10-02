using NUnit.Framework;
using UnityEngine;

namespace FlashGame.Tests
{
    public class SpeedMathTests
    {
        [Test]
        public void FastRunnerCrossingGateBetweenFramesStillCounts()
        {
            Assert.IsTrue(SpeedMath.SegmentHitsSphere(new Vector3(0, 0, -20), new Vector3(0, 0, 20), Vector3.zero, 7));
        }
        [Test]
        public void ParallelNearMissDoesNotCount()
        {
            Assert.IsFalse(SpeedMath.SegmentHitsSphere(new Vector3(8, 0, -20), new Vector3(8, 0, 20), Vector3.zero, 7));
        }
        [Test]
        public void GateBeyondTravelSegmentDoesNotCount()
        {
            Assert.IsFalse(SpeedMath.SegmentHitsSphere(Vector3.zero, Vector3.forward, Vector3.forward * 20, 7));
        }
        [Test]
        public void StationaryRunnerHasFiniteResult()
        {
            Assert.IsTrue(SpeedMath.SegmentHitsSphere(Vector3.zero, Vector3.zero, Vector3.zero, 7));
            Assert.IsFalse(SpeedMath.SegmentHitsSphere(Vector3.one * 20, Vector3.one * 20, Vector3.zero, 7));
        }
        [TestCase(0f)]
        [TestCase(6.5f)]
        [TestCase(130f)]
        public void CollisionStepsNeverExceedThreeQuartersOfAMetre(float distance)
        {
            int steps = SpeedMath.MovementSteps(distance);
            Assert.GreaterOrEqual(steps, 1);
            Assert.LessOrEqual(distance / steps, 0.75f);
        }
    }
}
