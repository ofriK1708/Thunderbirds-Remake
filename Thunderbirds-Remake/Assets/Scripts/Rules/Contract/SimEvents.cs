using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// Something the simulation announces. Events say what changed; <see cref="IReadOnlySimulationState"/>
    /// says what is. The list below is the contract between the layers (GDD §7, issue #3).
    /// </summary>
    public abstract class SimEvent
    {
    }

    public sealed class ShipMoved : SimEvent
    {
        public readonly ShipId Ship;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly float StepSeconds;

        public ShipMoved(ShipId ship, GridPos from, GridPos to, float stepSeconds)
        {
            Ship = ship;
            From = from;
            To = to;
            StepSeconds = stepSeconds;
        }
    }

    /// <summary>
    /// The ship bumps and the chain flashes <see cref="ChainColour"/>. Raised on the first refusal of a hold;
    /// while the direction stays held the ship retries silently (see <see cref="RefusalHint"/>).
    /// </summary>
    public sealed class MoveRefused : SimEvent
    {
        public readonly ShipId Ship;
        public readonly Direction Direction;
        public readonly RefuseReason Reason;
        public readonly IReadOnlyList<BlockId> Chain;

        /// <summary>Colour class of the chain's total weight: red when no ship could push it.</summary>
        public readonly ColourClass ChainColour;

        public MoveRefused(ShipId ship, Direction direction, RefuseReason reason, IReadOnlyList<BlockId> chain,
            ColourClass chainColour)
        {
            Ship = ship;
            Direction = direction;
            Reason = reason;
            Chain = chain ?? new BlockId[0];
            ChainColour = chainColour;
        }
    }

    /// <summary>
    /// The player has kept pushing into a refused move for refusalHintSeconds. Raised once per hold;
    /// the HUD turns it into a hint popup (e.g. "Too heavy for Kestrel").
    /// </summary>
    public sealed class RefusalHint : SimEvent
    {
        public readonly ShipId Ship;
        public readonly Direction Direction;
        public readonly RefuseReason Reason;
        public readonly IReadOnlyList<BlockId> Chain;

        public RefusalHint(ShipId ship, Direction direction, RefuseReason reason, IReadOnlyList<BlockId> chain)
        {
            Ship = ship;
            Direction = direction;
            Reason = reason;
            Chain = chain ?? new BlockId[0];
        }
    }

    /// <summary>A block pushed or carried one cell.</summary>
    public sealed class BlockMoved : SimEvent
    {
        public readonly BlockId Block;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly float StepSeconds;

        public BlockMoved(BlockId block, GridPos from, GridPos to, float stepSeconds)
        {
            Block = block;
            From = from;
            To = to;
            StepSeconds = stepSeconds;
        }
    }

    /// <summary>A block fell one cell. Raised once per cell.</summary>
    public sealed class BlockFell : SimEvent
    {
        public readonly BlockId Block;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly float StepSeconds;

        public BlockFell(BlockId block, GridPos from, GridPos to, float stepSeconds)
        {
            Block = block;
            From = from;
            To = to;
            StepSeconds = stepSeconds;
        }
    }

    public sealed class BlockLanded : SimEvent
    {
        public readonly BlockId Block;

        public BlockLanded(BlockId block)
        {
            Block = block;
        }
    }

    /// <summary>A carried block hit an obstacle and was left behind.</summary>
    public sealed class BlockReleased : SimEvent
    {
        public readonly BlockId Block;
        public readonly ShipId From;

        public BlockReleased(BlockId block, ShipId from)
        {
            Block = block;
            From = from;
        }
    }

    public sealed class ShipStressed : SimEvent
    {
        public readonly ShipId Ship;
        public readonly int Load;
        public readonly float GraceSeconds;

        public ShipStressed(ShipId ship, int load, float graceSeconds)
        {
            Ship = ship;
            Load = load;
            GraceSeconds = graceSeconds;
        }
    }

    public sealed class ShipRelieved : SimEvent
    {
        public readonly ShipId Ship;

        public ShipRelieved(ShipId ship)
        {
            Ship = ship;
        }
    }

    public sealed class ShipCrushed : SimEvent
    {
        public readonly ShipId Ship;
        public readonly int LivesLeft;

        public ShipCrushed(ShipId ship, int livesLeft)
        {
            Ship = ship;
            LivesLeft = livesLeft;
        }
    }

    /// <summary>
    /// Raised with isGhost = true when the start area is occupied, then again with
    /// isGhost = false when the ship materialises.
    /// </summary>
    public sealed class ShipRespawned : SimEvent
    {
        public readonly ShipId Ship;
        public readonly GridPos At;
        public readonly bool IsGhost;

        public ShipRespawned(ShipId ship, GridPos at, bool isGhost)
        {
            Ship = ship;
            At = at;
            IsGhost = isGhost;
        }
    }

    public sealed class ActiveShipChanged : SimEvent
    {
        public readonly ShipId Active;
        public readonly bool Automatic; // true when switched because the other ship became a ghost

        public ActiveShipChanged(ShipId active, bool automatic)
        {
            Active = active;
            Automatic = automatic;
        }
    }

    /// <summary>Raised only when the whole-second value changes; read State for a smooth bar.</summary>
    public sealed class OxygenChanged : SimEvent
    {
        public readonly float Remaining;
        public readonly float Total;

        public OxygenChanged(float remaining, float total)
        {
            Remaining = remaining;
            Total = total;
        }
    }

    public sealed class LevelComplete : SimEvent
    {
    }

    public sealed class LevelFailed : SimEvent
    {
        public readonly FailReason Reason;

        public LevelFailed(FailReason reason)
        {
            Reason = reason;
        }
    }
}
