using System;
using System.Collections.Generic;

namespace Apogee.Core
{
    /// <summary>
    /// Central dispatcher for executing, validating, auditing, and undoing/redoing state-mutating commands.
    /// Engine-agnostic, deterministic, and supports multi-role authorization.
    /// </summary>
    public sealed class CommandBus
    {
        private readonly List<ICommand> _undoStack = new List<ICommand>();
        private readonly List<ICommand> _redoStack = new List<ICommand>();
        private readonly List<CommandRecord> _history = new List<CommandRecord>();

        private ulong _nextSequenceNumber = 1;
        private int _maxHistorySize = 100;

        /// <summary>
        /// Maximum number of undoable commands stored in history.
        /// </summary>
        public int MaxHistorySize
        {
            get => _maxHistorySize;
            set => _maxHistorySize = Math.Max(1, value);
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Linear audit trail of all commands successfully executed through this bus.
        /// </summary>
        public IReadOnlyList<CommandRecord> History => _history;

        public event Action<ICommand, CommandRecord> CommandExecuted;
        public event Action<ICommand> CommandUndone;
        public event Action<ICommand> CommandRedone;
        public event Action<ICommand, string> CommandRejected;

        public CommandBus(int maxHistorySize = 100)
        {
            MaxHistorySize = maxHistorySize;
        }

        /// <summary>
        /// Executes a command after performing role authorization and validity checks.
        /// </summary>
        public CommandExecutionResult Execute(ICommand command, CommandContext context)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // 1. Role Authorization Check
            if (!IsAuthorized(context.ActorRole, command.RequiredRoles))
            {
                string reason = $"Unauthorized: Actor role '{context.ActorRole}' does not satisfy required role(s) '{command.RequiredRoles}'.";
                CommandRejected?.Invoke(command, reason);
                return CommandExecutionResult.Failed(reason, "UNAUTHORIZED");
            }

            // 2. State Validation Check
            CommandValidationResult validation = command.CanExecute(context);
            if (!validation.IsValid)
            {
                string reason = string.IsNullOrEmpty(validation.Reason)
                    ? $"Validation failed for command '{command.Name}'."
                    : validation.Reason;
                CommandRejected?.Invoke(command, reason);
                return CommandExecutionResult.Failed(reason, "VALIDATION_FAILED");
            }

            // 3. Execution
            CommandExecutionResult result = command.Execute(context);
            if (!result.Success)
            {
                CommandRejected?.Invoke(command, result.Message);
                return result;
            }

            // 4. Record and Undo History
            var record = new CommandRecord(
                sequenceNumber: _nextSequenceNumber++,
                commandId: command.Id,
                commandType: command.Name,
                requiredRoles: command.RequiredRoles,
                actorRole: context.ActorRole,
                playerId: context.PlayerId,
                tick: context.Tick
            );

            _history.Add(record);

            if (command.CanUndo)
            {
                _undoStack.Add(command);
                if (_undoStack.Count > _maxHistorySize)
                {
                    _undoStack.RemoveAt(0);
                }

                // Any new action clears the forward redo history
                _redoStack.Clear();
            }

            CommandExecuted?.Invoke(command, record);
            return result;
        }

        /// <summary>
        /// Reverses the most recently executed undoable command.
        /// </summary>
        public CommandExecutionResult Undo(CommandContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_undoStack.Count == 0)
            {
                return CommandExecutionResult.Failed("Nothing to undo.", "STACK_EMPTY");
            }

            int lastIndex = _undoStack.Count - 1;
            ICommand command = _undoStack[lastIndex];
            _undoStack.RemoveAt(lastIndex);

            CommandExecutionResult undoResult = command.Undo(context);
            if (!undoResult.Success)
            {
                // Restore to undo stack on failure so state isn't orphaned
                _undoStack.Add(command);
                return undoResult;
            }

            _redoStack.Add(command);
            CommandUndone?.Invoke(command);
            return undoResult;
        }

        /// <summary>
        /// Re-applies the most recently undone command.
        /// </summary>
        public CommandExecutionResult Redo(CommandContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_redoStack.Count == 0)
            {
                return CommandExecutionResult.Failed("Nothing to redo.", "STACK_EMPTY");
            }

            int lastIndex = _redoStack.Count - 1;
            ICommand command = _redoStack[lastIndex];
            _redoStack.RemoveAt(lastIndex);

            // Re-validate against current state
            CommandValidationResult validation = command.CanExecute(context);
            if (!validation.IsValid)
            {
                _redoStack.Add(command);
                string reason = string.IsNullOrEmpty(validation.Reason)
                    ? $"Cannot redo '{command.Name}': validation failed."
                    : validation.Reason;
                CommandRejected?.Invoke(command, reason);
                return CommandExecutionResult.Failed(reason, "REDO_VALIDATION_FAILED");
            }

            CommandExecutionResult result = command.Execute(context);
            if (!result.Success)
            {
                _redoStack.Add(command);
                CommandRejected?.Invoke(command, result.Message);
                return result;
            }

            _undoStack.Add(command);
            CommandRedone?.Invoke(command);
            return result;
        }

        /// <summary>
        /// Resets all undo/redo and audit history.
        /// </summary>
        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _history.Clear();
        }

        /// <summary>
        /// Checks whether the actor possesses at least one of the required authorization roles.
        /// </summary>
        public static bool IsAuthorized(PlayerRole actorRole, PlayerRole requiredRoles)
        {
            if (requiredRoles == PlayerRole.None)
            {
                return true;
            }

            return (actorRole & requiredRoles) != 0;
        }
    }
}
