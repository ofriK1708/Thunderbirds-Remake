using System.Collections.Generic;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class FakeSimulationTests
    {
        private static readonly GridPos KestrelStart = new GridPos(1, 1);

        private static ShipMoved KestrelRight(int fromX) =>
            new ShipMoved(ShipId.Kestrel, new GridPos(fromX, 1), new GridPos(fromX + 1, 1), 0.08f);

        private static FakeSimulation Fake(FakeScript script) => new FakeSimulation(TestStates.SmallRoom, script);

        [Test]
        public void Tick_BeforeStepTime_RaisesNothing()
        {
            var sim = Fake(new FakeScript().At(0.5f, KestrelRight(1)));

            sim.Tick(0.4f);

            Assert.IsEmpty(sim.Events.Log);
        }

        [Test]
        public void Tick_ReachingStepTime_RaisesTheEvent()
        {
            var move = KestrelRight(1);
            var sim = Fake(new FakeScript().At(0.5f, move));

            sim.Tick(0.25f);
            sim.Tick(0.25f);

            CollectionAssert.AreEqual(new[] { move }, sim.Events.Log);
        }

        [Test]
        public void Tick_AtTimeZero_RaisesOnFirstTick()
        {
            var move = KestrelRight(1);
            var sim = Fake(new FakeScript().At(0f, move));

            sim.Tick(0.016f);

            CollectionAssert.AreEqual(new[] { move }, sim.Events.Log);
        }

        [Test]
        public void Tick_LongFrame_RaisesEveryDueStepInOrder_OnlyOnce()
        {
            var a = KestrelRight(1);
            var b = KestrelRight(2);
            var c = KestrelRight(3);
            var sim = Fake(new FakeScript().At(0.2f, a).At(0.1f, b).At(5f, c));

            sim.Tick(1f);
            sim.Tick(1f);

            CollectionAssert.AreEqual(new SimEvent[] { b, a }, sim.Events.Log);
        }

        [Test]
        public void Tick_NotifiesListeners_AndUpdatesState()
        {
            var sim = Fake(new FakeScript().At(0.1f, KestrelRight(1)));
            var heard = new List<ShipMoved>();
            sim.Events.ShipMoved += heard.Add;

            sim.Tick(0.1f);

            Assert.AreEqual(1, heard.Count);
            Assert.AreEqual(new GridPos(2, 1), sim.State.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void Tick_AfterScriptEnds_DoesNothing()
        {
            var sim = Fake(new FakeScript().At(0.1f, KestrelRight(1)));

            sim.Tick(1f);
            Assert.DoesNotThrow(() => sim.Tick(1f));

            Assert.AreEqual(1, sim.Events.Log.Count);
        }

        [Test]
        public void Restart_ReplaysFromTheStart_WithFreshState()
        {
            var sim = Fake(new FakeScript().At(0.1f, KestrelRight(1)));
            sim.Tick(1f);

            sim.Restart();

            Assert.IsEmpty(sim.Events.Log);
            Assert.AreEqual(KestrelStart, sim.State.GetShip(ShipId.Kestrel).Position);

            sim.Tick(1f);
            Assert.AreEqual(1, sim.Events.Log.Count);
        }

        [Test]
        public void SwitchShip_TogglesActiveShip_AndRaisesManualChange()
        {
            var sim = Fake(new FakeScript());
            var heard = new List<ActiveShipChanged>();
            sim.Events.ActiveShipChanged += heard.Add;

            sim.SwitchShip();

            Assert.AreEqual(ShipId.Atlas, sim.State.ActiveShip);
            Assert.AreEqual(1, heard.Count);
            Assert.IsFalse(heard[0].Automatic);
        }

        [Test]
        public void SetHeldDirection_IsRecorded()
        {
            var sim = Fake(new FakeScript());

            sim.SetHeldDirection(Direction.Left);
            Assert.AreEqual(Direction.Left, sim.HeldDirection);

            sim.SetHeldDirection(null);
            Assert.IsNull(sim.HeldDirection);
        }

        [Test]
        public void FakeScript_SortsByTime_KeepingOrderForEqualTimes()
        {
            var a = KestrelRight(1);
            var b = KestrelRight(2);
            var c = KestrelRight(3);

            var steps = new FakeScript().At(1f, a).At(0.5f, b).At(1f, c).Steps;

            Assert.AreSame(b, steps[0].Event);
            Assert.AreSame(a, steps[1].Event);
            Assert.AreSame(c, steps[2].Event);
        }
    }
}
