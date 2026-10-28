namespace Apogee.Core
{
    /// <summary>
    /// Result of a pre-execution command validation check.
    /// Carries human-readable / localization failure reason.
    /// </summary>
    public readonly struct CommandValidationResult
    {
        public bool IsValid { get; }
        public string Reason { get; }

        public CommandValidationResult(bool isValid, string reason = "")
        {
            IsValid = isValid;
            Reason = reason ?? string.Empty;
        }

        public static CommandValidationResult Success() => new CommandValidationResult(true, string.Empty);

        public static CommandValidationResult Fail(string reason) => new CommandValidationResult(false, reason);

        public override string ToString() => IsValid ? "Valid" : $"Invalid: {Reason}";
    }
}
