using System.IO;
using System.Linq;
using NUnit.Framework;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;

namespace Apogee.Tests.Strategy
{
    /// <summary>
    /// Unit tests for M3-03: MissionDifficultyCalculator — Zorluk Katsayısı ve İddia Seviyesi.
    /// Verifies: difficulty formula (GDD §5.3), claim level thresholds, reward/risk multiplier scaling,
    /// player-facing summary generation, and integration with MissionComposer (profile carries full eval).
    /// </summary>
    [TestFixture]
    public sealed class MissionDifficultyTests
    {
        private CatalogCollection<DestinationRecord> _destinations;
        private CatalogCollection<MissionTypeRecord> _missionTypes;
        private CatalogCollection<ObjectiveRecord> _objectives;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            string jsonDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            if (!Directory.Exists(jsonDir))
                jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");

            _destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
            _missionTypes = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
            _objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        }

        // ── Claim Level Threshold Tests ────────────────────────────────────────────

        [Test]
        public void ClaimLevel_BelowFive_IsLow()
        {
            var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(3.0);
            Assert.AreEqual(MissionClaimLevel.Low, level);
            Assert.AreEqual("Düşük", label);
        }

        [Test]
        public void ClaimLevel_AtFive_IsMedium()
        {
            var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(5.0);
            Assert.AreEqual(MissionClaimLevel.Medium, level);
            Assert.AreEqual("Orta", label);
        }

        [Test]
        public void ClaimLevel_AtThirteen_IsHigh()
        {
            var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(13.0);
            Assert.AreEqual(MissionClaimLevel.High, level);
            Assert.AreEqual("Yüksek", label);
        }

        [Test]
        public void ClaimLevel_AtTwentyFive_IsExtreme()
        {
            var (level, label) = MissionDifficultyCalculator.ClassifyClaimLevel(25.0);
            Assert.AreEqual(MissionClaimLevel.Extreme, level);
            Assert.AreEqual("Çok Yüksek", label);
        }

        [Test]
        public void ClaimLevel_Zero_IsLow()
        {
            var (level, _) = MissionDifficultyCalculator.ClassifyClaimLevel(0.0);
            Assert.AreEqual(MissionClaimLevel.Low, level);
        }

        // ── Destination Multiplier Tests ───────────────────────────────────────────

        [Test]
        public void DestinationMultiplier_LEO_IsOne()
        {
            var leo = _destinations.GetById("leo");
            Assert.AreEqual(1.0, MissionDifficultyCalculator.GetDestinationMultiplier(leo), 0.001);
        }

        [Test]
        public void DestinationMultiplier_Moon_IsOnePointThree()
        {
            var moon = _destinations.GetById("moon");
            Assert.AreEqual(1.3, MissionDifficultyCalculator.GetDestinationMultiplier(moon), 0.001);
        }

        [Test]
        public void DestinationMultiplier_Mars_IsTwo()
        {
            var mars = _destinations.GetById("mars");
            Assert.AreEqual(2.0, MissionDifficultyCalculator.GetDestinationMultiplier(mars), 0.001);
        }

        [Test]
        public void DestinationMultiplier_Europa_IsTwoPointFive()
        {
            var europa = _destinations.GetById("europa");
            Assert.AreEqual(2.5, MissionDifficultyCalculator.GetDestinationMultiplier(europa), 0.001);
        }

        [Test]
        public void DestinationMultiplier_Null_IsOne()
        {
            Assert.AreEqual(1.0, MissionDifficultyCalculator.GetDestinationMultiplier(null), 0.001);
        }

        // ── Difficulty Formula Tests ───────────────────────────────────────────────

        [Test]
        public void Evaluate_LEOOrbiter_NoObjectives_BaselineDifficultyIsLow()
        {
            // No objectives → baseline sumObjectiveDifficulty = 1.0
            // Score = 1.0 × 1.0 (LEO) × 1.0 (orbiter riskMultiplier) = 1.0 → "Düşük"
            var leo = _destinations.GetById("leo");
            var orbiter = _missionTypes.GetById("orbiter");

            var eval = MissionDifficultyCalculator.Evaluate(leo, orbiter, null);

            Assert.AreEqual(1.0, eval.DifficultyScore, 0.05);
            Assert.AreEqual(MissionClaimLevel.Low, eval.ClaimLevel);
            Assert.AreEqual("Düşük", eval.ClaimLevelLabel);
            Assert.GreaterOrEqual(eval.RewardMultiplier, 1.0, "Reward multiplier must be >= 1.0");
            Assert.GreaterOrEqual(eval.RiskMultiplier, 1.0, "Risk multiplier must be >= 1.0");
        }

