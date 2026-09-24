using System.Collections.Generic;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>What the skeleton already guarantees; rule issues add their own test files.</summary>
    public class SimulationSkeletonTests
    {
        private static Simulation NewSim() => new Simulation(TestStates.SmallRoom, new SimulationConfig());

        [Test]
        public void Config_StepSeconds_KestrelIsFasterByRatio()
        {
            var config = new SimulationConfig();

            Assert.AreEqual(0.16f, config.StepSeconds(ShipId.Atlas), 1e-6f);
            Assert.AreEqual(0.08f, config.StepSeconds(ShipId.Kestrel), 1e-6f);
        }

        [Test]
        public void Tick_WithNoInput_RaisesNothing()
        {
            var sim = NewSim();

            sim.Tick(0.016f);

            Assert.IsEmpty(sim.Events.Log);
        }

        [Test]
        public void SwitchShip_TogglesActiveShip_AndNotifies()
        {
            var sim = NewSim();
            var heard = new List<ActiveShipChanged>();
            sim.Events.ActiveShipChanged += heard.Add;

            sim.SwitchShip();

            Assert.AreEqual(ShipId.Atlas, sim.State.ActiveShip);
            Assert.AreEqual(1, heard.Count);
        }

        [Test]
        public void SwitchShip_ToAGhost_IsIgnored()
        {
            var sim = new Simulation(() =>
            {
                var state = TestStates.SmallRoom();
                state.GetShip(ShipId.Atlas).IsGhost = true;
                return state;
            }, new SimulationConfig());

            sim.SwitchShip();

            Assert.AreEqual(ShipId.Kestrel, sim.State.ActiveShip);
            Assert.IsEmpty(sim.Events.Log);
        }

        [Test]
        public void SetHeldDirection_TapReleasedBeforeStep_IsStillLatched()
        {
            var sim = NewSim();

            sim.SetHeldDirection(Direction.Right);
            sim.SetHeldDirection(null);

            Assert.AreEqual(Direction.Right, sim.NextStepDirection);
        }

        [Test]
        public void SetHeldDirection_HeldDirectionWinsOverOlderTap()
        {
            var sim = NewSim();

            sim.SetHeldDirection(Direction.Right);
            sim.SetHeldDirection(Direction.Up);

            Assert.AreEqual(Direction.Up, sim.NextStepDirection);
        }

        [Test]
        public void Restart_ReloadsConfig_SoInspectorEditsApply()
        {
            var tuned = new SimulationConfig();
            var sim = new Simulation(TestStates.SmallRoom, () => new SimulationConfig
            {
                AtlasMoveStepSeconds = tuned.AtlasMoveStepSeconds
            });
            Assert.AreEqual(0.16f, sim.Config.AtlasMoveStepSeconds, 1e-6f);

            tuned.AtlasMoveStepSeconds = 0.2f; // "edited in the Inspector" mid-level
            Assert.AreEqual(0.16f, sim.Config.AtlasMoveStepSeconds, 1e-6f, "no change until Restart");

            sim.Restart();

            Assert.AreEqual(0.2f, sim.Config.AtlasMoveStepSeconds, 1e-6f);
        }

        [Test]
        public void Restart_WithInvalidConfig_Throws()
        {
            var step = 0.16f;
            var sim = new Simulation(TestStates.SmallRoom, () => new SimulationConfig { AtlasMoveStepSeconds = step });

            step = 0f;

            Assert.Throws<System.ArgumentException>(() => sim.Restart());
        }

        [Test]
        public void Restart_RebuildsState_AndClearsLog()
        {
            var sim = NewSim();
            sim.SwitchShip();

            sim.Restart();

            Assert.AreEqual(ShipId.Kestrel, sim.State.ActiveShip);
            Assert.IsEmpty(sim.Events.Log);
        }
    }
}
