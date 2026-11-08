using System;
using System.Collections.Generic;
using Apogee.Core.Attribution;
using Apogee.Core.Education;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Milestones;
using Apogee.UI.Campaign;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Milestones;
using Apogee.UI.Bootstrap;
using Apogee.UI.Screens.Attribution;
using Apogee.UI.Screens.Events;
using Apogee.UI.Screens.Glossary;
using Apogee.UI.Screens.MainMenu;
using Apogee.UI.Screens.CampaignMap;
using Apogee.UI.Screens.Bases;
using Apogee.UI.Screens.Contracts;
using Apogee.UI.Screens.Production;
using Apogee.UI.Screens.Research;
using Apogee.UI.Screens.SolarMap;
using Apogee.UI.Screens.Settings;
using Apogee.UI.Screens.Composer;
using Apogee.UI.Screens.Workshop;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Launch;
using Apogee.UI.Screens.Operations;
using Apogee.UI.Screens.Report;
using Apogee.Core.Audio;
using Apogee.Core.Localization;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Report;
using Apogee.Core.Simulation.Operations;
using Apogee.Strategy.Mission;
using Apogee.Strategy.Persistence;
using UnityEngine.UIElements;

namespace ApogeeLocalTestRunner
{
    public static class Milestone9Tests
    {
        private class DummyScreen : IScreenController
        {
            public ScreenId ScreenId { get; }
            public VisualElement RootElement => null;
            public bool IsVisible { get; private set; }
            public int EnterCount { get; private set; }
            public object LastContext { get; private set; }

            public DummyScreen(ScreenId id) => ScreenId = id;

            public void Initialize(VisualElement parent) { }
            public void OnEnter(object context = null)
            {
                IsVisible = true;
                EnterCount++;
                LastContext = context;
            }
            public void OnExit() => IsVisible = false;
            public void Dispose() { }
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M9_01_Navigator_CanRegisterAndNavigateAllNewScreens", Test_M9_01_Navigator_CanRegisterAndNavigateAllNewScreens);
            runTest("M9_01_MilestoneBoardViewModel_BindCampaign_UpdatesObservables", Test_M9_01_MilestoneBoardViewModel_BindCampaign_UpdatesObservables);
            runTest("M9_01_CampaignEndViewModel_WithCampaignState_BindsObservables", Test_M9_01_CampaignEndViewModel_WithCampaignState_BindsObservables);
            runTest("M9_01_EventPopupViewModel_InitializesCleanly", Test_M9_01_EventPopupViewModel_InitializesCleanly);

            runTest("M9_02_CampaignFactory_CreateDefault_InitializesCampaignCorrectly", Test_M9_02_CampaignFactory_CreateDefault_InitializesCampaignCorrectly);
            runTest("M9_02_CampaignFactory_LoadCatalogsFromJson_WiresAllSubsystems", Test_M9_02_CampaignFactory_LoadCatalogsFromJson_WiresAllSubsystems);
            runTest("M9_02_CampaignFactory_LoadCatalogsFromDirectory_LoadsFullGameCatalogs", Test_M9_02_CampaignFactory_LoadCatalogsFromDirectory_LoadsFullGameCatalogs);
            runTest("M9_02_CampaignSaveAndRestore_RoundTrip_PreservesSubsystemState", Test_M9_02_CampaignSaveAndRestore_RoundTrip_PreservesSubsystemState);

            runTest("M9_03_MainMenuViewModel_InitialState_Defaults", Test_M9_03_MainMenuViewModel_InitialState_Defaults);
            runTest("M9_03_MainMenuViewModel_NewCampaign_StartsAndNavigates", Test_M9_03_MainMenuViewModel_NewCampaign_StartsAndNavigates);
            runTest("M9_03_MainMenuViewModel_NewCampaign_FallbackToWorkshopWhenMapMissing", Test_M9_03_MainMenuViewModel_NewCampaign_FallbackToWorkshopWhenMapMissing);
            runTest("M9_03_MainMenuViewModel_ContinueCampaign_LoadsFromStorage", Test_M9_03_MainMenuViewModel_ContinueCampaign_LoadsFromStorage);
            runTest("M9_03_MainMenuViewModel_ContinueCampaign_NoSaveFailsGracefully", Test_M9_03_MainMenuViewModel_ContinueCampaign_NoSaveFailsGracefully);
            runTest("M9_03_MainMenuViewModel_NavigationCommands_DispatchScreens", Test_M9_03_MainMenuViewModel_NavigationCommands_DispatchScreens);
            runTest("M9_03_MainMenuViewModel_RefreshSaveState_UpdatesObservables", Test_M9_03_MainMenuViewModel_RefreshSaveState_UpdatesObservables);
            // Note: M9_03_MainMenuScreenController_CreateViewAndLifecycle requires Unity Play Mode (VisualElement ECall) — skipped in headless runner.

            runTest("M9_04_CampaignMapViewModel_InitialState_Defaults", Test_M9_04_CampaignMapViewModel_InitialState_Defaults);
            runTest("M9_04_CampaignMapViewModel_SelectRegion_ShowsLensEvaluationAndBases", Test_M9_04_CampaignMapViewModel_SelectRegion_ShowsLensEvaluationAndBases);
            runTest("M9_04_CampaignMapViewModel_SetMapLens_DispatchesCommandAndUpdatesScores", Test_M9_04_CampaignMapViewModel_SetMapLens_DispatchesCommandAndUpdatesScores);
            runTest("M9_04_CampaignMapViewModel_TimeSpeedControls_DispatchesSetTimeSpeedCommand", Test_M9_04_CampaignMapViewModel_TimeSpeedControls_DispatchesSetTimeSpeedCommand);
            runTest("M9_04_CampaignMapViewModel_AdvanceDays_AdvancesClock", Test_M9_04_CampaignMapViewModel_AdvanceDays_AdvancesClock);
            runTest("M9_04_CampaignMapViewModel_NavigationCommands_DispatchScreens", Test_M9_04_CampaignMapViewModel_NavigationCommands_DispatchScreens);
            runTest("M9_04_RegionMapItemViewModel_NormalizedCoordinates", Test_M9_04_RegionMapItemViewModel_NormalizedCoordinates);

            runTest("M9_05_ResearchTreeViewModel_InitialState_And_NodeBinding", Test_M9_05_ResearchTreeViewModel_InitialState_And_NodeBinding);
            runTest("M9_05_ResearchTreeViewModel_StartResearch_And_Cancel", Test_M9_05_ResearchTreeViewModel_StartResearch_And_Cancel);
            runTest("M9_05_BaseManagementViewModel_InitialState_And_UpgradeBase", Test_M9_05_BaseManagementViewModel_InitialState_And_UpgradeBase);
            runTest("M9_05_BaseManagementViewModel_BuildBase_AddsFacility", Test_M9_05_BaseManagementViewModel_BuildBase_AddsFacility);
            runTest("M9_05_ProductionQueueViewModel_InitialState_And_StartTesting", Test_M9_05_ProductionQueueViewModel_InitialState_And_StartTesting);
            runTest("M9_05_ContractsViewModel_InitialState_And_SignContract", Test_M9_05_ContractsViewModel_InitialState_And_SignContract);

            runTest("M9_06_SolarMapViewModel_InitialState_And_BodyBinding", Test_M9_06_SolarMapViewModel_InitialState_And_BodyBinding);
            runTest("M9_06_SolarMapViewModel_SelectDestination_UpdatesBriefingCardAndWindows", Test_M9_06_SolarMapViewModel_SelectDestination_UpdatesBriefingCardAndWindows);
            runTest("M9_06_SolarMapViewModel_AdvanceDays_UpdatesOrbitalPositions", Test_M9_06_SolarMapViewModel_AdvanceDays_UpdatesOrbitalPositions);
            runTest("M9_06_SolarMapViewModel_ActiveMissionMarkers_Interpolation", Test_M9_06_SolarMapViewModel_ActiveMissionMarkers_Interpolation);
            runTest("M9_06_SolarMapViewModel_NavigationCommands", Test_M9_06_SolarMapViewModel_NavigationCommands);

