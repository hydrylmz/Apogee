namespace Apogee.Core
{
    /// <summary>
    /// Result of an executed or undone command.
    /// Informs whether operation succeeded and provides error/status messages.
    /// </summary>
    public readonly struct CommandExecutionResult
    {
        public bool Success { get; }
        public string Message { get; }
        public string ErrorCode { get; }

        public CommandExecutionResult(bool success, string message = "", string errorCode = null)
        {
            Success = success;
            Message = message ?? string.Empty;
            ErrorCode = errorCode;
        }

        public static CommandExecutionResult Succeeded(string message = "") =>
            new CommandExecutionResult(true, message);

        public static CommandExecutionResult Failed(string reason, string errorCode = null) =>
            new CommandExecutionResult(false, reason, errorCode);

        public override string ToString() => Success
            ? (string.IsNullOrEmpty(Message) ? "Success" : $"Success: {Message}")
            : $"Failed: {Message} (Code: {ErrorCode ?? "None"})";
    }
}
