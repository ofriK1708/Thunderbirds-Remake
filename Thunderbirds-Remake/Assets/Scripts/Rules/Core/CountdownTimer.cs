using System;

namespace Thunderbirds.Rules
{
    /// <summary>Explicitly started, reusable timer. Only Tick advances time; expiry fires once per start.</summary>
    public sealed class CountdownTimer
    {
        private double _remaining;
        public float Remaining => (float)_remaining;
        public bool IsRunning { get; private set; }
        public bool HasExpired { get; private set; }
        public event Action Expired;

        public void Start(float seconds)
        {
            Validate(seconds, nameof(seconds));
            _remaining = seconds;
            HasExpired = false;
            IsRunning = true;
        }

        public void Tick(float dt)
        {
            Validate(dt, nameof(dt));
            if (!IsRunning) return;
            _remaining = Math.Max(0, _remaining - dt);
            if (_remaining > 0) return;
            IsRunning = false;
            HasExpired = true;
            Expired?.Invoke();
        }

        public void Reset()
        {
            _remaining = 0;
            IsRunning = false;
            HasExpired = false;
        }

        private static void Validate(float value, string name)
        {
            if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Time must be finite and non-negative.");
        }
    }
}
