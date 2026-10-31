using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Composer;

namespace Apogee.Tests.UI
{
    [TestFixture]
    public sealed class MissionComposerViewModelTests
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
                jsonDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            }

            _destinations = CatalogLoader.Load<DestinationRecord>(File.ReadAllText(Path.Combine(jsonDir, "destinations.json")));
            _missionTypes = CatalogLoader.Load<MissionTypeRecord>(File.ReadAllText(Path.Combine(jsonDir, "mission_types.json")));
            _objectives = CatalogLoader.Load<ObjectiveRecord>(File.ReadAllText(Path.Combine(jsonDir, "objectives.json")));
        }

        private MissionComposerViewModel CreateViewModel(IScreenNavigator navigator = null)
        {
            return new MissionComposerViewModel(
                navigator: navigator,
                destinations: _destinations.All,
                missionTypes: _missionTypes.All,
                objectives: _objectives.All
            );
        }

        [Test]
        public void InitialState_DefaultSelectionsAndStepZero()
        {
            using var vm = CreateViewModel();

            Assert.AreEqual(ComposerStep.Destination, vm.CurrentStep.Value);
            Assert.AreEqual(0, vm.CurrentStepIndex.Value);
            Assert.IsFalse(vm.CanGoPrevious.Value);
            Assert.IsTrue(vm.CanGoNext.Value);
            Assert.IsNotNull(vm.SelectedDestination.Value);
            Assert.IsNotNull(vm.SelectedMissionType.Value);
            Assert.AreEqual("moon", vm.SelectedDestination.Value.Id);
            Assert.AreEqual("rover", vm.SelectedMissionType.Value.Id);
            Assert.IsTrue(vm.CanFinalize.Value);
        }

        [Test]
        public void StepNavigation_ForwardAndBackward_TransitionsState()
        {
            using var vm = CreateViewModel();

            // Step 0: Destination
            Assert.AreEqual(ComposerStep.Destination, vm.CurrentStep.Value);
            Assert.IsTrue(vm.CanGoNext.Value);
            Assert.IsFalse(vm.CanGoPrevious.Value);

            // Step 1: Mission Type
            vm.NextStep();
            Assert.AreEqual(ComposerStep.MissionType, vm.CurrentStep.Value);
            Assert.AreEqual(1, vm.CurrentStepIndex.Value);
            Assert.IsTrue(vm.CanGoNext.Value);
            Assert.IsTrue(vm.CanGoPrevious.Value);

            // Step 2: Objectives
            vm.NextStep();
            Assert.AreEqual(ComposerStep.Objectives, vm.CurrentStep.Value);
            Assert.AreEqual(2, vm.CurrentStepIndex.Value);
            Assert.IsTrue(vm.CanGoNext.Value);
            Assert.IsTrue(vm.CanGoPrevious.Value);

            // Step 3: Schedule & Budget
            vm.NextStep();
            Assert.AreEqual(ComposerStep.ScheduleAndBudget, vm.CurrentStep.Value);
            Assert.AreEqual(3, vm.CurrentStepIndex.Value);
            Assert.IsFalse(vm.CanGoNext.Value); // Last step
            Assert.IsTrue(vm.CanGoPrevious.Value);
            Assert.IsTrue(vm.CanFinalize.Value);

            // Step back to Objectives
            vm.PreviousStep();
            Assert.AreEqual(ComposerStep.Objectives, vm.CurrentStep.Value);
            Assert.AreEqual(2, vm.CurrentStepIndex.Value);

            // Jump directly to Destination
            vm.GoToStep(ComposerStep.Destination);
            Assert.AreEqual(ComposerStep.Destination, vm.CurrentStep.Value);
            Assert.AreEqual(0, vm.CurrentStepIndex.Value);
        }

        [Test]
        public void SelectDestination_UpdatesSelectionsAndDerivesEstimates()
        {
            using var vm = CreateViewModel();
            var leo = _destinations.GetById("leo");
            vm.SelectDestination(leo);
            double leoDv = vm.EstimatedDeltaV.Value;

            var mars = _destinations.GetById("mars");
            vm.SelectDestination(mars);

            Assert.AreEqual("mars", vm.SelectedDestination.Value.Id);
            Assert.AreEqual("mars", vm.ClaimViewModel.Destination.Value.Id);
            Assert.Greater(vm.EstimatedDeltaV.Value, leoDv);
            StringAssert.Contains("180", vm.EstimatedTransitRange.Value);
            StringAssert.Contains("MARS", vm.MissionName.Value);
        }

        [Test]
        public void SelectMissionType_CrewedVsRover_UpdatesRequirementsAndClaim()
        {
            using var vm = CreateViewModel();
            var mars = _destinations.GetById("mars");
            vm.SelectDestination(mars);

            var roverType = _missionTypes.GetById("rover");
            vm.SelectMissionType(roverType);
            double roverDv = vm.EstimatedDeltaV.Value;
            double roverRel = vm.EstimatedReliabilityThreshold.Value;

            var crewedType = _missionTypes.GetById("crewed");
            vm.SelectMissionType(crewedType);
            double crewedDv = vm.EstimatedDeltaV.Value;
            double crewedRel = vm.EstimatedReliabilityThreshold.Value;

            Assert.AreEqual("crewed", vm.SelectedMissionType.Value.Id);
            Assert.AreEqual(19200.0, crewedDv, 50.0);
            Assert.Greater(crewedDv, roverDv + 5000.0);
            Assert.GreaterOrEqual(crewedRel, 0.98);
            Assert.AreEqual(5, vm.EstimatedLaunchPadLevel.Value);
            Assert.Greater(vm.ClaimViewModel.RewardMultiplier.Value, 2.0);
        }

        [Test]
        public void Objectives_CompatibleFilteringAndPruning()
        {
            using var vm = CreateViewModel();
            var mars = _destinations.GetById("mars");
            var roverType = _missionTypes.GetById("rover");
            vm.SelectDestination(mars);
            vm.SelectMissionType(roverType);

            // Verify compatible list contains Mars objectives
            var compatible = vm.CompatibleObjectives.Value;
            Assert.IsTrue(compatible.Any(o => o.Id == "mars-rover-exploration"));

            var marsObj = compatible.First(o => o.Id == "mars-rover-exploration");
            vm.ToggleObjective(marsObj);
            Assert.AreEqual(1, vm.SelectedObjectives.Value.Count);
            Assert.AreEqual("mars-rover-exploration", vm.SelectedObjectives.Value[0].Id);

            // Now switch destination to LEO
            var leo = _destinations.GetById("leo");
            vm.SelectDestination(leo);

            // Mars objective must be pruned because it's incompatible with LEO
            Assert.AreEqual(0, vm.SelectedObjectives.Value.Count);
            Assert.IsFalse(vm.CompatibleObjectives.Value.Any(o => o.Id == "mars-rover-exploration"));
            Assert.IsTrue(vm.CompatibleObjectives.Value.Any(o => o.Id == "leo-global-imaging"));
        }

        [Test]
        public void Objectives_SelectAllAndClear()
        {
            using var vm = CreateViewModel();
            var moon = _destinations.GetById("moon");
            var roverType = _missionTypes.GetById("rover");
            vm.SelectDestination(moon);
            vm.SelectMissionType(roverType);

            int compatibleCount = vm.CompatibleObjectives.Value.Count;
            Assert.Greater(compatibleCount, 0);

            vm.SelectAllCompatibleObjectives();
            Assert.AreEqual(compatibleCount, vm.SelectedObjectives.Value.Count);

            vm.ClearSelectedObjectives();
            Assert.AreEqual(0, vm.SelectedObjectives.Value.Count);
        }

        [Test]
        public void ScheduleAndBudget_CustomSettings()
        {
            using var vm = CreateViewModel();
            var moon = _destinations.GetById("moon");
            vm.SelectDestination(moon);

            vm.SetWindowIndex(1);
            Assert.AreEqual(1, vm.SelectedWindowIndex.Value);
            StringAssert.Contains("Pencere 1", vm.LaunchWindowText.Value);

            vm.SetBudgetCeiling(75.5);
            Assert.AreEqual(75.5, vm.BudgetCeiling.Value);

            vm.SetMissionName("LUNA-EXPLORER-X");
            Assert.AreEqual("LUNA-EXPLORER-X", vm.MissionName.Value);
        }

        [Test]
        public void ComposeMissionProfile_ProducesValidProfileAndFiresEvent()
        {
            using var vm = CreateViewModel();
            var moon = _destinations.GetById("moon");
            var roverType = _missionTypes.GetById("rover");
            vm.SelectDestination(moon);
            vm.SelectMissionType(roverType);
            vm.SelectAllCompatibleObjectives();

            MissionProfile eventProfile = null;
            vm.OnMissionComposed += p => eventProfile = p;

            var profile = vm.ComposeMissionProfile();

            Assert.IsNotNull(profile);
            Assert.AreSame(profile, eventProfile);
            Assert.AreSame(profile, vm.ComposedProfile.Value);
            Assert.AreEqual(15100.0, profile.RequiredDeltaV, 50.0);
            Assert.IsTrue(profile.RequiresEdl);
            CollectionAssert.Contains(profile.RequiredModuleCategories, "edl");
            Assert.Greater(profile.SuccessCriteria.Count, 0);
        }

        [Test]
        public void ComposeMissionProfile_WithoutDestination_ThrowsInvalidOperation()
        {
            using var vm = new MissionComposerViewModel(
                destinations: Array.Empty<DestinationRecord>(),
                missionTypes: Array.Empty<MissionTypeRecord>(),
                objectives: Array.Empty<ObjectiveRecord>()
            );

            Assert.Throws<InvalidOperationException>(() => vm.ComposeMissionProfile());
        }

        [Test]
        public void NavigationIntegration_FinalizeDispatchesArgsToNavigator()
        {
            var navigator = new ScreenNavigator();
            using var vm = CreateViewModel(navigator);

            bool briefNavigated = false;
            object capturedContext = null;

            // Register a mock controller for MissionBrief
            var dummyController = new DummyBriefController(args =>
            {
                briefNavigated = true;
                capturedContext = args;
            });
            navigator.RegisterScreen(dummyController);

            vm.ComposeMissionProfile();

            Assert.IsTrue(briefNavigated);
            Assert.IsInstanceOf<MissionCompositionResultArgs>(capturedContext);
            var resultArgs = (MissionCompositionResultArgs)capturedContext;
            Assert.IsNotNull(resultArgs.Profile);
            Assert.AreEqual("moon", resultArgs.Destination.Id);
            Assert.AreEqual("rover", resultArgs.MissionType.Id);
        }

        private sealed class DummyBriefController : IScreenController
        {
            private readonly Action<object> _onEnterCallback;
            public ScreenId ScreenId => ScreenId.MissionBrief;
            public UnityEngine.UIElements.VisualElement RootElement => null;
            public bool IsVisible => true;

            public DummyBriefController(Action<object> onEnterCallback)
            {
                _onEnterCallback = onEnterCallback;
            }

            public void Initialize(UnityEngine.UIElements.VisualElement parentContainer) { }
            public void OnEnter(object context = null) => _onEnterCallback?.Invoke(context);
            public void OnExit() { }
            public void Dispose() { }
        }
    }
}
