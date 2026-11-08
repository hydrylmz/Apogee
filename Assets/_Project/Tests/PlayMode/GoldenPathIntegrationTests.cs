using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Apogee.Core.Audio;
using Apogee.Core.Localization;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Report;
using Apogee.Data;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Campaign;
using Apogee.Strategy.Mission;
using Apogee.Core.Simulation.Operations;
using Apogee.Strategy.Persistence;
using Apogee.UI.Bootstrap;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Managers;
using Apogee.UI.Screens.MainMenu;
using Apogee.UI.Screens.Composer;
using Apogee.UI.Screens.Workshop;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Launch;
using Apogee.UI.Screens.Operations;
using Apogee.UI.Screens.Report;
using Apogee.UI.Screens.Settings;
using Apogee.UI.Screens.Attribution;

namespace Apogee.Tests.PlayMode
{
    /// <summary>
    /// Play Mode Integration Test verifying the complete 3-minute "Golden Path" demo loop (GDD §20, DEMO_GUIDE.md, M9-08).
    /// Covers: MainMenu → CampaignMap → Composer (Moon Rover) → Workshop (Assembly) → Review → Launch → Cruise & Decision → EDL → Science → Report & What-If → Attributions.
    /// </summary>
    public class GoldenPathIntegrationTests
    {
        private GameObject _uiHost;
        private UIDocument _uiDocument;
        private ApogeeUIManager _uiManager;
        private CampaignBootstrapper _bootstrapper;
        private IScreenNavigator _navigator;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _uiHost = new GameObject("Test_GoldenPath_Host");
            _uiDocument = _uiHost.AddComponent<UIDocument>();
            _bootstrapper = _uiHost.AddComponent<CampaignBootstrapper>();
            _uiManager = _uiHost.AddComponent<ApogeeUIManager>();

            yield return null;

