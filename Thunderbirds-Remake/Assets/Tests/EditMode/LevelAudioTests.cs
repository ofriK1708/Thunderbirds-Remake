using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Which sound plays when. A fake records the calls, so no audio device is involved.</summary>
    public class LevelAudioTests
    {
        private sealed class FakeAudio : IGameAudio
        {
            public readonly List<GameSound> Played = new List<GameSound>();
            public readonly Dictionary<ShipId, float> Engines = new Dictionary<ShipId, float>();
            public bool Alarm;
            public void Play(GameSound sound) => Played.Add(sound);
            public void SetEngineLevel(ShipId ship, float level) => Engines[ship] = level;
            public void SetOverloadAlarm(bool on) => Alarm = on;
            public int Count(GameSound sound) => Played.Count(s => s == sound);
        }

        private const float Idle = 0.25f;

        private FakeAudio _fake;
        private EventHub _events;
        private SimulationState _state;
        private LevelAudio _audio;

        [SetUp]
        public void SetUp()
        {
            _fake = new FakeAudio();
            _events = new EventHub();
            _state = TestStates.SmallRoom(); // Kestrel active at (1,1), dock (7,1); Atlas at (3,1), dock (5,2)
            _audio = new LevelAudio(_fake, () => _state, _events, Idle);
        }

        [TearDown]
        public void TearDown() => _audio.Dispose();

        private void Raise(SimEvent e)
        {
            _events.Raise(e);
            _events.Flush();
        }

        [Test]
        public void Engines_IdleWhileHovering_FullWhileFlying_ThenSettleAgain()
        {
            _audio.Update(0.02f, paused: false);
            Assert.AreEqual(Idle, _fake.Engines[ShipId.Kestrel]);
            Assert.AreEqual(Idle, _fake.Engines[ShipId.Atlas]);

            Raise(new ShipMoved(ShipId.Kestrel, new GridPos(1, 1), new GridPos(2, 1), 0.08f));
            _audio.Update(0.02f, paused: false);
            Assert.AreEqual(1f, _fake.Engines[ShipId.Kestrel], "flying");
            Assert.AreEqual(Idle, _fake.Engines[ShipId.Atlas], "the other ship keeps hovering");

            _audio.Update(0.2f, paused: false); // well past the end of the step
            Assert.AreEqual(Idle, _fake.Engines[ShipId.Kestrel]);
        }

        [Test]
        public void BackToBackSteps_KeepTheEngineAtFullLevel()
        {
            for (var i = 0; i < 5; i++)
            {
                Raise(new ShipMoved(ShipId.Kestrel, new GridPos(1, 1), new GridPos(2, 1), 0.08f));
                _audio.Update(0.04f, paused: false);
                Assert.AreEqual(1f, _fake.Engines[ShipId.Kestrel]);
                _audio.Update(0.04f, paused: false);
                Assert.AreEqual(1f, _fake.Engines[ShipId.Kestrel], "no dip between two chained steps");
            }
        }

        [Test]
        public void Paused_OrAGhost_HasNoEngineSound()
        {
            _audio.Update(0.02f, paused: true);
            Assert.AreEqual(0f, _fake.Engines[ShipId.Kestrel]);

            _state.GetShip(ShipId.Atlas).IsGhost = true;
            _audio.Update(0.02f, paused: false);
            Assert.AreEqual(0f, _fake.Engines[ShipId.Atlas]);
            Assert.AreEqual(Idle, _fake.Engines[ShipId.Kestrel]);
        }

        [Test]
        public void EachEvent_PlaysItsOwnSound()
        {
            Raise(new MoveRefused(ShipId.Kestrel, Direction.Right, RefuseReason.Blocked, new BlockId[0], ColourClass.Light));
            Raise(new ActiveShipChanged(ShipId.Atlas, automatic: false));
            Raise(new ShipCrushed(ShipId.Kestrel, 2));
            Raise(new LevelComplete());
            Raise(new LevelFailed(FailReason.Crushed));

            CollectionAssert.AreEqual(new[]
            {
                GameSound.Bump, GameSound.SwitchShip, GameSound.Crush, GameSound.LevelComplete, GameSound.GameOver
            }, _fake.Played);
        }

        [Test]
        public void Docking_ChimesOnce_WhenAShipArrives_AndAgainOnlyAfterItLeft()
        {
            var kestrel = _state.GetShip(ShipId.Kestrel);
            _audio.Update(0.02f, false);
            Assert.AreEqual(0, _fake.Count(GameSound.Dock));

            kestrel.Position = kestrel.Dock;
            _audio.Update(0.02f, false);
            _audio.Update(0.02f, false);
            Assert.AreEqual(1, _fake.Count(GameSound.Dock), "once, not every frame it sits there");

            kestrel.Position = new GridPos(1, 1);
            _audio.Update(0.02f, false);
            kestrel.Position = kestrel.Dock;
            _audio.Update(0.02f, false);
            Assert.AreEqual(2, _fake.Count(GameSound.Dock));
        }

        [Test]
        public void AShipThatStartsOnItsDock_DoesNotChime_AndNeitherDoesRestart()
        {
            var kestrel = _state.GetShip(ShipId.Kestrel);
            kestrel.Position = kestrel.Dock;
            _audio.Reset(); // level start / Restart
            _audio.Update(0.02f, false);
            Assert.AreEqual(0, _fake.Count(GameSound.Dock));
        }

        [Test]
        public void OverloadAlarm_RunsWhileAnyShipIsStressed_NotWhilePaused_NotForAGhost()
        {
            var atlas = _state.GetShip(ShipId.Atlas);
            _audio.Update(0.02f, false);
            Assert.IsFalse(_fake.Alarm);

            atlas.IsStressed = true;
            _audio.Update(0.02f, false);
            Assert.IsTrue(_fake.Alarm);

            _audio.Update(0.02f, paused: true);
            Assert.IsFalse(_fake.Alarm, "silent in the pause menu");

            _audio.Update(0.02f, false);
            Assert.IsTrue(_fake.Alarm);
            atlas.IsStressed = false;
            _audio.Update(0.02f, false);
            Assert.IsFalse(_fake.Alarm, "relieved");
        }

        [Test]
        public void AfterTheLevelEnds_EnginesAndAlarmStop()
        {
            _state.GetShip(ShipId.Atlas).IsStressed = true;
            _state.Status = SimStatus.Failed;
            _audio.Update(0.02f, false);
            Assert.AreEqual(0f, _fake.Engines[ShipId.Kestrel]);
            Assert.IsFalse(_fake.Alarm);
        }

        [Test]
        public void Dispose_SilencesEverything_AndStopsListening()
        {
            _state.GetShip(ShipId.Atlas).IsStressed = true;
            _audio.Update(0.02f, false);
            _audio.Dispose();

            Assert.AreEqual(0f, _fake.Engines[ShipId.Kestrel]);
            Assert.AreEqual(0f, _fake.Engines[ShipId.Atlas]);
            Assert.IsFalse(_fake.Alarm);
            Raise(new LevelComplete());
            Assert.IsEmpty(_fake.Played, "leaving a level must not leave a listener behind");
        }

        [Test]
        public void RealAudioConfig_IsInResources_WithEveryClipAssigned()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>("Assets/Resources/AudioConfig.asset");
            Assert.IsNotNull(config, "AudioManager loads it from Resources");
            Assert.AreSame(config, Resources.Load<AudioConfig>(AudioConfig.ResourcePath));
            foreach (var (clip, name) in new[]
                     {
                         (config.music, "music"), (config.kestrelEngine, "kestrelEngine"), (config.atlasEngine, "atlasEngine"),
                         (config.bump, "bump"), (config.dock, "dock"), (config.switchShip, "switchShip"),
                         (config.overload, "overload"), (config.crush, "crush"),
                         (config.levelComplete, "levelComplete"), (config.gameOver, "gameOver")
                     })
                Assert.IsNotNull(clip, name);
        }
    }
}
