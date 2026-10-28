using System;
using NUnit.Framework;
using Apogee.Core;

namespace Apogee.Tests
{
    [TestFixture]
    public class SeededRngTests
    {
        [Test]
        public void Next_SameSeed_ProducesIdenticalSequence()
        {
            var rng1 = new SeededRng(123456789UL);
            var rng2 = new SeededRng(123456789UL);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(rng1.NextUInt64(), rng2.NextUInt64(), $"Mismatch at step {i}");
            }
        }

        [Test]
        public void Next_DifferentSeed_ProducesDifferentSequence()
        {
            var rng1 = new SeededRng(11111UL);
            var rng2 = new SeededRng(22222UL);

            int matches = 0;
            for (int i = 0; i < 100; i++)
            {
                if (rng1.NextUInt64() == rng2.NextUInt64())
                {
                    matches++;
                }
            }

            Assert.Less(matches, 3, "Different seeds should not produce matching values.");
        }

        [Test]
        public void NextInt_WithinRange_StaysWithinBounds()
        {
            var rng = new SeededRng(42UL);

            for (int i = 0; i < 1000; i++)
            {
                int val = rng.NextInt(10, 50);
                Assert.GreaterOrEqual(val, 10);
                Assert.Less(val, 50);
            }
        }

        [Test]
        public void NextInt_SingleValueRange_ReturnsMin()
        {
            var rng = new SeededRng(42UL);
            int val = rng.NextInt(7, 7);
            Assert.AreEqual(7, val);
        }

        [Test]
        public void NextInt_InvalidRange_ThrowsArgumentException()
        {
            var rng = new SeededRng(42UL);
            Assert.Throws<ArgumentException>(() => rng.NextInt(50, 10));
        }

        [Test]
        public void NextInt_ZeroOrNegativeMax_ThrowsArgumentOutOfRangeException()
        {
            var rng = new SeededRng(42UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-5));
        }

        [Test]
        public void NextFloat_ProducesValuesInRangeZeroToOne()
        {
            var rng = new SeededRng(99UL);

            for (int i = 0; i < 1000; i++)
            {
                float val = rng.NextFloat();
                Assert.GreaterOrEqual(val, 0.0f);
                Assert.Less(val, 1.0f);
            }
        }

        [Test]
        public void NextDouble_ProducesValuesInRangeZeroToOne()
        {
            var rng = new SeededRng(101UL);

            for (int i = 0; i < 1000; i++)
            {
                double val = rng.NextDouble();
                Assert.GreaterOrEqual(val, 0.0);
                Assert.Less(val, 1.0);
            }
        }

        [Test]
        public void NextBool_ProbabilityBoundaries_ReturnExpectedValues()
        {
            var rng = new SeededRng(77UL);

            for (int i = 0; i < 50; i++)
            {
                Assert.IsFalse(rng.NextBool(0.0f));
                Assert.IsTrue(rng.NextBool(1.0f));
            }
        }

        [Test]
        public void State_SaveAndRestore_ContinuesSequenceAccurately()
        {
            var rng = new SeededRng(54321UL);

            // Advance 50 steps
            for (int i = 0; i < 50; i++)
            {
                rng.NextUInt64();
            }

            // Save state
            RngState snapshot = rng.GetState();

            // Read next 50 values
            ulong[] expected = new ulong[50];
            for (int i = 0; i < 50; i++)
            {
                expected[i] = rng.NextUInt64();
            }

            // Restore state
            rng.SetState(snapshot);

            // Verify identical continuation
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(expected[i], rng.NextUInt64(), $"Mismatch after state restore at index {i}");
            }
        }

        [Test]
        public void Fork_ProducesDeterministicIndependentChild()
        {
            var parent1 = new SeededRng(8888UL);
            var parent2 = new SeededRng(8888UL);

            SeededRng child1 = parent1.Fork();
            SeededRng child2 = parent2.Fork();

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(child1.NextUInt64(), child2.NextUInt64());
            }
        }
    }
}
