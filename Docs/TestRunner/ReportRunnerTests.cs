using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.EDL;
using Apogee.Core.Simulation.Launch;
using Apogee.Core.Simulation.Operations;
using Apogee.Core.Simulation.Report;
using Apogee.Core.Simulation.Science;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Report;

namespace ApogeeLocalTestRunner
{
    public static class ReportRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_06_Report_AllCriteriaMet_EvaluatesToOverallSuccess", Test_AllCriteriaMet_Success);
            runTest("M5_06_Report_MissingData_CriteriaFailsAndFlagsDeficit", Test_MissingData_CriteriaFails);
            runTest("M5_06_Report_LaunchCatastrophe_GeneratesFailureReportAndPenalty", Test_LaunchCatastrophe_LossReport);
            runTest("M5_06_Report_EdlCrash_GeneratesThermalLossChainAndLesson", Test_EdlCrash_ThermalLossChain);
            runTest("M5_06_Report_DataBottleneck_IdentifiesBufferOverflowAndDeficit", Test_DataBottleneck_Analysis);
            runTest("M5_06_Report_CauseAndEffectChain_ChronologicalPhaseFlow", Test_CauseAndEffectChain_PhaseFlow);
            runTest("M5_06_Report_CompleteMissionCommand_ExecutesAndGrantsCampaignImpact", Test_CompleteCommand_ExecutionAndImpact);
            runTest("M5_06_Report_CompleteMissionCommand_UndoRestoresCampaignAndMissionState", Test_CompleteCommand_UndoRedo);
            runTest("M5_06_Report_ViewModel_BindsReportAndFiresUpdateEvent", Test_ViewModel_BindingAndEvent);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new Exception($"Assertion Failed: {message}. Expected: {expected}, Actual: {actual}");
        }

        private static void Test_AllCriteriaMet_Success()
        {
            var criteria = new List<MissionSuccessCriterion>
            {
                new MissionSuccessCriterion("obj_map", "data-delivered-gb", 10.0, "En az 10 GB veri"),
                new MissionSuccessCriterion("obj_dur", "operation-days", 3, "En az 3 gün çalışma")
            };

            var profile = new MissionProfile(
                id: "prof_test",
                name: "Ay Haritalama",
                targetDestinationId: "moon",
                targetMissionTypeId: "orbiter",
                requiredDeltaV: 13500,
                minReliabilityThreshold: 0.90,
                requiredLaunchPadLevel: 2,
                minDownlinkRate: 5.0,
                minDataStorage: 20.0,
                successCriteria: criteria,
                rewardMultiplier: 1.5,
                totalScienceValue: 50.0
            );

            var launch = new LaunchSimulationResult(LaunchOutcome.Success, true, LaunchWeatherCondition.Clear, 0.1, 9500, "nominal", "Başarılı fırlatma");
            var edl = new EdlSimulationResult(EdlOutcome.Success, true, 0.85, 1.2, "nominal", "Başarılı iniş");

            var report = MissionReportEvaluator.Evaluate(
                missionId: "m1",
                missionName: "Artemis-1",
                destinationId: "moon",
                profile: profile,
                blueprint: null,
                launchResult: launch,
                edlResult: edl,
                downlinkedGb: 12.5,
                rawGeneratedGb: 13.0,
                droppedGb: 0.0,
                scienceDaysCompleted: 5,
                accumulatedReliabilityPenalty: 0.02,
                accumulatedCorrectionFuel: 0.03,
                operationEvents: null
            );

            AssertTrue(report.IsOverallSuccess, "Tüm kriterler karşılandığında IsOverallSuccess true olmalı");
            AssertEqual(2, report.CriteriaResults.Count, "Kriter sayısı 2 olmalı");
            AssertTrue(report.CriteriaResults.All(c => c.IsMet), "Tüm kriterler IsMet == true olmalı");
            AssertTrue(report.CampaignImpact.BudgetChange > 0, "Başarılı görev bütçe ödülü vermeli");
            AssertTrue(report.CampaignImpact.ReputationChange > 0, "Başarılı görev itibar kazandırmalı");
        }

        private static void Test_MissingData_CriteriaFails()
        {
            var criteria = new List<MissionSuccessCriterion>
            {
                new MissionSuccessCriterion("obj_map", "data-delivered-gb", 20.0, "En az 20 GB veri")
            };

            var profile = new MissionProfile(
                id: "prof_test2",
                name: "Mars Keşfi",
                targetDestinationId: "mars",
                targetMissionTypeId: "orbiter",
                requiredDeltaV: 15000,
                minReliabilityThreshold: 0.90,
                requiredLaunchPadLevel: 3,
                minDownlinkRate: 5.0,
                minDataStorage: 30.0,
                successCriteria: criteria
            );

            var report = MissionReportEvaluator.Evaluate(
                missionId: "m2",
                missionName: "Mars-1",
                destinationId: "mars",
                profile: profile,
                blueprint: null,
                launchResult: null,
                edlResult: null,
                downlinkedGb: 8.0, // 20 GB eşiğinin altında
                rawGeneratedGb: 10.0,
                droppedGb: 0.0,
                scienceDaysCompleted: 10,
                accumulatedReliabilityPenalty: 0.0,
                accumulatedCorrectionFuel: 0.0,
                operationEvents: null
            );

            AssertTrue(!report.IsOverallSuccess, "Kriter sağlanamadığında IsOverallSuccess false olmalı");
            AssertTrue(!report.CriteriaResults[0].IsMet, "Veri kriteri başarısız olarak işaretlenmeli");
            AssertEqual(8.0, report.CriteriaResults[0].ActualValue, "Gerçekleşen değer 8.0 GB olmalı");
        }

        private static void Test_LaunchCatastrophe_LossReport()
        {
            var launch = new LaunchSimulationResult(
                LaunchOutcome.CatastrophicFailure,
                false,
                LaunchWeatherCondition.Windy,
                0.4,
                2000,
                "Stage1Explosion",
                "1. Aşama infilak etti"
            );

            var report = MissionReportEvaluator.Evaluate(
                missionId: "m3",
                missionName: "Titan-Launch",
                destinationId: "moon",
                profile: null,
                blueprint: null,
                launchResult: launch,
                edlResult: null,
                downlinkedGb: 0,
                rawGeneratedGb: 0,
                droppedGb: 0,
                scienceDaysCompleted: 0,
                accumulatedReliabilityPenalty: 0,
                accumulatedCorrectionFuel: 0,
                operationEvents: null,
                isMissionFailedEarly: true
            );

            AssertTrue(!report.IsOverallSuccess, "Fırlatma felaketi görevi başarısız kılmalı");
            AssertTrue(report.CampaignImpact.ReputationChange < 0, "Fırlatma kaybı itibar cezası uygulamalı");
            AssertTrue(report.CauseAndEffectChain.Any(n => n.Phase == "Fırlatma" && n.IsNegativeOrFault),
                "Neden-sonuç zinciri fırlatma hatasını içermeli");
        }

        private static void Test_EdlCrash_ThermalLossChain()
        {
            var edl = new EdlSimulationResult(
                EdlOutcome.ThermalDestruction,
                false,
                0.0,
                240.0,
                "ThermalShieldDepleted",
                "Isı kalkanı eridi ve araç yandı"
            );

            var report = MissionReportEvaluator.Evaluate(
                missionId: "m4",
                missionName: "Venus-EDL",
                destinationId: "venus",
                profile: null,
                blueprint: null,
                launchResult: null,
                edlResult: edl,
                downlinkedGb: 0,
                rawGeneratedGb: 0,
                droppedGb: 0,
                scienceDaysCompleted: 0,
                accumulatedReliabilityPenalty: 0,
                accumulatedCorrectionFuel: 0,
                operationEvents: null,
                isMissionFailedEarly: true
            );

            AssertTrue(!report.IsOverallSuccess, "EDL yanması görevi başarısız kılmalı");
            AssertTrue(report.CauseAndEffectChain.Any(n => n.Phase == "Varış ve İniş" && n.IsNegativeOrFault),
                "Neden-sonuç zinciri EDL hatasını içermeli");
            AssertTrue(report.LessonsLearned.Any(l => l.Contains("EDL")), "EDL dersi türetilmeli");
        }

        private static void Test_DataBottleneck_Analysis()
        {
            var report = MissionReportEvaluator.Evaluate(
                missionId: "m5",
                missionName: "Europa-Bottleneck",
                destinationId: "europa",
                profile: null,
                blueprint: null,
                launchResult: null,
                edlResult: null,
                downlinkedGb: 5.0,
                rawGeneratedGb: 25.0,
                droppedGb: 15.0,
                scienceDaysCompleted: 5,
                accumulatedReliabilityPenalty: 0,
                accumulatedCorrectionFuel: 0,
                operationEvents: null
            );

            AssertTrue(report.ScienceSummary.HasDataBottleneck, "Veri darboğazı tespit edilmeli");
            AssertEqual(15.0, report.ScienceSummary.TotalDroppedGb, "Kayıp veri 15 GB olmalı");
            AssertTrue(report.CauseAndEffectChain.Any(n => n.Phase == "Bilim" && n.IsNegativeOrFault),
                "Zincirde depolama taşması ve veri kaybı düğümü olmalı");
            AssertTrue(report.LessonsLearned.Any(l => l.Contains("Haberleşme & Depolama")),
                "Haberleşme ve depolama dersi eklenmeli");
        }

        private static void Test_CauseAndEffectChain_PhaseFlow()
        {
            var optSafe = new OperationEventOption(
                "opt_safe",
                "Güvenli Mod",
                "Aletleri koru",
                "SAFE_MODE",
                budgetDelta: -2.0,
                reliabilityPenalty: 0.05,
                scienceDataDeltaGb: 0,
                researchPointsDelta: 0,
                reputationDelta: 0
            );

            var rec = new OperationEventRecord(
                "op_solar_storm",
                "Güneş Fırtınası",
                "Radyasyon tepe noktasına ulaştı",
                OperationEventPhase.Cruise,
                10.0,
                new[] { optSafe }
            );

            var opEvent = new ActiveOperationEvent(
                "inst_solar",
                rec,
                OperationEventPhase.Cruise,
                1
            )
            {
                ChosenOptionId = "opt_safe"
            };

            var report = MissionReportEvaluator.Evaluate(
                missionId: "m6",
                missionName: "Solar-Chain",
                destinationId: "mars",
                profile: null,
                blueprint: null,
                launchResult: new LaunchSimulationResult(LaunchOutcome.Success, true, LaunchWeatherCondition.Clear, 0.1, 9500, "ok", "Nominal"),
                edlResult: new EdlSimulationResult(EdlOutcome.Success, true, 0.7, 1.0, "ok", "Nominal"),
                downlinkedGb: 10,
                rawGeneratedGb: 10,
                droppedGb: 0,
                scienceDaysCompleted: 3,
                accumulatedReliabilityPenalty: 0.10,
                accumulatedCorrectionFuel: 0.12,
                operationEvents: new[] { opEvent }
            );

            var phases = report.CauseAndEffectChain.Select(c => c.Phase).ToList();
            AssertTrue(phases.Contains("Fırlatma"), "Zincir Fırlatma safhasını içermeli");
            AssertTrue(phases.Contains("Seyir"), "Zincir Seyir safhasını içermeli");
            AssertTrue(phases.Contains("Operasyon Olayı"), "Zincir Operasyon Olayı safhasını içermeli");
            AssertTrue(phases.Contains("Varış ve İniş"), "Zincir Varış ve İniş safhasını içermeli");
            AssertTrue(phases.Contains("Bilim"), "Zincir Bilim safhasını içermeli");
        }

        private static void Test_CompleteCommand_ExecutionAndImpact()
        {
            var campaign = new CampaignState();
            campaign.Budget = 50.0;
            campaign.Reputation = 60.0;
            campaign.ResearchPoints = 20.0;
            campaign.KnowledgePoints = 10.0;

            var mission = new MissionInstance("m7", "Atlas-7", "moon", null, 30, 100);
            mission.Status = MissionStatus.Operating;
            mission.AccumulatedDownlinkDataGb = 20.0;
            mission.AccumulatedRawDataGeneratedGb = 20.0;
            mission.ScienceDaysCompleted = 5;

            var criteria = new List<MissionSuccessCriterion>
            {
                new MissionSuccessCriterion("obj_lunar", "data-delivered-gb", 15.0)
            };
            mission.Profile = new MissionProfile(
                "prof_lunar",
                "Ay",
                "moon",
                "orbiter",
                13500,
                0.90,
                2,
                5.0,
                20.0,
                successCriteria: criteria,
                rewardMultiplier: 1.0,
                totalScienceValue: 30.0
            );

            campaign.Missions["m7"] = mission;

            var cmd = new CompleteMissionCommand(campaign, "m7");
            var result = campaign.Execute(cmd);

            AssertTrue(result.Success, "CompleteMissionCommand başarıyla yürütülmeli");
            AssertEqual(MissionStatus.Completed, mission.Status, "Misyon durumu Completed olmalı");
            AssertTrue(mission.Report != null, "Misyon instance'ı üretilen rapora sahip olmalı");
            AssertTrue(mission.Report.IsOverallSuccess, "Sonuç raporu genel başarıyı doğrulamalı");
            AssertTrue(campaign.Reputation > 60.0, "İtibar artmalı");
            AssertTrue(campaign.KnowledgePoints > 10.0, "Bilgi puanı artmalı");
        }

        private static void Test_CompleteCommand_UndoRedo()
        {
            var campaign = new CampaignState();
            campaign.Budget = 50.0;
            campaign.Reputation = 60.0;
            campaign.ResearchPoints = 20.0;
            campaign.KnowledgePoints = 10.0;

            var mission = new MissionInstance("m8", "Atlas-8", "moon", null, 30, 100);
            mission.Status = MissionStatus.Operating;
            campaign.Missions["m8"] = mission;

            var cmd = new CompleteMissionCommand(campaign, "m8");
            campaign.Execute(cmd);

            AssertTrue(mission.Report != null, "Komut yürütülünce rapor atanmış olmalı");

            var undoResult = campaign.Undo();
            AssertTrue(undoResult.Success, "Undo başarılı olmalı");
            AssertEqual(MissionStatus.Operating, mission.Status, "Misyon durumu Operating'e geri dönmeli");
            AssertTrue(mission.Report == null, "Misyon raporu null'a geri dönmeli");
            AssertEqual(50.0, campaign.Budget, "Bütçe eski değerine dönmeli");
            AssertEqual(60.0, campaign.Reputation, "İtibar eski değerine dönmeli");
            AssertEqual(20.0, campaign.ResearchPoints, "Araştırma puanı eski değerine dönmeli");
            AssertEqual(10.0, campaign.KnowledgePoints, "Bilgi puanı eski değerine dönmeli");
        }

        private static void Test_ViewModel_BindingAndEvent()
        {
            var campaign = new CampaignState();
            var mission = new MissionInstance("m9", "Surveyor-9", "moon", null, 30, 200);
            mission.Status = MissionStatus.Operating;
            mission.AccumulatedDownlinkDataGb = 15.0;
            mission.AccumulatedRawDataGeneratedGb = 16.0;
            mission.ScienceDaysCompleted = 4;
            campaign.Missions["m9"] = mission;

            var vm = new MissionReportViewModel();
            vm.Campaign = campaign;

            bool updatedFired = false;
            vm.OnReportDataUpdated += () => { updatedFired = true; };

            vm.BindReport("m9");

            AssertTrue(updatedFired, "BindReport çağrılınca OnReportDataUpdated tetiklenmeli");
            AssertEqual("Surveyor-9", vm.MissionName.Value, "ViewModel MissionName eşleşmeli");
            AssertEqual("MOON", vm.DestinationText.Value, "ViewModel DestinationText MOON olmalı");
            AssertTrue(vm.CriteriaList.Count > 0, "CriteriaList doldurulmuş olmalı");
            AssertTrue(vm.CauseAndEffectNodes.Count > 0, "CauseAndEffectNodes doldurulmuş olmalı");
            AssertTrue(vm.LessonsLearnedList.Count > 0, "LessonsLearnedList doldurulmuş olmalı");
        }
    }
}
