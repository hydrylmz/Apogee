using System;
using System.Collections.Generic;
using System.Linq;

namespace Apogee.Design
{
    /// <summary>
    /// Thread-safe in-memory implementation of <see cref="ITemplateRepository"/>.
    /// Used by default in Workshop and Unit Tests.
    /// </summary>
    public class InMemoryTemplateRepository : ITemplateRepository
    {
        private readonly Dictionary<string, DesignTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public event Action OnTemplatesChanged;

        public InMemoryTemplateRepository(IEnumerable<DesignTemplate> initialTemplates = null)
        {
            if (initialTemplates != null)
            {
                foreach (var t in initialTemplates)
                {
                    if (t != null)
                    {
                        _templates[t.Id] = t;
                    }
                }
            }
        }

        public IReadOnlyList<DesignTemplate> GetAll()
        {
            lock (_lock)
            {
                return _templates.Values.ToList();
            }
        }

        public DesignTemplate GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            lock (_lock)
            {
                return _templates.TryGetValue(id, out var template) ? template : null;
            }
        }

        public bool Contains(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            lock (_lock)
            {
                return _templates.ContainsKey(id);
            }
        }

        public void Save(DesignTemplate template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            lock (_lock)
            {
                _templates[template.Id] = template;
            }

            OnTemplatesChanged?.Invoke();
        }

        public bool Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            bool removed;
            lock (_lock)
            {
                removed = _templates.Remove(id);
            }

            if (removed)
            {
                OnTemplatesChanged?.Invoke();
            }

            return removed;
        }

        public DesignTemplate Duplicate(string sourceId, string newId, string newName)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("Source template ID cannot be empty.", nameof(sourceId));
            if (string.IsNullOrWhiteSpace(newId))
                throw new ArgumentException("New template ID cannot be empty.", nameof(newId));

            DesignTemplate cloned;
            lock (_lock)
            {
                if (!_templates.TryGetValue(sourceId, out var source))
                    throw new KeyNotFoundException($"Template '{sourceId}' not found.");

                cloned = source.Clone(newId, newName);
                _templates[newId] = cloned;
            }

            OnTemplatesChanged?.Invoke();
            return cloned;
        }

        public bool Rename(string id, string newName)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newName))
                return false;

            lock (_lock)
            {
                if (!_templates.TryGetValue(id, out var source))
                    return false;

                var renamed = source.WithName(newName);
                _templates[id] = renamed;
            }

            OnTemplatesChanged?.Invoke();
            return true;
        }
    }
}
