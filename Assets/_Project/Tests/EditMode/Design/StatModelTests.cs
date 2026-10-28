using NUnit.Framework;
using Apogee.Design;

namespace Apogee.Tests
{
    [TestFixture]
    public class StatModelTests
    {
        [Test]
        public void Stat_BaseValueOnly_ReturnsBaseValue()
        {
            var stat = new Stat(StatType.DryMass, baseValue: 10.0);
            Assert.AreEqual(10.0, stat.Value, 1e-6);
        }

        [Test]
        public void Stat_FlatModifiers_AddDirectlyToBase()
        {
            var stat = new Stat(StatType.DryMass, baseValue: 10.0);
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 2.5, sourceName: "Fairing"));
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, -1.0, sourceName: "Lightweight Adapter"));

            Assert.AreEqual(11.5, stat.Value, 1e-6);
        }

        [Test]
        public void Stat_PercentAddModifiers_SumAdditivelyBeforeMultiplying()
        {
            // Base = 100, Mod1 = +10% (+0.10), Mod2 = +20% (+0.20)
            // Expected = 100 * (1 + 0.10 + 0.20) = 130
            var stat = new Stat(StatType.SpecificImpulse, baseValue: 100.0);
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.SpecificImpulse, 0.10, sourceName: "Tech A"));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.SpecificImpulse, 0.20, sourceName: "Tech B"));

            Assert.AreEqual(130.0, stat.Value, 1e-6);
        }

        [Test]
        public void Stat_PercentMultModifiers_CompoundMultiplicatively()
        {
            // Base = 100, Mod1 = -20% (-0.20), Mod2 = -10% (-0.10)
            // Expected = 100 * (1 - 0.20) * (1 - 0.10) = 100 * 0.8 * 0.9 = 72
            var stat = new Stat(StatType.Reliability, baseValue: 1.0);
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.Reliability, -0.20, sourceName: "Radiation"));
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.Reliability, -0.10, sourceName: "Dust Storm"));

            Assert.AreEqual(0.72, stat.Value, 1e-6);
        }

        [Test]
        public void Stat_FullPipeline_CalculatesInExactEvaluationOrder()
        {
            // Calculation Pipeline: (Base + Flat) * (1 + PercentAdd) * (1 + PercentMult)
            // Base = 10
            // Flat = +2 -> basePlusFlat = 12
            // PercentAdd = +0.50 (+50%) -> 12 * 1.50 = 18
            // PercentMult = -0.10 (-10%) -> 18 * 0.90 = 16.2
            var stat = new Stat(StatType.DryMass, baseValue: 10.0);
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.DryMass, -0.10, sourceName: "Optimization"));
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 2.0, sourceName: "Reinforced Structure"));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.DryMass, 0.50, sourceName: "Heavy Avionics"));

            Assert.AreEqual(16.2, stat.Value, 1e-6);
        }

        [Test]
        public void Stat_Clamping_ConstrainsToMinAndMax()
        {
            var stat = new Stat(StatType.Reliability, baseValue: 0.95, minValue: 0.0, maxValue: 1.0);
            stat.AddModifier(StatModifier.CreateFlat(StatType.Reliability, 0.20));

            Assert.AreEqual(1.0, stat.Value, 1e-6);

            stat.ClearModifiers();
            stat.AddModifier(StatModifier.CreateFlat(StatType.Reliability, -1.5));
            Assert.AreEqual(0.0, stat.Value, 1e-6);
        }

        [Test]
        public void Stat_RemoveModifier_RemovesEffectAndRecalculates()
        {
            var stat = new Stat(StatType.Cost, baseValue: 50.0);
            var mod = StatModifier.CreateFlat(StatType.Cost, 15.0, sourceName: "Overpriced Sensor");
            stat.AddModifier(mod);

            Assert.AreEqual(65.0, stat.Value, 1e-6);

            bool removed = stat.RemoveModifier(mod.Id);
            Assert.IsTrue(removed);
            Assert.AreEqual(50.0, stat.Value, 1e-6);
        }

        [Test]
        public void Stat_RemoveModifiersFromSource_RemovesAllMatchingSources()
        {
            var stat = new Stat(StatType.DryMass, baseValue: 20.0);
            string supplier = "Helion Dynamics";

            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 3.0, source: supplier));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.DryMass, 0.05, source: supplier));
            stat.AddModifier(StatModifier.CreateFlat(StatType.DryMass, 1.0, source: "Nordvik"));

            int removedCount = stat.RemoveModifiersFromSource(supplier);
            Assert.AreEqual(2, removedCount);
            Assert.AreEqual(21.0, stat.Value, 1e-6);
        }

        [Test]
        public void StatBreakdown_AccuratelyTracksNetContributions()
        {
            // Base = 100
            // Flat = +20 -> basePlusFlat = 120 (Contribution: +20)
            // PercentAdd = +10% -> 120 * 0.10 = +12 (Contribution: +12, total: 132)
            // PercentMult = -10% -> 132 * -0.10 = -13.2 (Contribution: -13.2, total: 118.8)
            var stat = new Stat(StatType.Thrust, baseValue: 100.0);
            stat.AddModifier(StatModifier.CreateFlat(StatType.Thrust, 20.0, sourceName: "Nozzle Upgrade"));
            stat.AddModifier(StatModifier.CreatePercentAdd(StatType.Thrust, 0.10, sourceName: "Supplier Calibration"));
            stat.AddModifier(StatModifier.CreatePercentMult(StatType.Thrust, -0.10, sourceName: "Atmospheric Backpressure"));

            StatBreakdown breakdown = stat.GetBreakdown();

            Assert.AreEqual(100.0, breakdown.BaseValue, 1e-6);
            Assert.AreEqual(118.8, breakdown.FinalValue, 1e-6);
            Assert.AreEqual(3, breakdown.Entries.Count);

            // Check individual entries
            Assert.AreEqual("Nozzle Upgrade", breakdown.Entries[0].SourceName);
            Assert.AreEqual(20.0, breakdown.Entries[0].NetContribution, 1e-6);

            Assert.AreEqual("Supplier Calibration", breakdown.Entries[1].SourceName);
            Assert.AreEqual(12.0, breakdown.Entries[1].NetContribution, 1e-6);

            Assert.AreEqual("Atmospheric Backpressure", breakdown.Entries[2].SourceName);
            Assert.AreEqual(-13.2, breakdown.Entries[2].NetContribution, 1e-6);

            // Verify breakdown summary output string
            string summary = breakdown.ToFormattedSummary("kN");
            Assert.IsTrue(summary.Contains("Nozzle Upgrade"));
            Assert.IsTrue(summary.Contains("Supplier Calibration"));
            Assert.IsTrue(summary.Contains("118.8"));
        }

        [Test]
        public void StatCollection_GetOrCreateAndBatchModifiers_FunctionAccurately()
        {
            var collection = new StatCollection();
            collection.SetBaseValue(StatType.DryMass, 10.0);
            collection.SetBaseValue(StatType.Cost, 25.0);

            Assert.AreEqual(10.0, collection.GetValue(StatType.DryMass), 1e-6);
            Assert.AreEqual(25.0, collection.GetValue(StatType.Cost), 1e-6);

            var modifiers = new[]
            {
                StatModifier.CreateFlat(StatType.DryMass, 1.5, sourceName: "Armor"),
                StatModifier.CreatePercentAdd(StatType.Cost, 0.20, sourceName: "Contract Overhead")
            };

            collection.AddModifiers(modifiers);

            Assert.AreEqual(11.5, collection.GetValue(StatType.DryMass), 1e-6);
            Assert.AreEqual(30.0, collection.GetValue(StatType.Cost), 1e-6);

            // Check breakdown from collection
            StatBreakdown costBreakdown = collection.GetBreakdown(StatType.Cost);
            Assert.AreEqual(30.0, costBreakdown.FinalValue, 1e-6);
        }

        [Test]
        public void StatCollection_Events_FiresWhenStatModified()
        {
            var collection = new StatCollection();
            bool eventFired = false;
            StatType changedType = StatType.Custom;

            collection.OnStatChanged += (type, stat) =>
            {
                eventFired = true;
                changedType = type;
            };

            collection.SetBaseValue(StatType.PowerGeneration, 5.0);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(StatType.PowerGeneration, changedType);
        }
    }
}
