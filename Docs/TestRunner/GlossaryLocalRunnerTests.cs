using System;
using System.IO;
using System.Linq;
using Apogee.Core.Education;
using Apogee.Data;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Glossary;

namespace ApogeeLocalTestRunner
{
    public static class GlossaryLocalRunnerTests
    {
        private static string GetJsonDir()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            if (!Directory.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            }
            return path;
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M6_06_Glossary_Catalog_LoadsAllStandardTermsSuccessfully", Test_Glossary_Catalog_LoadsAllStandardTermsSuccessfully);
            runTest("M6_06_Glossary_Manager_SearchAndCategoryFilter",          Test_Glossary_Manager_SearchAndCategoryFilter);
            runTest("M6_06_Glossary_ViewModel_ReactiveSelectionAndSearch",     Test_Glossary_ViewModel_ReactiveSelectionAndSearch);
        }

        private static void Test_Glossary_Catalog_LoadsAllStandardTermsSuccessfully()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "glossary.json"));
            var catalog = CatalogLoader.Load<GlossaryTermRecord>(json);

            AssertTrue(catalog != null, "Glossary catalog not null");
            AssertTrue(catalog.Validation.IsValid, "Catalog JSON is valid");
            AssertTrue(catalog.Count >= 10, "Glossary contains at least 10 items");

            var deltaV = catalog.GetById("term-delta-v");
            AssertTrue(deltaV != null, "Delta-V term exists");
            AssertTrue(deltaV.PlainTitle.Contains("Menzil"), "Delta-V plain title includes 'Menzil'");

            var edl = catalog.GetById("term-edl");
            AssertTrue(edl != null, "EDL term exists");
            AssertTrue(edl.PlainTitle.Contains("İniş"), "EDL plain title includes 'İniş'");
        }

        private static void Test_Glossary_Manager_SearchAndCategoryFilter()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "glossary.json"));
            var catalog = CatalogLoader.Load<GlossaryTermRecord>(json);
            var manager = new GlossaryManager(catalog.All);

            var searchResults = manager.Search("verim");
            AssertTrue(searchResults.Any(t => t.Id == "term-isp"), "Search for 'verim' finds Isp");

            var catResults = manager.Search("", "Fizik & İtki");
            AssertTrue(catResults.All(t => t.Category == "Fizik & İtki"), "Filtered results match category");
            AssertTrue(catResults.Count >= 2, "Category has at least 2 entries");
        }

        private static void Test_Glossary_ViewModel_ReactiveSelectionAndSearch()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "glossary.json"));
            var catalog = CatalogLoader.Load<GlossaryTermRecord>(json);
            var manager = new GlossaryManager(catalog.All);
            var nav = new ScreenNavigator();
            var vm = new GlossaryViewModel(manager, nav);

            AssertTrue(vm.SelectedTerm.Value != null, "Initial selected term populated");
            AssertTrue(vm.Categories.Value.Count >= 3, "Categories list contains multiple categories");

            vm.SetSearchQuery("downlink");
            AssertEqual(1, vm.FilteredTerms.Value.Count, "Filtered terms count for 'downlink'");
            AssertEqual("term-downlink", vm.SelectedTerm.Value.Id, "Selected term matches downlink");

            vm.SelectCategory("All");
            vm.SetSearchQuery("");
            AssertTrue(vm.FilteredTerms.Value.Count >= 10, "Resetting search returns full collection");
        }

        private static void AssertEqual<T>(T expected, T actual, string message = "")
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"Expected '{expected}' but got '{actual}'. {message}");
            }
        }

        private static void AssertTrue(bool condition, string message = "")
        {
            if (!condition) throw new Exception($"Expected condition to be true. {message}");
        }
    }
}
