using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// L6 - The Hook, L7 - The Gate and L8 - The Cork: the three big levels (L6 is 50 cells wide, L7 and L8 are 80), played through the real
    /// rules from the catalog. A route is a script: R3 L2 U1 D4 move the active ship that many cells,
    /// S switches ship, W2 waits two seconds.
    /// </summary>
    public class CampaignBigLevelTests
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

        private BlockState Block(char letter) => _sim.State.GetBlock(new BlockId(letter));

        private MoveRefused LastRefusal() => _sim.Events.Log.OfType<MoveRefused>().Last();

        private void AssertComplete()
        {
            Assert.AreEqual(SimStatus.Complete, _sim.State.Status);
            foreach (var ship in _sim.State.Ships) Assert.AreEqual(ship.Dock, ship.Position);
            Assert.AreEqual(_config.livesPerLevel, _sim.State.LivesLeft);
            Assert.IsEmpty(_sim.Events.Log.OfType<ShipStressed>());
            Assert.Greater(_sim.State.OxygenRemaining, 60f, "a big level: a minute of oxygen left to look around");
        }

        // ------------------------------------------------------------------ L6 ----

        [Test]
        public void L6_KestrelIsShutInTheCellar_UntilAtlasHoldsTheHookUp()
        {
            Load(5, "L6_TheHook");
            Assert.Greater(Block('a').Weight, _config.kestrel.pushCapacity);
            Assert.LessOrEqual(Block('a').Weight, _config.atlas.loadCapacity);

            Assert.IsFalse(TryMove(Direction.Left, 8), "the hook's stem stands in the cellar");
            Assert.AreEqual(RefuseReason.Blocked, LastRefusal().Reason);

            Play("S D2 L6 U3");
            Assert.AreEqual(ShipId.Atlas, Block('a').CarriedBy, "lifted to the roof: the stem is clear of the cellar");

            Play("S L6");
            Assert.IsEmpty(_sim.Events.Log.OfType<ShipStressed>(), "one free cell between Kestrel and the stem");
        }

        [Test]
        public void L6_KestrelTakesTheShaft_ThenAtlasGoesRoundAndPushesTheDoorIn()
        {
            Load(5, "L6_TheHook");
            Play("S D2 L6 U3 S L13 U11 R15 U10");
            Assert.AreEqual(_sim.State.GetShip(ShipId.Kestrel).Dock, _sim.State.GetShip(ShipId.Kestrel).Position);

            Play("S D3 R1 U3 L37 U7");
            Assert.AreEqual(ShipId.Atlas, Block('r').CarriedBy, "the bar over the opening rides up on Atlas");

            Play("R38");
            Assert.IsNull(Block('r').CarriedBy, "the lintel scraped the bar off");

            Play("U4 R6");
            AssertComplete();
        }

        [Test]
        public void L6_DoorPushedInFirst_FillsTheShaft_AndKestrelCannotClimb()
        {
            Load(5, "L6_TheHook");
            Play("S D2 L6 U3 S L13 U11 S D3 R1 U3 L37 U7 R38 U4 R4 W3 S");
            Assert.IsFalse(TryMove(Direction.Right, 15), "the three door blocks stand at the foot of the shaft");
            Assert.AreEqual(RefuseReason.Blocked, LastRefusal().Reason);
            Assert.AreEqual(SimStatus.Playing, _sim.State.Status);
        }

        // ------------------------------------------------------------------ L7 ----

        [Test]
        public void L7_ThePlugStandsOnAtlas_AndDropsOutOfKestrelsRoadWhenAtlasLeaves()
        {
            Load(6, "L7_TheGate");
            Assert.AreEqual(ShipId.Atlas, Block('b').CarriedBy, "placed on Atlas in the level text");

            Play("D4");
            Assert.IsFalse(TryMove(Direction.Right, 16), "the plug stands in the low road");

            Play("S R1 W1");
            Assert.AreEqual(2, Block('b').Position.Y, "fell through the corridor into the room below");

            Play("S R20");
            Assert.AreEqual(35, _sim.State.GetShip(ShipId.Kestrel).Position.X, "through to the far side of the ring");
        }

        [Test]
        public void L7_AtlasSinksTheCup_OpensTheGate_AndBothClimbTheShaft()
        {
            Load(6, "L7_TheGate");
            Play("S R45");
            Assert.AreEqual(3, Block('c').Position.Y, "the cup sank into the pit, flush with the corridor floor");

            Play("U9 L19 W2");
            Assert.AreEqual(1, Block('d').Position.Y, "the gate fell down the shaft and through the floor into the cellar");

            Play("R19 D9 S D4 R32 U3 R4 U8 S L24 U12");
            AssertComplete();
        }

        [Test]
        public void L7_KestrelCanTakeTheHighRoadInstead_PushingTheBlockOffTheEnd()
        {
            Load(6, "L7_TheGate");
            Play("U7 R32 D8");
            Assert.AreEqual(8, Block('a').Position.Y, "the block fell down the far side onto the low road");

            Play("S R45 U9 L19 W2 R19 D9 S R4 U8 S L24 U12");
            AssertComplete();
        }

        [Test]
        public void L7_KestrelWaitingInTheShaft_IsCrushedByTheGate()
        {
            Load(6, "L7_TheGate");
            Play("S R45 S D4 R32 U3 R6 S U9 L19");
            Wait(0.5f);
            Assert.IsTrue(_sim.State.GetShip(ShipId.Kestrel).IsStressed, "the gate landed on Kestrel");

            Wait(_config.crushGraceSeconds + 0.5f);
            Assert.AreEqual(ShipId.Kestrel, _sim.Events.Log.OfType<ShipCrushed>().Single().Ship);
            Assert.AreEqual(_config.livesPerLevel - 1, _sim.State.LivesLeft);
        }

        // ------------------------------------------------------------------ L8 ----

        [Test]
        public void L8_TheCorkCannotBePushed_ByKestrelAbove_OrByAtlasBelow()
        {
            Load(7, "L8_TheCork");
            Play("R14 U2");
            Assert.AreEqual(17, Block('a').Position.Y, "the small block dropped into its pocket");
            Assert.IsFalse(TryMove(Direction.Right, 7), "the cork stands through a hole in the floor");
            Assert.AreEqual(RefuseReason.Blocked, LastRefusal().Reason);

            Play("S U5 R26 U8");
            Assert.IsFalse(TryMove(Direction.Left, 4), "the stone under the cork cannot be pushed out either");
            Assert.AreEqual(RefuseReason.Blocked, LastRefusal().Reason);
        }

        [Test]
        public void L8_AtlasCarriesTheStoneAway_CorkDropsFlush_ThenAtlasClearsTheDocks()
        {
            Load(7, "L8_TheCork");
            Play("S U5 R26 U7 L5 U1");
            Assert.AreEqual(ShipId.Atlas, Block('d').CarriedBy);
            Assert.AreEqual(ShipId.Atlas, Block('c').CarriedBy, "the cork rides up with the stone: Atlas's full load");
            Assert.IsFalse(_sim.State.GetShip(ShipId.Atlas).IsStressed);

            Play("R3 W1");
            Assert.AreEqual(13, Block('c').Position.Y, "the cork's top is now the floor of Kestrel's corridor");

            Play("R2 D8 R21 U11 R22 L2");
            Assert.AreEqual(16, Block('b').Position.Y, "the small block was pushed into the pit");
            Assert.AreEqual(74, Block('e').Position.X, "the bar is against the far wall, clear of both docks");

            Play("S R14 U2 R10 D2 R35 U1 R12 D2");
            AssertComplete();
        }

        [Test]
        public void L8_KestrelDockedEarly_IsInTheBarsWay_AndMustLiftOffForAtlas()
        {
            Load(7, "L8_TheCork");
            Play("R14 U2 R6 S U5 R26 U7 L5 U1 R3 S R4 D2 R35 U1 R12 D2"); // Kestrel waits at the cork, then docks
            Play("S R2 D8 R21 U11");
            Assert.IsFalse(TryMove(Direction.Right, 22), "the bar runs into Kestrel on its dock");
            Assert.AreEqual(RefuseReason.Blocked, LastRefusal().Reason);
            Assert.AreEqual(SimStatus.Playing, _sim.State.Status);

            Play("S U2 S R7 L2 S D2");
            AssertComplete();
        }
    }
}
