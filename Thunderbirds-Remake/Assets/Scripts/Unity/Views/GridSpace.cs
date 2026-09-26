using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Grid ↔ world conversion, shared by every view (GDD §6: 1 grid cell = 1 world unit).
    /// The grid's bottom-left corner is at the world origin; cell (x, y) covers [x, x+1] × [y, y+1].
    /// </summary>
    public static class GridSpace
    {
        /// <summary>World centre of one cell.</summary>
        public static Vector3 CellCenter(GridPos p) => new Vector3(p.X + 0.5f, p.Y + 0.5f, 0f);

        /// <summary>
        /// World centre of a width × height footprint whose bottom-left cell is <paramref name="bottomLeft"/>
        /// (ships, docks). Positions in the rules are always bottom-left cells.
        /// </summary>
        public static Vector3 FootprintCenter(GridPos bottomLeft, int width, int height) =>
            new Vector3(bottomLeft.X + width * 0.5f, bottomLeft.Y + height * 0.5f, 0f);
    }
}
