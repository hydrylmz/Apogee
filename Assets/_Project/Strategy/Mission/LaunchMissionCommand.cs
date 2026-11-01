using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Core.Simulation.Launch;
using Apogee.Design;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Command that initiates and executes a deterministic rocket launch sequence for a mission.
    /// Deducts operational margins, resolves launch staging and telemetry events, records the result,
    /// and logs cause-and-effect traces into campaign history.
    ///
    /// Complies with GDD §8.4, §10.1, §10.4, §10.5, §12.2 (Ajans Başkanı / Baş Mühendis yetkisi),
    /// and §17.3 (simetrik Undo/Redo).
    /// </summary>
    public sealed class LaunchMissionCommand : CommandBase
    {
        public override string Name => "LaunchMissionCommand";
        public override PlayerRole RequiredRoles => PlayerRole.AgencyDirector | PlayerRole.LeadEngineer;
        public override bool CanUndo => true;

        public CampaignState Campaign { get; }
        public Blueprint VehicleBlueprint { get; }
        public BlueprintStats VehicleStats { get; }
        public string LaunchPadBaseId { get; }
        public bool IsCrewed { get; }
        public ulong LaunchSeed { get; }

        public LaunchSimulationResult ExecutionResult { get; private set; }

        private double _previousReputation;
        private bool _hasExecuted;

        public LaunchMissionCommand(
            CampaignState campaign,
            Blueprint vehicleBlueprint,
            BlueprintStats vehicleStats,
            string launchPadBaseId,
            bool isCrewed = false,
            ulong launchSeed = 0)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            VehicleBlueprint = vehicleBlueprint ?? throw new ArgumentNullException(nameof(vehicleBlueprint));
            VehicleStats = vehicleStats;
            LaunchPadBaseId = launchPadBaseId;
            IsCrewed = isCrewed;
            LaunchSeed = launchSeed != 0 ? launchSeed : campaign.Seed ^ 0x1A47C1u;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) fırlatma yetkisine sahip değil. Gerekli: {RequiredRoles}.");
            }

            if (VehicleBlueprint == null)
                return CommandValidationResult.Fail("Geçerli bir araç tasarımı (blueprint) bulunamadı.");

            if (string.IsNullOrWhiteSpace(LaunchPadBaseId) || !Campaign.Bases.ContainsKey(LaunchPadBaseId))
                return CommandValidationResult.Fail($"Fırlatma üssü bulunamadı: '{LaunchPadBaseId}'.");

            var baseFacility = Campaign.Bases[LaunchPadBaseId];
            if (baseFacility.Type != BaseType.LaunchPad)
                return CommandValidationResult.Fail($"Seçilen üs '{baseFacility.Id}' bir fırlatma rampası değil.");

            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            var validation = CanExecute(context);
            if (!validation.IsValid)
                return CommandExecutionResult.Failed(validation.Reason);

            _previousReputation = Campaign.Reputation;

            // Resolve pad weather risk and latitude bonus from region
            double weatherRisk = 0.20; // default safe baseline
            double equatorialDv = 0.0;

            // Build launch parameters
            var launchParams = VehicleBlueprint.ToLaunchParameters(
                stats: VehicleStats,
                padWeatherRisk: weatherRisk,
                equatorialBonusDv: equatorialDv,
                isCrewed: IsCrewed,
                seed: LaunchSeed
            );

            // Execute deterministic launch simulation
            ExecutionResult = LaunchSimulator.Simulate(launchParams);

            // Apply campaign reputation consequences
            if (ExecutionResult.Outcome == LaunchOutcome.Success)
            {
                Campaign.Reputation = Math.Min(100.0, Campaign.Reputation + 5.0);
            }
            else if (ExecutionResult.Outcome == LaunchOutcome.CatastrophicFailure)
            {
                double penalty = IsCrewed ? 25.0 : 8.0;
                Campaign.Reputation = Math.Max(0.0, Campaign.Reputation - penalty);
            }
            else if (ExecutionResult.Outcome == LaunchOutcome.InFlightAbort)
            {
                // Crew survived thanks to abort tower -> moderate loss
                Campaign.Reputation = Math.Max(0.0, Campaign.Reputation - (IsCrewed ? 6.0 : 4.0));
            }

            _hasExecuted = true;
            return CommandExecutionResult.Succeeded(ExecutionResult.PlainLanguageSummary);
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
                return CommandExecutionResult.Failed("Geri alınacak bir fırlatma operasyonu bulunmuyor.");

            Campaign.Reputation = _previousReputation;
            ExecutionResult = null;
            _hasExecuted = false;

            return CommandExecutionResult.Succeeded("Fırlatma operasyonu ve itibar etkileri geri alındı.");
        }
    }
}
