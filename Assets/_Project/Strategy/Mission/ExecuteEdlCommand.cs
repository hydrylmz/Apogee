using System;
using Apogee.Core;
using Apogee.Core.Simulation.EDL;
using Apogee.Design;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Executes the Entry, Descent, and Landing (EDL) phase for an arrived mission.
    /// Evaluates target atmosphere, heat shield protection, parachutes, and powered descent systems.
    ///
    /// If successful, transitions mission to <see cref="MissionStatus.Operating"/> (on surface).
    /// If catastrophic failure occurs, transitions to <see cref="MissionStatus.Failed"/>, deducts campaign reputation,
    /// and logs cause-and-effect telemetry.
    ///
    /// Fully supports symmetric Undo/Redo.
    /// Complies with GDD §7.7, §8.1, §10.1, §10.4, §17.3, and §21 M5-03.
    /// </summary>
    public sealed class ExecuteEdlCommand : CommandBase
    {
        public override string Name => "ExecuteEdlCommand";

        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.ScienceDirector | PlayerRole.LeadEngineer;

        public override bool CanUndo => true;

        // ---------------------------------------------------------------- inputs
        public CampaignState Campaign { get; }
        public string MissionId { get; }
        public double SpacecraftReliability { get; }

        // ---------------------------------------------------------------- output
        public EdlSimulationResult SimulationResult { get; private set; }

        // ---------------------------------------------------------------- undo state
        private MissionStatus _previousStatus;
        private EdlSimulationResult _previousEdlResult;
        private double _previousReputation;
        private bool _hasExecuted;

        public ExecuteEdlCommand(
            CampaignState campaign,
            string missionId,
            double spacecraftReliability = 0.90)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            MissionId = missionId ?? throw new ArgumentNullException(nameof(missionId));
            SpacecraftReliability = spacecraftReliability;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) iniş/EDL sekansını yönetme yetkisine sahip değil.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandValidationResult.Fail($"Misyon bulunamadı: '{MissionId}'.");

            if (mission.Status != MissionStatus.Arrival)
            {
                return CommandValidationResult.Fail(
                    $"Misyon varış fazında değil (mevcut durum: {mission.Status}). İniş için önce hedefe varılmalıdır.");
            }

            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            var validation = CanExecute(context);
            if (!validation.IsValid)
                return CommandExecutionResult.Failed(validation.Reason);

            var mission = Campaign.Missions[MissionId];

            // Capture undo snapshot
            _previousStatus = mission.Status;
            _previousEdlResult = mission.EdlResult;
            _previousReputation = Campaign.Reputation;

            // Extract design features from mission blueprint
            ExtractDesignEdlFeatures(mission.Blueprint, out bool hasAtmosphere, out double atmospherePressureKpa,
                out bool hasHeatShield, out string heatShieldTier, out bool hasParachute, out bool hasSkycraneOrRetro,
                out double availableLandingDv, out double requiredLandingDv, out double edlSystemReliability,
                out bool isCrewed, mission.DestinationId);

            double effectiveCraftReliability = Math.Max(0.10, SpacecraftReliability - mission.AccumulatedReliabilityPenalty);

            var parameters = new EdlSimulationParameters(
                missionId: MissionId,
                destinationId: mission.DestinationId,
                hasAtmosphere: hasAtmosphere,
                atmospherePressureKpa: atmospherePressureKpa,
                hasHeatShield: hasHeatShield,
                heatShieldTier: heatShieldTier,
                hasParachute: hasParachute,
                hasSkycraneOrRetro: hasSkycraneOrRetro,
                availableLandingDv: availableLandingDv,
                requiredLandingDv: requiredLandingDv,
                spacecraftReliability: effectiveCraftReliability,
                edlSystemReliability: edlSystemReliability,
                isCrewed: isCrewed,
                seed: mission.MissionSeed ^ 0xEEDD11u
            );

            SimulationResult = EdlSimulator.Simulate(parameters);
            mission.EdlResult = SimulationResult;

            if (SimulationResult.IsSuccessful)
            {
                mission.Status = MissionStatus.Operating;
                Campaign.Reputation += 10.0;
                _hasExecuted = true;
                return CommandExecutionResult.Succeeded(
                    $"İniş başarılı ({SimulationResult.Outcome}): {SimulationResult.PlainLanguageSummary}");
            }
            else
            {
                mission.Status = MissionStatus.Failed;
                double repPenalty = isCrewed ? 25.0 : 8.0;
                Campaign.Reputation = Math.Max(0.0, Campaign.Reputation - repPenalty);
                _hasExecuted = true;
                return CommandExecutionResult.Succeeded(
                    $"İniş başarısız oldu ({SimulationResult.Outcome}): {SimulationResult.PlainLanguageSummary}");
            }
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
                return CommandExecutionResult.Failed("Geri alınacak yürütülmüş komut yok.");

            if (Campaign.Missions.TryGetValue(MissionId, out var mission))
            {
                mission.Status = _previousStatus;
                mission.EdlResult = _previousEdlResult;
            }

            Campaign.Reputation = _previousReputation;
            _hasExecuted = false;
            return CommandExecutionResult.Succeeded("İniş/EDL sekansı geri alındı.");
        }

        private static void ExtractDesignEdlFeatures(
            Blueprint blueprint,
            out bool hasAtmosphere,
            out double atmospherePressureKpa,
            out bool hasHeatShield,
            out string heatShieldTier,
            out bool hasParachute,
            out bool hasSkycraneOrRetro,
            out double availableLandingDv,
            out double requiredLandingDv,
            out double edlSystemReliability,
            out bool isCrewed,
            string destinationId)
        {
            // Default target parameters
            if (destinationId.Equals("mars", StringComparison.OrdinalIgnoreCase))
            {
                hasAtmosphere = true;
                atmospherePressureKpa = 0.636;
                requiredLandingDv = 600.0;
            }
            else if (destinationId.Equals("venus", StringComparison.OrdinalIgnoreCase))
            {
                hasAtmosphere = true;
                atmospherePressureKpa = 9200.0;
                requiredLandingDv = 1000.0;
            }
            else
            {
                // Moon / Vacuum
                hasAtmosphere = false;
                atmospherePressureKpa = 0.0;
                requiredLandingDv = 1700.0;
            }

            hasHeatShield = false;
            heatShieldTier = "none";
            hasParachute = false;
            hasSkycraneOrRetro = false;
            availableLandingDv = 2000.0; // default margin unless deficient
            edlSystemReliability = 0.90;
            isCrewed = false;

            if (blueprint == null) return;

            isCrewed = blueprint.Payload?.Cabin != null;

            // Check EDL slot component
            var edlComponent = blueprint.GetComponentInSlot(VehicleSlotId.PayloadEdl);
            if (edlComponent != null)
            {
                string id = edlComponent.Id ?? string.Empty;
                if (id.IndexOf("heat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    id.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasHeatShield = true;
                    heatShieldTier = id.IndexOf("mk2", StringComparison.OrdinalIgnoreCase) >= 0 ? "mk2" : "mk1";
                }
                if (id.IndexOf("airbag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    id.IndexOf("chute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    id.IndexOf("parachute", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasParachute = true;
                }
                if (id.IndexOf("skycrane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    id.IndexOf("retro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    id.IndexOf("rocket", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasSkycraneOrRetro = true;
                }

                // If specialized EDL package (like edl-lunar-skycrane), it inherently provides retro and landing
                if (id.Equals("edl-lunar-skycrane", StringComparison.OrdinalIgnoreCase))
                {
                    hasSkycraneOrRetro = true;
                    edlSystemReliability = 0.93;
                }
                else if (id.Equals("edl-airbag-cushion", StringComparison.OrdinalIgnoreCase))
                {
                    hasParachute = true;
                    edlSystemReliability = 0.88;
                }
            }
        }
    }
}
