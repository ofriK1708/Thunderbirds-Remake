namespace Thunderbirds.Rules
{
    /// <summary>Why a ship move was refused (the view bumps and flashes the chain).</summary>
    public enum RefuseReason
    {
        TooHeavy,   // push chain or lift weight above capacity
        Blocked,    // front of the push chain hits a wall or ship
        Ceiling,    // a lift or carried block would hit a ceiling
        FallWonTie  // a falling block took the cell this tick
    }

    public enum FailReason
    {
        Crushed,
        OutOfOxygen
    }

    /// <summary>Block colour by weight vs. push capacities (GDD §3 Blocks).</summary>
    public enum ColourClass
    {
        Teal,   // weight <= Kestrel pushCapacity
        Yellow, // weight <= Atlas pushCapacity
        Red     // too heavy for either ship
    }

    public enum SimStatus
    {
        Playing,
        Complete,
        Failed
    }
}
