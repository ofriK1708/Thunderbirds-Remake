using System;

namespace Thunderbirds.Rules
{
    /// <summary>Identifies a block by its letter in the level text ('a'–'z').</summary>
    public readonly struct BlockId : IEquatable<BlockId>
    {
        public readonly char Letter;

        public BlockId(char letter)
        {
            Letter = letter;
        }

        public static bool operator ==(BlockId a, BlockId b) => a.Equals(b);
        public static bool operator !=(BlockId a, BlockId b) => !a.Equals(b);

        public bool Equals(BlockId other) => Letter == other.Letter;
        public override bool Equals(object obj) => obj is BlockId other && Equals(other);
        public override int GetHashCode() => Letter.GetHashCode();
        public override string ToString() => $"Block '{Letter}'";
    }
}
