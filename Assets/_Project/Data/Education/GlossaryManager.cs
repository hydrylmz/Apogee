using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Data;

namespace Apogee.Core.Education
{
    /// <summary>
    /// In-memory manager and query service for game educational glossary terms.
    /// Complies with GDD §13.2, §14.1 #18, and §23.
    /// </summary>
    public sealed class GlossaryManager
    {
        private readonly List<GlossaryTermRecord> _terms;

        public IReadOnlyList<GlossaryTermRecord> AllTerms => _terms;

        public GlossaryManager(IEnumerable<GlossaryTermRecord> terms)
        {
            _terms = terms != null ? terms.Where(t => t != null).ToList() : new List<GlossaryTermRecord>();
        }

        public GlossaryTermRecord GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _terms.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<string> GetCategories()
        {
            return _terms
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
        }

        public IReadOnlyList<GlossaryTermRecord> Search(string query, string categoryFilter = null)
        {
            var results = _terms.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(categoryFilter) && !string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                results = results.Where(t => string.Equals(t.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                string q = query.Trim().ToLowerInvariant();
                results = results.Where(t =>
                    (t.Term != null && t.Term.ToLowerInvariant().Contains(q)) ||
                    (t.PlainTitle != null && t.PlainTitle.ToLowerInvariant().Contains(q)) ||
                    (t.Summary != null && t.Summary.ToLowerInvariant().Contains(q)) ||
                    (t.WhyItMatters != null && t.WhyItMatters.ToLowerInvariant().Contains(q)));
            }

            return results.ToList();
        }
    }
}
