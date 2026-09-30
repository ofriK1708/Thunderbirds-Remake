using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Tick stage 5 (GDD §3 Load, crush & lives), issue #14.</summary>
    public class LivesAndRespawnTests
    {
        private static readonly GridPos KestrelStart = new GridPos(2, 1);
        private static readonly GridPos KestrelAway = new GridPos(6, 1);
        private static readonly BlockId Load = new BlockId('a');
        private static readonly BlockId Blocker = new BlockId('b');

        private SimulationState _state;

        /// <summary>
        /// Kestrel (capacity 4) sits away from its start carrying a 5-cell bar, so it is stressed from the
        /// first tick. Atlas waits far to the right. Crush grace is 1 s.
        /// </summary>
        private Simulation NewSim(int lives = 3, bool blockStart = false)
        {
            return new Simulation(() =>
            {
                var walls = new bool[20, 12];
                for (var x = 0; x < 20; x++) walls[x, 0] = true;
                var kestrel = new ShipState(ShipId.Kestrel, 2, 2, KestrelStart, new GridPos(2, 8)) { Position = KestrelAway };
                var atlas = new ShipState(ShipId.Atlas, 4, 2, new GridPos(14, 1), new GridPos(14, 8));
                var blocks = new[]
                {
                    new BlockState(Load, new GridPos(6, 3),
                        Enumerable.Range(0, 5).Select(dx => new GridPos(dx, 0)).ToArray(), ColourClass.Yellow)
                }.ToList();
                if (blockStart)
                    blocks.Add(new BlockState(Blocker, KestrelStart, new[] { new GridPos(0, 0) }, ColourClass.Teal));
                return _state = new SimulationState(walls, new[] { kestrel, atlas }, blocks, lives, 90);
            }, new SimulationConfig { CrushGraceSeconds = 1f });
        }

        private static void RunUntilCrush(Simulation sim)
        {
            sim.Tick(0f); // stressed, countdown starts
            sim.Tick(1f); // countdown ends, crush resolves in the same tick
        }

        [Test]
        public void Crush_CostsALife_ReleasesItsLoad_AndRespawnsAtStart()
        {
            var sim = NewSim();
            RunUntilCrush(sim);

            var kestrel = _state.GetShip(ShipId.Kestrel);
            Assert.AreEqual(2, _state.LivesLeft);
            Assert.AreEqual(KestrelStart, kestrel.Position);
            Assert.IsFalse(kestrel.IsGhost);
            Assert.IsFalse(kestrel.IsStressed);
            Assert.AreEqual(ShipId.Kestrel, _state.ActiveShip, "respawned in place: no switch needed");

            var log = sim.Events.Log;
            Assert.AreEqual(2, log.OfType<ShipCrushed>().Single(e => e.Ship == ShipId.Kestrel).LivesLeft);
            Assert.AreEqual(Load, log.OfType<BlockReleased>().Single(e => e.From == ShipId.Kestrel).Block);
            var respawn = log.OfType<ShipRespawned>().Single();
            Assert.AreEqual(KestrelStart, respawn.At);
            Assert.IsFalse(respawn.IsGhost);
            Assert.AreEqual(SimStatus.Playing, _state.Status);
        }

        [Test]
        public void Crush_LeavesTheRestOfTheLevelUntouched()
        {
            var sim = NewSim();
            RunUntilCrush(sim);

            var load = _state.GetBlock(Load);
            Assert.IsNull(load.CarriedBy);
            Assert.AreEqual(new GridPos(6, 3), load.Position, "released where it was; gravity takes it next tick");
            Assert.AreEqual(new GridPos(14, 1), _state.GetShip(ShipId.Atlas).Position);

            sim.Tick(0.1f);
            Assert.AreEqual(new GridPos(6, 2), load.Position, "the ship no longer holds it up");
        }

        [Test]
        public void OccupiedStart_WaitsAsGhost_AndControlSwitchesToTheOtherShip()
        {
            var sim = NewSim(blockStart: true);
            RunUntilCrush(sim);

            var kestrel = _state.GetShip(ShipId.Kestrel);
            Assert.IsTrue(kestrel.IsGhost);
            Assert.AreEqual(KestrelStart, kestrel.Position);
            Assert.AreEqual(ShipId.Atlas, _state.ActiveShip);

            var log = sim.Events.Log;
            Assert.IsTrue(log.OfType<ShipRespawned>().Single().IsGhost);
            var changed = log.OfType<ActiveShipChanged>().Single();
            Assert.AreEqual(ShipId.Atlas, changed.Active);
            Assert.IsTrue(changed.Automatic);

            sim.SwitchShip();
            Assert.AreEqual(ShipId.Atlas, _state.ActiveShip, "a ghost is not selectable");
        }

        [Test]
        public void Ghost_MaterialisesOnceItsStartAreaIsClear()
        {
            var sim = NewSim(blockStart: true);
            RunUntilCrush(sim);

            sim.Tick(0f);
            Assert.IsTrue(_state.GetShip(ShipId.Kestrel).IsGhost, "still blocked");
            Assert.AreEqual(1, sim.Events.Log.OfType<ShipRespawned>().Count(), "the ghost is announced once");

            _state.GetBlock(Blocker).Position = new GridPos(10, 1); // pushed out of the way
            sim.Tick(0f);

            var kestrel = _state.GetShip(ShipId.Kestrel);
            Assert.IsFalse(kestrel.IsGhost);
            Assert.AreEqual(KestrelStart, kestrel.Position);
            var respawns = sim.Events.Log.OfType<ShipRespawned>().ToList();
            Assert.AreEqual(2, respawns.Count);
            Assert.IsFalse(respawns[1].IsGhost);
            Assert.AreEqual(ShipId.Atlas, _state.ActiveShip, "materialising does not steal control back");
        }

        [Test]
        public void Ghost_CannotBeMoved()
        {
            var sim = NewSim(blockStart: true);
            RunUntilCrush(sim);
            _state.ActiveShip = ShipId.Kestrel; // force the ghost active, as if both ships were ghosts

            sim.SetHeldDirection(Direction.Right);
            sim.Tick(1f);

            Assert.AreEqual(KestrelStart, _state.GetShip(ShipId.Kestrel).Position);
            Assert.IsEmpty(sim.Events.Log.OfType<ShipMoved>());
        }

        [Test]
        public void LosingTheLastLife_FailsTheLevel_WithoutRespawning()
        {
            var sim = NewSim(lives: 1);
            RunUntilCrush(sim);

            Assert.AreEqual(0, _state.LivesLeft);
            Assert.AreEqual(SimStatus.Failed, _state.Status);
            var log = sim.Events.Log;
            Assert.AreEqual(0, log.OfType<ShipCrushed>().Single().LivesLeft);
            Assert.AreEqual(FailReason.Crushed, log.OfType<LevelFailed>().Single().Reason);
            Assert.IsEmpty(log.OfType<ShipRespawned>());
        }

        [Test]
        public void Restart_RefillsLives()
        {
            var sim = NewSim();
            RunUntilCrush(sim);
            sim.Restart();

            Assert.AreEqual(3, _state.LivesLeft);
            Assert.AreEqual(KestrelAway, _state.GetShip(ShipId.Kestrel).Position);
            Assert.IsFalse(_state.GetShip(ShipId.Kestrel).IsGhost);
        }
    }
}
