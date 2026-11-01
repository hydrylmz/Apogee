using System;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command: enqueues a research node for the campaign's research manager.
    /// Charges any start cost from the campaign budget.
    ///
    /// GDD §9.2, §21 M4-05.
    /// Authorized roles: All (any player may queue research).
    /// Undo: removes the node from queue and refunds start cost.
    /// </summary>
    public sealed class StartResearchCommand : CommandBase<CampaignState>
    {
        public string NodeId { get; }

        private double _startCostCharged;

        public StartResearchCommand(string nodeId, string id = null) : base(id)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        }

        public override string Name => nameof(StartResearchCommand);
        public override PlayerRole RequiredRoles => PlayerRole.All;
        public override bool CanUndo => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(NodeId))
                return CommandValidationResult.Fail("Araştırma düğüm kimliği boş olamaz.");

            if (state.Research == null)
                return CommandValidationResult.Fail("Araştırma yöneticisi kampanyada başlatılmamış.");

            var (ok, reason) = state.Research.TryEnqueue(NodeId, context.Tick, state.Budget, out _);
            if (!ok)
                return CommandValidationResult.Fail(reason);

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            state.Research.TryEnqueue(NodeId, context.Tick, state.Budget, out double cost);
            _startCostCharged = cost;
            if (cost > 0)
                state.Budget -= cost;

            return CommandExecutionResult.Succeeded(
                $"'{NodeId}' araştırması kuyruğa eklendi. Başlangıç maliyeti: {cost:F0}M$.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            var (ok, reason) = state.Research.TryCancel(NodeId);
            if (!ok)
                return CommandExecutionResult.Failed($"Geri alınamadı: {reason}");

            state.Budget += _startCostCharged;
            return CommandExecutionResult.Succeeded(
                $"StartResearchCommand geri alındı: '{NodeId}' kuyruktan çıkarıldı, {_startCostCharged:F0}M$ iade edildi.");
        }
    }
}
