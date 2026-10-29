using System;
using Apogee.Core;

namespace Apogee.Design.Commands
{
    /// <summary>
    /// Command: Save the current active Blueprint as a DesignTemplate.
    /// Undoable: removes the saved template on Undo.
    /// GDD §7.5.
    /// </summary>
    public sealed class SaveTemplateCommand : CommandBase
    {
        private readonly ITemplateRepository _repository;
        private readonly DesignTemplate _template;
        private bool _replacedExisting;
        private DesignTemplate _previousVersion;

        public override string Name => "SaveTemplate";
        public override bool CanUndo => true;
        public override PlayerRole RequiredRoles => PlayerRole.LeadEngineer | PlayerRole.AgencyDirector;

        public SaveTemplateCommand(ITemplateRepository repository, DesignTemplate template)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _template = template ?? throw new ArgumentNullException(nameof(template));
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (_template.Blueprint == null)
                return CommandValidationResult.Fail("Kaydedilecek blueprint boş.");
            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            _replacedExisting = _repository.Contains(_template.Id);
            _previousVersion = _replacedExisting ? _repository.GetById(_template.Id) : null;
            _repository.Save(_template);
            return CommandExecutionResult.Succeeded();
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (_replacedExisting && _previousVersion != null)
                _repository.Save(_previousVersion);
            else
                _repository.Delete(_template.Id);
            return CommandExecutionResult.Succeeded();
        }
    }

    /// <summary>
    /// Command: Rename an existing DesignTemplate.
    /// Undoable: reverts to old name.
    /// GDD §7.5.
    /// </summary>
    public sealed class RenameTemplateCommand : CommandBase
    {
        private readonly ITemplateRepository _repository;
        private readonly string _templateId;
        private readonly string _newName;
        private string _oldName;

        public override string Name => "RenameTemplate";
        public override bool CanUndo => true;
        public override PlayerRole RequiredRoles => PlayerRole.LeadEngineer | PlayerRole.AgencyDirector;

        public RenameTemplateCommand(ITemplateRepository repository, string templateId, string newName)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _templateId = templateId;
            _newName = newName;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(_templateId))
                return CommandValidationResult.Fail("Şablon ID boş.");
            if (string.IsNullOrWhiteSpace(_newName))
                return CommandValidationResult.Fail("Yeni isim boş olamaz.");
            if (!_repository.Contains(_templateId))
                return CommandValidationResult.Fail($"Şablon '{_templateId}' bulunamadı.");
            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            var existing = _repository.GetById(_templateId);
            _oldName = existing?.Name;
            _repository.Rename(_templateId, _newName);
            return CommandExecutionResult.Succeeded();
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (_oldName != null)
                _repository.Rename(_templateId, _oldName);
            return CommandExecutionResult.Succeeded();
        }
    }

    /// <summary>
    /// Command: Delete a DesignTemplate by ID.
    /// Undoable: re-saves the deleted template on Undo.
    /// GDD §7.5.
    /// </summary>
    public sealed class DeleteTemplateCommand : CommandBase
    {
        private readonly ITemplateRepository _repository;
        private readonly string _templateId;
        private DesignTemplate _deleted;

        public override string Name => "DeleteTemplate";
        public override bool CanUndo => true;
        public override PlayerRole RequiredRoles => PlayerRole.LeadEngineer | PlayerRole.AgencyDirector;

        public DeleteTemplateCommand(ITemplateRepository repository, string templateId)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _templateId = templateId;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(_templateId))
                return CommandValidationResult.Fail("Şablon ID boş.");
            if (!_repository.Contains(_templateId))
                return CommandValidationResult.Fail($"Şablon '{_templateId}' mevcut değil.");
            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            _deleted = _repository.GetById(_templateId);
            _repository.Delete(_templateId);
            return CommandExecutionResult.Succeeded();
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            if (_deleted != null)
                _repository.Save(_deleted);
            return CommandExecutionResult.Succeeded();
        }
    }

    /// <summary>
    /// Command: Duplicate a DesignTemplate under a new ID and name.
    /// Undoable: removes the duplicated template on Undo.
    /// GDD §7.5.
    /// </summary>
    public sealed class DuplicateTemplateCommand : CommandBase
    {
        private readonly ITemplateRepository _repository;
        private readonly string _sourceId;
        private readonly string _newId;
        private readonly string _newName;

        public override string Name => "DuplicateTemplate";
        public override bool CanUndo => true;
        public override PlayerRole RequiredRoles => PlayerRole.LeadEngineer | PlayerRole.AgencyDirector;

        public DuplicateTemplateCommand(ITemplateRepository repository, string sourceId, string newId, string newName)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _sourceId = sourceId;
            _newId = newId;
            _newName = newName;
        }

        public override CommandValidationResult CanExecute(CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(_sourceId))
                return CommandValidationResult.Fail("Kaynak şablon ID boş.");
            if (string.IsNullOrWhiteSpace(_newId))
                return CommandValidationResult.Fail("Yeni şablon ID boş.");
            if (!_repository.Contains(_sourceId))
                return CommandValidationResult.Fail($"Kaynak şablon '{_sourceId}' bulunamadı.");
            if (_repository.Contains(_newId))
                return CommandValidationResult.Fail($"ID '{_newId}' zaten kullanımda.");
            return CommandValidationResult.Success();
        }

        public override CommandExecutionResult Execute(CommandContext context)
        {
            _repository.Duplicate(_sourceId, _newId, _newName);
            return CommandExecutionResult.Succeeded();
        }

        public override CommandExecutionResult Undo(CommandContext context)
        {
            _repository.Delete(_newId);
            return CommandExecutionResult.Succeeded();
        }
    }
}
