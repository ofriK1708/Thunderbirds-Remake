using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>A level text that can't be played. <see cref="Errors"/> lists every problem found, not just the first.</summary>
    public sealed class LevelParseException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public LevelParseException(IReadOnlyList<string> errors)
            : base("Invalid level:\n  " + string.Join("\n  ", errors))
        {
            Errors = errors;
        }
    }
}
