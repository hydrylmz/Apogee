using System;
using System.IO;
using System.Linq;
using Apogee.Core.Attribution;
using Apogee.Data;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Attribution;

namespace ApogeeLocalTestRunner
{
    public static class AttributionRunnerTests
    {
        private static string GetJsonDir()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json");
            if (!Directory.Exists(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "_Project", "Data", "Catalog", "Json");
            return path;
        }

        public static void Run(Action<string, Action> runTest)
        {
            runTest("M8_06_Attribution_Catalog_LoadsAllRecords",              Test_Catalog_LoadsAllRecords);
            runTest("M8_06_Attribution_Catalog_HasNasaSources",               Test_Catalog_HasNasaSources);
            runTest("M8_06_Attribution_Catalog_HasOpenSourceLibraries",       Test_Catalog_HasOpenSourceLibraries);
            runTest("M8_06_Attribution_Manager_GetById",                      Test_Manager_GetById);
            runTest("M8_06_Attribution_Manager_GetCategories",                Test_Manager_GetCategories);
            runTest("M8_06_Attribution_Manager_GetByCategory",                Test_Manager_GetByCategory);
            runTest("M8_06_Attribution_Manager_Search",                       Test_Manager_Search);
            runTest("M8_06_Attribution_ViewModel_InitialState",               Test_ViewModel_InitialState);
            runTest("M8_06_Attribution_ViewModel_SelectCategory_Filter",      Test_ViewModel_SelectCategory_Filter);
            runTest("M8_06_Attribution_Record_Fields_NonNull",                Test_Record_Fields_NonNull);
        }

        private static CatalogCollection<AttributionRecord> LoadCatalog()
        {
            string json = File.ReadAllText(Path.Combine(GetJsonDir(), "attribution_data.json"));
            return CatalogLoader.Load<AttributionRecord>(json);
        }

        private static void Test_Catalog_LoadsAllRecords()
        {
            var catalog = LoadCatalog();
            AssertTrue(catalog != null, "Catalog not null");
            AssertTrue(catalog.Validation.IsValid, string.Join("; ", catalog.Validation.Errors));
            AssertTrue(catalog.Count >= 10, $"At least 10 attribution records (got {catalog.Count})");
        }

        private static void Test_Catalog_HasNasaSources()
        {
            var catalog = LoadCatalog();
            var nasa = catalog.All.Where(r => r.Category == AttributionCategory.NasaData).ToList();
            AssertTrue(nasa.Count >= 4, $"At least 4 NASA sources (got {nasa.Count})");

            var spaceApps = catalog.GetById("attr-nasa-space-apps");
            AssertTrue(spaceApps != null, "NASA Space Apps entry exists");
            AssertTrue(spaceApps.LicenseType == LicenseType.PublicDomain, "Space Apps entry is Public Domain");
        }

        private static void Test_Catalog_HasOpenSourceLibraries()
        {
            var catalog = LoadCatalog();
            var oss = catalog.All.Where(r => r.Category == AttributionCategory.OpenSourceLibrary).ToList();
            AssertTrue(oss.Count >= 2, $"At least 2 open-source libraries (got {oss.Count})");

            var xoshiro = catalog.GetById("attr-xoshiro-rng");
            AssertTrue(xoshiro != null, "xoshiro256** entry exists");
            AssertTrue(xoshiro.LicenseType == LicenseType.PublicDomain, "xoshiro is Public Domain");
            AssertTrue(xoshiro.UsedInSystems.Length >= 1, "xoshiro lists used systems");
        }

        private static void Test_Manager_GetById()
        {
            var catalog = LoadCatalog();
            var mgr = new AttributionManager(catalog.All);

            var rec = mgr.GetById("attr-nasa-planetary-factsheet");
            AssertTrue(rec != null, "GetById returns existing record");
            AssertTrue(rec.Title.Contains("Planetary Fact Sheet"), "Title matches");

            AssertTrue(mgr.GetById("does-not-exist") == null, "GetById returns null for missing id");
            AssertTrue(mgr.GetById(null) == null,             "GetById handles null safely");
            AssertTrue(mgr.GetById("") == null,               "GetById handles empty string safely");
        }