        [Test]
        public void Evaluate_MarsRover_OneObjective_YieldsHighDifficulty()
        {
            var mars = _destinations.GetById("mars");
            var rover = _missionTypes.GetById("rover");
            var obj = _objectives.GetById("mars-rover-exploration"); // difficultyPoints = 6

            // Score = 6 × 2.0 (Mars) × 1.4 (rover riskMultiplier) = 16.8 → "Yüksek"
            var eval = MissionDifficultyCalculator.Evaluate(mars, rover, new[] { obj });

            Assert.AreEqual(16.8, eval.DifficultyScore, 0.05);
            Assert.AreEqual(MissionClaimLevel.High, eval.ClaimLevel);
            Assert.AreEqual("Yüksek", eval.ClaimLevelLabel);
            Assert.Greater(eval.RewardMultiplier, eval.RiskMultiplier == 1.0 ? 1.0 : 1.0);
        }

        [Test]
        public void Evaluate_MarsCrewed_TwoObjectives_YieldsExtremeDifficulty()
        {
            var mars = _destinations.GetById("mars");
            var crewed = _missionTypes.GetById("crewed");
            var obj1 = _objectives.GetById("mars-life-signatures");   // difficultyPoints = 7
            var obj2 = _objectives.GetById("mars-sample-preparation"); // difficultyPoints = 7

            // Score = (7+7) × 2.0 × 2.5 = 70.0 → "Çok Yüksek"
            var eval = MissionDifficultyCalculator.Evaluate(mars, crewed, new[] { obj1, obj2 });

            Assert.Greater(eval.DifficultyScore, 25.0, "Crewed Mars with hard objectives must be Extreme (>25).");
            Assert.AreEqual(MissionClaimLevel.Extreme, eval.ClaimLevel);
            Assert.AreEqual("Çok Yüksek", eval.ClaimLevelLabel);
        }

        [Test]
        public void Evaluate_EuropaOrbiter_YieldsExtremeAndHighestMultipliers()
        {
            var europa = _destinations.GetById("europa");
            var orbiter = _missionTypes.GetById("orbiter");
            var obj = _objectives.GetById("europa-ice-radar-sounding"); // difficultyPoints = 8

            // Score = 8 × 2.5 × 1.0 = 20.0 → "Yüksek"
            var eval = MissionDifficultyCalculator.Evaluate(europa, orbiter, new[] { obj });

            Assert.AreEqual(20.0, eval.DifficultyScore, 0.05);
            Assert.AreEqual(MissionClaimLevel.High, eval.ClaimLevel);

            // Reward/risk must scale higher than LEO baseline
            var leoEval = MissionDifficultyCalculator.Evaluate(_destinations.GetById("leo"), orbiter, null);
            Assert.Greater(eval.RewardMultiplier, leoEval.RewardMultiplier, "Europa reward must exceed LEO.");
            Assert.Greater(eval.RiskMultiplier, leoEval.RiskMultiplier, "Europa risk must exceed LEO.");
        }

        // ── Reward/Risk Multiplier Scaling Tests ───────────────────────────────────

        [Test]
        public void RewardMultiplier_ScalesMonotonically_WithIncreasingDifficulty()
        {
            var leo = _destinations.GetById("leo");
            var mars = _destinations.GetById("mars");
            var orbiter = _missionTypes.GetById("orbiter");
            var crewed = _missionTypes.GetById("crewed");
            var obj = _objectives.GetById("leo-climate-measurement");

            var evalLow = MissionDifficultyCalculator.Evaluate(leo, orbiter, null);
            var evalMed = MissionDifficultyCalculator.Evaluate(leo, orbiter, new[] { obj });
            var evalHigh = MissionDifficultyCalculator.Evaluate(mars, orbiter, new[] { _objectives.GetById("mars-surface-mapping") });
            var evalExtreme = MissionDifficultyCalculator.Evaluate(mars, crewed, new[] { _objectives.GetById("mars-life-signatures") });

            Assert.LessOrEqual(evalLow.RewardMultiplier, evalMed.RewardMultiplier, "Med >= Low reward.");
            Assert.LessOrEqual(evalMed.RewardMultiplier, evalHigh.RewardMultiplier, "High >= Med reward.");
            Assert.LessOrEqual(evalHigh.RewardMultiplier, evalExtreme.RewardMultiplier, "Extreme >= High reward.");
        }

        [Test]
        public void RiskMultiplier_ScalesMonotonically_WithIncreasingDifficulty()
        {
            var leo = _destinations.GetById("leo");
            var mars = _destinations.GetById("mars");
            var orbiter = _missionTypes.GetById("orbiter");
            var crewed = _missionTypes.GetById("crewed");

            var evalLow = MissionDifficultyCalculator.Evaluate(leo, orbiter, null);
            var evalHigh = MissionDifficultyCalculator.Evaluate(mars, orbiter, new[] { _objectives.GetById("mars-atmosphere-sampling") });
            var evalExtreme = MissionDifficultyCalculator.Evaluate(mars, crewed, new[] { _objectives.GetById("mars-life-signatures") });

            Assert.LessOrEqual(evalLow.RiskMultiplier, evalHigh.RiskMultiplier, "High >= Low risk.");
            Assert.LessOrEqual(evalHigh.RiskMultiplier, evalExtreme.RiskMultiplier, "Extreme >= High risk.");
        }

