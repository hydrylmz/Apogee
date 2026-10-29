using System.Collections.Generic;
using Apogee.Core;
using Apogee.Design;
using Apogee.Design.Commands;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;
using Apogee.UI.Screens.Comparison;

namespace Apogee.UI.Screens.Templates
{
    /// <summary>
    /// ViewModel for the template management panel within the Workshop (GDD §7.5).
    /// Handles save/duplicate/rename/delete/compare operations via CommandBus.
    /// </summary>
    public sealed class TemplateManagerViewModel : ViewModelBase
    {
        private readonly ITemplateRepository _repository;
        private readonly CommandBus _commandBus;
        private readonly IScreenNavigator _navigator;

        public ObservableValue<IReadOnlyList<TemplateListItemViewModel>> Templates { get; } =
            new(System.Array.Empty<TemplateListItemViewModel>());

        public ObservableValue<TemplateListItemViewModel> SelectedTemplate { get; } = new(null);
        public ObservableValue<string> MissionCode { get; } = new(string.Empty);
        public ObservableValue<string> DecodeError { get; } = new(string.Empty);
        public ObservableValue<bool> HasTemplates { get; } = new(false);

        public ViewActionCommand SaveCurrentCommand { get; }
        public ViewActionCommand DeleteSelectedCommand { get; }
        public ViewActionCommand DuplicateSelectedCommand { get; }
        public ViewActionCommand CompareWithCurrentCommand { get; }
        public ViewActionCommand LoadSelectedCommand { get; }
        public ViewActionCommand ExportCodeCommand { get; }

        // Callbacks injected from the WorkshopViewModel layer
        public System.Func<Blueprint> GetActiveBlueprint { get; set; }
        public System.Action<Blueprint> LoadBlueprintCallback { get; set; }
        public System.Func<string> GenerateNewTemplateId { get; set; }

        public TemplateManagerViewModel(
            ITemplateRepository repository,
            CommandBus commandBus,
            IScreenNavigator navigator = null)
        {
            _repository = repository;
            _commandBus = commandBus;
            _navigator = navigator;

            _repository.OnTemplatesChanged += RefreshList;

            SaveCurrentCommand = new ViewActionCommand(ExecuteSave);
            DeleteSelectedCommand = new ViewActionCommand(ExecuteDelete, () => SelectedTemplate.Value != null);
            DuplicateSelectedCommand = new ViewActionCommand(ExecuteDuplicate, () => SelectedTemplate.Value != null);
            CompareWithCurrentCommand = new ViewActionCommand(ExecuteCompare, () => SelectedTemplate.Value != null);
            LoadSelectedCommand = new ViewActionCommand(ExecuteLoad, () => SelectedTemplate.Value != null);
            ExportCodeCommand = new ViewActionCommand(ExecuteExportCode, () => GetActiveBlueprint?.Invoke() != null);

            RefreshList();
        }

        public void SelectTemplate(string id)
        {
            var items = Templates.Value;
            TemplateListItemViewModel found = null;
            foreach (var item in items)
            {
                if (item.TemplateId == id)
                {
                    found = item;
                    break;
                }
            }

            SelectedTemplate.Value = found;
            RaiseSelectionDependentCommands();
        }

        private void RefreshList()
        {
            var all = _repository.GetAll();
            var items = new List<TemplateListItemViewModel>();
            foreach (var t in all)
            {
                items.Add(new TemplateListItemViewModel(t, () => SelectTemplate(t.Id)));
            }

            Templates.Value = items;
            HasTemplates.Value = items.Count > 0;

            // Deselect if the previously selected item is gone
            var sel = SelectedTemplate.Value;
            if (sel != null && !_repository.Contains(sel.TemplateId))
            {
                SelectedTemplate.Value = null;
            }

            RaiseSelectionDependentCommands();
        }

