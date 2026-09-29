using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    public class OxygenWinTests
    {
        private SimulationState _state;
        private Simulation NewSim(float seconds = 3, int lives = 3) => new Simulation(() =>
            _state = new SimulationState(new bool[20, 12], new[]
            {
                new ShipState(ShipId.Kestrel, 2, 2, new GridPos(1, 1), new GridPos(4, 1)),
                new ShipState(ShipId.Atlas, 4, 2, new GridPos(10, 1), new GridPos(10, 5))
            }, new BlockState[0], lives, seconds), new SimulationConfig());

        private void DockBoth()
        {
            foreach (var ship in _state.Ships) ship.Position = ship.Dock;
        }

        [Test]
        public void PartialStateWithoutBothShips_CannotWin()
        {
            var state = new SimulationState(new bool[10, 10], new ShipState[0], new BlockState[0], 3, 90);
            Assert.IsFalse(WinChecker.IsComplete(state));
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(1, 1), new GridPos(1, 1));
            state = new SimulationState(new bool[10, 10], new[] { ship }, new BlockState[0], 3, 90);
            Assert.IsFalse(WinChecker.IsComplete(state));
        }

        [Test]
        public void Oxygen_IsContinuous_ButNotifiesOnlyOnDisplayedSecondChanges()
        {
            var sim = NewSim();
            sim.Tick(0.25f);
            Assert.AreEqual(2.75f, sim.State.OxygenRemaining);
            Assert.IsEmpty(sim.Events.Log);
            sim.Tick(0.75f);
            var change = sim.Events.Log.OfType<OxygenChanged>().Single();
            Assert.AreEqual(2, change.Remaining);
            Assert.AreEqual(3, change.Total);
        }

        [Test]
        public void Timeout_FailsOnce_ClampsAndFreezesUntilRestart()
        {
            var sim = NewSim();
            sim.Tick(20);
            Assert.AreEqual(SimStatus.Failed, sim.State.Status);
            Assert.AreEqual(0, sim.State.OxygenRemaining);
            Assert.AreEqual(FailReason.OutOfOxygen, sim.Events.Log.OfType<LevelFailed>().Single().Reason);
            var count = sim.Events.Log.Count;
            var position = _state.GetShip(ShipId.Kestrel).Position;
            sim.SetHeldDirection(Direction.Right);
            sim.SwitchShip();
            sim.Tick(10);
            Assert.AreEqual(position, _state.GetShip(ShipId.Kestrel).Position);
            Assert.AreEqual(count, sim.Events.Log.Count);
            _state.LivesLeft = 1;
            sim.Restart();
            Assert.AreEqual(3, sim.State.LivesLeft);
            Assert.AreEqual(3, sim.State.OxygenRemaining);
            Assert.AreEqual(SimStatus.Playing, sim.State.Status);
            Assert.IsEmpty(sim.Events.Log);
            sim.Tick(0.5f);
            Assert.AreEqual(2.5f, sim.State.OxygenRemaining);
        }

        [Test]
        public void BothDocked_CompletesOnce_AndFreezesClock()
        {
            var sim = NewSim();
            DockBoth();
            sim.Tick(0.25f);
            Assert.AreEqual(SimStatus.Complete, sim.State.Status);
            sim.Tick(100);
            Assert.AreEqual(2.75f, sim.State.OxygenRemaining);
            Assert.AreEqual(1, sim.Events.Log.OfType<LevelComplete>().Count());
        }

        [TestCase(false)] [TestCase(true)]
        public void MissingOrGhostShip_DoesNotWin(bool ghost)
        {
            var sim = NewSim();
            if (ghost) DockBoth();
            else _state.GetShip(ShipId.Kestrel).Position = _state.GetShip(ShipId.Kestrel).Dock;
            _state.GetShip(ShipId.Atlas).IsGhost = ghost;
            sim.Tick(0);
            Assert.AreEqual(SimStatus.Playing, sim.State.Status);
        }

        [Test]
        public void LastMoveOntoDock_AndOxygenExpiryInSameTick_Fails()
        {
            var sim = NewSim(0.5f);
            DockBoth();
            var ship = _state.GetShip(ShipId.Kestrel);
            ship.Position = ship.Dock + new GridPos(-1, 0);
            sim.SetHeldDirection(Direction.Right);
            sim.Tick(0.5f);
            Assert.AreEqual(ship.Dock, ship.Position);
            Assert.AreEqual(SimStatus.Failed, sim.State.Status);
            Assert.IsFalse(sim.Events.Log.OfType<LevelComplete>().Any());
            Assert.AreEqual(FailReason.OutOfOxygen, sim.Events.Log.OfType<LevelFailed>().Single().Reason);
        }

        [TestCase(3f)] [TestCase(0f)]
        public void NoLives_OverridesDockingAndOxygenFailure(float oxygen)
        {
            // Stage-5 handoff: #14 will produce this no-lives state after the last crush.
            var sim = NewSim(oxygen, 0);
            DockBoth();
            sim.Tick(0);
            Assert.AreEqual(FailReason.Crushed, sim.Events.Log.OfType<LevelFailed>().Single().Reason);
            Assert.IsFalse(sim.Events.Log.OfType<LevelComplete>().Any());
        }

        [Test]
        public void NotificationOrder_OxygenThenFailure_ListenersSeeFinalState()
        {
            var sim = NewSim(1);
            sim.Events.OxygenChanged += _ => Assert.AreEqual(SimStatus.Failed, sim.State.Status);
            sim.Tick(1);
            Assert.IsInstanceOf<OxygenChanged>(sim.Events.Log[0]);
            Assert.IsInstanceOf<LevelFailed>(sim.Events.Log[1]);
        }

        [TestCase(12f, 12f)] [TestCase(0f, 90f)]
        public void LevelTime_OverridesConfig_OrUsesDefault(float levelTime, float expected)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var data = ScriptableObject.CreateInstance<LevelData>();
            try
            {
                data.timeLimitSeconds = levelTime;
                config.defaultTimeLimitSeconds = 90;
                var sim = NewSim(config.OxygenFor(data));
                Assert.AreEqual(expected, sim.State.OxygenTotal);
                sim.Tick(1);
                Assert.AreEqual(expected - 1, sim.State.OxygenRemaining);
            }
            finally { Object.DestroyImmediate(config); Object.DestroyImmediate(data); }
        }
    }
}
