using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// A timed list of events for <see cref="FakeSimulation"/> to replay:
    /// <code>new FakeScript().At(0.5f, new ShipMoved(...)).At(2f, new ShipStressed(...));</code>
    /// </summary>
    public sealed class FakeScript
    {
        public readonly struct Step
        {
            public readonly float Time;
            public readonly SimEvent Event;

            public Step(float time, SimEvent e)
            {
                Time = time;
                Event = e;
            }
        }

        private readonly List<Step> _steps = new List<Step>();

        /// <summary>Steps sorted by time; steps with equal time keep the order they were added.</summary>
        public IReadOnlyList<Step> Steps => _steps;

        public FakeScript At(float seconds, SimEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds), "Time must be >= 0");

            // Insert after every step with time <= seconds, so equal times keep insertion order.
            var index = _steps.Count;
            while (index > 0 && _steps[index - 1].Time > seconds) index--;
            _steps.Insert(index, new Step(seconds, e));
            return this;
        }
    }
}
