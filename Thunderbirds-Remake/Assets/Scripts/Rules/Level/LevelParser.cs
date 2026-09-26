using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// Text grid → validated <see cref="LevelDefinition"/> (GDD §7 Level format).
    /// One character = one cell; the first text line is the top row. Errors name the character and the
    /// row/column as the author sees them (1-based, row 1 = first line).
    /// </summary>
    public static class LevelParser
    {
        public const char Wall = '#', Empty = '.';
        public const char KestrelStart = 'K', AtlasStart = 'A';
        public const char KestrelDock = '1', AtlasDock = '2';

        /// <param name="maxBlockWeight">Atlas's pushCapacity: no single block may be authored red.</param>
        /// <exception cref="LevelParseException">Lists every problem in the text.</exception>
        public static LevelDefinition Parse(string text, int maxBlockWeight)
        {
            var errors = new List<string>();
            var rows = SplitRows(text);
            if (rows.Count == 0)
                throw new LevelParseException(new[] { "The level is empty." });

            var width = rows[0].Length;
            var height = rows.Count;
            for (var r = 1; r < rows.Count; r++)
                if (rows[r].Length != width)
                    errors.Add($"Row {r + 1} has {rows[r].Length} characters, expected {width} (the length of row 1).");
            if (errors.Count > 0) throw new LevelParseException(errors);

            // Group every non-wall, non-empty cell by its character, in grid coordinates (y = 0 at the bottom).
            var walls = new bool[width, height];
            // Cells are collected top row first, so cells[0] is the one the author sees first.
            var cellsByChar = new SortedDictionary<char, List<GridPos>>();
            for (var r = 0; r < height; r++)
            {
                var y = height - 1 - r;
                for (var x = 0; x < width; x++)
                {
                    var c = rows[r][x];
                    if (c == Wall) { walls[x, y] = true; continue; }
                    if (c == Empty) continue;

                    if (!IsKnown(c))
                    {
                        errors.Add($"Unknown character '{c}' at {Where(new GridPos(x, y), height)}.");
                        continue;
                    }
                    if (!cellsByChar.TryGetValue(c, out var list))
                        cellsByChar[c] = list = new List<GridPos>();
                    list.Add(new GridPos(x, y));
                }
            }

            var kestrel = ReadRectangle(cellsByChar, KestrelStart, "Kestrel start", LevelDefinition.KestrelWidth, LevelDefinition.KestrelHeight, height, errors);
            var atlas = ReadRectangle(cellsByChar, AtlasStart, "Atlas start", LevelDefinition.AtlasWidth, LevelDefinition.AtlasHeight, height, errors);
            var kestrelDock = ReadRectangle(cellsByChar, KestrelDock, "Kestrel dock", LevelDefinition.KestrelWidth, LevelDefinition.KestrelHeight, height, errors);
            var atlasDock = ReadRectangle(cellsByChar, AtlasDock, "Atlas dock", LevelDefinition.AtlasWidth, LevelDefinition.AtlasHeight, height, errors);

            var blocks = new List<BlockDefinition>();
            foreach (var pair in cellsByChar)
            {
                var letter = pair.Key;
                if (letter < 'a' || letter > 'z') continue; // ships and docks were handled above
                var cells = pair.Value;

                if (!IsConnected(cells))
                    errors.Add($"Block '{letter}' is split into separate pieces (first cell at {Where(cells[0], height)}). " +
                               "All cells of one letter must touch side by side.");
                if (cells.Count > maxBlockWeight)
                    errors.Add($"Block '{letter}' weighs {cells.Count}, more than Atlas can push ({maxBlockWeight}) " +
                               $"(first cell at {Where(cells[0], height)}).");

                blocks.Add(ToBlock(letter, cells));
            }

            if (errors.Count > 0) throw new LevelParseException(errors);
            return new LevelDefinition(walls, kestrel, atlas, kestrelDock, atlasDock, blocks);
        }

        /// <summary>
        /// True when every cell can be reached from the first one by stepping between cells in the list.
        /// A block is one rigid piece, so its cells must be connected (GDD §7 validation).
        /// </summary>
        internal static bool IsConnected(IReadOnlyList<GridPos> cells)
        {
            if (cells.Count == 0) return false;

            // BFS from cells[0]. A cell leaves `unvisited` the moment it is discovered, so it is queued once.
            var unvisited = new HashSet<GridPos>(cells);
            var queue = new Queue<GridPos>();
            unvisited.Remove(cells[0]);
            queue.Enqueue(cells[0]);
            while (queue.Count > 0)
            {
                var currentPos = queue.Dequeue();
                foreach (var offset in NeighbourOffsets)
                {
                    var newPos = currentPos + offset;
                    if (unvisited.Remove(newPos))
                        queue.Enqueue(newPos);
                }
            }
            return unvisited.Count == 0;
        }

        // ------------------------------------------------------------ helpers ----

        /// <summary>Side neighbours only: cells touching at a corner are not one rigid piece (GDD §6 borders).</summary>
        private static readonly GridPos[] NeighbourOffsets =
        {
            new GridPos(0, 1), new GridPos(0, -1), new GridPos(1, 0), new GridPos(-1, 0)
        };

        private static List<string> SplitRows(string text)
        {
            var rows = new List<string>();
            if (string.IsNullOrEmpty(text)) return rows;

            foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                rows.Add(raw.Trim()); // the Inspector's TextArea can leave stray spaces at line ends

            // Blank lines before and after the grid are ignored; blank lines inside it are an error (row length).
            while (rows.Count > 0 && rows[0].Length == 0) rows.RemoveAt(0);
            while (rows.Count > 0 && rows[^1].Length == 0) rows.RemoveAt(rows.Count - 1);
            return rows;
        }

        private static bool IsKnown(char c) =>
            c == KestrelStart || c == AtlasStart || c == KestrelDock || c == AtlasDock || (c >= 'a' && c <= 'z');

        /// <summary>A ship start or dock: exactly one filled rectangle of the given size. Returns its bottom-left.</summary>
        private static GridPos ReadRectangle(IDictionary<char, List<GridPos>> cellsByChar, char c, string what,
            int width, int height, int gridHeight, List<string> errors)
        {
            if (!cellsByChar.TryGetValue(c, out var cells))
            {
                errors.Add($"Missing {what} '{c}' (needs one {width}x{height} rectangle).");
                return default;
            }

            var min = Min(cells);
            var ok = cells.Count == width * height;
            if (ok)
                foreach (var p in cells)
                    if (p.X - min.X >= width || p.Y - min.Y >= height) { ok = false; break; }

            if (!ok)
                errors.Add($"{what} '{c}' must be exactly one {width}x{height} rectangle; found {cells.Count} cell(s), " +
                           $"first at {Where(cells[0], gridHeight)}.");
            return min;
        }

        private static BlockDefinition ToBlock(char letter, List<GridPos> cells)
        {
            var min = Min(cells);
            var offsets = new GridPos[cells.Count];
            for (var i = 0; i < cells.Count; i++) offsets[i] = cells[i] - min;
            return new BlockDefinition(new BlockId(letter), min, offsets);
        }

        /// <summary>Bottom-left corner of the bounding box (the block/ship position convention).</summary>
        private static GridPos Min(List<GridPos> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var p in cells)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
            }
            return new GridPos(minX, minY);
        }

        /// <summary>Grid position → "row R, column C" as seen in the text (1-based, row 1 = top line).</summary>
        private static string Where(GridPos p, int gridHeight) => $"row {gridHeight - p.Y}, column {p.X + 1}";
    }
}