        private void RaiseSelectionDependentCommands()
        {
            DeleteSelectedCommand.RaiseCanExecuteChanged();
            DuplicateSelectedCommand.RaiseCanExecuteChanged();
            CompareWithCurrentCommand.RaiseCanExecuteChanged();
            LoadSelectedCommand.RaiseCanExecuteChanged();
            ExportCodeCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteSave()
        {
            var blueprint = GetActiveBlueprint?.Invoke();
            if (blueprint == null) return;

            string newId = GenerateNewTemplateId?.Invoke() ??
                           $"template-{System.DateTime.UtcNow.Ticks}";

            var template = new DesignTemplate(
                id: newId,
                name: blueprint.Name,
                blueprint: blueprint,
                description: $"Kaydedilen tasarım: {blueprint.TargetDestinationId ?? "?"} / {blueprint.TargetMissionTypeId ?? "?"}",
                createdAtTicks: System.DateTime.UtcNow.Ticks
            );

            var command = new SaveTemplateCommand(_repository, template);
            var context = CommandContext.CreateLocalAdmin(null);
            _commandBus.Execute(command, context);
        }

        private void ExecuteDelete()
        {
            var sel = SelectedTemplate.Value;
            if (sel == null) return;

            var command = new DeleteTemplateCommand(_repository, sel.TemplateId);
            var context = CommandContext.CreateLocalAdmin(null);
            _commandBus.Execute(command, context);
        }

        private void ExecuteDuplicate()
        {
            var sel = SelectedTemplate.Value;
            if (sel == null) return;

            string newId = $"{sel.TemplateId}-kopya-{System.DateTime.UtcNow.Ticks % 10000}";
            string newName = $"{sel.Name} (Kopya)";

            var command = new DuplicateTemplateCommand(_repository, sel.TemplateId, newId, newName);
            var context = CommandContext.CreateLocalAdmin(null);
            _commandBus.Execute(command, context);
        }

        private void ExecuteCompare()
        {
            var sel = SelectedTemplate.Value;
            if (sel == null) return;

            var current = GetActiveBlueprint?.Invoke();
            if (current == null) return;

            var template = _repository.GetById(sel.TemplateId);
            if (template == null) return;

            var args = new ComparisonNavigationArgs(
                blueprintA: current,
                blueprintB: template.Blueprint,
                labelA: current.Name,
                labelB: template.Name
            );

            _navigator?.GoTo(ScreenId.Comparison, args);
        }

        private void ExecuteLoad()
        {
            var sel = SelectedTemplate.Value;
            if (sel == null) return;

            var template = _repository.GetById(sel.TemplateId);
            if (template == null) return;

            LoadBlueprintCallback?.Invoke(template.Blueprint);
        }

        private void ExecuteExportCode()
        {
            var blueprint = GetActiveBlueprint?.Invoke();
            if (blueprint == null) return;

            string code = MissionCodeCodec.Encode(blueprint);
            MissionCode.Value = code;
        }

        public bool TryImportCode(string code)
        {
            DecodeError.Value = string.Empty;
            if (MissionCodeCodec.TryDecode(code, out var data, out var error))
            {
                MissionCode.Value = code;
                return true;
            }

            DecodeError.Value = error ?? "Geçersiz kod.";
            return false;
        }

        public override void Dispose()
        {
            _repository.OnTemplatesChanged -= RefreshList;
            base.Dispose();
        }
    }

    /// <summary>
    /// Lightweight list item ViewModel for a single DesignTemplate in the template list.
    /// </summary>
    public sealed class TemplateListItemViewModel : ViewModelBase
    {
        public string TemplateId { get; }
        public string Name { get; }
        public string Description { get; }
        public ObservableValue<bool> IsSelected { get; } = new(false);

        private readonly System.Action _onSelect;

        public ViewActionCommand SelectCommand { get; }

        public TemplateListItemViewModel(DesignTemplate template, System.Action onSelect)
        {
            TemplateId = template.Id;
            Name = template.Name;
            Description = template.Description;
            _onSelect = onSelect;
            SelectCommand = new ViewActionCommand(() => _onSelect?.Invoke());
        }
    }
}
