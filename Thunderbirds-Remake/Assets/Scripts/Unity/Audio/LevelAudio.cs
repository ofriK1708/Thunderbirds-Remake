using System;
using System.Collections.Generic;
using Thunderbirds.Rules;

namespace Thunderbirds.Unity
{
    /// <summary>The one-shot sounds of a level.</summary>
    public enum GameSound { Bump, Dock, SwitchShip, Crush, LevelComplete, GameOver }

    /// <summary>What <see cref="LevelAudio"/> needs from the audio system. AudioManager implements it; tests use a fake.</summary>
    public interface IGameAudio
    {
        void Play(GameSound sound);

        /// <summary>0 = silent, 1 = flying. The implementation fades between levels.</summary>
        void SetEngineLevel(ShipId ship, float level);

        void SetOverloadAlarm(bool on);
    }

    /// <summary>
    /// Turns what happens in the simulation into sound (GDD §6 Audio). Like every view it only listens to
    /// events and reads state; it decides nothing. Plain C#, so it is tested without an audio device.
    /// </summary>
    public sealed class LevelAudio : IDisposable
    {
        private readonly IGameAudio _audio;
        private readonly Func<IReadOnlySimulationState> _state;
        private readonly ISimulationEvents _events;
        private readonly float _idleEngineLevel;

        private readonly Dictionary<ShipId, float> _flyingSecondsLeft = new Dictionary<ShipId, float>();
        private readonly HashSet<ShipId> _docked = new HashSet<ShipId>();

        /// <param name="state">A function, because Restart replaces the state object.</param>
        public LevelAudio(IGameAudio audio, Func<IReadOnlySimulationState> state, ISimulationEvents events,
            float idleEngineLevel = 0.25f)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _idleEngineLevel = idleEngineLevel;

            _events.ShipMoved += OnShipMoved;
            _events.MoveRefused += OnMoveRefused;
            _events.ActiveShipChanged += OnActiveShipChanged;
            _events.ShipCrushed += OnShipCrushed;
            _events.LevelComplete += OnLevelComplete;
            _events.LevelFailed += OnLevelFailed;
            Reset();
        }

        /// <summary>A level start or Restart: forget timers, and don't chime for a ship that starts on its dock.</summary>
        public void Reset()
        {
            _flyingSecondsLeft.Clear();
            _docked.Clear();
            foreach (var ship in _state().Ships)
                if (IsDocked(ship)) _docked.Add(ship.Id);
            _audio.SetOverloadAlarm(false);
        }

        /// <summary>Call once per frame, after the simulation ticked.</summary>
        public void Update(float dt, bool paused)
        {
            var state = _state();
            var playing = !paused && state.Status == SimStatus.Playing;
            var anyStressed = false;

            foreach (var ship in state.Ships)
            {
                _flyingSecondsLeft.TryGetValue(ship.Id, out var flying);
                flying = Math.Max(0f, flying - dt);
                _flyingSecondsLeft[ship.Id] = flying;

                // A ghost has no engine; a hovering ship idles; a flying one is at full level.
                var level = !playing || ship.IsGhost ? 0f : flying > 0f ? 1f : _idleEngineLevel;
                _audio.SetEngineLevel(ship.Id, level);

                anyStressed |= ship.IsStressed && !ship.IsGhost;

                var docked = IsDocked(ship);
                if (docked && _docked.Add(ship.Id) && state.Status != SimStatus.Failed) _audio.Play(GameSound.Dock);
                else if (!docked) _docked.Remove(ship.Id);
            }

            _audio.SetOverloadAlarm(playing && anyStressed);
        }

        private static bool IsDocked(ShipState ship) => !ship.IsGhost && ship.Position == ship.Dock;

        private void OnShipMoved(ShipMoved e)
        {
            // Stay at flying level a little past the end of the step, so back-to-back steps sound continuous.
            _flyingSecondsLeft[e.Ship] = e.StepSeconds * 1.5f;
        }

        private void OnMoveRefused(MoveRefused e) => _audio.Play(GameSound.Bump);
        private void OnActiveShipChanged(ActiveShipChanged e) => _audio.Play(GameSound.SwitchShip);
        private void OnShipCrushed(ShipCrushed e) => _audio.Play(GameSound.Crush);
        private void OnLevelComplete(LevelComplete e) => _audio.Play(GameSound.LevelComplete);
        private void OnLevelFailed(LevelFailed e) => _audio.Play(GameSound.GameOver);

        public void Dispose()
        {
            _events.ShipMoved -= OnShipMoved;
            _events.MoveRefused -= OnMoveRefused;
            _events.ActiveShipChanged -= OnActiveShipChanged;
            _events.ShipCrushed -= OnShipCrushed;
            _events.LevelComplete -= OnLevelComplete;
            _events.LevelFailed -= OnLevelFailed;
            foreach (ShipId ship in Enum.GetValues(typeof(ShipId))) _audio.SetEngineLevel(ship, 0f);
            _audio.SetOverloadAlarm(false);
        }
    }
}
