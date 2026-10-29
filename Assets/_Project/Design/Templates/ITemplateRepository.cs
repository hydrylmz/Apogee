using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Repository contract for saving, retrieving, copying, renaming, and deleting design templates (GDD §7.5).
    /// </summary>
    public interface ITemplateRepository
    {
        IReadOnlyList<DesignTemplate> GetAll();
        DesignTemplate GetById(string id);
        bool Contains(string id);
        void Save(DesignTemplate template);
        bool Delete(string id);
        DesignTemplate Duplicate(string sourceId, string newId, string newName);
        bool Rename(string id, string newName);

        event Action OnTemplatesChanged;
    }
}
