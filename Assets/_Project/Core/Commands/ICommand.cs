namespace Apogee.Core
{
    /// <summary>
    /// Base interface for all commands that mutate game state.
    /// Commands support authorization checks, validation, execution, and symmetric undo.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Unique identifier for this command instance.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Human-readable type name or identifier for this command.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Bitmask of roles authorized to execute or approve this command.
        /// </summary>
        PlayerRole RequiredRoles { get; }

        /// <summary>
        /// Whether this command supports being undone.
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Checks whether the command is currently valid to execute against the target state.
        /// Does not mutate state.
        /// </summary>
        CommandValidationResult CanExecute(CommandContext context);

        /// <summary>
        /// Executes the command and mutates state.
        /// </summary>
        CommandExecutionResult Execute(CommandContext context);

        /// <summary>
        /// Reverses the mutations performed by Execute.
        /// </summary>
        CommandExecutionResult Undo(CommandContext context);
    }
}