            _navigator = _uiManager.Navigator;
            Assert.IsNotNull(_navigator, "ScreenNavigator initialized");
            Assert.IsNotNull(_bootstrapper.CurrentCampaign, "Campaign bootstrapped on awake");
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_uiHost != null)
            {
                UnityEngine.Object.Destroy(_uiHost);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoldenPath_FullDemoLoop_SucceedsEndToEnd()
        {
            // ─────────────────────────────────────────────────────────────────
            // ⏱️ Step 1: Main Menu & New Campaign Initialization (00:00 - 00:30)
            // ─────────────────────────────────────────────────────────────────
            Assert.AreEqual(ScreenId.MainMenu, _navigator.CurrentScreenId, "App starts on MainMenu");

            var mainMenuVm = _uiManager.MainMenuController.ViewModel;
            Assert.IsNotNull(mainMenuVm, "MainMenuViewModel present");

            mainMenuVm.AgencyNameInput.Value = "Birleşik Yörünge Ajansı";
            mainMenuVm.SeedInput.Value = 42UL;
            mainMenuVm.NewCampaignCommand.Execute();

            yield return null;

            Assert.AreEqual(ScreenId.CampaignMap, _navigator.CurrentScreenId, "Navigated to CampaignMap after starting campaign");
            var campaign = _bootstrapper.CurrentCampaign;
            Assert.IsNotNull(campaign, "Campaign state created and active");
            Assert.AreEqual(500.0, campaign.Budget, "Initial budget is 500 M$");

            // ─────────────────────────────────────────────────────────────────
            // ⏱️ Step 2: Mission Composer — Moon Rover & Objectives (00:30 - 01:00)
            // ─────────────────────────────────────────────────────────────────
            _navigator.GoTo(ScreenId.MissionComposer, "moon");
            yield return null;

            Assert.AreEqual(ScreenId.MissionComposer, _navigator.CurrentScreenId, "Navigated to MissionComposer");
            var composerVm = _uiManager.ComposerController.ViewModel;
            Assert.IsNotNull(composerVm, "Composer ViewModel present");

            // Select Target: Moon, Type: Rover
            composerVm.SelectDestination("moon");
            composerVm.SelectMissionType("rover");

            // Select Scientific Objectives: Polar Ice and Surface Mapping
            composerVm.ToggleObjective("moon_polar_ice");
            composerVm.ToggleObjective("moon_surface_mapping");

            Assert.IsTrue(composerVm.SelectedObjectiveIds.Value.Contains("moon_polar_ice"), "Polar ice objective selected");
            Assert.IsTrue(composerVm.SelectedObjectiveIds.Value.Contains("moon_surface_mapping"), "Surface mapping objective selected");

            // Check claim level and derived Delta-V
            Assert.IsTrue(composerVm.TotalRequiredDeltaV.Value > 11000.0, $"Required Delta-V is substantial: {composerVm.TotalRequiredDeltaV.Value:F0} m/s");

            var profile = composerVm.ComposeMissionProfile();
            Assert.IsNotNull(profile, "Mission profile successfully composed");
            Assert.AreEqual("moon", profile.TargetBody);

            // ─────────────────────────────────────────────────────────────────
            // ⏱️ Step 3: Workshop — Modular Rocket & Rover Assembly (01:00 - 01:30)
            // ─────────────────────────────────────────────────────────────────
            _navigator.GoTo(ScreenId.Workshop, profile);
            yield return null;

            Assert.AreEqual(ScreenId.Workshop, _navigator.CurrentScreenId, "Navigated to Workshop");
            var workshopVm = _uiManager.WorkshopController.ViewModel;
            Assert.IsNotNull(workshopVm, "Workshop ViewModel present");

            // Mount passing components to turn NO-GO to GO
            workshopVm.Blueprint.Stage1.Engine = new DesignComponent("engine-kerolox-mk1", "Kerosen Motoru Mk-1", ComponentType.Engine, 3.5, 305, 12.0);
            workshopVm.Blueprint.Stage2.Engine = new DesignComponent("engine-hydrolox-upper", "Hidrojen Üst Aşama Motoru", ComponentType.Engine, 1.8, 440, 18.0);
            workshopVm.Blueprint.Payload.EdlSystem = new DesignComponent("edl-lunar-retro", "Ay İniş Retro Paketi", ComponentType.Edl, 0.45, 310, 8.0);

            // Re-evaluate design
            var valContext = new ValidationContext(profile, workshopVm.Blueprint, LaunchPadInfo.Level3Heavy);
            var report = BlueprintValidator.Validate(valContext);
            Assert.IsTrue(report.CanLaunch, "Blueprint passed all validation checks — GO FOR LAUNCH");

            // ─────────────────────────────────────────────────────────────────
            // ⏱️ Step 4: Design Review & Launch Sequence (01:30 - 02:00)
            // ─────────────────────────────────────────────────────────────────
            var reviewArgs = new ReviewNavigationArgs(workshopVm.Blueprint, valContext, report);
            _navigator.GoTo(ScreenId.DesignReview, reviewArgs);
            yield return null;

            Assert.AreEqual(ScreenId.DesignReview, _navigator.CurrentScreenId, "Navigated to DesignReview");
            var reviewVm = _uiManager.ReviewController.ViewModel;
            Assert.IsTrue(reviewVm.IsGoForLaunch.Value, "Review reports GO FOR LAUNCH");

            // Proceed to launch sequence
            reviewVm.ProceedToLaunchCommand.Execute();
            yield return null;

            Assert.AreEqual(ScreenId.LaunchSequence, _navigator.CurrentScreenId, "Navigated to LaunchSequence");
            var launchVm = _uiManager.LaunchController.ViewModel;
            Assert.IsNotNull(launchVm, "Launch Sequence ViewModel present");

            // Skip to end of countdown/ascent and verify orbital insertion
            launchVm.SkipToEndCommand.Execute();
            Assert.IsTrue(launchVm.IsSequenceFinished, "Launch sequence completed");
            Assert.IsTrue(launchVm.Outcome == LaunchOutcome.Success || launchVm.Outcome == LaunchOutcome.PartialOrbit, "Launch outcome is orbit");

            // ─────────────────────────────────────────────────────────────────
            // ⏱️ Step 5: Operations — Cruise, Decision Card & EDL Touchdown (02:00 - 02:30)
            // ─────────────────────────────────────────────────────────────────
            var missionInstance = new MissionInstance("MSN-MOON-GOLDEN", "Artemis Pathfinder", "moon", workshopVm.Blueprint, cruiseDurationDays: 3, missionSeed: 42)
            {
                Status = MissionStatus.InCruise,
                CruiseProgress = 0.50
            };
            campaign.Missions[missionInstance.Id] = missionInstance;

            _navigator.GoTo(ScreenId.Operations, missionInstance);
            yield return null;

            Assert.AreEqual(ScreenId.Operations, _navigator.CurrentScreenId, "Navigated to Operations");
            var opsVm = _uiManager.OperationsController.ViewModel;
            Assert.IsNotNull(opsVm, "Operations ViewModel present");

            // Advance cruise and resolve anomaly decision
            campaign.Execute(new AdvanceCruiseCommand(campaign, missionInstance.Id, daysToAdvance: 2));
            Assert.IsTrue(missionInstance.CruiseProgress > 0.80, "Cruise progressed towards Moon arrival");

            // Simulate EDL Touchdown
            var edlParams = new EdlSimulationParameters(
                missionId: missionInstance.Id,
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

            var edlResult = EdlSimulator.Simulate(edlParams);
            Assert.AreEqual(EdlOutcome.Success, edlResult.Outcome, "EDL touchdown succeeded with soft landing on lunar regolith");
            missionInstance.Status = MissionStatus.SurfaceOperational;

            // Execute science downlink
            campaign.Execute(new ExecuteScienceTurnCommand(campaign, missionInstance.Id, scienceYield: 150.0, dataDeliveredGb: 4.5));
            Assert.IsTrue(missionInstance.DataDeliveredGb > 0.0, "Science telemetry downlinked to Earth");

            // ─────────────────────────────────────────────────────────────────
            // ⏱️ Step 6: Mission Report, What-If Analysis & Attributions (02:30 - 03:00)
            // ─────────────────────────────────────────────────────────────────
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

            var reportLaunch = new LaunchSimulationResult(LaunchOutcome.Success, true, LaunchWeatherCondition.Clear, 0.05, 9500, "nominal", "Nominal Fırlatma");
            var originalReport = MissionReportEvaluator.Evaluate(
                missionId: missionInstance.Id,
                missionName: missionInstance.Name,
                destinationId: "moon",
                profile: profile,
                blueprint: workshopVm.Blueprint,
                launchResult: reportLaunch,
                edlResult: edlResult,
                downlinkedGb: missionInstance.DataDeliveredGb,
                rawGeneratedGb: missionInstance.DataDeliveredGb + 2.0,
                droppedGb: 0.5,
                scienceDaysCompleted: 3,
                accumulatedReliabilityPenalty: 0.0,
                accumulatedCorrectionFuel: 0.0,
                operationEvents: missionInstance.OperationEventLog);

            missionInstance.Report = originalReport;
            campaign.Execute(new CompleteMissionCommand(campaign, missionInstance.Id, originalReport));

            var reportArgs = new MissionReportNavigationArgs(missionInstance.Id, originalReport);
            _navigator.GoTo(ScreenId.MissionReport, reportArgs);
            yield return null;

            Assert.AreEqual(ScreenId.MissionReport, _navigator.CurrentScreenId, "Navigated to MissionReport");
            var reportVm = _uiManager.ReportController.ViewModel;
            Assert.IsNotNull(reportVm, "Mission Report ViewModel present");

            // Verify What-If counterfactual analysis
            var whatIfComparison = WhatIfSimulator.Simulate(
                missionId: missionInstance.Id,
                missionName: missionInstance.Name,
                destinationId: "moon",
                profile: profile,
                blueprint: workshopVm.Blueprint,
                launchResult: reportLaunch,
                edlResult: edlResult,
                downlinkedGb: missionInstance.DataDeliveredGb,
                rawGeneratedGb: missionInstance.DataDeliveredGb + 2.0,
                droppedGb: 0.5,
                scienceDaysCompleted: 3,
                accumulatedReliabilityPenalty: 0.0,
                accumulatedCorrectionFuel: 0.0,
                operationEventLog: missionInstance.OperationEventLog,
                targetEvent: activeEvent,
                alternativeOptionId: "opt_aggr",
                originalReport: originalReport);

            Assert.IsNotNull(whatIfComparison, "What-If comparison calculated");
            Assert.IsNotNull(whatIfComparison.CounterfactualReport, "Counterfactual report generated");

            // Navigate to NASA Attributions
            _navigator.GoTo(ScreenId.Attribution);
            yield return null;
            Assert.AreEqual(ScreenId.Attribution, _navigator.CurrentScreenId, "Navigated to NASA Attributions");

            // Navigate to Settings
            _navigator.GoTo(ScreenId.Settings);
            yield return null;
            Assert.AreEqual(ScreenId.Settings, _navigator.CurrentScreenId, "Navigated to Settings");

            // Navigate back to Main Menu
            _navigator.GoTo(ScreenId.MainMenu);
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, _navigator.CurrentScreenId, "Returned cleanly to MainMenu");
        }
    }
}
