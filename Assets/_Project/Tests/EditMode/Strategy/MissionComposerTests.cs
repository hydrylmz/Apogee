using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;

namespace Apogee.Tests.Strategy
{
    [TestFixture]
    public sealed class MissionComposerTests
    {
        private CatalogCollection<DestinationRecord> _destinations;
        private CatalogCollection<MissionTypeRecord> _missionTypes;
        private CatalogCollection<ObjectiveRecord> _objectives;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            string jsonDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            if (!Directory.Exists(jsonDir))
            {
                // Fallback for runner environments
                jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            }

            string destJson = File.ReadAllText(Path.Combine(jsonDir, "destinations.json"));
            _destinations = CatalogLoader.Load<DestinationRecord>(destJson);

            string typeJson = File.ReadAllText(Path.Combine(jsonDir, "mission_types.json"));
            _missionTypes = CatalogLoader.Load<MissionTypeRecord>(typeJson);

            string objJson = File.ReadAllText(Path.Combine(jsonDir, "objectives.json"));
            _objectives = CatalogLoader.Load<ObjectiveRecord>(objJson);
        }

        [Test]
        public void Compose_MarsCrewed_Vs_MarsRover_DistinctlyDifferentOutputs()
        {
            var mars = _destinations.GetById("mars");
            var roverType = _missionTypes.GetById("rover");
            var crewedType = _missionTypes.GetById("crewed");

            var roverObjective = _objectives.GetById("mars-rover-exploration");
            var crewedObjectives = new[] { _objectives.GetById("mars-atmosphere-sampling"), _objectives.GetById("mars-life-signatures") };

            var roverProfile = MissionComposer.Compose(mars, roverType, new[] { roverObjective }, missionName: "Mars Rover Mission");
            var crewedProfile = MissionComposer.Compose(mars, crewedType, crewedObjectives, missionName: "Mars Crewed Expedition");

            // Δv must be distinctly different: Mars Rover is one-way surface (13,600 m/s); Mars Crewed is round trip (19,200 m/s)
            Assert.AreEqual(13600.0, roverProfile.RequiredDeltaV, 50.0);
            Assert.AreEqual(19200.0, crewedProfile.RequiredDeltaV, 50.0);
            Assert.Greater(crewedProfile.RequiredDeltaV, roverProfile.RequiredDeltaV + 5000.0);

            // Life support requirements
            Assert.IsFalse(roverProfile.RequiresLifeSupport);
            Assert.IsTrue(crewedProfile.RequiresLifeSupport);

            // Both require EDL
            Assert.IsTrue(roverProfile.RequiresEdl);
            Assert.IsTrue(crewedProfile.RequiresEdl);

            // Mandatory module categories
            CollectionAssert.Contains(roverProfile.RequiredModuleCategories, "edl");
            CollectionAssert.Contains(roverProfile.RequiredModuleCategories, "instrument");
            CollectionAssert.DoesNotContain(roverProfile.RequiredModuleCategories, "cabin");

            CollectionAssert.Contains(crewedProfile.RequiredModuleCategories, "cabin");
            CollectionAssert.Contains(crewedProfile.RequiredModuleCategories, "life-support");
            CollectionAssert.Contains(crewedProfile.RequiredModuleCategories, "abort-system");
            CollectionAssert.Contains(crewedProfile.RequiredModuleCategories, "radiation-shielding");

            // Reliability thresholds: crewed requires >= 0.98; rover allows calibrated 0.50
            Assert.AreEqual(0.50, roverProfile.MinReliabilityThreshold, 0.01);
            Assert.AreEqual(0.98, crewedProfile.MinReliabilityThreshold, 0.01);

            // Launch pad level: crewed Mars requires Level 5 Super Heavy; rover requires Level 3 Heavy
            Assert.AreEqual(3, roverProfile.RequiredLaunchPadLevel);
            Assert.AreEqual(5, crewedProfile.RequiredLaunchPadLevel);

            // Duration: crewed is round trip transit (2x 180-270 = 360-540 days) + mission duration
            Assert.AreEqual(180, roverProfile.TransitDaysMin);
            Assert.AreEqual(270, roverProfile.TransitDaysMax);
            Assert.AreEqual(360, crewedProfile.TransitDaysMin);
            Assert.AreEqual(540, crewedProfile.TransitDaysMax);
            Assert.Greater(crewedProfile.MinDurationDays, roverProfile.MinDurationDays);

            // Difficulty & claim level: crewed is substantially higher risk and claim
            Assert.Greater(crewedProfile.DifficultyScore, roverProfile.DifficultyScore);
            Assert.AreEqual("Çok Yüksek", crewedProfile.ClaimLevel);
        }