            runTest("M9_07_SettingsViewModel_InitialState_Defaults", Test_M9_07_SettingsViewModel_InitialState_Defaults);
            runTest("M9_07_SettingsViewModel_VolumeAndMute_PropagatesToAudioService", Test_M9_07_SettingsViewModel_VolumeAndMute_PropagatesToAudioService);
            runTest("M9_07_SettingsViewModel_Language_PropagatesToLocalizationManager", Test_M9_07_SettingsViewModel_Language_PropagatesToLocalizationManager);
            runTest("M9_07_SettingsViewModel_FullscreenAndHints_Toggle", Test_M9_07_SettingsViewModel_FullscreenAndHints_Toggle);
            runTest("M9_07_SettingsViewModel_ApplySettings_SavesToStorage", Test_M9_07_SettingsViewModel_ApplySettings_SavesToStorage);
            runTest("M9_07_SettingsViewModel_ResetToDefaults_RestoresValues", Test_M9_07_SettingsViewModel_ResetToDefaults_RestoresValues);
            runTest("M9_07_SettingsViewModel_Navigation_GoBackOrMainMenu", Test_M9_07_SettingsViewModel_Navigation_GoBackOrMainMenu);
            runTest("M9_07_Settings_ScreenRegistration_And_Navigation", Test_M9_07_Settings_ScreenRegistration_And_Navigation);

            runTest("M9_08_GoldenPath_FullDemoLoop_Integration", Test_M9_08_GoldenPath_FullDemoLoop_Integration);
        }

        private static void Test_M9_01_Navigator_CanRegisterAndNavigateAllNewScreens()
        {
            var nav = new ScreenNavigator();

            var glossary = new DummyScreen(ScreenId.Glossary);
            var attribution = new DummyScreen(ScreenId.Attribution);
            var eventPopup = new DummyScreen(ScreenId.EventPopup);
            var milestoneBoard = new DummyScreen(ScreenId.MilestoneBoard);
            var campaignEnd = new DummyScreen(ScreenId.CampaignEnd);

            nav.RegisterScreen(glossary);
            nav.RegisterScreen(attribution);
            nav.RegisterScreen(eventPopup);
            nav.RegisterScreen(milestoneBoard);
            nav.RegisterScreen(campaignEnd);

            nav.GoTo(ScreenId.Glossary);
            AssertTrue(nav.CurrentScreenId == ScreenId.Glossary, "Navigated to Glossary");
            AssertTrue(glossary.IsVisible, "Glossary is visible");

            nav.GoTo(ScreenId.Attribution);
            AssertTrue(nav.CurrentScreenId == ScreenId.Attribution, "Navigated to Attribution");
            AssertTrue(attribution.IsVisible, "Attribution is visible");
            AssertTrue(!glossary.IsVisible, "Glossary is hidden");

            nav.GoTo(ScreenId.EventPopup);
            AssertTrue(nav.CurrentScreenId == ScreenId.EventPopup, "Navigated to EventPopup");

            nav.GoTo(ScreenId.MilestoneBoard);
            AssertTrue(nav.CurrentScreenId == ScreenId.MilestoneBoard, "Navigated to MilestoneBoard");

            nav.GoTo(ScreenId.CampaignEnd);
            AssertTrue(nav.CurrentScreenId == ScreenId.CampaignEnd, "Navigated to CampaignEnd");
        }

        private static void Test_M9_01_MilestoneBoardViewModel_BindCampaign_UpdatesObservables()
        {
            var vm = new MilestoneBoardViewModel(null);
            AssertTrue(vm.MilestoneItems.Count == 0, "Initial items empty with null campaign");

            var campaign = new CampaignState(12345);
            var records = new List<MilestoneRecord>
            {
                new MilestoneRecord
                {
                    Id = "m1",
                    Title = "First Orbit",
                    Description = "Enter LEO",
                    OrderIndex = 1,
                    RewardBudget = 10,
                    RewardReputation = 5,
                    RewardKnowledgePoints = 2
                }
            };
            campaign.InitialiseMilestones(records);

            vm.BindCampaign(campaign);
            AssertTrue(vm.MilestoneItems.Count == 1, "Has 1 milestone item");
            AssertTrue(vm.MilestoneItems[0].Title == "First Orbit", "Milestone title matches");
            AssertTrue(vm.ActiveGoalTitleText.Value == "First Orbit", "Active goal title updated");
        }

        private static void Test_M9_01_CampaignEndViewModel_WithCampaignState_BindsObservables()
        {
            var campaign = new CampaignState(54321);
            campaign.AgencyName = "Test Space Agency";
            var cmd = new EndCampaignCommand(CampaignEndStatus.AllMilestonesCompleted);
            campaign.Execute(cmd);

            var vm = new CampaignEndViewModel(campaign);
            AssertTrue(vm.IsVictory.Value, "IsVictory is true");
            AssertTrue(vm.AgencyNameText.Value == "Test Space Agency", "Agency name matches");
            AssertTrue(vm.EndStatusBadgeText.Value.Contains("VICTORY") || vm.EndStatusBadgeText.Value.Contains("BAŞARI") || vm.EndStatusBadgeText.Value.Length > 0, "Badge text non-empty");
        }

        private static void Test_M9_01_EventPopupViewModel_InitializesCleanly()
        {
            var vm = new EventPopupViewModel();
            AssertTrue(!vm.HasPendingEvent.Value, "Initially no pending event");
            AssertTrue(vm.Options.Count == 0, "No options initially");
        }

        private static string GetJsonDir()
        {
            string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            if (!System.IO.Directory.Exists(path))
                path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            return path;
        }

        private static void Test_M9_02_CampaignFactory_CreateDefault_InitializesCampaignCorrectly()
        {
            var args = new CampaignSetupArgs
            {
                AgencyName = "Kozmik Ajans",
                StartingRegionId = "anatolian_plateau",
                Seed = 99999,
                StartingBudget = 750.0,
                StartingReputation = 45.0,
                CampaignId = "CAM-TEST99"
            };

            var campaign = CampaignFactory.CreateDefault(args);
            AssertTrue(campaign != null, "Campaign created");
            AssertTrue(campaign.AgencyName == "Kozmik Ajans", "Agency name matches");
            AssertTrue(campaign.SelectedRegionId == "anatolian_plateau", "Starting region matches");
            AssertTrue(campaign.Seed == 99999, "Seed matches");
            AssertTrue(campaign.Budget == 750.0, "Budget matches");
            AssertTrue(campaign.Reputation == 45.0, "Reputation matches");
            AssertTrue(campaign.CampaignId == "CAM-TEST99", "CampaignId matches");
            AssertTrue(campaign.Bases.Count == 3, "Has 3 starting base facilities");
            AssertTrue(campaign.Economy != null, "Economy manager initialized");
            AssertTrue(campaign.Production != null, "Production manager initialized");
            AssertTrue(campaign.Time != null, "Time system initialized");
        }

        private static void Test_M9_02_CampaignFactory_LoadCatalogsFromJson_WiresAllSubsystems()
        {
            var campaign = CampaignFactory.CreateDefault();

            string researchJson = "[{\"id\":\"res-test\",\"branch\":\"Propulsion\",\"nameKey\":\"Test Tech\"}]";
            string suppliersJson = "[{\"id\":\"sup-test\",\"name\":\"Test Corp\",\"category\":\"General\",\"reliability_rating\":0.95,\"cost_modifier\":1.0,\"speed_modifier\":1.0}]";
            string eventsJson = "[{\"id\":\"evt-test\",\"titleKey\":\"EVENT_TEST_TITLE\",\"descKey\":\"EVENT_TEST_DESC\"}]";
            string milestonesJson = "[{\"id\":\"ms-test\",\"title\":\"Test Milestone\",\"description\":\"Desc\",\"reward_budget\":100,\"reward_reputation\":10,\"reward_knowledge_points\":5,\"order_index\":1}]";

            CampaignFactory.LoadCatalogsFromJson(campaign, researchJson, suppliersJson, eventsJson, milestonesJson);

            AssertTrue(campaign.Research != null, "Research initialized");
            AssertTrue(campaign.Research.GetRecord("res-test") != null, "1 research node loaded");

            AssertTrue(campaign.Suppliers != null, "Suppliers initialized");
            AssertTrue(campaign.Suppliers.AllCompanies.Count == 1, "1 supplier loaded");

            AssertTrue(campaign.Events != null, "Events initialized");
            AssertTrue(campaign.Events.CatalogEvents.Count == 1, "1 event loaded");

            AssertTrue(campaign.Milestones != null, "Milestones initialized");
            AssertTrue(campaign.Milestones.AllMilestones.Count == 1, "1 milestone loaded");
        }

