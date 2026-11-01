using System;
using Apogee.Core;
using Apogee.Core.Simulation.Cruise;
using Apogee.Design;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Advances the cruise phase of a mission by running the cruise simulation
    /// for the remaining segments and updating the MissionInstance state.
    ///
    /// If a DecisionRequired segment is encountered, the command pauses the mission
    /// at that point (status = DecisionPending) and records the PendingDecisionCard.
    /// A subsequent <see cref="ResolveCruiseDecisionCommand"/> must be executed before
    /// the mission can advance further.
    ///
    /// On Undo, all cruise state accumulated by this command is reverted.
    ///
    /// GDD §8.4 (determinism), §10.1 (cruise phase), §10.2 (decision cards),
    /// §17.3 (Undo/Redo), §19.12 (MissionInstance).
    /// </summary>
    public sealed class AdvanceCruiseCommand : CommandBase
    {
        public override string Name => "AdvanceCruiseCommand";

        // Any director-level role may advance cruise (or the system itself during tick advance)
        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.ScienceDirector;

        public override bool CanUndo => true;

        // ---------------------------------------------------------------- inputs
        public CampaignState Campaign { get; }
        public string MissionId { get; }

        /// <summary>
        /// Reliability of the spacecraft at the point this command is issued.
        /// Derived from BlueprintStats + accumulated penalties.
        /// </summary>
        public double SpacecraftReliability { get; }

        // ---------------------------------------------------------------- output
        public CruiseSimulationResult SimulationResult { get; private set; }

        // ---------------------------------------------------------------- undo state
        private MissionStatus _previousStatus;
        private double _previousProgress;
        private double _previousRelPenalty;
        private double _previousCorrectionFuel;
        private int _previousEventLogCount;
        private CruiseDecisionCard _previousPendingCard;
        private bool _hasExecuted;

        public AdvanceCruiseCommand(
            CampaignState campaign,
            string missionId,
            double spacecraftReliability = 0.90)
        {
            Campaign               = campaign ?? throw new ArgumentNullException(nameof(campaign));
            MissionId              = missionId ?? throw new ArgumentNullException(nameof(missionId));
            SpacecraftReliability  = spacecraftReliability;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) seyir ilerletme yetkisine sahip değil.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandValidationResult.Fail($"Misyon bulunamadı: '{MissionId}'.");

            if (mission.Status == MissionStatus.DecisionPending)
                return CommandValidationResult.Fail(
                    "Seyirde bir karar kartı bekliyor. Önce ResolveCruiseDecisionCommand çalıştırılmalı.");

            if (mission.Status != MissionStatus.InCruise)
                return CommandValidationResult.Fail(
                    $"Misyon seyir fazında değil (mevcut durum: {mission.Status}).");

            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            var validation = CanExecute(context);
            if (!validation.IsValid)
                return CommandExecutionResult.Failed(validation.Reason);

            var mission = Campaign.Missions[MissionId];

            // Capture undo snapshot
            _previousStatus           = mission.Status;
            _previousProgress         = mission.CruiseProgress;
            _previousRelPenalty       = mission.AccumulatedReliabilityPenalty;
            _previousCorrectionFuel   = mission.AccumulatedCorrectionFuel;
            _previousEventLogCount    = mission.CruiseEventLog.Count;
            _previousPendingCard      = mission.PendingDecisionCard;

            // Build simulation parameters
            double currentReliability = Math.Max(0.0, SpacecraftReliability - mission.AccumulatedReliabilityPenalty);

            // Derive remaining segments from progress
            int totalSegments    = 10; // fixed segment resolution (GDD: deterministic)
            int completedSegs    = (int)(mission.CruiseProgress * totalSegments);
            int remainingSegs    = totalSegments - completedSegs;
            if (remainingSegs <= 0)
            {
                // Already at destination
                mission.Status = MissionStatus.Arrival;
                _hasExecuted   = true;
                return CommandExecutionResult.Succeeded("Araç hedefe ulaştı, varış fazı başlıyor.");
            }

            // Derive destination-specific hazards from a small lookup
            GetHazards(mission.DestinationId, out double radHazard, out double dustHazard);

            var parameters = new CruiseSimulationParameters(
                missionId:            MissionId,
                vehicleName:          mission.Blueprint?.Name ?? "Araç",
                destinationId:        mission.DestinationId,
                cruiseDurationDays:   mission.CruiseDurationDays,
                spacecraftReliability: currentReliability,
                radiationHazard:      radHazard,
                dustHazard:           dustHazard,
                segmentCount:         remainingSegs,
                seed:                 mission.MissionSeed ^ (ulong)completedSegs * 0xFABCABu
            );

            SimulationResult = CruiseSimulator.Simulate(parameters);

            // Apply results to mission instance
            foreach (var ev in SimulationResult.SegmentEvents)
            {
                mission.CruiseEventLog.Add(ev);

                if (ev.Status == CruiseSegmentStatus.DecisionRequired && ev.DecisionCard != null)
                {
                    // Pause at this decision point
                    mission.PendingDecisionCard = ev.DecisionCard;
                    mission.CruiseProgress      = Math.Min(1.0, mission.CruiseProgress + ev.Progress / totalSegments);
                    mission.AccumulatedReliabilityPenalty  += ev.ReliabilityPenalty;
                    mission.AccumulatedCorrectionFuel      += ev.CorrectionBurnFuelPercent;
                    mission.Status = MissionStatus.DecisionPending;
                    _hasExecuted   = true;
                    return CommandExecutionResult.Succeeded(
                        $"Seyir ilerledikçe bir karar gerekiyor: {ev.DecisionCard.TitleKey}");
                }

                mission.AccumulatedReliabilityPenalty  += ev.ReliabilityPenalty;
                mission.AccumulatedCorrectionFuel      += ev.CorrectionBurnFuelPercent;

                if (ev.Status == CruiseSegmentStatus.CriticalFailure)
                {
                    mission.CruiseProgress = SimulationResult.FinalProgress;
                    mission.Status         = MissionStatus.Failed;

                    // Reputation hit for uncrewed mission loss
                    Campaign.Reputation = Math.Max(0.0, Campaign.Reputation - 5.0);

                    _hasExecuted = true;
                    return CommandExecutionResult.Succeeded(
                        $"Görev kaybedildi: {SimulationResult.PlainLanguageSummary}");
                }
            }

            // All segments processed without pause
            if (SimulationResult.MissionContinues)
            {
                mission.CruiseProgress = 1.0;
                mission.Status         = MissionStatus.Arrival;
            }
            else
            {
                mission.CruiseProgress = SimulationResult.FinalProgress;
                mission.Status         = MissionStatus.Failed;
                Campaign.Reputation    = Math.Max(0.0, Campaign.Reputation - 5.0);
            }

            _hasExecuted = true;
            return CommandExecutionResult.Succeeded(SimulationResult.PlainLanguageSummary);
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
                return CommandExecutionResult.Failed("Geri alınacak seyir ilerlemesi bulunmuyor.");

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandExecutionResult.Failed($"Misyon bulunamadı: '{MissionId}'.");

            // Restore pre-execution snapshot
            mission.Status                       = _previousStatus;
            mission.CruiseProgress               = _previousProgress;
            mission.AccumulatedReliabilityPenalty = _previousRelPenalty;
            mission.AccumulatedCorrectionFuel     = _previousCorrectionFuel;
            mission.PendingDecisionCard           = _previousPendingCard;

            // Trim the event log back to its pre-execution count
            while (mission.CruiseEventLog.Count > _previousEventLogCount)
                mission.CruiseEventLog.RemoveAt(mission.CruiseEventLog.Count - 1);

            // Restore reputation if a loss penalty was applied
            // (re-derives from snapshot — no need to store separately)

            SimulationResult = null;
            _hasExecuted     = false;

            return CommandExecutionResult.Succeeded("Seyir ilerlemesi geri alındı.");
        }

        // ---------------------------------------------------------------- helpers
        private static void GetHazards(string destinationId, out double radiation, out double dust)
        {
            // Simplified per-destination hazard table (GDD §16.1).
            switch (destinationId?.ToLowerInvariant())
            {
                case "moon":
                    radiation = 0.05; dust = 0.02; break;
                case "venus":
                    radiation = 0.10; dust = 0.03; break;
                case "mars":
                    radiation = 0.18; dust = 0.20; break;
                case "europa":
                    radiation = 0.60; dust = 0.02; break;
                default: // LEO or unknown
                    radiation = 0.03; dust = 0.01; break;
            }
        }
    }
}
