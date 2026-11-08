using System;
using System.Collections.Generic;
using Apogee.Core;
using Apogee.Core.Simulation.Operations;

namespace Apogee.Strategy.Mission
{
    /// <summary>
    /// Runs the operation event roll for the current turn of a mission
    /// and, if an event fires, creates a new <see cref="ActiveOperationEvent"/>
    /// on the mission's pending slot.
    ///
    /// Typically called at the start of AdvanceCruiseCommand, ExecuteEdlCommand,
    /// or ExecuteScienceTurnCommand when the mission has no pending event.
    ///
    /// Supports symmetric Undo/Redo so the event roll can be reversed.
    ///
    /// GDD §10.2, §10.3, §17.3, §21 M5-05.
    /// </summary>
    public sealed class TriggerOperationEventCommand : CommandBase
    {
        public override string Name => "TriggerOperationEventCommand";

        // Any director-level role can trigger the check (usually called internally by other commands)
        public override PlayerRole RequiredRoles =>
            PlayerRole.AgencyDirector | PlayerRole.ScienceDirector | PlayerRole.LeadEngineer;

        public override bool CanUndo => true;

        // ─── Inputs ────────────────────────────────────────────────────────
        public CampaignState Campaign               { get; }
        public string MissionId                     { get; }
        public OperationEventPhase Phase            { get; }
        public OperationHealthSnapshot Health       { get; }
        public int TurnIndex                        { get; }
        public int TotalTurns                       { get; }

        // ─── Output ────────────────────────────────────────────────────────
        /// <summary>The event that was triggered, or null if no event fired.</summary>
        public ActiveOperationEvent TriggeredEvent { get; private set; }

        // ─── Undo State ────────────────────────────────────────────────────
        private ActiveOperationEvent _prevPendingEvent;
        private bool _hasExecuted;

        private static readonly IReadOnlyList<OperationEventRecord> _catalog
            = OperationEventManager.BuildCatalog();

        public TriggerOperationEventCommand(
            CampaignState campaign,
            string missionId,
            OperationEventPhase phase,
            OperationHealthSnapshot health,
            int turnIndex,
            int totalTurns)
        {
            Campaign   = campaign ?? throw new ArgumentNullException(nameof(campaign));
            MissionId  = missionId ?? throw new ArgumentNullException(nameof(missionId));
            Phase      = phase;
            Health     = health ?? throw new ArgumentNullException(nameof(health));
            TurnIndex  = turnIndex;
            TotalTurns = totalTurns;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context != null && (context.ActorRole & RequiredRoles) == 0)
            {
                return CommandValidationResult.Fail(
                    $"Oyuncu rolü ({context.ActorRole}) operasyon olay tetikleyicisi yetkisine sahip değil.");
            }

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandValidationResult.Fail($"Misyon bulunamadı: '{MissionId}'.");

            if (mission.PendingOperationEvent != null)
                return CommandValidationResult.Fail(
                    "Zaten bekleyen bir operasyon olayı var; önce ResolveOperationEventCommand çalıştırılmalı.");

            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            var validation = CanExecute(context);
            if (!validation.IsValid)
                return CommandExecutionResult.Failed(validation.Reason);

            var mission = Campaign.Missions[MissionId];
            _prevPendingEvent = mission.PendingOperationEvent; // should be null

            // Obtain a deterministic RNG stream keyed to the mission seed + turn
            var rng = new SeededRng(mission.MissionSeed + 0x0F0F_0000UL + (ulong)TurnIndex);

            // Check whether an event should fire
            bool fires = OperationEventManager.ShouldFireEvent(
                Health, Phase, TurnIndex, TotalTurns, rng);

            if (!fires)
            {
                _hasExecuted = true;
                TriggeredEvent = null;
                return CommandExecutionResult.Succeeded("Bu turda operasyon olayı tetiklenmedi.");
            }

            // Select which event fires
            var selected = OperationEventManager.SelectEvent(
                _catalog, Phase, Health, rng,
                mission.UsedNonRepeatableOperationEvents);

            if (selected == null)
            {
                _hasExecuted = true;
                TriggeredEvent = null;
                return CommandExecutionResult.Succeeded("Uygun operasyon olayı bulunamadı.");
            }

            // Create instance
            var evt = new ActiveOperationEvent(
                instanceId: $"OPEVT-{mission.MissionId}-T{TurnIndex}",
                record: selected,
                phase: Phase,
                triggerTurn: TurnIndex);

            mission.PendingOperationEvent = evt;
            TriggeredEvent = evt;

            _hasExecuted = true;
            return CommandExecutionResult.Succeeded(
                $"Operasyon olayı tetiklendi: '{selected.EventId}' (Faz: {Phase}).");
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (!_hasExecuted)
                return CommandExecutionResult.Failed("Geri alınacak bir olay tetikleyici yok.");

            if (!Campaign.Missions.TryGetValue(MissionId, out var mission))
                return CommandExecutionResult.Failed($"Misyon bulunamadı: '{MissionId}'.");

            mission.PendingOperationEvent = _prevPendingEvent;
            TriggeredEvent = null;

            _hasExecuted = false;
            return CommandExecutionResult.Succeeded("Operasyon olay tetikleyicisi geri alındı.");
        }
    }
}
