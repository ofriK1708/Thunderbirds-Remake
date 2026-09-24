namespace Thunderbirds.Rules
{
    /// <summary>A ship as views see it. Setters are internal: only the rules layer changes it.</summary>
    public sealed class ShipState
    {
        public ShipId Id { get; }
        public int Width { get; }
        public int Height { get; }
        public GridPos Start { get; }
        public GridPos Dock { get; }

        public GridPos Position { get; internal set; }
        public bool IsGhost { get; internal set; }
        public bool IsStressed { get; internal set; }
        public int Load { get; internal set; }
        public float CrushSecondsLeft { get; internal set; }

        public ShipState(ShipId id, int width, int height, GridPos start, GridPos dock)
        {
            Id = id;
            Width = width;
            Height = height;
            Start = start;
            Dock = dock;
            Position = start;
        }
    }
}
