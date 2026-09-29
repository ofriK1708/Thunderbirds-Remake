using System;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class CountdownTimerTests
    {
        [Test]
        public void Expiry_ClampsAtZero_AndFiresOnce()
        {
            var timer = new CountdownTimer();
            var count = 0;
            timer.Expired += () => count++;
            timer.Start(3);
            timer.Tick(1);
            Assert.AreEqual(2, timer.Remaining);
            Assert.IsFalse(timer.HasExpired);
            timer.Tick(10);
            timer.Tick(10);
            Assert.AreEqual(0, timer.Remaining);
            Assert.IsTrue(timer.HasExpired);
            Assert.IsFalse(timer.IsRunning);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Reset_Cancels_AndStartRearms()
        {
            var timer = new CountdownTimer();
            var count = 0;
            timer.Expired += () => count++;
            timer.Start(3);
            timer.Tick(2);
            timer.Reset();
            timer.Tick(5);
            Assert.AreEqual(0, count);
            Assert.IsFalse(timer.HasExpired);
            timer.Start(1);
            timer.Tick(1);
            timer.Start(2);
            timer.Tick(2);
            Assert.AreEqual(2, count);
        }

        [Test]
        public void ZeroDuration_ExpiresOnFirstTick_AndZeroDeltaDoesNotAdvanceActiveTimer()
        {
            var timer = new CountdownTimer();
            timer.Start(0);
            Assert.IsFalse(timer.HasExpired);
            timer.Tick(0);
            Assert.IsTrue(timer.HasExpired);
            timer.Start(3);
            timer.Tick(0);
            Assert.AreEqual(3, timer.Remaining);
        }

        [Test]
        public void SmallSteps_AndOneLargeStep_ExpireAtSameTime()
        {
            var timer = new CountdownTimer();
            timer.Start(3);
            for (var i = 0; i < 30; i++) timer.Tick(0.1f);
            Assert.IsTrue(timer.HasExpired);
            Assert.AreEqual(0, timer.Remaining);
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidTime_IsRejected(float value)
        {
            var timer = new CountdownTimer();
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.Start(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.Tick(value));
        }
    }
}
