using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Apogee.Core;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;
using Apogee.UI.Screens.Composer;
using Apogee.UI.Screens.Dossier;
using UnityEngine.UIElements;

namespace ApogeeLocalTestRunner
{
    public static class DossierTests
    {
        private static string GetJsonDir()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            if (!Directory.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            }
            return path;
        }

        public static CatalogCollection<DestinationRecord> LoadDestinations()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "destinations.json"));
            return CatalogLoader.Load<DestinationRecord>(json);
        }

        public static CatalogCollection<MissionTypeRecord> LoadMissionTypes()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "mission_types.json"));
            return CatalogLoader.Load<MissionTypeRecord>(json);
        }

        public static CatalogCollection<ObjectiveRecord> LoadObjectives()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "objectives.json"));
            return CatalogLoader.Load<ObjectiveRecord>(json);
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("UI_DossierViewModel_LoadProfile_PopulatesTelemetryAndOverview", Test_UI_DossierViewModel_LoadProfile_PopulatesTelemetryAndOverview);
            runTest("UI_DossierViewModel_MandatoryRequirements_DerivedCorrectly",     Test_UI_DossierViewModel_MandatoryRequirements_DerivedCorrectly);
            runTest("UI_DossierViewModel_ResearchRequirements_And_QueueAction",       Test_UI_DossierViewModel_ResearchRequirements_And_QueueAction);
            runTest("UI_DossierViewModel_QueueAllLockedResearch_QueuesOnlyLocked",    Test_UI_DossierViewModel_QueueAllLockedResearch_QueuesOnlyLocked);
            runTest("UI_DossierViewModel_SuccessCriteria_Populated",                  Test_UI_DossierViewModel_SuccessCriteria_Populated);
            runTest("UI_DossierViewModel_NavigationCommands_DispatchProperScreens",   Test_UI_DossierViewModel_NavigationCommands_DispatchProperScreens);
            runTest("UI_DossierViewModel_CompositionResultArgs_And_ContextLifecycle",  Test_UI_DossierViewModel_CompositionResultArgs_And_ContextLifecycle);
        }

        private static void Test_UI_DossierViewModel_LoadProfile_PopulatesTelemetryAndOverview()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var mars = destCatalog.GetById("mars");
            var rover = typesCatalog.GetById("rover");

            var profile = MissionComposer.Compose(
                destination: mars,
                missionType: rover,
                objectives: null,
                windowIndex: 1,
                budgetCeiling: 75.0,
                missionName: "Mars Explorer I"
            );

            var vm = new MissionDossierViewModel();
            vm.LoadProfile(profile);

            AssertEqual("Mars Explorer I", vm.MissionName.Value, "Mission name");
            AssertTrue(vm.DestinationLabel.Value.Contains("Mars"), "Destination label contains Mars");
            AssertTrue(vm.MissionTypeLabel.Value.Contains("Gezgin") || vm.MissionTypeLabel.Value.Contains("Rover"), "Mission type label contains Rover/Gezgin");
            AssertEqual(profile.RequiredDeltaV, vm.RequiredDeltaV.Value, "Required Delta-V value");
            AssertEqual(profile.MinReliabilityThreshold, vm.MinReliabilityThreshold.Value, "Min reliability");
            AssertEqual(profile.RequiredLaunchPadLevel, vm.RequiredLaunchPadLevel.Value, "Required launch pad level");
            AssertTrue(vm.LaunchWindowInfoText.Value.Contains("2."), "Second launch window text");
            AssertEqual("75.0M$", vm.BudgetCeilingText.Value, "Budget ceiling formatted");
        }

        private static void Test_UI_DossierViewModel_MandatoryRequirements_DerivedCorrectly()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var objCatalog = LoadObjectives();

            var moon = destCatalog.GetById("moon");
            var rover = typesCatalog.GetById("rover");
            var objSurface = objCatalog.GetById("moon-surface-experiment"); // operation-days type, rover compatible

            var profile = MissionComposer.Compose(
                destination: moon,
                missionType: rover,
                objectives: new[] { objSurface }
            );

            var vm = new MissionDossierViewModel();
            vm.LoadProfile(profile);

            var reqs = vm.MandatoryRequirements.Value;
            AssertTrue(reqs.Count >= 3, "At least 3 mandatory requirements");
            AssertTrue(reqs.Any(r => r.Title.Contains("Menzil")), "Contains Delta-V requirement");
            AssertTrue(reqs.Any(r => r.Title.Contains("EDL")), "Contains EDL requirement for rover");
            AssertTrue(reqs.Any(r => r.Title.Contains("Enstrüman") || r.Category.Contains("Bilim")), "Contains instrument requirement");
            AssertTrue(reqs.Any(r => r.Title.Contains("Güvenilirlik")), "Contains reliability requirement");
        }

        private static void Test_UI_DossierViewModel_ResearchRequirements_And_QueueAction()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var mars = destCatalog.GetById("mars");
            var rover = typesCatalog.GetById("rover");

            var profile = MissionComposer.Compose(
                destination: mars,
                missionType: rover,
                objectives: null
            );

            var vm = new MissionDossierViewModel(unlockedResearch: Array.Empty<string>());
            vm.LoadProfile(profile);

            AssertTrue(vm.LockedResearchCount.Value > 0, "Mars rover has locked research");
            string firstLockedId = vm.ResearchRequirements.Value.First(r => !r.IsUnlocked).ResearchId;

            string queuedEventId = null;
            vm.OnResearchQueued += id => queuedEventId = id;

            bool queued = vm.QueueResearch(firstLockedId);
            AssertTrue(queued, "Queued successfully");
            AssertEqual(firstLockedId, queuedEventId, "Event fired with correct ID");
            AssertEqual(1, vm.QueuedResearchCount.Value, "Queued count is 1");

            bool queuedAgain = vm.QueueResearch(firstLockedId);
            AssertTrue(!queuedAgain, "Cannot double queue same research");
        }

        private static void Test_UI_DossierViewModel_QueueAllLockedResearch_QueuesOnlyLocked()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var mars = destCatalog.GetById("mars");
            var rover = typesCatalog.GetById("rover");

            var profile = MissionComposer.Compose(
                destination: mars,
                missionType: rover,
                objectives: null
            );

            string alreadyUnlocked = profile.RequiredResearchIds.FirstOrDefault();
            var vm = new MissionDossierViewModel(unlockedResearch: new[] { alreadyUnlocked });
            vm.LoadProfile(profile);

            int initialLocked = vm.LockedResearchCount.Value;
            vm.QueueAllLockedResearchCommand.Execute();

            AssertEqual(initialLocked, vm.QueuedResearchCount.Value, "All locked research queued");
            AssertEqual(initialLocked, vm.ResearchQueue.Count, "Research queue matches locked count");
        }

        private static void Test_UI_DossierViewModel_SuccessCriteria_Populated()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var objCatalog = LoadObjectives();

            var moon = destCatalog.GetById("moon");
            var rover = typesCatalog.GetById("rover");
            var objSurface = objCatalog.GetById("moon-surface-experiment"); // operation-days type

            var profile = MissionComposer.Compose(
                destination: moon,
                missionType: rover,
                objectives: new[] { objSurface }
            );

            var vm = new MissionDossierViewModel();
            vm.LoadProfile(profile);

            var criteria = vm.SuccessCriteria.Value;
            AssertTrue(criteria.Count > 0, "Success criteria populated");
            AssertTrue(criteria.Any(c => c.CriterionType == "operation-days"), "Contains operation-days criterion for rover surface objective");
        }

        private static void Test_UI_DossierViewModel_NavigationCommands_DispatchProperScreens()
        {
            var navigator = new ScreenNavigator();
            var composerScreen = new MockScreen(ScreenId.MissionComposer);
            var dossierScreen = new MockScreen(ScreenId.MissionBrief);
            var workshopScreen = new MockScreen(ScreenId.Workshop);

            navigator.RegisterScreen(composerScreen);
            navigator.RegisterScreen(dossierScreen);
            navigator.RegisterScreen(workshopScreen);

            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var moon = destCatalog.GetById("moon");
            var rover = typesCatalog.GetById("rover");
            var profile = MissionComposer.Compose(moon, rover, null);

            var vm = new MissionDossierViewModel(navigator);
            vm.LoadProfile(profile);

            vm.GoToWorkshopCommand.Execute();
            AssertEqual(ScreenId.Workshop, navigator.CurrentScreenId, "Navigated to Workshop");
            AssertEqual(profile, workshopScreen.LastContext, "Workshop received mission profile context");

            vm.BackToComposerCommand.Execute();
            AssertEqual(ScreenId.MissionComposer, navigator.CurrentScreenId, "Navigated back to Mission Composer");
        }

        private static void Test_UI_DossierViewModel_CompositionResultArgs_And_ContextLifecycle()
        {
            var destCatalog = LoadDestinations();
            var typesCatalog = LoadMissionTypes();
            var moon = destCatalog.GetById("moon");
            var rover = typesCatalog.GetById("rover");
            var profile = MissionComposer.Compose(moon, rover, null, missionName: "Apollo Revival");

            var args = new MissionCompositionResultArgs(profile, moon, rover);
            var vm = new MissionDossierViewModel();
            vm.LoadProfile(args.Profile, args);

            AssertEqual("Apollo Revival", vm.MissionName.Value, "Mission name from args profile");
            AssertTrue(vm.RequiredDeltaV.Value > 0, "Required Delta-V initialized");
            AssertTrue(vm.MandatoryRequirements.Value.Count > 0, "Requirements populated");

            // Verify SetUnlockedResearch updates items
            string firstReqResearch = vm.ResearchRequirements.Value.FirstOrDefault()?.ResearchId;
            if (!string.IsNullOrEmpty(firstReqResearch))
            {
                int beforeLocked = vm.LockedResearchCount.Value;
                vm.SetUnlockedResearch(new[] { firstReqResearch });
                AssertEqual(beforeLocked - 1, vm.LockedResearchCount.Value, "Unlocking research decreases locked count");
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"Expected '{expected}' but got '{actual}'. {message}");
            }
        }

        private static void AssertTrue(bool condition, string message = "")
        {
            if (!condition) throw new Exception($"Expected condition to be true. {message}");
        }

        private sealed class MockScreen : IScreenController
        {
            public ScreenId ScreenId { get; }
            public VisualElement RootElement => null;
            public bool IsVisible { get; private set; }
            public object LastContext { get; private set; }

            public MockScreen(ScreenId screenId) => ScreenId = screenId;

            public void Initialize(VisualElement parent) { }
            public void OnEnter(object context = null)
            {
                IsVisible = true;
                LastContext = context;
            }
            public void OnExit() => IsVisible = false;
            public void Dispose() { }
        }    }
}
