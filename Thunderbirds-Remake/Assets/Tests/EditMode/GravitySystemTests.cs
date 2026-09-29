using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class GravitySystemTests
    {
        private static BlockState Block(char id, int x, int y, params GridPos[] shape) =>
            new BlockState(new BlockId(id), new GridPos(x, y),
                shape.Length == 0 ? new[] { new GridPos(0, 0) } : shape, ColourClass.Teal);

        private static SimulationState Room(BlockState[] blocks, bool[,] walls = null, params ShipState[] ships) =>
            new SimulationState(walls ?? new bool[12, 20], ships, blocks, 3, 90);

        private static Simulation Sim(SimulationState state) => new Simulation(() => state, new SimulationConfig());

        [Test]
        public void Fall_WaitsForInterval_AndPreservesRemainder()
        {
            var block = Block('a', 3, 8);
            var sim = Sim(Room(new[] { block }));
            sim.Tick(0.06f);
            Assert.AreEqual(8, block.Position.Y);
            sim.Tick(0.06f);
            Assert.AreEqual(7, block.Position.Y);
            sim.Tick(0.08f);
            Assert.AreEqual(6, block.Position.Y);
            var falls = sim.Events.Log.OfType<BlockFell>().ToArray();
            Assert.AreEqual(2, falls.Length);
            Assert.AreEqual(new GridPos(3, 8), falls[0].From);
            Assert.AreEqual(new GridPos(3, 7), falls[0].To);
            Assert.AreEqual(0.1f, falls[0].StepSeconds);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Stack_FallsTogether_RegardlessOfCollectionOrder(bool reverse)
        {
            var bottom = Block('c', 3, 5);
            var middle = Block('b', 3, 6);
            var top = Block('a', 3, 7);
            var blocks = new[] { bottom, middle, top };
            var sim = Sim(Room(reverse ? blocks.Reverse().ToArray() : blocks));
            sim.Tick(0.1f);
            Assert.AreEqual(4, bottom.Position.Y);
            Assert.AreEqual(5, middle.Position.Y);
            Assert.AreEqual(6, top.Position.Y);
            CollectionAssert.AreEqual(new[] { 'a', 'b', 'c' },
                sim.Events.Log.OfType<BlockFell>().Select(e => e.Block.Letter));
        }

        [Test]
        public void Bridge_OnTwoFallingRoots_FollowsBoth()
        {
            var left = Block('a', 2, 5);
            var right = Block('b', 4, 5);
            var bridge = Block('c', 2, 6, new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0));
            Sim(Room(new[] { bridge, right, left })).Tick(0.1f);
            Assert.AreEqual(5, bridge.Position.Y);
            Assert.AreEqual(4, left.Position.Y);
            Assert.AreEqual(4, right.Position.Y);
        }

        [Test]
        public void RiderAlsoOnLedge_StaysWhileItsUnsupportedBlockFalls()
        {
            var lower = Block('a', 2, 4);
            var bridge = Block('b', 2, 5, new GridPos(0, 0), new GridPos(1, 0));
            var walls = new bool[12, 20];
            walls[3, 4] = true;
            Sim(Room(new[] { lower, bridge }, walls)).Tick(0.1f);
            Assert.AreEqual(3, lower.Position.Y);
            Assert.AreEqual(5, bridge.Position.Y);
        }

        [Test]
        public void LShape_LandsOnHigherArm_WithoutPassingThroughLedge()
        {
            var block = Block('a', 2, 4, new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1));
            var walls = new bool[12, 20];
            walls[3, 3] = true;
            var sim = Sim(Room(new[] { block }, walls));
            sim.Tick(1f);
            Assert.AreEqual(new GridPos(2, 3), block.Position);
            Assert.AreEqual(1, sim.Events.Log.OfType<BlockFell>().Count());
            Assert.AreEqual(1, sim.Events.Log.OfType<BlockLanded>().Count());
        }

        [Test]
        public void Landing_ReportsOncePerBlock_AfterAllStackFalls()
        {
            var bottom = Block('b', 3, 1);
            var top = Block('a', 3, 2);
            var sim = Sim(Room(new[] { top, bottom }));
            sim.Tick(0.1f);
            Assert.AreEqual(0, bottom.Position.Y);
            Assert.AreEqual(1, top.Position.Y);
            Assert.IsInstanceOf<BlockFell>(sim.Events.Log[0]);
            Assert.IsInstanceOf<BlockFell>(sim.Events.Log[1]);
            Assert.AreEqual(2, sim.Events.Log.OfType<BlockLanded>().Count());
            sim.Tick(10f);
            Assert.AreEqual(2, sim.Events.Log.OfType<BlockFell>().Count(), "A resting stack does not keep falling.");
            Assert.AreEqual(2, sim.Events.Log.OfType<BlockLanded>().Count(), "A resting stack does not repeatedly land.");
        }

        [Test]
        public void FallsOntoStationaryBlock_AndStops()
        {
            var lower = Block('a', 3, 0);
            var upper = Block('b', 3, 4);
            var sim = Sim(Room(new[] { upper, lower }));
            sim.Tick(1f);
            Assert.AreEqual(0, lower.Position.Y);
            Assert.AreEqual(1, upper.Position.Y);
            Assert.AreEqual(3, sim.Events.Log.OfType<BlockFell>().Count());
            Assert.AreEqual(upper.Id, sim.Events.Log.OfType<BlockLanded>().Single().Block);
        }

        [TestCase(false, 3)]
        [TestCase(true, 0)]
        public void ShipsSupportBlocks_UnlessGhosts_AndNeverFall(bool ghost, int expectedY)
        {
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(3, 1), default) { IsGhost = ghost };
            var block = Block('a', 3, 5);
            var sim = Sim(Room(new[] { block }, null, ship));
            sim.Tick(1f);
            Assert.AreEqual(expectedY, block.Position.Y);
            Assert.AreEqual(new GridPos(3, 1), ship.Position);
        }

        [Test]
        public void LongFrame_MatchesSplitFrames_WithoutTunnelling()
        {
            var a = Block('a', 3, 8);
            var b = Block('a', 3, 8);
            var whole = Sim(Room(new[] { a }));
            var split = Sim(Room(new[] { b }));
            whole.Tick(0.35f);
            for (var i = 0; i < 7; i++) split.Tick(0.05f);
            Assert.AreEqual(new GridPos(3, 5), a.Position);
            Assert.AreEqual(a.Position, b.Position);
            CollectionAssert.AreEqual(whole.Events.Log.OfType<BlockFell>().Select(e => e.To),
                split.Events.Log.OfType<BlockFell>().Select(e => e.To));
        }

        [Test]
        public void RootsHaveIndependentClocks_AndSupportedTimeIsNotBanked()
        {
            var free = Block('a', 2, 8);
            var held = Block('b', 6, 3);
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(6, 1), default);
            var sim = Sim(Room(new[] { free, held }, null, ship));
            sim.Tick(0.06f);
            ship.Position = new GridPos(9, 1);
            sim.Tick(0.04f);
            Assert.AreEqual(7, free.Position.Y);
            Assert.AreEqual(3, held.Position.Y);
            sim.Tick(0.06f);
            Assert.AreEqual(2, held.Position.Y);
        }

        [Test]
        public void GainingSupport_ClearsPartialTimer()
        {
            var block = Block('a', 3, 3);
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(8, 1), default);
            var sim = Sim(Room(new[] { block }, null, ship));
            sim.Tick(0.08f);
            ship.Position = new GridPos(3, 1);
            sim.Tick(0.5f);
            ship.Position = new GridPos(8, 1);
            sim.Tick(0.02f);
            Assert.AreEqual(3, block.Position.Y);
            sim.Tick(0.08f);
            Assert.AreEqual(2, block.Position.Y);
        }

        [Test]
        public void RootLandingOnAnotherFallingRoot_JoinsItsNextStep()
        {
            var lower = Block('a', 3, 5);
            var upper = Block('b', 3, 7);
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(3, 3), default);
            var sim = Sim(Room(new[] { upper, lower }, null, ship));
            sim.Tick(0.06f); // only upper's clock runs
            ship.Position = new GridPos(8, 3);
            sim.Tick(0.04f); // upper reaches lower, which is still waiting for its own step
            Assert.AreEqual(6, upper.Position.Y);
            Assert.IsEmpty(sim.Events.Log.OfType<BlockLanded>());
            sim.Tick(0.06f);
            Assert.AreEqual(4, lower.Position.Y);
            Assert.AreEqual(5, upper.Position.Y);
        }

        [Test]
        public void FallWinsTie_EvenWhenShipCouldPushBlock_ReservationExpiresNextTick()
        {
            var block = Block('a', 3, 3);
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(1, 1), default);
            var sim = Sim(Room(new[] { block }, null, ship));
            sim.SetHeldDirection(Direction.Right);
            sim.Tick(0.1f);
            Assert.AreEqual(new GridPos(3, 2), block.Position);
            Assert.AreEqual(new GridPos(1, 1), ship.Position);
            Assert.IsInstanceOf<BlockFell>(sim.Events.Log[0]);
            Assert.AreEqual(RefuseReason.FallWonTie, sim.Events.Log.OfType<MoveRefused>().Single().Reason);
            sim.Tick(0.01f);
            Assert.AreEqual(new GridPos(2, 1), ship.Position);
            Assert.AreEqual(new GridPos(4, 2), block.Position);
        }

        [Test]
        public void GravityPrecedesShipMovement_AndEventsSeeCompletedTick()
        {
            var block = Block('a', 8, 5);
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(1, 1), default);
            var sim = Sim(Room(new[] { block }, null, ship));
            var notified = false;
            sim.Events.BlockFell += e =>
            {
                notified = true;
                Assert.AreEqual(new GridPos(2, 1), ship.Position);
            };
            sim.SetHeldDirection(Direction.Right);
            sim.Tick(0.1f);
            Assert.IsTrue(notified);
            Assert.IsInstanceOf<BlockFell>(sim.Events.Log[0]);
            Assert.IsInstanceOf<ShipMoved>(sim.Events.Log[1]);
        }

        [Test]
        public void Restart_ClearsFallClocksAndLandingHistory_AndReadsNewInterval()
        {
            var step = 0.1f;
            var sim = new Simulation(() => Room(new[] { Block('a', 3, 1) }),
                () => new SimulationConfig { FallStepSeconds = step });
            sim.Tick(0.1f);
            Assert.AreEqual(1, sim.Events.Log.OfType<BlockLanded>().Count());
            sim.Restart();
            sim.Tick(0.08f); // A partial old clock must not survive the next restart.
            step = 0.2f;
            sim.Restart();
            sim.Tick(0.12f);
            Assert.IsEmpty(sim.Events.Log);
            sim.Tick(0.08f);
            Assert.AreEqual(1, sim.Events.Log.OfType<BlockLanded>().Count());
            Assert.AreEqual(0.2f, sim.Events.Log.OfType<BlockFell>().Single().StepSeconds);
        }

        [TestCase(SimStatus.Complete)]
        [TestCase(SimStatus.Failed)]
        public void FinishedSimulation_DoesNotAdvanceGravity(SimStatus status)
        {
            var block = Block('a', 3, 5);
            var state = Room(new[] { block });
            var sim = Sim(state);
            state.Status = status;
            sim.Tick(1f);
            Assert.AreEqual(5, block.Position.Y);
            state.Status = SimStatus.Playing;
            sim.Tick(0.1f);
            Assert.AreEqual(4, block.Position.Y);
        }
    }
}
