using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core;

namespace Apogee.Strategy
{
    /// <summary>
    /// Command: cancels an in-progress or queued research node.
    /// Refunds the start cost (if any) back to the campaign budget.
    ///
    /// GDD §9.2, §21 M4-05.
    /// Undo: re-enqueues the node (without re-charging the start cost).
    /// </summary>
    public sealed class CancelResearchCommand : CommandBase<CampaignState>
    {
        public string NodeId { get; }

        private double _refundedCost;

        public CancelResearchCommand(string nodeId, string id = null) : base(id)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        }

        public override string Name => nameof(CancelResearchCommand);
        public override PlayerRole RequiredRoles => PlayerRole.All;
        public override bool CanUndo => true;

        protected override CommandValidationResult CanExecute(CampaignState state, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(NodeId))
                return CommandValidationResult.Fail("Araştırma düğüm kimliği boş olamaz.");

            if (state.Research == null)
                return CommandValidationResult.Fail("Araştırma yöneticisi kampanyada başlatılmamış.");

            if (!state.Research.Queue.Any(id => string.Equals(id, NodeId, StringComparison.OrdinalIgnoreCase)))
                return CommandValidationResult.Fail($"'{NodeId}' araştırması kuyrukta değil veya zaten tamamlandı.");

            return CommandValidationResult.Success();
        }

        protected override CommandExecutionResult Execute(CampaignState state, CommandContext context)
        {
            var record = state.Research.GetRecord(NodeId);
            _refundedCost = record?.StartCostMUSD ?? 0;

            var (ok, reason) = state.Research.TryCancel(NodeId);
            if (!ok)
                return CommandExecutionResult.Failed(reason);

            state.Budget += _refundedCost;
            return CommandExecutionResult.Succeeded(
                $"'{NodeId}' araştırması iptal edildi. İade: {_refundedCost:F0}M$.");
        }

        protected override CommandExecutionResult Undo(CampaignState state, CommandContext context)
        {
            // Re-enqueue without re-charging (restores pre-cancel state)
            state.Research.TryEnqueue(NodeId, context.Tick, double.MaxValue, out _);
            state.Budget -= _refundedCost;
            return CommandExecutionResult.Succeeded(
                $"CancelResearchCommand geri alındı: '{NodeId}' yeniden kuyruğa eklendi.");
        }
    }
}
