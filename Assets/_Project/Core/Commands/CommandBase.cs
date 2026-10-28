using System;

namespace Apogee.Core
{
    /// <summary>
    /// Abstract base class for commands with default identifier and validation logic.
    /// </summary>
    public abstract class CommandBase : ICommand
    {
        public string Id { get; }
        public virtual string Name => GetType().Name;
        public virtual PlayerRole RequiredRoles => PlayerRole.All;
        public virtual bool CanUndo => true;

        protected CommandBase(string id = null)
        {
            Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id;
        }

        public abstract CommandValidationResult CanExecute(CommandContext context);

        public abstract CommandExecutionResult Execute(CommandContext context);

        public virtual CommandExecutionResult Undo(CommandContext context)
        {
            if (!CanUndo)
            {
                return CommandExecutionResult.Failed($"Command '{Name}' does not support Undo.");
            }

            return CommandExecutionResult.Failed($"Undo not implemented for command '{Name}'.");
        }
    }

    /// <summary>
    /// Strongly-typed base class for commands operating on a specific state model (e.g. CampaignState).
    /// </summary>
    public abstract class CommandBase<TState> : CommandBase where TState : class
    {
        protected CommandBase(string id = null) : base(id)
        {
        }

        public sealed override CommandValidationResult CanExecute(CommandContext context)
        {
            if (context == null)
            {
                return CommandValidationResult.Fail("CommandContext cannot be null.");
            }

            var state = context.GetState<TState>();
            if (state == null)
            {
                return CommandValidationResult.Fail($"Target state is not of type {typeof(TState).Name}.");
            }

            return CanExecute(state, context);
        }

        public sealed override CommandExecutionResult Execute(CommandContext context)
        {
            if (context == null)
            {
                return CommandExecutionResult.Failed("CommandContext cannot be null.");
            }

            var state = context.GetState<TState>();
            if (state == null)
            {
                return CommandExecutionResult.Failed($"Target state is not of type {typeof(TState).Name}.");
            }

            return Execute(state, context);
        }

        public sealed override CommandExecutionResult Undo(CommandContext context)
        {
            if (!CanUndo)
            {
                return CommandExecutionResult.Failed($"Command '{Name}' does not support Undo.");
            }

            if (context == null)
            {
                return CommandExecutionResult.Failed("CommandContext cannot be null.");
            }

            var state = context.GetState<TState>();
            if (state == null)
            {
                return CommandExecutionResult.Failed($"Target state is not of type {typeof(TState).Name}.");
            }

            return Undo(state, context);
        }

        protected abstract CommandValidationResult CanExecute(TState state, CommandContext context);
        protected abstract CommandExecutionResult Execute(TState state, CommandContext context);
        protected virtual CommandExecutionResult Undo(TState state, CommandContext context)
        {
            return CommandExecutionResult.Failed($"Undo not implemented for command '{Name}'.");
        }
    }
}
