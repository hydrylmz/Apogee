namespace Apogee.UI.Core.Tooltips
{
    public enum TooltipSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Immutable payload representing a rich tooltip in the APOGEE UI system (GDD §8.3 & §13.2).
    /// </summary>
    public sealed class TooltipData
    {
        public string Title { get; }
        public string Content { get; }
        public string Reason { get; }
        public string DeltaText { get; }
        public bool? IsPositiveDelta { get; }
        public TooltipSeverity Severity { get; }

        public TooltipData(
            string title,
            string content,
            string reason = null,
            string deltaText = null,
            bool? isPositiveDelta = null,
            TooltipSeverity severity = TooltipSeverity.Info)
        {
            Title = title ?? string.Empty;
            Content = content ?? string.Empty;
            Reason = reason;
            DeltaText = deltaText;
            IsPositiveDelta = isPositiveDelta;
            Severity = severity;
        }

        public static TooltipData CreateSimple(string title, string content)
        {
            return new TooltipData(title, content);
        }

        public static TooltipData CreateWithReason(string title, string content, string reason, TooltipSeverity severity = TooltipSeverity.Info)
        {
            return new TooltipData(title, content, reason, null, null, severity);
        }

        public static TooltipData CreateWithDelta(string title, string content, string deltaText, bool isPositive)
        {
            return new TooltipData(title, content, null, deltaText, isPositive, isPositive ? TooltipSeverity.Success : TooltipSeverity.Warning);
        }
    }
}
