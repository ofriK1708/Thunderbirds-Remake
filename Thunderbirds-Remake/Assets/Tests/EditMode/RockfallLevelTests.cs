using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// L4 - Rockfall (issue #23): Atlas pushes a 5-cell slab off a ledge above Kestrel's start. Played through
    /// the real rules three ways: the safe order, getting hit and escaping in time, and getting crushed.
    /// </summary>
    public class RockfallLevelTests
    {
        private static readonly BlockId Slab = new BlockId('h');

        private Simulation _sim;
        private LevelData _level;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _level = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/L4_Rockfall.asset");
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Config/GameConfig.asset");
            Assert.IsNotNull(_level);
            Assert.IsFalse(string.IsNullOrWhiteSpace(_level.hintText));
            var definition = LevelParser.Parse(_level.grid, _config.atlas.pushCapacity);
            _sim = new Simulation(() => definition.CreateState(_config.ToSimulationConfig(), _config.livesPerLevel,
                _config.OxygenFor(_level)), _config.ToSimulationConfig);
            _sim.Tick(12f); // time to read the hint: nothing may happen to a player who is only reading
        }

        private void Move(Direction direction, int cells)
        {
            for (var i = 0; i < cells; i++)
            {
                var ship = _sim.State.GetShip(_sim.State.ActiveShip);
                var start = ship.Position;
                _sim.SetHeldDirection(direction);
                for (var frame = 0; frame < 100 && ship.Position == start; frame++) _sim.Tick(0.01f);
                _sim.ClearInput();
                Assert.AreEqual(start + direction.ToOffset(), ship.Position, $"{ship.Id} {direction}, step {i + 1}");
            }
        }

        /// <summary>Let time pass in frame-sized steps, as the game does (one huge step would skip the countdown).</summary>
        private void Wait(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += 0.02f) _sim.Tick(0.02f);
        }

        private void AssertBothDocked()
        {
            Assert.AreEqual(SimStatus.Complete, _sim.State.Status);
            foreach (var ship in _sim.State.Ships) Assert.AreEqual(ship.Dock, ship.Position);
            Assert.Greater(_sim.State.OxygenRemaining, 30f);
        }

        [Test]
        public void ReadingTheHint_IsSafe_AndTheSlabIsTooHeavyForKestrel()
        {
            Assert.AreEqual(SimStatus.Playing, _sim.State.Status);
            Assert.IsEmpty(_sim.Events.Log.OfType<ShipStressed>());
            var slab = _sim.State.GetBlock(Slab);
            Assert.AreEqual(5, slab.Weight);
            Assert.Greater(slab.Weight, _config.kestrel.loadCapacity, "the slab must overload Kestrel");
            Assert.LessOrEqual(slab.Weight, _config.atlas.pushCapacity, "Atlas must be able to push it");
        }

        [Test]
        public void SafeOrder_KestrelFirst_ThenAtlasDropsTheSlab()
        {
            Move(Direction.Right, 14);          // Kestrel into the tunnel, onto its dock
            _sim.SwitchShip();
            Move(Direction.Left, 14);           // Atlas pushes the slab off the ledge, then flies on to its dock

            AssertBothDocked();
            Assert.AreEqual(1, _sim.State.GetBlock(Slab).Position.Y, "the slab ends on the pit floor");
            Assert.IsTrue(_sim.Events.Log.OfType<BlockFell>().Any(e => e.Block == Slab));
            Assert.IsEmpty(_sim.Events.Log.OfType<ShipStressed>());
            Assert.AreEqual(_config.livesPerLevel, _sim.State.LivesLeft);
        }

        [Test]
        public void WrongOrder_SlabLandsOnKestrel_ButSlidingOutInTimeSavesIt()
        {
            _sim.SwitchShip();
            Move(Direction.Left, 10);           // Atlas drops the slab while Kestrel is still underneath
            Wait(0.5f);

            var kestrel = _sim.State.GetShip(ShipId.Kestrel);
            Assert.IsTrue(kestrel.IsStressed, "the slab landed on Kestrel");
            Assert.AreEqual(5, kestrel.Load);

            _sim.SwitchShip();
            Move(Direction.Right, 14);          // the ledge scrapes the slab off as Kestrel slides under it

            Assert.IsEmpty(_sim.Events.Log.OfType<ShipCrushed>());
            Assert.IsTrue(_sim.Events.Log.OfType<ShipRelieved>().Any(e => e.Ship == ShipId.Kestrel));
            Assert.AreEqual(_config.livesPerLevel, _sim.State.LivesLeft);
            Assert.AreEqual(SimStatus.Playing, _sim.State.Status, "Atlas still has to reach its dock");
            _sim.SwitchShip();
            Move(Direction.Left, 4);
            AssertBothDocked();
        }

        [Test]
        public void WrongOrder_AndNoReaction_CostsExactlyOneLife_AndTheSlabFallsThroughTheGhost()
        {
            _sim.SwitchShip();
            Move(Direction.Left, 14);           // Atlas drops the slab and flies on to its dock
            Wait(_config.crushGraceSeconds + 0.5f); // nobody rescues Kestrel

            var kestrel = _sim.State.GetShip(ShipId.Kestrel);
            Assert.AreEqual(1, _sim.Events.Log.OfType<ShipCrushed>().Count());
            Assert.IsTrue(kestrel.IsGhost, "respawn protection: back at the start, but as a ghost");
            Assert.AreEqual(kestrel.Start, kestrel.Position);

            Wait(_config.respawnGhostSeconds + _config.crushGraceSeconds + 1f); // long enough to be crushed again

            Assert.AreEqual(1, _sim.Events.Log.OfType<ShipCrushed>().Count(), "the slab must not crush Kestrel a second time");
            Assert.AreEqual(_config.livesPerLevel - 1, _sim.State.LivesLeft);
            Assert.AreEqual(1, _sim.State.GetBlock(Slab).Position.Y, "the slab fell through the ghost to the pit floor");
            Assert.IsFalse(kestrel.IsGhost, "the start cell is clear again, so Kestrel is solid");
            Assert.IsFalse(kestrel.IsStressed);

            _sim.SwitchShip();
            Assert.AreEqual(ShipId.Kestrel, _sim.State.ActiveShip);
            Move(Direction.Right, 14);
            AssertBothDocked();
        }
    }
}
