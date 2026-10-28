using System;
using System.Collections.Generic;

namespace Apogee.Data
{
    /// <summary>
    /// An immutable, validated collection of catalog records of type <typeparamref name="T"/>.
    /// Produced exclusively by <see cref="CatalogLoader"/>; consumers treat it as read-only.
    ///
    /// Guarantees (when <see cref="Validation"/>.<see cref="CatalogValidationResult.IsValid"/> is true):
    ///   • Every record has a non-null, non-empty <see cref="ICatalogRecord.Id"/>.
    ///   • No two records share the same <c>Id</c> (case-sensitive).
    ///   • The internal dictionary enables O(1) lookup by id.
    /// </summary>
    /// <typeparam name="T">A concrete catalog record type implementing <see cref="ICatalogRecord"/>.</typeparam>
    public sealed class CatalogCollection<T> where T : class, ICatalogRecord
    {
        private readonly Dictionary<string, T> _byId;

        /// <summary>All records in load order.</summary>
        public IReadOnlyList<T> All { get; }

        /// <summary>Validation result produced during construction.</summary>
        public CatalogValidationResult Validation { get; }

        /// <summary>Number of records in this collection.</summary>
        public int Count => All.Count;

        /// <summary>
        /// Returns the record with the given <paramref name="id"/>, or <c>null</c> if not found.
        /// </summary>
        public T GetById(string id)
        {
            if (id == null) return null;
            _byId.TryGetValue(id, out T record);
            return record;
        }

        /// <summary>
        /// Returns <c>true</c> and sets <paramref name="record"/> if the id exists.
        /// </summary>
        public bool TryGetById(string id, out T record)
        {
            if (id == null) { record = null; return false; }
            return _byId.TryGetValue(id, out record);
        }

        /// <summary>
        /// Internal constructor — only <see cref="CatalogLoader"/> should call this.
        /// </summary>
        internal CatalogCollection(IReadOnlyList<T> records, CatalogValidationResult validation)
        {
            All        = records   ?? throw new ArgumentNullException(nameof(records));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));

            // Build lookup dictionary (only for valid ids; duplicates already reported in validation).
            _byId = new Dictionary<string, T>(All.Count, StringComparer.Ordinal);
            foreach (var r in All)
            {
                if (r != null && !string.IsNullOrEmpty(r.Id) && !_byId.ContainsKey(r.Id))
                    _byId[r.Id] = r;
            }
        }

        public override string ToString() =>
            $"CatalogCollection<{typeof(T).Name}> count={Count}  {Validation}";
    }
}
