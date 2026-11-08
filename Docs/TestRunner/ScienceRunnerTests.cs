using System;
using System.Linq;
using Apogee.Core;
using Apogee.Core.Simulation.Science;
using Apogee.Design;
using Apogee.Strategy;
using Apogee.Strategy.Mission;
using Apogee.UI.Screens.Operations;

namespace ApogeeLocalTestRunner
{
    public static class ScienceRunnerTests
    {
        public static void Run(Action<string, Action> runTest)
        {
            runTest("M5_04_Science_SeededDeterminism_ProducesIdenticalTelemetryAndData", Test_Science_Determinism);
            runTest("M5_04_Science_NominalOperation_GeneratesAndDownlinksExpectedData", Test_Science_Nominal);
            runTest("M5_04_Science_InsufficientBattery_CausesBrownoutAndThrottlesInstruments", Test_Science_BatteryDeficit);
            runTest("M5_04_Science_ThermalOverload_ThrottlesDutyCycle", Test_Science_ThermalOverload);
            runTest("M5_04_Science_BufferOverflow_DropsDataAndSignalsBottleneck", Test_Science_BufferOverflow);
            runTest("M5_04_Science_ConstrainedDownlink_CreatesDataBottleneck", Test_Science_ConstrainedDownlink);
            runTest("M5_04_Science_ExecuteScienceTurnCommand_AccruesDataAndRewards", Test_Science_Command_Execution);
            runTest("M5_04_Science_ExecuteScienceTurnCommand_UndoRestoresStateSymmetrically", Test_Science_Command_UndoRedo);
            runTest("M5_04_Science_OperationsViewModel_ScienceExecutionAndTelemetryBinding", Test_OperationsViewModel_ScienceBinding);
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

        private static void Test_Science_Determinism()
        {
            var p1 = new ScienceSimulationParameters(
                missionId: "seed_test",
                destinationId: "mars",
                dayNumber: 1,
                solarGenerationKw: 1.5,
                rtgGenerationKw: 0.0,
                powerConsumptionKw: 0.8,
                batteryCapacityKwh: 5.0,
                initialBatteryStorageKwh: 5.0,
                eclipseFraction: 0.4,
                thermalLoadKw: 1.0,
                thermalDissipationKw: 2.0,
                instrumentDataRateGbPerDay: 4.0,
                instrumentDutyCycle: 1.0,
                maxStorageCapacityGb: 20.0,
                initialBufferStorageGb: 0.0,
                downlinkRateGbPerDay: 5.0,
                groundStationVisibilityFraction: 0.5,
                systemReliability: 0.95,
                seed: 88776655UL
            );

            var p2 = new ScienceSimulationParameters(
                missionId: "seed_test",
                destinationId: "mars",
                dayNumber: 1,
                solarGenerationKw: 1.5,
                rtgGenerationKw: 0.0,
                powerConsumptionKw: 0.8,
                batteryCapacityKwh: 5.0,
                initialBatteryStorageKwh: 5.0,
                eclipseFraction: 0.4,
                thermalLoadKw: 1.0,
                thermalDissipationKw: 2.0,
                instrumentDataRateGbPerDay: 4.0,
                instrumentDutyCycle: 1.0,
                maxStorageCapacityGb: 20.0,
                initialBufferStorageGb: 0.0,
                downlinkRateGbPerDay: 5.0,
                groundStationVisibilityFraction: 0.5,
                systemReliability: 0.95,
                seed: 88776655UL
            );

            var res1 = ScienceSimulator.SimulateDay(p1);
            var res2 = ScienceSimulator.SimulateDay(p2);

            AssertEqual(res1.EffectiveDataCollectedGb, res2.EffectiveDataCollectedGb, "Science determinism data generation mismatch");
            AssertEqual(res1.DownlinkDeliveredGb, res2.DownlinkDeliveredGb, "Science determinism downlink mismatch");
            AssertEqual(res1.FinalBatteryStorageKwh, res2.FinalBatteryStorageKwh, "Science determinism battery mismatch");
            AssertEqual(res1.HasDataBottleneck, res2.HasDataBottleneck, "Science determinism bottleneck mismatch");
            AssertEqual(res1.Events.Count, res2.Events.Count, "Science determinism events count mismatch");
        }

        private static void Test_Science_Nominal()
        {
            var p = new ScienceSimulationParameters(
                missionId: "nominal_test",
                destinationId: "moon",
                dayNumber: 1,
                solarGenerationKw: 2.0,
                rtgGenerationKw: 0.5,
                powerConsumptionKw: 0.5,
                batteryCapacityKwh: 10.0,
                initialBatteryStorageKwh: 10.0,
                eclipseFraction: 0.2,
                thermalLoadKw: 1.0,
                thermalDissipationKw: 3.0,
                instrumentDataRateGbPerDay: 5.0,
                instrumentDutyCycle: 1.0,
                maxStorageCapacityGb: 50.0,
                initialBufferStorageGb: 0.0,
                downlinkRateGbPerDay: 10.0,
                groundStationVisibilityFraction: 0.8,
                systemReliability: 0.98,
                seed: 12345UL
            );

            var res = ScienceSimulator.SimulateDay(p);

            AssertTrue(!res.HadPowerDeficit, "Nominal science should have no power deficit");
            AssertTrue(!res.HadThermalOverload, "Nominal science should have no thermal overload");
            AssertTrue(!res.HasDataBottleneck, "Nominal science should have no data bottleneck");
            AssertEqual(5.0, res.EffectiveDataCollectedGb, "Effective collected data should equal 5.0 GB");
            AssertEqual(5.0, res.DownlinkDeliveredGb, "All 5.0 GB should be delivered to ground");
            AssertEqual(0.0, res.FinalBufferStorageGb, "Final buffer storage should be 0");
            AssertEqual(0.0, res.DroppedDataGb, "Dropped data should be 0");
        }

        private static void Test_Science_BatteryDeficit()
        {
            // Solar only (no RTG), heavy night (eclipse 0.5 = 12h night), tiny battery (1 kWh), 1.0 kW night consumption -> needs 12 kWh
            var p = new ScienceSimulationParameters(
                missionId: "deficit_test",
                destinationId: "mars",
                dayNumber: 1,
                solarGenerationKw: 2.0,
                rtgGenerationKw: 0.0,
                powerConsumptionKw: 1.0,
                batteryCapacityKwh: 1.0,
                initialBatteryStorageKwh: 1.0,
                eclipseFraction: 0.5,
                thermalLoadKw: 1.0,
                thermalDissipationKw: 3.0,
                instrumentDataRateGbPerDay: 6.0,
                instrumentDutyCycle: 1.0,
                maxStorageCapacityGb: 50.0,
                initialBufferStorageGb: 0.0,
                downlinkRateGbPerDay: 10.0,
                groundStationVisibilityFraction: 0.5,
                systemReliability: 0.95,
                seed: 998877UL
            );

            var res = ScienceSimulator.SimulateDay(p);

            AssertTrue(res.HadPowerDeficit, "Should detect power deficit during eclipse");
            AssertTrue(res.HasPowerBottleneck, "Should flag power bottleneck");
            AssertTrue(res.EffectiveDataCollectedGb < 6.0, "Instruments should be throttled due to lack of power");
            AssertTrue(res.Events.Any(e => e.EventType == ScienceEventType.PowerBalance && e.Status == ScienceEventStatus.Critical),
                "Should emit critical power brownout event");
        }

        private static void Test_Science_ThermalOverload()
        {
            // High thermal load (8 kW), poor radiator (2 kW)
            var p = new ScienceSimulationParameters(
                missionId: "thermal_test",
                destinationId: "venus",
                dayNumber: 1,
                solarGenerationKw: 2.0,
                rtgGenerationKw: 0.0,
                powerConsumptionKw: 0.5,
                batteryCapacityKwh: 10.0,
                initialBatteryStorageKwh: 10.0,
                eclipseFraction: 0.0,
                thermalLoadKw: 8.0,
                thermalDissipationKw: 2.0,
                instrumentDataRateGbPerDay: 4.0,
                instrumentDutyCycle: 1.0,
                maxStorageCapacityGb: 50.0,
                initialBufferStorageGb: 0.0,
                downlinkRateGbPerDay: 10.0,
                groundStationVisibilityFraction: 0.5,
                systemReliability: 0.95,
                seed: 112233UL
            );

            var res = ScienceSimulator.SimulateDay(p);

            AssertTrue(res.HadThermalOverload, "Should flag thermal overload");
            AssertTrue(res.HasThermalBottleneck, "Should flag thermal bottleneck");
            AssertTrue(res.EffectiveDataCollectedGb < 4.0, "Instruments should be throttled to prevent cooking the spacecraft");
            AssertTrue(res.Events.Any(e => e.EventType == ScienceEventType.ThermalStatus && e.Status == ScienceEventStatus.Warning),
                "Should log thermal warning event");
        }

        private static void Test_Science_BufferOverflow()
        {
            // Tiny buffer (2 GB), high data production (10 GB), zero ground visibility (0% downlink today)
            var p = new ScienceSimulationParameters(
                missionId: "overflow_test",
                destinationId: "europa",
                dayNumber: 1,
                solarGenerationKw: 0.0,
                rtgGenerationKw: 1.0,
                powerConsumptionKw: 0.5,
                batteryCapacityKwh: 10.0,
                initialBatteryStorageKwh: 10.0,
                eclipseFraction: 0.0,
                thermalLoadKw: 1.0,
                thermalDissipationKw: 2.0,
                instrumentDataRateGbPerDay: 10.0,
                instrumentDutyCycle: 1.0,
                maxStorageCapacityGb: 2.0,
                initialBufferStorageGb: 0.0,
                downlinkRateGbPerDay: 5.0,
                groundStationVisibilityFraction: 0.0, // no ground contact today!
                systemReliability: 0.95,
                seed: 334455UL
            );

            var res = ScienceSimulator.SimulateDay(p);

            AssertTrue(res.HasDataBottleneck, "Should identify data bottleneck");
            AssertTrue(res.DroppedDataGb > 0, $"Expected dropped data but got {res.DroppedDataGb}");
            AssertEqual(2.0, res.FinalBufferStorageGb, "Buffer should be clamped to max storage");
            AssertEqual(0.0, res.DownlinkDeliveredGb, "Downlink should be 0 due to zero ground visibility");
            AssertTrue(res.Events.Any(e => e.EventType == ScienceEventType.BufferOverflow && e.Status == ScienceEventStatus.Bottleneck),
                "Should log buffer overflow loss event");
        }

        private static void Test_Science_ConstrainedDownlink()
        {
            // Moderate storage (10 GB), data rate 6 GB/day, downlink rate only 2 GB/day
            var p = new ScienceSimulationParameters(
                missionId: "downlink_bottleneck_test",
                destinationId: "mars",
                dayNumber: 1,
                solarGenerationKw: 2.0,
                rtgGenerationKw: 0.0,
                powerConsumptionKw: 0.5,
                batteryCapacityKwh: 10.0,
                initialBatteryStorageKwh: 10.0,
                eclipseFraction: 0.0,
                thermalLoadKw: 1.0,
                thermalDissipationKw: 2.0,
                instrumentDataRateGbPerDay: 6.0,
                instrumentDutyCycle: 1.0,
                maxStorageCapacityGb: 10.0,
                initialBufferStorageGb: 5.0,
                downlinkRateGbPerDay: 2.0,
                groundStationVisibilityFraction: 1.0,
                systemReliability: 0.95,
                seed: 556677UL
            );

            var res = ScienceSimulator.SimulateDay(p);

            AssertTrue(res.HasDataBottleneck, "Should flag data bottleneck when storage is filling up faster than downlink");
            AssertEqual(2.0, res.DownlinkDeliveredGb, "Downlink delivered should be limited to 2.0 GB");
        }

        private static void Test_Science_Command_Execution()
        {
            var campaign = new CampaignState(seed: 42UL);
            campaign.Reputation = 50.0;
            campaign.ResearchPoints = 10.0;

            var mission = new MissionInstance("cmd_sci_test", "Mars Explorer", "mars", null, 180, 42UL)
            {
                Status = MissionStatus.Operating
            };
            campaign.Missions[mission.MissionId] = mission;

            var ctx = new CommandContext("player1", PlayerRole.AgencyDirector);
            var cmd = new ExecuteScienceTurnCommand(campaign, mission.MissionId);
            var result = campaign.CommandBus.Execute(cmd, ctx);

            AssertTrue(result.Success, "ExecuteScienceTurnCommand should succeed on Operating mission");
            AssertEqual(1, mission.ScienceDaysCompleted, "ScienceDaysCompleted should increment to 1");
            AssertTrue(mission.AccumulatedDownlinkDataGb > 0, "Downlink data should accumulate");
            AssertEqual(1, mission.ScienceLog.Count, "ScienceLog should contain 1 day result");
        }

        private static void Test_Science_Command_UndoRedo()
        {
            var campaign = new CampaignState(seed: 42UL);
            campaign.Reputation = 50.0;
            campaign.ResearchPoints = 10.0;
            var ctx = new CommandContext("player1", PlayerRole.AgencyDirector);

            var mission = new MissionInstance("cmd_undo_test", "Lunar Base", "moon", null, 3, 42UL)
            {
                Status = MissionStatus.Operating,
                AccumulatedDownlinkDataGb = 0.0,
                ScienceDaysCompleted = 0
            };
            campaign.Missions[mission.MissionId] = mission;

            var cmd = new ExecuteScienceTurnCommand(campaign, mission.MissionId);
            campaign.CommandBus.Execute(cmd, ctx);

            double recordedDownlink = mission.AccumulatedDownlinkDataGb;
            AssertTrue(recordedDownlink > 0, "Recorded downlink should be > 0");

            // Undo
            var undoRes = campaign.CommandBus.Undo(ctx);
            AssertTrue(undoRes.Success, "Undo should succeed");
            AssertEqual(0, mission.ScienceDaysCompleted, "ScienceDaysCompleted should restore to 0");
            AssertEqual(0.0, mission.AccumulatedDownlinkDataGb, "AccumulatedDownlinkDataGb should restore to 0");
            AssertEqual(0, mission.ScienceLog.Count, "ScienceLog should be cleared on undo");

            // Redo
            var redoRes = campaign.CommandBus.Redo(ctx);
            AssertTrue(redoRes.Success, "Redo should succeed");
            AssertEqual(1, mission.ScienceDaysCompleted, "ScienceDaysCompleted should restore to 1");
            AssertEqual(recordedDownlink, mission.AccumulatedDownlinkDataGb, "Downlink should re-apply");
            AssertEqual(1, mission.ScienceLog.Count, "ScienceLog should contain entry again");
        }

        private static void Test_OperationsViewModel_ScienceBinding()
        {
            var campaign = new CampaignState(seed: 42UL);
            var mission = new MissionInstance("vm_sci_test", "Venus Orbiter", "venus", null, 120, 42UL)
            {
                Status = MissionStatus.Operating
            };
            campaign.Missions[mission.MissionId] = mission;

            var vm = new OperationsViewModel();
            var navArgs = new OperationsNavigationArgs(
                missionId: mission.MissionId,
                missionName: mission.MissionName,
                destinationId: mission.DestinationId,
                blueprint: null,
                cruiseDurationDays: 120,
                missionSeed: 42UL
            );

            vm.BindMission(campaign, mission.MissionId, navArgs);

            AssertTrue(vm.CanExecuteScience.Value, "CanExecuteScience should be true when Operating");
            AssertTrue(!vm.CanAdvance.Value, "CanAdvance should be false when Operating");
            AssertTrue(!vm.CanExecuteEdl.Value, "CanExecuteEdl should be false when Operating");

            // Execute science step via ViewModel command
            vm.ExecuteScienceStepCommand.Execute();

            AssertEqual(1, vm.ScienceDaysCompleted.Value, "ViewModel should reflect ScienceDaysCompleted = 1");
            AssertTrue(vm.DownlinkDeliveredGb.Value > 0, "ViewModel should reflect delivered downlink data");
            AssertTrue(vm.ScienceEventLog.Count > 0, "ViewModel should populate ScienceEventLog");
        }
    }
}
