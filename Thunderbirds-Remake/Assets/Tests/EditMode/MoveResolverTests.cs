using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>GDD §3 Push. Kestrel pushCapacity = 4, Atlas = 8 (SimulationConfig defaults).</summary>
    public class MoveResolverTests
    {
        private static readonly SimulationConfig Config = new SimulationConfig();

        internal static SimulationState Level(params string[] rows) =>
            LevelParser.Parse(string.Join("\n", rows), Config.AtlasPushCapacity).CreateState(Config, 3, 90f);

        private static BlockState Block(SimulationState s, char c) => s.GetBlock(new BlockId(c));

        private static char[] Letters(MoveResult r) => r.Chain.Select(b => b.Id.Letter).OrderBy(c => c).ToArray();

        // ------------------------------------------------------------ plain moves ----

        [Test]
        public void EmptyCell_Moves()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KK........11#",
                "#KK........11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsTrue(r.Accepted);
            Assert.IsEmpty(r.Chain);
            Assert.AreEqual(new GridPos(2, 1), s.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void Wall_Refuses_AndNothingMoves()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KK........11#",
                "#KK........11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Left, Config);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RefuseReason.Blocked, r.Reason);
            Assert.AreEqual(new GridPos(1, 1), s.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void OtherShip_IsAnObstacle()
        {
            var s = Level(
                "##############",
                "#..........11#",
                "#..........11#",
                "#............#",
                "#KKAAAA..2222#",
                "#KKAAAA..2222#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RefuseReason.Blocked, r.Reason);
        }

        // ------------------------------------------------------------ push capacity ----

        [Test]
        public void Push_ExactlyAtCapacity_Passes()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KKaa......11#",
                "#KKaa......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(4, r.ChainWeight);
            Assert.AreEqual(new GridPos(4, 1), Block(s, 'a').Position);
            Assert.AreEqual(new GridPos(2, 1), s.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void Push_OneOverCapacity_RefusesTooHeavy_AndNothingMoves()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KKaaa.....11#",
                "#KKaa......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RefuseReason.TooHeavy, r.Reason);
            Assert.AreEqual(5, r.ChainWeight);
            CollectionAssert.AreEqual(new[] { 'a' }, Letters(r));
            Assert.AreEqual(new GridPos(3, 1), Block(s, 'a').Position);
            Assert.AreEqual(new GridPos(1, 1), s.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void Push_Atlas_MovesWhatKestrelCannot()
        {
            var s = Level(
                "##############",
                "#KK......11..#",
                "#KK......11..#",
                "#........2222#",
                "#AAAAaaa.2222#",
                "#AAAAaa......#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Atlas, Direction.Right, Config);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(5, r.ChainWeight);
        }

        // ------------------------------------------------------------ chains ----

        [Test]
        public void Chain_BlockPushesTheNextBlock()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KKab......11#",
                "#KKab......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsTrue(r.Accepted);
            CollectionAssert.AreEqual(new[] { 'a', 'b' }, Letters(r));
            Assert.AreEqual(new GridPos(4, 1), Block(s, 'a').Position);
            Assert.AreEqual(new GridPos(5, 1), Block(s, 'b').Position);
        }

        [Test]
        public void Chain_WeightIsTheWholeChain()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KKabb.....11#",
                "#KKab......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.AreEqual(RefuseReason.TooHeavy, r.Reason);
            Assert.AreEqual(5, r.ChainWeight);
        }

        [Test]
        public void Chain_FrontAgainstWall_RefusesBlocked()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KKaa#.....11#",
                "#KKaa#.....11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RefuseReason.Blocked, r.Reason);
            CollectionAssert.AreEqual(new[] { 'a' }, Letters(r));
        }

        [Test]
        public void Chain_GapStopsIt()
        {
            // b is not touching a, so it is not displaced
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KKa.b.....11#",
                "#KKa.b.....11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsTrue(r.Accepted);
            CollectionAssert.AreEqual(new[] { 'a' }, Letters(r));
            Assert.AreEqual(new GridPos(5, 1), Block(s, 'b').Position);
        }

        // ------------------------------------------------------------ riders ----

        [Test]
        public void Rider_OnAPushedBlock_ComesAlong()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#..b.........#",
                "#KKa.......11#",
                "#KKa.......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsTrue(r.Accepted);
            CollectionAssert.AreEqual(new[] { 'a', 'b' }, Letters(r));
            Assert.AreEqual(3, r.ChainWeight);
            Assert.AreEqual(new GridPos(4, 3), Block(s, 'b').Position);
        }

        [Test]
        public void Rider_CountsTowardWeight()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#..bbb.......#",
                "#KKa.......11#",
                "#KKa.......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.AreEqual(RefuseReason.TooHeavy, r.Reason);
            Assert.AreEqual(5, r.ChainWeight, "a (2) + its rider b (3)");
        }

        [Test]
        public void Rider_OfARider_ComesAlong()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#..c.........#",
                "#..b.........#",
                "#KKa.......11#",
                "#KKa.......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            CollectionAssert.AreEqual(new[] { 'a', 'b', 'c' }, Letters(r));
        }

        [Test]
        public void Rider_HittingALedge_BlocksTheWholePush()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#..b#........#",
                "#KKa.......11#",
                "#KKa.......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(RefuseReason.Blocked, r.Reason);
            Assert.AreEqual(new GridPos(3, 1), Block(s, 'a').Position);
        }

        [Test]
        public void BlockUnderAPushedBlock_StaysPut()
        {
            // a rests on b; pushing a must not drag b (only things ON the chain ride along)
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#............#",
                "#KKaa......11#",
                "#KK.b......11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Right, Config);

            Assert.IsTrue(r.Accepted);
            CollectionAssert.AreEqual(new[] { 'a' }, Letters(r));
        }

        // ------------------------------------------------------------ vertical (until #12) ----

        [Test]
        public void Up_IntoABlock_RefusesUntilLiftExists()
        {
            var s = Level(
                "##############",
                "#AAAA....2222#",
                "#AAAA....2222#",
                "#.a..........#",
                "#KK........11#",
                "#KK........11#",
                "##############");

            var r = MoveResolver.TryMove(s, ShipId.Kestrel, Direction.Up, Config);

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(new GridPos(1, 1), s.GetShip(ShipId.Kestrel).Position);
        }
    }
}
