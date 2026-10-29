using System;
using System.Collections.Generic;

namespace Apogee.Design
{
    /// <summary>
    /// Represents a saved vehicle design template with metadata (GDD §7.5 & §19.11).
    /// Stores the vehicle Blueprint, user-assigned name, description, creation timestamp, and tags.
    /// </summary>
    public sealed class DesignTemplate
    {
        private readonly List<string> _tags = new List<string>();

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public long CreatedAtTicks { get; }
        public Blueprint Blueprint { get; }
        public IReadOnlyList<string> Tags => _tags;

        public DesignTemplate(
            string id,
            string name,
            Blueprint blueprint,
            string description = null,
            long createdAtTicks = 0,
            IEnumerable<string> tags = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Template ID cannot be empty.", nameof(id));
            if (blueprint == null)
                throw new ArgumentNullException(nameof(blueprint), "Template must reference a valid Blueprint.");

            Id = id;
            Name = !string.IsNullOrWhiteSpace(name) ? name : blueprint.Name;
            Description = description ?? string.Empty;
            CreatedAtTicks = createdAtTicks;
            Blueprint = blueprint;

            if (tags != null)
            {
                _tags.AddRange(tags);
            }
        }

        public DesignTemplate WithName(string newName)
        {
            return new DesignTemplate(Id, newName, Blueprint.WithName(newName), Description, CreatedAtTicks, _tags);
        }

        public DesignTemplate WithDescription(string newDescription)
        {
            return new DesignTemplate(Id, Name, Blueprint, newDescription, CreatedAtTicks, _tags);
        }

        public DesignTemplate WithBlueprint(Blueprint newBlueprint)
        {
            return new DesignTemplate(Id, Name, newBlueprint, Description, CreatedAtTicks, _tags);
        }

        public DesignTemplate Clone(string newId, string newName)
        {
            var clonedBlueprint = Blueprint.WithId(newId).WithName(newName);
            return new DesignTemplate(newId, newName, clonedBlueprint, Description, CreatedAtTicks, _tags);
        }

        public override string ToString() => $"[DesignTemplate '{Name}' ({Id}) Blueprint: {Blueprint.Name}]";
    }
}