        private static void Test_M9_02_CampaignFactory_LoadCatalogsFromDirectory_LoadsFullGameCatalogs()
        {
            var campaign = CampaignFactory.CreateDefault();
            string jsonDir = GetJsonDir();
            CampaignFactory.LoadCatalogsFromDirectory(campaign, jsonDir);

            AssertTrue(campaign.Research != null, "Research initialized from directory");
            int researchCount = 0;
            foreach (var branch in campaign.Research.GetTreeByBranch().Values) researchCount += branch.Count;
            AssertTrue(researchCount >= 20, $"At least 20 research nodes (got {researchCount})");

            AssertTrue(campaign.Suppliers != null, "Suppliers initialized from directory");
            AssertTrue(campaign.Suppliers.AllCompanies.Count >= 5, $"At least 5 suppliers (got {campaign.Suppliers.AllCompanies.Count})");

            AssertTrue(campaign.Events != null, "Events initialized from directory");
            AssertTrue(campaign.Events.CatalogEvents.Count >= 10, $"At least 10 events (got {campaign.Events.CatalogEvents.Count})");

            AssertTrue(campaign.Milestones != null, "Milestones initialized from directory");
            AssertTrue(campaign.Milestones.AllMilestones.Count == 8, $"Exactly 8 official milestones (got {campaign.Milestones.AllMilestones.Count})");
        }

        private static void Test_M9_02_CampaignSaveAndRestore_RoundTrip_PreservesSubsystemState()
        {
            var campaign = CampaignFactory.CreateDefault(new CampaignSetupArgs
            {
                AgencyName = "Alpha Space",
                Seed = 1337
            }, GetJsonDir());

            campaign.Budget = 1234.5;
            campaign.Reputation = 88.0;

            string json = Apogee.Strategy.Persistence.CampaignSaveSerializer.ToJson(campaign);
            var loadedSaveData = Apogee.Strategy.Persistence.CampaignSaveSerializer.FromJson(json);

            var restoredCampaign = CampaignFactory.CreateDefault(new CampaignSetupArgs
            {
                Seed = loadedSaveData.Seed,
                CampaignId = loadedSaveData.CampaignId
            }, GetJsonDir());

            Apogee.Strategy.Persistence.CampaignSaveSerializer.RestoreFromSaveData(restoredCampaign, loadedSaveData);

            AssertTrue(restoredCampaign.AgencyName == "Alpha Space", "Agency name preserved");
            AssertTrue(restoredCampaign.Budget == 1234.5, "Budget preserved");
            AssertTrue(restoredCampaign.Reputation == 88.0, "Reputation preserved");
            AssertTrue(restoredCampaign.Milestones != null && restoredCampaign.Milestones.AllMilestones.Count == 8, "Milestones intact");
            AssertTrue(restoredCampaign.Research != null, "Research restored");
        }

        private class MockLifecycleHandler : ICampaignLifecycleHandler
        {
            public CampaignState CurrentCampaign { get; private set; }
            public bool IsInitialized => CurrentCampaign != null;
            public CampaignSetupArgs LastStartArgs { get; private set; }
            public string LastLoadJson { get; private set; }

            public CampaignState StartNewCampaign(CampaignSetupArgs args = null)
            {
                LastStartArgs = args;
                CurrentCampaign = CampaignFactory.CreateDefault(args);
                return CurrentCampaign;
            }

            public CampaignState LoadCampaign(string jsonSaveData)
            {
                LastLoadJson = jsonSaveData;
                var data = CampaignSaveSerializer.FromJson(jsonSaveData);
                CurrentCampaign = new CampaignState(data.Seed, data.CampaignId);
                CampaignSaveSerializer.RestoreFromSaveData(CurrentCampaign, data);
                return CurrentCampaign;
            }
        }

        private static void Test_M9_03_MainMenuViewModel_InitialState_Defaults()
        {
            var nav = new ScreenNavigator();
            var storage = new InMemoryCampaignSaveStorage();
            var vm = new MainMenuViewModel(nav, null, storage);

            AssertTrue(vm.Title.Value == "APOGEE", "Title is APOGEE");
            AssertTrue(!string.IsNullOrEmpty(vm.Subtitle.Value), "Subtitle present");
            AssertTrue(vm.AgencyNameInput.Value == "Birleşik Yörünge Ajansı", "Default agency name");
            AssertTrue(vm.SeedInput.Value == 42UL, "Default seed is 42");
            AssertTrue(vm.StartingBudget.Value == 500.0, "Starting budget is 500");
            AssertTrue(vm.StartingReputation.Value == 30.0, "Starting reputation is 30");
            AssertTrue(!vm.HasSaveGame.Value, "No save game initially");
            AssertTrue(string.IsNullOrEmpty(vm.SaveGameInfo.Value), "No save info initially");
            AssertTrue(vm.NewCampaignCommand != null, "NewCampaignCommand exists");
            AssertTrue(vm.ContinueCampaignCommand != null, "ContinueCampaignCommand exists");
            AssertTrue(vm.QuickMissionCommand != null, "QuickMissionCommand exists");
        }

        private static void Test_M9_03_MainMenuViewModel_NewCampaign_StartsAndNavigates()
        {
            var nav = new ScreenNavigator();
            var dummyMap = new DummyScreen(ScreenId.CampaignMap);
            nav.RegisterScreen(dummyMap);

            var lifecycle = new MockLifecycleHandler();
            var storage = new InMemoryCampaignSaveStorage();
            var vm = new MainMenuViewModel(nav, lifecycle, storage);

            vm.AgencyNameInput.Value = "Galactic Voyages";
            vm.SeedInput.Value = 9999UL;

            bool eventFired = false;
            vm.OnCampaignStarted += c => eventFired = (c != null && c.AgencyName == "Galactic Voyages");

            vm.NewCampaignCommand.Execute();

            AssertTrue(lifecycle.LastStartArgs != null, "Lifecycle received start args");
            AssertTrue(lifecycle.LastStartArgs.AgencyName == "Galactic Voyages", "Agency name passed");
            AssertTrue(lifecycle.LastStartArgs.Seed == 9999UL, "Seed passed");
            AssertTrue(eventFired, "OnCampaignStarted event fired");
            AssertTrue(nav.CurrentScreenId == ScreenId.CampaignMap, "Navigated to CampaignMap");
            AssertTrue(dummyMap.IsVisible, "CampaignMap is visible");
            AssertTrue(vm.StatusMessage.Value.Contains("Galactic Voyages"), "Status message updated");
        }

        private static void Test_M9_03_MainMenuViewModel_NewCampaign_FallbackToWorkshopWhenMapMissing()
        {
            var nav = new ScreenNavigator();
            var dummyWorkshop = new DummyScreen(ScreenId.Workshop);
            nav.RegisterScreen(dummyWorkshop);

            var lifecycle = new MockLifecycleHandler();
            var storage = new InMemoryCampaignSaveStorage();
            var vm = new MainMenuViewModel(nav, lifecycle, storage);

            vm.NewCampaignCommand.Execute();

            AssertTrue(nav.CurrentScreenId == ScreenId.Workshop, "Navigated to Workshop as fallback");
            AssertTrue(dummyWorkshop.IsVisible, "Workshop is visible");
        }

        private static void Test_M9_03_MainMenuViewModel_ContinueCampaign_LoadsFromStorage()
        {
            var campaign = CampaignFactory.CreateDefault(new CampaignSetupArgs
            {
                AgencyName = "Orion Horizons",
                Seed = 777
            });
            campaign.Budget = 850.0;
            string saveJson = CampaignSaveSerializer.ToJson(campaign);

            var nav = new ScreenNavigator();
            var dummyMap = new DummyScreen(ScreenId.CampaignMap);
            nav.RegisterScreen(dummyMap);

            var lifecycle = new MockLifecycleHandler();
            var storage = new InMemoryCampaignSaveStorage(saveJson);
            var vm = new MainMenuViewModel(nav, lifecycle, storage);

            AssertTrue(vm.HasSaveGame.Value, "Save game detected");
            AssertTrue(vm.SaveGameInfo.Value.Contains("Orion Horizons"), "Save game info has agency name");

            bool loadedEventFired = false;
            vm.OnCampaignLoaded += c => loadedEventFired = (c != null && c.AgencyName == "Orion Horizons");

            vm.ContinueCampaignCommand.Execute();

            AssertTrue(lifecycle.CurrentCampaign != null, "Campaign loaded");
            AssertTrue(lifecycle.CurrentCampaign.AgencyName == "Orion Horizons", "Agency name restored");
            AssertTrue(loadedEventFired, "OnCampaignLoaded event fired");
            AssertTrue(nav.CurrentScreenId == ScreenId.CampaignMap, "Navigated to CampaignMap");
            AssertTrue(vm.StatusMessage.Value.Contains("Orion Horizons"), "Status message reflects campaign loaded");
        }

