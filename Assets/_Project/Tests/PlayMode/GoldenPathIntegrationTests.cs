using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Bootstrap;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Managers;
using Apogee.UI.Screens.MainMenu;
using Apogee.UI.Screens.Review;
using Apogee.UI.Screens.Report;
using UnityEngine.UIElements;

namespace Apogee.Tests.PlayMode
{
    /// <summary>
    /// Play Mode Integration Test verifying the complete "Golden Path" demo loop (GDD §20, DEMO_GUIDE.md, M9-08).
    /// Covers: MainMenu -> CampaignMap -> Composer (Moon Rover) -> Workshop -> Review -> Launch ->
    /// Cruise -> EDL -> Science -> Report &amp; What-If -> Attribution -> Settings -> MainMenu.
    /// </summary>
    public class GoldenPathIntegrationTests
    {
        private GameObject _uiHost;
        private ApogeeUIManager _uiManager;
        private CampaignBootstrapper _bootstrapper;
        private IScreenNavigator _navigator;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _uiHost = new GameObject("Test_GoldenPath_Host");
            _uiHost.AddComponent<UIDocument>();
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
                UnityEngine.Object.Destroy(_uiHost);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoldenPath_FullDemoLoop_SucceedsEndToEnd()
        {
            // ─────────────────────────────────────────────────────────────────
            // Step 1: Main Menu & New Campaign (00:00 - 00:30)
            // ─────────────────────────────────────────────────────────────────
            Assert.AreEqual(ScreenId.MainMenu, _navigator.CurrentScreenId, "App starts on MainMenu");

            var mainMenuVm = _uiManager.MainMenuController.ViewModel;
            Assert.IsNotNull(mainMenuVm, "MainMenuViewModel present");

            mainMenuVm.AgencyNameInput.Value = "Birleşik Yörünge Ajansı";
            mainMenuVm.SeedInput.Value = 42UL;
            mainMenuVm.NewCampaignCommand.Execute();
            yield return null;

            Assert.AreEqual(ScreenId.CampaignMap, _navigator.CurrentScreenId, "Navigated to CampaignMap");
            var campaign = _bootstrapper.CurrentCampaign;
            Assert.IsNotNull(campaign, "Campaign state created and active");
            Assert.AreEqual(500.0, campaign.Budget, "Initial budget is 500 M$");

            // ─────────────────────────────────────────────────────────────────
            // Step 2: Mission Composer — Moon Rover (00:30 - 01:00)
            // ─────────────────────────────────────────────────────────────────
            _navigator.GoTo(ScreenId.MissionComposer, "moon");
            yield return null;

            Assert.AreEqual(ScreenId.MissionComposer, _navigator.CurrentScreenId, "Navigated to MissionComposer");

            // Use LunarRoverVerticalSlice — avoids catalog wiring complexity in Play Mode
            var profile = LunarRoverVerticalSlice.CreateMissionProfile();
            Assert.IsNotNull(profile, "Mission profile created");
            Assert.AreEqual("moon", profile.TargetDestinationId, "Target destination is moon");
            Assert.AreEqual("rover", profile.TargetMissionTypeId, "Target mission type is rover");
            Assert.IsTrue(profile.RequiredDeltaV > 11000.0, "Required Delta-V is substantial");

            // ─────────────────────────────────────────────────────────────────
            // Step 3: Workshop — Blueprint Assembly (01:00 - 01:30)
            // ─────────────────────────────────────────────────────────────────
            _navigator.GoTo(ScreenId.Workshop, profile);
            yield return null;

            Assert.AreEqual(ScreenId.Workshop, _navigator.CurrentScreenId, "Navigated to Workshop");

            var blueprint = LunarRoverVerticalSlice.CreatePassingBlueprint();
            var valContext = LunarRoverVerticalSlice.CreateValidationContext();
            var validationReport = BlueprintValidator.Validate(blueprint, valContext);
            Assert.IsTrue(validationReport.IsValid, "Blueprint passed all validation checks — GO FOR LAUNCH");

            // ─────────────────────────────────────────────────────────────────
            // Step 4: Design Review & Launch Sequence (01:30 - 02:00)
            // ─────────────────────────────────────────────────────────────────
            var reviewArgs = new ReviewNavigationArgs(blueprint, valContext);
            _navigator.GoTo(ScreenId.DesignReview, reviewArgs);
            yield return null;

            Assert.AreEqual(ScreenId.DesignReview, _navigator.CurrentScreenId, "Navigated to DesignReview");
            var reviewVm = new ReviewViewModel(_navigator, blueprint, valContext);
            Assert.IsTrue(reviewVm.IsValid.Value, "Review reports GO FOR LAUNCH");

            reviewVm.ProceedToLaunchCommand.Execute();
            yield return null;

            Assert.AreEqual(ScreenId.LaunchSequence, _navigator.CurrentScreenId, "Navigated to LaunchSequence");

            // Deterministic launch: zero weather risk + reliability 1.0 -> guaranteed Success
            var launchParams = new LaunchSimulationParameters(
                missionId:            "MSN-MOON-GOLDEN",
                vehicleName:          blueprint.Name,
                stageCount:           2,
                stageReliabilities:   new[] { 1.0, 1.0 },
                overallReliability:   1.0,
                launchPadWeatherRisk: 0.0,
                seed:                 42UL);
            var launchResult = LaunchSimulator.Simulate(launchParams);
            Assert.IsTrue(launchResult.IsSuccessful, "Launch succeeded into orbit");

            // ─────────────────────────────────────────────────────────────────
            // Step 5: Operations — Cruise, EDL & Science (02:00 - 02:30)
            // ─────────────────────────────────────────────────────────────────
            var missionInstance = new MissionInstance(
                "MSN-MOON-GOLDEN", "Artemis Pathfinder", "moon",
                blueprint, cruiseDurationDays: 3, missionSeed: 42)
            {
                Status        = MissionStatus.InCruise,
                CruiseProgress = 0.50
            };
            campaign.Missions[missionInstance.MissionId] = missionInstance;

            _navigator.GoTo(ScreenId.Operations, missionInstance);
            yield return null;

            Assert.AreEqual(ScreenId.Operations, _navigator.CurrentScreenId, "Navigated to Operations");

            // Advance cruise (2-arg, no daysToAdvance param)
            campaign.Execute(new AdvanceCruiseCommand(campaign, missionInstance.MissionId));
            Assert.IsTrue(missionInstance.CruiseProgress > 0.50, "Cruise progressed towards Moon arrival");

            // EDL — lunar soft landing
            var edlParams = new EdlSimulationParameters(
                missionId:            missionInstance.MissionId,
                destinationId:        "moon",
                hasAtmosphere:        false,
                atmospherePressureKpa: 0.0,
                hasHeatShield:        false,
                heatShieldTier:       "none",
                hasParachute:         false,
                hasSkycraneOrRetro:   true,
                availableLandingDv:   2000.0,
                requiredLandingDv:    1700.0,
                spacecraftReliability: 0.98,
                edlSystemReliability: 0.98,
                isCrewed:             false,
                seed:                 42UL);
            var edlResult = EdlSimulator.Simulate(edlParams);
            Assert.AreEqual(EdlOutcome.Success, edlResult.Outcome, "EDL soft landing on lunar surface");
            missionInstance.Status = MissionStatus.Operating;  // was SurfaceOperational — correct enum is Operating

            // Science downlink (2-arg, no yield/data params)
            campaign.Execute(new ExecuteScienceTurnCommand(campaign, missionInstance.MissionId));
            Assert.IsTrue(missionInstance.AccumulatedDownlinkDataGb > 0.0, "Science telemetry downlinked");

            // ─────────────────────────────────────────────────────────────────
            // Step 6: Mission Report, What-If & Navigation (02:30 - 03:00)
            // ─────────────────────────────────────────────────────────────────
            var eventRecord = new OperationEventRecord(
                eventId:    "op_solar_align",
                titleKey:   "Güneş Paneli Uyarısı",
                narrativeKey: "Hizalama optimizasyonu",
                phase:      OperationEventPhase.Cruise,
                baseWeight: 10.0,
                options:    new[]
                {
                    new OperationEventOption("opt_safe", "Güvenli Mod", "Hız azalır, veri korunur", "SAFE",
                        budgetDelta: 0, reliabilityPenalty: 0.0, scienceDataDeltaGb: -1.0),
                    new OperationEventOption("opt_aggr", "İtki Desteği", "Zaman kazanılır", "FAST",
                        budgetDelta: -1.0, reliabilityPenalty: 0.01, scienceDataDeltaGb: 2.0)
                });

            var activeEvent = new ActiveOperationEvent("evt_golden_1", eventRecord, OperationEventPhase.Cruise, 1)
            {
                ChosenOptionId = "opt_safe"
            };
            missionInstance.OperationEventLog.Add(activeEvent);

            var missionReport = MissionReportEvaluator.Evaluate(
                missionId:                   missionInstance.MissionId,
                missionName:                 missionInstance.MissionName,
                destinationId:               "moon",
                profile:                     profile,
                blueprint:                   blueprint,
                launchResult:                launchResult,
                edlResult:                   edlResult,
                downlinkedGb:                missionInstance.AccumulatedDownlinkDataGb,
                rawGeneratedGb:              missionInstance.AccumulatedRawDataGeneratedGb,
                droppedGb:                   missionInstance.AccumulatedDroppedDataGb,
                scienceDaysCompleted:        missionInstance.ScienceDaysCompleted,
                accumulatedReliabilityPenalty: 0.0,
                accumulatedCorrectionFuel:   0.0,
                operationEvents:             missionInstance.OperationEventLog);

            missionInstance.Report = missionReport;
            campaign.Execute(new CompleteMissionCommand(campaign, missionInstance.MissionId)); // 2-arg only
            Assert.IsTrue(missionReport.ScoreBreakdown.FinalScore > 50.0, "Final mission score calculated (>50)");

            var reportArgs = new MissionReportNavigationArgs(missionInstance.MissionId, missionReport);
            _navigator.GoTo(ScreenId.MissionReport, reportArgs);
            yield return null;
            Assert.AreEqual(ScreenId.MissionReport, _navigator.CurrentScreenId, "Navigated to MissionReport");

            // What-If counterfactual (note: .WhatIfReport, not .CounterfactualReport)
            var whatIfComparison = WhatIfSimulator.Simulate(
                missionId:                   missionInstance.MissionId,
                missionName:                 missionInstance.MissionName,
                destinationId:               "moon",
                profile:                     profile,
                blueprint:                   blueprint,
                launchResult:                launchResult,
                edlResult:                   edlResult,
                downlinkedGb:                missionInstance.AccumulatedDownlinkDataGb,
                rawGeneratedGb:              missionInstance.AccumulatedRawDataGeneratedGb,
                droppedGb:                   missionInstance.AccumulatedDroppedDataGb,
                scienceDaysCompleted:        missionInstance.ScienceDaysCompleted,
                accumulatedReliabilityPenalty: 0.0,
                accumulatedCorrectionFuel:   0.0,
                operationEventLog:           missionInstance.OperationEventLog,
                targetEvent:                 activeEvent,
                alternativeOptionId:         "opt_aggr",
                originalReport:              missionReport);

            Assert.IsNotNull(whatIfComparison, "What-If comparison calculated");
            Assert.IsNotNull(whatIfComparison.WhatIfReport, "Counterfactual report generated");

            // Attribution screen
            _navigator.GoTo(ScreenId.Attribution);
            yield return null;
            Assert.AreEqual(ScreenId.Attribution, _navigator.CurrentScreenId, "Navigated to Attribution");

            // Settings screen
            _navigator.GoTo(ScreenId.Settings);
            yield return null;
            Assert.AreEqual(ScreenId.Settings, _navigator.CurrentScreenId, "Navigated to Settings");

            // Back to Main Menu
            _navigator.GoTo(ScreenId.MainMenu);
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, _navigator.CurrentScreenId, "Returned cleanly to MainMenu");
        }
    }
}
