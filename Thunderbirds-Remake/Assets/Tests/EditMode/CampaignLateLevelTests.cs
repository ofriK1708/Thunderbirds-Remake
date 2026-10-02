using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// L3 - Heavy Lift and L5 - The Vault (issues #23, #24), played through the real rules from the catalog.
    /// L4 - Rockfall has its own file. A route is a script: R3 L2 U1 D4 move the active ship that many cells,
    /// S switches ship, W2 waits two seconds.
    /// </summary>
    public class CampaignLateLevelTests
    {
        private const float ThinkingSeconds = 1f; // a pause before every instruction, like a player deciding

        private Simulation _sim;
        private LevelData _level;
        private GameConfig _config;

        private void Load(int catalogIndex, string expectedName)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset");
            _level = catalog.Get(catalogIndex);
            Assert.IsNotNull(_level, $"catalog slot {catalogIndex}");
            Assert.AreEqual(expectedName, _level.name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(_level.hintText));
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Config/GameConfig.asset");
            var definition = LevelParser.Parse(_level.grid, _config.atlas.pushCapacity);
            _sim = new Simulation(() => definition.CreateState(_config.ToSimulationConfig(), _config.livesPerLevel,
                _config.OxygenFor(_level)), _config.ToSimulationConfig);
            Wait(_config.hintBannerSeconds); // read the hint first
        }

        private void Wait(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += 0.02f) _sim.Tick(0.02f);
        }

        /// <summary>Returns false (without failing) when a move is refused, so tests can assert on refusals.</summary>
        private bool TryMove(Direction direction, int cells)
        {
            for (var i = 0; i < cells; i++)
            {
                var ship = _sim.State.GetShip(_sim.State.ActiveShip);
                var start = ship.Position;
                _sim.SetHeldDirection(direction);
                for (var frame = 0; frame < 60 && ship.Position == start; frame++) _sim.Tick(0.01f);
                _sim.ClearInput();
                if (ship.Position == start) return false;
            }
            return true;
        }

        private void Play(string route)
        {
            foreach (var token in route.Split(' '))
            {
                Wait(ThinkingSeconds);
                switch (token[0])
                {
                    case 'S': _sim.SwitchShip(); break;
                    case 'W': Wait(float.Parse(token.Substring(1), System.Globalization.CultureInfo.InvariantCulture)); break;
                    default:
                        var direction = token[0] == 'R' ? Direction.Right : token[0] == 'L' ? Direction.Left
                            : token[0] == 'U' ? Direction.Up : Direction.Down;
                        Assert.IsTrue(TryMove(direction, int.Parse(token.Substring(1))),
                            $"{_level.name}: {_sim.State.ActiveShip} was refused during '{token}' of \"{route}\"");
                        break;
                }
            }
        }

        private void AssertComplete(bool expectNoStress = true)
        {
            Assert.AreEqual(SimStatus.Complete, _sim.State.Status);
            foreach (var ship in _sim.State.Ships) Assert.AreEqual(ship.Dock, ship.Position);
            Assert.AreEqual(_config.livesPerLevel, _sim.State.LivesLeft);
            if (expectNoStress) Assert.IsEmpty(_sim.Events.Log.OfType<ShipStressed>());
        }

        // ------------------------------------------------------------------ L3 ----

        [Test]
        public void L3_KestrelCannotLiftTheBar_ButAtlasCan()
        {
            Load(2, "L3_HeavyLift");
            var bar = _sim.State.GetBlock(new BlockId('h'));
            Assert.Greater(bar.Weight, _config.kestrel.loadCapacity);
            Assert.LessOrEqual(bar.Weight, _config.atlas.loadCapacity);

            Play("R4 U3");
            Assert.IsFalse(TryMove(Direction.Up, 1), "the bar covers the shaft and is too heavy for Kestrel");
            var refused = _sim.Events.Log.OfType<MoveRefused>().Single();
            Assert.AreEqual(RefuseReason.TooHeavy, refused.Reason);
            Assert.AreEqual(ColourClass.Heavy, refused.ChainColour, "so the message says: try Atlas");
        }

        [Test]
        public void L3_AtlasLiftsCarriesAndSetsTheBarDown_ThenKestrelGetsThrough()
        {
            Load(2, "L3_HeavyLift");
            Play("S L2 U5");
            var bar = _sim.State.GetBlock(new BlockId('h'));
            Assert.AreEqual(ShipId.Atlas, bar.CarriedBy, "lifted: the bar rides on Atlas");

            Play("R11");
            Assert.AreEqual(ShipId.Atlas, bar.CarriedBy, "carried sideways");

            Play("D3");
            Assert.IsNull(bar.CarriedBy, "set down on the rim of the pit: too wide to follow Atlas in");
            Assert.AreEqual(6, bar.Position.Y);
            Assert.IsTrue(_sim.Events.Log.OfType<BlockReleased>().Any(e => e.Block == bar.Id));

            Play("S R4 U5 L4");
            AssertComplete();
            Assert.Greater(_sim.State.OxygenRemaining, 60f, "a teaching level: plenty of oxygen left");
        }

        // ------------------------------------------------------------------ L4 ----

        [Test]
        public void L4_IsInTheCatalog_AfterL3()
        {
            Load(3, "L4_Rockfall"); // its play-throughs are in RockfallLevelTests
        }

        // ------------------------------------------------------------------ L5 ----

        [Test]
        public void L5_AtlasFirst_BarScrapedIntoTheTrench_SlabDropped_ThenKestrel()
        {
            Load(4, "L5_TheVault");
            Play("S L2 U5 R25");
            Assert.AreEqual(5, _sim.State.GetBlock(new BlockId('h')).Position.Y, "the bar lies in the trench, flush with the floor");
            Assert.AreEqual(1, _sim.State.GetBlock(new BlockId('s')).Position.Y, "the slab fell into the lower chamber");

            Play("S R4 U5 R19 D4 L2 D1 L4");
            AssertComplete();
            Assert.AreEqual(new GridPos(17, 1), _sim.State.GetBlock(new BlockId('q')).Position, "Kestrel pushed the small block clear of its dock");
        }

        [Test]
        public void L5_KestrelOvertakesAndDocksFirst_ThenAtlasDropsTheSlab()
        {
            Load(4, "L5_TheVault");
            Play("S L2 U5 R11 S R4 U5 U3 R21 D8 L8 S R14");
            AssertComplete();
        }

        [Test]
        public void L5_KestrelWaitsUnderTheHole_GetsHit_AndEscapesIntoTheTunnel()
        {
            Load(4, "L5_TheVault");
            Play("S L2 U5 R11 S R4 U5 U3 R21 D8"); // Kestrel parks under the hole
            _sim.SwitchShip();
            Assert.IsTrue(TryMove(Direction.Right, 9)); // Atlas pushes the slab in
            Wait(0.5f);
            Assert.IsTrue(_sim.State.GetShip(ShipId.Kestrel).IsStressed, "the slab landed on Kestrel");

            _sim.SwitchShip();
            Assert.IsTrue(TryMove(Direction.Left, 6), "the tunnel roof scrapes the slab off");
            Wait(0.5f);
            Assert.IsFalse(_sim.State.GetShip(ShipId.Kestrel).IsStressed);
            Assert.IsEmpty(_sim.Events.Log.OfType<ShipCrushed>());

            Play("L2 S R5");
            AssertComplete(expectNoStress: false);
        }

        [Test]
        public void L5_OxygenIsTight_ButEnoughForAPlayerWhoKnowsTheRoute()
        {
            Load(4, "L5_TheVault");
            Assert.Less(_level.timeLimitSeconds, 90f, "tighter than the teaching levels");
            Play("S L2 U5 R25 S R4 U5 R19 D4 L2 D1 L4");
            AssertComplete();
            Assert.Greater(_sim.State.OxygenRemaining, 10f, "finishable with a second of thought per decision");
            Assert.Less(_sim.State.OxygenRemaining, 45f, "but not with time to waste");
        }
    }
}