        private static void Test_M9_03_MainMenuViewModel_ContinueCampaign_NoSaveFailsGracefully()
        {
            var nav = new ScreenNavigator();
            var dummyMap = new DummyScreen(ScreenId.CampaignMap);
            nav.RegisterScreen(dummyMap);

            var lifecycle = new MockLifecycleHandler();
            var storage = new InMemoryCampaignSaveStorage(null);
            var vm = new MainMenuViewModel(nav, lifecycle, storage);

            vm.ContinueCampaignCommand.Execute();

            AssertTrue(lifecycle.CurrentCampaign == null, "No campaign loaded");
            AssertTrue(nav.CurrentScreenId == ScreenId.None, "Did not navigate");
            AssertTrue(vm.StatusMessage.Value.Contains("Kayıtlı kampanya bulunamadı"), "Error status reported");
        }

        private static void Test_M9_03_MainMenuViewModel_NavigationCommands_DispatchScreens()
        {
            var nav = new ScreenNavigator();
            var workshop = new DummyScreen(ScreenId.Workshop);
            var glossary = new DummyScreen(ScreenId.Glossary);
            var attribution = new DummyScreen(ScreenId.Attribution);
            var settings = new DummyScreen(ScreenId.Settings);

            nav.RegisterScreen(workshop);
            nav.RegisterScreen(glossary);
            nav.RegisterScreen(attribution);
            nav.RegisterScreen(settings);

            var vm = new MainMenuViewModel(nav, null, new InMemoryCampaignSaveStorage());

            vm.QuickMissionCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.Workshop, "QuickMission navigated to Workshop");

            vm.OpenGlossaryCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.Glossary, "Navigated to Glossary");

