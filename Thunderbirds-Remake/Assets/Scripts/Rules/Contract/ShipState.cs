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

        /// <summary>
        /// Respawn protection: while this is above 0 a ghost stays a ghost even if its start area is clear,
        /// so whatever crushed it can fall through instead of landing on it again (GDD §3).
        /// </summary>
        public float GhostSecondsLeft { get; internal set; }

        /// <summary>Stage 4's handoff to lives/respawn in stage 5 (#14).</summary>
        public bool IsCrushDue => !IsGhost && IsStressed && CrushSecondsLeft <= 0f;

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
