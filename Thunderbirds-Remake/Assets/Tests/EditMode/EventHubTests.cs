using System.Collections.Generic;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class EventHubTests
    {
        private static readonly BlockId B = new BlockId('b');

        /// <summary>One sample of every contract event, in the GDD's order.</summary>
        private static SimEvent[] OneOfEach() => new SimEvent[]
        {
            new ShipMoved(ShipId.Atlas, new GridPos(1, 1), new GridPos(2, 1), 0.16f),
            new MoveRefused(ShipId.Kestrel, Direction.Right, RefuseReason.TooHeavy, new[] { B }, ColourClass.Yellow),
            new BlockMoved(B, new GridPos(3, 1), new GridPos(4, 1), 0.16f),
            new BlockFell(B, new GridPos(4, 2), new GridPos(4, 1), 0.1f),
            new BlockLanded(B),
            new BlockReleased(B, ShipId.Kestrel),
            new ShipStressed(ShipId.Atlas, 10, 3f),
            new ShipRelieved(ShipId.Atlas),
            new ShipCrushed(ShipId.Atlas, 2),
            new ShipRespawned(ShipId.Atlas, new GridPos(1, 1), true),
            new ActiveShipChanged(ShipId.Kestrel, true),
            new OxygenChanged(89f, 90f),
            new LevelComplete(),
            new LevelFailed(FailReason.OutOfOxygen),
        };

        private static List<SimEvent> SubscribeToAll(ISimulationEvents events)
        {
            var heard = new List<SimEvent>();
            events.ShipMoved += heard.Add;
            events.MoveRefused += heard.Add;
            events.BlockMoved += heard.Add;
            events.BlockFell += heard.Add;
            events.BlockLanded += heard.Add;
            events.BlockReleased += heard.Add;
            events.ShipStressed += heard.Add;
            events.ShipRelieved += heard.Add;
            events.ShipCrushed += heard.Add;
            events.ShipRespawned += heard.Add;
            events.ActiveShipChanged += heard.Add;
            events.OxygenChanged += heard.Add;
            events.LevelComplete += heard.Add;
            events.LevelFailed += heard.Add;
            return heard;
        }

        [Test]
        public void Contract_HasExactly14Events()
        {
            Assert.AreEqual(14, OneOfEach().Length);
        }

        [Test]
        public void Raise_LogsImmediately_ButNotifiesOnlyOnFlush()
        {
            var hub = new EventHub();
            var heard = SubscribeToAll(hub);
            var e = new LevelComplete();

            hub.Raise(e);

            CollectionAssert.AreEqual(new[] { e }, hub.Log);
            Assert.IsEmpty(heard, "listeners must not run mid-tick");

            hub.Flush();

            CollectionAssert.AreEqual(new[] { e }, heard);
        }

        [Test]
        public void Flush_DeliversEveryEventTypeToItsTypedEvent_InRaiseOrder()
        {
            var hub = new EventHub();
            var heard = SubscribeToAll(hub);
            var all = OneOfEach();

            foreach (var e in all) hub.Raise(e);
            hub.Flush();

            CollectionAssert.AreEqual(all, heard);
            CollectionAssert.AreEqual(all, hub.Log);
        }

        [Test]
        public void Flush_Twice_DoesNotNotifyTwice()
        {
            var hub = new EventHub();
            var heard = SubscribeToAll(hub);

            hub.Raise(new LevelComplete());
            hub.Flush();
            hub.Flush();

            Assert.AreEqual(1, heard.Count);
        }

        [Test]
        public void Clear_EmptiesLogAndDropsPending_ButKeepsSubscribers()
        {
            var hub = new EventHub();
            var heard = SubscribeToAll(hub);

            hub.Raise(new LevelComplete());
            hub.Clear();
            hub.Flush();

            Assert.IsEmpty(hub.Log);
            Assert.IsEmpty(heard);

            hub.Raise(new LevelComplete());
            hub.Flush();
            Assert.AreEqual(1, heard.Count);
        }
    }
}