            vm.OpenAttributionsCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.Attribution, "Navigated to Attribution");

            vm.OpenSettingsCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.Settings, "Navigated to Settings");

            bool quitFired = false;
            vm.OnQuitRequested += () => quitFired = true;
            vm.QuitCommand.Execute();
            AssertTrue(quitFired, "Quit event fired");
        }

        private static void Test_M9_03_MainMenuViewModel_RefreshSaveState_UpdatesObservables()
        {
            var storage = new InMemoryCampaignSaveStorage();
            var vm = new MainMenuViewModel(new ScreenNavigator(), null, storage);

            AssertTrue(!vm.HasSaveGame.Value, "Initially false");

            var campaign = CampaignFactory.CreateDefault(new CampaignSetupArgs { AgencyName = "Nova Corp" });
            campaign.Budget = 620.0;
            storage.Save(CampaignSaveSerializer.ToJson(campaign));

            vm.RefreshSaveState();

            AssertTrue(vm.HasSaveGame.Value, "Becomes true after save");
            AssertTrue(vm.SaveGameInfo.Value.Contains("Nova Corp"), "Contains agency name");
            AssertTrue(vm.SaveGameInfo.Value.Contains("620"), "Contains budget");
        }

        private static void Test_M9_03_MainMenuScreenController_CreateViewAndLifecycle()
        {
            var nav = new ScreenNavigator();
            var storage = new InMemoryCampaignSaveStorage();
            var vm = new MainMenuViewModel(nav, new MockLifecycleHandler(), storage);
            var controller = new MainMenuScreenController(vm);

            var parentContainer = new VisualElement();
            controller.Initialize(parentContainer);

            AssertTrue(controller.RootElement != null, "Root element created");
            AssertTrue(parentContainer.Children().Contains(controller.RootElement), "Root element added to container");
            AssertTrue(controller.ScreenId == ScreenId.MainMenu, "ScreenId matches MainMenu");

            controller.OnEnter();
            AssertTrue(controller.IsVisible, "Controller is visible on enter");

            controller.OnExit();
            AssertTrue(!controller.IsVisible, "Controller is hidden on exit");
        }

        private static IReadOnlyList<RegionRecord> LoadTestRegions()
        {
            string path = System.IO.Path.Combine(GetJsonDir(), "regions.json");
            string json = System.IO.File.ReadAllText(path);
            return CatalogLoader.Load<RegionRecord>(json).All;
        }

        private static void Test_M9_04_CampaignMapViewModel_InitialState_Defaults()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var regions = LoadTestRegions();
            var vm = new CampaignMapViewModel(nav, campaign, regions);

            AssertTrue(vm.Campaign != null, "Campaign state bound");
            AssertTrue(vm.Budget.Value == 500.0, "Initial budget is 500 M$");
            AssertTrue(vm.Reputation.Value == 30.0, "Initial reputation is 30");
            AssertTrue(vm.AgencyName.Value == "Birleşik Yörünge Ajansı", "Default agency name");
            AssertTrue(vm.ActiveLens.Value == MapLens.Overview, "Default lens is Overview");
            AssertTrue(vm.Regions.Value.Count == 8, "8 regions loaded");
            AssertTrue(vm.CurrentSpeed.Value == TimeSpeed.Paused, "Initial speed paused");
            AssertTrue(!string.IsNullOrEmpty(vm.DateDisplay.Value), "Date display non-empty");
        }

        private static void Test_M9_04_CampaignMapViewModel_SelectRegion_ShowsLensEvaluationAndBases()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var regions = LoadTestRegions();
            var vm = new CampaignMapViewModel(nav, campaign, regions);

            vm.SelectRegion("cape_peninsula");

            AssertTrue(vm.SelectedRegion.Value != null, "Region selected");
            AssertTrue(vm.SelectedRegion.Value.Id == "cape_peninsula", "Cape peninsula selected");
            AssertTrue(vm.SelectedRegionScore.Value > 0.0, "Lens score evaluated");
            AssertTrue(!string.IsNullOrEmpty(vm.SelectedRegionDescription.Value), "Description generated");
            AssertTrue(vm.SelectedRegion.Value.BaseCount == 3, "Cape peninsula has 3 starting bases");
            AssertTrue(vm.SelectedRegion.Value.HasLaunchPad, "Has launch pad");
            AssertTrue(vm.SelectedRegion.Value.HasFactory, "Has factory");
            AssertTrue(vm.SelectedRegion.Value.HasResearchCenter, "Has research center");
            AssertTrue(campaign.SelectedRegionId == "cape_peninsula", "CampaignState SelectedRegionId updated via CommandBus");
        }

        private static void Test_M9_04_CampaignMapViewModel_SetMapLens_DispatchesCommandAndUpdatesScores()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var regions = LoadTestRegions();
            var vm = new CampaignMapViewModel(nav, campaign, regions);

            vm.SelectRegion("guiana_coast");
            vm.SetMapLens(MapLens.LaunchEfficiency);

            AssertTrue(vm.ActiveLens.Value == MapLens.LaunchEfficiency, "Active lens is LaunchEfficiency");
            AssertTrue(campaign.ActiveMapLens == MapLens.LaunchEfficiency, "CampaignState updated via CommandBus");
            AssertTrue(vm.SelectedRegionScore.Value > 0.9, $"Guiana launch efficiency score is high ({vm.SelectedRegionScore.Value})");
            AssertTrue(vm.SelectedRegionDescription.Value.Contains("460"), "Description mentions +460 m/s");
        }

        private static void Test_M9_04_CampaignMapViewModel_TimeSpeedControls_DispatchesSetTimeSpeedCommand()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new CampaignMapViewModel(nav, campaign);

            vm.Speed1xCommand.Execute();
            AssertTrue(campaign.Time.Speed == TimeSpeed.Normal, "Speed 1x set");
            AssertTrue(vm.CurrentSpeed.Value == TimeSpeed.Normal, "Observable updated to Normal");

            vm.Speed2xCommand.Execute();
            AssertTrue(campaign.Time.Speed == TimeSpeed.Fast, "Speed 2x set");

            vm.Speed3xCommand.Execute();
            AssertTrue(campaign.Time.Speed == TimeSpeed.VeryFast, "Speed 3x set");

            vm.PauseCommand.Execute();
            AssertTrue(campaign.Time.Speed == TimeSpeed.Paused, "Paused set");
        }

        private static void Test_M9_04_CampaignMapViewModel_AdvanceDays_AdvancesClock()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new CampaignMapViewModel(nav, campaign);

            long startTick = campaign.Time.Tick;
            vm.AdvanceDayCommand.Execute();

            AssertTrue(campaign.Time.Tick == startTick + 1, "Clock advanced by 1 tick");
            AssertTrue(campaign.Time.ElapsedDays == 1, "Elapsed days is 1");
            AssertTrue(vm.DateDisplay.Value.Contains("Gün 1"), "Date display shows Gün 1");
        }

        private static void Test_M9_04_CampaignMapViewModel_NavigationCommands_DispatchScreens()
        {
            var nav = new ScreenNavigator();
            var composer = new DummyScreen(ScreenId.MissionComposer);
            var workshop = new DummyScreen(ScreenId.Workshop);
            var solarMap = new DummyScreen(ScreenId.SolarMap);
            var research = new DummyScreen(ScreenId.ResearchTree);
            var bases = new DummyScreen(ScreenId.BaseManagement);
            var production = new DummyScreen(ScreenId.ProductionQueue);
            var contracts = new DummyScreen(ScreenId.Contracts);
            var milestones = new DummyScreen(ScreenId.MilestoneBoard);
            var mainMenu = new DummyScreen(ScreenId.MainMenu);

            nav.RegisterScreen(composer);
            nav.RegisterScreen(workshop);
            nav.RegisterScreen(solarMap);
            nav.RegisterScreen(research);
            nav.RegisterScreen(bases);
            nav.RegisterScreen(production);
            nav.RegisterScreen(contracts);
            nav.RegisterScreen(milestones);
            nav.RegisterScreen(mainMenu);

            var vm = new CampaignMapViewModel(nav);

            vm.GoToComposerCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.MissionComposer, "Navigated to MissionComposer");

            vm.GoToWorkshopCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.Workshop, "Navigated to Workshop");

            vm.GoToSolarMapCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.SolarMap, "Navigated to SolarMap");

            vm.GoToResearchCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.ResearchTree, "Navigated to ResearchTree");

            vm.GoToBaseManagementCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.BaseManagement, "Navigated to BaseManagement");

            vm.GoToProductionCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.ProductionQueue, "Navigated to ProductionQueue");

            vm.GoToContractsCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.Contracts, "Navigated to Contracts");

            vm.GoToMilestonesCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.MilestoneBoard, "Navigated to MilestoneBoard");

            vm.GoToMainMenuCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.MainMenu, "Navigated to MainMenu");
        }

        private static void Test_M9_04_RegionMapItemViewModel_NormalizedCoordinates()
        {
            var regions = LoadTestRegions();
            foreach (var r in regions)
            {
                var item = new RegionMapItemViewModel(r);
                AssertTrue(item.NormalizedX >= 0f && item.NormalizedX <= 100f, $"{r.Id} NormalizedX in [0, 100]");
                AssertTrue(item.NormalizedY >= 0f && item.NormalizedY <= 100f, $"{r.Id} NormalizedY in [0, 100]");
                AssertTrue(!string.IsNullOrEmpty(item.Name), $"{r.Id} formatted name non-empty");
            }
        }

        private static void Test_M9_05_ResearchTreeViewModel_InitialState_And_NodeBinding()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            CampaignFactory.LoadCatalogsFromDirectory(campaign, GetJsonDir());
            var vm = new ResearchTreeViewModel(nav, campaign);

            AssertTrue(vm.Budget.Value == campaign.Budget, "Budget reflects campaign budget");
            AssertTrue(vm.VisibleNodes.Value.Count > 0, "Nodes are visible in research tree");
            AssertTrue(vm.Branches.Value.Count > 1, "Multiple research branches available");
            AssertTrue(vm.SelectedNode.Value != null, "Default selected node is set");
            AssertTrue(vm.ActiveResearchTitle.Value.Contains("Yok"), "No active research initially");

            vm.SelectBranch("Propulsion");
            AssertTrue(vm.SelectedBranch.Value == "Propulsion", "Propulsion branch selected");
        }

        private static void Test_M9_05_ResearchTreeViewModel_StartResearch_And_Cancel()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            CampaignFactory.LoadCatalogsFromDirectory(campaign, GetJsonDir());
            var vm = new ResearchTreeViewModel(nav, campaign);

            var available = vm.VisibleNodes.Value.FirstOrDefault(n => n.IsAvailable);
            AssertTrue(available != null, "Found available research node to start");

            vm.SelectNode(available.Id);
            AssertTrue(vm.SelectedNode.Value.Id == available.Id, "Selected node matches");

            bool started = vm.StartResearch(available.Id);
            AssertTrue(started, "Research successfully started");
            AssertTrue(campaign.Research.Queue.Count > 0, "Campaign research queue has active job");
            AssertTrue(vm.ActiveResearchTitle.Value.Contains("🔬"), "Active research title indicates in-progress research");

            bool cancelled = vm.CancelActiveResearch();
            AssertTrue(cancelled, "Research successfully cancelled");
            AssertTrue(campaign.Research.Queue.Count == 0, "Queue is empty after cancellation");
        }

        private static void Test_M9_05_BaseManagementViewModel_InitialState_And_UpgradeBase()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new BaseManagementViewModel(nav, campaign);

            AssertTrue(vm.Budget.Value == campaign.Budget, "Budget bound");
            AssertTrue(vm.Facilities.Value.Count == 3, $"Expected 3 default facilities, got {vm.Facilities.Value.Count}");
            AssertTrue(vm.TotalMonthlyMaintenance.Value > 0.0, "Total upkeep calculated");

            var capePad = vm.Facilities.Value.FirstOrDefault(f => f.Id == "base_cape_pad");
            AssertTrue(capePad != null, "Cape pad facility exists");
            AssertTrue(capePad.Level == 1, "Starting level is 1");
            AssertTrue(capePad.CanUpgrade, "Can upgrade level 1 pad");

            vm.SelectFacility("base_cape_pad");
            AssertTrue(vm.SelectedFacility.Value.Id == "base_cape_pad", "Selected facility set");

            double budgetBefore = campaign.Budget;
            bool upgraded = vm.UpgradeFacility("base_cape_pad");
            AssertTrue(upgraded, "Cape pad upgraded");
            AssertTrue(campaign.Bases["base_cape_pad"].Level == 2, "Level is now 2 in CampaignState");
            AssertTrue(campaign.Budget < budgetBefore, "Budget deducted for upgrade");
        }

        private static void Test_M9_05_BaseManagementViewModel_BuildBase_AddsFacility()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new BaseManagementViewModel(nav, campaign);

            int countBefore = campaign.Bases.Count;
            bool built = vm.BuildFacility("alrisha_plateau", BaseType.GroundStation);
            AssertTrue(built, "New ground station built");
            AssertTrue(campaign.Bases.Count == countBefore + 1, "Facility count increased by 1");
            AssertTrue(vm.Facilities.Value.Any(f => f.RegionId == "alrisha_plateau" && f.Type == BaseType.GroundStation), "New facility visible in list");
        }

        private static void Test_M9_05_ProductionQueueViewModel_InitialState_And_StartTesting()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new ProductionQueueViewModel(nav, campaign);

            AssertTrue(vm.Budget.Value == campaign.Budget, "Budget bound");
            AssertTrue(vm.FactoryCapacityText.Value.Contains("/"), "Factory capacity text formatted");
            AssertTrue(vm.TestCapacityText.Value.Contains("/"), "Test capacity text formatted");

            // Test vehicle assembly (factory capacity exists)
            bool vehicleStarted = vm.StartQuickVehicleProduction("Delta-Test");
            AssertTrue(vehicleStarted, "Vehicle assembly started");
            AssertTrue(vm.Jobs.Value.Any(j => j.JobType == ProductionJobType.VehicleAssembly), "Assembly job visible in VM Jobs list");

            // Also test qualification testing by first adding a Test Facility
            campaign.Execute(new BuildBaseCommand("base_test_facility", "cape_peninsula", BaseType.TestFacility));
            bool testStarted = vm.StartTesting("TEST-JOB-001", "Vakum Motor Testi", null, durationDays: 14, costMUSD: 5.0, reliabilityBonus: 0.03);
            AssertTrue(testStarted, "Testing job started after building test facility");
            AssertTrue(vm.Jobs.Value.Any(j => j.Id == "TEST-JOB-001"), "Job visible in VM Jobs list");

            bool cancelled = vm.CancelJob("TEST-JOB-001");
            AssertTrue(cancelled, "Job cancelled");
        }

        private static void Test_M9_05_ContractsViewModel_InitialState_And_SignContract()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            CampaignFactory.LoadCatalogsFromDirectory(campaign, GetJsonDir());
            var vm = new ContractsViewModel(nav, campaign);

            AssertTrue(vm.Budget.Value == campaign.Budget, "Budget bound");
            AssertTrue(vm.Companies.Value.Count > 0, "Suppliers bound");
            AssertTrue(vm.AvailableOffers.Value.Count > 0, "Offers generated");

            var offer = vm.AvailableOffers.Value.First();
            double budgetBefore = campaign.Budget;
            bool signed = vm.SignContract(offer);
            AssertTrue(signed, "Offer signed");
            AssertTrue(vm.Contracts.Value.Count > 0, "Contract added to active contracts");
            AssertTrue(campaign.Budget < budgetBefore, "Budget deducted for contract");

            var signedContract = vm.Contracts.Value.First();
            bool cancelled = vm.CancelContract(signedContract.Id);
            AssertTrue(cancelled, "Contract cancelled");
        }

        private static void Test_M9_06_SolarMapViewModel_InitialState_And_BodyBinding()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new SolarMapViewModel(nav, campaign);

            AssertTrue(vm.Bodies.Value.Count >= 5, $"Expected at least 5 celestial bodies, got {vm.Bodies.Value.Count}");
            AssertTrue(vm.SelectedBody.Value != null, "Default selected body not null");
            AssertTrue(vm.BriefingCard.Value != null, "Default briefing card generated");
            AssertTrue(vm.Budget.Value == campaign.Budget, "Budget bound to campaign");
            AssertTrue(vm.DateDisplay.Value.Contains("2031"), "Date display formatted with epoch year");
            AssertTrue(vm.Bodies.Value.Any(b => b.Id == "mars"), "Mars exists in solar system map");
            AssertTrue(vm.Bodies.Value.Any(b => b.Id == "moon"), "Moon exists in solar system map");
            AssertTrue(vm.Bodies.Value.Any(b => b.Id == "venus"), "Venus exists in solar system map");
        }

        private static void Test_M9_06_SolarMapViewModel_SelectDestination_UpdatesBriefingCardAndWindows()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new SolarMapViewModel(nav, campaign);

            vm.SelectBody("mars");
            AssertTrue(vm.SelectedBody.Value.Id == "mars", "Mars selected");
            AssertTrue(vm.BriefingCard.Value.DestinationId == "mars", "Briefing card is for Mars");
            AssertTrue(vm.BriefingCard.Value.RequiredDeltaVMps > 15000, "Mars requires substantial Delta-V");
            AssertTrue(!string.IsNullOrEmpty(vm.SelectedBody.Value.WindowStatusText), "Window status non-empty");

            vm.SelectBody("venus");
            AssertTrue(vm.SelectedBody.Value.Id == "venus", "Venus selected");
            AssertTrue(vm.BriefingCard.Value.DestinationId == "venus", "Briefing card is for Venus");
            AssertTrue(vm.SelectedBody.Value.SemiMajorAxisAu == PlanetaryOrbitCalculator.VenusDistanceAu, "Venus AU matches");
        }

        private static void Test_M9_06_SolarMapViewModel_AdvanceDays_UpdatesOrbitalPositions()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new SolarMapViewModel(nav, campaign);

            vm.SelectBody("mars");
            double initialAngle = vm.SelectedBody.Value.CurrentAngleRad;
            float initialX = vm.SelectedBody.Value.NormalizedX;

            vm.AdvanceDays(150);

            AssertTrue(vm.SelectedBody.Value.CurrentAngleRad != initialAngle, "Mars orbital angle updated after 150 days");
            AssertTrue(vm.SelectedBody.Value.NormalizedX != initialX, "Mars map coordinates updated after 150 days");
            AssertTrue(vm.DateDisplay.Value.Contains("Gün 150"), "Date display updated to day 150");
        }

        private static void Test_M9_06_SolarMapViewModel_ActiveMissionMarkers_Interpolation()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();

            // Commission a cruise mission
            var bp = new Blueprint("bp-ares", "Ares-Test");
            var mission = new MissionInstance("MSN-MARS-01", "Ares Explorer", "mars", bp, cruiseDurationDays: 200, missionSeed: 12345)
            {
                Status = MissionStatus.InCruise,
                CruiseProgress = 0.50
            };
            campaign.Missions["MSN-MARS-01"] = mission;

            var vm = new SolarMapViewModel(nav, campaign);

            AssertTrue(vm.ActiveMissions.Value.Count == 1, "1 active mission found in flight");
            var marker = vm.ActiveMissions.Value[0];
            AssertTrue(marker.MissionId == "MSN-MARS-01", "Marker mission ID matches");
            AssertTrue(marker.ProgressPercent == "%50", "Progress percentage formatted");
            AssertTrue(marker.NormalizedX > 0f && marker.NormalizedX < 100f, "Marker X coordinate within bounds");
            AssertTrue(marker.NormalizedY > 0f && marker.NormalizedY < 100f, "Marker Y coordinate within bounds");

            vm.SelectMission("MSN-MARS-01");
            AssertTrue(vm.SelectedMission.Value.MissionId == "MSN-MARS-01", "Mission selected");
        }

        private static void Test_M9_06_SolarMapViewModel_NavigationCommands()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            var vm = new SolarMapViewModel(nav, campaign);

            var campaignMapScreen = new DummyScreen(ScreenId.CampaignMap);
            var composerScreen = new DummyScreen(ScreenId.MissionComposer);
            var workshopScreen = new DummyScreen(ScreenId.Workshop);
            var mainMenuScreen = new DummyScreen(ScreenId.MainMenu);

            nav.RegisterScreen(campaignMapScreen);
            nav.RegisterScreen(composerScreen);
            nav.RegisterScreen(workshopScreen);
            nav.RegisterScreen(mainMenuScreen);

            vm.GoToCampaignMapCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.CampaignMap, "Navigated to CampaignMap");

            vm.GoToComposerCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.MissionComposer, "Navigated to MissionComposer");

            vm.GoToWorkshopCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.Workshop, "Navigated to Workshop");

            vm.GoToMainMenuCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.MainMenu, "Navigated to MainMenu");

            vm.SelectBody("mars");
            vm.LaunchToSelectedDestinationCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.MissionComposer, "Launched design to MissionComposer");
            AssertTrue((string)composerScreen.LastContext == "mars", "Composer received selected target 'mars'");
        }

        private static void Test_M9_07_SettingsViewModel_InitialState_Defaults()
        {
            var nav = new ScreenNavigator();
            var audio = new NullAudioService();
            var loc = new LocalizationManager();
            var storage = new InMemorySettingsStorage();

            var vm = new SettingsViewModel(nav, audio, loc, storage);

            AssertTrue(Math.Abs(vm.MasterVolume.Value - 1.0f) < 0.001f, "Default master volume is 1.0");
            AssertTrue(Math.Abs(vm.MusicVolume.Value - 0.75f) < 0.001f, "Default music volume is 0.75");
            AssertTrue(Math.Abs(vm.SfxVolume.Value - 0.85f) < 0.001f, "Default sfx volume is 0.85");
            AssertTrue(Math.Abs(vm.UiVolume.Value - 0.90f) < 0.001f, "Default ui volume is 0.90");
            AssertTrue(!vm.IsMuted.Value, "Default is not muted");
            AssertTrue(vm.CurrentLanguage.Value == Language.TR, "Default language is TR");
            AssertTrue(vm.IsFullscreen.Value, "Default is fullscreen");
            AssertTrue(vm.ShowHints.Value, "Default show hints is true");
            AssertTrue(!vm.IsDirty.Value, "Initial dirty state is false");
        }

        private static void Test_M9_07_SettingsViewModel_VolumeAndMute_PropagatesToAudioService()
        {
            var nav = new ScreenNavigator();
            var audio = new NullAudioService();
            var loc = new LocalizationManager();
            var storage = new InMemorySettingsStorage();

            var vm = new SettingsViewModel(nav, audio, loc, storage);

            vm.SetMasterVolume(0.50f);
            vm.SetMusicVolume(0.60f);
            vm.SetSfxVolume(0.70f);
            vm.SetUiVolume(0.40f);
            vm.SetMuted(true);

            AssertTrue(vm.IsDirty.Value, "ViewModel marked dirty after adjustments");
            AssertTrue(Math.Abs(audio.Settings.MasterVolume - 0.50f) < 0.001f, "AudioService MasterVolume updated");
            AssertTrue(Math.Abs(audio.Settings.MusicVolume - 0.60f) < 0.001f, "AudioService MusicVolume updated");
            AssertTrue(Math.Abs(audio.Settings.SfxVolume - 0.70f) < 0.001f, "AudioService SfxVolume updated");
            AssertTrue(Math.Abs(audio.Settings.UiVolume - 0.40f) < 0.001f, "AudioService UiVolume updated");
            AssertTrue(audio.Settings.IsMuted, "AudioService IsMuted is true");

            vm.ToggleMute();
            AssertTrue(!vm.IsMuted.Value, "Mute toggled back to false");
            AssertTrue(!audio.Settings.IsMuted, "AudioService IsMuted is false");
        }

        private static void Test_M9_07_SettingsViewModel_Language_PropagatesToLocalizationManager()
        {
            var nav = new ScreenNavigator();
            var audio = new NullAudioService();
            var loc = new LocalizationManager();
            var storage = new InMemorySettingsStorage();

            var vm = new SettingsViewModel(nav, audio, loc, storage);

            bool eventFired = false;
            loc.LanguageChanged += lang => eventFired = true;

            vm.SetLanguage(Language.EN);
            AssertTrue(vm.CurrentLanguage.Value == Language.EN, "CurrentLanguage observable updated to EN");
            AssertTrue(loc.CurrentLanguage == Language.EN, "LocalizationManager updated to EN");
            AssertTrue(eventFired, "LanguageChanged event fired");

            vm.SetLanguage(Language.TR);
            AssertTrue(vm.CurrentLanguage.Value == Language.TR, "CurrentLanguage observable restored to TR");
            AssertTrue(loc.CurrentLanguage == Language.TR, "LocalizationManager restored to TR");
        }

        private static void Test_M9_07_SettingsViewModel_FullscreenAndHints_Toggle()
        {
            var nav = new ScreenNavigator();
            var audio = new NullAudioService();
            var loc = new LocalizationManager();
            var storage = new InMemorySettingsStorage();

            bool capturedFullscreen = true;
            var vm = new SettingsViewModel(nav, audio, loc, storage, fs => capturedFullscreen = fs);

            vm.ToggleFullscreen();
            AssertTrue(!vm.IsFullscreen.Value, "Fullscreen toggled to false");
            AssertTrue(!capturedFullscreen, "Fullscreen callback invoked with false");

            vm.ToggleShowHints();
            AssertTrue(!vm.ShowHints.Value, "ShowHints toggled to false");

            vm.SetShowHints(true);
            AssertTrue(vm.ShowHints.Value, "ShowHints set back to true");
        }

        private static void Test_M9_07_SettingsViewModel_ApplySettings_SavesToStorage()
        {
            var nav = new ScreenNavigator();
            var audio = new NullAudioService();
            var loc = new LocalizationManager();
            var storage = new InMemorySettingsStorage();

            var vm = new SettingsViewModel(nav, audio, loc, storage);

            vm.SetMasterVolume(0.42f);
            vm.SetLanguage(Language.EN);
            vm.SetFullscreen(false);
            vm.SetShowHints(false);

            AssertTrue(vm.IsDirty.Value, "ViewModel is dirty before Apply");
            vm.ApplySettingsCommand.Execute();

            AssertTrue(!vm.IsDirty.Value, "ViewModel is no longer dirty after Apply");

            var saved = storage.LoadSettings();
            AssertTrue(Math.Abs(saved.MasterVolume - 0.42f) < 0.001f, "Saved master volume matches");
            AssertTrue(saved.Language == Language.EN, "Saved language is EN");
            AssertTrue(!saved.IsFullscreen, "Saved fullscreen is false");
            AssertTrue(!saved.ShowHints, "Saved show hints is false");
        }

        private static void Test_M9_07_SettingsViewModel_ResetToDefaults_RestoresValues()
        {
            var nav = new ScreenNavigator();
            var audio = new NullAudioService();
            var loc = new LocalizationManager();
            var storage = new InMemorySettingsStorage();

            var vm = new SettingsViewModel(nav, audio, loc, storage);

            vm.SetMasterVolume(0.10f);
            vm.SetMusicVolume(0.20f);
            vm.SetLanguage(Language.EN);
            vm.SetFullscreen(false);
            vm.SetShowHints(false);
            vm.ApplySettings();

            vm.ResetDefaultsCommand.Execute();

            AssertTrue(Math.Abs(vm.MasterVolume.Value - GameSettingsData.DefaultMasterVolume) < 0.001f, "Master reset");
            AssertTrue(Math.Abs(vm.MusicVolume.Value - GameSettingsData.DefaultMusicVolume) < 0.001f, "Music reset");
            AssertTrue(vm.CurrentLanguage.Value == GameSettingsData.DefaultLanguage, "Language reset to TR");
            AssertTrue(vm.IsFullscreen.Value == GameSettingsData.DefaultIsFullscreen, "Fullscreen reset");
            AssertTrue(vm.ShowHints.Value == GameSettingsData.DefaultShowHints, "ShowHints reset");

            var reloaded = storage.LoadSettings();
            AssertTrue(Math.Abs(reloaded.MasterVolume - GameSettingsData.DefaultMasterVolume) < 0.001f, "Persisted settings reset");
        }

        private static void Test_M9_07_SettingsViewModel_Navigation_GoBackOrMainMenu()
        {
            var nav = new ScreenNavigator();
            var audio = new NullAudioService();
            var loc = new LocalizationManager();
            var storage = new InMemorySettingsStorage();

            var mainMenuScreen = new DummyScreen(ScreenId.MainMenu);
            var settingsScreen = new DummyScreen(ScreenId.Settings);
            var campaignMapScreen = new DummyScreen(ScreenId.CampaignMap);

            nav.RegisterScreen(mainMenuScreen);
            nav.RegisterScreen(settingsScreen);
            nav.RegisterScreen(campaignMapScreen);

            var vm = new SettingsViewModel(nav, audio, loc, storage);

            // Case A: With back stack
            nav.GoTo(ScreenId.CampaignMap);
            nav.GoTo(ScreenId.Settings);
            AssertTrue(nav.CurrentScreenId == ScreenId.Settings, "Navigated to Settings");
            AssertTrue(nav.CanGoBack, "CanGoBack is true");

            vm.CloseCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.CampaignMap, "Popped back to CampaignMap");

            // Case B: No back stack fallback to MainMenu
            nav.GoTo(ScreenId.Settings, addToHistory: false);
            nav.ClearHistory();
            AssertTrue(!nav.CanGoBack, "CanGoBack is false");

            vm.CloseCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.MainMenu, "Fell back to MainMenu when no back history");
        }

        private static void Test_M9_07_Settings_ScreenRegistration_And_Navigation()
        {
            var nav = new ScreenNavigator();
            var settingsScreen = new DummyScreen(ScreenId.Settings);
            nav.RegisterScreen(settingsScreen);

            bool navigated = nav.GoTo(ScreenId.Settings);
            AssertTrue(navigated, "Successfully navigated to registered Settings screen");
            AssertTrue(nav.CurrentScreenId == ScreenId.Settings, "Current screen is Settings");
            AssertTrue(nav.CurrentScreen == settingsScreen, "CurrentScreen is settingsScreen");
            AssertTrue(settingsScreen.IsVisible, "Settings screen is visible");
        }

        private static void Test_M9_08_GoldenPath_FullDemoLoop_Integration()
        {
            var nav = new ScreenNavigator();
            var campaign = CampaignFactory.CreateDefault();
            CampaignFactory.LoadCatalogsFromDirectory(campaign, GetJsonDir());

            // Register dummy screens for full routing
            var mainMenuScreen    = new DummyScreen(ScreenId.MainMenu);
            var campaignMapScreen = new DummyScreen(ScreenId.CampaignMap);
            var composerScreen    = new DummyScreen(ScreenId.MissionComposer);
            var workshopScreen    = new DummyScreen(ScreenId.Workshop);
            var reviewScreen      = new DummyScreen(ScreenId.DesignReview);
            var launchScreen      = new DummyScreen(ScreenId.LaunchSequence);
            var operationsScreen  = new DummyScreen(ScreenId.Operations);
            var reportScreen      = new DummyScreen(ScreenId.MissionReport);
            var attributionScreen = new DummyScreen(ScreenId.Attribution);
            var settingsScreen    = new DummyScreen(ScreenId.Settings);

            nav.RegisterScreen(mainMenuScreen);
            nav.RegisterScreen(campaignMapScreen);
            nav.RegisterScreen(composerScreen);
            nav.RegisterScreen(workshopScreen);
            nav.RegisterScreen(reviewScreen);
            nav.RegisterScreen(launchScreen);
            nav.RegisterScreen(operationsScreen);
            nav.RegisterScreen(reportScreen);
            nav.RegisterScreen(attributionScreen);
            nav.RegisterScreen(settingsScreen);

            // Step 1: Main Menu & New Campaign
            nav.GoTo(ScreenId.MainMenu);
            AssertTrue(nav.CurrentScreenId == ScreenId.MainMenu, "App starts on MainMenu");

            var mockLifecycle = new MockLifecycleHandler();
            var mainMenuVm = new MainMenuViewModel(nav, mockLifecycle, new InMemoryCampaignSaveStorage());
            mainMenuVm.AgencyNameInput.Value = "Birleşik Yörünge Ajansı";
            mainMenuVm.SeedInput.Value = 42UL;
            mainMenuVm.NewCampaignCommand.Execute();

            AssertTrue(nav.CurrentScreenId == ScreenId.CampaignMap, "Transitioned to CampaignMap");
            AssertTrue(mockLifecycle.CurrentCampaign != null, "Campaign started");
            AssertTrue(mockLifecycle.CurrentCampaign.Budget == 500.0, "Campaign started with initial budget 500 M$");

            // Step 2: Mission Composer (Moon Rover)
            nav.GoTo(ScreenId.MissionComposer, "moon");
            var profile = LunarRoverVerticalSlice.CreateMissionProfile();
            AssertTrue(profile != null, "Mission profile created");
            AssertTrue(profile.TargetDestinationId == "moon", "Target destination is moon");
            AssertTrue(profile.TargetMissionTypeId == "rover", "Target mission type is rover");

            // Step 3: Workshop — Modular Rocket & Rover Assembly
            nav.GoTo(ScreenId.Workshop, profile);
            var blueprint = LunarRoverVerticalSlice.CreatePassingBlueprint();
            var valContext = LunarRoverVerticalSlice.CreateValidationContext();
            var report = BlueprintValidator.Validate(blueprint, valContext);
            AssertTrue(report.IsValid, "Blueprint passed all validation checks — GO FOR LAUNCH");

            // Step 4: Design Review & Launch Sequence
            var reviewArgs = new ReviewNavigationArgs(blueprint, valContext);
            nav.GoTo(ScreenId.DesignReview, reviewArgs);
            var reviewVm = new ReviewViewModel(nav, blueprint, valContext);
            AssertTrue(reviewVm.IsValid.Value, "Review indicates GO");

            reviewVm.ProceedToLaunchCommand.Execute();
            AssertTrue(nav.CurrentScreenId == ScreenId.LaunchSequence, "Transitioned to LaunchSequence");

            var launchParams = new LaunchSimulationParameters(
                missionId: "MSN-GOLDEN",
                vehicleName: blueprint.Name,
                stageCount: 2,
                stageReliabilities: new[] { 1.0, 1.0 },
                overallReliability: 1.0,
                launchPadWeatherRisk: 0.0,
                seed: 42UL);
            var launchSimResult = LaunchSimulator.Simulate(launchParams);
            AssertTrue(launchSimResult.IsSuccessful, "Launch succeeded into orbit");

            // Step 5: Operations — Cruise, Anomaly Decision & EDL Landing
            var missionInstance = new MissionInstance("MSN-GOLDEN", "Artemis Pathfinder", "moon", blueprint, cruiseDurationDays: 3, missionSeed: 42)
            {
                Status = MissionStatus.InCruise,
                CruiseProgress = 0.50
            };
            campaign.Missions[missionInstance.MissionId] = missionInstance;
            nav.GoTo(ScreenId.Operations, missionInstance);

            campaign.Execute(new AdvanceCruiseCommand(campaign, missionInstance.MissionId));
            AssertTrue(missionInstance.CruiseProgress > 0.80, "Cruise advanced towards Moon");

            var edlParams = new EdlSimulationParameters(
                missionId: missionInstance.MissionId,
                destinationId: "moon",
                hasAtmosphere: false,
                atmospherePressureKpa: 0.0,
                hasHeatShield: false,
                heatShieldTier: "none",
                hasParachute: false,
                hasSkycraneOrRetro: true,
                availableLandingDv: 2000.0,
                requiredLandingDv: 1700.0,
                spacecraftReliability: 0.98,
                edlSystemReliability: 0.98,
                isCrewed: false,
                seed: 42UL);
            var edlSimResult = EdlSimulator.Simulate(edlParams);
            AssertTrue(edlSimResult.Outcome == EdlOutcome.Success, "EDL soft landing succeeded on lunar surface");
            missionInstance.Status = MissionStatus.Operating;

            campaign.Execute(new ExecuteScienceTurnCommand(campaign, missionInstance.MissionId));
            AssertTrue(missionInstance.AccumulatedDownlinkDataGb > 0.0, "Science telemetry downlinked");

            // Step 6: Mission Report, What-If Analysis & Attributions
            var eventRecord = new OperationEventRecord(
                eventId: "op_solar_align",
                titleKey: "Güneş Paneli Uyarısı",
                narrativeKey: "Hizalama optimizasyonu",
                phase: OperationEventPhase.Cruise,
                baseWeight: 10.0,
                options: new[]
                {
                    new OperationEventOption("opt_safe", "Güvenli Mod", "Hız azalır, veri korunur", "SAFE", budgetDelta: 0, reliabilityPenalty: 0.0, scienceDataDeltaGb: -1.0),
                    new OperationEventOption("opt_aggr", "İtki Desteği", "Zaman kazanılır", "FAST", budgetDelta: -1.0, reliabilityPenalty: 0.01, scienceDataDeltaGb: 2.0)
                });
            var activeEvent = new ActiveOperationEvent("evt_golden_1", eventRecord, OperationEventPhase.Cruise, 1)
            {
                ChosenOptionId = "opt_safe"
            };
            missionInstance.OperationEventLog.Add(activeEvent);

            var missionReport = MissionReportEvaluator.Evaluate(
                missionId: missionInstance.MissionId,
                missionName: missionInstance.MissionName,
                destinationId: "moon",
                profile: profile,
                blueprint: blueprint,
                launchResult: launchSimResult,
                edlResult: edlSimResult,
                downlinkedGb: missionInstance.AccumulatedDownlinkDataGb,
                rawGeneratedGb: missionInstance.AccumulatedRawDataGeneratedGb,
                droppedGb: missionInstance.AccumulatedDroppedDataGb,
                scienceDaysCompleted: 3,
                accumulatedReliabilityPenalty: 0.0,
                accumulatedCorrectionFuel: 0.0,
                operationEvents: missionInstance.OperationEventLog);

            missionInstance.Report = missionReport;
            campaign.Execute(new CompleteMissionCommand(campaign, missionInstance.MissionId));

            var reportArgs = new MissionReportNavigationArgs(missionInstance.MissionId, missionReport);
            nav.GoTo(ScreenId.MissionReport, reportArgs);
            AssertTrue(nav.CurrentScreenId == ScreenId.MissionReport, "Navigated to MissionReport");
            AssertTrue(missionReport.ScoreBreakdown.FinalScore > 50.0, "Final mission score is calculated (>50)");

            var whatIfComparison = WhatIfSimulator.Simulate(
                missionId: missionInstance.MissionId,
                missionName: missionInstance.MissionName,
                destinationId: "moon",
                profile: profile,
                blueprint: blueprint,
                launchResult: launchSimResult,
                edlResult: edlSimResult,
                downlinkedGb: missionInstance.AccumulatedDownlinkDataGb,
                rawGeneratedGb: missionInstance.AccumulatedRawDataGeneratedGb,
                droppedGb: missionInstance.AccumulatedDroppedDataGb,
                scienceDaysCompleted: 3,
                accumulatedReliabilityPenalty: 0.0,
                accumulatedCorrectionFuel: 0.0,
                operationEventLog: missionInstance.OperationEventLog,
                targetEvent: activeEvent,
                alternativeOptionId: "opt_aggr",
                originalReport: missionReport);

            AssertTrue(whatIfComparison != null && whatIfComparison.WhatIfReport != null, "What-If counterfactual scenario generated");

            nav.GoTo(ScreenId.Attribution);
            AssertTrue(nav.CurrentScreenId == ScreenId.Attribution, "Navigated to Attribution");

            nav.GoTo(ScreenId.Settings);
            AssertTrue(nav.CurrentScreenId == ScreenId.Settings, "Navigated to Settings");

            nav.GoTo(ScreenId.MainMenu);
            AssertTrue(nav.CurrentScreenId == ScreenId.MainMenu, "Returned cleanly to MainMenu");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion Failed: {message}");
            }
        }
    }
}
