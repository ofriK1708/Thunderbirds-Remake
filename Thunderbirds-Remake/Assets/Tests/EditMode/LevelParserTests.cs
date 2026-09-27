using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class LevelParserTests
    {
        private const int AtlasPush = 8;

        /// <summary>The example level from GDD §7.</summary>
        private const string GddExample =
            "################################\n" +
            "#..............................#\n" +
            "#..........................cccc#\n" +
            "#......bbb.................cccc#\n" +
            "#KK....b...................AAAA#\n" +
            "#KK...######...............AAAA#\n" +
            "#11...#....#............dd.2222#\n" +
            "#11...#....#............d..2222#\n" +
            "################################";

        private static LevelParseException ParseFails(string text) =>
            Assert.Throws<LevelParseException>(() => LevelParser.Parse(text, AtlasPush));

        /// <summary>The GDD example with one character replaced; row/column are 1-based as in error messages.</summary>
        private static string Put(int row, int column, char c) => Put(GddExample, row, column, c);

        private static string Put(string text, int row, int column, char c)
        {
            var rows = text.Split('\n');
            var chars = rows[row - 1].ToCharArray();
            chars[column - 1] = c;
            rows[row - 1] = new string(chars);
            return string.Join("\n", rows);
        }

        private static void AssertAnyError(LevelParseException e, params string[] fragments)
        {
            var hit = e.Errors.Any(err => fragments.All(err.Contains));
            Assert.IsTrue(hit, $"No error containing [{string.Join(", ", fragments)}]. Got:\n{e.Message}");
        }

        // ------------------------------------------------------------ valid ----

        [Test]
        public void GddExample_Parses()
        {
            var level = LevelParser.Parse(GddExample, AtlasPush);

            Assert.AreEqual(32, level.Width);
            Assert.AreEqual(9, level.Height);
            Assert.AreEqual(new GridPos(1, 3), level.KestrelStart);
            Assert.AreEqual(new GridPos(27, 3), level.AtlasStart);
            Assert.AreEqual(new GridPos(1, 1), level.KestrelDock);
            Assert.AreEqual(new GridPos(27, 1), level.AtlasDock);
            Assert.IsTrue(level.IsWall(new GridPos(0, 0)));
            Assert.IsTrue(level.IsWall(new GridPos(7, 3)), "the ledge under b");
            Assert.IsFalse(level.IsWall(new GridPos(1, 1)));
        }

        [Test]
        public void GddExample_BlocksHaveTheirWeightsAndShapes()
        {
            var level = LevelParser.Parse(GddExample, AtlasPush);
            var byLetter = level.Blocks.ToDictionary(b => b.Id.Letter);

            Assert.AreEqual(3, level.Blocks.Count);
            Assert.AreEqual(4, byLetter['b'].Weight);
            Assert.AreEqual(8, byLetter['c'].Weight);
            Assert.AreEqual(3, byLetter['d'].Weight);

            // b is an L: "bbb" over "b..", bottom-left at the lone cell
            Assert.AreEqual(new GridPos(7, 4), byLetter['b'].Position);
            CollectionAssert.AreEquivalent(
                new[] { new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1) },
                byLetter['b'].Cells);
        }

        [Test]
        public void CreateState_ColoursBlocksByPushCapacities()
        {
            var level = LevelParser.Parse(GddExample, AtlasPush);
            var state = level.CreateState(new SimulationConfig(), lives: 3, oxygenSeconds: 60f);

            Assert.AreEqual(ColourClass.Teal, state.GetBlock(new BlockId('b')).Colour);
            Assert.AreEqual(ColourClass.Yellow, state.GetBlock(new BlockId('c')).Colour);
            Assert.AreEqual(ColourClass.Teal, state.GetBlock(new BlockId('d')).Colour);
            Assert.AreEqual(new GridPos(1, 3), state.GetShip(ShipId.Kestrel).Position);
            Assert.AreEqual(3, state.LivesLeft);
            Assert.AreEqual(60f, state.OxygenTotal);
        }

        [Test]
        public void CreateState_GivesEachStateItsOwnCopy()
        {
            var level = LevelParser.Parse(GddExample, AtlasPush);
            var first = level.CreateState(new SimulationConfig(), 3, 60f);
            first.GetShip(ShipId.Kestrel).Position = new GridPos(5, 5);

            var second = level.CreateState(new SimulationConfig(), 3, 60f);

            Assert.AreEqual(level.KestrelStart, second.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void WindowsLineEndings_AndSurroundingBlankLines_AreAccepted()
        {
            var text = "\r\n" + GddExample.Replace("\n", "\r\n") + "\r\n\r\n";

            Assert.AreEqual(9, LevelParser.Parse(text, AtlasPush).Height);
        }

        // ------------------------------------------------------------ invalid ----

        [Test]
        public void Empty_Fails()
        {
            AssertAnyError(ParseFails("  \n \n"), "empty");
        }

        [Test]
        public void RaggedRows_NameTheRow()
        {
            var e = ParseFails("#####\n#KK..\n#KK.\n#####");

            AssertAnyError(e, "Row 3", "4 characters", "expected 5");
        }

        [Test]
        public void UnknownCharacter_NamesCharRowAndColumn()
        {
            AssertAnyError(ParseFails(Put(2, 15, '?')), "'?'", "row 2", "column 15");
        }

        [Test]
        public void MissingAtlas_Fails()
        {
            AssertAnyError(ParseFails(GddExample.Replace('A', '.')), "Missing Atlas start 'A'");
        }

        [Test]
        public void TwoKestrels_Fail()
        {
            var text = Put(Put(2, 2, 'K'), 2, 3, 'K'); // an extra KK on row 2

            AssertAnyError(ParseFails(text), "Kestrel start 'K'", "2x2", "found 6 cell(s)");
        }

        [Test]
        public void WrongDockShape_Fails()
        {
            // Atlas dock squeezed to 3 wide on its top row
            var text = GddExample.Replace("#11...#....#............dd.2222#",
                                          "#11...#....#............dd.222.#");

            AssertAnyError(ParseFails(text), "Atlas dock '2'", "4x2");
        }

        [Test]
        public void SplitBlock_Fails()
        {
            // a second 'd' far away from the first
            AssertAnyError(ParseFails(Put(2, 2, 'd')),"Block 'd'", "separate pieces", "row 2, column 2");
        }

        [Test]
        public void DiagonalOnlyBlock_Fails()
        {
            // d's top-right cell is row 7, column 26; row 6, column 27 touches it only at a corner
            AssertAnyError(ParseFails(Put(6, 27, 'd')),"Block 'd'", "separate pieces");
        }

        [Test]
        public void BlockHeavierThanAtlas_Fails()
        {
            AssertAnyError(ParseFails(Put(3, 27, 'c')),"Block 'c'", "weighs 9", "(8)");
        }

        [Test]
        public void ManyProblems_AreAllReported()
        {
            var e = ParseFails(GddExample.Replace('A', '.').Replace('1', '.'));

            Assert.AreEqual(2, e.Errors.Count, e.Message);
        }

        // ------------------------------------------------------------ connectivity ----

        [Test]
        public void IsConnected_SingleCell() =>
            Assert.IsTrue(LevelParser.IsConnected(new[] { new GridPos(3, 3) }));

        [Test]
        public void IsConnected_LShape() =>
            Assert.IsTrue(LevelParser.IsConnected(new[]
                { new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1) }));

        [Test]
        public void IsConnected_UShape_ReachedTheLongWayRound() =>
            Assert.IsTrue(LevelParser.IsConnected(new[]
                { new GridPos(0, 1), new GridPos(2, 1), new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0) }));

        [Test]
        public void IsConnected_TwoPieces() =>
            Assert.IsFalse(LevelParser.IsConnected(new[]
                { new GridPos(0, 0), new GridPos(1, 0), new GridPos(5, 0) }));

        [Test]
        public void IsConnected_DuplicateCells_StillConnected() =>
            Assert.IsTrue(LevelParser.IsConnected(new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 0) }));

        [Test]
        public void IsConnected_DiagonalTouch_IsNotConnected() =>
            Assert.IsFalse(LevelParser.IsConnected(new[] { new GridPos(0, 0), new GridPos(1, 1) }));
    }
}
