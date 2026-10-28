using System.Collections.Generic;

namespace Apogee.Data
{
    /// <summary>
    /// Represents the outcome of validating a catalog JSON file.
    /// Immutable; produced by <see cref="CatalogLoader.Validate{T}"/> or
    /// embedded in <see cref="CatalogCollection{T}"/>.
    ///
    /// GDD §21 M1-05 Kabul kriteri:
    ///   "Bozuk veri anlaşılır hatayla reddedilir."
    /// </summary>
    public sealed class CatalogValidationResult
    {
        /// <summary>Whether the catalog passed all validation checks.</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Blocking errors — the catalog cannot be used until these are fixed.
        /// Examples: duplicate id, null id, JSON parse failure.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Non-blocking warnings — catalog can be loaded but data quality may be reduced.
        /// Examples: unknown extra fields, recommended-but-missing optional fields.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        public CatalogValidationResult(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Errors   = errors   ?? new List<string>();
            Warnings = warnings ?? new List<string>();
        }

        /// <summary>A passing result with no errors or warnings.</summary>
        public static readonly CatalogValidationResult Ok =
            new CatalogValidationResult(new List<string>(), new List<string>());

        public override string ToString() =>
            IsValid
                ? $"Valid ({Warnings.Count} warning(s))"
                : $"Invalid: {Errors.Count} error(s), {Warnings.Count} warning(s)";
    }
}
