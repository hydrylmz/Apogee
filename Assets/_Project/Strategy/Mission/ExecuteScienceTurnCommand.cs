using System;
using Apogee.Core;
using Apogee.Core.Simulation.Science;
using Apogee.Design;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Executes one daily step of the operational science phase for an active mission.
    /// Simulates power balance, thermal dissipation, instrument data harvesting,
    /// on-board storage buffer usage, and ground station downlinks.
    ///
    /// Detects data, power, and thermal bottlenecks, updates mission accumulated statistics,
    /// and grants research points / campaign reputation upon milestone science downlinks.
    /// Fully supports symmetric Undo/Redo.
    ///
    /// Complies with GDD §8.1 (Power/Thermal/Data/Comms), §10.1, §10.5, §11.1, §17.3, and §21 M5-04.
    /// </summary>
    public sealed class ExecuteScienceTurnCommand : CommandBase
    {
        public override string Name => "ExecuteScienceTurnCommand";

        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.ScienceDirector | PlayerRole.LeadEngineer;

        public override bool CanUndo => true;

        // ---------------------------------------------------------------- Inputs
        public CampaignState Campaign { get; }
        public string MissionId { get; }
        public ScienceSimulationParameters CustomParameters { get; }

        // ---------------------------------------------------------------- Output
        public ScienceSimulationResult TurnResult { get; private set; }

        // ---------------------------------------------------------------- Undo State
        private double _prevDownlinkGb;
        private double _prevRawGb;
        private double _prevDroppedGb;
        private double _prevBufferGb;
        private double _prevBatteryKwh;
        private int _prevScienceDays;
        private MissionStatus _prevStatus;
        private double _prevReputation;
        private double _prevResearchPoints;
        private bool _hasExecuted;

        public ExecuteScienceTurnCommand(
            CampaignState campaign,
            string missionId,
            ScienceSimulationParameters customParameters = null)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            MissionId = missionId ?? throw new ArgumentNullException(nameof(missionId));
            CustomParameters = customParameters;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) bilim operasyonunu yönetme yetkisine sahip değil.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
            {
                return CommandValidationResult.Fail($"Misyon bulunamadı: '{MissionId}'.");
            }

            if (mission.Status != MissionStatus.Operating)
            {
                return CommandValidationResult.Fail(
                    $"Misyon operasyonel durumda değil (mevcut durum: {mission.Status}). Bilim görevi için uzay aracı hedefe ulaşmış olmalıdır.");
            }

            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            var validation = CanExecute(context);
            if (!validation.IsValid)
            {
                return CommandExecutionResult.Failed(validation.Reason);
            }

            var mission = Campaign.Missions[MissionId];

            // Capture undo state
            _prevDownlinkGb = mission.AccumulatedDownlinkDataGb;
            _prevRawGb = mission.AccumulatedRawDataGeneratedGb;
            _prevDroppedGb = mission.AccumulatedDroppedDataGb;
            _prevBufferGb = mission.CurrentBufferStorageGb;
            _prevBatteryKwh = mission.CurrentBatteryStorageKwh;
            _prevScienceDays = mission.ScienceDaysCompleted;
            _prevStatus = mission.Status;
            _prevReputation = Campaign.Reputation;
            _prevResearchPoints = Campaign.ResearchPoints;

            int nextDay = mission.ScienceDaysCompleted + 1;

            // Determine simulation parameters
            ScienceSimulationParameters parameters = CustomParameters;
            if (parameters == null)
            {
                // Derive from Blueprint and Mission context
                var bp = mission.Blueprint;
                double solarKw = 0.0;
                double rtgKw = 0.0;
                double powerConsKw = 0.0;
                double batteryCapKwh = 10.0; // default baseline battery
                double thermLoadKw = 0.0;
                double thermDissKw = 5.0;   // default baseline radiation
                double instDataRate = 2.0;  // default instrument output (GB/day)
                double dutyCycle = 1.0;
                double maxStorageGb = 20.0; // default storage (GB)
                double downlinkRate = 5.0;  // default downlink (GB/day)
                double gndVisibility = 0.5; // 50% orbital / station contact per day
                double eclipseFraction = 0.3; // 30% shadow by default (e.g. orbit or day/night)

                if (bp != null)
                {
                    var evaluated = BlueprintEvaluator.Evaluate(bp);
                    if (evaluated != null)
                    {
                        solarKw = Math.Max(0.0, evaluated.PowerGeneration);
                        powerConsKw = Math.Max(0.5, evaluated.PowerConsumption);
                        batteryCapKwh = Math.Max(batteryCapKwh, evaluated.BatteryCapacity);
                        thermLoadKw = Math.Max(0.0, evaluated.ThermalLoad);
                        thermDissKw = Math.Max(1.0, evaluated.ThermalDissipation);
                        instDataRate = Math.Max(0.5, evaluated.DataRate);
                        maxStorageGb = Math.Max(5.0, evaluated.DataStorage);
                        downlinkRate = Math.Max(1.0, evaluated.DownlinkRate);
                    }

                    // Check for RTG in payload components
                    if (bp.Payload != null)
                    {
                        foreach (var comp in bp.Payload.AllComponents)
                        {
                            if (comp?.Id != null && comp.Id.IndexOf("rtg", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                rtgKw += 0.5;
                            }
                        }
                    }
                }

                // If no battery recorded yet, initialize battery to capacity
                double initialBattery = mission.ScienceDaysCompleted == 0 ? batteryCapKwh : mission.CurrentBatteryStorageKwh;
                double initialBuffer = mission.CurrentBufferStorageGb;

                ulong seed = mission.MissionSeed != 0 ? mission.MissionSeed : 987654321UL;

                double reliability = 0.85;
                if (bp != null)
                {
                    var eval = BlueprintEvaluator.Evaluate(bp);
                    reliability = eval.Reliability;
                }

                parameters = new ScienceSimulationParameters(
                    missionId: mission.MissionId,
                    destinationId: mission.DestinationId,
                    dayNumber: nextDay,
                    solarGenerationKw: solarKw,
                    rtgGenerationKw: rtgKw,
                    powerConsumptionKw: powerConsKw,
                    batteryCapacityKwh: batteryCapKwh,
                    initialBatteryStorageKwh: initialBattery,
                    eclipseFraction: eclipseFraction,
                    thermalLoadKw: thermLoadKw,
                    thermalDissipationKw: thermDissKw,
                    instrumentDataRateGbPerDay: instDataRate,
                    instrumentDutyCycle: dutyCycle,
                    maxStorageCapacityGb: maxStorageGb,
                    initialBufferStorageGb: initialBuffer,
                    downlinkRateGbPerDay: downlinkRate,
                    groundStationVisibilityFraction: gndVisibility,
                    systemReliability: reliability,
                    seed: seed);
            }

            // Run deterministic simulation
            TurnResult = ScienceSimulator.SimulateDay(parameters);

            // Apply mutations to mission instance
            mission.ScienceDaysCompleted = nextDay;
            mission.CurrentBufferStorageGb = TurnResult.FinalBufferStorageGb;
            mission.CurrentBatteryStorageKwh = TurnResult.FinalBatteryStorageKwh;
            mission.AccumulatedRawDataGeneratedGb += TurnResult.EffectiveDataCollectedGb;
            mission.AccumulatedDownlinkDataGb += TurnResult.DownlinkDeliveredGb;
            mission.AccumulatedDroppedDataGb += TurnResult.DroppedDataGb;
            mission.ScienceLog.Add(TurnResult);

            // Award campaign rewards: 1 research point per 5 GB downlinked
            double researchGained = Math.Floor(TurnResult.DownlinkDeliveredGb / 5.0);
            if (researchGained > 0)
            {
                Campaign.ResearchPoints += researchGained;
            }

            _hasExecuted = true;
            return CommandExecutionResult.Succeeded($"Science Turn #{nextDay} completed: {TurnResult.PlainLanguageSummary}");
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
            {
                return CommandExecutionResult.Failed("Komut henüz çalıştırılmadı.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
            {
                return CommandExecutionResult.Failed($"Geri alma sırasında misyon bulunamadı: '{MissionId}'.");
            }

            mission.AccumulatedDownlinkDataGb = _prevDownlinkGb;
            mission.AccumulatedRawDataGeneratedGb = _prevRawGb;
            mission.AccumulatedDroppedDataGb = _prevDroppedGb;
            mission.CurrentBufferStorageGb = _prevBufferGb;
            mission.CurrentBatteryStorageKwh = _prevBatteryKwh;
            mission.ScienceDaysCompleted = _prevScienceDays;
            mission.Status = _prevStatus;

            if (mission.ScienceLog.Count > 0 && mission.ScienceLog[mission.ScienceLog.Count - 1] == TurnResult)
            {
                mission.ScienceLog.RemoveAt(mission.ScienceLog.Count - 1);
            }

            Campaign.Reputation = _prevReputation;
            Campaign.ResearchPoints = _prevResearchPoints;

            _hasExecuted = false;
            return CommandExecutionResult.Succeeded($"Science Turn #{_prevScienceDays + 1} geri alındı.");
        }
    }
}
