using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Tick step 2: step timing, the tap latch, and refusal feedback (move-flow.md Q5).</summary>
    public class SimulationMoveTests
    {
        private const float Frame = 0.01f;

        /// <summary>Long corridor; a teal block 'a' (5 cells, too heavy for Kestrel) sits 3 cells right of Kestrel.</summary>
        private static readonly string[] Corridor =
        {
            "##############################",
            "#AAAA...................2222.#",
            "#AAAA...................2222.#",
            "#............................#",
            "#KK...aaa................11..#",
            "#KK...aa.................11..#",
            "##############################",
        };

        private static Simulation NewSim(SimulationConfig config = null) =>
            new Simulation(() => MoveResolverTests.Level(Corridor), config ?? new SimulationConfig());

        private static void Run(Simulation sim, float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame) sim.Tick(Frame);
        }

        private static int Count<T>(Simulation sim) where T : SimEvent => sim.Events.Log.OfType<T>().Count();

        [Test]
        public void Hold_StepsAtTheShipsSpeed_FrameRateIndependent()
        {
            // Kestrel: 0.16 / 2 = 0.08 s per cell. First step on the first tick, then every 0.08 s.
            var sim = new Simulation(() => MoveResolverTests.Level(
                "##############################",
                "#AAAA...................2222.#",
                "#AAAA...................2222.#",
                "#............................#",
                "#KK......................11..#",
                "#KK......................11..#",
                "##############################"), new SimulationConfig());
            sim.SetHeldDirection(Direction.Right);

            Run(sim, 0.4f);

            Assert.AreEqual(5, Count<ShipMoved>(sim), "t = 0.01, 0.09, 0.17, 0.25, 0.33");
            Assert.AreEqual(0.08f, sim.Events.Log.OfType<ShipMoved>().First().StepSeconds, 1e-6f);
        }

        [Test]
        public void Tap_ShorterThanAStep_StillSteps()
        {
            var sim = NewSim();
            sim.SetHeldDirection(Direction.Right);
            sim.SetHeldDirection(null); // released before any tick

            Run(sim, 0.3f);

            Assert.AreEqual(1, Count<ShipMoved>(sim));
        }

        [Test]
        public void Idle_DoesNotBankSteps()
        {
            var sim = NewSim();
            Run(sim, 1f); // nothing held
            sim.SetHeldDirection(Direction.Right);

            sim.Tick(Frame);
            sim.Tick(Frame);

            Assert.AreEqual(1, Count<ShipMoved>(sim), "one step, not a burst of saved-up steps");
        }

        [Test]
        public void Push_RaisesShipMovedThenBlockMoved_WithTheSameStepSeconds()
        {
            var sim = new Simulation(() => MoveResolverTests.Level(
                "##############################",
                "#AAAA...................2222.#",
                "#AAAA...................2222.#",
                "#............................#",
                "#KKa.....................11..#",
                "#KKa.....................11..#",
                "##############################"), new SimulationConfig());
            sim.SetHeldDirection(Direction.Right);

            sim.Tick(Frame);

            Assert.IsInstanceOf<ShipMoved>(sim.Events.Log[0]);
            var moved = (BlockMoved)sim.Events.Log[1];
            Assert.AreEqual(new GridPos(3, 1), moved.From);
            Assert.AreEqual(new GridPos(4, 1), moved.To);
            Assert.AreEqual(0.08f, moved.StepSeconds, 1e-6f);
        }

        [Test]
        public void HoldingIntoARefusal_BumpsOnce()
        {
            var sim = NewSim();
            sim.SetHeldDirection(Direction.Right);

            Run(sim, 2f); // 3 free steps, then pushing into 'a' (weight 5 > 4)

            Assert.AreEqual(3, Count<ShipMoved>(sim));
            Assert.AreEqual(1, Count<MoveRefused>(sim));
            var refused = sim.Events.Log.OfType<MoveRefused>().Single();
            Assert.AreEqual(RefuseReason.TooHeavy, refused.Reason);
            Assert.AreEqual(ColourClass.Yellow, refused.ChainColour);
            CollectionAssert.AreEqual(new[] { new BlockId('a') }, refused.Chain);
        }

        [Test]
        public void RePress_BumpsAgain()
        {
            var sim = NewSim();
            sim.SetHeldDirection(Direction.Right);
            Run(sim, 1f);
            sim.SetHeldDirection(null);
            sim.Tick(Frame);

            sim.SetHeldDirection(Direction.Right);
            Run(sim, 0.2f);

            Assert.AreEqual(2, Count<MoveRefused>(sim));
        }

        [Test]
        public void KeepPushing_RaisesRefusalHintOnce_AfterTheConfiguredTime()
        {
            var config = new SimulationConfig { RefusalHintSeconds = 2f };
            var sim = NewSim(config);
            sim.SetHeldDirection(Direction.Right);

            Run(sim, 2.1f); // refused from about t = 0.25: not yet 2 s of pushing
            Assert.AreEqual(0, Count<RefusalHint>(sim));

            Run(sim, 3f);
            Assert.AreEqual(1, Count<RefusalHint>(sim), "once per hold, not every step");
            Assert.AreEqual(RefuseReason.TooHeavy, sim.Events.Log.OfType<RefusalHint>().Single().Reason);
        }

        [Test]
        public void ReleasingEarly_ResetsTheHintTimer()
        {
            var config = new SimulationConfig { RefusalHintSeconds = 2f };
            var sim = NewSim(config);
            sim.SetHeldDirection(Direction.Right);
            Run(sim, 1.5f);
            sim.SetHeldDirection(null);
            sim.Tick(Frame);

            sim.SetHeldDirection(Direction.Right);
            Run(sim, 1.5f);

            Assert.AreEqual(0, Count<RefusalHint>(sim));
        }

        [Test]
        public void StillHeld_WhenTheObstacleClears_TheShipMovesOn()
        {
            var sim = NewSim();
            sim.SetHeldDirection(Direction.Right);
            Run(sim, 1f);
            Assert.AreEqual(3, Count<ShipMoved>(sim));

            // stand-in for 'a' being taken away (e.g. pushed off by Atlas)
            ((SimulationState)sim.State).GetBlock(new BlockId('a')).Position = new GridPos(20, 3);
            Run(sim, 0.1f);

            Assert.AreEqual(4, Count<ShipMoved>(sim));
            Assert.AreEqual(1, Count<MoveRefused>(sim), "the silent retries raised nothing");
        }

        [Test]
        public void Restart_ForgetsTheStepTimer()
        {
            var sim = NewSim();
            sim.SetHeldDirection(Direction.Right);
            sim.Tick(Frame);

            sim.Restart();
            sim.SetHeldDirection(Direction.Right);
            sim.Tick(Frame);

            Assert.AreEqual(1, Count<ShipMoved>(sim), "moves on the first tick after Restart");
        }
    }
}
