using System.Collections.Generic;
using NUnit.Framework;
using Apogee.Data;

namespace Apogee.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="CatalogLoader"/>, <see cref="CatalogCollection{T}"/>,
    /// and the concrete record types (<see cref="ModuleRecord"/>, <see cref="DestinationRecord"/>,
    /// <see cref="SupplierRecord"/>).
    ///
    /// GDD §21 M1-05 Kabul kriteri:
    ///   "Bozuk veri anlaşılır hatayla reddedilir."
    /// GDD §20.5: "Birim testleri yazıldı ve geçiyor" → önkoşul.
    ///
    /// These tests use only string-based JSON inputs — no disk I/O, no Unity TextAsset —
    /// so they run entirely in EditMode without MonoBehaviour scaffolding.
    /// </summary>
    [TestFixture]
    public class CatalogLoaderTests
    {
        // ─── Helper: minimal valid records ────────────────────────────────────────

        private const string ValidModuleJson = @"
        {
          ""modules"": [
            { ""id"": ""engine-kerosene-mk1"", ""category"": ""engine"", ""dryMassTons"": 2.5, ""isp"": 311 },
            { ""id"": ""tank-kerosene-small"", ""category"": ""tank"",   ""dryMassTons"": 1.0 }
          ]
        }";

        private const string ValidBareArrayJson = @"
        [
          { ""id"": ""engine-kerosene-mk1"", ""category"": ""engine"" },
          { ""id"": ""tank-kerosene-small"", ""category"": ""tank"" }
        ]";

        private const string ValidDestinationJson = @"
        {
          ""destinations"": [
            {
              ""id"": ""leo"",
              ""bodyType"": ""orbit"",
              ""dvAscent"": 9400,
              ""solarFluxMultiplier"": 1.0
            },
            {
              ""id"": ""mars"",
              ""bodyType"": ""body"",
              ""dvAscent"": 9400,
              ""dvTransfer"": 2700,
              ""dvArrival"": 900,
              ""dvLanding"": 600,
              ""solarFluxMultiplier"": 0.43
            }
          ]
        }";

        // ─── Happy path: valid JSON ───────────────────────────────────────────────

        [Test]
        public void Load_ValidWrappedObject_ReturnsValidCollection()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);

            Assert.IsTrue(result.Validation.IsValid,
                "Valid JSON should produce a valid collection. Errors: " +
                string.Join("; ", result.Validation.Errors));
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Load_ValidBareArray_ReturnsValidCollection()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidBareArrayJson);

            Assert.IsTrue(result.Validation.IsValid,
                "Bare array JSON should be accepted. Errors: " +
                string.Join("; ", result.Validation.Errors));
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Load_ValidDestinations_CorrectCount()
        {
            var result = CatalogLoader.Load<DestinationRecord>(ValidDestinationJson);

            Assert.IsTrue(result.Validation.IsValid);
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Load_ModuleRecord_DeserializesFields()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);
            var engine = result.GetById("engine-kerosene-mk1");

            Assert.IsNotNull(engine);
            Assert.AreEqual("engine-kerosene-mk1", engine.Id);
            Assert.AreEqual("engine", engine.Category);
            Assert.AreEqual(2.5, engine.DryMassTons, 1e-9);
            Assert.AreEqual(311.0, engine.Isp, 1e-9);
        }

        [Test]
        public void Load_DestinationRecord_DvTotalComputed()
        {
            var result = CatalogLoader.Load<DestinationRecord>(ValidDestinationJson);
            var mars = result.GetById("mars");

            Assert.IsNotNull(mars);
            // dvAscent(9400) + dvTransfer(2700) + dvArrival(900) + dvLanding(600) = 13600
            Assert.AreEqual(13600.0, mars.DvTotal, 1e-9);
        }

        // ─── Lookup ───────────────────────────────────────────────────────────────

        [Test]
        public void GetById_ExistingId_ReturnsRecord()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);
            var rec = result.GetById("tank-kerosene-small");
            Assert.IsNotNull(rec);
            Assert.AreEqual("tank-kerosene-small", rec.Id);
        }

        [Test]
        public void GetById_UnknownId_ReturnsNull()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);
            Assert.IsNull(result.GetById("does-not-exist"));
        }

        [Test]
        public void GetById_NullId_ReturnsNull()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);
            Assert.IsNull(result.GetById(null));
        }

        [Test]
        public void TryGetById_ExistingId_ReturnsTrueAndRecord()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);
            bool found = result.TryGetById("engine-kerosene-mk1", out var rec);
            Assert.IsTrue(found);
            Assert.IsNotNull(rec);
        }

        [Test]
        public void TryGetById_UnknownId_ReturnsFalseAndNull()
        {
            var result = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);
            bool found = result.TryGetById("ghost", out var rec);
            Assert.IsFalse(found);
            Assert.IsNull(rec);
        }

        // ─── Error: null / empty JSON ─────────────────────────────────────────────

        [Test]
        public void Load_NullJson_ReportsError()
        {
            var result = CatalogLoader.Load<ModuleRecord>(null);

            Assert.IsFalse(result.Validation.IsValid);
            Assert.AreEqual(0, result.Count);
            StringAssert.Contains("null or empty", result.Validation.Errors[0]);
        }

        [Test]
        public void Load_EmptyJson_ReportsError()
        {
            var result = CatalogLoader.Load<ModuleRecord>("   ");

            Assert.IsFalse(result.Validation.IsValid);
            Assert.AreEqual(0, result.Count);
        }

        // ─── Error: malformed JSON ────────────────────────────────────────────────

        [Test]
        public void Load_BrokenJson_ReportsParseError()
        {
            var result = CatalogLoader.Load<ModuleRecord>("{ broken json {{{{ ]]]");

            Assert.IsFalse(result.Validation.IsValid);
            Assert.AreEqual(0, result.Count);
            Assert.IsTrue(result.Validation.Errors[0].StartsWith("JSON parse error"));
        }

        [Test]
        public void Load_JsonScalarRoot_ReportsError()
        {
            var result = CatalogLoader.Load<ModuleRecord>("42");

            Assert.IsFalse(result.Validation.IsValid);
            StringAssert.Contains("Unexpected JSON root type", result.Validation.Errors[0]);
        }

        [Test]
        public void Load_ObjectWithNoArray_ReportsError()
        {
            var result = CatalogLoader.Load<ModuleRecord>(@"{ ""foo"": ""bar"" }");

            Assert.IsFalse(result.Validation.IsValid);
            StringAssert.Contains("no array property", result.Validation.Errors[0]);
        }

        // ─── Error: missing / empty id ────────────────────────────────────────────

        [Test]
        public void Load_NullIdField_ReportsError()
        {
            const string json = @"[ { ""id"": null, ""category"": ""engine"" } ]";
            var result = CatalogLoader.Load<ModuleRecord>(json);

            Assert.IsFalse(result.Validation.IsValid);
            Assert.IsTrue(result.Validation.Errors.Count >= 1);
            StringAssert.Contains("null or empty 'id'", result.Validation.Errors[0]);
        }

        [Test]
        public void Load_EmptyStringId_ReportsError()
        {
            const string json = @"[ { ""id"": """", ""category"": ""engine"" } ]";
            var result = CatalogLoader.Load<ModuleRecord>(json);

            Assert.IsFalse(result.Validation.IsValid);
            StringAssert.Contains("null or empty 'id'", result.Validation.Errors[0]);
        }

        [Test]
        public void Load_WhitespaceId_ReportsError()
        {
            const string json = @"[ { ""id"": ""   "", ""category"": ""engine"" } ]";
            var result = CatalogLoader.Load<ModuleRecord>(json);

            Assert.IsFalse(result.Validation.IsValid);
        }

        // ─── Error: duplicate id ──────────────────────────────────────────────────

        [Test]
        public void Load_DuplicateIds_ReportsError()
        {
            const string json = @"
            [
              { ""id"": ""same-id"", ""category"": ""engine"" },
              { ""id"": ""same-id"", ""category"": ""tank""   }
            ]";
            var result = CatalogLoader.Load<ModuleRecord>(json);

            Assert.IsFalse(result.Validation.IsValid);
            StringAssert.Contains("Duplicate id", result.Validation.Errors[0]);
            StringAssert.Contains("same-id", result.Validation.Errors[0]);
        }

        [Test]
        public void Load_DuplicateIds_StillLoadsFirstRecord()
        {
            // Even with errors, the collection should contain the first valid copy.
            const string json = @"
            [
              { ""id"": ""dup"", ""category"": ""engine"" },
              { ""id"": ""dup"", ""category"": ""tank""   }
            ]";
            var result = CatalogLoader.Load<ModuleRecord>(json);

            // Lookup should return the first one (engine).
            var rec = result.GetById("dup");
            Assert.IsNotNull(rec);
            Assert.AreEqual("engine", rec.Category);
        }

        // ─── Mixed valid + invalid ────────────────────────────────────────────────

        [Test]
        public void Load_MixedValidAndInvalidRecords_ErrorsListedSeparately()
        {
            const string json = @"
            [
              { ""id"": ""valid-one"",  ""category"": ""engine"" },
              { ""id"": """",            ""category"": ""tank""   },
              { ""id"": ""valid-two"",  ""category"": ""bus""    }
            ]";
            var result = CatalogLoader.Load<ModuleRecord>(json);

            Assert.IsFalse(result.Validation.IsValid);
            Assert.AreEqual(1, result.Validation.Errors.Count, "Exactly one error (the empty id).");
            // Valid records are still reachable.
            Assert.IsNotNull(result.GetById("valid-one"));
            Assert.IsNotNull(result.GetById("valid-two"));
        }

        // ─── Edge cases ───────────────────────────────────────────────────────────

        [Test]
        public void Load_EmptyArray_ReturnsValidEmptyCollection()
        {
            var result = CatalogLoader.Load<ModuleRecord>("[]");

            Assert.IsTrue(result.Validation.IsValid,
                "An empty array is valid — the catalog may have no entries yet.");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Load_WrappedEmptyArray_IsValid()
        {
            var result = CatalogLoader.Load<ModuleRecord>(@"{ ""modules"": [] }");
            Assert.IsTrue(result.Validation.IsValid);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Load_ExtraUnknownFields_AreIgnoredNoError()
        {
            // MissingMemberHandling.Ignore — extra fields should not cause errors.
            const string json = @"
            [
              { ""id"": ""eng-1"", ""category"": ""engine"", ""unknownFutureProp"": 99 }
            ]";
            var result = CatalogLoader.Load<ModuleRecord>(json);

            Assert.IsTrue(result.Validation.IsValid,
                "Unknown fields should be silently ignored per GDD §0.1 forward-compat.");
            Assert.AreEqual(1, result.Count);
        }

        // ─── Validate() standalone ────────────────────────────────────────────────

        [Test]
        public void Validate_ListWithNoDuplicates_ReturnsValid()
        {
            var records = new List<ModuleRecord>
            {
                // We can't easily construct ModuleRecord (private setters + Newtonsoft),
                // so we test via the loader round-trip.
            };
            // Use loader to produce records, then validate separately.
            var collection = CatalogLoader.Load<ModuleRecord>(ValidModuleJson);
            var validation = CatalogLoader.Validate<ModuleRecord>(collection.All);
            Assert.IsTrue(validation.IsValid);
        }

        [Test]
        public void Validate_NullList_ReturnsError()
        {
            var validation = CatalogLoader.Validate<ModuleRecord>(null);
            Assert.IsFalse(validation.IsValid);
        }

        // ─── Supplier record ──────────────────────────────────────────────────────

        [Test]
        public void Load_SupplierJson_DeserializesCategoryMultipliers()
        {
            const string json = @"
            {
              ""suppliers"": [
                {
                  ""id"": ""helion-dynamics"",
                  ""relationshipStart"": 0.5,
                  ""costMultiplierByCategory"": { ""engine"": 0.80, ""tank"": 0.85 },
                  ""strengthTags"": [""low-cost""]
                }
              ]
            }";
            var result = CatalogLoader.Load<SupplierRecord>(json);

            Assert.IsTrue(result.Validation.IsValid);
            var supplier = result.GetById("helion-dynamics");
            Assert.IsNotNull(supplier);
            Assert.AreEqual(0.5, supplier.RelationshipStart, 1e-9);
            Assert.AreEqual(0.80, supplier.CostMultiplierByCategory["engine"], 1e-9);
            Assert.AreEqual(1, supplier.StrengthTags.Count);
        }

        // ─── CatalogValidationResult ──────────────────────────────────────────────

        [Test]
        public void CatalogValidationResult_Ok_IsValid()
        {
            Assert.IsTrue(CatalogValidationResult.Ok.IsValid);
            Assert.AreEqual(0, CatalogValidationResult.Ok.Errors.Count);
        }
    }
}