        [Test]
        public void Compose_LeoOrbiter_DerivesBaselineRequirements()
        {
            var leo = _destinations.GetById("leo");
            var orbiter = _missionTypes.GetById("orbiter");
            var objective = _objectives.GetById("leo-global-imaging");

            var profile = MissionComposer.Compose(leo, orbiter, new[] { objective });

            Assert.AreEqual(9400.0, profile.RequiredDeltaV, 10.0);
            Assert.IsFalse(profile.RequiresEdl);
            Assert.IsFalse(profile.RequiresLifeSupport);
            Assert.AreEqual(1, profile.RequiredLaunchPadLevel);
            Assert.AreEqual(0, profile.WindowPeriodDays); // Continuous window
            Assert.AreEqual(0.80, profile.MinReliabilityThreshold, 0.01);
            CollectionAssert.Contains(profile.RequiredModuleCategories, "comms");
            CollectionAssert.Contains(profile.RequiredInstrumentCategories, "camera");
            Assert.AreEqual(1, profile.SuccessCriteria.Count);
            Assert.AreEqual("data-delivered-gb", profile.SuccessCriteria[0].CriterionType);
            Assert.AreEqual(12.0, profile.SuccessCriteria[0].Threshold);
        }

        [Test]
        public void Compose_MoonLander_WithObjectives_DerivesCompleteProfile()
        {
            var moon = _destinations.GetById("moon");
            var lander = _missionTypes.GetById("lander");
            var obj1 = _objectives.GetById("moon-polar-ice-survey");
            var obj2 = _objectives.GetById("moon-surface-experiment");

            var profile = MissionComposer.Compose(moon, lander, new[] { obj1, obj2 }, windowIndex: 1, budgetCeiling: 250.0);

            // Moon lander Δv = 9400 (ascent) + 3100 (transfer) + 900 (arrival) + 1700 (landing) = 15,100 m/s
            Assert.AreEqual(15100.0, profile.RequiredDeltaV, 20.0);
            Assert.IsTrue(profile.RequiresEdl);
            Assert.IsFalse(profile.RequiresLifeSupport);
            Assert.AreEqual(0.85, profile.MinReliabilityThreshold, 0.01);
            Assert.AreEqual(3, profile.RequiredLaunchPadLevel);
            Assert.AreEqual(1, profile.SelectedWindowIndex);
            Assert.AreEqual(250.0, profile.BudgetCeiling.Value);

            // Instruments and categories
            CollectionAssert.Contains(profile.RequiredInstrumentCategories, "radar");
            CollectionAssert.Contains(profile.RequiredInstrumentCategories, "spectrometer");
            CollectionAssert.Contains(profile.RequiredInstrumentCategories, "surface-science");

            // Success criteria
            Assert.AreEqual(2, profile.SuccessCriteria.Count);
            Assert.IsTrue(profile.SuccessCriteria.Any(c => c.CriterionType == "data-delivered-gb" && c.Threshold == 15.0));
            Assert.IsTrue(profile.SuccessCriteria.Any(c => c.CriterionType == "operation-days" && c.Threshold == 14.0));
        }

        [Test]
        public void Compose_EuropaOrbiter_DerivesExtremeEnvironmentAndResearch()
        {
            var europa = _destinations.GetById("europa");
            var orbiter = _missionTypes.GetById("orbiter");
            var objective = _objectives.GetById("europa-ice-radar-sounding");

            var profile = MissionComposer.Compose(europa, orbiter, new[] { objective });

            // 9400 + 5500 + 1800 = 16,700 m/s
            Assert.AreEqual(16700.0, profile.RequiredDeltaV, 50.0);
            Assert.AreEqual(4, profile.RequiredLaunchPadLevel);

            // Research requirements
            CollectionAssert.Contains(profile.RequiredResearchIds, "power-rtg");
            CollectionAssert.Contains(profile.RequiredResearchIds, "comms-laser");
            CollectionAssert.Contains(profile.RequiredResearchIds, "edl-heatshield-2");

            // Environmental radiation shielding
            CollectionAssert.Contains(profile.RequiredModuleCategories, "radiation-shielding");

            // Signal delay
            Assert.Greater(profile.CommDelaySecondsMin, 2000.0);
            Assert.Greater(profile.TransitDaysMin, 1800);
        }

        [Test]
        public void Compose_SampleReturn_RoundTripDeltaVAndCriteria()
        {
            var moon = _destinations.GetById("moon");
            var sampleReturn = _missionTypes.GetById("sample-return");
            var objective = _objectives.GetById("moon-sample-collection");

            var profile = MissionComposer.Compose(moon, sampleReturn, new[] { objective });

            // Moon sample-return = 9400 + 3100 + 900 + 1700 (landing) + 2400 (return) = 17,500 m/s
            Assert.AreEqual(17500.0, profile.RequiredDeltaV, 50.0);
            Assert.AreEqual(0.90, profile.MinReliabilityThreshold, 0.01);
            Assert.AreEqual(4, profile.RequiredLaunchPadLevel);
            CollectionAssert.Contains(profile.RequiredModuleCategories, "sample-container");
            Assert.IsTrue(profile.SuccessCriteria.Any(c => c.CriterionType == "sample-returned"));
        }

        [Test]
        public void Compose_NullInputs_ThrowsArgumentNullException()
        {
            var moon = _destinations.GetById("moon");
            var orbiter = _missionTypes.GetById("orbiter");

            Assert.Throws<System.ArgumentNullException>(() => MissionComposer.Compose(null, orbiter, null));
            Assert.Throws<System.ArgumentNullException>(() => MissionComposer.Compose(moon, null, null));
            Assert.Throws<System.ArgumentNullException>(() => MissionComposer.Compose((MissionCompositionRequest)null));
        }
    }
}
