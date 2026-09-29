using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>GDD §3 Lift and Carrying. Kestrel loadCapacity = 4 (SimulationConfig defaults).</summary>
    public class LiftCarryTests
    {
        private static readonly SimulationConfig Config = new SimulationConfig();

        private static SimulationState Level(params string[] rows)
        {
            var s = MoveResolverTests.Level(rows);
            CarryTracker.Update(s); // what tick step 3 did at the end of the previous tick
            return s;
        }

        private static BlockState Block(SimulationState s, char c) => s.GetBlock(new BlockId(c));

        private static char[] Letters(System.Collections.Generic.IEnumerable<BlockState> blocks) =>
            blocks.Select(b => b.Id.Letter).OrderBy(c => c).ToArray();

        private static MoveResult Move(SimulationState s, Direction dir)
        {
            var r = MoveResolver.TryMove(s, ShipId.Kestrel, dir, Config);
            CarryTracker.Update(s);
            return r;
        }

        // ------------------------------------------------------------ lift ----

        [Test]
        public void Lift_UnderCapacity_RaisesTheWholeStack()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#.b......2222#",
                "#aa......2222#",
                "#KK....AAAA11#",
                "#KK....AAAA11#",
                "##############");

            var r = Move(s, Direction.Up);

            Assert.IsTrue(r.Accepted);
            CollectionAssert.AreEqual(new[] { 'a', 'b' }, Letters(r.Chain));
            Assert.AreEqual(new GridPos(1, 2), s.GetShip(ShipId.Kestrel).Position);
            Assert.AreEqual(new GridPos(1, 4), Block(s, 'a').Position);
            Assert.AreEqual(new GridPos(2, 5), Block(s, 'b').Position);
        }

        [Test]
        public void Lift_OverLoadCapacity_IsRefusedTooHeavy()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#bbb.....2222#",
                "#aa......2222#",
                "#KK....AAAA11#",
                "#KK....AAAA11#",
                "##############");

            var r = Move(s, Direction.Up);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RefuseReason.TooHeavy, r.Reason);
            Assert.AreEqual(5, r.ChainWeight);
            Assert.AreEqual(new GridPos(1, 1), s.GetShip(ShipId.Kestrel).Position, "a refused lift moves nothing");
        }

        [TestCase("#aaaa....2222#", true, TestName = "Lift_PartlyOnALedge_Weight4_Lifts")]
        [TestCase("#aaaaa...2222#", false, TestName = "Lift_PartlyOnALedge_Weight5_CountsFullWeight")]
        public void Lift_CountsFullWeight_EvenPartlyOnALedge(string blockRow, bool accepted)
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#........2222#",
                blockRow,
                "#KK##..AAAA11#",
                "#KK....AAAA11#",
                "##############");
            Assert.IsNull(Block(s, 'a').CarriedBy, "resting on a ledge and a ship: not carried");

            var r = Move(s, Direction.Up);

            Assert.AreEqual(accepted, r.Accepted);
            if (!accepted) Assert.AreEqual(RefuseReason.TooHeavy, r.Reason);
        }

        [Test]
        public void Lift_IntoACeiling_IsRefused()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#.#......2222#",
                "#.a......2222#",
                "#KK....AAAA11#",
                "#KK....AAAA11#",
                "##############");

            var r = Move(s, Direction.Up);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RefuseReason.Ceiling, r.Reason);
        }

        // ------------------------------------------------------------ carried state ----

        [Test]
        public void OnlyOnTheShip_IsCarried()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#........2222#",
                "#.a......2222#",
                "#KK....AAAA11#",
                "#KK....AAAA11#",
                "##############");

            Assert.AreEqual(ShipId.Kestrel, Block(s, 'a').CarriedBy);
        }

        [Test]
        public void OnACarriedBlock_IsCarriedToo()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#.b......2222#",
                "#aa......2222#",
                "#KK....AAAA11#",
                "#KK....AAAA11#",
                "##############");

            Assert.AreEqual(ShipId.Kestrel, Block(s, 'a').CarriedBy);
            Assert.AreEqual(ShipId.Kestrel, Block(s, 'b').CarriedBy);
        }

        [Test]
        public void OnTheShipAndALedge_IsResting()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#........2222#",
                "#aaa.....2222#",
                "#KK#...AAAA11#",
                "#KK....AAAA11#",
                "##############");

            Assert.IsNull(Block(s, 'a').CarriedBy);
        }

        [Test]
        public void OnBothShips_IsCarriedByNeither()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#........2222#",
                "#.aa.....2222#",
                "#KKAAAA....11#",
                "#KKAAAA....11#",
                "##############");

            Assert.IsNull(Block(s, 'a').CarriedBy);
        }

        [Test]
        public void InTheAir_IsNotCarried()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#....a...2222#",
                "#........2222#",
                "#KK....AAAA11#",
                "#KK....AAAA11#",
                "##############");

            Assert.IsNull(Block(s, 'a').CarriedBy);
        }

        // ------------------------------------------------------------ carrying and release ----

        [Test]
        public void Carried_TravelsSideways()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#........2222#",
                "#.a......2222#",
                "#KK....AAAA11#",
                "#KK....AAAA11#",
                "##############");

            var r = Move(s, Direction.Right);

            Assert.IsTrue(r.Accepted);
            CollectionAssert.AreEqual(new[] { 'a' }, Letters(r.Chain));
            Assert.AreEqual(new GridPos(3, 3), Block(s, 'a').Position);
            Assert.AreEqual(ShipId.Kestrel, Block(s, 'a').CarriedBy);
        }

        [Test]
        public void Sideways_WallInTheCarriedBlocksPath_ReleasesIt_AndWhatRidesOnlyOnIt()
        {
            var s = Level(
                "##############",
                "#............#",
                "#............#",
                "#.b......2222#",
                "#aa......2222#",
                "#.KK...AAAA11#",
                "#.KK...AAAA11#",
                "##############");
            Assert.AreEqual(ShipId.Kestrel, Block(s, 'b').CarriedBy);

            var r = Move(s, Direction.Left);

            Assert.IsTrue(r.Accepted, "the ship slides out from under its load");
            Assert.AreEqual(new GridPos(1, 1), s.GetShip(ShipId.Kestrel).Position);
            CollectionAssert.AreEqual(new[] { 'a', 'b' }, Letters(r.Released));
            Assert.AreEqual(new GridPos(1, 3), Block(s, 'a').Position);
            Assert.AreEqual(new GridPos(2, 4), Block(s, 'b').Position);
        }

        [Test]
        public void Down_Carries_SupportsUnderneathNeverRelease_ThenTheLedgeSetsItDown()
        {
            var s = Level(
                "##############",
                "#........2222#",
                "#aaa.....2222#",
                "#.KK.........#",
                "##KK.........#",
                "#......AAAA11#",
                "#......AAAA11#",
                "##############");
            Assert.AreEqual(ShipId.Kestrel, Block(s, 'a').CarriedBy);

            var first = Move(s, Direction.Down);
            Assert.IsTrue(first.Accepted);
            Assert.AreEqual(new GridPos(1, 4), Block(s, 'a').Position, "carried down one cell");
            Assert.AreEqual(ShipId.Kestrel, Block(s, 'a').CarriedBy,
                "now also touching the ledge — still carried: supports underneath never release");

            var second = Move(s, Direction.Down);
            Assert.IsTrue(second.Accepted);
            CollectionAssert.AreEqual(new[] { 'a' }, Letters(second.Released));
            Assert.AreEqual(new GridPos(1, 4), Block(s, 'a').Position, "set down on the ledge");
            Assert.AreEqual(new GridPos(2, 1), s.GetShip(ShipId.Kestrel).Position, "the ship descends away");
            Assert.IsNull(Block(s, 'a').CarriedBy);
        }

        /// <summary>GDD §3 "Example — using walls as leverage", shifted one cell into a walled room.</summary>
        [Test]
        public void GddExample_SlotLeverage_FreesKestrel()
        {
            var s = Level(
                "################",
                "#........11AAAA#",
                "#........11AAAA#",
                "#...aaaaa......#",
                "#...a.KK.......#",
                "#...a.KK.......#",
                "#..#.#.....2222#",
                "#..#.#.....2222#",
                "################");
            var l = Block(s, 'a');
            Assert.AreEqual(ShipId.Kestrel, l.CarriedBy, "1. carried to the slot");

            Assert.IsTrue(Move(s, Direction.Down).Accepted, "2. lower the leg into the slot");
            var inSlot = l.Position;

            for (var i = 0; i < 3; i++)
            {
                var r = Move(s, Direction.Right);
                Assert.IsTrue(r.Accepted, $"3. fly right (step {i + 1})");
                CollectionAssert.Contains(r.Released.ToArray(), l, "the leg hits the wall: released");
                Assert.AreEqual(inSlot, l.Position, "the block stays in the slot");
            }
            Assert.AreEqual(9, s.GetShip(ShipId.Kestrel).Position.X);
            Assert.IsNull(l.CarriedBy, "Kestrel is free");
        }

        // ------------------------------------------------------------ simulation ----

        [Test]
        public void Simulation_StartsCarried_AndRaisesBlockReleased()
        {
            var sim = new Simulation(() => MoveResolverTests.Level(
                "##############",
                "#............#",
                "#............#",
                "#........2222#",
                "#aa......2222#",
                "#.KK...AAAA11#",
                "#.KK...AAAA11#",
                "##############"), Config);
            Assert.AreEqual(ShipId.Kestrel, sim.State.GetBlock(new BlockId('a')).CarriedBy);

            sim.SetHeldDirection(Direction.Left);
            sim.Tick(0.01f);

            var released = sim.Events.Log.OfType<BlockReleased>().Single();
            Assert.AreEqual('a', released.Block.Letter);
            Assert.AreEqual(ShipId.Kestrel, released.From);
        }
    }
}