        private static void Test_Manager_GetCategories()
        {
            var catalog = LoadCatalog();
            var mgr = new AttributionManager(catalog.All);

            var cats = mgr.GetCategories();
            AssertTrue(cats.Count >= 2, $"At least 2 categories (got {cats.Count})");
            AssertTrue(cats.Contains(AttributionCategory.NasaData),         "NasaData category present");
            AssertTrue(cats.Contains(AttributionCategory.OpenSourceLibrary), "OpenSourceLibrary category present");
        }

        private static void Test_Manager_GetByCategory()
        {
            var catalog = LoadCatalog();
            var mgr = new AttributionManager(catalog.All);

            var nasa = mgr.GetNasaSources();
            AssertTrue(nasa.Count >= 4, "GetNasaSources returns >= 4");
            AssertTrue(nasa.All(r => r.Category == AttributionCategory.NasaData), "All are NasaData");

            var oss = mgr.GetByCategory(AttributionCategory.OpenSourceLibrary);
            AssertTrue(oss.Count >= 2, "GetByCategory OpenSource returns >= 2");
        }

        private static void Test_Manager_Search()
        {
            var catalog = LoadCatalog();
            var mgr = new AttributionManager(catalog.All);

            var results = mgr.Search("JPL");
            AssertTrue(results.Any(r => r.Id == "attr-nasa-launch-windows"), "Search 'JPL' finds launch windows entry");

            var empty = mgr.Search("");
            AssertEqual(mgr.AllRecords.Count, empty.Count, "Empty search returns all records");

            var noResult = mgr.Search("XXXXXXXXXXXXXXXXXXXXXXX");
            AssertEqual(0, noResult.Count, "No results for nonsense query");
        }

        private static void Test_ViewModel_InitialState()
        {
            var catalog = LoadCatalog();
            var mgr = new AttributionManager(catalog.All);
            var nav = new ScreenNavigator();
            var vm  = new AttributionViewModel(mgr, nav);

            AssertTrue(vm.Items.Value.Count >= 10,      "Items populated on init");
            AssertTrue(vm.CategoryTabs.Value.Count >= 3, "Category tabs include All + categories");
            AssertTrue(vm.CategoryTabs.Value[0] == "All", "First tab is 'All'");
            AssertTrue(vm.SelectedItem.Value != null,   "First item auto-selected");
        }

        private static void Test_ViewModel_SelectCategory_Filter()
        {
            var catalog = LoadCatalog();
            var mgr = new AttributionManager(catalog.All);
            var nav = new ScreenNavigator();
            var vm  = new AttributionViewModel(mgr, nav);

            vm.SelectCategory("NASA Data");
            AssertTrue(vm.Items.Value.Count >= 4, "NASA Data tab shows >= 4 items");

            vm.SelectCategory("All");
            AssertEqual(mgr.AllRecords.Count, vm.Items.Value.Count, "All tab restores full list");
        }

        private static void Test_Record_Fields_NonNull()
        {
            var catalog = LoadCatalog();
            foreach (var rec in catalog.All)
            {
                AssertTrue(!string.IsNullOrWhiteSpace(rec.Id),    $"Record has Id: {rec}");
                AssertTrue(!string.IsNullOrWhiteSpace(rec.Title), $"Record has Title: {rec.Id}");
                AssertTrue(rec.UsedInSystems != null,             $"UsedInSystems not null: {rec.Id}");
            }
        }

        // ── Assertion helpers ─────────────────────────────────────────────────

        private static void AssertEqual<T>(T expected, T actual, string message = "")
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Expected '{expected}' but got '{actual}'. {message}");
        }

        private static void AssertTrue(bool condition, string message = "")
        {
            if (!condition) throw new Exception($"Expected condition to be true. {message}");
        }
    }
}
