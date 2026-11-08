using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Apogee.Core.Education;
using Apogee.Data;
using Apogee.UI.Core.Navigation;
using Apogee.UI.Screens.Glossary;

namespace Apogee.Tests
{
    [TestFixture]
    public class GlossaryTests
    {
        private CatalogCollection<GlossaryTermRecord> _catalog;
        private GlossaryManager _manager;

        [SetUp]
        public void Setup()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "_Project", "Data", "Catalog", "Json", "glossary.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "Data", "Catalog", "Json", "glossary.json");
            }

            string json = File.ReadAllText(path);
            _catalog = CatalogLoader.Load<GlossaryTermRecord>(json);
            _manager = new GlossaryManager(_catalog.All);
        }

        [Test]
        public void GlossaryCatalog_LoadsAllStandardTermsSuccessfully()
        {
            Assert.IsNotNull(_catalog);
            Assert.IsTrue(_catalog.Validation.IsValid, "Glossary catalog must be valid JSON");
            Assert.GreaterOrEqual(_catalog.Count, 10, "Glossary must have at least 10 core terms");

            var deltaV = _catalog.GetById("term-delta-v");
            Assert.IsNotNull(deltaV, "Δv term must exist");
            StringAssert.Contains("Menzil", deltaV.PlainTitle);

            var edl = _catalog.GetById("term-edl");
            Assert.IsNotNull(edl, "EDL term must exist");
            StringAssert.Contains("İniş", edl.PlainTitle);
        }

        [Test]
        public void GlossaryManager_SearchAndFilter_WorksAccurately()
        {
            var searchResults = _manager.Search("verim");
            Assert.IsTrue(searchResults.Any(t => t.Id == "term-isp"), "Search for 'verim' should find Isp");

            var catResults = _manager.Search("", "Fizik & İtki");
            Assert.IsTrue(catResults.All(t => t.Category == "Fizik & İtki"), "Filtered results must match category");
            Assert.GreaterOrEqual(catResults.Count, 2);
        }

        [Test]
        public void GlossaryViewModel_ReactiveSelectionAndNavigation()
        {
            var nav = new ScreenNavigator();
            var vm = new GlossaryViewModel(_manager, nav);

            Assert.IsNotNull(vm.SelectedTerm.Value);
            Assert.GreaterOrEqual(vm.Categories.Value.Count, 3);

            vm.SetSearchQuery("downlink");
            Assert.AreEqual(1, vm.FilteredTerms.Value.Count);
            Assert.AreEqual("term-downlink", vm.SelectedTerm.Value.Id);

            vm.SelectCategory("All");
            vm.SetSearchQuery("");
            Assert.GreaterOrEqual(vm.FilteredTerms.Value.Count, 10);
        }
    }
}
