using NUnit.Framework;
using Apogee.Core;

namespace Apogee.Tests
{
    [TestFixture]
    public class RngManagerTests
    {
        [Test]
        public void Stream_Independence_AdvancingOneStreamDoesNotAffectOtherStream()
        {
            var mgr1 = new RngManager(1001UL);
            var mgr2 = new RngManager(1001UL);

            // In mgr1, advance Events stream heavily
            IRandom eventsStream1 = mgr1.GetStream(RngStreamType.Events);
            for (int i = 0; i < 500; i++)
            {
                eventsStream1.NextInt();
            }

            // In mgr2, do not advance Events stream
            // Compare Launch stream from both managers: they MUST be identical
            IRandom launch1 = mgr1.GetStream(RngStreamType.Launch);
            IRandom launch2 = mgr2.GetStream(RngStreamType.Launch);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(launch1.NextUInt64(), launch2.NextUInt64(),
                    $"Launch stream diverged at step {i} due to Events stream consumption!");
            }

            // Compare Edl stream from both managers: they MUST be identical as well
            IRandom edl1 = mgr1.GetStream(RngStreamType.Edl);
            IRandom edl2 = mgr2.GetStream(RngStreamType.Edl);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(edl1.NextUInt64(), edl2.NextUInt64(),
                    $"EDL stream diverged at step {i} due to Events stream consumption!");
            }
        }

        [Test]
        public void Stream_DifferentStreamsWithinSameManager_ProduceDifferentSequences()
        {
            var mgr = new RngManager(424242UL);

            IRandom events = mgr.GetStream(RngStreamType.Events);
            IRandom launch = mgr.GetStream(RngStreamType.Launch);
            IRandom edl = mgr.GetStream(RngStreamType.Edl);

            int matches = 0;
            for (int i = 0; i < 50; i++)
            {
                ulong evVal = events.NextUInt64();
                ulong launchVal = launch.NextUInt64();
                ulong edlVal = edl.NextUInt64();

                if (evVal == launchVal || evVal == edlVal || launchVal == edlVal)
                {
                    matches++;
                }
            }

            Assert.Less(matches, 2, "Different streams within same manager produced correlated values.");
        }

        [Test]
        public void Stream_DeterministicForking_SameMasterSeedProducesSameStreams()
        {
            var mgr1 = new RngManager(777UL);
            var mgr2 = new RngManager(777UL);

            IRandom b1 = mgr1.GetStream(RngStreamType.Bot);
            IRandom b2 = mgr2.GetStream(RngStreamType.Bot);

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(b1.NextUInt64(), b2.NextUInt64());
            }
        }

        [Test]
        public void SaveAndRestore_AllStreams_RestoresStateExact()
        {
            var mgr = new RngManager(99999UL);

            // Advance each stream differently
            IRandom ev = mgr.GetStream(RngStreamType.Events);
            IRandom la = mgr.GetStream(RngStreamType.Launch);
            IRandom ed = mgr.GetStream(RngStreamType.Edl);

            for (int i = 0; i < 30; i++) ev.NextInt();
            for (int i = 0; i < 60; i++) la.NextInt();
            for (int i = 0; i < 90; i++) ed.NextInt();

            // Save state
            RngManagerState state = mgr.GetState();

            // Record next outputs
            ulong[] expectedEv = new ulong[20];
            ulong[] expectedLa = new ulong[20];
            ulong[] expectedEd = new ulong[20];

            for (int i = 0; i < 20; i++) expectedEv[i] = ev.NextUInt64();
            for (int i = 0; i < 20; i++) expectedLa[i] = la.NextUInt64();
            for (int i = 0; i < 20; i++) expectedEd[i] = ed.NextUInt64();

            // Create new manager and restore state
            var restoredMgr = new RngManager(state);
            IRandom restoredEv = restoredMgr.GetStream(RngStreamType.Events);
            IRandom restoredLa = restoredMgr.GetStream(RngStreamType.Launch);
            IRandom restoredEd = restoredMgr.GetStream(RngStreamType.Edl);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(expectedEv[i], restoredEv.NextUInt64(), $"Events mismatch at {i}");
                Assert.AreEqual(expectedLa[i], restoredLa.NextUInt64(), $"Launch mismatch at {i}");
                Assert.AreEqual(expectedEd[i], restoredEd.NextUInt64(), $"Edl mismatch at {i}");
            }
        }

        [Test]
        public void CreateDerivedStream_SameKey_ProducesIdenticalStream()
        {
            var mgr1 = new RngManager(555UL);
            var mgr2 = new RngManager(555UL);

            SeededRng s1 = mgr1.CreateDerivedStream("mission_mars_01");
            SeededRng s2 = mgr2.CreateDerivedStream("mission_mars_01");

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(s1.NextUInt64(), s2.NextUInt64());
            }
        }
    }
}
