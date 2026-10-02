using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class LoadTrackerTests
    {
        private LoadTracker _loads;
        private EventHub _events;
        private SimulationConfig _config;

        [SetUp]
        public void SetUp()
        {
            _loads = new LoadTracker();
            _events = new EventHub();
            _config = new SimulationConfig();
        }

        private static BlockState Block(char id, int x, int y, int width) =>
            new BlockState(new BlockId(id), new GridPos(x, y),
                Enumerable.Range(0, width).Select(dx => new GridPos(dx, 0)).ToArray(), ColourClass.Light);

        private static SimulationState State(params BlockState[] blocks)
        {
            var walls = new bool[20, 12];
            for (var x = 0; x < 20; x++) walls[x, 0] = true;
            return new SimulationState(walls, new[]
            {
                new ShipState(ShipId.Kestrel, 2, 2, new GridPos(2, 1), new GridPos(2, 8)),
                new ShipState(ShipId.Atlas, 4, 2, new GridPos(8, 1), new GridPos(8, 8))
            }, blocks, 3, 90);
        }

        private void Tick(SimulationState state, float dt = 0)
        {
            CarryTracker.Update(state);
            _loads.Tick(state, _config, dt, _events);
            _events.Flush();
        }

        [Test]
        public void FullStackWeight_CountsOncePerBlock_AndStartsStress()
        {
            var state = State(Block('a', 2, 3, 2), Block('b', 2, 4, 3));
            Tick(state);
            var ship = state.GetShip(ShipId.Kestrel);
            Assert.AreEqual(5, ship.Load);
            Assert.IsTrue(ship.IsStressed);
            Assert.AreEqual(3, ship.CrushSecondsLeft);
            Assert.AreEqual(1, _events.Log.OfType<ShipStressed>().Count());
            Assert.AreEqual(5, _events.Log.OfType<ShipStressed>().Single().Load);
        }

        [Test]
        public void SharedSupportPaths_DoNotDoubleCountRiders()
        {
            var state = State(Block('a', 2, 3, 1), Block('b', 3, 3, 1), Block('c', 2, 4, 2));
            Tick(state);
            Assert.AreEqual(4, state.GetShip(ShipId.Kestrel).Load);
            Assert.IsFalse(state.GetShip(ShipId.Kestrel).IsStressed, "Capacity is inclusive.");
        }

        [Test]
        public void StaticSupportThroughAnotherBlock_RemovesLoadIncludingRiders()
        {
            var state = State(Block('a', 4, 1, 1), Block('b', 4, 2, 1),
                Block('c', 2, 3, 3), Block('d', 2, 4, 4));
            // Even a previously carried shape now supported by a static stack contributes no load.
            state.GetBlock(new BlockId('c')).CarriedBy = ShipId.Kestrel;
            Tick(state);
            Assert.AreEqual(0, state.GetShip(ShipId.Kestrel).Load);
            Assert.IsFalse(state.GetShip(ShipId.Kestrel).IsStressed);
        }

        [Test]
        public void BothShipsSupportBridgeAndRider_EachGetsFullWeight()
        {
            var state = State(Block('a', 2, 3, 7), Block('b', 5, 4, 2));
            Tick(state);
            Assert.IsNull(state.GetBlock(new BlockId('a')).CarriedBy);
            foreach (var ship in state.Ships)
            {
                Assert.AreEqual(9, ship.Load);
                Assert.IsTrue(ship.IsStressed);
            }
            Assert.AreEqual(2, _events.Log.OfType<ShipStressed>().Count());
        }

        [Test]
        public void DirectStaticSupport_TakesBridgeLoadOffBothShips()
        {
            var state = State(Block('a', 2, 3, 7));
            var walls = new bool[20, 12];
            walls[5, 2] = true;
            state = new SimulationState(walls, state.Ships, state.Blocks, 3, 90);
            Tick(state);
            Assert.IsTrue(state.Ships.All(s => s.Load == 0));
        }

        [Test]
        public void OverloadPersists_ExpiresWithoutRestartingOrRepeatingStress()
        {
            var state = State(Block('a', 2, 3, 5));
            Tick(state, 10); // First detection grants the entire grace period.
            var ship = state.GetShip(ShipId.Kestrel);
            Assert.AreEqual(3, ship.CrushSecondsLeft);
            Tick(state, 1);
            Assert.AreEqual(2, ship.CrushSecondsLeft);
            Tick(state, 2);
            Assert.IsTrue(ship.IsCrushDue);
            Tick(state, 100);
            Assert.AreEqual(0, ship.CrushSecondsLeft);
            Assert.AreEqual(1, _events.Log.OfType<ShipStressed>().Count());
            Assert.AreEqual(3, state.LivesLeft, "Lives/respawn is stage 5, issue #14.");
        }

        [Test]
        public void RescueBeforeExpiry_ClearsTimer_AndNextOverloadGetsFullGrace()
        {
            var block = Block('a', 2, 3, 5);
            var state = State(block);
            Tick(state);
            Tick(state, 2.9f);
            block.Position = new GridPos(14, 1);
            Tick(state, 1);
            var ship = state.GetShip(ShipId.Kestrel);
            Assert.IsFalse(ship.IsStressed);
            Assert.IsFalse(ship.IsCrushDue);
            Assert.AreEqual(0, ship.CrushSecondsLeft);
            Tick(state, 5);
            Assert.AreEqual(1, _events.Log.OfType<ShipRelieved>().Count());
            block.Position = new GridPos(2, 3);
            Tick(state);
            Assert.AreEqual(3, ship.CrushSecondsLeft);
            Assert.AreEqual(2, _events.Log.OfType<ShipStressed>().Count());
        }

        [Test]
        public void LoadChangesAboveCapacity_DoNotResetCountdown()
        {
            var rider = Block('b', 2, 4, 2);
            var state = State(Block('a', 2, 3, 5), rider);
            Tick(state);
            Tick(state, 1);
            rider.Position = new GridPos(14, 1);
            Tick(state, 1);
            var ship = state.GetShip(ShipId.Kestrel);
            Assert.AreEqual(5, ship.Load);
            Assert.AreEqual(1, ship.CrushSecondsLeft);
            Assert.AreEqual(1, _events.Log.OfType<ShipStressed>().Count());
        }

        [Test]
        public void Ghosts_ContributeNoSupport_AndHaveNoCountdown()
        {
            var state = State(Block('a', 2, 3, 5));
            Tick(state);
            var ship = state.GetShip(ShipId.Kestrel);
            ship.IsGhost = true;
            Tick(state, 5);
            Assert.AreEqual(0, ship.Load);
            Assert.IsFalse(ship.IsStressed);
            Assert.IsFalse(ship.IsCrushDue);
        }

        [Test]
        public void ShipsHaveIndependentCountdowns()
        {
            var b = Block('b', 8, 5, 9);
            var state = State(Block('a', 2, 3, 5), b);
            Tick(state);
            Tick(state, 1);
            b.Position = new GridPos(8, 3);
            Tick(state, 1);
            Assert.AreEqual(1, state.GetShip(ShipId.Kestrel).CrushSecondsLeft);
            Assert.AreEqual(3, state.GetShip(ShipId.Atlas).CrushSecondsLeft);
        }

        [Test]
        public void Simulation_FallCausesStress_EventsSeeFinalState_RestartClearsTimers()
        {
            SimulationState state = null;
            var sim = new Simulation(() => state = State(Block('a', 2, 4, 5)), () => _config);
            var notified = 0;
            sim.Events.ShipStressed += e =>
            {
                notified++;
                Assert.AreEqual(5, sim.State.GetShip(e.Ship).Load);
                Assert.AreEqual(3, state.GetBlock(new BlockId('a')).Position.Y);
            };
            sim.Tick(0.1f);
            Assert.AreEqual(1, notified);
            Assert.AreEqual(3, sim.State.GetShip(ShipId.Kestrel).CrushSecondsLeft);
            sim.Tick(1);
            Assert.AreEqual(2, sim.State.GetShip(ShipId.Kestrel).CrushSecondsLeft);
            _config.CrushGraceSeconds = 4;
            sim.Restart();
            Assert.IsFalse(sim.State.GetShip(ShipId.Kestrel).IsStressed);
            Assert.AreEqual(0, sim.State.GetShip(ShipId.Kestrel).CrushSecondsLeft);
            sim.Tick(0.1f);
            Assert.AreEqual(4, sim.State.GetShip(ShipId.Kestrel).CrushSecondsLeft);
        }
    }
}
