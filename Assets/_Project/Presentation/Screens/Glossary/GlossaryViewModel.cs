using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Education;
using Apogee.Data;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Glossary
{
    /// <summary>
    /// Presentation item representation for a single glossary entry.
    /// </summary>
    public sealed class GlossaryItemViewModel
    {
        public string Id { get; }
        public string Term { get; }
        public string PlainTitle { get; }
        public string Category { get; }
        public string Summary { get; }
        public string DetailedExplanation { get; }
        public string WhyItMatters { get; }

        public GlossaryItemViewModel(GlossaryTermRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            Id = record.Id;
            Term = record.Term ?? record.Id;
            PlainTitle = record.PlainTitle ?? "";
            Category = record.Category ?? "Genel";
            Summary = record.Summary ?? "";
            DetailedExplanation = record.DetailedExplanation ?? "";
            WhyItMatters = record.WhyItMatters ?? "";
        }
    }

    /// <summary>
    /// ViewModel for the educational Glossary / Help Screen (ScreenId.Glossary = 17).
    /// Complies with GDD §13.2, §14.1 #18, and §23.
    /// </summary>
    public sealed class GlossaryViewModel : ViewModelBase
    {
        private readonly GlossaryManager _manager;
        private readonly IScreenNavigator _navigator;

        public ObservableValue<string> SearchQuery { get; } = new ObservableValue<string>("");
        public ObservableValue<string> SelectedCategory { get; } = new ObservableValue<string>("All");
        public ObservableValue<IReadOnlyList<string>> Categories { get; } = new ObservableValue<IReadOnlyList<string>>(Array.Empty<string>());
        public ObservableValue<IReadOnlyList<GlossaryItemViewModel>> FilteredTerms { get; } = new ObservableValue<IReadOnlyList<GlossaryItemViewModel>>(Array.Empty<GlossaryItemViewModel>());
        public ObservableValue<GlossaryItemViewModel> SelectedTerm { get; } = new ObservableValue<GlossaryItemViewModel>(null);

        public ViewActionCommand CloseCommand { get; }
        public ViewActionCommand ClearSearchCommand { get; }

        public GlossaryViewModel(GlossaryManager manager, IScreenNavigator navigator = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _navigator = navigator;

            var cats = new List<string> { "All" };
            cats.AddRange(_manager.GetCategories());
            Categories.Value = cats;

            CloseCommand = new ViewActionCommand(() =>
            {
                if (_navigator != null && _navigator.CanGoBack)
                {
                    _navigator.GoBack();
                }
            });

            ClearSearchCommand = new ViewActionCommand(() =>
            {
                SetSearchQuery("");
            });

            RefreshList();
        }

        public void SetSearchQuery(string query)
        {
            SearchQuery.Value = query ?? "";
            RefreshList();
        }

        public void SelectCategory(string category)
        {
            SelectedCategory.Value = string.IsNullOrWhiteSpace(category) ? "All" : category;
            RefreshList();
        }

        public void SelectTerm(GlossaryItemViewModel item)
        {
            SelectedTerm.Value = item;
        }

        public void SelectTermById(string termId)
        {
            if (string.IsNullOrWhiteSpace(termId)) return;
            var record = _manager.GetById(termId);
            if (record != null)
            {
                SelectedTerm.Value = new GlossaryItemViewModel(record);
            }
        }

        private void RefreshList()
        {
            var raw = _manager.Search(SearchQuery.Value, SelectedCategory.Value);
            var viewModels = raw.Select(r => new GlossaryItemViewModel(r)).ToList();
            FilteredTerms.Value = viewModels;

            if (SelectedTerm.Value == null || !viewModels.Any(v => v.Id == SelectedTerm.Value.Id))
            {
                SelectedTerm.Value = viewModels.FirstOrDefault();
            }
        }
    }
}