        // ── Summary String Tests ───────────────────────────────────────────────────

        [Test]
        public void Evaluate_RewardSummary_IsNonEmpty()
        {
            var leo = _destinations.GetById("leo");
            var orbiter = _missionTypes.GetById("orbiter");
            var eval = MissionDifficultyCalculator.Evaluate(leo, orbiter, null);

            Assert.IsFalse(string.IsNullOrWhiteSpace(eval.RewardSummary), "RewardSummary must not be blank.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(eval.RiskSummary), "RiskSummary must not be blank.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(eval.CombinedSummary), "CombinedSummary must not be blank.");
        }

        [Test]
        public void Evaluate_CrewedMission_RewardSummary_ContainsPrestigeNote()
        {
            var mars = _destinations.GetById("mars");
            var crewed = _missionTypes.GetById("crewed");
            var eval = MissionDifficultyCalculator.Evaluate(mars, crewed, null);

            StringAssert.Contains("Prestiж", eval.RewardSummary.Replace("j", "j") ?? string.Empty,
                "Expected crewed prestige bonus note in reward summary.");
        }

        [Test]
        public void Evaluate_EuropaOrbiter_RiskSummary_ContainsRadiationNote()
        {
            var europa = _destinations.GetById("europa");
            var orbiter = _missionTypes.GetById("orbiter");
            var eval = MissionDifficultyCalculator.Evaluate(europa, orbiter, new[] { _objectives.GetById("europa-ice-radar-sounding") });

            StringAssert.Contains("Radyasyon", eval.RiskSummary, "Europa risk summary must mention radiation.");
        }

        // ── Integration with MissionComposer ──────────────────────────────────────

        [Test]
        public void Compose_Profile_CarriesClaimLevelTypeAndMultipliers()
        {
            var mars = _destinations.GetById("mars");
            var rover = _missionTypes.GetById("rover");
            var obj = _objectives.GetById("mars-rover-exploration");

            var profile = MissionComposer.Compose(mars, rover, new[] { obj }, missionName: "Test Rover");

            Assert.AreNotEqual(MissionClaimLevel.Low, profile.ClaimLevelType,
                "Mars Rover profile must not be Low difficulty.");
            Assert.Greater(profile.RewardMultiplier, 1.0, "RewardMultiplier must exceed baseline.");
            Assert.Greater(profile.RiskMultiplier, 1.0, "RiskMultiplier must exceed baseline.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(profile.RewardSummary));
            Assert.IsFalse(string.IsNullOrWhiteSpace(profile.RiskSummary));
        }

        [Test]
        public void Compose_LeoOrbiter_NoObjectives_IsLowAndProfileConsistent()
        {
            var leo = _destinations.GetById("leo");
            var orbiter = _missionTypes.GetById("orbiter");

            var profile = MissionComposer.Compose(leo, orbiter, null, missionName: "LEO Baseline");

            Assert.AreEqual(MissionClaimLevel.Low, profile.ClaimLevelType);
            Assert.AreEqual("Düşük", profile.ClaimLevel);
            Assert.AreEqual(profile.DifficultyScore, MissionDifficultyCalculator.Evaluate(leo, orbiter, null).DifficultyScore, 0.01);
        }

        [Test]
        public void Compose_DifficultyChanges_WhenObjectivesAdded()
        {
            var moon = _destinations.GetById("moon");
            var orbiter = _missionTypes.GetById("orbiter");
            var obj = _objectives.GetById("moon-surface-mapping"); // difficultyPoints = 3

            var profileEmpty = MissionComposer.Compose(moon, orbiter, null);
            var profileWithObj = MissionComposer.Compose(moon, orbiter, new[] { obj });

            Assert.Greater(profileWithObj.DifficultyScore, profileEmpty.DifficultyScore,
                "Adding objectives must increase difficulty score.");
            Assert.Greater(profileWithObj.RewardMultiplier, profileEmpty.RewardMultiplier,
                "Adding objectives must increase reward multiplier.");
        }

        [Test]
        public void Compose_SumOfComponents_MatchesFormula()
        {
            var moon = _destinations.GetById("moon");
            var rover = _missionTypes.GetById("rover");
            var obj1 = _objectives.GetById("moon-polar-ice-survey");   // difficultyPoints = 4
            var obj2 = _objectives.GetById("moon-surface-experiment"); // difficultyPoints = 4

            // Expected: (4+4) × 1.3 (moon) × 1.4 (rover) = 14.56 → "Yüksek"
            var eval = MissionDifficultyCalculator.Evaluate(moon, rover, new[] { obj1, obj2 });
            Assert.AreEqual(14.6, eval.DifficultyScore, 0.1, "Moon Rover 2-objective score mismatch.");
            Assert.AreEqual(MissionClaimLevel.High, eval.ClaimLevel);
        }
    }
}
