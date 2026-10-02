using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;

namespace Thunderbirds.Tests.EditMode
{
    public class CampaignLevelTests
    {
        private Simulation _sim;
        private LevelData _level;

        private void Load(int index)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset");
            _level = catalog.Get(index);
            Assert.IsNotNull(_level);
            Assert.IsFalse(string.IsNullOrWhiteSpace(_level.hintText));
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Config/GameConfig.asset");
            var definition = LevelParser.Parse(_level.grid, config.atlas.pushCapacity);
            _sim = new Simulation(() => definition.CreateState(config.ToSimulationConfig(), config.livesPerLevel,
                config.OxygenFor(_level)), config.ToSimulationConfig);
            _sim.Tick(12f); // time to read the hint before moving
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
                Assert.AreEqual(start + direction.ToOffset(), ship.Position, $"{_level.name}: {ship.Id} {direction}, step {i + 1}");
            }
            _sim.Tick(3f); // generous thinking time between instructions
        }

        private void AssertSafeCompletion(float maxSeconds)
        {
            Assert.AreEqual(SimStatus.Complete, _sim.State.Status);
            Assert.Less(_level.timeLimitSeconds - _sim.State.OxygenRemaining, maxSeconds);
            Assert.Greater(_sim.State.OxygenRemaining, 30f);
            Assert.IsFalse(_sim.Events.Log.OfType<ShipStressed>().Any());
            Assert.IsFalse(_sim.Events.Log.OfType<ShipCrushed>().Any());
            foreach (var ship in _sim.State.Ships) Assert.AreEqual(ship.Dock, ship.Position);
        }

        [Test]
        public void L1_MovementSwitchAndDock_CompletesUnderOneMinute()
        {
            Load(0);
            Assert.IsEmpty(_sim.State.Blocks);
            Move(Direction.Right, 16);
            Assert.AreEqual(SimStatus.Playing, _sim.State.Status, "one docked ship is insufficient");
            _sim.SwitchShip();
            Move(Direction.Right, 15);
            Move(Direction.Up, 2);
            AssertSafeCompletion(60f);
        }

        [Test]
        public void L2_KestrelCannotPushYellow_BothShipsSolveTheirRoutes()
        {
            Load(1);
            Assert.AreEqual(1, _sim.State.Blocks.Count(b => b.Colour == ColourClass.Teal));
            Assert.AreEqual(1, _sim.State.Blocks.Count(b => b.Colour == ColourClass.Yellow));
            Move(Direction.Right, 2);
            var before = _sim.State.GetShip(ShipId.Kestrel).Position;
            _sim.SetHeldDirection(Direction.Right);
            _sim.Tick(0.2f);
            _sim.ClearInput();
            Assert.AreEqual(before, _sim.State.GetShip(ShipId.Kestrel).Position);
            Assert.IsTrue(_sim.Events.Log.OfType<MoveRefused>().Any(e => e.Ship == ShipId.Kestrel && e.Reason == RefuseReason.TooHeavy));
            Move(Direction.Left, 2);
            Move(Direction.Up, 4);
            Move(Direction.Right, 12);
            Assert.AreEqual(SimStatus.Playing, _sim.State.Status);
            _sim.SwitchShip();
            Move(Direction.Right, 19);
            AssertSafeCompletion(60f);
        }
    }
}
