using System;
using Apogee.Core;
using Apogee.Core.Simulation.Report;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Evaluates and concludes an active mission, generating the final post-mission diagnostic report
    /// (<see cref="MissionReport"/>) and applying campaign financial, reputational, and research impacts.
    ///
    /// Fully supports symmetric Undo/Redo.
    /// Complies with GDD §4.3, §10.5, §11.1, §12.2, §17.3, and §21 M5-06.
    /// </summary>
    public sealed class CompleteMissionCommand : CommandBase
    {
        public override string Name => "CompleteMissionCommand";

        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.ScienceDirector | PlayerRole.LeadEngineer;

        public override bool CanUndo => true;

        // ---------------------------------------------------------------- Inputs
        public CampaignState Campaign { get; }
        public string MissionId { get; }

        // ---------------------------------------------------------------- Output
        public MissionReport GeneratedReport { get; private set; }

        // ---------------------------------------------------------------- Undo State
        private MissionStatus _prevStatus;
        private MissionReport _prevReport;
        private double _prevBudget;
        private double _prevReputation;
        private double _prevResearchPoints;
        private double _prevKnowledgePoints;
        private bool _hasExecuted;

        public CompleteMissionCommand(CampaignState campaign, string missionId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            MissionId = missionId ?? throw new ArgumentNullException(nameof(missionId));
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) misyon sonuçlandırma yetkisine sahip değil. Gerekli: {RequiredRoles}.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
            {
                return CommandValidationResult.Fail($"Misyon bulunamadı: '{MissionId}'.");
            }

            if (mission.Status == MissionStatus.Planning)
            {
                return CommandValidationResult.Fail("Henüz fırlatılmamış veya planlama aşamasındaki bir misyon sonuçlandırılamaz.");
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

            // Capture state for undo
            _prevStatus = mission.Status;
            _prevReport = mission.Report;
            _prevBudget = Campaign.Budget;
            _prevReputation = Campaign.Reputation;
            _prevResearchPoints = Campaign.ResearchPoints;
            _prevKnowledgePoints = Campaign.KnowledgePoints;

            // Generate report using MissionReportEvaluator
            bool isFailedEarly = mission.Status == MissionStatus.Failed;
            string earlyFailureReason = null;
            if (isFailedEarly)
            {
                earlyFailureReason = mission.EdlResult != null && !mission.EdlResult.IsSuccessful
                    ? $"İniş arızası: {mission.EdlResult.PlainLanguageSummary}"
                    : (mission.LaunchResult != null && !mission.LaunchResult.IsSuccessful
                        ? $"Fırlatma arızası: {mission.LaunchResult.PlainLanguageSummary}"
                        : "Seyir sırasında kritik arıza nedeniyle görev kaybedildi.");
            }

            GeneratedReport = MissionReportEvaluator.Evaluate(
                missionId: mission.MissionId,
                missionName: mission.MissionName,
                destinationId: mission.DestinationId,
                profile: mission.Profile,
                blueprint: mission.Blueprint,
                launchResult: mission.LaunchResult,
                edlResult: mission.EdlResult,
                downlinkedGb: mission.AccumulatedDownlinkDataGb,
                rawGeneratedGb: mission.AccumulatedRawDataGeneratedGb,
                droppedGb: mission.AccumulatedDroppedDataGb,
                scienceDaysCompleted: mission.ScienceDaysCompleted,
                accumulatedReliabilityPenalty: mission.AccumulatedReliabilityPenalty,
                accumulatedCorrectionFuel: mission.AccumulatedCorrectionFuel,
                operationEvents: mission.OperationEventLog,
                isMissionFailedEarly: isFailedEarly,
                earlyFailureReason: earlyFailureReason
            );

            // Assign report to mission instance
            mission.Report = GeneratedReport;
            mission.Status = GeneratedReport.IsOverallSuccess ? MissionStatus.Completed : MissionStatus.Failed;

            // Apply campaign financial and progress impacts
            var impact = GeneratedReport.CampaignImpact;
            if (impact != null)
            {
                Campaign.Budget = Math.Max(0.0, Campaign.Budget + impact.BudgetChange);
                Campaign.Reputation = Math.Max(0.0, Math.Min(100.0, Campaign.Reputation + impact.ReputationChange));
                Campaign.ResearchPoints = Math.Max(0.0, Campaign.ResearchPoints + impact.ResearchPointsGranted);
                Campaign.KnowledgePoints = Math.Max(0.0, Campaign.KnowledgePoints + impact.KnowledgePointsGranted);
            }

            // Evaluate campaign milestones (GDD §4.4, M6-01)
            if (Campaign.Milestones != null && GeneratedReport.IsOverallSuccess)
            {
                Campaign.Milestones.EvaluateProgress(Campaign.Missions.Values, Campaign.Time?.CurrentDate.ToString());
            }

            _hasExecuted = true;
            return CommandExecutionResult.Succeeded(
                $"Misyon sonuç raporu oluşturuldu: {(GeneratedReport.IsOverallSuccess ? "BAŞARILI" : "BAŞARISIZ")} - {GeneratedReport.FinalStatusSummary}");
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
            {
                return CommandExecutionResult.Failed("Geri alınacak bir sonuçlandırma işlemi bulunamadı.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
            {
                return CommandExecutionResult.Failed($"Misyon bulunamadı: '{MissionId}'.");
            }

            mission.Status = _prevStatus;
            mission.Report = _prevReport;
            Campaign.Budget = _prevBudget;
            Campaign.Reputation = _prevReputation;
            Campaign.ResearchPoints = _prevResearchPoints;
            Campaign.KnowledgePoints = _prevKnowledgePoints;

            GeneratedReport = null;
            _hasExecuted = false;

            return CommandExecutionResult.Succeeded("Misyon sonuçlandırma işlemi ve kampanya etkileri geri alındı.");
        }
    }
}
