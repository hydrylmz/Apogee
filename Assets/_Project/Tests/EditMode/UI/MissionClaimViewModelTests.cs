using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Apogee.Data;
using Apogee.Design;
using Apogee.UI.Screens.Composer;

namespace Apogee.Tests.UI
{
    /// <summary>
    /// Unit tests for M3-03: MissionClaimViewModel & reactive presentation of claim levels, rewards, and risks.
    /// Verifies acceptance criterion: "Seçim değişince seviye/ödül/risk güncellenir."
    /// </summary>
    [TestFixture]
    public sealed class MissionClaimViewModelTests
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

        [Test]
        public void ViewModel_InitialState_DefaultsToLowDifficulty()
        {
            using var vm = new MissionClaimViewModel();

            Assert.AreEqual(MissionClaimLevel.Low, vm.ClaimLevelEnum.Value);
            Assert.AreEqual("claim-badge--low", vm.BadgeCssClass.Value);
            Assert.GreaterOrEqual(vm.DifficultyScore.Value, 1.0);
            Assert.GreaterOrEqual(vm.RewardMultiplier.Value, 1.0);
            Assert.GreaterOrEqual(vm.RiskMultiplier.Value, 1.0);
            Assert.IsNotNull(vm.CurrentEvaluation.Value);
        }

        [Test]
        public void ViewModel_SetDestination_DynamicallyUpdatesEvaluation()
        {
            var leo = _destinations.GetById("leo");
            var mars = _destinations.GetById("mars");
            var orbiter = _missionTypes.GetById("orbiter");

            using var vm = new MissionClaimViewModel(initialDestination: leo, initialMissionType: orbiter);
            double leoScore = vm.DifficultyScore.Value;

            int notificationCount = 0;
            using var sub = vm.DifficultyScore.Subscribe(_ => notificationCount++, notifyImmediately: false);

            // Change destination to Mars
            vm.SetDestination(mars);

            Assert.AreEqual(1, notificationCount, "Changing destination must notify DifficultyScore subscriber.");
            Assert.Greater(vm.DifficultyScore.Value, leoScore, "Mars difficulty must exceed LEO difficulty.");
            Assert.AreEqual(mars.Id, vm.Destination.Value.Id);
        }

        [Test]
        public void ViewModel_SetMissionType_DynamicallyUpdatesMultipliers()
        {
            var moon = _destinations.GetById("moon");
            var orbiter = _missionTypes.GetById("orbiter");
            var rover = _missionTypes.GetById("rover");

            using var vm = new MissionClaimViewModel(initialDestination: moon, initialMissionType: orbiter);
            double orbiterReward = vm.RewardMultiplier.Value;
            double orbiterRisk = vm.RiskMultiplier.Value;

            // Change to rover
            vm.SetMissionType(rover);

            Assert.Greater(vm.RewardMultiplier.Value, orbiterReward, "Rover reward multiplier must exceed orbiter.");
            Assert.Greater(vm.RiskMultiplier.Value, orbiterRisk, "Rover risk multiplier must exceed orbiter.");
            Assert.AreEqual(rover.Id, vm.MissionType.Value.Id);
        }

        [Test]
        public void ViewModel_ToggleObjective_AddsAndRemovesReactively()
        {
            var mars = _destinations.GetById("mars");
            var rover = _missionTypes.GetById("rover");
            var obj = _objectives.GetById("mars-rover-exploration"); // difficulty = 6

            using var vm = new MissionClaimViewModel(initialDestination: mars, initialMissionType: rover);
            double baselineScore = vm.DifficultyScore.Value;

            // Toggle on
            vm.ToggleObjective(obj);
            Assert.AreEqual(1, vm.SelectedObjectives.Value.Count);
            Assert.Greater(vm.DifficultyScore.Value, baselineScore);
            Assert.AreEqual(MissionClaimLevel.High, vm.ClaimLevelEnum.Value);
            Assert.AreEqual("claim-badge--high", vm.BadgeCssClass.Value);

            // Toggle off
            vm.ToggleObjective(obj);
            Assert.AreEqual(0, vm.SelectedObjectives.Value.Count);
            Assert.AreEqual(baselineScore, vm.DifficultyScore.Value);
        }

        [Test]
        public void ViewModel_AddAndRemoveObjective_UpdatesClaimLevelAndBadgeClass()
        {
            var mars = _destinations.GetById("mars");
            var crewed = _missionTypes.GetById("crewed");
            var obj1 = _objectives.GetById("mars-life-signatures");   // diff 7
            var obj2 = _objectives.GetById("mars-sample-preparation"); // diff 7

            using var vm = new MissionClaimViewModel(initialDestination: mars, initialMissionType: crewed);

            vm.AddObjective(obj1);
            vm.AddObjective(obj2);

            Assert.AreEqual(2, vm.SelectedObjectives.Value.Count);
            Assert.AreEqual(MissionClaimLevel.Extreme, vm.ClaimLevelEnum.Value);
            Assert.AreEqual("claim-badge--extreme", vm.BadgeCssClass.Value);
            Assert.Greater(vm.RewardMultiplier.Value, 5.0, "Mars crewed 2 hard objectives reward must be very high.");

            // Remove one objective
            vm.RemoveObjective(obj2.Id);
            Assert.AreEqual(1, vm.SelectedObjectives.Value.Count);

            // Clear remaining
            vm.ClearObjectives();
            Assert.AreEqual(0, vm.SelectedObjectives.Value.Count);
        }

        [Test]
        public void ViewModel_AllObservableProperties_NotifyOnStateMutation()
        {
            var leo = _destinations.GetById("leo");
            var orbiter = _missionTypes.GetById("orbiter");
            var mars = _destinations.GetById("mars");
            var crewed = _missionTypes.GetById("crewed");
            var obj = _objectives.GetById("mars-life-signatures");

            using var vm = new MissionClaimViewModel(initialDestination: leo, initialMissionType: orbiter);

            int scoreNotifications = 0;
            int levelNotifications = 0;
            int rewardNotifications = 0;
            int riskNotifications = 0;
            int badgeNotifications = 0;

            using var subScore = vm.DifficultyScore.Subscribe(_ => scoreNotifications++, notifyImmediately: false);
            using var subLevel = vm.ClaimLevel.Subscribe(_ => levelNotifications++, notifyImmediately: false);
            using var subReward = vm.RewardMultiplier.Subscribe(_ => rewardNotifications++, notifyImmediately: false);
            using var subRisk = vm.RiskMultiplier.Subscribe(_ => riskNotifications++, notifyImmediately: false);
            using var subBadge = vm.BadgeCssClass.Subscribe(_ => badgeNotifications++, notifyImmediately: false);

            // Change VM selection
            vm.SetDestination(mars);
            vm.SetMissionType(crewed);
            vm.AddObjective(obj);

            Assert.GreaterOrEqual(scoreNotifications, 3);
            Assert.GreaterOrEqual(levelNotifications, 1);
            Assert.GreaterOrEqual(rewardNotifications, 3);
            Assert.GreaterOrEqual(riskNotifications, 3);
            Assert.GreaterOrEqual(badgeNotifications, 1);

            Assert.AreEqual(MissionClaimLevel.Extreme, vm.ClaimLevelEnum.Value);
            Assert.AreEqual("claim-badge--extreme", vm.BadgeCssClass.Value);
        }
    }
}
