using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apogee.Data
{
    /// <summary>
    /// Loads and validates JSON catalog files into <see cref="CatalogCollection{T}"/> instances.
    ///
    /// ADR-005: All catalog data is stored as JSON (no ScriptableObjects).
    ///
    /// JSON format expected — a root object with a single array property whose name matches
    /// the record type, OR a bare JSON array:
    ///
    ///   Wrapped form (preferred):
    ///     { "modules": [ { "id": "...", ... }, ... ] }
    ///
    ///   Bare array form (also accepted):
    ///     [ { "id": "...", ... }, ... ]
    ///
    /// Validation rules (errors = blocking):
    ///   1. JSON must parse without exception.
    ///   2. Root must be an array or an object containing exactly one array.
    ///   3. Every record must deserialize to a non-null object.
    ///   4. Every record must have a non-null, non-empty <c>id</c> field.
    ///   5. No two records may share the same <c>id</c> (case-sensitive).
    ///
    /// GDD §21 M1-05 Kabul kriteri:
    ///   "Bozuk veri anlaşılır hatayla reddedilir."
    /// </summary>
    public static class CatalogLoader
    {
        // ─── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Parses <paramref name="json"/> and returns a validated
        /// <see cref="CatalogCollection{T}"/>.
        ///
        /// The collection is always returned (never null); check
        /// <c>result.Validation.IsValid</c> before use in production code.
        /// </summary>
        /// <typeparam name="T">Concrete record type implementing <see cref="ICatalogRecord"/>.</typeparam>
        /// <param name="json">Raw JSON string. May be null or empty (produces an error).</param>
        /// <param name="settings">
        ///   Optional Newtonsoft.Json settings. If null, a permissive default is used
        ///   (MissingMemberHandling.Ignore, NullValueHandling.Ignore).
        /// </param>
        public static CatalogCollection<T> Load<T>(string json, JsonSerializerSettings settings = null)
            where T : class, ICatalogRecord
        {
            var errors   = new List<string>();
            var warnings = new List<string>();

            // ── 1. Null / empty guard ────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("Catalog JSON is null or empty.");
                return Empty<T>(errors, warnings);
            }

            // ── 2. Parse root token ──────────────────────────────────────────────
            JToken root;
            try
            {
                root = JToken.Parse(json);
            }
            catch (JsonException ex)
            {
                errors.Add($"JSON parse error: {ex.Message}");
                return Empty<T>(errors, warnings);
            }

            // ── 3. Find the array ────────────────────────────────────────────────
            JArray array = ExtractArray(root, errors, warnings);
            if (array == null)
                return Empty<T>(errors, warnings);

            // ── 4. Deserialize each element ──────────────────────────────────────
            var serializer = JsonSerializer.CreateDefault(settings ?? DefaultSettings());
            var records    = new List<T>(array.Count);

            for (int i = 0; i < array.Count; i++)
            {
                JToken element = array[i];
                if (element == null || element.Type == JTokenType.Null)
                {
                    errors.Add($"Record at index {i} is null — every entry must be a JSON object.");
                    continue;
                }

                if (element is JObject obj && (obj["id"] == null || obj["id"].Type == JTokenType.Null || string.IsNullOrWhiteSpace(obj["id"].ToString())))
                {
                    errors.Add($"Record at index {i} has null or empty 'id'.");
                    continue;
                }

                T record;
                try
                {
                    record = element.ToObject<T>(serializer);
                }
                catch (JsonException ex)
                {
                    errors.Add($"Record at index {i} failed to deserialize: {ex.Message}");
                    continue;
                }

                if (record == null)
                {
                    errors.Add($"Record at index {i} deserialized to null.");
                    continue;
                }

                records.Add(record);
            }

            // ── 5. Validate ids ──────────────────────────────────────────────────
            ValidateIds(records, errors);

            var validation = new CatalogValidationResult(errors, warnings);
            return new CatalogCollection<T>(records, validation);
        }

        /// <summary>
        /// Validates an already-deserialized list without re-parsing JSON.
        /// Useful for testing record models directly.
        /// </summary>
        public static CatalogValidationResult Validate<T>(IReadOnlyList<T> records)
            where T : class, ICatalogRecord
        {
            if (records == null)
                return new CatalogValidationResult(new[] { "Record list is null." }, Array.Empty<string>());

            var errors   = new List<string>();
            var warnings = new List<string>();
            ValidateIds(records, errors);
            return new CatalogValidationResult(errors, warnings);
        }

        // ─── Private helpers ─────────────────────────────────────────────────────

        private static JArray ExtractArray(JToken root, List<string> errors, List<string> warnings)
        {
            if (root.Type == JTokenType.Array)
                return (JArray)root;

            if (root.Type == JTokenType.Object)
            {
                var obj = (JObject)root;
                JArray found = null;
                string foundKey = null;

                foreach (var prop in obj.Properties())
                {
                    if (prop.Value.Type == JTokenType.Array)
                    {
                        if (found == null)
                        {
                            found    = (JArray)prop.Value;
                            foundKey = prop.Name;
                        }
                        else
                        {
                            // Multiple arrays in root — ambiguous; take the first and warn.
                            warnings.Add(
                                $"Root object has multiple array properties ('{foundKey}', '{prop.Name}', …). " +
                                $"Using '{foundKey}'. Wrap the catalog in an object with a single array property.");
                            break;
                        }
                    }
                }

                if (found != null)
                    return found;

                errors.Add("Root JSON object contains no array property. " +
                            "Expected a bare array [ … ] or an object with one array property.");
                return null;
            }

            errors.Add($"Unexpected JSON root type '{root.Type}'. Expected array or object.");
            return null;
        }

        private static void ValidateIds<T>(IReadOnlyList<T> records, List<string> errors)
            where T : class, ICatalogRecord
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null) continue;

                if (string.IsNullOrWhiteSpace(r.Id))
                {
                    errors.Add($"Record at index {i} (type {r.GetType().Name}) has a null or empty 'id'. " +
                               "Every catalog entry must have a unique non-empty id.");
                    continue;
                }

                if (!seen.Add(r.Id))
                {
                    errors.Add($"Duplicate id '{r.Id}' found at index {i}. " +
                               "Catalog ids must be unique within their collection.");
                }
            }
        }

        private static CatalogCollection<T> Empty<T>(List<string> errors, List<string> warnings)
            where T : class, ICatalogRecord
            => new CatalogCollection<T>(
                new List<T>(),
                new CatalogValidationResult(errors, warnings));

        private static JsonSerializerSettings DefaultSettings() => new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling     = NullValueHandling.Ignore,
        };
    }
}
