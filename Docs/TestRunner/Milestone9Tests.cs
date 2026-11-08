using System;
using System.Collections.Generic;
using Apogee.Core.Attribution;
using Apogee.Core.Education;
using Apogee.Data;
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

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion Failed: {message}");
            }
        }
    }
}
