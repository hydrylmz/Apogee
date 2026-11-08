using System;
using System.Collections.Generic;
using System.Linq;
using Apogee.Core.Attribution;
using Apogee.Data;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Core.ViewModels;

namespace Apogee.UI.Screens.Attribution
{
    /// <summary>
    /// Presentation item for a single attribution entry.
    /// </summary>
    public sealed class AttributionItemViewModel
    {
        public string Id { get; }
        public string Title { get; }
        public string Source { get; }
        public string Description { get; }
        public string Url { get; }
        public string LicenseDisplay { get; }
        public string CategoryDisplay { get; }

        public AttributionItemViewModel(AttributionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            Id = record.Id;
            Title = record.Title ?? record.Id;
            Source = record.Source ?? "";
            Description = record.Description ?? "";
            Url = record.Url ?? "";
            LicenseDisplay = FormatLicense(record.LicenseType, record.LicenseText);
            CategoryDisplay = FormatCategory(record.Category);
        }

        private static string FormatLicense(LicenseType type, string text)
        {
            string label = type switch
            {
                LicenseType.PublicDomain        => "Public Domain",
                LicenseType.CreativeCommonsCC0  => "CC0 1.0",
                LicenseType.CreativeCommonsBY   => "CC-BY",
                LicenseType.MIT                 => "MIT License",
                LicenseType.Apache2             => "Apache 2.0",
                LicenseType.Boost               => "Boost License 1.0",
                LicenseType.Commercial          => "Commercial",
                _                               => "Other"
            };
            return string.IsNullOrWhiteSpace(text) ? label : $"{label} — {text}";
        }

        private static string FormatCategory(AttributionCategory cat)
        {
            return cat switch
            {
                AttributionCategory.NasaData          => "NASA Data",
                AttributionCategory.OpenSourceLibrary  => "Open Source",
                AttributionCategory.AudioAsset         => "Audio",
                AttributionCategory.ArtAsset           => "Art Assets",
                _                                      => "Other"
            };
        }
    }

    /// <summary>
    /// ViewModel for the NASA Attribution Screen (ScreenId.Attribution = 23).
    /// Complies with GDD §17.7 and M8-06 requirements.
    /// </summary>
    public sealed class AttributionViewModel : ViewModelBase
    {
        private readonly AttributionManager _manager;
        private readonly IScreenNavigator _navigator;

        public ObservableValue<IReadOnlyList<AttributionItemViewModel>> Items { get; }
            = new ObservableValue<IReadOnlyList<AttributionItemViewModel>>(
                new List<AttributionItemViewModel>());

        public ObservableValue<IReadOnlyList<string>> CategoryTabs { get; }
            = new ObservableValue<IReadOnlyList<string>>(new List<string>());

        public ObservableValue<string> SelectedCategory { get; }
            = new ObservableValue<string>("All");

        public ObservableValue<AttributionItemViewModel> SelectedItem { get; }
            = new ObservableValue<AttributionItemViewModel>(null);

        public ViewActionCommand CloseCommand { get; }

        public AttributionViewModel(AttributionManager manager, IScreenNavigator navigator)
        {
            _manager   = manager   ?? throw new ArgumentNullException(nameof(manager));
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

            CloseCommand = new ViewActionCommand(() => _navigator.GoBack());

            var tabs = new List<string> { "All" };
            tabs.AddRange(manager.GetCategories().Select(c => FormatCategoryName(c)));
            CategoryTabs.Value = tabs;

            Refresh();
        }

        public void SelectCategory(string category)
        {
            SelectedCategory.Value = category;
            Refresh();
        }

        public void SelectItem(AttributionItemViewModel item)
        {
            SelectedItem.Value = item;
        }

        private void Refresh()
        {
            IReadOnlyList<AttributionRecord> raw;
            string cat = SelectedCategory.Value;

            if (string.IsNullOrEmpty(cat) || cat == "All")
            {
                raw = _manager.AllRecords;
            }
            else
            {
                // Map display name back to enum
                var matchedCat = _manager.GetCategories()
                    .FirstOrDefault(c => FormatCategoryName(c) == cat);
                raw = _manager.GetByCategory(matchedCat);
            }

            var vms = raw.Select(r => new AttributionItemViewModel(r)).ToList();
            Items.Value = vms;

            // Auto-select first
            SelectedItem.Value = vms.Count > 0 ? vms[0] : null;
        }

        private static string FormatCategoryName(AttributionCategory cat) => cat switch
        {
            AttributionCategory.NasaData          => "NASA Data",
            AttributionCategory.OpenSourceLibrary  => "Open Source",
            AttributionCategory.AudioAsset         => "Audio",
            AttributionCategory.ArtAsset           => "Art Assets",
            _                                      => "Other"
        };
    }
}
