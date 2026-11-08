using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;

namespace Apogee.Core.Attribution
{
    /// <summary>
    /// In-memory query service for APOGEE attribution records (GDD §17.7, M8-06).
    /// Pure C# — no Unity dependencies.
    /// </summary>
    public sealed class AttributionManager
    {
        private readonly List<AttributionRecord> _records;

        public IReadOnlyList<AttributionRecord> AllRecords => _records;

        public AttributionManager(IEnumerable<AttributionRecord> records)
        {
            _records = records != null
                ? records.Where(r => r != null).ToList()
                : new List<AttributionRecord>();
        }

        /// <summary>Returns all distinct categories that have at least one record.</summary>
        public IReadOnlyList<AttributionCategory> GetCategories()
        {
            return _records
                .Select(r => r.Category)
                .Distinct()
                .OrderBy(c => (int)c)
                .ToList();
        }

        /// <summary>Returns all records for the given category.</summary>
        public IReadOnlyList<AttributionRecord> GetByCategory(AttributionCategory category)
        {
            return _records
                .Where(r => r.Category == category)
                .ToList();
        }

        /// <summary>Returns the record with the given id, or null if not found.</summary>
        public AttributionRecord GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _records.FirstOrDefault(r =>
                string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Returns all NASA data sources.</summary>
        public IReadOnlyList<AttributionRecord> GetNasaSources()
            => GetByCategory(AttributionCategory.NasaData);

        /// <summary>Returns records whose title or source matches the query (case-insensitive).</summary>
        public IReadOnlyList<AttributionRecord> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return _records;

            string q = query.Trim().ToLowerInvariant();
            return _records
                .Where(r =>
                    (r.Title != null && r.Title.ToLowerInvariant().Contains(q)) ||
                    (r.Source != null && r.Source.ToLowerInvariant().Contains(q)) ||
                    (r.Description != null && r.Description.ToLowerInvariant().Contains(q)))
                .ToList();
        }
    }
}
